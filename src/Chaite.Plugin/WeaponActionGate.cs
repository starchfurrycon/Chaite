namespace Chaite.Plugin
{
    /// <summary>
    /// Never replay a shot planned for the old weapon after native delayed
    /// selection installs another slot. Wait one frame for its real profile.
    /// A pending slot of -1 is the only valid "no request this frame" sentinel.
    /// Inventory bounds, ammo, LOS and native use eligibility remain caller checks.
    /// </summary>
    internal static class WeaponActionGate
    {
        public static bool ShouldFire(bool requested, int snapshotSlot, int actualSlot, int pendingSlot) =>
            requested && snapshotSlot >= 0 && actualSlot >= 0 && pendingSlot >= -1 &&
            snapshotSlot == actualSlot && (pendingSlot < 0 || pendingSlot == actualSlot);

        public static bool ShouldFireExact(bool requested, int snapshotSlot,
            int actualSlot, int pendingSlot, int expectedWeaponId,
            int actualWeaponId, int expectedAmmoId, int actualAmmoId,
            int expectedProjectileId, int actualProjectileId) =>
            ShouldFire(requested, snapshotSlot, actualSlot, pendingSlot) &&
            expectedWeaponId > 0 && expectedWeaponId == actualWeaponId &&
            expectedAmmoId >= 0 && expectedAmmoId == actualAmmoId &&
            expectedProjectileId > 0 &&
            expectedProjectileId == actualProjectileId;

        /// <summary>
        /// Converts an already identity-certified firing request into the
        /// native use-item level. Automatic/channelled weapons may stay held;
        /// a non-auto weapon receives exactly one press only while vanilla's
        /// releaseUseItem edge is armed. The following frame is therefore a
        /// real release instead of an ineffective permanent hold.
        /// </summary>
        public static bool ShouldEmitUseItem(bool certifiedFire, bool visible,
            bool autoReuse, bool channel, bool releaseUseItem) =>
            certifiedFire && visible &&
            (autoReuse || channel || releaseUseItem);

        /// <summary>
        /// Quick-heal is a separate vanilla input edge, but it still competes
        /// with the selected item's animation/update slot.  When that edge is
        /// armed, defer the ordinary output pulse for this frame so the native
        /// potion action can run.  The caller re-evaluates the release latch on
        /// the next frame; this does not consume or invent a weapon action.
        /// </summary>
        public static bool ShouldEmitUseItemUnlessQuickHeal(
            bool quickHealPulse, bool certifiedFire, bool visible,
            bool autoReuse, bool channel, bool releaseUseItem) =>
            !quickHealPulse && ShouldEmitUseItem(certifiedFire, visible,
                autoReuse, channel, releaseUseItem);
    }
}
