using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Chaite.Manager
{
    /// <summary>
    /// The console's whole visual language, in one place.
    ///
    /// The owner asked for a technical, minimal, light console with no drawn
    /// shapes at all. Concretely, and these are the rules this file implements:
    ///
    ///   * No rectangle anywhere. Not a button outline, not a card border, not a
    ///     window frame, not a vertical rule. Every separator is a one pixel
    ///     HORIZONTAL gradient line: solid at the left end, fading to fully
    ///     transparent at the right.
    ///   * A button is its caption plus one such line underneath. No box, no
    ///     fill, no outline.
    ///   * State is never a filled shape. Hover, selection and focus raise the
    ///     line to two pixels and make it fully opaque, and turn the caption to
    ///     the accent ink. That is the entire state vocabulary.
    ///   * One quiet title and one full width gradient rule; no colour band, no
    ///     big bold block.
    ///
    /// The colour layer is not invented: it is Pico CSS v2's light scheme (MIT),
    /// whose defaults this mirrors value for value -- background #fff, text
    /// #373c44, muted #646b79, accent #0172ad. No shadow, no gradient fill, no
    /// decorative graphic is drawn by this file.
    ///
    /// Every ink below clears 4.5:1 against the canvas. That is checked twice:
    /// by tools/ui-contrast.py against this file, and by UiSmokeTest.ValidateTree
    /// against the rendered tree.
    /// </summary>
    internal static class UiTheme
    {
        internal static readonly Color Canvas = Color.FromArgb(255, 255, 255);
        internal static readonly Color Text = Color.FromArgb(55, 60, 68);
        internal static readonly Color Muted = Color.FromArgb(100, 107, 121);
        internal static readonly Color Accent = Color.FromArgb(1, 114, 173);
        internal static readonly Color Mint = Color.FromArgb(31, 122, 77);
        internal static readonly Color Danger = Color.FromArgb(179, 38, 30);
        // Secondary inks. Nothing draws them today: the boss heading is body
        // ink, and the meme register is muted, because the owner asked for
        // restraint rather than a colourful console. They are kept, and kept
        // legible on white, because tools/ui-contrast.py audits the palette by
        // name and reports a missing entry as a failure -- a palette that is
        // edited by deleting a colour is exactly the drift that audit exists to
        // catch. Add a use for them, do not silently drop them.
        internal static readonly Color Ember = Color.FromArgb(138, 90, 0);
        internal static readonly Color Slime = Color.FromArgb(14, 110, 140);
        internal static readonly Color Meme = Color.FromArgb(122, 61, 133);
        internal static readonly Color Halo = Color.FromArgb(150, 100, 0);
        internal static readonly Color DisabledInk = Color.FromArgb(135, 142, 153);
        /// <summary>
        /// The base colour of a neutral separator: a quiet blue grey that reads
        /// as a hairline at the left end of its gradient and is invisible by the
        /// right end.
        /// </summary>
        internal static readonly Color Separator = Color.FromArgb(199, 208, 218);
        /// <summary>The line under an action that cannot be used right now.</summary>
        internal static readonly Color Quiet = Color.FromArgb(226, 231, 237);

        /// <summary>
        /// The one line the console draws. Solid at the left edge, fading to
        /// fully transparent at the right; the caller decides the colour and the
        /// weight. A faded line is the resting state, an opaque one is the
        /// hovered/selected/primary state.
        /// </summary>
        internal static void DrawRule(Graphics graphics, Rectangle bounds,
            Color color, int thickness, bool fade)
        {
            if (graphics == null || bounds.Width <= 0 || thickness <= 0) return;
            var line = new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness);
            if (!fade || line.Width < 2)
            {
                using (var brush = new SolidBrush(color))
                    graphics.FillRectangle(brush, line);
                return;
            }
            using (var brush = new LinearGradientBrush(line, color,
                Color.FromArgb(0, color), LinearGradientMode.Horizontal))
                graphics.FillRectangle(brush, line);
        }

        internal static TableLayoutPanel Table(int columns)
        {
            var table = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Top, ColumnCount = columns, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = Color.Transparent, GrowStyle = TableLayoutPanelGrowStyle.AddRows };
            for (var i = 0; i < columns; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
            return table;
        }

        internal static Label Label(string text, float points, bool bold)
        {
            return new ThemedLabel(text, points, bold);
        }

        /// <summary>
        /// The size of the small muted lines, including the header's joke line.
        /// Named rather than inlined because the joke line's box is floored to
        /// this size's line height (see MainForm.BuildHeader), and the two must
        /// not drift apart.
        /// </summary>
        internal const float ParagraphPoints = 9.5f;

        internal static Label Paragraph(string text, Color? color = null)
        {
            return new ThemedLabel(text, ParagraphPoints, false)
            {
                ForeColor = color ?? Muted,
                Margin = new Padding(0, 3, 0, 0)
            };
        }

        /// <summary>
        /// An action is its caption and the gradient line under it. The primary
        /// action is not a filled shape -- that would be a rectangle -- it is the
        /// same control in its permanent emphasised state: accent caption, two
        /// pixel opaque accent line.
        /// </summary>
        internal static Button Button(string text, bool primary = false)
        {
            return new ActionButton(primary)
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(84, 36),
                Padding = new Padding(14, 5, 14, 5),
                Margin = new Padding(8, 0, 0, 0),
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Cursor = Cursors.Hand,
                Font = FontBook.For(text, 10f, true),
                AccessibleName = text
            };
        }

        /// <summary>
        /// Test-only: forces a control into its hovered look so the isolated
        /// pixel checks can capture it. Hover, selection and focus are the one
        /// state vocabulary the owner specified, and "the hovered control is not
        /// a filled rectangle either" is only a measurement if something renders
        /// it. Nothing in the product calls this.
        /// </summary>
        internal static void PreviewHover(Control control, bool hover)
        {
            var action = control as ActionButton;
            if (action != null)
            {
                action.SetHover(hover);
                return;
            }
            var close = control as CloseButton;
            if (close != null) close.SetHover(hover);
        }

        /// <summary>
        /// The window's only chrome control: a bare multiplication sign. At rest
        /// it is muted text and nothing else; hovered it turns accent and gains
        /// the same two pixel line every other action gains. No box, ever.
        /// </summary>
        internal sealed class CloseButton : Control
        {
            private bool _hover;

            internal CloseButton()
            {
                Size = new Size(30, 26);
                MinimumSize = new Size(30, 26);
                MaximumSize = new Size(30, 26);
                Cursor = Cursors.Hand;
                TabStop = false;
                AccessibleName = "关闭";
                SetStyle(ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint, true);
                BackColor = Color.Transparent;
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hover = true;
                Invalidate();
                base.OnMouseEnter(e);
            }

            internal void SetHover(bool hover)
            {
                if (_hover == hover) return;
                _hover = hover;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hover = false;
                Invalidate();
                base.OnMouseLeave(e);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var ink = _hover ? Accent : Muted;
                var font = FontBook.For("\u00d7", 12f, false);
                var text = new Rectangle(0, 0, Width, Height - 3);
                TextRenderer.DrawText(e.Graphics, "\u00d7", font, text, ink,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                if (!_hover) return;
                DrawRule(e.Graphics,
                    new Rectangle(0, Height - 2, Width, 2), Accent, 2, false);
            }
        }

        /// <summary>
        /// A separator or a button underline, as a control. One pixel tall by
        /// default; the caller sets Margin for spacing.
        /// </summary>
        internal sealed class RuleLine : Control
        {
            private readonly Color _color;
            private readonly int _thickness;
            private readonly bool _fade;

            internal RuleLine(Color color, int thickness, bool fade)
            {
                _color = color;
                _thickness = Math.Max(1, thickness);
                _fade = fade;
                Dock = DockStyle.Top;
                Height = _thickness;
                TabStop = false;
                SetStyle(ControlStyles.SupportsTransparentBackColor |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint, true);
                BackColor = Color.Transparent;
            }

            public override Size GetPreferredSize(Size proposedSize)
            {
                var width = proposedSize.Width > 0
                    ? proposedSize.Width
                    : Parent != null ? Parent.ClientSize.Width : Width;
                return new Size(Math.Max(1, width), _thickness);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                DrawRule(e.Graphics, new Rectangle(0, 0, Width, _thickness),
                    _color, _thickness, _fade);
            }
        }

        /// <summary>
        /// A label that picks its own face from its own text, and re-picks it
        /// when the text changes: a status line that switches from "可以安装" to
        /// "F8" must switch from Noto to Plex, and nothing else would notice.
        /// </summary>
        private sealed class ThemedLabel : Label
        {
            private readonly float _points;
            private readonly bool _bold;

            internal ThemedLabel(string text, float points, bool bold)
            {
                _points = points;
                _bold = bold;
                AutoSize = true;
                Dock = DockStyle.Fill;
                // Qualified for the same reason as in ActionButton below: inside
                // a Control-derived class the bare name Text is the caption.
                ForeColor = UiTheme.Text;
                BackColor = Color.Transparent;
                Margin = Padding.Empty;
                UseCompatibleTextRendering = false;
                Font = FontBook.For(text, points, bold);
                Text = text;
            }

            protected override void OnTextChanged(EventArgs e)
            {
                Font = FontBook.For(Text, _points, _bold);
                base.OnTextChanged(e);
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
                // The control's own rectangle is never filled; the canvas shows
                // through it, which is the whole point.
                BackColor = Canvas;
                // UiTheme.Text must stay qualified here. Inside a Control-derived
                // class the inherited Control.Text (string) wins name resolution
                // over the enclosing type's Text (Color), so the bare name binds
                // to this button's caption and the assignment does not compile.
                // OnPaint below wants exactly that caption and is correct bare.
                ForeColor = primary ? Accent : UiTheme.Text;
                DoubleBuffered = true;
                SetStyle(ControlStyles.UserPaint |
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
            internal void SetHover(bool hover) { if (_hover == hover) return; _hover = hover; Invalidate(); }
            protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
            protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
            protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

            protected override void OnPaint(PaintEventArgs e)
            {
                using (var brush = new SolidBrush(Canvas))
                    e.Graphics.FillRectangle(brush, ClientRectangle);

                // Focus counts as emphasis, which is why there is no focus
                // rectangle: a dotted box would be a rectangle, and the owner
                // ruled those out.
                var emphasised = Enabled && (_primary || _hover || Focused);
                var ink = !Enabled
                    ? DisabledInk
                    : emphasised ? Accent : UiTheme.Text;
                var thickness = emphasised ? 2 : 1;
                var line = !Enabled ? Quiet : Accent;

                var caption = new Rectangle(0, 0, Width,
                    Math.Max(1, Height - thickness - 2));
                TextRenderer.DrawText(e.Graphics, Text, Font, caption, ink,
                    TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix);
                DrawRule(e.Graphics,
                    new Rectangle(0, Height - thickness, Width, thickness),
                    line, thickness, !emphasised);
            }
        }
    }
}
