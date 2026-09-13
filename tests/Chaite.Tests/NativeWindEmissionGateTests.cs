using Chaite.Core;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunNativeWindEmissionGateRegressions()
        {
            Run(nameof(NativeWindGateAdmitsEverySpecifiedOutputOnlyWhenOff),
                NativeWindGateAdmitsEverySpecifiedOutputOnlyWhenOff);
            Run(nameof(NativeWindGateFailsClosedForUnknownOrEnabledState),
                NativeWindGateFailsClosedForUnknownOrEnabledState);
            Run(nameof(NativeWindGateRejectsUnspecifiedAndFutureRouteKinds),
                NativeWindGateRejectsUnspecifiedAndFutureRouteKinds);
        }

        private static void NativeWindGateAdmitsEverySpecifiedOutputOnlyWhenOff()
        {
            foreach (var kind in new[]
            {
                OutputRouteKind.StraightRanged,
                OutputRouteKind.StraightMagic,
                OutputRouteKind.MinionAndWhip,
                OutputRouteKind.MeleeProjectile,
                OutputRouteKind.DirectedMeleeWave,
                OutputRouteKind.ConvergingRangedBurst,
                OutputRouteKind.HomingMagicProjectile
            })
            {
                True(NativeWindEmissionGate.IsSpecifiedOutputRoute(kind));
                True(NativeWindEmissionGate.PermitsSpecifiedOutput(kind,
                    true, false));
            }
        }

        private static void NativeWindGateFailsClosedForUnknownOrEnabledState()
        {
            foreach (var kind in new[]
            {
                OutputRouteKind.StraightRanged,
                OutputRouteKind.StraightMagic,
                OutputRouteKind.MinionAndWhip,
                OutputRouteKind.MeleeProjectile,
                OutputRouteKind.DirectedMeleeWave,
                OutputRouteKind.ConvergingRangedBurst,
                OutputRouteKind.HomingMagicProjectile
            })
            {
                False(NativeWindEmissionGate.PermitsSpecifiedOutput(kind,
                    false, false));
                False(NativeWindEmissionGate.PermitsSpecifiedOutput(kind,
                    true, true));
            }
        }

        private static void NativeWindGateRejectsUnspecifiedAndFutureRouteKinds()
        {
            False(NativeWindEmissionGate.IsSpecifiedOutputRoute(
                OutputRouteKind.Unspecified));
            False(NativeWindEmissionGate.PermitsSpecifiedOutput(
                OutputRouteKind.Unspecified, true, false));
            False(NativeWindEmissionGate.IsSpecifiedOutputRoute(
                (OutputRouteKind)999));
            False(NativeWindEmissionGate.PermitsSpecifiedOutput(
                (OutputRouteKind)999, true, false));
        }
    }
}
