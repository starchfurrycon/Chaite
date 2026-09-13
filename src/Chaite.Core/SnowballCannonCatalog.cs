using System;

namespace Chaite.Core
{
    /// <summary>
    /// The Snowball Cannon is not a bullet weapon.  Vanilla's ItemCheck_Shoot
    /// branch adds independent +/-0.8 velocity components and projectile 166's
    /// AI_002 mutates velocity on the twentieth update.  This contract admits
    /// only the exact straight prefix before that mutation; callers must not
    /// extend it into the gravity/damping phase.
    /// </summary>
    public static class SnowballCannonCatalog
    {
        public const int WeaponId = 1319;
        public const int AmmoId = 949;
        public const int ProjectileId = 166;
        public const int PrefixUpdateLimit = 19;
        public const float ComponentSpread = 0.8f;

        public static bool IsWeapon(int weaponId) => weaponId == WeaponId;

        /// <summary>
        /// Validates the live values which define the bounded prefix.  Damage,
        /// speed, and use cadence remain live so normal prefixes/stronger-world
        /// variants can be represented by the common evaluator.  Projectile
        /// identity and update cadence are fixed by the native branch.
        /// </summary>
        public static bool TryValidate(WeaponProfileInput input,
            WeaponProfile profile, out WeaponProfileStatus failure)
        {
            failure = WeaponProfileStatus.Supported;
            if (profile == null || !IsWeapon(input.WeaponId) ||
                input.AmmoId != AmmoId || profile.Key.WeaponId != WeaponId ||
                profile.Key.AmmoId != AmmoId ||
                profile.ProjectileId != ProjectileId ||
                profile.Ballistics != WeaponBallisticKind.
                    ConservativeStraightPrefix ||
                profile.NativeDamagingUpdateLimit != PrefixUpdateLimit)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }

            if (input.ProjectileId != ProjectileId ||
                input.ProjectileExtraUpdates != 0 ||
                input.ProjectileLifetimeSubupdates < PrefixUpdateLimit)
            {
                failure = WeaponProfileStatus.InvalidBallistics;
                return false;
            }

            // Both normal and stronger variants are auto-repeating and have no
            // native reuse delay.  Prefix-modified useTime/useAnimation values
            // are handled by the common timing/DPS calculation.
            if (!input.AutoReuse || input.ReuseDelay != 0)
            {
                failure = WeaponProfileStatus.InvalidTiming;
                return false;
            }

            if (!input.HasAmmo || input.AmmoBaseDamage < 0 ||
                input.AmmoShootSpeed < 0f ||
                float.IsNaN(input.AmmoShootSpeed) ||
                float.IsInfinity(input.AmmoShootSpeed))
            {
                failure = WeaponProfileStatus.MissingAmmo;
                return false;
            }
            return true;
        }
    }
}
