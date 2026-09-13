using System;
using System.Linq;
using Chaite.Core;
using Mono.Cecil;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void FishronWingCircuitKeepsDashDirectionAndLands()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000), Width = 20, Height = 40,
                WorldLeft = 0, WorldRight = 5000, WingTime = 130, OnGround = true };
            var boss = new TargetSnapshot { Type = 370, Position = new Vec2(2600, 800), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370, Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0 };
            var opening = controller.Tick(in input, player, in boss, arena);
            Equal(-1, opening.Horizontal); // Move away from the emerging Boss.
            input.NativeState = 1; input.NativeTimer = 1;
            player.OnGround = false; player.Velocity = new Vec2(-6, -5);
            var escape = controller.Tick(in input, player, in boss, arena);
            Equal(-1, escape.Vertical); False(escape.Dash);
            boss.Position = new Vec2(1500, 1200);
            player.Velocity = new Vec2(-6, 2); input.NativeTimer = 15;
            var crossed = controller.Tick(in input, player, in boss, arena);
            Equal(escape.Horizontal, crossed.Horizontal); Equal(escape.Vertical, crossed.Vertical);
            input.NativeState = 0;
            player.Position = new Vec2(1200, 500);
            var landing = controller.Tick(in input, player, in boss, arena);
            Equal(1, landing.Vertical); False(landing.Jump);
            player.Position = new Vec2(1000, 1000); player.OnGround = true; player.WingTime = 130;
            var launched = controller.Tick(in input, player, in boss, arena);
            Equal(1, launched.Horizontal); Equal(-1, launched.Vertical);
            controller.Reset();
            var reset = controller.Tick(in input, player, in boss, arena);
            Equal(-1, reset.Horizontal);
        }

        private static void FishronShieldCounterIsAlignedAndSingleEdge()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000), Width = 20, Height = 40,
                WorldLeft = 0, WorldRight = 5000, WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370, Position = new Vec2(2090, 970), Width = 150,
                Height = 100, Velocity = new Vec2(-17, 0) };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370, Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 1, NativeTimer = 1 };
            var counter = controller.Tick(in input, player, in boss, arena, true);
            True(counter.Dash); Equal(1, counter.Horizontal);
            input.NativeTimer++;
            False(controller.Tick(in input, player, in boss, arena, true).Dash);
            controller.Reset();
            False(controller.Tick(in input, player, in boss, arena, false).Dash);
            controller.Reset(); boss.Position.Y -= 200;
            False(controller.Tick(in input, player, in boss, arena, true).Dash);
            controller.Reset(); boss.Position.Y += 200; boss.Velocity.X = 17;
            False(controller.Tick(in input, player, in boss, arena, true).Dash);
            controller.Reset();
            boss.Position = new Vec2(1940, 1040); boss.Velocity = new Vec2(-13, -10);
            // X has passed, but the diagonal body still approaches from below.
            var diagonal = controller.Tick(in input, player, in boss, arena, true);
            True(diagonal.Dash); Equal(1, diagonal.Horizontal);
            controller.Reset(); boss.Velocity.Y = 10;
            False(controller.Tick(in input, player, in boss, arena, true).Dash);
        }

        private static void BossMonitoringDoesNotOwnControls()
        {
            var controller = new EncounterController(3);
            var observation = new EncounterObservation { StartAuthorized = true, PlayerLife = 400 };
            var armed = controller.ArmMonitoring(observation);
            Equal(SessionState.Monitoring, armed.Current);
            Equal(AudioCue.MonitorArmed, armed.Cue);
            False(armed.ApplyControls);
            False(controller.IsControlling);
            True(controller.IsSessionActive);
            observation.PlayerLife = 250;
            for (var i = 0; i < 2000; i++)
            {
                var update = controller.Update(observation);
                False(update.ApplyControls);
                Equal(AudioCue.None, update.Cue);
                Equal(SessionState.Monitoring, update.Current);
            }
            Equal(AudioCue.None, controller.ArmMonitoring(observation).Cue);
            controller.Cancel();
            False(controller.IsSessionActive);
            controller.ReturnToIdle();
            Equal(AudioCue.MonitorArmed, controller.ArmMonitoring(observation).Cue);
        }

        private static void BossMonitoringRejectsMidFightAndDeadArming()
        {
            var controller = new EncounterController(3);
            var observation = new EncounterObservation { StartAuthorized = true, Flags = EncounterFlags.Boss };
            False(controller.ArmMonitoring(observation).ApplyControls);
            False(controller.IsSessionActive);
            observation.Flags = EncounterFlags.None;
            observation.PlayerDead = true;
            False(controller.ArmMonitoring(observation).ApplyControls);
            False(controller.IsSessionActive);
        }

        private static void BossMonitoringTransitionsOnceAndResetsLifeAccounting()
        {
            var controller = new EncounterController(3);
            controller.ArmMonitoring(new EncounterObservation { StartAuthorized = true, PlayerLife = 400 });
            var arrived = new EncounterObservation { StartAuthorized = true, Flags = EncounterFlags.Boss,
                PlayerLife = 250, ActiveBossKeys = new[] { 4 }, ActiveBossTypes = new[] { 370 } };
            var activation = controller.Activate(arrived);
            Equal(SessionState.EngagedAlive, activation.Current);
            Equal(AudioCue.TryMinnie, activation.Cue);
            True(activation.ApplyControls);
            Equal(AudioCue.None, controller.Update(arrived).Cue);
            Equal(AudioCue.None, controller.Activate(arrived).Cue);
            arrived.PlayerLife = 249;
            Equal(AudioCue.Man, controller.Update(arrived).Cue);
        }

        private static void BossMonitoringProductionHasNoSummonOrSurvivalPath()
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var runtime = FindCecilType(assembly, "Chaite.Plugin.Runtime");
                foreach (var method in runtime.Methods.Where(m => m.HasBody))
                    foreach (var instruction in method.Body.Instructions)
                    {
                        var call = instruction.Operand as MethodReference;
                        if (call == null) continue;
                        False(call.Name == "ExecuteBossStart" || call.Name == "FindBossStartPlan" || call.Name == "PlanSurvival",
                            "Production monitor must not retain auto-summon/survival calls: " + method.Name);
                        if (method.Name == "ArmBossMonitor" || method.Name == "StopBossMonitor")
                            False(call.Name == "ApplyPlan" || call.Name == "SetSelectedItem" || call.Name == "ClearCombatControls",
                                "Passive monitor must not write native controls/selection: " + method.Name);
                    }
            }
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(CombatPlanner).Assembly.Location))
            {
                var planner = FindCecilType(assembly, "Chaite.Core.CombatPlanner");
                var formula = FindCecilMethod(planner, "PlanFormula");
                foreach (var instruction in formula.Body.Instructions)
                {
                    var call = instruction.Operand as MethodReference;
                    if (call == null) continue;
                    False(call.Name == "FindBestCandidate" || call.Name == "Evaluate" || call.Name == "PlanSurvival" ||
                        call.Name == "TrySelectReadyMobilityRoute", "Formula path must not run old scoring or mobility selection");
                }
            }
        }
    }
}
