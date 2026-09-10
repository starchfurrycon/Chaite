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
    }
}
