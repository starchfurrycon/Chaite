using Chaite.Core;

namespace Chaite.Plugin
{
    internal static class MovementActionGate
    {
        public static bool ShouldHoldJump(bool requested, bool grounded, bool releaseReady, bool grappling)
        {
            // Vanilla JumpMovement only rearms releaseJump on a released-input
            // frame. Holding through a landing otherwise leaves the player stuck
            // on the floor. Flight and the planner's grapple-release pulses must
            // not be chopped into alternating key presses.
            return requested && (!grounded || releaseReady || grappling);
        }

        public static bool ResolveJump(bool requested, JumpAction action, in JumpSnapshot state,
            bool grounded, bool grappling)
        {
            return JumpMotion.ResolveControl(requested, action, in state, grounded, grappling);
        }
    }
}
