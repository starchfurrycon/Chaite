using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Chaite.Patcher
{
    public static class TerrariaLocator
    {
        public static string FindTerrariaExe()
        {
            foreach (var root in SteamRoots())
            {
                var direct = Path.Combine(root, "steamapps", "common", "Terraria", "Terraria.exe");
                if (File.Exists(direct))
                    return direct;

                var libraries = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraries))
                    continue;
                foreach (Match match in Regex.Matches(File.ReadAllText(libraries), "\\\"path\\\"\\s+\\\"(?<path>[^\\\"]+)\\\""))
                {
                    var library = match.Groups["path"].Value.Replace("\\\\", "\\");
                    var candidate = Path.Combine(library, "steamapps", "common", "Terraria", "Terraria.exe");
                    if (File.Exists(candidate))
                        return candidate;
                }
            }
            return null;
        }

        private static IEnumerable<string> SteamRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    var value = key?.GetValue("InstallPath") as string;
                    if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
                        yield return value;
                }
            }

            foreach (var common in new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                @"D:\Program Files (x86)\Steam",
                @"D:\SteamLibrary"
            })
            {
                if (Directory.Exists(common) && seen.Add(common))
                    yield return common;
            }
        }
    }
}

