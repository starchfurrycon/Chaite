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
            Run(nameof(WeaponActionGateRejectsChangedNativeOutputIdentity), WeaponActionGateRejectsChangedNativeOutputIdentity);
            Run(nameof(WeaponActionGatePulsesEveryNonAutomaticUse),
                WeaponActionGatePulsesEveryNonAutomaticUse);
            Run(nameof(WeaponActionGateDefersOutputDuringQuickHealPulse),
                WeaponActionGateDefersOutputDuringQuickHealPulse);
        }

        private static bool WeaponGate(bool requested, int snapshotSlot, int actualSlot, int pendingSlot)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.WeaponActionGate", true);
            var method = type.GetMethod("ShouldFire", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null);
            return (bool)method.Invoke(null, new object[] { requested, snapshotSlot, actualSlot, pendingSlot });
        }

        private static bool ExactWeaponGate(int weapon, int actualWeapon,
            int ammo, int actualAmmo, int projectile, int actualProjectile)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType("Chaite.Plugin.WeaponActionGate", true);
            var method = type.GetMethod("ShouldFireExact", BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic);
            True(method != null);
            return (bool)method.Invoke(null, new object[] { true, 2, 2, -1,
                weapon, actualWeapon, ammo, actualAmmo, projectile,
                actualProjectile });
        }

        private static bool EmitWeaponUse(bool fire, bool visible,
            bool autoReuse, bool channel, bool releaseUseItem)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.WeaponActionGate", true);
            var method = type.GetMethod("ShouldEmitUseItem",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            True(method != null);
            return (bool)method.Invoke(null, new object[] { fire, visible,
                autoReuse, channel, releaseUseItem });
        }

        private static bool EmitWeaponUseUnlessQuickHeal(bool quickHeal,
            bool fire, bool visible, bool autoReuse, bool channel,
            bool releaseUseItem)
        {
            var type = typeof(Chaite.Plugin.Runtime).Assembly.GetType(
                "Chaite.Plugin.WeaponActionGate", true);
            var method = type.GetMethod("ShouldEmitUseItemUnlessQuickHeal",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            True(method != null);
            return (bool)method.Invoke(null, new object[] { quickHeal, fire,
                visible, autoReuse, channel, releaseUseItem });
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

        private static void WeaponActionGateRejectsChangedNativeOutputIdentity()
        {
            True(ExactWeaponGate(98, 98, 97, 97, 14, 14));
            False(ExactWeaponGate(98, 434, 97, 97, 14, 14));
            False(ExactWeaponGate(98, 98, 97, 515, 14, 14));
            False(ExactWeaponGate(98, 98, 97, 97, 14, 89));
            False(ExactWeaponGate(0, 0, 0, 0, 0, 0));
        }

        private static void WeaponActionGatePulsesEveryNonAutomaticUse()
        {
            // First native-ready frame presses; the disarmed frame must be a
            // real release; once vanilla re-arms, the next use can press.
            True(EmitWeaponUse(true, true, false, false, true));
            False(EmitWeaponUse(true, true, false, false, false));
            True(EmitWeaponUse(true, true, false, false, true));

            // Automatic and channelled routes remain held, but neither route
            // may bypass LOS or the exact weapon/ammo certificate.
            True(EmitWeaponUse(true, true, true, false, false));
            True(EmitWeaponUse(true, true, false, true, false));
            False(EmitWeaponUse(true, false, true, true, true));
            False(EmitWeaponUse(false, true, true, true, true));
        }

        private static void WeaponActionGateDefersOutputDuringQuickHealPulse()
        {
            // The health edge owns this native update even for an automatic or
            // channelled weapon.  Once the release latch is no longer armed,
            // the ordinary output gate remains unchanged.
            False(EmitWeaponUseUnlessQuickHeal(true, true, true, true,
                false, false));
            False(EmitWeaponUseUnlessQuickHeal(true, true, true, false,
                true, false));
            True(EmitWeaponUseUnlessQuickHeal(false, true, true, true,
                false, false));
            True(EmitWeaponUseUnlessQuickHeal(false, true, true, false,
                false, true));
            False(EmitWeaponUseUnlessQuickHeal(false, true, false, true,
                false, true));
        }
    }
}
