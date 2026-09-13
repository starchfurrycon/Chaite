using System;

namespace Chaite.Core
{
    public enum ElectrospherePlacementPhase
    {
        Unsupported = 0,
        Traveling442 = 1,
        Area443 = 2,
        Retired = 3,
        Blocked = 4
    }

    public enum ElectrospherePlacementEvent
    {
        None = 0,
        Traveling = 1,
        Detonated = 2,
        AreaPulse = 3,
        Retired = 4,
        Blocked = 5
    }

    public struct ElectrospherePlacementRequest
    {
        public bool Known;
        public Vec2 Origin;
        public Vec2 Velocity;
        public int TargetTileX;
        public int TargetTileY;
        public int Damage;
        public int TimeLeft;
        public int Owner;
    }

    /// <summary>
    /// Collision observations for one native projectile update.  TargetReached
    /// is optional evidence; the solver also checks the recorded tile center.
    /// Unknown collision state fails closed, because 442 must never be predicted
    /// through a wall or an unobserved NPC hit.
    /// </summary>
    public struct ElectrosphereCollision
    {
        public bool Known;
        public bool TargetReached;
        public bool TileHit;
        public bool NpcHit;
        public bool ExistingAreaIntersects;
    }

    public struct ElectrospherePlacementState
    {
        public bool Known;
        public ElectrospherePlacementPhase Phase;
        public Vec2 Origin;
        public Vec2 Position;
        public Vec2 Velocity;
        public int TargetTileX;
        public int TargetTileY;
        public int Damage;
        public int Owner;
        public int TravelUpdates;
        public int AreaUpdates;
        public int TimeLeft;
        public Vec2 AreaCenter;
        public int AreaAge;
        public bool RetireOldArea;

        public bool IsTraveling => Known && Phase == ElectrospherePlacementPhase.Traveling442;
        public bool IsAreaActive => Known && Phase == ElectrospherePlacementPhase.Area443 &&
            AreaUpdates > 0;
        public bool IsTerminal => Phase == ElectrospherePlacementPhase.Retired ||
            Phase == ElectrospherePlacementPhase.Blocked;
    }

    public struct ElectrospherePlacementStep
    {
        public ElectrospherePlacementEvent Event;
        public ElectrospherePlacementPhase Phase;
        public Vec2 Position;
        public Vec2 AreaCenter;
        public float AreaWidth;
        public float AreaHeight;
        public int DirectDamage;
        public int AreaDamage;
        public float Knockback;
        public bool RetireOldArea;
        public bool Safe;
    }

    /// <summary>
    /// Deterministic, allocation-free controller for the vanilla Electrosphere
    /// Launcher (2796) travel projectile 442 and its sustained area projectile
    /// 443.  The type intentionally has no Terraria references; an adapter feeds
    /// the current collision result and can therefore be hash/version locked at
    /// the boundary.
    /// </summary>
    public static class ElectrospherePlacement
    {
        public const int LauncherItemId = 2796;
        public const int TravelProjectileId = 442;
        public const int AreaProjectileId = 443;
        public const int NativeTravelLifetime = 600;
        public const int NativeAreaLifetime = 300;
        public const int VisibleTravelAgeLimit = 120;
        public const float TileSize = 16f;
        public const float TileCenterOffset = 8f;
        public const float TargetRadius = 16f;
        public const float AreaMinimumSize = 62f;
        public const float AreaMaximumSize = 80f;
        public const float AreaHalfExtent = 40f;
        public const int AreaPulsePeriod = 30;

        public static bool TryQuantizeTarget(Vec2 worldPosition,
            out int tileX, out int tileY)
        {
            tileX = tileY = 0;
            if (!Finite(worldPosition)) return false;
            var x = Math.Floor(worldPosition.X / TileSize);
            var y = Math.Floor(worldPosition.Y / TileSize);
            if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue)
                return false;
            tileX = (int)x;
            tileY = (int)y;
            return true;
        }

        public static Vec2 TileCenter(int tileX, int tileY) =>
            new Vec2(tileX * TileSize + TileCenterOffset,
                tileY * TileSize + TileCenterOffset);

        public static bool IsWithinTargetRadius(Vec2 position, int tileX, int tileY)
        {
            return Vec2.DistanceSquared(position, TileCenter(tileX, tileY)) <=
                TargetRadius * TargetRadius;
        }

        public static bool TryCreate(in ElectrospherePlacementRequest request,
            out ElectrospherePlacementState state, out string reason)
        {
            state = default(ElectrospherePlacementState);
            reason = null;
            if (!request.Known || !Finite(request.Origin) || !Finite(request.Velocity) ||
                request.Velocity.LengthSquared < .0001f || request.Damage <= 0 ||
                request.TimeLeft <= 0 || request.TimeLeft > NativeTravelLifetime ||
                request.TargetTileX < -1000000 || request.TargetTileX > 1000000 ||
                request.TargetTileY < -1000000 || request.TargetTileY > 1000000 ||
                request.Owner < 0)
            {
                reason = "invalid Electrosphere 442 native state";
                return false;
            }
            state = new ElectrospherePlacementState
            {
                Known = true,
                Phase = ElectrospherePlacementPhase.Traveling442,
                Origin = request.Origin,
                Position = request.Origin,
                Velocity = request.Velocity,
                TargetTileX = request.TargetTileX,
                TargetTileY = request.TargetTileY,
                Damage = request.Damage,
                Owner = request.Owner,
                TimeLeft = request.TimeLeft,
                AreaUpdates = 0,
                AreaAge = 0
            };
            return true;
        }

        /// <summary>
        /// Advances one game update. During 442 travel the target tile is fixed at
        /// fire time; mouse movement cannot retarget the state.  NPC collision is
        /// an impact event but direct 442 damage remains zero, matching native
        /// Damage_CanDealDamage/Kill ordering.  During 443 area time no tile
        /// collision is needed, and the conservative 62..80 px pulse envelope is
        /// returned for the caller's coverage scorer.
        /// </summary>
        public static bool TryAdvance(ref ElectrospherePlacementState state,
            in ElectrosphereCollision collision, out ElectrospherePlacementStep step)
        {
            step = default(ElectrospherePlacementStep);
            if (!Valid(state) || !collision.Known)
            {
                state.Phase = ElectrospherePlacementPhase.Blocked;
                step.Event = ElectrospherePlacementEvent.Blocked;
                step.Phase = ElectrospherePlacementPhase.Blocked;
                step.Position = state.Position;
                step.Safe = false;
                return false;
            }

            if (state.Phase == ElectrospherePlacementPhase.Traveling442)
                return AdvanceTravel(ref state, in collision, out step);
            if (state.Phase == ElectrospherePlacementPhase.Area443)
                return AdvanceArea(ref state, out step);

            step.Event = ElectrospherePlacementEvent.Retired;
            step.Phase = state.Phase;
            step.Position = state.Position;
            step.AreaCenter = state.AreaCenter;
            step.Safe = true;
            return true;
        }

        public static bool TryRetire(ref ElectrospherePlacementState state)
        {
            if (!Valid(state) || state.Phase == ElectrospherePlacementPhase.Blocked)
                return false;
            state.Phase = ElectrospherePlacementPhase.Retired;
            state.TimeLeft = 0;
            state.AreaUpdates = 0;
            return true;
        }

        /// <summary>
        /// Native 443 rectangles overlap when their 80 px envelopes intersect.
        /// The envelope is intentionally used here rather than a center-distance
        /// shortcut, because vanilla retires an old area on rectangle overlap.
        /// </summary>
        public static bool AreasOverlap(Vec2 firstCenter, Vec2 secondCenter,
            float firstSize = AreaMaximumSize, float secondSize = AreaMaximumSize)
        {
            if (!Finite(firstCenter) || !Finite(secondCenter) ||
                firstSize <= 0f || secondSize <= 0f) return false;
            var half = (firstSize + secondSize) * .5f;
            return Math.Abs(firstCenter.X - secondCenter.X) <= half &&
                Math.Abs(firstCenter.Y - secondCenter.Y) <= half;
        }

        public static RectF AreaBounds(Vec2 center, float size = AreaMaximumSize)
        {
            var half = Math.Max(0f, size) * .5f;
            return new RectF(center.X - half, center.Y - half, half * 2f, half * 2f);
        }

        private static bool AdvanceTravel(ref ElectrospherePlacementState state,
            in ElectrosphereCollision collision, out ElectrospherePlacementStep step)
        {
            var reached = collision.TargetReached || IsWithinTargetRadius(
                state.Position, state.TargetTileX, state.TargetTileY);
            if (reached || collision.TileHit || collision.NpcHit)
            {
                state.AreaCenter = state.Position;
                state.AreaAge = 0;
                state.AreaUpdates = NativeAreaLifetime;
                state.TimeLeft = NativeAreaLifetime;
                state.Phase = ElectrospherePlacementPhase.Area443;
                state.RetireOldArea = collision.ExistingAreaIntersects;
                step = new ElectrospherePlacementStep
                {
                    Event = ElectrospherePlacementEvent.Detonated,
                    Phase = state.Phase,
                    Position = state.Position,
                    AreaCenter = state.AreaCenter,
                    AreaWidth = AreaMaximumSize,
                    AreaHeight = AreaMaximumSize,
                    // Projectile 442 is killed before native damage resolution.
                    DirectDamage = 0,
                    AreaDamage = state.Damage,
                    Knockback = 0f,
                    RetireOldArea = state.RetireOldArea,
                    Safe = true
                };
                return true;
            }

            var next = state;
            next.Position += next.Velocity;
            next.TravelUpdates++;
            next.TimeLeft--;
            // localAI[1] reaches 120 on the visible branch before the nominal
            // 600-tick fallback.  Retire at the observed boundary, conservatively.
            if (next.TimeLeft <= 0 || next.TravelUpdates >= VisibleTravelAgeLimit)
            {
                next.Phase = ElectrospherePlacementPhase.Retired;
                next.TimeLeft = 0;
                state = next;
                step = new ElectrospherePlacementStep
                {
                    Event = ElectrospherePlacementEvent.Retired,
                    Phase = next.Phase,
                    Position = next.Position,
                    Safe = true
                };
                return true;
            }
            state = next;
            step = new ElectrospherePlacementStep
            {
                Event = ElectrospherePlacementEvent.Traveling,
                Phase = next.Phase,
                Position = next.Position,
                AreaCenter = next.AreaCenter,
                Safe = true
            };
            return true;
        }

        private static bool AdvanceArea(ref ElectrospherePlacementState state,
            out ElectrospherePlacementStep step)
        {
            if (state.AreaUpdates <= 0)
            {
                state.Phase = ElectrospherePlacementPhase.Retired;
                state.TimeLeft = 0;
                step = new ElectrospherePlacementStep
                {
                    Event = ElectrospherePlacementEvent.Retired,
                    Phase = state.Phase,
                    Position = state.Position,
                    AreaCenter = state.AreaCenter,
                    Safe = true
                };
                return true;
            }
            var next = state;
            next.AreaAge++;
            next.AreaUpdates--;
            next.TimeLeft = next.AreaUpdates;
            var size = PulseSize(next.AreaAge);
            if (next.AreaUpdates <= 0) next.Phase = ElectrospherePlacementPhase.Retired;
            state = next;
            step = new ElectrospherePlacementStep
            {
                Event = next.Phase == ElectrospherePlacementPhase.Retired
                    ? ElectrospherePlacementEvent.Retired
                    : ElectrospherePlacementEvent.AreaPulse,
                Phase = next.Phase,
                Position = next.Position,
                AreaCenter = next.AreaCenter,
                AreaWidth = size,
                AreaHeight = size,
                DirectDamage = 0,
                AreaDamage = next.Damage,
                Knockback = 0f,
                RetireOldArea = next.RetireOldArea,
                Safe = true
            };
            return true;
        }

        private static float PulseSize(int age)
        {
            var phase = age % AreaPulsePeriod;
            var envelope = phase <= AreaPulsePeriod / 2
                ? phase / (float)(AreaPulsePeriod / 2)
                : (AreaPulsePeriod - phase) / (float)(AreaPulsePeriod / 2);
            return AreaMinimumSize + (AreaMaximumSize - AreaMinimumSize) * envelope;
        }

        private static bool Valid(in ElectrospherePlacementState state)
        {
            return state.Known && state.Phase != ElectrospherePlacementPhase.Unsupported &&
                state.Phase != ElectrospherePlacementPhase.Blocked &&
                state.TargetTileX >= -1000000 && state.TargetTileX <= 1000000 &&
                state.TargetTileY >= -1000000 && state.TargetTileY <= 1000000 &&
                state.Damage > 0 && state.TimeLeft >= 0 &&
                state.TimeLeft <= NativeTravelLifetime && state.TravelUpdates >= 0 &&
                state.AreaUpdates >= 0 && state.AreaUpdates <= NativeAreaLifetime &&
                Finite(state.Position) && Finite(state.Velocity) &&
                Finite(state.AreaCenter);
        }

        private static bool Finite(Vec2 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }
}
