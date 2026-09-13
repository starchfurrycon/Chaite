using Chaite.Core;
using System;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunHostileProjectileMotionRegressions()
        {
            Run(nameof(HostileTrajectoryClassificationIsExplicit),
                HostileTrajectoryClassificationIsExplicit);
            Run(nameof(PriorityBossThreatClassificationRequiresLiveSource),
                PriorityBossThreatClassificationRequiresLiveSource);
            Run(nameof(FishronTelegraphCaptureIsStrictlySourceBound),
                FishronTelegraphCaptureIsStrictlySourceBound);
            Run(nameof(PriorityBossNeutralHoldRequiresExactProvenance),
                PriorityBossNeutralHoldRequiresExactProvenance);
            Run(nameof(UnmodeledPriorityThreatsFailClosedInThePlanner),
                UnmodeledPriorityThreatsFailClosedInThePlanner);
            Run(nameof(RainbowStreakFirstStageMatchesNativeUpdates),
                RainbowStreakFirstStageMatchesNativeUpdates);
            Run(nameof(RainbowStreakHomingEnvelopeContainsNativeExtremes),
                RainbowStreakHomingEnvelopeContainsNativeExtremes);
            Run(nameof(RainbowStreakTargetedUpdatesMatchNativeAi171),
                RainbowStreakTargetedUpdatesMatchNativeAi171);
            Run(nameof(RainbowStreakTargetedStateRequiresExactLocalPlayer),
                RainbowStreakTargetedStateRequiresExactLocalPlayer);
            Run(nameof(RainbowStreakCandidatePathsProduceDistinctSweeps),
                RainbowStreakCandidatePathsProduceDistinctSweeps);
            Run(nameof(RainbowStreakPlannerCacheMatchesAcrossDayAndDifficulty),
                RainbowStreakPlannerCacheMatchesAcrossDayAndDifficulty);
            Run(nameof(RainbowStreakTargetedLifetimeIsFinite),
                RainbowStreakTargetedLifetimeIsFinite);
            Run(nameof(HostileMotionRejectsMalformedAndNegativeSamples),
                HostileMotionRejectsMalformedAndNegativeSamples);
            Run(nameof(FallingBoltMatchesGravityThresholdAndContainsLiquids),
                FallingBoltMatchesGravityThresholdAndContainsLiquids);
            Run(nameof(FallingBoltLiquidEnvelopeContainsVelocitySignChange),
                FallingBoltLiquidEnvelopeContainsVelocitySignChange);
            Run(nameof(FallingBoltMatchesNativeVerticalSpeedCap),
                FallingBoltMatchesNativeVerticalSpeedCap);
            Run(nameof(FallingBoltEnvelopeContainsNativeWind),
                FallingBoltEnvelopeContainsNativeWind);
            Run(nameof(BouncingBoltEnvelopeContainsNativeResponses),
                BouncingBoltEnvelopeContainsNativeResponses);
            Run(nameof(EmpressLanceLaunchAndDamageGateMatchNative),
                EmpressLanceLaunchAndDamageGateMatchNative);
            Run(nameof(EmpressLanceSweepKeepsLineWidthAndTravel),
                EmpressLanceSweepKeepsLineWidthAndTravel);
            Run(nameof(EmpressLanceRepeatsRawNativeFloatMovement),
                EmpressLanceRepeatsRawNativeFloatMovement);
            Run(nameof(EmpressLanceWorldScaleBoundsContainNativeCorners),
                EmpressLanceWorldScaleBoundsContainNativeCorners);
            Run(nameof(EmpressLanceHugeSweepClampsToNativeLifetime),
                EmpressLanceHugeSweepClampsToNativeLifetime);
            Run(nameof(PlannerUsesLanceDamageGate),
                PlannerUsesLanceDamageGate);
            Run(nameof(PlannerFailsClosedForMalformedLance),
                PlannerFailsClosedForMalformedLance);
            Run(nameof(PlannerUsesBouncingBoltEnvelopeInEverySafetyPath),
                PlannerUsesBouncingBoltEnvelopeInEverySafetyPath);
            Run(nameof(HostileMotionHotPathsAllocateNothing),
                HostileMotionHotPathsAllocateNothing);
        }

        private static void HostileTrajectoryClassificationIsExplicit()
        {
            Equal(ThreatTrajectory.EmpressRainbowStreak,
                HostileProjectileMotion.ForProjectileType(873));
            Equal(ThreatTrajectory.FallingHostileBolt,
                HostileProjectileMotion.ForProjectileType(920));
            Equal(ThreatTrajectory.BouncingFallingHostileBolt,
                HostileProjectileMotion.ForProjectileType(921));

            foreach (var ordinary in new[] { 0, 874, 919, 923, 924, 9999 })
                Equal(ThreatTrajectory.Linear,
                    HostileProjectileMotion.ForProjectileType(ordinary));

            var stationary = new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Type = 874,
                Position = new Vec2(120f, 240f),
                Velocity = new Vec2(0f, 0f),
                Width = 30,
                Height = 30,
                TimeLeft = 210
            };
            ProjectileMotionSample ignored;
            False(HostileProjectileMotion.TrySample(stationary, 10,
                out ignored));
            var future = stationary.BoundsAt(10f);
            Equal(120f, future.X);
            Equal(240f, future.Y);
            Equal(30f, future.Width);
            Equal(30f, future.Height);
        }

        private static void PriorityBossThreatClassificationRequiresLiveSource()
        {
            foreach (var ordinary in new[] { 370, 374 })
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(ordinary,
                        8f, true, true));
            foreach (var fishronHazard in new[] { 371, 372, 373 })
            {
                Equal(ThreatTrajectory.UnmodeledDukeFishronHazard,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(
                        fishronHazard, 0f, true, false));
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(
                        fishronHazard, 0f, false, true));
            }

            foreach (var state in new[] { 7f, 10f, 8.5f, float.NaN })
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(636,
                        state, false, true));
            foreach (var dashState in new[] { 8f, 9f })
            {
                Equal(ThreatTrajectory.UnmodeledEmpressDashContact,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(636,
                        dashState, false, true));
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(636,
                        dashState, true, false));
            }

            foreach (var ordinary in new[] { 383, 387, 871, 874 })
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        ordinary, true, true));
            foreach (var fishronHazard in new[] { 384, 385, 386 })
            {
                Equal(ThreatTrajectory.UnmodeledDukeFishronHazard,
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        fishronHazard, true, false));
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        fishronHazard, false, true));
            }
            Equal(ThreatTrajectory.UnmodeledEmpressRainbowTrail,
                PriorityBossThreatGate.SourceBoundProjectileTrajectory(872,
                    false, true));
            Equal(ThreatTrajectory.Linear,
                PriorityBossThreatGate.SourceBoundProjectileTrajectory(872,
                    true, false));
            // Type 873 already has a reviewed trajectory and is not one of the
            // new source-gated unknown-motion sentinels.
            Equal(ThreatTrajectory.EmpressRainbowStreak,
                PriorityBossThreatGate.SourceBoundProjectileTrajectory(873,
                    false, false));
        }

        private static void FishronTelegraphCaptureIsStrictlySourceBound()
        {
            var fishron = ThreatTrajectory.UnmodeledDukeFishronHazard;
            False(PriorityBossThreatGate.ShouldCaptureProjectile(false, 1,
                385, fishron));
            False(PriorityBossThreatGate.ShouldCaptureProjectile(false, 0,
                385, fishron));
            False(PriorityBossThreatGate.ShouldCaptureProjectile(true, 0,
                385, ThreatTrajectory.Linear));
            False(PriorityBossThreatGate.ShouldCaptureProjectile(true, -1,
                384, fishron));
            False(PriorityBossThreatGate.ShouldCaptureProjectile(true, 0,
                384, fishron));

            // Once type 385 is source locked, its current damage metadata does
            // not erase the dangerous 384/386 chain created by Kill.
            foreach (var damage in new[] { -1, 0, 1 })
                True(PriorityBossThreatGate.ShouldCaptureProjectile(true,
                    damage, 385, fishron));
            True(PriorityBossThreatGate.ShouldCaptureProjectile(true, 1,
                9999, ThreatTrajectory.Linear));
        }

        private static void PriorityBossNeutralHoldRequiresExactProvenance()
        {
            var fishron = CombatScenario(370);
            var hazard = PriorityBossThreat(ThreatKind.Projectile, 384,
                ThreatTrajectory.UnmodeledDukeFishronHazard, 370);
            fishron.Threats.Add(hazard);
            var fishronDecision = new BossStrategyEngine().Evaluate(fishron).
                Directive;
            True(fishronDecision.HoldNeutralControls,
                "a source-locked Fishron tornado did not request a neutral hold");
            False(fishronDecision.RequestControlReturn,
                "a transient Fishron hazard ended the takeover session");
            True(fishronDecision.NeutralControlReason.Contains("trajectory"));
            False(fishronDecision.ForceContinuousMovement);

            var empress = CombatScenario(636);
            hazard = PriorityBossThreat(ThreatKind.Projectile, 872,
                ThreatTrajectory.UnmodeledEmpressRainbowTrail, 636);
            empress.Threats.Add(hazard);
            var empressDecision = new BossStrategyEngine().Evaluate(empress).
                Directive;
            True(empressDecision.HoldNeutralControls,
                "a source-locked Empress history trail did not request a neutral hold");
            False(empressDecision.RequestControlReturn,
                "a transient Empress hazard ended the takeover session");
            True(empressDecision.NeutralControlReason.Contains("history"));
            False(empressDecision.ForceContinuousMovement);

            var dash = CombatScenario(636);
            dash.Threats.Add(PriorityBossThreat(ThreatKind.NpcContact, 636,
                ThreatTrajectory.UnmodeledEmpressDashContact, 636));
            var dashDecision = new BossStrategyEngine().Evaluate(dash).
                Directive;
            True(dashDecision.HoldNeutralControls,
                "Empress dash contact did not request a neutral hold");
            False(dashDecision.RequestControlReturn);

            var missing = CombatScenario(370);
            hazard = PriorityBossThreat(ThreatKind.Projectile, 384,
                ThreatTrajectory.UnmodeledDukeFishronHazard, 370);
            hazard.SourceBossContextKnown = false;
            missing.Threats.Add(hazard);
            False(new BossStrategyEngine().Evaluate(missing).Directive.
                HoldNeutralControls,
                "an unknown provenance tag opened the Fishron hold gate");

            var wrong = CombatScenario(370);
            hazard = PriorityBossThreat(ThreatKind.Projectile, 384,
                ThreatTrajectory.UnmodeledDukeFishronHazard, 636);
            wrong.Threats.Add(hazard);
            False(new BossStrategyEngine().Evaluate(wrong).Directive.
                HoldNeutralControls,
                "a mismatched provenance tag opened the Fishron hold gate");

            string ignored;
            var mismatchedKind = CombatScenario(370);
            mismatchedKind.Threats.Add(PriorityBossThreat(
                ThreatKind.NpcContact, 384,
                ThreatTrajectory.UnmodeledDukeFishronHazard, 370));
            False(PriorityBossThreatGate.TryGetNeutralHoldReason(
                mismatchedKind, 370, out ignored));
            var mismatchedType = CombatScenario(636);
            mismatchedType.Threats.Add(PriorityBossThreat(
                ThreatKind.Projectile, 873,
                ThreatTrajectory.UnmodeledEmpressRainbowTrail, 636));
            False(PriorityBossThreatGate.TryGetNeutralHoldReason(
                mismatchedType, 636, out ignored));
            var mismatchedTrajectory = CombatScenario(636);
            mismatchedTrajectory.Threats.Add(PriorityBossThreat(
                ThreatKind.Projectile, 872,
                ThreatTrajectory.EmpressRainbowStreak, 636));
            False(PriorityBossThreatGate.TryGetNeutralHoldReason(
                mismatchedTrajectory, 636, out ignored));

            fishron.Player.Life = 1;
            var heldPlan = new CombatPlanner(new PlannerSettings()).Plan(fishron);
            True(heldPlan.HoldNeutralControls);
            False(heldPlan.RequestControlReturn,
                "neutral hold was encoded as a Runtime handoff");
            Equal(TacticalMode.EmergencyEvade, heldPlan.TacticalMode);
            Equal(0, heldPlan.Horizontal);
            False(heldPlan.Jump || heldPlan.Drop || heldPlan.Fire ||
                heldPlan.QuickHeal || heldPlan.QuickMana || heldPlan.Dash ||
                heldPlan.Hook || heldPlan.ToggleMount ||
                heldPlan.FeatherFallUp);
            Equal(0, heldPlan.GravityControl);
            Equal(JumpAction.Release, heldPlan.JumpAction);
            True(heldPlan.NeutralControlReason.Contains("trajectory"));
            True(string.IsNullOrEmpty(heldPlan.ControlReturnReason));
        }

        private static void UnmodeledPriorityThreatsFailClosedInThePlanner()
        {
            var threats = new[]
            {
                PriorityBossThreat(ThreatKind.Projectile, 384,
                    ThreatTrajectory.UnmodeledDukeFishronHazard, 370),
                PriorityBossThreat(ThreatKind.Projectile, 385,
                    ThreatTrajectory.UnmodeledDukeFishronHazard, 370),
                PriorityBossThreat(ThreatKind.Projectile, 386,
                    ThreatTrajectory.UnmodeledDukeFishronHazard, 370),
                PriorityBossThreat(ThreatKind.Projectile, 872,
                    ThreatTrajectory.UnmodeledEmpressRainbowTrail, 636),
                PriorityBossThreat(ThreatKind.NpcContact, 371,
                    ThreatTrajectory.UnmodeledDukeFishronHazard, 370),
                PriorityBossThreat(ThreatKind.NpcContact, 636,
                    ThreatTrajectory.UnmodeledEmpressDashContact, 636)
            };
            for (var index = 0; index < threats.Length; index++)
            {
                ProjectileMotionSample sample;
                ProjectileMotionSweep sweep;
                False(HostileProjectileMotion.TrySample(threats[index], 0,
                    out sample));
                False(HostileProjectileMotion.TrySweep(threats[index], 0, 6,
                    out sweep));
            }

            var settings = new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                ImmediateThreatTicks = 6,
                NearMissPenalty = 0f
            };
            var scenario = CombatScenario(4);
            var unknown = threats[0];
            unknown.Position = new Vec2(9000f, 4000f);
            scenario.Threats.Add(unknown);
            var planner = new CombatPlanner(settings);
            var plan = planner.Plan(scenario);
            Equal(1, planner.LastRelevantThreatCount);
            Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
        }

        private static ThreatSnapshot PriorityBossThreat(ThreatKind kind,
            int type, ThreatTrajectory trajectory, int sourceBossType)
        {
            return new ThreatSnapshot
            {
                Kind = kind,
                Geometry = ThreatGeometry.Body,
                Trajectory = trajectory,
                Type = type,
                Position = new Vec2(1200f, 700f),
                Velocity = new Vec2(3f, -2f),
                Width = 30,
                Height = 30,
                Damage = 100,
                TimeLeft = 120,
                NativeIdentity = 17,
                TrajectoryAi0 = 0f,
                SourceBossContextKnown = true,
                SourceBossType = sourceBossType
            };
        }

        private static void RainbowStreakFirstStageMatchesNativeUpdates()
        {
            var threat = HostileRainbowThreat();
            var nativePosition = threat.Position;
            var nativeVelocity = threat.Velocity;
            for (var tick = 1; tick <= 60; tick++)
            {
                nativeVelocity *= .98f;
                var wave = (float)Math.Cos(threat.NativeIdentity % 6f / 6f +
                    nativePosition.X / 320f + nativePosition.Y / 160f);
                var rotation = wave * (HostilePi * 2f) * .125f / 30f;
                nativeVelocity = HostileRotate(nativeVelocity, rotation);
                nativePosition += nativeVelocity;

                ProjectileMotionSample sample;
                True(HostileProjectileMotion.TrySample(threat, tick,
                    out sample));
                True(sample.Active);
                HostileNear(nativePosition.X, sample.Position.X);
                HostileNear(nativePosition.Y, sample.Position.Y);
                HostileNear(nativeVelocity.X, sample.Velocity.X);
                HostileNear(nativeVelocity.Y, sample.Velocity.Y);
                Equal(0f, sample.UncertaintyX);
                Equal(0f, sample.UncertaintyY);
                HostileBodyInside(sample.Bounds, nativePosition,
                    threat.Width, threat.Height, "rainbow first stage " + tick);
            }
        }

        private static void RainbowStreakHomingEnvelopeContainsNativeExtremes()
        {
            var threat = HostileRainbowThreat();
            threat.TimeLeft = 140;
            threat.Position = new Vec2(900f, 700f);
            threat.Velocity = new Vec2(11f, -7f);

            for (var path = 0; path < 7; path++)
            {
                var nativePosition = threat.Position;
                var nativeVelocity = threat.Velocity;
                var timeLeft = threat.TimeLeft;
                for (var tick = 1; tick <= 50; tick++)
                {
                    var progress = Math.Max(0f, Math.Min(1f,
                        (140f - timeLeft) / 110f));
                    var amount = .05f + .05f * progress;
                    var weight = amount * amount * (3f - 2f * amount);
                    var direction = HostileTargetDirection(path, tick);
                    var targetVelocity = direction * 30f;
                    nativeVelocity = nativeVelocity * (1f - weight) +
                        targetVelocity * weight;
                    nativePosition += nativeVelocity;
                    timeLeft--;

                    ProjectileMotionSample sample;
                    True(HostileProjectileMotion.TrySample(threat, tick,
                        out sample));
                    True(sample.Active);
                    HostileBodyInside(sample.Bounds, nativePosition,
                        threat.Width, threat.Height,
                        "rainbow homing path " + path + " tick " + tick);
                }
            }
        }

        private static void RainbowStreakTargetedUpdatesMatchNativeAi171()
        {
            var threat = HostileRainbowThreat();
            threat.TimeLeft = 140;
            threat.Position = new Vec2(900f, 700f);
            threat.Velocity = new Vec2(11f, -7f);
            TargetedProjectileMotionState state;
            True(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out state));

            var nativePosition = threat.Position;
            var nativeVelocity = threat.Velocity;
            var nativeTimeLeft = threat.TimeLeft;
            for (var tick = 1; tick <= 109; tick++)
            {
                var targetCenter = new Vec2(
                    1150f + (float)Math.Cos(tick * .17f) * 180f,
                    650f + (float)Math.Sin(tick * .11f) * 240f);
                var projectileCenter = new Vec2(
                    nativePosition.X + threat.Width * .5f,
                    nativePosition.Y + threat.Height * .5f);
                var direction = targetCenter - projectileCenter;
                var targetDirection = direction *
                    (1f / (float)Math.Sqrt(direction.LengthSquared));
                var targetVelocity = targetDirection * 30f;
                var progress = Math.Max(0f, Math.Min(1f,
                    (140f - nativeTimeLeft) / 110f));
                var amount = .05f + .05f * progress;
                var weight = amount * amount * (3f - 2f * amount);
                nativeVelocity = nativeVelocity * (1f - weight) +
                    targetVelocity * weight;
                nativePosition += nativeVelocity;
                nativeTimeLeft--;

                ProjectileMotionSample sample;
                True(HostileProjectileMotion.TryAdvanceTargeted(ref state,
                    targetCenter, out sample));
                True(sample.Active);
                HostileNear(nativePosition.X, sample.Position.X);
                HostileNear(nativePosition.Y, sample.Position.Y);
                HostileNear(nativeVelocity.X, sample.Velocity.X);
                HostileNear(nativeVelocity.Y, sample.Velocity.Y);
                Equal(0f, sample.UncertaintyX);
                Equal(0f, sample.UncertaintyY);
                HostileBodyInside(sample.Bounds, nativePosition,
                    threat.Width, threat.Height,
                    "targeted rainbow tick " + tick);
            }
            Equal(31, state.TimeLeft);
        }

        private static void RainbowStreakTargetedStateRequiresExactLocalPlayer()
        {
            var threat = HostileRainbowThreat();
            TargetedProjectileMotionState state;
            True(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out state));
            False(HostileProjectileMotion.TryCreateTargetedState(threat, 1,
                out state));
            False(HostileProjectileMotion.TryCreateTargetedState(threat, -1,
                out state));
            False(HostileProjectileMotion.TryCreateTargetedState(threat, 255,
                out state));

            threat.NativeTargetPlayerKnown = false;
            False(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out state));
            threat.NativeTargetPlayerKnown = true;
            threat.NativeTargetPlayerIndex = 255;
            False(HostileProjectileMotion.TryCreateTargetedState(threat, 255,
                out state));
            threat.NativeTargetPlayerIndex = 0;
            threat.TrajectoryAi0 = 1f;
            False(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out state));
        }

        private static void RainbowStreakCandidatePathsProduceDistinctSweeps()
        {
            var threat = HostileRainbowThreat();
            threat.TimeLeft = 100;
            threat.Position = new Vec2(900f, 700f);
            threat.Velocity = new Vec2(11f, -7f);
            TargetedProjectileMotionState upper;
            TargetedProjectileMotionState lower;
            True(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out upper));
            True(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out lower));

            var start = new Vec2(1150f, 700f);
            ProjectileMotionSweep upperSweep;
            ProjectileMotionSweep lowerSweep;
            True(HostileProjectileMotion.TryAdvanceTargetedSweep(ref upper,
                start, new Vec2(1150f, 300f), 12, out upperSweep));
            True(HostileProjectileMotion.TryAdvanceTargetedSweep(ref lower,
                start, new Vec2(1150f, 1100f), 12, out lowerSweep));
            True(upperSweep.Active && lowerSweep.Active);
            True(upper.Position.Y < lower.Position.Y - 10f,
                "candidate-specific target paths did not diverge");

            ProjectileMotionSweep broad;
            True(HostileProjectileMotion.TrySweep(threat, 0, 12,
                out broad));
            True(broad.Active);
            HostileBodyInside(broad.Bounds, upper.Position, threat.Width,
                threat.Height, "upper targeted path escaped broadphase");
            HostileBodyInside(broad.Bounds, lower.Position, threat.Width,
                threat.Height, "lower targeted path escaped broadphase");
            True(upperSweep.Bounds.Height < broad.Bounds.Height &&
                lowerSweep.Bounds.Height < broad.Bounds.Height,
                "candidate sweep did not narrow the broadphase envelope");
        }

        private static void RainbowStreakTargetedLifetimeIsFinite()
        {
            var threat = HostileRainbowThreat();
            threat.TimeLeft = 2;
            TargetedProjectileMotionState state;
            True(HostileProjectileMotion.TryCreateTargetedState(threat, 0,
                out state));
            ProjectileMotionSweep sweep;
            True(HostileProjectileMotion.TryAdvanceTargetedSweep(ref state,
                new Vec2(500f, 500f), new Vec2(520f, 500f), 3,
                out sweep));
            True(sweep.Active);
            False(state.Active);
            Equal(0, state.TimeLeft);

            True(HostileProjectileMotion.TryAdvanceTargetedSweep(ref state,
                new Vec2(520f, 500f), new Vec2(540f, 500f), 3,
                out sweep));
            False(sweep.Active);
            Equal(0, state.TimeLeft);
            False(HostileProjectileMotion.TryAdvanceTargetedSweep(ref state,
                new Vec2(540f, 500f), new Vec2(540f, 500f), 0,
                out sweep));
        }

        private static void RainbowStreakPlannerCacheMatchesAcrossDayAndDifficulty()
        {
            for (var day = 0; day <= 1; day++)
            for (var mode = 0; mode <= 2; mode++)
            {
                var scenario = CombatScenario(636);
                scenario.Difficulty.DayTime = day != 0;
                scenario.Difficulty.GameModeKnown = true;
                scenario.Difficulty.GameMode = mode;
                scenario.Difficulty.Expert = mode >= 1;
                scenario.Difficulty.Master = mode >= 2;
                RefreshPriorityNativeContext(scenario);
                var streak = HostileRainbowThreat();
                streak.TimeLeft = 60;
                streak.Position = scenario.Player.Position +
                    new Vec2(-140f, -90f);
                streak.Velocity = new Vec2(13f, 4f);
                scenario.Threats.Add(streak);

                var cached = new CombatPlanner(new PlannerSettings
                {
                    CacheThreatPrediction = true,
                    EnableScorePruning = false
                });
                var uncached = new CombatPlanner(new PlannerSettings
                {
                    CacheThreatPrediction = false,
                    EnableScorePruning = false
                });
                AssertPlansIdentical(uncached.Plan(scenario),
                    cached.Plan(scenario), 87300 + day * 10 + mode, 0);
                True(cached.LastRelevantThreatCount > 0,
                    "targeted rainbow streak was culled before planning");
            }
        }

        private static void HostileMotionRejectsMalformedAndNegativeSamples()
        {
            var threat = HostileRainbowThreat();
            ProjectileMotionSample sample;
            ProjectileMotionSweep sweep;
            False(HostileProjectileMotion.TrySample(threat, -1, out sample));
            False(HostileProjectileMotion.TrySweep(threat, -1, 3,
                out sweep));
            False(HostileProjectileMotion.TrySweep(threat, 4, 3,
                out sweep));

            var malformed = threat;
            malformed.Position.X = float.NaN;
            False(HostileProjectileMotion.TrySample(malformed, 1,
                out sample));
            malformed = threat;
            malformed.Velocity.Y = float.PositiveInfinity;
            False(HostileProjectileMotion.TrySample(malformed, 1,
                out sample));
            malformed = threat;
            malformed.TrajectoryAi0 = float.NaN;
            False(HostileProjectileMotion.TrySample(malformed, 1,
                out sample));
            malformed = threat;
            malformed.Kind = ThreatKind.NpcContact;
            False(HostileProjectileMotion.TrySample(malformed, 1,
                out sample));

            threat.TimeLeft = 3;
            True(HostileProjectileMotion.TrySample(threat, 3, out sample));
            True(sample.Active);
            True(HostileProjectileMotion.TrySample(threat, 4, out sample));
            False(sample.Active);

            var lance = HostileLance(61f);
            False(BeamGeometry.AtTime(lance, -1f).Active);
            False(BeamGeometry.AtTime(lance, float.NaN).Active);
            False(BeamGeometry.Sweep(lance, -1f, 2f).Active);
            False(BeamGeometry.Sweep(lance, 2f, 1f).Active);
            False(BeamGeometry.Sweep(lance, 0f,
                float.PositiveInfinity).Active);

            malformed = lance;
            malformed.Kind = ThreatKind.NpcContact;
            False(EmpressLanceGeometry.AtTime(malformed, 0f).Active);
            malformed = lance;
            malformed.Geometry = ThreatGeometry.EmpressSunDance;
            False(EmpressLanceGeometry.AtTime(malformed, 0f).Active);
            malformed = lance;
            malformed.Type = 920;
            False(EmpressLanceGeometry.AtTime(malformed, 0f).Active);
            malformed = lance;
            malformed.TimeLeft = 0;
            False(EmpressLanceGeometry.AtTime(malformed, 0f).Active);
            malformed = lance;
            malformed.TimeLeft = 2;
            True(BeamGeometry.AtTime(malformed, 2f).Active);
            False(BeamGeometry.AtTime(malformed, 3f).Active);
            malformed = lance;
            malformed.BeamAge = -1f;
            False(EmpressLanceGeometry.AtTime(malformed, 0f).Active);
            malformed = lance;
            malformed.BeamDirection = default(Vec2);
            False(EmpressLanceGeometry.AtTime(malformed, 0f).Active);
            var invalidSafetyBeam = BeamGeometry.AtTime(malformed, 0f);
            True(invalidSafetyBeam.Active,
                "the public safety boundary accepted a malformed lance as empty");
            True(BeamGeometry.Intersects(new RectF(120000f, 30000f, 1f, 1f),
                invalidSafetyBeam),
                "the malformed-lance sentinel did not cover vanilla world space");
            malformed = lance;
            malformed.BeamAge = 359f;
            True(BeamGeometry.AtTime(malformed, 0f).Active);
            False(BeamGeometry.AtTime(malformed, 1f).Active);
        }

        private static void FallingBoltMatchesGravityThresholdAndContainsLiquids()
        {
            var bolt = HostileFallingThreat(920,
                ThreatTrajectory.FallingHostileBolt, 4f);
            ProjectileMotionSample first;
            True(HostileProjectileMotion.TrySample(bolt, 1, out first));
            HostileNear(bolt.Velocity.Y + .15f, first.Velocity.Y);
            HostileNear(bolt.Position.X + bolt.Velocity.X,
                first.Position.X);
            HostileNear(bolt.Position.Y + bolt.Velocity.Y + .15f,
                first.Position.Y);
            HostileBodyInside(first.Bounds, first.Position, bolt.Width,
                bolt.Height, "920 dry first tick");

            var delayed = bolt;
            delayed.TrajectoryAi0 = 3f;
            ProjectileMotionSample delayedFirst;
            True(HostileProjectileMotion.TrySample(delayed, 1,
                out delayedFirst));
            HostileNear(delayed.Velocity.Y, delayedFirst.Velocity.Y);
            HostileNear(delayed.Position.Y + delayed.Velocity.Y,
                delayedFirst.Position.Y);
            ProjectileMotionSample delayedSecond;
            True(HostileProjectileMotion.TrySample(delayed, 2,
                out delayedSecond));
            HostileNear(delayed.Velocity.Y + .15f,
                delayedSecond.Velocity.Y);

            var liquidPosition = bolt.Position;
            var liquidVelocity = bolt.Velocity;
            var ai0 = bolt.TrajectoryAi0;
            for (var tick = 1; tick <= 24; tick++)
            {
                HostileApplyFallingAi(ref liquidVelocity, ref ai0);
                var scale = tick % 3 == 0 ? .25f :
                    tick % 3 == 1 ? .375f : .5f;
                liquidPosition += liquidVelocity * scale;
                ProjectileMotionSample liquidEnvelope;
                True(HostileProjectileMotion.TrySample(bolt, tick,
                    out liquidEnvelope));
                HostileBodyInside(liquidEnvelope.Bounds, liquidPosition,
                    bolt.Width, bolt.Height, "920 liquid tick " + tick);
            }
        }

        private static void BouncingBoltEnvelopeContainsNativeResponses()
        {
            var bolt = HostileFallingThreat(921,
                ThreatTrajectory.BouncingFallingHostileBolt, 4f);
            var position = bolt.Position;
            var velocity = bolt.Velocity;
            var ai0 = bolt.TrajectoryAi0;
            for (var tick = 1; tick <= 28; tick++)
            {
                HostileApplyFallingAi(ref velocity, ref ai0);
                // Native dry collision handling installs the reflected velocity
                // before UpdatePosition. Two collisions survive penetrate=3;
                // the third one destroys the projectile.
                if (tick == 4 && Math.Abs(velocity.X) > 1f)
                    velocity.X *= -.4f;
                if (tick == 11 && Math.Abs(velocity.Y) > 1f)
                    velocity.Y *= -.95f;
                position += velocity;

                ProjectileMotionSample envelope;
                True(HostileProjectileMotion.TrySample(bolt, tick,
                    out envelope));
                HostileBodyInside(envelope.Bounds, position, bolt.Width,
                    bolt.Height, "921 bounce tick " + tick);
            }

            ProjectileMotionSweep sweep;
            True(HostileProjectileMotion.TrySweep(bolt, 0, 28, out sweep));
            True(sweep.Active);
            HostileBodyInside(sweep.Bounds, position, bolt.Width,
                bolt.Height, "921 full sweep");
        }

        private static void FallingBoltLiquidEnvelopeContainsVelocitySignChange()
        {
            var bolt = HostileFallingThreat(920,
                ThreatTrajectory.FallingHostileBolt, 5f);
            bolt.Velocity = new Vec2(8f, -2f);
            var nativePosition = bolt.Position;
            var nativeVelocity = bolt.Velocity;
            var ai0 = bolt.TrajectoryAi0;
            for (var tick = 1; tick <= 30; tick++)
            {
                HostileApplyFallingAi(ref nativeVelocity, ref ai0);
                // Cross zero while leaving honey.  The signed per-update range
                // must enclose this mixed wet/dry path, not just either endpoint
                // of one all-liquid or all-dry trajectory.
                nativePosition += nativeVelocity * (tick <= 13 ? .25f : 1f);
                ProjectileMotionSample envelope;
                True(HostileProjectileMotion.TrySample(bolt, tick,
                    out envelope));
                HostileBodyInside(envelope.Bounds, nativePosition,
                    bolt.Width, bolt.Height,
                    "920 honey sign-change tick " + tick);
            }
        }

        private static void FallingBoltMatchesNativeVerticalSpeedCap()
        {
            var bolt = HostileFallingThreat(920,
                ThreatTrajectory.FallingHostileBolt, 5f);
            bolt.Velocity = new Vec2(0f, 15.95f);
            ProjectileMotionSample first;
            True(HostileProjectileMotion.TrySample(bolt, 1, out first));
            Equal(16f, first.Velocity.Y);
            Equal(bolt.Position.Y + 16f, first.Position.Y);

            // Positive fall speed remains capped during a long honey path. A
            // gravity-only lower bound would eventually move below this native
            // trajectory and cease to be conservative.
            var honeyPosition = bolt.Position;
            var honeyVelocity = bolt.Velocity;
            var ai0 = bolt.TrajectoryAi0;
            for (var tick = 1; tick <= 30; tick++)
            {
                HostileApplyFallingAi(ref honeyVelocity, ref ai0);
                honeyPosition += honeyVelocity * .25f;
            }
            ProjectileMotionSample honeyEnvelope;
            True(HostileProjectileMotion.TrySample(bolt, 30,
                out honeyEnvelope));
            HostileBodyInside(honeyEnvelope.Bounds, honeyPosition,
                bolt.Width, bolt.Height, "920 capped honey path");

            // AI_001 does not symmetrically clamp this profile's upward speed.
            bolt.Velocity.Y = -20f;
            True(HostileProjectileMotion.TrySample(bolt, 1, out first));
            HostileNear(-19.85f, first.Velocity.Y);
        }

        private static void FallingBoltEnvelopeContainsNativeWind()
        {
            var bolt = HostileFallingThreat(920,
                ThreatTrajectory.FallingHostileBolt, 4f);
            var nativePosition = bolt.Position;
            var nativeVelocity = bolt.Velocity;
            var ai0 = bolt.TrajectoryAi0;
            for (var tick = 1; tick <= 24; tick++)
            {
                HostileApplyFallingAi(ref nativeVelocity, ref ai0);
                // .8 maximum wind target, maximum rain multiplier and the
                // native .1 strength produce < .125 horizontal acceleration.
                nativeVelocity.X += .1244f;
                nativePosition += nativeVelocity;
                ProjectileMotionSample envelope;
                True(HostileProjectileMotion.TrySample(bolt, tick,
                    out envelope));
                HostileBodyInside(envelope.Bounds, nativePosition,
                    bolt.Width, bolt.Height,
                    "920 maximum-wind tick " + tick);
            }
        }

        private static void EmpressLanceLaunchAndDamageGateMatchNative()
        {
            var lance = HostileLance(59f);
            False(BeamGeometry.AtTime(lance, 0f).Active);
            False(BeamGeometry.AtTime(lance, 1f).Active);
            var firstDamage = BeamGeometry.AtTime(lance, 2f);
            True(firstDamage.Active);
            HostileNear(lance.BeamOrigin.X + 80f,
                firstDamage.First.Center.X);
            HostileNear(lance.BeamOrigin.Y, firstDamage.First.Center.Y);

            lance = HostileLance(60f);
            False(BeamGeometry.AtTime(lance, 0f).Active);
            var next = BeamGeometry.AtTime(lance, 1f);
            True(next.Active);
            HostileNear(lance.BeamOrigin.X + 40f, next.First.Center.X);

            lance = HostileLance(61f);
            var current = BeamGeometry.AtTime(lance, 0f);
            True(current.Active);
            HostileNear(lance.BeamOrigin.X, current.First.Center.X);
            True(current.First.HalfLength >= 40f);
            True(current.First.HalfWidth >= 4f);
        }

        private static void EmpressLanceSweepKeepsLineWidthAndTravel()
        {
            var lance = HostileLance(61f);
            var sweep = BeamGeometry.Sweep(lance, 0f, 2f);
            True(sweep.Active);
            Equal(1, sweep.Count);
            HostileNear(lance.BeamOrigin.X + 40f,
                sweep.First.Center.X);
            HostileNear(lance.BeamOrigin.Y, sweep.First.Center.Y);
            HostileNear(80f, sweep.First.HalfLength);
            HostileNear(4f, sweep.First.HalfWidth);

            True(BeamGeometry.Intersects(new RectF(
                lance.BeamOrigin.X - 39.9f, lance.BeamOrigin.Y - .05f,
                .1f, .1f), sweep));
            True(BeamGeometry.Intersects(new RectF(
                lance.BeamOrigin.X + 119.8f, lance.BeamOrigin.Y - .05f,
                .1f, .1f), sweep));
            False(BeamGeometry.Intersects(new RectF(
                lance.BeamOrigin.X + 121f, lance.BeamOrigin.Y - .05f,
                .1f, .1f), sweep));
            False(BeamGeometry.Intersects(new RectF(
                lance.BeamOrigin.X + 40f, lance.BeamOrigin.Y + 4.2f,
                .1f, .1f), sweep));

            var first = BeamGeometry.AtTime(lance, 0f);
            var last = BeamGeometry.AtTime(lance, 2f);
            True(BeamGeometry.Intersects(first.Bounds, sweep));
            True(BeamGeometry.Intersects(last.Bounds, sweep));
        }

        private static void EmpressLanceRepeatsRawNativeFloatMovement()
        {
            var lance = HostileLance(61f);
            lance.BeamOrigin = new Vec2(130000.25f, 10000.25f);
            const float angle = 1.3f;
            lance.BeamDirection = new Vec2((float)Math.Cos(angle),
                (float)Math.Sin(angle));
            lance.TimeLeft = 300;

            var nativeCenter = lance.BeamOrigin;
            var nativeVelocity = lance.BeamDirection * 40f;
            for (var tick = 0; tick < 179; tick++)
                nativeCenter += nativeVelocity;

            var sample = BeamGeometry.AtTime(lance, 179f);
            True(sample.Active);
            Equal(nativeCenter.X, sample.First.Center.X);
            Equal(nativeCenter.Y, sample.First.Center.Y);
            True(sample.First.HalfLength >=
                lance.BeamDirection.Length * 40f);

            var nativeEndpoint = nativeCenter + nativeVelocity;
            True(BeamGeometry.Intersects(new RectF(nativeEndpoint.X - .01f,
                nativeEndpoint.Y - .01f, .02f, .02f), sample),
                "raw non-unit endpoint escaped the current lance lobe");

            var sweep = BeamGeometry.Sweep(lance, 0f, 179f);
            True(sweep.Active);
            True(BeamGeometry.Intersects(new RectF(nativeEndpoint.X - .01f,
                nativeEndpoint.Y - .01f, .02f, .02f), sweep),
                "world-scale per-update float drift escaped the lance sweep");
        }

        private static void EmpressLanceWorldScaleBoundsContainNativeCorners()
        {
            var lance = HostileLance(61f);
            lance.BeamOrigin = new Vec2(133442.969f, -15247.26f);
            const float angle = -1.46412122f;
            lance.BeamDirection = new Vec2((float)Math.Cos(angle),
                (float)Math.Sin(angle));

            var firstCenter = lance.BeamOrigin;
            var secondCenter = firstCenter + lance.BeamDirection * 40f;
            var current = BeamGeometry.AtTime(lance, 1f);
            True(current.Active);
            HostileNativeLanceInside(current.Bounds, secondCenter,
                lance.BeamDirection, "world-scale instant lance");

            var sweep = BeamGeometry.Sweep(lance, 0f, 1f);
            True(sweep.Active);
            HostileNativeLanceInside(sweep.Bounds, firstCenter,
                lance.BeamDirection, "world-scale sweep first sample");
            HostileNativeLanceInside(sweep.Bounds, secondCenter,
                lance.BeamDirection, "world-scale sweep second sample");
        }

        private static void EmpressLanceHugeSweepClampsToNativeLifetime()
        {
            var lance = HostileLance(61f);
            lance.TimeLeft = 1000;
            var sweep = BeamGeometry.Sweep(lance, 0f, float.MaxValue);
            True(sweep.Active,
                "a huge finite caller horizon discarded the current lance");

            // Age 359 is the last valid future integer sample (61 + 298).
            var last = BeamGeometry.AtTime(lance, 298f);
            True(last.Active);
            False(BeamGeometry.AtTime(lance, 299f).Active);
            True(BeamGeometry.Intersects(last.Bounds, sweep),
                "native age clipping dropped the final active lance sample");
        }

        private static void PlannerUsesLanceDamageGate()
        {
            var settings = new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                ImmediateThreatTicks = 6,
                NearMissPenalty = 0f
            };
            var warning = CombatScenario(4);
            var lance = HostileLance(54f);
            lance.BeamOrigin = warning.Player.Center;
            warning.Threats.Add(lance);
            var warningPlanner = new CombatPlanner(settings);
            var warningPlan = warningPlanner.Plan(warning);
            Equal(0, warningPlanner.LastRelevantThreatCount);
            False(warningPlan.TacticalMode == TacticalMode.EmergencyEvade);

            var active = CombatScenario(4);
            lance = HostileLance(61f);
            lance.BeamOrigin = active.Player.Center;
            active.Threats.Add(lance);
            var activePlanner = new CombatPlanner(settings);
            var activePlan = activePlanner.Plan(active);
            Equal(1, activePlanner.LastRelevantThreatCount);
            Equal(TacticalMode.EmergencyEvade, activePlan.TacticalMode);
        }

        private static void PlannerFailsClosedForMalformedLance()
        {
            var settings = new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                ImmediateThreatTicks = 6,
                NearMissPenalty = 0f
            };
            var scenario = CombatScenario(4);
            var malformed = HostileLance(61f);
            malformed.BeamDirection = default(Vec2);
            scenario.Threats.Add(malformed);

            var planner = new CombatPlanner(settings);
            var plan = planner.Plan(scenario);
            Equal(1, planner.LastRelevantThreatCount);
            Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
        }

        private static void PlannerUsesBouncingBoltEnvelopeInEverySafetyPath()
        {
            var settings = new PlannerSettings
            {
                HorizonTicks = 6,
                SimulationStepTicks = 1,
                ImmediateThreatTicks = 6,
                NearMissPenalty = 0f
            };
            var ordinary = CombatScenario(4);
            var bolt = HostileFallingThreat(921,
                ThreatTrajectory.Linear, 4f);
            bolt.Position = ordinary.Player.Position + new Vec2(85f, 0f);
            bolt.Velocity = new Vec2(15f, 0f);
            bolt.Damage = 100;
            ordinary.Threats.Add(bolt);
            var ordinaryPlan = new CombatPlanner(settings).Plan(ordinary);
            False(ordinaryPlan.TacticalMode == TacticalMode.EmergencyEvade);

            var native = CombatScenario(4);
            bolt.Trajectory = ThreatTrajectory.BouncingFallingHostileBolt;
            native.Threats.Add(bolt);
            var nativePlanner = new CombatPlanner(settings);
            var nativePlan = nativePlanner.Plan(native);
            Equal(TacticalMode.EmergencyEvade, nativePlan.TacticalMode);

            var recovery = typeof(CombatPlanner).GetMethod(
                "RecoveryThreatStepSafe", BindingFlags.Instance |
                BindingFlags.NonPublic);
            True(recovery != null);
            var arguments = new object[]
            {
                native,
                new BossDirective(),
                native.Player.Position,
                native.Player.Position,
                6,
                0f
            };
            var recoverySafe = (bool)recovery.Invoke(nativePlanner, arguments);
            False(recoverySafe,
                "optional-mobility recovery ignored the 921 envelope");
        }

        private static void HostileMotionHotPathsAllocateNothing()
        {
            var rainbow = HostileRainbowThreat();
            var falling = HostileFallingThreat(920,
                ThreatTrajectory.FallingHostileBolt, 4f);
            var bouncing = HostileFallingThreat(921,
                ThreatTrajectory.BouncingFallingHostileBolt, 4f);
            var lance = HostileLance(61f);
            ProjectileMotionSample sample;
            ProjectileMotionSweep motionSweep;
            TargetedProjectileMotionState targeted;
            for (var warmup = 0; warmup < 32; warmup++)
            {
                HostileProjectileMotion.TrySample(rainbow, 12, out sample);
                HostileProjectileMotion.TryCreateTargetedState(rainbow, 0,
                    out targeted);
                HostileProjectileMotion.TryAdvanceTargetedSweep(ref targeted,
                    new Vec2(500f, 500f), new Vec2(560f, 440f), 12,
                    out motionSweep);
                HostileProjectileMotion.TrySample(falling, 12, out sample);
                HostileProjectileMotion.TrySweep(bouncing, 0, 12,
                    out motionSweep);
                BeamGeometry.AtTime(lance, 2f);
                BeamGeometry.Sweep(lance, 0f, 3f);
            }

            GC.GetAllocatedBytesForCurrentThread();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0f;
            for (var iteration = 0; iteration < 2048; iteration++)
            {
                HostileProjectileMotion.TrySample(rainbow,
                    iteration % 48, out sample);
                checksum += sample.Bounds.X + sample.Velocity.Y;
                HostileProjectileMotion.TryCreateTargetedState(rainbow, 0,
                    out targeted);
                HostileProjectileMotion.TryAdvanceTargetedSweep(ref targeted,
                    new Vec2(500f, 500f), new Vec2(560f, 440f), 12,
                    out motionSweep);
                checksum += motionSweep.Bounds.X + targeted.Velocity.Y;
                HostileProjectileMotion.TrySample(falling,
                    iteration % 48, out sample);
                checksum += sample.Bounds.Y + sample.Velocity.X;
                HostileProjectileMotion.TrySweep(bouncing, 0, 12,
                    out motionSweep);
                checksum += motionSweep.Bounds.Width;
                var beam = BeamGeometry.AtTime(lance, iteration % 4);
                checksum += beam.Bounds.Height;
                beam = BeamGeometry.Sweep(lance, 0f, 3f);
                checksum += beam.Bounds.Width;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Equal(0L, allocated);
            False(float.IsNaN(checksum) || float.IsInfinity(checksum));
        }

        private const float HostilePi = 3.14159265358979323846f;

        private static ThreatSnapshot HostileRainbowThreat()
        {
            return new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = ThreatTrajectory.EmpressRainbowStreak,
                Type = 873,
                NativeIdentity = 17,
                TrajectoryAi0 = 0f,
                TrajectoryAi0Known = true,
                NativeTargetPlayerKnown = true,
                NativeTargetPlayerIndex = 0,
                Position = new Vec2(123.25f, 456.75f),
                Velocity = new Vec2(13.5f, -4.25f),
                Width = 30,
                Height = 30,
                TimeLeft = 200,
                Damage = 120
            };
        }

        private static ThreatSnapshot HostileFallingThreat(int type,
            ThreatTrajectory trajectory, float ai0)
        {
            return new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.Body,
                Trajectory = trajectory,
                Type = type,
                TrajectoryAi0 = ai0,
                Position = new Vec2(600f, 500f),
                Velocity = new Vec2(7.25f, -5.5f),
                Width = 6,
                Height = 6,
                TimeLeft = 600,
                Damage = 40
            };
        }

        private static ThreatSnapshot HostileLance(float age)
        {
            return new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile,
                Geometry = ThreatGeometry.EmpressLance,
                Type = 919,
                BeamOrigin = new Vec2(100f, 200f),
                BeamDirection = new Vec2(1f, 0f),
                BeamAge = age,
                Width = 8,
                Height = 8,
                TimeLeft = 240,
                Damage = 120
            };
        }

        private static Vec2 HostileTargetDirection(int path, int tick)
        {
            switch (path)
            {
                case 0: return new Vec2(1f, 0f);
                case 1: return new Vec2(-1f, 0f);
                case 2: return new Vec2(0f, 1f);
                case 3: return new Vec2(0f, -1f);
                case 4: return new Vec2(.70710677f, .70710677f);
                case 5: return tick % 2 == 0
                    ? new Vec2(1f, 0f) : new Vec2(-1f, 0f);
                default:
                    var angle = tick * .73f;
                    return new Vec2((float)Math.Cos(angle),
                        (float)Math.Sin(angle));
            }
        }

        private static void HostileApplyFallingAi(ref Vec2 velocity,
            ref float ai0)
        {
            ai0 += 1f;
            if (ai0 >= 5f)
            {
                ai0 = 5f;
                velocity.Y += .15f;
            }
            if (velocity.Y > 16f) velocity.Y = 16f;
        }

        private static Vec2 HostileRotate(Vec2 value, float radians)
        {
            var cosine = (float)Math.Cos(radians);
            var sine = (float)Math.Sin(radians);
            return new Vec2(value.X * cosine - value.Y * sine,
                value.X * sine + value.Y * cosine);
        }

        private static void HostileBodyInside(RectF envelope, Vec2 position,
            int width, int height, string label)
        {
            const float tolerance = .02f;
            True(envelope.Left <= position.X + tolerance &&
                envelope.Top <= position.Y + tolerance &&
                envelope.Right + tolerance >= position.X + width &&
                envelope.Bottom + tolerance >= position.Y + height,
                label + " escaped [" + envelope.Left + "," + envelope.Top +
                ".." + envelope.Right + "," + envelope.Bottom + "] at " +
                position.X + "," + position.Y);
        }

        private static void HostileNativeLanceInside(RectF envelope,
            Vec2 center, Vec2 rawDirection, string label)
        {
            var halfVector = rawDirection * 40f;
            var axis = rawDirection.Normalized();
            var perpendicular = new Vec2(-axis.Y, axis.X) * 4f;
            for (var along = -1; along <= 1; along += 2)
                for (var across = -1; across <= 1; across += 2)
                {
                    var corner = center + halfVector * along +
                        perpendicular * across;
                    True(corner.X >= envelope.Left &&
                        corner.X <= envelope.Right &&
                        corner.Y >= envelope.Top &&
                        corner.Y <= envelope.Bottom,
                        label + " corner escaped [" + envelope.Left + "," +
                        envelope.Top + ".." + envelope.Right + "," +
                        envelope.Bottom + "] at " + corner.X + "," +
                        corner.Y);
                }
        }

        private static void HostileNear(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("expected " + expected +
                    ", got " + actual);
        }
    }
}
