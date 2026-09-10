using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Chaite.Patcher;

// Workspace-only harness patcher. No source game is ever overwritten.
static class GameProbePatcher
{
    static void Main(string[] args)
    {
        string game=Path.GetFullPath(args[0]),plugin=Path.GetFullPath(args[1]),probe=Path.GetFullPath(args[2]),run=Path.GetFullPath(args[3]);
        string output=Path.Combine(run,"Terraria.exe");
        if(File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite existing test game");
        new InstallationService().PatchCopyForTest(game,plugin,output);
        using(var module=ModuleDefinition.ReadModule(output,new ReaderParameters{InMemory=true}))
        using(var p=ModuleDefinition.ReadModule(probe))
        {
            var api=p.Types.Single(t=>t.Name=="ChaiteGameProbe");
            Func<string,MethodReference> method=name=>module.ImportReference(api.Methods.Single(m=>m.Name==name));
            var main=module.GetType("Terraria.Main");
            var launch=module.GetType("Terraria.Program").Methods.Single(m=>m.Name=="LaunchGame");
            var savePath=launch.Body.Instructions.Single(i=>i.OpCode==OpCodes.Stsfld && ((FieldReference)i.Operand).Name=="SavePath");
            launch.Body.GetILProcessor().InsertAfter(savePath,Instruction.Create(OpCodes.Call,method("ValidateLaunch")));
            var kill=module.GetType("Terraria.Player").Methods.Single(m=>m.Name=="KillMe");
            var ki=kill.Body.GetILProcessor();var kfirst=kill.Body.Instructions[0];
            ki.InsertBefore(kfirst,ki.Create(OpCodes.Ldarg_1));ki.InsertBefore(kfirst,ki.Create(OpCodes.Call,method("PlayerDeath")));
            var chat=main.Methods.Single(m=>m.Name=="NewText" && m.Parameters.Count==4 && m.Parameters[0].ParameterType.FullName=="System.String");
            chat.Body.Instructions.Clear();chat.Body.ExceptionHandlers.Clear();chat.Body.Variables.Clear();
            var chatIl=chat.Body.GetILProcessor();chatIl.Append(chatIl.Create(OpCodes.Ldarg_0));chatIl.Append(chatIl.Create(OpCodes.Call,method("ChatMessage")));chatIl.Append(chatIl.Create(OpCodes.Ret));
            Prefix(main.Methods.Single(m=>m.Name=="Initialize"),method("EarlyInitialize"));
            Suffix(main.Methods.Single(m=>m.Name=="PostContentLoadInitialize"),method("ContentReady"));
            Prefix(main.Methods.Single(m=>m.Name=="DoUpdate"),method("BeforeUpdate"));
            Suffix(main.Methods.Single(m=>m.Name=="Draw" && m.Parameters.Count==1),method("AfterDraw"));
            var playerUpdate=module.GetType("Terraria.Player").Methods.Single(m=>m.Name=="Update" && m.Parameters.Count==1);
            PrefixPlayer(playerUpdate,method("MotionBeforePlayerUpdate"));
            // Replay only after native CopyInto AND the existing production hook.
            // Branches skipping native input retain the pre-frame test controls.
            var pendingInput=playerUpdate.Body.Instructions.Single(i=>i.Operand is MethodReference &&
                ((MethodReference)i.Operand).DeclaringType.FullName=="Chaite.Plugin.Runtime" &&
                ((MethodReference)i.Operand).Name=="ApplyPendingInput");
            var mpi=playerUpdate.Body.GetILProcessor();
            var motionPlayer=mpi.Create(OpCodes.Ldarg_0);mpi.InsertAfter(pendingInput,motionPlayer);
            mpi.InsertAfter(motionPlayer,mpi.Create(OpCodes.Call,method("MotionAfterInput")));
            var jumpMovement=module.GetType("Terraria.Player").Methods.Single(m=>m.Name=="JumpMovement" && m.Parameters.Count==0);
            PrefixPlayer(jumpMovement,method("MotionBeforeJump"));
            SuffixPlayer(jumpMovement,method("MotionAfterJump"));
            var shotUpdate=module.GetType("Terraria.Projectile").Methods.Single(m=>m.Name=="Update" && m.Parameters.Count==1);
            var shotKill=module.GetType("Terraria.Projectile").Methods.Single(m=>m.Name=="Kill" && m.Parameters.Count==0);
            var ski=shotKill.Body.GetILProcessor();var skfirst=shotKill.Body.Instructions[0];
            ski.InsertBefore(skfirst,ski.Create(OpCodes.Ldarg_0));ski.InsertBefore(skfirst,ski.Create(OpCodes.Call,method("ProjectileKilled")));
            var si=shotUpdate.Body.GetILProcessor();var sfirst=shotUpdate.Body.Instructions[0];
            si.InsertBefore(sfirst,si.Create(OpCodes.Ldarg_0));si.InsertBefore(sfirst,si.Create(OpCodes.Call,method("ProjectileBeforeUpdate")));
            foreach(var guarded in main.Methods.Where(m=>m.Name.StartsWith("UpdateWorld_") && m.HasBody).Concat(new[]{shotUpdate,playerUpdate}))
            foreach(var handler in guarded.Body.ExceptionHandlers.Where(h=>h.HandlerType==ExceptionHandlerType.Catch))
            {
                var gi=guarded.Body.GetILProcessor();var original=handler.HandlerStart;
                var preserved=Instruction.Create(OpCodes.Nop);preserved.OpCode=original.OpCode;preserved.Operand=original.Operand;
                original.OpCode=OpCodes.Dup;original.Operand=null;
                var report=gi.Create(OpCodes.Call,method("FatalCaughtObject"));gi.InsertAfter(original,report);gi.InsertAfter(report,preserved);
            }
            // Test arenas are intentionally memory-only. Vanilla's initial
            // character-save timer is independent of the autoSave preference.
            // Suppress persistence entry points, never physics, damage or AI.
            foreach(var target in new[]{Tuple.Create("Terraria.Player","SavePlayer"),Tuple.Create("Terraria.Map.MapHelper","SaveMap"),Tuple.Create("Terraria.WorldGen","saveWorld")})
            foreach(var save in module.GetType(target.Item1).Methods.Where(m=>m.Name==target.Item2 && m.HasBody))
            {
                if(save.ReturnType.FullName!="System.Void") throw new InvalidOperationException("Unexpected save signature");
                save.Body.Instructions.Clear();save.Body.ExceptionHandlers.Clear();save.Body.Variables.Clear();
                save.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
            }
            var pi=playerUpdate.Body.GetILProcessor();
            foreach(var ret in playerUpdate.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())
            {
                string location=ret.Offset.ToString("X4");
                ret.OpCode=OpCodes.Ldarg_0;ret.Operand=null;
                var label=pi.Create(OpCodes.Ldstr,location);pi.InsertAfter(ret,label);
                var report=pi.Create(OpCodes.Call,method("PlayerReturned"));pi.InsertAfter(label,report);pi.InsertAfter(report,pi.Create(OpCodes.Ret));
            }
            var runGame=module.GetType("Terraria.Program").Methods.Single(m=>m.Name=="RunGame");
            var social=runGame.Body.Instructions.Single(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).FullName.Contains("Terraria.Social.SocialAPI::Initialize"));
            social.Operand=method("InitializeSocial");
            if(args.Length > 4 && args[4]=="headless")
            {
                runGame.Body.Instructions.Clear();runGame.Body.ExceptionHandlers.Clear();runGame.Body.Variables.Clear();
                var il=runGame.Body.GetILProcessor();il.Append(il.Create(OpCodes.Call,method("RunHeadless")));il.Append(il.Create(OpCodes.Ret));
                var gore=main.Methods.Single(m=>m.Name=="UpdateWorld_Gores");
                gore.Body.Instructions.Clear();gore.Body.ExceptionHandlers.Clear();gore.Body.Variables.Clear();
                gore.Body.GetILProcessor().Append(Instruction.Create(OpCodes.Ret));
                // Floating text measures a GPU-loaded font even during netMode=0
                // headless gameplay. Suppress presentation ONLY, never Hurt/damage.
                foreach(var visual in module.GetType("Terraria.CombatText").Methods.Where(m=>m.Name=="NewText" && m.ReturnType.FullName=="System.Int32"))
                {
                    visual.Body.Instructions.Clear();visual.Body.ExceptionHandlers.Clear();visual.Body.Variables.Clear();
                    var vi=visual.Body.GetILProcessor();vi.Append(vi.Create(OpCodes.Ldc_I4,100));vi.Append(vi.Create(OpCodes.Ret));
                }
            }
            foreach(var m in module.GetType("Terraria.Program").Methods.Where(m=>m.HasBody))
            foreach(var i in m.Body.Instructions)
                if(i.Operand is MethodReference && ((MethodReference)i.Operand).Name=="DisplayException") i.Operand=method("FatalException");
            // Preserve vanilla input processing. Headless does not poll OS input;
            // only the test poller supplies activation/cancellation edges.
            foreach(var type in module.Types) WidenType(type);
            module.Write(output);
        }
        string testPlugin=Path.Combine(run,"Chaite.Plugin.dll");
        File.Copy(plugin,testPlugin,false);
        using(var module=ModuleDefinition.ReadModule(testPlugin,new ReaderParameters{InMemory=true}))
        using(var p=ModuleDefinition.ReadModule(probe))
        {
            var poller=module.GetType("Chaite.Plugin.HotkeyPoller");
            var poll=poller.Methods.Single(m=>m.Name=="Poll");
            var sample=poller.Methods.Single(m=>m.Name=="Sample");
            var api=p.Types.Single(t=>t.Name=="ChaiteGameProbe");
            poll.Body.Instructions.Clear();poll.Body.ExceptionHandlers.Clear();poll.Body.Variables.Clear();
            var il=poll.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Call,module.ImportReference(api.Methods.Single(m=>m.Name=="ActivateDown"))));
            il.Append(il.Create(OpCodes.Call,module.ImportReference(api.Methods.Single(m=>m.Name=="CancelDown"))));
            il.Append(il.Create(OpCodes.Call,sample));il.Append(il.Create(OpCodes.Ret));
            module.Write(testPlugin);
        }
        Console.WriteLine("Workspace-only probe patches ready: "+run);
    }
    static void Prefix(MethodDefinition m,MethodReference call)
    {
        var il=m.Body.GetILProcessor();il.InsertBefore(m.Body.Instructions[0],il.Create(OpCodes.Call,call));
    }
    static void PrefixPlayer(MethodDefinition m,MethodReference call)
    {
        var il=m.Body.GetILProcessor();var first=m.Body.Instructions[0];
        il.InsertBefore(first,il.Create(OpCodes.Ldarg_0));il.InsertBefore(first,il.Create(OpCodes.Call,call));
    }
    static void SuffixPlayer(MethodDefinition m,MethodReference call)
    {
        var il=m.Body.GetILProcessor();
        foreach(var ret in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())
        {
            ret.OpCode=OpCodes.Ldarg_0;ret.Operand=null;
            var report=il.Create(OpCodes.Call,call);il.InsertAfter(ret,report);il.InsertAfter(report,il.Create(OpCodes.Ret));
        }
    }
    static void WidenType(TypeDefinition type)
    {
        foreach(var nested in type.NestedTypes) WidenType(nested);
        foreach(var m in type.Methods.Where(m=>m.HasBody))
        foreach(var i in m.Body.Instructions)
        {
            if(i.OpCode.OperandType!=OperandType.ShortInlineBrTarget) continue;
            var name=i.OpCode.Name;
            var field=typeof(OpCodes).GetFields().Single(f=>f.FieldType==typeof(OpCode) && ((OpCode)f.GetValue(null)).Name==name.Substring(0,name.Length-2));
            i.OpCode=(OpCode)field.GetValue(null);
        }
    }
    static void Suffix(MethodDefinition m,MethodReference call)
    {
        var il=m.Body.GetILProcessor();
        foreach(var ret in m.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray())
        { ret.OpCode=OpCodes.Call;ret.Operand=call;il.InsertAfter(ret,il.Create(OpCodes.Ret)); }
    }
}
