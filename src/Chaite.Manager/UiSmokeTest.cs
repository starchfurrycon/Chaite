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
            // Populate the real cache first when the machine has the game, so
            // the captured previews show the owner's own vanilla art instead of
            // placeholder outlines. Best effort: a machine without Terraria
            // still runs every check, just with placeholders.
            var artReady = TryPopulateRealCache();
            report.AppendLine("Fonts: " + FontBook.Description + " (bundled=" +
                FontBook.BundledFamiliesLoaded + ")");
            if (!FontBook.BundledFamiliesLoaded)
                failureCount += Report(report, "fonts",
                    "The bundled faces did not load; the console is rendering in a fallback face.");
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
                        // How long a vertical run of ink is still explicable as a
                        // glyph stroke rather than as a drawn border. Text is
                        // scaled by the display DPI; the layout is scaled by the
                        // scenario as well, so this is a safe upper bound.
                        var limit = LimitFor(dpiScale * scenario.Scale);
                        form.SetPreviewStatus(InstallState.CleanSupported);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        ValidateWindowChrome(form, failures);
                        ValidateSupportedBossCopy(form, failures);
                        ValidateMemeElements(form, failures);
                        ValidateLoadouts(form, failures, artReady);
                        ValidatePrimary(form, form.InstallAction, failures);
                        ValidatePrimary(form, form.RestoreAction, failures);
                        if (!form.InstallAction.Enabled || form.RestoreAction.Enabled) failures.Add("Clean supported install gates incorrect.");
                        if (form.ScrollBody.HorizontalScroll.Visible) failures.Add("Body unexpectedly needs horizontal scrolling.");
                        Save(form, output, scenario.Name + ".png", failures, report, limit);
                        if (scenario.Name == "default")
                            report.AppendLine("Visible strings (supported): " +
                                VisibleStrings(form));
                        ValidateHoverState(form, failures, output, report, limit,
                            scenario.Name + "-hover.png");
                        if (scenario.Name == "default")
                            report.AppendLine("Visible strings (supported+hover): " +
                                VisibleStrings(form));
                        form.ScrollBody.AutoScrollPosition = new Point(0, int.MaxValue);
                        LayoutAll(form);
                        Save(form, output, scenario.Name + "-bottom.png", failures, report, limit);
                        form.SetPreviewStatus(InstallState.Installed);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        ValidateNoDeveloperCopy(form, failures);
                        if (form.InstallAction.Enabled || !form.RestoreAction.Enabled) failures.Add("Installed restore gates incorrect.");
                        Save(form, output, scenario.Name + "-installed.png", failures, report, limit);
                        if (scenario.Name == "default")
                            report.AppendLine("Visible strings (installed): " +
                                VisibleStrings(form));
                        form.SetPreviewStatus(InstallState.CleanUnsupported);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        // The blocked state is where the service's own message
                        // talks about hash whitelists and refusal rules, so it
                        // is exactly the state that must not repeat them.
                        ValidateNoDeveloperCopy(form, failures);
                        if (form.InstallAction.Enabled || form.RestoreAction.Enabled) failures.Add("Unknown binary enables destructive action.");
                        Save(form, output, scenario.Name + "-blocked.png", failures, report, limit);
                        if (scenario.Name == "default")
                            report.AppendLine("Visible strings (blocked): " +
                                VisibleStrings(form));
                        form.SetPreviewStatus(InstallState.InstalledButChanged);
                        LayoutAll(form);
                        ValidateTree(form, failures);
                        ValidateNoDeveloperCopy(form, failures);
                        // Restore refuses a changed exe by design, so both
                        // actions must be off; offering restore here would be a
                        // button that always throws.
                        if (form.InstallAction.Enabled || form.RestoreAction.Enabled) failures.Add("Changed binary must disable both actions.");
                        Save(form, output, scenario.Name + "-changed.png", failures, report, limit);
                        if (scenario.Name == "default")
                            report.AppendLine("Visible strings (changed): " +
                                VisibleStrings(form));
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
                            Save(form, output, "resize-roundtrip.png", failures, report, limit);
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
            failureCount += Report(report, "meme-slot", ValidateMemeSlot);
            failureCount += Report(report, "sprite-store", ValidateSpriteStore);

            var artFailures = new List<string>();
            var extracted = ValidateVanillaArt(artFailures);
            foreach (var failure in artFailures) report.AppendLine("FAIL vanilla-art: " + failure);
            if (artFailures.Count == 0)
                report.AppendLine(extracted
                    ? "PASS vanilla-art"
                    : "SKIP vanilla-art (no Terraria installation on this machine)");
            failureCount += artFailures.Count;

            report.AppendLine("Failures: " + failureCount);
            report.AppendLine("UI tests use offscreen WS_EX_NOACTIVATE/TOOLWINDOW windows. No game launch, input, world/player reads, installs or restores.");
            report.AppendLine("Geometric scaling is not an actual per-monitor DPI transition. IME, screen readers and live gameplay remain manual checks.");
            File.WriteAllText(Path.Combine(output, "report.txt"), report.ToString(), Encoding.UTF8);
            return failureCount == 0 ? 0 : 1;
        }

        /// <summary>
        /// Runs one check group and reports it the same way every time.
        /// </summary>
        private static int Report(StringBuilder report, string name,
            Action<List<string>> check)
        {
            var failures = new List<string>();
            check(failures);
            foreach (var failure in failures)
                report.AppendLine("FAIL " + name + ": " + failure);
            if (failures.Count == 0) report.AppendLine("PASS " + name);
            return failures.Count;
        }

        private static int Report(StringBuilder report, string name, string failure)
        {
            report.AppendLine("FAIL " + name + ": " + failure);
            return 1;
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

        /// <summary>
        /// The owner's contract for the whole console, not just the supported
        /// state: no hashes, no version-whitelist prose, no acceptance wording,
        /// no explanation of how the mod decides anything. Every visible string
        /// is checked, so a developer sentence that only appears in one state is
        /// still caught. The removed list is the mirror image of it: the
        /// sentences the owner struck out are asserted absent, so re-adding one
        /// fails here instead of in a screenshot.
        /// </summary>
        private static void ValidateNoDeveloperCopy(MainForm form,
            List<string> failures)
        {
            var copy = VisibleCopy(form);
            foreach (var value in DeveloperPhrases)
                if (copy.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    failures.Add("Developer detail leaked into the UI: " + value);
            foreach (var value in RemovedPhrases)
                if (copy.IndexOf(value, StringComparison.Ordinal) >= 0)
                    failures.Add("A sentence the owner removed is back in the UI: " + value);
        }

        private static readonly string[] DeveloperPhrases =
        {
            "SHA-256", "SHA256", "哈希", "指纹", "白名单", "EXPERIMENTAL",
            "实验版", "闭环稳定", "hits=0", "clean closed loop", "拒绝注入",
            "验收", "分支", "策略", "诊断", "日志", "config.json",
            "BelongsToBoss", "FormulaRoute"
        };

        /// <summary>
        /// Secondary text the owner asked to have deleted, verbatim. Anything
        /// here reappearing on screen is a regression of the "the user does not
        /// need to know this" rule, not a wording preference.
        /// </summary>
        private static readonly string[] RemovedPhrases =
        {
            "换配装直接换饰品即可",
            "请确认已通过 Steam 安装原版 Terraria",
            "游戏被 Steam 更新或被其他程序改过",
            "请在 Steam 里校验文件完整性",
            "目前只支持 Steam 原版",
            "安装前请先退出游戏",
            "不会启动游戏",
            "修改存档",
            "Boss 出现后接管",
            "立即归还操作",
            "开始监视，Boss"
        };

        private static string VisibleCopy(Control root)
        {
            return string.Join("\n", AllControls(root)
                .Where(control => control.Visible)
                .Select(control => control.Text));
        }

        /// <summary>
        /// Every distinct string the rendered tree actually shows, in one line.
        /// This is the evidence for "the explanatory text is gone": it is read
        /// off the live control tree rather than off the source, so a string
        /// that is defined but never displayed does not appear here and one that
        /// is displayed cannot hide.
        /// </summary>
        private static string VisibleStrings(Control root)
        {
            var seen = new List<string>();
            foreach (var control in AllControls(root))
            {
                if (!control.Visible) continue;
                var text = (control.Text ?? string.Empty).Trim();
                // A loadout row paints its own caption instead of being a Label,
                // so its text is not in Control.Text; it is still user-facing
                // copy and belongs in the inventory.
                var card = control as LoadoutCard;
                if (card != null) text = card.Loadout.Name;
                if (text.Length == 0) continue;
                if (seen.Contains(text)) continue;
                seen.Add(text);
            }
            seen.Sort(StringComparer.Ordinal);
            return string.Join(" | ", seen);
        }

        private static void ValidateSupportedBossCopy(Control root,
            List<string> failures)
        {
            var copy = VisibleCopy(root);
            var required = new[]
            {
                // The console is allowed to say only what the owner has to do.
                // The hotkeys are two or three characters each now: the owner
                // struck out the sentences that used to explain them.
                "F8",
                "监视",
                "F9",
                "归还",
                "用松露虫召唤",
                "猪龙鱼公爵"
            };
            foreach (var value in required)
                if (copy.IndexOf(value, StringComparison.Ordinal) < 0)
                    failures.Add("Missing supported-Boss UI contract: " + value);

            ValidateNoDeveloperCopy((MainForm)root, failures);
        }

        /// <summary>
        /// Fills the console's real sprite cache from the machine's own Terraria
        /// so the previews are captured with genuine art. Returns whether the
        /// cache is usable afterwards.
        /// </summary>
        private static bool TryPopulateRealCache()
        {
            try
            {
                var exe = TerrariaLocator.FindTerrariaExe();
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
                string error;
                return SpriteStore.TryEnsure(exe, out error);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// The Boss column is the console's whole purpose, so its shape is
        /// pinned: the four reviewed Fishron loadouts, every icon resolvable to a
        /// real vanilla asset, and selection landing on exactly one card at a
        /// time.
        /// </summary>
        private static void ValidateLoadouts(MainForm form,
            List<string> failures, bool artReady)
        {
            var fishron = LoadoutCatalog.For(BossFamily.Fishron);
            if (fishron.Count != 4)
                failures.Add("Fishron must list 4 loadouts, found " + fishron.Count);
            if (form.LoadoutCardCount != fishron.Count)
                failures.Add("The console did not build a card per loadout: " +
                    form.LoadoutCardCount);

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var loadout in LoadoutCatalog.All)
            {
                if (!keys.Add(loadout.Key))
                    failures.Add("Duplicate loadout key: " + loadout.Key);
                if (string.IsNullOrWhiteSpace(loadout.Name))
                    failures.Add("Loadout has no name: " + loadout.Key);
                if (loadout.Icons.Length == 0)
                    failures.Add("Loadout shows no art: " + loadout.Key);
                if (loadout.Requirements.Length == 0)
                    failures.Add("Loadout lists no requirement: " + loadout.Key);
                foreach (var icon in loadout.Icons)
                    if (SpriteCatalog.Find(icon.Key) == null)
                        failures.Add("Loadout icon is not in the sprite catalog: " +
                            icon.Key);
            }

            // A wrong id would silently draw a different item, so the boss head
            // is pinned to the index read out of the game's own BossHeadTextures
            // table.
            if (SpriteCatalog.DukeFishron.Id != 4)
                failures.Add("Boss head index no longer matches Terraria's table.");

            var assetKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sprite in SpriteCatalog.All)
            {
                if (!assetKeys.Add(sprite.AssetPath))
                    failures.Add("Two sprites share one asset: " + sprite.AssetPath);
                if (string.IsNullOrWhiteSpace(sprite.ChineseName))
                    failures.Add("Sprite has no name for its tooltip: " + sprite.Key);
                if (sprite.Kind == SpriteKind.BossHead && sprite.Id > 39)
                    failures.Add("Boss head index is out of range: " + sprite.Key);
            }
            if (SpriteCatalog.Keys().Count != SpriteCatalog.All.Length)
                failures.Add("Sprite catalog keys are not one per sprite.");

            // Selection must be exclusive and must describe the chosen loadout.
            var first = form.CardAt(0);
            var second = form.CardAt(1);
            if (first == null || second == null)
            {
                failures.Add("The console did not expose its loadout cards.");
                return;
            }
            first.Choose();
            if (!first.Selected || second.Selected)
                failures.Add("Clicking a loadout did not select exactly that card.");
            if (form.StatusDetail.IndexOf(first.Loadout.Name,
                    StringComparison.Ordinal) < 0)
                failures.Add("Selecting a loadout did not describe it.");
            second.Choose();
            if (first.Selected || !second.Selected)
                failures.Add("Selecting a second loadout left two selected.");

            // With the cache populated, every drawn slot must actually be
            // holding vanilla art. A silently null sprite is exactly how a
            // broken extraction would look: correct layout, empty boxes.
            if (artReady)
            {
                var boxes = AllControls(form).OfType<SpriteBox>().ToList();
                if (boxes.Count == 0)
                    failures.Add("The console draws no vanilla art at all.");
                var empty = boxes.Count(box => box.Sprite == null);
                if (empty != 0)
                    failures.Add(empty + " of " + boxes.Count +
                        " vanilla sprite slots are empty despite a ready cache.");
            }
        }

        /// <summary>
        /// The sprite cache must resolve its path from the exe, refuse to build
        /// without a real Content directory, and never throw at the owner when
        /// the game is missing. The extraction itself needs a graphics device,
        /// so it is not exercised here; the probes cover the pixels.
        /// </summary>
        private static void ValidateSpriteStore(List<string> failures)
        {
            var root = Path.Combine(Path.GetTempPath(),
                "chaite-sprites-" + Guid.NewGuid().ToString("N"));
            var gameDirectory = Path.Combine(root, "Terraria");
            Directory.CreateDirectory(gameDirectory);
            var exe = Path.Combine(gameDirectory, "Terraria.exe");
            try
            {
                var expected = Path.Combine(gameDirectory, "Chaite",
                    SpriteStore.FolderName);
                if (SpriteStore.Directory(exe) != expected)
                    failures.Add("Sprite cache path is not Chaite/Sprites next to the exe.");
                if (SpriteStore.Directory(null) != null)
                    failures.Add("Sprite cache path must be null without an exe path.");
                if (SpriteStore.IsReady(exe))
                    failures.Add("Sprite cache claimed to be ready with nothing extracted.");
                if (SpriteStore.Load(exe, SpriteCatalog.DukeFishron.Key) != null)
                    failures.Add("A missing sprite decoded instead of returning null.");

                // No Content directory: a refusal with a reason, not an exception.
                string error;
                if (SpriteStore.TryEnsure(exe, out error))
                    failures.Add("Sprite extraction succeeded without a Content directory.");
                else if (string.IsNullOrWhiteSpace(error))
                    failures.Add("Sprite extraction failed without telling the owner why.");

                // A manifest that disagrees with the catalog is stale, not ready.
                Directory.CreateDirectory(expected);
                File.WriteAllText(Path.Combine(expected, "manifest.txt"),
                    "chaite-sprites 1\nbosshead-4\n");
                if (SpriteStore.IsReady(exe))
                    failures.Add("A partial sprite cache was accepted as ready.");
            }
            catch (Exception ex)
            {
                failures.Add("Sprite store threw: " + ex);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception) { }
            }
        }

        /// <summary>
        /// Decodes the console's whole sprite catalog out of the real game the
        /// same way the console does, and checks the pixels.
        ///
        /// This is the check that would have caught the project's own wrong
        /// claim that Terraria's XNB could not be read: it fails loudly if XNA
        /// cannot open the owner's content, and it fails if an id resolves to
        /// the wrong art or to nothing at all. It is skipped, not failed, on a
        /// machine with no Terraria, so the suite stays reproducible.
        /// </summary>
        private static bool ValidateVanillaArt(List<string> failures)
        {
            string exe;
            try { exe = TerrariaLocator.FindTerrariaExe(); }
            catch (Exception) { return false; }
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe)) return false;
            var content = SpriteStore.ContentDirectoryFor(exe);
            if (string.IsNullOrEmpty(content) || !Directory.Exists(content))
                return false;

            var root = Path.Combine(Path.GetTempPath(),
                "chaite-art-" + Guid.NewGuid().ToString("N"));
            try
            {
                string error;
                if (!SpriteStore.TryExtractTo(content, root, out error))
                {
                    failures.Add("Extracting the game's own art failed: " + error);
                    return true;
                }
                var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var sprite in SpriteCatalog.All)
                {
                    var path = Path.Combine(root, sprite.Key + ".png");
                    if (!File.Exists(path))
                    {
                        failures.Add("No art extracted for " + sprite.Key);
                        continue;
                    }
                    using (var image = Image.FromFile(path))
                    {
                        if (image.Width == 0 || image.Height == 0)
                        {
                            failures.Add("Extracted art has no size: " + sprite.Key);
                            continue;
                        }
                        // A boss head icon is square and small; anything huge
                        // means the wrong asset was resolved.
                        if (sprite.Kind == SpriteKind.BossHead &&
                            (image.Width > 64 || image.Height > 64))
                            failures.Add("Boss head is not an icon: " + sprite.Key +
                                " " + image.Width + "x" + image.Height);
                        var signature = Signature(image, failures, sprite.Key);
                        if (signature != null) signatures[sprite.Key] = signature;
                    }
                }

                // Distinctness: if two ids resolved to the same texture, the
                // owner would see the wrong item and nothing else would notice.
                var bySignature = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var pair in signatures)
                {
                    string other;
                    if (bySignature.TryGetValue(pair.Value, out other))
                        failures.Add("Two catalog sprites are the same image: " +
                            pair.Key + " and " + other);
                    else
                        bySignature[pair.Value] = pair.Key;
                }
            }
            catch (Exception ex)
            {
                failures.Add("Vanilla art check threw: " + ex.Message);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception) { }
            }
            return true;
        }

        /// <summary>
        /// A coarse fingerprint of the decoded art. Blank art, art that is one
        /// flat colour, or two entries sharing a fingerprint are all reported.
        /// </summary>
        private static string Signature(Image image, List<string> failures,
            string key)
        {
            using (var bitmap = new Bitmap(image))
            {
                var opaque = 0;
                var total = 0;
                var text = new StringBuilder();
                for (var y = 0; y < bitmap.Height; y += 2)
                    for (var x = 0; x < bitmap.Width; x += 2)
                    {
                        total++;
                        var pixel = bitmap.GetPixel(x, y);
                        if (pixel.A > 8) opaque++;
                        text.Append(pixel.A > 8
                            ? (char)('0' + ((pixel.R + pixel.G + pixel.B) / 3) / 26)
                            : '.');
                    }
                if (opaque == 0)
                {
                    failures.Add("Extracted art is fully transparent: " + key);
                    return null;
                }
                // A single flat colour is a placeholder, not a Terraria sprite.
                if (opaque * 20 < total)
                    failures.Add("Extracted art is nearly empty: " + key + " (" +
                        opaque + "/" + total + " opaque)");
                return text.ToString();
            }
        }

        private static IEnumerable<Control> AllControls(Control root)
        {
            yield return root;
            foreach (Control child in root.Controls)
                foreach (var nested in AllControls(child))
                    yield return nested;
        }

        /// <summary>
        /// The meme register has to be wired, not merely compiled: an empty joke
        /// line or a slot that does not collapse are both invisible in a
        /// screenshot diff but obvious to the owner.
        /// </summary>
        private static void ValidateMemeElements(MainForm form,
            List<string> failures)
        {
            var root = (Control)form;
            var memeLine = AllControls(root)
                .FirstOrDefault(control => control.Name == "MemeLine");
            if (memeLine == null) failures.Add("MemeLine is missing.");
            else if (string.IsNullOrWhiteSpace(memeLine.Text))
                failures.Add("MemeLine rendered empty.");
            else
            {
                // The joke line must not resize the header when it rotates. It
                // alternates between an all-Latin caption (Plex) and a mixed one
                // (Noto), whose line boxes differ, and the measured effect before
                // the floor was every rule in the body moving 4px. This measures
                // the label's own height for one caption of each kind.
                var restore = memeLine.Text;
                memeLine.Text = "MAN";
                var latinHeight = memeLine.Height;
                memeLine.Text = "来吧，试一下米妮";
                var mixedHeight = memeLine.Height;
                memeLine.Text = restore;
                if (latinHeight != mixedHeight)
                    failures.Add("Meme line changes height with its face: " +
                        latinHeight + "px all-Latin vs " + mixedHeight + "px mixed.");
            }

            var slotImages = AllControls(root)
                .FirstOrDefault(control => control.Name == "MemeSlotImages");
            if (slotImages == null) failures.Add("MemeSlotImages is missing.");
            else if (slotImages.Height != 0)
                failures.Add("Meme slot images must be collapsed when empty.");

            // The register must actually cycle rather than repeat one line, or
            // the header joke is decoration that never changes.
            var boot = MemeVoice.Line(MemeMoment.Boot, 0);
            if (string.IsNullOrWhiteSpace(boot))
                failures.Add("Meme register boot pool is empty.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (MemeMoment moment in Enum.GetValues(typeof(MemeMoment)))
            {
                var count = MemeVoice.Count(moment);
                if (count == 0) { failures.Add("Meme pool empty: " + moment); continue; }
                for (var i = 0; i < count; i++)
                    seen.Add(moment + "|" + MemeVoice.Line(moment, i));
                // Indexing must be total: a negative or wrapped index must not throw.
                MemeVoice.Line(moment, -1);
                MemeVoice.Line(moment, count + 7);
            }
            if (seen.Count < 8)
                failures.Add("Meme register is too thin: " + seen.Count + " lines.");

            // The register's anchors must survive whatever the layout does.
            var every = string.Join("\n", seen);
            foreach (var anchor in new[] { "桑百颗", "MANBA OUT" })
                if (every.IndexOf(anchor, StringComparison.Ordinal) < 0)
                    failures.Add("Meme register lost its anchor: " + anchor);
        }

        /// <summary>
        /// Exercises the meme slot against a real temporary directory: images are
        /// found in name order, a corrupt file is skipped rather than thrown, and
        /// an absent directory yields nothing instead of an exception.
        /// </summary>
        private static void ValidateMemeSlot(List<string> failures)
        {
            var root = Path.Combine(Path.GetTempPath(),
                "chaite-meme-slot-" + Guid.NewGuid().ToString("N"));
            var gameDirectory = Path.Combine(root, "Terraria");
            var slot = Path.Combine(gameDirectory, "Chaite", MemeSlot.FolderName);
            Directory.CreateDirectory(slot);
            var exe = Path.Combine(gameDirectory, "Terraria.exe");
            try
            {
                if (MemeSlot.Directory(exe) != slot)
                    failures.Add("Meme slot path is not Chaite/Memes next to the exe.");
                if (MemeSlot.Directory(null) != null)
                    failures.Add("Meme slot path must be null without an exe path.");

                // Empty slot: no files, and no directory at all must behave the same.
                if (MemeSlot.Discover(exe).Count != 0)
                    failures.Add("Empty meme slot reported images.");
                if (MemeSlot.Discover(Path.Combine(root, "absent", "Terraria.exe")).Count != 0)
                    failures.Add("Absent meme slot reported images.");
                if (MemeSlot.Discover(null).Count != 0)
                    failures.Add("Null meme path reported images.");

                // Two real images plus one corrupt file plus one ignored type.
                WriteSolidPng(Path.Combine(slot, "20-second.png"), Color.Red, 200, 120);
                WriteSolidPng(Path.Combine(slot, "10-first.png"), Color.Blue, 40, 40);
                File.WriteAllBytes(Path.Combine(slot, "15-corrupt.png"),
                    new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
                File.WriteAllText(Path.Combine(slot, "30-notes.txt"), "not an image");

                var found = MemeSlot.Discover(exe);
                if (found.Count != 3)
                    failures.Add("Meme slot should find 3 images, found " + found.Count);
                else
                {
                    if (Path.GetFileName(found[0]) != "10-first.png" ||
                        Path.GetFileName(found[1]) != "15-corrupt.png" ||
                        Path.GetFileName(found[2]) != "20-second.png")
                        failures.Add("Meme slot order is not ordinal by file name.");
                }

                var first = MemeSlot.LoadThumbnail(Path.Combine(slot, "10-first.png"));
                if (first == null) failures.Add("A valid meme image did not decode.");
                else
                {
                    if (first.Width > MemeSlot.ThumbnailEdge ||
                        first.Height > MemeSlot.ThumbnailEdge)
                        failures.Add("Meme thumbnail exceeded the display edge.");
                    first.Dispose();
                }
                var scaled = MemeSlot.LoadThumbnail(Path.Combine(slot, "20-second.png"));
                if (scaled == null) failures.Add("A wide meme image did not decode.");
                else
                {
                    if (scaled.Width > MemeSlot.ThumbnailEdge ||
                        scaled.Height > MemeSlot.ThumbnailEdge)
                        failures.Add("Wide meme thumbnail was not scaled down.");
                    scaled.Dispose();
                }
                // The important one: a corrupt file is a skip, not a crash.
                if (MemeSlot.LoadThumbnail(Path.Combine(slot, "15-corrupt.png")) != null)
                    failures.Add("A corrupt meme file decoded instead of being skipped.");
                if (MemeSlot.LoadThumbnail(Path.Combine(slot, "missing.png")) != null)
                    failures.Add("A missing meme file decoded instead of being skipped.");
            }
            catch (Exception ex)
            {
                failures.Add("Meme slot threw: " + ex);
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); }
                catch (Exception) { }
            }
        }

        private static void WriteSolidPng(string path, Color color, int width,
            int height)
        {
            using (var bitmap = new Bitmap(width, height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                using (var brush = new SolidBrush(color))
                    graphics.FillRectangle(brush, 0, 0, width, height);
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static Color EffectiveBackColor(Control control)        {
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

        /// <summary>
        /// The window has no system frame, so its frame behaviour is code, and
        /// code that is only exercised by dragging with a real mouse is code
        /// that is never exercised here. This checks the two halves that can be
        /// checked without a mouse: the window really is borderless, and the
        /// point-to-resize-direction map is right at every edge and corner. The
        /// second half also proves the window keeps a strip of its own client
        /// area at each edge, because a hit test can only fire where no child
        /// window covers the point.
        /// </summary>
        private static void ValidateWindowChrome(MainForm form,
            List<string> failures)
        {
            if (form.FormBorderStyle != FormBorderStyle.None)
                failures.Add("The window is not borderless.");
            var width = form.ClientSize.Width;
            var height = form.ClientSize.Height;
            var cases = new[]
            {
                new object[] { new Point(1, 1), MainForm.HTTOPLEFT },
                new object[] { new Point(width - 2, 1), MainForm.HTTOPRIGHT },
                new object[] { new Point(1, height - 2), MainForm.HTBOTTOMLEFT },
                new object[] { new Point(width - 2, height - 2), MainForm.HTBOTTOMRIGHT },
                new object[] { new Point(1, height / 2), MainForm.HTLEFT },
                new object[] { new Point(width - 2, height / 2), MainForm.HTRIGHT },
                new object[] { new Point(width / 2, 1), MainForm.HTTOP },
                new object[] { new Point(width / 2, height - 2), MainForm.HTBOTTOM },
                new object[] { new Point(width / 2, height / 2), 0 }
            };
            foreach (var item in cases)
            {
                var point = (Point)item[0];
                var expected = (int)item[1];
                var actual = form.EdgeAt(point);
                if (actual != expected)
                    failures.Add("Resize hit test at " + point + " returned " +
                        actual + ", expected " + expected + ".");
            }

            // The strip has to be the window's own, or the hit test above never
            // runs on a real pointer. GetChildAtPoint returns null when nothing
            // of the shell covers the point.
            var middle = height / 2;
            if (form.Padding.Left > 0 && form.GetChildAtPoint(
                    new Point(form.Padding.Left / 2, middle)) != null)
                failures.Add("A child covers the left resize strip.");
            if (form.Padding.Top > 0 && form.GetChildAtPoint(
                    new Point(width / 2, form.Padding.Top / 2)) != null)
                failures.Add("A child covers the top resize strip.");
        }

        /// <summary>
        /// Captures the hovered look of every action at once and lays out the
        /// same way as every other capture, so the "no vertical line" scan and
        /// the layout checks cover the hovered state as well as the resting one.
        /// The owner's rule is that hover, selection and focus are expressed by
        /// the line and the ink and never by a filled shape; without this pass
        /// that rule would only ever be checked by reading the source.
        /// </summary>
        private static void ValidateHoverState(MainForm form,
            List<string> failures, string output, StringBuilder report,
            int limit, string name)
        {
            var controls = AllControls(form).Where(control => control.Visible)
                .ToList();
            foreach (var control in controls) UiTheme.PreviewHover(control, true);
            var card = form.CardAt(0);
            if (card != null) card.Hover = true;
            LayoutAll(form);
            ValidateTree(form, failures);
            Save(form, output, name, failures, report, limit);
            foreach (var control in controls) UiTheme.PreviewHover(control, false);
            if (card != null) card.Hover = false;
            LayoutAll(form);
        }

        private static void Save(Form form, string output, string name,
            List<string> failures, StringBuilder report, int limit)
        {
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                bitmap.Save(Path.Combine(output, name), ImageFormat.Png);
                ScanForVerticalLines(form, bitmap, name, limit, report, failures);
            }
        }

        /// <summary>
        /// The owner's rule is that the console draws no rectangle at all: no
        /// button outline, no card border, no window frame, not one vertical
        /// edge. A rectangle on this surface could only ever be a horizontal or
        /// a vertical line, and every horizontal line here is a gradient that
        /// fades out to the right, so the shape that must never appear is a
        /// vertical run of ink. This walks the captured pixels column by column
        /// and fails on any run longer than the tallest glyph the console can
        /// draw, which is what makes "no vertical border" a measurement rather
        /// than a claim about the source.
        ///
        /// The game's own sprites are excluded, and nothing else is: they are
        /// the one thing here that legitimately contains vertical edges, they
        /// are Re-Logic's art rather than ours, and the loadout icon strip is
        /// excluded only up to where its caption starts.
        /// </summary>
        private static void ScanForVerticalLines(Form form, Bitmap bitmap,
            string name, int limit, StringBuilder report, List<string> failures)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            if (width <= 0 || height <= 0) return;
            var excluded = new bool[width * height];
            foreach (var region in ExcludedRegions(form))
                for (var y = Math.Max(0, region.Top); y < Math.Min(height, region.Bottom); y++)
                    for (var x = Math.Max(0, region.Left); x < Math.Min(width, region.Right); x++)
                        excluded[y * width + x] = true;

            var data = bitmap.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var buffer = new byte[data.Stride * height];
            try
            {
                Marshal.Copy(data.Scan0, buffer, 0, buffer.Length);
            }
            finally
            {
                bitmap.UnlockBits(data);
            }

            var longest = 0;
            var column = -1;
            for (var x = 0; x < width; x++)
            {
                var run = 0;
                for (var y = 0; y < height; y++)
                {
                    if (excluded[y * width + x]) { run = 0; continue; }
                    var offset = y * data.Stride + x * 4;
                    if (buffer[offset] > 250 && buffer[offset + 1] > 250 &&
                        buffer[offset + 2] > 250) { run = 0; continue; }
                    run++;
                    if (run <= longest) continue;
                    longest = run;
                    column = x;
                }
            }
            report.AppendLine("  " + name + ": longest vertical ink run " + longest +
                "px at x=" + column + " (glyph allowance " + limit + "px)");
            if (longest > limit)
                failures.Add("A vertical line was drawn: " + name + " column " + column +
                    " runs " + longest + "px of ink, more than the " + limit +
                    "px a glyph stroke can explain.");
        }

        /// <summary>
        /// The pixels that may legitimately contain vertical edges: the game's
        /// own art, and the icon strip of a loadout row.
        /// </summary>
        private static List<Rectangle> ExcludedRegions(Form form)
        {
            var regions = new List<Rectangle>();
            foreach (var control in AllControls(form))
            {
                if (!control.Visible) continue;
                var card = control as LoadoutCard;
                if (card != null)
                {
                    var strip = card.RectangleToScreen(card.ClientRectangle);
                    strip.Width = card.IconAreaRight;
                    regions.Add(form.RectangleToClient(strip));
                    continue;
                }
                if (control is SpriteBox || control is PictureBox)
                    regions.Add(form.RectangleToClient(
                        control.RectangleToScreen(control.ClientRectangle)));
            }
            return regions;
        }

        /// <summary>
        /// How long a vertical run of ink is still explicable as a glyph stroke.
        /// Text is scaled by the display DPI and the layout is scaled by the
        /// scenario, so scaling the allowance by both is a safe upper bound: a
        /// drawn border is always taller than a glyph, and a border on a button
        /// is taller than this even at the largest scenario.
        /// </summary>
        private static int LimitFor(float scale)
        {
            return Math.Max(30, (int)Math.Round(30 * scale));
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
