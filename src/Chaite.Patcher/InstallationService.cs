using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml.Serialization;

namespace Chaite.Patcher
{
    public sealed class InstallationService
    {
        public const string SupportedVersion = "1.4.5.8";
        public const string SupportedSha256 = "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";

        public InstallStatus GetStatus(string terrariaExe)
        {
            if (string.IsNullOrWhiteSpace(terrariaExe) || !File.Exists(terrariaExe))
                return NewStatus(InstallState.NotFound, terrariaExe, null, null, "未找到 Terraria.exe。");

            try
            {
                var version = FileVersionInfo.GetVersionInfo(terrariaExe).FileVersion;
                var hash = Sha256(terrariaExe);
                var manifest = ReadManifest(terrariaExe);
                if (manifest != null)
                {
                    var state = hash.Equals(manifest.PatchedSha256, StringComparison.OrdinalIgnoreCase)
                        ? InstallState.Installed
                        : InstallState.InstalledButChanged;
                    return NewStatus(state, terrariaExe, version, hash,
                        state == InstallState.Installed ? "拆特已注入。" : "安装后 Terraria.exe 已被 Steam 或其他工具改动，请先恢复/更新。");
                }

                var supported = version == SupportedVersion && hash.Equals(SupportedSha256, StringComparison.OrdinalIgnoreCase);
                return NewStatus(supported ? InstallState.CleanSupported : InstallState.CleanUnsupported,
                    terrariaExe, version, hash,
                    supported ? "检测到受支持的 Terraria 1.4.5.8。" : "此 Terraria.exe 不在已验证哈希白名单中，拒绝注入。");
            }
            catch (Exception ex)
            {
                return NewStatus(InstallState.Invalid, terrariaExe, null, null, ex.Message);
            }
        }

        public InstallStatus Install(string terrariaExe, string payloadDirectory)
        {
            EnsureTerrariaClosed();
            var before = GetStatus(terrariaExe);
            if (before.State == InstallState.Installed)
                return before;
            if (before.State != InstallState.CleanSupported)
                throw new InvalidOperationException(before.Message);

            var pluginSource = Path.Combine(payloadDirectory, "Chaite.Plugin.dll");
            var coreSource = Path.Combine(payloadDirectory, "Chaite.Core.dll");
            if (!File.Exists(pluginSource) || !File.Exists(coreSource))
                throw new FileNotFoundException("安装载荷缺少 Chaite.Plugin.dll 或 Chaite.Core.dll。", payloadDirectory);

            var gameDirectory = Path.GetDirectoryName(terrariaExe);
            var dataDirectory = Path.Combine(gameDirectory, "Chaite");
            Directory.CreateDirectory(dataDirectory);
            Directory.CreateDirectory(Path.Combine(dataDirectory, "Audio"));

            var backupName = "Terraria.exe." + before.Sha256.Substring(0, 16) + ".backup";
            var backupPath = Path.Combine(dataDirectory, backupName);
            if (!File.Exists(backupPath))
                File.Copy(terrariaExe, backupPath, false);
            if (!Sha256(backupPath).Equals(before.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("备份哈希校验失败，安装已中止。");

            File.Copy(pluginSource, Path.Combine(gameDirectory, "Chaite.Plugin.dll"), true);
            File.Copy(coreSource, Path.Combine(gameDirectory, "Chaite.Core.dll"), true);
            CopyIfMissing(Path.Combine(payloadDirectory, "config.json"), Path.Combine(dataDirectory, "config.json"));
            CopyIfMissing(Path.Combine(payloadDirectory, "Audio", "README.txt"), Path.Combine(dataDirectory, "Audio", "README.txt"));

            var temporary = Path.Combine(gameDirectory, "Terraria.exe.chaite-new");
            try
            {
                PatchAssembly(terrariaExe, pluginSource, temporary);
                ValidatePatchedAssembly(temporary);
                File.Replace(temporary, terrariaExe, null, true);
            }
            finally
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }

            var manifest = new InstallManifest
            {
                GameVersion = before.GameVersion,
                OriginalSha256 = before.Sha256,
                PatchedSha256 = Sha256(terrariaExe),
                BackupFile = backupName,
                InstalledUtc = DateTime.UtcNow,
                PatcherVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString()
            };
            WriteManifest(terrariaExe, manifest);
            return GetStatus(terrariaExe);
        }

        public InstallStatus Restore(string terrariaExe)
        {
            EnsureTerrariaClosed();
            var manifest = ReadManifest(terrariaExe);
            if (manifest == null)
                throw new InvalidOperationException("没有找到拆特安装清单，拒绝猜测恢复源。");

            var currentHash = Sha256(terrariaExe);
            if (!currentHash.Equals(manifest.PatchedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Terraria.exe 在安装后已变化。为防止覆盖 Steam 更新或其他修改，拒绝自动恢复。");

            var gameDirectory = Path.GetDirectoryName(terrariaExe);
            var dataDirectory = Path.Combine(gameDirectory, "Chaite");
            var backupPath = Path.Combine(dataDirectory, manifest.BackupFile);
            if (!File.Exists(backupPath) || !Sha256(backupPath).Equals(manifest.OriginalSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("原版备份缺失或哈希不匹配，拒绝恢复。");

            var temporary = Path.Combine(gameDirectory, "Terraria.exe.chaite-restore");
            File.Copy(backupPath, temporary, true);
            File.Replace(temporary, terrariaExe, null, true);
            DeleteIfExists(Path.Combine(gameDirectory, "Chaite.Plugin.dll"));
            DeleteIfExists(Path.Combine(gameDirectory, "Chaite.Core.dll"));
            DeleteIfExists(ManifestPath(terrariaExe));
            return GetStatus(terrariaExe);
        }

        public void PatchCopyForTest(string sourceExe, string pluginDll, string outputExe)
        {
            var outputFullPath = Path.GetFullPath(outputExe);
            if (outputFullPath.Equals(Path.GetFullPath(sourceExe), StringComparison.OrdinalIgnoreCase) ||
                outputFullPath.Equals(Path.GetFullPath(pluginDll), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("副本验证不得覆盖输入的游戏程序或插件。请指定独立输出文件。");
            PatchAssembly(sourceExe, pluginDll, outputExe);
            ValidatePatchedAssembly(outputExe);
        }

        private static void PatchAssembly(string sourceExe, string pluginDll, string outputExe)
        {
            using (var plugin = ModuleDefinition.ReadModule(pluginDll))
            using (var module = ModuleDefinition.ReadModule(sourceExe, new ReaderParameters { InMemory = true, ReadWrite = false }))
            {
                if (module.AssemblyReferences.Any(r => r.Name == "Chaite.Plugin"))
                    throw new InvalidOperationException("Terraria.exe 已包含拆特引用，拒绝重复注入。");

                var runtime = plugin.Types.Single(t => t.FullName == "Chaite.Plugin.Runtime");
                var tickReference = module.ImportReference(runtime.Methods.Single(m => m.Name == "Tick" && m.Parameters.Count == 2));
                var inputReference = module.ImportReference(runtime.Methods.Single(m => m.Name == "ApplyPendingInput" && m.Parameters.Count == 1));
                var selectionReference = module.ImportReference(runtime.Methods.Single(m => m.Name == "ApplyPendingSelection" && m.Parameters.Count == 1));
                var lootReference = module.ImportReference(runtime.Methods.Single(m => m.Name == "OnNpcKilled" && m.Parameters.Count == 1));

                var player = module.Types.Single(t => t.FullName == "Terraria.Player");
                var update = player.Methods.Single(m => m.Name == "Update" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.MetadataType == MetadataType.Int32);
                var copyInput = RequireUniqueCall(update, "Terraria.GameInput.TriggersSet", "CopyInto");
                var selectHotbar = RequireUniqueCall(update, "Terraria.Player", "HandleHotbarControls");
                if (update.Body.Instructions.IndexOf(copyInput) >= update.Body.Instructions.IndexOf(selectHotbar))
                    throw new InvalidDataException("原版输入复制/快捷栏处理顺序不符合已验证的注入布局。");
                InjectAtStart(update, new[]
                {
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Ldarg_1),
                    Instruction.Create(OpCodes.Call, tickReference)
                });
                // Player.Update's native input copy would overwrite controls computed by the entry
                // hook. Replay the already-computed plan only on the fall-through path AFTER copying.
                // Branches which skip native input processing retain their original destination.
                InjectAfter(update, copyInput, new[]
                {
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Call, inputReference)
                });
                // Mouse-wheel / number-key selection is processed later. Restore only selection and
                // aim here, never the full movement controls (which may have been constrained by CC).
                InjectAfter(update, selectHotbar, new[]
                {
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Call, selectionReference)
                });

                var npc = module.Types.Single(t => t.FullName == "Terraria.NPC");
                var loot = npc.Methods.Single(m => m.Name == "NPCLoot" && m.Parameters.Count == 0);
                InjectAtStart(loot, new[]
                {
                    Instruction.Create(OpCodes.Ldarg_0),
                    Instruction.Create(OpCodes.Call, lootReference)
                });

                WidenShortBranches(update);
                WidenShortBranches(loot);
                module.Write(outputExe, new WriterParameters { WriteSymbols = false });
            }
        }

        private static void InjectAtStart(MethodDefinition method, IEnumerable<Instruction> instructions)
        {
            var first = method.Body.Instructions[0];
            var processor = method.Body.GetILProcessor();
            foreach (var instruction in instructions)
                processor.InsertBefore(first, instruction);
        }

        private static void InjectAfter(MethodDefinition method, Instruction anchor, IEnumerable<Instruction> instructions)
        {
            var processor = method.Body.GetILProcessor();
            foreach (var instruction in instructions)
            {
                processor.InsertAfter(anchor, instruction);
                anchor = instruction;
            }
        }

        private static Instruction RequireUniqueCall(MethodDefinition method, string declaringType, string name)
        {
            var calls = method.Body.Instructions.Where(i => IsCall(i, declaringType, name)).ToList();
            if (calls.Count != 1)
                throw new InvalidDataException(method.FullName + " 的 " + declaringType + "." + name +
                    " 调用点应唯一，实际为 " + calls.Count + "；拒绝猜测注入位置。");
            return calls[0];
        }

        private static bool IsCall(Instruction instruction, string declaringType, string name)
        {
            return (instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                instruction.Operand is MethodReference reference && reference.DeclaringType.FullName == declaringType && reference.Name == name;
        }

        private static void WidenShortBranches(MethodDefinition method)
        {
            // Cecil does not automatically widen existing short branches when inserted instructions
            // increase a branch's distance. Keep targets unchanged and emit the safe long encoding.
            foreach (var instruction in method.Body.Instructions)
            {
                switch (instruction.OpCode.Code)
                {
                    case Code.Br_S: instruction.OpCode = OpCodes.Br; break;
                    case Code.Brfalse_S: instruction.OpCode = OpCodes.Brfalse; break;
                    case Code.Brtrue_S: instruction.OpCode = OpCodes.Brtrue; break;
                    case Code.Beq_S: instruction.OpCode = OpCodes.Beq; break;
                    case Code.Bge_S: instruction.OpCode = OpCodes.Bge; break;
                    case Code.Bge_Un_S: instruction.OpCode = OpCodes.Bge_Un; break;
                    case Code.Bgt_S: instruction.OpCode = OpCodes.Bgt; break;
                    case Code.Bgt_Un_S: instruction.OpCode = OpCodes.Bgt_Un; break;
                    case Code.Ble_S: instruction.OpCode = OpCodes.Ble; break;
                    case Code.Ble_Un_S: instruction.OpCode = OpCodes.Ble_Un; break;
                    case Code.Blt_S: instruction.OpCode = OpCodes.Blt; break;
                    case Code.Blt_Un_S: instruction.OpCode = OpCodes.Blt_Un; break;
                    case Code.Bne_Un_S: instruction.OpCode = OpCodes.Bne_Un; break;
                    case Code.Leave_S: instruction.OpCode = OpCodes.Leave; break;
                }
            }
        }

        private static void ValidatePatchedAssembly(string path)
        {
            using (var module = ModuleDefinition.ReadModule(path))
            {
                if (!module.AssemblyReferences.Any(r => r.Name == "Chaite.Plugin"))
                    throw new InvalidDataException("写入后的程序没有 Chaite.Plugin 引用。");
                var player = module.Types.Single(t => t.FullName == "Terraria.Player");
                var update = player.Methods.Single(m => m.Name == "Update" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.MetadataType == MetadataType.Int32);
                var tick = RequireUniqueCall(update, "Chaite.Plugin.Runtime", "Tick");
                var input = RequireUniqueCall(update, "Chaite.Plugin.Runtime", "ApplyPendingInput");
                var selection = RequireUniqueCall(update, "Chaite.Plugin.Runtime", "ApplyPendingSelection");
                var copyInput = RequireUniqueCall(update, "Terraria.GameInput.TriggersSet", "CopyInto");
                var selectHotbar = RequireUniqueCall(update, "Terraria.Player", "HandleHotbarControls");
                var instructions = update.Body.Instructions;
                if (instructions.Count < 3 || instructions[0].OpCode != OpCodes.Ldarg_0 ||
                    instructions[1].OpCode != OpCodes.Ldarg_1 || instructions[2] != tick)
                    throw new InvalidDataException("Player.Update 入口 Tick 注入点验证失败。");
                ValidatePostCallHook(copyInput, input, "原版输入复制后");
                ValidatePostCallHook(selectHotbar, selection, "原版快捷栏处理后");
                if (instructions.IndexOf(tick) >= instructions.IndexOf(copyInput) ||
                    instructions.IndexOf(input) >= instructions.IndexOf(selectHotbar))
                    throw new InvalidDataException("Player.Update 四阶段输入处理顺序验证失败。");

                var npc = module.Types.Single(t => t.FullName == "Terraria.NPC");
                var loot = npc.Methods.Single(m => m.Name == "NPCLoot" && m.Parameters.Count == 0);
                var killed = RequireUniqueCall(loot, "Chaite.Plugin.Runtime", "OnNpcKilled");
                if (loot.Body.Instructions.Count < 2 || loot.Body.Instructions[0].OpCode != OpCodes.Ldarg_0 ||
                    loot.Body.Instructions[1] != killed)
                    throw new InvalidDataException("NPC.NPCLoot 击杀确认注入点验证失败。");
            }
        }

        private static void ValidatePostCallHook(Instruction anchor, Instruction hook, string label)
        {
            if (anchor.Next == null || anchor.Next.OpCode != OpCodes.Ldarg_0 || anchor.Next.Next != hook)
                throw new InvalidDataException(label + "的重放钩子位置验证失败。");
        }

        private static void EnsureTerrariaClosed()
        {
            if (Process.GetProcessesByName("Terraria").Length > 0)
                throw new InvalidOperationException("Terraria 正在运行。请退出游戏后再安装或恢复；工具不会强制结束进程。");
        }

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty);
        }

        private static void CopyIfMissing(string source, string destination)
        {
            if (File.Exists(source) && !File.Exists(destination))
                File.Copy(source, destination, false);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static InstallManifest ReadManifest(string terrariaExe)
        {
            var path = ManifestPath(terrariaExe);
            if (!File.Exists(path))
                return null;
            var serializer = new XmlSerializer(typeof(InstallManifest));
            using (var stream = File.OpenRead(path))
                return (InstallManifest)serializer.Deserialize(stream);
        }

        private static void WriteManifest(string terrariaExe, InstallManifest manifest)
        {
            var path = ManifestPath(terrariaExe);
            var serializer = new XmlSerializer(typeof(InstallManifest));
            using (var stream = File.Create(path))
                serializer.Serialize(stream, manifest);
        }

        private static string ManifestPath(string terrariaExe)
        {
            return Path.Combine(Path.GetDirectoryName(terrariaExe), "Chaite", "install.xml");
        }

        private static InstallStatus NewStatus(InstallState state, string exe, string version, string hash, string message)
        {
            return new InstallStatus { State = state, TerrariaExe = exe, GameVersion = version, Sha256 = hash, Message = message };
        }
    }
}
