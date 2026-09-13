using System;

namespace Chaite.Core
{
    /// <summary>Only the native path shapes with an explicit environment gate.</summary>
    public enum NativeTrajectoryGatePathKind
    {
        Unspecified,
        StraightPrefix,
        DiscreteVerticalAcceleration
    }

    /// <summary>
    /// AllLiquids guards Projectile.Update's wet movement/destruction branch.
    /// LavaOnly guards a separately authored lava branch while the projectile
    /// itself has ignoreWater=true.
    /// </summary>
    public enum NativeTrajectoryGateLiquidPolicy
    {
        Unspecified,
        AllLiquids,
        LavaOnly
    }

    /// <summary>
    /// A finite, source-reviewed path that must remain in a known environment
    /// before native input can be emitted.  The profile contains no map or
    /// reflection state; callers sample only the small local trajectory.
    /// </summary>
    public struct NativeTrajectoryGateProfile
    {
        public int WeaponId;
        public int ProjectileId;
        public int MaximumSubupdates;
        public int ProjectileWidthPixels;
        public int ProjectileHeightPixels;
        public int VerticalAccelerationDelaySubupdates;
        public float InitialSpeedPixelsPerSubupdate;
        public float VerticalAccelerationPerSubupdate;
        public NativeTrajectoryGatePathKind PathKind;
        public NativeTrajectoryGateLiquidPolicy LiquidPolicy;

        // The all-liquid policy conservatively covers the projectile body and
        // the immediately adjacent tile ring. LavaOnly uses type 22's exact
        // single Center tile instead.
        public const float TileScanMarginPixels = 16f;

        public int MaximumSamples => MaximumSubupdates;
        public int MaximumTileReads => MaximumSamples *
            (LiquidPolicy == NativeTrajectoryGateLiquidPolicy.LavaOnly ?
                1 : 9);

        public bool IsSpecified => WeaponId > 0 && ProjectileId > 0 &&
            MaximumSubupdates > 0 && ProjectileWidthPixels > 0 &&
            ProjectileHeightPixels > 0 &&
            InitialSpeedPixelsPerSubupdate > 0f &&
            !float.IsNaN(InitialSpeedPixelsPerSubupdate) &&
            !float.IsInfinity(InitialSpeedPixelsPerSubupdate) &&
            PathKind != NativeTrajectoryGatePathKind.Unspecified &&
            LiquidPolicy != NativeTrajectoryGateLiquidPolicy.Unspecified;
    }

    /// <summary>
    /// Tiny exact allow-list for environment-sensitive reviewed paths.  It is
    /// intentionally not an aiStyle or projectile-family classifier: a new
    /// weapon needs an explicit native contract before it can reuse this gate.
    /// </summary>
    public static class NativeTrajectoryGateCatalog
    {
        public static bool RequiresGate(int weaponId, int projectileId) =>
            TryGet(weaponId, projectileId,
                out NativeTrajectoryGateProfile ignored);

        public static bool TryGet(int weaponId, int projectileId,
            out NativeTrajectoryGateProfile profile)
        {
            SimpleMagicPrefixProfile prefix;
            // Razorpine is intentionally absent. ItemCheck_Shoot emits 2..4
            // projectiles from different advanced centers with random spread
            // and random ai[0]. AI_001 then starts +0.5 Y at ai[0] >= 50, so
            // it cannot share this single straight AllLiquids trajectory.
            if ((weaponId == SimpleMagicPrefixCatalog.FlowerOfFireWeaponId ||
                    weaponId == SimpleMagicPrefixCatalog.
                        CursedFlamesWeaponId ||
                    weaponId == SimpleMagicPrefixCatalog.
                        FlowerOfFrostWeaponId) &&
                SimpleMagicPrefixCatalog.TryGet(weaponId, out prefix) &&
                prefix.ProjectileId == projectileId)
            {
                profile = new NativeTrajectoryGateProfile
                {
                    WeaponId = prefix.WeaponId,
                    ProjectileId = prefix.ProjectileId,
                    MaximumSubupdates = prefix.PrefixUpdateLimit,
                    ProjectileWidthPixels = 16,
                    ProjectileHeightPixels = 16,
                    InitialSpeedPixelsPerSubupdate = prefix.ShootSpeed,
                    VerticalAccelerationDelaySubupdates = 0,
                    VerticalAccelerationPerSubupdate = 0f,
                    PathKind = NativeTrajectoryGatePathKind.StraightPrefix,
                    LiquidPolicy = NativeTrajectoryGateLiquidPolicy.AllLiquids
                };
                return profile.IsSpecified;
            }

            if (weaponId == AquaScepterCatalog.WeaponId &&
                projectileId == AquaScepterCatalog.ProjectileId)
            {
                profile = new NativeTrajectoryGateProfile
                {
                    WeaponId = AquaScepterCatalog.WeaponId,
                    ProjectileId = AquaScepterCatalog.ProjectileId,
                    MaximumSubupdates = AquaScepterCatalog.
                        LastReviewedSubupdate,
                    ProjectileWidthPixels = AquaScepterCatalog.
                        ProjectileWidthPixels,
                    ProjectileHeightPixels = AquaScepterCatalog.
                        ProjectileHeightPixels,
                    InitialSpeedPixelsPerSubupdate = AquaScepterCatalog.
                        ShootSpeed,
                    VerticalAccelerationDelaySubupdates = AquaScepterCatalog.
                        StraightSubupdates,
                    VerticalAccelerationPerSubupdate = AquaScepterCatalog.
                        VerticalAccelerationPerSubupdate,
                    PathKind = NativeTrajectoryGatePathKind.
                        DiscreteVerticalAcceleration,
                    LiquidPolicy = NativeTrajectoryGateLiquidPolicy.LavaOnly
                };
                return profile.IsSpecified;
            }

            profile = default(NativeTrajectoryGateProfile);
            return false;
        }

        /// <summary>
        /// Advances one source-reviewed dry-path update.  Environment checks
        /// must run at the current point before the call: that ordering matches
        /// the native wet/lava branches.  This method is allocation-free and
        /// refuses all unlisted or out-of-range paths.
        /// </summary>
        public static bool TryAdvance(ref Vec2 position, ref Vec2 velocity,
            int oneBasedSubupdate, NativeTrajectoryGateProfile profile)
        {
            if (!profile.IsSpecified || oneBasedSubupdate < 1 ||
                oneBasedSubupdate > profile.MaximumSubupdates ||
                !Finite(position) || !Finite(velocity))
                return false;

            switch (profile.PathKind)
            {
                case NativeTrajectoryGatePathKind.StraightPrefix:
                    position += velocity;
                    break;
                case NativeTrajectoryGatePathKind.
                    DiscreteVerticalAcceleration:
                    if (oneBasedSubupdate >
                        profile.VerticalAccelerationDelaySubupdates)
                    {
                        velocity.Y += profile.
                            VerticalAccelerationPerSubupdate;
                    }
                    position += velocity;
                    break;
                default:
                    return false;
            }
            return Finite(position) && Finite(velocity);
        }

        private static bool Finite(Vec2 value) => !float.IsNaN(value.X) &&
            !float.IsInfinity(value.X) && !float.IsNaN(value.Y) &&
            !float.IsInfinity(value.Y);
    }
}
