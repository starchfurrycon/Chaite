using System;

namespace Chaite.Core
{
    /// <summary>
    /// Allocation-free sample of a native hostile-projectile trajectory.  A
    /// non-zero uncertainty is a proven positional envelope, not an accuracy
    /// estimate.  Bounds includes that envelope and the projectile body.
    /// </summary>
    public struct ProjectileMotionSample
    {
        public bool Active;
        public Vec2 Position;
        public Vec2 Velocity;
        public float UncertaintyX;
        public float UncertaintyY;
        public RectF Bounds;
    }

    public struct ProjectileMotionSweep
    {
        public bool Active;
        public RectF Bounds;
    }

    /// <summary>
    /// Incremental AI_171 state for a hostile type-873 rainbow streak whose
    /// exact target player is known. The planner keeps one state per relevant
    /// streak while rolling out a movement candidate, so each native update
    /// can use that candidate's own future player center without allocating.
    /// </summary>
    public struct TargetedProjectileMotionState
    {
        public bool Valid;
        public bool Active;
        public Vec2 Position;
        public Vec2 Velocity;
        public int TimeLeft;
        internal int Type;
        internal int Width;
        internal int Height;
        internal int NativeIdentity;
        // Fishron bubble (type 385) can share the candidate-coupled path when
        // its native target and ai/localAI values are all available.  These
        // fields remain zero for the Empress 873 state and are never treated
        // as known unless the corresponding factory validates them.
        internal float Ai1;
        internal float Ai2;
        internal float LocalAi0;
        internal int Direction;
    }

    /// <summary>
    /// Candidate-coupled state for NPC 636's native AI_120 state 8/9 dash.
    /// Position is the NPC top-left, while TargetCenter is supplied afresh for
    /// every rollout tick. The state is a value type so the planner can reuse a
    /// preallocated array on its hot path.
    /// </summary>
    public struct EmpressDashMotionState
    {
        public bool Valid;
        public bool Active;
        public Vec2 Position;
        public Vec2 Velocity;
        public int Width;
        public int Height;
        public int State;
        public int Timer;
        public bool Phase2;
        public bool Expert;
        public bool Enraged;
        internal int NativeIdentity;
    }

    /// <summary>
    /// Terraria 1.4.5.8 hostile projectile motion used by the bounded threat
    /// planner.  The reviewed branches are Projectile.AI_171 and the type
    /// 920/921 branches of Projectile.AI_001.  No heap state is retained or
    /// allocated while sampling.
    /// </summary>
    public static class HostileProjectileMotion
    {
        private const float RainbowSlowdown = .98f;
        private const float RainbowHomingSpeed = 30f;
        private const float FallingGravity = .15f;
        private const float MaximumFallingSpeed = 16f;
        private const float MinimumLiquidTravel = .25f;
        // Main.windSpeedTarget is clamped to .8; rain raises the live target by
        // at most 1 + 5/9, and Main.windPhysicsStrength is .1 in 1.4.5.8.
        // Round .124444... upward for an environment-independent envelope.
        private const float MaximumWindAcceleration = .125f;
        // Projectile.Update computes a [-16,16] clamp after wind but discards
        // its return value. A value just below 16 can therefore move once at
        // up to this speed before AI_001 observes it again.
        private const float MaximumWindSpeed =
            MaximumFallingSpeed + MaximumWindAcceleration;
        private const float Pi = 3.14159265358979323846f;
        private const float FishronBubbleBaseSpeed = 4f;
        private const float FishronBubbleFastSpeed = 16f;
        private const float FishronHazardMaxUncertainty = 4096f;

        public static ThreatTrajectory ForProjectileType(int projectileType)
        {
            switch (projectileType)
            {
                case 384:
                case 385:
                case 386:
                    return ThreatTrajectory.UnmodeledDukeFishronHazard;
                case 872:
                    return ThreatTrajectory.EmpressRainbowTrail;
                case 873:
                    return ThreatTrajectory.EmpressRainbowStreak;
                case 920:
                    return ThreatTrajectory.FallingHostileBolt;
                case 921:
                    return ThreatTrajectory.BouncingFallingHostileBolt;
                default:
                    return ThreatTrajectory.Linear;
            }
        }

        public static bool TrySample(in ThreatSnapshot threat, int ticks,
            out ProjectileMotionSample sample)
        {
            sample = default(ProjectileMotionSample);
            if (threat.Kind != ThreatKind.Projectile || ticks < 0 ||
                threat.Geometry != ThreatGeometry.Body ||
                threat.Trajectory == ThreatTrajectory.Linear ||
                threat.Trajectory != ForProjectileType(threat.Type) &&
                !(threat.Kind == ThreatKind.NpcContact &&
                  threat.Trajectory ==
                    ThreatTrajectory.UnmodeledDukeFishronHazard &&
                  threat.Type >= 371 && threat.Type <= 373) ||
                threat.TimeLeft <= 0 || threat.Width <= 0 ||
                threat.Height <= 0 ||
                !Finite(threat.Position) || !Finite(threat.Velocity) ||
                !Finite(threat.TrajectoryAi0)) return false;

            switch (threat.Trajectory)
            {
                case ThreatTrajectory.UnmodeledDukeFishronHazard:
                    if (!SampleDukeFishronHazard(threat, ticks,
                            ref sample))
                    {
                        sample = default(ProjectileMotionSample);
                    }
                    break;
                case ThreatTrajectory.EmpressRainbowTrail:
                    if (!SampleRainbowTrail(threat, ticks, ref sample))
                    {
                        sample = default(ProjectileMotionSample);
                    }
                    break;
                case ThreatTrajectory.EmpressRainbowStreak:
                    if (threat.NativeIdentity < 0) return false;
                    SampleRainbowStreak(threat, ticks, ref sample);
                    break;
                case ThreatTrajectory.FallingHostileBolt:
                    SampleFallingBolt(threat, ticks, false, ref sample);
                    break;
                case ThreatTrajectory.BouncingFallingHostileBolt:
                    SampleFallingBolt(threat, ticks, true, ref sample);
                    break;
                default:
                    return false;
            }
            if (Valid(sample)) return true;
            sample = default(ProjectileMotionSample);
            return false;
        }

        public static bool TrySweep(in ThreatSnapshot threat, int fromTicks,
            int toTicks, out ProjectileMotionSweep sweep)
        {
            sweep = default(ProjectileMotionSweep);
            if (fromTicks < 0 || toTicks < fromTicks ||
                threat.Trajectory == ThreatTrajectory.Linear) return false;

            var handled = false;
            var lastTick = threat.TimeLeft > 0
                ? Math.Min(toTicks, threat.TimeLeft) : toTicks;
            if (fromTicks > lastTick)
            {
                ProjectileMotionSample expired;
                return TrySample(threat, fromTicks, out expired);
            }
            for (var tick = fromTicks; ; tick++)
            {
                ProjectileMotionSample sample;
                if (!TrySample(threat, tick, out sample)) return false;
                handled = true;
                if (!sample.Active) continue;
                if (!sweep.Active)
                {
                    sweep.Bounds = sample.Bounds;
                    sweep.Active = true;
                }
                else sweep.Bounds = Union(sweep.Bounds, sample.Bounds);
                if (tick == lastTick) break;
            }
            return handled;
        }

        public static RectF ConservativeBounds(in ThreatSnapshot threat,
            int horizonTicks)
        {
            ProjectileMotionSweep sweep;
            return horizonTicks >= 0 &&
                TrySweep(threat, 0, horizonTicks, out sweep) &&
                sweep.Active ? sweep.Bounds : default(RectF);
        }

        /// <summary>
        /// Starts candidate-coupled prediction only when AI_171's captured
        /// ai[0] is an exact match for the local player. Unknown, fractional,
        /// out-of-range, or other-player targets keep using TrySweep's
        /// direction-independent conservative envelope.
        /// </summary>
        public static bool TryCreateTargetedState(in ThreatSnapshot threat,
            int localPlayerIndex, out TargetedProjectileMotionState state)
        {
            state = default(TargetedProjectileMotionState);
            if (localPlayerIndex < 0 || localPlayerIndex >= 255 ||
                !threat.NativeTargetPlayerKnown ||
                threat.NativeTargetPlayerIndex != localPlayerIndex ||
                !threat.TrajectoryAi0Known ||
                !Finite(threat.TrajectoryAi0) ||
                threat.TrajectoryAi0 != threat.NativeTargetPlayerIndex ||
                threat.Kind != ThreatKind.Projectile ||
                threat.Geometry != ThreatGeometry.Body ||
                threat.Trajectory != ThreatTrajectory.EmpressRainbowStreak ||
                threat.Type != 873 ||
                threat.NativeIdentity < 0 ||
                threat.TimeLeft <= 0 || threat.Width <= 0 ||
                threat.Height <= 0 || !Finite(threat.Position) ||
                !Finite(threat.Velocity))
                return false;

            state.Valid = true;
            state.Active = true;
            state.Position = threat.Position;
            state.Velocity = threat.Velocity;
            state.TimeLeft = threat.TimeLeft;
            state.Type = threat.Type;
            state.Width = threat.Width;
            state.Height = threat.Height;
            state.NativeIdentity = threat.NativeIdentity;
            state.Ai1 = threat.TrajectoryAi1;
            state.Ai2 = threat.TrajectoryAi2;
            state.LocalAi0 = threat.TrajectoryLocalAi0;
            state.Direction = threat.NativeDirectionKnown ?
                threat.NativeDirection : 0;
            return true;
        }

        /// <summary>
        /// Creates the exact native state needed for an Empress 8/9 contact
        /// dash. Every clock and difficulty bit is required; a caller cannot
        /// accidentally turn a default snapshot into a predictable dash.
        /// </summary>
        public static bool TryCreateEmpressDashState(
            in ThreatSnapshot threat, out EmpressDashMotionState state)
        {
            state = default(EmpressDashMotionState);
            if (threat.Kind != ThreatKind.NpcContact ||
                threat.Type != PriorityBossThreatGate.EmpressType ||
                threat.Trajectory != ThreatTrajectory.EmpressDashContact ||
                !threat.SourceBossContextKnown ||
                threat.SourceBossType != PriorityBossThreatGate.EmpressType ||
                !threat.TrajectoryAi1Known || !threat.TrajectoryAi3Known ||
                !threat.NativeExpertModeKnown ||
                !threat.NativeShouldBeEnragedKnown ||
                !threat.NativeTargetPlayerKnown ||
                threat.NativeTargetPlayerIndex < 0 ||
                threat.NativeTargetPlayerIndex >= 255 ||
                !threat.TrajectoryAi0Known ||
                !Finite(threat.TrajectoryAi0) ||
                !Finite(threat.TrajectoryAi1) ||
                !Finite(threat.TrajectoryAi3) ||
                threat.TrajectoryAi0 != 8f && threat.TrajectoryAi0 != 9f ||
                !IsIntegerInRange(threat.TrajectoryAi1, 0, 200) ||
                !IsIntegerInRange(threat.TrajectoryAi3, 0, 3) ||
                threat.Width <= 0 || threat.Height <= 0 ||
                !Finite(threat.Position) || !Finite(threat.Velocity) ||
                threat.NativeIdentity < 0)
                return false;

            state.Valid = true;
            state.Active = true;
            state.Position = threat.Position;
            state.Velocity = threat.Velocity;
            state.Width = threat.Width;
            state.Height = threat.Height;
            state.State = (int)threat.TrajectoryAi0;
            state.Timer = (int)threat.TrajectoryAi1;
            state.Phase2 = threat.TrajectoryAi3 == 1f ||
                threat.TrajectoryAi3 == 3f;
            state.Expert = threat.NativeExpertMode;
            state.Enraged = threat.NativeShouldBeEnraged;
            state.NativeIdentity = threat.NativeIdentity;
            var duration = DashDuration(in state);
            if (state.Timer < 0 || state.Timer >= duration)
            {
                state.Valid = false;
                state.Active = false;
                return false;
            }
            return true;
        }

        /// <summary>Advances one AI_120 dash update and integrates position in
        /// the same order as NPC.UpdateNPC (AI, then position += velocity).</summary>
        public static bool TryAdvanceEmpressDash(
            ref EmpressDashMotionState state, Vec2 targetCenter,
            out ProjectileMotionSample sample)
        {
            sample = default(ProjectileMotionSample);
            if (!ValidDashState(in state) || !Finite(targetCenter))
            {
                state.Valid = false;
                return false;
            }
            if (!state.Active)
            {
                sample.Position = state.Position;
                sample.Velocity = state.Velocity;
                sample.Bounds = BodyBounds(state.Position, state.Width,
                    state.Height, 0f, 0f);
                sample.Active = false;
                return Valid(sample);
            }

            var timer = state.Timer;
            var num33 = state.State == 8 ? -1f : 1f;
            if (timer <= 40)
            {
                var destination = targetCenter + new Vec2(num33 * -550f, 0f);
                var delta = destination - new Vec2(
                    state.Position.X + state.Width * .5f,
                    state.Position.Y + state.Height * .5f);
                // AI_120_DashTo first applies the -300 vertical offset and
                // retreats 100 px along the resulting direction when farther
                // than 200 px. This is the exact native target construction.
                destination = targetCenter + new Vec2(num33 * -550f, -300f);
                var toDestination = destination - new Vec2(
                    state.Position.X + state.Width * .5f,
                    state.Position.Y + state.Height * .5f);
                var length = toDestination.Length;
                if (!Finite(length)) return InvalidateDash(ref state);
                if (length > 200f && length > .0001f)
                    destination -= toDestination * (100f / length);
                delta = destination - new Vec2(
                    state.Position.X + state.Width * .5f,
                    state.Position.Y + state.Height * .5f);
                var direction = delta.Length > .0001f ?
                    delta * (1f / delta.Length) : default(Vec2);
                var desired = direction * 12f;
                SimpleFly(ref state.Velocity, desired, 1f);
                if (timer == 40) state.Velocity *= .3f;
            }
            else if (timer <= 90)
            {
                var desired = new Vec2(num33 * 50f, 0f);
                // Vector2.Lerp(value1: velocity, value2: desired, .05).
                state.Velocity = state.Velocity * .95f + desired * .05f;
                if (timer == 90) state.Velocity *= .7f;
            }
            else
            {
                state.Velocity *= .92f;
            }

            state.Position += state.Velocity;
            state.Timer++;
            if (state.Timer >= DashDuration(in state)) state.Active = false;
            sample.Active = true;
            sample.Position = state.Position;
            sample.Velocity = state.Velocity;
            sample.Bounds = BodyBounds(state.Position, state.Width,
                state.Height, 0f, 0f);
            if (Valid(sample)) return true;
            return InvalidateDash(ref state);
        }

        public static bool TryAdvanceEmpressDashSweep(
            ref EmpressDashMotionState state, Vec2 targetCenterBefore,
            Vec2 targetCenterAfter, int ticks, out ProjectileMotionSweep sweep)
        {
            sweep = default(ProjectileMotionSweep);
            if (ticks <= 0 || !Finite(targetCenterBefore) ||
                !Finite(targetCenterAfter) || !ValidDashState(in state))
                return false;
            if (state.Active)
            {
                sweep.Active = true;
                sweep.Bounds = BodyBounds(state.Position, state.Width,
                    state.Height, 0f, 0f);
            }
            var delta = targetCenterAfter - targetCenterBefore;
            for (var tick = 1; tick <= ticks; tick++)
            {
                var target = targetCenterBefore + delta *
                    (tick / (float)ticks);
                ProjectileMotionSample sample;
                if (!TryAdvanceEmpressDash(ref state, target, out sample))
                    return false;
                if (!sample.Active) continue;
                if (!sweep.Active)
                {
                    sweep.Active = true;
                    sweep.Bounds = sample.Bounds;
                }
                else sweep.Bounds = Union(sweep.Bounds, sample.Bounds);
            }
            return true;
        }

        /// <summary>
        /// Advances one native AI_171 update using the candidate player's
        /// center for this exact update. SmoothStep and position integration
        /// match Terraria 1.4.5.8's hostile type-873 branch.
        /// </summary>
        public static bool TryAdvanceTargeted(
            ref TargetedProjectileMotionState state, Vec2 targetPlayerCenter,
            out ProjectileMotionSample sample)
        {
            sample = default(ProjectileMotionSample);
            if (!ValidTargetedState(in state) ||
                !Finite(targetPlayerCenter))
            {
                state.Valid = false;
                return false;
            }

            if (!state.Active || state.TimeLeft <= 0)
            {
                sample.Active = false;
                sample.Position = state.Position;
                sample.Velocity = state.Velocity;
                sample.Bounds = BodyBounds(state.Position, state.Width,
                    state.Height, 0f, 0f);
                return Valid(sample);
            }

            var velocity = state.Velocity;
            if (state.Type == 385)
            {
                state.LocalAi0 += 1f;
                var speed = (state.Ai2 == 1f ? FishronBubbleFastSpeed :
                    FishronBubbleBaseSpeed) + state.LocalAi0 / 20f;
                if (!Finite(speed) || speed > 64f)
                {
                    state.Valid = false;
                    return false;
                }
                var bubbleCenter = new Vec2(state.Position.X +
                    state.Width * .5f, state.Position.Y + state.Height * .5f);
                var direction = targetPlayerCenter - bubbleCenter;
                var lengthSquared = direction.LengthSquared;
                if (!Finite(lengthSquared) || lengthSquared <= 0f)
                {
                    state.Valid = false;
                    return false;
                }
                velocity = direction * (speed /
                    (float)Math.Sqrt(lengthSquared));
            }
            else if (state.TimeLeft > 140)
            {
                velocity *= RainbowSlowdown;
                var wave = (float)Math.Cos(state.NativeIdentity % 6f / 6f +
                    state.Position.X / 320f + state.Position.Y / 160f);
                var rotation = wave * (Pi * 2f) * .125f / 30f;
                velocity = Rotate(velocity, rotation);
            }
            else if (state.TimeLeft > 30)
            {
                var projectileCenter = new Vec2(
                    state.Position.X + state.Width * .5f,
                    state.Position.Y + state.Height * .5f);
                var direction = targetPlayerCenter - projectileCenter;
                var lengthSquared = direction.LengthSquared;
                if (!Finite(lengthSquared) || lengthSquared <= 0f)
                {
                    state.Valid = false;
                    return false;
                }
                var targetDirection = direction *
                    (1f / (float)Math.Sqrt(lengthSquared));
                var targetVelocity = targetDirection * RainbowHomingSpeed;
                var progress = Clamp01((140f - state.TimeLeft) / 110f);
                var amount = .05f + .05f * progress;
                var weight = amount * amount * (3f - 2f * amount);
                velocity = velocity * (1f - weight) +
                    targetVelocity * weight;
            }

            state.Velocity = velocity;
            state.Position += velocity;
            state.TimeLeft--;
            state.Active = state.TimeLeft > 0;
            sample.Active = true;
            sample.Position = state.Position;
            sample.Velocity = state.Velocity;
            sample.Bounds = BodyBounds(state.Position, state.Width,
                state.Height, 0f, 0f);
            if (Valid(sample)) return true;
            state.Valid = false;
            sample = default(ProjectileMotionSample);
            return false;
        }

        /// <summary>
        /// Advances a bounded group of native updates. The candidate segment is
        /// sampled once per native tick rather than aiming every update at one
        /// endpoint; the result encloses each exact projectile body.
        /// </summary>
        public static bool TryAdvanceTargetedSweep(
            ref TargetedProjectileMotionState state,
            Vec2 targetPlayerCenterBefore, Vec2 targetPlayerCenterAfter,
            int ticks, out ProjectileMotionSweep sweep)
        {
            sweep = default(ProjectileMotionSweep);
            if (ticks <= 0 || !Finite(targetPlayerCenterBefore) ||
                !Finite(targetPlayerCenterAfter) ||
                !ValidTargetedState(in state)) return false;
            if (state.Active && state.TimeLeft > 0)
            {
                sweep.Active = true;
                sweep.Bounds = BodyBounds(state.Position, state.Width,
                    state.Height, 0f, 0f);
            }
            var delta = targetPlayerCenterAfter - targetPlayerCenterBefore;
            for (var tick = 1; tick <= ticks; tick++)
            {
                var target = targetPlayerCenterBefore +
                    delta * (tick / (float)ticks);
                ProjectileMotionSample sample;
                if (!TryAdvanceTargeted(ref state, target, out sample))
                    return false;
                if (!sample.Active) continue;
                if (!sweep.Active)
                {
                    sweep.Active = true;
                    sweep.Bounds = sample.Bounds;
                }
                else sweep.Bounds = Union(sweep.Bounds, sample.Bounds);
            }
            return true;
        }

        /// <summary>
        /// Samples the Fishron-owned secondary hazard families.  The vanilla
        /// branches are split between Projectile.AI_064/065 and NPC AI_070/071.
        /// We keep the source tag as the public trajectory value for backwards
        /// compatibility, but only admit a sample when every native field
        /// needed by that branch was captured.  Missing fields therefore stay
        /// fail-closed at PriorityBossThreatGate instead of silently becoming
        /// a guessed linear path.
        /// </summary>
        private static bool SampleDukeFishronHazard(
            in ThreatSnapshot threat, int ticks,
            ref ProjectileMotionSample sample)
        {
            if (!ValidFishronHazardSnapshot(in threat) || ticks < 0 ||
                ticks > 2000) return false;
            if (threat.Kind == ThreatKind.Projectile)
            {
                if (threat.Type == 384 || threat.Type == 386)
                    return SampleFishronTornadoProjectile(threat, ticks,
                        ref sample);
                if (threat.Type == 385)
                    return SampleFishronBubbleProjectile(threat, ticks,
                        ref sample);
                return false;
            }
            if (threat.Kind == ThreatKind.NpcContact)
            {
                if (threat.Type == 371)
                    return SampleFishronSharkNpc(threat, ticks,
                        ref sample);
                if (threat.Type == 372 || threat.Type == 373)
                    return SampleFishronTornadoNpc(threat, ticks,
                        ref sample);
            }
            return false;
        }

        private static bool ValidFishronHazardSnapshot(
            in ThreatSnapshot threat)
        {
            if (!threat.SourceBossContextKnown ||
                threat.SourceBossType != PriorityBossThreatGate.DukeFishronType ||
                threat.NativeIdentity < 0 || threat.Width <= 0 ||
                threat.Height <= 0 || threat.TimeLeft <= 0 ||
                !Finite(threat.Position) || !Finite(threat.Velocity)) return false;
            if (threat.TrajectoryAi0Known &&
                !FiniteNativeValue(threat.TrajectoryAi0) ||
                threat.TrajectoryAi1Known &&
                !FiniteNativeValue(threat.TrajectoryAi1) ||
                threat.TrajectoryAi2Known &&
                !FiniteNativeValue(threat.TrajectoryAi2) ||
                threat.TrajectoryAi3Known &&
                !FiniteNativeValue(threat.TrajectoryAi3) ||
                threat.TrajectoryLocalAi0Known &&
                !FiniteNativeValue(threat.TrajectoryLocalAi0) ||
                threat.TrajectoryLocalAi1Known &&
                !FiniteNativeValue(threat.TrajectoryLocalAi1)) return false;
            switch (threat.Type)
            {
                case 384:
                case 386:
                    // AI_064 only uses ai[0], ai[1] and localAI[0].
                    return threat.Kind == ThreatKind.Projectile &&
                        threat.TrajectoryAi0Known &&
                        threat.TrajectoryAi1Known &&
                        threat.TrajectoryLocalAi0Known &&
                        IsFiniteScale(threat.TrajectoryAi0) &&
                        (threat.TrajectoryAi1 == -1f ||
                         IsIntegerInRange(threat.TrajectoryAi1, 0, 1000)) &&
                        IsIntegerInRange(threat.TrajectoryLocalAi0, 0, 100000);
                case 385:
                    return threat.Kind == ThreatKind.Projectile &&
                        threat.TrajectoryAi0Known &&
                        threat.TrajectoryAi1Known &&
                        threat.TrajectoryAi2Known &&
                        threat.TrajectoryLocalAi0Known &&
                        IsFiniteScale(threat.TrajectoryAi0) &&
                        IsIntegerInRange(threat.TrajectoryAi1, -1, 255) &&
                        (threat.TrajectoryAi1 <= 0f ||
                         threat.NativeTargetPlayerKnown &&
                         threat.NativeTargetPlayerIndex >= 0 &&
                         threat.NativeTargetPlayerIndex < 255) &&
                        IsIntegerInRange(threat.TrajectoryAi2, 0, 1) &&
                        IsIntegerInRange(threat.TrajectoryLocalAi0, 0, 100000);
                case 371:
                    return threat.Kind == ThreatKind.NpcContact &&
                        threat.TrajectoryAi0Known &&
                        threat.TrajectoryAi1Known &&
                        threat.TrajectoryAi3Known &&
                        IsIntegerInRange(threat.TrajectoryAi0, 0, 1) &&
                        IsIntegerInRange(threat.TrajectoryAi1, 0, 1000) &&
                        IsIntegerInRange(threat.TrajectoryAi2, -1, 1000) &&
                        IsFiniteScale(threat.TrajectoryAi3);
                case 372:
                    return threat.Kind == ThreatKind.NpcContact &&
                        threat.TrajectoryAi0Known &&
                        threat.TrajectoryAi1Known &&
                        IsIntegerInRange(threat.TrajectoryAi0, 0, 1) &&
                        IsIntegerInRange(threat.TrajectoryAi1, 0, 1000);
                case 373:
                    return threat.Kind == ThreatKind.NpcContact &&
                        threat.TrajectoryAi0Known &&
                        threat.TrajectoryAi1Known &&
                        threat.TrajectoryAi2Known &&
                        threat.TrajectoryAi3Known &&
                        threat.TrajectoryLocalAi1Known &&
                        IsIntegerInRange(threat.TrajectoryAi0, 0, 1) &&
                        IsIntegerInRange(threat.TrajectoryAi1, 0, 1000) &&
                        IsFiniteScale(threat.TrajectoryAi2) &&
                        IsFiniteScale(threat.TrajectoryAi3) &&
                        IsFiniteScale(threat.TrajectoryLocalAi1);
                default:
                    return false;
            }
        }

        private static bool SampleFishronTornadoProjectile(
            in ThreatSnapshot threat, int ticks,
            ref ProjectileMotionSample sample)
        {
            var position = threat.Position;
            var velocity = threat.Velocity;
            var ai0 = threat.TrajectoryAi0;
            var ai1 = threat.TrajectoryAi1;
            var local0 = threat.TrajectoryLocalAi0;
            var direction = threat.NativeDirectionKnown &&
                (threat.NativeDirection == -1 || threat.NativeDirection == 1)
                ? threat.NativeDirection : Math.Sign(velocity.X);
            if (direction == 0) direction = 1;
            var width = threat.Width;
            var height = threat.Height;
            var directionUncertainty = 0f;
            var baseWidth = threat.Type == 386 ? 150f : 150f;
            var baseHeight = threat.Type == 386 ? 42f : 42f;
            var scaleFactor = threat.Type == 386 ? 1.5f : 1f;
            for (var tick = 0; tick < ticks; tick++)
            {
                if (ai1 != -1f)
                {
                    var scale = ((threat.Type == 386 ? 32f : 25f) - ai1) *
                        scaleFactor / (threat.Type == 386 ? 32f : 25f);
                    if (!Finite(scale) || scale <= 0f || scale > 4f)
                        return false;
                    width = Math.Max(1, (int)(baseWidth * scale));
                    height = Math.Max(1, (int)(baseHeight * scale));
                }

                // AI_064 decrements a positive ai[0] before the late
                // oscillation branch.  Its x correction is deterministic for
                // a known direction and bounded by the two signs otherwise.
                if (ai0 > 0f) ai0 -= 1f;
                if (ai0 <= 0f)
                {
                    var phase0 = Pi / 30f * (-ai0);
                    var amp = width / 5f * (threat.Type == 386 ? 2f : 1f);
                    var first = ((float)Math.Cos(phase0) - .5f) * amp;
                    ai0 -= 1f;
                    var second = ((float)Math.Cos(Pi / 30f * (-ai0)) -
                        .5f) * amp;
                    position.X += (second - first) * (-direction);
                    if (!threat.NativeDirectionKnown)
                        directionUncertainty += Math.Abs(second - first) * 2f;
                }
                position += velocity;
                if (timeLeftPositive(threat.TimeLeft, tick)) { }
            }
            sample.Active = threat.TimeLeft <= 0 || ticks < threat.TimeLeft;
            sample.Position = position;
            sample.Velocity = velocity;
            sample.UncertaintyX = threat.NativeDirectionKnown ? 0f :
                Math.Min(FishronHazardMaxUncertainty,
                    Math.Max(directionUncertainty,
                        Math.Abs(width / 5f) * 2f * Math.Max(1, ticks)));
            sample.UncertaintyY = 0f;
            sample.Bounds = BodyBounds(position, width, height,
                sample.UncertaintyX, sample.UncertaintyY);
            return Valid(sample);
        }

        private static bool SampleFishronBubbleProjectile(
            in ThreatSnapshot threat, int ticks,
            ref ProjectileMotionSample sample)
        {
            var position = threat.Position;
            var velocity = threat.Velocity;
            var ai0 = threat.TrajectoryAi0;
            var ai1 = threat.TrajectoryAi1;
            var ai2 = threat.TrajectoryAi2;
            var local0 = threat.TrajectoryLocalAi0;
            var uncertainty = 0f;
            for (var tick = 0; tick < ticks; tick++)
            {
                if (ai1 > 0f)
                {
                    local0 += 1f;
                    var speed = (ai2 == 1f ? FishronBubbleFastSpeed :
                        FishronBubbleBaseSpeed) + local0 / 20f;
                    if (!Finite(speed) || speed > 64f) return false;
                    // The exact target direction is candidate-coupled in the
                    // planner.  A radial envelope here covers all possible
                    // player movement and keeps the generic broadphase safe.
                    uncertainty += speed;
                    velocity = new Vec2(0f, 0f);
                }
                else
                {
                    var phase = Pi / 15f * ai0;
                    var correction = ((float)Math.Cos(phase) - .5f) * 4f;
                    ai0 += 1f;
                    var next = ((float)Math.Cos(Pi / 15f * ai0) - .5f) * 4f;
                    velocity.Y += next - correction;
                    position += velocity;
                }
                if (ai1 > 0f) position += new Vec2(0f, 0f);
            }
            sample.Active = threat.TimeLeft <= 0 || ticks < threat.TimeLeft;
            sample.Position = position;
            sample.Velocity = velocity;
            sample.UncertaintyX = uncertainty;
            sample.UncertaintyY = uncertainty;
            sample.Bounds = BodyBounds(position, threat.Width, threat.Height,
                uncertainty, uncertainty);
            return Valid(sample);
        }

        private static bool SampleFishronSharkNpc(in ThreatSnapshot threat,
            int ticks, ref ProjectileMotionSample sample)
        {
            var position = threat.Position;
            var velocity = threat.Velocity;
            var uncertainty = 0f;
            var targetKnown = threat.NativeTargetPlayerKnown &&
                threat.NativeTargetPlayerIndex >= 0 &&
                threat.NativeTargetPlayerIndex < 255;
            for (var tick = 0; tick < ticks; tick++)
            {
                // AI_070 adds bounded random wind terms after blending toward
                // its target.  Preserve the observed velocity as the center and
                // accumulate a proven envelope for those terms; when target
                // ownership is unknown, include one full native-speed radius.
                var speed = velocity.Length;
                var acceleration = targetKnown ? 18f : 20f;
                uncertainty += acceleration;
                if (speed > 24f) speed = 24f;
                position += velocity;
                velocity *= 40f / 41f;
            }
            sample.Active = true;
            sample.Position = position;
            sample.Velocity = velocity;
            sample.UncertaintyX = uncertainty;
            sample.UncertaintyY = uncertainty;
            sample.Bounds = BodyBounds(position, threat.Width, threat.Height,
                uncertainty, uncertainty);
            return Valid(sample);
        }

        private static bool SampleFishronTornadoNpc(in ThreatSnapshot threat,
            int ticks, ref ProjectileMotionSample sample)
        {
            var position = threat.Position;
            var velocity = threat.Velocity;
            var ai0 = threat.TrajectoryAi0;
            var ai1 = threat.TrajectoryAi1;
            var amplitude = threat.Type == 373 ? threat.TrajectoryAi2 : 0f;
            var phase = threat.Type == 373 ? threat.TrajectoryLocalAi1 : 0f;
            var uncertainty = 0f;
            for (var tick = 0; tick < ticks; tick++)
            {
                if (ai0 == 0f)
                {
                    ai1 += 1f;
                    velocity.Y = threat.TrajectoryAi3;
                    if (threat.Type == 373)
                    {
                        var first = ((float)Math.Cos(Pi / 30f * phase) -
                            .5f) * amplitude;
                        phase += 1f;
                        var second = ((float)Math.Cos(Pi / 30f * phase) -
                            .5f) * amplitude;
                        position.X += (second - first) *
                            (-(threat.NativeDirection == 0 ? 1 :
                                Math.Sign(threat.NativeDirection)));
                    }
                    position += velocity;
                    if (ai1 >= 90f)
                    {
                        ai0 = 1f;
                        ai1 = 0f;
                        // Transition velocity is target-derived and therefore
                        // receives a one-time 16 px/tick radial envelope.
                        uncertainty += 16f;
                    }
                }
                else
                {
                    if (ai1 < 1f) ai1 = 1f;
                    // AI_071 retargets the live player at the transition and
                    // then travels at 16 px/tick.  The snapshot has no future
                    // player path, so each chasing tick contributes the full
                    // native speed to the positional envelope.
                    uncertainty += 16f;
                    position += velocity;
                    ai1 += 1f;
                }
            }
            sample.Active = true;
            sample.Position = position;
            sample.Velocity = velocity;
            sample.UncertaintyX = uncertainty;
            sample.UncertaintyY = uncertainty;
            sample.Bounds = BodyBounds(position, threat.Width, threat.Height,
                uncertainty, uncertainty);
            return Valid(sample);
        }

        private static bool IsFiniteScale(float value)
        {
            return FiniteNativeValue(value);
        }

        private static bool FiniteNativeValue(float value)
        {
            return Finite(value) && value > -100000f && value < 100000f;
        }

        // Kept as a tiny named predicate to make the lifetime intent in the
        // projectile loop explicit without introducing a mutable extra field.
        private static bool timeLeftPositive(int timeLeft, int elapsed)
        {
            return timeLeft <= 0 || elapsed + 1 < timeLeft;
        }

        private static void SampleRainbowStreak(in ThreatSnapshot threat,
            int ticks, ref ProjectileMotionSample sample)
        {
            var position = threat.Position;
            var velocity = threat.Velocity;
            var positionRadius = 0f;
            var velocityRadius = 0f;
            var timeLeft = threat.TimeLeft;

            for (var tick = 0; tick < ticks &&
                (timeLeft <= 0 || tick < threat.TimeLeft); tick++)
            {
                // AI_171's first stage is deterministic: the native code uses
                // whoAmI, top-left position and the current velocity in exactly
                // this order before HandleMovement applies the new velocity.
                if (timeLeft <= 0 || timeLeft > 140)
                {
                    velocity = velocity * RainbowSlowdown;
                    var wave = (float)Math.Cos(threat.NativeIdentity % 6f / 6f +
                        position.X / 320f + position.Y / 160f);
                    var rotation = wave * (Pi * 2f) * .125f / 30f;
                    velocity = Rotate(velocity, rotation);
                }
                else if (timeLeft > 30)
                {
                    // The target player's future position is intentionally not
                    // guessed. Vector2.SmoothStep is affine after its scalar
                    // cubic, so this recurrence encloses every possible native
                    // target direction of length 30.
                    var progress = Clamp01((140f - timeLeft) / 110f);
                    var amount = .05f + .05f * progress;
                    var weight = amount * amount * (3f - 2f * amount);
                    var targetRadius = Math.Max(RainbowHomingSpeed,
                        velocity.Length + velocityRadius);
                    velocity = velocity * (1f - weight);
                    velocityRadius = velocityRadius * (1f - weight) +
                        targetRadius * weight;
                }

                position += velocity;
                positionRadius += velocityRadius;
                if (timeLeft > 0) timeLeft--;
            }

            sample.Active = threat.TimeLeft <= 0 || ticks <= threat.TimeLeft;
            sample.Position = position;
            sample.Velocity = velocity;
            sample.UncertaintyX = positionRadius;
            sample.UncertaintyY = positionRadius;
            sample.Bounds = BodyBounds(threat, position, positionRadius,
                positionRadius);
        }

        private static bool SampleRainbowTrail(in ThreatSnapshot threat,
            int ticks, ref ProjectileMotionSample sample)
        {
            if (!threat.NativeRainbowHistoryKnown ||
                !threat.TrajectoryAi0Known ||
                RainbowTrailHistory50.Length != 50 || ticks < 0)
                return false;

            var position = threat.Position;
            var velocity = threat.Velocity;
            var ai0 = threat.TrajectoryAi0;
            var timeLeft = threat.TimeLeft;
            var history = threat.NativeRainbowHistory;
            if (!Finite(ai0) || !Finite(position) || !Finite(velocity) ||
                timeLeft <= 0) return false;

            // At t=0 Damage() sees the captured history before the next native
            // update. Every later sample first runs AI_173 and integrates the
            // new body position, then checks the *pre-shift* history, matching
            // Projectile.Update's Damage -> oldPos shift -> timeLeft order.
            for (var tick = 0; tick <= ticks; tick++)
            {
                if (timeLeft <= 0)
                {
                    sample.Active = false;
                    sample.Position = position;
                    sample.Velocity = velocity;
                    sample.Bounds = default(RectF);
                    return true;
                }

                if (tick > 0)
                {
                    var rotation = ai0;
                    velocity = Rotate(velocity, rotation);
                    if (ai0 < Pi / 360f)
                        ai0 += (Pi / 360f) / 30f;
                    position += velocity;
                }

                RectF hazard = default(RectF);
                var hasHazard = false;
                for (var slot = 0; slot < 50; slot += 2)
                {
                    var old = history.Get(slot);
                    // Vector2.Zero is vanilla's uninitialized trail sentinel.
                    if (old.X == 0f && old.Y == 0f) continue;
                    if (!Finite(old)) return false;
                    var body = new RectF((int)old.X, (int)old.Y,
                        threat.Width, threat.Height);
                    if (!hasHazard)
                    {
                        hazard = body;
                        hasHazard = true;
                    }
                    else hazard = Union(hazard, body);
                }

                sample.Active = hasHazard;
                sample.Position = position;
                sample.Velocity = velocity;
                sample.UncertaintyX = 0f;
                sample.UncertaintyY = 0f;
                sample.Bounds = hasHazard ? hazard : default(RectF);

                // The native update shifts the trail and decrements lifetime
                // after Damage(). Do this after producing the current sample,
                // including the final damaging frame when timeLeft == 1.
                if (tick > 0)
                {
                    for (var slot = 49; slot > 0; slot--)
                        history.Set(slot, history.Get(slot - 1));
                    history.Set(0, position);
                    timeLeft--;
                }
            }
            return true;
        }

        private static void SampleFallingBolt(in ThreatSnapshot threat,
            int ticks, bool canBounce, ref ProjectileMotionSample sample)
        {
            var position = threat.Position;
            var velocity = threat.Velocity;
            var ai0 = threat.TrajectoryAi0;
            var horizontalTravelBound = 0f;
            var verticalTravelBound = 0f;
            var horizontalSpeedBound = Math.Abs(velocity.X);
            var verticalSpeedBound = Math.Abs(velocity.Y);
            var windVelocityRadius = 0f;
            var windTravelBound = 0f;
            var minimumTravelX = 0f;
            var maximumTravelX = 0f;
            var minimumTravelY = 0f;
            var maximumTravelY = 0f;
            var largestSlopeCalls = 0;
            var secondSlopeCalls = 0;
            var thirdSlopeCalls = 0;

            for (var tick = 0; tick < ticks &&
                (threat.TimeLeft <= 0 || tick < threat.TimeLeft); tick++)
            {
                ai0 += 1f;
                var gravityActive = ai0 >= 5f;
                if (gravityActive)
                {
                    ai0 = 5f;
                    velocity.Y += FallingGravity;
                }
                // The common AI_001 falling branch caps only positive Y for
                // these types. An initially faster upward value is not mirrored
                // to -16, and therefore remains part of the bound.
                if (velocity.Y > MaximumFallingSpeed)
                    velocity.Y = MaximumFallingSpeed;
                if (gravityActive &&
                    verticalSpeedBound <= MaximumFallingSpeed)
                    verticalSpeedBound = Math.Min(MaximumFallingSpeed,
                        verticalSpeedBound + FallingGravity);

                position += velocity;
                if (horizontalSpeedBound < MaximumWindSpeed)
                    horizontalSpeedBound = Math.Min(MaximumWindSpeed,
                        horizontalSpeedBound + MaximumWindAcceleration);
                horizontalTravelBound += horizontalSpeedBound;
                verticalTravelBound += verticalSpeedBound;
                AddLiquidTravelRange(velocity.X, ref minimumTravelX,
                    ref maximumTravelX);
                AddLiquidTravelRange(velocity.Y, ref minimumTravelY,
                    ref maximumTravelY);
                // Wind is conditional on world height, walls and direction,
                // none of which belongs in a compact threat snapshot. Enclose
                // every possible application/non-application sequence. Liquid
                // scales movement by at most one, so the full velocity radius
                // is also a valid positional increment bound.
                windVelocityRadius = Math.Min(
                    Math.Abs(threat.Velocity.X) +
                        Math.Max(Math.Abs(threat.Velocity.X),
                            MaximumWindSpeed),
                    windVelocityRadius + MaximumWindAcceleration);
                windTravelBound += windVelocityRadius;
                if (canBounce)
                    InsertTopThree(SlopeCallBound(horizontalSpeedBound,
                        verticalSpeedBound, threat.Width, threat.Height),
                        ref largestSlopeCalls, ref secondSlopeCalls,
                        ref thirdSlopeCalls);
            }

            sample.Active = threat.TimeLeft <= 0 || ticks <= threat.TimeLeft;
            sample.Position = position;
            sample.Velocity = velocity;
            // Both defaults have ignoreWater=false. Native water, honey and
            // shimmer movement can reduce an update to .5, .25 or .375 of its
            // dry velocity. Type 920 dies before Damage when it touches a tile,
            // so its dangerous future paths are enclosed by accumulating the
            // per-update [.25*v, v] liquid range on each signed component and
            // the independent worst-case native wind radius on X.
            if (!canBounce)
            {
                sample.UncertaintyX = Math.Max(
                    Math.Abs(minimumTravelX - windTravelBound),
                    Math.Abs(maximumTravelX + windTravelBound));
                sample.UncertaintyY = Math.Max(Math.Abs(minimumTravelY),
                    Math.Abs(maximumTravelY));
                sample.Bounds = new RectF(threat.Position.X +
                        minimumTravelX - windTravelBound,
                    threat.Position.Y + minimumTravelY,
                    threat.Width + maximumTravelX - minimumTravelX +
                        windTravelBound * 2f,
                    threat.Height + maximumTravelY - minimumTravelY);
                return;
            }

            // Type 921 can survive three tile responses. Its -.4/-.95 bounce
            // never increases a component, while SlopeCollision may translate
            // position once per high-speed collision slice. Each snap is less
            // than one tile plus the projectile's largest dimension; summing the
            // three largest possible collision-update call counts covers even
            // hoik/embedded-tile states without copying the tile map into Core.
            var slopeCorrection = (16f + Math.Max(threat.Width,
                threat.Height)) * (largestSlopeCalls + secondSlopeCalls +
                thirdSlopeCalls);
            sample.UncertaintyX = horizontalTravelBound + slopeCorrection;
            sample.UncertaintyY = verticalTravelBound + slopeCorrection;
            sample.Bounds = BodyBounds(threat, threat.Position,
                sample.UncertaintyX, sample.UncertaintyY);
        }

        private static RectF BodyBounds(in ThreatSnapshot threat, Vec2 position,
            float uncertaintyX, float uncertaintyY)
        {
            return BodyBounds(position, threat.Width, threat.Height,
                uncertaintyX, uncertaintyY);
        }

        private static RectF BodyBounds(Vec2 position, int width, int height,
            float uncertaintyX, float uncertaintyY)
        {
            return new RectF(position.X - uncertaintyX,
                position.Y - uncertaintyY, Math.Max(0, width) +
                uncertaintyX * 2f, Math.Max(0, height) +
                uncertaintyY * 2f);
        }

        private static void AddLiquidTravelRange(float velocity,
            ref float minimum, ref float maximum)
        {
            if (velocity < 0f)
            {
                minimum += velocity;
                maximum += velocity * MinimumLiquidTravel;
            }
            else
            {
                minimum += velocity * MinimumLiquidTravel;
                maximum += velocity;
            }
        }

        private static int SlopeCallBound(float horizontalSpeed,
            float verticalSpeed, int width, int height)
        {
            var collisionSlice = Math.Max(3, Math.Min(16,
                Math.Min(width, height)));
            var speedSquared = horizontalSpeed * horizontalSpeed +
                verticalSpeed * verticalSpeed;
            if (!Finite(speedSquared) || speedSquared >
                    300f * collisionSlice * (300f * collisionSlice))
                return 301;
            var speed = (float)Math.Sqrt(speedSquared);
            return speed <= collisionSlice ? 1 :
                Math.Min(300, (int)Math.Ceiling(speed / collisionSlice)) + 1;
        }

        private static void InsertTopThree(int value, ref int first,
            ref int second, ref int third)
        {
            if (value >= first)
            {
                third = second;
                second = first;
                first = value;
            }
            else if (value >= second)
            {
                third = second;
                second = value;
            }
            else if (value > third) third = value;
        }

        private static Vec2 Rotate(Vec2 value, float radians)
        {
            var cosine = (float)Math.Cos(radians);
            var sine = (float)Math.Sin(radians);
            return new Vec2(value.X * cosine - value.Y * sine,
                value.X * sine + value.Y * cosine);
        }

        private static void SimpleFly(ref Vec2 velocity, Vec2 desired,
            float moveSpeed)
        {
            if (velocity.X < desired.X)
            {
                velocity.X += moveSpeed;
                if (velocity.X < 0f && desired.X > 0f)
                    velocity.X += moveSpeed;
            }
            else if (velocity.X > desired.X)
            {
                velocity.X -= moveSpeed;
                if (velocity.X > 0f && desired.X < 0f)
                    velocity.X -= moveSpeed;
            }
            if (velocity.Y < desired.Y)
            {
                velocity.Y += moveSpeed;
                if (velocity.Y < 0f && desired.Y > 0f)
                    velocity.Y += moveSpeed;
            }
            else if (velocity.Y > desired.Y)
            {
                velocity.Y -= moveSpeed;
                if (velocity.Y > 0f && desired.Y < 0f)
                    velocity.Y -= moveSpeed;
            }
        }

        private static int DashDuration(in EmpressDashMotionState state)
        {
            var num17 = 0;
            if (state.Phase2) num17 += 15;
            if (state.Expert || state.Enraged) num17 += 5;
            return 90 + 20 - num17;
        }

        private static bool ValidDashState(in EmpressDashMotionState state)
        {
            return state.Valid && state.State >= 8 && state.State <= 9 &&
                state.Width > 0 && state.Height > 0 &&
                state.NativeIdentity >= 0 && state.Timer >= 0 &&
                state.Timer < DashDuration(in state) &&
                Finite(state.Position) && Finite(state.Velocity);
        }

        private static bool InvalidateDash(ref EmpressDashMotionState state)
        {
            state.Valid = false;
            state.Active = false;
            return false;
        }

        private static bool IsIntegerInRange(float value, int minimum,
            int maximum)
        {
            return Finite(value) && value >= minimum && value <= maximum &&
                value == (int)value;
        }

        private static RectF Union(in RectF first, in RectF second)
        {
            var left = Math.Min(first.Left, second.Left);
            var top = Math.Min(first.Top, second.Top);
            return new RectF(left, top,
                Math.Max(first.Right, second.Right) - left,
                Math.Max(first.Bottom, second.Bottom) - top);
        }

        private static bool Finite(Vec2 value) => Finite(value.X) &&
            Finite(value.Y);
        private static bool Finite(in RectF value) => Finite(value.X) &&
            Finite(value.Y) && Finite(value.Width) && Finite(value.Height);
        private static bool Valid(in ProjectileMotionSample sample) =>
            Finite(sample.Position) && Finite(sample.Velocity) &&
            Finite(sample.UncertaintyX) && sample.UncertaintyX >= 0f &&
            Finite(sample.UncertaintyY) && sample.UncertaintyY >= 0f &&
            Finite(sample.Bounds) && sample.Bounds.Width > 0f &&
            sample.Bounds.Height > 0f;
        private static bool ValidTargetedState(
            in TargetedProjectileMotionState state) => state.Valid &&
            state.Type == 873 && state.Width > 0 && state.Height > 0 &&
            state.NativeIdentity >= 0 && state.TimeLeft >= 0 &&
            Finite(state.Position) && Finite(state.Velocity);
        private static bool Finite(float value) => !float.IsNaN(value) &&
            !float.IsInfinity(value);
        private static float Clamp01(float value) => Math.Max(0f,
            Math.Min(1f, value));
    }

    /// <summary>
    /// Narrow admission boundary for native hazards whose current hitbox and
    /// linear velocity do not enclose their future damage.  This deliberately
    /// covers only Duke Fishron and Empress of Light; it does not make these
    /// trajectories predictable and cannot classify them without a live
    /// same-frame Boss source supplied by the native adapter.
    /// </summary>
    public static class PriorityBossThreatGate
    {
        public const int DukeFishronType = 370;
        public const int EmpressType = 636;

        public static ThreatTrajectory SourceBoundProjectileTrajectory(
            int projectileType, bool dukeFishronSourceActive,
            bool empressSourceActive)
        {
            var trajectory = HostileProjectileMotion.ForProjectileType(
                projectileType);
            if (trajectory == ThreatTrajectory.UnmodeledDukeFishronHazard &&
                !dukeFishronSourceActive)
                return ThreatTrajectory.Linear;
            if (projectileType == 872)
            {
                // A type-872 projectile is only safe to model after the
                // adapter has captured its native oldPos history.  The simple
                // source-bound classifier deliberately returns an unknown
                // sentinel; TerrariaFacade upgrades it to
                // EmpressRainbowTrail only when all 50 entries are finite.
                return empressSourceActive
                    ? ThreatTrajectory.UnmodeledEmpressRainbowTrail
                    : ThreatTrajectory.Linear;
            }
            if ((trajectory == ThreatTrajectory.EmpressRainbowTrail ||
                 trajectory == ThreatTrajectory.UnmodeledEmpressRainbowTrail) &&
                !empressSourceActive)
                return ThreatTrajectory.Linear;
            return trajectory;
        }

        // Compatibility surface used by older/synthetic adapters.  The
        // four-argument form has no way to distinguish an unavailable ai[0]
        // from a deliberately supplied value, so it only classifies the two
        // reviewed dash states and leaves all other contacts Linear.  The
        // native facade uses the strict overload below and supplies that
        // availability bit explicitly.
        public static ThreatTrajectory SourceBoundNpcTrajectory(int npcType,
            float ai0, bool dukeFishronSourceActive,
            bool empressSourceActive)
        {
            if (dukeFishronSourceActive && npcType >= 371 && npcType <= 373)
                return ThreatTrajectory.UnmodeledDukeFishronHazard;
            if (empressSourceActive && npcType == EmpressType &&
                (ai0 == 8f || ai0 == 9f))
                return ThreatTrajectory.UnmodeledEmpressDashContact;
            return ThreatTrajectory.Linear;
        }

        public static ThreatTrajectory SourceBoundNpcTrajectory(int npcType,
            float ai0, bool ai0Known, bool dukeFishronSourceActive,
            bool empressSourceActive)
        {
            if (dukeFishronSourceActive && npcType >= 371 && npcType <= 373)
                return ThreatTrajectory.UnmodeledDukeFishronHazard;
            if (empressSourceActive && npcType == EmpressType)
            {
                // A missing ai[0] must never inherit a default value which
                // happens to equal a dash state.  The strict native adapter
                // supplies this bit explicitly; reject before comparing the
                // payload so malformed snapshots remain fail-closed.
                if (!ai0Known)
                    return ThreatTrajectory.UnmodeledEmpressDashContact;
                if (ai0 == 8f || ai0 == 9f)
                    return ThreatTrajectory.EmpressDashContact;
                // A native adapter which explicitly reports an unavailable,
                // fractional, or out-of-range state cannot prove the contact
                // branch.  Keep that path source-bound and fail closed.  The
                // compatibility overload above intentionally does not make
                // this inference from a bare float.
                if (ai0Known && (!IsFinite(ai0) || ai0 != (int)ai0 ||
                    ai0 < 0f || ai0 > 13f))
                    return ThreatTrajectory.UnmodeledEmpressDashContact;
            }
            return ThreatTrajectory.Linear;
        }

        public static bool ShouldCaptureProjectile(bool hostile, int damage,
            int projectileType, ThreatTrajectory trajectory)
        {
            if (!hostile) return false;
            if (damage > 0) return true;
            // Type 385 is non-damaging only until Kill spawns a damaging 384
            // or 386.  It is admitted solely inside the source-bound Fishron
            // branch; a type match by itself does not widen the normal filter.
            return projectileType == 385 && trajectory ==
                ThreatTrajectory.UnmodeledDukeFishronHazard;
        }

        public static int RequiredSourceBossType(
            ThreatTrajectory trajectory)
        {
            switch (trajectory)
            {
                case ThreatTrajectory.UnmodeledDukeFishronHazard:
                    return DukeFishronType;
                case ThreatTrajectory.EmpressRainbowTrail:
                case ThreatTrajectory.EmpressDashContact:
                case ThreatTrajectory.UnmodeledEmpressRainbowTrail:
                case ThreatTrajectory.UnmodeledEmpressDashContact:
                    return EmpressType;
                default:
                    return 0;
            }
        }

        public static bool TryGetNeutralHoldReason(CombatSnapshot snapshot,
            int sourceBossType, out string reason)
        {
            reason = null;
            if (snapshot == null || !HasLiveSource(snapshot, sourceBossType))
                return false;

            for (var index = 0; index < snapshot.Threats.Count; index++)
            {
                var threat = snapshot.Threats[index];
                if (!threat.SourceBossContextKnown ||
                    threat.SourceBossType != sourceBossType)
                    continue;

                if (sourceBossType == DukeFishronType &&
                    threat.Trajectory ==
                        ThreatTrajectory.UnmodeledDukeFishronHazard &&
                    (threat.Kind == ThreatKind.NpcContact &&
                            threat.Type >= 371 && threat.Type <= 373 ||
                        threat.Kind == ThreatKind.Projectile &&
                            threat.Type >= 384 && threat.Type <= 386))
                {
                    ProjectileMotionSample fishronSample;
                    if (!HostileProjectileMotion.TrySample(threat, 0,
                            out fishronSample))
                    {
                        reason = "Duke Fishron has an active bubble, shark, or tornado hazard without a proven native trajectory envelope";
                        return true;
                    }
                }

                if (sourceBossType == EmpressType &&
                    threat.Kind == ThreatKind.Projectile &&
                    threat.Type == 872 &&
                    (threat.Trajectory == ThreatTrajectory.EmpressRainbowTrail ||
                     threat.Trajectory == ThreatTrajectory.UnmodeledEmpressRainbowTrail) &&
                    !IsValidRainbowTrailThreat(in threat))
                {
                    reason = "Empress of Light rainbow trail native history is unavailable or malformed";
                    return true;
                }

                if (sourceBossType == EmpressType &&
                    threat.Kind == ThreatKind.NpcContact &&
                    threat.Type == EmpressType &&
                    (threat.Trajectory == ThreatTrajectory.EmpressDashContact ||
                     threat.Trajectory == ThreatTrajectory.UnmodeledEmpressDashContact) &&
                    !IsValidDashThreat(in threat))
                {
                    reason = "Empress of Light dash contact native state is unavailable or malformed";
                    return true;
                }
            }
            return false;
        }

        private static bool HasLiveSource(CombatSnapshot snapshot,
            int sourceBossType)
        {
            for (var index = 0; index < snapshot.Targets.Count; index++)
            {
                var target = snapshot.Targets[index];
                if (target.Type == sourceBossType && target.Boss &&
                    target.Life > 0 && target.Key >= 0)
                    return true;
            }
            return false;
        }

        private static bool IsValidRainbowTrailThreat(
            in ThreatSnapshot threat)
        {
            ProjectileMotionSample sample;
            return threat.Trajectory == ThreatTrajectory.EmpressRainbowTrail &&
                HostileProjectileMotion.TrySample(threat, 0, out sample);
        }

        private static bool IsValidDashThreat(in ThreatSnapshot threat)
        {
            EmpressDashMotionState state;
            return threat.Trajectory == ThreatTrajectory.EmpressDashContact &&
                HostileProjectileMotion.TryCreateEmpressDashState(threat,
                out state);
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }

    /// <summary>
    /// Projectile 919's native AI_179 movement and Colliding line segment.
    /// Integral tick samples exactly preserve its age-60 launch, age-61 damage
    /// gate, 40 px/tick travel and 80-by-8 oriented collision shape.
    /// </summary>
    public static class EmpressLanceGeometry
    {
        private const float HalfLength = 40f;
        private const float HalfWidth = 4f;
        // A handful of float projections/reconstructions are used to turn the
        // native line into a lobe. Inflate by roughly eight single-precision
        // ulps, including the absolute world-coordinate magnitude: otherwise
        // a locally tiny lobe can lose an edge when Bounds adds it to a center
        // near Terraria's world limit.
        private const float RoundoffScale = .000001f;

        public static BeamSample AtTime(in ThreatSnapshot threat, float ticks)
        {
            if (!Finite(ticks) || ticks < 0f || ticks >= 361f ||
                !ValidSnapshot(threat))
                return default(BeamSample);
            var completed = CompletedTicks(ticks);
            if (!ValidTick(threat, completed) ||
                threat.BeamAge + completed <= 60f)
                return default(BeamSample);
            var rawDirection = threat.BeamDirection;
            var axis = rawDirection.Normalized();
            Vec2 origin;
            if (!TryOriginAt(threat, rawDirection, completed, out origin))
                return default(BeamSample);
            float nativeHalfLength;
            float nativeHalfWidth;
            NativeExtents(rawDirection, axis, out nativeHalfLength,
                out nativeHalfWidth);
            var padding = RoundoffPadding(origin, origin,
                Math.Max(nativeHalfLength, nativeHalfWidth));
            var lobe = new BeamLobe { Center = origin, Axis = axis,
                HalfLength = nativeHalfLength + padding,
                HalfWidth = nativeHalfWidth + padding };
            return new BeamSample { Count = 1, First = lobe,
                Bounds = Bounds(lobe) };
        }

        public static BeamSample Sweep(in ThreatSnapshot threat,
            float fromTicks, float toTicks)
        {
            if (!Finite(fromTicks) || !Finite(toTicks) || fromTicks < 0f ||
                toTicks < fromTicks || !ValidSnapshot(threat))
                return default(BeamSample);

            // Resolve the finite native lifetime before converting caller
            // floats to ints.  Besides being constant-time for huge horizons,
            // this avoids the implementation-defined out-of-range float-to-int
            // conversion which could otherwise discard an active current lance.
            var minimum = MinimumDamagingTick(threat);
            var maximum = MaximumValidTick(threat);
            if (maximum < minimum || fromTicks > maximum ||
                toTicks < minimum) return default(BeamSample);
            var first = (int)Math.Ceiling(Math.Max(fromTicks, minimum));
            var last = (int)Math.Floor(Math.Min(toTicks, maximum));
            if (last < first) return default(BeamSample);

            var rawDirection = threat.BeamDirection;
            var axis = rawDirection.Normalized();
            var perpendicular = new Vec2(-axis.Y, axis.X);
            float nativeHalfLength;
            float nativeHalfWidth;
            NativeExtents(rawDirection, axis, out nativeHalfLength,
                out nativeHalfWidth);
            Vec2 firstCenter;
            if (!TryOriginAt(threat, rawDirection, first,
                    out firstCenter)) return default(BeamSample);

            var minimumAlong = -nativeHalfLength;
            var maximumAlong = nativeHalfLength;
            var minimumAcross = -nativeHalfWidth;
            var maximumAcross = nativeHalfWidth;
            var center = firstCenter;
            var step = rawDirection * HalfLength;
            for (var tick = first + 1; tick <= last; tick++)
            {
                // AI_179 installs the same raw cos/sin velocity every update,
                // then XNA performs a float position += velocity.  Repeating
                // that addition is necessary: a closed-form multiply can miss
                // the native center by more than half a pixel at world-scale X.
                center += step;
                if (!Finite(center)) return default(BeamSample);
                var delta = center - firstCenter;
                var along = Vec2.Dot(delta, axis);
                var across = Vec2.Dot(delta, perpendicular);
                if (!Finite(along) || !Finite(across))
                    return default(BeamSample);
                minimumAlong = Math.Min(minimumAlong,
                    along - nativeHalfLength);
                maximumAlong = Math.Max(maximumAlong,
                    along + nativeHalfLength);
                minimumAcross = Math.Min(minimumAcross,
                    across - nativeHalfWidth);
                maximumAcross = Math.Max(maximumAcross,
                    across + nativeHalfWidth);
            }

            var middleAlong = minimumAlong * .5f + maximumAlong * .5f;
            var middleAcross = minimumAcross * .5f + maximumAcross * .5f;
            var sweepCenter = firstCenter + axis * middleAlong +
                perpendicular * middleAcross;
            if (!Finite(sweepCenter)) return default(BeamSample);

            // Reproject the rounded center so each side receives any centering
            // error, then add a magnitude-scaled float roundoff allowance.
            var roundedDelta = sweepCenter - firstCenter;
            var roundedAlong = Vec2.Dot(roundedDelta, axis);
            var roundedAcross = Vec2.Dot(roundedDelta, perpendicular);
            var magnitude = Math.Max(Math.Max(Math.Abs(minimumAlong),
                Math.Abs(maximumAlong)), Math.Max(Math.Abs(minimumAcross),
                Math.Abs(maximumAcross)));
            magnitude = Math.Max(magnitude, CoordinateMagnitude(sweepCenter));
            var padding = RoundoffPadding(firstCenter, center, magnitude);
            var lobe = new BeamLobe
            {
                Center = sweepCenter,
                Axis = axis,
                HalfLength = Math.Max(maximumAlong - roundedAlong,
                    roundedAlong - minimumAlong) + padding,
                HalfWidth = Math.Max(maximumAcross - roundedAcross,
                    roundedAcross - minimumAcross) + padding
            };
            if (!Finite(lobe.HalfLength) || !Finite(lobe.HalfWidth) ||
                lobe.HalfLength <= 0f || lobe.HalfWidth <= 0f)
                return default(BeamSample);
            return new BeamSample { Count = 1, First = lobe,
                Bounds = Bounds(lobe) };
        }

        public static RectF ConservativeBounds(in ThreatSnapshot threat,
            float horizon)
        {
            return !Finite(horizon) || horizon < 0f ? default(RectF) :
                Sweep(threat, 0f, horizon).Bounds;
        }

        private static bool TryOriginAt(in ThreatSnapshot threat,
            Vec2 rawDirection, int completedTicks, out Vec2 origin)
        {
            int movingTicks;
            if (threat.BeamAge >= 60f) movingTicks = completedTicks;
            else
            {
                var firstMovingTick = (int)Math.Ceiling(60f - threat.BeamAge);
                movingTicks = Math.Max(0, completedTicks - firstMovingTick + 1);
            }
            origin = threat.BeamOrigin;
            var step = rawDirection * HalfLength;
            for (var tick = 0; tick < movingTicks; tick++) origin += step;
            return Finite(origin);
        }

        private static void NativeExtents(Vec2 rawDirection, Vec2 axis,
            out float halfLength, out float halfWidth)
        {
            var halfVector = rawDirection * HalfLength;
            var perpendicular = new Vec2(-axis.Y, axis.X);
            halfLength = Math.Max(HalfLength * rawDirection.Length,
                Math.Abs(Vec2.Dot(halfVector, axis)));
            // Mathematically the second term is zero.  Retaining its float
            // residue guarantees that normalization never shaves a raw native
            // endpoint off the represented 8-pixel-wide segment.
            halfWidth = HalfWidth +
                Math.Abs(Vec2.Dot(halfVector, perpendicular));
        }

        private static float RoundoffPadding(Vec2 first, Vec2 last,
            float localMagnitude)
        {
            var magnitude = Math.Max(localMagnitude,
                Math.Max(CoordinateMagnitude(first),
                    CoordinateMagnitude(last)));
            return Math.Max(.00001f, magnitude * RoundoffScale);
        }

        private static float CoordinateMagnitude(Vec2 value) =>
            Math.Max(Math.Abs(value.X), Math.Abs(value.Y));

        internal static bool ValidSnapshot(in ThreatSnapshot threat)
        {
            var directionLengthSquared = threat.BeamDirection.LengthSquared;
            return threat.Kind == ThreatKind.Projectile &&
                threat.Geometry == ThreatGeometry.EmpressLance &&
                threat.Type == 919 && threat.TimeLeft > 0 &&
                Finite(threat.BeamOrigin) && Finite(threat.BeamDirection) &&
                Finite(directionLengthSquared) &&
                directionLengthSquared >= .00001f &&
                Finite(threat.BeamAge) && threat.BeamAge >= 0f &&
                threat.BeamAge < 360f;
        }

        internal static bool RequiresSafetyModel(in ThreatSnapshot threat) =>
            threat.Geometry == ThreatGeometry.EmpressLance;

        private static bool ValidTick(in ThreatSnapshot threat, int ticks) =>
            ticks >= 0 && ticks <= threat.TimeLeft &&
            threat.BeamAge + ticks < 360f;

        private static int MinimumDamagingTick(in ThreatSnapshot threat) =>
            threat.BeamAge > 60f ? 0 :
                (int)Math.Floor(60f - threat.BeamAge) + 1;

        private static int MaximumValidTick(in ThreatSnapshot threat)
        {
            var ageLimit = (int)Math.Ceiling(360f - threat.BeamAge) - 1;
            return Math.Min(threat.TimeLeft, ageLimit);
        }

        private static int CompletedTicks(float ticks) => ticks <= 0f ? 0 :
            (int)Math.Floor(ticks + .00001f);

        private static RectF Bounds(in BeamLobe beam)
        {
            var x = Math.Abs(beam.Axis.X) * beam.HalfLength +
                Math.Abs(beam.Axis.Y) * beam.HalfWidth;
            var y = Math.Abs(beam.Axis.Y) * beam.HalfLength +
                Math.Abs(beam.Axis.X) * beam.HalfWidth;
            return new RectF(beam.Center.X - x, beam.Center.Y - y,
                x * 2f, y * 2f);
        }

        private static bool Finite(Vec2 value) => Finite(value.X) &&
            Finite(value.Y);
        private static bool Finite(float value) => !float.IsNaN(value) &&
            !float.IsInfinity(value);
    }
}
