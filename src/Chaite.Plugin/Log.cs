using System;
using System.IO;

namespace Chaite.Plugin
{
    internal sealed class Log
    {
        private readonly string _path;

        public Log(string path)
        {
            _path = path;
        }

        public void Write(string message)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > 1024 * 1024)
                {
                    var previous = _path + ".previous";
                    if (File.Exists(previous))
                        File.Delete(previous);
                    File.Move(_path, previous);
                }
                File.AppendAllText(_path, DateTime.Now.ToString("s") + " " + message + Environment.NewLine);
            }
            catch
            {
                // Logging must never take down Terraria.
            }
        }
    }
}
