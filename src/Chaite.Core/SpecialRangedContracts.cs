using System;

namespace Chaite.Core
{
    /// <summary>
    /// The small, version-locked portion of the vanilla special-ranged table
    /// which is safe to share with a planner.  This is deliberately a switch
    /// table instead of a dictionary: production calls it once per snapshot and
    /// the hot trajectory loops never allocate or depend on Terraria objects.
    /// </summary>
    [Flags]
    public enum SpecialRangedSafetyFlags
    {
        None = 0,
        RequiresCollisionEvidence = 1 << 0,
        DirectDamageDeferred = 1 << 1,
        WorldMutation = 1 << 2,
        RandomizedSpread = 1 << 3,
        ConservativeOnly = 1 << 4,
        Unknown = 1 << 5
    }

    public enum SpecialRangedLauncherKind
    {
        GrenadeLauncher = 0,
        RocketLauncher = 1,
        ProximityMineLauncher = 2,
        SnowmanCannon = 3,
        ElectrosphereLauncher = 4,
        Xenopopper = 5,
        VortexBeater = 6,
        CelebrationMk2 = 7
    }

    public struct SpecialRangedLauncherProfile
    {
        public int WeaponId;
        public SpecialRangedLauncherKind Kind;
        public int Damage;
        public int UseTime;
        public int UseAnimation;
        public float ShootSpeed;
        public bool AutoReuse;
        public bool Channel;
        public bool UsesHeldProjectile;
        public int HeldProjectileId;

        public bool IsKnown => WeaponId > 0 && UseTime > 0 &&
            UseAnimation > 0 && ShootSpeed > 0f;
    }

    public struct RocketAmmoProfile
    {
        public int AmmoId;
        public int Damage;
        public int GrenadeProjectileId;
        public int RocketProjectileId;
        public int ProximityProjectileId;
        public int SnowmanProjectileId;
        public int CelebrationProjectileId;
        public int TileDestructionRadius;
        public bool Cluster;
        public bool Liquid;
        public bool WorldMutation;

        public bool IsKnown => AmmoId > 0 && Damage > 0 &&
            GrenadeProjectileId > 0 && RocketProjectileId > 0 &&
            ProximityProjectileId > 0 && SnowmanProjectileId > 0 &&
            CelebrationProjectileId > 0;
    }

    public struct SpecialRangedProjectileContract
    {
        public int WeaponId;
        public int AmmoId;
        public int ProjectileId;
        public int Damage;
        public int ExplosionWidth;
        public int ExplosionHeight;
        public int TileDestructionRadius;
        public SpecialRangedSafetyFlags Safety;

        public bool IsKnown => WeaponId > 0 && AmmoId > 0 && ProjectileId > 0 &&
            Damage > 0 && ExplosionWidth >= 0 && ExplosionHeight >= 0;

        public bool CanUseForBossByDefault => IsKnown &&
            (Safety & (SpecialRangedSafetyFlags.WorldMutation |
                SpecialRangedSafetyFlags.Unknown)) == 0;
    }

    public static class SpecialRangedNativeCatalog
    {
        public static bool TryGetLauncher(int weaponId,
            out SpecialRangedLauncherProfile profile)
        {
            profile = default(SpecialRangedLauncherProfile);
            switch (weaponId)
            {
                case 758:
                    profile = Launcher(758, SpecialRangedLauncherKind.GrenadeLauncher,
                        60, 20, 10, true, false, false, 0);
                    return true;
                case 759:
                    profile = Launcher(759, SpecialRangedLauncherKind.RocketLauncher,
                        55, 30, 5, true, false, false, 0);
                    return true;
                case 760:
                    profile = Launcher(760, SpecialRangedLauncherKind.ProximityMineLauncher,
                        80, 50, 12, true, false, false, 0);
                    return true;
                case 1946:
                    profile = Launcher(1946, SpecialRangedLauncherKind.SnowmanCannon,
                        67, 15, 15, true, false, false, 0);
                    return true;
                case 2796:
                    profile = Launcher(2796, SpecialRangedLauncherKind.ElectrosphereLauncher,
                        40, 12, 12, false, false, false, 0);
                    return true;
                case 2797:
                    profile = Launcher(2797, SpecialRangedLauncherKind.Xenopopper,
                        45, 21, 12, true, false, false, 0);
                    return true;
                case 3475:
                    profile = Launcher(3475, SpecialRangedLauncherKind.VortexBeater,
                        50, 20, 20, false, true, true, 615);
                    return true;
                case 3930:
                    profile = Launcher(3930, SpecialRangedLauncherKind.CelebrationMk2,
                        50, 6, 17, false, true, true, 714);
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryGetAmmo(int ammoId, out RocketAmmoProfile profile)
        {
            profile = default(RocketAmmoProfile);
            switch (ammoId)
            {
                case 771:
                    profile = Ammo(771, 40, 133, 134, 135, 338, 715, 0, false, false, false);
                    return true;
                case 772:
                    profile = Ammo(772, 40, 136, 137, 138, 339, 716, 3, false, false, true);
                    return true;
                case 773:
                    profile = Ammo(773, 65, 139, 140, 141, 340, 717, 0, false, false, false);
                    return true;
                case 774:
                    profile = Ammo(774, 65, 142, 143, 144, 341, 718, 5, false, false, true);
                    return true;
                case 4445:
                    profile = Ammo(4445, 50, 777, 776, 778, 803, 717, 0, true, false, false);
                    return true;
                case 4446:
                    profile = Ammo(4446, 50, 781, 780, 782, 804, 718, 3, true, false, true);
                    return true;
                case 4447:
                    profile = Ammo(4447, 40, 785, 784, 786, 805, 717, 0, false, true, true);
                    return true;
                case 4448:
                    profile = Ammo(4448, 40, 788, 787, 789, 806, 717, 0, false, true, true);
                    return true;
                case 4449:
                    profile = Ammo(4449, 40, 791, 790, 792, 807, 717, 0, false, true, true);
                    return true;
                case 4457:
                    profile = Ammo(4457, 75, 794, 793, 795, 808, 717, 0, false, false, false);
                    return true;
                case 4458:
                    profile = Ammo(4458, 75, 797, 796, 798, 809, 718, 7, false, false, true);
                    return true;
                case 4459:
                    profile = Ammo(4459, 40, 800, 799, 801, 810, 717, 0, false, false, true);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Resolves the final projectile after vanilla's launcher/ammo mapping.
        /// Celebration deliberately folds special ammo to 717/718 and therefore
        /// does not inherit the ordinary launcher world-writing side effects.
        /// </summary>
        public static bool TryResolveProjectile(int weaponId, int ammoId,
            out SpecialRangedProjectileContract contract)
        {
            contract = default(SpecialRangedProjectileContract);
            SpecialRangedLauncherProfile launcher;
            RocketAmmoProfile ammo;
            if (!TryGetLauncher(weaponId, out launcher) || !TryGetAmmo(ammoId, out ammo))
                return false;

            var projectile = 0;
            var width = 128;
            var height = 128;
            var radius = ammo.TileDestructionRadius;
            var safety = SpecialRangedSafetyFlags.RequiresCollisionEvidence |
                SpecialRangedSafetyFlags.ConservativeOnly;
            switch (launcher.Kind)
            {
                case SpecialRangedLauncherKind.GrenadeLauncher:
                    projectile = ammo.GrenadeProjectileId;
                    width = height = ResolveBombExplosionSize(
                        launcher.Kind, ammoId, projectile);
                    break;
                case SpecialRangedLauncherKind.RocketLauncher:
                    projectile = ammo.RocketProjectileId;
                    width = height = ResolveBombExplosionSize(
                        launcher.Kind, ammoId, projectile);
                    break;
                case SpecialRangedLauncherKind.ProximityMineLauncher:
                    projectile = ammo.ProximityProjectileId;
                    width = height = ResolveBombExplosionSize(
                        launcher.Kind, ammoId, projectile);
                    break;
                case SpecialRangedLauncherKind.SnowmanCannon:
                    projectile = ammo.SnowmanProjectileId;
                    width = height = ResolveBombExplosionSize(
                        launcher.Kind, ammoId, projectile);
                    break;
                case SpecialRangedLauncherKind.ElectrosphereLauncher:
                    projectile = 442;
                    width = height = 0;
                    radius = 0;
                    safety |= SpecialRangedSafetyFlags.DirectDamageDeferred;
                    break;
                case SpecialRangedLauncherKind.CelebrationMk2:
                    projectile = ammo.CelebrationProjectileId;
                    width = height = projectile == 717 || projectile == 718 ? 240 : 128;
                    radius = projectile == 718 ? 5 : projectile == 716 ? 3 : 0;
                    safety &= ~SpecialRangedSafetyFlags.WorldMutation;
                    break;
                default:
                    // Xenopopper and Vortex use a separately audited held/in-flight
                    // contract; a guessed final projectile would be unsafe.
                    return false;
            }

            // Electrosphere's native shoot path deliberately replaces the
            // picked projectile with 442.  It therefore does not inherit the
            // picked rocket's cluster, liquid, nuke, or tile-writing effects,
            // just like Celebration's 717/718 folding path.
            if (ammo.WorldMutation &&
                launcher.Kind != SpecialRangedLauncherKind.CelebrationMk2 &&
                launcher.Kind != SpecialRangedLauncherKind.ElectrosphereLauncher)
                safety |= SpecialRangedSafetyFlags.WorldMutation;
            if (launcher.Kind == SpecialRangedLauncherKind.CelebrationMk2)
                safety |= SpecialRangedSafetyFlags.RandomizedSpread;
            contract = new SpecialRangedProjectileContract
            {
                WeaponId = weaponId,
                AmmoId = ammoId,
                ProjectileId = projectile,
                Damage = launcher.Damage + ammo.Damage,
                ExplosionWidth = width,
                ExplosionHeight = height,
                TileDestructionRadius = radius,
                Safety = safety
            };
            return true;
        }

        /// <summary>
        /// Returns the native damage rectangle used by the bomb-style
        /// projectiles after <c>PrepareBombToBlow</c>.  The mapped projectile is
        /// a launcher-specific parent for cluster rockets, so cluster children
        /// are intentionally not represented here.  The 809 case preserves a
        /// real 1.4.5.8 asymmetry: Snowman Cannon + Mini Nuke II keeps its
        /// 14x14 damage rectangle even though tile destruction still uses the
        /// Mini Nuke II seven-tile radius.
        /// </summary>
        private static int ResolveBombExplosionSize(
            SpecialRangedLauncherKind launcher, int ammoId, int projectile)
        {
            if (launcher == SpecialRangedLauncherKind.SnowmanCannon &&
                projectile == 809)
                return 14;

            // Wet/Lava/Honey/Dry projectiles are small liquid-effect bombs.
            if (ammoId == 4447 || ammoId == 4448 || ammoId == 4449 ||
                ammoId == 4459)
                return 48;

            // Mini Nuke I/II use the large 250 px hitbox (except 809 above).
            if (ammoId == 4457 || ammoId == 4458)
                return 250;

            // Rocket III/IV are larger than the ordinary Rocket I/II and
            // cluster parents.  The same family applies to all three
            // bomb-style launchers represented by this method.
            if (ammoId == 773 || ammoId == 774)
                return 200;

            return 128;
        }

        private static SpecialRangedLauncherProfile Launcher(int id,
            SpecialRangedLauncherKind kind, int damage, int useTime,
            float speed, bool autoReuse, bool channel, bool held, int heldId)
        {
            return new SpecialRangedLauncherProfile
            {
                WeaponId = id,
                Kind = kind,
                Damage = damage,
                UseTime = useTime,
                UseAnimation = useTime,
                ShootSpeed = speed,
                AutoReuse = autoReuse,
                Channel = channel,
                UsesHeldProjectile = held,
                HeldProjectileId = heldId
            };
        }

        private static RocketAmmoProfile Ammo(int id, int damage, int grenade,
            int rocket, int proximity, int snowman, int celebration, int radius,
            bool cluster, bool liquid, bool worldMutation)
        {
            return new RocketAmmoProfile
            {
                AmmoId = id,
                Damage = damage,
                GrenadeProjectileId = grenade,
                RocketProjectileId = rocket,
                ProximityProjectileId = proximity,
                SnowmanProjectileId = snowman,
                CelebrationProjectileId = celebration,
                TileDestructionRadius = radius,
                Cluster = cluster,
                Liquid = liquid,
                WorldMutation = worldMutation
            };
        }
    }

    public enum SpecialRangedTrajectoryPhase
    {
        Unsupported = 0,
        Traveling = 1,
        Impacted = 2,
        Expired = 3,
        Retired = 4
    }

    public struct StraightRocketTrajectoryState
    {
        public bool Known;
        public int ProjectileId;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Age;
        public int TimeLeft;
        public bool AcceleratesBelowThreshold;

        public bool IsActive => Known && TimeLeft > 0 &&
            Age >= 0 && PhaseIsFinite(Position) && PhaseIsFinite(Velocity);

        private static bool PhaseIsFinite(Vec2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }

    public struct StraightRocketCollision
    {
        public bool Known;
        public bool TileHit;
        public bool NpcHit;
    }

    public struct StraightRocketStep
    {
        public SpecialRangedTrajectoryPhase Phase;
        public Vec2 Position;
        public Vec2 Velocity;
        public bool Exploded;
        public bool Safe;
    }

    public static class StraightRocketTrajectory
    {
        public const float AccelerationThreshold = 15f;
        public const float Acceleration = 1.1f;

        public static bool TryCreate(Vec2 position, Vec2 velocity,
            int projectileId, int timeLeft, bool acceleratesBelowThreshold,
            out StraightRocketTrajectoryState state, out string reason)
        {
            state = default(StraightRocketTrajectoryState);
            reason = null;
            if (projectileId <= 0 || timeLeft <= 0 || timeLeft > 3600 ||
                !Finite(position) || !Finite(velocity))
            {
                reason = "invalid straight-rocket native state";
                return false;
            }
            state = new StraightRocketTrajectoryState
            {
                Known = true,
                ProjectileId = projectileId,
                Position = position,
                Velocity = velocity,
                Age = 0,
                TimeLeft = timeLeft,
                AcceleratesBelowThreshold = acceleratesBelowThreshold
            };
            return true;
        }

        /// <summary>
        /// Advances one projectile update. Collision evidence is mandatory: an
        /// unknown tile/NPC result fails closed instead of silently predicting a
        /// path through a wall or claiming a hit.
        /// </summary>
        public static bool TryAdvance(ref StraightRocketTrajectoryState state,
            in StraightRocketCollision collision, out StraightRocketStep step)
        {
            step = default(StraightRocketStep);
            if (!Valid(state) || !collision.Known)
            {
                step.Phase = SpecialRangedTrajectoryPhase.Unsupported;
                return false;
            }
            if (state.TimeLeft <= 0)
            {
                step.Phase = SpecialRangedTrajectoryPhase.Expired;
                step.Position = state.Position;
                step.Velocity = state.Velocity;
                step.Safe = true;
                return true;
            }

            var next = state;
            next.Age++;
            next.TimeLeft--;
            if (next.AcceleratesBelowThreshold &&
                Math.Abs(next.Velocity.X) < AccelerationThreshold &&
                Math.Abs(next.Velocity.Y) < AccelerationThreshold)
                next.Velocity *= Acceleration;
            next.Position += next.Velocity;

            if (collision.TileHit || collision.NpcHit)
            {
                next.Velocity = new Vec2(0f, 0f);
                state = next;
                step = new StraightRocketStep
                {
                    Phase = SpecialRangedTrajectoryPhase.Impacted,
                    Position = next.Position,
                    Velocity = next.Velocity,
                    Exploded = true,
                    Safe = true
                };
                return true;
            }
            state = next;
            step = new StraightRocketStep
            {
                Phase = next.TimeLeft <= 0 ? SpecialRangedTrajectoryPhase.Expired :
                    SpecialRangedTrajectoryPhase.Traveling,
                Position = next.Position,
                Velocity = next.Velocity,
                Exploded = false,
                Safe = true
            };
            return true;
        }

        public static bool TryPredictCollisionFree(in StraightRocketTrajectoryState source,
            int updates, out Vec2 position, out Vec2 velocity)
        {
            position = source.Position;
            velocity = source.Velocity;
            if (!Valid(source) || updates < 0 || updates > source.TimeLeft)
                return false;
            var state = source;
            var collision = new StraightRocketCollision { Known = true };
            for (var i = 0; i < updates; i++)
            {
                StraightRocketStep step;
                if (!TryAdvance(ref state, in collision, out step) || step.Exploded)
                    return false;
            }
            position = state.Position;
            velocity = state.Velocity;
            return true;
        }

        private static bool Valid(in StraightRocketTrajectoryState state) =>
            state.Known && state.ProjectileId > 0 && state.Age >= 0 &&
            state.TimeLeft >= 0 && state.TimeLeft <= 3600 && Finite(state.Position) &&
            Finite(state.Velocity);

        private static bool Finite(Vec2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }

    public struct GrenadeTrajectoryState
    {
        public bool Known;
        public int ProjectileId;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Age;
        public int TimeLeft;
        public float Gravity;
        public float BounceDamping;
        public float LandDamping;
    }

    public struct GrenadeCollision
    {
        public bool Known;
        public bool TileHit;
        public bool Landed;
        public bool HasNormal;
        public Vec2 Normal;
    }

    public struct GrenadeTrajectoryStep
    {
        public SpecialRangedTrajectoryPhase Phase;
        public Vec2 Position;
        public Vec2 Velocity;
        public bool Exploded;
        public bool Safe;
    }

    public static class GrenadeTrajectory
    {
        public const int GravityStartsAfterUpdate = 15;
        public const int DefaultLifetime = 180;

        public static bool TryCreate(Vec2 position, Vec2 velocity, int projectileId,
            int timeLeft, out GrenadeTrajectoryState state, out string reason)
        {
            state = default(GrenadeTrajectoryState);
            reason = null;
            if (projectileId <= 0 || timeLeft <= 0 || timeLeft > 3600 ||
                !Finite(position) || !Finite(velocity))
            {
                reason = "invalid grenade native state";
                return false;
            }
            state = new GrenadeTrajectoryState
            {
                Known = true,
                ProjectileId = projectileId,
                Position = position,
                Velocity = velocity,
                TimeLeft = timeLeft,
                Gravity = .2f,
                BounceDamping = -.4f,
                LandDamping = .95f
            };
            return true;
        }

        public static bool TryAdvance(ref GrenadeTrajectoryState state,
            in GrenadeCollision collision, out GrenadeTrajectoryStep step)
        {
            step = default(GrenadeTrajectoryStep);
            if (!Valid(state) || !collision.Known)
            {
                step.Phase = SpecialRangedTrajectoryPhase.Unsupported;
                return false;
            }
            if (state.TimeLeft <= 0)
            {
                step.Phase = SpecialRangedTrajectoryPhase.Expired;
                step.Position = state.Position;
                step.Velocity = state.Velocity;
                step.Safe = true;
                return true;
            }
            var next = state;
            next.Age++;
            next.TimeLeft--;
            if (next.Age > GravityStartsAfterUpdate)
                next.Velocity.Y += next.Gravity;
            next.Position += next.Velocity;
            if (collision.TileHit)
            {
                if (!collision.HasNormal)
                {
                    step.Phase = SpecialRangedTrajectoryPhase.Unsupported;
                    return false;
                }
                if (Math.Abs(collision.Normal.X) >= Math.Abs(collision.Normal.Y))
                    next.Velocity.X *= next.BounceDamping;
                else
                    next.Velocity.Y *= next.BounceDamping;
            }
            if (collision.Landed)
                next.Velocity.X *= next.LandDamping;
            state = next;
            step = new GrenadeTrajectoryStep
            {
                Phase = next.TimeLeft <= 0 ? SpecialRangedTrajectoryPhase.Expired :
                    SpecialRangedTrajectoryPhase.Traveling,
                Position = next.Position,
                Velocity = next.Velocity,
                Exploded = false,
                Safe = true
            };
            return true;
        }

        public static bool TryPredictNoCollision(in GrenadeTrajectoryState source,
            int updates, out Vec2 position, out Vec2 velocity)
        {
            position = source.Position;
            velocity = source.Velocity;
            if (!Valid(source) || updates < 0 || updates > source.TimeLeft)
                return false;
            var state = source;
            var collision = new GrenadeCollision { Known = true };
            for (var i = 0; i < updates; i++)
            {
                GrenadeTrajectoryStep step;
                if (!TryAdvance(ref state, in collision, out step)) return false;
            }
            position = state.Position;
            velocity = state.Velocity;
            return true;
        }

        private static bool Valid(in GrenadeTrajectoryState state) =>
            state.Known && state.ProjectileId > 0 && state.Age >= 0 &&
            state.TimeLeft >= 0 && state.TimeLeft <= 3600 &&
            state.Gravity >= 0f && state.BounceDamping <= 0f &&
            state.LandDamping >= 0f && state.LandDamping <= 1f &&
            Finite(state.Position) && Finite(state.Velocity);

        private static bool Finite(Vec2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }

    public struct SnowmanTargetObservation
    {
        public bool Known;
        public int Key;
        public Vec2 Center;
        public Vec2 Velocity;
        public bool Chaseable;
        public bool LineOfSightKnown;
        public bool HasLineOfSight;
    }

    public struct SnowmanTargetSelection
    {
        public bool Found;
        public int Key;
        public Vec2 Center;
        public Vec2 Velocity;
        public int ManhattanDistance;
    }

    public struct SnowmanTrajectoryState
    {
        public bool Known;
        public int ProjectileId;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Age;
        public int TimeLeft;
        public bool HasTarget;
        public int TargetKey;
        public Vec2 TargetCenter;
        public Vec2 TargetVelocity;
    }

    public struct SnowmanCollision
    {
        public bool Known;
        public bool TileHit;
    }

    public struct SnowmanTrajectoryStep
    {
        public SpecialRangedTrajectoryPhase Phase;
        public Vec2 Position;
        public Vec2 Velocity;
        public bool HasTarget;
        public int TargetKey;
        public bool Exploded;
        public bool Safe;
    }

    public static class SnowmanTrajectory
    {
        public const int SearchStartsAfterUpdate = 30;
        public const int SearchManhattanRange = 600;
        public const int RetainManhattanRange = 1000;
        public const float TargetSpeed = 16f;
        public const float LerpAmount = 1f / 12f;

        /// <summary>Chooses the nearest known chaseable LOS target without allocation.</summary>
        public static bool TrySelectTarget(Vec2 projectileCenter,
            SnowmanTargetObservation[] candidates, int count,
            int maxManhattanDistance, out SnowmanTargetSelection selection)
        {
            selection = default(SnowmanTargetSelection);
            if (candidates == null || count < 0 || count > candidates.Length ||
                maxManhattanDistance <= 0 || !Finite(projectileCenter)) return false;
            var bestDistance = int.MaxValue;
            var bestKey = int.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Known || candidate.Key < 0 || !candidate.Chaseable ||
                    !candidate.LineOfSightKnown || !candidate.HasLineOfSight ||
                    !Finite(candidate.Center) || !Finite(candidate.Velocity)) continue;
                var distance = Manhattan(projectileCenter, candidate.Center);
                if (distance >= maxManhattanDistance || distance > bestDistance ||
                    distance == bestDistance && candidate.Key >= bestKey) continue;
                bestDistance = distance;
                bestKey = candidate.Key;
                selection = new SnowmanTargetSelection
                {
                    Found = true,
                    Key = candidate.Key,
                    Center = candidate.Center,
                    Velocity = candidate.Velocity,
                    ManhattanDistance = distance
                };
            }
            return true;
        }

        public static bool TryCreate(Vec2 position, Vec2 velocity, int projectileId,
            int timeLeft, out SnowmanTrajectoryState state, out string reason)
        {
            state = default(SnowmanTrajectoryState);
            reason = null;
            if (projectileId <= 0 || timeLeft <= 0 || timeLeft > 3600 ||
                !Finite(position) || !Finite(velocity))
            {
                reason = "invalid snowman native state";
                return false;
            }
            state = new SnowmanTrajectoryState
            {
                Known = true,
                ProjectileId = projectileId,
                Position = position,
                Velocity = velocity,
                TimeLeft = timeLeft
            };
            return true;
        }

        /// <summary>
        /// Advances a Snowman projectile. The caller supplies the complete current
        /// candidate set; a missing/unknown set is rejected once native search has
        /// started, preventing a guessed lock on a boss behind a wall.
        /// </summary>
        public static bool TryAdvance(ref SnowmanTrajectoryState state,
            SnowmanTargetObservation[] candidates, int count,
            in SnowmanCollision collision, out SnowmanTrajectoryStep step)
        {
            step = default(SnowmanTrajectoryStep);
            if (!Valid(state) || !collision.Known)
            {
                step.Phase = SpecialRangedTrajectoryPhase.Unsupported;
                return false;
            }
            var next = state;
            next.Age++;
            next.TimeLeft--;
            if (next.Age > SearchStartsAfterUpdate)
            {
                if (candidates == null || count < 0 || count > candidates.Length)
                {
                    step.Phase = SpecialRangedTrajectoryPhase.Unsupported;
                    return false;
                }
                SnowmanTargetSelection selected = default(SnowmanTargetSelection);
                var lostTarget = next.HasTarget && !TryFindRetained(next.Position,
                    candidates, count, next.TargetKey, out selected);
                if (lostTarget)
                    next.HasTarget = false;
                // Native's locked-target branch clears ai[1] and leaves this
                // update without a replacement search.  Reacquiring in the same
                // frame would turn a bounded loss into an unobserved target swap.
                if (!next.HasTarget && !lostTarget)
                {
                    // Search uses the projectile center and a 600 px Manhattan
                    // gate, exactly as the reviewed native branch does.
                    if (!TrySelectTarget(next.Position, candidates, count,
                        SearchManhattanRange, out selected))
                    {
                        step.Phase = SpecialRangedTrajectoryPhase.Unsupported;
                        return false;
                    }
                    if (selected.Found)
                    {
                        next.HasTarget = true;
                        next.TargetKey = selected.Key;
                        next.TargetCenter = selected.Center;
                        next.TargetVelocity = selected.Velocity;
                    }
                }
                else
                {
                    next.TargetCenter = selected.Center;
                    next.TargetVelocity = selected.Velocity;
                }
            }
            if (next.HasTarget)
            {
                var delta = next.TargetCenter - next.Position;
                var desired = delta.LengthSquared > .0001f
                    ? delta.Normalized() * TargetSpeed : new Vec2(0f, 0f);
                next.Velocity += (desired - next.Velocity) * LerpAmount;
            }
            next.Position += next.Velocity;
            if (collision.TileHit)
            {
                next.Velocity = new Vec2(0f, 0f);
                next.HasTarget = false;
                state = next;
                step = new SnowmanTrajectoryStep
                {
                    Phase = SpecialRangedTrajectoryPhase.Impacted,
                    Position = next.Position,
                    Velocity = next.Velocity,
                    HasTarget = false,
                    Exploded = true,
                    Safe = true
                };
                return true;
            }
            state = next;
            step = new SnowmanTrajectoryStep
            {
                Phase = next.TimeLeft <= 0 ? SpecialRangedTrajectoryPhase.Expired :
                    SpecialRangedTrajectoryPhase.Traveling,
                Position = next.Position,
                Velocity = next.Velocity,
                HasTarget = next.HasTarget,
                TargetKey = next.TargetKey,
                Exploded = false,
                Safe = true
            };
            return true;
        }

        private static bool TryFindRetained(Vec2 position,
            SnowmanTargetObservation[] candidates, int count, int key,
            out SnowmanTargetSelection selection)
        {
            selection = default(SnowmanTargetSelection);
            for (var i = 0; i < count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.Known || candidate.Key != key || !candidate.Chaseable ||
                    !candidate.LineOfSightKnown || !candidate.HasLineOfSight ||
                    !Finite(candidate.Center) || !Finite(candidate.Velocity)) continue;
                var distance = Manhattan(position, candidate.Center);
                if (distance >= RetainManhattanRange) continue;
                selection = new SnowmanTargetSelection
                {
                    Found = true,
                    Key = key,
                    Center = candidate.Center,
                    Velocity = candidate.Velocity,
                    ManhattanDistance = distance
                };
                return true;
            }
            return false;
        }

        private static int Manhattan(Vec2 a, Vec2 b) =>
            (int)Math.Min(int.MaxValue, Math.Abs((double)a.X - b.X) +
                Math.Abs((double)a.Y - b.Y));

        private static bool Valid(in SnowmanTrajectoryState state) =>
            state.Known && state.ProjectileId > 0 && state.Age >= 0 &&
            state.TimeLeft >= 0 && state.TimeLeft <= 3600 &&
            Finite(state.Position) && Finite(state.Velocity) &&
            (!state.HasTarget || Finite(state.TargetCenter) && Finite(state.TargetVelocity));

        private static bool Finite(Vec2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }

    public struct CelebrationHeldState
    {
        public bool Known;
        public int HeldProjectileId;
        public int StateIndex;
        public int TicksUntilEvent;
        public int EventOrdinal;
        public bool ChannelHeld;
    }

    public struct CelebrationEventPlan
    {
        public bool Emitted;
        public int StateIndex;
        public int RocketCount;
        public int ProjectileId;
        public float BaseSpeed;
        public float AimSpreadDegrees;
        public bool AmmoPickPerformed;
        public bool AmmoConsumptionGuaranteed;
        public int MaxAmmoConsumed;
        public SpecialRangedSafetyFlags Safety;
    }

    public static class CelebrationMk2Trajectory
    {
        public const int HeldProjectileId = 714;
        public const int EventPeriod = 8;
        public const int StateCount = 7;
        public const float HeldAimSpreadDegrees = 1.875f;
        public const float RocketSpreadDegrees = 5.625f;
        public const float WorstCaseAimSpreadDegrees = 7.5f;

        public static bool TryCreate(int stateIndex, int ticksUntilEvent,
            out CelebrationHeldState state, out string reason)
        {
            state = default(CelebrationHeldState);
            reason = null;
            if (stateIndex < 0 || stateIndex >= StateCount ||
                ticksUntilEvent < 0 || ticksUntilEvent > EventPeriod)
            {
                reason = "invalid Celebration Mk2 held state";
                return false;
            }
            state = new CelebrationHeldState
            {
                Known = true,
                HeldProjectileId = HeldProjectileId,
                StateIndex = stateIndex,
                TicksUntilEvent = ticksUntilEvent,
                EventOrdinal = 0,
                ChannelHeld = true
            };
            return true;
        }

        /// <summary>
        /// Advances the held 714 state by one game update. Each event performs
        /// exactly one PickAmmo-equivalent lookup; state 4 emits two rockets and
        /// state 5 emits three, while consuming at most one ammo item.
        /// </summary>
        public static bool TryAdvance(ref CelebrationHeldState state,
            bool channelHeld, int ammoId, out CelebrationEventPlan plan)
        {
            plan = default(CelebrationEventPlan);
            if (!Valid(state)) return false;
            state.ChannelHeld = channelHeld;
            if (!channelHeld) return true;
            if (state.TicksUntilEvent > 0)
            {
                state.TicksUntilEvent--;
                return true;
            }
            RocketAmmoProfile ammo;
            if (!SpecialRangedNativeCatalog.TryGetAmmo(ammoId, out ammo)) return false;
            var projectile = ammo.CelebrationProjectileId;
            var count = state.StateIndex == 4 ? 2 : state.StateIndex == 5 ? 3 : 1;
            var speed = state.StateIndex == 3 ? 9f : 8f;
            plan = new CelebrationEventPlan
            {
                Emitted = true,
                StateIndex = state.StateIndex,
                RocketCount = count,
                ProjectileId = projectile,
                BaseSpeed = speed,
                AimSpreadDegrees = WorstCaseAimSpreadDegrees,
                AmmoPickPerformed = true,
                AmmoConsumptionGuaranteed = false,
                MaxAmmoConsumed = 1,
                Safety = SpecialRangedSafetyFlags.RandomizedSpread |
                    SpecialRangedSafetyFlags.RequiresCollisionEvidence |
                    SpecialRangedSafetyFlags.ConservativeOnly
            };
            state.StateIndex = (state.StateIndex + 1) % StateCount;
            state.TicksUntilEvent = EventPeriod;
            state.EventOrdinal++;
            return true;
        }

        public static int RocketsInFullCycle()
        {
            return 10;
        }

        private static bool Valid(in CelebrationHeldState state) =>
            state.Known && state.HeldProjectileId == HeldProjectileId &&
            state.StateIndex >= 0 && state.StateIndex < StateCount &&
            state.TicksUntilEvent >= 0 && state.TicksUntilEvent <= EventPeriod &&
            state.EventOrdinal >= 0;
    }
}
