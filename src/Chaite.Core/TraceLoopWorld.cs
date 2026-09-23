using System;
using System.Collections.Generic;
using System.Globalization;

namespace Chaite.Core
{
    /// <summary>One hostile projectile as the engine reported it at the end of a
    /// tick. Recorded rather than reconstructed, because the live projectile
    /// field is the only place it exists: this project has already been burned
    /// once by an observation that never looked at Game.projectile and then
    /// reported no threat on every row of a fight that projectiles decide.</summary>
    public struct TraceProjectile
    {
        public int Slot;
        public int Type;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Width;
        public int Height;
        public int Damage;
        public int TimeLeft;
        public int ExtraUpdates;
        public float Ai0;
        public float Ai1;
        public float Ai2;
        public float LocalAi0;
        public float LocalAi1;
        public int Direction;
        public float Scale;
    }

    /// <summary>The threat at one tick: the Boss's body and every hostile
    /// projectile, as the engine had them at the end of that tick.</summary>
    public struct TraceThreat
    {
        public int Tick;
        public bool BossPresent;
        public int BossType;
        public Vec2 BossPosition;
        /// <summary>Where the player was on this tick in the recording.
        ///
        /// The Boss chases the player, so its recorded position is only the right
        /// position for the recorded route. Measured: a candidate route put the
        /// player two thousand pixels from where the recording had it, and the
        /// Boss ended up fifty-six hundred pixels away from its recorded place.
        /// The relative geometry is what carries over, so this is kept to rebuild
        /// the Boss's position around wherever the player actually is.</summary>
        public Vec2 PlayerPosition;
        public int BossWidth;
        public int BossHeight;
        public TraceProjectile[] Projectiles;
        /// <summary>The same field one tick earlier, before the native update.
        /// This is the one a hit test has to use: a hit removes the projectile
        /// that caused it, so the post-update list cannot contain it. Null when
        /// the recording predates the field, which is visible rather than
        /// silently equal to an empty list.</summary>
        public TraceProjectile[] ProjectilesBeforeUpdate;
        /// <summary>Hostile projectiles the recording had to drop. Non-zero
        /// means the threat field is incomplete, so the world refuses the tick
        /// instead of scoring a hit it cannot see.</summary>
        public int Omitted;
    }

    public sealed class TraceLoopWorldOptions
    {
        /// <summary>How close to the loop's start the player must return, in
        /// pixels, measured relative to the Boss.</summary>
        public float GoalTolerance = 4f;
        /// <summary>Side of a dominance cell, in pixels. Two partial routes at
        /// the same tick within one cell are treated as interchangeable.</summary>
        public float PositionCellSize = 8f;
        /// <summary>Side of a dominance cell for velocity, in pixels per tick.
        /// Position alone is not enough: two routes in the same place moving in
        /// opposite directions are not the same state.</summary>
        public float VelocityCellSize = 2f;
        /// <summary>The playable span, in pixels. The forward model has no tile
        /// collision, so a predicted position beyond either edge is exactly the
        /// case it would get wrong.</summary>
        public float ArenaLeft = float.NegativeInfinity;
        public float ArenaRight = float.PositiveInfinity;
        /// <summary>Clamp to the arena instead of refusing. The engine pins the
        /// player against a wall and zeroes the velocity that pushed into it;
        /// refusing turns a surface into a dead end, so every route that leans
        /// on a wall becomes unfindable and the search reports an exhaustive
        /// failure that is really a missing wall. Measured on the Fishron
        /// trace: the player sits at exactly x 640 for six hundred ninety of
        /// four thousand three hundred ninety-four ticks with a horizontal
        /// velocity of zero while the controls still press left.</summary>
        public bool ClampToArena;
        /// <summary>Score hits against the pre-update projectile field with
        /// swept boxes rather than the post-update field. On by default, because
        /// the post-update field cannot contain the projectile a hit removed and
        /// therefore cannot detect a hit at all. Turned off only to measure that
        /// difference.</summary>
        public bool SweptProjectiles = true;
        /// <summary>Score hits with the reviewed threat model rather than an
        /// axis-aligned box at the projectile's current position. On by
        /// default: the box was wrong in both directions at once, missing the
        /// long oldPos trail that type 872 collides with and flagging the
        /// eighty-by-eight oriented segment that type 919 collides with.</summary>
        public bool ModeledProjectiles = true;

        /// <summary>Rebuild the Boss's body position around the player's actual
        /// position instead of using the recorded one.
        ///
        /// The recorded position is only valid for the recorded route, because
        /// the Boss chases the player. Measured on one candidate: the player was
        /// two thousand pixels from the recorded player and the Boss was five
        /// thousand six hundred from its recorded place, so a hit test against the
        /// recording was answering a question about a different fight. The Boss
        /// keeps its offset from the player, which is the part that carries over,
        /// and needs no fitted parameter to do it.</summary>
        public bool BossFollowsPlayer;

        /// <summary>When set, the goal is reaching the end of the loop in any
        /// legal state rather than in a particular place.
        ///
        /// The place matters when the target is a reference trajectory's own
        /// boundary. It stops mattering when that boundary was produced by a hit:
        /// measurement on a real fight shows consecutive loops do not share a
        /// start, not even in Boss-relative terms, so a closed condition cannot
        /// compose at all, and the boundaries of the eleven hit loops out of
        /// fifty-one are places only knockback's free upward velocity ever
        /// reached. Searching for those places is searching for something a
        /// clean route cannot do. What the loop decomposition is actually for is
        /// bounding the search, so this mode keeps the bound and drops the place.</summary>
        public bool SurviveToEnd;
    }

    /// <summary>
    /// A loop of a real fight, turned into a world the route enumerator can walk.
    ///
    /// The player is predicted by <see cref="PlayerForwardModel"/>; the threat
    /// is the recorded Boss body and hostile projectiles. The goal is the one
    /// the closed-loop method names: be back where the loop started, measured
    /// relative to the Boss rather than in absolute coordinates, at the tick the
    /// loop ends, with no tick of lost life along the way.
    ///
    /// It refuses rather than guesses. The forward model has no tile collision,
    /// the recording can be truncated, and the loop ends. Each of those is a
    /// tick the model cannot honestly predict, and a wrong prediction here is a
    /// false clean, which is the one failure this whole method exists to avoid.
    /// </summary>
    public sealed class TraceLoopWorld : IRouteWorld
    {
        private readonly IReadOnlyList<TraceThreat> _threats;
        private readonly TraceLoopWorldOptions _options;
        private readonly int _firstTick;
        private readonly int _goalTick;
        private readonly float _startRelativeX;
        private readonly float _startRelativeY;
        private readonly float _toleranceSquared;
        private readonly Dictionary<long, int> _buckets =
            new Dictionary<long, int>();
        /// <summary>Buckets for the follow mode, where the Boss's state is part
        /// of the state and the packed key has no bits left for it.</summary>
        private readonly Dictionary<string, int> _followBuckets =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private int _nextBucket;

        public TraceLoopWorld(IReadOnlyList<TraceThreat> threats,
            float startRelativeX, float startRelativeY,
            TraceLoopWorldOptions options = null)
        {
            if (threats == null || threats.Count == 0)
                throw new ArgumentException("a loop needs at least one tick",
                    "threats");
            _threats = threats;
            _options = options ?? new TraceLoopWorldOptions();
            _firstTick = threats[0].Tick;
            _goalTick = threats[threats.Count - 1].Tick;
            _startRelativeX = startRelativeX;
            _startRelativeY = startRelativeY;
            _toleranceSquared = _options.GoalTolerance * _options.GoalTolerance;
        }

        /// <summary>The tick the loop ends on, which is also the tick a route
        /// has to still be alive at to close it.</summary>
        public int GoalTick { get { return _goalTick; } }

        /// <summary>Hostile projectiles seen anywhere in the loop. Zero on a
        /// trace recorded before the projectile field existed, which is worth
        /// being able to see rather than infer from a suspiciously easy
        /// search.</summary>
        public int RecordedProjectiles
        {
            get
            {
                var total = 0;
                for (var index = 0; index < _threats.Count; index++)
                    if (_threats[index].Projectiles != null)
                        total += _threats[index].Projectiles.Length;
                return total;
            }
        }

        public bool TryStep(in PlayerMotionFrame state,
            in PlayerControlFrame controls, out PlayerMotionFrame next,
            out bool hit, out ForwardModelRefusal refusal)
        {
            next = state;
            hit = false;
            refusal = ForwardModelRefusal.None;
            var index = state.Tick - _firstTick;
            if (index < 0 || index >= _threats.Count)
            {
                refusal = ForwardModelRefusal.PastLoopEnd;
                return false;
            }
            var threat = _threats[index];
            if (threat.Omitted > 0)
            {
                refusal = ForwardModelRefusal.ThreatIncomplete;
                return false;
            }

            ForwardModelRefusal modelRefusal;
            if (!PlayerForwardModel.TryAdvance(in state, in controls, out next,
                    out modelRefusal))
            {
                refusal = modelRefusal;
                return false;
            }

            if (next.Position.X < _options.ArenaLeft ||
                next.Position.X + next.Width > _options.ArenaRight)
            {
                if (!_options.ClampToArena)
                {
                    refusal = ForwardModelRefusal.OutsideArena;
                    return false;
                }
                var clamped = next.Position.X;
                if (clamped < _options.ArenaLeft)
                    clamped = _options.ArenaLeft;
                if (clamped + next.Width > _options.ArenaRight)
                    clamped = _options.ArenaRight - next.Width;
                if (clamped != next.Position.X)
                {
                    next.Position = new Vec2(clamped, next.Position.Y);
                    // Only the axis that hit the wall is zeroed. The engine
                    // keeps falling and jumping while pinned horizontally, so
                    // zeroing both would invent a different failure.
                    next.Velocity = new Vec2(0f, next.Velocity.Y);
                }
            }

            BossChaseState bossState;
            var scored = ThreatFor(index, in next, out bossState);
            next.Boss = bossState;
            hit = Hit(in next, in scored, _options.SweptProjectiles,
                _options.ModeledProjectiles);
            return true;
        }

        /// <summary>The Boss's body for this tick, advanced from its own state
        /// when the world is asked to make it follow.
        ///
        /// The speed is the recording's own displacement between this tick and
        /// the last, so nothing is fitted; the direction is toward the player,
        /// which is what a chasing Boss does. Holding the recorded offset instead
        /// -- the first attempt -- pinned the Boss to the player and stopped the
        /// composition at the first loop after three and a half million refusals.
        ///
        /// The advanced state is written back to the player frame, because it is
        /// per-branch: the world is shared across the search tree and cannot hold
        /// it.</summary>
        private TraceThreat ThreatFor(int index, in PlayerMotionFrame player,
            out BossChaseState boss)
        {
            var threat = _threats[index];
            boss = player.Boss;
            if (!_options.BossFollowsPlayer || !threat.BossPresent) return threat;
            if (!boss.Active)
            {
                boss.Active = true;
                boss.Position = threat.BossPosition;
                boss.Velocity = new Vec2(0f, 0f);
            }
            var previous = index > 0
                ? _threats[index - 1].BossPosition
                : threat.BossPosition;
            var stepX = threat.BossPosition.X - previous.X;
            var stepY = threat.BossPosition.Y - previous.Y;
            var speed = (float)Math.Sqrt(stepX * stepX + stepY * stepY);
            var targetX = player.Position.X + player.Width * .5f;
            var targetY = player.Position.Y + player.Height * .5f;
            var towardX = targetX - boss.Position.X;
            var towardY = targetY - boss.Position.Y;
            var distance = (float)Math.Sqrt(towardX * towardX + towardY * towardY);
            if (distance > 1e-3f && speed > 0f)
            {
                boss.Position = new Vec2(
                    boss.Position.X + towardX / distance * speed,
                    boss.Position.Y + towardY / distance * speed);
                boss.Velocity = new Vec2(
                    towardX / distance * speed, towardY / distance * speed);
            }
            threat.BossPosition = boss.Position;
            return threat;
        }

        /// <summary>
        /// Rebuilds the reviewed model's own snapshot from a recorded
        /// projectile, so the threat is advanced by HostileProjectileMotion
        /// rather than by a box at the projectile's current position.
        ///
        /// The source-Boss provenance is deliberately left unknown: a recorded
        /// trace row carries no same-frame live Boss source, and the
        /// source-bound Fishron families must stay fail-closed without one.
        /// </summary>
        public static bool TryBuildSnapshot(in TraceProjectile projectile,
            out ThreatSnapshot snapshot)
        {
            snapshot = default(ThreatSnapshot);
            var trajectory = HostileProjectileMotion.ForProjectileType(
                projectile.Type);
            snapshot.Kind = ThreatKind.Projectile;
            snapshot.Trajectory = trajectory;
            snapshot.Position = projectile.Position;
            snapshot.Velocity = projectile.Velocity;
            snapshot.Width = projectile.Width;
            snapshot.Height = projectile.Height;
            snapshot.Damage = projectile.Damage;
            snapshot.TimeLeft = projectile.TimeLeft;
            snapshot.Type = projectile.Type;
            snapshot.NativeIdentity = projectile.Slot;
            snapshot.TrajectoryAi0Known = true;
            snapshot.TrajectoryAi0 = projectile.Ai0;
            snapshot.TrajectoryAi1Known = true;
            snapshot.TrajectoryAi1 = projectile.Ai1;
            snapshot.TrajectoryAi2Known = true;
            snapshot.TrajectoryAi2 = projectile.Ai2;
            snapshot.TrajectoryLocalAi0Known = true;
            snapshot.TrajectoryLocalAi0 = projectile.LocalAi0;
            snapshot.TrajectoryLocalAi1Known = true;
            snapshot.TrajectoryLocalAi1 = projectile.LocalAi1;
            snapshot.NativeDirectionKnown = true;
            snapshot.NativeDirection = projectile.Direction;
            return true;
        }

        /// <summary>
        /// The hit test that uses the reviewed threat model: sweep each
        /// pre-update projectile across the tick with
        /// HostileProjectileMotion and test the player against the swept
        /// bounds. Falls back to the axis-aligned sweep when the model refuses
        /// the threat, so an unmodellable projectile is still not silently
        /// ignored.
        /// </summary>
        public static bool OverlapsModeled(in PlayerMotionFrame player,
            in TraceThreat threat)
        {
            var projectiles = threat.ProjectilesBeforeUpdate;
            if (projectiles == null) return false;
            for (var index = 0; index < projectiles.Length; index++)
            {
                var projectile = projectiles[index];
                if (projectile.Width <= 0 || projectile.Height <= 0) continue;
                ThreatSnapshot snapshot;
                ProjectileMotionSweep sweep;
                if (TryBuildSnapshot(in projectile, out snapshot) &&
                    HostileProjectileMotion.TrySweep(in snapshot, 0, 1,
                        out sweep) && sweep.Active)
                {
                    if (Boxes(player.Position.X, player.Position.Y,
                        player.Width, player.Height, sweep.Bounds.X,
                        sweep.Bounds.Y, sweep.Bounds.Width,
                        sweep.Bounds.Height))
                        return true;
                    continue;
                }
                // The model refused, so the threat is advanced by the plain
                // swept box instead. Refusing to score it at all would be
                // worse: an unscored projectile reads as a clean tick.
                if (SweptBoxOverlap(in player, in projectile)) return true;
            }
            return false;
        }

        private static bool SweptBoxOverlap(in PlayerMotionFrame player,
            in TraceProjectile projectile)
        {
            // Native advances a projectile extraUpdates + 1 times per tick for
            // everything that is not a beam, so one tick of travel is the
            // velocity scaled by that, not the velocity.
            var updates = projectile.ExtraUpdates + 1;
            if (updates < 1) updates = 1;
            var stepX = projectile.Velocity.X * updates;
            var stepY = projectile.Velocity.Y * updates;
            var left = projectile.Position.X + Math.Min(0f, stepX);
            var top = projectile.Position.Y + Math.Min(0f, stepY);
            var right = projectile.Position.X + projectile.Width +
                Math.Max(0f, stepX);
            var bottom = projectile.Position.Y + projectile.Height +
                Math.Max(0f, stepY);
            return player.Position.X < right &&
                left < player.Position.X + player.Width &&
                player.Position.Y < bottom &&
                top < player.Position.Y + player.Height;
        }

        public static bool Hit(in PlayerMotionFrame player,
            in TraceThreat threat, bool swept)
        {
            return Hit(in player, in threat, swept, false);
        }

        public static bool Hit(in PlayerMotionFrame player,
            in TraceThreat threat, bool swept, bool modeled)
        {
            if (threat.BossPresent &&
                Boxes(player.Position.X, player.Position.Y, player.Width,
                    player.Height, threat.BossPosition.X, threat.BossPosition.Y,
                    threat.BossWidth, threat.BossHeight))
                return true;
            if (modeled && threat.ProjectilesBeforeUpdate != null)
                return OverlapsModeled(in player, in threat);
            if (swept && threat.ProjectilesBeforeUpdate != null)
                return OverlapsSwept(in player, in threat);
            return OverlapsProjectiles(in player, in threat);
        }

        public bool IsGoal(in PlayerMotionFrame state)
        {
            // Both halves matter. The tick alone would accept any position at
            // the end of the loop; the position alone would accept arriving back
            // early, which is not the loop closing, it is the loop being cut
            // short. The pitfall this guards against is the one already recorded:
            // a bucket that collapsed time marked the goal reached a tick early.
            if (state.Tick < _goalTick) return false;
            if (_options.SurviveToEnd) return true;
            var boss = _threats[_threats.Count - 1].BossPosition;
            var dx = state.Position.X - boss.X - _startRelativeX;
            var dy = state.Position.Y - boss.Y - _startRelativeY;
            return dx * dx + dy * dy <= _toleranceSquared;
        }

        public int Bucket(in PlayerMotionFrame state)
        {
            // Time is a dimension of the bucket on purpose. The Boss's script is
            // indexed by tick, so two states at different ticks face different
            // threats and are not interchangeable; collapsing this dimension is
            // one of the two ways this enumerator has already deleted its own
            // answer. Velocity is in for the same reason: same place, opposite
            // directions, not the same state.
            var key = Key(in state);
            int bucket;
            if (_options.BossFollowsPlayer)
            {
                // The Boss's own state is part of the state now, so it has to be
                // part of the bucket. Leaving it out would merge two branches
                // whose player is in the same cell but whose Boss is not, which
                // is exactly the collapse this enumerator has already deleted its
                // own answer with twice. The packed key has no room left for it,
                // and the string form is only paid for in this mode.
                var followKey = string.Format(CultureInfo.InvariantCulture,
                    "{0}|{1:F0}|{2:F0}",
                    key,
                    state.Boss.Position.X / _options.PositionCellSize,
                    state.Boss.Position.Y / _options.PositionCellSize);
                if (_followBuckets.TryGetValue(followKey, out bucket)) return bucket;
                bucket = _nextBucket++;
                _followBuckets[followKey] = bucket;
                return bucket;
            }
            if (_buckets.TryGetValue(key, out bucket)) return bucket;
            // A canonical identifier per distinct cell rather than a packed
            // hash, so two different states can never share a bucket by
            // collision and be pruned as if they were one.
            bucket = _nextBucket++;
            _buckets[key] = bucket;
            return bucket;
        }

        public float DistanceToGoal(in PlayerMotionFrame state)
        {
            // Surviving to the end of the loop is unavoidable, so the remaining
            // ticks is a lower bound on the cost and the only term that orders
            // the open set honestly. The gap to the goal is a tie-break scaled
            // far below one tick, so it can never claim a state is closer than
            // one that genuinely has fewer ticks left.
            var remaining = _goalTick - state.Tick;
            if (remaining < 0) remaining = 0;
            var boss = BossAt(state.Tick);
            var dx = state.Position.X - boss.X - _startRelativeX;
            var dy = state.Position.Y - boss.Y - _startRelativeY;
            return remaining +
                0.001f * (float)Math.Sqrt((double)(dx * dx + dy * dy));
        }

        public Vec2 BossAt(int tick)
        {
            var index = tick - _firstTick;
            if (index < 0) index = 0;
            if (index >= _threats.Count) index = _threats.Count - 1;
            return _threats[index].BossPosition;
        }

        private long Key(in PlayerMotionFrame state)
        {
            var index = state.Tick - _firstTick;
            var cellX = (long)Math.Floor(
                state.Position.X / _options.PositionCellSize);
            var cellY = (long)Math.Floor(
                state.Position.Y / _options.PositionCellSize);
            var cellVX = (long)Math.Floor(
                state.Velocity.X / _options.VelocityCellSize);
            var cellVY = (long)Math.Floor(
                state.Velocity.Y / _options.VelocityCellSize);
            // Packed exactly rather than hashed: the ranges are bounded by the
            // loop length, the arena and the reachable speed, so distinct cells
            // stay distinct and no two states can collide into one bucket.
            return ((((index & 0x3FFL) * 65536L + (cellX & 0xFFFFL)) * 8192L +
                (cellY & 0x1FFFL)) * 256L + (cellVX & 0xFFL)) * 256L +
                (cellVY & 0xFFL);
        }

        /// <summary>Axis-aligned overlap between the player's box and the Boss
        /// or a hostile projectile.
        ///
        /// This is not native collision. Several projectiles use a rotated or
        /// non-rectangular hitbox, and the trace records the axis-aligned
        /// dimensions only. It is calibrated against the recorded fight rather
        /// than trusted: the ticks it flags have to be the ticks where life
        /// actually fell, and a flag on a tick where life did not fall would
        /// make the search reject routes the engine accepts.</summary>
        /// <summary>
        /// Overlap against the pre-update projectile field, with each box swept
        /// along the path it travels during the tick.
        ///
        /// This is the test a hit needs. A dense row is sampled after the native
        /// update, and native kills a projectile on contact, so the projectile
        /// that caused a hit is gone from the post-update list; calibrating that
        /// list against a real fight found none of eleven hits. The pre-update
        /// list still has it, and sweeping the box by one tick of velocity
        /// covers the whole path rather than only its starting point.
        ///
        /// Still axis-aligned, and still not native collision: several
        /// projectiles use a rotated or non-rectangular hitbox and the trace
        /// records the axis-aligned dimensions only. Sweeping is deliberately
        /// conservative, because a missed hit becomes a false clean.
        /// </summary>
        public static bool OverlapsSwept(in PlayerMotionFrame player,
            in TraceThreat threat)
        {
            var projectiles = threat.ProjectilesBeforeUpdate;
            if (projectiles == null) return false;
            for (var index = 0; index < projectiles.Length; index++)
            {
                var projectile = projectiles[index];
                if (projectile.Width <= 0 || projectile.Height <= 0) continue;
                var left = projectile.Position.X +
                    Math.Min(0f, projectile.Velocity.X);
                var top = projectile.Position.Y +
                    Math.Min(0f, projectile.Velocity.Y);
                var right = projectile.Position.X + projectile.Width +
                    Math.Max(0f, projectile.Velocity.X);
                var bottom = projectile.Position.Y + projectile.Height +
                    Math.Max(0f, projectile.Velocity.Y);
                if (player.Position.X < right &&
                    left < player.Position.X + player.Width &&
                    player.Position.Y < bottom &&
                    top < player.Position.Y + player.Height)
                    return true;
            }
            return false;
        }

        public static bool Overlaps(in PlayerMotionFrame player,
            in TraceThreat threat)
        {
            if (threat.BossPresent &&
                Boxes(player.Position.X, player.Position.Y, player.Width,
                    player.Height, threat.BossPosition.X, threat.BossPosition.Y,
                    threat.BossWidth, threat.BossHeight))
                return true;
            return OverlapsProjectiles(in player, in threat);
        }

        private static bool OverlapsProjectiles(in PlayerMotionFrame player,
            in TraceThreat threat)
        {
            var projectiles = threat.Projectiles;
            if (projectiles == null) return false;
            for (var index = 0; index < projectiles.Length; index++)
                if (Boxes(player.Position.X, player.Position.Y, player.Width,
                    player.Height, projectiles[index].Position.X,
                    projectiles[index].Position.Y, projectiles[index].Width,
                    projectiles[index].Height))
                    return true;
            return false;
        }

        private static bool Boxes(float leftX, float leftY, int leftWidth,
            int leftHeight, float rightX, float rightY, int rightWidth,
            int rightHeight)
        {
            return leftX < rightX + rightWidth && rightX < leftX + leftWidth &&
                leftY < rightY + rightHeight && rightY < leftY + leftHeight;
        }

        /// <summary>The same test with float extents, which is what the
        /// reviewed model reports: its swept bounds are not integer-aligned.</summary>
        private static bool Boxes(float leftX, float leftY, int leftWidth,
            int leftHeight, float rightX, float rightY, float rightWidth,
            float rightHeight)
        {
            return leftX < rightX + rightWidth && rightX < leftX + leftWidth &&
                leftY < rightY + rightHeight && rightY < leftY + leftHeight;
        }
    }
}
