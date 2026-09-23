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
    /// Terraria 1.4.5.8 hostile projectile motion used by the bounded threat
    /// planner.  The reviewed branches are the Duke Fishron-owned hazard
    /// families and the type 920/921 branches of Projectile.AI_001.  No heap
    /// state is retained or allocated while sampling.
    /// </summary>
    public static class HostileProjectileMotion
    {
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
        private static bool Finite(float value) => !float.IsNaN(value) &&
            !float.IsInfinity(value);
        private static float Clamp01(float value) => Math.Max(0f,
            Math.Min(1f, value));
    }

    /// <summary>
    /// Narrow admission boundary for native hazards whose current hitbox and
    /// linear velocity do not enclose their future damage.  This deliberately
    /// covers only Duke Fishron; it does not make these trajectories
    /// predictable and cannot classify them without a live same-frame Boss
    /// source supplied by the native adapter.
    /// </summary>
    public static class PriorityBossThreatGate
    {
        public const int DukeFishronType = 370;

        public static ThreatTrajectory SourceBoundProjectileTrajectory(
            int projectileType, bool dukeFishronSourceActive)
        {
            var trajectory = HostileProjectileMotion.ForProjectileType(
                projectileType);
            if (trajectory == ThreatTrajectory.UnmodeledDukeFishronHazard &&
                !dukeFishronSourceActive)
                return ThreatTrajectory.Linear;
            return trajectory;
        }

        /// <summary>Only the Fishron-owned shark/tornado contact bodies have a
        /// source-bound native trajectory.  Every other contact stays Linear,
        /// and the family tag requires a live same-frame Fishron source.</summary>
        public static ThreatTrajectory SourceBoundNpcTrajectory(int npcType,
            bool dukeFishronSourceActive)
        {
            if (dukeFishronSourceActive && npcType >= 371 && npcType <= 373)
                return ThreatTrajectory.UnmodeledDukeFishronHazard;
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
            return trajectory == ThreatTrajectory.UnmodeledDukeFishronHazard
                ? DukeFishronType : 0;
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
                        if (threat.Kind == ThreatKind.NpcContact &&
                            threat.Type >= 371 && threat.Type <= 373)
                            continue;
                        reason = "Duke Fishron has an active bubble, shark, or tornado hazard without a proven native trajectory envelope";
                        return true;
                    }
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
    }
}
