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
            True(opening.Horizontal == -1,
                "opening horizontal=" + opening.Horizontal);
            input.NativeState = 1; input.NativeTimer = 1;
            player.OnGround = false; player.Velocity = new Vec2(-6, -5);
            var escape = controller.Tick(in input, player, in boss, arena);
            True(escape.Horizontal == -1,
                "escape horizontal=" + escape.Horizontal);
            True(escape.Vertical == -1,
                "escape vertical=" + escape.Vertical);
            False(escape.Dash);
            boss.Position = new Vec2(1500, 1200);
            player.Velocity = new Vec2(-6, 2); input.NativeTimer = 15;
            var crossed = controller.Tick(in input, player, in boss, arena);
            Equal(escape.Horizontal, crossed.Horizontal);
            Equal(escape.Vertical, crossed.Vertical);
            input.NativeState = 0;
            player.Position = new Vec2(1200, 500);
            var landing = controller.Tick(in input, player, in boss, arena);
            Equal(1, landing.Vertical); False(landing.Jump);
            player.Position = new Vec2(1000, 1000); player.OnGround = true; player.WingTime = 130;
            var launched = controller.Tick(in input, player, in boss, arena);
            True(launched.Horizontal == 1,
                "launched horizontal=" + launched.Horizontal);
            True(launched.Vertical == -1,
                "launched vertical=" + launched.Vertical);
            controller.Reset();
            var reset = controller.Tick(in input, player, in boss, arena);
            True(reset.Horizontal == -1,
                "reset horizontal=" + reset.Horizontal);
        }

        private static void FishronShieldEdgeIsEarlyAndSingle()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000), Width = 20, Height = 40,
                WorldLeft = 0, WorldRight = 5000, WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370, Position = new Vec2(2500, 970), Width = 150,
                Height = 100, Velocity = new Vec2(-17, 0) };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370, Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 1, NativeTimer = 1 };
            var counter = controller.Tick(in input, player, in boss, arena, true);
            True(counter.Dash); Equal(-1, counter.Horizontal);
            input.NativeTimer++;
            False(controller.Tick(in input, player, in boss, arena, true).Dash);
            controller.Reset();
            False(controller.Tick(in input, player, in boss, arena, false).Dash);
        }

        private static void FishronCloseContactDoesNotCounterDash()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000),
                Width = 20, Height = 40, WorldRight = 5000 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2100, 1000), Width = 150, Height = 100,
                Velocity = new Vec2(-17, 0) };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 1, NativeTimer = 1 };
            var mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true
            };
            var close = controller.Tick(in input, player, in boss, arena,
                mobility);
            False(close.Dash);
            Equal(1, close.Vertical);
        }

        private static void FishronWingScriptIgnoresThreatListContents()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000),
                Width = 20, Height = 40, WorldRight = 5000 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 900), Width = 150, Height = 100,
                Velocity = new Vec2(-17, 4) };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 3, NativeTimer = 4 };
            var mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true
            };
            var emptyArena = new ArenaSnapshot();
            var populatedArena = new ArenaSnapshot
            {
                LocalOpenBounds = new RectF(100, 100, 40, 40),
                ClearanceLeft = 1,
                ClearanceRight = 9999
            };
            var first = new FishronWingScript().Tick(in input, player,
                in boss, emptyArena, mobility);
            var second = new FishronWingScript().Tick(in input, player,
                in boss, populatedArena, mobility);
            Equal(first.Horizontal, second.Horizontal);
            Equal(first.Vertical, second.Vertical);
            Equal(first.Dash, second.Dash);
            Equal("fishron-wing-hazard-run-edge", first.Phase);
        }

        private static void FishronChilletUsesReviewedNativeDashCadence()
        {
            var controller = new FishronChilletScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 980), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 800, Right = 3600 } };
            var mobility = new MobilitySnapshot
            {
                SelectedMountIdentityKnown = true,
                SelectedMountType = 64,
                ActiveMountReleaseReady = true
            };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronTrustyChillet,
                NativeState = 0, NativeSequence = 0 };
            var mount = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(mount.Accepted); True(mount.ToggleMount); False(mount.Fire);

            mobility.MountActive = true;
            mobility.ActiveMountIdentityKnown = true;
            mobility.ActiveMountType = 64;
            mobility.DashType = 6;
            mobility.DashReady = true;
            var dashState = mobility.EyeShieldDash;
            dashState.ReleaseDash = true;
            mobility.EyeShieldDash = dashState;

            input.NativeState = 1; input.NativeSequence = 0;
            False(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "the first P1 charge is deliberately passed");
            input.NativeState = 0; input.NativeSequence = 2;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 1;
            var second = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(second.Dash); Equal(1, second.Horizontal);
            False(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "one native charge cannot emit two dash edges");

            input.NativeState = 5; input.NativeSequence = 4;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 6;
            False(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "the third P2 charge keeps running");
            input.NativeState = 12; input.NativeSequence = 4;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 11; input.NativeSequence = 5;
            True(controller.Tick(in input, player, in boss, arena,
                mobility).Dash, "every P3 charge is a reviewed counter edge");
        }

        private static void FishronChilletRejectsWrongMountAndShortRunway()
        {
            var player = new PlayerSnapshot { Position = new Vec2(1000, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(1400, 1000), Width = 150, Height = 100 };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronTrustyChillet };
            var shortArena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 500, Right = 1500 } };
            False(new FishronChilletScript().Tick(in input, player, in boss,
                shortArena, new MobilitySnapshot()).Accepted);

            var arena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 0, Right = 3000 } };
            var wrong = new MobilitySnapshot { MountActive = true,
                ActiveMountIdentityKnown = true, ActiveMountType = 63 };
            False(new FishronChilletScript().Tick(in input, player, in boss,
                arena, wrong).Accepted);
        }

        private static void FishronChilletLockedRouteAcceptsOnlyItsActiveMount()
        {
            var scene = CombatScenario(370);
            scene.Player.FunctionalEquipmentIdentityKnown = true;
            scene.Mobility.FormulaAccessoryScanKnown = true;
            scene.Mobility.SelectedMountIdentityKnown = true;
            scene.Mobility.SelectedMountType = 64;
            FormulaRoute route;
            string reason;
            True(FormulaMobilityContract.TrySelectRoute(scene, 370,
                out route, out reason), reason);
            Equal(FormulaRoute.FishronTrustyChillet, route);

            scene.Mobility.MountActive = true;
            scene.Mobility.ActiveMountIdentityKnown = true;
            scene.Mobility.ActiveMountType = 64;
            True(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                route, out reason), reason);
            scene.Mobility.ActiveMountType = 65;
            False(FormulaMobilityContract.TryValidateLockedRoute(scene, 370,
                route, out reason));
        }

        private static void FishronQueenSlimeUsesMountAndFixedRunway()
        {
            var controller = new FishronQueenSlimeScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 980), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot { FloorSupport = new SupportSpan
                { Valid = true, Left = 600, Right = 3400 } };
            var mobility = new MobilitySnapshot
            {
                SelectedMountIdentityKnown = true,
                SelectedMountType = FishronQueenSlimeScript.QueenSlimeMountType,
                ActiveMountReleaseReady = true
            };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronQueenSlime,
                NativeState = 0, NativeSequence = 0 };
            var mount = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(mount.Accepted); True(mount.ToggleMount); False(mount.Fire);

            mobility.MountActive = true;
            mobility.ActiveMountIdentityKnown = true;
            mobility.ActiveMountType =
                FishronQueenSlimeScript.QueenSlimeMountType;
            var runway = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(runway.Accepted); True(runway.Fire);
            input.NativeState = 1;
            var charge = controller.Tick(in input, player, in boss, arena,
                mobility);
            True(charge.Jump); Equal(1, charge.Horizontal);
            Equal(-1, charge.Vertical);
        }

        private static void EmpressFlightUsesReviewedMountAndRainGate()
        {
            var broom = new EmpressFlightScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 1200),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 636,
                Position = new Vec2(2200, 900), Width = 80, Height = 80 };
            var arena = new ArenaSnapshot
            {
                ClearanceLeft = 1500, ClearanceRight = 1800,
                ClearanceUp = 700, ClearanceDown = 900
            };
            var mobility = new MobilitySnapshot
            {
                SelectedMountIdentityKnown = true,
                SelectedMountType = EmpressFlightScript.WitchBroomMountType,
                ActiveMountReleaseReady = true
            };
            var difficulty = new DifficultySnapshot
            {
                RainKnown = true, Rain = false
            };
            var input = new FormulaScriptInput { BossType = 636,
                Route = FormulaRoute.EmpressBroom,
                NativeState = 8, NativeSequence = 1, PlayerBelowBoss = true };
            var mount = broom.Tick(in input, player, in boss, arena,
                mobility, difficulty);
            True(mount.Accepted); True(mount.ToggleMount); False(mount.Fire);

            mobility.MountActive = true;
            mobility.ActiveMountIdentityKnown = true;
            mobility.ActiveMountType = EmpressFlightScript.WitchBroomMountType;
            var dash = broom.Tick(in input, player, in boss, arena,
                mobility, difficulty);
            True(dash.Accepted); True(dash.Fire);
            Equal(0, dash.Horizontal); Equal(-1, dash.Vertical);
            True(dash.Jump);

            var rain = new EmpressFlightScript();
            input.Route = FormulaRoute.EmpressRainFishron;
            mobility.MountActive = false;
            mobility.ActiveMountIdentityKnown = false;
            mobility.SelectedMountType =
                EmpressFlightScript.ShrimpyTruffleMountType;
            False(rain.Tick(in input, player, in boss, arena,
                mobility, difficulty).Accepted);
            difficulty.Rain = true;
            True(rain.Tick(in input, player, in boss, arena,
                mobility, difficulty).Accepted);
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
