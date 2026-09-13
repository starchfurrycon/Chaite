using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunSnowballCannonRegressions()
        {
            Run(nameof(SnowballCannonPinsNativeIdentity),
                SnowballCannonPinsNativeIdentity);
            Run(nameof(SnowballCannonAcceptsNormalAndStrongerVariants),
                SnowballCannonAcceptsNormalAndStrongerVariants);
            Run(nameof(SnowballCannonRejectsPostPrefixState),
                SnowballCannonRejectsPostPrefixState);
            Run(nameof(SnowballCannonAimHonorsPrefixLifetime),
                SnowballCannonAimHonorsPrefixLifetime);
            Run(nameof(SnowballCannonBuildsRangedOutputRoute),
                SnowballCannonBuildsRangedOutputRoute);
        }

        private static WeaponProfileInput SnowballInput(bool stronger = false)
        {
            return new WeaponProfileInput
            {
                WeaponId = SnowballCannonCatalog.WeaponId,
                AmmoId = SnowballCannonCatalog.AmmoId,
                ProjectileId = SnowballCannonCatalog.ProjectileId,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = 11f,
                AmmoShootSpeed = 7f,
                WeaponDamageAfterModifiers = stronger ? 22 : 10,
                AmmoBaseDamage = 8,
                AmmoDamageMultiplier = 1f,
                UseTime = stronger ? 6 : 19,
                UseAnimation = stronger ? 6 : 19,
                ReuseDelay = 0,
                AutoReuse = true,
                HasAmmo = true
            };
        }

        private static void SnowballCannonPinsNativeIdentity()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(
                SnowballCannonCatalog.WeaponId,
                SnowballCannonCatalog.AmmoId, out profile));
            Equal(new WeaponProfileKey(1319, 949), profile.Key);
            Equal(166, profile.ProjectileId);
            Equal("snowball-cannon-snowball", profile.OutputRouteId);
            Equal(OutputRouteKind.StraightRanged, profile.OutputKind);
            Equal(OutputResourceKind.Ammunition, profile.ResourceKind);
            Equal(WeaponBallisticKind.ConservativeStraightPrefix,
                profile.Ballistics);
            Equal(10, profile.DefaultWeaponDamage);
            Equal(8, profile.DefaultAmmoDamage);
            Equal(11f, profile.DefaultWeaponShootSpeed);
            Equal(7f, profile.DefaultAmmoShootSpeed);
            Equal(19, profile.DefaultUseTime);
            Equal(19, profile.DefaultUseAnimation);
            Equal(3600, profile.DefaultLifetimeSubupdates);
            Equal(0, profile.DefaultExtraUpdates);
            // The native prefix limit is intentionally an internal catalog
            // detail.  Verify its public contract through the evaluated
            // lifetime, which is clamped to the 19-update damaging prefix.
            var evaluated = WeaponProfileCatalog.Evaluate(SnowballInput());
            True(evaluated.IsSupported, evaluated.Reason);
            SnowballNear(SnowballCannonCatalog.PrefixUpdateLimit,
                evaluated.MaxFlightTicks);
            SnowballNear(SnowballCannonCatalog.ComponentSpread,
                profile.ComponentSpreadForTests());
        }

        private static void SnowballCannonAcceptsNormalAndStrongerVariants()
        {
            var normal = WeaponProfileCatalog.Evaluate(SnowballInput());
            True(normal.IsSupported, normal.Reason);
            Equal(18, normal.DirectDamage);
            SnowballNear(18f, normal.SpeedPixelsPerTick);
            SnowballNear(19f, normal.MaxFlightTicks);
            SnowballNear(18f * 60f / 19f, normal.ApproximateDirectDps);

            var stronger = WeaponProfileCatalog.Evaluate(
                SnowballInput(true));
            True(stronger.IsSupported, stronger.Reason);
            Equal(30, stronger.DirectDamage);
            SnowballNear(30f * 60f / 6f,
                stronger.ApproximateDirectDps);
            Equal(19f, stronger.MaxFlightTicks);
        }

        private static void SnowballCannonRejectsPostPrefixState()
        {
            var input = SnowballInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SnowballInput();
            input.ProjectileLifetimeSubupdates = 18;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SnowballInput();
            input.ProjectileId = 3;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SnowballInput();
            input.AmmoId = 97;
            input.ProjectileId = 14;
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SnowballInput();
            input.HasAmmo = false;
            input.AmmoId = 0;
            Equal(WeaponProfileStatus.MissingAmmo,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void SnowballCannonAimHonorsPrefixLifetime()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(SnowballInput());
            var aim = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(342f, 0f), new Vec2());
            True(aim.CanFire, aim.Status.ToString());
            SnowballNear(19f, aim.LeadTicks);
            SnowballNear(342f, aim.AimWorld.X);

            aim = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(343f, 0f), new Vec2());
            Equal(WeaponAimStatus.BeyondLifetime, aim.Status);
        }

        private static void SnowballCannonBuildsRangedOutputRoute()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(SnowballInput());
            var snapshot = new CombatSnapshot
            {
                Weapon = new WeaponSnapshot
                {
                    Slot = 1,
                    Damage = evaluation.DirectDamage,
                    UseTime = 19,
                    ShootSpeed = evaluation.SpeedPixelsPerTick,
                    IsProjectile = true,
                    HasAmmo = true,
                    IsUsable = true,
                    NativeProfileRequired = true,
                    WeaponId = 1319,
                    AmmoId = 949,
                    ProjectileId = 166,
                    Profile = evaluation
                }
            };
            OutputRouteProfile route;
            string reason;
            True(OutputRouteContract.TryCreateReady(snapshot,
                out route, out reason), reason);
            Equal("snowball-cannon-snowball", route.Id);
            Equal(OutputRouteKind.StraightRanged, route.Kind);
            Equal(OutputResourceKind.Ammunition, route.Resource);
            Equal(1, route.WeaponSlot);
            Equal(1319, route.WeaponId);
            Equal(949, route.AmmoId);
            Equal(166, route.ProjectileId);
        }

        private static void SnowballNear(float expected, float actual,
            float tolerance = .0001f) =>
            True(Math.Abs(expected - actual) <= tolerance,
                "snowball numeric mismatch: expected " + expected +
                ", actual " + actual);
    }

    // The profile's spread field is intentionally internal. Keep the test
    // source-independent by exposing only a tiny assertion helper through an
    // internal extension in this test assembly.
    internal static class SnowballProfileTestExtensions
    {
        public static float ComponentSpreadForTests(this WeaponProfile profile)
        {
            // The public evaluation exposes the equivalent worst-case speed
            // error; infer the component envelope for the default speed.
            var input = new WeaponProfileInput
            {
                WeaponId = 1319,
                AmmoId = 949,
                ProjectileId = 166,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = 11f,
                AmmoShootSpeed = 7f,
                WeaponDamageAfterModifiers = 10,
                AmmoBaseDamage = 8,
                AmmoDamageMultiplier = 1f,
                UseTime = 19,
                UseAnimation = 19,
                AutoReuse = true,
                HasAmmo = true
            };
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            return evaluation.SpreadSpeedPixelsPerSubupdate /
                1.4142135623730951f;
        }
    }
}
