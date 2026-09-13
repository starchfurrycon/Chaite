using Chaite.Patcher;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Chaite.Manager
{
    internal sealed class MainForm : Form
    {
        private readonly InstallationService _installer = new InstallationService();
        private readonly bool _previewOnly;
        private readonly Timer _pathDebounce = new Timer { Interval = 550 };
        private readonly TextBox _path = new TextBox();
        private readonly Label _statusTitle = UiTheme.Label("尚未检查", 17, true);
        private readonly Label _status = UiTheme.Paragraph("选择 Terraria.exe，然后检查兼容性。只有版本与完整哈希都通过时才允许安装。");
        private readonly Label _version = UiTheme.Paragraph("目标版本 1.4.5.8 · Windows Steam 原版 · x86");
        private readonly Label _footerHint = UiTheme.Paragraph("先检查，再安装。安装器不会启动或结束游戏。");
        private readonly Label _audioStatus = UiTheme.Paragraph("自备 PCM WAV 音频；不提供下载，不修改原片段。缺少音频也能正常使用。");
        private readonly Button _inspect = UiTheme.Button("检查兼容性 (&C)");
        private readonly Button _browse = UiTheme.Button("浏览… (&B)");
        private readonly Button _install = UiTheme.Button("安装拆特 (&I)", true);
        private readonly Button _restore = UiTheme.Button("恢复原版 (&R)");
        private readonly Button _audio = UiTheme.Button("打开音频槽位");
        private readonly Button _config = UiTheme.Button("打开配置文件");
        private readonly Panel _body = new Panel();
        private InstallStatus _lastStatus;
        private bool _inspectionRunning;
        private bool _pendingInspection;
        private bool _busy;
        private string _checkedPath;

        internal MainForm(bool previewOnly = false)
        {
            _previewOnly = previewOnly;
            SuspendLayout();
            Text = "拆特 · Boss 战术控制台";
            Font = new Font("Microsoft YaHei UI", 10f);
            BackColor = UiTheme.Canvas;
            ForeColor = UiTheme.Text;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96, 96);
            ClientSize = new Size(1060, 810);
            MinimumSize = new Size(860, 620);
            StartPosition = previewOnly ? FormStartPosition.Manual : FormStartPosition.CenterScreen;
            if (previewOnly) { Location = new Point(-30000, -30000); ShowInTaskbar = false; }

            var shell = UiTheme.Table(1);
            shell.Dock = DockStyle.Fill;
            shell.AutoSize = false;
            shell.RowCount = 3;
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            shell.Controls.Add(BuildHeader(), 0, 0);
            _body.Dock = DockStyle.Fill;
            _body.AutoScroll = true;
            _body.Padding = new Padding(28, 0, 28, 16);
            _body.BackColor = UiTheme.Canvas;
            var content = UiTheme.Table(1);
            content.Dock = DockStyle.Top;
            content.Controls.Add(BuildConnection());
            content.Controls.Add(BuildGuide());
            content.Controls.Add(BuildSettings());
            _body.Controls.Add(content);
            shell.Controls.Add(_body, 0, 1);
            shell.Controls.Add(BuildFooter(), 0, 2);
            Controls.Add(shell);

            _path.TextChanged += (_, __) => PathChanged();
            _pathDebounce.Tick += async (_, __) => { _pathDebounce.Stop(); await InspectAsync(); };
            _browse.Click += (_, __) => Browse();
            _inspect.Click += async (_, __) => { _pathDebounce.Stop(); await InspectAsync(); };
            _install.Click += async (_, __) => await ChangeInstallationAsync(false);
            _restore.Click += async (_, __) => await ChangeInstallationAsync(true);
            _audio.Click += (_, __) => OpenData("Audio");
            _config.Click += (_, __) => OpenData("config.json");
            FormClosing += (_, e) =>
            {
                if (_busy) { e.Cancel = true; _footerHint.Text = "正在完成文件操作，请稍候；完成后可安全关闭。"; }
            };
            if (!previewOnly)
            {
                try { _path.Text = TerrariaLocator.FindTerrariaExe() ?? string.Empty; }
                catch { _status.Text = "未能自动定位 Steam 目录。可以点击“浏览”手动选择 Terraria.exe。"; }
                Shown += async (_, __) => { _pathDebounce.Stop(); await InspectAsync(); };
            }
            UpdateButtons();
            ResumeLayout(true);
        }

        protected override bool ShowWithoutActivation { get { return _previewOnly || base.ShowWithoutActivation; } }
        protected override void OnShown(EventArgs e)
        {
            if (!_previewOnly)
            {
                var area = Screen.FromControl(this).WorkingArea;
                var available = new Size(Math.Max(640, area.Width - 24), Math.Max(480, area.Height - 24));
                MinimumSize = new Size(Math.Min(MinimumSize.Width, available.Width), Math.Min(MinimumSize.Height, available.Height));
                Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));
                Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
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
            var header = UiTheme.Table(2);
            header.Padding = new Padding(30, 23, 30, 20);
            header.ColumnStyles.Clear();
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var brand = UiTheme.Table(1);
            var title = UiTheme.Label("拆特", 32, true);
            title.ForeColor = UiTheme.Accent;
            title.Margin = new Padding(0, 0, 0, 2);
            brand.Controls.Add(title);
            brand.Controls.Add(UiTheme.Paragraph("BOSS 战术控制台  /  原版 Terraria 低延迟战斗接管", UiTheme.Text));
            var right = UiTheme.Table(1);
            right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            right.Margin = new Padding(16, 7, 0, 0);
            var tag = UiTheme.Label("EXPERIMENTAL  /  实验版", 9, true);
            tag.ForeColor = UiTheme.Accent;
            tag.TextAlign = ContentAlignment.MiddleRight;
            right.Controls.Add(tag);
            var quote = UiTheme.Label("来吧，试一下米妮", 12, false);
            quote.TextAlign = ContentAlignment.MiddleRight;
            quote.ForeColor = UiTheme.Muted;
            right.Controls.Add(quote);
            header.Controls.Add(brand, 0, 0);
            header.Controls.Add(right, 1, 0);
            return header;
        }

        private Control BuildConnection()
        {
            var card = UiTheme.Card();
            var stack = UiTheme.Table(1);
            stack.Controls.Add(UiTheme.Section("01", "连接游戏", "只检查本地程序；不读取角色和世界存档。"));
            var pathRow = UiTheme.Table(3);
            pathRow.ColumnStyles.Clear();
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.Margin = new Padding(0, 10, 0, 14);
            var pathHost = new Panel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = UiTheme.Field, Padding = new Padding(10, 10, 10, 10), Margin = new Padding(0, 0, 10, 0), MinimumSize = new Size(200, 44), Size = new Size(200, 44) };
            _path.Name = "GamePath";
            _path.AccessibleName = "Terraria.exe 完整路径";
            _path.BorderStyle = BorderStyle.None;
            _path.BackColor = UiTheme.Field;
            _path.ForeColor = UiTheme.Text;
            _path.Dock = DockStyle.Fill;
            _path.Font = new Font("Segoe UI", 10.5f);
            pathHost.Controls.Add(_path);
            pathRow.Controls.Add(pathHost, 0, 0);
            pathRow.Controls.Add(_browse, 1, 0);
            pathRow.Controls.Add(_inspect, 2, 0);
            stack.Controls.Add(pathRow);
            _statusTitle.ForeColor = UiTheme.Muted;
            _statusTitle.Name = "CompatibilityStatus";
            stack.Controls.Add(_statusTitle);
            _status.Margin = new Padding(0, 5, 0, 6);
            _status.ForeColor = UiTheme.Text;
            stack.Controls.Add(_status);
            stack.Controls.Add(_version);
            card.Controls.Add(stack);
            return card;
        }

        private Control BuildGuide()
        {
            var grid = UiTheme.Table(2);
            grid.Margin = new Padding(0, 0, 0, 14);
            grid.ColumnStyles.Clear();
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var controls = UiTheme.Card();
            controls.Margin = new Padding(0, 0, 7, 0);
            var left = UiTheme.Table(1);
            left.Controls.Add(UiTheme.Section("02", "游戏内操作", "装好后进入游戏，管理器可直接关闭。"));
            left.Controls.Add(Hotkey("F8", "MAN！开始监视", "Boss 出现前开启，你自行召唤并保留操作；猪鲨或光女出现后才接管。来吧，试一下米妮！", UiTheme.Accent));
            left.Controls.Add(Hotkey("F9", "取消监视 / 立即归还操作", "监视或接管期间均可终止；不接受战斗中途 F8。", UiTheme.Mint));
            controls.Controls.Add(left);
            grid.Controls.Add(controls, 0, 0);
            var strategy = UiTheme.Card();
            strategy.Margin = new Padding(7, 0, 0, 0);
            var right = UiTheme.Table(1);
            right.Controls.Add(UiTheme.Section("03", "战斗准备", "生产白名单仅猪鲨与昼/夜光女；其他 Boss 一律拒绝。"));
            right.Controls.Add(GuideLine("猪鲨召唤", "开启监视后，自行用松露虫在海洋钓鱼；拆特不会抛竿或消耗鱼饵。"));
            right.Controls.Add(GuideLine("光女召唤", "开启监视后，自行释放并击杀七彩草蛉；非神圣区也可召唤，需尽快击杀草蛉。"));
            right.Controls.Add(GuideLine("公式重做中", "猪鲨：翅膀＋冲刺、史莱姆女士、可靠旋鼬。光女：强翼＋冲刺、扫帚、雨天虾松露。各路线尚待实战验收。"));
            right.Controls.Add(GuideLine("拆特的原则", "配装不对：从来没试过哦。其他波斯：超囊的对我来说。F9 随时下班。"));
            strategy.Controls.Add(right);
            grid.Controls.Add(strategy, 1, 0);
            return grid;
        }

        private Control BuildSettings()
        {
            var card = UiTheme.Card();
            var stack = UiTheme.Table(1);
            stack.Controls.Add(UiTheme.Section("04", "拆特语音席", "开场米妮 · 挨打 MAN · 亡了亡了 · 胜利 MANBA OUT"));
            var row = UiTheme.Table(3);
            row.ColumnStyles.Clear();
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.Margin = new Padding(0, 10, 0, 0);
            _audioStatus.Margin = new Padding(0, 0, 14, 0);
            row.Controls.Add(_audioStatus, 0, 0);
            row.Controls.Add(_audio, 1, 0);
            row.Controls.Add(_config, 2, 0);
            stack.Controls.Add(row);
            stack.Controls.Add(UiTheme.Paragraph("热键显示为默认值；自定义热键以配置文件为准。更改配置后请重启游戏。"));
            card.Controls.Add(stack);
            return card;
        }

        private Control BuildFooter()
        {
            var footer = UiTheme.Table(3);
            footer.BackColor = UiTheme.Footer;
            footer.Padding = new Padding(28, 16, 28, 18);
            footer.ColumnStyles.Clear();
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            var message = UiTheme.Table(1);
            message.Margin = new Padding(0, 0, 16, 0);
            var safe = UiTheme.Label("F9 立即归还操作  ·  可恢复注入  ·  不改存档", 10, true);
            safe.ForeColor = UiTheme.Mint;
            message.Controls.Add(safe);
            message.Controls.Add(_footerHint);
            _restore.MinimumSize = new Size(146, 48);
            _install.MinimumSize = new Size(172, 48);
            _install.Name = "InstallAction";
            _restore.Name = "RestoreAction";
            footer.Controls.Add(message, 0, 0);
            footer.Controls.Add(_restore, 1, 0);
            footer.Controls.Add(_install, 2, 0);
            return footer;
        }

        private static Control Hotkey(string key, string title, string description, Color color)
        {
            var row = UiTheme.Table(2);
            row.Margin = new Padding(0, 12, 0, 0);
            row.ColumnStyles.Clear();
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var badge = UiTheme.Label(key, 18, true);
            badge.Name = "Hotkey" + key;
            badge.ForeColor = color;
            badge.BackColor = UiTheme.Field;
            badge.Padding = new Padding(4, 6, 4, 6);
            badge.Margin = new Padding(0, 0, 12, 0);
            badge.TextAlign = ContentAlignment.MiddleCenter;
            var copy = UiTheme.Table(1);
            copy.Controls.Add(UiTheme.Label(title, 11, true));
            copy.Controls.Add(UiTheme.Paragraph(description));
            row.Controls.Add(badge, 0, 0);
            row.Controls.Add(copy, 1, 0);
            return row;
        }

        private static Control GuideLine(string title, string description)
        {
            var label = UiTheme.Paragraph(title + "  /  " + description);
            label.Margin = new Padding(0, 10, 0, 0);
            return label;
        }

        private void Browse()
        {
            if (_previewOnly || _busy) return;
            using (var dialog = new OpenFileDialog { Filter = "Terraria.exe|Terraria.exe", CheckFileExists = true, Title = "选择 Steam 原版 Terraria.exe" })
                if (dialog.ShowDialog(this) == DialogResult.OK) _path.Text = dialog.FileName;
        }

        private void PathChanged()
        {
            _lastStatus = null;
            _checkedPath = null;
            _statusTitle.Text = "等待检查";
            _statusTitle.ForeColor = UiTheme.Muted;
            _status.Text = "路径已更改，正在等待检查。此时不会安装或读取游戏内数据。";
            _version.Text = "目标版本 1.4.5.8 · 完整 SHA-256 白名单校验";
            _footerHint.Text = "先检查，再安装。安装器不会启动或结束游戏。";
            UpdateButtons();
            _pathDebounce.Stop();
            if (!_previewOnly) _pathDebounce.Start();
        }

        private async Task InspectAsync()
        {
            if (_previewOnly || _busy || IsDisposed) return;
            if (_inspectionRunning) { _pendingInspection = true; return; }
            _inspectionRunning = true;
            _lastStatus = null;
            var path = _path.Text.Trim();
            _statusTitle.Text = "正在检查兼容性…";
            _statusTitle.ForeColor = UiTheme.Accent;
            _status.Text = "校验程序版本与文件指纹。检查在后台完成，不会接管游戏操作。";
            UpdateButtons();
            try
            {
                var status = await Task.Run(() => _installer.GetStatus(path));
                if (!IsDisposed && string.Equals(path, _path.Text.Trim(), StringComparison.Ordinal)) ApplyStatus(status, path);
            }
            catch (Exception ex)
            {
                if (!IsDisposed) { _statusTitle.Text = "检查未完成"; _status.Text = ex.Message; }
            }
            finally
            {
                _inspectionRunning = false;
                if (!IsDisposed)
                {
                    UpdateButtons();
                    if (_pendingInspection) { _pendingInspection = false; await InspectAsync(); }
                }
            }
        }

        private void ApplyStatus(InstallStatus status, string path)
        {
            _lastStatus = status;
            _checkedPath = path;
            var installed = status.State == InstallState.Installed;
            var supported = status.State == InstallState.CleanSupported;
            _statusTitle.Text = installed ? "已安装，进入游戏后按 F8" : supported ? "检查通过，可以安装" : status.State == InstallState.NotFound ? "请选择 Terraria.exe" : "暂不能安全安装";
            _statusTitle.ForeColor = installed || supported ? UiTheme.Mint : UiTheme.Accent;
            _status.Text = status.Message;
            var hash = status.Sha256 ?? string.Empty;
            _version.Text = string.IsNullOrEmpty(status.GameVersion) ? "目标版本 1.4.5.8 · Windows Steam 原版 · x86" : "检测版本 " + status.GameVersion + "   /   SHA-256 " + (hash.Length > 16 ? hash.Substring(0, 16) + "…" : hash);
            _footerHint.Text = installed ? "更新拆特：先退出游戏并恢复原版，再安装新版。" : supported ? "安装前请退出游戏。会保留经哈希验证的原版备份。" : "未知版本或被改动的程序一律不注入、不覆盖。";
            _audioStatus.Text = installed ? "缺少音频也能接管。自备 PCM WAV 放入音频槽位；音频不会随项目分发。" : "安装后可打开音频与配置。自备 PCM WAV；缺少音频也能正常使用。";
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            var valid = _lastStatus != null && _checkedPath == _path.Text.Trim();
            _install.Enabled = !_busy && !_inspectionRunning && valid && _lastStatus.State == InstallState.CleanSupported;
            _restore.Enabled = !_busy && !_inspectionRunning && valid && _lastStatus.State == InstallState.Installed;
            _audio.Enabled = _config.Enabled = !_busy && valid && _lastStatus.State == InstallState.Installed;
            _inspect.Enabled = !_busy && !_inspectionRunning;
            _browse.Enabled = _path.Enabled = !_busy;
        }

        private async Task ChangeInstallationAsync(bool restore)
        {
            if (_previewOnly || _busy || _inspectionRunning) return;
            if (_lastStatus == null || _checkedPath != _path.Text.Trim() || _lastStatus.State != (restore ? InstallState.Installed : InstallState.CleanSupported)) return;
            var path = _checkedPath;
            var prompt = restore ? "恢复前请先退出 Terraria。\n\n仅在程序仍匹配安装清单时恢复原版，配置、自备音频与原版备份会保留。\n\n" : "安装前请先退出 Terraria，并建议先备份角色与世界。\n\n这会修改所选 Terraria.exe 以接入拆特，并保留哈希锁定的原版备份。不会启动游戏、自动接管或修改存档。\n\n";
            if (MessageBox.Show(this, prompt + path + "\n\n确认继续？", restore ? "确认恢复原版" : "确认安装拆特（实验版）", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
            _busy = true;
            _pathDebounce.Stop();
            _statusTitle.Text = restore ? "正在恢复原版…" : "正在安装拆特…";
            _statusTitle.ForeColor = UiTheme.Accent;
            _status.Text = "正在验证并执行文件操作，请等待完成。";
            UpdateButtons();
            try
            {
                var result = await Task.Run(() => restore ? _installer.Restore(path) : _installer.Install(path, AppDomain.CurrentDomain.BaseDirectory));
                ApplyStatus(result, path);
            }
            catch (Exception ex)
            {
                _statusTitle.Text = restore ? "恢复已中止" : "安装已中止";
                _status.Text = ex.Message;
                _footerHint.Text = "操作未完成，请根据上方提示处理后重新检查。";
                _lastStatus = null;
            }
            finally { _busy = false; UpdateButtons(); }
        }

        private void OpenData(string relative)
        {
            if (_previewOnly || _busy || string.IsNullOrEmpty(_checkedPath)) return;
            try
            {
                var path = Path.Combine(Path.GetDirectoryName(_checkedPath), "Chaite", relative);
                if (!Directory.Exists(path) && !File.Exists(path)) throw new FileNotFoundException("文件或文件夹不存在，请检查安装是否完整。", path);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "无法打开", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        internal void SetPreviewStatus(InstallState state)
        {
            if (!_previewOnly) throw new InvalidOperationException("Preview state is only available to isolated UI tests.");
            _path.Text = @"D:\SteamLibrary\steamapps\common\Terraria\Terraria.exe";
            ApplyStatus(new InstallStatus { State = state, GameVersion = "1.4.5.8", Sha256 = InstallationService.SupportedSha256, Message = state == InstallState.CleanSupported ? "版本与完整 SHA-256 均匹配白名单，可执行可恢复注入。（UI 示例）" : state == InstallState.Installed ? "拆特已注入。游戏内 F8 尝试接管，F9 立即终止。（UI 示例）" : "程序版本或文件指纹不匹配。为避免覆盖 Steam 更新或其他工具的修改，已禁用安装与恢复。（UI 示例）" }, _path.Text);
        }

        internal void InvalidatePreviewPath() { if (_previewOnly) _path.Text = "preview-invalid-path"; }
        internal Button InstallAction { get { return _install; } }
        internal Button RestoreAction { get { return _restore; } }
        internal Panel ScrollBody { get { return _body; } }
        protected override void Dispose(bool disposing)
        {
            if (disposing) _pathDebounce.Dispose();
            base.Dispose(disposing);
        }
    }
}
