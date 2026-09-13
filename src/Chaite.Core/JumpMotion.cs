using System;

namespace Chaite.Core
{
    public enum JumpAction { Default, Hold, Release, Cloud }

    public enum GravityPhase { Unsupported, Ballistic, FeatherFall, FeatherFallUp }

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
        public bool SlowFall;
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

        public static GravityPhase ApplyGravityChecked(ref float velocityY, float gravity, float maxFallSpeed,
            bool inverted, bool slowFall, bool controlUp, bool controlDown)
        {
            // A caller must not turn a corrupt observation into a plausible
            // trajectory. Preserve the sampled velocity and explicitly report
            // unsupported whenever a required native scalar is not finite.
            if (!IsFinite(velocityY) || !IsFinite(gravity) || gravity < 0f ||
                !IsFinite(maxFallSpeed) || maxFallSpeed <= 0f)
                return GravityPhase.Unsupported;

            // The native cap only limits falling. Symmetric +/-maxFallSpeed
            // clamping incorrectly reduces strong upward jumps/knockback.
            var direction = inverted ? -1f : 1f;
            var featherFall = slowFall && !controlDown;
            var divisor = featherFall ? (controlUp ? 10f : 3f) : 1f;
            var fallingSpeed = velocityY * direction + gravity / divisor;
            fallingSpeed = Math.Min(maxFallSpeed, fallingSpeed);
            if (featherFall && fallingSpeed > maxFallSpeed / 3f)
                fallingSpeed = maxFallSpeed / 3f;
            // Native 1.4.5.8 deliberately uses max/5 as the trigger and
            // max/10 as the replacement. This produces a small saw-tooth band;
            // treating max/10 as an unconditional cap is observably different.
            if (featherFall && controlUp && fallingSpeed > maxFallSpeed / 5f)
                fallingSpeed = maxFallSpeed / 10f;
            velocityY = fallingSpeed * direction;
            return featherFall ? (controlUp ? GravityPhase.FeatherFallUp : GravityPhase.FeatherFall) :
                GravityPhase.Ballistic;
        }

        public static float ApplyGravity(float velocityY, float gravity, float maxFallSpeed, bool inverted)
            => ApplyGravity(velocityY, gravity, maxFallSpeed, inverted, false, false, false);

        // Exact feather-fall overload. Down bypasses feather fall completely;
        // Up selects the native one-tenth branch only while feather fall is in
        // force. Invalid inputs remain invalid/unchanged rather than being
        // coerced into an apparently safe prediction.
        public static float ApplyGravity(float velocityY, float gravity, float maxFallSpeed, bool inverted,
            bool slowFall, bool controlUp, bool controlDown)
        {
            var result = velocityY;
            ApplyGravityChecked(ref result, gravity, maxFallSpeed, inverted, slowFall, controlUp, controlDown);
            return result;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
