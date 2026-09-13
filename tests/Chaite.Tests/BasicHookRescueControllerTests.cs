using System;
using System.Collections.Generic;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static class BasicHookRescueControllerTests
    {
        public static int RunAll()
        {
            MissingProofAndHeldJumpLeaveThePlanUntouched();
            FactoryEvidenceDrivesTheCompleteSuccessfulTimeline();
            ProjectileAndPlayerDiscontinuitiesAbort();
            StrategyPhaseSequenceAndLoopChangesAbort();
            MissProjectileCannotCompleteUntilNativeDestruction();
            VerifiedProofDoesNotRetainMutableEvidenceArrays();
            VerifiedHotStartPathAllocatesNothing();
            return 7;
        }

        private static void MissingProofAndHeldJumpLeaveThePlanUntouched()
        {
            var controller = new BasicHookRescueController();
            var frame = default(BasicHookFrameSnapshot);
            var plan = SeedPlan();
            var expected = plan;
            Equal(BasicHookRescueStep.Ineligible, controller.Apply(in frame,
                "eye", "loop", true, true, ref plan));
            EqualPlan(in expected, in plan);

            var fixture = Fixture.Create();
            frame = fixture.InitialFrame(10);
            frame.ReleaseJump = false;
            plan = SeedPlan();
            expected = plan;
            Equal(BasicHookRescueStep.Ineligible, controller.Apply(in frame,
                "eye", "loop", true, true, ref plan));
            EqualPlan(in expected, in plan);
        }

        private static void FactoryEvidenceDrivesTheCompleteSuccessfulTimeline()
        {
            var fixture = Fixture.Create();
            var controller = new BasicHookRescueController();
            var plan = SeedPlan();
            var initial = fixture.InitialFrame(100);
            Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                "eye", "low-config-loop", true, true, ref plan));
            True(plan.Hook);
            Near(fixture.Route.IntendedAnchorCenter.X, plan.HookWorld.X);
            Near(fixture.Route.IntendedAnchorCenter.Y, plan.HookWorld.Y);

            for (var index = 0; index < fixture.Route.Evidence.SuccessTickCount; index++)
            {
                var tick = fixture.Route.Evidence.SuccessTicks[index];
                var frame = fixture.FrameAfter(in tick, 100);
                plan = SeedPlan();
                var step = controller.Apply(in frame, "eye", "low-config-loop",
                    true, true, ref plan);
                var expected = ExpectedSuccessStep(fixture.Route, index);
                Equal(expected, step);
                if (expected == BasicHookRescueStep.PulseJumpToDetach)
                {
                    True(plan.Jump);
                    Equal(JumpAction.Hold, plan.JumpAction);
                }
            }
            False(controller.Active);
        }

        private static void ProjectileAndPlayerDiscontinuitiesAbort()
        {
            var fixture = Fixture.Create();
            var first = fixture.Route.Evidence.SuccessTicks[0];

            var controller = new BasicHookRescueController();
            var plan = SeedPlan();
            var initial = fixture.InitialFrame(200);
            Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                "eye", "loop", true, true, ref plan));
            var wrongProjectile = fixture.FrameAfter(in first, 200);
            wrongProjectile.Projectile.Center.X += 1f;
            plan = SeedPlan();
            Equal(BasicHookRescueStep.Abort, controller.Apply(in wrongProjectile,
                "eye", "loop", true, true, ref plan));
            False(controller.Active);

            controller = new BasicHookRescueController();
            plan = SeedPlan();
            initial = fixture.InitialFrame(300);
            Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                "eye", "loop", true, true, ref plan));
            var wrongPlayer = fixture.FrameAfter(in first, 300);
            wrongPlayer.PlayerCenter.Y += 1f;
            plan = SeedPlan();
            Equal(BasicHookRescueStep.Abort, controller.Apply(in wrongPlayer,
                "eye", "loop", true, true, ref plan));
            False(controller.Active);

            var latchIndex = Find(fixture.Route.Evidence.SuccessTicks,
                fixture.Route.Evidence.SuccessTickCount, BasicHookEvidencePhase.Latch);
            controller = StartThrough(fixture, 400, latchIndex);
            var firstPull = fixture.Route.Evidence.SuccessTicks[latchIndex + 1];
            var wrongPull = fixture.FrameAfter(in firstPull, 400);
            wrongPull.PlayerVelocity.X += .5f;
            plan = SeedPlan();
            Equal(BasicHookRescueStep.Abort, controller.Apply(in wrongPull,
                "eye", "loop", true, true, ref plan));
            // A safely attached hook is released with one native jump edge before reset.
            True(controller.Active);
            True(plan.Jump);
        }

        private static void StrategyPhaseSequenceAndLoopChangesAbort()
        {
            var fixture = Fixture.Create();
            var tick = fixture.Route.Evidence.SuccessTicks[0];
            foreach (var mutation in new[] { 0, 1, 2, 3 })
            {
                var controller = new BasicHookRescueController();
                var plan = SeedPlan();
                var initial = fixture.InitialFrame(500 + mutation * 20);
                Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                    "eye", "loop", true, true, ref plan));
                var frame = fixture.FrameAfter(in tick, 500 + mutation * 20);
                var strategy = "eye";
                var phase = "loop";
                if (mutation == 0) strategy = "king-slime";
                else if (mutation == 1) phase = "recover";
                else if (mutation == 2) frame.Sequence++;
                else frame.LowConfigLoopEpoch++;
                plan = SeedPlan();
                Equal(BasicHookRescueStep.Abort, controller.Apply(in frame,
                    strategy, phase, true, true, ref plan));
                False(controller.Active);
            }
        }

        private static void MissProjectileCannotCompleteUntilNativeDestruction()
        {
            var fixture = Fixture.Create();
            var controller = new BasicHookRescueController();
            var plan = SeedPlan();
            var initial = fixture.InitialFrame(700);
            Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                "eye", "loop", true, true, ref plan));

            var sawReturningProjectile = false;
            for (var index = 0; index < fixture.Route.Evidence.MissTickCount; index++)
            {
                var tick = fixture.Route.Evidence.MissTicks[index];
                var frame = fixture.FrameAfter(in tick, 700);
                plan = SeedPlan();
                var step = controller.Apply(in frame, "eye", "loop", true, true,
                    ref plan);
                if (tick.ProjectileActiveAfter && tick.ProjectileAiAfter == 1f)
                {
                    sawReturningProjectile = true;
                    Equal(BasicHookRescueStep.FollowCertifiedMissRecovery, step);
                    True(controller.Active);
                }
                else if (!tick.ProjectileActiveAfter)
                {
                    Equal(BasicHookRescueStep.ReenteredLowConfigLoop, step);
                    False(controller.Active);
                }
                else
                {
                    Equal(BasicHookRescueStep.AwaitLatch, step);
                }
            }
            True(sawReturningProjectile);
        }

        private static void VerifiedProofDoesNotRetainMutableEvidenceArrays()
        {
            var fixture = Fixture.Create();
            for (var index = 0; index < fixture.Route.Evidence.SuccessTickCount; index++)
                fixture.Route.Evidence.SuccessTicks[index] = default(BasicHookTrajectoryTick);
            for (var index = 0; index < fixture.Route.Evidence.MissTickCount; index++)
                fixture.Route.Evidence.MissTicks[index] = default(BasicHookTrajectoryTick);

            var controller = new BasicHookRescueController();
            var plan = SeedPlan();
            var initial = fixture.InitialFrame(900);
            Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                "eye", "loop", true, true, ref plan));
            True(controller.Active);
        }

        private static void VerifiedHotStartPathAllocatesNothing()
        {
            var fixture = Fixture.Create();
            var controller = new BasicHookRescueController();
            var initial = fixture.InitialFrame(1000);
            var plan = SeedPlan();
            controller.Apply(in initial, "eye", "loop", true, true, ref plan);
            controller.Reset();
            GC.GetAllocatedBytesForCurrentThread();
            var before = GC.GetAllocatedBytesForCurrentThread();
            var checksum = 0;
            for (var iteration = 0; iteration < 512; iteration++)
            {
                plan = SeedPlan();
                checksum += (int)controller.Apply(in initial, "eye", "loop",
                    true, true, ref plan);
                controller.Reset();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Equal(0L, allocated);
            Equal(512 * (int)BasicHookRescueStep.ReadyToFire, checksum);
        }

        private static BasicHookRescueController StartThrough(Fixture fixture,
            long sequence, int inclusiveEvidenceIndex)
        {
            var controller = new BasicHookRescueController();
            var plan = SeedPlan();
            var initial = fixture.InitialFrame(sequence);
            Equal(BasicHookRescueStep.ReadyToFire, controller.Apply(in initial,
                "eye", "loop", true, true, ref plan));
            for (var index = 0; index <= inclusiveEvidenceIndex; index++)
            {
                var tick = fixture.Route.Evidence.SuccessTicks[index];
                var frame = fixture.FrameAfter(in tick, sequence);
                plan = SeedPlan();
                var step = controller.Apply(in frame, "eye", "loop", true, true,
                    ref plan);
                Equal(ExpectedSuccessStep(fixture.Route, index), step);
            }
            return controller;
        }

        private static BasicHookRescueStep ExpectedSuccessStep(
            BasicHookRouteCertificate route, int index)
        {
            var tick = route.Evidence.SuccessTicks[index];
            switch (tick.Phase)
            {
            case BasicHookEvidencePhase.Outbound:
                return BasicHookRescueStep.AwaitLatch;
            case BasicHookEvidencePhase.Latch:
                return BasicHookRescueStep.Pulling;
            case BasicHookEvidencePhase.Pull:
                return index + 1 < route.Evidence.SuccessTickCount &&
                    route.Evidence.SuccessTicks[index + 1].Phase ==
                        BasicHookEvidencePhase.DetachPulse
                    ? BasicHookRescueStep.PulseJumpToDetach
                    : BasicHookRescueStep.Pulling;
            case BasicHookEvidencePhase.DetachPulse:
                return BasicHookRescueStep.FollowCertifiedReturn;
            case BasicHookEvidencePhase.ReturnBallistic:
            case BasicHookEvidencePhase.ReturnFeatherFall:
                return index == route.Evidence.SuccessTickCount - 1
                    ? BasicHookRescueStep.ReenteredLowConfigLoop
                    : BasicHookRescueStep.FollowCertifiedReturn;
            default:
                throw new InvalidOperationException("unexpected successful phase " + tick.Phase);
            }
        }

        private static ControlPlan SeedPlan()
        {
            return new ControlPlan
            {
                Horizontal = -1,
                Jump = true,
                JumpAction = JumpAction.Cloud,
                Drop = true,
                Fire = true,
                QuickHeal = true,
                QuickMana = true,
                Dash = true,
                Hook = true,
                ToggleMount = true,
                GravityControl = -1,
                FeatherFallUp = true,
                AimWorld = new Vec2(1f, 2f),
                HookWorld = new Vec2(3f, 4f),
                TargetKey = 5,
                RiskScore = 6f,
                PreferredWeaponSlot = 7,
                TacticalMode = TacticalMode.EmergencyEvade,
                StrategyId = "seed-strategy",
                PhaseId = "seed-phase",
                WeaponIssue = "seed-weapon",
                RequestControlReturn = true,
                ControlReturnReason = "seed-return"
            };
        }

        private static void EqualPlan(in ControlPlan expected, in ControlPlan actual)
        {
            Equal(expected.Horizontal, actual.Horizontal);
            Equal(expected.Jump, actual.Jump);
            Equal(expected.JumpAction, actual.JumpAction);
            Equal(expected.Drop, actual.Drop);
            Equal(expected.Fire, actual.Fire);
            Equal(expected.QuickHeal, actual.QuickHeal);
            Equal(expected.QuickMana, actual.QuickMana);
            Equal(expected.Dash, actual.Dash);
            Equal(expected.Hook, actual.Hook);
            Equal(expected.ToggleMount, actual.ToggleMount);
            Equal(expected.GravityControl, actual.GravityControl);
            Equal(expected.FeatherFallUp, actual.FeatherFallUp);
            Near(expected.AimWorld.X, actual.AimWorld.X);
            Near(expected.AimWorld.Y, actual.AimWorld.Y);
            Near(expected.HookWorld.X, actual.HookWorld.X);
            Near(expected.HookWorld.Y, actual.HookWorld.Y);
            Equal(expected.TargetKey, actual.TargetKey);
            Near(expected.RiskScore, actual.RiskScore);
            Equal(expected.PreferredWeaponSlot, actual.PreferredWeaponSlot);
            Equal(expected.TacticalMode, actual.TacticalMode);
            Equal(expected.StrategyId, actual.StrategyId);
            Equal(expected.PhaseId, actual.PhaseId);
            Equal(expected.WeaponIssue, actual.WeaponIssue);
            Equal(expected.RequestControlReturn, actual.RequestControlReturn);
            Equal(expected.ControlReturnReason, actual.ControlReturnReason);
        }

        private static int Find(BasicHookTrajectoryTick[] ticks, int count,
            BasicHookEvidencePhase phase)
        {
            for (var index = 0; index < count; index++)
                if (ticks[index].Phase == phase) return index;
            throw new InvalidOperationException("phase not found: " + phase);
        }

        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .001f)
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }

        private static void True(bool value)
        {
            if (!value) throw new InvalidOperationException("expected true");
        }

        private static void False(bool value) => True(!value);

        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }

        private sealed class Fixture
        {
            public BasicHookIdentity Identity;
            public BasicHookUseContext Context;
            public BasicHookRouteCertificate Route;
            public BasicHookVerifiedRoute Proof;
            public int LoopEpoch;

            public static Fixture Create()
            {
                var fixture = new Fixture
                {
                    Identity = ExactIdentity(),
                    Context = ExactContext(),
                    LoopEpoch = 4
                };
                var world = new FakeWorld
                {
                    Anchor = new BasicHookAnchorObservation
                    {
                        Known = true,
                        TileX = 108,
                        TileY = 92,
                        TileType = 1,
                        NativeActive = true,
                        NativeSolid = true,
                        HookCenter = new Vec2(1736f, 1480f)
                    }
                };
                var request = new BasicHookRouteBuildRequest
                {
                    Known = true,
                    Identity = fixture.Identity,
                    Context = fixture.Context,
                    FireCenter = new Vec2(1600f, 1600f),
                    PlayerVelocity = new Vec2(0f, 0f),
                    PlayerGrounded = true,
                    PlayerWidth = 20,
                    PlayerHeight = 42,
                    Gravity = .4f,
                    MaximumFallSpeed = 10f,
                    HorizontalSlowdown = .3f,
                    JumpSpeed = 5.01f,
                    JumpHeight = 15,
                    LowConfigLoopCenter = new Vec2(1600f, 1600f),
                    LoopHalfWidth = 200f,
                    LoopHalfHeight = 200f,
                    LoopMaximumAbsVelocityX = 20f,
                    LoopMaximumAbsVelocityY = 20f,
                    LowConfigLoopEpoch = fixture.LoopEpoch,
                    MaximumAcceptedRiskScore = 0f,
                    PlayerSafetyMargin = 4f,
                    Threats = new List<ThreatSnapshot>(),
                    CandidateAnchors = new[] { world.Anchor },
                    CandidateAnchorCount = 1
                };
                var buffers = new BasicHookRouteBuffers
                {
                    SuccessTicks = new BasicHookTrajectoryTick[
                        BasicHookRouteFactory.RequiredSuccessTickCapacity],
                    MissTicks = new BasicHookTrajectoryTick[
                        BasicHookRouteFactory.RequiredMissTickCapacity],
                    SuccessTileSamples = new BasicHookTileSample[
                        BasicHookRouteFactory.RequiredTileSampleCapacity],
                    MissTileSamples = new BasicHookTileSample[
                        BasicHookRouteFactory.RequiredTileSampleCapacity]
                };
                BasicHookFailure failure;
                True(BasicHookRouteFactory.TryBuild(in request, world, in buffers,
                    out fixture.Route, out fixture.Proof, out failure));
                Equal(BasicHookFailure.None, failure);
                return fixture;
            }

            public BasicHookFrameSnapshot InitialFrame(long sequence)
            {
                return new BasicHookFrameSnapshot
                {
                    Known = true,
                    Sequence = sequence,
                    Identity = Identity,
                    Context = Context,
                    ReleaseJump = true,
                    PlayerCenter = Route.FireCenter,
                    PlayerVelocity = new Vec2(0f, 0f),
                    PlayerGrounded = true,
                    CandidateKnown = true,
                    CandidateRoute = Proof,
                    CandidateAimWorld = Route.IntendedAnchorCenter,
                    Link = EmptyLink(),
                    LiveThreatFieldKnown = true,
                    CertifiedTrajectoryStillSafe = true,
                    LiveWorstCaseRiskScore = 0f,
                    LowConfigLoopEpoch = LoopEpoch
                };
            }

            public BasicHookFrameSnapshot FrameAfter(in BasicHookTrajectoryTick tick,
                long startSequence)
            {
                var frame = new BasicHookFrameSnapshot
                {
                    Known = true,
                    Sequence = startSequence + tick.Tick + 1,
                    Identity = Identity,
                    Context = Context,
                    ReleaseJump = tick.ReleaseJumpAfter,
                    PlayerCenter = tick.PlayerCenterAfter,
                    PlayerVelocity = tick.PlayerVelocityAfter,
                    PlayerGrounded = tick.Grounded,
                    Link = EmptyLink(),
                    LiveThreatFieldKnown = true,
                    CertifiedTrajectoryStillSafe = true,
                    LiveWorstCaseRiskScore = tick.RiskScore,
                    LowConfigLoopEpoch = LoopEpoch
                };
                if (!tick.ProjectileActiveAfter) return frame;
                frame.ProjectileObserved = true;
                frame.Projectile = Projectile(tick.ProjectileAiAfter,
                    tick.ProjectileCenterAfter);
                if (tick.ProjectileAiAfter == 2f)
                {
                    frame.Link = AttachedLink();
                    frame.Anchor = Route.IntendedAnchor;
                }
                return frame;
            }

            private static BasicHookIdentity ExactIdentity()
            {
                return new BasicHookIdentity
                {
                    Known = true,
                    ResolvedByQuickGrapple = true,
                    ProjectileMarkedAsHook = true,
                    ItemType = 84,
                    ProjectileType = 13,
                    ProjectileAiStyle = 7,
                    ShootSpeed = 11.5f,
                    UseStyle = 5,
                    UseAnimation = 20,
                    UseTime = 20,
                    NoUseGraphic = true,
                    NoMelee = true,
                    ProjectileWidth = 18,
                    ProjectileHeight = 18,
                    ProjectileNetImportant = true,
                    ProjectileTileCollide = false,
                    MaximumSimultaneousHooks = 1
                };
            }

            private static BasicHookUseContext ExactContext()
            {
                return new BasicHookUseContext
                {
                    Known = true,
                    LocalPlayerKnown = true,
                    LocalPlayerIndex = 0,
                    NormalGravity = true,
                    ReleaseHook = true,
                    ItemStartGateKnown = true,
                    ItemStartGateOpen = true
                };
            }

            private static BasicHookProjectileObservation Projectile(float ai, Vec2 center)
            {
                return new BasicHookProjectileObservation
                {
                    Known = true,
                    Active = true,
                    Index = 1,
                    Owner = 0,
                    Type = BasicHookMotion.ProjectileType,
                    AiStyle = BasicHookMotion.ProjectileAiStyle,
                    AiState = ai,
                    Center = center
                };
            }

            private static BasicHookLinkObservation AttachedLink()
            {
                return new BasicHookLinkObservation
                {
                    Known = true,
                    AtGrappleMovementEntry = true,
                    GrappleCount = 1,
                    FirstProjectileIndex = 1
                };
            }

            private static BasicHookLinkObservation EmptyLink()
            {
                return new BasicHookLinkObservation
                {
                    Known = true,
                    AtGrappleMovementEntry = true,
                    GrappleCount = 0,
                    FirstProjectileIndex = -1
                };
            }
        }

        private sealed class FakeWorld : IBasicHookWorldEvidenceSource
        {
            public BasicHookAnchorObservation Anchor;
            public int MaxTilesX => 1000;
            public int MaxTilesY => 1000;

            public bool TryReadTile(int x, int y, out BasicHookAnchorObservation tile)
            {
                tile = new BasicHookAnchorObservation
                {
                    Known = true,
                    TileX = x,
                    TileY = y,
                    TileType = 0,
                    HookCenter = new Vec2(x * 16f + 8f, y * 16f + 8f)
                };
                if (x == Anchor.TileX && y == Anchor.TileY) tile = Anchor;
                return true;
            }

            public bool TrySweepPlayer(in RectF before, in RectF after,
                out bool pathClear, out bool platformFree)
            {
                pathClear = true;
                platformFree = true;
                return true;
            }
        }
    }
}
