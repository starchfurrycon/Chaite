namespace Chaite.Core
{
    public enum CombatWeaponSelectionAction
    {
        Ready,
        RequestWhileNativeUseFinishes,
        RequestAfterNativeRelease,
        Rejected
    }

    public struct CombatWeaponSelectionDecision
    {
        public CombatWeaponSelectionAction Action;
        public bool Complete;
        public bool Rejected;
        public bool RequestSelection;
        public string Reason;
    }

    /// <summary>
    /// Bounded handoff from a consumed summon item to the combat hotbar slot.
    /// Vanilla buffers SelectedItemState.Select while the old item is active,
    /// then commits it from Player.Update once UsingOrReusingItem is false and
    /// ItemTimeIsZero is true.
    /// </summary>
    public sealed class CombatWeaponSelectionHandoff
    {
        public const int MaximumFrames = 300;
        public const int MaximumReleasedFrames = 3;

        private int _frames;
        private int _releasedFrames;

        public void Reset()
        {
            _frames = 0;
            _releasedFrames = 0;
        }

        public CombatWeaponSelectionDecision Advance(bool desiredSlotSelected,
            bool nativeCanChangeImmediately)
        {
            if (desiredSlotSelected)
            {
                Reset();
                return Decision(CombatWeaponSelectionAction.Ready, true,
                    false, false, null);
            }

            if (++_frames > MaximumFrames)
                return Reject("native item use did not release within the bounded weapon-selection handoff");

            if (!nativeCanChangeImmediately)
            {
                _releasedFrames = 0;
                return Decision(
                    CombatWeaponSelectionAction.RequestWhileNativeUseFinishes,
                    false, false, true, null);
            }

            if (++_releasedFrames > MaximumReleasedFrames)
                return Reject("released native selection did not commit at the reviewed Player.Update hook");

            return Decision(CombatWeaponSelectionAction.RequestAfterNativeRelease,
                false, false, true, null);
        }

        private CombatWeaponSelectionDecision Reject(string reason)
        {
            return Decision(CombatWeaponSelectionAction.Rejected, false,
                true, false, reason);
        }

        private static CombatWeaponSelectionDecision Decision(
            CombatWeaponSelectionAction action, bool complete, bool rejected,
            bool requestSelection, string reason)
        {
            return new CombatWeaponSelectionDecision
            {
                Action = action,
                Complete = complete,
                Rejected = rejected,
                RequestSelection = requestSelection,
                Reason = reason
            };
        }
    }
}
