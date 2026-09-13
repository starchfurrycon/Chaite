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
            Run(nameof(CommonGunCatalogEvaluatesEveryExplicitPair), CommonGunCatalogEvaluatesEveryExplicitPair);
            Run(nameof(CommonDartCatalogUsesExactReviewedPairs), CommonDartCatalogUsesExactReviewedPairs);
            Run(nameof(CommonBowCatalogUsesExactReviewedPairs), CommonBowCatalogUsesExactReviewedPairs);
            Run(nameof(ArrowCatalogLocksNativeProjectileMotion), ArrowCatalogLocksNativeProjectileMotion);
            Run(nameof(BowProjectileConversionsFollowNativeOrder), BowProjectileConversionsFollowNativeOrder);
            Run(nameof(ArrowModifiersUseLiveNativeOrder), ArrowModifiersUseLiveNativeOrder);
            Run(nameof(MagicQuiverPreservesFirstTickUpdateSchedule), MagicQuiverPreservesFirstTickUpdateSchedule);
            Run(nameof(DiscreteArrowGravityChangesVelocityBeforeMovement), DiscreteArrowGravityChangesVelocityBeforeMovement);
            Run(nameof(MultiArrowBowsCreditOneGuaranteedPrimary), MultiArrowBowsCreditOneGuaranteedPrimary);
            Run(nameof(ComplexBowFamiliesRemainExplicitlyUnsupported), ComplexBowFamiliesRemainExplicitlyUnsupported);
            Run(nameof(CommonMagicRoutesUseExactManaAndProjectileProfiles), CommonMagicRoutesUseExactManaAndProjectileProfiles);
            Run(nameof(AdditionalMagicProfilesCreditOnlyExactPrimary), AdditionalMagicProfilesCreditOnlyExactPrimary);
            Run(nameof(StarCannonProfilesKeepNativeProjectileAndResource), StarCannonProfilesKeepNativeProjectileAndResource);
            Run(nameof(MinisharkAmmoChangesSpeedDamageAndSecondaryEffect), MinisharkAmmoChangesSpeedDamageAndSecondaryEffect);
            Run(nameof(ClockworkBurstUsesNativeAnimationThresholds), ClockworkBurstUsesNativeAnimationThresholds);
            Run(nameof(ClockworkNewAnimationAccountsForNativeDecrement), ClockworkNewAnimationAccountsForNativeDecrement);
            Run(nameof(RedRyderAndGatligatorKeepNativePairSemantics), RedRyderAndGatligatorKeepNativePairSemantics);
            Run(nameof(OnyxBlasterUsesNativeRadialEnvelope), OnyxBlasterUsesNativeRadialEnvelope);
            Run(nameof(PewMaticHornUsesNativeProjectileOverride), PewMaticHornUsesNativeProjectileOverride);
            Run(nameof(CrystalStormUsesNativeDragProfile), CrystalStormUsesNativeDragProfile);
            Run(nameof(CrystalStormAimIntegratesDragBeforeMovement), CrystalStormAimIntegratesDragBeforeMovement);
            Run(nameof(CrystalStormRequiresEnvelopeInsideLiveHitbox), CrystalStormRequiresEnvelopeInsideLiveHitbox);
            Run(nameof(CombatPlannerPassesLiveHitboxToCrystalStorm), CombatPlannerPassesLiveHitboxToCrystalStorm);
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
                ReuseDelay = weaponId == 98 ? 0 : 14, AutoReuse = true,
                HasAmmo = true
            };
        }

        private static WeaponProfileInput StarCannonInput(int weaponId)
        {
            var super = weaponId == 4060;
            return new WeaponProfileInput
            {
                WeaponId = weaponId,
                AmmoId = 75,
                // Fallen Star has shoot=0; ItemCheck retains the weapon's
                // native projectile (955 or 728) after PickAmmo.
                ProjectileId = super ? 728 : 955,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = super ? 20f : 14f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = super ? 60 : 55,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 1f,
                UseTime = super ? 18 : 12,
                UseAnimation = super ? 18 : 12,
                ReuseDelay = 0,
                AutoReuse = true,
                HasAmmo = true
            };
        }

        private static WeaponProfileInput CrystalStormInput()
        {
            return new WeaponProfileInput
            {
                WeaponId = 518,
                AmmoId = 0,
                ProjectileId = 94,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 600,
                WeaponShootSpeed = 16f,
                WeaponDamageAfterModifiers = 35,
                UseTime = 7,
                UseAnimation = 7,
                ReuseDelay = 0,
                AutoReuse = true,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = 5
            };
        }

        private static void WeaponCatalogUsesExactWeaponAmmoPairs()
        {
            Equal(885, WeaponProfileCatalog.Count);
            foreach (var weaponId in new[] { 95, 96, 98, 164, 219, 434,
                533, 534, 679, 800, 964, 1254, 1255, 1265, 1553, 1929,
                1870, 2269, 2270, 3788, 4703, 5117 })
            foreach (var ammoId in new[] { 97, 234, 278, 515, 546, 1179,
                1302, 1335, 1342, 1349, 1350, 1351, 1352, 3104, 3567,
                4915 })
            {
                WeaponProfile profile;
                var supported = ammoId != 1179;
                Equal(supported, WeaponProfileCatalog.TryGet(weaponId,
                    ammoId, out profile));
                if (supported)
                    Equal(new WeaponProfileKey(weaponId, ammoId),
                        profile.Key);
            }
            foreach (var weaponId in new[] { 127, 165, 514, 518, 519, 683,
                726, 1295, 1308, 1336, 1444, 1445, 2188, 3209 })
            {
                WeaponProfile profile;
                True(WeaponProfileCatalog.TryGet(weaponId, 0, out profile));
                Equal(OutputRouteKind.StraightMagic, profile.OutputKind);
                    Equal(OutputResourceKind.Mana, profile.ResourceKind);
            }
            foreach (var weaponId in new[] { 197, 4060 })
            {
                WeaponProfile profile;
                True(WeaponProfileCatalog.TryGet(weaponId, 75,
                    out profile));
                Equal(OutputRouteKind.StraightRanged, profile.OutputKind);
                Equal(OutputResourceKind.Ammunition, profile.ResourceKind);
                False(WeaponProfileCatalog.TryGet(weaponId, 97,
                    out profile));
            }
            foreach (var key in new[] { new WeaponProfileKey(434, 1179),
                new WeaponProfileKey(2797, 97),
                new WeaponProfileKey(281, 97),
                new WeaponProfileKey(98, 283),
                new WeaponProfileKey(98, 0),
                new WeaponProfileKey(-1, 97) })
            {
                WeaponProfile profile;
                False(WeaponProfileCatalog.TryGet(key.WeaponId, key.AmmoId, out profile));
                True(profile == null);
            }
            False(new WeaponProfileKey(98, 97).Equals(new WeaponProfileKey(97, 98)));
        }

        private static void WeaponProfilesSeparateSourceAndBehaviorEvidence()
        {
            foreach (var weaponId in new[] { 98, 434, 1870, 2270 })
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

        private static void CommonGunCatalogEvaluatesEveryExplicitPair()
        {
            foreach (var weapon in CommonGunFixtures)
            foreach (var ammo in CommonAmmoFixtures)
            {
                var projectile = weapon.ConvertsMusket &&
                    ammo.ProjectileId == 14 ? 242 : ammo.ProjectileId;
                var extraUpdates = projectile == 242 ? 7 :
                    ammo.ExtraUpdates;
                var input = new WeaponProfileInput
                {
                    WeaponId = weapon.Id,
                    AmmoId = ammo.Id,
                    ProjectileId = projectile,
                    ProjectileExtraUpdates = extraUpdates,
                    ProjectileLifetimeSubupdates = ammo.Lifetime,
                    WeaponShootSpeed = weapon.ShootSpeed,
                    AmmoShootSpeed = ammo.ShootSpeed,
                    WeaponDamageAfterModifiers = weapon.Damage,
                    AmmoBaseDamage = ammo.Damage,
                    AmmoDamageMultiplier = 1f,
                    UseTime = weapon.UseTime,
                    UseAnimation = weapon.UseAnimation,
                    ReuseDelay = weapon.ReuseDelay,
                    AutoReuse = weapon.AutoReuse,
                    HasAmmo = true
                };
                var evaluation = WeaponProfileCatalog.Evaluate(input);
                True(evaluation.IsSupported, weapon.Id + "/" + ammo.Id +
                    ": " + evaluation.Reason);
                Equal(projectile, evaluation.Profile.ProjectileId);
                Equal(projectile, WeaponProfileCatalog.ResolveProjectileForPair(
                    weapon.Id, ammo.Id, ammo.ProjectileId));
                Equal(weapon.Damage + ammo.Damage, evaluation.DirectDamage);
                Equal(OutputRouteKind.StraightRanged,
                    evaluation.Profile.OutputKind);
                Equal(OutputResourceKind.Ammunition,
                    evaluation.Profile.ResourceKind);
                Equal(weapon.AutoReuse,
                    evaluation.Profile.DefaultAutoReuse);
                Equal(extraUpdates,
                    evaluation.Profile.DefaultExtraUpdates);
                True(evaluation.ApproximateDirectDps > 0f);
            }
            // Conversion is authorized only for the exact reviewed pair/raw
            // projectile. A stale or modded raw projectile remains unchanged.
            Equal(242, WeaponProfileCatalog.ResolveProjectileForPair(1254,
                97, 14));
            Equal(89, WeaponProfileCatalog.ResolveProjectileForPair(1254,
                515, 89));
            Equal(12345, WeaponProfileCatalog.ResolveProjectileForPair(1254,
                97, 12345));
        }

        private static void CommonDartCatalogUsesExactReviewedPairs()
        {
            var supportedPairs = 0;
            foreach (var weapon in CommonDartWeaponFixtures)
            foreach (var dart in CommonDartFixtures)
            {
                supportedPairs++;
                WeaponProfile profile;
                True(WeaponProfileCatalog.TryGet(weapon.Id, dart.Id,
                    out profile));
                var input = DartInput(weapon, dart);
                var evaluation = WeaponProfileCatalog.Evaluate(input);
                True(evaluation.IsSupported, weapon.Id + "/" + dart.Id +
                    ": " + evaluation.Reason);
                Equal(dart.ProjectileId, profile.ProjectileId);
                Equal(dart.ProjectileId,
                    WeaponProfileCatalog.ResolveProjectileForPair(weapon.Id,
                        dart.Id, dart.ProjectileId));
                Equal(WeaponBallisticKind.
                    DiscreteVerticalAccelerationPrimaryProjectile,
                    profile.Ballistics);
                Equal(OutputRouteKind.StraightRanged, profile.OutputKind);
                Equal(OutputResourceKind.Ammunition, profile.ResourceKind);
                Equal(weapon.Damage, profile.DefaultWeaponDamage);
                Equal(dart.Damage, profile.DefaultAmmoDamage);
                Equal(weapon.UseTime, profile.DefaultUseTime);
                Equal(weapon.UseAnimation, profile.DefaultUseAnimation);
                Equal(weapon.AutoReuse, profile.DefaultAutoReuse);
                Equal(dart.ExtraUpdates, profile.DefaultExtraUpdates);
                Equal(dart.Lifetime, profile.DefaultLifetimeSubupdates);
                Equal(dart.Delay,
                    profile.VerticalAccelerationDelaySubupdates);
                NearWeapon(dart.Acceleration,
                    profile.VerticalAccelerationPerSubupdate);
                Equal(weapon.Damage + dart.Damage,
                    evaluation.DirectDamage);
                True(evaluation.ApproximateDirectDps > 0f);
            }
            Equal(20, supportedPairs);

            Equal(WeaponSecondaryEffect.None,
                WeaponProfileCatalog.Evaluate(DartInput(
                    CommonDartWeaponFixtures[0], CommonDartFixtures[0])).
                    Profile.SecondaryEffect);
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                WeaponProfileCatalog.Evaluate(DartInput(
                    CommonDartWeaponFixtures[1], CommonDartFixtures[1])).
                    Profile.SecondaryEffect);
            Equal(WeaponSecondaryEffect.HomingOrBounceNotCredited,
                WeaponProfileCatalog.Evaluate(DartInput(
                    CommonDartWeaponFixtures[2], CommonDartFixtures[2])).
                    Profile.SecondaryEffect);
            Equal(WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited,
                WeaponProfileCatalog.Evaluate(DartInput(
                    CommonDartWeaponFixtures[3], CommonDartFixtures[3])).
                    Profile.SecondaryEffect);
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                WeaponProfileCatalog.Evaluate(DartInput(
                    CommonDartWeaponFixtures[3], CommonDartFixtures[4])).
                    Profile.SecondaryEffect);

            WeaponProfile unsupported;
            False(WeaponProfileCatalog.TryGet(281, 97, out unsupported));
            False(WeaponProfileCatalog.TryGet(98, 283, out unsupported));
            Equal(12345, WeaponProfileCatalog.ResolveProjectileForPair(281,
                283, 12345));

            var noAmmo = DartInput(CommonDartWeaponFixtures[0],
                CommonDartFixtures[0]);
            noAmmo.HasAmmo = false;
            noAmmo.AmmoId = 0;
            Equal(WeaponProfileStatus.MissingAmmo,
                WeaponProfileCatalog.Evaluate(noAmmo).Status);
        }

        private static void CommonBowCatalogUsesExactReviewedPairs()
        {
            var supportedPairs = 0;
            foreach (var bow in CommonBowFixtures)
            foreach (var arrow in CommonArrowFixtures)
            {
                var expected = !bow.WoodenArrowOnly || arrow.Id == 40 ||
                    arrow.Id == 3103;
                WeaponProfile profile;
                Equal(expected, WeaponProfileCatalog.TryGet(bow.Id,
                    arrow.Id, out profile));
                if (!expected)
                {
                    True(profile == null);
                    continue;
                }
                supportedPairs++;
                var evaluation = WeaponProfileCatalog.Evaluate(
                    BowInput(bow.Id, arrow.Id));
                True(evaluation.IsSupported, bow.Id + "/" + arrow.Id +
                    ": " + evaluation.Reason);
                var projectile = ExpectedBowProjectile(bow.Id,
                    arrow.ProjectileId, false);
                var motion = ProjectileMotion(projectile);
                Equal(projectile, profile.ProjectileId);
                Equal(projectile, WeaponProfileCatalog.
                    ResolveProjectileForPair(bow.Id, arrow.Id,
                        arrow.ProjectileId));
                Equal(WeaponBallisticKind.
                    DiscreteVerticalAccelerationPrimaryProjectile,
                    profile.Ballistics);
                Equal(OutputRouteKind.StraightRanged, profile.OutputKind);
                Equal(OutputResourceKind.Ammunition,
                    profile.ResourceKind);
                Equal(bow.Damage, profile.DefaultWeaponDamage);
                Equal(arrow.Damage, profile.DefaultAmmoDamage);
                Equal(bow.UseTime, profile.DefaultUseTime);
                Equal(bow.UseAnimation, profile.DefaultUseAnimation);
                Equal(bow.AutoReuse, profile.DefaultAutoReuse);
                Equal(motion.ExtraUpdates, profile.DefaultExtraUpdates);
                Equal(motion.Lifetime, profile.DefaultLifetimeSubupdates);
                Equal(bow.Damage + arrow.Damage,
                    evaluation.DirectDamage);
                True(evaluation.ApproximateDirectDps > 0f);
            }
            Equal(512, supportedPairs);
        }

        private static void ArrowCatalogLocksNativeProjectileMotion()
        {
            foreach (var arrow in CommonArrowFixtures)
            {
                var input = BowInput(3516, arrow.Id);
                var evaluation = WeaponProfileCatalog.Evaluate(input);
                True(evaluation.IsSupported);
                var motion = ProjectileMotion(arrow.ProjectileId);
                Equal(arrow.ProjectileId, evaluation.Profile.ProjectileId);
                Equal(arrow.Damage, evaluation.Profile.DefaultAmmoDamage);
                NearWeapon(arrow.ShootSpeed,
                    evaluation.Profile.DefaultAmmoShootSpeed);
                Equal(arrow.ExtraUpdates,
                    evaluation.Profile.DefaultExtraUpdates);
                Equal(arrow.Lifetime,
                    evaluation.Profile.DefaultLifetimeSubupdates);
                Equal(motion.Delay,
                    evaluation.Profile.VerticalAccelerationDelaySubupdates);
                NearWeapon(motion.Acceleration,
                    evaluation.Profile.VerticalAccelerationPerSubupdate);
                Equal(arrow.ExtraUpdates + 1,
                    evaluation.FirstTickProjectileUpdates);
                Equal(arrow.ExtraUpdates + 1,
                    evaluation.SustainedProjectileUpdatesPerTick);
            }
            // Hellfire Arrow is not in AI_001's no-age switch: its ordinary
            // +0.1 gravity starts on update 15 despite its long lifetime.
            var hellfire = WeaponProfileCatalog.Evaluate(BowInput(3516,
                265));
            Equal(14,
                hellfire.Profile.VerticalAccelerationDelaySubupdates);
            NearWeapon(.1f,
                hellfire.Profile.VerticalAccelerationPerSubupdate);
            Equal(3600, hellfire.Profile.DefaultLifetimeSubupdates);
        }

        private static void BowProjectileConversionsFollowNativeOrder()
        {
            Equal(2, WeaponProfileCatalog.ResolveProjectileForPair(120, 40,
                1));
            Equal(2, WeaponProfileCatalog.ResolveProjectileForPair(120, 41,
                2));
            Equal(117, WeaponProfileCatalog.ResolveProjectileForPair(682,
                516, 91));
            Equal(120, WeaponProfileCatalog.ResolveProjectileForPair(725,
                545, 103));
            Equal(357, WeaponProfileCatalog.ResolveProjectileForPair(2223,
                3568, 639));
            Equal(495, WeaponProfileCatalog.ResolveProjectileForPair(3052,
                51, 5));
            Equal(469, WeaponProfileCatalog.ResolveProjectileForPair(2888,
                40, 1));
            Equal(485, WeaponProfileCatalog.ResolveProjectileForPair(3019,
                3103, 1));

            // Molten Quiver is a PickAmmo conversion. Ordinary wooden-arrow
            // profiles fail closed because one key cannot claim both type 1
            // and type 2; post-PickAmmo weapon conversions retain identity.
            Equal(2, WeaponProfileCatalog.ResolveProjectileForPair(3516, 40,
                1, true));
            Equal(117, WeaponProfileCatalog.ResolveProjectileForPair(682, 40,
                1, true));
            Equal(469, WeaponProfileCatalog.ResolveProjectileForPair(2888,
                40, 1, true));
            Equal(485, WeaponProfileCatalog.ResolveProjectileForPair(3019,
                40, 1, true));
            Equal(12345, WeaponProfileCatalog.ResolveProjectileForPair(120,
                40, 12345, true));
        }

        private static void ArrowModifiersUseLiveNativeOrder()
        {
            var plain = WeaponProfileCatalog.Evaluate(BowInput(3516, 40));
            NearWeapon(9.6f, plain.InitialSpeedPixelsPerSubupdate);
            Equal(16, plain.DirectDamage);

            var archery = BowInput(3516, 40, archery: true);
            NearWeapon(11.52f, WeaponProfileCatalog.Evaluate(archery).
                InitialSpeedPixelsPerSubupdate);
            var both = BowInput(3516, 40, archery: true,
                magicQuiver: true);
            NearWeapon(12.672f, WeaponProfileCatalog.Evaluate(both).
                InitialSpeedPixelsPerSubupdate, .00001f);

            var capped = BowInput(2624, 265, archery: true,
                magicQuiver: true);
            NearWeapon(20f, WeaponProfileCatalog.Evaluate(capped).
                InitialSpeedPixelsPerSubupdate);

            var sharp = BowInput(3516, 40, sharpBarb: true);
            sharp.AmmoDamageMultiplier = 1.25f;
            Equal(18, WeaponProfileCatalog.Evaluate(sharp).DirectDamage);
            sharp.SharpBarb = false;
            Equal(17, WeaponProfileCatalog.Evaluate(sharp).DirectDamage);

            var ordinaryMolten = BowInput(3516, 40,
                hasMoltenQuiver: true);
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(ordinaryMolten).Status);
            var moltenFury = BowInput(120, 40,
                hasMoltenQuiver: true);
            Equal(38, WeaponProfileCatalog.Evaluate(moltenFury).
                DirectDamage);
            moltenFury.HasMoltenQuiver = false;
            Equal(36, WeaponProfileCatalog.Evaluate(moltenFury).
                DirectDamage);
            var marrow = BowInput(682, 40, hasMoltenQuiver: true);
            Equal(47, WeaponProfileCatalog.Evaluate(marrow).DirectDamage);
            var bees = BowInput(2888, 40, hasMoltenQuiver: true);
            Equal(28, WeaponProfileCatalog.Evaluate(bees).DirectDamage);

            var unknown = BowInput(3516, 40);
            unknown.ArrowStateKnown = false;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(unknown).Status);
            var homingAccessory = BowInput(3516, 40);
            homingAccessory.HarpyCharm = true;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(homingAccessory).Status);
        }

        private static void MagicQuiverPreservesFirstTickUpdateSchedule()
        {
            var input = BowInput(3516, 40, magicQuiver: true);
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);
            Equal(1, evaluation.FirstTickProjectileUpdates);
            Equal(2, evaluation.SustainedProjectileUpdatesPerTick);
            NearWeapon(10.56f, evaluation.InitialSpeedPixelsPerSubupdate);
            NearWeapon(21.12f, evaluation.SpeedPixelsPerTick);
            NearWeapon(600.5f, evaluation.MaxFlightTicks);

            var first = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(10.56f, 0f), new Vec2());
            True(first.CanFire);
            NearWeapon(1f, first.LeadTicks, .00001f);
            var second = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(31.68f, 0f), new Vec2());
            True(second.CanFire);
            NearWeapon(2f, second.LeadTicks, .00001f);

            var pulse = WeaponProfileCatalog.Evaluate(BowInput(2223, 40,
                magicQuiver: true));
            Equal(3, pulse.FirstTickProjectileUpdates);
            Equal(3, pulse.SustainedProjectileUpdatesPerTick);
            NearWeapon(200f, pulse.MaxFlightTicks);
            False(pulse.Profile.MagicQuiverCanAddProjectileUpdate);
        }

        private static void DiscreteArrowGravityChangesVelocityBeforeMovement()
        {
            var wooden = WeaponProfileCatalog.Evaluate(BowInput(3516, 40));
            var before = WeaponAimSolver.Solve(wooden, new Vec2(),
                new Vec2(134.4f, 0f), new Vec2(), 30f);
            True(before.CanFire);
            NearWeapon(14f, before.LeadTicks, .0001f);
            NearWeapon(0f, before.AimWorld.Y, .0001f);

            var firstGravity = WeaponAimSolver.Solve(wooden, new Vec2(),
                new Vec2(144f, .1f), new Vec2(), 30f);
            True(firstGravity.CanFire);
            NearWeapon(15f, firstGravity.LeadTicks, .0001f);
            NearWeapon(0f, firstGravity.AimWorld.Y, .0001f);

            // A moving target constructed from the reviewed discrete path
            // must recover the same launch vector, not a linear-gravity guess.
            var moving = WeaponAimSolver.Solve(wooden, new Vec2(),
                new Vec2(172f, 6.1f), new Vec2(1f, -.2f), 30f);
            True(moving.CanFire);
            NearWeapon(20f, moving.LeadTicks, .0001f);
            NearWeapon(192f, moving.AimWorld.X, .001f);
            NearWeapon(0f, moving.AimWorld.Y, .001f);

            var shimmer = WeaponProfileCatalog.Evaluate(BowInput(3516,
                5348));
            NearWeapon(-.1f,
                shimmer.Profile.VerticalAccelerationPerSubupdate);
            var rising = WeaponAimSolver.Solve(shimmer, new Vec2(),
                new Vec2(144f, -.1f), new Vec2(), 30f);
            True(rising.CanFire);
            NearWeapon(15f, rising.LeadTicks, .0001f);
            NearWeapon(0f, rising.AimWorld.Y, .0001f);

            Equal(WeaponAimStatus.BeyondPredictionHorizon,
                WeaponAimSolver.Solve(wooden, new Vec2(),
                    new Vec2(1000f, 0f), new Vec2(), 5f).Status);
            var changedUpdates = BowInput(3516, 40);
            changedUpdates.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(changedUpdates).Status);
        }

        private static void MultiArrowBowsCreditOneGuaranteedPrimary()
        {
            var shotbow = WeaponProfileCatalog.Evaluate(BowInput(1229, 516));
            True(shotbow.IsSupported);
            Equal(47, shotbow.DirectDamage);
            Equal(WeaponSecondaryEffect.AdditionalArrowsNotCredited,
                shotbow.Profile.SecondaryEffect);
            var tsunami = WeaponProfileCatalog.Evaluate(BowInput(2624, 40));
            Equal(58, tsunami.DirectDamage);
            Equal(WeaponSecondaryEffect.AdditionalArrowsNotCredited,
                tsunami.Profile.SecondaryEffect);
            Equal(WeaponSecondaryEffect.BeeSpawnsNotCredited,
                WeaponProfileCatalog.Evaluate(BowInput(2888, 40)).Profile.
                    SecondaryEffect);
            Equal(WeaponSecondaryEffect.FallingStarsNotCredited,
                WeaponProfileCatalog.Evaluate(BowInput(3516, 516)).Profile.
                    SecondaryEffect);
            Equal(WeaponSecondaryEffect.HomingOrBounceNotCredited,
                WeaponProfileCatalog.Evaluate(BowInput(3516, 1235)).Profile.
                    SecondaryEffect);
            Equal(WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited,
                WeaponProfileCatalog.Evaluate(BowInput(3516, 5348)).Profile.
                    SecondaryEffect);
        }

        private static void ComplexBowFamiliesRemainExplicitlyUnsupported()
        {
            // Eventide (4953) now has a separate five-event convergence
            // controller. These remaining identities still have no reviewed
            // production model and must not fall through to the ordinary bow
            // matrix.
            foreach (var weaponId in new[] { 3029, 4381, 3540,
                3859, 3854 })
            {
                WeaponProfile profile;
                False(WeaponProfileCatalog.TryGet(weaponId, 40,
                    out profile));
                True(profile == null);
                Equal(WeaponProfileStatus.UnsupportedCombination,
                    WeaponProfileCatalog.Evaluate(new WeaponProfileInput
                    {
                        WeaponId = weaponId,
                        AmmoId = 40,
                        HasAmmo = true,
                        ArrowStateKnown = true
                    }).Status);
            }
            WeaponProfile unsupportedHellwing;
            False(WeaponProfileCatalog.TryGet(3019, 516,
                out unsupportedHellwing));
            True(unsupportedHellwing == null);
            var hellwing = WeaponProfileCatalog.Evaluate(BowInput(3019, 40));
            True(hellwing.IsSupported);
            Equal(485, hellwing.Profile.ProjectileId);
            True(hellwing.SpreadSpeedPixelsPerSubupdate > 0f);
        }

        private static void CommonMagicRoutesUseExactManaAndProjectileProfiles()
        {
            foreach (var magic in CommonMagicFixtures)
            {
                var input = new WeaponProfileInput
                {
                    WeaponId = magic.Id,
                    AmmoId = 0,
                    ProjectileId = magic.ProjectileId,
                    ProjectileExtraUpdates = magic.ExtraUpdates,
                    ProjectileLifetimeSubupdates = magic.Lifetime,
                    WeaponShootSpeed = magic.ShootSpeed,
                    WeaponDamageAfterModifiers = magic.Damage,
                    UseTime = magic.UseTime,
                    UseAnimation = magic.UseAnimation,
                    ReuseDelay = 0,
                    AutoReuse = magic.AutoReuse,
                    HasAmmo = true,
                    ManaCostKnown = true,
                    ManaCostPerUse = magic.Mana
                };
                var evaluation = WeaponProfileCatalog.Evaluate(input);
                True(evaluation.IsSupported, magic.Id + ": " +
                    evaluation.Reason);
                Equal(OutputRouteKind.StraightMagic,
                    evaluation.Profile.OutputKind);
                Equal(OutputResourceKind.Mana,
                    evaluation.Profile.ResourceKind);
                Equal(magic.Mana, evaluation.Profile.DefaultManaCost);
                Equal(magic.Mana, evaluation.ManaCostPerUse);
                Equal(magic.Damage, evaluation.DirectDamage);
                NearWeapon(magic.ShootSpeed * (magic.ExtraUpdates + 1),
                    evaluation.SpeedPixelsPerTick);

                input.ManaCostPerUse = 0; // Space armor/free-use effects.
                Equal(0, WeaponProfileCatalog.Evaluate(input).ManaCostPerUse);
                input.ManaCostKnown = false;
                Equal(WeaponProfileStatus.InvalidResource,
                    WeaponProfileCatalog.Evaluate(input).Status);
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

        private static void AdditionalMagicProfilesCreditOnlyExactPrimary()
        {
            var frost = EvaluateMagicFixture(new MagicFixture(726, 359, 46,
                16f, 12, 12, 12, 0, 3600));
            Equal(WeaponSecondaryEffect.PenetrationNotCredited,
                frost.Profile.SecondaryEffect);

            var poison = EvaluateMagicFixture(new MagicFixture(1308, 265,
                43, 13.5f, 36, 36, 22, 0, 37));
            Equal(WeaponSecondaryEffect.AdditionalPelletsNotCredited,
                poison.Profile.SecondaryEffect);
            Equal(43, poison.DirectDamage);

            var serpent = EvaluateMagicFixture(new MagicFixture(3209, 521,
                40, 8.5f, 29, 29, 9, 1, 3600));
            Equal(WeaponSecondaryEffect.CrystalShardsNotCredited,
                serpent.Profile.SecondaryEffect);
            Equal(1800f, serpent.MaxFlightTicks);
            Equal(17f, serpent.SpeedPixelsPerTick);

            var golden = EvaluateMagicFixture(new MagicFixture(1336, 280,
                30, 10f, 6, 18, 7, 2, 3600));
            Equal(WeaponBallisticKind.
                DiscreteVerticalAccelerationPrimaryProjectile,
                golden.Profile.Ballistics);
            Equal(4, golden.Profile.VerticalAccelerationDelaySubupdates);
            NearWeapon(.075f,
                golden.Profile.VerticalAccelerationPerSubupdate);
            NearWeapon(160f, golden.MaxFlightTicks);
            Equal(WeaponSecondaryEffect.DebuffNotCredited,
                golden.Profile.SecondaryEffect);
            NearWeapon(300f, golden.ApproximateDirectDps);
            // Nine subupdates take exactly three ticks. Updates 5..9 add
            // .075,.15,.225,.30,.375 Y before movement (1.125 total), so
            // the solver must recover a level launch rather than use a
            // continuous-gravity approximation.
            var goldenAim = WeaponAimSolver.Solve(golden, new Vec2(),
                new Vec2(90f, 1.125f), new Vec2());
            True(goldenAim.CanFire);
            NearWeapon(3f, goldenAim.LeadTicks, .0001f);
            NearWeapon(0f, goldenAim.AimWorld.Y, .0001f);

            var fork = EvaluateMagicFixture(new MagicFixture(1445, 295,
                70, 8f, 30, 30, 18, 0, 3600, false));
            Equal(WeaponSecondaryEffect.ExplosionNotCredited,
                fork.Profile.SecondaryEffect);
            NearWeapon(70f * 60f / 31f, fork.ApproximateDirectDps);
        }

        private static void StarCannonProfilesKeepNativeProjectileAndResource()
        {
            var star = WeaponProfileCatalog.Evaluate(StarCannonInput(197));
            True(star.IsSupported, "Star Cannon should use its native star projectile");
            Equal(197, star.Profile.Key.WeaponId);
            Equal(75, star.Profile.Key.AmmoId);
            Equal(955, star.Profile.ProjectileId);
            Equal(WeaponFireMode.Automatic, star.Profile.FireMode);
            Equal(WeaponBallisticKind.StraightPrimaryProjectile,
                star.Profile.Ballistics);
            Equal(OutputRouteKind.StraightRanged, star.Profile.OutputKind);
            Equal(OutputResourceKind.Ammunition, star.Profile.ResourceKind);
            Equal(14f, star.Profile.DefaultWeaponShootSpeed);
            Equal(0f, star.Profile.DefaultAmmoShootSpeed);
            Equal(55, star.Profile.DefaultWeaponDamage);
            Equal(0, star.Profile.DefaultAmmoDamage);
            Equal(12, star.Profile.DefaultUseTime);
            Equal(12, star.Profile.DefaultUseAnimation);
            Equal(0, star.Profile.DefaultExtraUpdates);
            Equal(3600, star.Profile.DefaultLifetimeSubupdates);
            Equal(955, WeaponProfileCatalog.ResolveProjectileForPair(197,
                75, 0));
            Equal(55, star.DirectDamage);
            NearWeapon(14f, star.SpeedPixelsPerTick);
            NearWeapon(3600f, star.MaxFlightTicks);
            NearWeapon(55f * 60f / 12f, star.ApproximateDirectDps);
            Equal(WeaponSecondaryEffect.None, star.Profile.SecondaryEffect);

            var super = WeaponProfileCatalog.Evaluate(StarCannonInput(4060));
            True(super.IsSupported,
                "Super Star Cannon should use its native star projectile");
            Equal(4060, super.Profile.Key.WeaponId);
            Equal(75, super.Profile.Key.AmmoId);
            Equal(728, super.Profile.ProjectileId);
            Equal(20f, super.Profile.DefaultWeaponShootSpeed);
            Equal(60, super.Profile.DefaultWeaponDamage);
            Equal(18, super.Profile.DefaultUseTime);
            Equal(18, super.Profile.DefaultUseAnimation);
            Equal(0, super.Profile.DefaultExtraUpdates);
            Equal(3600, super.Profile.DefaultLifetimeSubupdates);
            Equal(60, super.DirectDamage);
            NearWeapon(20f, super.SpeedPixelsPerTick);
            NearWeapon(3600f, super.MaxFlightTicks);
            NearWeapon(60f * 60f / 18f, super.ApproximateDirectDps);
            Equal(WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited,
                super.Profile.SecondaryEffect);

            var wrongAmmo = StarCannonInput(197);
            wrongAmmo.AmmoId = 97;
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(wrongAmmo).Status);
            var wrongProjectile = StarCannonInput(197);
            wrongProjectile.ProjectileId = 14;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(wrongProjectile).Status);
            Equal(14, WeaponProfileCatalog.ResolveProjectileForPair(197,
                75, 14));
            var missing = StarCannonInput(197);
            missing.HasAmmo = false;
            missing.AmmoId = 0;
            Equal(WeaponProfileStatus.MissingAmmo,
                WeaponProfileCatalog.Evaluate(missing).Status);
        }

        private static WeaponProfileEvaluation EvaluateMagicFixture(
            MagicFixture magic)
        {
            return WeaponProfileCatalog.Evaluate(new WeaponProfileInput
            {
                WeaponId = magic.Id,
                AmmoId = 0,
                ProjectileId = magic.ProjectileId,
                ProjectileExtraUpdates = magic.ExtraUpdates,
                ProjectileLifetimeSubupdates = magic.Lifetime,
                WeaponShootSpeed = magic.ShootSpeed,
                WeaponDamageAfterModifiers = magic.Damage,
                UseTime = magic.UseTime,
                UseAnimation = magic.UseAnimation,
                ReuseDelay = 0,
                AutoReuse = magic.AutoReuse,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = magic.Mana
            });
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

        private static void RedRyderAndGatligatorKeepNativePairSemantics()
        {
            var red = new WeaponProfileInput
            {
                WeaponId = 1870, AmmoId = 97, ProjectileId = 14,
                ProjectileExtraUpdates = 1,
                ProjectileLifetimeSubupdates = 600,
                WeaponShootSpeed = 8f, AmmoShootSpeed = 4f,
                WeaponDamageAfterModifiers = 20, AmmoBaseDamage = 7,
                AmmoDamageMultiplier = 1f, UseTime = 38,
                UseAnimation = 38, ReuseDelay = 0, AutoReuse = true,
                HasAmmo = true
            };
            var redEvaluation = WeaponProfileCatalog.Evaluate(red);
            True(redEvaluation.IsSupported);
            Equal(14, redEvaluation.Profile.ProjectileId);
            Equal(WeaponFireMode.Automatic, redEvaluation.Profile.FireMode);
            Equal(38, redEvaluation.Profile.DefaultUseTime);
            NearWeapon(0f, redEvaluation.SpreadSpeedPixelsPerTick);
            NearWeapon(24f, redEvaluation.SpeedPixelsPerTick);
            Equal(14, WeaponProfileCatalog.ResolveProjectileForPair(1870,
                97, 14)); // Red Ryder does not convert the selected ammo.

            var gatligator = red;
            gatligator.WeaponId = 2270;
            gatligator.WeaponDamageAfterModifiers = 21;
            gatligator.UseTime = 7;
            gatligator.UseAnimation = 7;
            var gatEvaluation = WeaponProfileCatalog.Evaluate(gatligator);
            True(gatEvaluation.IsSupported);
            Equal(14, gatEvaluation.Profile.ProjectileId);
            Equal(7, gatEvaluation.Profile.DefaultUseTime);
            // Two additive envelopes are +/-3.5 per component. On the one-in-
            // three scale branch, each component can additionally become
            // 0.4x..1.6x. The bound intentionally exceeds nominal velocity;
            // the independent positive minimum-path bound keeps range valid.
            var expectedSpread = (.6f * 12f + 1.6f * 3.5f *
                1.4142135623730951f) * 2f;
            NearWeapon(expectedSpread,
                gatEvaluation.SpreadSpeedPixelsPerTick, .00001f);
            True(gatEvaluation.SpreadSpeedPixelsPerTick >
                gatEvaluation.SpeedPixelsPerTick);
            var expectedMinimumSpeed = .4f * (12f - 3.5f *
                1.4142135623730951f) * 2f;
            NearWeapon(expectedMinimumSpeed * 300f,
                gatEvaluation.ConservativeRangePixels, .02f);
            Equal(14, WeaponProfileCatalog.ResolveProjectileForPair(2270,
                97, 14)); // Gatligator also keeps the ammo projectile.

            red.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(red).Status);
            gatligator.UseTime = 8;
            gatligator.UseAnimation = 8;
            // Live prefixes may alter timing, but auto-reuse and native
            // animation shape remain validated independently.
            True(WeaponProfileCatalog.Evaluate(gatligator).IsSupported);
        }

        private static void CrystalStormUsesNativeDragProfile()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(
                CrystalStormInput());
            True(evaluation.IsSupported);
            Equal(WeaponBallisticKind.ExponentialDragPrimaryProjectile,
                evaluation.Profile.Ballistics);
            Equal(94, evaluation.Profile.ProjectileId);
            Equal(35, evaluation.DirectDamage);
            Equal(5, evaluation.ManaCostPerUse);
            Equal(150f, evaluation.MaxFlightTicks);
            Equal(16f, evaluation.SpeedPixelsPerTick);
            NearWeapon(1.6f * 1.4142135623730951f,
                evaluation.SpreadSpeedPixelsPerTick);
            var movement = .985f * (1f - (float)Math.Pow(.985f, 150f)) /
                (1f - .985f);
            NearWeapon((16f - 1.6f * 1.4142135623730951f) * movement,
                evaluation.ConservativeRangePixels, .01f);

            var changedUpdates = CrystalStormInput();
            changedUpdates.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(changedUpdates).Status);
            var shorterLife = CrystalStormInput();
            shorterLife.ProjectileLifetimeSubupdates = 100;
            Equal(100f,
                WeaponProfileCatalog.Evaluate(shorterLife).MaxFlightTicks);
        }

        private static void OnyxBlasterUsesNativeRadialEnvelope()
        {
            var input = new WeaponProfileInput
            {
                WeaponId = 3788, AmmoId = 97, ProjectileId = 14,
                ProjectileExtraUpdates = 1,
                ProjectileLifetimeSubupdates = 600,
                WeaponShootSpeed = 7f, AmmoShootSpeed = 4f,
                WeaponDamageAfterModifiers = 24, AmmoBaseDamage = 7,
                AmmoDamageMultiplier = 1f, UseTime = 48,
                UseAnimation = 48, ReuseDelay = 0, AutoReuse = false,
                HasAmmo = true
            };
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);
            // Native adds one arbitrary-direction vector whose magnitude is
            // bounded by 2px/update. It is not a +/-2 component square.
            NearWeapon(4f, evaluation.SpreadSpeedPixelsPerTick);
            NearWeapon(22f, evaluation.SpeedPixelsPerTick);
            NearWeapon(18f * 300f,
                evaluation.ConservativeRangePixels);
            Equal(WeaponSecondaryEffect.AdditionalPelletsNotCredited,
                evaluation.Profile.SecondaryEffect);
            Equal(31, evaluation.DirectDamage); // one conservative ammo path
            NearWeapon(31f * 60f / 49f,
                evaluation.ApproximateDirectDps);
        }

        private static void PewMaticHornUsesNativeProjectileOverride()
        {
            // PickAmmo still contributes the bullet's damage and speed, but
            // ItemCheck_Shoot replaces every selected bullet projectile with
            // type 968 and adds independent +/-1.125 velocity components.
            var input = new WeaponProfileInput
            {
                WeaponId = 5117, AmmoId = 97, ProjectileId = 968,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 3600,
                WeaponShootSpeed = 14f, AmmoShootSpeed = 4f,
                WeaponDamageAfterModifiers = 20, AmmoBaseDamage = 7,
                AmmoDamageMultiplier = 1f, UseTime = 15,
                UseAnimation = 15, ReuseDelay = 0, AutoReuse = true,
                HasAmmo = true
            };
            var evaluation = WeaponProfileCatalog.Evaluate(input);
            True(evaluation.IsSupported);
            Equal(968, evaluation.Profile.ProjectileId);
            Equal(WeaponBallisticKind.StraightPrimaryProjectile,
                evaluation.Profile.Ballistics);
            Equal(0, evaluation.Profile.DefaultExtraUpdates);
            Equal(3600, evaluation.Profile.DefaultLifetimeSubupdates);
            NearWeapon(18f, evaluation.SpeedPixelsPerTick);
            NearWeapon(1.125f * 1.4142135623730951f,
                evaluation.SpreadSpeedPixelsPerTick);
            NearWeapon((18f - 1.125f * 1.4142135623730951f) * 3600f,
                evaluation.ConservativeRangePixels);
            Equal(27, evaluation.DirectDamage);
            Equal(WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited,
                evaluation.Profile.SecondaryEffect);
            Equal(968, WeaponProfileCatalog.ResolveProjectileForPair(5117,
                97, 14));
            // An unknown/stale live sample must remain unknown. Do not
            // silently replace it with the ammo default or Pew override.
            Equal(12345, WeaponProfileCatalog.ResolveProjectileForPair(5117,
                97, 12345));

            input.ProjectileId = 14;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);
            input.ProjectileId = 968;
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void CrystalStormAimIntegratesDragBeforeMovement()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(
                CrystalStormInput());
            var firstStep = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(16f * .985f, 0f), new Vec2(), 90f, 100, 100);
            True(firstStep.CanFire);
            NearWeapon(1f, firstStep.LeadTicks, .00001f);

            var target = new Vec2(200f, 0f);
            var stationary = WeaponAimSolver.Solve(evaluation, new Vec2(),
                target, new Vec2(), 90f, 100, 100);
            True(stationary.CanFire);
            var expectedTime = Math.Log(1d - 200d * (1d - .985d) /
                (16d * .985d)) / Math.Log(.985d);
            NearWeapon((float)expectedTime, stationary.LeadTicks, .0001f);
            True(Math.Abs(stationary.LeadTicks - 12.5f) > .5f);

            var origin = new Vec2(100f, 200f);
            target = new Vec2(400f, 260f);
            var targetVelocity = new Vec2(-1f, .5f);
            var moving = WeaponAimSolver.Solve(evaluation, origin, target,
                targetVelocity, 90f, 1000, 1000);
            True(moving.CanFire);
            var nominalTravel = 16d * .985d *
                (1d - Math.Pow(.985d, moving.LeadTicks)) /
                (1d - .985d);
            NearWeapon((float)nominalTravel,
                (moving.AimWorld - origin).Length, .002f);
            NearWeapon(target.X + targetVelocity.X * moving.LeadTicks,
                moving.AimWorld.X, .001f);

            Equal(WeaponAimStatus.BeyondPredictionHorizon,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(400f, 0f), new Vec2(), 10f, 1000, 1000).Status);
            var maximumNativeRange = 16d * .985d *
                (1d - Math.Pow(.985d, 150d)) / (1d - .985d);
            Equal(WeaponAimStatus.BeyondLifetime,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2((float)(maximumNativeRange + 5d), 0f),
                    new Vec2(), 200f, 1000, 1000).Status);
            var asymptoticRange = 16d * .985d / (1d - .985d);
            Equal(WeaponAimStatus.NoIntercept,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2((float)(asymptoticRange + 5d), 0f),
                    new Vec2(), 200f, 1000, 1000).Status);
        }

        private static void CrystalStormRequiresEnvelopeInsideLiveHitbox()
        {
            var evaluation = WeaponProfileCatalog.Evaluate(
                CrystalStormInput());
            var target = new Vec2(200f, 0f);
            var wide = WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                new Vec2(), 90f, 80, 80);
            True(wide.CanFire);
            NearWeapon(200f / 16f *
                (1.6f * 1.4142135623730951f),
                wide.SpreadRadiusPixels, .001f);
            Equal(WeaponAimStatus.SpreadEnvelopeOutsideTarget,
                WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                    new Vec2(), 90f, 60, 60).Status);
            // Compatibility callers have only a 16px safety budget and must
            // fail closed instead of treating the decelerating shot as linear.
            Equal(WeaponAimStatus.SpreadEnvelopeOutsideTarget,
                WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                    new Vec2()).Status);
            Equal(WeaponAimStatus.InvalidInput,
                WeaponAimSolver.Solve(evaluation, new Vec2(), target,
                    new Vec2(), 90f, 0, 80).Status);
        }

        private static void CombatPlannerPassesLiveHitboxToCrystalStorm()
        {
            var snapshot = MagicScenario();
            var evaluation = WeaponProfileCatalog.Evaluate(
                CrystalStormInput());
            snapshot.Weapon.Damage = evaluation.DirectDamage;
            snapshot.Weapon.UseTime = 7;
            snapshot.Weapon.ShootSpeed = evaluation.SpeedPixelsPerTick;
            snapshot.Weapon.WeaponId = 518;
            snapshot.Weapon.ProjectileId = 94;
            snapshot.Weapon.Profile = evaluation;
            var mana = snapshot.Weapon.Mana;
            mana.ManaCostPerUse = 5;
            snapshot.Weapon.Mana = mana;

            var target = snapshot.Targets[0];
            target.Velocity = new Vec2();
            target.Width = target.Height = 80;
            target.Position = new Vec2(snapshot.Player.Center.X + 200f -
                target.Width * .5f, snapshot.Player.Center.Y -
                target.Height * .5f);
            snapshot.Targets[0] = target;
            var wide = new CombatPlanner(new PlannerSettings()).Plan(snapshot);
            True(wide.Fire,
                "production planner must pass the live 80x80 hitbox");

            target.Width = target.Height = 60;
            target.Position = new Vec2(snapshot.Player.Center.X + 200f -
                target.Width * .5f, snapshot.Player.Center.Y -
                target.Height * .5f);
            snapshot.Targets[0] = target;
            var narrow = new CombatPlanner(new PlannerSettings()).Plan(snapshot);
            False(narrow.Fire,
                "the same shot must close when its envelope exceeds the live hitbox");
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
            foreach (var weaponId in new[] { 98, 434, 1870, 2270 })
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
                WeaponProfileCatalog.Evaluate(new WeaponProfileInput { WeaponId = 2797, HasAmmo = false }).Status);
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
            var crystalInput = CrystalStormInput();
            var arrowInput = BowInput(3516, 40, magicQuiver: true);
            var demonScytheInput = DemonScytheInput();
            var origin = new Vec2(100, 200);
            var target = new Vec2(600, 400);
            var velocity = new Vec2(-5, 7);
            var demonTarget = new Vec2(100, 0);
            var checksum = 0f;
            for (var warm = 0; warm < 1000; warm++)
            {
                checksum += WeaponAimSolver.Solve(WeaponProfileCatalog.Evaluate(input), origin, target, velocity).LeadTicks;
                checksum += WeaponAimSolver.Solve(
                    WeaponProfileCatalog.Evaluate(crystalInput), origin,
                    target, velocity, 90f, 1000, 1000).LeadTicks;
                checksum += WeaponAimSolver.Solve(
                    WeaponProfileCatalog.Evaluate(arrowInput), origin,
                    target, velocity).LeadTicks;
                checksum += WeaponAimSolver.Solve(
                    WeaponProfileCatalog.Evaluate(demonScytheInput),
                    new Vec2(), demonTarget, new Vec2(), 90f).LeadTicks;
            }
            readAllocated();
            var before = readAllocated();
            for (var index = 0; index < 10000; index++)
            {
                input.AnimationRemainingAtShot = 1 + index % 11;
                var evaluation = WeaponProfileCatalog.Evaluate(input);
                var aim = WeaponAimSolver.Solve(evaluation, origin, target, velocity);
                checksum += aim.LeadTicks;
                var crystal = WeaponProfileCatalog.Evaluate(crystalInput);
                var dragAim = WeaponAimSolver.Solve(crystal, origin,
                    target, velocity, 90f, 1000, 1000);
                checksum += dragAim.LeadTicks;
                var arrow = WeaponProfileCatalog.Evaluate(arrowInput);
                checksum += WeaponAimSolver.Solve(arrow, origin, target,
                    velocity).LeadTicks;
                var demonScythe = WeaponProfileCatalog.Evaluate(
                    demonScytheInput);
                checksum += WeaponAimSolver.Solve(demonScythe, new Vec2(),
                    demonTarget, new Vec2(), 90f).LeadTicks;
            }
            var allocated = readAllocated() - before;
            True(checksum > 0f);
            Equal(0L, allocated);
        }

        private static void NearWeapon(float expected, float actual, float tolerance = .0001f) =>
            True(Math.Abs(expected - actual) <= tolerance, "weapon numeric mismatch: expected " + expected + ", actual " + actual);

        private struct GunFixture
        {
            public int Id, Damage, UseTime, UseAnimation, ReuseDelay;
            public float ShootSpeed;
            public bool AutoReuse, ConvertsMusket;

            public GunFixture(int id, int damage, int useTime,
                int useAnimation, int reuseDelay, float shootSpeed,
                bool autoReuse, bool convertsMusket)
            {
                Id = id; Damage = damage; UseTime = useTime;
                UseAnimation = useAnimation; ReuseDelay = reuseDelay;
                ShootSpeed = shootSpeed; AutoReuse = autoReuse;
                ConvertsMusket = convertsMusket;
            }
        }

        private struct AmmoFixture
        {
            public int Id, ProjectileId, Damage, ExtraUpdates, Lifetime;
            public float ShootSpeed;

            public AmmoFixture(int id, int projectileId, int damage,
                float shootSpeed, int extraUpdates, int lifetime)
            {
                Id = id; ProjectileId = projectileId; Damage = damage;
                ShootSpeed = shootSpeed; ExtraUpdates = extraUpdates;
                Lifetime = lifetime;
            }
        }

        private struct DartWeaponFixture
        {
            public int Id, Damage, UseTime, UseAnimation;
            public float ShootSpeed;
            public bool AutoReuse;

            public DartWeaponFixture(int id, int damage, int useTime,
                int useAnimation, float shootSpeed, bool autoReuse)
            {
                Id = id; Damage = damage; UseTime = useTime;
                UseAnimation = useAnimation; ShootSpeed = shootSpeed;
                AutoReuse = autoReuse;
            }
        }

        private struct DartFixture
        {
            public int Id, ProjectileId, Damage, ExtraUpdates, Lifetime,
                Delay;
            public float ShootSpeed, Acceleration;

            public DartFixture(int id, int projectileId, int damage,
                float shootSpeed, int extraUpdates, int lifetime, int delay,
                float acceleration)
            {
                Id = id; ProjectileId = projectileId; Damage = damage;
                ShootSpeed = shootSpeed; ExtraUpdates = extraUpdates;
                Lifetime = lifetime; Delay = delay;
                Acceleration = acceleration;
            }
        }

        private struct BowFixture
        {
            public int Id, Damage, UseTime, UseAnimation;
            public float ShootSpeed;
            public bool AutoReuse, WoodenArrowOnly;

            public BowFixture(int id, int damage, int useTime,
                int useAnimation, float shootSpeed, bool autoReuse,
                bool woodenArrowOnly = false)
            {
                Id = id; Damage = damage; UseTime = useTime;
                UseAnimation = useAnimation; ShootSpeed = shootSpeed;
                AutoReuse = autoReuse; WoodenArrowOnly = woodenArrowOnly;
            }
        }

        private struct ArrowFixture
        {
            public int Id, ProjectileId, Damage, ExtraUpdates, Lifetime;
            public float ShootSpeed;

            public ArrowFixture(int id, int projectileId, int damage,
                float shootSpeed, int extraUpdates, int lifetime)
            {
                Id = id; ProjectileId = projectileId; Damage = damage;
                ShootSpeed = shootSpeed; ExtraUpdates = extraUpdates;
                Lifetime = lifetime;
            }
        }

        private struct ProjectileMotionFixture
        {
            public int ExtraUpdates, Lifetime, Delay;
            public float Acceleration;

            public ProjectileMotionFixture(int extraUpdates, int lifetime,
                int delay, float acceleration)
            {
                ExtraUpdates = extraUpdates; Lifetime = lifetime;
                Delay = delay; Acceleration = acceleration;
            }
        }

        private static WeaponProfileInput BowInput(int weaponId, int ammoId,
            bool archery = false, bool magicQuiver = false,
            bool hasMoltenQuiver = false, bool sharpBarb = false,
            bool harpyCharm = false)
        {
            var bow = FindBow(weaponId);
            var arrow = FindArrow(ammoId);
            var projectile = ExpectedBowProjectile(weaponId,
                arrow.ProjectileId, hasMoltenQuiver);
            var motion = ProjectileMotion(projectile);
            return new WeaponProfileInput
            {
                WeaponId = weaponId,
                AmmoId = ammoId,
                ProjectileId = projectile,
                ProjectileExtraUpdates = motion.ExtraUpdates,
                ProjectileLifetimeSubupdates = motion.Lifetime,
                WeaponShootSpeed = bow.ShootSpeed,
                AmmoShootSpeed = arrow.ShootSpeed,
                WeaponDamageAfterModifiers = bow.Damage,
                AmmoBaseDamage = arrow.Damage,
                AmmoDamageMultiplier = 1f,
                UseTime = bow.UseTime,
                UseAnimation = bow.UseAnimation,
                ReuseDelay = 0,
                AutoReuse = bow.AutoReuse,
                HasAmmo = true,
                ArrowStateKnown = true,
                Archery = archery,
                MagicQuiver = magicQuiver,
                HasMoltenQuiver = hasMoltenQuiver,
                SharpBarb = sharpBarb,
                HarpyCharm = harpyCharm
            };
        }

        private static WeaponProfileInput DartInput(DartWeaponFixture weapon,
            DartFixture dart)
        {
            return new WeaponProfileInput
            {
                WeaponId = weapon.Id,
                AmmoId = dart.Id,
                ProjectileId = dart.ProjectileId,
                ProjectileExtraUpdates = dart.ExtraUpdates,
                ProjectileLifetimeSubupdates = dart.Lifetime,
                WeaponShootSpeed = weapon.ShootSpeed,
                AmmoShootSpeed = dart.ShootSpeed,
                WeaponDamageAfterModifiers = weapon.Damage,
                AmmoBaseDamage = dart.Damage,
                AmmoDamageMultiplier = 1f,
                UseTime = weapon.UseTime,
                UseAnimation = weapon.UseAnimation,
                ReuseDelay = 0,
                AutoReuse = weapon.AutoReuse,
                HasAmmo = true
            };
        }

        private static BowFixture FindBow(int id)
        {
            foreach (var value in CommonBowFixtures)
                if (value.Id == id) return value;
            throw new InvalidOperationException("unknown bow fixture " + id);
        }

        private static ArrowFixture FindArrow(int id)
        {
            foreach (var value in CommonArrowFixtures)
                if (value.Id == id) return value;
            throw new InvalidOperationException("unknown arrow fixture " + id);
        }

        private static int ExpectedBowProjectile(int weaponId,
            int rawProjectile, bool molten)
        {
            var projectile = rawProjectile;
            if (weaponId == 3019 && projectile == 1) projectile = 485;
            if (weaponId == 3052) projectile = 495;
            if (weaponId == 2888 && projectile == 1) projectile = 469;
            if (molten && projectile == 1) projectile = 2;
            if (weaponId == 120 && projectile == 1) projectile = 2;
            if (weaponId == 682) projectile = 117;
            if (weaponId == 725) projectile = 120;
            if (weaponId == 2223) projectile = 357;
            return projectile;
        }

        private static ProjectileMotionFixture ProjectileMotion(
            int projectileId)
        {
            switch (projectileId)
            {
                case 1:
                case 2:
                case 4:
                case 103:
                case 474:
                    return new ProjectileMotionFixture(0, 1200, 14, .1f);
                case 41:
                    return new ProjectileMotionFixture(0, 3600, 14, .1f);
                case 5:
                    return new ProjectileMotionFixture(1, 120, 0, 0f);
                case 91:
                    return new ProjectileMotionFixture(0, 1200, 19, .07f);
                case 172:
                    return new ProjectileMotionFixture(0, 1200, 16,
                        .085f);
                case 225:
                case 278:
                case 282:
                    return new ProjectileMotionFixture(1, 1200, 14, .1f);
                case 117:
                    return new ProjectileMotionFixture(2, 1200, 34, .06f);
                case 120:
                    return new ProjectileMotionFixture(1, 1200, 29, .05f);
                case 357:
                    return new ProjectileMotionFixture(2, 600, 0, 0f);
                case 469:
                case 485:
                    return new ProjectileMotionFixture(0, 1200, 0, 0f);
                case 495:
                    return new ProjectileMotionFixture(0, 1200, 29, .04f);
                case 639:
                    return new ProjectileMotionFixture(1, 90, 14, .1f);
                case 1006:
                    return new ProjectileMotionFixture(0, 1200, 14, -.1f);
                case 51:
                    return new ProjectileMotionFixture(0, 600, 14, .1f);
                case 267:
                case 479:
                    return new ProjectileMotionFixture(0, 600, 19, .075f);
                case 477:
                    return new ProjectileMotionFixture(1, 600, 0, 0f);
                case 478:
                    return new ProjectileMotionFixture(0, 300, 19, .075f);
                default:
                    throw new InvalidOperationException(
                        "unknown projectile motion fixture " + projectileId);
            }
        }

        private struct MagicFixture
        {
            public int Id, ProjectileId, Damage, UseTime, UseAnimation,
                Mana, ExtraUpdates, Lifetime;
            public float ShootSpeed;
            public bool AutoReuse;

            public MagicFixture(int id, int projectileId, int damage,
                float shootSpeed, int useTime, int useAnimation, int mana,
                int extraUpdates, int lifetime, bool autoReuse = true)
            {
                Id = id; ProjectileId = projectileId; Damage = damage;
                ShootSpeed = shootSpeed; UseTime = useTime;
                UseAnimation = useAnimation; Mana = mana;
                ExtraUpdates = extraUpdates; Lifetime = lifetime;
                AutoReuse = autoReuse;
            }
        }

        private static readonly GunFixture[] CommonGunFixtures =
        {
            new GunFixture(95, 13, 16, 16, 0, 6f, false, false),
            new GunFixture(96, 31, 32, 32, 0, 9f, false, false),
            new GunFixture(98, 6, 8, 8, 0, 7f, true, false),
            new GunFixture(164, 26, 15, 15, 0, 10f, false, false),
            new GunFixture(219, 30, 14, 14, 0, 13f, false, false),
            new GunFixture(434, 17, 4, 12, 14, 7.75f, true, false),
            new GunFixture(533, 25, 7, 7, 0, 10f, true, false),
            new GunFixture(534, 24, 45, 45, 0, 7f, false, false),
            new GunFixture(679, 36, 34, 34, 0, 6f, true, false),
            new GunFixture(800, 19, 20, 20, 0, 6f, false, false),
            new GunFixture(964, 14, 40, 40, 0, 5.35f, false, false),
            new GunFixture(1254, 200, 36, 36, 0, 16f, false, true),
            new GunFixture(1255, 50, 9, 9, 0, 13.5f, true, true),
            new GunFixture(1265, 30, 9, 9, 0, 13f, true, true),
            new GunFixture(1553, 85, 5, 5, 0, 12f, true, false),
            new GunFixture(1929, 38, 4, 4, 0, 14f, true, false),
            new GunFixture(1870, 20, 38, 38, 0, 8f, true, false),
            new GunFixture(2269, 20, 22, 22, 0, 16f, false, false),
            new GunFixture(2270, 21, 7, 7, 0, 8f, true, false),
            new GunFixture(3788, 24, 48, 48, 0, 7f, false, false),
            new GunFixture(4703, 14, 55, 55, 0, 7f, false, false)
        };

        private static readonly AmmoFixture[] CommonAmmoFixtures =
        {
            new AmmoFixture(97, 14, 7, 4f, 1, 600),
            new AmmoFixture(234, 36, 8, 3f, 1, 600),
            new AmmoFixture(515, 89, 9, 5f, 1, 600),
            new AmmoFixture(546, 104, 15, 5f, 2, 600),
            new AmmoFixture(3104, 14, 7, 4f, 1, 600),
            new AmmoFixture(4915, 14, 9, 4.5f, 1, 600),
            new AmmoFixture(278, 981, 9, 4.5f, 1, 600),
            new AmmoFixture(1302, 242, 11, 4f, 7, 600),
            new AmmoFixture(1335, 279, 13, 5.25f, 2, 600),
            new AmmoFixture(1342, 283, 15, 5.3f, 2, 600),
            new AmmoFixture(1349, 284, 10, 5.1f, 2, 600),
            new AmmoFixture(1350, 285, 10, 4.6f, 2, 600),
            new AmmoFixture(1351, 286, 10, 4.7f, 2, 600),
            new AmmoFixture(1352, 287, 10, 4.6f, 2, 600),
            new AmmoFixture(3567, 638, 20, 2f, 5, 600)
        };

        private static readonly DartWeaponFixture[] CommonDartWeaponFixtures =
        {
            new DartWeaponFixture(281, 9, 25, 25, 11f, true),
            new DartWeaponFixture(986, 27, 35, 35, 13f, true),
            new DartWeaponFixture(3007, 28, 22, 22, 13f, true),
            new DartWeaponFixture(3008, 52, 38, 38, 14.5f, true)
        };

        private static readonly DartFixture[] CommonDartFixtures =
        {
            new DartFixture(283, 51, 4, 0f, 0, 600, 14, .1f),
            new DartFixture(1310, 267, 10, 2f, 0, 600, 19, .075f),
            new DartFixture(3009, 477, 14, 1f, 1, 600, 0, 0f),
            new DartFixture(3010, 478, 9, 3f, 0, 300, 19, .075f),
            new DartFixture(3011, 479, 10, 3f, 0, 600, 19, .075f)
        };

        private static readonly BowFixture[] CommonBowFixtures =
        {
            new BowFixture(39, 4, 30, 30, 6.1f, false),
            new BowFixture(655, 8, 28, 28, 6.6f, false),
            new BowFixture(923, 8, 28, 28, 6.6f, false),
            new BowFixture(658, 6, 29, 29, 6.6f, false),
            new BowFixture(2515, 6, 29, 29, 6.6f, false),
            new BowFixture(2747, 6, 29, 29, 6.6f, false),
            new BowFixture(5282, 10, 25, 25, 6.6f, false),
            new BowFixture(661, 12, 20, 20, 7f, false),
            new BowFixture(3504, 6, 29, 29, 6.6f, false),
            new BowFixture(3498, 7, 28, 28, 6.6f, false),
            new BowFixture(99, 8, 28, 28, 6.6f, false),
            new BowFixture(3492, 9, 27, 27, 6.6f, false),
            new BowFixture(3510, 9, 27, 27, 6.6f, false),
            new BowFixture(3486, 10, 26, 26, 6.6f, false),
            new BowFixture(3516, 11, 26, 26, 6.6f, false),
            new BowFixture(3480, 13, 25, 25, 6.6f, false),
            new BowFixture(4058, 8, 17, 17, 11f, false),
            new BowFixture(44, 14, 25, 25, 6.7f, false),
            new BowFixture(796, 19, 30, 30, 6.7f, false),
            new BowFixture(120, 31, 22, 22, 8f, false),
            new BowFixture(2888, 23, 23, 23, 8f, false),
            new BowFixture(3019, 22, 13, 13, 6f, true, true),
            new BowFixture(435, 35, 23, 23, 9f, true),
            new BowFixture(1187, 37, 22, 22, 9.25f, true),
            new BowFixture(436, 39, 20, 20, 9.5f, true),
            new BowFixture(1194, 40, 19, 19, 9.75f, true),
            new BowFixture(481, 42, 18, 18, 10f, true),
            new BowFixture(1201, 43, 17, 17, 10.5f, true),
            new BowFixture(578, 53, 16, 16, 11f, true),
            new BowFixture(682, 40, 19, 19, 11f, true),
            new BowFixture(725, 39, 14, 14, 10f, true),
            new BowFixture(2223, 80, 20, 20, 7.75f, true),
            new BowFixture(3052, 47, 20, 20, 11f, true),
            new BowFixture(1229, 34, 19, 19, 11.5f, true),
            new BowFixture(2624, 53, 24, 24, 10f, true)
        };

        private static readonly ArrowFixture[] CommonArrowFixtures =
        {
            new ArrowFixture(40, 1, 5, 3f, 0, 1200),
            new ArrowFixture(41, 2, 7, 3.5f, 0, 1200),
            new ArrowFixture(47, 4, 12, 3.4f, 0, 1200),
            new ArrowFixture(51, 5, 10, .5f, 1, 120),
            new ArrowFixture(265, 41, 13, 6.5f, 0, 3600),
            new ArrowFixture(516, 91, 13, 3.5f, 0, 1200),
            new ArrowFixture(545, 103, 17, 4f, 0, 1200),
            new ArrowFixture(988, 172, 9, 3.75f, 0, 1200),
            new ArrowFixture(1235, 225, 16, 4.5f, 1, 1200),
            new ArrowFixture(1334, 278, 16, 4.25f, 1, 1200),
            new ArrowFixture(1341, 282, 19, 4.3f, 1, 1200),
            new ArrowFixture(3003, 474, 8, 3.5f, 0, 1200),
            new ArrowFixture(3103, 1, 5, 3f, 0, 1200),
            new ArrowFixture(3568, 639, 15, 3f, 1, 90),
            new ArrowFixture(5348, 1006, 12, 3f, 0, 1200)
        };

        private static readonly MagicFixture[] CommonMagicFixtures =
        {
            new MagicFixture(127, 20, 20, 10f, 17, 17, 6, 2, 600),
            new MagicFixture(165, 27, 19, 4.5f, 17, 17, 10, 0, 1800),
            new MagicFixture(514, 88, 29, 17f, 12, 12, 8, 4, 600),
            new MagicFixture(518, 94, 35, 16f, 7, 7, 5, 0, 600),
            new MagicFixture(519, 95, 55, 10f, 15, 15, 9, 0, 3600),
            new MagicFixture(726, 359, 46, 16f, 12, 12, 12, 0, 3600),
            new MagicFixture(1295, 260, 90, 15f, 10, 10, 8, 100, 200),
            new MagicFixture(1308, 265, 43, 13.5f, 36, 36, 22, 0, 37),
            new MagicFixture(1336, 280, 30, 10f, 6, 18, 7, 2, 3600),
            new MagicFixture(1444, 294, 80, 6f, 15, 15, 7, 100, 300),
            new MagicFixture(1445, 295, 70, 8f, 30, 30, 18, 0, 3600,
                false),
            new MagicFixture(2188, 355, 44, 14f, 30, 30, 25, 0, 58),
            new MagicFixture(3209, 521, 40, 8.5f, 29, 29, 9, 1, 3600)
        };
    }
}
