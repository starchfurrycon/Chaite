using Chaite.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunPriorityBossActiveRecoveryRegressions()
        {
            Run(nameof(PriorityBossMidFightPhasesChooseConvergentRecovery),
                PriorityBossMidFightPhasesChooseConvergentRecovery);
            Run(nameof(MoonLordCombatCoreStateClocksAdmitActiveRecovery),
                MoonLordCombatCoreStateClocksAdmitActiveRecovery);
            Run(nameof(MoonLordTerminalCoreStatesRejectActiveRecovery),
                MoonLordTerminalCoreStatesRejectActiveRecovery);
            Run(nameof(MoonLordSourceAndProjectileRecoveryMatrixIsBounded),
                MoonLordSourceAndProjectileRecoveryMatrixIsBounded);
            Run(nameof(MoonLordTerminalTransitionReturnsNeutralControl),
                MoonLordTerminalTransitionReturnsNeutralControl);
            Run(nameof(EmpressP1DashDirectionsAndWindowsAdmitActiveRecovery),
                EmpressP1DashDirectionsAndWindowsAdmitActiveRecovery);
            Run(nameof(EmpressNightRepositionJoinsItsExplicitMovementLoop),
                EmpressNightRepositionJoinsItsExplicitMovementLoop);
            Run(nameof(ActiveRecoveryRequiresThreeConsecutiveClosedFrames),
                ActiveRecoveryRequiresThreeConsecutiveClosedFrames);
            Run(nameof(ActiveRecoveryDoesNotResetForAdvancingNativeClockLabels),
                ActiveRecoveryDoesNotResetForAdvancingNativeClockLabels);
            Run(nameof(EmpressStateOneToTwoRebasesRecoveryDuringEmergency),
                EmpressStateOneToTwoRebasesRecoveryDuringEmergency);
            Run(nameof(BlockedActiveRecoveryReturnsControlInFiniteTime),
                BlockedActiveRecoveryReturnsControlInFiniteTime);
            Run(nameof(PhaseChurnCannotExtendActiveRecoveryForever),
                PhaseChurnCannotExtendActiveRecoveryForever);
            Run(nameof(EmergencyRouteChurnReturnsNeutralAtAbsoluteDeadline),
                EmergencyRouteChurnReturnsNeutralAtAbsoluteDeadline);
            Run(nameof(DeerclopsSlowRemainsInsideItsReviewedRecoveryLoop),
                DeerclopsSlowRemainsInsideItsReviewedRecoveryLoop);
            Run(nameof(PriorityBossActiveAdmissionRejectsInvalidNativeStateBeforeLatching),
                PriorityBossActiveAdmissionRejectsInvalidNativeStateBeforeLatching);
            Run(nameof(PriorityBossNativeStateDriftReturnsNeutralControlAfterAdmission),
                PriorityBossNativeStateDriftReturnsNeutralControlAfterAdmission);
            Run(nameof(ActiveEncounterDefersNativeOwnedMobilityThenRetries),
                ActiveEncounterDefersNativeOwnedMobilityThenRetries);
            Run(nameof(ActiveEncounterObservationWindowIsBounded),
                ActiveEncounterObservationWindowIsBounded);
        }

        private static void ActiveEncounterDefersNativeOwnedMobilityThenRetries()
        {
            foreach (var mounted in new[] { false, true })
            foreach (var grappling in new[] { false, true })
            {
                if (!mounted && !grappling) continue;
                var scene = PriorityRecoveryScene(222);
                scene.Mobility.MountActive = mounted;
                scene.Mobility.Grappling = grappling;
                var planner = new CombatPlanner(new PlannerSettings());
                string reason;
                Equal(ActiveEncounterPreparationResult.
                    AwaitingNativeMobilityRelease,
                    planner.PrepareForActiveEncounterDetailed(scene,
                        out reason));
                True(!string.IsNullOrEmpty(reason));
                Equal(-1, planner.LatchedOutputSlot);

                // Vanilla/player input resolves the native action. A fresh
                // snapshot is then admitted into the ordinary recovery loop.
                scene.Mobility.MountActive = false;
                scene.Mobility.Grappling = false;
                var ready = planner.PrepareForActiveEncounterDetailed(scene,
                    out reason);
                True(ready == ActiveEncounterPreparationResult.Ready,
                    reason);
                var plan = planner.Plan(scene);
                False(plan.RequestControlReturn, plan.ControlReturnReason);
                Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            }

            // Busy mobility never turns an unrelated/unsupported scene into a
            // deferred admission merely because a flag is set.
            var noBoss = PriorityRecoveryScene(222);
            noBoss.Targets.Clear();
            noBoss.Mobility.Grappling = true;
            var rejected = new CombatPlanner(new PlannerSettings()).
                PrepareForActiveEncounterDetailed(noBoss, out var failure);
            Equal(ActiveEncounterPreparationResult.Rejected, rejected);
            True(!string.IsNullOrEmpty(failure));
        }

        private static void ActiveEncounterObservationWindowIsBounded()
        {
            var window = new ActiveEncounterObservationWindow(3);
            window.Begin();
            True(window.Active);
            Equal(ActiveEncounterObservationResult.Waiting,
                window.Observe(true, true));
            Equal(ActiveEncounterObservationResult.Waiting,
                window.Observe(true, true));
            Equal(ActiveEncounterObservationResult.TimedOut,
                window.Observe(true, true));
            False(window.Active);

            window.Begin();
            Equal(ActiveEncounterObservationResult.ReadyToRetry,
                window.Observe(true, false));
            True(window.Active,
                "runtime must reset only after the retry result is consumed");
            window.Reset();

            window.Begin();
            Equal(ActiveEncounterObservationResult.EncounterEnded,
                window.Observe(false, true));
            False(window.Active);
        }

        private static void PriorityBossMidFightPhasesChooseConvergentRecovery()
        {
            var observedPhases = new Dictionary<string, HashSet<string>>();
            foreach (var item in PriorityRecoveryCases())
            {
                var scene = PriorityRecoveryScene(item.BossType);
                item.Configure(scene);
                RefreshRecoveryClearance(scene);
                var planner = new CombatPlanner(new PlannerSettings
                {
                    EmergencyRiskThreshold = float.MaxValue,
                    PatternSafeRiskThreshold = float.MaxValue
                });
                string reason;
                True(planner.PrepareForActiveEncounter(scene, out reason),
                    item.Name + " admission: " + reason);

                var plan = planner.Plan(scene);
                False(plan.RequestControlReturn,
                    item.Name + " returned control on its first recovery frame: " +
                    plan.ControlReturnReason);
                True(plan.TacticalMode == TacticalMode.RecoverToPattern,
                    item.Name + " did not begin in active recovery");
                var current = GetPrivateField(planner,
                    "_scoreRecoveryCurrent");
                var fallback = GetPrivateField(planner,
                    "_lateMobilityFallback");
                True(ReadBool(current, "Valid"),
                    item.Name + " current recovery measure is invalid");
                var currentDistance = ReadFloat(current, "Distance");
                True(ReadBool(fallback, "RecoveryValid"),
                    item.Name + " has no valid edge-free recovery candidate; " +
                    "current=" + currentDistance + ", candidates=" +
                    planner.LastCandidateCount + ", horizontal=" +
                    plan.Horizontal + ", score=" + plan.RiskScore);
                var projectedDistance = ReadFloat(fallback,
                    "RecoveryDistance");
                var closesOrAdvances = ReadBool(fallback,
                    "RecoveryClosed") || ReadBool(fallback,
                    "RecoveryProgress") &&
                    projectedDistance < currentDistance;
                True(closesOrAdvances,
                    item.Name + " selected a safe local candidate which neither " +
                    "shortens nor closes the stable-manifold distance (" +
                    currentDistance + " -> " + projectedDistance + ")");
                True(planner.LastCandidateCount > 1 || closesOrAdvances,
                    item.Name + " used the safe early-return gate without a " +
                    "convergence proof");
                var group = item.BossType == 636 ?
                    (item.Name.StartsWith("day-", StringComparison.Ordinal) ?
                        "636-day" : "636-night") :
                    item.BossType.ToString();
                HashSet<string> phases;
                if (!observedPhases.TryGetValue(group, out phases))
                {
                    phases = new HashSet<string>(StringComparer.Ordinal);
                    observedPhases.Add(group, phases);
                }
                phases.Add(plan.PhaseId);
            }
            AssertPhaseCount(observedPhases, "668", 9);
            AssertPhaseCount(observedPhases, "35", 12);
            AssertPhaseCount(observedPhases, "222", 8);
            AssertPhaseCount(observedPhases, "113", 5);
            AssertPhaseCount(observedPhases, "370", 17);
            AssertPhaseCount(observedPhases, "636-day", 10);
            AssertPhaseCount(observedPhases, "636-night", 10);
            // Intro and the first source-ready observation intentionally
            // share Moon Lord's synchronize-open-eyes loop.  The other five
            // entries must remain distinct source-clock recovery routes.
            AssertPhaseCount(observedPhases, "398", 6);
        }

        private static void MoonLordCombatCoreStateClocksAdmitActiveRecovery()
        {
            // ValidMoonCore is deliberately more exact than a generic
            // "Moon Lord exists" check. Cover every state/clock range encoded
            // for the summon, assembly, and vulnerable combat states so an F8
            // takeover of a source-less but legal core snapshot does not
            // falsely fail closed. Native defeat/departure states are checked
            // separately because they are not combat loops.
            var cases = new[]
            {
                new MoonCoreRecoveryFixture("core-state-minus-two-first",
                    -2,
                    0, true, "synchronize-open-eyes"),
                new MoonCoreRecoveryFixture("core-state-minus-two-last",
                    -2,
                    60, true, "synchronize-open-eyes"),
                new MoonCoreRecoveryFixture("core-state-minus-one-first",
                    -1, 0,
                    true, "synchronize-open-eyes"),
                new MoonCoreRecoveryFixture("core-state-minus-one-last",
                    -1, 60,
                    true, "synchronize-open-eyes"),
                new MoonCoreRecoveryFixture("core-state-zero", 0, 0,
                    true, "synchronize-open-eyes"),
                // BalancedMoonTarget explicitly identifies this exact,
                // vulnerable ai[0]==1 core as the damageable finish target.
                new MoonCoreRecoveryFixture("core-vulnerable-finish", 1, 0,
                    false, "core-finish")
            };

            foreach (var item in cases)
            {
                var scene = PriorityRecoveryScene(398);
                ConfigureMoonLordCore(scene, item.State, item.Clock,
                    item.Invulnerable);
                RefreshPriorityNativeContext(scene);

                // Validate the strategy-level source-clock contract first,
                // then prove that the same live snapshot is admitted by the
                // active-recovery controller rather than only by a unit-level
                // strategy call.
                var directive = new BossStrategyEngine().Evaluate(scene).
                    Directive;
                False(directive.RequestControlReturn,
                    item.Name + ": " + directive.ControlReturnReason);
                True(directive.UseExplicitMovement &&
                    directive.ForceContinuousMovement,
                    item.Name + " lost Moon Lord's immediate controller");
                True(directive.PhaseId.IndexOf(item.ExpectedPhase,
                    StringComparison.Ordinal) >= 0, item.Name + ": " +
                    directive.PhaseId);

                var plan = PlanFirstPriorityRecovery(scene, item.Name);
                True(plan.PhaseId.IndexOf(item.ExpectedPhase,
                    StringComparison.Ordinal) >= 0, item.Name + ": " +
                    plan.PhaseId);
            }
        }

        private static void MoonLordTerminalCoreStatesRejectActiveRecovery()
        {
            var cases = new[]
            {
                new MoonCoreRecoveryFixture("core-defeat-first", 2, 0,
                    true, "native-defeat-sequence"),
                new MoonCoreRecoveryFixture("core-defeat-middle", 2, 300,
                    true, "native-defeat-sequence"),
                new MoonCoreRecoveryFixture("core-defeat-last", 2, 600,
                    true, "native-defeat-sequence"),
                new MoonCoreRecoveryFixture("core-departure-first", 3, 0,
                    true, "native-departure"),
                new MoonCoreRecoveryFixture("core-departure-last", 3, 60,
                    true, "native-departure")
            };

            foreach (var item in cases)
            {
                var scene = PriorityRecoveryScene(398);
                ConfigureMoonLordCore(scene, item.State, item.Clock,
                    item.Invulnerable);
                RefreshPriorityNativeContext(scene);

                var directive = new BossStrategyEngine().Evaluate(scene).
                    Directive;
                True(directive.RequestControlReturn, item.Name);
                False(directive.Fire || directive.ForceContinuousMovement,
                    item.Name + " leaked terminal combat input");
                True(directive.PhaseId.IndexOf(item.ExpectedPhase,
                    StringComparison.Ordinal) >= 0, item.Name + ": " +
                    directive.PhaseId);

                var planner = new CombatPlanner(new PlannerSettings());
                string reason;
                Equal(ActiveEncounterPreparationResult.Rejected,
                    planner.PrepareForActiveEncounterDetailed(scene,
                        out reason));
                True(!string.IsNullOrEmpty(reason), item.Name +
                    " rejected without a terminal-state reason");
                True(GetPrivateField(planner, "_latchedMobilityRouteId") ==
                    null, item.Name + " latched mobility after rejection");
                False((bool)GetPrivateField(planner,
                    "_activePatternRecovery"), item.Name +
                    " opened recovery after terminal-state rejection");
            }
        }

        private static void MoonLordSourceAndProjectileRecoveryMatrixIsBounded()
        {
            var cases = new List<MoonRecoveryMatrixFixture>();
            AddMoonRecoveryBoundaries(cases, "head", 396, 0,
                new[] { 3, 0, 2, 3, 1 },
                new[] { 180, 30, 435, 180, 375 });
            AddMoonRecoveryBoundaries(cases, "left-hand", 397, 0,
                new[] { 0, 1, 2, 0, 3 },
                new[] { 50, 70, 330, 60, 90 });
            AddMoonRecoveryBoundaries(cases, "right-hand", 397, 1,
                new[] { 1, 0, 3, 0, 2 },
                new[] { 70, 50, 90, 60, 330 });
            AddMoonRecoveryBoundaries(cases, "true-eye", 400, 0,
                new[] { 0, 1, 0, 2, 0, 3, 0, 4, 0, 2 },
                new[] { 53, 90, 53, 135, 53, 200, 53, 375, 53, 135 });

            cases.Add(MoonMatrix("head-closed-first", s =>
                AddMoonLordPart(s, 91, 396, -2, 0, 0), true));
            cases.Add(MoonMatrix("head-closed-last", s =>
                AddMoonLordPart(s, 91, 396, -2, 1199, 0), true));
            cases.Add(MoonMatrix("head-dying-last", s =>
                AddMoonLordPart(s, 91, 396, -3, 1199, 0), true));
            cases.Add(MoonMatrix("left-hand-closed-first", s =>
                AddMoonLordPart(s, 92, 397, -2, 0, 0), true));
            cases.Add(MoonMatrix("right-hand-closed-last", s =>
                AddMoonLordPart(s, 93, 397, -2, 31, 1), true));
            cases.Add(MoonMatrix("true-eye-new-offset", s =>
                AddMoonLordPart(s, 94, 400, -2, 196, 0), true));

            cases.Add(MoonMatrix("projectile-454-attached-first", s =>
                AddMoonLordSphere(s, 70, 0, 91)));
            cases.Add(MoonMatrix("projectile-454-attached-last", s =>
                AddMoonLordSphere(s, 71, 29, 91)));
            cases.Add(MoonMatrix("projectile-454-armed-last", s =>
                AddMoonLordSphere(s, 72, 59, -1)));
            cases.Add(MoonMatrix("projectile-454-damaging", s =>
                AddMoonLordSphere(s, 73, 60, -1)));
            cases.Add(MoonMatrix("projectile-454-detached", s =>
                AddMoonLordSphere(s, 74, -1, -1)));
            cases.Add(MoonMatrix("projectile-456-outbound", s =>
                AddMoonLordTongue(s, 80, 91, false)));
            cases.Add(MoonMatrix("projectile-456-returning", s =>
                AddMoonLordTongue(s, 81, 91, true)));
            cases.Add(MoonMatrix("projectile-455-warmup", s =>
                AddMoonLordRay(s, 5f, .02f)));
            cases.Add(MoonMatrix("projectile-455-active", s =>
                AddMoonLordRay(s, 20f, -.02f)));
            cases.Add(MoonMatrix("concurrent-native-sources", s =>
            {
                AddMoonLordPart(s, 91, 396, 1, 985, 0);
                AddMoonLordPart(s, 92, 397, 2, 390, 0);
                AddMoonLordPart(s, 93, 397, 2, 540, 1);
                AddMoonLordPart(s, 94, 400, 3, 500, 0);
                AddMoonLordSphere(s, 75, 60, -1, false);
                AddMoonLordTongue(s, 82, 91, false, false);
                AddMoonLordRay(s, 20f, .02f);
                AddMoonLordRay(s, 20f, -.02f);
            }));

            var converged = 0;
            var returned = 0;
            foreach (var item in cases)
            {
                var scene = PriorityRecoveryScene(398);
                ConfigureMoonLordCore(scene, 0, 0, true);
                RefreshPriorityNativeContext(scene);
                item.Configure(scene);
                RefreshRecoveryClearance(scene);
                if (AssertMoonRecoveryReachesBoundedOutcome(scene, item.Name,
                        item.SimulateMovement))
                    converged++;
                else
                    returned++;
            }
            True(converged > 0,
                "Moon Lord matrix never observed bounded convergence");
            True(returned > 0,
                "Moon Lord matrix never exercised finite neutral return");
        }

        private static void MoonLordTerminalTransitionReturnsNeutralControl()
        {
            foreach (var item in new[]
            {
                new MoonCoreRecoveryFixture("defeated-600-tick-drama", 2,
                    0, true, "native-defeat-sequence"),
                new MoonCoreRecoveryFixture("no-live-player-departure", 3,
                    0, true, "native-departure")
            })
            {
                var scene = PriorityRecoveryScene(398);
                ConfigureMoonLordCore(scene, 0, 0, true);
                RefreshPriorityNativeContext(scene);
                var planner = new CombatPlanner(new PlannerSettings
                {
                    EmergencyRiskThreshold = float.MaxValue,
                    PatternSafeRiskThreshold = float.MaxValue
                });
                string reason;
                True(planner.PrepareForActiveEncounter(scene, out reason),
                    item.Name + " initial admission: " + reason);

                ConfigureMoonLordCore(scene, item.State, item.Clock,
                    item.Invulnerable);
                RefreshPriorityNativeContext(scene);
                var returned = planner.Plan(scene);
                AssertNeutralPriorityStateReturn(returned, item.Name);
                True(returned.PhaseId.IndexOf(item.ExpectedPhase,
                    StringComparison.Ordinal) >= 0, item.Name + ": " +
                    returned.PhaseId);
                True(returned.ControlReturnReason.IndexOf(item.State == 2 ?
                    "defeat" : "departure", StringComparison.OrdinalIgnoreCase) >= 0,
                    item.Name + ": " + returned.ControlReturnReason);
            }
        }

        private static void EmpressP1DashDirectionsAndWindowsAdmitActiveRecovery()
        {
            // AI_120's P1 table contains dash at post-selection indices
            // 2/4/7/9. State 9 is not an invented attack: native rewrites a
            // state-8 dash to 9 when the player is on the opposite side.
            // Each direction is sampled in its source-defined telegraph,
            // committed, and brake windows. Reverse horizontal dashes can
            // legitimately choose the same perpendicular escape, so the
            // assertion is admission and the native phase contract, not an
            // artificial requirement that their input vector must differ.
            var cases = new[]
            {
                new EmpressP1DashRecoveryFixture("left-telegraph", 8, 20,
                    2, 0f, "telegraph"),
                new EmpressP1DashRecoveryFixture("right-telegraph", 9, 20,
                    4, 0f, "telegraph"),
                new EmpressP1DashRecoveryFixture("left-committed", 8, 50,
                    7, -12f, "committed"),
                new EmpressP1DashRecoveryFixture("right-committed", 9, 50,
                    9, 12f, "committed"),
                new EmpressP1DashRecoveryFixture("left-braking", 8, 100,
                    4, -3f, "braking"),
                new EmpressP1DashRecoveryFixture("right-braking", 9, 100,
                    2, 3f, "braking")
            };
            var observedStates = new HashSet<int>();
            var observedWindows = new HashSet<string>(StringComparer.Ordinal);
            foreach (var day in new[] { false, true })
            foreach (var item in cases)
            {
                var scene = PriorityRecoveryScene(636);
                ConfigureEmpressP1Dash(scene, day, item.State, item.Tick,
                    item.AttackIndex, item.VelocityX);
                var expected = (day ? "day-lethal-p1-horizontal-dash-" :
                    "night-p1-horizontal-dash-") + item.Window;
                var directive = new BossStrategyEngine().Evaluate(scene).
                    Directive;
                False(directive.RequestControlReturn,
                    (day ? "day-" : "night-") + item.Name + ": " +
                    directive.ControlReturnReason);
                Equal(BossPattern.PerpendicularDashDodge,
                    directive.Pattern);
                True(directive.UseExplicitMovement &&
                    directive.ForceContinuousMovement,
                    item.Name + " lost its explicit dash contract");
                True(directive.PhaseId.IndexOf(expected,
                    StringComparison.Ordinal) >= 0, item.Name + ": " +
                    directive.PhaseId);

                var plan = PlanFirstPriorityRecovery(scene,
                    (day ? "day-" : "night-") + item.Name);
                True(plan.PhaseId.IndexOf(expected,
                    StringComparison.Ordinal) >= 0, item.Name + ": " +
                    plan.PhaseId);
                observedStates.Add(item.State);
                observedWindows.Add(item.Window);
            }
            Equal(2, observedStates.Count);
            Equal(3, observedWindows.Count);
        }

        private static void EmpressNightRepositionJoinsItsExplicitMovementLoop()
        {
            var scene = PriorityRecoveryScene(636);
            ConfigureEmpressPhase(scene, false, "p1-reposition");
            scene.Player.Velocity = default(Vec2);
            RefreshRecoveryClearance(scene);
            var directive = new BossStrategyEngine().Evaluate(scene).Directive;
            True(directive.UseExplicitMovement,
                "night Empress recovery fell back to a moving target-relative ring");

            var planner = new CombatPlanner(new PlannerSettings
            {
                StuckTicksBeforeRecovery = 10000,
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);
            for (var frame = 1; frame <= 3; frame++)
            {
                var plan = planner.Plan(scene);
                False(plan.RequestControlReturn, plan.ControlReturnReason);
                Equal(frame < 3 ? TacticalMode.RecoverToPattern :
                    TacticalMode.EstablishPattern, plan.TacticalMode);
            }
        }

        private static ControlPlan PlanFirstPriorityRecovery(
            CombatSnapshot scene, string name)
        {
            RefreshRecoveryClearance(scene);
            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason),
                name + " admission: " + reason);
            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn,
                name + " returned control on its first recovery frame: " +
                plan.ControlReturnReason);
            Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            return plan;
        }

        private static void ActiveRecoveryRequiresThreeConsecutiveClosedFrames()
        {
            var scene = PriorityRecoveryScene(113);
            SetTarget(scene, lifeFraction: .8f, velocityX: 0f);
            // Wall runway ideal distance is 560 px in this phase. Preserve a
            // real, already established away-running state for three identical
            // native observations.
            scene.Player.Position = new Vec2(1485f, 800f);
            scene.Player.Velocity = new Vec2(-2f, 0f);
            RefreshRecoveryClearance(scene);
            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);

            var first = planner.Plan(scene);
            var second = planner.Plan(scene);
            var third = planner.Plan(scene);
            True(first.TacticalMode == TacticalMode.RecoverToPattern,
                "one closed observation ended active recovery");
            True(second.TacticalMode == TacticalMode.RecoverToPattern,
                "two closed observations ended active recovery");
            True(third.TacticalMode == TacticalMode.EstablishPattern,
                "three consecutive closed observations did not establish the loop");
            False(first.RequestControlReturn || second.RequestControlReturn ||
                third.RequestControlReturn);
        }

        private static void ActiveRecoveryDoesNotResetForAdvancingNativeClockLabels()
        {
            var scene = PriorityRecoveryScene(113);
            SetTarget(scene, lifeFraction: .8f, velocityX: 0f, ai1: 59f,
                ai2: 2f);
            // The Wall's leech phase label includes ai[2] for diagnostics, but
            // the admitted runway manifold is unchanged while that native clock
            // advances. A real game never presents three frozen clock frames.
            scene.Player.Position = new Vec2(1485f, 800f);
            scene.Player.Velocity = new Vec2(-2f, 0f);
            RefreshRecoveryClearance(scene);
            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);

            var first = planner.Plan(scene);
            SetTarget(scene, lifeFraction: .8f, velocityX: 0f, ai1: 60f,
                ai2: 2f);
            var second = planner.Plan(scene);
            SetTarget(scene, lifeFraction: .8f, velocityX: 0f, ai1: 0f,
                ai2: 3f);
            var third = planner.Plan(scene);

            True(first.PhaseId != second.PhaseId &&
                 second.PhaseId != third.PhaseId,
                "fixture did not advance the native diagnostic clock label");
            Equal(TacticalMode.RecoverToPattern, first.TacticalMode);
            Equal(TacticalMode.RecoverToPattern, second.TacticalMode);
            Equal(TacticalMode.EstablishPattern, third.TacticalMode);
            False(first.RequestControlReturn || second.RequestControlReturn ||
                third.RequestControlReturn);
        }

        private static void BlockedActiveRecoveryReturnsControlInFiniteTime()
        {
            var scene = PriorityRecoveryScene(113);
            SetTarget(scene, lifeFraction: .8f, velocityX: 0f);
            var planner = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 12,
                SimulationStepTicks = 1,
                RecoveryTicks = 2,
                StuckTicksBeforeRecovery = 2,
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);

            var returned = default(ControlPlan);
            var returnTick = -1;
            for (var tick = 1; tick <= 100; tick++)
            {
                returned = planner.Plan(scene); // Native position never responds.
                if (returned.RequestControlReturn)
                {
                    returnTick = tick;
                    break;
                }
                True(returned.TacticalMode == TacticalMode.RecoverToPattern ||
                     returned.TacticalMode == TacticalMode.EmergencyEvade,
                    "blocked recovery impersonated an established/stable loop " +
                    "on tick " + tick);
            }
            True(returnTick > 0 && returnTick <= 100,
                "blocked active recovery retained control indefinitely");
            AssertNeutralRecoveryReturn(returned);
        }

        private static void EmpressStateOneToTwoRebasesRecoveryDuringEmergency()
        {
            var scene = PriorityRecoveryScene(636);
            ConfigureEmpressPhase(scene, false, "p1-reposition");
            scene.Player.Velocity = new Vec2(-8f, 0f);
            RefreshRecoveryClearance(scene);
            var planner = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 12,
                SimulationStepTicks = 1,
                RecoveryTicks = 1,
                StuckTicksBeforeRecovery = 10000,
                EmergencyRiskThreshold = 0f,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);

            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn, plan.ControlReturnReason);
            Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
            var oldRouteDeadline = (int)GetPrivateField(planner,
                "_activeRecoveryDeadlineTicks");
            var absoluteDeadline = (int)GetPrivateField(planner,
                "_activeRecoveryAbsoluteDeadlineTicks");
            True(oldRouteDeadline > 1 && oldRouteDeadline <
                absoluteDeadline,
                "fixture did not create a finite pre-cap Empress route deadline");

            for (var tick = 2; tick < oldRouteDeadline; tick++)
            {
                plan = planner.Plan(scene);
                False(plan.RequestControlReturn,
                    "continuous emergency consumed the current route early: " +
                    plan.ControlReturnReason);
                Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
            }
            Equal(oldRouteDeadline - 1, (int)GetPrivateField(planner,
                "_activeRecoveryElapsedTicks"));

            // AI_120 moves from native reposition state 1 into its selected
            // prismatic-bolt state 2. The first state-2 frame is a new stable
            // route and must receive a fresh interval even though the old one
            // would expire on this exact total-recovery frame.
            ConfigureEmpressPhase(scene, false, "p1-bolts");
            plan = planner.Plan(scene);
            False(plan.RequestControlReturn,
                "state 2 inherited state 1's exhausted route deadline: " +
                plan.ControlReturnReason);
            Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
            Equal(1, (int)GetPrivateField(planner,
                "_activeRecoveryElapsedTicks"));
            Equal(oldRouteDeadline, (int)GetPrivateField(planner,
                "_activeRecoveryTotalElapsedTicks"));
        }

        private static void PhaseChurnCannotExtendActiveRecoveryForever()
        {
            var scene = PriorityRecoveryScene(113);
            var planner = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 12,
                SimulationStepTicks = 1,
                RecoveryTicks = 1,
                StuckTicksBeforeRecovery = 10000,
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);

            var returned = default(ControlPlan);
            var returnTick = -1;
            for (var tick = 1; tick <= 610; tick++)
            {
                // Alternates the native Wall phase every frame. Phase-local
                // counters may reset, but the absolute recovery deadline may not.
                SetTarget(scene, tick % 2 == 0 ? .8f : .4f, 0f);
                returned = planner.Plan(scene);
                if (returned.RequestControlReturn)
                {
                    returnTick = tick;
                    break;
                }
                True(returned.TacticalMode != TacticalMode.StablePattern &&
                     returned.TacticalMode != TacticalMode.EstablishPattern,
                    "phase churn escaped recovery without three closed frames");
            }
            True(returnTick > 0 && returnTick <= 600,
                "phase changes extended active recovery beyond its hard deadline");
            AssertNeutralRecoveryReturn(returned);
        }

        private static void EmergencyRouteChurnReturnsNeutralAtAbsoluteDeadline()
        {
            var scene = PriorityRecoveryScene(636);
            ConfigureEmpressPhase(scene, false, "p1-reposition");
            scene.Player.Velocity = new Vec2(-8f, 0f);
            RefreshRecoveryClearance(scene);
            var planner = new CombatPlanner(new PlannerSettings
            {
                HorizonTicks = 12,
                SimulationStepTicks = 1,
                RecoveryTicks = 1,
                StuckTicksBeforeRecovery = 10000,
                EmergencyRiskThreshold = 0f,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason), reason);
            var absoluteDeadline = (int)GetPrivateField(planner,
                "_activeRecoveryAbsoluteDeadlineTicks");

            var returned = default(ControlPlan);
            var returnTick = -1;
            for (var tick = 1; tick <= absoluteDeadline + 1;
                tick++)
            {
                ConfigureEmpressPhase(scene, false,
                    tick % 2 == 0 ? "p1-reposition" : "p1-bolts");
                returned = planner.Plan(scene);
                if (returned.RequestControlReturn)
                {
                    returnTick = tick;
                    break;
                }
                Equal(TacticalMode.EmergencyEvade, returned.TacticalMode);
            }
            Equal(absoluteDeadline, returnTick);
            True(returned.ControlReturnReason.IndexOf("absolute",
                StringComparison.OrdinalIgnoreCase) >= 0,
                returned.ControlReturnReason);
            AssertNeutralRecoveryReturn(returned);
        }

        private static void DeerclopsSlowRemainsInsideItsReviewedRecoveryLoop()
        {
            var scene = PriorityRecoveryScene(668);
            SetTarget(scene, .8f, 0f, ai0: 3f, ai1: 35f);
            scene.Player.BaseRunSpeed = 1.5f;
            scene.Player.MaxRunSpeed = 3f;
            scene.Player.RunAcceleration = .04f;
            scene.Player.SprintAcceleration = .008f;
            scene.Player.RunSlowdown = .2f;
            scene.Player.SlowDebuffKnown = true;
            scene.Player.SlowDebuffActive = true;
            scene.Player.MoveSpeedDebuffFactorKnown = true;
            scene.Player.MoveSpeedDebuffFactor = .5f;

            var planner = new CombatPlanner(new PlannerSettings
            {
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            True(planner.PrepareForActiveEncounter(scene, out reason),
                "mid-roar Slow admission: " + reason);
            var roar = planner.Plan(scene);
            False(roar.RequestControlReturn,
                "reviewed Deerclops Slow returned control: " +
                roar.ControlReturnReason);
            True(roar.PhaseId.IndexOf("slow-roar", StringComparison.Ordinal) >= 0,
                roar.PhaseId);

            // Buff 32 lasts 720 ticks, well beyond native state 3. The route
            // remains the same while the real per-frame movement snapshot is
            // still impaired and used by candidate prediction.
            SetTarget(scene, .8f, 0f, ai0: 0f, ai1: 4f);
            var aftermath = planner.Plan(scene);
            False(aftermath.RequestControlReturn,
                "post-roar Slow returned control: " +
                aftermath.ControlReturnReason);

            // This exception is exact, not a general latch of old speed. Lost
            // Slow identity with the same weak live motion must fail closed.
            scene.Player.SlowDebuffActive = false;
            var unexplainedLoss = planner.Plan(scene);
            True(unexplainedLoss.RequestControlReturn);
            Equal("unsupported-mobility-route",
                unexplainedLoss.StrategyId);

            planner.Reset();
            scene.Player.SlowDebuffActive = true;
            scene.Player.MoveSpeedDebuffFactor = 1f / 3f;
            False(planner.PrepareForActiveEncounter(scene, out reason));
            scene.Player.MoveSpeedDebuffFactor = .5f;
            scene.Player.MoveSpeedDebuffFactorKnown = false;
            False(planner.PrepareForActiveEncounter(scene, out reason));

            // Another Boss never inherits Deerclops' modeled impairment.
            var eye = CombatScenario(4);
            eye.Player.BaseRunSpeed = 1.5f;
            eye.Player.MaxRunSpeed = 3f;
            eye.Player.RunAcceleration = .04f;
            eye.Player.SprintAcceleration = .008f;
            eye.Player.RunSlowdown = .2f;
            eye.Player.SlowDebuffKnown = true;
            eye.Player.SlowDebuffActive = true;
            eye.Player.MoveSpeedDebuffFactorKnown = true;
            eye.Player.MoveSpeedDebuffFactor = .5f;
            False(new CombatPlanner(new PlannerSettings()).
                PrepareForActiveEncounter(eye, out reason));
        }

        private static void PriorityBossActiveAdmissionRejectsInvalidNativeStateBeforeLatching()
        {
            // Active F8 is not a generic "a Boss exists" admission. The
            // current native state is the authority for the recovery loop. A
            // stale or malformed source must be rejected before it can latch
            // a mobility/output route and make the runtime announce takeover.
            foreach (var item in PriorityActiveNativeFailureCases())
            {
                var scene = item.Create();
                item.Corrupt(scene);
                RefreshRecoveryClearance(scene);
                var planner = new CombatPlanner(new PlannerSettings
                {
                    EmergencyRiskThreshold = float.MaxValue,
                    PatternSafeRiskThreshold = float.MaxValue
                });
                string reason;
                Equal(ActiveEncounterPreparationResult.Rejected,
                    planner.PrepareForActiveEncounterDetailed(scene,
                        out reason));
                True(!string.IsNullOrEmpty(reason), item.Name +
                    " rejected without an actionable native-state reason");
                True(GetPrivateField(planner, "_latchedMobilityRouteId") ==
                    null, item.Name +
                    " latched a mobility route after invalid native state");
                False((bool)GetPrivateField(planner,
                    "_activePatternRecovery"), item.Name +
                    " opened active recovery after invalid native state");
            }
        }

        private static void PriorityBossNativeStateDriftReturnsNeutralControlAfterAdmission()
        {
            // Even after a good active-F8 snapshot, the next production frame
            // can reveal a source/state mismatch (desync, transition, or an
            // adapter regression). The already-admitted session must return a
            // fully neutral plan rather than leak the previous route's fire,
            // dash, jump, or movement input.
            foreach (var item in PriorityActiveNativeFailureCases())
            {
                var scene = item.Create();
                RefreshRecoveryClearance(scene);
                var planner = new CombatPlanner(new PlannerSettings
                {
                    EmergencyRiskThreshold = float.MaxValue,
                    PatternSafeRiskThreshold = float.MaxValue
                });
                string reason;
                True(ActiveEncounterPreparationResult.Ready ==
                    planner.PrepareForActiveEncounterDetailed(scene,
                        out reason), item.Name + " valid admission: " +
                        reason);
                item.Corrupt(scene);
                var plan = planner.Plan(scene);
                AssertNeutralPriorityStateReturn(plan, item.Name);
            }
        }

        private static IEnumerable<PriorityActiveNativeFailureCase>
            PriorityActiveNativeFailureCases()
        {
            yield return ActiveFailure("deerclops-state-one-timer-overflow",
                () =>
                {
                    var scene = PriorityRecoveryScene(668);
                    ConfigureDeerclops(scene, 1, 20);
                    return scene;
                }, scene => ConfigureDeerclops(scene, 1, 81));

            yield return ActiveFailure("skeletron-foreign-hand-parent",
                () =>
                {
                    var scene = PriorityRecoveryScene(35);
                    ConfigureSkeletron(scene, 0, 200);
                    AddSkeletronHand(scene, 0, 270);
                    return scene;
                }, scene =>
                {
                    var hand = scene.Targets[1];
                    hand.Ai1 = scene.Targets[0].Key + 1;
                    scene.Targets[1] = hand;
                });

            yield return ActiveFailure("queen-bee-inconsistent-enrage-source",
                () =>
                {
                    var scene = PriorityRecoveryScene(222);
                    SetTarget(scene, .8f, 0f, ai0: 3f, ai1: 10f);
                    return scene;
                }, scene =>
                {
                    var native = scene.PriorityBoss.QueenBees[0];
                    native.NativeEnrageFactor += .25f;
                    scene.PriorityBoss.QueenBees[0] = native;
                });

            yield return ActiveFailure("wall-of-flesh-short-native-tunnel",
                () =>
                {
                    var scene = PriorityRecoveryScene(113);
                    ConfigureWall(scene, .8f);
                    return scene;
                }, scene =>
                {
                    var tunnel = scene.PriorityBoss.WallOfFleshTunnels[0];
                    tunnel.DrawAreaBottomPixels =
                        tunnel.DrawAreaTopPixels + 159;
                    scene.PriorityBoss.WallOfFleshTunnels[0] = tunnel;
                });

            yield return ActiveFailure("fishron-inconsistent-enrage-source",
                () =>
                {
                    var scene = PriorityRecoveryScene(370);
                    ConfigureFishron(scene, 0, 0, 0, .8f, false);
                    return scene;
                }, scene =>
                {
                    var native = scene.PriorityBoss.DukeFishrons[0];
                    native.NativeEnraged = !native.NativeEnraged;
                    scene.PriorityBoss.DukeFishrons[0] = native;
                });

            yield return ActiveFailure("empress-inconsistent-attack-clock",
                () =>
                {
                    var scene = PriorityRecoveryScene(636);
                    ConfigureEmpressPhase(scene, false, "p1-bolts");
                    return scene;
                }, scene =>
                {
                    var native = scene.PriorityBoss.Empresses[0];
                    native.Ai1AttackTimer += 1f;
                    scene.PriorityBoss.Empresses[0] = native;
                });

            // Daytime uses AI_120's lethal/expert schedule even when the
            // world's ordinary difficulty is Classic. Keep it separate from
            // the night row so an adapter cannot accidentally validate only
            // the nonlethal schedule before active takeover.
            yield return ActiveFailure("day-empress-inconsistent-attack-clock",
                () =>
                {
                    var scene = PriorityRecoveryScene(636);
                    ConfigureEmpressPhase(scene, true, "p1-bolts");
                    return scene;
                }, scene =>
                {
                    var native = scene.PriorityBoss.Empresses[0];
                    native.Ai1AttackTimer += 1f;
                    scene.PriorityBoss.Empresses[0] = native;
                });

            yield return ActiveFailure("moon-lord-inconsistent-head-clock",
                () =>
                {
                    var scene = PriorityRecoveryScene(398);
                    ConfigureMoonLordPhase(scene, "head-bolts");
                    return scene;
                }, scene =>
                {
                    var head = scene.Targets[1];
                    // Clock 120 belongs to head state 3 in the native table;
                    // state 1 at that clock has no legal source route.
                    head.Ai0 = 1f;
                    scene.Targets[1] = head;
                });
        }

        private static PriorityActiveNativeFailureCase ActiveFailure(
            string name, Func<CombatSnapshot> create,
            Action<CombatSnapshot> corrupt) =>
            new PriorityActiveNativeFailureCase
            {
                Name = name,
                Create = create,
                Corrupt = corrupt
            };

        private static IEnumerable<PriorityRecoveryCase> PriorityRecoveryCases()
        {
            // Deerclops: these are the exact non-departure AI_123 states
            // used by the isolated native fixture.  Do not call a spawned
            // projectile a phase: the recovery admission must still work in
            // its deterministic telegraph before that projectile exists.
            yield return Case("deer-spawn-settle", 668,
                s => ConfigureDeerclops(s, -1, 0));
            yield return Case("deer-opening", 668,
                s => ConfigureDeerclops(s, 0, 0));
            yield return Case("deer-forward-spikes", 668,
                s => ConfigureDeerclops(s, 1, 19));
            yield return Case("deer-rubble", 668,
                s => ConfigureDeerclops(s, 2, 21));
            yield return Case("deer-slow-roar", 668,
                s => ConfigureDeerclops(s, 3, 27));
            yield return Case("deer-double-spikes", 668,
                s => ConfigureDeerclops(s, 4, 35));
            yield return Case("deer-shadow-hands", 668,
                s => ConfigureDeerclops(s, 5, 20));
            yield return Case("deer-return-home", 668,
                s => ConfigureDeerclops(s, 6, 0));
            yield return Case("deer-teleport-home", 668,
                s => ConfigureDeerclops(s, 7, 30));

            // Skeletron: cover the exact AI_035 head clocks plus every
            // non-departure hand state.  The hand's committed vector is
            // intentionally represented in the fixture so ChargeEscape is
            // exercised as an active-entry path, not merely as a label.
            yield return Case("skeletron-hover", 35,
                s => ConfigureSkeletron(s, 0, 600));
            yield return Case("skeletron-pre-spin", 35,
                s => ConfigureSkeletron(s, 0, 730));
            yield return Case("skeletron-spin-imminent", 35,
                s => ConfigureSkeletron(s, 0, 790));
            yield return Case("skeletron-spin-entry", 35,
                s => ConfigureSkeletron(s, 1, 0));
            yield return Case("skeletron-spin-pursuit", 35,
                s => ConfigureSkeletron(s, 1, 200));
            yield return Case("skeletron-spin-exit", 35,
                s => ConfigureSkeletron(s, 1, 370));
            yield return Case("skeletron-hand-vertical-imminent", 35,
                s => ConfigureSkeletron(s, 0, 200, 0, 270));
            yield return Case("skeletron-hand-vertical-locking", 35,
                s => ConfigureSkeletron(s, 0, 200, 1, 0));
            yield return Case("skeletron-hand-vertical-committed", 35,
                s => ConfigureSkeletron(s, 0, 200, 2, 0));
            yield return Case("skeletron-hand-horizontal-imminent", 35,
                s => ConfigureSkeletron(s, 0, 200, 3, 270));
            yield return Case("skeletron-hand-horizontal-locking", 35,
                s => ConfigureSkeletron(s, 0, 200, 4, 0));
            yield return Case("skeletron-hand-horizontal-committed", 35,
                s => ConfigureSkeletron(s, 0, 200, 5, 0));

            // Queen Bee AI_043: choice, charge telegraph/commit/brake, bee
            // wave, stinger clock, and long-range reacquire are distinct native
            // states even when their instantaneous speeds happen to match.
            yield return Case("bee-choose", 222,
                s => SetTarget(s, .8f, 0f, ai0: -1f));
            yield return Case("bee-charge-align", 222,
                s => SetTarget(s, .8f, -9f, ai0: 0f, ai1: 0f));
            yield return Case("bee-charge-commit", 222,
                s => SetTarget(s, .8f, -9f, ai0: 0f, ai1: 1f));
            yield return Case("bee-charge-brake", 222,
                s => SetTarget(s, .8f, -5f, ai0: 0f, ai1: 1f,
                    ai2: 1f));
            yield return Case("bee-wave", 222,
                // AI_043 uses ai[1] as the current spawn clock and ai[2] as
                // the completed-wave count.  Keep both inside a reachable
                // state-1 tuple while exercising the imminent edge.
                s => SetTarget(s, .8f, 0f, ai0: 1f, ai1: 36f,
                    ai2: 2f));
            yield return Case("bee-move-above", 222,
                s => SetTarget(s, .8f, 0f, ai0: 2f));
            yield return Case("bee-stingers", 222,
                s => SetTarget(s, .8f, 0f, ai0: 3f, ai1: 10f));
            yield return Case("bee-reacquire", 222,
                s => SetTarget(s, .8f, 0f, ai0: 4f));

            // Wall of Flesh: each fixture health band and the independently
            // clocked eye-laser branch.  The eye is not represented by a
            // generic hostile projectile; its localAI clock is part of the
            // admission context.
            yield return Case("wall-runway", 113,
                s => ConfigureWall(s, .8f));
            yield return Case("wall-accelerating", 113,
                s => ConfigureWall(s, .4f));
            yield return Case("wall-low", 113,
                s => ConfigureWall(s, .2f));
            yield return Case("wall-critical", 113,
                s => ConfigureWall(s, .08f));
            yield return Case("wall-eye-laser", 113, s =>
            {
                ConfigureWall(s, .8f);
                AddWallLaserEye(s);
            });

            // Fishron: use every exact, reachable AI_069 tuple staged by the
            // isolated engine harness.  Expert phase three also requires the
            // independently certified burst/brake route at admission time.
            yield return Case("fishron-spawn-fade", 370,
                s => ConfigureFishron(s, -1, 20, 0, .8f, false));
            yield return Case("fishron-spawn-emerge", 370,
                s => ConfigureFishron(s, -1, 60, 0, .8f, false));
            yield return Case("fishron-p1-hover", 370,
                s => ConfigureFishron(s, 0, 0, 0, .8f, false));
            yield return Case("fishron-p1-dash", 370,
                s => ConfigureFishron(s, 1, 0, 0, .8f, false));
            yield return Case("fishron-p1-bubbles", 370,
                s => ConfigureFishron(s, 2, 0, 1, .8f, false));
            yield return Case("fishron-p1-sharknado", 370,
                s => ConfigureFishron(s, 3, 50, 0, .8f, false));
            yield return Case("fishron-p2-transition-fade", 370,
                s => ConfigureFishron(s, 4, 60, 0, .4f, false));
            yield return Case("fishron-p2-transition-emerge", 370,
                s => ConfigureFishron(s, 4, 140, 0, .4f, false));
            yield return Case("fishron-p2-hover", 370,
                s => ConfigureFishron(s, 5, 0, 0, .4f, false));
            yield return Case("fishron-p2-dash", 370,
                s => ConfigureFishron(s, 6, 0, 0, .4f, false));
            yield return Case("fishron-p2-bubbles", 370,
                s => ConfigureFishron(s, 7, 0, 1, .4f, false));
            yield return Case("fishron-p2-sharknado", 370,
                s => ConfigureFishron(s, 8, 50, 0, .4f, false));
            yield return Case("fishron-p3-transition-fade", 370,
                s => ConfigureFishron(s, 9, 60, 0, .1f, true));
            yield return Case("fishron-p3-transition-hidden", 370,
                s => ConfigureFishron(s, 9, 140, 0, .1f, true));
            yield return Case("fishron-p3-reposition", 370,
                s => ConfigureFishron(s, 10, 0, 1, .1f, true));
            yield return Case("fishron-p3-dash", 370,
                s => ConfigureFishron(s, 11, 0, 0, .1f, true));
            yield return Case("fishron-p3-teleport", 370,
                s => ConfigureFishron(s, 12, 10, 1, .1f, true));

            foreach (var day in new[] { false, true })
            {
                var prefix = day ? "day-empress-" : "night-empress-";
                // The day and night variants get their own rows because
                // day-time AI_120 is lethal and uses the expert schedule even
                // in a Classic world.  P2 predictive lances additionally
                // require the real expert table, not a generic P2 alias.
                foreach (var phase in new[]
                {
                    "p1-reposition", "p1-bolts", "p1-rainbow",
                    "p1-sun-dance", "p1-dash", "transition",
                    "p2-reposition", "p2-lance-wall",
                    "p2-predictive-lances", "p2-spiral"
                })
                {
                    var capturedPhase = phase;
                    yield return Case(prefix + capturedPhase, 636,
                        s => ConfigureEmpressPhase(s, day, capturedPhase));
                }
            }

            // Moon Lord: exact source clocks are required for each component;
            // a generic projectile only supplements those clocks after the
            // native source is present.  This mirrors every staged mid-fight
            // entry supported by the isolated engine harness.
            foreach (var phase in new[]
            {
                "intro", "synchronize-eyes", "head-bolts", "head-tongue",
                "head-deathray-telegraph", "left-sphere-release",
                "right-sphere-release"
            })
            {
                var capturedPhase = phase;
                yield return Case("moon-" + capturedPhase, 398,
                    s => ConfigureMoonLordPhase(s, capturedPhase));
            }
        }

        private static PriorityRecoveryCase Case(string name, int bossType,
            Action<CombatSnapshot> configure) => new PriorityRecoveryCase
            {
                Name = name,
                BossType = bossType,
                Configure = configure
            };

        private static CombatSnapshot PriorityRecoveryScene(int bossType)
        {
            var scene = CombatScenario(bossType);
            scene.Player.Position = new Vec2(600f, 800f);
            scene.Player.Velocity = new Vec2(-2f, 0f);
            scene.Player.OnGround = true;
            scene.Arena.LocalOpenBounds = new RectF(0f, 0f, 5000f, 2400f);
            scene.Arena.SafeCenter = new Vec2(2500f, 1200f);
            scene.Arena.HasFloor = true;
            scene.Arena.FloorSupport = new SupportSpan
            {
                Valid = true,
                Left = 0f,
                Right = 5000f,
                SurfaceY = scene.Player.Position.Y + scene.Player.Height
            };
            scene.Arena.RecoverySupport = scene.Arena.FloorSupport;
            SetTarget(scene, .8f, 0f);
            RefreshRecoveryClearance(scene);
            return scene;
        }

        private static void SetTarget(CombatSnapshot scene,
            float lifeFraction, float velocityX, float ai0 = 0f,
            float ai1 = 0f, float ai2 = 0f, float ai3 = 0f)
        {
            var target = scene.Targets[0];
            target.Position = new Vec2(2000f, 800f);
            target.Velocity = new Vec2(velocityX, 0f);
            target.LifeMax = 10000;
            target.Life = (int)(target.LifeMax * lifeFraction);
            target.Ai0 = ai0;
            target.Ai1 = ai1;
            target.Ai2 = ai2;
            target.Ai3 = ai3;
            target.Chaseable = true;
            target.Invulnerable = false;
            scene.Targets[0] = target;
            RefreshPriorityNativeContext(scene);
        }

        private static void ConfigureEmpressPhase(CombatSnapshot scene,
            bool day, string phase)
        {
            // These tuples are selected from AI_120's actual tables rather
            // than simply naming the desired state.  In particular, ai[2]
            // is the post-selection index and the daytime lethal schedule is
            // the expert P2 table even in a Classic world.
            var state = 1;
            var tick = 0;
            var index = 0;
            var second = phase == "transition" || phase.StartsWith("p2-",
                StringComparison.Ordinal);
            switch (phase)
            {
                case "p1-reposition": break;
                case "p1-bolts": state = 2; tick = 1; index = 1; break;
                case "p1-rainbow": state = 5; tick = 5; index = 5; break;
                case "p1-sun-dance": state = 6; tick = 3; index = 3; break;
                case "p1-dash": state = 8; tick = 50; index = 2; break;
                case "transition": state = 10; tick = 20; index = 1; break;
                case "p2-reposition": break;
                case "p2-lance-wall": state = 7; tick = 80; index = 1; break;
                case "p2-predictive-lances":
                    state = 11; tick = 40; index = 4;
                    // At night this attack only exists in Expert/Master.  By
                    // day AI_120 selects the same table from its lethal rage
                    // predicate, so retain Classic difficulty for that half.
                    scene.Difficulty.Expert = !day;
                    break;
                case "p2-spiral":
                    state = 12;
                    tick = 70;
                    // The classic P2 table has nine entries; the lethal
                    // daytime table has ten and ends in spiral bolts.
                    index = day ? 10 : 9;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase),
                        phase, "Unreviewed Empress active-recovery phase");
            }

            scene.Difficulty.DayTime = day;
            var form = (day ? 2 : 0) +
                (second && phase != "transition" ? 1 : 0);
            SetTarget(scene, second && phase != "transition" ? .4f : .8f,
                state == 8 ? -12f : 0f, state, tick, index, form);
        }

        private static void ConfigureEmpressP1Dash(CombatSnapshot scene,
            bool day, int state, int tick, int attackIndex, float velocityX)
        {
            scene.Difficulty.DayTime = day;
            // P1 is form zero. The supplied state/index pairs are the real
            // post-selection dash entries from PhaseOneAttacks; SetTarget also
            // refreshes the independent native observation from this same
            // snapshot, as Runtime does.
            SetTarget(scene, .8f, velocityX, state, tick, attackIndex, 0f);
        }

        private static void ConfigureMoonLordPhase(CombatSnapshot scene,
            string phase)
        {
            ConfigureMoonLordCore(scene, phase == "intro" ? -1 : 0, 0,
                true);

            // Every non-intro source carries its genuine parent key, side,
            // integer clock and local-player target.  This makes active
            // takeover exercise validation of the real source clocks, not a
            // generic hostile-projectile substitute.
            switch (phase)
            {
                case "intro": break;
                case "synchronize-eyes":
                    AddMoonLordPart(scene, 91, 396, 3, 0, 0);
                    AddMoonLordPart(scene, 92, 397, 0, 0, 0);
                    AddMoonLordPart(scene, 93, 397, 1, 0, 1);
                    break;
                case "head-bolts":
                    AddMoonLordPart(scene, 91, 396, 3, 120, 0);
                    break;
                case "head-tongue":
                    AddMoonLordPart(scene, 91, 396, 2, 300, 0);
                    break;
                case "head-deathray-telegraph":
                    AddMoonLordPart(scene, 91, 396, 1, 985, 0);
                    break;
                case "left-sphere-release":
                    AddMoonLordPart(scene, 92, 397, 2, 390, 0);
                    break;
                case "right-sphere-release":
                    AddMoonLordPart(scene, 93, 397, 2, 540, 1);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase),
                        phase, "Unreviewed Moon Lord active-recovery phase");
            }
            RefreshPriorityNativeContext(scene);
        }

        private static void ConfigureMoonLordCore(CombatSnapshot scene,
            int state, int clock, bool invulnerable)
        {
            var core = scene.Targets[0];
            core.Ai0 = state;
            core.Ai1 = clock;
            core.Ai2 = 0f;
            core.Ai3 = 0f;
            core.Invulnerable = invulnerable;
            core.Chaseable = true;
            scene.Targets[0] = core;
        }

        private static void ConfigureDeerclops(CombatSnapshot scene,
            int state, int timer)
        {
            SetTarget(scene, .8f, 0f, ai0: state, ai1: timer);
        }

        private static void ConfigureSkeletron(CombatSnapshot scene,
            int headState, int headClock, int handState = -1,
            int handClock = 0)
        {
            var head = scene.Targets[0];
            head.Ai0 = 0f;
            head.Ai1 = headState;
            head.Ai2 = headClock;
            head.Ai3 = 0f;
            head.Velocity = headState == 1 ? new Vec2(7f, 0f) :
                new Vec2(0f, 0f);
            scene.Targets[0] = head;
            if (handState >= 0)
                AddSkeletronHand(scene, handState, handClock);
            RefreshPriorityNativeContext(scene);
        }

        private static void ConfigureWall(CombatSnapshot scene,
            float lifeFraction)
        {
            SetTarget(scene, lifeFraction, 0f, ai1: 0f, ai2: 0f);
        }

        private static void AddWallLaserEye(CombatSnapshot scene)
        {
            scene.Targets.Add(new TargetSnapshot
            {
                Key = 92,
                Type = 114,
                Position = new Vec2(1960f, 720f),
                Width = 60,
                Height = 60,
                Life = 8000,
                LifeMax = 10000,
                Boss = true,
                Chaseable = true,
                Ai0Known = true,
                Ai0 = 1f,
                LocalAi1Known = true,
                LocalAi1 = 590f,
                LocalAi2Known = true,
                LocalAi2 = 0f,
                LineOfSightKnown = true,
                HasLineOfSight = true,
                NativeTargetKnown = true,
                NativeTargetPlayerIndex = 0
            });
            RefreshPriorityNativeContext(scene);
        }

        private static void ConfigureFishron(CombatSnapshot scene, int state,
            int timer, int sequence, float lifeFraction, bool expert)
        {
            scene.Difficulty.Expert = expert;
            if (expert) AddExactDash(scene);
            var velocity = state == 1 ? -11f : state == 6 ? -12f :
                state == 11 ? -14f : 0f;
            SetTarget(scene, lifeFraction, velocity, ai0: state, ai1: 0f,
                ai2: timer, ai3: sequence);
        }

        private static void AddSkeletronHand(CombatSnapshot scene,
            int state = 0, int clock = 0)
        {
            var center = new Vec2(1970f, 850f);
            var velocity = new Vec2(0f, 0f);
            if (state == 2)
            {
                center = scene.Player.Center + new Vec2(0f, -360f);
                velocity = new Vec2(0f, 18f);
            }
            else if (state == 5)
            {
                center = scene.Player.Center + new Vec2(420f, 0f);
                velocity = new Vec2(-17f, 0f);
            }
            scene.Targets.Add(new TargetSnapshot
            {
                Key = 90,
                Type = 36,
                Position = center - new Vec2(20f, 20f),
                Velocity = velocity,
                Width = 40,
                Height = 40,
                Life = 400,
                LifeMax = 1000,
                Damage = 20,
                Chaseable = true,
                Ai0Known = true,
                Ai0 = -1f,
                Ai1Known = true,
                Ai1 = scene.Targets[0].Key,
                Ai2Known = true,
                Ai2 = state,
                Ai3Known = true,
                Ai3 = clock,
                NativeTargetKnown = true,
                NativeTargetPlayerIndex = 0
            });
        }

        private static void AddMoonLordPart(CombatSnapshot scene, int key,
            int type, int state, int clock, int side)
        {
            var coreKey = scene.Targets[0].Key;
            scene.Targets.Add(new TargetSnapshot
            {
                Key = key,
                Type = type,
                Position = new Vec2(2000f, 720f),
                Width = 80,
                Height = 80,
                Life = 500,
                LifeMax = 5000,
                Damage = 40,
                Boss = true,
                Chaseable = true,
                Ai0Known = true,
                Ai1Known = true,
                Ai2Known = true,
                Ai3Known = true,
                Ai0 = state,
                Ai1 = clock,
                Ai2 = side,
                Ai3 = coreKey,
                NativeTargetKnown = true,
                NativeTargetPlayerIndex = 0
            });
        }

        private static void AddMoonRecoveryBoundaries(
            IList<MoonRecoveryMatrixFixture> result, string name, int type,
            int side, int[] states, int[] durations)
        {
            var start = 0;
            for (var index = 0; index < states.Length; index++)
            {
                var capturedIndex = index;
                var capturedStart = start;
                var capturedState = states[index];
                var capturedDuration = durations[index];
                result.Add(MoonMatrix(name + "-" + index + "-first", s =>
                    AddMoonLordPart(s, 100 + capturedIndex, type,
                        capturedState, capturedStart, side), true));
                result.Add(MoonMatrix(name + "-" + index + "-last", s =>
                    AddMoonLordPart(s, 100 + capturedIndex, type,
                        capturedState, capturedStart + capturedDuration - 1,
                        side), true));
                start += durations[index];
            }
            Equal(type == 397 ? 600 : 1200, start);
        }

        private static MoonRecoveryMatrixFixture MoonMatrix(string name,
            Action<CombatSnapshot> configure, bool simulateMovement = false) =>
            new MoonRecoveryMatrixFixture
            {
                Name = name,
                Configure = configure,
                SimulateMovement = simulateMovement
            };

        private static void AddMoonLordSphere(CombatSnapshot scene, int key,
            int ageOrMode, int sourceKey, bool ensureSource = true)
        {
            if (ensureSource && sourceKey >= 0 &&
                !HasTargetKey(scene, sourceKey))
                AddMoonLordPart(scene, sourceKey, 396, 3, 0, 0);
            scene.PriorityBoss.MoonLordProjectiles454.Add(
                new MoonLordProjectile454Observation
                {
                    Known = true,
                    ProjectileKey = key,
                    Ai0AgeOrMode = ageOrMode,
                    SourceNpcAi1 = sourceKey,
                    LocalAi0 = 0f,
                    LocalAi1 = 0f,
                    TimeLeft = 300,
                    Alpha = 0,
                    ExtraUpdates = 1,
                    SourceNpcIdentityKnown = sourceKey >= 0,
                    SourceNpcActive = sourceKey >= 0,
                    SourceNpcType = sourceKey >= 0 ? 396 : 0
                });
        }

        private static void AddMoonLordTongue(CombatSnapshot scene, int key,
            int sourceKey, bool returning, bool ensureSource = true)
        {
            if (ensureSource && !HasTargetKey(scene, sourceKey))
                AddMoonLordPart(scene, sourceKey, 396, 2, 300, 0);
            scene.PriorityBoss.MoonLordProjectiles456.Add(
                new MoonLordProjectile456Observation
                {
                    Known = true,
                    ProjectileKey = key,
                    EncodedSourceAi0 = (returning ? -1f : 1f) *
                        (sourceKey + 1),
                    TargetPlayerAi1 = scene.LocalPlayerIndex,
                    AgeTicks = returning ? 331f : 20f,
                    ContactLatchAi = 0f,
                    TimeLeft = 300,
                    SourceNpcIdentityKnown = true,
                    SourceNpcActive = true,
                    SourceNpcType =
                        MoonLordProjectile456Observation.RequiredSourceNpcType
                });
        }

        private static void AddMoonLordRay(CombatSnapshot scene, float age,
            float angularVelocity)
        {
            scene.Threats.Add(new ThreatSnapshot
            {
                Type = 455,
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.MoonLordDeathray,
                Position = new Vec2(2500f, 500f),
                BeamOrigin = new Vec2(2500f, 500f),
                BeamDirection = new Vec2(0f, 1f),
                BeamAngularVelocity = angularVelocity,
                BeamAge = age,
                BeamLength = 2400f,
                BeamScale = 1f,
                BeamScaleLimit = 1f,
                TimeLeft = 180
            });
        }

        private static bool HasTargetKey(CombatSnapshot scene, int key)
        {
            for (var index = 0; index < scene.Targets.Count; index++)
                if (scene.Targets[index].Key == key) return true;
            return false;
        }

        private static bool AssertMoonRecoveryReachesBoundedOutcome(
            CombatSnapshot scene, string name, bool simulateMovement)
        {
            var planner = new CombatPlanner(new PlannerSettings
            {
                RecoveryTicks = 2,
                StuckTicksBeforeRecovery = 10000,
                EmergencyRiskThreshold = float.MaxValue,
                PatternSafeRiskThreshold = float.MaxValue
            });
            string reason;
            Equal(ActiveEncounterPreparationResult.Ready,
                planner.PrepareForActiveEncounterDetailed(scene, out reason));

            var plan = planner.Plan(scene);
            False(plan.RequestControlReturn,
                name + " returned on its first controlled frame: " +
                plan.ControlReturnReason);
            Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            if (simulateMovement)
                ApplyMoonRecoveryPlan(scene, plan);

            for (var tick = 2; tick <= 600; tick++)
            {
                plan = planner.Plan(scene);
                if (plan.RequestControlReturn)
                {
                    AssertNeutralRecoveryReturn(plan);
                    return false;
                }
                if (plan.TacticalMode == TacticalMode.EstablishPattern ||
                    plan.TacticalMode == TacticalMode.StablePattern)
                    return true;
                True(plan.TacticalMode == TacticalMode.RecoverToPattern ||
                    plan.TacticalMode == TacticalMode.EmergencyEvade,
                    name + " escaped recovery without convergence on tick " +
                    tick + ": " + plan.TacticalMode);
                if (simulateMovement)
                    ApplyMoonRecoveryPlan(scene, plan);
            }
            throw new InvalidOperationException(name +
                " neither converged nor returned neutral control in 600 ticks");
        }

        private static void ApplyMoonRecoveryPlan(CombatSnapshot scene,
            ControlPlan plan)
        {
            var horizontal = plan.Horizontal == 0 ?
                scene.Player.Velocity.X * .5f : plan.Horizontal * 2f;
            var vertical = plan.Jump ? -1f : plan.Drop ? 1f : 0f;
            scene.Player.Velocity = new Vec2(horizontal, vertical);
            scene.Player.Position += scene.Player.Velocity;
            scene.Player.OnGround = !plan.Jump && !plan.Drop &&
                Math.Abs(vertical) < .01f;
            RefreshRecoveryClearance(scene);
        }

        private static void AddPhaseThreat(CombatSnapshot scene, int type)
        {
            var threat = new ThreatSnapshot
            {
                Type = type,
                Kind = ThreatKind.Projectile,
                Position = new Vec2(4800f, 2200f),
                Width = 2,
                Height = 2,
                Damage = 0,
                TimeLeft = 300
            };
            if (type == 455)
            {
                // A production Moon Lord ray is never a point projectile.
                // Preserve the native beam geometry so this fixture exercises
                // the active deathray lane instead of collapsing to core-finish.
                threat.Geometry = ThreatGeometry.MoonLordDeathray;
                threat.BeamOrigin = new Vec2(2500f, 500f);
                threat.BeamDirection = new Vec2(0f, 1f);
                threat.BeamAge = 20f;
                threat.BeamLength = 2400f;
                threat.BeamScale = 1f;
                threat.BeamScaleLimit = 1f;
                threat.TimeLeft = 180;
            }
            scene.Threats.Add(threat);
            if (type == 454)
            {
                scene.PriorityBoss.MoonLordProjectiles454.Add(
                    new MoonLordProjectile454Observation
                    {
                        Known = true,
                        ProjectileKey = 77,
                        Ai0AgeOrMode = -1f,
                        SourceNpcAi1 = -1f,
                        LocalAi0 = 0f,
                        LocalAi1 = 0f,
                        TimeLeft = 300,
                        Alpha = 0,
                        ExtraUpdates = 1
                    });
            }
        }

        private static void RefreshRecoveryClearance(CombatSnapshot scene)
        {
            scene.Arena.ClearanceLeft = scene.Player.Position.X -
                scene.Arena.LocalOpenBounds.Left;
            scene.Arena.ClearanceRight = scene.Arena.LocalOpenBounds.Right -
                scene.Player.Position.X - scene.Player.Width;
            scene.Arena.ClearanceUp = scene.Player.Position.Y -
                scene.Arena.LocalOpenBounds.Top;
            scene.Arena.ClearanceDown = scene.Arena.LocalOpenBounds.Bottom -
                scene.Player.Position.Y - scene.Player.Height;
        }

        private static object GetPrivateField(object owner, string name)
        {
            var field = owner.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            True(field != null, "missing recovery field " + name);
            return field.GetValue(owner);
        }

        private static void AssertPhaseCount(
            IDictionary<string, HashSet<string>> observed, string group,
            int minimum)
        {
            HashSet<string> phases;
            True(observed.TryGetValue(group, out phases) &&
                phases.Count >= minimum,
                "native recovery fixtures collapsed " + group + " to " +
                (phases == null ? 0 : phases.Count) + " distinct phases");
        }

        private static bool ReadBool(object value, string name)
        {
            var field = value.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            True(field != null, "missing recovery candidate field " + name);
            return (bool)field.GetValue(value);
        }

        private static float ReadFloat(object value, string name)
        {
            var field = value.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.Public |
                BindingFlags.NonPublic);
            True(field != null, "missing recovery candidate field " + name);
            return (float)field.GetValue(value);
        }

        private static void AssertNeutralRecoveryReturn(ControlPlan plan)
        {
            True(plan.RequestControlReturn);
            Equal(TacticalMode.RecoverToPattern, plan.TacticalMode);
            Equal(0, plan.Horizontal);
            False(plan.Jump || plan.Drop || plan.Fire || plan.QuickHeal ||
                plan.QuickMana || plan.Dash || plan.Hook || plan.ToggleMount ||
                plan.FeatherFallUp || plan.GravityControl != 0 ||
                plan.LateMobilityFallback.Known,
                "failed active recovery leaked native input");
            True(!string.IsNullOrEmpty(plan.ControlReturnReason));
        }

        private static void AssertNeutralPriorityStateReturn(ControlPlan plan,
            string name)
        {
            True(plan.RequestControlReturn, name +
                " did not return control after native-state drift");
            Equal(0, plan.Horizontal);
            False(plan.Jump || plan.Drop || plan.Fire || plan.QuickHeal ||
                plan.QuickMana || plan.Dash || plan.Hook || plan.ToggleMount ||
                plan.FeatherFallUp || plan.GravityControl != 0 ||
                plan.LateMobilityFallback.Known,
                name + " leaked input after native-state drift");
            True(!string.IsNullOrEmpty(plan.ControlReturnReason), name +
                " returned control without a native-state reason");
        }

        private sealed class MoonCoreRecoveryFixture
        {
            public readonly string Name;
            public readonly int State;
            public readonly int Clock;
            public readonly bool Invulnerable;
            public readonly string ExpectedPhase;

            public MoonCoreRecoveryFixture(string name, int state, int clock,
                bool invulnerable, string expectedPhase)
            {
                Name = name;
                State = state;
                Clock = clock;
                Invulnerable = invulnerable;
                ExpectedPhase = expectedPhase;
            }
        }

        private sealed class MoonRecoveryMatrixFixture
        {
            public string Name;
            public Action<CombatSnapshot> Configure;
            public bool SimulateMovement;
        }

        private sealed class EmpressP1DashRecoveryFixture
        {
            public readonly string Name;
            public readonly int State;
            public readonly int Tick;
            public readonly int AttackIndex;
            public readonly float VelocityX;
            public readonly string Window;

            public EmpressP1DashRecoveryFixture(string name, int state,
                int tick, int attackIndex, float velocityX, string window)
            {
                Name = name;
                State = state;
                Tick = tick;
                AttackIndex = attackIndex;
                VelocityX = velocityX;
                Window = window;
            }
        }

        private sealed class PriorityRecoveryCase
        {
            public string Name;
            public int BossType;
            public Action<CombatSnapshot> Configure;
        }

        private sealed class PriorityActiveNativeFailureCase
        {
            public string Name;
            public Func<CombatSnapshot> Create;
            public Action<CombatSnapshot> Corrupt;
        }
    }
}
