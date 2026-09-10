using System;
using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunJumpMotionRegressions()
        {
            Run(nameof(NativeHoldIncludesLaunchAndFifteenHeldTicks), NativeHoldIncludesLaunchAndFifteenHeldTicks);
            Run(nameof(NativeReleaseStopsHoldWithoutCuttingVelocity), NativeReleaseStopsHoldWithoutCuttingVelocity);
            Run(nameof(NativeCloudRequiresReleaseAndConsumesOneCharge), NativeCloudRequiresReleaseAndConsumesOneCharge);
            Run(nameof(NativeCloudHasTruncatedThreeQuarterHold), NativeCloudHasTruncatedThreeQuarterHold);
            Run(nameof(NativeZeroVelocityBranchClearsOldHold), NativeZeroVelocityBranchClearsOldHold);
            Run(nameof(NativeGroundLaunchRefreshesCloud), NativeGroundLaunchRefreshesCloud);
            Run(nameof(NativeLandedReleaseRefreshesCloudBeforeJumpMovement), NativeLandedReleaseRefreshesCloudBeforeJumpMovement);
            Run(nameof(NativeJumpMirrorsGravityWithoutClampingAscent), NativeJumpMirrorsGravityWithoutClampingAscent);
            Run(nameof(NativeLowAndZeroGravityAreNotInflated), NativeLowAndZeroGravityAreNotInflated);
            Run(nameof(CloudControlPulseRequiresObservedResource), CloudControlPulseRequiresObservedResource);
            Run(nameof(CloudControlDoesNotInterruptGrappleRelease), CloudControlDoesNotInterruptGrappleRelease);
            Run(nameof(JumpInputsHonorReleaseAndAutoJump), JumpInputsHonorReleaseAndAutoJump);
            Run(nameof(UnknownNativeJumpDoesNotInventState), UnknownNativeJumpDoesNotInventState);
            Run(nameof(NativeJumpPlannerMatchesUncachedReference), NativeJumpPlannerMatchesUncachedReference);
            Run(nameof(NativeLaunchKeepsGroundedHorizontalAcceleration), NativeLaunchKeepsGroundedHorizontalAcceleration);
            Run(nameof(NativeLandingRetainsDisplacementUntilNextTick), NativeLandingRetainsDisplacementUntilNextTick);
        }

        private static JumpSnapshot OrdinaryJump(bool cloud = false)
        {
            return new JumpSnapshot { Known = true, Speed = 5.01f, Height = 15, ReleaseReady = true,
                CloudEnabled = cloud, CloudAvailable = cloud };
        }

        private static void NativeHoldIncludesLaunchAndFifteenHeldTicks()
        {
            var state = OrdinaryJump();
            var vy = 0f;
            for (var tick = 0; tick < 16; tick++)
            {
                JumpMotion.ApplyJump(ref state, ref vy, true, false);
                Equal(15 - tick, state.RemainingTicks);
                MotionNear(-5.01f, vy);
                vy = JumpMotion.ApplyGravity(vy, .4f, 10.01f, false);
                MotionNear(-4.61f, vy);
                False(state.ReleaseReady);
            }
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            MotionNear(-4.61f, vy);
        }

        private static void NativeReleaseStopsHoldWithoutCuttingVelocity()
        {
            var state = OrdinaryJump();
            state.RemainingTicks = 10;
            state.ReleaseReady = false;
            var vy = -4.61f;
            JumpMotion.ApplyJump(ref state, ref vy, false, false);
            Equal(0, state.RemainingTicks);
            True(state.ReleaseReady);
            MotionNear(-4.61f, vy);
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            MotionNear(-4.61f, vy); // No cloud: repress cannot invent another jump.
        }

        private static void NativeCloudRequiresReleaseAndConsumesOneCharge()
        {
            var state = OrdinaryJump(true);
            state.ReleaseReady = false;
            var vy = 3f;
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            MotionNear(3f, vy);
            True(state.CloudAvailable);
            JumpMotion.ApplyJump(ref state, ref vy, false, false);
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            MotionNear(-5.01f, vy);
            Equal(11, state.RemainingTicks);
            False(state.CloudAvailable);
            JumpMotion.ApplyJump(ref state, ref vy, false, false);
            vy = 2f;
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            MotionNear(2f, vy);
        }

        private static void NativeCloudHasTruncatedThreeQuarterHold()
        {
            var state = OrdinaryJump(true);
            var vy = 1f;
            for (var tick = 0; tick < 12; tick++)
            {
                JumpMotion.ApplyJump(ref state, ref vy, true, false);
                Equal(11 - tick, state.RemainingTicks);
                MotionNear(-5.01f, vy);
            }
        }

        private static void NativeZeroVelocityBranchClearsOldHold()
        {
            var state = OrdinaryJump(true);
            state.RemainingTicks = 8;
            var vy = 0f;
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            Equal(0, state.RemainingTicks);
            MotionNear(0f, vy);
        }

        private static void NativeGroundLaunchRefreshesCloud()
        {
            var state = OrdinaryJump(true);
            state.CloudAvailable = false;
            var vy = 0f;
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            True(state.CloudAvailable);
            Equal(15, state.RemainingTicks);
        }

        private static void NativeLandedReleaseRefreshesCloudBeforeJumpMovement()
        {
            var state = OrdinaryJump(true);
            state.CloudAvailable = false;
            var vy = .000001f;
            JumpMotion.RefreshBeforeMovement(ref state, vy);
            False(state.CloudAvailable);
            vy = 0f;
            JumpMotion.RefreshBeforeMovement(ref state, vy);
            JumpMotion.ApplyJump(ref state, ref vy, false, false);
            True(state.CloudAvailable);
            True(state.ReleaseReady);
        }

        private static void NativeJumpMirrorsGravityWithoutClampingAscent()
        {
            var normal = OrdinaryJump();
            var inverted = normal;
            var down = 0f;
            var up = 0f;
            for (var tick = 0; tick < 24; tick++)
            {
                JumpMotion.ApplyJump(ref normal, ref down, tick < 20, false);
                JumpMotion.ApplyJump(ref inverted, ref up, tick < 20, true);
                down = JumpMotion.ApplyGravity(down, .4f, 2f, false);
                up = JumpMotion.ApplyGravity(up, .4f, 2f, true);
                MotionNear(-down, up);
                if (tick < 16) True(down < -2f);
            }
            MotionNear(2f, JumpMotion.ApplyGravity(12f, .4f, 2f, false));
            MotionNear(-2f, JumpMotion.ApplyGravity(-12f, .4f, 2f, true));
        }

        private static void NativeLowAndZeroGravityAreNotInflated()
        {
            MotionNear(-5f, JumpMotion.ApplyGravity(-5f, 0f, 10f, false));
            MotionNear(-4.96f, JumpMotion.ApplyGravity(-5f, .04f, 10f, false));
        }

        private static void CloudControlPulseRequiresObservedResource()
        {
            var state = OrdinaryJump(true);
            state.ReleaseReady = false;
            False(JumpMotion.ResolveControl(true, JumpAction.Cloud, in state, false, false));
            state.ReleaseReady = true;
            True(JumpMotion.ResolveControl(true, JumpAction.Cloud, in state, false, false));
            state.CloudAvailable = false;
            state.ReleaseReady = false;
            True(JumpMotion.ResolveControl(true, JumpAction.Cloud, in state, false, false));
            state.Known = false;
            state.CloudAvailable = true;
            True(JumpMotion.ResolveControl(true, JumpAction.Cloud, in state, false, false));
        }

        private static void CloudControlDoesNotInterruptGrappleRelease()
        {
            var state = OrdinaryJump(true);
            state.ReleaseReady = false;
            True(JumpMotion.ResolveControl(true, JumpAction.Cloud, in state, false, true));
            False(JumpMotion.ResolveControl(false, JumpAction.Default, in state, false, true));
        }

        private static void JumpInputsHonorReleaseAndAutoJump()
        {
            var state = OrdinaryJump();
            False(JumpMotion.ResolveControl(true, JumpAction.Release, in state, false, false));
            True(JumpMotion.ResolveControl(false, JumpAction.Hold, in state, true, false));
            state.ReleaseReady = false;
            False(JumpMotion.ResolveControl(true, JumpAction.Default, in state, true, false));
            state.AutoJump = true;
            True(JumpMotion.ResolveControl(true, JumpAction.Default, in state, true, false));
        }

        private static void UnknownNativeJumpDoesNotInventState()
        {
            var state = default(JumpSnapshot);
            var vy = 3f;
            JumpMotion.ApplyJump(ref state, ref vy, true, false);
            MotionNear(3f, vy);
            Equal(0, state.RemainingTicks);
        }

        private static void NativeJumpPlannerMatchesUncachedReference()
        {
            for (var sceneIndex = 0; sceneIndex < 18; sceneIndex++)
            {
                var scene = CombatScenario(4);
                scene.Player.Jump = OrdinaryJump(sceneIndex % 2 == 0);
                scene.Player.Jump.RemainingTicks = sceneIndex % 4;
                scene.Player.Jump.ReleaseReady = sceneIndex % 3 == 0;
                scene.Player.Velocity.Y = sceneIndex - 9f;
                scene.Player.OnGround = sceneIndex == 9;
                scene.Player.Gravity = .4f;
                scene.Player.MaxFallSpeed = 10.01f;
                var optimized = new CombatPlanner(new PlannerSettings());
                var reference = new CombatPlanner(new PlannerSettings { CacheThreatPrediction = false, EnableScorePruning = false });
                for (var tick = 0; tick < 12; tick++)
                {
                    var actual = optimized.Plan(scene);
                    var expected = reference.Plan(scene);
                    Equal(expected.Horizontal, actual.Horizontal);
                    Equal(expected.Jump, actual.Jump);
                    Equal(expected.JumpAction, actual.JumpAction);
                    Equal(expected.Drop, actual.Drop);
                    MotionNear(expected.RiskScore, actual.RiskScore);
                }
            }
        }

        private static void NativeLaunchKeepsGroundedHorizontalAcceleration()
        {
            var scene = CombatScenario(4);
            scene.Player.BaseRunSpeed = 3f;
            scene.Player.MaxRunSpeed = 6f;
            scene.Player.SprintAcceleration = .016f;
            scene.Player.RunSlowdown = .2f;
            scene.Player.CanSprintInAir = false;
            scene.Player.Gravity = .4f;
            scene.Player.MaxFallSpeed = 10.01f;
            scene.Player.Velocity = new Vec2(4.5f, 0f);
            scene.Player.Jump = OrdinaryJump(true);
            var floor = new SupportSpan { Valid = true, Left = scene.Player.Position.X - 500f,
                Right = scene.Player.Position.X + 500f, SurfaceY = scene.Player.Position.Y + scene.Player.Height };
            var method = typeof(CombatPlanner).GetMethod("AdvanceNativeJump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            True(method != null);
            object[] arguments = { scene, 1, 1, JumpAction.Hold, 0f, floor, scene.Player.Jump,
                scene.Player.Position, scene.Player.Velocity, true, floor, scene.Player.Position.Y };
            method.Invoke(null, arguments);
            var velocity = (Vec2)arguments[8];
            MotionNear(4.516f, velocity.X);
            MotionNear(-4.61f, velocity.Y);
            False((bool)arguments[9]);
            Equal(15, ((JumpSnapshot)arguments[6]).RemainingTicks);
        }

        private static void NativeLandingRetainsDisplacementUntilNextTick()
        {
            var scene = CombatScenario(4);
            scene.Player.Gravity = .4f;
            scene.Player.MaxFallSpeed = 10.01f;
            scene.Player.Position = new Vec2(1500f, 7956.513184f);
            scene.Player.Velocity = new Vec2(0f, 8.590001f);
            scene.Player.Jump = OrdinaryJump(true);
            scene.Player.Jump.ReleaseReady = false;
            scene.Player.Jump.CloudAvailable = false;
            var floor = new SupportSpan { Valid = true, Left = 0f, Right = 3000f, SurfaceY = 8000f };
            var method = typeof(CombatPlanner).GetMethod("AdvanceNativeJump",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            object[] arguments = { scene, 0, 1, JumpAction.Hold, 0f, floor, scene.Player.Jump,
                scene.Player.Position, scene.Player.Velocity, false, floor, scene.Player.Position.Y };
            method.Invoke(null, arguments);
            Equal(7958f, ((Vec2)arguments[7]).Y);
            MotionNear(1.486816f, ((Vec2)arguments[8]).Y);
            False((bool)arguments[9]);
            False(((JumpSnapshot)arguments[6]).CloudAvailable);
            method.Invoke(null, arguments);
            Equal(0f, ((Vec2)arguments[8]).Y);
            True((bool)arguments[9]);
            Equal(0, ((JumpSnapshot)arguments[6]).RemainingTicks);
            method.Invoke(null, arguments); // Grounded release gate, no early autojump.
            Equal(0f, ((Vec2)arguments[8]).Y);
            True(((JumpSnapshot)arguments[6]).ReleaseReady);
            True(((JumpSnapshot)arguments[6]).CloudAvailable);
            method.Invoke(null, arguments);
            Equal(15, ((JumpSnapshot)arguments[6]).RemainingTicks);
            MotionNear(-4.61f, ((Vec2)arguments[8]).Y);
        }
    }
}
