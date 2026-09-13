using System;

namespace Chaite.Core
{
    public enum WeaponAimStatus
    {
        UnsupportedWeapon,
        Ready,
        InvalidInput,
        NoIntercept,
        BeyondLifetime,
        BeyondPredictionHorizon,
        SpreadEnvelopeOutsideTarget
    }

    public struct WeaponAimSolution
    {
        public WeaponAimStatus Status;
        public Vec2 AimWorld;
        public float LeadTicks;
        public float SpreadRadiusPixels;
        public bool CanFire => Status == WeaponAimStatus.Ready;
    }

    /// <summary>
    /// Zero-allocation direct-primary intercept for explicitly reviewed pairs.
    /// No RNG simulation, homing fallback, weapon switching or input calls.
    /// The caller must still check native LOS and actual weapon-use eligibility.
    /// Origin must be the expected native shot origin. A pre-physics Player.Center
    /// is an approximation: player movement, MountedCenter, gfxOffY and item-use
    /// animation can change it before ItemCheck_Shoot. Likewise the caller owns
    /// projecting AnimationRemainingAtShot across the native update boundary.
    /// Random component spread is reported as uncertainty, never "cancelled" by
    /// consuming the game's RNG. The Crystal Storm overload receiving the live
    /// target hitbox admits fire only when its complete worst-case displacement
    /// envelope fits in a conservative circle inside that hitbox. Accelerating
    /// targets and liquid paths are not modeled; this is still not a general
    /// hit-probability or safe-shot guarantee.
    /// </summary>
    public static class WeaponAimSolver
    {
        public static WeaponAimSolution Solve(WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks = 90f)
        {
            // Compatibility policy for non-production callers which do not yet
            // carry target dimensions. CombatPlanner uses the hitbox overload.
            return SolveCore(weapon, origin, target, targetVelocity,
                maxLeadTicks, 16f);
        }

        public static WeaponAimSolution Solve(WeaponProfileEvaluation weapon,
            Vec2 origin, Vec2 target, Vec2 targetVelocity,
            float maxLeadTicks, int targetWidthPixels, int targetHeightPixels)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported) return result;
            if (CommonWeaponOutputCatalog.RequiresTargetHitbox(
                    weapon.Profile))
            {
                if (targetWidthPixels < 1 || targetHeightPixels < 1)
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                return CommonWeaponOutputCatalog.SolveAim(weapon, origin,
                    target, targetVelocity, maxLeadTicks, targetWidthPixels,
                    targetHeightPixels);
            }
            if (weapon.Profile.Ballistics !=
                WeaponBallisticKind.ExponentialDragPrimaryProjectile)
                return SolveCore(weapon, origin, target, targetVelocity,
                    maxLeadTicks, float.PositiveInfinity);
            if (targetWidthPixels < 1 || targetHeightPixels < 1)
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }
            // Keeping the entire projectile collision disk within the target's
            // inscribed circle is stricter than merely intersecting its AABB.
            var safeRadius = Math.Min(targetWidthPixels, targetHeightPixels) *
                .5f - weapon.Profile.ProjectileSafetyRadiusPixels;
            return SolveCore(weapon, origin, target, targetVelocity,
                maxLeadTicks, safeRadius);
        }

        private static WeaponAimSolution SolveCore(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks,
            float conservativeSpreadRadiusPixels)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported) return result;
            if (!Finite(origin) || !Finite(target) || !Finite(targetVelocity) ||
                !WeaponProfileCatalog.FinitePositive(weapon.SpeedPixelsPerTick) ||
                !WeaponProfileCatalog.FinitePositive(weapon.MaxFlightTicks) ||
                !WeaponProfileCatalog.FiniteNonnegative(weapon.SpreadSpeedPixelsPerTick) ||
                !WeaponProfileCatalog.FinitePositive(maxLeadTicks))
            { result.Status = WeaponAimStatus.InvalidInput; return result; }

            if (weapon.Profile.Ballistics ==
                WeaponBallisticKind.ExponentialDragPrimaryProjectile)
                return SolveExponentialDrag(weapon, origin, target,
                    targetVelocity, maxLeadTicks,
                    conservativeSpreadRadiusPixels);
            if (weapon.Profile.Ballistics == WeaponBallisticKind.
                DiscreteVerticalAccelerationPrimaryProjectile)
                return SolveDiscreteVerticalAcceleration(weapon, origin,
                    target, targetVelocity, maxLeadTicks);
            if (weapon.Profile.Ballistics == WeaponBallisticKind.RocketAcceleration)
                return SolveRocketAcceleration(weapon, origin, target,
                    targetVelocity, maxLeadTicks);
            if (weapon.Profile.Ballistics == WeaponBallisticKind.
                DemonScytheAcceleration)
                return DemonScytheCatalog.Solve(weapon, origin, target,
                    targetVelocity, maxLeadTicks);
            if (weapon.Profile.Ballistics == WeaponBallisticKind.
                UnholyTridentDecay)
                return UnholyTridentCatalog.Solve(weapon, origin, target,
                    targetVelocity, maxLeadTicks);
            if (CommonWeaponOutputCatalog.IsCommonProfile(weapon.Profile))
                return CommonWeaponOutputCatalog.SolveAim(weapon, origin,
                    target, targetVelocity, maxLeadTicks);
            if (MeleeProjectileCatalog.IsMeleeProfile(weapon.Profile))
                return MeleeProjectileCatalog.SolveAim(weapon, origin, target,
                    targetVelocity, maxLeadTicks);
            if (weapon.Profile.Ballistics !=
                    WeaponBallisticKind.StraightPrimaryProjectile &&
                weapon.Profile.Ballistics !=
                WeaponBallisticKind.ConservativeStraightPrefix)
                return result;

            var x = (double)target.X - origin.X;
            var y = (double)target.Y - origin.Y;
            var vx = (double)targetVelocity.X;
            var vy = (double)targetVelocity.Y;
            var speed = (double)weapon.SpeedPixelsPerTick;
            var a = vx * vx + vy * vy - speed * speed;
            var b = 2d * (x * vx + y * vy);
            var c = x * x + y * y;
            double time;
            if (c == 0d) time = 0d;
            else if (Math.Abs(a) <= 1e-10 * Math.Max(1d, speed * speed))
                time = b < 0d ? -c / b : -1d;
            else
            {
                var discriminant = b * b - 4d * a * c;
                if (discriminant < 0d) { result.Status = WeaponAimStatus.NoIntercept; return result; }
                // Numerically stable quadratic roots; no positive root means
                // don't fire, rather than aim at an impossible current position.
                var root = Math.Sqrt(discriminant);
                var q = -.5d * (b + (b >= 0d ? root : -root));
                var first = q / a;
                var second = q == 0d ? -1d : c / q;
                time = first >= 0d && second >= 0d ? Math.Min(first, second) : first >= 0d ? first : second;
            }
            if (time < 0d || double.IsNaN(time) || double.IsInfinity(time))
            { result.Status = WeaponAimStatus.NoIntercept; return result; }
            if (time > weapon.MaxFlightTicks)
            { result.Status = WeaponAimStatus.BeyondLifetime; return result; }
            if (time > maxLeadTicks)
            { result.Status = WeaponAimStatus.BeyondPredictionHorizon; return result; }
            var aim = new Vec2((float)(target.X + vx * time), (float)(target.Y + vy * time));
            var spread = (float)(weapon.SpreadSpeedPixelsPerTick * time);
            if (!Finite(aim) || !WeaponProfileCatalog.FiniteNonnegative(spread))
            { result.Status = WeaponAimStatus.InvalidInput; return result; }
            result.Status = WeaponAimStatus.Ready;
            result.AimWorld = aim;
            result.LeadTicks = (float)time;
            result.SpreadRadiusPixels = spread;
            return result;
        }

        // Rocket I accelerates each velocity component by 1.1 while both
        // components remain below 15 px/update. A short fixed-point trace is
        // deterministic and allocation-free, avoiding a constant-speed guess.
        private static WeaponAimSolution SolveRocketAcceleration(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            var speed = weapon.InitialSpeedPixelsPerSubupdate;
            if (!(speed > 0f) || !Finite(origin) || !Finite(target) ||
                !Finite(targetVelocity) || !WeaponProfileCatalog.FinitePositive(maxLeadTicks))
            { result.Status = WeaponAimStatus.InvalidInput; return result; }
            var limit = Math.Min(weapon.MaxFlightTicks, maxLeadTicks);
            if (!(limit >= 0f)) { result.Status = WeaponAimStatus.InvalidInput; return result; }
            var aim = target;
            var lead = 0;
            for (var iteration = 0; iteration < 5; iteration++)
            {
                var delta = aim - origin;
                if (delta.LengthSquared <= .0001f) { lead = 0; break; }
                var direction = delta.Normalized();
                var velocity = direction * speed;
                var position = origin;
                var bestError = float.PositiveInfinity;
                var bestTick = -1;
                var ticks = (int)Math.Min(180f, Math.Floor(limit));
                for (var tick = 1; tick <= ticks; tick++)
                {
                    if (Math.Abs(velocity.X) < 15f && Math.Abs(velocity.Y) < 15f)
                        velocity *= 1.1f;
                    position += velocity;
                    var movingTarget = target + targetVelocity * tick;
                    var error = Vec2.DistanceSquared(position, movingTarget);
                    if (error < bestError)
                    { bestError = error; bestTick = tick; }
                }
                if (bestTick < 0) { result.Status = WeaponAimStatus.BeyondLifetime; return result; }
                lead = bestTick;
                aim = target + targetVelocity * lead;
            }
            result.Status = WeaponAimStatus.Ready;
            result.AimWorld = aim;
            result.LeadTicks = lead;
            result.SpreadRadiusPixels = weapon.SpreadSpeedPixelsPerTick * lead;
            return result;
        }

        private static WeaponAimSolution SolveDiscreteVerticalAcceleration(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            var speed = (double)weapon.InitialSpeedPixelsPerSubupdate;
            var spreadPerSubupdate =
                (double)weapon.SpreadSpeedPixelsPerSubupdate;
            var firstUpdates = weapon.FirstTickProjectileUpdates;
            var sustainedUpdates =
                weapon.SustainedProjectileUpdatesPerTick;
            var acceleration =
                (double)weapon.Profile.VerticalAccelerationPerSubupdate;
            var delay = weapon.Profile.VerticalAccelerationDelaySubupdates;
            if (!(speed > 0d) || double.IsNaN(speed) ||
                double.IsInfinity(speed) || spreadPerSubupdate < 0d ||
                double.IsNaN(spreadPerSubupdate) ||
                double.IsInfinity(spreadPerSubupdate) ||
                firstUpdates < 1 || sustainedUpdates < 1 || delay < 0 ||
                double.IsNaN(acceleration) || double.IsInfinity(acceleration))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }

            var x = (double)target.X - origin.X;
            var y = (double)target.Y - origin.Y;
            var vx = (double)targetVelocity.X;
            var vy = (double)targetVelocity.Y;
            if (x == 0d && y == 0d)
            {
                result.Status = WeaponAimStatus.Ready;
                result.LeadTicks = 0f;
                result.SpreadRadiusPixels = 0f;
                return result;
            }

            var lifetime = (double)weapon.MaxFlightTicks;
            var searchLimit = Math.Min(lifetime, (double)maxLeadTicks);
            double time;
            if (!TryFindFirstDiscreteIntercept(x, y, vx, vy, speed,
                delay, acceleration, firstUpdates, sustainedUpdates,
                searchLimit, out time))
            {
                result.Status = maxLeadTicks < weapon.MaxFlightTicks ?
                    WeaponAimStatus.BeyondPredictionHorizon :
                    (vx * vx + vy * vy <= 1e-12 ?
                        WeaponAimStatus.BeyondLifetime :
                        WeaponAimStatus.NoIntercept);
                return result;
            }

            var subupdates = SubupdatesAtTick(time, firstUpdates,
                sustainedUpdates);
            var gravityDisplacement = VerticalDisplacement(subupdates,
                delay, acceleration);
            var aim = new Vec2((float)(target.X + vx * time),
                (float)(target.Y + vy * time - gravityDisplacement));
            var spread = (float)(spreadPerSubupdate * subupdates);
            if (!Finite(aim) ||
                !WeaponProfileCatalog.FiniteNonnegative(spread))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }
            result.Status = WeaponAimStatus.Ready;
            result.AimWorld = aim;
            result.LeadTicks = (float)time;
            result.SpreadRadiusPixels = spread;
            return result;
        }

        private static bool TryFindFirstDiscreteIntercept(double x,
            double y, double vx, double vy, double speed, int delay,
            double acceleration, int firstUpdates, int sustainedUpdates,
            double tickLimit, out double time)
        {
            time = 0d;
            if (!(tickLimit >= 0d) || double.IsNaN(tickLimit) ||
                double.IsInfinity(tickLimit))
                return false;
            var maximumSubupdates = SubupdatesAtTick(tickLimit,
                firstUpdates, sustainedUpdates);
            var segmentCount = (int)Math.Ceiling(maximumSubupdates);
            for (var segment = 0; segment < segmentCount; segment++)
            {
                var u0 = (double)segment;
                var u1 = Math.Min(u0 + 1d, maximumSubupdates);
                var t0 = TickAtSubupdates(u0, firstUpdates,
                    sustainedUpdates);
                var t1 = TickAtSubupdates(u1, firstUpdates,
                    sustainedUpdates);
                if (!(t1 > t0)) continue;

                // Within one native projectile-update interval both u(t) and
                // the delayed acceleration displacement are affine. Squaring
                // |target(t)-gravity(u)| = speed*u therefore gives an exact
                // quadratic, avoiding iterative simulation or RNG use.
                var uRate = (u1 - u0) / (t1 - t0);
                var uIntercept = u0 - uRate * t0;
                var g0 = VerticalDisplacement(u0, delay, acceleration);
                var g1 = VerticalDisplacement(u1, delay, acceleration);
                var gRate = (g1 - g0) / (t1 - t0);
                var gIntercept = g0 - gRate * t0;
                var targetYIntercept = y - gIntercept;
                var targetYRate = vy - gRate;
                var a = vx * vx + targetYRate * targetYRate -
                    speed * speed * uRate * uRate;
                var b = 2d * (x * vx + targetYIntercept * targetYRate -
                    speed * speed * uIntercept * uRate);
                var c = x * x + targetYIntercept * targetYIntercept -
                    speed * speed * uIntercept * uIntercept;
                if (TryFirstQuadraticRootInInterval(a, b, c, t0, t1,
                    out time))
                    return true;
            }
            return false;
        }

        private static bool TryFirstQuadraticRootInInterval(double a,
            double b, double c, double minimum, double maximum,
            out double root)
        {
            root = 0d;
            var coefficientScale = Math.Max(1d,
                Math.Max(Math.Abs(a), Math.Max(Math.Abs(b), Math.Abs(c))));
            var epsilon = coefficientScale * 1e-12;
            if (Math.Abs(a) <= epsilon)
            {
                if (Math.Abs(b) <= epsilon) return Math.Abs(c) <= epsilon &&
                    AssignRoot(minimum, out root);
                var linear = -c / b;
                if (Inside(linear, minimum, maximum))
                    return AssignRoot(Clamp(linear, minimum, maximum),
                        out root);
                return false;
            }
            var discriminant = b * b - 4d * a * c;
            var discriminantTolerance = 1e-12 * Math.Max(1d,
                b * b + Math.Abs(4d * a * c));
            if (discriminant < -discriminantTolerance) return false;
            if (discriminant < 0d) discriminant = 0d;
            var squareRoot = Math.Sqrt(discriminant);
            var q = -.5d * (b + (b >= 0d ? squareRoot : -squareRoot));
            var first = q / a;
            var second = q == 0d ? -b / (2d * a) : c / q;
            var found = false;
            var earliest = double.PositiveInfinity;
            if (Inside(first, minimum, maximum))
            {
                earliest = Clamp(first, minimum, maximum);
                found = true;
            }
            if (Inside(second, minimum, maximum))
            {
                second = Clamp(second, minimum, maximum);
                if (!found || second < earliest) earliest = second;
                found = true;
            }
            return found && AssignRoot(earliest, out root);
        }

        private static bool AssignRoot(double value, out double root)
        {
            root = value;
            return !double.IsNaN(value) && !double.IsInfinity(value) &&
                value >= 0d;
        }

        private static bool Inside(double value, double minimum,
            double maximum)
        {
            const double tolerance = 1e-9;
            return !double.IsNaN(value) && !double.IsInfinity(value) &&
                value >= minimum - tolerance && value <= maximum + tolerance;
        }

        private static double Clamp(double value, double minimum,
            double maximum) => value < minimum ? minimum :
                value > maximum ? maximum : value;

        private static double SubupdatesAtTick(double tick,
            int firstUpdates, int sustainedUpdates) => tick <= 1d ?
                tick * firstUpdates : firstUpdates +
                    (tick - 1d) * sustainedUpdates;

        private static double TickAtSubupdates(double subupdates,
            int firstUpdates, int sustainedUpdates) =>
            subupdates <= firstUpdates ? subupdates / firstUpdates :
                1d + (subupdates - firstUpdates) / sustainedUpdates;

        private static double VerticalDisplacement(double subupdates,
            int delay, double acceleration)
        {
            if (acceleration == 0d || subupdates <= delay) return 0d;
            var completeUpdates = Math.Floor(subupdates);
            var fraction = subupdates - completeUpdates;
            var acceleratedUpdates = Math.Max(0d,
                completeUpdates - delay);
            return acceleration * acceleratedUpdates *
                (acceleratedUpdates + 1d) * .5d + fraction * acceleration *
                (acceleratedUpdates + 1d);
        }

        private static WeaponAimSolution SolveExponentialDrag(
            WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks,
            float conservativeSpreadRadiusPixels)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            var retention = (double)weapon.Profile.VelocityRetentionPerUpdate;
            if (!(retention > 0d && retention < 1d) ||
                double.IsNaN(conservativeSpreadRadiusPixels) ||
                double.IsInfinity(conservativeSpreadRadiusPixels))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }

            var x = (double)target.X - origin.X;
            var y = (double)target.Y - origin.Y;
            var vx = (double)targetVelocity.X;
            var vy = (double)targetVelocity.Y;
            var speed = (double)weapon.SpeedPixelsPerTick;
            var lifetime = (double)weapon.MaxFlightTicks;
            double time;
            if (x == 0d && y == 0d)
                time = 0d;
            else if (!TryFindFirstDragIntercept(x, y, vx, vy, speed,
                retention, lifetime, out time))
            {
                // For a stationary target, distinguish an intercept which the
                // infinite drag series could reach only after native despawn.
                var distance = Math.Sqrt(x * x + y * y);
                var asymptoticRange = speed * retention / (1d - retention);
                result.Status = vx * vx + vy * vy <= 1e-12 &&
                    distance <= asymptoticRange ?
                    WeaponAimStatus.BeyondLifetime :
                    WeaponAimStatus.NoIntercept;
                return result;
            }

            if (time > maxLeadTicks)
            {
                result.Status = WeaponAimStatus.BeyondPredictionHorizon;
                return result;
            }
            var aim = new Vec2((float)(target.X + vx * time),
                (float)(target.Y + vy * time));
            var movementFactor = DragMovementFactor(retention, time);
            var spread = (float)(weapon.SpreadSpeedPixelsPerTick *
                movementFactor);
            if (!Finite(aim) ||
                !WeaponProfileCatalog.FiniteNonnegative(spread))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }
            result.AimWorld = aim;
            result.LeadTicks = (float)time;
            result.SpreadRadiusPixels = spread;
            if (spread > conservativeSpreadRadiusPixels)
            {
                result.Status = WeaponAimStatus.SpreadEnvelopeOutsideTarget;
                return result;
            }
            result.Status = WeaponAimStatus.Ready;
            return result;
        }

        private static bool TryFindFirstDragIntercept(double x, double y,
            double vx, double vy, double speed, double retention,
            double lifetime, out double time)
        {
            // |p+v*t| is convex and -A(1-r^t) is convex for 0<r<1, so
            // their sum has one minimum. Locate it with a fixed iteration
            // budget, then bisect the first root. This is deterministic and
            // allocation-free on the 60 Hz planning path.
            var left = 0d;
            var right = lifetime;
            for (var iteration = 0; iteration < 56; iteration++)
            {
                var third = (right - left) / 3d;
                var first = left + third;
                var second = right - third;
                if (DragGap(x, y, vx, vy, speed, retention, first) <=
                    DragGap(x, y, vx, vy, speed, retention, second))
                    right = second;
                else
                    left = first;
            }
            var minimumTime = (left + right) * .5d;
            var minimumGap = DragGap(x, y, vx, vy, speed, retention,
                minimumTime);
            var endGap = DragGap(x, y, vx, vy, speed, retention, lifetime);
            if (endGap < minimumGap)
            {
                minimumGap = endGap;
                minimumTime = lifetime;
            }
            if (minimumGap > 0d)
            {
                time = 0d;
                return false;
            }

            left = 0d;
            right = minimumTime;
            for (var iteration = 0; iteration < 56; iteration++)
            {
                var middle = (left + right) * .5d;
                if (DragGap(x, y, vx, vy, speed, retention, middle) > 0d)
                    left = middle;
                else
                    right = middle;
            }
            time = right;
            return !double.IsNaN(time) && !double.IsInfinity(time);
        }

        private static double DragGap(double x, double y, double vx,
            double vy, double speed, double retention, double time)
        {
            var targetX = x + vx * time;
            var targetY = y + vy * time;
            return Math.Sqrt(targetX * targetX + targetY * targetY) -
                speed * DragMovementFactor(retention, time);
        }

        private static double DragMovementFactor(double retention,
            double time) => retention * (1d - Math.Pow(retention, time)) /
                (1d - retention);

        private static bool Finite(Vec2 value) => !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }
}
