using Chaite.Core;
using System;

namespace Chaite.Tests
{
    /// <summary>
    /// Standalone regressions for the type-23 pure model. Program.cs and the
    /// legacy project include list intentionally remain untouched until the
    /// concurrent integration work is frozen.
    /// </summary>
    public static class WitchBroomMotionTests
    {
        public static int RunAll()
        {
            Action[] tests =
            {
                VerifiedIdentityIsExact,
                MountKeyRequiresReleaseEdge,
                InventoryItemAndActiveMountIdentitiesStaySeparate,
                NativeMountGatesAreConservative,
                SafeDismountRequiresSpace,
                WrongOrUnknownProfileFailsClosed,
                NonFiniteMotionFailsClosed,
                MixedMobilityActionsFailClosed,
                EveryUnmodeledInteractionFailsClosed,
                OptionalFeatherFallDoesNotChangeMountedHover,
                HorizontalAccelerationMatchesNativeOrder,
                HorizontalOppositeDirectionBrakesBeforeAccelerating,
                HorizontalAtCapUsesAirSlowdown,
                NativeThresholdsAndOverflowStayFailClosed,
                NeutralHoverConvergesToNativeSentinel,
                UpAndDownConvergeToEightPixelTargets,
                MidairMountEntryPreservesNativeVelocity,
                ExactZeroUsesNativeNonFlyingFrameAndThenFailsClosed,
                GroundUpEdgeUsesNativeBootstrap,
                GroundHeldUpWithoutEdgeIsNotGuessed,
                ClearanceMustBeProvedEveryTick,
                MultiTickCandidateCanReturnToASafeDismountState
            };
            foreach (var test in tests) test();
            return tests.Length;
        }

        private static void VerifiedIdentityIsExact()
        {
            Equal(4444, WitchBroomMotion.WitchBroomItemType);
            Equal(23, WitchBroomMotion.WitchBroomMountType);
            Equal(230, WitchBroomMotion.WitchBroomBuffType);
            Near(9f, WitchBroomMotion.RunSpeed);
            Near(.16f, WitchBroomMotion.Acceleration);
            Equal(320, WitchBroomMotion.FlightTimeMax);
            Equal(320, WitchBroomMotion.FatigueMax);
        }

        private static void MountKeyRequiresReleaseEdge()
        {
            var edge = MountEdge();
            edge.ControlMount = false;
            edge.ReleaseMount = false;
            var released = WitchBroomMotion.ResolveToggle(in edge);
            True(released.Supported);
            Equal(WitchBroomMountTransition.None, released.Transition);
            True(released.NextReleaseMount);

            edge.ControlMount = true;
            edge.ReleaseMount = false;
            var held = WitchBroomMotion.ResolveToggle(in edge);
            Equal(WitchBroomMountTransition.None, held.Transition);
            False(held.NextReleaseMount);

            edge.ReleaseMount = true;
            var pressed = WitchBroomMotion.ResolveToggle(in edge);
            Equal(WitchBroomMountTransition.Mount, pressed.Transition);
            False(pressed.NextReleaseMount);
            True(pressed.NextMountActive);
            Equal(23, pressed.NextMountType);
        }

        private static void InventoryItemAndActiveMountIdentitiesStaySeparate()
        {
            var edge = MountEdge();
            edge.QuickMountItemType = 4443;
            False(WitchBroomMotion.ResolveToggle(in edge).Supported);

            edge = MountEdge();
            edge.QuickMountType = 22;
            False(WitchBroomMotion.ResolveToggle(in edge).Supported);

            edge = MountEdge();
            edge.MountActive = true;
            edge.ActiveMountType = 22;
            False(WitchBroomMotion.ResolveToggle(in edge).Supported);

            edge.ActiveMountType = 23;
            edge.QuickMountItemType = 0;
            edge.QuickMountType = -1;
            edge.CanFitDismount = true;
            var dismounted = WitchBroomMotion.ResolveToggle(in edge);
            Equal(WitchBroomMountTransition.Dismount, dismounted.Transition);
            False(dismounted.NextMountActive);
            Equal(-1, dismounted.NextMountType);
        }

        private static void NativeMountGatesAreConservative()
        {
            Action<WitchBroomToggleSnapshot> rejected = value =>
                Equal(WitchBroomMountTransition.RejectedByNative,
                    WitchBroomMotion.ResolveToggle(in value).Transition);
            var edge = MountEdge(); edge.CrowdControlled = true; rejected(edge);
            edge = MountEdge(); edge.Tongued = true; rejected(edge);
            edge = MountEdge(); edge.Dead = true; rejected(edge);
            edge = MountEdge(); edge.NoItems = true; rejected(edge);
            edge = MountEdge(); edge.GravityInverted = true; rejected(edge);
            edge = MountEdge(); edge.Grappling = true; rejected(edge);
            edge = MountEdge(); edge.ItemUseStartEdgeReady = false; rejected(edge);
            edge = MountEdge(); edge.CanFitMount = false; rejected(edge);
        }

        private static void SafeDismountRequiresSpace()
        {
            var edge = MountEdge();
            edge.MountActive = true;
            edge.ActiveMountType = 23;
            edge.CanFitDismount = true;
            Equal(WitchBroomMountTransition.Dismount,
                WitchBroomMotion.ResolveToggle(in edge).Transition);

            edge.CanFitDismount = false;
            Equal(WitchBroomMountTransition.UnsafeDismountBlocked,
                WitchBroomMotion.ResolveToggle(in edge).Transition);

            edge.CanFitDismount = true;
            edge.Dead = true;
            Equal(WitchBroomMountTransition.RejectedByNative,
                WitchBroomMotion.ResolveToggle(in edge).Transition);
        }

        private static void WrongOrUnknownProfileFailsClosed()
        {
            var state = Air(); state.Known = false;
            False(Step(in state, default(WitchBroomMotionInput), out _));
            state = Air(); state.MountType = 7;
            False(Step(in state, default(WitchBroomMotionInput), out _));
            state = Air(); state.MountActive = false;
            False(Step(in state, default(WitchBroomMotionInput), out _));
            state = Air(); state.Dry = false;
            False(Step(in state, default(WitchBroomMotionInput), out _));
            state = Air(); state.NormalGravity = false;
            False(Step(in state, default(WitchBroomMotionInput), out _));
        }

        private static void NonFiniteMotionFailsClosed()
        {
            foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var state = Air(); state.PositionX = invalid;
                False(Step(in state, default(WitchBroomMotionInput), out _));
                state = Air(); state.PositionY = invalid;
                False(Step(in state, default(WitchBroomMotionInput), out _));
                state = Air(); state.VelocityX = invalid;
                False(Step(in state, default(WitchBroomMotionInput), out _));
                state = Air(); state.VelocityY = invalid;
                False(Step(in state, default(WitchBroomMotionInput), out _));
                state = Air(); state.Gravity = invalid;
                False(Step(in state, default(WitchBroomMotionInput), out _));
                state = Air(); state.TrackBoost = invalid;
                False(Step(in state, default(WitchBroomMotionInput), out _));
            }
        }

        private static void MixedMobilityActionsFailClosed()
        {
            Action<WitchBroomMotionInput> rejected = input =>
            {
                var state = Air();
                False(Step(in state, input, out _));
            };
            var input = new WitchBroomMotionInput { Jump = true }; rejected(input);
            input = new WitchBroomMotionInput { Hook = true }; rejected(input);
            input = new WitchBroomMotionInput { Dash = true }; rejected(input);
            input = new WitchBroomMotionInput { ToggleMount = true }; rejected(input);
            input = new WitchBroomMotionInput { FlipGravity = true }; rejected(input);
            input = new WitchBroomMotionInput { Up = true, Down = true }; rejected(input);
            input = new WitchBroomMotionInput { Horizontal = 2 }; rejected(input);

            var state2 = Air(); state2.Grappling = true;
            False(Step(in state2, default(WitchBroomMotionInput), out _));
            state2 = Air(); state2.HookInFlight = true;
            False(Step(in state2, default(WitchBroomMotionInput), out _));
            state2 = Air(); state2.DashInProgress = true;
            False(Step(in state2, default(WitchBroomMotionInput), out _));
            state2 = Air(); state2.PortalPhysicsDisabled = false;
            False(Step(in state2, default(WitchBroomMotionInput), out _));
        }

        private static void OptionalFeatherFallDoesNotChangeMountedHover()
        {
            var ordinary = Air(); ordinary.VelocityY = 2f;
            var feather = ordinary; feather.SlowFall = true;
            True(Step(in ordinary, default(WitchBroomMotionInput), out var a));
            True(Step(in feather, default(WitchBroomMotionInput), out var b));
            Near(a.Next.VelocityY, b.Next.VelocityY);
            Near(a.Next.PositionY, b.Next.PositionY);
        }

        private static void EveryUnmodeledInteractionFailsClosed()
        {
            var cases = new WitchBroomMotionSnapshot[11];
            for (var i = 0; i < cases.Length; i++) cases[i] = Air();
            cases[0].OpenDryPath = false;
            cases[1].CrowdControlled = true;
            cases[2].Tongued = true;
            cases[3].Dead = true;
            cases[4].Pulley = true;
            cases[5].Sliding = true;
            cases[6].WindPushed = true;
            cases[7].ForcedMotion = true;
            cases[8].TrackBoost = .1f;
            cases[9].FrameState = 3;
            cases[10].Gravity = -.1f;
            foreach (var state in cases)
                False(Step(in state, default(WitchBroomMotionInput), out _));
        }

        private static void HorizontalAccelerationMatchesNativeOrder()
        {
            var state = Air();
            True(Step(in state, new WitchBroomMotionInput { Horizontal = 1 }, out var right));
            Near(.16f, right.Next.VelocityX);

            state = Air(); state.VelocityX = 2f;
            True(Step(in state, new WitchBroomMotionInput { Horizontal = 1 }, out right));
            Near(2.16f, right.Next.VelocityX);

            state = Air(); state.VelocityX = -2f;
            True(Step(in state, new WitchBroomMotionInput { Horizontal = -1 }, out var left));
            Near(-2.16f, left.Next.VelocityX);
        }

        private static void HorizontalOppositeDirectionBrakesBeforeAccelerating()
        {
            var state = Air(); state.VelocityX = -2f;
            True(Step(in state, new WitchBroomMotionInput { Horizontal = 1 }, out var result));
            Near(-1.64f, result.Next.VelocityX);
            state = Air(); state.VelocityX = 2f;
            True(Step(in state, new WitchBroomMotionInput { Horizontal = -1 }, out result));
            Near(1.64f, result.Next.VelocityX);
        }

        private static void HorizontalAtCapUsesAirSlowdown()
        {
            var state = Air(); state.VelocityX = 9f; state.VelocityY = 1f;
            True(Step(in state, new WitchBroomMotionInput { Horizontal = 1 }, out var result));
            Near(8.9f, result.Next.VelocityX);
            state = Air(); state.VelocityX = .05f; state.VelocityY = 1f;
            True(Step(in state, default(WitchBroomMotionInput), out result));
            Near(0f, result.Next.VelocityX);
        }

        private static void NativeThresholdsAndOverflowStayFailClosed()
        {
            var state = Air(); state.VelocityX = 8.99f; state.VelocityY = 1f;
            True(Step(in state, new WitchBroomMotionInput { Horizontal = 1 }, out var result));
            Near(9.15f, result.Next.VelocityX);

            state = Air(); state.VelocityY = .1f;
            True(Step(in state, default(WitchBroomMotionInput), out result));
            Near(-.001f, result.Next.VelocityY);

            // The native pre-acceleration and correction cancel when already
            // beyond the commanded target; do not add a guessed hard clamp.
            state = Air(); state.VelocityY = -8.2f;
            True(Step(in state, new WitchBroomMotionInput { Up = true }, out result));
            Near(-8.2f, result.Next.VelocityY);

            state = Air(); state.PositionX = float.MaxValue; state.VelocityX = float.MaxValue;
            False(Step(in state, default(WitchBroomMotionInput), out _));

            state = Air(); state.VelocityX = 13f; state.VelocityY = 10f;
            False(Step(in state, default(WitchBroomMotionInput), out _));
        }

        private static void NeutralHoverConvergesToNativeSentinel()
        {
            var state = Air(); state.VelocityY = 2f;
            True(Step(in state, default(WitchBroomMotionInput), out var falling));
            Near(1.84f, falling.Next.VelocityY);

            state = Air(); state.VelocityY = 0f; state.PositionY = 100f;
            True(Step(in state, default(WitchBroomMotionInput), out var neutral));
            Near(-.001f, neutral.Next.VelocityY);
            Near(100f, neutral.Next.PositionY);
        }

        private static void UpAndDownConvergeToEightPixelTargets()
        {
            var state = Air(); state.VelocityY = 0f;
            True(Step(in state, new WitchBroomMotionInput { Up = true }, out var up));
            Near(-.16f, up.Next.VelocityY);
            Equal(WitchBroomMotionPhase.HoverAscent, up.Phase);

            state = Air(); state.VelocityY = 0f;
            True(Step(in state, new WitchBroomMotionInput { Down = true }, out var down));
            Near(.16f, down.Next.VelocityY);
            Equal(WitchBroomMotionPhase.HoverDescent, down.Phase);

            state = Air(); state.VelocityY = -8f;
            True(Step(in state, new WitchBroomMotionInput { Up = true }, out up));
            Near(-8f, up.Next.VelocityY);
            state = Air(); state.VelocityY = 8f;
            True(Step(in state, new WitchBroomMotionInput { Down = true }, out down));
            Near(8f, down.Next.VelocityY);
        }

        private static void GroundUpEdgeUsesNativeBootstrap()
        {
            var state = Air();
            state.FrameState = 0;
            state.VelocityY = 0f;
            state.Gravity = .4f;
            state.ReleaseUp = true;
            var input = new WitchBroomMotionInput { Up = true };
            True(Step(in state, input, out var result));
            Equal(WitchBroomMotionPhase.GroundLaunch, result.Phase);
            Near(-.561f, result.Next.VelocityY);
            False(result.Next.ReleaseUp);
            Equal(2, result.Next.FrameState);
        }

        private static void MidairMountEntryPreservesNativeVelocity()
        {
            var state = Air();
            state.FrameState = 0;
            state.VelocityY = 3f;
            True(Step(in state, new WitchBroomMotionInput { Up = true }, out var result));
            Equal(WitchBroomMotionPhase.MountEntry, result.Phase);
            Near(3f, result.Next.VelocityY);
            Equal(2, result.Next.FrameState);

            state = Air();
            state.FrameState = 0;
            state.VelocityY = WitchBroomMotion.NeutralTarget;
            True(Step(in state, default(WitchBroomMotionInput), out result));
            Near(0f, result.Next.VelocityY);
            Equal(0, result.Next.FrameState);
        }

        private static void ExactZeroUsesNativeNonFlyingFrameAndThenFailsClosed()
        {
            var state = Air();
            state.VelocityY = -.16f;
            True(Step(in state, new WitchBroomMotionInput { Down = true }, out var result));
            Near(0f, result.Next.VelocityY);
            Equal(0, result.Next.FrameState);
            var reproved = result.Next;
            reproved.OpenDryPath = true;
            False(Step(in reproved, default(WitchBroomMotionInput), out _));
        }

        private static void GroundHeldUpWithoutEdgeIsNotGuessed()
        {
            var state = Air();
            state.FrameState = 1;
            state.VelocityY = 0f;
            state.ReleaseUp = false;
            False(Step(in state, new WitchBroomMotionInput { Up = true }, out _));
            state.ReleaseUp = true;
            False(Step(in state, default(WitchBroomMotionInput), out _));
        }

        private static void ClearanceMustBeProvedEveryTick()
        {
            var state = Air();
            True(Step(in state, new WitchBroomMotionInput { Up = true }, out var first));
            False(first.Next.OpenDryPath);
            False(Step(in first.Next, new WitchBroomMotionInput { Up = true }, out _));
            var reproved = first.Next;
            reproved.OpenDryPath = true;
            True(Step(in reproved, new WitchBroomMotionInput { Up = true }, out _));
        }

        private static void MultiTickCandidateCanReturnToASafeDismountState()
        {
            var state = Air();
            for (var tick = 0; tick < 120; tick++)
            {
                state.OpenDryPath = true;
                var input = new WitchBroomMotionInput
                {
                    Horizontal = tick < 30 ? 1 : tick < 60 ? -1 : 0,
                    Up = tick < 25,
                    Down = tick >= 40 && tick < 65
                };
                True(Step(in state, input, out var step));
                True(step.Supported);
                state = step.Next;
                True(IsFinite(state.PositionX) && IsFinite(state.PositionY));
                True(IsFinite(state.VelocityX) && IsFinite(state.VelocityY));
            }
            Near(0f, state.VelocityX);
            Near(WitchBroomMotion.NeutralTarget, state.VelocityY);

            var release = MountEdge();
            release.MountActive = true;
            release.ActiveMountType = 23;
            release.ControlMount = false;
            release.ReleaseMount = false;
            release.CanFitDismount = true;
            var rearmed = WitchBroomMotion.ResolveToggle(in release);
            True(rearmed.NextReleaseMount);
            release.ControlMount = true;
            release.ReleaseMount = rearmed.NextReleaseMount;
            Equal(WitchBroomMountTransition.Dismount,
                WitchBroomMotion.ResolveToggle(in release).Transition);
        }

        private static WitchBroomToggleSnapshot MountEdge() => new WitchBroomToggleSnapshot
        {
            Known = true,
            ControlMount = true,
            ReleaseMount = true,
            ActiveMountType = -1,
            QuickMountItemType = 4444,
            QuickMountType = 23,
            ItemUseStartEdgeReady = true,
            CanFitMount = true,
            CanFitDismount = true
        };

        private static WitchBroomMotionSnapshot Air() => new WitchBroomMotionSnapshot
        {
            Known = true,
            MountActive = true,
            MountType = 23,
            FrameState = 2,
            PositionX = 100f,
            PositionY = 100f,
            Gravity = .4f,
            NormalGravity = true,
            Dry = true,
            OpenDryPath = true,
            PortalPhysicsDisabled = true,
            ReleaseUp = true
        };

        private static bool Step(in WitchBroomMotionSnapshot state, in WitchBroomMotionInput input,
            out WitchBroomMotionResult result) =>
            WitchBroomMotion.TryAdvanceOpenDryTick(in state, in input, out result);

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void True(bool value) { if (!value) throw new InvalidOperationException("expected true"); }
        private static void False(bool value) { if (value) throw new InvalidOperationException("expected false"); }
        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .0001f)
                throw new InvalidOperationException($"expected {expected}, actual {actual}");
        }
        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"expected {expected}, actual {actual}");
        }
    }
}
