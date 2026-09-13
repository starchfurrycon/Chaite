using Chaite.Patcher;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Chaite.Manager
{
    // Preview-only windows stay outside every monitor and can never activate.
    // These checks use fake installation states, no game reads or patch writes.
    internal static class UiSmokeTest
    {
        internal static int Run(string outputDirectory)
        {
            var output = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(output);
            var report = new StringBuilder();
            var failureCount = 0;
            var scenarios = new[]
            {
                new Scenario("default", 1060, 810, 1),
                new Scenario("compact", 860, 620, 1),
                new Scenario("wide", 1380, 900, 1),
                new Scenario("geometry-125", 1060, 810, 1.25f),
                new Scenario("geometry-150", 1060, 810, 1.5f),
                new Scenario("geometry-200", 1060, 810, 2)
            };
            foreach (var scenario in scenarios)
            {
                var failures = new List<string>();
                try
                {
                    using (var form = new MainForm(true))
                    {
                        var foreground = GetForegroundWindow();
                        AssertOffscreen(form);
                        form.Show();
                        Application.DoEvents();
                        AssertOffscreen(form);
                        if (GetForegroundWindow() == form.Handle) failures.Add("Preview took focus.");
                        var dpiScale = form.DeviceDpi / 96f;
                        form.ClientSize = new Size((int)(scenario.Width * dpiScale), (int)(scenario.Height * dpiScale));
                        if (scenario.Scale != 1)
                        {
                            form.Scale(new SizeF(scenario.Scale, scenario.Scale));
                            form.ClientSize = new Size((int)(scenario.Width * dpiScale * scenario.Scale), (int)(scenario.Height * dpiScale * scenario.Scale));
                        }
                        form.SetPreviewStatus(InstallState.CleanSupported);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        ValidateSupportedBossCopy(form, failures);
                        ValidatePrimary(form, form.InstallAction, failures);
                        ValidatePrimary(form, form.RestoreAction, failures);
                        if (!form.InstallAction.Enabled || form.RestoreAction.Enabled) failures.Add("Clean supported install gates incorrect.");
                        if (form.ScrollBody.HorizontalScroll.Visible) failures.Add("Body unexpectedly needs horizontal scrolling.");
                        Save(form, output, scenario.Name + ".png");
                        form.ScrollBody.AutoScrollPosition = new Point(0, int.MaxValue);
                        LayoutAll(form);
                        Save(form, output, scenario.Name + "-bottom.png");
                        form.SetPreviewStatus(InstallState.Installed);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        if (form.InstallAction.Enabled || !form.RestoreAction.Enabled) failures.Add("Installed restore gates incorrect.");
                        Save(form, output, scenario.Name + "-installed.png");
                        form.SetPreviewStatus(InstallState.CleanUnsupported);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        if (form.InstallAction.Enabled || form.RestoreAction.Enabled) failures.Add("Unknown binary enables destructive action.");
                        Save(form, output, scenario.Name + "-blocked.png");
                        form.SetPreviewStatus(InstallState.CleanSupported);
                        if (scenario.Name == "default")
                        {
                            var originalSize = form.ClientSize;
                            form.ClientSize = new Size((int)(860 * dpiScale), (int)(620 * dpiScale));
                            LayoutAll(form);
                            ValidateTree(form, failures);
                            form.ClientSize = originalSize;
                            LayoutAll(form);
                            ValidateTree(form, failures);
                            ValidatePrimary(form, form.InstallAction, failures);
                            Save(form, output, "resize-roundtrip.png");
                        }
                        form.InvalidatePreviewPath();
                        if (form.InstallAction.Enabled || form.RestoreAction.Enabled) failures.Add("Path edit leaves stale actions enabled.");
                        AssertOffscreen(form);
                        report.AppendLine(scenario.Name + " | " + form.ClientSize + " | DPI " + form.DeviceDpi + " | foreground " + foreground.ToInt64() + " -> " + GetForegroundWindow().ToInt64());
                    }
                }
                catch (Exception ex) { failures.Add(ex.ToString()); }
                foreach (var failure in failures) report.AppendLine("FAIL " + scenario.Name + ": " + failure);
                if (failures.Count == 0) report.AppendLine("PASS " + scenario.Name);
                failureCount += failures.Count;
            }
            report.AppendLine("Failures: " + failureCount);
            report.AppendLine("UI tests use offscreen WS_EX_NOACTIVATE/TOOLWINDOW windows. No game launch, input, world/player reads, installs or restores.");
            report.AppendLine("Geometric scaling is not an actual per-monitor DPI transition. IME, screen readers and live gameplay remain manual checks.");
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString(), Encoding.UTF8);
            return failureCount == 0 ? 0 : 1;
        }

        private static void ValidateTree(Control control, List<string> failures)
        {
            if (control is TableLayoutPanel)
            {
                var children = control.Controls.Cast<Control>().Where(c => c.Visible).ToArray();
                for (var i = 0; i < children.Length; i++)
                    for (var j = i + 1; j < children.Length; j++)
                    {
                        var overlap = Rectangle.Intersect(children[i].Bounds, children[j].Bounds);
                        if (overlap.Width > 1 && overlap.Height > 1) failures.Add("Sibling overlap: " + Describe(children[i]) + " / " + Describe(children[j]));
                    }
            }
            foreach (Control child in control.Controls)
            {
                if (!(control is Form) && !(control is ScrollableControl && ((ScrollableControl)control).AutoScroll) && child.Visible && child.Bottom > control.ClientSize.Height + 1)
                    failures.Add("Child extends below container: " + Describe(child) + " bottom=" + child.Bottom + " parentHeight=" + control.ClientSize.Height);
                if (child is Label && child.Visible && child.Width > 0)
                {
                    var label = (Label)child;
                    var size = TextRenderer.MeasureText(label.Text, label.Font, new Size(Math.Max(1, label.ClientSize.Width - label.Padding.Horizontal), int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
                    if (label.Height + 2 < size.Height + label.Padding.Vertical) failures.Add("Label too short: " + Describe(label) + " actual=" + label.Size + " needs=" + size);
                    if (label.Name.StartsWith("Hotkey", StringComparison.Ordinal) && TextRenderer.MeasureText(label.Text, label.Font).Width + label.Padding.Horizontal > label.Width)
                        failures.Add("Hotkey badge wraps: " + label.Name);
                }
                if ((child is TextBox || child is Label) && child.Visible && Contrast(child.ForeColor, EffectiveBackColor(child)) < 4.5)
                    failures.Add("Low contrast: " + Describe(child));
                if (child is TextBox && child.Height < child.Font.Height) failures.Add("Input too short: " + Describe(child));
                ValidateTree(child, failures);
            }
        }

        private static void ValidateSupportedBossCopy(Control root,
            List<string> failures)
        {
            var copy = string.Join("\n", AllControls(root)
                .Where(control => control.Visible)
                .Select(control => control.Text));
            var required = new[]
            {
                "来吧，试一下米妮",
                "仅接管猪鲨公爵与昼间/夜间光之女皇",
                "其他 Boss 直接拒绝",
                "用松露虫在海洋水体钓鱼",
                "七彩草蛉可在昼夜自动释放并击杀",
                "白天按致命光女门槛预检"
            };
            foreach (var value in required)
                if (copy.IndexOf(value, StringComparison.Ordinal) < 0)
                    failures.Add("Missing supported-Boss UI contract: " + value);
        }

        private static IEnumerable<Control> AllControls(Control root)
        {
            yield return root;
            foreach (Control child in root.Controls)
                foreach (var nested in AllControls(child))
                    yield return nested;
        }

        private static Color EffectiveBackColor(Control control)
        {
            for (var current = control; current != null; current = current.Parent)
                if (current.BackColor.A == 255) return current.BackColor;
            return UiTheme.Canvas;
        }

        private static void ValidatePrimary(Form form, Button button, List<string> failures)
        {
            var rectangle = new Rectangle(Point.Empty, button.Size);
            for (Control current = button; current != null && current != form; current = current.Parent)
            {
                rectangle.Offset(current.Location);
                if (current.Parent != null && !current.Parent.ClientRectangle.Contains(rectangle)) { failures.Add("Primary action clipped: " + button.Name); break; }
            }
            var text = TextRenderer.MeasureText(button.Text.Replace("&", string.Empty), button.Font);
            if (button.Width < text.Width + 8 || button.Height < text.Height + 6) failures.Add("Primary label cramped: " + button.Name);
        }

        private static void LayoutAll(Control control)
        {
            control.PerformLayout();
            foreach (Control child in control.Controls) LayoutAll(child);
            control.PerformLayout();
        }

        private static void Save(Form form, string output, string name)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(output, name), ImageFormat.Png);
            }
        }

        private static void AssertOffscreen(Form form)
        {
            if (Screen.AllScreens.Any(s => s.Bounds.IntersectsWith(form.Bounds))) throw new InvalidOperationException("Preview overlaps a monitor; refusing visual test.");
        }

        private static string Describe(Control control) { return control.GetType().Name + "(" + control.Text + ")"; }
        private static double Channel(byte value) { var x = value / 255d; return x <= .04045 ? x / 12.92 : Math.Pow((x + .055) / 1.055, 2.4); }
        private static double Luminance(Color c) { return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B); }
        private static double Contrast(Color a, Color b) { var x = Luminance(a); var y = Luminance(b); return (Math.Max(x, y) + .05) / (Math.Min(x, y) + .05); }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        private sealed class Scenario
        {
            internal readonly string Name;
            internal readonly int Width, Height;
            internal readonly float Scale;
            internal Scenario(string name, int width, int height, float scale) { Name = name; Width = width; Height = height; Scale = scale; }
        }
    }
}
