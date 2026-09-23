using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace Chaite.Manager
{
    /// <summary>
    /// The console's own typefaces, loaded once from the Fonts folder beside the
    /// executable.
    ///
    /// Two families, both SIL Open Font License 1.1, neither invented here:
    ///
    ///   * Chaite IBM Plex Sans   - Latin letters, digits and punctuation. IBM's
    ///                              engineering face: neutral, no ornament.
    ///   * Chaite Noto Sans SC    - Simplified Chinese. Evenly weighted, no
    ///                              decorative strokes.
    ///
    /// The face is chosen per string, not per control: any string containing a
    /// character above U+2E7F (which is where the CJK blocks start) is rendered
    /// in Noto, everything else in Plex. A control whose caption changes at run
    /// time therefore re-picks its face, which is why the labels in UiTheme hook
    /// OnTextChanged.
    ///
    /// Why both AddFontResourceEx and PrivateFontCollection, which looks
    /// redundant: they feed two different font engines. WinForms draws text
    /// through TextRenderer, i.e. GDI, and GDI can only resolve a face that is
    /// in the process font table -- that is what AddFontResourceEx with
    /// FR_PRIVATE provides, and it is private to this process, not installed on
    /// the machine. GDI+ (the FontFamily objects) is fed by the private
    /// collection. Measured on this machine: a Font built from the private
    /// collection reports GDI face "Chaite IBM Plex Sans", while
    /// new Font("Chaite IBM Plex Sans", ...) silently falls back to Microsoft
    /// Sans Serif -- so the families below are always taken from the collection
    /// and never looked up by name.
    ///
    /// Nothing here draws anything; this file only resolves faces.
    /// </summary>
    internal static class FontBook
    {
        private const string LatinName = "Chaite IBM Plex Sans";
        private const string CjkName = "Chaite Noto Sans SC";
        private const uint FR_PRIVATE = 0x10;

        private static readonly string[] Bundled =
        {
            "IBMPlexSans-Regular.ttf",
            "IBMPlexSans-SemiBold.ttf",
            "NotoSansSC-Regular.ttf",
            "NotoSansSC-Bold.ttf"
        };

        private static readonly object Gate = new object();
        private static readonly PrivateFontCollection Collection = Load();
        private static readonly FontFamily LatinFamily = Resolve(LatinName);
        private static readonly FontFamily CjkFamily = Resolve(CjkName);
        private static readonly Dictionary<string, Font> Cache =
            new Dictionary<string, Font>(StringComparer.Ordinal);

        /// <summary>
        /// Whether both bundled families were found and loaded. The isolated
        /// layout checks report this, so a missing Fonts folder shows up as a
        /// named line in report.txt instead of as an unexplained typeface.
        /// </summary>
        internal static bool BundledFamiliesLoaded
        {
            get
            {
                return string.Equals(LatinFamily.Name, LatinName,
                           StringComparison.Ordinal) &&
                       string.Equals(CjkFamily.Name, CjkName, StringComparison.Ordinal);
            }
        }

        internal static string Description
        {
            get
            {
                return BundledFamiliesLoaded
                    ? LatinFamily.Name + " + " + CjkFamily.Name
                    : "fallback (" + LatinFamily.Name + " + " + CjkFamily.Name + ")";
            }
        }

        /// <summary>
        /// The face for one string at one size. Shared and cached: a Font holds a
        /// GDI+ family reference, and building a fresh one per paint would churn
        /// handles for no reason.
        /// </summary>
        internal static Font For(string text, float points, bool bold)
        {
            var family = NeedsChinese(text) ? CjkFamily : LatinFamily;
            var style = bold && family.IsStyleAvailable(FontStyle.Bold)
                ? FontStyle.Bold
                : FontStyle.Regular;
            var key = family.Name + "|" +
                points.ToString("0.##", CultureInfo.InvariantCulture) + "|" +
                (style == FontStyle.Bold ? "b" : "r");
            lock (Gate)
            {
                Font cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
                var font = new Font(family, points, style, GraphicsUnit.Point);
                Cache[key] = font;
                return font;
            }
        }

        /// <summary>
        /// The line box height for one point size, taken as the taller of the two
        /// bundled families.
        ///
        /// The faces have different ascent and descent, so a caption that changes
        /// face at run time changes height with it. That is not hypothetical: the
        /// header's joke line alternates between an all-Latin caption (Plex) and a
        /// mixed one (Noto), and the measured effect was every rule above the
        /// footer moving 4px each time the line rotated -- the whole body
        /// twitching on a text change. Flooring a label's box to this value keeps
        /// it the same whichever face the current caption resolves to.
        /// </summary>
        internal static int LineHeight(float points)
        {
            // "\u62c6" is 拆: above U+2E7F, so it resolves to the CJK family.
            var latin = For("a", points, false);
            var cjk = For("\u62c6", points, false);
            return Math.Max(latin.Height, cjk.Height);
        }

        /// <summary>
        /// True when any character sits above U+2E7F, the top of the Latin
        /// Extended blocks and the start of CJK punctuation and ideographs.
        /// Surrogates fall on the CJK side, which is correct: nothing in the
        /// console is outside the BMP.
        /// </summary>
        private static bool NeedsChinese(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (var i = 0; i < text.Length; i++)
                if (text[i] > '\u2E7F') return true;
            return false;
        }

        private static PrivateFontCollection Load()
        {
            var collection = new PrivateFontCollection();
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "Fonts");
            foreach (var name in Bundled)
            {
                var path = Path.Combine(directory, name);
                if (!File.Exists(path)) continue;
                try
                {
                    AddFontResourceEx(path, FR_PRIVATE, IntPtr.Zero);
                }
                catch (Exception)
                {
                    // GDI will fall back to the GDI+ face it can still see; a
                    // missing process font table entry is not fatal.
                }
                try
                {
                    collection.AddFontFile(path);
                }
                catch (Exception)
                {
                    // A corrupt or unreadable file simply does not contribute a
                    // family; Resolve below then falls back to a system face and
                    // the console still runs.
                }
            }
            return collection;
        }

        private static FontFamily Resolve(string name)
        {
            try
            {
                foreach (var family in Collection.Families)
                    if (string.Equals(family.Name, name, StringComparison.Ordinal))
                        return family;
            }
            catch (Exception)
            {
            }
            return FontFamily.GenericSansSerif;
        }

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int AddFontResourceEx(string file, uint flags,
            IntPtr reserved);
    }
}
