namespace Chaite.Core
{
    /// <summary>
    /// Read-only facts needed to hand an already-active vanilla movement
    /// controller back to the ordinary Boss route.  This deliberately models
    /// only native release edges: it does not pretend an arbitrary mount or
    /// grapple has a combat movement implementation.
    /// </summary>
    public struct ActiveNativeMobilityHandoffSnapshot
    {
        public bool Known;
        public bool EncounterActive;
        public bool PlayerDead;

        public bool Grappling;
        public bool ReleaseJump;

        public bool MountActive;
        public bool ReleaseMount;
        public bool MountDismountProbeKnown;
        public bool MountCanDismount;
    }

    public enum ActiveNativeMobilityHandoffAction
    {
        None,
        RearmJump,
        DetachGrapple,
        RearmMount,
        DismountMount,
        Ready,
        Rejected
    }

    /// <summary>
    /// A single output frame for <see cref="ActiveNativeMobilityHandoff"/>.
    /// All fields other than the explicitly named native release key remain
    /// neutral, so joining an encounter never accidentally fires an item or
    /// mixes an unreviewed controller with the Boss planner.
    /// </summary>
    public struct ActiveNativeMobilityHandoffDecision
    {
        public ActiveNativeMobilityHandoffAction Action;
        public bool Complete;
        public bool Rejected;
        public bool HoldJump;
        public bool ToggleMount;
        public string Reason;
    }

    /// <summary>
    /// Bounded native-controller handoff for joining an already running Boss
    /// fight.  Terraria 1.4.5.8 removes all attached hooks on a rearmed
    /// controlJump edge in Player.GrappleMovement.  For mounts, QuickMount
    /// performs one ordinary TryDismountWithResult call on a rearmed
    /// controlMount edge; repeated failed edges eventually take the vanilla
    /// forced-dismount/teleport path, so this controller never retries a mount
    /// pulse after it has been sent.
    /// </summary>
    public sealed class ActiveNativeMobilityHandoff
    {
        public const int MaximumFrames = 12;

        private bool _active;
        private bool _grapplePulseIssued;
        private bool _mountPulseIssued;
        private int _frames;

        public bool Active => _active;

        public void Begin()
        {
            _active = true;
            _grapplePulseIssued = false;
            _mountPulseIssued = false;
            _frames = 0;
        }

        public void Reset()
        {
            _active = false;
            _grapplePulseIssued = false;
            _mountPulseIssued = false;
            _frames = 0;
        }

        public ActiveNativeMobilityHandoffDecision Advance(
            in ActiveNativeMobilityHandoffSnapshot snapshot)
        {
            if (!_active)
                return Decision(ActiveNativeMobilityHandoffAction.Rejected,
                    false, true, false, false, "native mobility handoff is not active");
            if (!snapshot.Known || !snapshot.EncounterActive)
                return Reject("native mobility observation is incomplete or the Boss left");
            if (snapshot.PlayerDead)
                return Decision(ActiveNativeMobilityHandoffAction.None,
                    false, false, false, false, null);
            if (++_frames > MaximumFrames)
                return Reject("native mobility release did not settle within the bounded handoff window");

            // A mount and a hook can coexist for a subset of vanilla mount
            // identities.  Detach the hook first: this exactly follows the
            // native GrappleMovement branch and avoids mixing two release keys
            // in one Player.Update.
            if (snapshot.Grappling)
            {
                if (_grapplePulseIssued)
                    return Reject("the verified native grapple-detach edge did not remove the active hook");
                if (!snapshot.ReleaseJump)
                    return Decision(ActiveNativeMobilityHandoffAction.RearmJump,
                        false, false, false, false, null);
                _grapplePulseIssued = true;
                return Decision(ActiveNativeMobilityHandoffAction.DetachGrapple,
                    false, false, true, false, null);
            }

            if (snapshot.MountActive)
            {
                if (!snapshot.MountDismountProbeKnown)
                    return Reject("the active mount has no verified dismount-space observation");
                if (!snapshot.MountCanDismount)
                    return Reject("the active mount cannot safely dismount at the current position");
                if (_mountPulseIssued)
                    return Reject("the verified native mount-dismount edge did not remove the active mount");
                if (!snapshot.ReleaseMount)
                    return Decision(ActiveNativeMobilityHandoffAction.RearmMount,
                        false, false, false, false, null);
                _mountPulseIssued = true;
                return Decision(ActiveNativeMobilityHandoffAction.DismountMount,
                    false, false, false, true, null);
            }

            _active = false;
            return Decision(ActiveNativeMobilityHandoffAction.Ready,
                true, false, false, false, null);
        }

        private ActiveNativeMobilityHandoffDecision Reject(string reason)
        {
            _active = false;
            return Decision(ActiveNativeMobilityHandoffAction.Rejected,
                false, true, false, false, reason);
        }

        private static ActiveNativeMobilityHandoffDecision Decision(
            ActiveNativeMobilityHandoffAction action, bool complete,
            bool rejected, bool holdJump, bool toggleMount, string reason)
        {
            return new ActiveNativeMobilityHandoffDecision
            {
                Action = action,
                Complete = complete,
                Rejected = rejected,
                HoldJump = holdJump,
                ToggleMount = toggleMount,
                Reason = reason
            };
        }
    }
}
