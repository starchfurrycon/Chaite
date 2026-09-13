using System;

namespace Chaite.Core
{
    /// <summary>
    /// Source-locked contract for the Aqua Scepter (item 157 / projectile 22)
    /// in Terraria 1.4.5.8.  This is a single weapon route, not an aiStyle-12
    /// classifier.
    ///
    /// Type 22 runs three projectile updates per game tick.  Its first four
    /// updates preserve launch velocity; update five first adds +0.15 Y before
    /// movement.  The ordinary type starts with scale 1 and subtracts 0.01 on
    /// every update.  A native Single-rounding residue can make the exact kill
    /// frame platform-sensitive, so the route credits only the first 100
    /// updates, which are safely before any possible scale-expiry ambiguity.
    /// ItemCheck_Shoot supplies RotatedRelativePoint(MountedCenter) as the
    /// projectile center. Before gravity and movement, the explicit lava
    /// branch reads only that Center tile and only while velocity.Y is
    /// positive. The branch is deliberately not modeled here: the plugin must
    /// prove the bounded native path is lava-free before it emits input.
    /// </summary>
    public static class AquaScepterCatalog
    {
        public const int WeaponId = 157;
        public const int ProjectileId = 22;
        public const int DefaultDamage = 27;
        public const int StrongerVariantDamage = 90;
        public const int ManaCost = 7;
        public const int NormalUseTime = 8;
        public const int NormalUseAnimation = 16;
        public const int StrongerVariantUseTime = 5;
        public const int StrongerVariantUseAnimation = 10;
        public const int ReuseDelay = 0;
        public const bool AutoReuse = true;
        public const float ShootSpeed = 12.5f;
        public const int ExtraUpdates = 2;
        public const int UpdatesPerTick = ExtraUpdates + 1;
        public const int LifetimeSubupdates = 3600;
        public const int StraightSubupdates = 4;
        public const int FirstAcceleratedSubupdate = StraightSubupdates + 1;
        public const float VerticalAccelerationPerSubupdate = .15f;
        public const int LastReviewedSubupdate = 100;
        public const int ProjectileWidthPixels = 18;
        public const int ProjectileHeightPixels = 18;

        public static bool IsWeapon(int weaponId) => weaponId == WeaponId;

        public static bool IsKnownTiming(int useTime, int useAnimation) =>
            useTime == NormalUseTime && useAnimation == NormalUseAnimation ||
            useTime == StrongerVariantUseTime &&
                useAnimation == StrongerVariantUseAnimation;

        /// <summary>
        /// Advances one native dry-path subupdate.  The caller performs any
        /// environment check at the current center before this method,
        /// matching type 22's pre-gravity lava-check order.
        /// </summary>
        public static bool TryAdvanceDryPath(ref Vec2 position,
            ref Vec2 velocity, int oneBasedSubupdate)
        {
            if (oneBasedSubupdate < 1 ||
                oneBasedSubupdate > LastReviewedSubupdate ||
                !Finite(position) || !Finite(velocity))
                return false;
            if (oneBasedSubupdate >= FirstAcceleratedSubupdate)
                velocity.Y += VerticalAccelerationPerSubupdate;
            position += velocity;
            return Finite(position) && Finite(velocity);
        }

        /// <summary>
        /// Validates every live input that determines the reviewed route.
        /// The stronger world variant legitimately changes only damage and
        /// use timing, so both native timing pairs are accepted while all
        /// projectile, mana, and ammo identity observations remain exact.
        /// </summary>
        public static bool TryValidate(WeaponProfileInput input,
            WeaponProfile profile, out WeaponProfileStatus failure)
        {
            failure = WeaponProfileStatus.Supported;
            if (profile == null || profile.Key.WeaponId != WeaponId ||
                profile.Key.AmmoId != 0 ||
                profile.ProjectileId != ProjectileId ||
                profile.Ballistics != WeaponBallisticKind.
                    DiscreteVerticalAccelerationPrimaryProjectile ||
                profile.ResourceKind != OutputResourceKind.Mana ||
                !NearlyEqual(profile.DefaultWeaponShootSpeed, ShootSpeed) ||
                profile.DefaultReuseDelay != ReuseDelay ||
                profile.DefaultAutoReuse != AutoReuse ||
                profile.DefaultExtraUpdates != ExtraUpdates ||
                profile.DefaultLifetimeSubupdates != LifetimeSubupdates ||
                profile.NativeDamagingUpdateLimit != LastReviewedSubupdate ||
                profile.VerticalAccelerationDelaySubupdates !=
                    StraightSubupdates ||
                !NearlyEqual(profile.VerticalAccelerationPerSubupdate,
                    VerticalAccelerationPerSubupdate))
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }

            if (input.ProjectileId != ProjectileId ||
                input.ProjectileExtraUpdates != ExtraUpdates ||
                input.ProjectileLifetimeSubupdates != LifetimeSubupdates)
            {
                failure = WeaponProfileStatus.InvalidBallistics;
                return false;
            }

            if (!NearlyEqual(input.WeaponShootSpeed, ShootSpeed) ||
                !IsKnownTiming(input.UseTime, input.UseAnimation) ||
                input.ReuseDelay != ReuseDelay || input.AutoReuse != AutoReuse)
            {
                failure = WeaponProfileStatus.InvalidTiming;
                return false;
            }

            // Magic ItemCheck_Shoot does not pick ammunition for this item.
            // Any nonzero ammo observation is a different native path.
            if (input.AmmoId != 0 || input.AmmoBaseDamage != 0 ||
                input.AmmoShootSpeed != 0f || !input.HasAmmo)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }

            int expectedMana;
            if (!input.ManaCostKnown || !input.ManaBaseCostKnown ||
                !input.ManaCostMultiplierKnown || input.ManaBaseCost !=
                    ManaCost ||
                !TryScaledManaCost(ManaCost, input.ManaCostMultiplier,
                    out expectedMana) || input.ManaCostPerUse != expectedMana)
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

        private static bool TryScaledManaCost(int rawMana, float multiplier,
            out int scaledMana)
        {
            scaledMana = -1;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
                multiplier < 0f)
                return false;
            var scaled = rawMana * multiplier;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled) ||
                scaled < 0f || scaled >= int.MaxValue)
                return false;
            scaledMana = (int)scaled;
            return true;
        }

        private static bool NearlyEqual(float left, float right) =>
            !float.IsNaN(left) && !float.IsInfinity(left) &&
            Math.Abs(left - right) <= .0001f;

        private static bool Finite(Vec2 value) => !float.IsNaN(value.X) &&
            !float.IsInfinity(value.X) && !float.IsNaN(value.Y) &&
            !float.IsInfinity(value.Y);
    }
}
