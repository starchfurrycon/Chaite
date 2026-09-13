using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunAquaScepterRegressions()
        {
            Run(nameof(AquaScepterPinsNativeIdentityVariantsAndCadence),
                AquaScepterPinsNativeIdentityVariantsAndCadence);
            Run(nameof(AquaScepterDryPathUsesDelayedGravity),
                AquaScepterDryPathUsesDelayedGravity);
            Run(nameof(AquaScepterAimUsesThreeSubupdatesPerTick),
                AquaScepterAimUsesThreeSubupdatesPerTick);
            Run(nameof(AquaScepterRejectsLiveRouteDrift),
                AquaScepterRejectsLiveRouteDrift);
        }

        private static WeaponProfileInput AquaScepterInput(
            bool strongerVariant = false, float manaMultiplier = 1f)
        {
            return new WeaponProfileInput
            {
                WeaponId = 157,
                AmmoId = 0,
                ProjectileId = 22,
                ProjectileExtraUpdates = 2,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = 12.5f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = strongerVariant ? 90 : 27,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 0f,
                UseTime = strongerVariant ? 5 : 8,
                UseAnimation = strongerVariant ? 10 : 16,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = true,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = (int)(7 * manaMultiplier),
                ManaBaseCostKnown = true,
                ManaBaseCost = 7,
                ManaCostMultiplierKnown = true,
                ManaCostMultiplier = manaMultiplier
            };
        }

        private static void AquaScepterPinsNativeIdentityVariantsAndCadence()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(157, 0, out profile));
            Equal(new WeaponProfileKey(157, 0), profile.Key);
            Equal(22, profile.ProjectileId);
            Equal("aqua-scepter", profile.OutputRouteId);
            Equal(OutputRouteKind.StraightMagic, profile.OutputKind);
            Equal(OutputResourceKind.Mana, profile.ResourceKind);
            Equal(WeaponBallisticKind.DiscreteVerticalAccelerationPrimaryProjectile,
                profile.Ballistics);
            Equal(27, profile.DefaultWeaponDamage);
            NearWeapon(12.5f, profile.DefaultWeaponShootSpeed);
            Equal(8, profile.DefaultUseTime);
            Equal(16, profile.DefaultUseAnimation);
            Equal(7, profile.DefaultManaCost);
            Equal(2, profile.DefaultExtraUpdates);
            Equal(3600, profile.DefaultLifetimeSubupdates);
            Equal(100, AquaScepterCatalog.LastReviewedSubupdate);
            Equal(4, profile.VerticalAccelerationDelaySubupdates);
            NearWeapon(.15f, profile.VerticalAccelerationPerSubupdate);
            Equal(WeaponSecondaryEffect.PenetrationNotCredited,
                profile.SecondaryEffect);

            var normal = WeaponProfileCatalog.Evaluate(AquaScepterInput());
            True(normal.IsSupported, normal.Reason);
            Equal(27, normal.DirectDamage);
            NearWeapon(202.5f, normal.ApproximateDirectDps);
            NearWeapon(37.5f, normal.SpeedPixelsPerTick);
            NearWeapon(12.5f, normal.InitialSpeedPixelsPerSubupdate);
            Equal(3, normal.FirstTickProjectileUpdates);
            Equal(3, normal.SustainedProjectileUpdatesPerTick);
            NearWeapon(100f / 3f, normal.MaxFlightTicks);
            NearWeapon(1250f, normal.ConservativeRangePixels);

            // The Item.SetDefaults stronger-world branch changes damage and
            // 8/16 timing to 5/10, but leaves mana, speed, projectile, and
            // update cadence intact.
            var stronger = WeaponProfileCatalog.Evaluate(AquaScepterInput(
                true));
            True(stronger.IsSupported, stronger.Reason);
            Equal(90, stronger.DirectDamage);
            NearWeapon(1080f, stronger.ApproximateDirectDps);
            NearWeapon(100f / 3f, stronger.MaxFlightTicks);
            True(AquaScepterCatalog.IsKnownTiming(8, 16));
            True(AquaScepterCatalog.IsKnownTiming(5, 10));
            False(AquaScepterCatalog.IsKnownTiming(8, 10));
        }

        private static void AquaScepterDryPathUsesDelayedGravity()
        {
            var position = new Vec2();
            var velocity = new Vec2(12.5f, 0f);
            for (var update = 1; update <= 4; update++)
            {
                True(AquaScepterCatalog.TryAdvanceDryPath(ref position,
                    ref velocity, update));
            }
            NearWeapon(50f, position.X);
            NearWeapon(0f, position.Y);
            NearWeapon(0f, velocity.Y);

            // ai[0] is incremented in updates 1..4; update 5 is the first
            // movement using the +0.15 native Y acceleration.
            True(AquaScepterCatalog.TryAdvanceDryPath(ref position,
                ref velocity, 5));
            NearWeapon(62.5f, position.X);
            NearWeapon(.15f, position.Y);
            NearWeapon(.15f, velocity.Y);

            for (var update = 6; update <= 100; update++)
                True(AquaScepterCatalog.TryAdvanceDryPath(ref position,
                    ref velocity, update));
            NearWeapon(1250f, position.X, .001f);
            NearWeapon(698.4f, position.Y, .01f);
            NearWeapon(14.4f, velocity.Y, .001f);
            False(AquaScepterCatalog.TryAdvanceDryPath(ref position,
                ref velocity, 101));
        }

        private static void AquaScepterAimUsesThreeSubupdatesPerTick()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(AquaScepterInput());
            var target = new Vec2(250f, 0f);
            var shot = WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                new Vec2(), 90f);
            True(shot.CanFire, shot.Status.ToString());
            True(shot.AimWorld.Y < -0.01f,
                "Aqua trajectory should lead above a level target");
            var updates = shot.LeadTicks * 3d;
            var direction = shot.AimWorld.Normalized();
            var predicted = direction * (float)(12.5d * updates) +
                new Vec2(0f, (float)AquaGravityDisplacement(updates));
            NearWeapon(target.X, predicted.X, .02f);
            NearWeapon(target.Y, predicted.Y, .02f);

            Equal(WeaponAimStatus.BeyondPredictionHorizon,
                WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                    new Vec2(), 1f).Status);
            Equal(WeaponAimStatus.BeyondLifetime,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(2000f, 0f), new Vec2(), 90f).Status);
        }

        private static void AquaScepterRejectsLiveRouteDrift()
        {
            var input = AquaScepterInput();
            input.ProjectileId = 23;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = AquaScepterInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.ProjectileLifetimeSubupdates = 3599;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.WeaponShootSpeed = 12.6f;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.UseTime = 7;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.ManaBaseCostKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.ManaBaseCost = 8;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.ManaCostPerUse = 6;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.AmmoBaseDamage = 1;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.HasAmmo = false;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = AquaScepterInput();
            input.WeaponDamageAfterModifiers = 0;
            Equal(WeaponProfileStatus.InvalidDamage,
                WeaponProfileCatalog.Evaluate(input).Status);

            // The effective mana value is accepted only when it is the exact
            // native Single multiplication of the known raw cost.
            True(WeaponProfileCatalog.Evaluate(AquaScepterInput(false, .8f)).
                IsSupported);
        }

        private static double AquaGravityDisplacement(double updates)
        {
            if (updates <= 4d) return 0d;
            var whole = Math.Floor(updates);
            var fraction = updates - whole;
            var accelerated = Math.Max(0d, whole - 4d);
            return .15d * accelerated * (accelerated + 1d) * .5d +
                fraction * .15d * (accelerated + 1d);
        }
    }
}
