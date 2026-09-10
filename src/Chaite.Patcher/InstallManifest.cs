using System;
using System.Xml.Serialization;

namespace Chaite.Patcher
{
    [Serializable]
    [XmlRoot("ChaiteInstallation")]
    public sealed class InstallManifest
    {
        public string GameVersion { get; set; }
        public string OriginalSha256 { get; set; }
        public string PatchedSha256 { get; set; }
        public string BackupFile { get; set; }
        public DateTime InstalledUtc { get; set; }
        public string PatcherVersion { get; set; }
    }

    public enum InstallState
    {
        NotFound,
        CleanSupported,
        CleanUnsupported,
        Installed,
        InstalledButChanged,
        Invalid
    }

    public sealed class InstallStatus
    {
        public InstallState State { get; set; }
        public string TerrariaExe { get; set; }
        public string GameVersion { get; set; }
        public string Sha256 { get; set; }
        public string Message { get; set; }
    }
}

