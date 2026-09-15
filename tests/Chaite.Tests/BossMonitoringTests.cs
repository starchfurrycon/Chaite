using System;
using System.Linq;
using Chaite.Core;
using Mono.Cecil;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        /// <summary>The threats a wing circuit cannot answer by moving. The
        /// split that matters is the removal cost, not the homing, so the
        /// one-life bubble and the two armoured ones must stay distinct.</summary>
        private static void FishronThreatIdentitiesSeparateByRemovalCost()
        {
            True(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.DetonatingBubbleType));
            True(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.LargeSharknadoBubbleType));
            True(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.SmallSharknadoBubbleType));
            False(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.SharknadoType));
            False(FishronThreatCatalog.IsHomingBubble(
                FishronThreatCatalog.SharknadoBoltType));
            False(FishronThreatCatalog.IsArmouredBubble(
                FishronThreatCatalog.DetonatingBubbleType));
            True(FishronThreatCatalog.IsArmouredBubble(
                FishronThreatCatalog.LargeSharknadoBubbleType));
            True(FishronThreatCatalog.IsArmouredBubble(
                FishronThreatCatalog.SmallSharknadoBubbleType));
            Equal(1, FishronThreatCatalog.DetonatingBubbleLife);
            Equal(100, FishronThreatCatalog.SharknadoBubbleLife);
            Equal(100, FishronThreatCatalog.SharknadoBubbleDefense);
            Equal(540, FishronThreatCatalog.SharknadoLifetimeTicks);
        }

        /// <summary>The reviewed W cycle: horizontal, ascend, descend,
        /// repeating, with one Shield-of-Cthulhu edge spent per charge.</summary>
        private static void FishronWingFollowsReviewedChargeCycle()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true
            };
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 1 };
            var horizontal = new int[4];
            var vertical = new int[4];
            var dashed = new bool[4];
            for (var beat = 0; beat < 4; beat++)
            {
                input.NativeState = 0;
                input.NativeTimer = 1;
                controller.Tick(in input, player, in boss, arena, mobility);
                input.NativeState = 1;
                input.NativeTimer = 1;
                var escape = controller.Tick(in input, player, in boss, arena,
                    mobility);
                horizontal[beat] = escape.Horizontal;
                vertical[beat] = escape.Vertical;
                dashed[beat] = escape.Dash;
            }
            // Away from the Boss on the horizontal axis of every beat, because
            // the shield dash follows the facing direction.
            for (var beat = 0; beat < 4; beat++)
                Equal(-1, horizontal[beat]);
            Equal(0, vertical[0]);
            Equal(-1, vertical[1]);
            Equal(1, vertical[2]);
            Equal(0, vertical[3]);
            for (var beat = 0; beat < 4; beat++)
                True(dashed[beat], "beat " + beat + " must spend the dash edge");
        }

        /// <summary>A charge aimed from closer than the standoff cannot be
        /// cleared by any available speed, so the circuit flees whenever the
        /// hover puts the Boss inside it.</summary>
        private static void FishronWingKeepsStandoffGap()
        {
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130, OnGround = true };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2300, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 0,
                NativeTimer = 2 };
            var close = new FishronWingScript().Tick(in input, player, in boss,
                arena);
            Equal(-1, close.Horizontal);
            // Past the standoff the circuit patrols instead of fleeing.
            boss.Position = new Vec2(4400, 5000);
            var far = new FishronWingScript().Tick(in input, player, in boss,
                arena);
            True(far.Phase.StartsWith("fishron-wing-cruise"),
                "phase=" + far.Phase);
        }

        /// <summary>A projectile attack restarts the charge group, so the cycle
        /// must begin again from its horizontal beat rather than continuing.</summary>
        private static void FishronWingRestartsCycleAfterProjectileAttack()
        {
            var controller = new FishronWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(2000, 5000),
                Width = 20, Height = 40, WorldLeft = 0, WorldRight = 67200,
                WingTime = 130 };
            var boss = new TargetSnapshot { Type = 370,
                Position = new Vec2(2600, 5000), Width = 150, Height = 100 };
            var arena = new ArenaSnapshot();
            var mobility = new MobilitySnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash, NativeState = 1,
                NativeTimer = 1 };
            Equal(0, controller.Tick(in input, player, in boss, arena,
                mobility).Vertical);
            input.NativeState = 0;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 1;
            Equal(-1, controller.Tick(in input, player, in boss, arena,
                mobility).Vertical);
            // A Bubble phase ends the group.
            input.NativeState = 2;
            controller.Tick(in input, player, in boss, arena, mobility);
            input.NativeState = 1;
            Equal(0, controller.Tick(in input, player, in boss, arena,
                mobility).Vertical);
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
                NativeState = 2, NativeTimer = 4 };
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
            Equal("fishron-wing-bubble-line", first.Phase);
        }

        private static void FishronFormulaRejectsImpossibleNativeTuples()
        {
            var difficulty = new DifficultySnapshot();
            var input = new FormulaScriptInput { BossType = 370,
                Route = FormulaRoute.FishronFairyWingsDash,
                NativeState = 11, NativeTimer = 1, NativeSequence = 0 };
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            difficulty.Expert = true;
            True(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeSequence = 1;
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeState = 1;
            input.NativeSequence = 0;
            input.NativeTimer = 29;
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeTimer = 27;
            True(FishronFormulaStateContract.IsValid(in input,
                difficulty, false));
            input.NativeTimer = 27;
            True(FishronFormulaStateContract.IsValid(in input,
                difficulty, true));
            input.NativeTimer = 28;
            False(FishronFormulaStateContract.IsValid(in input,
                difficulty, true));
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

        private static void FormulaAdmissionSeparatesMobilityAndOutputRefusal()
        {
            var scene = CombatScenario(370);
            scene.Player.FunctionalEquipmentIdentityKnown = true;
            scene.Mobility.FormulaAccessoryScanKnown = true;
            scene.Mobility.SelectedMountIdentityKnown = true;
            scene.Mobility.SelectedMountType = 64;
            scene.Weapon.NativeProfileRequired = true;
            scene.Weapon.WeaponId = -1;
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            bool mobilityRefusal;
            False(planner.PrepareForMonitoredFormulaEncounter(scene, 370,
                FormulaRoute.FishronTrustyChillet, out reason,
                out mobilityRefusal));
            False(mobilityRefusal);
            True(reason.Contains("output route"), reason);

            False(planner.PrepareForMonitoredFormulaEncounter(scene, 370,
                FormulaRoute.FishronQueenSlime, out reason,
                out mobilityRefusal));
            True(mobilityRefusal);
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
                NativeState = 8, NativeSequence = 2, PlayerBelowBoss = true,
                NativeFormKnown = true };
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

        private static void EmpressWingUsesOneRepositionDashEdge()
        {
            var script = new EmpressWingScript();
            var player = new PlayerSnapshot { Position = new Vec2(1800, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 636,
                Position = new Vec2(2200, 800), Width = 80, Height = 80 };
            var arena = new ArenaSnapshot
            {
                ClearanceLeft = 800,
                ClearanceRight = 1200
            };
            var mobility = new MobilitySnapshot
            {
                CanDash = true,
                DashReady = true
            };
            var difficulty = new DifficultySnapshot();
            var input = new FormulaScriptInput { BossType = 636,
                Route = FormulaRoute.EmpressStrongWingsDash,
                NativeState = 1, NativeTimer = 1, NativeSequence = 2,
                NativeFormKnown = true };
            var edge = script.Tick(in input, player, in boss, arena,
                mobility, difficulty);
            True(edge.Accepted); True(edge.Dash); Equal(-1, edge.Horizontal);
            input.NativeTimer++;
            False(script.Tick(in input, player, in boss, arena,
                mobility, difficulty).Dash);

            input.NativeState = 8;
            input.NativeTimer = 41;
            input.PlayerBelowBoss = true;
            var charge = script.Tick(in input, player, in boss, arena,
                mobility, difficulty);
            False(charge.Dash); Equal(0, charge.Horizontal);
            Equal(-1, charge.Vertical); True(charge.Jump);
            input.PlayerBelowBoss = false;
            input.NativeTimer++;
            var crossed = script.Tick(in input, player, in boss, arena,
                mobility, difficulty);
            Equal(charge.Vertical, crossed.Vertical);
            mobility.MountActive = true;
            False(script.Tick(in input, player, in boss, arena,
                mobility, difficulty).Accepted);
        }

        private static void EmpressFormulaKeepsLoopAcrossAttackBoundaries()
        {
            var player = new PlayerSnapshot { Position = new Vec2(1800, 1000),
                Width = 20, Height = 40 };
            var boss = new TargetSnapshot { Type = 636,
                Position = new Vec2(2200, 800), Width = 80, Height = 80 };
            var arena = new ArenaSnapshot
            {
                ClearanceLeft = 800,
                ClearanceRight = 1200
            };
            var mobility = new MobilitySnapshot();
            var difficulty = new DifficultySnapshot();
            var input = new FormulaScriptInput { BossType = 636,
                Route = FormulaRoute.EmpressStrongWingsDash,
                NativeState = 2, NativeTimer = 140, NativeSequence = 1,
                NativeFormKnown = true };
            var wing = new EmpressWingScript();
            FormulaScriptOutput before = default(FormulaScriptOutput);
            // Stop one tick short of the quadrant boundary so the next attack
            // must land in the following quadrant.
            for (var tick = 0; tick < 59; tick++)
                before = wing.Tick(in input, player, in boss, arena,
                    mobility, difficulty);
            input.NativeState = 1;
            input.NativeTimer = 1;
            input.NativeSequence = 2;
            mobility.CanDash = false;
            var after = wing.Tick(in input, player, in boss, arena,
                mobility, difficulty);
            True(before.Accepted && after.Accepted);
            True(before.Horizontal != after.Horizontal ||
                before.Vertical != after.Vertical,
                "attack boundary must advance the continuous loop");

            input.Route = FormulaRoute.EmpressBroom;
            input.NativeState = 8;
            input.NativeTimer = 100;
            mobility.MountActive = true;
            mobility.ActiveMountIdentityKnown = true;
            mobility.ActiveMountType = 23;
            var mount = new EmpressFlightScript();
            mount.Tick(in input, player, in boss, arena, mobility,
                new DifficultySnapshot());
            input.NativeState = 1;
            input.NativeTimer = 1;
            var reversed = mount.Tick(in input, player, in boss, arena,
                mobility, new DifficultySnapshot());
            True(reversed.Accepted);
            Equal(-1, reversed.Horizontal);

            input.NativeState = 6;
            input.NativeTimer = 1;
            input.NativeSequence = 3;
            input.PlayerBelowBoss = true;
            input.PlayerRightOfBoss = true;
            var sun = mount.Tick(in input, player, in boss, arena,
                mobility, new DifficultySnapshot());
            True(sun.Accepted);
            True(sun.Horizontal != 0 && sun.Vertical != 0);
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
