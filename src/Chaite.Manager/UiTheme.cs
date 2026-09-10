using System;
using System.Drawing;
using System.Windows.Forms;

namespace Chaite.Manager
{
    internal static class UiTheme
    {
        internal static readonly Color Canvas = Color.FromArgb(13, 19, 32);
        internal static readonly Color Surface = Color.FromArgb(23, 31, 49);
        internal static readonly Color Footer = Color.FromArgb(19, 27, 43);
        internal static readonly Color Field = Color.FromArgb(11, 18, 31);
        internal static readonly Color Text = Color.FromArgb(239, 243, 252);
        internal static readonly Color Muted = Color.FromArgb(173, 185, 207);
        internal static readonly Color Accent = Color.FromArgb(246, 192, 100);
        internal static readonly Color Mint = Color.FromArgb(134, 226, 193);
        internal static readonly Color Border = Color.FromArgb(55, 69, 93);

        internal static TableLayoutPanel Table(int columns)
        {
            var table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = columns, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent, GrowStyle = TableLayoutPanelGrowStyle.AddRows };
            for (var i = 0; i < columns; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            return table;
        }

        internal static Label Label(string text, float points, bool bold)
        {
            return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, Font = new Font("Microsoft YaHei UI", points, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = Text, BackColor = Color.Transparent, Margin = Padding.Empty, UseCompatibleTextRendering = false };
        }

        internal static Label Paragraph(string text, Color? color = null)
        {
            return new Label { Text = text, AutoSize = true, Dock = DockStyle.Fill, ForeColor = color ?? Muted, BackColor = Color.Transparent, Margin = new Padding(0, 3, 0, 0), UseCompatibleTextRendering = false };
        }

        internal static Control Section(string number, string title, string description)
        {
            var table = Table(1);
            table.Controls.Add(Label(number + "  /  " + title, 12, true));
            table.Controls.Add(Paragraph(description));
            return table;
        }

        internal static Panel Card()
        {
            return new CardPanel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(20, 17, 20, 18), Margin = new Padding(0, 0, 0, 14), BackColor = Surface };
        }

        internal static Button Button(string text, bool primary = false)
        {
            return new ActionButton(primary) { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new Size(90, 44), Padding = new Padding(15, 8, 15, 8), Margin = new Padding(6, 0, 0, 0), Anchor = AnchorStyles.Right | AnchorStyles.Top, Cursor = Cursors.Hand, Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold), AccessibleName = text.Replace("(&C)", "").Replace("(&B)", "").Replace("(&I)", "").Replace("(&R)", "").Trim() };
        }

        private sealed class CardPanel : Panel
        {
            internal CardPanel() { DoubleBuffered = true; ResizeRedraw = true; }
            public override Size GetPreferredSize(Size proposedSize)
            {
                if (Controls.Count == 0) return base.GetPreferredSize(proposedSize);
                var width = proposedSize.Width > Padding.Horizontal ? proposedSize.Width : Width;
                width = Math.Max(Padding.Horizontal + 1, width);
                var childSize = Controls[0].GetPreferredSize(new Size(width - Padding.Horizontal, 0));
                return new Size(width, childSize.Height + Padding.Vertical);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                using (var pen = new Pen(Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }

        private sealed class ActionButton : Button
        {
            private readonly bool _primary;
            private bool _hover;
            internal ActionButton(bool primary)
            {
                _primary = primary;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                UseVisualStyleBackColor = false;
                BackColor = primary ? Accent : Color.FromArgb(36, 48, 71);
                ForeColor = primary ? Field : UiTheme.Text;
                DoubleBuffered = true;
            }
            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                var fill = Enabled ? (_hover ? (_primary ? Color.FromArgb(255, 213, 141) : Color.FromArgb(48, 63, 91)) : BackColor) : Color.FromArgb(36, 44, 59);
                var ink = Enabled ? ForeColor : Color.FromArgb(154, 166, 185);
                using (var brush = new SolidBrush(fill)) e.Graphics.FillRectangle(brush, ClientRectangle);
                using (var pen = new Pen(Enabled && _primary ? Accent : Border)) e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix));
                if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -5, -5), ink, fill);
            }
        }
    }
}
