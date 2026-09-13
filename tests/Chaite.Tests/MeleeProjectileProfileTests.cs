using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunMeleeProjectileRegressions()
        {
            Run(nameof(MeleeCatalogPinsReviewedIdentities),
                MeleeCatalogPinsReviewedIdentities);
            Run(nameof(MeleeCatalogEvaluatesNonLinearFamilies),
                MeleeCatalogEvaluatesNonLinearFamilies);
            Run(nameof(MeleeCatalogPinsAutoReuseAndReturnWindows),
                MeleeCatalogPinsAutoReuseAndReturnWindows);
            Run(nameof(MeleeCatalogMarksDerivedEffectsUncredited),
                MeleeCatalogMarksDerivedEffectsUncredited);
            Run(nameof(MeleeCatalogRejectsDriftAndWrongResource),
                MeleeCatalogRejectsDriftAndWrongResource);
            Run(nameof(MeleeAimDoesNotUseStraightIntercept),
                MeleeAimDoesNotUseStraightIntercept);
            Run(nameof(MeleeAimRejectsOutOfEnvelopeTargets),
                MeleeAimRejectsOutOfEnvelopeTargets);
            Run(nameof(MeleeOutputRouteAcceptsNoAmmoIdentity),
                MeleeOutputRouteAcceptsNoAmmoIdentity);
            Run(nameof(MeleeProductionAdmissionRejectsExperimentalRoute),
                MeleeProductionAdmissionRejectsExperimentalRoute);
        }

        private static readonly int[] ReviewedMeleeWeapons =
        {
            MeleeProjectileCatalog.EnchantedBoomerangWeaponId,
            MeleeProjectileCatalog.FlamarangWeaponId,
            MeleeProjectileCatalog.ThornChakramWeaponId,
            MeleeProjectileCatalog.WoodenBoomerangWeaponId,
            MeleeProjectileCatalog.TridentWeaponId,
            MeleeProjectileCatalog.SpearWeaponId,
            MeleeProjectileCatalog.IceBoomerangWeaponId,
            MeleeProjectileCatalog.LightDiscWeaponId,
            MeleeProjectileCatalog.BananarangWeaponId,
            MeleeProjectileCatalog.FruitcakeChakramWeaponId,
            MeleeProjectileCatalog.MushroomSpearWeaponId,
            MeleeProjectileCatalog.TitaniumTridentWeaponId,
            MeleeProjectileCatalog.ThunderSpearWeaponId,
            MeleeProjectileCatalog.SlimeSpearWeaponId,
            MeleeProjectileCatalog.WoodYoyoWeaponId
        };

        private static WeaponProfileInput MeleeInput(int weaponId)
        {
            var projectile = weaponId == MeleeProjectileCatalog.EnchantedBoomerangWeaponId ? 6 :
                weaponId == MeleeProjectileCatalog.FlamarangWeaponId ? 19 :
                weaponId == MeleeProjectileCatalog.ThornChakramWeaponId ? 33 :
                weaponId == MeleeProjectileCatalog.WoodenBoomerangWeaponId ? 52 :
                weaponId == MeleeProjectileCatalog.TridentWeaponId ? 47 :
                weaponId == MeleeProjectileCatalog.SpearWeaponId ? 49 :
                weaponId == MeleeProjectileCatalog.IceBoomerangWeaponId ? 113 :
                weaponId == MeleeProjectileCatalog.LightDiscWeaponId ? 106 :
                weaponId == MeleeProjectileCatalog.BananarangWeaponId ? 272 :
                weaponId == MeleeProjectileCatalog.FruitcakeChakramWeaponId ? 333 :
                weaponId == MeleeProjectileCatalog.MushroomSpearWeaponId ? 130 :
                weaponId == MeleeProjectileCatalog.TitaniumTridentWeaponId ? 218 :
                weaponId == MeleeProjectileCatalog.ThunderSpearWeaponId ? 730 :
                weaponId == MeleeProjectileCatalog.SlimeSpearWeaponId ? 1103 : 541;
            var speed = weaponId == MeleeProjectileCatalog.EnchantedBoomerangWeaponId ? 10f :
                weaponId == MeleeProjectileCatalog.FlamarangWeaponId ? 14f :
                weaponId == MeleeProjectileCatalog.ThornChakramWeaponId ? 14f :
                weaponId == MeleeProjectileCatalog.WoodenBoomerangWeaponId ? 6.5f :
                weaponId == MeleeProjectileCatalog.TridentWeaponId ? 4f :
                weaponId == MeleeProjectileCatalog.SpearWeaponId ? 3.7f :
                weaponId == MeleeProjectileCatalog.IceBoomerangWeaponId ? 11.5f :
                weaponId == MeleeProjectileCatalog.LightDiscWeaponId ? 16f :
                weaponId == MeleeProjectileCatalog.BananarangWeaponId ? 16f :
                weaponId == MeleeProjectileCatalog.FruitcakeChakramWeaponId ? 11f :
                weaponId == MeleeProjectileCatalog.MushroomSpearWeaponId ? 5.5f :
                weaponId == MeleeProjectileCatalog.TitaniumTridentWeaponId ? 5f :
                weaponId == MeleeProjectileCatalog.ThunderSpearWeaponId ? 3.5f :
                weaponId == MeleeProjectileCatalog.SlimeSpearWeaponId ? 5.5f : 16f;
            var damage = weaponId == MeleeProjectileCatalog.EnchantedBoomerangWeaponId ? 17 :
                weaponId == MeleeProjectileCatalog.FlamarangWeaponId ? 49 :
                weaponId == MeleeProjectileCatalog.ThornChakramWeaponId ? 25 :
                weaponId == MeleeProjectileCatalog.WoodenBoomerangWeaponId ? 10 :
                weaponId == MeleeProjectileCatalog.TridentWeaponId ? 14 :
                weaponId == MeleeProjectileCatalog.SpearWeaponId ? 8 :
                weaponId == MeleeProjectileCatalog.IceBoomerangWeaponId ? 21 :
                weaponId == MeleeProjectileCatalog.LightDiscWeaponId ? 60 :
                weaponId == MeleeProjectileCatalog.BananarangWeaponId ? 45 :
                weaponId == MeleeProjectileCatalog.FruitcakeChakramWeaponId ? 19 :
                weaponId == MeleeProjectileCatalog.MushroomSpearWeaponId ? 60 :
                weaponId == MeleeProjectileCatalog.TitaniumTridentWeaponId ? 48 :
                weaponId == MeleeProjectileCatalog.ThunderSpearWeaponId ? 14 :
                weaponId == MeleeProjectileCatalog.SlimeSpearWeaponId ? 12 : 9;
            var use = weaponId == MeleeProjectileCatalog.WoodYoyoWeaponId ? 25 :
                weaponId == MeleeProjectileCatalog.ThornChakramWeaponId ? 15 :
                weaponId == MeleeProjectileCatalog.LightDiscWeaponId ? 14 :
                weaponId == MeleeProjectileCatalog.BananarangWeaponId ? 11 :
                weaponId == MeleeProjectileCatalog.FruitcakeChakramWeaponId ? 15 :
                weaponId == MeleeProjectileCatalog.MushroomSpearWeaponId ? 40 :
                weaponId == MeleeProjectileCatalog.TitaniumTridentWeaponId ? 23 :
                weaponId == MeleeProjectileCatalog.ThunderSpearWeaponId ? 28 :
                weaponId == MeleeProjectileCatalog.SlimeSpearWeaponId ? 24 :
                weaponId == MeleeProjectileCatalog.TridentWeaponId ||
                weaponId == MeleeProjectileCatalog.SpearWeaponId ? 31 : 20;
            return new WeaponProfileInput
            {
                WeaponId = weaponId,
                AmmoId = 0,
                ProjectileId = projectile,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = speed,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = damage,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 1f,
                UseTime = use,
                UseAnimation = use,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = weaponId == MeleeProjectileCatalog.LightDiscWeaponId ||
                    weaponId == MeleeProjectileCatalog.BananarangWeaponId,
                HasAmmo = true
            };
        }

        private static void MeleeCatalogPinsReviewedIdentities()
        {
            Equal(15, MeleeProjectileCatalog.Count);
            var projectiles = new[] { 6, 19, 33, 52, 47, 49, 113, 106, 272,
                333, 130, 218, 730, 1103, 541 };
            for (var i = 0; i < ReviewedMeleeWeapons.Length; i++)
            {
                WeaponProfile profile;
                True(WeaponProfileCatalog.TryGet(ReviewedMeleeWeapons[i], 0,
                    out profile));
                Equal(new WeaponProfileKey(ReviewedMeleeWeapons[i], 0),
                    profile.Key);
                Equal(projectiles[i], profile.ProjectileId);
                Equal(OutputRouteKind.MeleeProjectile, profile.OutputKind);
                Equal(OutputResourceKind.Melee, profile.ResourceKind);
                True(MeleeProjectileCatalog.TryGetProjectileDefaults(
                    projectiles[i], out var updates, out var lifetime));
                Equal(0, updates);
                Equal(3600, lifetime);
            }
        }

        private static void MeleeCatalogEvaluatesNonLinearFamilies()
        {
            for (var i = 0; i < ReviewedMeleeWeapons.Length; i++)
            {
                var evaluation = WeaponProfileCatalog.Evaluate(
                    MeleeInput(ReviewedMeleeWeapons[i]));
                True(evaluation.IsSupported, evaluation.Reason);
                True(evaluation.DirectDamage > 0);
                True(evaluation.ApproximateDirectDps > 0f);
                True(evaluation.ConservativeRangePixels > 0f);
                Equal(0f, evaluation.SpreadSpeedPixelsPerTick);
            }
        }

        private static void MeleeCatalogPinsAutoReuseAndReturnWindows()
        {
            var lightDisc = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.LightDiscWeaponId));
            True(lightDisc.IsSupported, lightDisc.Reason);
            Equal(WeaponFireMode.Automatic,
                lightDisc.Profile.FireMode);
            Equal(true, lightDisc.Profile.DefaultAutoReuse);
            Equal(45f, lightDisc.MaxFlightTicks);
            NearWeapon(720f, lightDisc.ConservativeRangePixels);

            var banana = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.BananarangWeaponId));
            True(banana.IsSupported, banana.Reason);
            Equal(WeaponFireMode.Automatic,
                banana.Profile.FireMode);
            Equal(true, banana.Profile.DefaultAutoReuse);
            Equal(30f, banana.MaxFlightTicks);
            NearWeapon(480f, banana.ConservativeRangePixels);

            var wooden = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.WoodenBoomerangWeaponId));
            Equal(WeaponFireMode.NativeAnimationBurst,
                wooden.Profile.FireMode);
            Equal(false, wooden.Profile.DefaultAutoReuse);
        }

        private static void MeleeCatalogMarksDerivedEffectsUncredited()
        {
            var flamarang = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.FlamarangWeaponId));
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                flamarang.Profile.SecondaryEffect);
            var thorn = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.ThornChakramWeaponId));
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                thorn.Profile.SecondaryEffect);
            var ice = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.IceBoomerangWeaponId));
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                ice.Profile.SecondaryEffect);
            var mushroom = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.MushroomSpearWeaponId));
            Equal(WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited,
                mushroom.Profile.SecondaryEffect);
            var thunder = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.ThunderSpearWeaponId));
            Equal(WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited,
                thunder.Profile.SecondaryEffect);
            var slime = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.SlimeSpearWeaponId));
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                slime.Profile.SecondaryEffect);

            var fruitcake = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.FruitcakeChakramWeaponId));
            Equal(WeaponSecondaryEffect.None,
                fruitcake.Profile.SecondaryEffect);
            var titanium = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.TitaniumTridentWeaponId));
            Equal(WeaponSecondaryEffect.None,
                titanium.Profile.SecondaryEffect);
        }

        private static void MeleeCatalogRejectsDriftAndWrongResource()
        {
            var input = MeleeInput(
                MeleeProjectileCatalog.EnchantedBoomerangWeaponId);
            input.ProjectileId = 19;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = MeleeInput(MeleeProjectileCatalog.SpearWeaponId);
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = MeleeInput(MeleeProjectileCatalog.WoodYoyoWeaponId);
            input.ProjectileLifetimeSubupdates = 3599;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = MeleeInput(MeleeProjectileCatalog.TridentWeaponId);
            input.WeaponShootSpeed = 5f;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = MeleeInput(MeleeProjectileCatalog.SpearWeaponId);
            input.AmmoId = 97;
            input.ProjectileId = 14;
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void MeleeAimDoesNotUseStraightIntercept()
        {
            foreach (var id in ReviewedMeleeWeapons)
            {
                var evaluation = WeaponProfileCatalog.Evaluate(MeleeInput(id));
                var target = new Vec2(20f, 10f);
                var aim = WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                    new Vec2(6f, -3f));
                True(aim.CanFire, id + ": " + aim.Status);
                Equal(0f, aim.LeadTicks);
                Equal(target, aim.AimWorld);
            }
        }

        private static void MeleeAimRejectsOutOfEnvelopeTargets()
        {
            var boomerang = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.FlamarangWeaponId));
            var aim = WeaponAimSolver.Solve(boomerang, new Vec2(),
                new Vec2(421f, 0f), new Vec2());
            Equal(WeaponAimStatus.BeyondLifetime, aim.Status);

            var spear = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.SpearWeaponId));
            aim = WeaponAimSolver.Solve(spear, new Vec2(), new Vec2(41f, 0f),
                new Vec2());
            Equal(WeaponAimStatus.BeyondLifetime, aim.Status);

            var yoyo = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.WoodYoyoWeaponId));
            aim = WeaponAimSolver.Solve(yoyo, new Vec2(), new Vec2(131f, 0f),
                new Vec2());
            Equal(WeaponAimStatus.BeyondLifetime, aim.Status);
        }

        private static void MeleeOutputRouteAcceptsNoAmmoIdentity()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.TridentWeaponId));
            var snapshot = new CombatSnapshot
            {
                Weapon = new WeaponSnapshot
                {
                    Slot = 2,
                    Damage = evaluation.DirectDamage,
                    UseTime = 31,
                    ShootSpeed = evaluation.SpeedPixelsPerTick,
                    IsProjectile = true,
                    IsMelee = true,
                    HasAmmo = true,
                    IsUsable = true,
                    NativeProfileRequired = true,
                    WeaponId = MeleeProjectileCatalog.TridentWeaponId,
                    AmmoId = 0,
                    ProjectileId = MeleeProjectileCatalog.TridentProjectileId,
                    Profile = evaluation
                }
            };
            OutputRouteProfile route;
            string reason;
            True(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason), reason);
            Equal(OutputRouteKind.MeleeProjectile, route.Kind);
            Equal(OutputResourceKind.Melee, route.Resource);
            Equal(0, route.AmmoId);
            snapshot.Weapon.ProjectileId = 49;
            False(OutputRouteContract.TryCreateReady(snapshot, out route,
                out reason));
        }

        private static void MeleeProductionAdmissionRejectsExperimentalRoute()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(MeleeInput(
                MeleeProjectileCatalog.LightDiscWeaponId));
            True(evaluation.ApproximateDirectDps > 18f);
            var snapshot = CombatScenario(4);
            snapshot.Weapon = new WeaponSnapshot
            {
                Slot = 2,
                Damage = evaluation.DirectDamage,
                UseTime = 14,
                ShootSpeed = evaluation.SpeedPixelsPerTick,
                IsProjectile = true,
                IsMelee = true,
                HasAmmo = true,
                IsUsable = true,
                NativeProfileRequired = true,
                WeaponId = MeleeProjectileCatalog.LightDiscWeaponId,
                AmmoId = 0,
                ProjectileId = MeleeProjectileCatalog.LightDiscProjectileId,
                Profile = evaluation
            };
            var planner = new CombatPlanner(new PlannerSettings());
            string reason;
            False(planner.RequirementsMetForExpected(snapshot,
                "suspicious-eye", 4, out reason));
            True(reason.Contains("Boss-pattern hit range") &&
                reason.Contains("native cadence"), reason);
            False(planner.PrepareForExpectedEncounter(snapshot,
                "suspicious-eye", 4, out reason));
            True(reason.Contains("Boss-pattern hit range") &&
                reason.Contains("native cadence"), reason);
            False(planner.PrepareForActiveEncounter(snapshot, out reason));
            True(reason.Contains("Boss-pattern hit range") &&
                reason.Contains("native cadence"), reason);
        }
    }
}
