using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Chaite.Patcher;

// Workspace-only harness patcher. No source game is ever overwritten.
static class GameProbePatcher
{
    const string ProbeTypeName="ChaiteGameProbe";

    static void Main(string[] args)
    {
        string game=Path.GetFullPath(args[0]),plugin=Path.GetFullPath(args[1]),probe=Path.GetFullPath(args[2]),run=Path.GetFullPath(args[3]);
        string output=Path.Combine(run,"Terraria.exe");
        if(File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite existing test game");
        new InstallationService().PatchCopyForTest(game,plugin,output);
        using(var module=ModuleDefinition.ReadModule(output,new ReaderParameters{InMemory=true}))
        using(var p=ModuleDefinition.ReadModule(probe))
        {
            var api=p.Types.Single(t=>t.FullName==ProbeTypeName);
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
            var wingMovement=module.GetType("Terraria.Player").Methods.Single(m=>m.Name=="WingMovement" && m.Parameters.Count==0);
            PrefixPlayer(wingMovement,method("FlightBeforeWing"));
            SuffixPlayer(wingMovement,method("FlightAfterWing"));
            var dashMovement=module.GetType("Terraria.Player").Methods.Single(m=>m.Name=="DashMovement" && m.Parameters.Count==0);
            PrefixPlayer(dashMovement,method("BeforeShieldDash"));
            SuffixPlayer(dashMovement,method("AfterShieldDash"));
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
            InjectPlayerHurtObservation(module,api);
            module.Write(output);
        }
        string testPlugin=Path.Combine(run,"Chaite.Plugin.dll");
        File.Copy(plugin,testPlugin,false);
        using(var module=ModuleDefinition.ReadModule(testPlugin,new ReaderParameters{InMemory=true}))
        using(var p=ModuleDefinition.ReadModule(probe))
        {
            if(module.Types.Any(ContainsProbeReference))
                throw new InvalidOperationException("Input production plugin already references ChaiteGameProbe");
            var poller=module.GetType("Chaite.Plugin.HotkeyPoller");
            var poll=poller.Methods.Single(m=>m.Name=="Poll");
            var sample=poller.Methods.Single(m=>m.Name=="Sample");
            var api=p.Types.Single(t=>t.FullName==ProbeTypeName);
            poll.Body.Instructions.Clear();poll.Body.ExceptionHandlers.Clear();poll.Body.Variables.Clear();
            var il=poll.Body.GetILProcessor();
            il.Append(il.Create(OpCodes.Ldarg_0));il.Append(il.Create(OpCodes.Ldc_I4_1));
            il.Append(il.Create(OpCodes.Call,module.ImportReference(api.Methods.Single(m=>m.Name=="ActivateDown"))));
            il.Append(il.Create(OpCodes.Call,module.ImportReference(api.Methods.Single(m=>m.Name=="CancelDown"))));
            il.Append(il.Create(OpCodes.Call,sample));il.Append(il.Create(OpCodes.Ret));
            // Observe the dispatched enum in the workspace plugin copy. The
            // original Play body and string return remain byte-for-byte ordered.
            var audioPlay=module.GetType("Chaite.Plugin.AudioCuePlayer").Methods.Single(m=>m.Name=="Play");
            if(!audioPlay.HasThis || !audioPlay.HasBody || audioPlay.ReturnType.FullName!="System.String" ||
                audioPlay.Parameters.Count!=1 || audioPlay.Parameters[0].ParameterType.FullName!="Chaite.Core.AudioCue")
                throw new InvalidOperationException("Unreviewed AudioCuePlayer.Play observer signature");
            int originalAudioInstructionCount=audioPlay.Body.Instructions.Count;
            var audioObserverDefinition=api.Methods.Single(m=>m.Name=="ObserveAudioCue");
            if(!audioObserverDefinition.IsPublic || !audioObserverDefinition.IsStatic || audioObserverDefinition.HasThis ||
                audioObserverDefinition.ReturnType.FullName!="System.Void" || audioObserverDefinition.Parameters.Count!=1 ||
                audioObserverDefinition.Parameters[0].ParameterType.FullName!="Chaite.Core.AudioCue")
                throw new InvalidOperationException("Unreviewed probe AudioCue observer signature");
            var audioIl=audioPlay.Body.GetILProcessor();var originalAudioFirst=audioPlay.Body.Instructions[0];
            audioIl.InsertBefore(originalAudioFirst,audioIl.Create(OpCodes.Ldarg_1));
            audioIl.InsertBefore(originalAudioFirst,audioIl.Create(OpCodes.Call,module.ImportReference(audioObserverDefinition)));
            if(audioPlay.Body.Instructions.Count!=originalAudioInstructionCount+2 ||
                audioPlay.Body.Instructions[0].OpCode!=OpCodes.Ldarg_1 ||
                !IsProbeCall(audioPlay.Body.Instructions[1],"ObserveAudioCue") ||
                audioPlay.Body.Instructions[2]!=originalAudioFirst ||
                audioPlay.Body.Instructions.Count(i=>IsProbeCall(i,"ObserveAudioCue"))!=1)
                throw new InvalidOperationException("AudioCue observer prefix is incomplete");
            // Observe only the workspace plugin copy. Preserve ControlPlan by
            // value and every original instruction; never intercept/replace
            // input gates or add hooks to the distributable production game.
            var applyPlan=module.GetType("Chaite.Plugin.TerrariaFacade").Methods.Single(m=>m.Name=="ApplyPlan");
            // ApplyPlan returns a nullable failure string.  Every injected
            // observer call must leave that exact value on the evaluation
            // stack so the isolated harness cannot hide or rewrite a native
            // output-certificate failure.
            if(!applyPlan.HasThis || !applyPlan.HasBody || applyPlan.ReturnType.FullName!="System.String" ||
                applyPlan.Parameters.Count!=2 || applyPlan.Parameters[0].ParameterType.FullName!="System.Object" ||
                applyPlan.Parameters[1].ParameterType.FullName!="Chaite.Core.ControlPlan" ||
                !applyPlan.Parameters[1].ParameterType.IsValueType || applyPlan.Body.ExceptionHandlers.Count!=0)
                throw new InvalidOperationException("Unreviewed ApplyPlan observer signature");
            int originalApplyInstructionCount=applyPlan.Body.Instructions.Count;
            var applyReturns=applyPlan.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray();
            int originalApplyReturnCount=applyReturns.Length;
            if(originalApplyReturnCount==0) throw new InvalidOperationException("ApplyPlan has no normal returns to observe");
            var beforePlanDefinition=api.Methods.Single(m=>m.Name=="ObserveApplyPlanBefore");
            var afterPlanDefinition=api.Methods.Single(m=>m.Name=="ObserveApplyPlanAfter");
            if(!beforePlanDefinition.IsPublic || !beforePlanDefinition.IsStatic || beforePlanDefinition.HasThis ||
                beforePlanDefinition.DeclaringType.FullName!=ProbeTypeName || beforePlanDefinition.ReturnType.FullName!="System.Void" ||
                beforePlanDefinition.Parameters.Count!=2 || beforePlanDefinition.Parameters[0].ParameterType.FullName!="System.Object" ||
                beforePlanDefinition.Parameters[1].ParameterType.FullName!="Chaite.Core.ControlPlan" || !beforePlanDefinition.Parameters[1].ParameterType.IsValueType ||
                !afterPlanDefinition.IsPublic || !afterPlanDefinition.IsStatic || afterPlanDefinition.HasThis ||
                afterPlanDefinition.DeclaringType.FullName!=ProbeTypeName ||
                afterPlanDefinition.ReturnType.FullName!="System.Void" || afterPlanDefinition.Parameters.Count!=1 ||
                afterPlanDefinition.Parameters[0].ParameterType.FullName!="System.Object")
                throw new InvalidOperationException("Unreviewed probe ApplyPlan observer signatures");
            var beforePlan=module.ImportReference(beforePlanDefinition);
            var afterPlan=module.ImportReference(afterPlanDefinition);
            var applyIl=applyPlan.Body.GetILProcessor();var originalApplyFirst=applyPlan.Body.Instructions[0];
            applyIl.InsertBefore(originalApplyFirst,applyIl.Create(OpCodes.Ldarg_1));
            applyIl.InsertBefore(originalApplyFirst,applyIl.Create(OpCodes.Ldarg_2));
            applyIl.InsertBefore(originalApplyFirst,applyIl.Create(OpCodes.Call,beforePlan));
            foreach(var ret in applyReturns)
            {
                ret.OpCode=OpCodes.Ldarg_1;ret.Operand=null;
                var observer=applyIl.Create(OpCodes.Call,afterPlan);applyIl.InsertAfter(ret,observer);
                applyIl.InsertAfter(observer,applyIl.Create(OpCodes.Ret));
            }
            applyPlan.Body.MaxStackSize=Math.Max(applyPlan.Body.MaxStackSize,2);
            foreach(var type in module.Types) WidenType(type);
            if(applyPlan.Body.Instructions.Count!=originalApplyInstructionCount+3+2*originalApplyReturnCount ||
                applyPlan.Body.Instructions.Count(i=>IsProbeCall(i,"ObserveApplyPlanBefore"))!=1 ||
                applyPlan.Body.Instructions.Count(i=>IsProbeCall(i,"ObserveApplyPlanAfter"))!=originalApplyReturnCount ||
                applyPlan.Body.Instructions.Count(i=>i.Operand is MethodReference &&
                    ((MethodReference)i.Operand).DeclaringType.FullName==ProbeTypeName)!=1+originalApplyReturnCount ||
                applyPlan.Body.Instructions.Count(i=>i.OpCode==OpCodes.Ret)!=originalApplyReturnCount ||
                applyReturns.Any(i=>i.OpCode!=OpCodes.Ldarg_1 || i.Next==null || i.Next.OpCode!=OpCodes.Call ||
                    !IsProbeCall(i.Next,"ObserveApplyPlanAfter") ||
                    i.Next.Next==null || i.Next.Next.OpCode!=OpCodes.Ret))
                throw new InvalidOperationException("ApplyPlan observer pairs are incomplete");
            module.Write(testPlugin);
        }
        Console.WriteLine("Workspace-only probe patches ready: "+run);
    }
    static void InjectPlayerHurtObservation(ModuleDefinition module,TypeDefinition api)
    {
        var player=module.GetType("Terraria.Player");
        var named=player.Methods.Where(m=>m.Name=="Hurt").ToArray();
        string[] parameters={"Terraria.DataStructures.PlayerDeathReason","System.Int32","System.Int32","System.Boolean",
            "System.Boolean","System.Boolean","System.Int32","System.Boolean"};
        if(named.Length!=1) throw new InvalidOperationException("Player.Hurt overload set is no longer unique");
        var hurt=named[0];
        if(!hurt.IsPublic || hurt.IsStatic || !hurt.HasThis || !hurt.HasBody || hurt.ReturnType.FullName!="System.Double" ||
            hurt.Parameters.Count!=parameters.Length || !hurt.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(parameters) ||
            hurt.Body.ExceptionHandlers.Count!=0 || hurt.Body.Variables.Count!=56 || hurt.Body.Instructions.Count!=1475)
            throw new InvalidOperationException("Unreviewed native Player.Hurt body/signature");
        var originalInstructions=hurt.Body.Instructions.Select(i=>Tuple.Create(i,i.OpCode,i.Operand)).ToArray();
        var originalParameters=hurt.Parameters.ToArray();
        var originalReturns=hurt.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray();
        if(originalReturns.Length!=8 || originalReturns.Any(ret=>HasIncomingControlFlow(hurt,ret)) ||
            HasIncomingControlFlow(hurt,originalInstructions[0].Item1))
            throw new InvalidOperationException("Player.Hurt entry/return structure cannot be observed without redirecting native control flow");
        if(hurt.Body.Instructions.Any(i=>i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.FullName==ProbeTypeName))
            throw new InvalidOperationException("Player.Hurt already contains a test observer");

        var before=api.Methods.Single(m=>m.Name=="ObservePlayerHurtBefore");
        var after=api.Methods.Single(m=>m.Name=="ObservePlayerHurtAfter");
        var capture=api.Methods.Single(m=>m.Name=="CaptureHurtReason");
        string[] beforeParameters={"Terraria.Player","Terraria.DataStructures.PlayerDeathReason","System.Int32","System.Int32",
            "System.Boolean","System.Boolean","System.Boolean","System.Int32","System.Boolean"};
        if(!ObserverSignature(before,"System.Void",beforeParameters) || !ObserverSignature(after,"System.Double",new[]{"System.Double","Terraria.Player"}) ||
            !capture.IsStatic || capture.HasThis || !capture.HasBody || capture.ReturnType.FullName!="System.Void" ||
            !capture.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(new[]{"Terraria.DataStructures.PlayerDeathReason","Terraria.Player","ChaiteGameProbe/HurtObservationPending&"}) ||
            before.Body.ExceptionHandlers.Count!=1 || before.Body.ExceptionHandlers[0].HandlerType!=ExceptionHandlerType.Catch ||
            before.Body.ExceptionHandlers[0].CatchType.FullName!="System.Exception" ||
            after.Body.ExceptionHandlers.Count!=1 || after.Body.ExceptionHandlers[0].HandlerType!=ExceptionHandlerType.Catch ||
            after.Body.ExceptionHandlers[0].CatchType.FullName!="System.Exception" ||
            before.Body.Instructions.Any(i=>i.OpCode==OpCodes.Starg || i.OpCode==OpCodes.Starg_S || i.OpCode==OpCodes.Ldarga || i.OpCode==OpCodes.Ldarga_S) ||
            after.Body.Instructions.Any(i=>i.OpCode==OpCodes.Starg || i.OpCode==OpCodes.Starg_S || i.OpCode==OpCodes.Ldarga || i.OpCode==OpCodes.Ldarga_S))
            throw new InvalidOperationException("Unreviewed Player.Hurt observer implementation/signature");
        var afterReturns=after.Body.Instructions.Where(i=>i.OpCode==OpCodes.Ret).ToArray();
        if(afterReturns.Length!=1 || afterReturns[0].Previous==null || afterReturns[0].Previous.OpCode!=OpCodes.Ldarg_0 ||
            HasIncomingControlFlow(after,afterReturns[0]))
            throw new InvalidOperationException("Player.Hurt post-observer does not return its original double argument");
        foreach(var observer in new[]{before,after})
            if(observer.Body.Instructions.Count(i=>IsProbeCall(i,"get_IsBattleObservation"))!=1)
                throw new InvalidOperationException("Player.Hurt observer lacks the explicit Boss-only Motion/Flight guard");
        ValidateHurtHotPath(api,before,after,capture);

        var beforeReference=module.ImportReference(before);
        var afterReference=module.ImportReference(after);
        var il=hurt.Body.GetILProcessor();var first=hurt.Body.Instructions[0];
        var prefix=new Instruction[2+hurt.Parameters.Count];
        prefix[0]=il.Create(OpCodes.Ldarg_0);il.InsertBefore(first,prefix[0]);
        for(int index=0;index<hurt.Parameters.Count;index++)
        {
            prefix[index+1]=il.Create(OpCodes.Ldarg,hurt.Parameters[index]);
            il.InsertBefore(first,prefix[index+1]);
        }
        prefix[prefix.Length-1]=il.Create(OpCodes.Call,beforeReference);il.InsertBefore(first,prefix[prefix.Length-1]);
        foreach(var ret in originalReturns)
        {
            il.InsertBefore(ret,il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(ret,il.Create(OpCodes.Call,afterReference));
        }

        if(hurt.Body.Instructions.Count!=originalInstructions.Length+prefix.Length+2*originalReturns.Length ||
            hurt.Body.Instructions.Count(i=>IsProbeCall(i,"ObservePlayerHurtBefore"))!=1 ||
            hurt.Body.Instructions.Count(i=>IsProbeCall(i,"ObservePlayerHurtAfter"))!=originalReturns.Length ||
            hurt.Body.Instructions.Count(i=>i.OpCode==OpCodes.Ret)!=originalReturns.Length ||
            originalReturns.Any(ret=>ret.Previous==null || !IsProbeCall(ret.Previous,"ObservePlayerHurtAfter") ||
                ret.Previous.Previous==null || ret.Previous.Previous.OpCode!=OpCodes.Ldarg_0 || HasIncomingControlFlow(hurt,ret)))
            throw new InvalidOperationException("Player.Hurt before/after observer layout is incomplete");
        for(int index=0;index<prefix.Length;index++)
            if(hurt.Body.Instructions[index]!=prefix[index]) throw new InvalidOperationException("Player.Hurt prefix observer is not the only entry path");
        if(!hurt.Parameters.SequenceEqual(originalParameters) || hurt.Body.Variables.Count!=56 || hurt.Body.ExceptionHandlers.Count!=0)
            throw new InvalidOperationException("Player.Hurt parameters/locals/EH changed during observation patching");
        int previousIndex=-1;
        foreach(var original in originalInstructions)
        {
            int currentIndex=hurt.Body.Instructions.IndexOf(original.Item1);
            if(currentIndex<=previousIndex || original.Item1.OpCode!=original.Item2 || !Object.ReferenceEquals(original.Item1.Operand,original.Item3))
                throw new InvalidOperationException("A native Player.Hurt instruction or control-flow operand changed");
            previousIndex=currentIndex;
        }
    }
    static bool ObserverSignature(MethodDefinition method,string returnType,string[] parameters)
    {
        return method.IsPublic && method.IsStatic && !method.HasThis && method.HasBody &&
            method.DeclaringType.FullName==ProbeTypeName && method.ReturnType.FullName==returnType &&
            method.Parameters.Select(p=>p.ParameterType.FullName).SequenceEqual(parameters);
    }
    static void ValidateHurtHotPath(TypeDefinition api,MethodDefinition before,MethodDefinition after,MethodDefinition capture)
    {
        var pending=api.NestedTypes.Single(t=>t.FullName=="ChaiteGameProbe/HurtObservationPending");
        if(pending.Fields.Any(f=>f.Name=="ReasonText") || api.Fields.Any(f=>f.Name=="hurtObservationReasonTextFailures"))
            throw new InvalidOperationException("Randomized death-text state must not exist in Hurt observations");
        if(api.Methods.Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Any(i=>i.Operand is MethodReference &&
            new[]{"GetDeathText","CreateDeathMessage","RandomFromCategory"}.Contains(((MethodReference)i.Operand).Name)))
            throw new InvalidOperationException("The probe must not generate randomized native death text");
        var guard=api.Methods.Single(m=>m.Name=="get_IsBattleObservation");
        if(!guard.IsStatic || guard.HasThis || !guard.HasBody || guard.ReturnType.FullName!="System.Boolean" || guard.Parameters.Count!=0)
            throw new InvalidOperationException("Player.Hurt observation scope guard changed");
        if(before.Body.Instructions.Count(i=>IsProbeCall(i,"CaptureHurtReason"))!=1 ||
            after.Body.Instructions.Any(i=>IsProbeCall(i,"CaptureHurtReason")) ||
            capture.Body.Instructions.Any(i=>i.Operand is MethodReference && ((MethodReference)i.Operand).DeclaringType.FullName==ProbeTypeName) ||
            guard.Body.Instructions.Any(i=>i.Operand is MethodReference))
            throw new InvalidOperationException("Player.Hurt hot path gained an unreviewed local helper/call edge");

        string[] allowedNativeMethods={
            "System.String Terraria.DataStructures.PlayerDeathReason::get_CustomReason()",
            "System.Nullable`1<System.Int32> Terraria.DataStructures.PlayerDeathReason::get_SourceOtherIndex()",
            "System.Nullable`1<System.Int32> Terraria.DataStructures.PlayerDeathReason::get_SourceProjectileType()",
            "System.Boolean Terraria.DataStructures.PlayerDeathReason::TryGetCausingEntity(Terraria.Entity&)"
        };
        string[] allowedNativeFields={
            "System.Int32 Terraria.Entity::whoAmI",
            "Microsoft.Xna.Framework.Vector2 Terraria.Entity::position","Microsoft.Xna.Framework.Vector2 Terraria.Entity::velocity",
            "System.Int32 Terraria.Player::statLife","System.Boolean Terraria.Player::active","System.Boolean Terraria.Player::hostile",
            "System.Int32 Terraria.NPC::type","System.Boolean Terraria.NPC::active","System.Int32 Terraria.NPC::life",
            "System.Int32 Terraria.NPC::lifeMax","System.Int32 Terraria.NPC::damage","System.Boolean Terraria.NPC::friendly",
            "System.Int32 Terraria.Projectile::type","System.Int32 Terraria.Projectile::owner","System.Boolean Terraria.Projectile::active",
            "System.Int32 Terraria.Projectile::damage","System.Boolean Terraria.Projectile::hostile","System.Boolean Terraria.Projectile::friendly"
        };
        string[] allowedVectorFields={"System.Single Microsoft.Xna.Framework.Vector2::X","System.Single Microsoft.Xna.Framework.Vector2::Y"};
        foreach(var current in new[]{before,after,capture,guard})
        foreach(var instruction in current.Body.Instructions)
        {
            if(instruction.OpCode==OpCodes.Newarr || instruction.OpCode==OpCodes.Box || instruction.OpCode==OpCodes.Localloc ||
                instruction.OpCode==OpCodes.Calli || instruction.OpCode==OpCodes.Ldftn || instruction.OpCode==OpCodes.Ldvirtftn)
                throw new InvalidOperationException("Player.Hurt hot path gained allocation/indirect-call IL: "+current.Name+" "+instruction);
            var called=instruction.Operand as MethodReference;
            if(called!=null)
            {
                bool allowed=false;
                if(called.DeclaringType.FullName==ProbeTypeName)
                    allowed=(current==before && (called.Name=="get_IsBattleObservation" || called.Name=="CaptureHurtReason")) ||
                        (current==after && called.Name=="get_IsBattleObservation");
                else if(called.DeclaringType.FullName.StartsWith("Terraria.",StringComparison.Ordinal))
                    allowed=current==capture && called.DeclaringType.Scope.Name=="Terraria" && allowedNativeMethods.Contains(called.FullName) &&
                        (instruction.OpCode==OpCodes.Call || instruction.OpCode==OpCodes.Callvirt);
                else if(called.FullName=="System.Int32 System.Math::Max(System.Int32,System.Int32)")
                    allowed=current==before && instruction.OpCode==OpCodes.Call;
                else if(called.Name==".ctor" && called.DeclaringType.FullName.StartsWith("System.Nullable`1<",StringComparison.Ordinal))
                    allowed=current==capture && instruction.OpCode==OpCodes.Newobj && called.DeclaringType.IsValueType &&
                        called.DeclaringType.Scope.Name=="mscorlib";
                if(!allowed) throw new InvalidOperationException("Player.Hurt hot path gained an unreviewed call: "+current.Name+" -> "+called.FullName);
            }
            var field=instruction.Operand as FieldReference;
            if(field!=null && field.DeclaringType.FullName.StartsWith("Terraria.",StringComparison.Ordinal) &&
                (field.DeclaringType.Scope.Name!="Terraria" || instruction.OpCode!=OpCodes.Ldfld || !allowedNativeFields.Contains(field.FullName)))
                throw new InvalidOperationException("Player.Hurt hot path gained a native field access: "+current.Name+" -> "+field.FullName);
            if(field!=null && field.DeclaringType.FullName=="Microsoft.Xna.Framework.Vector2" &&
                (instruction.OpCode!=OpCodes.Ldfld || !allowedVectorFields.Contains(field.FullName)))
                throw new InvalidOperationException("Player.Hurt hot path gained an unreviewed vector field access: "+field.FullName);
            if(field!=null && !field.DeclaringType.FullName.StartsWith(ProbeTypeName,StringComparison.Ordinal) &&
                !field.DeclaringType.FullName.StartsWith("Terraria.",StringComparison.Ordinal) &&
                field.DeclaringType.FullName!="Microsoft.Xna.Framework.Vector2")
                throw new InvalidOperationException("Player.Hurt hot path gained an unreviewed external field access: "+current.Name+" -> "+field.FullName);
        }
    }
    static bool HasIncomingControlFlow(MethodDefinition method,Instruction target)
    {
        foreach(var instruction in method.Body.Instructions)
        {
            if(Object.ReferenceEquals(instruction.Operand,target)) return true;
            var targets=instruction.Operand as Instruction[];
            if(targets!=null && targets.Any(candidate=>Object.ReferenceEquals(candidate,target))) return true;
        }
        return false;
    }
    static bool ContainsProbeReference(TypeDefinition type)
    {
        if(type.Methods.Any(m=>m.HasBody && m.Body.Instructions.Any(i=>i.Operand is MethodReference &&
            ((MethodReference)i.Operand).DeclaringType.FullName==ProbeTypeName))) return true;
        return type.NestedTypes.Any(ContainsProbeReference);
    }
    static bool IsProbeCall(Instruction instruction,string name)
    {
        var method=instruction.Operand as MethodReference;
        return instruction.OpCode==OpCodes.Call && method!=null && method.DeclaringType.FullName==ProbeTypeName && method.Name==name;
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
