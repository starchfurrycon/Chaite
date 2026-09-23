using Chaite.Patcher;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPatcherRegressions()
        {
            Run(nameof(PatcherPlacesFiveHooksWithoutModifyingSource), PatcherPlacesFiveHooksWithoutModifyingSource);
            Run(nameof(PatcherRejectsMissingOrAmbiguousInputCopy), PatcherRejectsMissingOrAmbiguousInputCopy);
            Run(nameof(PatcherRejectsMissingOrAmbiguousHorizontalMovement), PatcherRejectsMissingOrAmbiguousHorizontalMovement);
            Run(nameof(PatcherRejectsUnreviewedNativeInputOrder), PatcherRejectsUnreviewedNativeInputOrder);
            Run(nameof(PatcherRejectsInPlaceTestAndDoubleInjection), PatcherRejectsInPlaceTestAndDoubleInjection);
            Run(nameof(PatcherWidensBranchesWithoutRedirectingBypass), PatcherWidensBranchesWithoutRedirectingBypass);
            Run(nameof(InstallPayloadRefreshesReadmeAndPreservesUserFiles),
                InstallPayloadRefreshesReadmeAndPreservesUserFiles);
            Run(nameof(InstalledUpgradeRepatchesAndPreservesUserFiles),
                InstalledUpgradeRepatchesAndPreservesUserFiles);
        }

        private static void PatcherPlacesFiveHooksWithoutModifyingSource()
        {
            WithPatchFixture((source, output) =>
            {
                WriteSyntheticGame(source);
                var before = TestHash(source);
                new InstallationService().PatchCopyForTest(source, typeof(Chaite.Plugin.Runtime).Assembly.Location, output);
                Equal(before, TestHash(source));
                using (var patched = ModuleDefinition.ReadModule(output))
                {
                    var update = patched.Types.Single(t => t.FullName == "Terraria.Player").Methods.Single(m => m.Name == "Update");
                    var loot = patched.Types.Single(t => t.FullName == "Terraria.NPC").Methods.Single(m => m.Name == "NPCLoot");
                    Equal(1, CountCalls(update, "Chaite.Plugin.Runtime", "Tick"));
                    Equal(1, CountCalls(update, "Chaite.Plugin.Runtime", "ApplyPendingInput"));
                    Equal(1, CountCalls(update, "Chaite.Plugin.Runtime", "ValidatePendingMobility"));
                    Equal(1, CountCalls(update, "Chaite.Plugin.Runtime", "ApplyPendingSelection"));
                    Equal(1, CountCalls(loot, "Chaite.Plugin.Runtime", "OnNpcKilled"));
                    Equal("Tick", ((MethodReference)update.Body.Instructions[2].Operand).Name);
                    var copy = update.Body.Instructions.Single(i => MethodCall(i, "Terraria.GameInput.TriggersSet", "CopyInto"));
                    Equal(OpCodes.Ldarg_0, copy.Next.OpCode);
                    True(MethodCall(copy.Next.Next, "Chaite.Plugin.Runtime", "ApplyPendingInput"));
                    var horizontal = update.Body.Instructions.Single(i => MethodCall(i,
                        "Terraria.Player", "HorizontalMovement"));
                    True(MethodCall(horizontal.Previous,
                        "Chaite.Plugin.Runtime", "ValidatePendingMobility"));
                    Equal(OpCodes.Ldarg_0, horizontal.Previous.Previous.OpCode);
                    var hotbar = update.Body.Instructions.Single(i => MethodCall(i, "Terraria.Player", "HandleHotbarControls"));
                    Equal(OpCodes.Ldarg_0, hotbar.Next.OpCode);
                    True(MethodCall(hotbar.Next.Next, "Chaite.Plugin.Runtime", "ApplyPendingSelection"));
                    True(update.Body.Instructions.IndexOf(copy.Next.Next) < update.Body.Instructions.IndexOf(hotbar));
                    True(update.Body.Instructions.IndexOf(hotbar.Next.Next) < update.Body.Instructions.IndexOf(horizontal));
                }
            });
        }

        private static void PatcherRejectsMissingOrAmbiguousInputCopy()
        {
            foreach (var count in new[] { 0, 2 })
                WithPatchFixture((source, output) =>
                {
                    WriteSyntheticGame(source, count);
                    var before = TestHash(source);
                    Throws<InvalidDataException>(() => new InstallationService().PatchCopyForTest(source,
                        typeof(Chaite.Plugin.Runtime).Assembly.Location, output));
                    False(File.Exists(output));
                    Equal(before, TestHash(source));
                });
        }

        private static void PatcherRejectsMissingOrAmbiguousHorizontalMovement()
        {
            foreach (var count in new[] { 0, 2 })
                WithPatchFixture((source, output) =>
                {
                    WriteSyntheticGame(source, 1, false, count);
                    var before = TestHash(source);
                    Throws<InvalidDataException>(() => new InstallationService().PatchCopyForTest(source,
                        typeof(Chaite.Plugin.Runtime).Assembly.Location, output));
                    False(File.Exists(output));
                    Equal(before, TestHash(source));
                });
        }

        private static void PatcherRejectsUnreviewedNativeInputOrder()
        {
            WithPatchFixture((source, output) =>
            {
                WriteSyntheticGame(source, 1, false, 1, true);
                var before = TestHash(source);
                Throws<InvalidDataException>(() => new InstallationService()
                    .PatchCopyForTest(source,
                        typeof(Chaite.Plugin.Runtime).Assembly.Location,
                        output));
                False(File.Exists(output));
                Equal(before, TestHash(source));
            });
        }

        private static void PatcherRejectsInPlaceTestAndDoubleInjection()
        {
            WithPatchFixture((source, output) =>
            {
                WriteSyntheticGame(source);
                var service = new InstallationService();
                var plugin = typeof(Chaite.Plugin.Runtime).Assembly.Location;
                var before = TestHash(source);
                Throws<InvalidOperationException>(() => service.PatchCopyForTest(source, plugin, source));
                Equal(before, TestHash(source));
                service.PatchCopyForTest(source, plugin, output);
                // Deliberately use the original fixture as the proposed output. Rejection occurs
                // before writing anything and its hash must remain unchanged.
                Throws<InvalidOperationException>(() => service.PatchCopyForTest(output, plugin, source));
                Equal(before, TestHash(source));
            });
        }

        private static void PatcherWidensBranchesWithoutRedirectingBypass()
        {
            WithPatchFixture((source, output) =>
            {
                WriteSyntheticGame(source, 1, true);
                new InstallationService().PatchCopyForTest(source, typeof(Chaite.Plugin.Runtime).Assembly.Location, output);
                using (var patched = ModuleDefinition.ReadModule(output))
                {
                    var update = patched.Types.Single(t => t.FullName == "Terraria.Player").Methods.Single(m => m.Name == "Update");
                    var branch = update.Body.Instructions.Single(i => i.OpCode == OpCodes.Br);
                    var copy = update.Body.Instructions.Single(i => MethodCall(i, "Terraria.GameInput.TriggersSet", "CopyInto"));
                    var hotbar = update.Body.Instructions.Single(i => MethodCall(i,
                        "Terraria.Player", "HandleHotbarControls"));
                    True(ReferenceEquals(hotbar.Previous, branch.Operand),
                        "a branch bypassing the original CopyInto must still bypass the replay hook");
                    False(update.Body.Instructions.Any(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget));
                }
            });
        }

        private static void InstallPayloadRefreshesReadmeAndPreservesUserFiles()
        {
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "patcher-payload-fixture-" + Guid.NewGuid().ToString("N"));
            var payload = Path.Combine(root, "payload");
            var payloadAudio = Path.Combine(payload, "Audio");
            var payloadMemes = Path.Combine(payload, "Memes");
            var data = Path.Combine(root, "game-data");
            var dataAudio = Path.Combine(data, "Audio");
            var dataMemes = Path.Combine(data, "Memes");
            Directory.CreateDirectory(payloadAudio);
            Directory.CreateDirectory(payloadMemes);
            Directory.CreateDirectory(dataAudio);
            Directory.CreateDirectory(dataMemes);
            var payloadConfig = Path.Combine(payload, "config.json");
            var installedConfig = Path.Combine(data, "config.json");
            var payloadReadme = Path.Combine(payloadAudio, "README.txt");
            var installedReadme = Path.Combine(dataAudio, "README.txt");
            var payloadMemesReadme = Path.Combine(payloadMemes, "README.txt");
            var installedMemesReadme = Path.Combine(dataMemes, "README.txt");
            var existingWave = Path.Combine(dataAudio, "custom.wav");
            var extraWave = Path.Combine(dataAudio, "keep.wav");
            var payloadWave = Path.Combine(payloadAudio, "custom.wav");
            // The owner's own meme image must survive an upgrade exactly like a WAV.
            var userMeme = Path.Combine(dataMemes, "10-ezfic.png");
            var createdFiles = new[]
            {
                payloadConfig, installedConfig, payloadReadme,
                installedReadme, payloadMemesReadme, installedMemesReadme,
                existingWave, extraWave, payloadWave, userMeme
            };
            try
            {
                File.WriteAllText(payloadConfig, "package-config");
                File.WriteAllText(installedConfig, "user-config");
                File.WriteAllText(payloadReadme, "new-slot-contract");
                File.WriteAllText(installedReadme, "old-slot-contract");
                File.WriteAllText(payloadMemesReadme, "new-meme-contract");
                File.WriteAllText(installedMemesReadme, "old-meme-contract");
                File.WriteAllBytes(existingWave, new byte[] { 1, 2, 3 });
                File.WriteAllBytes(extraWave, new byte[] { 4, 5, 6 });
                File.WriteAllBytes(payloadWave, new byte[] { 7, 8, 9 });
                File.WriteAllBytes(userMeme, new byte[] { 11, 12, 13 });

                var copy = typeof(InstallationService).GetMethod(
                    "CopyUserDataPayload", System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.NonPublic);
                True(copy != null, "payload copy policy helper missing");
                copy.Invoke(null, new object[] { payload, data });

                Equal("new-slot-contract", File.ReadAllText(installedReadme));
                Equal("new-meme-contract", File.ReadAllText(installedMemesReadme));
                Equal("user-config", File.ReadAllText(installedConfig));
                Equal("01-02-03", BitConverter.ToString(
                    File.ReadAllBytes(existingWave)));
                Equal("04-05-06", BitConverter.ToString(
                    File.ReadAllBytes(extraWave)));
                Equal("0B-0C-0D", BitConverter.ToString(
                    File.ReadAllBytes(userMeme)));
                Equal(2, Directory.GetFiles(dataAudio, "*.wav").Length);
                Equal(1, Directory.GetFiles(dataMemes, "*.png").Length);
            }
            finally
            {
                foreach (var file in createdFiles)
                    if (File.Exists(file)) File.Delete(file);
                if (Directory.Exists(payloadAudio))
                    Directory.Delete(payloadAudio, false);
                if (Directory.Exists(payloadMemes))
                    Directory.Delete(payloadMemes, false);
                if (Directory.Exists(payload)) Directory.Delete(payload, false);
                if (Directory.Exists(dataAudio)) Directory.Delete(dataAudio, false);
                if (Directory.Exists(dataMemes)) Directory.Delete(dataMemes, false);
                if (Directory.Exists(data)) Directory.Delete(data, false);
                if (Directory.Exists(root)) Directory.Delete(root, false);
            }
        }

        private static void InstalledUpgradeRepatchesAndPreservesUserFiles()
        {
            var root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "patcher-upgrade-fixture-" + Guid.NewGuid().ToString("N"));
            var game = Path.Combine(root, "game");
            var payload = Path.Combine(root, "payload");
            var payloadAudio = Path.Combine(payload, "Audio");
            var payloadMemes = Path.Combine(payload, "Memes");
            var data = Path.Combine(game, "Chaite");
            var installedAudio = Path.Combine(data, "Audio");
            var installedMemes = Path.Combine(data, "Memes");
            Directory.CreateDirectory(game);
            Directory.CreateDirectory(payloadAudio);
            Directory.CreateDirectory(payloadMemes);
            Directory.CreateDirectory(installedAudio);

            var original = Path.Combine(root, "Terraria.original.exe");
            var terraria = Path.Combine(game, "Terraria.exe");
            var backupName = "Terraria.original.backup";
            var backup = Path.Combine(data, backupName);
            var pluginSource = Path.Combine(payload, "Chaite.Plugin.dll");
            var coreSource = Path.Combine(payload, "Chaite.Core.dll");
            var installedPlugin = Path.Combine(game, "Chaite.Plugin.dll");
            var installedCore = Path.Combine(game, "Chaite.Core.dll");
            var userConfig = Path.Combine(data, "config.json");
            var readme = Path.Combine(installedAudio, "README.txt");
            var userWave = Path.Combine(installedAudio, "boss_too_hard_for_me.wav");
            try
            {
                WriteSyntheticGame(original);
                File.Copy(original, backup, false);
                File.Copy(typeof(Chaite.Plugin.Runtime).Assembly.Location,
                    pluginSource, false);
                File.Copy(typeof(Chaite.Core.CombatPlanner).Assembly.Location,
                    coreSource, false);
                File.WriteAllText(Path.Combine(payload, "config.json"),
                    "package-config");
                File.WriteAllText(Path.Combine(payloadAudio, "README.txt"),
                    "new-scope-slots");
                // The meme slot README is part of the payload contract now: an
                // upgrade must refresh it exactly like the audio one, without
                // touching the owner's images.
                Directory.CreateDirectory(installedMemes);
                File.WriteAllText(Path.Combine(payloadMemes, "README.txt"),
                    "new-meme-slots");
                File.WriteAllText(Path.Combine(installedMemes, "README.txt"),
                    "old-meme-slots");
                File.WriteAllText(userConfig, "user-config");
                File.WriteAllText(readme, "old-scope-slots");
                File.WriteAllBytes(userWave, new byte[] { 9, 8, 7, 6 });
                File.WriteAllText(installedPlugin, "old-plugin");
                File.WriteAllText(installedCore, "old-core");

                var service = new InstallationService();
                service.PatchCopyForTest(original, pluginSource, terraria);
                var manifest = new InstallManifest
                {
                    GameVersion = InstallationService.SupportedVersion,
                    OriginalSha256 = TestHash(original),
                    PatchedSha256 = TestHash(terraria),
                    BackupFile = backupName,
                    InstalledUtc = DateTime.UtcNow.AddDays(-1),
                    PatcherVersion = "old"
                };
                var serializer = new System.Xml.Serialization.XmlSerializer(
                    typeof(InstallManifest));
                using (var stream = File.Create(Path.Combine(data,
                    "install.xml"))) serializer.Serialize(stream, manifest);
                Equal(InstallState.Installed,
                    service.GetStatus(terraria).State);

                var installClosed = typeof(InstallationService).GetMethod(
                    "InstallWithGameClosed",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                True(installClosed != null,
                    "closed-game upgrade entry missing");
                InstallStatus result;
                try
                {
                    result = (InstallStatus)installClosed.Invoke(service,
                        new object[] { terraria, payload });
                }
                catch (System.Reflection.TargetInvocationException ex)
                {
                    throw ex.InnerException ?? ex;
                }

                Equal(InstallState.Installed, result.State);
                Equal(TestHash(pluginSource), TestHash(installedPlugin));
                Equal(TestHash(coreSource), TestHash(installedCore));
                Equal("new-scope-slots", File.ReadAllText(readme));
                Equal("new-meme-slots", File.ReadAllText(
                    Path.Combine(installedMemes, "README.txt")));
                Equal("user-config", File.ReadAllText(userConfig));
                Equal("09-08-07-06", BitConverter.ToString(
                    File.ReadAllBytes(userWave)));
                using (var module = ModuleDefinition.ReadModule(terraria))
                {
                    var update = module.Types.Single(t =>
                        t.FullName == "Terraria.Player").Methods.Single(m =>
                            m.Name == "Update");
                    Equal(1, CountCalls(update, "Chaite.Plugin.Runtime",
                        "ValidatePendingMobility"));
                }
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        private static void WriteSyntheticGame(string path, int copyCount = 1,
            bool bypassBranch = false, int horizontalCount = 1,
            bool horizontalBeforeHotbar = false)
        {
            using (var module = ModuleDefinition.CreateModule("Chaite.SyntheticTerraria", ModuleKind.Dll))
            {
                var player = new TypeDefinition("Terraria", "Player", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
                var npc = new TypeDefinition("Terraria", "NPC", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
                var triggers = new TypeDefinition("Terraria.GameInput", "TriggersSet", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
                module.Types.Add(player);
                module.Types.Add(npc);
                module.Types.Add(triggers);
                var copy = new MethodDefinition("CopyInto", MethodAttributes.Public, module.TypeSystem.Void);
                copy.Parameters.Add(new ParameterDefinition("player", ParameterAttributes.None, player));
                copy.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                triggers.Methods.Add(copy);
                var hotbar = new MethodDefinition("HandleHotbarControls", MethodAttributes.Public, module.TypeSystem.Void);
                hotbar.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                player.Methods.Add(hotbar);
                var horizontal = new MethodDefinition("HorizontalMovement", MethodAttributes.Public, module.TypeSystem.Void);
                horizontal.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                player.Methods.Add(horizontal);
                var update = new MethodDefinition("Update", MethodAttributes.Public, module.TypeSystem.Void);
                update.Parameters.Add(new ParameterDefinition("index", ParameterAttributes.None, module.TypeSystem.Int32));
                player.Methods.Add(update);
                var selectThis = Instruction.Create(OpCodes.Ldarg_0);
                if (bypassBranch) update.Body.Instructions.Add(Instruction.Create(OpCodes.Br_S, selectThis));
                for (var i = 0; i < copyCount; i++)
                {
                    update.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                    update.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                    update.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, copy));
                }
                Action appendHorizontal = () =>
                {
                    for (var i = 0; i < horizontalCount; i++)
                    {
                        update.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
                        update.Body.Instructions.Add(Instruction.Create(OpCodes.Call, horizontal));
                    }
                };
                if (horizontalBeforeHotbar) appendHorizontal();
                update.Body.Instructions.Add(selectThis);
                update.Body.Instructions.Add(Instruction.Create(OpCodes.Call, hotbar));
                if (!horizontalBeforeHotbar) appendHorizontal();
                update.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                var loot = new MethodDefinition("NPCLoot", MethodAttributes.Public, module.TypeSystem.Void);
                loot.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                npc.Methods.Add(loot);
                module.Write(path);
            }
        }

        private static void WithPatchFixture(Action<string, string> test)
        {
            var directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "patcher-fixture-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var source = Path.Combine(directory, "synthetic-source.dll");
            var output = Path.Combine(directory, "synthetic-patched.dll");
            try
            {
                test(source, output);
            }
            finally
            {
                // Exact known synthetic artifacts only; never recursively delete a workspace.
                if (File.Exists(source)) File.Delete(source);
                if (File.Exists(output)) File.Delete(output);
                Directory.Delete(directory, false);
            }
        }

        private static int CountCalls(MethodDefinition method, string type, string name) =>
            method.Body.Instructions.Count(i => MethodCall(i, type, name));

        private static bool MethodCall(Instruction instruction, string type, string name) =>
            (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
            instruction.Operand is MethodReference reference && reference.DeclaringType.FullName == type && reference.Name == name;

        private static string TestHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(stream))
                    .Replace("-", string.Empty);
        }
    }
}
