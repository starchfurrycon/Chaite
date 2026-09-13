using Chaite.Core;
using System;
using System.Collections.Generic;

namespace Chaite.Plugin
{
    /// <summary>
    /// Ordinary enemies are distance-bounded, but an active Boss root remains
    /// part of the control state even while its body or a charge carries the
    /// root outside the local targeting radius. Native slot identity is always
    /// required before the object can later be indexed for a LOS query.
    /// </summary>
    internal static class NativeTargetCapturePolicy
    {
        public static bool ShouldInclude(float distanceSquared, float maximumDistanceSquared,
            bool boss, bool validEntitySlot)
        {
            return validEntitySlot && (boss || distanceSquared <= maximumDistanceSquared);
        }
    }

    /// <summary>
    /// A per-NPC-slot observation for vanilla Destroyer parts. Body and tail
    /// segments clear their native velocity and move by assigning position, so
    /// their useful motion is the consecutive-frame position delta.
    /// </summary>
    internal struct DestroyerMotionObservation
    {
        public bool TrustedMotion;
        public Vec2 Velocity;
        public Vec2 SweepPosition;
        public int SweepWidth;
        public int SweepHeight;
    }

    /// <summary>
    /// Fixed-capacity history for Terraria's 200 native NPC slots. It neither
    /// allocates nor grows while snapshots are read.
    /// </summary>
    internal sealed class DestroyerMotionHistory
    {
        internal const int Capacity = 200;
        // Native head speed is around 16 px/tick and adjacent parts are about
        // 44 px apart. This deliberately generous limit rejects slot reuse and
        // spawn/despawn snaps without treating ordinary chain correction as a
        // world-spanning contact sweep.
        internal const float MaximumTrustedStepPixels = 192f;

        private struct Entry
        {
            public bool Active;
            public int EntityKey;
            public int Type;
            public int RootKey;
            public int PredecessorKey;
            public int Frame;
            public Vec2 Position;
            public int Width;
            public int Height;
            public int Life;
            public int LifeMax;
            public DestroyerMotionObservation Observation;
        }

        private readonly Entry[] _entries = new Entry[Capacity];

        public void Invalidate(int slot)
        {
            if (slot < 0 || slot >= _entries.Length) return;
            _entries[slot] = default(Entry);
        }

        public void Clear() => Array.Clear(_entries, 0, _entries.Length);

        public DestroyerMotionObservation Observe(int slot, int entityKey, int type, int rootKey,
            int predecessorKey, bool chainConnected, Vec2 position, int width, int height,
            Vec2 nativeVelocity, int frame, int life, int lifeMax)
        {
            var fallback = CurrentOnly(type, position, width, height, nativeVelocity);
            if (slot < 0 || slot >= _entries.Length)
                return fallback;

            // whoAmI is a slot number rather than a generation id. Reject a
            // malformed current node here as well as in the facade so it cannot
            // inherit the slot's prior observation.
            if (entityKey != slot || !IsDestroyerPart(type) || frame <= 0 || width <= 0 || height <= 0 ||
                life <= 0 || lifeMax <= 0 || life > lifeMax || !Finite(position))
            {
                Invalidate(slot);
                return fallback;
            }

            if (!chainConnected)
            {
                Invalidate(slot);
                return fallback;
            }

            ref var entry = ref _entries[slot];
            // Vanilla Destroyer parts do not heal or resize while alive. These
            // public, allocation-free observations make a life reset or variant
            // replacement fail closed even though native whoAmI itself is reused.
            // An equal-state replacement which occurs wholly between two reads is
            // fundamentally indistinguishable without a native generation id.
            var sameIdentity = entry.Active && entry.EntityKey == entityKey && entry.Type == type &&
                entry.RootKey == rootKey && entry.PredecessorKey == predecessorKey &&
                entry.Width == width && entry.Height == height && entry.LifeMax == lifeMax && life <= entry.Life;

            // BuildCombatSnapshot can be requested more than once during one
            // input frame. Return the first observation verbatim so a duplicate
            // read cannot replace a real delta with zero.
            if (sameIdentity && entry.Frame == frame && entry.Life == life && Same(entry.Position, position))
                return entry.Observation;

            var observation = fallback;
            if (sameIdentity && frame == entry.Frame + 1)
            {
                var delta = position - entry.Position;
                if (Finite(delta) && delta.LengthSquared <= MaximumTrustedStepPixels * MaximumTrustedStepPixels)
                {
                    var left = Math.Min(entry.Position.X, position.X);
                    var top = Math.Min(entry.Position.Y, position.Y);
                    var right = Math.Max(entry.Position.X + entry.Width, position.X + width);
                    var bottom = Math.Max(entry.Position.Y + entry.Height, position.Y + height);
                    observation.TrustedMotion = true;
                    // The head exposes a meaningful native velocity. Body/tail
                    // do not, and therefore use their observed position delta.
                    observation.Velocity = type == 134 && TrustedStep(nativeVelocity) ? nativeVelocity : delta;
                    observation.SweepPosition = new Vec2(left, top);
                    observation.SweepWidth = Math.Max(1, (int)Math.Ceiling(right - left));
                    observation.SweepHeight = Math.Max(1, (int)Math.Ceiling(bottom - top));
                }
            }

            entry.Active = true;
            entry.EntityKey = entityKey;
            entry.Type = type;
            entry.RootKey = rootKey;
            entry.PredecessorKey = predecessorKey;
            entry.Frame = frame;
            entry.Position = position;
            entry.Width = width;
            entry.Height = height;
            entry.Life = life;
            entry.LifeMax = lifeMax;
            entry.Observation = observation;
            return observation;
        }

        private static DestroyerMotionObservation CurrentOnly(int type, Vec2 position, int width, int height,
            Vec2 nativeVelocity)
        {
            return new DestroyerMotionObservation
            {
                TrustedMotion = false,
                Velocity = type == 134 && TrustedStep(nativeVelocity) ? nativeVelocity : new Vec2(0f, 0f),
                SweepPosition = position,
                SweepWidth = Math.Max(1, width),
                SweepHeight = Math.Max(1, height)
            };
        }

        private static bool IsDestroyerPart(int type) => type >= 134 && type <= 136;
        private static bool Same(Vec2 first, Vec2 second) => first.X == second.X && first.Y == second.Y;
        private static bool TrustedStep(Vec2 value) => Finite(value) &&
            value.LengthSquared <= MaximumTrustedStepPixels * MaximumTrustedStepPixels;
        private static bool Finite(Vec2 value) => !float.IsNaN(value.X) && !float.IsNaN(value.Y) &&
            !float.IsInfinity(value.X) && !float.IsInfinity(value.Y);
    }

    internal static class DestroyerLinkIdentity
    {
        public static bool TryReadSlot(float value, int slotCount, out int slot)
        {
            slot = -1;
            if (slotCount <= 0 || float.IsNaN(value) || float.IsInfinity(value) ||
                value < 0f || value >= slotCount)
                return false;
            var candidate = (int)value;
            if (candidate != value) return false;
            slot = candidate;
            return true;
        }
    }

    /// <summary>
    /// Reads the final branch remembered by vanilla's Destroyer head. The value
    /// is only meaningful after this exact head identity has survived a trusted
    /// consecutive observation; localAI[1] is merely the material-scan result.
    /// </summary>
    internal static class DestroyerBranchObservation
    {
        public static bool TryReadHeadBranch(int type, bool trustedIdentityHistory, float[] localAi,
            out bool usesWormMovement)
        {
            usesWormMovement = false;
            if (type != 134 || !trustedIdentityHistory || localAi == null || localAi.Length == 0)
                return false;
            var branch = localAi[0];
            if (float.IsNaN(branch) || float.IsInfinity(branch) || branch != 0f && branch != 1f)
                return false;
            usesWormMovement = branch == 1f;
            return true;
        }
    }

    internal struct TargetSightQueryState
    {
        public int NextGeneralSlot;
        public int NextProbeSlot;
        public bool FairQueryPending;
        public bool PressureQueryPending;

        public void BeginFrame()
        {
            FairQueryPending = true;
            PressureQueryPending = true;
        }

        public void Clear()
        {
            this = default(TargetSightQueryState);
            BeginFrame();
        }
    }

    /// <summary>Allocation-free selection for the facade's bounded LOS budget.</summary>
    internal static class TargetSightQuerySelector
    {
        private const float ProbePressureRange = 900f;
        private const float CloseProbeRange = 320f;

        public static int Select(IList<TargetSnapshot> targets, Vec2 playerCenter, int remainingBudget,
            ref TargetSightQueryState state)
        {
            if (targets == null || remainingBudget <= 0) return -1;
            var nearest = -1;
            var nearestDistance = float.MaxValue;
            var fair = -1;
            var fairDistance = int.MaxValue;
            var nearestProbe = -1;
            var nearestProbeDistance = float.MaxValue;
            var fairProbe = -1;
            var fairProbeDistance = int.MaxValue;
            var pressureProbeCount = 0;
            var closeProbe = false;

            for (var i = 0; i < targets.Count; i++)
            {
                var candidate = targets[i];
                var distance = Vec2.DistanceSquared(candidate.Center, playerCenter);
                var eligible = !candidate.Invulnerable && candidate.Chaseable;
                if (candidate.Type == 139 && eligible &&
                    distance < ProbePressureRange * ProbePressureRange)
                {
                    pressureProbeCount++;
                    if (distance < CloseProbeRange * CloseProbeRange) closeProbe = true;
                }
                if (candidate.LineOfSightKnown || !eligible) continue;
                if (distance < nearestDistance)
                {
                    nearest = i;
                    nearestDistance = distance;
                }
                var cursorDistance = candidate.Type == 139 ? int.MaxValue :
                    CircularDistance(candidate.Key, state.NextGeneralSlot);
                if (cursorDistance < fairDistance)
                {
                    fair = i;
                    fairDistance = cursorDistance;
                }
                if (candidate.Type == 139)
                {
                    if (distance < nearestProbeDistance)
                    {
                        nearestProbe = i;
                        nearestProbeDistance = distance;
                    }
                    var probeCursorDistance = CircularDistance(candidate.Key, state.NextProbeSlot);
                    if (probeCursorDistance < fairProbeDistance)
                    {
                        fairProbe = i;
                        fairProbeDistance = probeCursorDistance;
                    }
                }
            }

            var probePressure = closeProbe || pressureProbeCount > 3;
            var probe = fairProbe >= 0 ? fairProbe : nearestProbe;
            // Pressure owns the first query, but never all three: the next query
            // remains a cross-frame fair walk through all native slots.
            if (state.PressureQueryPending)
            {
                state.PressureQueryPending = false;
                if (probePressure && probe >= 0)
                    return SelectProbe(targets, probe, ref state);
            }

            // Do not spend the final available ray on general fairness when an
            // unknown Probe needs the reserved last slot.
            if (state.FairQueryPending && fair >= 0 && !(remainingBudget == 1 && probe >= 0))
            {
                state.FairQueryPending = false;
                Advance(ref state.NextGeneralSlot, targets[fair].Key);
                if (targets[fair].Type == 139) Advance(ref state.NextProbeSlot, targets[fair].Key);
                return fair;
            }

            if (remainingBudget == 1 && probe >= 0)
                return SelectProbe(targets, probe, ref state);
            return nearest;
        }

        // Compatibility helper for isolated one-shot tests and adapters. The
        // production facade uses the stateful overload above.
        public static int Select(IList<TargetSnapshot> targets, Vec2 playerCenter, int remainingBudget)
        {
            var state = default(TargetSightQueryState);
            state.BeginFrame();
            return Select(targets, playerCenter, remainingBudget, ref state);
        }

        private static int SelectProbe(IList<TargetSnapshot> targets, int selected,
            ref TargetSightQueryState state)
        {
            Advance(ref state.NextProbeSlot, targets[selected].Key);
            return selected;
        }

        private static int CircularDistance(int slot, int cursor)
        {
            if (slot < 0 || slot >= DestroyerMotionHistory.Capacity) return int.MaxValue;
            if (cursor < 0 || cursor >= DestroyerMotionHistory.Capacity) cursor = 0;
            return slot >= cursor ? slot - cursor : slot + DestroyerMotionHistory.Capacity - cursor;
        }

        private static void Advance(ref int cursor, int selectedSlot)
        {
            if (selectedSlot < 0 || selectedSlot >= DestroyerMotionHistory.Capacity) return;
            cursor = selectedSlot + 1;
            if (cursor >= DestroyerMotionHistory.Capacity) cursor = 0;
        }
    }
}
