using System;

namespace Chaite.Core
{
    /// <summary>
    /// The first production special-ranged route.  Rocket Launcher + Rocket I
    /// has no tile/liquid/cluster side effect and is therefore safe to admit
    /// once its accelerating projectile is solved by the dedicated aim path.
    /// All other launcher/ammunition pairs remain research-only.
    /// </summary>
    public static class RocketProductionCatalog
    {
        public const int WeaponId = 759;
        public const int AmmoId = 771;
        public const int ProjectileId = 134;
        public const int LifetimeSubupdates = 180;

        private static readonly WeaponProfile Profile = new WeaponProfile(
            WeaponId, AmmoId, "Rocket Launcher / Rocket I",
            "rocket-launcher-rocket-i", 0, ProjectileId,
            WeaponFireMode.Automatic, WeaponSpreadModel.None, 0f,
            WeaponBallisticKind.RocketAcceleration, 1f, 0, 0f,
            OutputRouteKind.StraightRanged, OutputResourceKind.Ammunition,
            false, 5f, 0f, 55, 40, 30, 30, 0, true, 0, 0,
            LifetimeSubupdates);

        public static bool TryGet(int weaponId, int ammoId,
            out WeaponProfile profile)
        {
            if (weaponId == WeaponId && ammoId == AmmoId)
            {
                profile = Profile;
                return true;
            }
            profile = null;
            return false;
        }

        public static bool IsWeapon(int weaponId) => weaponId == WeaponId;

        public static bool TryValidate(WeaponProfileInput input,
            WeaponProfile profile, out WeaponProfileStatus failure)
        {
            failure = WeaponProfileStatus.Supported;
            if (profile == null || input.WeaponId != WeaponId ||
                input.AmmoId != AmmoId || input.ProjectileId != ProjectileId ||
                profile.Key.WeaponId != WeaponId ||
                profile.Key.AmmoId != AmmoId ||
                profile.ProjectileId != ProjectileId ||
                profile.Ballistics != WeaponBallisticKind.RocketAcceleration)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }
            if (!input.HasAmmo || input.ProjectileExtraUpdates != 0 ||
                input.ProjectileLifetimeSubupdates != LifetimeSubupdates)
            {
                failure = input.HasAmmo ? WeaponProfileStatus.InvalidBallistics :
                    WeaponProfileStatus.MissingAmmo;
                return false;
            }
            if (Math.Abs(input.WeaponShootSpeed - 5f) > .0001f ||
                input.AmmoShootSpeed != 0f || input.UseTime != 30 ||
                input.UseAnimation != 30 || input.ReuseDelay != 0 ||
                !input.AutoReuse)
            {
                failure = WeaponProfileStatus.InvalidTiming;
                return false;
            }
            return true;
        }
    }
}
