using Chaite.Core;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections;
using System.Collections.Generic;
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
            Run(nameof(FormulaRoutesKeepBossAndWeatherBoundaries), FormulaRoutesKeepBossAndWeatherBoundaries);
            Run(nameof(FormulaMobilityContractReadsFrogLegDashAndRain), FormulaMobilityContractReadsFrogLegDashAndRain);
            Run(nameof(FormulaScriptIsDeterministic), FormulaScriptIsDeterministic);
            Run(nameof(FormulaScriptReadsBossSpecificClocks), FormulaScriptReadsBossSpecificClocks);
            Run(nameof(UnsupportedBossCueRemainsDistinct), UnsupportedBossCueRemainsDistinct);
            Run(nameof(LegacyAudioCueOrdinalsRemainStable),
                LegacyAudioCueOrdinalsRemainStable);
            Run(nameof(AudioCuePlayerMapsUnsupportedBossToUserSlot),
                AudioCuePlayerMapsUnsupportedBossToUserSlot);
            Run(nameof(FirstCueWaitsForPreloadAndPlaysOnce),
                FirstCueWaitsForPreloadAndPlaysOnce);
            Run(nameof(RuntimeUsesFixedScopeMessages),
                RuntimeUsesFixedScopeMessages);
            Run(nameof(AudioReadmeListsEveryMappedSlot),
                AudioReadmeListsEveryMappedSlot);
        }

        /// <summary>
        /// The shipped slot README and the player's mapping must agree in both
        /// directions. A cue wired to an undocumented file is invisible to the
        /// owner; a documented file nothing plays is a slot they fill for
        /// nothing. Neither shows up as a failing cue at runtime.
        /// </summary>
        private static void AudioReadmeListsEveryMappedSlot()
        {
            var root = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(root) &&
                !File.Exists(Path.Combine(root, "Chaite.sln")))
                root = Path.GetDirectoryName(root);
            True(!string.IsNullOrEmpty(root), "repository root not found");

            var readme = Path.Combine(root, "src", "Chaite.Plugin", "Audio",
                "README.txt");
            True(File.Exists(readme), "audio slot README is missing: " + readme);
            var documented = new SortedSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match match in
                System.Text.RegularExpressions.Regex.Matches(
                    File.ReadAllText(readme), @"[A-Za-z0-9_]+\.wav"))
                documented.Add(match.Value);

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
            // A no-op preload queue keeps this test off the thread pool.
            var player = ctor.Invoke(new object[]
            {
                Path.GetTempPath(), (Action<Action>)(_ => { }),
                (Action<SoundPlayer>)(_ => { })
            });
            var files = playerType.GetField("_files",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
            var mapped = new SortedSet<string>(StringComparer.Ordinal);
            foreach (System.Collections.DictionaryEntry entry in
                (System.Collections.IDictionary)files)
                mapped.Add((string)entry.Value);

            var undocumented = new List<string>();
            foreach (var file in mapped)
                if (!documented.Contains(file)) undocumented.Add(file);
            Equal(0, undocumented.Count);

            var unplayed = new List<string>();
            foreach (var file in documented)
                if (!mapped.Contains(file)) unplayed.Add(file);
            Equal(0, unplayed.Count);

            // Every cue in the enum must either be the explicit no-op or have a
            // slot, otherwise a state can silently produce no sound at all.
            var cues = new SortedSet<string>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in (IDictionary)files)
                cues.Add(entry.Key.ToString());
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                if (cue == AudioCue.None) continue;
                True(cues.Contains(cue.ToString()),
                    "audio cue has no slot: " + cue);
            }
        }

        private static void UnsupportedBossCueRemainsDistinct()
        {
            // Keep the old joke slots distinct when the refusal cue is added;
            // each legacy cue remains mapped to its own local WAV below.
            True(AudioCue.UnsupportedBoss != AudioCue.NoSlimeAng);
            True(AudioCue.UnsupportedBoss != AudioCue.LowLevelChaite);
            Equal("UnsupportedBoss", AudioCue.UnsupportedBoss.ToString());
        }

        private static void FormulaRoutesKeepBossAndWeatherBoundaries()
        {
            Equal(FormulaRoute.FishronFairyWingsDash, FormulaRouteCatalog.Select(370, 761, 3097, false, true, -1, false));
            Equal(FormulaRoute.FishronStrongWingsDash, FormulaRouteCatalog.Select(370, 2609, 984, false, true, -1, false));
            // Duke Fishron is the only admitted Boss. NPC 636 is no longer
            // supported at all, so even its formerly reviewed wing loadout
            // falls through to None instead of selecting a route.
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(636, 2609, 0, true, true, -1, false));
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(636, 761, 3097, false, true, -1, false));
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 50, false));
            // The Queen Slime saddle (mount 50) was withdrawn from the Fishron
            // admission set on 2026-09-21, so it is now refused exactly like any
            // other unreviewed mount rather than selecting its own route.
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 50, true));
            Equal(FormulaRoute.FishronTrustyChillet, FormulaRouteCatalog.Select(370, 0, 0, false, false, 64, false));
            // Mount 65 is a reskin of mount 64, not a second loadout, so it
            // selects the same route instead of its own. Training it twice
            // would have cost a full run for a different skin.
            Equal(FormulaRoute.FishronTrustyChillet, FormulaRouteCatalog.Select(370, 0, 0, false, false, 65, false));
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 62, false));
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 63, false));
            // Mount 23 (the Witch Broom) was the Empress of Light's formula
            // mount. NPC 636 is out of scope, so the mount is refused for the
            // only admitted Boss exactly like any other unreviewed mount.
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 23, false));
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 12, false));
            // Mount 12 in the rain used to select the Shrimpy Truffle route. That
            // route was withdrawn as out of scope, so weather no longer changes
            // the answer and the loadout is refused either way.
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(370, 0, 0, false, false, 12, true));
            Equal(FormulaRoute.None, FormulaRouteCatalog.Select(4, 2609, 3097, false, true, -1, true));
        }

        private static void FormulaMobilityContractReadsFrogLegDashAndRain()
        {
            var fishron = CombatScenario(370);
            fishron.Player.FunctionalEquipmentIdentityKnown = true;
            fishron.Player.WingAccessoryItemType = 761;
            fishron.Mobility.FormulaAccessoryScanKnown = true;
            fishron.Mobility.FrogLegAccessoryKnown = true;
            fishron.Mobility.FrogLegAccessoryPresent = true;
            var dash = fishron.Mobility.EyeShieldDash;
            dash.EquipmentIdentity =
                DashEquipmentIdentity.ShieldOfCthulhuItem3097;
            fishron.Mobility.EyeShieldDash = dash;
            FormulaRoute route;
            string reason;
            True(FormulaMobilityContract.TrySelectRoute(fishron, 370,
                out route, out reason), reason);
            Equal(FormulaRoute.FishronFairyWingsDash, route);

            fishron.Player.WingAccessoryItemType = 2609;
            dash.EquipmentIdentity =
                DashEquipmentIdentity.MasterNinjaGearItem984;
            fishron.Mobility.EyeShieldDash = dash;
            True(FormulaMobilityContract.TrySelectRoute(fishron, 370,
                out route, out reason), reason);
            Equal(FormulaRoute.FishronStrongWingsDash, route);

            // The Crystal Assassin set counts as a dash source for the same
            // strong-wing route, on the only admitted Boss.
            var crystalAssassin = CombatScenario(370);
            crystalAssassin.Player.FunctionalEquipmentIdentityKnown = true;
            crystalAssassin.Player.WingAccessoryItemType = 2609;
            crystalAssassin.Mobility.FormulaAccessoryScanKnown = true;
            crystalAssassin.Mobility.FrogLegAccessoryKnown = true;
            crystalAssassin.Mobility.FrogLegAccessoryPresent = true;
            dash.EquipmentIdentity =
                DashEquipmentIdentity.CrystalAssassinArmorSet;
            crystalAssassin.Mobility.EyeShieldDash = dash;
            True(FormulaMobilityContract.TrySelectRoute(crystalAssassin, 370,
                out route, out reason), reason);
            Equal(FormulaRoute.FishronStrongWingsDash, route);

            // Mount 12, rain or not, is no longer a reviewed loadout: the
            // Shrimpy Truffle route was withdrawn as out of scope, so selection
            // refuses it instead of producing a route. Rain must not rescue it.
            crystalAssassin.Player.WingAccessoryItemType = 0;
            crystalAssassin.Mobility.SelectedMountIdentityKnown = true;
            crystalAssassin.Mobility.SelectedMountType = 12;
            crystalAssassin.Difficulty.RainKnown = true;
            crystalAssassin.Difficulty.Rain = true;
            False(FormulaMobilityContract.TrySelectRoute(crystalAssassin, 370,
                out route, out reason));
            crystalAssassin.Difficulty.Rain = false;
            False(FormulaMobilityContract.TrySelectRoute(crystalAssassin, 370,
                out route, out reason));
        }

        private static void FormulaScriptIsDeterministic()
        {
            var input = new FormulaScriptInput
            {
                BossType = 370, Route = FormulaRoute.FishronStrongWingsDash,
                NativeState = 6, NativeTimer = 20, PlayerBelowBoss = true,
                PlayerRightOfBoss = false, NativeFormKnown = true
            };
            var a = FormulaScriptController.Tick(in input);
            var b = FormulaScriptController.Tick(in input);
            True(a.Accepted && a.Fire && a.Jump && a.Dash);
            Equal(a.Horizontal, b.Horizontal); Equal(a.Vertical, b.Vertical);
            Equal(a.Phase, b.Phase);
            input.BossType = 4;
            False(FormulaScriptController.Tick(in input).Accepted);
        }

        private static void FormulaScriptReadsBossSpecificClocks()
        {
            var target = new TargetSnapshot { Type = 370, Ai0 = 1, Ai1 = 39,
                Ai2 = 2, Ai3 = 0, Ai0Known = true, Ai1Known = true,
                Ai2Known = true, Ai3Known = true };
            var player = new PlayerSnapshot();
            FormulaScriptInput input;
            True(FormulaScriptController.TryReadInput(in target,
                FormulaRoute.FishronStrongWingsDash, player, out input));
            Equal(2, input.NativeTimer); Equal(0, input.NativeSequence);
            True(input.NativeFormKnown); Equal(0, input.NativeForm);
            target.Type = 4;
            False(FormulaScriptController.TryReadInput(in target,
                FormulaRoute.FishronStrongWingsDash, player, out input));
            target.Type = 370; target.Ai0 = 1; target.Ai2 = 27; target.Ai3 = 6;
            True(FormulaScriptController.TryReadInput(in target,
                FormulaRoute.FishronStrongWingsDash, player, out input));
            Equal(27, input.NativeTimer); Equal(6, input.NativeSequence);
            target.Ai2 = float.NaN;
            False(FormulaScriptController.TryReadInput(in target,
                FormulaRoute.FishronStrongWingsDash, player, out input));
            target.Ai2 = 1.5f;
            False(FormulaScriptController.TryReadInput(in target,
                FormulaRoute.FishronStrongWingsDash, player, out input));
            target.Ai2 = 1; target.Ai3Known = false;
            False(FormulaScriptController.TryReadInput(in target,
                FormulaRoute.FishronStrongWingsDash, player, out input));
            input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronStrongWingsDash, NativeState = 13 };
            False(FormulaScriptController.Tick(in input).Accepted);
            input.NativeState = 12;
            True(FormulaScriptController.Tick(in input).Accepted);
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
                Equal("never_tried_this_loadout.wav", (string)map[AudioCue.UntestedLoadout]);
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
