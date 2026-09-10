using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunWeaponProfileRegressions()
        {
            Run(nameof(WeaponCatalogUsesExactWeaponAmmoPairs), WeaponCatalogUsesExactWeaponAmmoPairs);
            Run(nameof(WeaponProfilesSeparateSourceAndBehaviorEvidence), WeaponProfilesSeparateSourceAndBehaviorEvidence);
            Run(nameof(MinisharkAmmoChangesSpeedDamageAndSecondaryEffect), MinisharkAmmoChangesSpeedDamageAndSecondaryEffect);
            Run(nameof(ClockworkBurstUsesNativeAnimationThresholds), ClockworkBurstUsesNativeAnimationThresholds);
            Run(nameof(ClockworkNewAnimationAccountsForNativeDecrement), ClockworkNewAnimationAccountsForNativeDecrement);
            Run(nameof(WeaponProfileUsesLiveSpeedSubupdatesAndLifetime), WeaponProfileUsesLiveSpeedSubupdatesAndLifetime);
            Run(nameof(WeaponProfileDamageUsesSeparateNativeAmmoRounding), WeaponProfileDamageUsesSeparateNativeAmmoRounding);
            Run(nameof(ClockworkDpsIncludesNativeReuseDelayNotShardGuesses), ClockworkDpsIncludesNativeReuseDelayNotShardGuesses);
            Run(nameof(WeaponProfilesFailClosedWithClearReasons), WeaponProfilesFailClosedWithClearReasons);
            Run(nameof(WeaponProfilesDistinguishAbsentAmmoFromUnsupportedPairs), WeaponProfilesDistinguishAbsentAmmoFromUnsupportedPairs);
            Run(nameof(WeaponAimRejectsImpossibleLifetimeAndLongHorizon), WeaponAimRejectsImpossibleLifetimeAndLongHorizon);
            Run(nameof(WeaponAimUsesPositivePhysicalInterceptAndSpread), WeaponAimUsesPositivePhysicalInterceptAndSpread);
            Run(nameof(WeaponProfileAndAimHotPathDoNotAllocate), WeaponProfileAndAimHotPathDoNotAllocate);
        }

        private static WeaponProfileInput ProfileInput(int weaponId = 98, int ammoId = 97)
        {
            // Explicit independent native defaults; do not derive test inputs from
            // the production catalog, which would hide a changed numeric table.
            return new WeaponProfileInput
            {
                WeaponId = weaponId, AmmoId = ammoId, ProjectileId = ammoId == 97 ? 14 : 89,
                ProjectileExtraUpdates = 1, ProjectileLifetimeSubupdates = 600,
                WeaponShootSpeed = weaponId == 98 ? 7f : 7.75f, AmmoShootSpeed = ammoId == 97 ? 4f : 5f,
                WeaponDamageAfterModifiers = weaponId == 98 ? 6 : 17,
                AmmoBaseDamage = ammoId == 97 ? 7 : 9, AmmoDamageMultiplier = 1f,
                UseTime = weaponId == 98 ? 8 : 4, UseAnimation = weaponId == 98 ? 8 : 12,
                ReuseDelay = weaponId == 98 ? 0 : 14, HasAmmo = true
            };
        }

        private static void WeaponCatalogUsesExactWeaponAmmoPairs()
        {
            Equal(4, WeaponProfileCatalog.Count);
            foreach (var weaponId in new[] { 98, 434 })
            foreach (var ammoId in new[] { 97, 515 })
            {
                WeaponProfile profile;
                True(WeaponProfileCatalog.TryGet(weaponId, ammoId, out profile));
                Equal(new WeaponProfileKey(weaponId, ammoId), profile.Key);
            }
            foreach (var key in new[] { new WeaponProfileKey(98, 278), new WeaponProfileKey(434, 1179),
                new WeaponProfileKey(533, 97), new WeaponProfileKey(98, 0), new WeaponProfileKey(-1, 97) })
            {
                WeaponProfile profile;
                False(WeaponProfileCatalog.TryGet(key.WeaponId, key.AmmoId, out profile));
                True(profile == null);
            }
            False(new WeaponProfileKey(98, 97).Equals(new WeaponProfileKey(97, 98)));
        }

        private static void WeaponProfilesSeparateSourceAndBehaviorEvidence()
        {
            foreach (var weaponId in new[] { 98, 434 })
            foreach (var ammoId in new[] { 97, 515 })
            {
                WeaponProfile profile;
                True(WeaponProfileCatalog.TryGet(weaponId, ammoId, out profile));
                True(profile.NativeSourceReviewed);
                Equal(weaponId == 98 && ammoId == 97 || weaponId == 434 && ammoId == 515,
                    profile.PairPreviouslyObservedInNativeEngine);
                False(profile.ThisSolverBehaviorVerifiedInNativeEngine);
            }
        }

        private static void MinisharkAmmoChangesSpeedDamageAndSecondaryEffect()
        {
            var musket = WeaponProfileCatalog.Evaluate(ProfileInput());
            var crystal = WeaponProfileCatalog.Evaluate(ProfileInput(98, 515));
            True(musket.IsSupported && crystal.IsSupported);
            Equal(22f, musket.SpeedPixelsPerTick);
            Equal(24f, crystal.SpeedPixelsPerTick);
            Equal(13, musket.DirectDamage);
            Equal(15, crystal.DirectDamage);
            Equal(300f, musket.MaxFlightTicks);
            Equal(WeaponSecondaryEffect.None, musket.Profile.SecondaryEffect);
            Equal(WeaponSecondaryEffect.CrystalShardsNotCredited, crystal.Profile.SecondaryEffect);
            NearWeapon(1.13137085f, musket.SpreadSpeedPixelsPerTick);
            True(musket.ConservativeRangePixels < 6600f && musket.ConservativeRangePixels > 6200f);
        }

        private static void ClockworkBurstUsesNativeAnimationThresholds()
        {
            foreach (var ammoId in new[] { 97, 515 })
            {
                var input = ProfileInput(434, ammoId);
                var baseSpeed = ammoId == 97 ? 23.5f : 25.5f;
                var animation = new[] { 11, 10, 9, 5, 4, 1 };
                var factors = new[] { 1f, 1f, 1.05f, 1.05f, 1.1f, 1.1f };
                var phases = new[] { 0, 0, 1, 1, 2, 2 };
                for (var index = 0; index < animation.Length; index++)
                {
                    input.AnimationRemainingAtShot = animation[index];
                    var evaluation = WeaponProfileCatalog.Evaluate(input);
                    True(evaluation.IsSupported);
                    NearWeapon(baseSpeed * factors[index], evaluation.SpeedPixelsPerTick);
                    Equal(phases[index], evaluation.BurstShotIndex);
                    var component = phases[index] == 0 ? 0f : phases[index] == 1 ? .2f : .4f;
                    NearWeapon(component * 2f * factors[index] * 1.4142135623730951f, evaluation.SpreadSpeedPixelsPerTick);
                }
            }
        }

        private static void ClockworkNewAnimationAccountsForNativeDecrement()
        {
            var input = ProfileInput(434, 515);
            Equal(0, WeaponProfileCatalog.Evaluate(input).BurstShotIndex);
            input.UseAnimation = 10;
            input.UseTime = 3;
            var nextBurst = WeaponProfileCatalog.Evaluate(input);
            Equal(1, nextBurst.BurstShotIndex); // Native starts at 10, decrements to 9 before Shoot.
            NearWeapon(26.775f, nextBurst.SpeedPixelsPerTick);
            input.AnimationRemainingAtShot = 10;
            Equal(0, WeaponProfileCatalog.Evaluate(input).BurstShotIndex); // Already-projected nonzero input is not decremented twice.
        }

        private static void WeaponProfileUsesLiveSpeedSubupdatesAndLifetime()
        {
            var input = ProfileInput();
            input.WeaponShootSpeed = 8f;
            input.AmmoShootSpeed = 5f;
            input.ProjectileExtraUpdates = 2;
            input.ProjectileLifetimeSubupdates = 450;
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);
            Equal(39f, evaluation.SpeedPixelsPerTick);
            Equal(150f, evaluation.MaxFlightTicks);
            input.AmmoId = 1179; // A sampled extraUpdates value never authorizes an unknown ammo behavior.
            Equal(WeaponProfileStatus.UnsupportedCombination, WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void WeaponProfileDamageUsesSeparateNativeAmmoRounding()
        {
            var input = ProfileInput();
            input.WeaponDamageAfterModifiers = 9;
            input.AmmoDamageMultiplier = 1.25f;
            Equal(17, WeaponProfileCatalog.Evaluate(input).DirectDamage); // 9 + floor(7 * 1.25), not floor((6 + 7) * 1.25).
            input.AmmoDamageMultiplier = float.NaN;
            Equal(WeaponProfileStatus.InvalidDamage, WeaponProfileCatalog.Evaluate(input).Status);
            input.AmmoDamageMultiplier = 1f;
            input.WeaponDamageAfterModifiers = int.MaxValue;
            Equal(WeaponProfileStatus.InvalidDamage, WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void ClockworkDpsIncludesNativeReuseDelayNotShardGuesses()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(ProfileInput(434, 515));
            Equal(26, evaluation.DirectDamage);
            NearWeapon(180f, evaluation.ApproximateDirectDps); // 26 direct * 3 shots * 60 / (12 + 14).
            True(evaluation.ApproximateDirectDps < 26f * 60f / 4f);
            Equal(WeaponFireMode.NativeAnimationBurst, evaluation.Profile.FireMode);
            NearWeapon(97.5f, WeaponProfileCatalog.Evaluate(ProfileInput()).ApproximateDirectDps);
        }

        private static void WeaponProfilesFailClosedWithClearReasons()
        {
            var input = ProfileInput();
            input.HasAmmo = false;
            Equal(WeaponProfileStatus.MissingAmmo, WeaponProfileCatalog.Evaluate(input).Status);
            input = ProfileInput(); input.ProjectileId = 242;
            Equal(WeaponProfileStatus.ProjectileMismatch, WeaponProfileCatalog.Evaluate(input).Status);
            foreach (var value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.MaxValue })
            {
                input = ProfileInput(); input.WeaponShootSpeed = value;
                var rejected = WeaponProfileCatalog.Evaluate(input);
                False(rejected.IsSupported);
                True(!string.IsNullOrEmpty(rejected.Reason));
                False(WeaponAimSolver.Solve(rejected, new Vec2(), new Vec2(100, 0), new Vec2()).CanFire);
            }
            input = ProfileInput(); input.ProjectileLifetimeSubupdates = 0;
            Equal(WeaponProfileStatus.InvalidBallistics, WeaponProfileCatalog.Evaluate(input).Status);
            input = ProfileInput(); input.ProjectileExtraUpdates = -1;
            Equal(WeaponProfileStatus.InvalidBallistics, WeaponProfileCatalog.Evaluate(input).Status);
            input = ProfileInput(); input.UseTime = 0;
            Equal(WeaponProfileStatus.InvalidTiming, WeaponProfileCatalog.Evaluate(input).Status);
            input = ProfileInput(); input.AnimationRemainingAtShot = -1;
            Equal(WeaponProfileStatus.InvalidTiming, WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void WeaponAimRejectsImpossibleLifetimeAndLongHorizon()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(ProfileInput());
            Equal(WeaponAimStatus.NoIntercept, WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(500, 0), new Vec2(30, 0)).Status);
            Equal(WeaponAimStatus.NoIntercept, WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(500, 0), new Vec2(22, 0)).Status);
            Equal(WeaponAimStatus.BeyondLifetime, WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(7000, 0), new Vec2(), 1000f).Status);
            Equal(WeaponAimStatus.BeyondPredictionHorizon, WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(2200, 0), new Vec2()).Status);
            Equal(WeaponAimStatus.UnsupportedWeapon, WeaponAimSolver.Solve(default(WeaponProfileEvaluation),
                new Vec2(), new Vec2(100, 0), new Vec2()).Status);
            Equal(WeaponAimStatus.InvalidInput, WeaponAimSolver.Solve(evaluation, new Vec2(float.NaN, 0),
                new Vec2(100, 0), new Vec2()).Status);
        }

        private static void WeaponProfilesDistinguishAbsentAmmoFromUnsupportedPairs()
        {
            foreach (var weaponId in new[] { 98, 434 })
            {
                var input = new WeaponProfileInput { WeaponId = weaponId, HasAmmo = false };
                var absent = WeaponProfileCatalog.Evaluate(input);
                Equal(WeaponProfileStatus.MissingAmmo, absent.Status);
                False(absent.IsSupported);
                True(absent.Profile == null); // Do not guess which ammo ran out.
                True(absent.Reason.Contains("没有可用弹药"));
                False(WeaponAimSolver.Solve(absent, new Vec2(), new Vec2(100, 0), new Vec2()).CanFire);
                input.HasAmmo = true;
                input.AmmoId = 1179;
                Equal(WeaponProfileStatus.UnsupportedCombination, WeaponProfileCatalog.Evaluate(input).Status);
            }
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(new WeaponProfileInput { WeaponId = 533, HasAmmo = false }).Status);
            Equal(WeaponProfileStatus.UnsupportedCombination, WeaponProfileCatalog.Evaluate(default(WeaponProfileInput)).Status);
        }

        private static void WeaponAimUsesPositivePhysicalInterceptAndSpread()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(ProfileInput());
            var aim = WeaponAimSolver.Solve(evaluation, new Vec2(), new Vec2(220, 0), new Vec2());
            True(aim.CanFire);
            NearWeapon(10f, aim.LeadTicks);
            NearWeapon(11.3137085f, aim.SpreadRadiusPixels);
            var origin = new Vec2(100, 200);
            var target = new Vec2(600, 400);
            var velocity = new Vec2(-5, 7);
            aim = WeaponAimSolver.Solve(evaluation, origin, target, velocity);
            True(aim.CanFire);
            NearWeapon(target.X + velocity.X * aim.LeadTicks, aim.AimWorld.X);
            NearWeapon(target.Y + velocity.Y * aim.LeadTicks, aim.AimWorld.Y);
            NearWeapon(evaluation.SpeedPixelsPerTick * aim.LeadTicks,
                (aim.AimWorld - origin).Length, .001f);
            True(WeaponAimSolver.Solve(evaluation, origin, origin, velocity).CanFire);
            NearWeapon(0f, WeaponAimSolver.Solve(evaluation, origin, origin, velocity).LeadTicks);
        }

        private static void WeaponProfileAndAimHotPathDoNotAllocate()
        {
            string allocationScope;
            var readAllocated = CreateAllocationCounter(out allocationScope);
            if (readAllocated == null) throw new InvalidOperationException("Cannot verify allocation contract: " + allocationScope);
            var input = ProfileInput(434, 515);
            var origin = new Vec2(100, 200);
            var target = new Vec2(600, 400);
            var velocity = new Vec2(-5, 7);
            var checksum = 0f;
            for (var warm = 0; warm < 1000; warm++)
                checksum += WeaponAimSolver.Solve(WeaponProfileCatalog.Evaluate(input), origin, target, velocity).LeadTicks;
            readAllocated();
            var before = readAllocated();
            for (var index = 0; index < 10000; index++)
            {
                input.AnimationRemainingAtShot = 1 + index % 11;
                var evaluation = WeaponProfileCatalog.Evaluate(input);
                var aim = WeaponAimSolver.Solve(evaluation, origin, target, velocity);
                checksum += aim.LeadTicks;
            }
            var allocated = readAllocated() - before;
            True(checksum > 0f);
            Equal(0L, allocated);
        }

        private static void NearWeapon(float expected, float actual, float tolerance = .0001f) =>
            True(Math.Abs(expected - actual) <= tolerance, "weapon numeric mismatch: expected " + expected + ", actual " + actual);
    }
}
