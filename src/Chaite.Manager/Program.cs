using System;
using System.Windows.Forms;

namespace Chaite.Manager
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 0 && args[0] == "--ui-smoke")
                return UiSmokeTest.Run(args.Length > 1 ? args[1] : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ui-smoke"));
            Application.Run(new MainForm());
            return 0;
        }
    }
}
