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
            Run(nameof(PatcherPlacesFourHooksWithoutModifyingSource), PatcherPlacesFourHooksWithoutModifyingSource);
            Run(nameof(PatcherRejectsMissingOrAmbiguousInputCopy), PatcherRejectsMissingOrAmbiguousInputCopy);
            Run(nameof(PatcherRejectsInPlaceTestAndDoubleInjection), PatcherRejectsInPlaceTestAndDoubleInjection);
            Run(nameof(PatcherWidensBranchesWithoutRedirectingBypass), PatcherWidensBranchesWithoutRedirectingBypass);
        }

        private static void PatcherPlacesFourHooksWithoutModifyingSource()
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
                    Equal(1, CountCalls(update, "Chaite.Plugin.Runtime", "ApplyPendingSelection"));
                    Equal(1, CountCalls(loot, "Chaite.Plugin.Runtime", "OnNpcKilled"));
                    Equal("Tick", ((MethodReference)update.Body.Instructions[2].Operand).Name);
                    var copy = update.Body.Instructions.Single(i => MethodCall(i, "Terraria.GameInput.TriggersSet", "CopyInto"));
                    Equal(OpCodes.Ldarg_0, copy.Next.OpCode);
                    True(MethodCall(copy.Next.Next, "Chaite.Plugin.Runtime", "ApplyPendingInput"));
                    var hotbar = update.Body.Instructions.Single(i => MethodCall(i, "Terraria.Player", "HandleHotbarControls"));
                    Equal(OpCodes.Ldarg_0, hotbar.Next.OpCode);
                    True(MethodCall(hotbar.Next.Next, "Chaite.Plugin.Runtime", "ApplyPendingSelection"));
                    True(update.Body.Instructions.IndexOf(copy.Next.Next) < update.Body.Instructions.IndexOf(hotbar));
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
                    True(ReferenceEquals(copy.Next.Next.Next, branch.Operand),
                        "a branch bypassing the original CopyInto must still bypass the replay hook");
                    False(update.Body.Instructions.Any(i => i.OpCode.OperandType == OperandType.ShortInlineBrTarget));
                }
            });
        }

        private static void WriteSyntheticGame(string path, int copyCount = 1, bool bypassBranch = false)
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
                update.Body.Instructions.Add(selectThis);
                update.Body.Instructions.Add(Instruction.Create(OpCodes.Call, hotbar));
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
                return BitConverter.ToString(hash.ComputeHash(stream));
        }
    }
}
