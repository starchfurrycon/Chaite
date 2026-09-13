using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunDemonScytheRegressions()
        {
            Run(nameof(DemonScythePinsNativeIdentityAndCadence),
                DemonScythePinsNativeIdentityAndCadence);
            Run(nameof(DemonScytheMovementMatchesPreMovementAcceleration),
                DemonScytheMovementMatchesPreMovementAcceleration);
            Run(nameof(DemonScytheAimUsesPiecewiseNativePath),
                DemonScytheAimUsesPiecewiseNativePath);
            Run(nameof(DemonScytheRejectsLiveRouteDrift),
                DemonScytheRejectsLiveRouteDrift);
        }

        private static WeaponProfileInput DemonScytheInput()
        {
            // Independent Terraria 1.4.5.8 Item.SetDefaults / Projectile
            // defaults, not values borrowed from the production catalog.
            return new WeaponProfileInput
            {
                WeaponId = 272,
                AmmoId = 0,
                ProjectileId = 45,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = .2f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = 35,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 0f,
                UseTime = 20,
                UseAnimation = 20,
                ReuseDelay = 0,
                AutoReuse = false,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = 14
            };
        }

        private static void DemonScythePinsNativeIdentityAndCadence()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(272, 0, out profile));
            Equal(new WeaponProfileKey(272, 0), profile.Key);
            Equal(45, profile.ProjectileId);
            Equal("demon-scythe", profile.OutputRouteId);
            Equal(OutputRouteKind.StraightMagic, profile.OutputKind);
            Equal(OutputResourceKind.Mana, profile.ResourceKind);
            Equal(WeaponBallisticKind.DemonScytheAcceleration,
                profile.Ballistics);
            Equal(35, profile.DefaultWeaponDamage);
            NearWeapon(.2f, profile.DefaultWeaponShootSpeed);
            Equal(20, profile.DefaultUseTime);
            Equal(20, profile.DefaultUseAnimation);
            Equal(0, profile.DefaultReuseDelay);
            Equal(false, profile.DefaultAutoReuse);
            Equal(14, profile.DefaultManaCost);
            Equal(0, profile.DefaultExtraUpdates);
            Equal(3600, profile.DefaultLifetimeSubupdates);
            Equal(WeaponSecondaryEffect.PenetrationNotCredited,
                profile.SecondaryEffect);

            var evaluation = WeaponProfileCatalog.Evaluate(
                DemonScytheInput());
            True(evaluation.IsSupported, evaluation.Reason);
            Equal(35, evaluation.DirectDamage);
            NearWeapon(.2f, evaluation.SpeedPixelsPerTick);
            NearWeapon(3600f, evaluation.MaxFlightTicks);
            NearWeapon(100f, evaluation.ApproximateDirectDps);
            NearWeapon((float)(.2d *
                DemonScytheCatalog.MovementFactor(3600d)),
                evaluation.ConservativeRangePixels, .01f);
            Equal(45, WeaponProfileCatalog.ResolveProjectileForPair(272,
                0, 45));
        }

        private static void DemonScytheMovementMatchesPreMovementAcceleration()
        {
            // ai[0] is incremented before the velocity branch, so 30 is the
            // first multiplied movement—not 29—and 99 is the 70th multiplier.
            NearDouble(0d, DemonScytheCatalog.MovementFactor(0d));
            NearDouble(29d, DemonScytheCatalog.MovementFactor(29d));
            NearDouble(30.059999942779541d,
                DemonScytheCatalog.MovementFactor(30d));
            NearDouble(31.183599821472171d,
                DemonScytheCatalog.MovementFactor(31d));
            NearDouble(1055.0050791811743d,
                DemonScytheCatalog.MovementFactor(99d), .000001d);
            // Update 100 only freezes ai[0] to 200; it travels at the final
            // 1.06^70 multiplier and does not receive an extra acceleration.
            NearDouble(1114.0807861297662d,
                DemonScytheCatalog.MovementFactor(100d), .000001d);
            NearDouble(29.52999997138977d,
                DemonScytheCatalog.MovementFactor(29.5d));
            True(double.IsNaN(DemonScytheCatalog.MovementFactor(
                double.NaN)));
        }

        private static void DemonScytheAimUsesPiecewiseNativePath()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(
                DemonScytheInput());
            var stationary = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(100f, 0f), new Vec2(), 90f);
            True(stationary.CanFire, stationary.Status.ToString());
            NearWeapon(85.97662f, stationary.LeadTicks, .0001f);
            NearWeapon(100f, (float)(.2d *
                DemonScytheCatalog.MovementFactor(stationary.LeadTicks)),
                .0002f);

            // Construct a moving target that lies on the reviewed horizontal
            // projectile path at tick 80. The solution must recover that
            // native segment rather than apply a constant-speed intercept.
            var arrival = (float)(.2d * DemonScytheCatalog.
                MovementFactor(80d));
            var target = new Vec2(arrival - 80f, 20f);
            var velocity = new Vec2(1f, -.25f);
            var moving = WeaponAimSolver.Solve(evaluation, new Vec2(),
                target, velocity, 90f);
            True(moving.CanFire, moving.Status.ToString());
            NearWeapon(80f, moving.LeadTicks, .0001f);
            NearWeapon(arrival, moving.AimWorld.X, .0002f);
            NearWeapon(0f, moving.AimWorld.Y, .0002f);

            Equal(WeaponAimStatus.BeyondPredictionHorizon,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(500f, 0f), new Vec2(), 90f).Status);
        }

        private static void DemonScytheRejectsLiveRouteDrift()
        {
            var input = DemonScytheInput();
            input.ProjectileId = 44;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = DemonScytheInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DemonScytheInput();
            input.ProjectileLifetimeSubupdates = 3599;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DemonScytheInput();
            input.WeaponShootSpeed = .21f;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DemonScytheInput();
            input.AutoReuse = true;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DemonScytheInput();
            input.ManaCostKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DemonScytheInput();
            input.AmmoBaseDamage = 1;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void NearDouble(double expected, double actual,
            double tolerance = .0000001d)
        {
            True(Math.Abs(expected - actual) <= tolerance,
                "Demon Scythe numeric mismatch: expected " + expected +
                ", actual " + actual);
        }
    }
}
