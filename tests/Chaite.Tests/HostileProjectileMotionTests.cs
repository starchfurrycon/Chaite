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
            Run(nameof(PlannerUsesBouncingBoltEnvelopeInEverySafetyPath),
                PlannerUsesBouncingBoltEnvelopeInEverySafetyPath);
            Run(nameof(HostileMotionHotPathsAllocateNothing),
                HostileMotionHotPathsAllocateNothing);
        }

        private static void HostileTrajectoryClassificationIsExplicit()
        {
            // Projectile types 872 and 873 belonged to the withdrawn Empress
            // rainbow streak. They now fall back to the conservative Linear
            // reading like every other unreviewed type.
            foreach (var withdrawn in new[] { 872, 873 })
                Equal(ThreatTrajectory.Linear,
                    HostileProjectileMotion.ForProjectileType(withdrawn));
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
            // The withdrawn Empress boss body (636) is no longer a source-bound
            // contact hazard, and neither is the Boss itself.
            foreach (var ordinary in new[] { 370, 374, 636 })
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(ordinary,
                        true));
            foreach (var fishronHazard in new[] { 371, 372, 373 })
            {
                Equal(ThreatTrajectory.UnmodeledDukeFishronHazard,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(
                        fishronHazard, true));
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundNpcTrajectory(
                        fishronHazard, false));
            }

            foreach (var ordinary in new[] { 383, 387, 871, 872, 873, 874 })
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        ordinary, true));
            foreach (var fishronHazard in new[] { 384, 385, 386 })
            {
                Equal(ThreatTrajectory.UnmodeledDukeFishronHazard,
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        fishronHazard, true));
                Equal(ThreatTrajectory.Linear,
                    PriorityBossThreatGate.SourceBoundProjectileTrajectory(
                        fishronHazard, false));
            }
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
                ThreatTrajectory.UnmodeledDukeFishronHazard, 439);
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
                PriorityBossThreat(ThreatKind.NpcContact, 371,
                    ThreatTrajectory.UnmodeledDukeFishronHazard, 370)
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

        private static void HostileMotionRejectsMalformedAndNegativeSamples()
        {
            var threat = HostileFishronTornado();
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
            // A missing same-frame Boss provenance leaves the hazard
            // fail-closed instead of silently becoming a linear guess.
            malformed = threat;
            malformed.SourceBossContextKnown = false;
            False(HostileProjectileMotion.TrySample(malformed, 1,
                out sample));

            // The tornado body is inert on the tick its lifetime runs out.
            threat.TimeLeft = 4;
            True(HostileProjectileMotion.TrySample(threat, 3, out sample));
            True(sample.Active);
            True(HostileProjectileMotion.TrySample(threat, 4, out sample));
            False(sample.Active);
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
            var tornado = HostileFishronTornado();
            var falling = HostileFallingThreat(920,
                ThreatTrajectory.FallingHostileBolt, 4f);
            var bouncing = HostileFallingThreat(921,
                ThreatTrajectory.BouncingFallingHostileBolt, 4f);
            ProjectileMotionSample sample;
            ProjectileMotionSweep motionSweep;
            for (var warmup = 0; warmup < 32; warmup++)
            {
                HostileProjectileMotion.TrySample(tornado, 12, out sample);
                HostileProjectileMotion.TrySweep(tornado, 0, 12,
                    out motionSweep);
                HostileProjectileMotion.TrySample(falling, 12, out sample);
                HostileProjectileMotion.TrySweep(bouncing, 0, 12,
                    out motionSweep);
            }

            GC.GetAllocatedBytesForCurrentThread();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0f;
            for (var iteration = 0; iteration < 2048; iteration++)
            {
                HostileProjectileMotion.TrySample(tornado,
                    iteration % 48, out sample);
                checksum += sample.Bounds.X + sample.Velocity.Y;
                HostileProjectileMotion.TrySweep(tornado, 0, 12,
                    out motionSweep);
                checksum += motionSweep.Bounds.X;
                HostileProjectileMotion.TrySample(falling,
                    iteration % 48, out sample);
                checksum += sample.Bounds.Y + sample.Velocity.X;
                HostileProjectileMotion.TrySweep(bouncing, 0, 12,
                    out motionSweep);
                checksum += motionSweep.Bounds.Width;
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Equal(0L, allocated);
            False(float.IsNaN(checksum) || float.IsInfinity(checksum));
        }

        private static ThreatSnapshot HostileFishronTornado()
        {
            var threat = PriorityBossThreat(ThreatKind.Projectile, 384,
                ThreatTrajectory.UnmodeledDukeFishronHazard,
                PriorityBossThreatGate.DukeFishronType);
            // AI_064 reads ai[0], ai[1] and localAI[0]; a snapshot that does
            // not declare all three stays fail-closed.
            threat.TrajectoryAi0Known = true;
            threat.TrajectoryAi1Known = true;
            threat.TrajectoryAi1 = -1f;
            threat.TrajectoryLocalAi0Known = true;
            return threat;
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

        private static void HostileNear(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("expected " + expected +
                    ", got " + actual);
        }
    }
}
