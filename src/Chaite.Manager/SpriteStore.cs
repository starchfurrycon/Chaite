using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Color = Microsoft.Xna.Framework.Color;

namespace Chaite.Manager
{
    /// <summary>
    /// Reads the console's sprites out of the owner's own Terraria install.
    ///
    /// Terraria ships its art as XNB, and those files are LZX compressed
    /// (header flag 0x80), which is why this project previously treated them as
    /// unreadable. They are readable: XNA's own ContentManager decompresses
    /// them, so no LZX decoder has to be ported and no Re-Logic asset has to be
    /// copied into this repository or the installer. The pixels come from the
    /// owner's copy, at the owner's version, on the owner's machine.
    ///
    /// The cost of that choice is honest and small: a window handle and a
    /// graphics device are needed, so the console must run as a 32-bit process
    /// (XNA is x86 only) and cannot extract on a machine with no display.
    /// </summary>
    internal static class SpriteStore
    {
        internal const string FolderName = "Sprites";
        private const string ManifestName = "manifest.txt";
        // Bump when the catalog, the crop rules or the pixel conversion change;
        // a stale cache is then rebuilt instead of being trusted.
        private const int FormatVersion = 1;

        internal static string Directory(string terrariaExePath)
        {
            if (string.IsNullOrEmpty(terrariaExePath)) return null;
            var gameDirectory = Path.GetDirectoryName(terrariaExePath);
            if (string.IsNullOrEmpty(gameDirectory)) return null;
            return Path.Combine(Path.Combine(gameDirectory, "Chaite"),
                FolderName);
        }

        private static string ContentDirectory(string terrariaExePath)
        {
            var gameDirectory = Path.GetDirectoryName(terrariaExePath);
            return string.IsNullOrEmpty(gameDirectory)
                ? null
                : Path.Combine(gameDirectory, "Content");
        }

        /// <summary>True when a complete, current cache is already on disk.</summary>
        internal static bool IsReady(string terrariaExePath)
        {
            var directory = Directory(terrariaExePath);
            if (directory == null) return false;
            string reason;
            return HasCompleteCache(directory, out reason);
        }

        /// <summary>
        /// Extracts any missing sprite. Safe to call repeatedly and safe to call
        /// when the game is not installed: it reports failure instead of
        /// throwing, because a console that cannot draw an icon must still be
        /// able to install the mod.
        /// </summary>
        internal static bool TryEnsure(string terrariaExePath, out string error)
        {
            error = null;
            var directory = Directory(terrariaExePath);
            var content = ContentDirectory(terrariaExePath);
            if (directory == null || content == null)
            {
                error = "尚未选择 Terraria.exe。";
                return false;
            }
            if (!System.IO.Directory.Exists(content))
            {
                error = "没有找到游戏的 Content 目录。";
                return false;
            }
            if (IsReady(terrariaExePath)) return true;
            return TryExtractTo(content, directory, out error);
        }

        /// <summary>The Content directory next to a Terraria.exe path.</summary>
        internal static string ContentDirectoryFor(string terrariaExePath)
        {
            return ContentDirectory(terrariaExePath);
        }

        /// <summary>
        /// Extracts the catalog from <paramref name="contentDirectory"/> into
        /// <paramref name="outputDirectory"/>.
        ///
        /// Split out from TryEnsure so the isolated checks can exercise the real
        /// decode path against the real game content while writing nothing into
        /// the owner's installation.
        /// </summary>
        internal static bool TryExtractTo(string contentDirectory,
            string outputDirectory, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(contentDirectory) ||
                !System.IO.Directory.Exists(contentDirectory))
            {
                error = "没有找到游戏的 Content 目录。";
                return false;
            }
            // A graphics device needs a window handle, and XNA requires an STA
            // thread, so the extraction gets its own rather than borrowing the
            // UI thread and freezing the window while it works.
            string failure = null;
            var thread = new Thread(delegate()
            {
                try { Extract(contentDirectory, outputDirectory); }
                catch (Exception ex) { failure = Describe(ex); }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null)
            {
                error = failure;
                return false;
            }
            return true;
        }

        private static string Describe(Exception ex)
        {
            // The interesting failure is always the innermost one: XNA wraps the
            // real "no graphics device" or "asset missing" cause.
            var inner = ex;
            while (inner.InnerException != null) inner = inner.InnerException;
            return inner.Message;
        }

        private static void Extract(string contentDirectory, string directory)
        {
            var staging = directory + ".tmp-" + Guid.NewGuid().ToString("N");
            System.IO.Directory.CreateDirectory(staging);
            try
            {
                using (var host = new ExtractionHost())
                using (var content = new ContentManager(host, contentDirectory))
                {
                    foreach (var definition in SpriteCatalog.All)
                    {
                        var texture = content.Load<Texture2D>(
                            definition.AssetPath);
                        try
                        {
                            WritePng(texture, definition,
                                Path.Combine(staging, definition.Key + ".png"));
                        }
                        finally
                        {
                            texture.Dispose();
                        }
                    }
                }
                // Only publish a complete set: a half-written cache would show
                // some icons and silently blank others forever.
                System.IO.Directory.CreateDirectory(directory);
                foreach (var file in System.IO.Directory.GetFiles(staging))
                    File.Copy(file, Path.Combine(directory,
                        Path.GetFileName(file)), true);
                File.WriteAllText(Path.Combine(directory, ManifestName),
                    BuildManifest(), new UTF8Encoding(false));
            }
            finally
            {
                try { System.IO.Directory.Delete(staging, true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static string BuildManifest()
        {
            var text = new StringBuilder();
            text.Append("chaite-sprites ").Append(FormatVersion)
                .Append('\n');
            foreach (var key in SpriteCatalog.Keys())
                text.Append(key).Append('\n');
            return text.ToString();
        }

        private static bool HasCompleteCache(string directory, out string reason)
        {
            reason = null;
            if (!System.IO.Directory.Exists(directory))
            {
                reason = "尚未提取本体贴图。";
                return false;
            }
            var manifest = Path.Combine(directory, ManifestName);
            if (!File.Exists(manifest))
            {
                reason = "贴图缓存缺少清单。";
                return false;
            }
            string actual;
            try { actual = File.ReadAllText(manifest, Encoding.UTF8); }
            catch (IOException) { reason = "贴图清单不可读。"; return false; }
            if (!string.Equals(actual, BuildManifest(), StringComparison.Ordinal))
            {
                reason = "贴图缓存来自旧版本。";
                return false;
            }
            foreach (var key in SpriteCatalog.Keys())
                if (!File.Exists(Path.Combine(directory, key + ".png")))
                {
                    reason = "贴图缓存缺少 " + key + "。";
                    return false;
                }
            return true;
        }

        /// <summary>
        /// Writes one sprite as a straight-alpha PNG.
        ///
        /// Two things are deliberate here. The frame is cropped, because a boss
        /// sheet is a tall strip of animation frames and the icon wants the
        /// first one. And the pixels are un-premultiplied, because XNA's
        /// content pipeline stores them premultiplied while PNG stores straight
        /// alpha; skipping that step darkens every soft edge.
        /// </summary>
        private static void WritePng(Texture2D texture,
            SpriteDefinition definition, string path)
        {
            var width = definition.FrameWidth > 0
                ? Math.Min(definition.FrameWidth, texture.Width)
                : texture.Width;
            var height = definition.FrameHeight > 0
                ? Math.Min(definition.FrameHeight, texture.Height)
                : texture.Height;

            var pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);

            using (var bitmap = new Bitmap(width, height,
                PixelFormat.Format32bppArgb))
            {
                var data = bitmap.LockBits(
                    new System.Drawing.Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    var row = new byte[width * 4];
                    for (var y = 0; y < height; y++)
                    {
                        for (var x = 0; x < width; x++)
                        {
                            var source = pixels[y * texture.Width + x];
                            var alpha = source.A;
                            byte red = source.R, green = source.G,
                                blue = source.B;
                            if (alpha != 0 && alpha != 255)
                            {
                                red = Unpremultiply(source.R, alpha);
                                green = Unpremultiply(source.G, alpha);
                                blue = Unpremultiply(source.B, alpha);
                            }
                            // Format32bppArgb is BGRA in memory.
                            row[x * 4] = blue;
                            row[x * 4 + 1] = green;
                            row[x * 4 + 2] = red;
                            row[x * 4 + 3] = alpha;
                        }
                        Marshal.Copy(row, 0,
                            new IntPtr(data.Scan0.ToInt64() +
                                (long)y * data.Stride), row.Length);
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }
                bitmap.Save(path, ImageFormat.Png);
            }
        }

        private static byte Unpremultiply(byte value, byte alpha)
        {
            var scaled = (value * 255 + alpha / 2) / alpha;
            return scaled > 255 ? (byte)255 : (byte)scaled;
        }

        /// <summary>
        /// Loads a cached sprite, or null when it has not been extracted. The
        /// caller shows a placeholder rather than failing: the console has to
        /// work before the first extraction has ever run.
        /// </summary>
        internal static Image Load(string terrariaExePath, string key)
        {
            var directory = Directory(terrariaExePath);
            if (directory == null) return null;
            var path = Path.Combine(directory, key + ".png");
            if (!File.Exists(path)) return null;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite))
                {
                    var loaded = Image.FromStream(stream, true, true);
                    // Copy out of the stream: the file handle must not outlive
                    // this call, and a lazy Image would keep it open.
                    var copy = new Bitmap(loaded.Width, loaded.Height,
                        PixelFormat.Format32bppArgb);
                    using (var graphics = Graphics.FromImage(copy))
                        graphics.DrawImageUnscaled(loaded, 0, 0);
                    loaded.Dispose();
                    return copy;
                }
            }
            catch (Exception)
            {
                // A corrupt cache entry is a missing icon, not a crash.
                return null;
            }
        }

        /// <summary>
        /// Supplies the graphics device the ContentManager needs. It exists only
        /// so a texture can be decoded and read back; nothing is ever drawn.
        /// </summary>
        private sealed class ExtractionHost : IServiceProvider,
            IGraphicsDeviceService, IDisposable
        {
            private readonly System.Windows.Forms.Form _window;
            private readonly GraphicsDevice _device;

            internal ExtractionHost()
            {
                _window = new System.Windows.Forms.Form();
                _window.ClientSize = new Size(8, 8);
                // Reading the handle forces window creation, which the device
                // needs even though the window is never shown.
                var handle = _window.Handle;
                var parameters = new PresentationParameters
                {
                    BackBufferWidth = 8,
                    BackBufferHeight = 8,
                    DeviceWindowHandle = handle,
                    IsFullScreen = false,
                    PresentationInterval = PresentInterval.Immediate,
                    RenderTargetUsage = RenderTargetUsage.PreserveContents
                };
                // Terraria's content is compiled for the HiDef profile; a Reach
                // device cannot load it.
                _device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter,
                    GraphicsProfile.HiDef, parameters);
            }

            public object GetService(Type serviceType)
            {
                return serviceType == typeof(IGraphicsDeviceService)
                    ? (object)this
                    : null;
            }

            public GraphicsDevice GraphicsDevice { get { return _device; } }

            public event EventHandler<EventArgs> DeviceCreated;
            public event EventHandler<EventArgs> DeviceDisposing;
            public event EventHandler<EventArgs> DeviceReset;
            public event EventHandler<EventArgs> DeviceResetting;

            public void Dispose()
            {
                if (_device != null) _device.Dispose();
                if (_window != null) _window.Dispose();
            }
        }
    }
}
