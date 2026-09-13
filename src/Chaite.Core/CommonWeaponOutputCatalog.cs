using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    /// <summary>
    /// The complete native emission set for one reviewed item-animation cycle.
    /// CreditedProjectileCountPerCycle is deliberately smaller when a native
    /// projectile cannot be used to certify Boss-pattern output.
    /// </summary>
    public struct CommonWeaponEmissionContract
    {
        public int WeaponId;
        public int NativeProjectileCountPerCycle;
        public int CreditedProjectileCountPerCycle;
        public int PrimaryProjectileId;
        public int AuxiliaryProjectileId;
        public int ActionCycleTicks;
        public int NativeDamagingUpdateLimit;
        public float PrimaryInitialSpeedMultiplier;
        public float PrimaryDamageMultiplier;
        public int CentralShotIndex;
        public int CentralDamageMultiplier;
        public float FanStepRadians;
        public float SpawnOffsetPixels;
        public float CollisionReachPixels;
        public float CollisionHalfAngleRadians;

        public bool IsSpecified => WeaponId > 0 &&
            NativeProjectileCountPerCycle > 0 &&
            CreditedProjectileCountPerCycle > 0 &&
            CreditedProjectileCountPerCycle <= NativeProjectileCountPerCycle &&
            PrimaryProjectileId > 0 && ActionCycleTicks > 0;
    }

    /// <summary>Finite native resource/cadence facts for one complete cycle.</summary>
    public struct CommonWeaponResourceContract
    {
        public OutputResourceKind Kind;
        public int NativeUseEventsPerCycle;
        public int ProjectileCountPerCycle;
        public int FreeOpeningUseEvents;
        public int MaximumAmmoConsumedPerCycle;

        public bool IsSpecified => Kind != OutputResourceKind.Unspecified &&
            NativeUseEventsPerCycle > 0 && ProjectileCountPerCycle > 0 &&
            FreeOpeningUseEvents >= 0 &&
            FreeOpeningUseEvents <= NativeUseEventsPerCycle &&
            MaximumAmmoConsumedPerCycle >= 0;
    }

    /// <summary>
    /// Hash-locked 1.4.5.8 models for common weapons whose native output is not
    /// a generic straight single projectile. Assembly SHA256:
    /// 960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3.
    ///
    /// Terra Blade evidence: Item.SetDefaults case 757; ItemCheck_Shoot creates
    /// both 984 and 985, multiplying the 985 launch velocity by five; type 985
    /// AI_191 applies 0.94 velocity retention before movement. With ai[1]=18,
    /// Lerp(18, 43, 0.65)=34.25, so integer localAI updates 1..34 can deal
    /// damage and update 35 disables it before movement/damage processing.
    /// Damage_PVE_Inner snapshots the current hit before its type-985 switch,
    /// then retains 0.75 projectile damage only for a later distinct NPC. With
    /// localNPCHitCooldown=-1 the aimed Boss entity therefore receives full
    /// damage once; cross-target follow-up damage is not credited. The
    /// owner-anchored type 984 swing remains declared but is not credited to
    /// ranged Boss output.
    ///
    /// Eventide evidence: Item.SetDefaults case 4953; ItemCheck_Shoot permits
    /// exactly five two-tick events in a 30-tick animation. Wooden Arrow and
    /// Endless Quiver become type 932 during PickAmmo, so every event receives
    /// Eventide's x2 speed; index two receives x2 damage. Native spawn offsets
    /// form an 18-degree fan at 40 pixels and every projectile converges on the
    /// cursor. Only the fifth event can reach the ordinary ammo-consumption
    /// check. Type 932 is straight AI_181 with timeLeft 120; Magic Quiver adds
    /// an extra update only after its first native update.
    ///
    /// Razorblade Typhoon evidence: Item.SetDefaults case 2622 has one
    /// no-ammo type-409 shot (85 damage, 6 speed, 40/40 auto-reuse, 20 mana).
    /// There is no item-2622 branch in ItemCheck_Shoot, so the normal one-shot
    /// path is retained. Type 409 is 30x30, aiStyle 71, timeLeft 300 and
    /// extraUpdates 2. Every subupdate it may acquire a visible chaseable NPC
    /// within 500px, rotates 10% of the remaining angular delta while its
    /// target is within Manhattan 1000px, then adds .0025 speed before moving.
    /// Its tile ricochet and repeated six-tick NPC immunity hits are not
    /// credited. The production controller permits a cast only after a
    /// same-frame, no-overlap pre-fire observation; that observation is not
    /// a claim that the later spawned projectile will lock or hit the target.
    /// </summary>
    public static class CommonWeaponOutputCatalog
    {
        public const int TerraBladeWeaponId = 757;
        public const int TerraBladeSwingProjectileId = 984;
        public const int TerraBladeWaveProjectileId = 985;
        public const int EventideWeaponId = 4953;
        public const int EventideProjectileId = 932;
        public const int WoodenArrowItemId = 40;
        public const int EndlessQuiverItemId = 3103;
        public const int RazorbladeTyphoonWeaponId = 2622;
        public const int RazorbladeTyphoonProjectileId = 409;

        public const int TerraBladeUseTicks = 18;
        public const int TerraBladeProjectileLifetime = 90;
        public const int TerraBladeDamagingUpdates = 34;
        public const float TerraBladeLaunchMultiplier = 5f;
        public const float TerraBladeVelocityRetention = .94f;
        public const float TerraBladeDamageMultiplier = 1f;
        public const float TerraBladeSubsequentTargetDamageRetention = .75f;
        public const float TerraBladeCollisionReach = 90f;
        public const float TerraBladeCollisionHalfAngle =
            0.7853981633974483f;

        public const int EventideUseTime = 2;
        public const int EventideUseAnimation = 30;
        public const int EventideShotCount = 5;
        public const int EventideCentralShotIndex = 2;
        public const float EventideSpeedMultiplier = 2f;
        public const float EventideSpawnDistance = 40f;
        public const float EventideFanStepRadians =
            0.3141592653589793f;
        public const int EventideProjectileLifetime = 120;

        public const int RazorbladeTyphoonUseTicks = 40;
        public const int RazorbladeTyphoonProjectileLifetime = 300;
        public const int RazorbladeTyphoonExtraUpdates = 2;
        public const int RazorbladeTyphoonUpdatesPerTick =
            RazorbladeTyphoonExtraUpdates + 1;
        // The controller deliberately waits until every prior same-owner
        // type-409 projectile has expired before allowing another cast. Its
        // 300 subupdate lifetime is therefore the only safe cadence used for
        // approximate output admission; native input still has a 40-tick use
        // animation and is declared separately in the emission contract.
        public const int RazorbladeTyphoonNoOverlapCycleTicks =
            RazorbladeTyphoonProjectileLifetime /
            RazorbladeTyphoonUpdatesPerTick;
        public const float RazorbladeTyphoonTurnFraction = .1f;
        public const float RazorbladeTyphoonSpeedGainPerUpdate = .0025f;
        public const float RazorbladeTyphoonNativeAcquireDistance = 500f;
        public const float RazorbladeTyphoonTargetRetainManhattanDistance =
            1000f;
        public const float RazorbladeTyphoonProjectileHalfSize = 15f;

        private static readonly WeaponProfile TerraBladeProfile =
            new WeaponProfile(TerraBladeWeaponId, 0, "Terra Blade",
                "terra-blade-wave", TerraBladeWaveProjectileId,
                TerraBladeWaveProjectileId, WeaponFireMode.Automatic,
                WeaponSpreadModel.None, 0f,
                WeaponBallisticKind.TerraBladeWave,
                TerraBladeVelocityRetention, TerraBladeDamagingUpdates, 0f,
                OutputRouteKind.DirectedMeleeWave,
                OutputResourceKind.Melee, false, 12f, 0f, 85, 0,
                TerraBladeUseTicks, TerraBladeUseTicks, 0, true, 0, 0,
                TerraBladeProjectileLifetime);

        private static readonly WeaponProfile EventideWoodenProfile =
            CreateEventideProfile(WoodenArrowItemId, "Wooden Arrow",
                "wooden-arrow");
        private static readonly WeaponProfile EventideEndlessProfile =
            CreateEventideProfile(EndlessQuiverItemId, "Endless Quiver",
                "endless-quiver");

        private static readonly WeaponProfile RazorbladeTyphoonProfile =
            new WeaponProfile(RazorbladeTyphoonWeaponId, 0,
                "Razorblade Typhoon", "razorblade-typhoon-homing",
                RazorbladeTyphoonProjectileId,
                RazorbladeTyphoonProjectileId, WeaponFireMode.Automatic,
                WeaponSpreadModel.None, 0f,
                WeaponBallisticKind.RazorbladeTyphoonHoming, 1f,
                RazorbladeTyphoonProjectileLifetime,
                RazorbladeTyphoonProjectileHalfSize,
                OutputRouteKind.HomingMagicProjectile,
                OutputResourceKind.Mana, false, 6f, 0f, 85, 0,
                RazorbladeTyphoonUseTicks, RazorbladeTyphoonUseTicks, 0,
                true, 20, RazorbladeTyphoonExtraUpdates,
                RazorbladeTyphoonProjectileLifetime);

        private static readonly Dictionary<WeaponProfileKey, WeaponProfile>
            Profiles = BuildProfiles();

        public static int Count => Profiles.Count;

        public static bool IsWeapon(int weaponId) =>
            weaponId == TerraBladeWeaponId || weaponId == EventideWeaponId ||
            weaponId == RazorbladeTyphoonWeaponId;

        public static bool IsAmmunitionWeapon(int weaponId) =>
            weaponId == EventideWeaponId;

        public static bool TryGet(int weaponId, int ammoId,
            out WeaponProfile profile) => Profiles.TryGetValue(
                new WeaponProfileKey(weaponId, ammoId), out profile);

        internal static bool IsCommonProfile(WeaponProfile profile) =>
            profile != null &&
            (profile.Ballistics == WeaponBallisticKind.TerraBladeWave ||
             profile.Ballistics == WeaponBallisticKind.
                 EventideFiveShotConvergence ||
             profile.Ballistics == WeaponBallisticKind.
                 RazorbladeTyphoonHoming);

        internal static bool RequiresTargetHitbox(WeaponProfile profile) =>
            profile != null && profile.Ballistics ==
                WeaponBallisticKind.RazorbladeTyphoonHoming;

        public static bool TryGetProjectileDefaults(int projectileId,
            out int extraUpdates, out int lifetimeSubupdates)
        {
            extraUpdates = 0;
            if (projectileId == TerraBladeWaveProjectileId)
            {
                lifetimeSubupdates = TerraBladeProjectileLifetime;
                return true;
            }
            if (projectileId == EventideProjectileId)
            {
                lifetimeSubupdates = EventideProjectileLifetime;
                return true;
            }
            if (projectileId == RazorbladeTyphoonProjectileId)
            {
                extraUpdates = RazorbladeTyphoonExtraUpdates;
                lifetimeSubupdates = RazorbladeTyphoonProjectileLifetime;
                return true;
            }
            lifetimeSubupdates = 0;
            return false;
        }

        public static bool TryGetEmissionContract(int weaponId,
            out CommonWeaponEmissionContract contract)
        {
            if (weaponId == TerraBladeWeaponId)
            {
                contract = new CommonWeaponEmissionContract
                {
                    WeaponId = TerraBladeWeaponId,
                    NativeProjectileCountPerCycle = 2,
                    CreditedProjectileCountPerCycle = 1,
                    PrimaryProjectileId = TerraBladeWaveProjectileId,
                    AuxiliaryProjectileId = TerraBladeSwingProjectileId,
                    ActionCycleTicks = TerraBladeUseTicks,
                    NativeDamagingUpdateLimit = TerraBladeDamagingUpdates,
                    PrimaryInitialSpeedMultiplier =
                        TerraBladeLaunchMultiplier,
                    PrimaryDamageMultiplier = TerraBladeDamageMultiplier,
                    CentralShotIndex = -1,
                    CentralDamageMultiplier = 1,
                    CollisionReachPixels = TerraBladeCollisionReach,
                    CollisionHalfAngleRadians =
                        TerraBladeCollisionHalfAngle
                };
                return true;
            }
            if (weaponId == EventideWeaponId)
            {
                contract = new CommonWeaponEmissionContract
                {
                    WeaponId = EventideWeaponId,
                    NativeProjectileCountPerCycle = EventideShotCount,
                    CreditedProjectileCountPerCycle = EventideShotCount,
                    PrimaryProjectileId = EventideProjectileId,
                    AuxiliaryProjectileId = 0,
                    ActionCycleTicks = EventideUseAnimation,
                    NativeDamagingUpdateLimit =
                        EventideProjectileLifetime,
                    PrimaryInitialSpeedMultiplier =
                        EventideSpeedMultiplier,
                    PrimaryDamageMultiplier = 1f,
                    CentralShotIndex = EventideCentralShotIndex,
                    CentralDamageMultiplier = 2,
                    FanStepRadians = EventideFanStepRadians,
                    SpawnOffsetPixels = EventideSpawnDistance,
                    CollisionReachPixels = 40f,
                    CollisionHalfAngleRadians = 0f
                };
                return true;
            }
            if (weaponId == RazorbladeTyphoonWeaponId)
            {
                contract = new CommonWeaponEmissionContract
                {
                    WeaponId = RazorbladeTyphoonWeaponId,
                    NativeProjectileCountPerCycle = 1,
                    CreditedProjectileCountPerCycle = 1,
                    PrimaryProjectileId = RazorbladeTyphoonProjectileId,
                    AuxiliaryProjectileId = 0,
                    ActionCycleTicks = RazorbladeTyphoonUseTicks,
                    NativeDamagingUpdateLimit =
                        RazorbladeTyphoonProjectileLifetime,
                    PrimaryInitialSpeedMultiplier = 1f,
                    PrimaryDamageMultiplier = 1f,
                    CentralShotIndex = -1,
                    CentralDamageMultiplier = 1,
                    CollisionReachPixels =
                        RazorbladeTyphoonNativeAcquireDistance,
                    CollisionHalfAngleRadians = 0f
                };
                return true;
            }
            contract = default(CommonWeaponEmissionContract);
            return false;
        }

        public static bool TryGetResourceContract(int weaponId, int ammoId,
            out CommonWeaponResourceContract contract)
        {
            if (weaponId == TerraBladeWeaponId && ammoId == 0)
            {
                contract = new CommonWeaponResourceContract
                {
                    Kind = OutputResourceKind.Melee,
                    NativeUseEventsPerCycle = 1,
                    ProjectileCountPerCycle = 2,
                    FreeOpeningUseEvents = 1,
                    MaximumAmmoConsumedPerCycle = 0
                };
                return true;
            }
            if (weaponId == EventideWeaponId &&
                (ammoId == WoodenArrowItemId ||
                 ammoId == EndlessQuiverItemId))
            {
                contract = new CommonWeaponResourceContract
                {
                    Kind = OutputResourceKind.Ammunition,
                    NativeUseEventsPerCycle = EventideShotCount,
                    ProjectileCountPerCycle = EventideShotCount,
                    FreeOpeningUseEvents = 4,
                    MaximumAmmoConsumedPerCycle =
                        ammoId == EndlessQuiverItemId ? 0 : 1
                };
                return true;
            }
            if (weaponId == RazorbladeTyphoonWeaponId && ammoId == 0)
            {
                contract = new CommonWeaponResourceContract
                {
                    Kind = OutputResourceKind.Mana,
                    NativeUseEventsPerCycle = 1,
                    ProjectileCountPerCycle = 1,
                    FreeOpeningUseEvents = 0,
                    MaximumAmmoConsumedPerCycle = 0
                };
                return true;
            }
            contract = default(CommonWeaponResourceContract);
            return false;
        }

        /// <summary>
        /// Exact free-flight distance through the last damage-bearing update.
        /// A known tile collision invalidates the reviewed free-flight prefix;
        /// zero is returned so callers cannot treat it as through-wall range.
        /// NaN marks invalid speed/update inputs or an update after damage ends.
        /// </summary>
        public static float TerraBladeWavePosition(int update,
            float initialSpeed, bool tileCollision = false)
        {
            if (tileCollision) return 0f;
            if (update < 0 || update > TerraBladeDamagingUpdates ||
                !FinitePositive(initialSpeed))
                return float.NaN;
            var velocity = initialSpeed;
            var position = 0f;
            for (var step = 0; step < update; step++)
            {
                if (velocity > 8f) velocity *= TerraBladeVelocityRetention;
                position += velocity;
            }
            return position;
        }

        /// <summary>
        /// Native Eventide fan spawn relative to the player origin. Rightward
        /// aim reverses the authored shot order; every returned offset is 40 px.
        /// </summary>
        public static Vec2 EventideSpawnOffset(int shotIndex,
            Vec2 aimDirection)
        {
            if (shotIndex < 0 || shotIndex >= EventideShotCount ||
                !Finite(aimDirection))
                return new Vec2(float.NaN, float.NaN);
            var direction = aimDirection.Normalized();
            if (direction.LengthSquared < .999f)
                return new Vec2(float.NaN, float.NaN);
            var ordinal = shotIndex - EventideCentralShotIndex;
            if (direction.X >= 0f) ordinal = -ordinal;
            var angle = ordinal * EventideFanStepRadians;
            var cosine = (float)Math.Cos(angle);
            var sine = (float)Math.Sin(angle);
            return new Vec2(
                (direction.X * cosine - direction.Y * sine) *
                    EventideSpawnDistance,
                (direction.X * sine + direction.Y * cosine) *
                    EventideSpawnDistance);
        }

        /// <summary>
        /// Unobstructed path length after a number of type-409 native updates.
        /// AI_071 raises speed before each movement. This deliberately does
        /// not claim a net displacement after homing turns or tile ricochets.
        /// </summary>
        public static float RazorbladeTyphoonTravelDistance(int update,
            float initialSpeed)
        {
            if (update < 0 || update > RazorbladeTyphoonProjectileLifetime ||
                !FinitePositive(initialSpeed))
                return float.NaN;
            var distance = 0f;
            var speed = initialSpeed;
            for (var step = 0; step < update; step++)
            {
                speed += RazorbladeTyphoonSpeedGainPerUpdate;
                distance += speed;
            }
            return distance;
        }

        internal static WeaponProfileEvaluation Evaluate(
            WeaponProfileInput input, WeaponProfile profile)
        {
            if (profile == null || !IsCommonProfile(profile) ||
                input.WeaponId != profile.Key.WeaponId ||
                input.AmmoId != profile.Key.AmmoId)
                return Rejected(WeaponProfileStatus.UnsupportedCombination,
                    profile);
            if (profile.Key.WeaponId == EventideWeaponId && !input.HasAmmo)
                return Rejected(WeaponProfileStatus.MissingAmmo, profile);
            if (input.ProjectileId != profile.ProjectileId)
                return Rejected(WeaponProfileStatus.ProjectileMismatch,
                    profile);

            if (profile.Key.WeaponId == TerraBladeWeaponId)
                return EvaluateTerraBlade(input, profile);
            if (profile.Key.WeaponId == EventideWeaponId)
                return EvaluateEventide(input, profile);
            return EvaluateRazorbladeTyphoon(input, profile);
        }

        internal static WeaponAimSolution SolveAim(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks)
        {
            return SolveAim(weapon, origin, target, targetVelocity,
                maxLeadTicks, 16, 16);
        }

        internal static WeaponAimSolution SolveAim(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks, int targetWidth,
            int targetHeight)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported || !IsCommonProfile(weapon.Profile))
                return result;
            if (!Finite(origin) || !Finite(target) || !Finite(targetVelocity) ||
                !FinitePositive(maxLeadTicks))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }
            if (weapon.Profile.Ballistics == WeaponBallisticKind.
                TerraBladeWave)
                return SolveTerraBlade(weapon, origin, target,
                    targetVelocity, maxLeadTicks);
            if (weapon.Profile.Ballistics == WeaponBallisticKind.
                EventideFiveShotConvergence)
                return SolveEventide(weapon, origin, target,
                    targetVelocity, maxLeadTicks);
            return SolveRazorbladeTyphoon(weapon, origin, target,
                targetVelocity, maxLeadTicks, targetWidth, targetHeight);
        }

        private static WeaponProfileEvaluation EvaluateTerraBlade(
            WeaponProfileInput input, WeaponProfile profile)
        {
            if (!Nearly(input.WeaponShootSpeed, 12f) ||
                !FiniteNonnegative(input.AmmoShootSpeed) ||
                input.AmmoShootSpeed != 0f || input.AmmoBaseDamage != 0 ||
                !FiniteNonnegative(input.AmmoDamageMultiplier) ||
                input.ProjectileExtraUpdates != 0 ||
                input.ProjectileLifetimeSubupdates !=
                    TerraBladeProjectileLifetime)
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            if (input.WeaponDamageAfterModifiers < 1)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            if (input.UseTime != TerraBladeUseTicks ||
                input.UseAnimation != TerraBladeUseTicks ||
                input.ReuseDelay != 0 || !input.AutoReuse ||
                input.AnimationRemainingAtShot < 0 ||
                input.AnimationRemainingAtShot > TerraBladeUseTicks)
                return Rejected(WeaponProfileStatus.InvalidTiming, profile);
            if (input.ManaCostPerUse != 0)
                return Rejected(WeaponProfileStatus.InvalidResource, profile);

            var initialSpeed = input.WeaponShootSpeed *
                TerraBladeLaunchMultiplier;
            var range = TerraBladeWavePosition(TerraBladeDamagingUpdates,
                initialSpeed);
            // Damage_PVE_Inner computes dmg before the type-985 switch. That
            // switch reduces projectile.damage only for a later distinct
            // target, while localNPCHitCooldown=-1 prevents a second hit on
            // this target. Credit the aimed first hit and no multipart
            // follow-up.
            var damage = input.WeaponDamageAfterModifiers;
            var dps = damage * (60f / TerraBladeUseTicks);
            if (damage < 1 || !FinitePositive(initialSpeed) ||
                !FinitePositive(range) || !FinitePositive(dps))
                return Rejected(damage < 1 ? WeaponProfileStatus.InvalidDamage :
                    WeaponProfileStatus.InvalidBallistics, profile);

            return new WeaponProfileEvaluation
            {
                Status = WeaponProfileStatus.Supported,
                Profile = profile,
                SpeedPixelsPerTick = initialSpeed,
                MaxFlightTicks = TerraBladeDamagingUpdates,
                ConservativeRangePixels = range,
                DirectDamage = damage,
                ApproximateDirectDps = dps,
                SpreadSpeedPixelsPerTick = 0f,
                BurstShotIndex = 0,
                ManaCostPerUse = 0,
                InitialSpeedPixelsPerSubupdate = initialSpeed,
                SpreadSpeedPixelsPerSubupdate = 0f,
                FirstTickProjectileUpdates = 1,
                SustainedProjectileUpdatesPerTick = 1
            };
        }

        private static WeaponProfileEvaluation EvaluateEventide(
            WeaponProfileInput input, WeaponProfile profile)
        {
            if (!input.ArrowStateKnown || input.HarpyCharm)
                return Rejected(
                    WeaponProfileStatus.InvalidProjectileFeatures, profile);
            if (!Nearly(input.WeaponShootSpeed, 10f) ||
                !Nearly(input.AmmoShootSpeed, 3f) ||
                input.ProjectileExtraUpdates != 0 ||
                input.ProjectileLifetimeSubupdates !=
                    EventideProjectileLifetime)
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            if (input.WeaponDamageAfterModifiers < 1 ||
                input.AmmoBaseDamage < 0 ||
                !FiniteNonnegative(input.AmmoDamageMultiplier))
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            if (input.UseTime != EventideUseTime ||
                input.UseAnimation != EventideUseAnimation ||
                input.ReuseDelay != 0 || !input.AutoReuse ||
                input.AnimationRemainingAtShot < 0 ||
                input.AnimationRemainingAtShot > EventideUseAnimation)
                return Rejected(WeaponProfileStatus.InvalidTiming, profile);
            if (input.ManaCostPerUse != 0)
                return Rejected(WeaponProfileStatus.InvalidResource, profile);

            var speedPerUpdate = input.WeaponShootSpeed +
                input.AmmoShootSpeed;
            if (input.MagicQuiver) speedPerUpdate *= 1.1f;
            if (input.Archery && speedPerUpdate < 20f)
                speedPerUpdate = Math.Min(20f, speedPerUpdate * 1.2f);
            speedPerUpdate *= EventideSpeedMultiplier;

            var firstUpdates = 1;
            var sustainedUpdates = input.MagicQuiver ? 2 : 1;
            var lifetime = LifetimeTicks(EventideProjectileLifetime,
                firstUpdates, sustainedUpdates);
            var range = speedPerUpdate * EventideProjectileLifetime;

            var ammoBase = (long)input.AmmoBaseDamage +
                (input.SharpBarb ? 1L : 0L);
            if (ammoBase < 0 || ammoBase > int.MaxValue)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            var ammoDamage = (float)ammoBase *
                input.AmmoDamageMultiplier;
            if (!FiniteNonnegative(ammoDamage) || ammoDamage >= int.MaxValue ||
                (long)input.WeaponDamageAfterModifiers + (long)ammoDamage >
                    int.MaxValue)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            var damage = input.WeaponDamageAfterModifiers + (int)ammoDamage;
            if (damage > int.MaxValue / 2)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            // Four ordinary events plus one double-damage central event.
            var dps = damage * (6f * 60f / EventideUseAnimation);
            if (damage < 1 || !FinitePositive(speedPerUpdate) ||
                !FinitePositive(lifetime) || !FinitePositive(range) ||
                !FinitePositive(dps))
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);

            var projectedAnimation = input.AnimationRemainingAtShot == 0 ?
                EventideUseAnimation - 1 :
                Math.Min(EventideUseAnimation - 1,
                    input.AnimationRemainingAtShot);
            var shotIndex = (EventideUseAnimation - projectedAnimation) / 2;
            if (shotIndex < 0) shotIndex = 0;
            if (shotIndex >= EventideShotCount)
                shotIndex = EventideShotCount - 1;

            return new WeaponProfileEvaluation
            {
                Status = WeaponProfileStatus.Supported,
                Profile = profile,
                SpeedPixelsPerTick = speedPerUpdate * sustainedUpdates,
                MaxFlightTicks = lifetime,
                ConservativeRangePixels = range,
                DirectDamage = shotIndex == EventideCentralShotIndex ?
                    damage * 2 : damage,
                ApproximateDirectDps = dps,
                SpreadSpeedPixelsPerTick = 0f,
                BurstShotIndex = shotIndex,
                ManaCostPerUse = 0,
                InitialSpeedPixelsPerSubupdate = speedPerUpdate,
                SpreadSpeedPixelsPerSubupdate = 0f,
                FirstTickProjectileUpdates = firstUpdates,
                SustainedProjectileUpdatesPerTick = sustainedUpdates
            };
        }

        private static WeaponProfileEvaluation EvaluateRazorbladeTyphoon(
            WeaponProfileInput input, WeaponProfile profile)
        {
            if (!Nearly(input.WeaponShootSpeed, 6f) ||
                !FiniteNonnegative(input.AmmoShootSpeed) ||
                input.AmmoShootSpeed != 0f || input.AmmoBaseDamage != 0 ||
                !FiniteNonnegative(input.AmmoDamageMultiplier) ||
                input.ProjectileExtraUpdates != RazorbladeTyphoonExtraUpdates ||
                input.ProjectileLifetimeSubupdates !=
                    RazorbladeTyphoonProjectileLifetime)
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            if (input.WeaponDamageAfterModifiers < 1)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            if (input.UseTime != RazorbladeTyphoonUseTicks ||
                input.UseAnimation != RazorbladeTyphoonUseTicks ||
                input.ReuseDelay != 0 || !input.AutoReuse ||
                input.AnimationRemainingAtShot < 0 ||
                input.AnimationRemainingAtShot > RazorbladeTyphoonUseTicks)
                return Rejected(WeaponProfileStatus.InvalidTiming, profile);
            // Mana-reduction equipment is valid native state, but the adapter
            // must expose the already-discounted cost exactly rather than
            // assuming the default item.mana value.
            if (!input.ManaCostKnown || input.ManaCostPerUse < 0 ||
                input.ManaCostPerUse > 10000)
                return Rejected(WeaponProfileStatus.InvalidResource, profile);

            var initialSpeed = input.WeaponShootSpeed;
            var firstTickDistance = RazorbladeTyphoonTravelDistance(
                RazorbladeTyphoonUpdatesPerTick, initialSpeed);
            var pathLength = RazorbladeTyphoonTravelDistance(
                RazorbladeTyphoonProjectileLifetime, initialSpeed);
            var damage = input.WeaponDamageAfterModifiers;
            // Type 409 can hit the same NPC again after the native owner
            // immunity expires, but that depends on post-impact geometry. The
            // production gate instead requires no existing same-owner 409,
            // so one live projectile occupies the full 100-tick lifetime.
            // This remains an approximate pre-fire value, not a hit promise.
            var dps = damage * (60f /
                RazorbladeTyphoonNoOverlapCycleTicks);
            if (damage < 1 || !FinitePositive(initialSpeed) ||
                !FinitePositive(firstTickDistance) ||
                !FinitePositive(pathLength) || !FinitePositive(dps))
                return Rejected(damage < 1 ? WeaponProfileStatus.InvalidDamage :
                    WeaponProfileStatus.InvalidBallistics, profile);

            return new WeaponProfileEvaluation
            {
                Status = WeaponProfileStatus.Supported,
                Profile = profile,
                SpeedPixelsPerTick = firstTickDistance,
                MaxFlightTicks = RazorbladeTyphoonProjectileLifetime /
                    (float)RazorbladeTyphoonUpdatesPerTick,
                // A longer physical path does not establish acquisition: the
                // native scan happens before the homing loop at the actual
                // projectile spawn centre. The facade supplies that centre
                // separately for each permitted cast.
                ConservativeRangePixels =
                    RazorbladeTyphoonNativeAcquireDistance,
                DirectDamage = damage,
                ApproximateDirectDps = dps,
                SpreadSpeedPixelsPerTick = 0f,
                BurstShotIndex = 0,
                ManaCostPerUse = input.ManaCostPerUse,
                InitialSpeedPixelsPerSubupdate = initialSpeed,
                SpreadSpeedPixelsPerSubupdate = 0f,
                FirstTickProjectileUpdates = RazorbladeTyphoonUpdatesPerTick,
                SustainedProjectileUpdatesPerTick =
                    RazorbladeTyphoonUpdatesPerTick
            };
        }

        private static WeaponAimSolution SolveTerraBlade(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            var limit = Math.Min(maxLeadTicks, weapon.MaxFlightTicks);
            double lead;
            if (!TryFindTerraBladeIntercept(origin, target, targetVelocity,
                    weapon.InitialSpeedPixelsPerSubupdate, limit, out lead))
            {
                result.Status = FailureStatus(weapon, targetVelocity,
                    maxLeadTicks);
                return result;
            }
            result.Status = WeaponAimStatus.Ready;
            result.LeadTicks = (float)lead;
            result.AimWorld = target + targetVelocity * (float)lead;
            result.SpreadRadiusPixels = 0f;
            return result;
        }

        private static WeaponAimSolution SolveEventide(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            var limit = Math.Min(maxLeadTicks, weapon.MaxFlightTicks);
            var aim = target;
            double lead = 0d;
            for (var iteration = 0; iteration < 6; iteration++)
            {
                var direction = (aim - origin).Normalized();
                if (direction.LengthSquared < .999f)
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                // Native shot ordering needs player.direction. It is implied by
                // horizontal cursor direction except for a perfectly vertical
                // non-central shot, which this input surface cannot certify.
                if (weapon.BurstShotIndex != EventideCentralShotIndex &&
                    Math.Abs(direction.X) < .0001f)
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                var offset = EventideSpawnOffset(weapon.BurstShotIndex,
                    direction);
                if (!Finite(offset))
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                var spawn = origin + offset;
                if (!TryFindConstantUpdateIntercept(spawn, target,
                        targetVelocity,
                        weapon.InitialSpeedPixelsPerSubupdate,
                        weapon.FirstTickProjectileUpdates,
                        weapon.SustainedProjectileUpdatesPerTick,
                        limit, out lead))
                {
                    result.Status = FailureStatus(weapon, targetVelocity,
                        maxLeadTicks);
                    return result;
                }
                aim = target + targetVelocity * (float)lead;
            }
            if (!Finite(aim) || lead < 0d || lead > limit + 1e-5)
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }
            result.Status = WeaponAimStatus.Ready;
            result.AimWorld = aim;
            result.LeadTicks = (float)lead;
            result.SpreadRadiusPixels = 0f;
            return result;
        }

        private static WeaponAimSolution SolveRazorbladeTyphoon(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks, int targetWidth,
            int targetHeight)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (targetWidth < 1 || targetHeight < 1 ||
                !FinitePositive(weapon.InitialSpeedPixelsPerSubupdate))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }

            // The native first scan is Euclidean and uses the projectile
            // centre. CombatPlanner passes the exact current muzzle centre
            // only after its facade observation has reproduced that scan.
            var initialDelta = target - origin;
            if (initialDelta.LengthSquared >
                RazorbladeTyphoonNativeAcquireDistance *
                RazorbladeTyphoonNativeAcquireDistance)
            {
                result.Status = targetVelocity.LengthSquared <= 1e-12f ?
                    WeaponAimStatus.BeyondLifetime : WeaponAimStatus.NoIntercept;
                return result;
            }
            if (initialDelta.LengthSquared <= .000001f)
            {
                result.Status = WeaponAimStatus.Ready;
                result.LeadTicks = 0f;
                return result;
            }

            var tickLimit = Math.Min(maxLeadTicks,
                weapon.MaxFlightTicks);
            var updateLimit = (int)Math.Floor(tickLimit *
                RazorbladeTyphoonUpdatesPerTick);
            if (updateLimit < 1)
            {
                result.Status = WeaponAimStatus.BeyondPredictionHorizon;
                return result;
            }
            if (updateLimit > RazorbladeTyphoonProjectileLifetime)
                updateLimit = RazorbladeTyphoonProjectileLifetime;

            // Type 409 is aimed through the actual primary target, then its
            // AI performs target acquisition/turning before every movement.
            // This fixed, allocation-free trace matches that order. It makes
            // no credit for an unobserved tile bounce or a reacquisition after
            // the target leaves the native retain envelope.
            var position = origin;
            var velocity = initialDelta.Normalized() *
                weapon.InitialSpeedPixelsPerSubupdate;
            var locked = true;
            for (var update = 1; update <= updateLimit; update++)
            {
                var elapsedTicks = (update - 1) /
                    (float)RazorbladeTyphoonUpdatesPerTick;
                var movingTarget = target + targetVelocity * elapsedTicks;
                var delta = movingTarget - position;
                if (locked && Math.Abs(delta.X) + Math.Abs(delta.Y) <
                    RazorbladeTyphoonTargetRetainManhattanDistance)
                {
                    TurnRazorbladeVelocityToward(ref velocity, delta);
                }
                else
                {
                    // Native clears ai[0] and can later re-scan only within
                    // 500px. Reacquisition needs live LOS that this pure
                    // solver does not own, so stop certifying curvature.
                    locked = false;
                }
                var length = velocity.Length;
                if (!FinitePositive(length))
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                velocity = velocity * ((length +
                    RazorbladeTyphoonSpeedGainPerUpdate) / length);
                position += velocity;
                var hitTarget = target + targetVelocity * (update /
                    (float)RazorbladeTyphoonUpdatesPerTick);
                if (RazorbladeOverlapsTarget(position, hitTarget,
                    targetWidth, targetHeight))
                {
                    result.Status = WeaponAimStatus.Ready;
                    result.LeadTicks = update /
                        (float)RazorbladeTyphoonUpdatesPerTick;
                    result.SpreadRadiusPixels = 0f;
                    return result;
                }
            }
            result.Status = maxLeadTicks < weapon.MaxFlightTicks ?
                WeaponAimStatus.BeyondPredictionHorizon :
                targetVelocity.LengthSquared <= 1e-12f ?
                    WeaponAimStatus.BeyondLifetime : WeaponAimStatus.NoIntercept;
            return result;
        }

        private static void TurnRazorbladeVelocityToward(ref Vec2 velocity,
            Vec2 targetDelta)
        {
            var velocityLength = velocity.Length;
            var targetLength = targetDelta.Length;
            if (!FinitePositive(velocityLength) || !FinitePositive(targetLength))
                return;
            var velocityAngle = (float)Math.Atan2(velocity.Y, velocity.X);
            var targetAngle = (float)Math.Atan2(targetDelta.Y, targetDelta.X);
            var difference = targetAngle - velocityAngle;
            if (difference > Math.PI) difference -= (float)(Math.PI * 2d);
            if (difference < -Math.PI) difference += (float)(Math.PI * 2d);
            var angle = velocityAngle + difference *
                RazorbladeTyphoonTurnFraction;
            velocity = new Vec2((float)Math.Cos(angle) * velocityLength,
                (float)Math.Sin(angle) * velocityLength);
        }

        private static bool RazorbladeOverlapsTarget(Vec2 projectileCenter,
            Vec2 targetCenter, int targetWidth, int targetHeight)
        {
            return Math.Abs(projectileCenter.X - targetCenter.X) <=
                targetWidth * .5f + RazorbladeTyphoonProjectileHalfSize &&
                Math.Abs(projectileCenter.Y - targetCenter.Y) <=
                targetHeight * .5f + RazorbladeTyphoonProjectileHalfSize;
        }

        private static bool TryFindTerraBladeIntercept(Vec2 origin,
            Vec2 target, Vec2 targetVelocity, float initialSpeed,
            double tickLimit, out double time)
        {
            time = 0d;
            if (!FinitePositive(initialSpeed) || tickLimit < 0d ||
                double.IsNaN(tickLimit) || double.IsInfinity(tickLimit))
                return false;
            if (Vec2.DistanceSquared(origin, target) <= .000001f)
                return true;
            var velocity = (double)initialSpeed;
            var travelled = 0d;
            var segments = (int)Math.Ceiling(tickLimit);
            for (var segment = 0; segment < segments; segment++)
            {
                var start = (double)segment;
                var end = Math.Min(start + 1d, tickLimit);
                if (!(end > start)) continue;
                if (velocity > 8d)
                    velocity *= TerraBladeVelocityRetention;
                if (TryFindRadialRoot(origin, target, targetVelocity,
                        travelled, velocity, start, end, out time))
                    return true;
                travelled += velocity * (end - start);
            }
            return false;
        }

        private static bool TryFindConstantUpdateIntercept(Vec2 origin,
            Vec2 target, Vec2 targetVelocity, float speedPerUpdate,
            int firstTickUpdates, int sustainedUpdates, double tickLimit,
            out double time)
        {
            time = 0d;
            if (!FinitePositive(speedPerUpdate) || firstTickUpdates < 1 ||
                sustainedUpdates < 1 || tickLimit < 0d ||
                double.IsNaN(tickLimit) || double.IsInfinity(tickLimit))
                return false;
            if (Vec2.DistanceSquared(origin, target) <= .000001f)
                return true;
            var travelled = 0d;
            var segments = (int)Math.Ceiling(tickLimit);
            for (var segment = 0; segment < segments; segment++)
            {
                var start = (double)segment;
                var end = Math.Min(start + 1d, tickLimit);
                if (!(end > start)) continue;
                var updates = segment == 0 ? firstTickUpdates :
                    sustainedUpdates;
                var rate = (double)speedPerUpdate * updates;
                if (TryFindRadialRoot(origin, target, targetVelocity,
                        travelled, rate, start, end, out time))
                    return true;
                travelled += rate * (end - start);
            }
            return false;
        }

        private static bool TryFindRadialRoot(Vec2 origin, Vec2 target,
            Vec2 targetVelocity, double travelledAtStart,
            double travelRate, double start, double end, out double root)
        {
            var x = (double)target.X - origin.X;
            var y = (double)target.Y - origin.Y;
            var vx = (double)targetVelocity.X;
            var vy = (double)targetVelocity.Y;
            var travelIntercept = travelledAtStart - travelRate * start;
            var a = vx * vx + vy * vy - travelRate * travelRate;
            var b = 2d * (x * vx + y * vy -
                travelIntercept * travelRate);
            var c = x * x + y * y -
                travelIntercept * travelIntercept;
            return TryFirstQuadraticRoot(a, b, c, start, end, out root);
        }

        private static bool TryFirstQuadraticRoot(double a, double b,
            double c, double minimum, double maximum, out double root)
        {
            root = 0d;
            var scale = Math.Max(1d,
                Math.Max(Math.Abs(a), Math.Max(Math.Abs(b), Math.Abs(c))));
            var epsilon = scale * 1e-12;
            if (Math.Abs(a) <= epsilon)
            {
                if (Math.Abs(b) <= epsilon)
                    return Math.Abs(c) <= epsilon &&
                        AssignRoot(minimum, out root);
                var linear = -c / b;
                return Inside(linear, minimum, maximum) &&
                    AssignRoot(Clamp(linear, minimum, maximum), out root);
            }
            var discriminant = b * b - 4d * a * c;
            var tolerance = 1e-12 * Math.Max(1d,
                b * b + Math.Abs(4d * a * c));
            if (discriminant < -tolerance) return false;
            if (discriminant < 0d) discriminant = 0d;
            var squareRoot = Math.Sqrt(discriminant);
            var q = -.5d * (b + (b >= 0d ? squareRoot : -squareRoot));
            var first = q / a;
            var second = q == 0d ? -b / (2d * a) : c / q;
            var found = false;
            var earliest = double.PositiveInfinity;
            if (Inside(first, minimum, maximum))
            {
                earliest = Clamp(first, minimum, maximum);
                found = true;
            }
            if (Inside(second, minimum, maximum))
            {
                earliest = Math.Min(earliest,
                    Clamp(second, minimum, maximum));
                found = true;
            }
            return found && AssignRoot(earliest, out root);
        }

        private static WeaponAimStatus FailureStatus(
            WeaponProfileEvaluation weapon, Vec2 targetVelocity,
            float maxLeadTicks)
        {
            if (maxLeadTicks < weapon.MaxFlightTicks)
                return WeaponAimStatus.BeyondPredictionHorizon;
            return targetVelocity.LengthSquared <= 1e-12f ?
                WeaponAimStatus.BeyondLifetime : WeaponAimStatus.NoIntercept;
        }

        private static Dictionary<WeaponProfileKey, WeaponProfile>
            BuildProfiles()
        {
            var result = new Dictionary<WeaponProfileKey, WeaponProfile>(4);
            result.Add(TerraBladeProfile.Key, TerraBladeProfile);
            result.Add(EventideWoodenProfile.Key, EventideWoodenProfile);
            result.Add(EventideEndlessProfile.Key, EventideEndlessProfile);
            result.Add(RazorbladeTyphoonProfile.Key,
                RazorbladeTyphoonProfile);
            return result;
        }

        private static WeaponProfile CreateEventideProfile(int ammoId,
            string ammoName, string ammoSlug)
        {
            return new WeaponProfile(EventideWeaponId, ammoId,
                "Eventide / " + ammoName, "eventide-" + ammoSlug,
                1, EventideProjectileId,
                WeaponFireMode.NativeAnimationBurst,
                WeaponSpreadModel.None, 0f,
                WeaponBallisticKind.EventideFiveShotConvergence,
                1f, EventideProjectileLifetime, 0f,
                OutputRouteKind.ConvergingRangedBurst,
                OutputResourceKind.Ammunition, false, 10f, 3f, 50, 5,
                EventideUseTime, EventideUseAnimation, 0, true, 0, 0,
                EventideProjectileLifetime, 0, 0f, true, true);
        }

        private static float LifetimeTicks(int lifetimeSubupdates,
            int firstTickUpdates, int sustainedUpdates)
        {
            if (lifetimeSubupdates <= firstTickUpdates)
                return lifetimeSubupdates / (float)firstTickUpdates;
            return 1f + (lifetimeSubupdates - firstTickUpdates) /
                (float)sustainedUpdates;
        }

        private static WeaponProfileEvaluation Rejected(
            WeaponProfileStatus status, WeaponProfile profile) =>
            new WeaponProfileEvaluation { Status = status, Profile = profile };

        private static bool Nearly(float value, float expected) =>
            Finite(value) && Math.Abs(value - expected) <= .0001f;

        private static bool FinitePositive(float value) => value > 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool FiniteNonnegative(float value) => value >= 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool Finite(float value) => !float.IsNaN(value) &&
            !float.IsInfinity(value);

        private static bool Finite(Vec2 value) => Finite(value.X) &&
            Finite(value.Y);

        private static bool Inside(double value, double minimum,
            double maximum) => !double.IsNaN(value) &&
            !double.IsInfinity(value) && value >= minimum - 1e-9 &&
            value <= maximum + 1e-9;

        private static double Clamp(double value, double minimum,
            double maximum) => value < minimum ? minimum :
            value > maximum ? maximum : value;

        private static bool AssignRoot(double value, out double root)
        {
            root = value;
            return true;
        }
    }
}
