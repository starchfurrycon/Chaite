using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunCommonWeaponOutputRegressions()
        {
            Run(nameof(CommonWeaponCatalogPinsNativeEmissionAndResourceFacts),
                CommonWeaponCatalogPinsNativeEmissionAndResourceFacts);
            Run(nameof(TerraBladeProfilePinsIdentityDamageAndCadence),
                TerraBladeProfilePinsIdentityDamageAndCadence);
            Run(nameof(TerraBladeTrajectoryUsesNativeDragAndCollisionBoundary),
                TerraBladeTrajectoryUsesNativeDragAndCollisionBoundary);
            Run(nameof(TerraBladeRejectsIdentityBallisticAndTimingDrift),
                TerraBladeRejectsIdentityBallisticAndTimingDrift);
            Run(nameof(EventideProfilePinsCentralShotAndOneAmmoCycle),
                EventideProfilePinsCentralShotAndOneAmmoCycle);
            Run(nameof(EventideSpawnGeometryDrivesDedicatedConvergenceAim),
                EventideSpawnGeometryDrivesDedicatedConvergenceAim);
            Run(nameof(EventideRejectsIdentityResourceAndTimingDrift),
                EventideRejectsIdentityResourceAndTimingDrift);
            Run(nameof(RazorbladeTyphoonPinsNativeCadenceAndPrefireGate),
                RazorbladeTyphoonPinsNativeCadenceAndPrefireGate);
            Run(nameof(RazorbladeTyphoonRejectsIdentityAndTrajectoryDrift),
                RazorbladeTyphoonRejectsIdentityAndTrajectoryDrift);
            Run(nameof(UnmodeledCommonWeaponsRemainFailClosed),
                UnmodeledCommonWeaponsRemainFailClosed);
        }

        private static WeaponProfileInput TerraBladeInput()
        {
            return new WeaponProfileInput
            {
                WeaponId = 757,
                AmmoId = 0,
                ProjectileId = 985,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 90,
                WeaponShootSpeed = 12f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = 85,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 1f,
                UseTime = 18,
                UseAnimation = 18,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = true,
                HasAmmo = true
            };
        }

        private static WeaponProfileInput EventideInput()
        {
            return new WeaponProfileInput
            {
                WeaponId = 4953,
                AmmoId = 40,
                ProjectileId = 932,
                ProjectileExtraUpdates = 0,
                ProjectileLifetimeSubupdates = 120,
                WeaponShootSpeed = 10f,
                AmmoShootSpeed = 3f,
                WeaponDamageAfterModifiers = 50,
                AmmoBaseDamage = 5,
                AmmoDamageMultiplier = 1f,
                UseTime = 2,
                UseAnimation = 30,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = true,
                HasAmmo = true,
                ArrowStateKnown = true,
                Archery = false,
                MagicQuiver = false,
                HasMoltenQuiver = false,
                SharpBarb = false,
                HarpyCharm = false
            };
        }

        private static WeaponProfileInput RazorbladeTyphoonInput()
        {
            return new WeaponProfileInput
            {
                WeaponId = 2622,
                AmmoId = 0,
                ProjectileId = 409,
                ProjectileExtraUpdates = 2,
                ProjectileLifetimeSubupdates = 300,
                WeaponShootSpeed = 6f,
                AmmoShootSpeed = 0f,
                WeaponDamageAfterModifiers = 85,
                AmmoBaseDamage = 0,
                AmmoDamageMultiplier = 1f,
                UseTime = 40,
                UseAnimation = 40,
                ReuseDelay = 0,
                AnimationRemainingAtShot = 0,
                AutoReuse = true,
                HasAmmo = true,
                ManaCostKnown = true,
                ManaCostPerUse = 20
            };
        }

        private static void CommonWeaponCatalogPinsNativeEmissionAndResourceFacts()
        {
            CommonWeaponEmissionContract emission;
            True(CommonWeaponOutputCatalog.TryGetEmissionContract(757,
                out emission));
            Equal(757, emission.WeaponId);
            Equal(2, emission.NativeProjectileCountPerCycle);
            Equal(1, emission.CreditedProjectileCountPerCycle);
            Equal(985, emission.PrimaryProjectileId);
            Equal(984, emission.AuxiliaryProjectileId);
            Equal(18, emission.ActionCycleTicks);
            Equal(34, emission.NativeDamagingUpdateLimit);
            CommonWeaponNear(5f, emission.PrimaryInitialSpeedMultiplier);
            CommonWeaponNear(1f, emission.PrimaryDamageMultiplier);
            CommonWeaponNear(.75f, CommonWeaponOutputCatalog.
                TerraBladeSubsequentTargetDamageRetention);

            CommonWeaponResourceContract resource;
            True(CommonWeaponOutputCatalog.TryGetResourceContract(757, 0,
                out resource));
            Equal(OutputResourceKind.Melee, resource.Kind);
            Equal(1, resource.NativeUseEventsPerCycle);
            Equal(2, resource.ProjectileCountPerCycle);
            Equal(0, resource.MaximumAmmoConsumedPerCycle);

            True(CommonWeaponOutputCatalog.TryGetEmissionContract(4953,
                out emission));
            Equal(4953, emission.WeaponId);
            Equal(5, emission.NativeProjectileCountPerCycle);
            Equal(5, emission.CreditedProjectileCountPerCycle);
            Equal(932, emission.PrimaryProjectileId);
            Equal(30, emission.ActionCycleTicks);
            Equal(120, emission.NativeDamagingUpdateLimit);
            Equal(2, emission.CentralShotIndex);
            CommonWeaponNear(2f, emission.CentralDamageMultiplier);
            CommonWeaponNear((float)Math.PI / 10f,
                emission.FanStepRadians);
            CommonWeaponNear(40f, emission.SpawnOffsetPixels);

            True(CommonWeaponOutputCatalog.TryGetResourceContract(4953, 40,
                out resource));
            Equal(OutputResourceKind.Ammunition, resource.Kind);
            Equal(5, resource.NativeUseEventsPerCycle);
            Equal(5, resource.ProjectileCountPerCycle);
            Equal(4, resource.FreeOpeningUseEvents);
            Equal(1, resource.MaximumAmmoConsumedPerCycle);

            True(CommonWeaponOutputCatalog.TryGetEmissionContract(2622,
                out emission));
            Equal(2622, emission.WeaponId);
            Equal(1, emission.NativeProjectileCountPerCycle);
            Equal(1, emission.CreditedProjectileCountPerCycle);
            Equal(409, emission.PrimaryProjectileId);
            Equal(40, emission.ActionCycleTicks);
            Equal(300, emission.NativeDamagingUpdateLimit);
            CommonWeaponNear(500f, emission.CollisionReachPixels);

            True(CommonWeaponOutputCatalog.TryGetResourceContract(2622, 0,
                out resource));
            Equal(OutputResourceKind.Mana, resource.Kind);
            Equal(1, resource.NativeUseEventsPerCycle);
            Equal(1, resource.ProjectileCountPerCycle);
            Equal(0, resource.MaximumAmmoConsumedPerCycle);

            False(CommonWeaponOutputCatalog.TryGetEmissionContract(999999,
                out emission));
            False(CommonWeaponOutputCatalog.TryGetResourceContract(4953,
                999999, out resource));
        }

        private static void TerraBladeProfilePinsIdentityDamageAndCadence()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(757, 0, out profile));
            Equal(new WeaponProfileKey(757, 0), profile.Key);
            Equal(985, profile.ProjectileId);
            Equal("terra-blade-wave", profile.OutputRouteId);
            Equal(OutputRouteKind.DirectedMeleeWave, profile.OutputKind);
            Equal(OutputResourceKind.Melee, profile.ResourceKind);
            Equal(WeaponBallisticKind.TerraBladeWave,
                profile.Ballistics);
            Equal(85, profile.DefaultWeaponDamage);
            Equal(12f, profile.DefaultWeaponShootSpeed);
            Equal(18, profile.DefaultUseTime);
            Equal(18, profile.DefaultUseAnimation);
            Equal(0, profile.DefaultReuseDelay);
            Equal(true, profile.DefaultAutoReuse);
            Equal(0, profile.DefaultManaCost);
            Equal(0, profile.DefaultExtraUpdates);
            Equal(90, profile.DefaultLifetimeSubupdates);

            var evaluation = WeaponProfileCatalog.Evaluate(
                TerraBladeInput());
            True(evaluation.IsSupported, evaluation.Reason);
            Equal(85, evaluation.DirectDamage);
            CommonWeaponNear(60f, evaluation.SpeedPixelsPerTick);
            CommonWeaponNear(34f, evaluation.MaxFlightTicks);
            CommonWeaponNear(85f * 60f / 18f,
                evaluation.ApproximateDirectDps);
            CommonWeaponNear(
                CommonWeaponOutputCatalog.TerraBladeWavePosition(34, 60f),
                evaluation.ConservativeRangePixels, .001f);
            Equal(985, WeaponProfileCatalog.ResolveProjectileForPair(757,
                0, 985));
        }

        private static void TerraBladeTrajectoryUsesNativeDragAndCollisionBoundary()
        {
            // AI_191 applies the .94 retention before movement while speed is
            // above eight pixels/update.  The damaging prefix ends after the
            // 34th movement; later native updates carry damage=0.
            CommonWeaponNear(0f,
                CommonWeaponOutputCatalog.TerraBladeWavePosition(0, 60f));
            CommonWeaponNear(56.4f,
                CommonWeaponOutputCatalog.TerraBladeWavePosition(1, 60f));
            CommonWeaponNear(109.416f,
                CommonWeaponOutputCatalog.TerraBladeWavePosition(2, 60f));
            CommonWeaponNear(825.7906f,
                CommonWeaponOutputCatalog.TerraBladeWavePosition(34, 60f),
                .001f);
            Equal(0f, CommonWeaponOutputCatalog.TerraBladeWavePosition(1,
                60f, true));
            Equal(0f, CommonWeaponOutputCatalog.TerraBladeWavePosition(34,
                60f, true));

            var evaluation = WeaponProfileCatalog.Evaluate(
                TerraBladeInput());
            var firstUpdate = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(56.4f, 0f), new Vec2(), 90f);
            True(firstUpdate.CanFire, firstUpdate.Status.ToString());
            CommonWeaponNear(1f, firstUpdate.LeadTicks, .0001f);

            var maximum = CommonWeaponOutputCatalog.TerraBladeWavePosition(
                34, 60f);
            Equal(WeaponAimStatus.BeyondLifetime,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(maximum + 1f, 0f), new Vec2(), 90f).Status);
        }

        private static void TerraBladeRejectsIdentityBallisticAndTimingDrift()
        {
            var input = TerraBladeInput();
            input.ProjectileId = 984;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = TerraBladeInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = TerraBladeInput();
            input.ProjectileLifetimeSubupdates = 89;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = TerraBladeInput();
            input.WeaponShootSpeed = 12.01f;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = TerraBladeInput();
            input.UseTime = 19;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = TerraBladeInput();
            input.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = TerraBladeInput();
            input.AmmoId = 40;
            input.ProjectileId = 932;
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void EventideProfilePinsCentralShotAndOneAmmoCycle()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(4953, 40, out profile));
            Equal(new WeaponProfileKey(4953, 40), profile.Key);
            Equal(932, profile.ProjectileId);
            Equal("eventide-wooden-arrow", profile.OutputRouteId);
            Equal(OutputRouteKind.ConvergingRangedBurst,
                profile.OutputKind);
            Equal(OutputResourceKind.Ammunition, profile.ResourceKind);
            Equal(WeaponBallisticKind.EventideFiveShotConvergence,
                profile.Ballistics);
            Equal(50, profile.DefaultWeaponDamage);
            Equal(5, profile.DefaultAmmoDamage);
            Equal(10f, profile.DefaultWeaponShootSpeed);
            Equal(3f, profile.DefaultAmmoShootSpeed);
            Equal(2, profile.DefaultUseTime);
            Equal(30, profile.DefaultUseAnimation);
            Equal(0, profile.DefaultReuseDelay);
            Equal(true, profile.DefaultAutoReuse);
            Equal(0, profile.DefaultExtraUpdates);
            Equal(120, profile.DefaultLifetimeSubupdates);

            var evaluation = WeaponProfileCatalog.Evaluate(EventideInput());
            True(evaluation.IsSupported, evaluation.Reason);
            // All five reviewed wooden-arrow projectiles converge. The new
            // animation begins with an ordinary 50+5 shot, while the complete
            // cycle has four ordinary hits and one doubled central hit.
            Equal(55, evaluation.DirectDamage);
            CommonWeaponNear(26f, evaluation.SpeedPixelsPerTick);
            CommonWeaponNear(120f, evaluation.MaxFlightTicks);
            CommonWeaponNear(55f * 6f * 60f / 30f,
                evaluation.ApproximateDirectDps);
            var centralInput = EventideInput();
            centralInput.AnimationRemainingAtShot = 26;
            var central = WeaponProfileCatalog.Evaluate(centralInput);
            Equal(2, central.BurstShotIndex);
            Equal(110, central.DirectDamage);
            Equal(932, WeaponProfileCatalog.ResolveProjectileForPair(4953,
                40, 1));
        }

        private static void EventideSpawnGeometryDrivesDedicatedConvergenceAim()
        {
            var direction = new Vec2(1f, 0f);
            var first = CommonWeaponOutputCatalog.EventideSpawnOffset(0,
                direction);
            var center = CommonWeaponOutputCatalog.EventideSpawnOffset(2,
                direction);
            var last = CommonWeaponOutputCatalog.EventideSpawnOffset(4,
                direction);
            CommonWeaponNear(40f, first.Length);
            CommonWeaponNear(40f, center.Length);
            CommonWeaponNear(40f, last.Length);
            CommonWeaponNear(40f, center.X);
            CommonWeaponNear(0f, center.Y);
            CommonWeaponNear(first.X, last.X);
            CommonWeaponNear(first.Y, -last.Y);

            // Native normalizes before applying the 40px fan offset.
            center = CommonWeaponOutputCatalog.EventideSpawnOffset(2,
                new Vec2(3f, 4f));
            CommonWeaponNear(24f, center.X);
            CommonWeaponNear(32f, center.Y);

            // The credited central shot starts 40px downrange and travels at
            // (weapon 10 + wooden-arrow 3) * 2 = 26px/tick. A generic solver
            // starting at the player would incorrectly report 300/26 ticks.
            var centralInput = EventideInput();
            centralInput.AnimationRemainingAtShot = 26;
            var evaluation = WeaponProfileCatalog.Evaluate(centralInput);
            var aim = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(300f, 0f), new Vec2(), 90f);
            True(aim.CanFire, aim.Status.ToString());
            CommonWeaponNear(10f, aim.LeadTicks, .0001f);
            CommonWeaponNear(300f, aim.AimWorld.X, .0001f);
        }

        private static void EventideRejectsIdentityResourceAndTimingDrift()
        {
            var input = EventideInput();
            input.ProjectileId = 1;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = EventideInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = EventideInput();
            input.ProjectileLifetimeSubupdates = 119;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = EventideInput();
            input.UseTime = 3;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = EventideInput();
            input.UseAnimation = 29;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = EventideInput();
            input.HasAmmo = false;
            Equal(WeaponProfileStatus.MissingAmmo,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = EventideInput();
            input.ArrowStateKnown = false;
            Equal(WeaponProfileStatus.InvalidProjectileFeatures,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = EventideInput();
            input.AmmoId = 41;
            Equal(WeaponProfileStatus.UnsupportedCombination,
                WeaponProfileCatalog.Evaluate(input).Status);
        }

        private static void RazorbladeTyphoonPinsNativeCadenceAndPrefireGate()
        {
            WeaponProfile profile;
            True(WeaponProfileCatalog.TryGet(2622, 0, out profile));
            Equal(new WeaponProfileKey(2622, 0), profile.Key);
            Equal(409, profile.ProjectileId);
            Equal("razorblade-typhoon-homing", profile.OutputRouteId);
            Equal(OutputRouteKind.HomingMagicProjectile, profile.OutputKind);
            Equal(OutputResourceKind.Mana, profile.ResourceKind);
            Equal(WeaponBallisticKind.RazorbladeTyphoonHoming,
                profile.Ballistics);
            Equal(6f, profile.DefaultWeaponShootSpeed);
            Equal(85, profile.DefaultWeaponDamage);
            Equal(40, profile.DefaultUseTime);
            Equal(40, profile.DefaultUseAnimation);
            Equal(20, profile.DefaultManaCost);
            Equal(2, profile.DefaultExtraUpdates);
            Equal(300, profile.DefaultLifetimeSubupdates);

            var evaluation = WeaponProfileCatalog.Evaluate(
                RazorbladeTyphoonInput());
            True(evaluation.IsSupported, evaluation.Reason);
            Equal(85, evaluation.DirectDamage);
            CommonWeaponNear(18.015f, evaluation.SpeedPixelsPerTick,
                .0001f);
            CommonWeaponNear(100f, evaluation.MaxFlightTicks);
            // The production gate waits for every own type-409 projectile to
            // expire, so it cannot claim the native 40-tick use cadence as a
            // sustained one-hit guarantee.
            CommonWeaponNear(85f * 60f / 100f,
                evaluation.ApproximateDirectDps);
            CommonWeaponNear(6.0025f,
                CommonWeaponOutputCatalog.RazorbladeTyphoonTravelDistance(
                    1, 6f), .0001f);
            CommonWeaponNear(18.015f,
                CommonWeaponOutputCatalog.RazorbladeTyphoonTravelDistance(
                    3, 6f), .0001f);
            CommonWeaponNear(1912.875f,
                CommonWeaponOutputCatalog.RazorbladeTyphoonTravelDistance(
                    300, 6f), .001f);

            var aim = WeaponAimSolver.Solve(evaluation, new Vec2(),
                new Vec2(100f, 0f), new Vec2(), 90f, 80, 80);
            True(aim.CanFire, aim.Status.ToString());

            var observation = new RazorbladeTyphoonFireObservation
            {
                Known = true,
                SpawnCenterKnown = true,
                SpawnCenter = new Vec2(10f, 20f),
                NoActiveOwnedProjectiles = true,
                PrefireSelectedNpcKey = 7,
                PrefireSelectedTargetHomingReady = true
            };
            True(observation.PermitsTarget(7));
            False(observation.PermitsTarget(6));
            observation.NoActiveOwnedProjectiles = false;
            False(observation.PermitsTarget(7));
            observation.NoActiveOwnedProjectiles = true;
            observation.PrefireSelectedTargetHomingReady = false;
            False(observation.PermitsTarget(7));
            observation.PrefireSelectedTargetHomingReady = true;
            observation.SpawnCenterKnown = false;
            False(observation.PermitsTarget(7));
        }

        private static void RazorbladeTyphoonRejectsIdentityAndTrajectoryDrift()
        {
            var input = RazorbladeTyphoonInput();
            input.ProjectileId = 408;
            Equal(WeaponProfileStatus.ProjectileMismatch,
                WeaponProfileCatalog.Evaluate(input).Status);

            input = RazorbladeTyphoonInput();
            input.ProjectileExtraUpdates = 1;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = RazorbladeTyphoonInput();
            input.ProjectileLifetimeSubupdates = 299;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = RazorbladeTyphoonInput();
            input.WeaponShootSpeed = 6.01f;
            Equal(WeaponProfileStatus.InvalidBallistics,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = RazorbladeTyphoonInput();
            input.UseTime = 39;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = RazorbladeTyphoonInput();
            input.AutoReuse = false;
            Equal(WeaponProfileStatus.InvalidTiming,
                WeaponProfileCatalog.Evaluate(input).Status);
            input = RazorbladeTyphoonInput();
            input.ManaCostKnown = false;
            Equal(WeaponProfileStatus.InvalidResource,
                WeaponProfileCatalog.Evaluate(input).Status);

            var evaluation = WeaponProfileCatalog.Evaluate(
                RazorbladeTyphoonInput());
            Equal(WeaponAimStatus.BeyondLifetime,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(500.01f, 0f), new Vec2(), 90f, 80, 80).Status);
            Equal(WeaponAimStatus.BeyondPredictionHorizon,
                WeaponAimSolver.Solve(evaluation, new Vec2(),
                    new Vec2(490f, 0f), new Vec2(), 10f, 80, 80).Status);
        }

        private static void UnmodeledCommonWeaponsRemainFailClosed()
        {
            var weaponIds = new[] { 273, 3029, 3540, 3541, 2611 };
            var ammoIds = new[] { 0, 40, 40, 0, 0 };
            var projectileIds = new[] { 972, 1, 630, 633, 404 };
            for (var i = 0; i < weaponIds.Length; i++)
            {
                WeaponProfile profile;
                False(WeaponProfileCatalog.TryGet(weaponIds[i], ammoIds[i],
                    out profile), "unexpected production profile for " +
                    weaponIds[i]);
                var rejected = WeaponProfileCatalog.Evaluate(
                    new WeaponProfileInput
                    {
                        WeaponId = weaponIds[i],
                        AmmoId = ammoIds[i],
                        ProjectileId = projectileIds[i],
                        HasAmmo = true
                    });
                Equal(WeaponProfileStatus.UnsupportedCombination,
                    rejected.Status);
                Equal(WeaponAimStatus.UnsupportedWeapon,
                    WeaponAimSolver.Solve(rejected, new Vec2(),
                        new Vec2(100f, 0f), new Vec2()).Status);

                CommonWeaponEmissionContract emission;
                CommonWeaponResourceContract resource;
                False(CommonWeaponOutputCatalog.TryGetEmissionContract(
                    weaponIds[i], out emission));
                False(CommonWeaponOutputCatalog.TryGetResourceContract(
                    weaponIds[i], ammoIds[i], out resource));
            }
        }

        private static void CommonWeaponNear(float expected, float actual,
            float tolerance = .0001f)
        {
            True(Math.Abs(expected - actual) <= tolerance,
                "common-weapon numeric mismatch: expected " + expected +
                ", actual " + actual);
        }
    }
}
