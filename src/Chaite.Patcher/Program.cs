using System;
using System.IO;

namespace Chaite.Patcher
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var command = args.Length > 0 ? args[0].ToLowerInvariant() : "status";
                var exe = GetArgument(args, "--terraria") ?? TerrariaLocator.FindTerrariaExe();
                var payload = GetArgument(args, "--payload") ?? AppDomain.CurrentDomain.BaseDirectory;
                var service = new InstallationService();
                InstallStatus status;

                switch (command)
                {
                    case "install":
                        status = service.Install(exe, payload);
                        break;
                    case "restore":
                    case "uninstall":
                        status = service.Restore(exe);
                        break;
                    case "status":
                        status = service.GetStatus(exe);
                        break;
                    default:
                        Console.Error.WriteLine("用法: Chaite.Patcher [status|install|restore] [--terraria <Terraria.exe>] [--payload <目录>]");
                        return 2;
                }

                Console.WriteLine(status.Message);
                Console.WriteLine("路径: " + (status.TerrariaExe ?? "未找到"));
                if (!string.IsNullOrEmpty(status.GameVersion)) Console.WriteLine("版本: " + status.GameVersion);
                if (!string.IsNullOrEmpty(status.Sha256)) Console.WriteLine("SHA-256: " + status.Sha256);
                return status.State == InstallState.Invalid || status.State == InstallState.NotFound || status.State == InstallState.CleanUnsupported ? 1 : 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("错误: " + ex.Message);
                return 1;
            }
        }

        private static string GetArgument(string[] args, string name)
        {
            for (var i = 0; i + 1 < args.Length; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(args[i + 1]);
            return null;
        }
    }
}

