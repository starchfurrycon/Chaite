using Chaite.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunAudioCueRegressions()
        {
            Run(nameof(UnsupportedBossCueRemainsDistinct), UnsupportedBossCueRemainsDistinct);
            Run(nameof(LegacyAudioCueOrdinalsRemainStable),
                LegacyAudioCueOrdinalsRemainStable);
            Run(nameof(AudioCuePlayerMapsUnsupportedBossToUserSlot),
                AudioCuePlayerMapsUnsupportedBossToUserSlot);
            Run(nameof(FirstCueWaitsForPreloadAndPlaysOnce),
                FirstCueWaitsForPreloadAndPlaysOnce);
            Run(nameof(RuntimeUsesFixedScopeMessages),
                RuntimeUsesFixedScopeMessages);
        }

        private static void UnsupportedBossCueRemainsDistinct()
        {
            // Keep the old joke slots distinct when the refusal cue is added;
            // each legacy cue remains mapped to its own local WAV below.
            True(AudioCue.UnsupportedBoss != AudioCue.NoSlimeAng);
            True(AudioCue.UnsupportedBoss != AudioCue.LowLevelChaite);
            Equal("UnsupportedBoss", AudioCue.UnsupportedBoss.ToString());
        }

        private static void LegacyAudioCueOrdinalsRemainStable()
        {
            Equal(0, (int)AudioCue.None);
            Equal(1, (int)AudioCue.NoSlimeAng);
            Equal(2, (int)AudioCue.TryMinnie);
            Equal(3, (int)AudioCue.Man);
            Equal(4, (int)AudioCue.Dead);
            Equal(5, (int)AudioCue.MambaOut);
            Equal(6, (int)AudioCue.FailedBossDesign);
            Equal(7, (int)AudioCue.LowLevelChaite);
            Equal(8, (int)AudioCue.UnsupportedBoss);
        }

        private static void AudioCuePlayerMapsUnsupportedBossToUserSlot()
        {
            var playerType = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.AudioCuePlayer", true);
            var ctor = playerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            True(ctor != null, "AudioCuePlayer(string) missing");

            var directory = Path.Combine(Path.GetTempPath(),
                "chaite-audio-cue-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var player = ctor.Invoke(new object[] { directory });
                var files = playerType.GetField("_files",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                True(files != null, "AudioCuePlayer file map missing");
                var map = files.GetValue(player) as IDictionary;
                True(map != null, "AudioCuePlayer file map is not a dictionary");
                True(map.Contains(AudioCue.UnsupportedBoss),
                    "unsupported Boss cue is not registered");
                Equal("boss_too_hard_for_me.wav",
                    (string)map[AudioCue.UnsupportedBoss]);
                Equal("no_slime_ang.wav", (string)map[AudioCue.NoSlimeAng]);
                Equal("try_minnie.wav", (string)map[AudioCue.TryMinnie]);
                Equal("man.wav", (string)map[AudioCue.Man]);
                Equal("dead.wav", (string)map[AudioCue.Dead]);
                Equal("mamba_out.wav", (string)map[AudioCue.MambaOut]);
                Equal("failed_boss_design.wav",
                    (string)map[AudioCue.FailedBossDesign]);
                Equal("low_level_chaite.wav",
                    (string)map[AudioCue.LowLevelChaite]);
                // A fresh test directory must remain empty: the player only
                // reads user-provided WAVs and never synthesizes/downloads one.
                Equal(0, Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        private static void FirstCueWaitsForPreloadAndPlaysOnce()
        {
            var playerType = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.AudioCuePlayer", true);
            var ctor = playerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic,
                null, new[]
                {
                    typeof(string), typeof(Action<Action>),
                    typeof(Action<SoundPlayer>)
                }, null);
            True(ctor != null, "controllable AudioCuePlayer constructor missing");

            var directory = Path.Combine(Path.GetTempPath(),
                "chaite-audio-preload-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var wave = Path.Combine(directory, "no_slime_ang.wav");
            WriteSilentPcmWave(wave);
            Action preload = null;
            var playCount = 0;
            object player = null;
            try
            {
                player = ctor.Invoke(new object[]
                {
                    directory,
                    (Action<Action>)(action => preload = action),
                    (Action<SoundPlayer>)(_ => playCount++)
                });
                True(preload != null, "preload work was not queued");
                var play = playerType.GetMethod("Play",
                    BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic);
                Equal(null, (string)play.Invoke(player,
                    new object[] { AudioCue.NoSlimeAng }));
                Equal(null, (string)play.Invoke(player,
                    new object[] { AudioCue.NoSlimeAng }));
                Equal(0, playCount);

                var pendingField = playerType.GetField("_pending",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var pending = pendingField.GetValue(player);
                var pendingCount = pending.GetType().GetProperty("Count");
                Equal(1, (int)pendingCount.GetValue(pending, null));

                preload();
                Equal(1, playCount);
                Equal(0, (int)pendingCount.GetValue(pending, null));
                Equal(null, (string)play.Invoke(player,
                    new object[] { AudioCue.NoSlimeAng }));
                Equal(2, playCount);
            }
            finally
            {
                if (player != null)
                {
                    var playersField = playerType.GetField("_players",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    var players = playersField.GetValue(player) as IDictionary;
                    if (players != null)
                        foreach (DictionaryEntry entry in players)
                            (entry.Value as IDisposable)?.Dispose();
                }
                if (File.Exists(wave)) File.Delete(wave);
                if (Directory.Exists(directory)) Directory.Delete(directory, false);
            }
        }

        private static void RuntimeUsesFixedScopeMessages()
        {
            using (var module = ModuleDefinition.ReadModule(
                typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var runtime = module.Types.Single(type =>
                    type.FullName == "Chaite.Plugin.Runtime");
                var reject = runtime.Methods.Single(method =>
                    method.Name == "RejectUnsupportedBoss");
                var rejectStrings = reject.Body.Instructions
                    .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                    .Select(instruction => (string)instruction.Operand)
                    .ToArray();
                Equal(1, rejectStrings.Count(value => value ==
                    "这个波斯可是超囊的对我来说"));
                False(rejectStrings.Any(value => value.Contains(
                    "拆特当前只接管")));

                var activate = runtime.Methods.Single(method =>
                    method.Name == "ActivatePreparedSession");
                var activationStrings = activate.Body.Instructions
                    .Where(instruction => instruction.OpCode == OpCodes.Ldstr)
                    .Select(instruction => (string)instruction.Operand)
                    .ToArray();
                Equal(1, activationStrings.Count(value => value ==
                    "我没有史莱姆 ang 啊"));
                False(activationStrings.Any(value => value.Contains(
                    "自然 Boss")));
            }
        }

        private static void WriteSilentPcmWave(string path)
        {
            using (var writer = new BinaryWriter(File.Create(path),
                Encoding.ASCII))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(38);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(8000);
                writer.Write(16000);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(2);
                writer.Write((short)0);
            }
        }
    }
}
