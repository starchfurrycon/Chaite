using Chaite.Core;
using System;
using System.IO;
using System.Web.Script.Serialization;

namespace Chaite.Plugin
{
    public sealed class ChaiteConfig
    {
        public string ActivateKey { get; set; } = "F8";
        public string EmergencyStopKey { get; set; } = "F9";
        public int ClearGraceTicks { get; set; } = 300;
        public int HitSoundCooldownTicks { get; set; } = 45;
        public bool AutoSwitchWeapon { get; set; } = true;
        public bool RestoreOriginalWeapon { get; set; } = true;
        public bool AutoQuickHeal { get; set; } = true;
        public bool AutoQuickMana { get; set; } = true;
        public bool ShowChatStatus { get; set; } = true;
        public int MaximumTargetDistancePixels { get; set; } = 2800;
        public int MaximumThreatDistancePixels { get; set; } = 2200;
        public PlannerSettings Planner { get; set; } = new PlannerSettings();

        public static ChaiteConfig LoadOrCreate(string path)
        {
            var serializer = new JavaScriptSerializer();
            if (File.Exists(path))
            {
                var loaded = serializer.Deserialize<ChaiteConfig>(File.ReadAllText(path));
                if (loaded != null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }

            var config = new ChaiteConfig();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, serializer.Serialize(config));
            return config;
        }

        private void Normalize()
        {
            Planner = Planner ?? new PlannerSettings();
            ClearGraceTicks = Math.Max(1, Math.Min(1800, ClearGraceTicks));
            HitSoundCooldownTicks = Math.Max(0, Math.Min(600, HitSoundCooldownTicks));
            MaximumTargetDistancePixels = Math.Max(256, Math.Min(8000, MaximumTargetDistancePixels));
            MaximumThreatDistancePixels = Math.Max(256, Math.Min(8000, MaximumThreatDistancePixels));
            Planner.HorizonTicks = Math.Max(6, Math.Min(90, Planner.HorizonTicks));
            Planner.SimulationStepTicks = Math.Max(1, Math.Min(6, Planner.SimulationStepTicks));
        }
    }
}
