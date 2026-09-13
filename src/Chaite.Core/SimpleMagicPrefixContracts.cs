using System;

namespace Chaite.Core
{
    /// <summary>
    /// Native projectile prefixes which are straight before their reviewed
    /// AI starts mutating velocity.  This is intentionally a tiny allow-list;
    /// it is not a generic magic-projectile classifier.
    /// </summary>
    public struct SimpleMagicPrefixProfile
    {
        public int WeaponId;
        public int ProjectileId;
        public int ManaCost;
        public int Damage;
        public int UseTime;
        public int UseAnimation;
        public int ReuseDelay;
        public int ExtraUpdates;
        public int PrefixUpdateLimit;
        public float ShootSpeed;
        public bool AutoReuse;

        public bool IsKnown => WeaponId > 0 && ProjectileId > 0 &&
            ManaCost >= 0 && Damage > 0 && UseTime > 0 &&
            UseAnimation >= UseTime && ReuseDelay >= 0 &&
            ExtraUpdates >= 0 && PrefixUpdateLimit > 0 &&
            FinitePositive(ShootSpeed);

        private static bool FinitePositive(float value) => value > 0f &&
            !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>
    /// Exact native identity and live-state gate for the four currently
    /// admitted prefix routes.  The values are from Terraria 1.4.5.8
    /// SetDefaults/Projectile AI and are deliberately strict: a changed
    /// weapon timing, projectile cadence, or effective mana cost is returned
    /// to the caller as a closed route instead of being guessed.
    /// </summary>
    public static class SimpleMagicPrefixCatalog
    {
        public const int FlowerOfFireWeaponId = 112;
        public const int FlowerOfFireProjectileId = 15;
        public const int FlowerOfFirePrefixUpdates = 19;
        public const int CursedFlamesWeaponId = 519;
        public const int CursedFlamesProjectileId = 95;
        // AI_008 increments ai[1] before testing it.  Updates 1..19 retain
        // the launched vector; update 20 applies +0.2 Y before movement.
        public const int CursedFlamesPrefixUpdates = 19;
        public const int FlowerOfFrostWeaponId = 1264;
        public const int FlowerOfFrostProjectileId = 253;
        // Flower of Frost uses the ordinary AI_008 branch. It increments
        // ai[1] before the >=20 gravity test, so updates 1..19 retain the
        // launch vector. Update 20 applies +0.2 Y before movement. Its cold
        // debuff is a native secondary effect and is not included in the
        // direct-primary certificate.
        public const int FlowerOfFrostPrefixUpdates = 19;
        public const int RazorpineWeaponId = 1930;
        public const int RazorpineProjectileId = 336;
        public const int RazorpinePrefixUpdates = 49;

        public static bool TryGet(int weaponId,
            out SimpleMagicPrefixProfile profile)
        {
            switch (weaponId)
            {
                case FlowerOfFireWeaponId:
                    profile = new SimpleMagicPrefixProfile
                    {
                        WeaponId = FlowerOfFireWeaponId,
                        ProjectileId = FlowerOfFireProjectileId,
                        ManaCost = 9,
                        Damage = 48,
                        UseTime = 16,
                        UseAnimation = 16,
                        ReuseDelay = 0,
                        ExtraUpdates = 0,
                        PrefixUpdateLimit = FlowerOfFirePrefixUpdates,
                        ShootSpeed = 7.5f,
                        AutoReuse = false
                    };
                    return true;
                case CursedFlamesWeaponId:
                    profile = new SimpleMagicPrefixProfile
                    {
                        WeaponId = CursedFlamesWeaponId,
                        ProjectileId = CursedFlamesProjectileId,
                        ManaCost = 9,
                        Damage = 55,
                        UseTime = 15,
                        UseAnimation = 15,
                        ReuseDelay = 0,
                        ExtraUpdates = 0,
                        PrefixUpdateLimit = CursedFlamesPrefixUpdates,
                        ShootSpeed = 10f,
                        AutoReuse = true
                    };
                    return true;
                case FlowerOfFrostWeaponId:
                    profile = new SimpleMagicPrefixProfile
                    {
                        WeaponId = FlowerOfFrostWeaponId,
                        ProjectileId = FlowerOfFrostProjectileId,
                        ManaCost = 11,
                        Damage = 60,
                        UseTime = 12,
                        UseAnimation = 12,
                        ReuseDelay = 0,
                        ExtraUpdates = 0,
                        PrefixUpdateLimit = FlowerOfFrostPrefixUpdates,
                        ShootSpeed = 9f,
                        AutoReuse = false
                    };
                    return true;
                case RazorpineWeaponId:
                    profile = new SimpleMagicPrefixProfile
                    {
                        WeaponId = RazorpineWeaponId,
                        ProjectileId = RazorpineProjectileId,
                        ManaCost = 5,
                        Damage = 48,
                        UseTime = 8,
                        UseAnimation = 8,
                        ReuseDelay = 0,
                        ExtraUpdates = 1,
                        PrefixUpdateLimit = RazorpinePrefixUpdates,
                        ShootSpeed = 12f,
                        AutoReuse = true
                    };
                    return true;
                default:
                    profile = default(SimpleMagicPrefixProfile);
                    return false;
            }
        }

        public static bool IsPrefixWeapon(int weaponId) =>
            weaponId == FlowerOfFireWeaponId ||
            weaponId == CursedFlamesWeaponId ||
            weaponId == FlowerOfFrostWeaponId ||
            weaponId == RazorpineWeaponId;

        /// <summary>
        /// Validates the live adapter fields which affect the prefix model.
        /// Damage may be changed by a legitimate prefix or player modifier;
        /// the route only requires it to remain positive.  The projectile
        /// identity, cadence, timing, and effective mana cost are not safely
        /// inferable when changed and therefore fail closed.
        /// </summary>
        public static bool TryValidate(WeaponProfileInput input,
            WeaponProfile profile, out WeaponProfileStatus failure)
        {
            failure = WeaponProfileStatus.Supported;
            SimpleMagicPrefixProfile expected;
            if (profile == null || !TryGet(input.WeaponId, out expected) ||
                !expected.IsKnown || profile.Key.WeaponId != expected.WeaponId ||
                profile.Key.AmmoId != 0 ||
                profile.ProjectileId != expected.ProjectileId ||
                profile.Ballistics != WeaponBallisticKind.
                    ConservativeStraightPrefix ||
                profile.NativeDamagingUpdateLimit !=
                    expected.PrefixUpdateLimit)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }

            if (input.ProjectileId != expected.ProjectileId ||
                input.ProjectileExtraUpdates != expected.ExtraUpdates ||
                input.ProjectileLifetimeSubupdates <
                    expected.PrefixUpdateLimit)
            {
                failure = WeaponProfileStatus.InvalidBallistics;
                return false;
            }

            if (!NearlyEqual(input.WeaponShootSpeed, expected.ShootSpeed) ||
                input.UseTime != expected.UseTime ||
                input.UseAnimation != expected.UseAnimation ||
                input.ReuseDelay != expected.ReuseDelay ||
                input.AutoReuse != expected.AutoReuse)
            {
                failure = WeaponProfileStatus.InvalidTiming;
                return false;
            }

            // A magic item must have no picked-ammunition contribution.  A
            // nonzero field here means the adapter read the wrong branch.
            if (input.AmmoId != 0 || input.AmmoBaseDamage != 0 ||
                input.AmmoShootSpeed != 0f || !input.HasAmmo)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }

            if (!input.ManaCostKnown ||
                input.ManaCostPerUse != expected.ManaCost)
            {
                failure = WeaponProfileStatus.InvalidResource;
                return false;
            }

            if (input.WeaponDamageAfterModifiers < 1)
            {
                failure = WeaponProfileStatus.InvalidDamage;
                return false;
            }
            return true;
        }

        private static bool NearlyEqual(float left, float right) =>
            !float.IsNaN(left) && !float.IsInfinity(left) &&
            Math.Abs(left - right) <= .0001f;
    }
}
