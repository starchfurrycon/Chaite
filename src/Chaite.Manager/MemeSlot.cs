using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Chaite.Manager
{
    /// <summary>
    /// The user-supplied meme image slot, next to the game install.
    ///
    /// This mirrors the audio slots exactly: the project ships the folder and its
    /// README, never an image. The owner drops in whatever they have the right to
    /// use, and the console renders it. Nothing here downloads or generates art.
    ///
    /// Every failure mode is a skip, never a throw: a corrupt file, an unsupported
    /// codec or a locked file must not stop the installer from opening.
    /// </summary>
    internal static class MemeSlot
    {
        internal const string FolderName = "Memes";
        internal const int MaximumImages = 8;
        internal const int ThumbnailEdge = 96;

        private static readonly string[] Extensions =
            { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

        internal static string Directory(string terrariaExePath)
        {
            if (string.IsNullOrEmpty(terrariaExePath)) return null;
            var gameDirectory = Path.GetDirectoryName(terrariaExePath);
            return string.IsNullOrEmpty(gameDirectory)
                ? null
                : Path.Combine(gameDirectory, "Chaite", FolderName);
        }

        /// <summary>
        /// Top-level image paths, ordered by file name so a numeric prefix gives
        /// the owner deterministic control over what shows first.
        /// </summary>
        internal static IList<string> Discover(string terrariaExePath)
        {
            var result = new List<string>();
            var directory = Directory(terrariaExePath);
            if (directory == null || !System.IO.Directory.Exists(directory))
                return result;
            string[] files;
            try
            {
                files = System.IO.Directory.GetFiles(directory);
            }
            catch (Exception)
            {
                return result;
            }
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (var file in files)
            {
                if (result.Count >= MaximumImages) break;
                var extension = Path.GetExtension(file);
                var matched = false;
                foreach (var candidate in Extensions)
                    if (string.Equals(extension, candidate,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                if (matched) result.Add(file);
            }
            return result;
        }

        /// <summary>
        /// A display-sized copy, or null when the file cannot be decoded. The
        /// source is never modified and never kept open.
        /// </summary>
        internal static Image LoadThumbnail(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.ReadWrite))
                using (var source = Image.FromStream(stream, true, true))
                {
                    var scale = Math.Min(1d, (double)ThumbnailEdge /
                        Math.Max(1, Math.Max(source.Width, source.Height)));
                    var width = Math.Max(1, (int)Math.Round(source.Width * scale));
                    var height = Math.Max(1, (int)Math.Round(source.Height * scale));
                    var thumbnail = new Bitmap(width, height);
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {
                        graphics.InterpolationMode =
                            System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, 0, 0, width, height);
                    }
                    return thumbnail;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
