using System;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunWeaponActionGateRegressions()
        {
            Run(nameof(WeaponActionGatePreservesStableSlotAndRequestedFire), WeaponActionGatePreservesStableSlotAndRequestedFire);
            Run(nameof(WeaponActionGatePausesQueuedSwitchUntilFreshSnapshot), WeaponActionGatePausesQueuedSwitchUntilFreshSnapshot);
            Run(nameof(WeaponActionGateRejectsStaleSnapshotAfterSelection), WeaponActionGateRejectsStaleSnapshotAfterSelection);
            Run(nameof(WeaponActionGateRejectsInvalidSlotSentinels), WeaponActionGateRejectsInvalidSlotSentinels);
        }

        private static bool WeaponGate(bool requested, int snapshotSlot, int actualSlot, int pendingSlot)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.WeaponActionGate", true);
            var method = type.GetMethod("ShouldFire", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null);
            return (bool)method.Invoke(null, new object[] { requested, snapshotSlot, actualSlot, pendingSlot });
        }

        private static void WeaponActionGatePreservesStableSlotAndRequestedFire()
        {
            foreach (var slot in new[] { 0, 1, 9 })
            {
                True(WeaponGate(true, slot, slot, -1));
                True(WeaponGate(true, slot, slot, slot)); // An idempotent Select need not add latency.
                False(WeaponGate(false, slot, slot, -1));
                False(WeaponGate(false, slot, slot, slot));
            }
        }

        private static void WeaponActionGatePausesQueuedSwitchUntilFreshSnapshot()
        {
            False(WeaponGate(true, 1, 1, 2)); // Queue B while the snapshot/actual weapon are still A.
            True(WeaponGate(true, 2, 2, -1)); // Next frame observes B and can fire normally.
            False(WeaponGate(true, 2, 1, 2)); // Guessing the pending profile is not authoritative either.
            False(WeaponGate(true, 9, 9, 0)); // Slot zero is a real request, not "no request".
            True(WeaponGate(true, 0, 0, -1));
        }

        private static void WeaponActionGateRejectsStaleSnapshotAfterSelection()
        {
            False(WeaponGate(true, 1, 2, -1));
            False(WeaponGate(true, 1, 2, 2));
            False(WeaponGate(true, 1, 2, 1));
        }

        private static void WeaponActionGateRejectsInvalidSlotSentinels()
        {
            False(WeaponGate(true, -1, -1, -1));
            False(WeaponGate(true, -1, 0, -1));
            False(WeaponGate(true, 0, -1, -1));
            False(WeaponGate(true, 0, 0, -2));
            False(WeaponGate(true, int.MinValue, int.MinValue, -1));
        }
    }
}
