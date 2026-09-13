using System;
using System.Collections.Generic;

namespace Chaite.Core
{
    public readonly struct WeaponProfileKey : IEquatable<WeaponProfileKey>
    {
        public readonly int WeaponId;
        public readonly int AmmoId;
        public WeaponProfileKey(int weaponId, int ammoId) { WeaponId = weaponId; AmmoId = ammoId; }
        public bool Equals(WeaponProfileKey other) => WeaponId == other.WeaponId && AmmoId == other.AmmoId;
        public override bool Equals(object value) => value is WeaponProfileKey && Equals((WeaponProfileKey)value);
        public override int GetHashCode() => unchecked(WeaponId * 397 ^ AmmoId);
    }

    public enum WeaponProfileStatus
    {
        UnsupportedCombination,
        Supported,
        MissingAmmo,
        ProjectileMismatch,
        InvalidBallistics,
        InvalidProjectileFeatures,
        InvalidDamage,
        InvalidTiming,
        InvalidResource
    }

    public enum WeaponFireMode { Automatic, NativeAnimationBurst }
    public enum WeaponBallisticKind
    {
        StraightPrimaryProjectile,
        ExponentialDragPrimaryProjectile,
        DiscreteVerticalAccelerationPrimaryProjectile,
        RocketAcceleration,
        // The reviewed projectile is straight only for a bounded native
        // prefix.  WeaponProfile.NativeDamagingUpdateLimit is the hard
        // boundary; callers must never extend this route into the later AI
        // phase.
        ConservativeStraightPrefix,
        // Terra Blade's type-985 wave applies native drag before each movement
        // and has a finite damage-bearing prefix. It is never a straight-speed
        // melee fallback.
        TerraBladeWave,
        // Eventide's five type-932 arrows use fan-offset spawn points and
        // converge on the cursor with a first-update Magic Quiver boundary.
        EventideFiveShotConvergence,
        // Razorblade Typhoon's type-409 projectile acquires a visible NPC
        // inside a finite radius, turns a fixed fraction toward it on every
        // extra update, and accelerates by a fixed amount.  It must not be
        // downgraded to a straight magic intercept.
        RazorbladeTyphoonHoming,
        // Demon Scythe's type-45 aiStyle 18 is a source-locked delayed
        // exponential acceleration route. It has its own per-update solver;
        // treating its 0.2 launch speed as an ordinary straight projectile
        // would make both aim and range incorrect.
        DemonScytheAcceleration,
        // Unholy Trident's type-114 aiStyle 27 preserves launch velocity for
        // 19 subupdates, then applies deterministic 0.98 decay before every
        // later movement. It has three projectile updates per game tick and
        // an early native speed-threshold kill, so it must never use a
        // constant-speed straight intercept.
        UnholyTridentDecay,
        // Explicit non-linear melee projectile families. These are dispatched
        // to MeleeProjectileCatalog and must never fall through to the
        // free-flight intercept solver.
        BoomerangReturn,
        SpearOwnerAnchored,
        YoyoCursorAnchored
    }
    public enum WeaponSecondaryEffect
    {
        None,
        CrystalShardsNotCredited,
        WallBounceNotCredited,
        DebuffNotCredited,
        ExplosionNotCredited,
        SilverBonusNotCredited,
        PenetrationNotCredited,
        AdditionalPelletsNotCredited,
        AdditionalArrowsNotCredited,
        FallingStarsNotCredited,
        BeeSpawnsNotCredited,
        HomingOrBounceNotCredited,
        SpecialProjectileEffectsNotCredited
    }
    internal enum WeaponSpreadModel
    {
        None,
        SymmetricComponent,
        RadialMagnitude,
        ClockworkBurst,
        Gatligator,
        AngularCone
    }

    /// <summary>Source review and prior engine observation are deliberately separate evidence levels.</summary>
    public sealed class WeaponProfile
    {
        public WeaponProfileKey Key { get; }
        public string Name { get; }
        public int ProjectileId { get; }
        public WeaponFireMode FireMode { get; }
        public WeaponBallisticKind Ballistics { get; }
        public WeaponSecondaryEffect SecondaryEffect { get; }
        public string OutputRouteId { get; }
        public OutputRouteKind OutputKind { get; }
        public OutputResourceKind ResourceKind { get; }
        public bool NativeSourceReviewed => true;
        // Prior headless combat exercised this pair; NOT an assertion that the
        // new solver, every burst phase, prefix, liquid path or client was tested.
        public bool PairPreviouslyObservedInNativeEngine { get; }
        public bool ThisSolverBehaviorVerifiedInNativeEngine => false;
        public float DefaultWeaponShootSpeed { get; }
        public float DefaultAmmoShootSpeed { get; }
        public int DefaultWeaponDamage { get; }
        public int DefaultAmmoDamage { get; }
        public int DefaultUseTime { get; }
        public int DefaultUseAnimation { get; }
        public int DefaultReuseDelay { get; }
        public bool DefaultAutoReuse { get; }
        public int DefaultManaCost { get; }
        public int DefaultExtraUpdates { get; }
        public int DefaultLifetimeSubupdates { get; }
        // Generic delayed vertical acceleration parameters. A delay of 14
        // means updates 1..14 are unaccelerated and update 15 first changes
        // velocity before movement. Zero acceleration is a reviewed discrete
        // trajectory too (for example Jester's Arrow).
        public int VerticalAccelerationDelaySubupdates { get; }
        public float VerticalAccelerationPerSubupdate { get; }
        public bool UsesArrowModifiers { get; }
        public bool MagicQuiverCanAddProjectileUpdate { get; }
        internal int RawAmmoProjectileId { get; }
        internal WeaponSpreadModel SpreadModel { get; }
        internal float ComponentSpread { get; }
        // Non-linear paths are hash-locked to the reviewed native AI.  These
        // values are deliberately not inferred from a generic projectile ID.
        internal float VelocityRetentionPerUpdate { get; }
        internal int NativeDamagingUpdateLimit { get; }
        internal float ProjectileSafetyRadiusPixels { get; }

        internal WeaponProfile(int weaponId, int ammoId, string name,
            string outputRouteId, int rawAmmoProjectileId,
            int projectileId, WeaponFireMode fireMode,
            WeaponSpreadModel spreadModel, float componentSpread,
            WeaponBallisticKind ballistics,
            float velocityRetentionPerUpdate,
            int nativeDamagingUpdateLimit,
            float projectileSafetyRadiusPixels,
            OutputRouteKind outputKind, OutputResourceKind resourceKind,
            bool previouslyObserved, float defaultWeaponShootSpeed,
            float defaultAmmoShootSpeed, int defaultWeaponDamage,
            int defaultAmmoDamage, int defaultUseTime,
            int defaultUseAnimation, int defaultReuseDelay,
            bool defaultAutoReuse, int defaultManaCost,
            int defaultExtraUpdates,
            int defaultLifetimeSubupdates,
            int verticalAccelerationDelaySubupdates = 0,
            float verticalAccelerationPerSubupdate = 0f,
            bool usesArrowModifiers = false,
            bool magicQuiverCanAddProjectileUpdate = false)
        {
            Key = new WeaponProfileKey(weaponId, ammoId);
            Name = name;
            OutputRouteId = outputRouteId;
            RawAmmoProjectileId = rawAmmoProjectileId;
            ProjectileId = projectileId;
            FireMode = fireMode;
            SpreadModel = spreadModel;
            ComponentSpread = componentSpread;
            Ballistics = ballistics;
            VelocityRetentionPerUpdate = velocityRetentionPerUpdate;
            NativeDamagingUpdateLimit = nativeDamagingUpdateLimit;
            ProjectileSafetyRadiusPixels = projectileSafetyRadiusPixels;
            OutputKind = outputKind;
            ResourceKind = resourceKind;
            SecondaryEffect = SecondaryFor(weaponId, ammoId, projectileId);
            PairPreviouslyObservedInNativeEngine = previouslyObserved;
            DefaultWeaponShootSpeed = defaultWeaponShootSpeed;
            DefaultAmmoShootSpeed = defaultAmmoShootSpeed;
            DefaultWeaponDamage = defaultWeaponDamage;
            DefaultAmmoDamage = defaultAmmoDamage;
            DefaultUseTime = defaultUseTime;
            DefaultUseAnimation = defaultUseAnimation;
            DefaultReuseDelay = defaultReuseDelay;
            DefaultAutoReuse = defaultAutoReuse;
            DefaultManaCost = defaultManaCost;
            DefaultExtraUpdates = defaultExtraUpdates;
            DefaultLifetimeSubupdates = defaultLifetimeSubupdates;
            VerticalAccelerationDelaySubupdates =
                verticalAccelerationDelaySubupdates;
            VerticalAccelerationPerSubupdate =
                verticalAccelerationPerSubupdate;
            UsesArrowModifiers = usesArrowModifiers;
            MagicQuiverCanAddProjectileUpdate =
                magicQuiverCanAddProjectileUpdate;
        }

        private static WeaponSecondaryEffect SecondaryFor(int weaponId,
            int ammoId, int projectileId)
        {
            // Super Star Cannon spawns a separate Super Star Slash (type 729)
            // on a successful hit.  The primary type-728 trajectory remains
            // deterministic, but the follow-up is deliberately excluded from
            // the conservative direct-DPS contract.
            if (weaponId == 4060 || projectileId == 728)
                return WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited;
            if (ammoId == 3009)
                return WeaponSecondaryEffect.HomingOrBounceNotCredited;
            if (ammoId == 3010)
                return WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited;
            if (ammoId == 1310 || ammoId == 3011)
                return WeaponSecondaryEffect.DebuffNotCredited;
            if (weaponId == 1229 || weaponId == 2624)
                return WeaponSecondaryEffect.AdditionalArrowsNotCredited;
            if (weaponId == 2888)
                return WeaponSecondaryEffect.BeeSpawnsNotCredited;
            if (weaponId == 3019 || ammoId == 5348)
                return WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited;
            if (weaponId == 5117)
                return WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited;
            // Several reviewed melee projectiles have deterministic primary
            // paths but apply a secondary debuff or spawn a separate projectile
            // in native AI/OnHit code. Keep those effects visible in the
            // profile metadata without allowing them into guaranteed DPS.
            if (projectileId == 19 || projectileId == 33 ||
                projectileId == 113 || projectileId == 1103)
                return WeaponSecondaryEffect.DebuffNotCredited;
            if (projectileId == 130 || projectileId == 730)
                return WeaponSecondaryEffect.SpecialProjectileEffectsNotCredited;
            if (ammoId == 516)
                return WeaponSecondaryEffect.FallingStarsNotCredited;
            if (ammoId == 1235)
                return WeaponSecondaryEffect.HomingOrBounceNotCredited;
            if (ammoId == 265)
                return WeaponSecondaryEffect.ExplosionNotCredited;
            if (ammoId == 545 || ammoId == 988 || ammoId == 1334 ||
                ammoId == 1341 || projectileId == 280 ||
                projectileId == 495)
                return WeaponSecondaryEffect.DebuffNotCredited;
            if (projectileId == 295)
                return WeaponSecondaryEffect.ExplosionNotCredited;
            if (ammoId == 47 || ammoId == 51 || ammoId == 3568)
                return WeaponSecondaryEffect.PenetrationNotCredited;
            // These weapons always create at least one copy of the reviewed
            // primary path. Their additional random pellets are useful in the
            // game, but are deliberately excluded from admission DPS.
            if (weaponId == 534 || weaponId == 679 || weaponId == 964 ||
                weaponId == 1308 || weaponId == 2188 ||
                weaponId == 3788 || weaponId == 4703)
                return WeaponSecondaryEffect.AdditionalPelletsNotCredited;
            if (ammoId == 515 || projectileId == 521)
                return WeaponSecondaryEffect.CrystalShardsNotCredited;
            if (ammoId == 278)
                return WeaponSecondaryEffect.SilverBonusNotCredited;
            if (ammoId == 546 || ammoId == 1335 || ammoId == 1342 ||
                ammoId == 1350 || ammoId == 1352)
                return WeaponSecondaryEffect.DebuffNotCredited;
            if (ammoId == 1351)
                return WeaponSecondaryEffect.ExplosionNotCredited;
            if (projectileId == 27 || projectileId == 36)
                return WeaponSecondaryEffect.WallBounceNotCredited;
            // Razorblade Typhoon's type-409 projectile bounces by negating
            // collided velocity components. The dedicated homing route only
            // credits its unobstructed acquisition/turn path; ricochet damage
            // is intentionally excluded from its conservative DPS estimate.
            if (projectileId == 409)
                return WeaponSecondaryEffect.WallBounceNotCredited;
            if (projectileId == 20 || projectileId == 22 ||
                projectileId == 88 ||
                projectileId == 114 || projectileId == 242 ||
                projectileId == 359 || projectileId == 45 ||
                projectileId == 638)
                return WeaponSecondaryEffect.PenetrationNotCredited;
            return WeaponSecondaryEffect.None;
        }
    }

    /// <summary>Live, read-only adapter values, never obtained by invoking PickAmmo or simulating a shot.</summary>
    public struct WeaponProfileInput
    {
        public int WeaponId, AmmoId, ProjectileId, ProjectileExtraUpdates, ProjectileLifetimeSubupdates;
        public float WeaponShootSpeed, AmmoShootSpeed;
        // GetWeaponDamage(weapon), then ammo.damage * GetWeaponDamageMultiplier(ammo).
        // Both native helpers only read state. Do not substitute weapon.damage alone.
        public int WeaponDamageAfterModifiers, AmmoBaseDamage;
        public float AmmoDamageMultiplier;
        public int UseTime, UseAnimation, ReuseDelay;
        // Value at the native ItemCheck_Shoot entry, NOT an arbitrary pre-update
        // sample. Zero explicitly means a NEW animation will reach the shooting
        // branch at UseAnimation - 1 after native ItemCheck's prior decrement.
        public int AnimationRemainingAtShot;
        public bool AutoReuse;
        public bool HasAmmo;
        // Magic routes provide the already-discounted native cost.  This is
        // zero for a reviewed free-use armor interaction, not an unknown value.
        public bool ManaCostKnown;
        public int ManaCostPerUse;
        // A strict route can additionally lock the raw Item.SetDefaults mana
        // and the read-only Player.manaCost multiplier which produced the
        // effective cost.  Generic magic profiles intentionally do not rely
        // on these fields, so absent values remain backward compatible.
        public bool ManaBaseCostKnown;
        public int ManaBaseCost;
        public bool ManaCostMultiplierKnown;
        public float ManaCostMultiplier;
        // Arrow modifiers are live Player fields, not prefix guesses. Bows
        // fail closed unless the adapter supplied all four values together.
        public bool ArrowStateKnown;
        public bool Archery;
        public bool MagicQuiver;
        public bool HasMoltenQuiver;
        public bool SharpBarb;
        // 1.4.5 Harpy Charm (including its Seraph/Phoenix upgrades) can rotate
        // an in-flight arrow toward a newly selected target. The ballistic
        // solver deliberately fails closed while that external FSM is active.
        public bool HarpyCharm;
        // Diamond Staff (744) shares the Gem Staff projectile family. Vanilla
        // reads the effective body slot (GetEffectiveArmor(1)) and can inject
        // homing/bounce/acceleration/swirl/spread features from gem robes.
        // The straight 744 route is admitted only when that read is known and
        // no dynamic gem-robe feature is active. Other weapon families ignore
        // these fields.
        public bool GemStaffFeatureStateKnown;
        public int GemStaffEffectiveArmorType;
    }

    public struct WeaponProfileEvaluation
    {
        public WeaponProfileStatus Status;
        public WeaponProfile Profile;
        public float SpeedPixelsPerTick;
        public float MaxFlightTicks;
        // Dry unobstructed primary-path length using the worst component spread.
        // This is NOT reliable hit range and does not include walls/liquid slowdown.
        public float ConservativeRangePixels;
        public int DirectDamage;
        // Direct pre-defense/crit damage only; no crystal-shard or piercing bonus.
        public float ApproximateDirectDps;
        // Maximum Euclidean initial-velocity error due to native random spread.
        // Straight paths scale this to one full tick; exponential-drag paths
        // retain the pre-first-AI value so their solver can integrate it.
        public float SpreadSpeedPixelsPerTick;
        public int BurstShotIndex;
        public int ManaCostPerUse;
        // Native projectile velocity is measured per projectile update. Most
        // callers want per-tick speed, but discrete acceleration needs the
        // unscaled value and the exact first/sustained update schedule.
        public float InitialSpeedPixelsPerSubupdate;
        public float SpreadSpeedPixelsPerSubupdate;
        public int FirstTickProjectileUpdates;
        public int SustainedProjectileUpdatesPerTick;
        public bool IsSupported => Status == WeaponProfileStatus.Supported && Profile != null;
        public string Reason => WeaponProfileCatalog.Describe(Status);
    }

    /// <summary>
    /// Explicit Terraria 1.4.5.8 pairs, not a generic "shoot > 0" fallback.
    /// Assembly SHA256 960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3.
    /// Read-only Cecil evidence: Item.SetDefaults1 at 21f3/2250/979e/b680;
    /// Player.ItemCheck_Shoot 1290..130b (98) and 436f..44ac (434);
    /// PickAmmo 01ce..01d8 (speed), 022f..0244 (ammo damage);
    /// Projectile.SetDefaults 0755..07bb / 1c6e..1cd4 (14 / 89).
    /// AI_001 exempts 14 and 89 from gravity timer at 68ef / 6942..6948.
    /// Crystal Kill 192cf..193fd creates TWO random half-damage type-90 shards;
    /// those secondary hits are deliberately not counted as guaranteed DPS.
    /// Additional 1.4.5.8 source evidence: Item.SetDefaults cases 518/1870/
    /// 2270; Player.ItemCheck_Shoot normalization at 47697..47715,
    /// Gatligator's second spread/scaling branch at 48046..48056, and Crystal
    /// Storm's +/-1.6 branch at 48258..48265. Projectile.Update calls AI before
    /// UpdatePosition (16913/20011), while projectile 94 AI multiplies velocity
    /// by .985 and fades after ai[1]&gt;130 (26335..26357).
    /// Further source-locked paths: Onyx Blaster Item case 3788 and its four
    /// bounded ammo vectors plus type-661 secondary at Player 49336..49346;
    /// bullet Item cases 234/546/1349..1352/3567 and projectile defaults
    /// 36/104/284..287/638, all excluded from AI_001's gravity counter;
    /// Frost Staff case 726/type-359 AI_028; Poison/Venom cases 1308/2188,
    /// whose index-zero pellet has exactly zero perturbation; and Crystal
    /// Serpent case 3209/type-521 AI_029, whose primary velocity is unchanged.
    /// Star Cannon case 197 retains type-955 when Fallen Star's raw shoot is
    /// zero; Super Star Cannon case 4060 retains type-728 and creates a
    /// separate type-729 Super Star Slash on hit, which is not credited.
    /// Pew-matic Horn case 5117 replaces the picked bullet with type-968 and
    /// adds independent +/-1.125 velocity components; type 968 is AI_002 with
    /// no velocity mutation (only wind/visual frame state), so its override
    /// retains the live bullet damage/speed while locking output identity.
    /// </summary>
    public static class WeaponProfileCatalog
    {
        private struct WeaponDefinition
        {
            public int Id, Damage, UseTime, UseAnimation, ReuseDelay;
            public float ShootSpeed, Spread;
            public bool AutoReuse, ConvertsMusket;
            // Some ranged items pick ammo for damage/speed but replace the
            // selected projectile in ItemCheck_Shoot. Keep that override
            // explicit so the ordinary ammo projectile can never leak into
            // the output route (Pew-matic Horn is the reviewed case).
            public int OutputProjectileId, OutputExtraUpdates,
                OutputLifetimeSubupdates;
            public string Slug, Name;
            public WeaponSpreadModel SpreadModel;
            public WeaponFireMode FireMode;
        }

        private struct AmmoDefinition
        {
            public int Id, ProjectileId, Damage, ExtraUpdates,
                LifetimeSubupdates;
            public float ShootSpeed;
            public string Slug, Name;
        }

        private struct BowDefinition
        {
            public int Id, Damage, UseTime, UseAnimation;
            public float ShootSpeed, AngularSpreadRadians;
            public bool AutoReuse, WoodenArrowOnly;
            public string Slug, Name;
        }

        private struct ArrowDefinition
        {
            public int Id, ProjectileId, Damage, ExtraUpdates,
                LifetimeSubupdates;
            public float ShootSpeed;
            public string Slug, Name;
        }

        private struct DartWeaponDefinition
        {
            public int Id, Damage, UseTime, UseAnimation;
            public float ShootSpeed;
            public bool AutoReuse;
            public string Slug, Name;
        }

        private struct DartDefinition
        {
            public int Id, ProjectileId, Damage, ExtraUpdates,
                LifetimeSubupdates, VerticalAccelerationDelaySubupdates;
            public float ShootSpeed, VerticalAccelerationPerSubupdate;
            public string Slug, Name;
        }

        private struct StarCannonDefinition
        {
            public int Id, ProjectileId, Damage, UseTime, UseAnimation;
            public float ShootSpeed;
            public bool AutoReuse;
            public string Slug, Name;
        }

        private struct MagicDefinition
        {
            public int Id, ProjectileId, Damage, UseTime, UseAnimation,
                ReuseDelay, Mana, ExtraUpdates, LifetimeSubupdates,
                NativeDamagingUpdateLimit,
                VerticalAccelerationDelaySubupdates;
            public float ShootSpeed, Spread, VelocityRetentionPerUpdate,
                ProjectileSafetyRadiusPixels,
                VerticalAccelerationPerSubupdate;
            public bool AutoReuse;
            public string Slug, Name;
            public WeaponSpreadModel SpreadModel;
            public WeaponBallisticKind Ballistics;
        }

        // These are explicit hash-locked Item.SetDefaults/ItemCheck_Shoot
        // families, not every item which happens to have useAmmo=Bullet.
        private static readonly WeaponDefinition[] Weapons =
        {
            Weapon(95, "flintlock-pistol", "Flintlock Pistol", 13, 16,
                16, 0, 6f, false, false, WeaponSpreadModel.None, 0f),
            Weapon(96, "musket", "Musket", 31, 32, 32, 0, 9f, false,
                false, WeaponSpreadModel.None, 0f),
            Weapon(98, "minishark", "Minishark", 6, 8, 8, 0, 7f, true,
                false, WeaponSpreadModel.SymmetricComponent, .4f),
            Weapon(164, "handgun", "Handgun", 26, 15, 15, 0, 10f, false,
                false, WeaponSpreadModel.None, 0f),
            Weapon(219, "phoenix-blaster", "Phoenix Blaster", 30, 14, 14,
                0, 13f, false, false, WeaponSpreadModel.None, 0f),
            Weapon(434, "clockwork-assault-rifle", "Clockwork Assault Rifle",
                17, 4, 12, 14, 7.75f, true, false,
                WeaponSpreadModel.ClockworkBurst, 0f,
                WeaponFireMode.NativeAnimationBurst),
            Weapon(533, "megashark", "Megashark", 25, 7, 7, 0, 10f, true,
                false, WeaponSpreadModel.SymmetricComponent, .4f),
            // Conservative profiles credit one guaranteed pellet only. The
            // exact native component bounds still drive aiming/range safety.
            Weapon(534, "shotgun", "Shotgun", 24, 45, 45, 0, 7f, false,
                false, WeaponSpreadModel.SymmetricComponent, 2f),
            Weapon(679, "tactical-shotgun", "Tactical Shotgun", 36, 34,
                34, 0, 6f, true, false,
                WeaponSpreadModel.SymmetricComponent, 2f),
            Weapon(800, "undertaker", "The Undertaker", 19, 20, 20, 0, 6f,
                false, false, WeaponSpreadModel.None, 0f),
            Weapon(964, "boomstick", "Boomstick", 14, 40, 40, 0, 5.35f,
                false, false, WeaponSpreadModel.SymmetricComponent, 1.4f),
            Weapon(1254, "sniper-rifle", "Sniper Rifle", 200, 36, 36, 0,
                16f, false, true, WeaponSpreadModel.None, 0f),
            Weapon(1255, "venus-magnum", "Venus Magnum", 50, 9, 9, 0,
                13.5f, true, true, WeaponSpreadModel.None, 0f),
            Weapon(1265, "uzi", "Uzi", 30, 9, 9, 0, 13f, true, true,
                WeaponSpreadModel.SymmetricComponent, .9f),
            Weapon(1553, "sdmg", "S.D.M.G.", 85, 5, 5, 0, 12f, true,
                false, WeaponSpreadModel.SymmetricComponent, .2f),
            // Chain Gun receives both the common pre-normalization ±1.5px/tick
            // component perturbation and its own ±1.2 perturbation.
            Weapon(1929, "chain-gun", "Chain Gun", 38, 4, 4, 0, 14f,
                true, false, WeaponSpreadModel.SymmetricComponent, 2.7f),
            // Red Ryder has no ItemCheck_Shoot special case in 1.4.5.8: the
            // selected ammo projectile and normalized velocity are used as-is.
            Weapon(1870, "red-ryder", "Red Ryder", 20, 38, 38, 0, 8f,
                true, false, WeaponSpreadModel.None, 0f),
            Weapon(2269, "revolver", "Revolver", 20, 22, 22, 0, 16f,
                false, false, WeaponSpreadModel.None, 0f),
            // Gatligator first perturbs each component by +/-1.5 before
            // normalization, then by +/-2.0 after it. One third of shots also
            // scale X and Y independently by a factor in [0.4, 1.6].
            Weapon(2270, "gatligator", "Gatligator", 21, 7, 7, 0, 8f,
                true, false, WeaponSpreadModel.Gatligator, 0f),
            // Each of Onyx Blaster's four ammo projectiles adds a vector with
            // magnitude at most 2 to the normalized primary velocity.  Its
            // separate double-damage type-661 projectile is useful in game,
            // but is deliberately excluded from admission damage/range.
            Weapon(3788, "onyx-blaster", "Onyx Blaster", 24, 48, 48, 0,
                7f, false, false, WeaponSpreadModel.RadialMagnitude, 2f),
            Weapon(4703, "quad-barrel-shotgun", "Quad-Barrel Shotgun", 14,
                55, 55, 0, 7f, false, false, WeaponSpreadModel.None, 0f),
            // PickAmmo supplies the bullet's damage/speed, then
            // ItemCheck_Shoot replaces its projectile with type 968. The
            // native branch adds independent [-1.125,+1.125] components;
            // Projectile AI_002 leaves velocity unchanged for this type.
            Weapon(5117, "pew-matic-horn", "Pew-matic Horn", 20, 15, 15,
                0, 14f, true, false, WeaponSpreadModel.SymmetricComponent,
                1.125f, WeaponFireMode.Automatic, 968, 0, 3600)
        };

        // Snowball Cannon uses AmmoID.Snowball rather than AmmoID.Bullet.
        // Keep it in a separate matrix so a future snowball-compatible item
        // cannot accidentally inherit the ordinary bullet projectile paths.
        private static readonly WeaponDefinition[] SnowballWeapons =
        {
            Weapon(1319, "snowball-cannon", "Snowball Cannon", 10, 19, 19,
                0, 11f, true, false, WeaponSpreadModel.SymmetricComponent,
                SnowballCannonCatalog.ComponentSpread)
        };

        // Projectile 14 aliases are intentionally separate keys because an
        // ammo stack change is a resource-route change even when ballistics
        // happen to match. Crystal secondary shards remain uncredited.
        private static readonly AmmoDefinition[] Ammunition =
        {
            Ammo(97, "musket-ball", "Musket Ball", 14, 7, 4f, 1, 600),
            Ammo(234, "meteor-shot", "Meteor Shot", 36, 8, 3f, 1, 600),
            Ammo(515, "crystal-bullet", "Crystal Bullet", 89, 9, 5f, 1, 600),
            Ammo(546, "cursed-bullet", "Cursed Bullet", 104, 15, 5f, 2,
                600),
            Ammo(3104, "endless-musket-pouch", "Endless Musket Pouch", 14,
                7, 4f, 1, 600),
            Ammo(4915, "tungsten-bullet", "Tungsten Bullet", 14, 9, 4.5f,
                1, 600),
            Ammo(278, "silver-bullet", "Silver Bullet", 981, 9, 4.5f,
                1, 600),
            Ammo(1302, "high-velocity-bullet", "High Velocity Bullet", 242,
                11, 4f, 7, 600),
            Ammo(1335, "ichor-bullet", "Ichor Bullet", 279, 13, 5.25f,
                2, 600),
            Ammo(1342, "venom-bullet", "Venom Bullet", 283, 15, 5.3f,
                2, 600),
            Ammo(1349, "party-bullet", "Party Bullet", 284, 10, 5.1f, 2,
                600),
            Ammo(1350, "nano-bullet", "Nano Bullet", 285, 10, 4.6f, 2,
                600),
            Ammo(1351, "exploding-bullet", "Exploding Bullet", 286, 10,
                4.7f, 2, 600),
            Ammo(1352, "golden-bullet", "Golden Bullet", 287, 10, 4.6f,
                2, 600),
            Ammo(3567, "luminite-bullet", "Luminite Bullet", 638, 20, 2f,
                5, 600)
        };

        private static readonly AmmoDefinition[] SnowballAmmunition =
        {
            // Projectile 166 has the default 3600-subupdate lifetime.  Its
            // AI_002 gravity/damping starts at update 20; production admits
            // only the 19-update straight prefix (see the catalog contract).
            Ammo(949, "snowball", "Snowball", 166, 8, 7f, 0, 3600)
        };

        // Common progression bows with an exact single-primary path.  The
        // Chlorophyte Shotbow and Tsunami create more arrows, but index zero /
        // every velocity respectively preserve at least one nominal primary;
        // conservative DPS credits only one. Hellwing's reviewed type-485
        // path is restricted to wooden-arrow identities because other arrows
        // receive random ai[] values which alter their acceleration age.
        private static readonly BowDefinition[] Bows =
        {
            // Every ordinary single-primary pre-Hardmode bow is explicit as
            // well.  The metal variants which delegate to item 99 retain the
            // final per-item timing/damage overrides from SetDefaults4; Ash
            // Wood's world variant is safe because live timing and damage are
            // still read from the actual item instance.
            Bow(39, "wooden-bow", "Wooden Bow", 4, 30, 30, 6.1f,
                false),
            Bow(655, "ebonwood-bow", "Ebonwood Bow", 8, 28, 28, 6.6f,
                false),
            Bow(923, "shadewood-bow", "Shadewood Bow", 8, 28, 28,
                6.6f, false),
            Bow(658, "rich-mahogany-bow", "Rich Mahogany Bow", 6, 29,
                29, 6.6f, false),
            Bow(2515, "palm-wood-bow", "Palm Wood Bow", 6, 29, 29,
                6.6f, false),
            Bow(2747, "boreal-wood-bow", "Boreal Wood Bow", 6, 29, 29,
                6.6f, false),
            Bow(5282, "ash-wood-bow", "Ash Wood Bow", 10, 25, 25,
                6.6f, false),
            Bow(661, "pearlwood-bow", "Pearlwood Bow", 12, 20, 20, 7f,
                false),
            Bow(3504, "copper-bow", "Copper Bow", 6, 29, 29, 6.6f,
                false),
            Bow(3498, "tin-bow", "Tin Bow", 7, 28, 28, 6.6f, false),
            Bow(99, "iron-bow", "Iron Bow", 8, 28, 28, 6.6f, false),
            Bow(3492, "lead-bow", "Lead Bow", 9, 27, 27, 6.6f, false),
            Bow(3510, "silver-bow", "Silver Bow", 9, 27, 27, 6.6f,
                false),
            Bow(3486, "tungsten-bow", "Tungsten Bow", 10, 26, 26,
                6.6f, false),
            Bow(3516, "gold-bow", "Gold Bow", 11, 26, 26, 6.6f, false),
            Bow(3480, "platinum-bow", "Platinum Bow", 13, 25, 25,
                6.6f, false),
            Bow(4058, "skeleton-bow", "Skeleton Bow", 8, 17, 17, 11f,
                false),
            Bow(44, "demon-bow", "Demon Bow", 14, 25, 25, 6.7f, false),
            Bow(796, "tendon-bow", "Tendon Bow", 19, 30, 30, 6.7f,
                false),
            Bow(120, "molten-fury", "Molten Fury", 31, 22, 22, 8f,
                false),
            Bow(2888, "bees-knees", "The Bee's Knees", 23, 23, 23, 8f,
                false),
            Bow(3019, "hellwing-bow", "Hellwing Bow", 22, 13, 13, 6f,
                true, true, .39269908169872414f),
            Bow(435, "cobalt-repeater", "Cobalt Repeater", 35, 23, 23,
                9f, true),
            Bow(1187, "palladium-repeater", "Palladium Repeater", 37,
                22, 22, 9.25f, true),
            Bow(436, "mythril-repeater", "Mythril Repeater", 39, 20, 20,
                9.5f, true),
            Bow(1194, "orichalcum-repeater", "Orichalcum Repeater", 40,
                19, 19, 9.75f, true),
            Bow(481, "adamantite-repeater", "Adamantite Repeater", 42,
                18, 18, 10f, true),
            Bow(1201, "titanium-repeater", "Titanium Repeater", 43, 17,
                17, 10.5f, true),
            Bow(578, "hallowed-repeater", "Hallowed Repeater", 53, 16,
                16, 11f, true),
            Bow(682, "marrow", "Marrow", 40, 19, 19, 11f, true),
            Bow(725, "ice-bow", "Ice Bow", 39, 14, 14, 10f, true),
            Bow(2223, "pulse-bow", "Pulse Bow", 80, 20, 20, 7.75f,
                true),
            Bow(3052, "shadowflame-bow", "Shadowflame Bow", 47, 20, 20,
                11f, true),
            Bow(1229, "chlorophyte-shotbow", "Chlorophyte Shotbow", 34,
                19, 19, 11.5f, true),
            Bow(2624, "tsunami", "Tsunami", 53, 24, 24, 10f, true)
        };

        private static readonly ArrowDefinition[] Arrows =
        {
            Arrow(40, "wooden-arrow", "Wooden Arrow", 1, 5, 3f, 0,
                1200),
            Arrow(41, "flaming-arrow", "Flaming Arrow", 2, 7, 3.5f, 0,
                1200),
            Arrow(47, "unholy-arrow", "Unholy Arrow", 4, 12, 3.4f, 0,
                1200),
            Arrow(51, "jesters-arrow", "Jester's Arrow", 5, 10, .5f, 1,
                120),
            Arrow(265, "hellfire-arrow", "Hellfire Arrow", 41, 13, 6.5f,
                0, 3600),
            Arrow(516, "holy-arrow", "Holy Arrow", 91, 13, 3.5f, 0,
                1200),
            Arrow(545, "cursed-arrow", "Cursed Arrow", 103, 17, 4f, 0,
                1200),
            Arrow(988, "frostburn-arrow", "Frostburn Arrow", 172, 9,
                3.75f, 0, 1200),
            Arrow(1235, "chlorophyte-arrow", "Chlorophyte Arrow", 225,
                16, 4.5f, 1, 1200),
            Arrow(1334, "ichor-arrow", "Ichor Arrow", 278, 16, 4.25f, 1,
                1200),
            Arrow(1341, "venom-arrow", "Venom Arrow", 282, 19, 4.3f, 1,
                1200),
            Arrow(3003, "bone-arrow", "Bone Arrow", 474, 8, 3.5f, 0,
                1200),
            Arrow(3103, "endless-quiver", "Endless Quiver", 1, 5, 3f, 0,
                1200),
            Arrow(3568, "luminite-arrow", "Luminite Arrow", 639, 15, 3f,
                1, 90),
            Arrow(5348, "shimmer-arrow", "Shimmer Arrow", 1006, 12, 3f,
                0, 1200)
        };

        // Darts use their own ammo family. Keep this exact 4 x 5 matrix
        // separate from bullets and arrows so an unrelated ranged weapon can
        // never inherit a merely similar projectile path.
        private static readonly DartWeaponDefinition[] DartWeapons =
        {
            DartWeapon(281, "blowpipe", "Blowpipe", 9, 25, 25, 11f,
                true),
            DartWeapon(986, "blowgun", "Blowgun", 27, 35, 35, 13f,
                true),
            DartWeapon(3007, "dart-pistol", "Dart Pistol", 28, 22, 22,
                13f, true),
            DartWeapon(3008, "dart-rifle", "Dart Rifle", 52, 38, 38,
                14.5f, true)
        };

        private static readonly DartDefinition[] Darts =
        {
            Dart(283, "seed", "Seed", 51, 4, 0f, 0, 600, 14, .1f),
            Dart(1310, "poison-dart", "Poison Dart", 267, 10, 2f, 0,
                600, 19, .075f),
            Dart(3009, "crystal-dart", "Crystal Dart", 477, 14, 1f, 1,
                600, 0, 0f),
            Dart(3010, "cursed-dart", "Cursed Dart", 478, 9, 3f, 0,
                300, 19, .075f),
            Dart(3011, "ichor-dart", "Ichor Dart", 479, 10, 3f, 0,
                600, 19, .075f)
        };

        // Fallen Star has shoot=0. Native PickAmmo therefore retains the
        // weapon's base projectile instead of replacing it with an ammo
        // projectile. These two identities remain explicit and separate.
        private static readonly StarCannonDefinition[] StarCannons =
        {
            StarCannon(197, "star-cannon", "Star Cannon", 955, 55, 12, 12,
                14f, true),
            StarCannon(4060, "super-star-cannon", "Super Star Cannon", 728,
                60, 18, 18, 20f, true)
        };

        // These are simple single-primary magic shots whose initial native
        // trajectory is explicitly reviewed. Bounce and penetration benefits
        // are not credited to conservative direct DPS.
        private static readonly MagicDefinition[] MagicWeapons =
        {
            // Demon Scythe's type-45 aiStyle 18 has a deterministic delayed
            // acceleration path. Its first 29 updates retain the 0.2 launch
            // velocity, updates 30..99 multiply by 1.06 before movement, and
            // later updates retain the resulting velocity. The separate
            // DemonScytheCatalog validates all of these source-locked facts.
            Magic(DemonScytheCatalog.WeaponId, "demon-scythe",
                "Demon Scythe", DemonScytheCatalog.ProjectileId,
                DemonScytheCatalog.DefaultDamage,
                DemonScytheCatalog.ShootSpeed,
                DemonScytheCatalog.UseTime,
                DemonScytheCatalog.UseAnimation,
                DemonScytheCatalog.ReuseDelay,
                DemonScytheCatalog.AutoReuse,
                DemonScytheCatalog.ManaCost,
                DemonScytheCatalog.ExtraUpdates,
                DemonScytheCatalog.LifetimeSubupdates,
                WeaponBallisticKind.DemonScytheAcceleration),
            // Unholy Trident's type-114 aiStyle 27 is not an ordinary
            // straight projectile: its first 19 subupdates retain launch
            // speed, then native AI applies 0.98 before each movement.  The
            // separate catalog also stops at its source-locked early speed
            // threshold kill rather than silently using timeLeft as range.
            Magic(UnholyTridentCatalog.WeaponId, "unholy-trident",
                "Unholy Trident", UnholyTridentCatalog.ProjectileId,
                UnholyTridentCatalog.DefaultDamage,
                UnholyTridentCatalog.ShootSpeed,
                UnholyTridentCatalog.UseTime,
                UnholyTridentCatalog.UseAnimation,
                UnholyTridentCatalog.ReuseDelay,
                UnholyTridentCatalog.AutoReuse,
                UnholyTridentCatalog.DefaultManaCost,
                UnholyTridentCatalog.ExtraUpdates,
                UnholyTridentCatalog.LifetimeSubupdates,
                WeaponBallisticKind.UnholyTridentDecay,
                WeaponSpreadModel.None, 0f, 1f,
                UnholyTridentCatalog.LastDamagingSubupdate),
            // Flower of Fire (type 15) uses AI_008.  The native branch starts
            // applying vertical acceleration when ai[1] reaches 20.  The
            // first 19 projectile updates are therefore an exact straight
            // prefix; later bouncing/gravity behavior is intentionally outside
            // this route.
            Magic(112, "flower-of-fire", "Flower of Fire", 15, 48, 7.5f,
                16, 16, 0, false, 9, 0, 3600,
                WeaponBallisticKind.ConservativeStraightPrefix,
                WeaponSpreadModel.None, 0f, 1f,
                SimpleMagicPrefixCatalog.FlowerOfFirePrefixUpdates),
            // Aqua Scepter type 22 has an exact dry ballistic path: updates
            // 1..4 retain launch velocity and update 5 onward adds +0.15 Y
            // before movement.  It has ignoreWater=true, but its own AI
            // replaces velocity with seeded random lava-bounce values when a
            // downward projectile reaches lava.  AquaScepterCatalog pins the
            // legitimate stronger-world timing variant and the bounded 100
            // update path; NativeTrajectoryGateCatalog refuses a lava route
            // immediately before native input is emitted.
            Magic(AquaScepterCatalog.WeaponId, "aqua-scepter",
                "Aqua Scepter", AquaScepterCatalog.ProjectileId,
                AquaScepterCatalog.DefaultDamage,
                AquaScepterCatalog.ShootSpeed,
                AquaScepterCatalog.NormalUseTime,
                AquaScepterCatalog.NormalUseAnimation,
                AquaScepterCatalog.ReuseDelay,
                AquaScepterCatalog.AutoReuse,
                AquaScepterCatalog.ManaCost,
                AquaScepterCatalog.ExtraUpdates,
                AquaScepterCatalog.LifetimeSubupdates,
                WeaponBallisticKind.
                    DiscreteVerticalAccelerationPrimaryProjectile,
                WeaponSpreadModel.None, 0f, 1f,
                AquaScepterCatalog.LastReviewedSubupdate, 0f,
                AquaScepterCatalog.StraightSubupdates,
                AquaScepterCatalog.VerticalAccelerationPerSubupdate),
            Magic(127, "space-gun", "Space Gun", 20, 20, 10f, 17, 17,
                0, true, 6, 2, 600),
            Magic(165, "water-bolt", "Water Bolt", 27, 19, 4.5f, 17, 17,
                0, true, 10, 0, 1800),
            Magic(514, "laser-rifle", "Laser Rifle", 88, 29, 17f, 12, 12,
                0, true, 8, 4, 600),
            // Projectile 94 runs AI before movement. Each update therefore
            // travels with v0*0.985^n (n starts at one). ai[1] starts fading
            // after 130; Single rounding keeps it damaging through update 150
            // and Kill occurs at the start of update 151. ItemCheck_Shoot adds
            // an independent [-1.6,+1.6] perturbation to each velocity axis.
            Magic(518, "crystal-storm", "Crystal Storm", 94, 35, 16f, 7,
                7, 0, true, 5, 0, 600,
                WeaponBallisticKind.ExponentialDragPrimaryProjectile,
                WeaponSpreadModel.SymmetricComponent, 1.6f, .985f, 150,
                5.656854249492381f),
            // Cursed Flames also uses AI_008. Its ai[1] counter is incremented
            // before the >=20 test, so only native updates 1..19 retain the
            // launch vector. Do not hand its later +0.2 Y/update, 16px/update
            // fall-speed cap, or surface wind branch to the straight solver.
            Magic(519, "cursed-flames", "Cursed Flames", 95, 55, 10f,
                15, 15, 0, true, 9, 0, 3600,
                WeaponBallisticKind.ConservativeStraightPrefix,
                WeaponSpreadModel.None, 0f, 1f,
                SimpleMagicPrefixCatalog.CursedFlamesPrefixUpdates),
            // Flower of Frost (type 253) shares AI_008's first nineteen
            // unchanged velocity updates. The later gravity and the surface
            // wind branch are deliberately outside this direct-primary
            // certificate; NativeWindEmissionGate independently prevents a
            // non-vanilla enabled wind setting from entering this route.
            Magic(SimpleMagicPrefixCatalog.FlowerOfFrostWeaponId,
                "flower-of-frost", "Flower of Frost",
                SimpleMagicPrefixCatalog.FlowerOfFrostProjectileId, 60, 9f,
                12, 12, 0, false, 11, 0, 3600,
                WeaponBallisticKind.ConservativeStraightPrefix,
                WeaponSpreadModel.None, 0f, 1f,
                SimpleMagicPrefixCatalog.FlowerOfFrostPrefixUpdates),
            // Frost Staff's type-359 AI_028 branch is visual-only after
            // launch; its two-hit penetration is not credited here.
            Magic(726, "frost-staff", "Frost Staff", 359, 46, 16f, 12,
                12, 0, true, 12, 0, 3600),
            // Diamond Staff is the one Gem Staff which has a deterministic
            // straight primary when no Gem Robe feature is active. Its native
            // projectile starts at timeLeft=300 and FinalizeProjectile adds
            // one penetration because BiggerHitbox is always set for type 126.
            // The 30px collision inflation is retained as route metadata; the
            // armor-state gate in Evaluate prevents dynamic Gem Robe paths.
            Magic(744, "diamond-staff", "Diamond Staff", 126, 23, 9.5f,
                26, 26, 0, true, 9, 0, 300,
                WeaponBallisticKind.StraightPrimaryProjectile,
                WeaponSpreadModel.None, 0f, 1f, 0, 30f),
            Magic(1295, "heat-ray", "Heat Ray", 260, 90, 15f, 10, 10,
                0, true, 8, 100, 200),
            // Poison/Venom Staff always launch at least three/four projectiles.
            // Pellet zero receives a zero random-perturbation coefficient and
            // is re-normalized to the nominal speed, so it is an exact direct
            // primary; all later random pellets remain uncredited.
            Magic(1308, "poison-staff", "Poison Staff", 265, 43, 13.5f,
                36, 36, 0, true, 22, 0, 37),
            // Golden Shower emits three identical type-280 primaries per
            // animation. AI_012 leaves updates 1..4 straight, then applies
            // +0.075 Y before every subsequent movement. Its shrinking scale
            // ends the useful path before the nominal timeLeft; 480 updates
            // is a deliberately conservative source-locked limit.
            Magic(1336, "golden-shower", "Golden Shower", 280, 30, 10f,
                6, 18, 0, true, 7, 2, 3600,
                WeaponBallisticKind.
                    DiscreteVerticalAccelerationPrimaryProjectile,
                WeaponSpreadModel.None, 0f, 1f, 480, 0f, 4, .075f),
            Magic(1444, "shadowbeam-staff", "Shadowbeam Staff", 294, 80,
                6f, 15, 15, 0, true, 7, 100, 300),
            // Type 295's AI_050 branch is visual-only during flight; its
            // separate type-296 death explosion is useful but not admitted as
            // guaranteed direct DPS.
            Magic(1445, "inferno-fork", "Inferno Fork", 295, 70, 8f,
                30, 30, 0, false, 18, 0, 3600),
            Magic(2188, "venom-staff", "Venom Staff", 355, 44, 14f, 30,
                30, 0, true, 25, 0, 58),
            // Razorpine (type 336) has one extra projectile update.  AI_001
            // leaves its velocity untouched through ai[0] == 49 and starts
            // the +0.5 Y acceleration at update 50.  Keep only that exact
            // straight prefix; the later capped fall is not inferred here.
            Magic(1930, "razorpine", "Razorpine", 336, 48, 12f, 8, 8,
                0, true, 5, 1, 3600,
                WeaponBallisticKind.ConservativeStraightPrefix,
                WeaponSpreadModel.None, 0f, 1f,
                SimpleMagicPrefixCatalog.RazorpinePrefixUpdates),
            // Crystal Serpent's type-521 AI_029 branch never changes velocity;
            // random shards are created only when the primary dies.
            Magic(3209, "crystal-serpent", "Crystal Serpent", 521, 40,
                8.5f, 29, 29, 0, true, 9, 1, 3600)
        };

        private static readonly Dictionary<WeaponProfileKey, WeaponProfile>
            Profiles = BuildProfiles();

        public static int Count => Profiles.Count;

        public static bool TryGet(int weaponId, int ammoId, out WeaponProfile profile)
        {
            if (Profiles.TryGetValue(new WeaponProfileKey(weaponId, ammoId),
                out profile)) return true;
            if (CommonWeaponOutputCatalog.TryGet(weaponId, ammoId,
                out profile)) return true;
            if (MeleeProjectileCatalog.TryGet(weaponId, ammoId,
                out profile)) return true;
            return RocketProductionCatalog.TryGet(weaponId, ammoId,
                out profile);
        }

        public static int ResolveProjectileForPair(int weaponId, int ammoId,
            int rawAmmoProjectileId)
        {
            return ResolveProjectileForPair(weaponId, ammoId,
                rawAmmoProjectileId, false);
        }

        public static int ResolveProjectileForPair(int weaponId, int ammoId,
            int rawAmmoProjectileId, bool hasMoltenQuiver)
        {
            WeaponProfile profile;
            if (!TryGet(weaponId, ammoId, out profile) ||
                profile.RawAmmoProjectileId != rawAmmoProjectileId)
                return rawAmmoProjectileId;
            if (!profile.UsesArrowModifiers) return profile.ProjectileId;
            bool moltenDamageBonus;
            return ResolveBowProjectile(weaponId, rawAmmoProjectileId,
                hasMoltenQuiver, out moltenDamageBonus);
        }

        private static Dictionary<WeaponProfileKey, WeaponProfile> BuildProfiles()
        {
            var result = new Dictionary<WeaponProfileKey, WeaponProfile>(
                Weapons.Length * Ammunition.Length +
                Bows.Length * Arrows.Length +
                DartWeapons.Length * Darts.Length + StarCannons.Length +
                MagicWeapons.Length +
                SnowballWeapons.Length * SnowballAmmunition.Length);
            for (var weaponIndex = 0; weaponIndex < Weapons.Length;
                weaponIndex++)
            {
                var weapon = Weapons[weaponIndex];
                for (var ammoIndex = 0; ammoIndex < Ammunition.Length;
                    ammoIndex++)
                {
                    var ammo = Ammunition[ammoIndex];
                    var projectile = weapon.OutputProjectileId > 0 ?
                        weapon.OutputProjectileId : weapon.ConvertsMusket &&
                        ammo.ProjectileId == 14 ? 242 : ammo.ProjectileId;
                    var extraUpdates = weapon.OutputProjectileId > 0 ?
                        weapon.OutputExtraUpdates : projectile == 242 ? 7 :
                        ammo.ExtraUpdates;
                    var lifetimeSubupdates = weapon.OutputProjectileId > 0 ?
                        weapon.OutputLifetimeSubupdates :
                        ammo.LifetimeSubupdates;
                    var observed = weapon.Id == 98 && ammo.Id == 97 ||
                        weapon.Id == 434 && ammo.Id == 515;
                    var profile = new WeaponProfile(weapon.Id, ammo.Id,
                        weapon.Name + " / " + ammo.Name,
                        weapon.Slug + "-" + ammo.Slug,
                        ammo.ProjectileId, projectile, weapon.FireMode,
                        weapon.SpreadModel, weapon.Spread,
                        WeaponBallisticKind.StraightPrimaryProjectile,
                        1f, 0, 0f,
                        OutputRouteKind.StraightRanged,
                        OutputResourceKind.Ammunition, observed,
                        weapon.ShootSpeed, ammo.ShootSpeed, weapon.Damage,
                        ammo.Damage, weapon.UseTime, weapon.UseAnimation,
                        weapon.ReuseDelay, weapon.AutoReuse, 0,
                        extraUpdates, lifetimeSubupdates);
                    result.Add(profile.Key, profile);
                }
            }
            for (var weaponIndex = 0; weaponIndex < SnowballWeapons.Length;
                weaponIndex++)
            {
                var weapon = SnowballWeapons[weaponIndex];
                for (var ammoIndex = 0; ammoIndex < SnowballAmmunition.Length;
                    ammoIndex++)
                {
                    var ammo = SnowballAmmunition[ammoIndex];
                    var profile = new WeaponProfile(weapon.Id, ammo.Id,
                        weapon.Name + " / " + ammo.Name,
                        weapon.Slug + "-" + ammo.Slug,
                        ammo.ProjectileId, ammo.ProjectileId,
                        weapon.FireMode, weapon.SpreadModel, weapon.Spread,
                        WeaponBallisticKind.ConservativeStraightPrefix,
                        1f, SnowballCannonCatalog.PrefixUpdateLimit, 0f,
                        OutputRouteKind.StraightRanged,
                        OutputResourceKind.Ammunition, false,
                        weapon.ShootSpeed, ammo.ShootSpeed, weapon.Damage,
                        ammo.Damage, weapon.UseTime, weapon.UseAnimation,
                        weapon.ReuseDelay, weapon.AutoReuse, 0,
                        ammo.ExtraUpdates, ammo.LifetimeSubupdates);
                    result.Add(profile.Key, profile);
                }
            }
            for (var bowIndex = 0; bowIndex < Bows.Length; bowIndex++)
            {
                var bow = Bows[bowIndex];
                for (var arrowIndex = 0; arrowIndex < Arrows.Length;
                    arrowIndex++)
                {
                    var arrow = Arrows[arrowIndex];
                    if (bow.WoodenArrowOnly && arrow.Id != 40 &&
                        arrow.Id != 3103)
                        continue;
                    bool moltenDamageBonus;
                    var projectile = ResolveBowProjectile(bow.Id,
                        arrow.ProjectileId, false, out moltenDamageBonus);
                    int extraUpdates;
                    int lifetimeSubupdates;
                    int accelerationDelay;
                    float verticalAcceleration;
                    bool quiverCanAddUpdate;
                    if (!TryGetDiscreteProjectileMotion(projectile,
                        out extraUpdates, out lifetimeSubupdates,
                        out accelerationDelay, out verticalAcceleration,
                        out quiverCanAddUpdate))
                        continue;
                    var spreadModel = bow.AngularSpreadRadians > 0f ?
                        WeaponSpreadModel.AngularCone :
                        WeaponSpreadModel.None;
                    var profile = new WeaponProfile(bow.Id, arrow.Id,
                        bow.Name + " / " + arrow.Name,
                        bow.Slug + "-" + arrow.Slug,
                        arrow.ProjectileId, projectile,
                        WeaponFireMode.Automatic, spreadModel,
                        bow.AngularSpreadRadians,
                        WeaponBallisticKind.
                            DiscreteVerticalAccelerationPrimaryProjectile,
                        1f, 0, 0f, OutputRouteKind.StraightRanged,
                        OutputResourceKind.Ammunition, false,
                        bow.ShootSpeed, arrow.ShootSpeed, bow.Damage,
                        arrow.Damage, bow.UseTime, bow.UseAnimation, 0,
                        bow.AutoReuse, 0, extraUpdates,
                        lifetimeSubupdates, accelerationDelay,
                        verticalAcceleration, true, quiverCanAddUpdate);
                    result.Add(profile.Key, profile);
                }
            }
            for (var weaponIndex = 0; weaponIndex < DartWeapons.Length;
                weaponIndex++)
            {
                var weapon = DartWeapons[weaponIndex];
                for (var dartIndex = 0; dartIndex < Darts.Length;
                    dartIndex++)
                {
                    var dart = Darts[dartIndex];
                    var profile = new WeaponProfile(weapon.Id, dart.Id,
                        weapon.Name + " / " + dart.Name,
                        weapon.Slug + "-" + dart.Slug,
                        dart.ProjectileId, dart.ProjectileId,
                        WeaponFireMode.Automatic, WeaponSpreadModel.None, 0f,
                        WeaponBallisticKind.
                            DiscreteVerticalAccelerationPrimaryProjectile,
                        1f, 0, 0f, OutputRouteKind.StraightRanged,
                        OutputResourceKind.Ammunition, false,
                        weapon.ShootSpeed, dart.ShootSpeed, weapon.Damage,
                        dart.Damage, weapon.UseTime, weapon.UseAnimation, 0,
                        weapon.AutoReuse, 0, dart.ExtraUpdates,
                        dart.LifetimeSubupdates,
                        dart.VerticalAccelerationDelaySubupdates,
                        dart.VerticalAccelerationPerSubupdate);
                    result.Add(profile.Key, profile);
                }
            }
            for (var index = 0; index < StarCannons.Length; index++)
            {
                var weapon = StarCannons[index];
                var profile = new WeaponProfile(weapon.Id, 75, weapon.Name,
                    weapon.Slug + "-fallen-star", 0, weapon.ProjectileId,
                    WeaponFireMode.Automatic, WeaponSpreadModel.None, 0f,
                    WeaponBallisticKind.StraightPrimaryProjectile,
                    1f, 0, 0f, OutputRouteKind.StraightRanged,
                    OutputResourceKind.Ammunition, false,
                    weapon.ShootSpeed, 0f, weapon.Damage, 0,
                    weapon.UseTime, weapon.UseAnimation, 0,
                    weapon.AutoReuse, 0, 0, 3600);
                result.Add(profile.Key, profile);
            }
            for (var index = 0; index < MagicWeapons.Length; index++)
            {
                var magic = MagicWeapons[index];
                var profile = new WeaponProfile(magic.Id, 0, magic.Name,
                    magic.Slug, magic.ProjectileId, magic.ProjectileId,
                    WeaponFireMode.Automatic, magic.SpreadModel,
                    magic.Spread, magic.Ballistics,
                    magic.VelocityRetentionPerUpdate,
                    magic.NativeDamagingUpdateLimit,
                    magic.ProjectileSafetyRadiusPixels,
                    OutputRouteKind.StraightMagic,
                    OutputResourceKind.Mana, false,
                    magic.ShootSpeed, 0f, magic.Damage, 0,
                    magic.UseTime, magic.UseAnimation, magic.ReuseDelay,
                    magic.AutoReuse, magic.Mana, magic.ExtraUpdates,
                    magic.LifetimeSubupdates,
                    magic.VerticalAccelerationDelaySubupdates,
                    magic.VerticalAccelerationPerSubupdate);
                result.Add(profile.Key, profile);
            }
            return result;
        }

        private static int ResolveBowProjectile(int weaponId,
            int rawAmmoProjectileId, bool hasMoltenQuiver,
            out bool moltenDamageBonus)
        {
            var projectile = rawAmmoProjectileId;
            // These PickAmmo conversions happen before the Molten Quiver test.
            if (weaponId == 3019 && projectile == 1) projectile = 485;
            if (weaponId == 3052) projectile = 495;
            if (weaponId == 2888 && projectile == 1) projectile = 469;
            if (weaponId == CommonWeaponOutputCatalog.EventideWeaponId &&
                projectile == 1)
                projectile = CommonWeaponOutputCatalog.EventideProjectileId;

            moltenDamageBonus = hasMoltenQuiver && projectile == 1;
            if (moltenDamageBonus) projectile = 2;

            // These ItemCheck_Shoot conversions happen after PickAmmo, so the
            // Molten Quiver damage bonus still applies to a raw wooden arrow.
            if (weaponId == 120 && projectile == 1) projectile = 2;
            if (weaponId == 682) projectile = 117;
            if (weaponId == 725) projectile = 120;
            if (weaponId == 2223) projectile = 357;
            return projectile;
        }

        private static bool TryGetDiscreteProjectileMotion(int projectileId,
            out int extraUpdates, out int lifetimeSubupdates,
            out int accelerationDelay, out float verticalAcceleration,
            out bool quiverCanAddUpdate)
        {
            extraUpdates = 0;
            lifetimeSubupdates = 1200;
            accelerationDelay = 14;
            verticalAcceleration = .1f;
            quiverCanAddUpdate = true;
            switch (projectileId)
            {
                case 1:
                case 2:
                case 4:
                case 103:
                case 474:
                case 41:
                    if (projectileId == 41) lifetimeSubupdates = 3600;
                    return true;
                case 5:
                    extraUpdates = 1;
                    lifetimeSubupdates = 120;
                    accelerationDelay = 0;
                    verticalAcceleration = 0f;
                    quiverCanAddUpdate = false;
                    return true;
                case 91:
                    accelerationDelay = 19;
                    verticalAcceleration = .07f;
                    return true;
                case 172:
                    accelerationDelay = 16;
                    verticalAcceleration = .085f;
                    return true;
                case 225:
                case 278:
                case 282:
                    extraUpdates = 1;
                    quiverCanAddUpdate = false;
                    return true;
                case 117:
                    extraUpdates = 2;
                    accelerationDelay = 34;
                    verticalAcceleration = .06f;
                    quiverCanAddUpdate = false;
                    return true;
                case 120:
                    extraUpdates = 1;
                    accelerationDelay = 29;
                    verticalAcceleration = .05f;
                    quiverCanAddUpdate = false;
                    return true;
                case 357:
                    extraUpdates = 2;
                    lifetimeSubupdates = 600;
                    accelerationDelay = 0;
                    verticalAcceleration = 0f;
                    quiverCanAddUpdate = false;
                    return true;
                case 469:
                case 485:
                    accelerationDelay = 0;
                    verticalAcceleration = 0f;
                    return true;
                case 495:
                    accelerationDelay = 29;
                    verticalAcceleration = .04f;
                    return true;
                case 639:
                    extraUpdates = 1;
                    lifetimeSubupdates = 90;
                    quiverCanAddUpdate = false;
                    return true;
                case 1006:
                    verticalAcceleration = -.1f;
                    return true;
                case CommonWeaponOutputCatalog.EventideProjectileId:
                    lifetimeSubupdates = CommonWeaponOutputCatalog.
                        EventideProjectileLifetime;
                    accelerationDelay = 0;
                    verticalAcceleration = 0f;
                    return true;
                default:
                    extraUpdates = lifetimeSubupdates = accelerationDelay = 0;
                    verticalAcceleration = 0f;
                    quiverCanAddUpdate = false;
                    return false;
            }
        }

        private static WeaponDefinition Weapon(int id, string slug,
            string name, int damage, int useTime, int useAnimation,
            int reuseDelay, float shootSpeed, bool autoReuse,
            bool convertsMusket, WeaponSpreadModel spreadModel,
            float spread, WeaponFireMode fireMode = WeaponFireMode.Automatic,
            int outputProjectileId = 0, int outputExtraUpdates = 0,
            int outputLifetimeSubupdates = 0)
        {
            return new WeaponDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                Damage = damage,
                UseTime = useTime,
                UseAnimation = useAnimation,
                ReuseDelay = reuseDelay,
                ShootSpeed = shootSpeed,
                AutoReuse = autoReuse,
                ConvertsMusket = convertsMusket,
                OutputProjectileId = outputProjectileId,
                OutputExtraUpdates = outputExtraUpdates,
                OutputLifetimeSubupdates = outputLifetimeSubupdates,
                SpreadModel = spreadModel,
                Spread = spread,
                FireMode = fireMode
            };
        }

        private static AmmoDefinition Ammo(int id, string slug, string name,
            int projectileId, int damage, float shootSpeed,
            int extraUpdates, int lifetimeSubupdates)
        {
            return new AmmoDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                ProjectileId = projectileId,
                Damage = damage,
                ShootSpeed = shootSpeed,
                ExtraUpdates = extraUpdates,
                LifetimeSubupdates = lifetimeSubupdates
            };
        }

        private static BowDefinition Bow(int id, string slug, string name,
            int damage, int useTime, int useAnimation, float shootSpeed,
            bool autoReuse, bool woodenArrowOnly = false,
            float angularSpreadRadians = 0f)
        {
            return new BowDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                Damage = damage,
                UseTime = useTime,
                UseAnimation = useAnimation,
                ShootSpeed = shootSpeed,
                AutoReuse = autoReuse,
                WoodenArrowOnly = woodenArrowOnly,
                AngularSpreadRadians = angularSpreadRadians
            };
        }

        private static ArrowDefinition Arrow(int id, string slug,
            string name, int projectileId, int damage, float shootSpeed,
            int extraUpdates, int lifetimeSubupdates)
        {
            return new ArrowDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                ProjectileId = projectileId,
                Damage = damage,
                ShootSpeed = shootSpeed,
                ExtraUpdates = extraUpdates,
                LifetimeSubupdates = lifetimeSubupdates
            };
        }

        private static DartWeaponDefinition DartWeapon(int id, string slug,
            string name, int damage, int useTime, int useAnimation,
            float shootSpeed, bool autoReuse)
        {
            return new DartWeaponDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                Damage = damage,
                UseTime = useTime,
                UseAnimation = useAnimation,
                ShootSpeed = shootSpeed,
                AutoReuse = autoReuse
            };
        }

        private static DartDefinition Dart(int id, string slug, string name,
            int projectileId, int damage, float shootSpeed, int extraUpdates,
            int lifetimeSubupdates, int verticalAccelerationDelaySubupdates,
            float verticalAccelerationPerSubupdate)
        {
            return new DartDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                ProjectileId = projectileId,
                Damage = damage,
                ShootSpeed = shootSpeed,
                ExtraUpdates = extraUpdates,
                LifetimeSubupdates = lifetimeSubupdates,
                VerticalAccelerationDelaySubupdates =
                    verticalAccelerationDelaySubupdates,
                VerticalAccelerationPerSubupdate =
                    verticalAccelerationPerSubupdate
            };
        }

        private static StarCannonDefinition StarCannon(int id, string slug,
            string name, int projectileId, int damage, int useTime,
            int useAnimation, float shootSpeed, bool autoReuse)
        {
            return new StarCannonDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                ProjectileId = projectileId,
                Damage = damage,
                UseTime = useTime,
                UseAnimation = useAnimation,
                ShootSpeed = shootSpeed,
                AutoReuse = autoReuse
            };
        }

        private static MagicDefinition Magic(int id, string slug, string name,
            int projectileId, int damage, float shootSpeed, int useTime,
            int useAnimation, int reuseDelay, bool autoReuse, int mana,
            int extraUpdates, int lifetimeSubupdates,
            WeaponBallisticKind ballistics =
                WeaponBallisticKind.StraightPrimaryProjectile,
            WeaponSpreadModel spreadModel = WeaponSpreadModel.None,
            float spread = 0f, float velocityRetentionPerUpdate = 1f,
            int nativeDamagingUpdateLimit = 0,
            float projectileSafetyRadiusPixels = 0f,
            int verticalAccelerationDelaySubupdates = 0,
            float verticalAccelerationPerSubupdate = 0f)
        {
            return new MagicDefinition
            {
                Id = id,
                Slug = slug,
                Name = name,
                ProjectileId = projectileId,
                Damage = damage,
                ShootSpeed = shootSpeed,
                UseTime = useTime,
                UseAnimation = useAnimation,
                ReuseDelay = reuseDelay,
                AutoReuse = autoReuse,
                Mana = mana,
                ExtraUpdates = extraUpdates,
                LifetimeSubupdates = lifetimeSubupdates,
                Ballistics = ballistics,
                SpreadModel = spreadModel,
                Spread = spread,
                VelocityRetentionPerUpdate = velocityRetentionPerUpdate,
                NativeDamagingUpdateLimit = nativeDamagingUpdateLimit,
                ProjectileSafetyRadiusPixels = projectileSafetyRadiusPixels,
                VerticalAccelerationDelaySubupdates =
                    verticalAccelerationDelaySubupdates,
                VerticalAccelerationPerSubupdate =
                    verticalAccelerationPerSubupdate
            };
        }

        private static bool IsKnownWeapon(int weaponId)
        {
            for (var index = 0; index < Weapons.Length; index++)
                if (Weapons[index].Id == weaponId) return true;
            for (var index = 0; index < Bows.Length; index++)
                if (Bows[index].Id == weaponId) return true;
            for (var index = 0; index < DartWeapons.Length; index++)
                if (DartWeapons[index].Id == weaponId) return true;
            for (var index = 0; index < StarCannons.Length; index++)
                if (StarCannons[index].Id == weaponId) return true;
            for (var index = 0; index < SnowballWeapons.Length; index++)
                if (SnowballWeapons[index].Id == weaponId) return true;
            if (RocketProductionCatalog.IsWeapon(weaponId)) return true;
            if (CommonWeaponOutputCatalog.IsWeapon(weaponId)) return true;
            for (var index = 0; index < MagicWeapons.Length; index++)
                if (MagicWeapons[index].Id == weaponId) return true;
            return false;
        }

        private static bool IsKnownAmmunitionWeapon(int weaponId)
        {
            for (var index = 0; index < Weapons.Length; index++)
                if (Weapons[index].Id == weaponId) return true;
            for (var index = 0; index < Bows.Length; index++)
                if (Bows[index].Id == weaponId) return true;
            for (var index = 0; index < DartWeapons.Length; index++)
                if (DartWeapons[index].Id == weaponId) return true;
            for (var index = 0; index < StarCannons.Length; index++)
                if (StarCannons[index].Id == weaponId) return true;
            for (var index = 0; index < SnowballWeapons.Length; index++)
                if (SnowballWeapons[index].Id == weaponId) return true;
            if (RocketProductionCatalog.IsWeapon(weaponId)) return true;
            if (CommonWeaponOutputCatalog.IsAmmunitionWeapon(weaponId))
                return true;
            return false;
        }

        public static WeaponProfileEvaluation Evaluate(WeaponProfileInput input)
        {
            WeaponProfile commonProfile;
            if (CommonWeaponOutputCatalog.TryGet(input.WeaponId,
                    input.AmmoId, out commonProfile))
                return CommonWeaponOutputCatalog.Evaluate(input,
                    commonProfile);
            WeaponProfile meleeProfile;
            if (MeleeProjectileCatalog.TryGet(input.WeaponId, input.AmmoId,
                out meleeProfile))
                return MeleeProjectileCatalog.Evaluate(input, meleeProfile);
            WeaponProfile profile;
            var knownPair = TryGet(input.WeaponId, input.AmmoId, out profile);
            // Native selection returns no ammo object (ID 0) when the last
            // compatible stack is exhausted. Keep that distinct from an
            // unsupported weapon/present ammo pair; do not invent an ammo ID.
            if (!input.HasAmmo && IsKnownAmmunitionWeapon(input.WeaponId))
                return Rejected(WeaponProfileStatus.MissingAmmo, profile);
            if (!knownPair)
                return Rejected(WeaponProfileStatus.UnsupportedCombination, null);
            var moltenArrowDamageBonus = false;
            if (profile.UsesArrowModifiers)
            {
                if (!input.ArrowStateKnown)
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                if (input.HarpyCharm)
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                var expectedProjectile = ResolveBowProjectile(
                    input.WeaponId, profile.RawAmmoProjectileId,
                    input.HasMoltenQuiver, out moltenArrowDamageBonus);
                if (input.ProjectileId != expectedProjectile ||
                    expectedProjectile != profile.ProjectileId)
                    return Rejected(WeaponProfileStatus.ProjectileMismatch,
                        profile);
            }
            else if (input.ProjectileId != profile.ProjectileId)
                return Rejected(WeaponProfileStatus.ProjectileMismatch,
                    profile);
            // Player.PackGemStaffFeatures reads the effective body armor for
            // every Gem Staff shot.  Robes 1282..1287 and 4256 alter the
            // projectile's trajectory, spawn count, damage, or hitbox. The
            // straight Diamond Staff contract cannot safely infer those bits,
            // so an unknown read or any such robe fails closed.
            if (IsGemStaffWeapon(profile.Key.WeaponId) &&
                (!input.GemStaffFeatureStateKnown ||
                 input.GemStaffEffectiveArmorType < 0 ||
                 IsDynamicGemStaffArmor(input.GemStaffEffectiveArmorType)))
                return Rejected(WeaponProfileStatus.InvalidProjectileFeatures,
                    profile);
            // Flower of Fire, Cursed Flames, and Razorpine are admitted only through their
            // separately reviewed straight prefixes.  Their later native AI
            // mutates velocity, so all identity/resource/timing checks must be
            // applied before the ordinary direct solver is allowed to see the
            // profile.
            if (SimpleMagicPrefixCatalog.IsPrefixWeapon(
                    profile.Key.WeaponId))
            {
                WeaponProfileStatus prefixFailure;
                if (!SimpleMagicPrefixCatalog.TryValidate(input, profile,
                        out prefixFailure))
                    return Rejected(prefixFailure, profile);
            }
            if (DemonScytheCatalog.IsWeapon(profile.Key.WeaponId))
            {
                WeaponProfileStatus demonScytheFailure;
                if (!DemonScytheCatalog.TryValidate(input, profile,
                        out demonScytheFailure))
                    return Rejected(demonScytheFailure, profile);
            }
            if (UnholyTridentCatalog.IsWeapon(profile.Key.WeaponId))
            {
                WeaponProfileStatus unholyTridentFailure;
                if (!UnholyTridentCatalog.TryValidate(input, profile,
                        out unholyTridentFailure))
                    return Rejected(unholyTridentFailure, profile);
            }
            if (AquaScepterCatalog.IsWeapon(profile.Key.WeaponId))
            {
                WeaponProfileStatus aquaScepterFailure;
                if (!AquaScepterCatalog.TryValidate(input, profile,
                        out aquaScepterFailure))
                    return Rejected(aquaScepterFailure, profile);
            }
            if (SnowballCannonCatalog.IsWeapon(profile.Key.WeaponId))
            {
                WeaponProfileStatus snowballFailure;
                if (!SnowballCannonCatalog.TryValidate(input, profile,
                        out snowballFailure))
                    return Rejected(snowballFailure, profile);
            }
            if (RocketProductionCatalog.IsWeapon(profile.Key.WeaponId))
            {
                WeaponProfileStatus rocketFailure;
                if (!RocketProductionCatalog.TryValidate(input, profile,
                        out rocketFailure))
                    return Rejected(rocketFailure, profile);
            }
            if (!FinitePositive(input.WeaponShootSpeed) || !FiniteNonnegative(input.AmmoShootSpeed) ||
                input.ProjectileExtraUpdates < 0 || input.ProjectileExtraUpdates > 128 ||
                input.ProjectileLifetimeSubupdates < 1 || input.ProjectileLifetimeSubupdates > 36000)
                return Rejected(WeaponProfileStatus.InvalidBallistics, profile);
            if (profile.Ballistics == WeaponBallisticKind.
                    DiscreteVerticalAccelerationPrimaryProjectile &&
                (input.ProjectileExtraUpdates != profile.DefaultExtraUpdates ||
                 profile.VerticalAccelerationDelaySubupdates < 0 ||
                 !FiniteSigned(profile.VerticalAccelerationPerSubupdate)))
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            // Pew-matic Horn's selected ammo may have extra updates of its
            // own, but the ItemCheck_Shoot override creates type 968, whose
            // native lifetime/update contract is independent of that ammo.
            // Do not let a stale pre-override sample silently select a wrong
            // trajectory model.
            if (profile.Key.WeaponId == 5117 &&
                (input.ProjectileExtraUpdates !=
                    profile.DefaultExtraUpdates ||
                 input.ProjectileLifetimeSubupdates !=
                    profile.DefaultLifetimeSubupdates))
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            // Gem Staff type 126 is authored with no extra updates. Unlike a
            // live lifetime sample (which may be captured one tick later), an
            // extra-update drift changes the movement cadence immediately and
            // cannot be represented by the straight route.
            if (IsGemStaffWeapon(profile.Key.WeaponId) &&
                input.ProjectileExtraUpdates != profile.DefaultExtraUpdates)
                return Rejected(WeaponProfileStatus.InvalidBallistics,
                    profile);
            var usesMana = profile.ResourceKind == OutputResourceKind.Mana;
            if (input.WeaponDamageAfterModifiers < 1 ||
                !usesMana && (input.AmmoBaseDamage < 0 ||
                    !FiniteNonnegative(input.AmmoDamageMultiplier)))
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            if (usesMana && (!input.ManaCostKnown ||
                input.ManaCostPerUse < 0 || input.ManaCostPerUse > 10000))
                return Rejected(WeaponProfileStatus.InvalidResource, profile);
            if (input.UseTime < 1 || input.UseTime > 3600 || input.UseAnimation < input.UseTime ||
                input.UseAnimation > 3600 || input.ReuseDelay < 0 || input.ReuseDelay > 3600 ||
                input.AnimationRemainingAtShot < 0 || input.AnimationRemainingAtShot > 3601 ||
                input.AutoReuse != profile.DefaultAutoReuse)
                return Rejected(WeaponProfileStatus.InvalidTiming, profile);

            var animation = input.AnimationRemainingAtShot == 0 ? input.UseAnimation - 1 : input.AnimationRemainingAtShot;
            var shotIndex = 0;
            var factor = 1f;
            var componentSpread = profile.SpreadModel ==
                WeaponSpreadModel.SymmetricComponent ?
                profile.ComponentSpread : 0f;
            if (profile.SpreadModel == WeaponSpreadModel.ClockworkBurst)
            {
                // Native uses absolute remaining-animation thresholds, not a
                // guessed fixed modulo of useTime or a generic bullet speed.
                if (animation < 5) { shotIndex = 2; factor = 1.1f; componentSpread = .4f; }
                else if (animation < 10) { shotIndex = 1; factor = 1.05f; componentSpread = .2f; }
                else { shotIndex = 0; componentSpread = 0f; }
            }
            var updates = input.ProjectileExtraUpdates + 1;
            var speedPerUpdate = (input.WeaponShootSpeed +
                (usesMana ? 0f : input.AmmoShootSpeed)) * factor;
            if (profile.UsesArrowModifiers)
            {
                if (input.MagicQuiver) speedPerUpdate *= 1.1f;
                if (input.Archery && speedPerUpdate < 20f)
                    speedPerUpdate = Math.Min(20f, speedPerUpdate * 1.2f);
            }
            var firstTickUpdates = updates;
            var sustainedUpdates = updates;
            if (profile.UsesArrowModifiers && input.MagicQuiver &&
                profile.MagicQuiverCanAddProjectileUpdate)
                sustainedUpdates = 2;
            var speed = speedPerUpdate * sustainedUpdates;
            var spreadPerUpdate = componentSpread * factor *
                1.4142135623730951f;
            if (profile.SpreadModel == WeaponSpreadModel.RadialMagnitude)
                spreadPerUpdate = profile.ComponentSpread * factor;
            if (profile.SpreadModel == WeaponSpreadModel.AngularCone)
                spreadPerUpdate = 2f * speedPerUpdate * (float)Math.Sin(
                    profile.ComponentSpread * .5f);
            var spread = spreadPerUpdate * sustainedUpdates;
            var minimumTravelSpeedPerUpdate = speedPerUpdate -
                spreadPerUpdate;
            if (profile.SpreadModel == WeaponSpreadModel.Gatligator)
            {
                // Native has two additive component envelopes (1.5 + 2.0).
                // On one third of shots each final component is independently
                // multiplied by [0.4,1.6]. Triangle inequalities give a strict
                // direction-independent maximum velocity error and minimum
                // path speed without sampling the game's RNG.
                const float additiveComponentEnvelope = 3.5f;
                const float maximumComponentMultiplier = 1.6f;
                const float maximumScaleError = .6f;
                const float minimumComponentMultiplier = .4f;
                var additiveEuclideanEnvelope = additiveComponentEnvelope *
                    1.4142135623730951f;
                spreadPerUpdate = maximumScaleError * speedPerUpdate +
                    maximumComponentMultiplier * additiveEuclideanEnvelope;
                spread = spreadPerUpdate * updates;
                minimumTravelSpeedPerUpdate = minimumComponentMultiplier *
                    (speedPerUpdate - additiveEuclideanEnvelope);
            }
            var lifetimeSubupdates = input.ProjectileLifetimeSubupdates;
            if (profile.NativeDamagingUpdateLimit > 0)
                lifetimeSubupdates = Math.Min(lifetimeSubupdates,
                    profile.NativeDamagingUpdateLimit);
            var lifetime = LifetimeTicks(lifetimeSubupdates,
                firstTickUpdates, sustainedUpdates);
            var range = minimumTravelSpeedPerUpdate * lifetimeSubupdates;
            if (profile.Ballistics ==
                WeaponBallisticKind.ExponentialDragPrimaryProjectile)
            {
                // The reviewed Crystal Storm path has no extra updates. A mod
                // changing that would alter both AI age and per-tick movement,
                // so fail closed instead of extending this evidence by guess.
                if (updates != 1 || profile.NativeDamagingUpdateLimit < 1 ||
                    !FinitePositive(profile.VelocityRetentionPerUpdate) ||
                    profile.VelocityRetentionPerUpdate >= 1f)
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                lifetime = lifetimeSubupdates;
                var retention = profile.VelocityRetentionPerUpdate;
                var movementFactor = retention *
                    (1f - (float)Math.Pow(retention, lifetimeSubupdates)) /
                    (1f - retention);
                range = (speedPerUpdate - spreadPerUpdate) * movementFactor;
                speed = speedPerUpdate;
            }
            if (profile.Ballistics ==
                WeaponBallisticKind.DemonScytheAcceleration)
            {
                // The standalone source contract already pinned these live
                // values. Keep a local structural check so this special route
                // cannot accidentally inherit a generic extra-update model.
                if (updates != 1 || input.ProjectileExtraUpdates !=
                        DemonScytheCatalog.ExtraUpdates ||
                    lifetimeSubupdates !=
                        DemonScytheCatalog.LifetimeSubupdates)
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                lifetime = lifetimeSubupdates;
                var movementFactor = DemonScytheCatalog.MovementFactor(
                    lifetimeSubupdates);
                if (!(movementFactor > 0d) || double.IsNaN(movementFactor) ||
                    double.IsInfinity(movementFactor))
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                range = (float)((speedPerUpdate - spreadPerUpdate) *
                    movementFactor);
                // This public field continues to describe launch speed. The
                // dedicated solver uses the exact per-update acceleration
                // schedule rather than reading it as a constant speed.
                speed = speedPerUpdate;
            }
            if (profile.Ballistics == WeaponBallisticKind.
                UnholyTridentDecay)
            {
                // The dedicated admission path pinned type 114's original
                // timeLeft=180, extraUpdates=2, and every item cadence field.
                // Native localAI[0] ends its useful flight at subupdate 146,
                // so use the final preceding live segment as the hard path
                // boundary rather than treating timeLeft as straight range.
                if (updates != UnholyTridentCatalog.UpdatesPerTick ||
                    input.ProjectileExtraUpdates !=
                        UnholyTridentCatalog.ExtraUpdates ||
                    input.ProjectileLifetimeSubupdates !=
                        UnholyTridentCatalog.LifetimeSubupdates ||
                    lifetimeSubupdates !=
                        UnholyTridentCatalog.LastDamagingSubupdate)
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                var movementFactor = UnholyTridentCatalog.MovementFactor(
                    lifetimeSubupdates);
                if (!(movementFactor > 0d) || double.IsNaN(movementFactor) ||
                    double.IsInfinity(movementFactor))
                    return Rejected(WeaponProfileStatus.InvalidBallistics,
                        profile);
                range = (float)((speedPerUpdate - spreadPerUpdate) *
                    movementFactor);
                // Keep the public speed as per-tick launch cadence for
                // selection code. UnholyTridentCatalog.Solve exclusively
                // consumes InitialSpeedPixelsPerSubupdate and the exact decay
                // recurrence, never this nominal 39px/tick value.
                speed = speedPerUpdate * updates;
            }
            // Match the native Single multiplication followed by conv.i4; the
            // weapon contribution was already rounded by GetWeaponDamage.
            var ammoBaseDamage = (long)input.AmmoBaseDamage;
            if (profile.UsesArrowModifiers)
            {
                if (moltenArrowDamageBonus) ammoBaseDamage += 2;
                if (input.SharpBarb) ammoBaseDamage += 1;
            }
            if (ammoBaseDamage < 0 || ammoBaseDamage > int.MaxValue)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            var ammoDamage = usesMana ? 0f :
                (float)ammoBaseDamage * input.AmmoDamageMultiplier;
            if (!FiniteNonnegative(ammoDamage) || ammoDamage >= int.MaxValue ||
                (long)input.WeaponDamageAfterModifiers + (long)ammoDamage > int.MaxValue)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            var damage = input.WeaponDamageAfterModifiers + (int)ammoDamage;
            var shotsPerCycle = 1 + (input.UseAnimation - 1) / input.UseTime;
            var actionCycle = input.UseAnimation + input.ReuseDelay +
                (input.AutoReuse ? 0 : 1);
            var dps = damage * (60f * shotsPerCycle / actionCycle);
            if (!FinitePositive(speed) ||
                !FinitePositive(minimumTravelSpeedPerUpdate) ||
                !FinitePositive(dps) || !FinitePositive(range))
                return Rejected(WeaponProfileStatus.InvalidBallistics, profile);
            return new WeaponProfileEvaluation
            {
                Status = WeaponProfileStatus.Supported, Profile = profile,
                SpeedPixelsPerTick = speed, MaxFlightTicks = lifetime,
                ConservativeRangePixels = range,
                DirectDamage = damage, ApproximateDirectDps = dps,
                SpreadSpeedPixelsPerTick = spread, BurstShotIndex = shotIndex,
                ManaCostPerUse = usesMana ? input.ManaCostPerUse : 0,
                InitialSpeedPixelsPerSubupdate = speedPerUpdate,
                SpreadSpeedPixelsPerSubupdate = spreadPerUpdate,
                FirstTickProjectileUpdates = firstTickUpdates,
                SustainedProjectileUpdatesPerTick = sustainedUpdates
            };
        }

        private static float LifetimeTicks(int lifetimeSubupdates,
            int firstTickUpdates, int sustainedUpdates)
        {
            if (lifetimeSubupdates <= firstTickUpdates)
                return lifetimeSubupdates / (float)firstTickUpdates;
            return 1f + (lifetimeSubupdates - firstTickUpdates) /
                (float)sustainedUpdates;
        }

        public static string Describe(WeaponProfileStatus status)
        {
            switch (status)
            {
                case WeaponProfileStatus.Supported: return "已适配此武器与弹药的主弹道；不保证命中或通关";
                case WeaponProfileStatus.MissingAmmo: return "已适配武器没有可用弹药；装入支持的弹药后重试";
                case WeaponProfileStatus.ProjectileMismatch: return "实际弹幕与此武器/弹药适配表不符，已停火";
                case WeaponProfileStatus.InvalidBallistics: return "无法确认实际弹速、更新次数或寿命，已停火";
                case WeaponProfileStatus.InvalidProjectileFeatures: return "live Gem Staff armor features are unknown or dynamic; fire is disabled";
                case WeaponProfileStatus.InvalidDamage: return "无法确认武器与弹药的实际伤害，已停火";
                case WeaponProfileStatus.InvalidTiming: return "无法确认该武器的动画与射击周期，已停火";
                case WeaponProfileStatus.InvalidResource: return "无法确认该魔法武器的实际魔力消耗，已停火";
                default: return "此武器与当前弹药的组合尚未适配，不进行通用猜测射击";
            }
        }

        private static WeaponProfileEvaluation Rejected(WeaponProfileStatus status, WeaponProfile profile) =>
            new WeaponProfileEvaluation { Status = status, Profile = profile };
        internal static bool FinitePositive(float value) => value > 0f && !float.IsInfinity(value);
        internal static bool FiniteNonnegative(float value) => value >= 0f && !float.IsInfinity(value);
        internal static bool FiniteSigned(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsDynamicGemStaffArmor(int armorType)
        {
            return armorType >= 1282 && armorType <= 1287 ||
                armorType == 4256;
        }

        private static bool IsGemStaffWeapon(int weaponId) =>
            weaponId == 739 || weaponId == 740 || weaponId == 741 ||
            weaponId == 742 || weaponId == 743 || weaponId == 744 ||
            weaponId == 3377;
    }
}
