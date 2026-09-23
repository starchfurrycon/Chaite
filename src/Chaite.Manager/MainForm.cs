using Chaite.Patcher;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chaite.Manager
{
    /// <summary>
    /// The console the owner actually sees.
    ///
    /// It shows three things and nothing else: a quiet title, the Boss column
    /// with its selectable loadouts, and the two hotkeys. Everything it used to
    /// print -- hash prefixes, version banners, install-state essays, the
    /// acceptance wording, the route reasoning, the sentences explaining what
    /// the mod does with the save -- was text the owner never has to act on. The
    /// owner's rule is blunt: if the user does not need to know it, it does not
    /// go on screen. What is left is a few words of state and the button labels.
    ///
    /// The window has no frame of its own (FormBorderStyle.None), so it draws no
    /// window rectangle either: the top of the window is the full width gradient
    /// rule under the title, and there is no vertical edge anywhere. Because
    /// there is no system frame, moving and resizing the window and closing it
    /// are implemented here.
    ///
    /// All art is the game's own, extracted from the owner's installation at run
    /// time by SpriteStore. Nothing Re-Logic made ships in this repository.
    /// </summary>
    internal sealed class MainForm : Form
    {
        // Non-client hit test. The window keeps its own client strip at every
        // edge -- see Padding below -- so these codes reach the frame even though
        // the shell covers the middle of the window.
        private const int WM_NCHITTEST = 0x0084;
        internal const int HTCLIENT = 1;
        internal const int HTLEFT = 10;
        internal const int HTRIGHT = 11;
        internal const int HTTOP = 12;
        internal const int HTTOPLEFT = 13;
        internal const int HTTOPRIGHT = 14;
        internal const int HTBOTTOM = 15;
        internal const int HTBOTTOMLEFT = 16;
        internal const int HTBOTTOMRIGHT = 17;
        /// <summary>Width of the window's own resize strip, at 96 DPI.</summary>
        private const int GripLogical = 6;

        private readonly InstallationService _installer = new InstallationService();
        private readonly bool _previewOnly;
        private readonly Timer _pathDebounce = new Timer { Interval = 550 };
        private readonly ToolTip _tooltip = new ToolTip
        {
            InitialDelay = 240,
            ReshowDelay = 80,
            AutoPopDelay = 9000,
            ShowAlways = true
        };

        private readonly Label _status = UiTheme.Label("正在检查游戏…", 10.5f, true);
        private readonly Label _statusDetail = UiTheme.Paragraph(string.Empty);
        private readonly Label _memeLine = UiTheme.Paragraph(string.Empty,
            UiTheme.Muted);
        private readonly UiTheme.CloseButton _close = new UiTheme.CloseButton();
        private readonly FlowLayoutPanel _memeStrip = new FlowLayoutPanel();
        private readonly Panel _columns = new Panel();
        private readonly Panel _body = new Panel();
        private readonly Button _pick = UiTheme.Button("选择游戏");
        private readonly Button _inspect = UiTheme.Button("重新检查");
        private readonly Button _install = UiTheme.Button("安装拆特", true);
        private readonly Button _restore = UiTheme.Button("恢复原版");
        private readonly Button _audio = UiTheme.Button("音频槽位");
        private readonly Button _memes = UiTheme.Button("梗图槽位");
        private readonly Button _config = UiTheme.Button("打开配置");
        private readonly List<LoadoutCard> _cards = new List<LoadoutCard>();

        private InstallStatus _lastStatus;
        private bool _inspectionRunning;
        private bool _pendingInspection;
        private bool _busy;
        private string _checkedPath;
        private string _manualPath;
        private LoadoutCard _selected;
        private int _memeCounter;
        private Control _dragSource;
        private Point _dragCursorOrigin;
        private Point _dragWindowOrigin;
        private int _resizeEdge;
        private Point _resizeCursorOrigin;
        private Rectangle _resizeBounds;

        internal MainForm(bool previewOnly = false)
        {
            _previewOnly = previewOnly;
            SuspendLayout();
            Text = "拆特";
            // Through FontBook, not a literal: the console carries its own
            // faces, and a second hard-coded family here is how the header and
            // the body drift apart.
            Font = FontBook.For(string.Empty, 10f, false);
            BackColor = UiTheme.Canvas;
            ForeColor = UiTheme.Text;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            // No system frame, no system title bar, no system close button: the
            // window draws no rectangle, and its chrome is the gradient rule
            // under the title plus one text close control.
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(1000, 760);
            MinimumSize = new Size(840, 580);
            // The shell is docked inside this padding, which leaves a strip of
            // the window's own client area at each edge. That strip is what the
            // resize hit test finds; it is painted canvas and is never drawn as
            // a border, so the window still shows no frame.
            Padding = new Padding(GripLogical);
            StartPosition = previewOnly
                ? FormStartPosition.Manual
                : FormStartPosition.CenterScreen;
            if (previewOnly)
            {
                Location = new Point(-30000, -30000);
                ShowInTaskbar = false;
            }

            var shell = UiTheme.Table(1);
            shell.Dock = DockStyle.Fill;
            shell.AutoSize = false;
            shell.RowCount = 4;
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.Controls.Add(BuildHeader(), 0, 0);
            shell.Controls.Add(BuildToolbar(), 0, 1);
            var body = _body;
            body.Dock = DockStyle.Fill;
            body.AutoScroll = true;
            body.Padding = new Padding(26, 2, 26, 16);
            body.BackColor = UiTheme.Canvas;
            _columns.Dock = DockStyle.Top;
            _columns.AutoSize = true;
            _columns.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _columns.BackColor = Color.Transparent;
            _columns.Height = 0;
            body.Controls.Add(_columns);
            shell.Controls.Add(body, 0, 2);
            shell.Controls.Add(BuildFooter(), 0, 3);
            Controls.Add(shell);

            _pathDebounce.Tick += async (_, __) =>
            {
                _pathDebounce.Stop();
                await InspectAsync();
            };
            _pick.Click += (_, __) => PickGame();
            _inspect.Click += async (_, __) =>
            {
                _pathDebounce.Stop();
                await InspectAsync();
            };
            _install.Click += async (_, __) => await ChangeInstallationAsync(false);
            _restore.Click += async (_, __) => await ChangeInstallationAsync(true);
            _audio.Click += (_, __) => OpenData("Audio");
            _memes.Click += (_, __) => OpenData(MemeSlot.FolderName);
            _config.Click += (_, __) => OpenData("config.json");
            FormClosing += (_, e) =>
            {
                if (!_busy) return;
                e.Cancel = true;
                _statusDetail.Text = "正在写入…";
            };
            if (!previewOnly)
            {
                Shown += async (_, __) =>
                {
                    _pathDebounce.Stop();
                    await InspectAsync();
                };
            }
            UpdateButtons();
            ResumeLayout(true);
        }

        protected override bool ShowWithoutActivation
        {
            get { return _previewOnly || base.ShowWithoutActivation; }
        }

        protected override void OnShown(EventArgs e)
        {
            if (!_previewOnly)
            {
                var area = Screen.FromControl(this).WorkingArea;
                var available = new Size(Math.Max(640, area.Width - 24),
                    Math.Max(480, area.Height - 24));
                MinimumSize = new Size(
                    Math.Min(MinimumSize.Width, available.Width),
                    Math.Min(MinimumSize.Height, available.Height));
                Size = new Size(Math.Min(Width, available.Width),
                    Math.Min(Height, available.Height));
                Location = new Point(area.Left + (area.Width - Width) / 2,
                    area.Top + (area.Height - Height) / 2);
            }
            base.OnShown(e);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var value = base.CreateParams;
                if (_previewOnly) value.ExStyle |= 0x08000000 | 0x00000080;
                return value;
            }
        }

        private Control BuildHeader()
        {
            var header = UiTheme.Table(1);
            var row = UiTheme.Table(3);
            row.Padding = new Padding(26, 14, 20, 6);
            row.ColumnStyles.Clear();
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            // A quiet title, not a colour band: small, body ink, and the one
            // full width gradient rule underneath carries the identity instead.
            var brand = UiTheme.Table(1);
            var title = UiTheme.Label("拆特", 13f, true);
            title.Name = "BrandTitle";
            brand.Controls.Add(title);
            _memeLine.Name = "MemeLine";
            // The joke line alternates between an all-Latin caption (Plex) and a
            // mixed one (Noto), whose line boxes differ. Without a floor the
            // header -- and with it every rule in the body below it -- moved 4px
            // whenever the line rotated: measured, the rules above the footer sat
            // at y=113/264/334/402/470/538 for a mixed caption and
            // y=109/260/330/398/466/534 for an all-Latin one. The floor is the
            // taller of the two faces, so the box is the same either way.
            _memeLine.MinimumSize = new Size(0,
                FontBook.LineHeight(UiTheme.ParagraphPoints));
            _memeLine.Text = MemeVoice.Line(MemeMoment.Boot, 0);
            brand.Controls.Add(_memeLine);
            row.Controls.Add(brand, 0, 0);

            _status.TextAlign = ContentAlignment.MiddleRight;
            _status.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _status.Name = "CompatibilityStatus";
            _status.Margin = new Padding(12, 0, 0, 0);
            row.Controls.Add(_status, 1, 0);

            _close.Name = "CloseAction";
            _close.Margin = new Padding(16, 0, 0, 0);
            _close.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            _close.Click += (_, __) => Close();
            row.Controls.Add(_close, 2, 0);

            header.Controls.Add(row);
            header.Controls.Add(new UiTheme.RuleLine(UiTheme.Accent, 1, true));

            // The title strip drags the window. It is wired on every child of
            // the strip as well, because each one is its own window and would
            // otherwise swallow the press.
            AttachDrag(row);
            AttachDrag(brand);
            AttachDrag(title);
            AttachDrag(_memeLine);
            AttachDrag(_status);
            return header;
        }

        /// <summary>
        /// The small set of things the owner may want to do besides play. They
        /// live here rather than in the footer so the footer can stay what the
        /// owner asked for: the hotkeys.
        /// </summary>
        private Control BuildToolbar()
        {
            var toolbar = UiTheme.Table(2);
            toolbar.Padding = new Padding(26, 0, 20, 4);
            toolbar.ColumnStyles.Clear();
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _memeStrip.Name = "MemeSlotImages";
            _memeStrip.FlowDirection = FlowDirection.LeftToRight;
            _memeStrip.WrapContents = false;
            _memeStrip.AutoScroll = false;
            _memeStrip.AutoSize = false;
            _memeStrip.Height = 0;
            _memeStrip.Dock = DockStyle.Top;
            _memeStrip.Margin = Padding.Empty;
            _memeStrip.Padding = Padding.Empty;
            _memeStrip.BackColor = UiTheme.Canvas;
            toolbar.Controls.Add(_memeStrip, 0, 0);

            var buttons = UiTheme.Table(5);
            buttons.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            foreach (var button in new[] { _pick, _inspect, _audio, _memes, _config })
                buttons.Controls.Add(button);
            toolbar.Controls.Add(buttons, 1, 0);
            return toolbar;
        }

        /// <summary>
        /// The Boss column. It lists that Boss's reviewed loadouts as art, and
        /// draws the summon item instead of describing it.
        /// </summary>
        private void BuildColumns(string terrariaExePath)
        {
            var previous = new Control[_columns.Controls.Count];
            _columns.Controls.CopyTo(previous, 0);
            _columns.Controls.Clear();
            foreach (var child in previous) child.Dispose();
            _cards.Clear();
            _selected = null;

            var grid = UiTheme.Table(1);
            grid.Dock = DockStyle.Top;
            grid.Controls.Add(BuildBossColumn(BossFamily.Fishron,
                terrariaExePath), 0, 0);
            _columns.Controls.Add(grid);
            _columns.Height = grid.PreferredSize.Height;
        }

        private Control BuildBossColumn(BossFamily boss, string terrariaExePath)
        {
            var stack = UiTheme.Table(1);

            var head = UiTheme.Table(2);
            head.ColumnStyles.Clear();
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var portrait = new SpriteBox(52);
            portrait.Margin = new Padding(0, 2, 10, 0);
            portrait.AccessibleName = LoadoutCatalog.BossName(boss);
            portrait.Sprite = SpriteStore.Load(terrariaExePath,
                LoadoutCatalog.BossSprite(boss).Key);
            head.Controls.Add(portrait, 0, 0);

            var copy = UiTheme.Table(1);
            var name = UiTheme.Label(LoadoutCatalog.BossName(boss), 12f, true);
            copy.Controls.Add(name);
            var summon = UiTheme.Table(2);
            summon.ColumnStyles.Clear();
            summon.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
            summon.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var summonIcon = new SpriteBox(24);
            summonIcon.Margin = new Padding(0, 3, 6, 0);
            summonIcon.Sprite = SpriteStore.Load(terrariaExePath,
                LoadoutCatalog.SummonSprite(boss).Key);
            summonIcon.AccessibleName = LoadoutCatalog.SummonItem(boss);
            summon.Controls.Add(summonIcon, 0, 0);
            var summonText = UiTheme.Paragraph("用" +
                LoadoutCatalog.SummonItem(boss) + "召唤");
            summonText.Margin = new Padding(0, 6, 0, 0);
            summon.Controls.Add(summonText, 1, 0);
            copy.Controls.Add(summon);
            head.Controls.Add(copy, 1, 0);
            stack.Controls.Add(head);

            // The separator between the Boss heading and its loadouts is the
            // same gradient line every other separator is.
            stack.Controls.Add(new UiTheme.RuleLine(UiTheme.Separator, 1, true)
            {
                Margin = new Padding(0, 8, 0, 8)
            });

            foreach (var loadout in LoadoutCatalog.For(boss))
            {
                var icons = new Image[loadout.Icons.Length];
                for (var i = 0; i < loadout.Icons.Length; i++)
                    icons[i] = SpriteStore.Load(terrariaExePath,
                        loadout.Icons[i].Key);
                var control = new LoadoutCard(loadout, icons, _tooltip);
                control.Click += (_, __) => Select(control);
                stack.Controls.Add(control);
                _cards.Add(control);
            }
            return stack;
        }

        /// <summary>
        /// Records the owner's pick and shows what it needs.
        ///
        /// It deliberately writes nothing. The mod decides the route from the
        /// equipment actually worn when the Boss appears, so a selection here
        /// cannot widen or narrow admission; treating it as a filter would mean
        /// a mis-click silently stops the mod from taking over. The requirements
        /// are the one thing the owner has to act on, so they are the only thing
        /// printed -- the old trailing explanation of how the route is chosen is
        /// gone.
        /// </summary>
        private void Select(LoadoutCard card)
        {
            _selected = card;
            foreach (var other in _cards)
                other.Selected = ReferenceEquals(other, card);
            _statusDetail.Text = card.Loadout.Name + "：" +
                string.Join(" + ", card.Loadout.Requirements);
        }

        private Control BuildFooter()
        {
            var footer = UiTheme.Table(1);
            footer.Controls.Add(new UiTheme.RuleLine(UiTheme.Separator, 1, true));

            var row = UiTheme.Table(3);
            row.Padding = new Padding(24, 10, 20, 12);
            row.ColumnStyles.Clear();
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var keys = UiTheme.Table(2);
            keys.Margin = new Padding(0, 0, 14, 0);
            keys.Controls.Add(Hotkey("F8", "监视"));
            keys.Controls.Add(Hotkey("F9", "归还"));
            row.Controls.Add(keys, 0, 0);

            var right = UiTheme.Table(1);
            right.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            _statusDetail.Name = "StatusDetail";
            _statusDetail.TextAlign = ContentAlignment.MiddleRight;
            _statusDetail.Margin = new Padding(0, 0, 0, 6);
            right.Controls.Add(_statusDetail);
            var actions = UiTheme.Table(2);
            actions.Anchor = AnchorStyles.Right;
            _restore.MinimumSize = new Size(104, 36);
            _install.MinimumSize = new Size(118, 36);
            _install.Name = "InstallAction";
            _restore.Name = "RestoreAction";
            actions.Controls.Add(_restore);
            actions.Controls.Add(_install);
            right.Controls.Add(actions);
            row.Controls.Add(right, 2, 0);
            footer.Controls.Add(row);
            return footer;
        }

        private static Control Hotkey(string key, string description)
        {
            var row = UiTheme.Table(2);
            row.Margin = new Padding(0, 2, 22, 2);
            row.ColumnStyles.Clear();
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var badge = UiTheme.Label(key, 10.5f, true);
            badge.Name = "Hotkey" + key;
            badge.ForeColor = UiTheme.Accent;
            badge.Margin = new Padding(0, 0, 7, 0);
            row.Controls.Add(badge, 0, 0);
            row.Controls.Add(UiTheme.Label(description, 10.5f, false), 1, 0);
            return row;
        }

        private async void PickGame()
        {
            if (_previewOnly || _busy) return;
            using (var dialog = new OpenFileDialog
            {
                Filter = "Terraria.exe|Terraria.exe",
                CheckFileExists = true,
                Title = "选择 Terraria.exe"
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _manualPath = dialog.FileName;
            }
            _pathDebounce.Stop();
            await InspectAsync();
        }

        /// <summary>
        /// The owner never has to type a path: Steam is located automatically
        /// and the picker is only a fallback.
        /// </summary>
        private string ResolvePath()
        {
            if (!string.IsNullOrEmpty(_manualPath)) return _manualPath;
            try { return TerrariaLocator.FindTerrariaExe() ?? string.Empty; }
            catch { return string.Empty; }
        }

        private async Task InspectAsync()
        {
            if (_previewOnly || _busy || IsDisposed) return;
            if (_inspectionRunning)
            {
                _pendingInspection = true;
                return;
            }
            _inspectionRunning = true;
            _lastStatus = null;
            var path = ResolvePath();
            _status.Text = "正在检查游戏…";
            _status.ForeColor = UiTheme.Muted;
            UpdateButtons();
            try
            {
                var status = await Task.Run(() => _installer.GetStatus(path));
                if (!IsDisposed) ApplyStatus(status, path);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    _status.Text = "检查失败";
                    _status.ForeColor = UiTheme.Danger;
                    _statusDetail.Text = ex.Message;
                }
            }
            finally
            {
                _inspectionRunning = false;
                if (!IsDisposed)
                {
                    UpdateButtons();
                    if (_pendingInspection)
                    {
                        _pendingInspection = false;
                        await InspectAsync();
                    }
                }
            }
        }

        /// <summary>
        /// One short line of state per case, and nothing else.
        ///
        /// This is where the owner's rule bites hardest. Each branch used to
        /// carry a second sentence explaining the situation -- why a changed exe
        /// must be verified through Steam, what version the whitelist covers,
        /// what installing does not touch. None of it is something the owner has
        /// to act on, and the version banner in particular was developer output.
        /// The state itself, plus a button, is the whole message.
        /// </summary>
        private void ApplyStatus(InstallStatus status, string path)
        {
            _lastStatus = status;
            _checkedPath = path;
            var installed = status.State == InstallState.Installed;
            var supported = status.State == InstallState.CleanSupported;
            _statusDetail.Text = string.Empty;
            if (installed)
            {
                _status.Text = "已安装 · 进游戏按 F8";
                _status.ForeColor = UiTheme.Mint;
            }
            else if (supported)
            {
                _status.Text = "可以安装";
                _status.ForeColor = UiTheme.Mint;
            }
            else if (status.State == InstallState.NotFound)
            {
                _status.Text = "没有找到 Terraria";
                _status.ForeColor = UiTheme.Danger;
                _statusDetail.Text = "请选择 Terraria.exe";
            }
            else if (status.State == InstallState.InstalledButChanged)
            {
                // Restore deliberately refuses this state: the exe no longer
                // matches what was patched, so overwriting it could destroy a
                // Steam update or someone else's change. The console must not
                // tell the owner to press a button it has disabled, so it names
                // the action that actually works.
                _status.Text = "需要先校验游戏文件";
                _status.ForeColor = UiTheme.Danger;
            }
            else
            {
                _status.Text = "这个版本暂不支持";
                _status.ForeColor = UiTheme.Danger;
            }
            var usable = installed || supported;
            BuildColumns(usable ? path : null);
            RefreshMemes(usable ? path : null);
            _memeLine.Text = MemeVoice.Line(
                installed ? MemeMoment.Installed
                    : supported ? MemeMoment.Ready : MemeMoment.Blocked,
                _memeCounter++);
            UpdateButtons();
        }

        /// <summary>
        /// Shows whatever meme images the owner dropped in. They are decoration,
        /// so they get no caption: a missing or unreadable file is simply not
        /// drawn, and the console never explains itself about it.
        /// </summary>
        private void RefreshMemes(string terrariaExePath)
        {
            // Disposing a control removes it from its parent's collection, so the
            // collection is snapshotted first. Disposing the PictureBox does not
            // dispose its Image, so the bitmaps are released explicitly.
            var previous = new Control[_memeStrip.Controls.Count];
            _memeStrip.Controls.CopyTo(previous, 0);
            _memeStrip.Controls.Clear();
            foreach (var child in previous)
            {
                var picture = child as PictureBox;
                if (picture != null && picture.Image != null)
                {
                    var image = picture.Image;
                    picture.Image = null;
                    image.Dispose();
                }
                child.Dispose();
            }
            _memeStrip.Height = 0;
            if (string.IsNullOrEmpty(terrariaExePath)) return;
            var shown = 0;
            foreach (var file in MemeSlot.Discover(terrariaExePath))
            {
                var image = MemeSlot.LoadThumbnail(file);
                if (image == null) continue;
                // Canvas, not a tinted field: a filled panel behind the image
                // would be a rectangle, and the thumbnails sit on the page.
                _memeStrip.Controls.Add(new PictureBox
                {
                    Image = image,
                    Size = new Size(40, 40),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = UiTheme.Canvas,
                    Margin = new Padding(0, 0, 6, 0),
                    AccessibleName = Path.GetFileNameWithoutExtension(file)
                });
                shown++;
            }
            if (shown > 0) _memeStrip.Height = 44;
        }

        private void UpdateButtons()
        {
            var valid = _lastStatus != null && string.Equals(_checkedPath,
                ResolvePath(), StringComparison.Ordinal);
            _install.Enabled = !_busy && !_inspectionRunning && valid &&
                _lastStatus.State == InstallState.CleanSupported;
            _restore.Enabled = !_busy && !_inspectionRunning && valid &&
                _lastStatus.State == InstallState.Installed;
            _audio.Enabled = _memes.Enabled = _config.Enabled = !_busy && valid &&
                _lastStatus.State == InstallState.Installed;
            _pick.Enabled = _inspect.Enabled = !_busy && !_inspectionRunning;
        }

        private async Task ChangeInstallationAsync(bool restore)
        {
            if (_previewOnly || _busy || _inspectionRunning) return;
            if (_lastStatus == null || _lastStatus.State !=
                (restore ? InstallState.Installed : InstallState.CleanSupported))
                return;
            var path = _checkedPath;
            // Two facts and a question. The path, the backup advice and the
            // "does not touch your save" reassurance were all text the owner
            // does not have to act on, so they are gone.
            var prompt = restore
                ? "恢复前请先退出 Terraria。\n\n确认继续？"
                : "安装前请先退出 Terraria。\n\n确认继续？";
            if (MessageBox.Show(this, prompt,
                    restore ? "恢复原版" : "安装拆特",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button2) != DialogResult.OK)
                return;
            _busy = true;
            _pathDebounce.Stop();
            _status.Text = restore ? "正在恢复…" : "正在安装…";
            _status.ForeColor = UiTheme.Muted;
            UpdateButtons();
            try
            {
                var result = await Task.Run(() => restore
                    ? _installer.Restore(path)
                    : _installer.Install(path,
                        AppDomain.CurrentDomain.BaseDirectory));
                ApplyStatus(result, path);
            }
            catch (Exception ex)
            {
                _status.Text = "操作未完成";
                _status.ForeColor = UiTheme.Danger;
                _statusDetail.Text = ex.Message;
                _lastStatus = null;
            }
            finally
            {
                _busy = false;
                UpdateButtons();
            }
        }

        private void OpenData(string relative)
        {
            if (_previewOnly || _busy || string.IsNullOrEmpty(_checkedPath)) return;
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(_checkedPath),
                    "Chaite", relative);
                if (!Directory.Exists(path) && !File.Exists(path))
                    throw new FileNotFoundException("文件或文件夹不存在。", path);
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "无法打开",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        internal void SetPreviewStatus(InstallState state)
        {
            if (!_previewOnly)
                throw new InvalidOperationException(
                    "Preview state is only available to isolated UI tests.");
            // Use the machine's real installation when there is one, so the
            // captured previews show the actual vanilla art rather than
            // placeholders. On a machine without Terraria the placeholder path
            // still exercises the layout, which is what the checks need.
            string path = null;
            try { path = TerrariaLocator.FindTerrariaExe(); }
            catch (Exception) { }
            _manualPath = string.IsNullOrEmpty(path)
                ? @"D:\SteamLibrary\steamapps\common\Terraria\Terraria.exe"
                : path;
            ApplyStatus(new InstallStatus
            {
                State = state,
                GameVersion = "1.4.5.8",
                Sha256 = InstallationService.SupportedSha256,
                Message = string.Empty
            }, _manualPath);
        }

        internal void InvalidatePreviewPath()
        {
            if (!_previewOnly) return;
            _manualPath = "preview-invalid-path";
            _lastStatus = null;
            // The actions are gated on the checked path still matching the
            // resolved one, so they have to be re-evaluated here; leaving them
            // enabled would let the owner install against a path that was never
            // inspected.
            UpdateButtons();
        }

        // ---- window chrome -------------------------------------------------
        //
        // There is no system frame, so the frame's jobs are done here: the edge
        // strip reports the resize direction, and the title strip moves the
        // window. Both are wired twice on purpose. The hit test codes below are
        // the frame's own mechanism and are what the shell's own windows are
        // expected to defer to; the mouse handlers are the same behaviour
        // expressed in the window's own client strip, which the shell cannot
        // cover, so a window that never sees a hit test message still resizes
        // and still moves.

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST && !_previewOnly)
            {
                base.WndProc(ref m);
                if (m.Result.ToInt32() == HTCLIENT)
                {
                    var packed = m.LParam.ToInt32();
                    var screen = new Point(
                        unchecked((short)(packed & 0xFFFF)),
                        unchecked((short)((packed >> 16) & 0xFFFF)));
                    var edge = EdgeAt(PointToClient(screen));
                    if (edge != 0) m.Result = (IntPtr)edge;
                }
                return;
            }
            base.WndProc(ref m);
        }

        /// <summary>
        /// Which resize direction a point in client coordinates belongs to, or 0
        /// for the body of the window. Internal rather than private because the
        /// isolated checks exercise it directly: the mapping from a point to an
        /// HT code is the whole of the resize behaviour that can be tested
        /// without a real mouse.
        /// </summary>
        internal int EdgeAt(Point point)
        {            var left = point.X < Padding.Left;
            var right = point.X >= ClientSize.Width - Padding.Right;
            var top = point.Y < Padding.Top;
            var bottom = point.Y >= ClientSize.Height - Padding.Bottom;
            if (top && left) return HTTOPLEFT;
            if (top && right) return HTTOPRIGHT;
            if (bottom && left) return HTBOTTOMLEFT;
            if (bottom && right) return HTBOTTOMRIGHT;
            if (left) return HTLEFT;
            if (right) return HTRIGHT;
            if (top) return HTTOP;
            if (bottom) return HTBOTTOM;
            return 0;
        }

        private static Cursor CursorFor(int edge)
        {
            switch (edge)
            {
                case HTLEFT:
                case HTRIGHT: return Cursors.SizeWE;
                case HTTOP:
                case HTBOTTOM: return Cursors.SizeNS;
                case HTTOPLEFT:
                case HTBOTTOMRIGHT: return Cursors.SizeNWSE;
                case HTTOPRIGHT:
                case HTBOTTOMLEFT: return Cursors.SizeNESW;
                default: return Cursors.Default;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!_previewOnly)
            {
                if (_resizeEdge == 0) Cursor = CursorFor(EdgeAt(e.Location));
                else ApplyResize(Cursor.Position);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (!_previewOnly && _resizeEdge == 0) Cursor = Cursors.Default;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (!_previewOnly && e.Button == MouseButtons.Left)
            {
                var edge = EdgeAt(e.Location);
                if (edge != 0)
                {
                    _resizeEdge = edge;
                    _resizeCursorOrigin = Cursor.Position;
                    _resizeBounds = Bounds;
                    Capture = true;
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (_resizeEdge != 0)
            {
                _resizeEdge = 0;
                Capture = false;
                Cursor = Cursors.Default;
            }
            base.OnMouseUp(e);
        }

        private void ApplyResize(Point cursor)
        {
            var dx = cursor.X - _resizeCursorOrigin.X;
            var dy = cursor.Y - _resizeCursorOrigin.Y;
            var bounds = _resizeBounds;
            var left = _resizeEdge == HTLEFT ||
                _resizeEdge == HTTOPLEFT || _resizeEdge == HTBOTTOMLEFT;
            var right = _resizeEdge == HTRIGHT ||
                _resizeEdge == HTTOPRIGHT || _resizeEdge == HTBOTTOMRIGHT;
            var top = _resizeEdge == HTTOP ||
                _resizeEdge == HTTOPLEFT || _resizeEdge == HTTOPRIGHT;
            var bottom = _resizeEdge == HTBOTTOM ||
                _resizeEdge == HTBOTTOMLEFT || _resizeEdge == HTBOTTOMRIGHT;
            if (left) { bounds.X += dx; bounds.Width -= dx; }
            if (right) bounds.Width += dx;
            if (top) { bounds.Y += dy; bounds.Height -= dy; }
            if (bottom) bounds.Height += dy;
            if (bounds.Width < MinimumSize.Width)
            {
                if (left) bounds.X = _resizeBounds.Right - MinimumSize.Width;
                bounds.Width = MinimumSize.Width;
            }
            if (bounds.Height < MinimumSize.Height)
            {
                if (top) bounds.Y = _resizeBounds.Bottom - MinimumSize.Height;
                bounds.Height = MinimumSize.Height;
            }
            Bounds = bounds;
        }

        private void AttachDrag(Control control)
        {
            control.MouseDown += DragStart;
            control.MouseMove += DragMove;
            control.MouseUp += DragEnd;
        }

        private void DragStart(object sender, MouseEventArgs e)
        {
            if (_previewOnly || e.Button != MouseButtons.Left) return;
            _dragSource = (Control)sender;
            _dragCursorOrigin = Cursor.Position;
            _dragWindowOrigin = Location;
            _dragSource.Capture = true;
        }

        private void DragMove(object sender, MouseEventArgs e)
        {
            if (_dragSource == null || e.Button != MouseButtons.Left) return;
            var now = Cursor.Position;
            Location = new Point(
                _dragWindowOrigin.X + now.X - _dragCursorOrigin.X,
                _dragWindowOrigin.Y + now.Y - _dragCursorOrigin.Y);
        }

        private void DragEnd(object sender, MouseEventArgs e)
        {
            if (_dragSource == null) return;
            _dragSource.Capture = false;
            _dragSource = null;
        }

        internal Button InstallAction { get { return _install; } }
        internal Button RestoreAction { get { return _restore; } }
        internal Control CloseAction { get { return _close; } }
        internal Panel ColumnHost { get { return _columns; } }
        internal Panel ScrollBody { get { return _body; } }
        internal FlowLayoutPanel MemeSlotImages { get { return _memeStrip; } }
        internal Label MemeLine { get { return _memeLine; } }
        internal string StatusDetail { get { return _statusDetail.Text; } }
        internal int LoadoutCardCount { get { return _cards.Count; } }

        internal LoadoutCard CardAt(int index)
        {
            return index >= 0 && index < _cards.Count ? _cards[index] : null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _pathDebounce.Dispose();
                _tooltip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
