using System;

namespace Chaite.Core
{
    public enum JumpAction { Default, Hold, Release, Cloud }

    // Known is an explicit, restricted native profile, not a promise that every
    // mount, liquid, wing, balloon or extra-jump accessory has been modeled.
    public struct JumpSnapshot
    {
        public bool Known;
        public int RemainingTicks;
        public float Speed;
        public int Height;
        public bool ReleaseReady;
        public bool CloudAvailable;
        public bool CloudEnabled;
        public bool AutoJump;
    }

    /// <summary>Allocation-free ordinary/cloud jump state and input transitions.</summary>
    public static class JumpMotion
    {
        // Player.Update does this before JumpMovement, including released-input
        // frames. Keep it separate so direct pre/post-JumpMovement oracles do
        // not accidentally hide an earlier update-order error.
        public static void RefreshBeforeMovement(ref JumpSnapshot state, float velocityY)
        {
            if (state.Known && velocityY == 0f) state.CloudAvailable = state.CloudEnabled;
        }

        // Use the same pure gate in the predictor and adapter. The Cloud action
        // rearms a held key only when a real, observed cloud charge exists. Once
        // consumed, holding continues the jump; it never invents another charge.
        public static bool ResolveControl(bool requested, JumpAction action, in JumpSnapshot state,
            bool grounded, bool grappling)
        {
            if (action == JumpAction.Release) return false;
            if (action == JumpAction.Hold) requested = true;
            if (action == JumpAction.Cloud && state.Known && state.CloudAvailable && !grounded && !grappling)
                return state.ReleaseReady;
            return requested && (!grounded || state.ReleaseReady || state.AutoJump || grappling);
        }

        // Called once per native tick before gravity and tile collision. Native
        // JumpMovement compares velocity.Y == 0 (including the exact apex), not
        // a broad "grounded" epsilon. Inputs are already resolved key states.
        public static void ApplyJump(ref JumpSnapshot state, ref float velocityY, bool controlJump,
            bool inverted)
        {
            if (!state.Known) return;
            if (!controlJump)
            {
                state.RemainingTicks = 0;
                state.ReleaseReady = true;
                return;
            }
            var zeroVelocity = velocityY == 0f;
            var gravityDirection = inverted ? -1f : 1f;
            if (state.RemainingTicks > 0)
            {
                if (zeroVelocity) state.RemainingTicks = 0;
                else
                {
                    velocityY = -state.Speed * gravityDirection;
                    state.RemainingTicks--;
                }
            }
            else if ((zeroVelocity || state.CloudAvailable) &&
                (state.ReleaseReady || state.AutoJump && zeroVelocity))
            {
                state.CloudAvailable = zeroVelocity && state.CloudEnabled;
                state.RemainingTicks = zeroVelocity ? state.Height : (int)(state.Height * .75f);
                velocityY = -state.Speed * gravityDirection;
            }
            state.ReleaseReady = false;
        }

        public static float ApplyGravity(float velocityY, float gravity, float maxFallSpeed, bool inverted)
        {
            // The native cap only limits falling. Symmetric +/-maxFallSpeed
            // clamping incorrectly reduces strong upward jumps/knockback.
            var direction = inverted ? -1f : 1f;
            var fallingSpeed = velocityY * direction + gravity;
            return Math.Min(maxFallSpeed, fallingSpeed) * direction;
        }
    }
}
