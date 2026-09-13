using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunSimpleMagicPrefixRegressions()
        {
            Run(nameof(SimpleMagicPrefixPinsNativeProfiles),
                SimpleMagicPrefixPinsNativeProfiles);
            Run(nameof(SimpleMagicPrefixEvaluationUsesBoundedLifetime),
                SimpleMagicPrefixEvaluationUsesBoundedLifetime);
            Run(nameof(SimpleMagicPrefixRejectsIdentityAndTimingDrift),
                SimpleMagicPrefixRejectsIdentityAndTimingDrift);
            Run(nameof(SimpleMagicPrefixRejectsResourceAndAmmoDrift),
                SimpleMagicPrefixRejectsResourceAndAmmoDrift);
            Run(nameof(SimpleMagicPrefixAimStopsAtReviewedPrefix),
                SimpleMagicPrefixAimStopsAtReviewedPrefix);
        }

        private static WeaponProfileInput SimpleMagicInput(int weaponId)
        {
            SimpleMagicPrefixProfile prefix;
            True(SimpleMagicPrefixCatalog.TryGet(weaponId, out prefix));
            return new WeaponProfileInput
            {
                WeaponId = prefix.WeaponId,
                AmmoId = 0,
                ProjectileId = prefix.ProjectileId,
                ProjectileExtraUpdates = prefix.ExtraUpdates,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = prefix.ShootSpeed,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = prefix.Damage,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 0f,
                UseTime = prefix.UseTime,
                UseAnimation = prefix.UseAnimation,
                ReuseDelay = prefix.ReuseDelay,
                AnimationRemainingAtShot = 0,
                AutoReuse = prefix.AutoReuse,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = prefix.ManaCost
            };
        }

        private static void SimpleMagicPrefixPinsNativeProfiles()
        {
            SimpleMagicPrefixProfile flower;
            True(SimpleMagicPrefixCatalog.TryGet(
                SimpleMagicPrefixCatalog.FlowerOfFireWeaponId, out flower));
            Equal(112, flower.WeaponId);
            Equal(15, flower.ProjectileId);
            Equal(48, flower.Damage);
            Equal(9, flower.ManaCost);
            Equal(16, flower.UseTime);
            Equal(16, flower.UseAnimation);
            Equal(0, flower.ReuseDelay);
            Equal(0, flower.ExtraUpdates);
            Equal(19, flower.PrefixUpdateLimit);
            Equal(7.5f, flower.ShootSpeed);
            Equal(false, flower.AutoReuse);
            True(flower.IsKnown);

            SimpleMagicPrefixProfile cursed;
            True(SimpleMagicPrefixCatalog.TryGet(
                SimpleMagicPrefixCatalog.CursedFlamesWeaponId, out cursed));
            Equal(519, cursed.WeaponId);
            Equal(95, cursed.ProjectileId);
            Equal(55, cursed.Damage);
            Equal(9, cursed.ManaCost);
            Equal(15, cursed.UseTime);
            Equal(15, cursed.UseAnimation);
            Equal(0, cursed.ReuseDelay);
            Equal(0, cursed.ExtraUpdates);
            Equal(19, cursed.PrefixUpdateLimit);
            Equal(10f, cursed.ShootSpeed);
            Equal(true, cursed.AutoReuse);
            True(cursed.IsKnown);

            SimpleMagicPrefixProfile razorpine;
            True(SimpleMagicPrefixCatalog.TryGet(
                SimpleMagicPrefixCatalog.RazorpineWeaponId, out razorpine));
            Equal(1930, razorpine.WeaponId);
            Equal(336, razorpine.ProjectileId);
            Equal(48, razorpine.Damage);
            Equal(5, razorpine.ManaCost);
            Equal(8, razorpine.UseTime);
            Equal(8, razorpine.UseAnimation);
            Equal(0, razorpine.ReuseDelay);
            Equal(1, razorpine.ExtraUpdates);
            Equal(49, razorpine.PrefixUpdateLimit);
            Equal(12f, razorpine.ShootSpeed);
            Equal(true, razorpine.AutoReuse);
            True(razorpine.IsKnown);

            SimpleMagicPrefixProfile frost;
            True(SimpleMagicPrefixCatalog.TryGet(
                SimpleMagicPrefixCatalog.FlowerOfFrostWeaponId, out frost));
            Equal(1264, frost.WeaponId);
            Equal(253, frost.ProjectileId);
            Equal(60, frost.Damage);
            Equal(11, frost.ManaCost);
            Equal(12, frost.UseTime);
            Equal(12, frost.UseAnimation);
            Equal(0, frost.ReuseDelay);
            Equal(0, frost.ExtraUpdates);
            Equal(19, frost.PrefixUpdateLimit);
            Equal(9f, frost.ShootSpeed);
            Equal(false, frost.AutoReuse);
            True(frost.IsKnown);
            False(SimpleMagicPrefixCatalog.TryGet(113, out razorpine));
            False(SimpleMagicPrefixCatalog.IsPrefixWeapon(113));
        }

        private static void SimpleMagicPrefixEvaluationUsesBoundedLifetime()
        {
            var flower = WeaponProfileCatalog.Evaluate(SimpleMagicInput(112));
            True(flower.IsSupported, flower.Reason);
            Equal(WeaponBallisticKind.ConservativeStraightPrefix,
                flower.Profile.Ballistics);
            Equal(15, flower.Profile.ProjectileId);
            NearWeapon(7.5f, flower.SpeedPixelsPerTick);
            NearWeapon(19f, flower.MaxFlightTicks);
            NearWeapon(48f, flower.DirectDamage);

            var cursed = WeaponProfileCatalog.Evaluate(
                SimpleMagicInput(519));
            True(cursed.IsSupported, cursed.Reason);
            Equal(WeaponBallisticKind.ConservativeStraightPrefix,
                cursed.Profile.Ballistics);
            Equal(95, cursed.Profile.ProjectileId);
            NearWeapon(10f, cursed.SpeedPixelsPerTick);
            NearWeapon(19f, cursed.MaxFlightTicks);
            NearWeapon(55f, cursed.DirectDamage);

            var frost = WeaponProfileCatalog.Evaluate(
                SimpleMagicInput(1264));
            True(frost.IsSupported, frost.Reason);
            Equal(WeaponBallisticKind.ConservativeStraightPrefix,
                frost.Profile.Ballistics);
            Equal(253, frost.Profile.ProjectileId);
            NearWeapon(9f, frost.SpeedPixelsPerTick);
            NearWeapon(19f, frost.MaxFlightTicks);
            NearWeapon(60f, frost.DirectDamage);

            var razorpine = WeaponProfileCatalog.Evaluate(
                SimpleMagicInput(1930));
            True(razorpine.IsSupported, razorpine.Reason);
            Equal(WeaponBallisticKind.ConservativeStraightPrefix,
                razorpine.Profile.Ballistics);
            Equal(336, razorpine.Profile.ProjectileId);
            NearWeapon(24f, razorpine.SpeedPixelsPerTick);
            // 49 native subupdates at two updates per game tick: one complete
            // first tick plus 47/2 of the remaining cadence.
            NearWeapon(24.5f, razorpine.MaxFlightTicks);
            NearWeapon(48f, razorpine.DirectDamage);
        }

        private static void SimpleMagicPrefixRejectsIdentityAndTimingDrift()
        {
            var input = SimpleMagicInput(112);
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);

            input.ProjectileId = 16;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(1930);
            input.ProjectileExtraUpdates = 0;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(112);
            input.ProjectileLifetimeSubupdates = 18;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = SimpleMagicInput(1930);
            input.ProjectileLifetimeSubupdates = 48;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(519);
            input.ProjectileLifetimeSubupdates = 18;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(1264);
            input.ProjectileLifetimeSubupdates = 18;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(112);
            input.WeaponShootSpeed = 7.6f;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = SimpleMagicInput(112);
            input.UseAnimation = 17;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = SimpleMagicInput(1930);
            input.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = SimpleMagicInput(519);
            input.WeaponShootSpeed = 10.1f;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(1264);
            input.ManaCostPerUse = 10;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void SimpleMagicPrefixRejectsResourceAndAmmoDrift()
        {
            var input = SimpleMagicInput(112);
            input.ManaCostKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(1930);
            input.ManaCostPerUse = 4;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = SimpleMagicInput(112);
            input.AmmoId = 97;
            // The pair is not in the magic catalog at all.  Catalog lookup
            // intentionally precedes live projectile validation so an
            // impossible weapon/ammo identity is reported distinctly.
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = SimpleMagicInput(112);
            input.AmmoBaseDamage = 1;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = SimpleMagicInput(112);
            input.HasAmmo = false;
            // Magic routes do not consume picked ammunition; the production
            // adapter represents that branch with HasAmmo=true. A false
            // value is therefore an identity/branch mismatch, not an empty
            // ammo stack.
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(112, 0, out profile));
            var direct = SimpleMagicInput(112);
            direct.WeaponDamageAfterModifiers = 0;
            WeaponProfileStatus failure;
            False(SimpleMagicPrefixCatalog.TryValidate(direct, profile,
                out failure));
            Equal(WeaponProfileStatus.InvalidDamage, failure);
        }

        private static void SimpleMagicPrefixAimStopsAtReviewedPrefix()
        {
            var flower = WeaponProfileCatalog.Evaluate(SimpleMagicInput(112));
            var inPrefix = WeaponAimSolver.Solve(flower, new Vec2(0, 0),
                new Vec2(100, 0), new Vec2(0, 0));
            True(inPrefix.CanFire, inPrefix.Status.ToString());
            NearWeapon(100f / 7.5f, inPrefix.LeadTicks);

            var flowerBeyond = WeaponAimSolver.Solve(flower,
                new Vec2(0, 0), new Vec2(160, 0), new Vec2(0, 0));
            Equal(WeaponAimStatus.BeyondLifetime, flowerBeyond.Status);

            var cursed = WeaponProfileCatalog.Evaluate(SimpleMagicInput(519));
            var cursedInPrefix = WeaponAimSolver.Solve(cursed,
                new Vec2(0, 0), new Vec2(180, 0), new Vec2(0, 0));
            True(cursedInPrefix.CanFire, cursedInPrefix.Status.ToString());
            NearWeapon(18f, cursedInPrefix.LeadTicks);
            var cursedBeyond = WeaponAimSolver.Solve(cursed,
                new Vec2(0, 0), new Vec2(200, 0), new Vec2(0, 0));
            Equal(WeaponAimStatus.BeyondLifetime, cursedBeyond.Status);

            var frost = WeaponProfileCatalog.Evaluate(SimpleMagicInput(1264));
            var frostInPrefix = WeaponAimSolver.Solve(frost,
                new Vec2(0, 0), new Vec2(171, 0), new Vec2(0, 0));
            True(frostInPrefix.CanFire, frostInPrefix.Status.ToString());
            NearWeapon(19f, frostInPrefix.LeadTicks);
            var frostBeyond = WeaponAimSolver.Solve(frost,
                new Vec2(0, 0), new Vec2(180, 0), new Vec2(0, 0));
            Equal(WeaponAimStatus.BeyondLifetime, frostBeyond.Status);

            var razorpine = WeaponProfileCatalog.Evaluate(
                SimpleMagicInput(1930));
            var razorInPrefix = WeaponAimSolver.Solve(razorpine,
                new Vec2(0, 0), new Vec2(480, 0), new Vec2(0, 0));
            True(razorInPrefix.CanFire, razorInPrefix.Status.ToString());
            NearWeapon(20f, razorInPrefix.LeadTicks);
            var razorBeyond = WeaponAimSolver.Solve(razorpine,
                new Vec2(0, 0), new Vec2(700, 0), new Vec2(0, 0));
            Equal(WeaponAimStatus.BeyondLifetime, razorBeyond.Status);
        }
    }
}
