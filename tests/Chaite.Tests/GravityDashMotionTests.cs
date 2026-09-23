using System;
using Chaite.Core;

namespace Chaite.Tests
{
    /// <summary>
    /// Standalone contract regressions. The project file intentionally does not
    /// include this file until the runtime reader/integration review is complete.
    /// </summary>
    public static class GravityDashMotionTests
    {
#if GRAVITY_DASH_STANDALONE
        public static int Main()
        {
            try
            {
                Console.WriteLine("PASS gravity/dash contracts: " + RunAll());
                return 0;
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL gravity/dash contracts: " + error);
                return 1;
            }
        }
#endif

        public static int RunAll()
        {
            var count = 0;
            Run(GravityUsesUpEdgeInBothDirections, ref count);
            Run(GravityReleaseFrameRearmsEdge, ref count);
            Run(GravityCandidateRejectsMountForcedAndUnknownState, ref count);
            Run(GravityRejectsNonFiniteWithoutMutation, ref count);
            Run(GravityFlipClearsJumpAndPreservesVelocity, ref count);
            Run(DedicatedDashDirectionUsesFacingUnlessOppositeHeld, ref count);
            Run(DedicatedDashStartsAtExactClearAndBlockedSpeeds, ref count);
            Run(DashReleaseIsRearmedOnlyByNativeInputCopy, ref count);
            Run(DashNegativeDelayIsActiveNotReady, ref count);
            Run(DashUsesReviewedHighAndLowSpeedDecay, ref count);
            Run(DashCooldownNeedsAllThirtyFollowingTicks, ref count);
            Run(DashRejectsUnknownIdentityMountContactAndNonFinite, ref count);
            return count;
        }

        private static GravityFlipState Gravity(float direction = 1f)
        {
            return new GravityFlipState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                Identity = GravityControlIdentity.GravitationBuff18,
                GravControl = true,
                GravityDirection = direction,
                PositionY = 801.75f,
                VelocityY = 3.25f,
                JumpTicks = 9,
                FallStart = 12,
                ReleaseUp = true
            };
        }

        private static EyeShieldDashState Shield()
        {
            return new EyeShieldDashState
            {
                Known = true,
                NormalPlayerUpdatePath = true,
                EquipmentIdentity = DashEquipmentIdentity.ShieldOfCthulhuItem3097,
                DashType = 2,
                Dash = 2,
                DashDelay = 0,
                DashTime = 0,
                TimeSinceLastDashStarted = 300,
                EocHit = -1,
                ReleaseDash = true,
                ControlDash = true,
                FacingDirection = 1,
                VelocityX = 2f,
                VelocityY = -1.5f,
                AccRunSpeed = 6f,
                MaxRunSpeed = 5f,
                ForwardSolidProbeKnown = true,
                HostileContactKnown = true
            };
        }

        private static void GravityUsesUpEdgeInBothDirections()
        {
            foreach (var direction in new[] { 1f, -1f })
            {
                var state = Gravity(direction);
                GravityFlipCandidate candidate;
                True(GravityFlipMotion.TryCreatePotionCandidate(state, -direction, out candidate));
                True(candidate.ControlUp);
                Equal(-direction, candidate.ExpectedGravityDirection);
                Equal(GravityFlipPhase.Flipped,
                    GravityFlipMotion.TryAdvanceNativeBranch(ref state, candidate.ControlUp));
                Equal(-direction, state.GravityDirection);
                False(state.ReleaseUp);
            }
        }

        private static void GravityReleaseFrameRearmsEdge()
        {
            var state = Gravity();
            Equal(GravityFlipPhase.Flipped, GravityFlipMotion.TryAdvanceNativeBranch(ref state, true));
            Equal(GravityFlipPhase.NoEdge, GravityFlipMotion.TryAdvanceNativeBranch(ref state, true));
            Equal(-1f, state.GravityDirection);
            Equal(GravityFlipPhase.NoEdge, GravityFlipMotion.TryAdvanceNativeBranch(ref state, false));
            True(state.ReleaseUp);
            Equal(GravityFlipPhase.Flipped, GravityFlipMotion.TryAdvanceNativeBranch(ref state, true));
            Equal(1f, state.GravityDirection);
        }

        private static void GravityCandidateRejectsMountForcedAndUnknownState()
        {
            GravityFlipCandidate candidate;
            var mounted = Gravity(-1f);
            mounted.MountActive = true;
            False(GravityFlipMotion.TryCreatePotionCandidate(mounted, 1f, out candidate));
            Equal(GravityFlipPhase.MountedForcedNormal,
                GravityFlipMotion.TryAdvanceNativeBranch(ref mounted, true));
            Equal(1f, mounted.GravityDirection);

            var forced = Gravity();
            forced.ForcedGravity = 2;
            False(GravityFlipMotion.TryCreatePotionCandidate(forced, -1f, out candidate));
            Equal(GravityFlipPhase.ForcedInverted,
                GravityFlipMotion.TryAdvanceNativeBranch(ref forced, false));
            Equal(-1f, forced.GravityDirection);

            var mismatch = Gravity();
            mismatch.Identity = GravityControlIdentity.Unknown;
            False(GravityFlipMotion.TryCreatePotionCandidate(mismatch, -1f, out candidate));
            mismatch.Identity = GravityControlIdentity.GravityGlobeItem1131;
            False(GravityFlipMotion.TryCreatePotionCandidate(mismatch, -1f, out candidate));
        }

        private static void GravityRejectsNonFiniteWithoutMutation()
        {
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var state = Gravity();
                state.PositionY = bad;
                var before = state.GravityDirection;
                Equal(GravityFlipPhase.Unsupported,
                    GravityFlipMotion.TryAdvanceNativeBranch(ref state, true));
                Equal(before, state.GravityDirection);
                GravityFlipCandidate candidate;
                False(GravityFlipMotion.TryCreatePotionCandidate(state, -1f, out candidate));

                state = Gravity();
                state.VelocityY = bad;
                Equal(GravityFlipPhase.Unsupported,
                    GravityFlipMotion.TryAdvanceNativeBranch(ref state, true));
            }
        }

        private static void GravityFlipClearsJumpAndPreservesVelocity()
        {
            var state = Gravity();
            var velocity = state.VelocityY;
            Equal(GravityFlipPhase.Flipped, GravityFlipMotion.TryAdvanceNativeBranch(ref state, true));
            Equal(50, state.FallStart); // native conv.i4 truncates 801.75 / 16 toward zero
            Equal(0, state.JumpTicks);
            Equal(velocity, state.VelocityY);
        }

        private static void DedicatedDashDirectionUsesFacingUnlessOppositeHeld()
        {
            var cases = new[]
            {
                Tuple.Create(false, false, 1),
                Tuple.Create(false, true, 1),
                Tuple.Create(true, true, 1),
                Tuple.Create(true, false, -1)
            };
            foreach (var item in cases)
            {
                var state = Shield();
                state.ControlLeft = item.Item1;
                state.ControlRight = item.Item2;
                EyeShieldDashCandidate candidate;
                True(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));
                Equal(item.Item3, candidate.Direction);
            }
        }

        private static void DedicatedDashStartsAtExactClearAndBlockedSpeeds()
        {
            foreach (var blocked in new[] { false, true })
            {
                var state = Shield();
                state.ForwardSolidProbeBlocked = blocked;
                var oldY = state.VelocityY;
                EyeShieldDashCandidate candidate;
                True(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));
                Equal(blocked ? EyeShieldDashPhase.StartedIntoSolidProbe : EyeShieldDashPhase.StartedClear,
                    candidate.Phase);
                Near(blocked ? 7.25f : 14.5f, candidate.AfterDashMovement.VelocityX);
                Equal(oldY, candidate.AfterDashMovement.VelocityY);
                Equal(-1, candidate.AfterDashMovement.DashDelay);
                Equal(15, candidate.AfterDashMovement.EocDash);
                Equal(0, candidate.AfterDashMovement.DashTime);
                Equal(0, candidate.AfterDashMovement.TimeSinceLastDashStarted);
                False(candidate.AfterDashMovement.ReleaseDash);
            }
        }

        private static void DashReleaseIsRearmedOnlyByNativeInputCopy()
        {
            var state = StartedShield();
            False(state.ReleaseDash);
            True(EyeShieldDashMotion.TryReplayPlannedControl(ref state, false));
            False(state.ReleaseDash);
            True(EyeShieldDashMotion.TryApplyNativeInputCopy(ref state, true));
            False(state.ReleaseDash);
            True(EyeShieldDashMotion.TryApplyNativeInputCopy(ref state, false));
            True(state.ReleaseDash);
            True(EyeShieldDashMotion.TryReplayPlannedControl(ref state, true));
            True(state.ReleaseDash);
        }

        private static void DashNegativeDelayIsActiveNotReady()
        {
            var state = StartedShield();
            False(EyeShieldDashMotion.IsReady(state));
            Equal(EyeShieldDashPhase.ActiveHighSpeed,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref state));
            var ready = Shield();
            True(EyeShieldDashMotion.IsReady(ready));
        }

        private static void DashUsesReviewedHighAndLowSpeedDecay()
        {
            var high = StartedShield();
            Equal(EyeShieldDashPhase.ActiveHighSpeed,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref high));
            // The decay is an engine measurement, and its form is the point: the
            // engine removes a tenth of a pixel of speed and then scales what is
            // left, so the ratio drifts with speed and a bare ratio is only
            // right at the speed it was read off. Re-measured per tick from the
            // replay dense trace, high speed is exactly (14.5 - .1) * .985 and
            // low speed is exactly (11.8208 - .1) * .94, both matching across
            // their whole runs.
            Near((14.5f - .1f) * .985f, high.VelocityX);

            var low = StartedShield();
            low.VelocityX = 10f;
            Equal(EyeShieldDashPhase.ActiveLowSpeed,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref low));
            Near((10f - .1f) * .94f, low.VelocityX);

            low.VelocityX = 6f;
            Equal(EyeShieldDashPhase.EnteredCooldown,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref low));
            Equal(30, low.DashDelay);
            Near(6f, low.VelocityX);
        }

        private static void DashCooldownNeedsAllThirtyFollowingTicks()
        {
            var state = StartedShield();
            // The next local-player update copies an unheld raw Dash key before
            // DashMovement, which is what rearms releaseDash.
            True(EyeShieldDashMotion.TryApplyNativeInputCopy(ref state, false));
            state.VelocityX = 6f;
            Equal(EyeShieldDashPhase.EnteredCooldown,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref state));
            for (var tick = 1; tick <= 30; tick++)
            {
                var phase = EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref state);
                Equal(tick == 30 ? EyeShieldDashPhase.ReadyNextUpdate : EyeShieldDashPhase.Cooldown,
                    phase);
                Equal(30 - tick, state.DashDelay);
                Equal(Math.Max(0, 15 - tick), state.EocDash);
            }
            True(EyeShieldDashMotion.IsReady(state));
        }

        private static void DashRejectsUnknownIdentityMountContactAndNonFinite()
        {
            EyeShieldDashCandidate candidate;
            var state = Shield();
            state.EquipmentIdentity = DashEquipmentIdentity.Unknown;
            False(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));
            state = Shield();
            state.DashType = 1;
            False(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));
            state = Shield();
            state.MountActive = true;
            False(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));
            state = Shield();
            state.ForwardSolidProbeKnown = false;
            False(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));

            var active = StartedShield();
            active.HostileContactKnown = false;
            var before = active.VelocityX;
            Equal(EyeShieldDashPhase.Unsupported,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref active));
            Equal(before, active.VelocityX);
            active.HostileContactKnown = true;
            active.HostileContact = true;
            Equal(EyeShieldDashPhase.Unsupported,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref active));

            active = StartedShield();
            active.VelocityX = 6f;
            Equal(EyeShieldDashPhase.EnteredCooldown,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref active));
            active.EocDash = 7; // impossible collision-free delay=30 lineage
            Equal(EyeShieldDashPhase.Unsupported,
                EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref active));

            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                active = StartedShield();
                active.VelocityX = bad;
                Equal(EyeShieldDashPhase.Unsupported,
                    EyeShieldDashMotion.TryAdvanceCollisionFreeTick(ref active));
            }
        }

        private static EyeShieldDashState StartedShield()
        {
            EyeShieldDashCandidate candidate;
            var state = Shield();
            True(EyeShieldDashMotion.TryCreateDedicatedCandidate(state, out candidate));
            return candidate.AfterDashMovement;
        }

        private static void Run(Action test, ref int count)
        {
            test();
            count++;
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

        private static void Near(float expected, float actual)
        {
            if (Math.Abs(expected - actual) > .0001f)
                throw new InvalidOperationException("expected " + expected + ", got " + actual);
        }
    }
}
