using System;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunFlightMotionRegressions()
        {
            Run(nameof(DemonGroundLaunchConvertsRocketAfterJump), DemonGroundLaunchConvertsRocketAfterJump);
            Run(nameof(DemonFinalHeldTickStartsWingsImmediately), DemonFinalHeldTickStartsWingsImmediately);
            Run(nameof(DemonThrustUsesPostSubtractionBranch), DemonThrustUsesPostSubtractionBranch);
            Run(nameof(DemonLastFuelTickSkipsGravity), DemonLastFuelTickSkipsGravity);
            Run(nameof(DemonEmptyWingsConvertFuelWithoutRetroactivePower), DemonEmptyWingsConvertFuelWithoutRetroactivePower);
            Run(nameof(DemonReleaseKeepsInertiaAndResource), DemonReleaseKeepsInertiaAndResource);
            Run(nameof(DemonEmptyHeldWingsGlideWithoutConsumingFuel), DemonEmptyHeldWingsGlideWithoutConsumingFuel);
            Run(nameof(DemonPoweredWingPrecedesFeatherFall), DemonPoweredWingPrecedesFeatherFall);
            Run(nameof(DemonFeatherFallPreemptsUnpoweredGlideAndPreservesResources), DemonFeatherFallPreemptsUnpoweredGlideAndPreservesResources);
            Run(nameof(DemonFeatherFallUpUsesTenthGravity), DemonFeatherFallUpUsesTenthGravity);
            Run(nameof(DemonFeatherFallDownRestoresWingGlide), DemonFeatherFallDownRestoresWingGlide);
            Run(nameof(DemonFeatherFallInvalidPhysicsFailsBeforeResourceMutation), DemonFeatherFallInvalidPhysicsFailsBeforeResourceMutation);
            Run(nameof(DemonLandingNeedsReleaseToRefillWings), DemonLandingNeedsReleaseToRefillWings);
            Run(nameof(DemonCloudRepressDefersWingPower), DemonCloudRepressDefersWingPower);
            Run(nameof(DemonJustJumpedDoesNotLeakIntoNextTick), DemonJustJumpedDoesNotLeakIntoNextTick);
            Run(nameof(DemonResourcesAreNotMaxOfUnrelatedCounters), DemonResourcesAreNotMaxOfUnrelatedCounters);
            Run(nameof(DemonUnknownProfileDoesNotChangeState), DemonUnknownProfileDoesNotChangeState);
            Run(nameof(DemonPlannerUsesPoweredTickWithoutGravity), DemonPlannerUsesPoweredTickWithoutGravity);
            Run(nameof(DemonCandidateTrajectoryUsesFeatherFallDownBypass), DemonCandidateTrajectoryUsesFeatherFallDownBypass);
            Run(nameof(DemonCandidateTrajectoryUsesFeatherFallUpBranch), DemonCandidateTrajectoryUsesFeatherFallUpBranch);
            Run(nameof(FeatherFallUpRequiresExactPotionIdentity), FeatherFallUpRequiresExactPotionIdentity);
            Run(nameof(FeatherFallUpRejectsEveryConflictingInputMeaning), FeatherFallUpRejectsEveryConflictingInputMeaning);
            Run(nameof(DemonPlannerMatchesUncachedReference), DemonPlannerMatchesUncachedReference);
        }

        private static FlightSnapshot DemonFlight(bool boots = true)
            => new FlightSnapshot { Known = true, WingsLogic = 1, RocketBoots = boots ? 2 : 0,
                WingTime = 100f, WingTimeMax = 100, RocketTime = 7, RocketTimeMax = 7,
                CanRocket = true, RocketRelease = true };

        private static FlightPhase FlightTick(ref FlightSnapshot flight, ref JumpSnapshot jump,
            ref float vy, bool held, bool down = false, bool up = false)
        {
            FlightMotion.RefreshBeforeMovement(ref flight, ref jump, vy);
            FlightMotion.ApplyJump(ref flight, ref jump, ref vy, held);
            return FlightMotion.ApplyAfterJump(ref flight, in jump, ref vy, held, up, down, .4f, 10f);
        }

        private static void DemonGroundLaunchConvertsRocketAfterJump()
        {
            var flight = DemonFlight(); var jump = OrdinaryJump(); var vy = 0f;
            Equal(FlightPhase.JumpHold, FlightTick(ref flight, ref jump, ref vy, true));
            Equal(15, jump.RemainingTicks); Equal(142f, flight.WingTime); Equal(0, flight.RocketTime);
            True(flight.JustJumped); False(flight.CanRocket); False(flight.RocketRelease);
            MotionNear(-4.61f, vy);
        }

        private static void DemonFinalHeldTickStartsWingsImmediately()
        {
            var flight = DemonFlight(false); var jump = OrdinaryJump(); var vy = -4.61f;
            jump.RemainingTicks = 1; jump.ReleaseReady = false;
            Equal(FlightPhase.WingPowered, FlightTick(ref flight, ref jump, ref vy, true));
            Equal(0, jump.RemainingTicks); Equal(99f, flight.WingTime); MotionNear(-5.11f, vy);
        }

        private static void DemonThrustUsesPostSubtractionBranch()
        {
            MotionNear(-.15f, FlightMotion.DemonThrust(.05f, 5.01f));
            MotionNear(-.45f, FlightMotion.DemonThrust(.15f, 5.01f));
            MotionNear(-2.55f, FlightMotion.DemonThrust(-2.45f, 5.01f));
            MotionNear(-7.515f, FlightMotion.DemonThrust(-8f, 5.01f));
        }

        private static void DemonLastFuelTickSkipsGravity()
        {
            var flight = DemonFlight(false); flight.WingTime = 1f;
            var jump = OrdinaryJump(); var vy = -5f;
            Equal(FlightPhase.WingPowered, FlightTick(ref flight, ref jump, ref vy, true));
            Equal(0f, flight.WingTime); MotionNear(-5.1f, vy);
            Equal(FlightPhase.Ballistic, FlightTick(ref flight, ref jump, ref vy, true));
            MotionNear(-4.7f, vy);
        }

        private static void DemonEmptyWingsConvertFuelWithoutRetroactivePower()
        {
            var flight = DemonFlight(); flight.WingTime = 0f;
            var jump = OrdinaryJump(); var vy = 2f;
            Equal(FlightPhase.Glide, FlightTick(ref flight, ref jump, ref vy, true));
            Equal(42f, flight.WingTime); Equal(0, flight.RocketTime); MotionNear(2f + .4f / 3f, vy);
        }

        private static void DemonReleaseKeepsInertiaAndResource()
        {
            var flight = DemonFlight(); flight.WingTime = 62f; flight.RocketTime = 0;
            var jump = OrdinaryJump(); jump.RemainingTicks = 3; var vy = -7f;
            Equal(FlightPhase.Ballistic, FlightTick(ref flight, ref jump, ref vy, false));
            MotionNear(-6.6f, vy); Equal(62f, flight.WingTime); True(flight.RocketRelease); Equal(0, jump.RemainingTicks);
        }

        private static void DemonEmptyHeldWingsGlideWithoutConsumingFuel()
        {
            var flight = DemonFlight(false); flight.WingTime = 0f;
            var jump = OrdinaryJump(); var vy = 9f;
            Equal(FlightPhase.Glide, FlightTick(ref flight, ref jump, ref vy, true));
            MotionNear(10f / 3f, vy); Equal(0f, flight.WingTime);
            vy = 9f;
            Equal(FlightPhase.Glide, FlightTick(ref flight, ref jump, ref vy, true, true));
            MotionNear(9f + .4f / 3f, vy);
            vy = 9f;
            Equal(FlightPhase.Ballistic, FlightTick(ref flight, ref jump, ref vy, false));
            MotionNear(9.4f, vy);
        }

        private static void DemonPoweredWingPrecedesFeatherFall()
        {
            var flight = DemonFlight(false); flight.RocketTime = 0;
            var jump = OrdinaryJump(); jump.ReleaseReady = false; jump.SlowFall = true;
            var vy = 2f;
            Equal(FlightPhase.WingPowered, FlightTick(ref flight, ref jump, ref vy, true));
            MotionNear(1.4f, vy);
            Equal(99f, flight.WingTime);
            Equal(0, flight.RocketTime);
        }

        private static void DemonFeatherFallPreemptsUnpoweredGlideAndPreservesResources()
        {
            var flight = DemonFlight(false); flight.WingTime = 0f; flight.RocketTime = 0;
            var jump = OrdinaryJump(); jump.ReleaseReady = false; jump.SlowFall = true;
            var vy = 2f;
            Equal(FlightPhase.FeatherFall, FlightTick(ref flight, ref jump, ref vy, true));
            MotionNear(2f + .4f / 3f, vy);
            Equal(0f, flight.WingTime);
            Equal(0, flight.RocketTime);
            Equal(0, flight.RocketDelay);
            True(flight.CanRocket && flight.RocketRelease);

            flight = DemonFlight(false); flight.WingTime = 62f; flight.RocketTime = 0;
            jump = OrdinaryJump(); jump.SlowFall = true;
            vy = 2f;
            Equal(FlightPhase.FeatherFall, FlightTick(ref flight, ref jump, ref vy, false));
            Equal(62f, flight.WingTime);
            Equal(0, flight.RocketTime);
        }

        private static void DemonFeatherFallUpUsesTenthGravity()
        {
            var flight = DemonFlight(false); flight.WingTime = 0f; flight.RocketTime = 0;
            var jump = OrdinaryJump(); jump.ReleaseReady = false; jump.SlowFall = true;
            var vy = .5f;
            Equal(FlightPhase.FeatherFall, FlightTick(ref flight, ref jump, ref vy, true, false, true));
            MotionNear(.54f, vy);
            Equal(0f, flight.WingTime);
            Equal(0, flight.RocketTime);
        }

        private static void DemonFeatherFallDownRestoresWingGlide()
        {
            var flight = DemonFlight(false); flight.WingTime = 0f;
            var jump = OrdinaryJump(); jump.ReleaseReady = false; jump.SlowFall = true;
            var vy = 9f;
            Equal(FlightPhase.Glide, FlightTick(ref flight, ref jump, ref vy, true, true));
            MotionNear(9f + .4f / 3f, vy);
            Equal(0f, flight.WingTime);
        }

        private static void DemonFeatherFallInvalidPhysicsFailsBeforeResourceMutation()
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var flight = DemonFlight();
                var jump = OrdinaryJump(); jump.SlowFall = true;
                var vy = 2f;
                Equal(FlightPhase.Unsupported, FlightMotion.ApplyAfterJump(
                    ref flight, in jump, ref vy, true, true, false, invalid, 10f));
                MotionNear(2f, vy);
                Equal(100f, flight.WingTime);
                Equal(7, flight.RocketTime);
                Equal(0, flight.RocketDelay);

                Equal(FlightPhase.Unsupported, FlightMotion.ApplyAfterJump(
                    ref flight, in jump, ref vy, true, false, false, .4f, invalid));
                MotionNear(2f, vy);
                Equal(100f, flight.WingTime);
                Equal(7, flight.RocketTime);
            }
        }

        private static void DemonLandingNeedsReleaseToRefillWings()
        {
            var flight = DemonFlight(); flight.WingTime = 0f; flight.RocketTime = 0;
            flight.CanRocket = false; flight.RocketRelease = false;
            var jump = OrdinaryJump(); jump.ReleaseReady = false; var vy = 0f;
            FlightTick(ref flight, ref jump, ref vy, true);
            Equal(0f, flight.WingTime); Equal(7, flight.RocketTime);
            vy = 0f; FlightTick(ref flight, ref jump, ref vy, false);
            Equal(100f, flight.WingTime); Equal(7, flight.RocketTime); True(jump.ReleaseReady);
            var noBoots = DemonFlight(false); var noJump = OrdinaryJump(); vy = 0f;
            FlightTick(ref noBoots, ref noJump, ref vy, false);
            Equal(7, noBoots.RocketTime); Equal(100f, FlightMotion.RemainingWingTicks(in noBoots));
        }

        private static void DemonCloudRepressDefersWingPower()
        {
            var flight = DemonFlight(false); var jump = OrdinaryJump(true); var vy = -4f;
            jump.ReleaseReady = false;
            FlightTick(ref flight, ref jump, ref vy, false);
            Equal(FlightPhase.JumpHold, FlightTick(ref flight, ref jump, ref vy, true));
            Equal(11, jump.RemainingTicks); Equal(100f, flight.WingTime);
            False(jump.CloudAvailable); False(flight.JustJumped); False(flight.RocketRelease);
            MotionNear(-4.61f, vy);
        }

        private static void DemonJustJumpedDoesNotLeakIntoNextTick()
        {
            var flight = DemonFlight(); var jump = OrdinaryJump(); jump.AutoJump = true; var vy = 0f;
            FlightTick(ref flight, ref jump, ref vy, true); True(flight.JustJumped);
            flight.WingTime = 30f; flight.RocketTime = 0;
            FlightTick(ref flight, ref jump, ref vy, true); False(flight.JustJumped); Equal(30f, flight.WingTime);
        }

        private static void DemonResourcesAreNotMaxOfUnrelatedCounters()
        {
            var flight = DemonFlight(); Equal(142f, FlightMotion.RemainingWingTicks(in flight));
            Equal(1f, FlightMotion.ResourceFraction(in flight));
            flight.WingTime = 142f; flight.RocketTime = 0;
            Equal(142f, FlightMotion.RemainingWingTicks(in flight)); Equal(1f, FlightMotion.ResourceFraction(in flight));
            flight.WingTime = 71f; flight.RocketTime = 0;
            Equal(.5f, FlightMotion.ResourceFraction(in flight));
            flight.RocketBoots = 0;
            MotionNear(.71f, FlightMotion.ResourceFraction(in flight));
        }

        private static void DemonUnknownProfileDoesNotChangeState()
        {
            var flight = DemonFlight(); flight.Known = false;
            var jump = OrdinaryJump(); var vy = 3f;
            Equal(FlightPhase.Unsupported, FlightTick(ref flight, ref jump, ref vy, true));
            Equal(3f, vy); Equal(100f, flight.WingTime); Equal(7, flight.RocketTime); Equal(0, jump.RemainingTicks);
        }

        private static void DemonPlannerUsesPoweredTickWithoutGravity()
        {
            var scene = CombatScenario(4); scene.Player.Jump = OrdinaryJump();
            scene.Player.Flight = DemonFlight(false); scene.Player.Gravity = .4f; scene.Player.MaxFallSpeed = 10f;
            scene.Player.Velocity = new Vec2(0f, -5f);
            var method = typeof(CombatPlanner).GetMethod("AdvanceNativeJump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] args = { scene, 0, 1, JumpAction.Hold, 0f, default(SupportSpan),
                scene.Player.Jump, scene.Player.Flight, scene.Player.Position, scene.Player.Velocity,
                false, default(SupportSpan), scene.Player.Position.Y };
            method.Invoke(null, args);
            MotionNear(-5.1f, ((Vec2)args[9]).Y); Equal(99f, ((FlightSnapshot)args[7]).WingTime);
        }

        private static void DemonCandidateTrajectoryUsesFeatherFallDownBypass()
        {
            var scene = CombatScenario(4);
            scene.Player.Jump = OrdinaryJump();
            scene.Player.Jump.SlowFall = true;
            scene.Player.Jump.ReleaseReady = false;
            scene.Player.Flight = DemonFlight(false);
            scene.Player.Flight.WingTime = 0f;
            scene.Player.Gravity = .4f;
            scene.Player.MaxFallSpeed = 10f;
            scene.Player.OnGround = false;
            scene.Player.Velocity = new Vec2(0f, 2f);
            var method = typeof(CombatPlanner).GetMethod("AdvanceNativeJump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            True(method != null);

            object[] feather = { scene, 0, 0, JumpAction.Default, 0f, default(SupportSpan),
                scene.Player.Jump, scene.Player.Flight, scene.Player.Position, scene.Player.Velocity,
                false, default(SupportSpan), scene.Player.Position.Y };
            method.Invoke(null, feather);
            MotionNear(2f + .4f / 3f, ((Vec2)feather[9]).Y);

            object[] down = { scene, 0, -1, JumpAction.Default, 0f, default(SupportSpan),
                scene.Player.Jump, scene.Player.Flight, scene.Player.Position, scene.Player.Velocity,
                false, default(SupportSpan), scene.Player.Position.Y };
            method.Invoke(null, down);
            MotionNear(2.4f, ((Vec2)down[9]).Y);
            True(((Vec2)down[8]).Y > ((Vec2)feather[8]).Y);
        }

        private static void DemonCandidateTrajectoryUsesFeatherFallUpBranch()
        {
            var scene = CombatScenario(4);
            scene.Player.Jump = OrdinaryJump();
            scene.Player.Jump.SlowFall = true;
            scene.Player.Jump.ReleaseReady = false;
            scene.Player.Flight = DemonFlight(false);
            scene.Player.Flight.WingTime = 0f;
            scene.Player.Gravity = .4f;
            scene.Player.MaxFallSpeed = 10f;
            scene.Player.OnGround = false;
            scene.Player.Velocity = new Vec2(0f, 2f);
            scene.Mobility.FeatherFall = true;
            var method = typeof(CombatPlanner).GetMethod("AdvanceNativeJump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            True(method != null);

            object[] ordinary = { scene, 0, 0, JumpAction.Default, 0f, default(SupportSpan),
                scene.Player.Jump, scene.Player.Flight, scene.Player.Position, scene.Player.Velocity,
                false, default(SupportSpan), scene.Player.Position.Y };
            method.Invoke(null, ordinary);
            object[] up = { scene, 0, 2, JumpAction.Default, 0f, default(SupportSpan),
                scene.Player.Jump, scene.Player.Flight, scene.Player.Position, scene.Player.Velocity,
                false, default(SupportSpan), scene.Player.Position.Y };
            method.Invoke(null, up);

            MotionNear(2f + .4f / 3f, ((Vec2)ordinary[9]).Y);
            MotionNear(1f, ((Vec2)up[9]).Y); // Crosses max/5 and snaps to max/10.
            True(((Vec2)up[8]).Y < ((Vec2)ordinary[8]).Y);
        }

        private static void FeatherFallUpRequiresExactPotionIdentity()
        {
            var scene = CombatScenario(4);
            var jump = scene.Player.Jump;
            jump.Known = true;
            jump.SlowFall = true;
            scene.Player.Jump = jump;
            scene.Mobility.FeatherFall = true;
            scene.Mobility.GravityFlip = new GravityFlipState { Known = true };

            // Aggregate slowFall alone can be Djinn's Curse or another native
            // source.  It remains valid for passive gravity prediction but may
            // never cause Chaite to hold Up.
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.FeatherFallPotionKnown = true;
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.FeatherFallPotionActive = true;
            True(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
        }

        private static void FeatherFallUpRejectsEveryConflictingInputMeaning()
        {
            var scene = CombatScenario(4);
            var jump = scene.Player.Jump;
            jump.Known = true;
            jump.SlowFall = true;
            scene.Player.Jump = jump;
            scene.Mobility.FeatherFall = true;
            scene.Mobility.FeatherFallPotionKnown = true;
            scene.Mobility.FeatherFallPotionActive = true;
            scene.Mobility.GravityFlip = new GravityFlipState { Known = true };
            True(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));

            scene.Mobility.GravityFlip = default(GravityFlipState);
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.GravityFlip = new GravityFlipState { Known = true,
                GravControl = true };
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.GravityFlip = new GravityFlipState { Known = true,
                GravControl2 = true };
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.GravityFlip = new GravityFlipState { Known = true,
                ForcedGravity = 1 };
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.GravityFlip = new GravityFlipState { Known = true };
            scene.Mobility.MountActive = true;
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.MountActive = false;
            scene.Mobility.Grappling = true;
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
            scene.Mobility.Grappling = false;
            scene.Mobility.GravityInverted = true;
            False(FlightMotion.CanRequestFeatherFallPotionUp(scene.Player,
                scene.Mobility));
        }

        private static void DemonPlannerMatchesUncachedReference()
        {
            for (var index = 0; index < 20; index++)
            {
                var scene = CombatScenario(4); scene.Player.Jump = OrdinaryJump(index % 2 == 0);
                scene.Player.Flight = DemonFlight(); scene.Player.Flight.WingTime = index * 5f;
                scene.Player.Flight.RocketTime = index % 8; scene.Player.OnGround = false;
                scene.Player.Velocity.Y = index - 9f; scene.Player.Gravity = .4f; scene.Player.MaxFallSpeed = 10f;
                var fast = new CombatPlanner(new PlannerSettings());
                var reference = new CombatPlanner(new PlannerSettings { CacheThreatPrediction = false, EnableScorePruning = false });
                for (var tick = 0; tick < 8; tick++)
                {
                    var actual = fast.Plan(scene); var expected = reference.Plan(scene);
                    Equal(expected.Horizontal, actual.Horizontal); Equal(expected.Jump, actual.Jump);
                    Equal(expected.JumpAction, actual.JumpAction); Equal(expected.Drop, actual.Drop);
                    MotionNear(expected.RiskScore, actual.RiskScore);
                }
            }
        }
    }
}
