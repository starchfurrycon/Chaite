using System;

namespace Chaite.Core
{
    /// <summary>
    /// The only mount-key transitions modeled by the verified Witch's Broom contract.
    /// This is descriptive data; resolving a transition never writes Terraria input.
    /// </summary>
    public enum WitchBroomMountTransition
    {
        Unsupported,
        None,
        Mount,
        Dismount,
        RejectedByNative,
        UnsafeDismountBlocked
    }

    public struct WitchBroomToggleSnapshot
    {
        public bool Known;
        public bool ControlMount;
        public bool ReleaseMount;
        public bool MountActive;
        public int ActiveMountType;
        public int QuickMountItemType;
        public int QuickMountType;
        public bool CrowdControlled;
        public bool Tongued;
        public bool Dead;
        public bool NoItems;
        public bool GravityInverted;
        public bool Grappling;
        public bool ItemUseStartEdgeReady;
        public bool CanFitMount;
        public bool CanFitDismount;
    }

    public struct WitchBroomToggleResult
    {
        public bool Supported;
        public WitchBroomMountTransition Transition;
        public bool NextReleaseMount;
        public bool NextMountActive;
        public int NextMountType;
    }

    public enum WitchBroomMotionPhase
    {
        Unsupported,
        GroundLaunch,
        MountEntry,
        HoverAscent,
        HoverDescent,
        HoverNeutral
    }

    /// <summary>
    /// Restricted native state for one dry, collision-free Witch's Broom tick.
    /// OpenDryPath is evidence for this tick only and is cleared in the result.
    /// </summary>
    public struct WitchBroomMotionSnapshot
    {
        public bool Known;
        public bool MountActive;
        public int MountType;
        public int FrameState;
        public float PositionX;
        public float PositionY;
        public float VelocityX;
        public float VelocityY;
        public float Gravity;
        public float TrackBoost;
        public bool ReleaseUp;
        public bool SlowFall;
        public bool NormalGravity;
        public bool Dry;
        public bool OpenDryPath;
        public bool PortalPhysicsDisabled;
        public bool Grappling;
        public bool HookInFlight;
        public bool DashInProgress;
        public bool CrowdControlled;
        public bool Tongued;
        public bool Dead;
        public bool Pulley;
        public bool Sliding;
        public bool WindPushed;
        public bool ForcedMotion;
    }

    public struct WitchBroomMotionInput
    {
        public int Horizontal;
        public bool Up;
        public bool Down;

        // These actions have separate native state machines. They are carried
        // here only so a mixed candidate fails closed instead of silently
        // pretending that the broom model covered it.
        public bool Jump;
        public bool Hook;
        public bool Dash;
        public bool ToggleMount;
        public bool FlipGravity;
    }

    public struct WitchBroomMotionResult
    {
        public bool Supported;
        public WitchBroomMotionPhase Phase;
        public WitchBroomMotionSnapshot Next;
    }

    /// <summary>
    /// Allocation-free, side-effect-free subset of Terraria 1.4.5.8's native
    /// Witch's Broom movement. The source assembly identity is documented in
    /// docs/native-witch-broom-policy.md.
    /// </summary>
    public static class WitchBroomMotion
    {
        public const string VerifiedTerrariaSha256 =
            "960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3";
        public const int WitchBroomItemType = 4444;
        public const int WitchBroomMountType = 23;
        public const int WitchBroomBuffType = 230;
        public const int HitboxWidth = 20;
        public const int HitboxHeight = 42;
        public const int FlightTimeMax = 320;
        public const int FatigueMax = 320;
        public const float RunSpeed = 9f;
        public const float DashSpeed = 9f;
        public const float Acceleration = .16f;
        public const float RunSlowdown = .2f;
        public const float UpTarget = -8f;
        public const float DownTarget = 8f;
        public const float NeutralTarget = -.001f;
        public const float MaxSingleCollisionStep = 16f;

        /// <summary>
        /// Exact live-state admission for a boss baseline which deliberately
        /// chooses the already-mounted type-23 broom. This does not prove a
        /// future path and therefore does not inspect OpenDryPath; every planned
        /// tick must still provide fresh collision/threat evidence.
        /// </summary>
        public static bool MatchesActiveBaseline(in WitchBroomMotionSnapshot state)
        {
            return state.Known && state.MountActive &&
                state.MountType == WitchBroomMountType &&
                (state.FrameState == 0 || state.FrameState == 1 ||
                 state.FrameState == 2) && state.NormalGravity && state.Dry &&
                state.PortalPhysicsDisabled && !state.Grappling &&
                !state.HookInFlight && !state.DashInProgress &&
                !state.CrowdControlled && !state.Tongued && !state.Dead &&
                !state.Pulley && !state.Sliding && !state.WindPushed &&
                !state.ForcedMotion && state.TrackBoost == 0f &&
                Finite(state.PositionX) && Finite(state.PositionY) &&
                Finite(state.VelocityX) && Finite(state.VelocityY) &&
                Finite(state.Gravity) && state.Gravity >= 0f &&
                WithinSingleCollisionStep(state.VelocityX, state.VelocityY);
        }

        /// <summary>
        /// Replays only the releaseMount edge and the conservative gates needed
        /// to enter or leave type 23. It does not call QuickMount or emit input.
        /// </summary>
        public static WitchBroomToggleResult ResolveToggle(in WitchBroomToggleSnapshot state)
        {
            var result = new WitchBroomToggleResult
            {
                Supported = false,
                Transition = WitchBroomMountTransition.Unsupported,
                NextReleaseMount = state.ReleaseMount,
                NextMountActive = state.MountActive,
                NextMountType = state.MountActive ? state.ActiveMountType : -1
            };
            if (!state.Known || state.MountActive && state.ActiveMountType != WitchBroomMountType)
                return result;

            result.Supported = true;
            if (!state.ControlMount)
            {
                result.Transition = WitchBroomMountTransition.None;
                result.NextReleaseMount = true;
                return result;
            }

            result.NextReleaseMount = false;
            if (!state.ReleaseMount)
            {
                result.Transition = WitchBroomMountTransition.None;
                return result;
            }

            // Native can force a no-space dismount after repeated key edges and
            // then teleport the player. A rescue candidate must never model or
            // intentionally enter that recovery path.
            if (state.MountActive)
            {
                // Player.Update returns through its dead path before copying a
                // local mount-key edge. Other QuickMount entry conditions are
                // in the unmounted branch only.
                if (state.Dead)
                {
                    result.Transition = WitchBroomMountTransition.RejectedByNative;
                    return result;
                }
                if (!state.CanFitDismount)
                {
                    result.Transition = WitchBroomMountTransition.UnsafeDismountBlocked;
                    return result;
                }
                result.Transition = WitchBroomMountTransition.Dismount;
                result.NextMountActive = false;
                result.NextMountType = -1;
                return result;
            }

            if (state.QuickMountItemType != WitchBroomItemType ||
                state.QuickMountType != WitchBroomMountType)
            {
                result.Supported = false;
                result.Transition = WitchBroomMountTransition.Unsupported;
                return result;
            }
            if (state.CrowdControlled || state.Tongued || state.Dead || state.NoItems ||
                state.GravityInverted || state.Grappling || !state.ItemUseStartEdgeReady || !state.CanFitMount)
            {
                result.Transition = WitchBroomMountTransition.RejectedByNative;
                return result;
            }
            result.Transition = WitchBroomMountTransition.Mount;
            result.NextMountActive = true;
            result.NextMountType = WitchBroomMountType;
            return result;
        }

        /// <summary>
        /// Advances one native-order tick: mounted horizontal movement, the
        /// ground Up-edge bootstrap, type-23 Hover, then an already-proven open
        /// dry collision step. Every unmodeled interaction is rejected.
        /// </summary>
        public static bool TryAdvanceOpenDryTick(in WitchBroomMotionSnapshot state,
            in WitchBroomMotionInput input, out WitchBroomMotionResult result)
        {
            result = new WitchBroomMotionResult
            {
                Supported = false,
                Phase = WitchBroomMotionPhase.Unsupported,
                Next = state
            };
            if (!Supports(in state, in input)) return false;

            var next = state;
            var horizontal = AdvanceHorizontal(state.VelocityX, state.VelocityY, input.Horizontal);
            if (!Finite(horizontal)) return false;

            var vertical = state.VelocityY;
            WitchBroomMotionPhase phase;
            if (state.FrameState == 0 || state.FrameState == 1)
            {
                if (vertical == 0f)
                {
                    // Player.Update performs this before UpdateControlHolds and
                    // before Mount.Hover. With the prior grounded frame state,
                    // Hover preserves this first lift velocity for the tick.
                    vertical = -(Acceleration + state.Gravity + .001f);
                    phase = WitchBroomMotionPhase.GroundLaunch;
                }
                else
                {
                    // A newly mounted broom retains the unmounted velocity.
                    // Its initial frame is normally standing; fatigue-ignoring
                    // Hover leaves a non-zero value alone except its sentinel.
                    if (vertical == NeutralTarget) vertical -= NeutralTarget;
                    phase = WitchBroomMotionPhase.MountEntry;
                }
            }
            else
            {
                phase = ApplyHover(ref vertical, input.Up, input.Down);
            }
            if (!Finite(vertical) || !WithinSingleCollisionStep(horizontal, vertical)) return false;

            var positionX = state.PositionX + horizontal;
            var positionY = state.PositionY;
            // Native Hover offsets Y by +.001 immediately before collision
            // when neutral convergence lands exactly on -.001. The following
            // open collision displacement of -.001 makes the net Y delta zero.
            if (state.FrameState == 2 && !input.Up && !input.Down && vertical == NeutralTarget)
                positionY += .001f;
            positionY += vertical;
            if (!Finite(positionX) || !Finite(positionY)) return false;

            next.PositionX = positionX;
            next.PositionY = positionY;
            next.VelocityX = horizontal;
            next.VelocityY = vertical;
            next.ReleaseUp = !input.Up;
            // PlayerFrame selects the in-air frame only for non-zero Y. An
            // exact zero produced while changing vertical intent is treated
            // as standing/running even in open air; do not invent frame 2.
            next.FrameState = vertical != 0f ? 2 : horizontal == 0f ? 0 : 1;
            // Clearance is a per-tick claim. A caller must prove the complete
            // 20x42 swept hitbox again before advancing the next candidate tick.
            next.OpenDryPath = false;
            result = new WitchBroomMotionResult
            {
                Supported = true,
                Phase = phase,
                Next = next
            };
            return true;
        }

        private static bool Supports(in WitchBroomMotionSnapshot state,
            in WitchBroomMotionInput input)
        {
            if (!state.Known || !state.MountActive || state.MountType != WitchBroomMountType ||
                !state.NormalGravity || !state.Dry || !state.OpenDryPath ||
                !state.PortalPhysicsDisabled || state.Grappling || state.HookInFlight ||
                state.DashInProgress || state.CrowdControlled || state.Tongued || state.Dead ||
                state.Pulley || state.Sliding || state.WindPushed || state.ForcedMotion ||
                state.TrackBoost != 0f)
                return false;
            if (input.Horizontal < -1 || input.Horizontal > 1 || input.Up && input.Down ||
                input.Jump || input.Hook || input.Dash || input.ToggleMount || input.FlipGravity)
                return false;
            if (!Finite(state.PositionX) || !Finite(state.PositionY) ||
                !Finite(state.VelocityX) || !Finite(state.VelocityY) ||
                !Finite(state.Gravity) || state.Gravity < 0f || !Finite(state.TrackBoost) ||
                !WithinSingleCollisionStep(state.VelocityX, state.VelocityY))
                return false;

            if (state.FrameState == 2) return true;
            if (state.FrameState != 0 && state.FrameState != 1) return false;
            // A mid-air activation begins with Mount's reset frame state 0 and
            // preserves non-zero velocity. At exact zero, only the Up edge is
            // modeled; all other cases require Hover's live downward tile probe.
            return state.VelocityY != 0f || input.Up && state.ReleaseUp;
        }

        private static float AdvanceHorizontal(float velocityX, float velocityY, int horizontal)
        {
            if (horizontal < 0 && velocityX > -RunSpeed)
            {
                if (velocityX > RunSlowdown) velocityX -= RunSlowdown;
                velocityX -= Acceleration;
            }
            else if (horizontal > 0 && velocityX < RunSpeed)
            {
                if (velocityX < -RunSlowdown) velocityX += RunSlowdown;
                velocityX += Acceleration;
            }
            else
            {
                var slowdown = velocityY == 0f ? RunSlowdown : RunSlowdown * .5f;
                if (velocityX > slowdown) velocityX -= slowdown;
                else if (velocityX < -slowdown) velocityX += slowdown;
                else velocityX = 0f;
            }
            return velocityX;
        }

        private static WitchBroomMotionPhase ApplyHover(ref float velocityY, bool up, bool down)
        {
            var lower = NeutralTarget;
            var upper = NeutralTarget;
            WitchBroomMotionPhase phase;
            if (up)
            {
                lower = UpTarget;
                velocityY -= Acceleration;
                phase = WitchBroomMotionPhase.HoverAscent;
            }
            else if (down)
            {
                upper = DownTarget;
                velocityY += Acceleration;
                phase = WitchBroomMotionPhase.HoverDescent;
            }
            else
            {
                phase = WitchBroomMotionPhase.HoverNeutral;
            }

            if (velocityY < lower)
                velocityY = lower - velocityY < Acceleration ? lower : velocityY + Acceleration;
            else if (velocityY > upper)
                velocityY = velocityY - upper < Acceleration ? upper : velocityY - Acceleration;
            return phase;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool WithinSingleCollisionStep(float x, float y) =>
            (double)x * x + (double)y * y <=
            (double)MaxSingleCollisionStep * MaxSingleCollisionStep;
    }
}
