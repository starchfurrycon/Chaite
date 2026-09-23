using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Chaite.Manager
{
    /// <summary>
    /// Draws one vanilla sprite scaled into a fixed square.
    ///
    /// Fixed size on purpose: the layout checks compare sibling rectangles, so a
    /// control that resized itself to its art would make the surrounding table's
    /// geometry depend on which sprites happened to be extracted.
    ///
    /// When the sprite has not been extracted yet it draws nothing at all. It
    /// used to draw a dotted placeholder rectangle, which the owner has since
    /// ruled out along with every other rectangle on the console; an absent
    /// sprite now reads as empty space, and the layout checks report it by name.
    /// </summary>
    internal sealed class SpriteBox : Control
    {
        private Image _image;

        internal SpriteBox(int size)
        {
            Size = new Size(size, size);
            MinimumSize = new Size(size, size);
            MaximumSize = new Size(size, size);
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }

        internal Image Sprite
        {
            get { return _image; }
            set
            {
                if (ReferenceEquals(_image, value)) return;
                _image = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (_image == null) return;
            // Preserve aspect: a boss frame is not square and stretching it would
            // misrepresent the art the owner sees in game.
            var scale = Math.Min((float)Width / _image.Width,
                (float)Height / _image.Height);
            var width = Math.Max(1, (int)Math.Round(_image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(_image.Height * scale));
            var target = new Rectangle((Width - width) / 2,
                (Height - height) / 2, width, height);
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(_image, target);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _image != null)
            {
                _image.Dispose();
                _image = null;
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// One selectable loadout: the equipment sprites it needs, its name, and the
    /// gradient line under it.
    ///
    /// There is no card any more. It used to be a bordered, filled rectangle with
    /// a solid bar marking the selection, and all three of those are rectangles,
    /// which the owner ruled out. A row is now its own content plus one
    /// horizontal gradient line, and that line is also the separator to the next
    /// row -- the only state vocabulary is the line's weight and opacity plus the
    /// caption's ink.
    ///
    /// The tooltip is the only text besides the name. That is deliberate: the
    /// only images on the console are the game's own equipment art, and the
    /// official item name is the one thing a sprite cannot convey on its own.
    /// </summary>
    internal sealed class LoadoutCard : Control
    {
        private const int IconEdge = 38;
        private const int IconGap = 6;
        private const int PadLeft = 14;
        private const int IconToName = 14;

        /// <summary>
        /// The widest loadout in the catalog, in icons. The name column is
        /// reserved for this many so every row's name starts at the same x.
        /// </summary>
        private const int ReservedIconSlots = 4;
        /// <summary>Where the first icon starts.</summary>
        private const int IconLeft = PadLeft;

        private readonly LoadoutDefinition _loadout;
        private readonly Image[] _icons;
        private readonly ToolTip _tooltip;
        private string _tooltipText;
        private bool _selected;
        private bool _hover;

        /// <summary>
        /// Where the name starts, derived from how many icons a loadout actually
        /// draws but never narrower than the widest one in the catalog.
        ///
        /// It used to be the constant <c>PadLeft + 3 * IconEdge + 2 * IconGap +
        /// 14</c>, which silently assumed three icons. Three of the four
        /// Fishron loadouts wear four items, so their name was painted straight
        /// over the fourth icon. The layout checks compare sibling control
        /// rectangles and cannot see painted text, so nothing caught it.
        ///
        /// The reserve is the catalog's widest row rather than each row's own
        /// count, so the four names line up in one column instead of stepping in
        /// and out with the icon count.
        /// </summary>
        private int NameLeft
        {
            get
            {
                var count = Math.Max(ReservedIconSlots, _icons.Length);
                return IconLeft + count * IconEdge + (count - 1) * IconGap +
                    IconToName;
            }
        }

        internal LoadoutCard(LoadoutDefinition loadout, Image[] icons,
            ToolTip tooltip)
        {
            _loadout = loadout;
            _icons = icons ?? new Image[0];
            _tooltip = tooltip;
            Height = IconEdge + 22;
            MinimumSize = new Size(0, Height);
            Margin = new Padding(0, 2, 0, 6);
            Dock = DockStyle.Top;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.Selectable, true);
            BackColor = Color.Transparent;
            Font = FontBook.For(loadout.Name, 11f, true);
            AccessibleName = loadout.Name;
            AccessibleDescription = loadout.Key;
        }

        internal LoadoutDefinition Loadout { get { return _loadout; } }

        /// <summary>
        /// Where the painted art ends and the caption begins. The pixel checks
        /// need it: the game's own sprites are the one thing on this console that
        /// legitimately contains vertical edges, so they are excluded from the
        /// "no vertical line anywhere" scan and nothing else is.
        /// </summary>
        internal int IconAreaRight { get { return NameLeft; } }

        /// <summary>
        /// Selects this card. Exposed because the isolated layout checks have to
        /// drive a selection without a mouse, and Control has no PerformClick.
        /// </summary>
        internal void Choose()
        {
            OnClick(EventArgs.Empty);
        }

        internal bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected == value) return;
                _selected = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Test-only: forces the hovered look, so the isolated pixel checks can
        /// capture it. Nothing in the product sets this; the mouse does.
        /// </summary>
        internal bool Hover
        {
            set
            {
                if (_hover == value) return;
                _hover = value;
                Invalidate();
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            ClearTooltip();
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var index = IconIndexAt(e.X);
            var text = index >= 0 && index < _loadout.Icons.Length
                ? _loadout.Icons[index].ChineseName
                : null;
            if (!string.Equals(_tooltipText, text, StringComparison.Ordinal))
            {
                _tooltipText = text;
                // Setting an empty string is how a ToolTip is told to show
                // nothing; the text is only rewritten when it actually changed,
                // otherwise the popup flickers on every mouse move.
                _tooltip.SetToolTip(this, text ?? string.Empty);
            }
            base.OnMouseMove(e);
        }

        private void ClearTooltip()
        {
            if (_tooltipText == null) return;
            _tooltipText = null;
            _tooltip.SetToolTip(this, string.Empty);
        }

        private int IconIndexAt(int x)
        {
            if (x < IconLeft) return -1;
            var offset = x - IconLeft;
            var index = offset / (IconEdge + IconGap);
            var within = offset % (IconEdge + IconGap);
            if (index < 0 || index >= _icons.Length || within > IconEdge)
                return -1;
            return index;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var brush = new SolidBrush(UiTheme.Canvas))
                e.Graphics.FillRectangle(brush, ClientRectangle);

            var emphasised = _selected || _hover;
            var ink = emphasised ? UiTheme.Accent : UiTheme.Text;
            var thickness = emphasised ? 2 : 1;
            UiTheme.DrawRule(e.Graphics,
                new Rectangle(0, Height - thickness, Width, thickness),
                UiTheme.Accent, thickness, !emphasised);

            var top = (Height - thickness - IconEdge) / 2;
            for (var i = 0; i < _icons.Length && i < _loadout.Icons.Length; i++)
            {
                var left = IconLeft + i * (IconEdge + IconGap);
                var image = _icons[i];
                if (image == null) continue;
                var scale = Math.Min((float)IconEdge / image.Width,
                    (float)IconEdge / image.Height);
                var width = Math.Max(1, (int)Math.Round(image.Width * scale));
                var height = Math.Max(1, (int)Math.Round(image.Height * scale));
                e.Graphics.InterpolationMode =
                    InterpolationMode.HighQualityBicubic;
                e.Graphics.DrawImage(image, new Rectangle(
                    left + (IconEdge - width) / 2,
                    top + (IconEdge - height) / 2, width, height));
            }

            TextRenderer.DrawText(e.Graphics, _loadout.Name, Font,
                new Rectangle(NameLeft, 0, Math.Max(0, Width - NameLeft - 8),
                    Math.Max(1, Height - thickness - 2)), ink,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _icons != null)
                for (var i = 0; i < _icons.Length; i++)
                    if (_icons[i] != null)
                    {
                        _icons[i].Dispose();
                        _icons[i] = null;
                    }
            base.Dispose(disposing);
        }
    }
}
