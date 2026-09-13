using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunDiamondStaffRegressions()
        {
            Run(nameof(DiamondStaffProfilePinsNativeIdentity),
                DiamondStaffProfilePinsNativeIdentity);
            Run(nameof(DiamondStaffRequiresStableGemStaffFeatureState),
                DiamondStaffRequiresStableGemStaffFeatureState);
            Run(nameof(DiamondStaffRejectsIdentityAndResourceDrift),
                DiamondStaffRejectsIdentityAndResourceDrift);
            Run(nameof(DiamondStaffBuildsStraightMagicOutputRoute),
                DiamondStaffBuildsStraightMagicOutputRoute);
        }

        private static WeaponProfileInput DiamondStaffInput(int armorType = 0)
        {
            return new WeaponProfileInput
            {
                WeaponId = 744,
                AmmoId = 0,
                ProjectileId = 126,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 300,
                WeaponShootSpeed = 9.5f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = 23,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 1f,
                UseTime = 26,
                UseAnimation = 26,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = true,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = 9,
                GemStaffFeatureStateKnown = true,
                GemStaffEffectiveArmorType = armorType
            };
        }

        private static void DiamondStaffProfilePinsNativeIdentity()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(744, 0, out profile));
            Equal(new WeaponProfileKey(744, 0), profile.Key);
            Equal(126, profile.ProjectileId);
            Equal("diamond-staff", profile.OutputRouteId);
            Equal(OutputRouteKind.StraightMagic, profile.OutputKind);
            Equal(OutputResourceKind.Mana, profile.ResourceKind);
            Equal(WeaponBallisticKind.StraightPrimaryProjectile,
                profile.Ballistics);
            Equal(23, profile.DefaultWeaponDamage);
            Equal(9.5f, profile.DefaultWeaponShootSpeed);
            Equal(26, profile.DefaultUseTime);
            Equal(26, profile.DefaultUseAnimation);
            Equal(0, profile.DefaultReuseDelay);
            Equal(true, profile.DefaultAutoReuse);
            Equal(9, profile.DefaultManaCost);
            Equal(0, profile.DefaultExtraUpdates);
            // ApplyGemStaffStats sets timeLeft=300; the BiggerHitbox feature
            // changes penetration, not lifetime, in FinalizeProjectile.
            Equal(300, profile.DefaultLifetimeSubupdates);
            Equal(WeaponSecondaryEffect.None, profile.SecondaryEffect);
        }

        private static void DiamondStaffRequiresStableGemStaffFeatureState()
        {
            var unknown = DiamondStaffInput();
            unknown.GemStaffFeatureStateKnown = false;
            Equal(WeaponProfileStatus.InvalidProjectileFeatures,
                WeaponProfileCatalog.Evaluate(unknown).Status);

            // Each body armor identity is a native PackGemStaffFeatures branch:
            // homing, AOE, swirl, fast/slow, bounce, armor-piercing spread,
            // or BiggerHitbox+RepeatsGem respectively.
            foreach (var armorType in new[] { 1282, 1283, 1284, 1285,
                1286, 1287, 4256 })
            {
                var input = DiamondStaffInput(armorType);
                Equal(WeaponProfileStatus.InvalidProjectileFeatures,
                    WeaponProfileCatalog.Evaluate(input).Status);
            }

            True(WeaponProfileCatalog.Evaluate(DiamondStaffInput(0)).IsSupported);
            True(WeaponProfileCatalog.Evaluate(DiamondStaffInput(1000)).IsSupported);
            var negative = DiamondStaffInput(-1);
            negative.GemStaffFeatureStateKnown = true;
            Equal(WeaponProfileStatus.InvalidProjectileFeatures,
                WeaponProfileCatalog.Evaluate(negative).Status);
        }

        private static void DiamondStaffRejectsIdentityAndResourceDrift()
        {
            var input = DiamondStaffInput();
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);
            NearWeapon(9.5f, evaluation.SpeedPixelsPerTick);
            NearWeapon(300f, evaluation.MaxFlightTicks);
            NearWeapon(23f, evaluation.DirectDamage);
            NearWeapon(23f * 60f / 26f,
                evaluation.ApproximateDirectDps);

            input.ProjectileId = 121;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DiamondStaffInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DiamondStaffInput();
            input.ProjectileLifetimeSubupdates = 301;
            True(WeaponProfileCatalog.Evaluate(input).IsSupported,
                "live lifetime may differ while remaining within the native limit");
            input.ManaCostKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = DiamondStaffInput();
            input.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void DiamondStaffBuildsStraightMagicOutputRoute()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(
                DiamondStaffInput());
            var snapshot = new CombatSnapshot
            {
                Weapon = new WeaponSnapshot
                {
                    Slot = 2,
                    Damage = evaluation.DirectDamage,
                    UseTime = 26,
                    ShootSpeed = evaluation.SpeedPixelsPerTick,
                    IsProjectile = true,
                    HasAmmo = true,
                    IsUsable = true,
                    NativeProfileRequired = true,
                    WeaponId = 744,
                    AmmoId = 0,
                    ProjectileId = 126,
                    Profile = evaluation,
                    Mana = new ManaOutputState
                    {
                        Known = true,
                        CurrentMana = 100,
                        MaximumMana = 100,
                        ManaCostPerUse = 9,
                        RegenerationDelay = 0f,
                        RegenerationCount = 0,
                        RegenerationRate = 0,
                        PotionDelay = 0,
                        ManaSicknessKnown = true,
                        ManaSicknessReduction = 0f
                    }
                }
            };
            OutputRouteProfile route;
            string reason;
            True(OutputRouteContract.TryCreateReady(snapshot,
                out route, out reason), reason);
            Equal("diamond-staff", route.Id);
            Equal(OutputRouteKind.StraightMagic, route.Kind);
            Equal(OutputResourceKind.Mana, route.Resource);
            Equal(2, route.WeaponSlot);
            Equal(744, route.WeaponId);
            Equal(126, route.ProjectileId);
        }
    }
}
