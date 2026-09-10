using System;

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
        InvalidDamage,
        InvalidTiming
    }

    public enum WeaponFireMode { Automatic, NativeAnimationBurst }
    public enum WeaponBallisticKind { StraightPrimaryProjectile }
    public enum WeaponSecondaryEffect { None, CrystalShardsNotCredited }

    /// <summary>Source review and prior engine observation are deliberately separate evidence levels.</summary>
    public sealed class WeaponProfile
    {
        public WeaponProfileKey Key { get; }
        public string Name { get; }
        public int ProjectileId { get; }
        public WeaponFireMode FireMode { get; }
        public WeaponBallisticKind Ballistics => WeaponBallisticKind.StraightPrimaryProjectile;
        public WeaponSecondaryEffect SecondaryEffect { get; }
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
        public int DefaultExtraUpdates => 1;
        public int DefaultLifetimeSubupdates => 600;

        internal WeaponProfile(int weaponId, int ammoId, string name, bool previouslyObserved)
        {
            Key = new WeaponProfileKey(weaponId, ammoId);
            Name = name;
            ProjectileId = ammoId == 97 ? 14 : 89;
            FireMode = weaponId == 98 ? WeaponFireMode.Automatic : WeaponFireMode.NativeAnimationBurst;
            SecondaryEffect = ammoId == 515 ? WeaponSecondaryEffect.CrystalShardsNotCredited : WeaponSecondaryEffect.None;
            PairPreviouslyObservedInNativeEngine = previouslyObserved;
            DefaultWeaponShootSpeed = weaponId == 98 ? 7f : 7.75f;
            DefaultAmmoShootSpeed = ammoId == 97 ? 4f : 5f;
            DefaultWeaponDamage = weaponId == 98 ? 6 : 17;
            DefaultAmmoDamage = ammoId == 97 ? 7 : 9;
            DefaultUseTime = weaponId == 98 ? 8 : 4;
            DefaultUseAnimation = weaponId == 98 ? 8 : 12;
            DefaultReuseDelay = weaponId == 98 ? 0 : 14;
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
        public bool HasAmmo;
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
        // Maximum Euclidean velocity error due to native random spread, per full tick.
        public float SpreadSpeedPixelsPerTick;
        public int BurstShotIndex;
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
    /// </summary>
    public static class WeaponProfileCatalog
    {
        private static readonly WeaponProfile MinisharkMusket = new WeaponProfile(98, 97, "迷你鲨 / 火枪子弹", true);
        private static readonly WeaponProfile MinisharkCrystal = new WeaponProfile(98, 515, "迷你鲨 / 水晶子弹", false);
        private static readonly WeaponProfile ClockworkMusket = new WeaponProfile(434, 97, "发条式突击步枪 / 火枪子弹", false);
        private static readonly WeaponProfile ClockworkCrystal = new WeaponProfile(434, 515, "发条式突击步枪 / 水晶子弹", true);

        public static int Count => 4;

        public static bool TryGet(int weaponId, int ammoId, out WeaponProfile profile)
        {
            profile = null;
            if (weaponId == 98)
            {
                if (ammoId == 97) profile = MinisharkMusket;
                else if (ammoId == 515) profile = MinisharkCrystal;
            }
            else if (weaponId == 434)
            {
                if (ammoId == 97) profile = ClockworkMusket;
                else if (ammoId == 515) profile = ClockworkCrystal;
            }
            return profile != null;
        }

        public static WeaponProfileEvaluation Evaluate(WeaponProfileInput input)
        {
            WeaponProfile profile;
            var knownPair = TryGet(input.WeaponId, input.AmmoId, out profile);
            // Native selection returns no ammo object (ID 0) when the last
            // compatible stack is exhausted. Keep that distinct from an
            // unsupported weapon/present ammo pair; do not invent an ammo ID.
            if (!input.HasAmmo && (input.WeaponId == 98 || input.WeaponId == 434))
                return Rejected(WeaponProfileStatus.MissingAmmo, profile);
            if (!knownPair)
                return Rejected(WeaponProfileStatus.UnsupportedCombination, null);
            if (input.ProjectileId != profile.ProjectileId) return Rejected(WeaponProfileStatus.ProjectileMismatch, profile);
            if (!FinitePositive(input.WeaponShootSpeed) || !FiniteNonnegative(input.AmmoShootSpeed) ||
                input.ProjectileExtraUpdates < 0 || input.ProjectileExtraUpdates > 15 ||
                input.ProjectileLifetimeSubupdates < 1 || input.ProjectileLifetimeSubupdates > 36000)
                return Rejected(WeaponProfileStatus.InvalidBallistics, profile);
            if (input.WeaponDamageAfterModifiers < 1 || input.AmmoBaseDamage < 0 || !FiniteNonnegative(input.AmmoDamageMultiplier))
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            if (input.UseTime < 1 || input.UseTime > 3600 || input.UseAnimation < input.UseTime ||
                input.UseAnimation > 3600 || input.ReuseDelay < 0 || input.ReuseDelay > 3600 ||
                input.AnimationRemainingAtShot < 0 || input.AnimationRemainingAtShot > 3601)
                return Rejected(WeaponProfileStatus.InvalidTiming, profile);

            var animation = input.AnimationRemainingAtShot == 0 ? input.UseAnimation - 1 : input.AnimationRemainingAtShot;
            var shotIndex = 0;
            var factor = 1f;
            var componentSpread = .4f;
            if (profile.FireMode == WeaponFireMode.NativeAnimationBurst)
            {
                // Native uses absolute remaining-animation thresholds, not a
                // guessed fixed modulo of useTime or a generic bullet speed.
                if (animation < 5) { shotIndex = 2; factor = 1.1f; componentSpread = .4f; }
                else if (animation < 10) { shotIndex = 1; factor = 1.05f; componentSpread = .2f; }
                else { shotIndex = 0; componentSpread = 0f; }
            }
            var updates = input.ProjectileExtraUpdates + 1;
            var speed = (input.WeaponShootSpeed + input.AmmoShootSpeed) * factor * updates;
            var spread = componentSpread * factor * updates * 1.4142135623730951f;
            var lifetime = input.ProjectileLifetimeSubupdates / (float)updates;
            // Match the native Single multiplication followed by conv.i4; the
            // weapon contribution was already rounded by GetWeaponDamage.
            var ammoDamage = input.AmmoBaseDamage * input.AmmoDamageMultiplier;
            if (!FiniteNonnegative(ammoDamage) || ammoDamage >= int.MaxValue ||
                (long)input.WeaponDamageAfterModifiers + (long)ammoDamage > int.MaxValue)
                return Rejected(WeaponProfileStatus.InvalidDamage, profile);
            var damage = input.WeaponDamageAfterModifiers + (int)ammoDamage;
            var shotsPerCycle = 1 + (input.UseAnimation - 1) / input.UseTime;
            var dps = damage * (60f * shotsPerCycle / (input.UseAnimation + input.ReuseDelay));
            var range = (speed - spread) * lifetime;
            if (!FinitePositive(speed) || speed <= spread || !FinitePositive(dps) || !FinitePositive(range))
                return Rejected(WeaponProfileStatus.InvalidBallistics, profile);
            return new WeaponProfileEvaluation
            {
                Status = WeaponProfileStatus.Supported, Profile = profile,
                SpeedPixelsPerTick = speed, MaxFlightTicks = lifetime,
                ConservativeRangePixels = range,
                DirectDamage = damage, ApproximateDirectDps = dps,
                SpreadSpeedPixelsPerTick = spread, BurstShotIndex = shotIndex
            };
        }

        public static string Describe(WeaponProfileStatus status)
        {
            switch (status)
            {
                case WeaponProfileStatus.Supported: return "已适配此武器与弹药的主弹道；不保证命中或通关";
                case WeaponProfileStatus.MissingAmmo: return "已适配武器没有可用弹药；装入支持的弹药后重试";
                case WeaponProfileStatus.ProjectileMismatch: return "实际弹幕与此武器/弹药适配表不符，已停火";
                case WeaponProfileStatus.InvalidBallistics: return "无法确认实际弹速、更新次数或寿命，已停火";
                case WeaponProfileStatus.InvalidDamage: return "无法确认武器与弹药的实际伤害，已停火";
                case WeaponProfileStatus.InvalidTiming: return "无法确认该武器的动画与射击周期，已停火";
                default: return "此武器与当前弹药的组合尚未适配，不进行通用猜测射击";
            }
        }

        private static WeaponProfileEvaluation Rejected(WeaponProfileStatus status, WeaponProfile profile) =>
            new WeaponProfileEvaluation { Status = status, Profile = profile };
        internal static bool FinitePositive(float value) => value > 0f && !float.IsInfinity(value);
        internal static bool FiniteNonnegative(float value) => value >= 0f && !float.IsInfinity(value);
    }
}
