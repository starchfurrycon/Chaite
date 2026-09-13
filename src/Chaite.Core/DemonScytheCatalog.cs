using System;

namespace Chaite.Core
{
    /// <summary>
    /// Hash-locked native contract for Demon Scythe (Item 272 / Projectile
    /// 45) in Terraria 1.4.5.8.  This is intentionally a single-item route,
    /// not a classifier for aiStyle 18 projectiles.
    ///
    /// Projectile 45 begins at the normal item velocity. Its aiStyle 18
    /// increments ai[0] before movement: updates 1..29 retain that velocity,
    /// updates 30..99 multiply it by 1.06 before movement, and update 100
    /// fixes ai[0] at 200 without another multiplier. Its default has no
    /// extra updates and timeLeft 3600. Tile collision remains a live native
    /// outcome; the controller therefore still requires the normal LOS gate
    /// and does not credit collision/penetration effects as guaranteed DPS.
    /// </summary>
    public static class DemonScytheCatalog
    {
        public const int WeaponId = 272;
        public const int ProjectileId = 45;
        public const int ManaCost = 14;
        public const int DefaultDamage = 35;
        public const int UseTime = 20;
        public const int UseAnimation = 20;
        public const int ReuseDelay = 0;
        public const int ExtraUpdates = 0;
        public const int LifetimeSubupdates = 3600;
        public const float ShootSpeed = .2f;
        public const bool AutoReuse = false;
        public const int FirstAcceleratedUpdate = 30;
        public const int LastAcceleratedUpdate = 99;
        public const float VelocityMultiplierPerAcceleratedUpdate = 1.06f;

        public static bool IsWeapon(int weaponId) => weaponId == WeaponId;

        /// <summary>
        /// Sum of the native per-update velocity multipliers through a possibly
        /// fractional update count. Multiplying this by the launch speed gives
        /// the unobstructed center-path distance. Fractional time linearly
        /// interpolates the one native movement segment currently in progress;
        /// no acceleration is invented between two updates.
        /// </summary>
        public static double MovementFactor(double updates)
        {
            if (double.IsNaN(updates) || double.IsInfinity(updates) ||
                updates < 0d)
                return double.NaN;
            var unchangedUpdates = FirstAcceleratedUpdate - 1;
            if (updates <= unchangedUpdates)
                return updates;

            var multiplier = (double)VelocityMultiplierPerAcceleratedUpdate;
            var acceleratedCount = LastAcceleratedUpdate -
                FirstAcceleratedUpdate + 1;
            var capped = Math.Min(updates, LastAcceleratedUpdate);
            var acceleratedUpdates = capped - unchangedUpdates;
            var complete = Math.Floor(acceleratedUpdates);
            var fraction = acceleratedUpdates - complete;
            var acceleratedDistance = GeometricSum(multiplier, complete);
            if (fraction > 0d)
                acceleratedDistance += fraction * Math.Pow(multiplier,
                    complete + 1d);
            if (updates <= LastAcceleratedUpdate)
                return unchangedUpdates + acceleratedDistance;

            return unchangedUpdates + GeometricSum(multiplier,
                acceleratedCount) + (updates - LastAcceleratedUpdate) *
                Math.Pow(multiplier, acceleratedCount);
        }

        /// <summary>
        /// Exact live-state admission for the one reviewed native route.
        /// Damage may be altered by normal player modifiers, but everything
        /// which determines emission or trajectory must still match source.
        /// </summary>
        public static bool TryValidate(WeaponProfileInput input,
            WeaponProfile profile, out WeaponProfileStatus failure)
        {
            failure = WeaponProfileStatus.Supported;
            if (profile == null || profile.Key.WeaponId != WeaponId ||
                profile.Key.AmmoId != 0 ||
                profile.ProjectileId != ProjectileId ||
                profile.Ballistics != WeaponBallisticKind.
                    DemonScytheAcceleration ||
                profile.ResourceKind != OutputResourceKind.Mana ||
                profile.DefaultExtraUpdates != ExtraUpdates ||
                profile.DefaultLifetimeSubupdates != LifetimeSubupdates)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }
            if (input.ProjectileId != ProjectileId ||
                input.ProjectileExtraUpdates != ExtraUpdates ||
                input.ProjectileLifetimeSubupdates != LifetimeSubupdates)
            {
                failure = WeaponProfileStatus.InvalidBallistics;
                return false;
            }
            if (!NearlyEqual(input.WeaponShootSpeed, ShootSpeed) ||
                input.UseTime != UseTime ||
                input.UseAnimation != UseAnimation ||
                input.ReuseDelay != ReuseDelay || input.AutoReuse != AutoReuse)
            {
                failure = WeaponProfileStatus.InvalidTiming;
                return false;
            }
            // Magic ItemCheck_Shoot uses no picked-ammunition branch. A
            // nonzero ammo observation here means the adapter read a different
            // native path and must not reuse this profile.
            if (input.AmmoId != 0 || input.AmmoBaseDamage != 0 ||
                input.AmmoShootSpeed != 0f || !input.HasAmmo)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }
            if (!input.ManaCostKnown || input.ManaCostPerUse != ManaCost)
            {
                failure = WeaponProfileStatus.InvalidResource;
                return false;
            }
            if (input.WeaponDamageAfterModifiers < 1)
            {
                failure = WeaponProfileStatus.InvalidDamage;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Intercepts against the exact piecewise-linear center path induced by
        /// the native discrete updates. Every one-tick segment is solved as a
        /// quadratic, so this path does not sample RNG or guess a constant
        /// speed. It remains an unobstructed primary-path aim solution, not a
        /// hit or Boss-clear guarantee.
        /// </summary>
        public static WeaponAimSolution Solve(WeaponProfileEvaluation weapon,
            Vec2 origin, Vec2 target, Vec2 targetVelocity,
            float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported || weapon.Profile == null ||
                weapon.Profile.Ballistics != WeaponBallisticKind.
                    DemonScytheAcceleration || !Finite(origin) ||
                !Finite(target) || !Finite(targetVelocity) ||
                !WeaponProfileCatalog.FinitePositive(maxLeadTicks) ||
                !WeaponProfileCatalog.FinitePositive(
                    weapon.InitialSpeedPixelsPerSubupdate) ||
                !(weapon.MaxFlightTicks >= 0f) ||
                float.IsNaN(weapon.MaxFlightTicks) ||
                float.IsInfinity(weapon.MaxFlightTicks))
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
                return result;
            }

            var limit = Math.Min((double)weapon.MaxFlightTicks,
                (double)maxLeadTicks);
            if (!(limit > 0d) || double.IsNaN(limit) ||
                double.IsInfinity(limit))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }

            var launchSpeed = (double)weapon.InitialSpeedPixelsPerSubupdate;
            var segments = (int)Math.Ceiling(limit);
            // Do not call Math.Pow per segment on the 60 Hz planning path.
            // This is the native velocity recurrence: update 30 first applies
            // 1.06, updates 30..99 keep multiplying, and every later update
            // reuses the final multiplier.
            var factorAtStart = 0d;
            var velocityFactor = 1d;
            for (var segment = 0; segment < segments; segment++)
            {
                var start = (double)segment;
                var end = Math.Min(start + 1d, limit);
                var nativeUpdate = segment + 1;
                if (nativeUpdate >= FirstAcceleratedUpdate &&
                    nativeUpdate <= LastAcceleratedUpdate)
                    velocityFactor *= VelocityMultiplierPerAcceleratedUpdate;
                var factorAtEnd = factorAtStart + velocityFactor *
                    (end - start);
                if (!(factorAtStart >= 0d) || !(factorAtEnd >=
                    factorAtStart) || !(end > start) ||
                    double.IsNaN(velocityFactor) ||
                    double.IsInfinity(velocityFactor))
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                // In this exact native movement interval, p(t) is a constant
                // launch direction multiplied by a + b*t.
                var b = launchSpeed * (factorAtEnd - factorAtStart) /
                    (end - start);
                var a = launchSpeed * factorAtStart - b * start;
                var quadraticA = vx * vx + vy * vy - b * b;
                var quadraticB = 2d * (x * vx + y * vy - a * b);
                var quadraticC = x * x + y * y - a * a;
                double time;
                if (!TryFirstQuadraticRootInInterval(quadraticA,
                    quadraticB, quadraticC, start, end, out time))
                {
                    factorAtStart = factorAtEnd;
                    continue;
                }

                var aim = new Vec2((float)(target.X + vx * time),
                    (float)(target.Y + vy * time));
                if (!Finite(aim))
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                result.Status = WeaponAimStatus.Ready;
                result.AimWorld = aim;
                result.LeadTicks = (float)time;
                result.SpreadRadiusPixels = 0f;
                return result;
            }

            result.Status = maxLeadTicks < weapon.MaxFlightTicks ?
                WeaponAimStatus.BeyondPredictionHorizon :
                vx * vx + vy * vy <= 1e-12 ?
                    WeaponAimStatus.BeyondLifetime : WeaponAimStatus.NoIntercept;
            return result;
        }

        private static double GeometricSum(double multiplier, double count)
        {
            if (count <= 0d) return 0d;
            return multiplier * (Math.Pow(multiplier, count) - 1d) /
                (multiplier - 1d);
        }

        private static bool TryFirstQuadraticRootInInterval(double a,
            double b, double c, double minimum, double maximum,
            out double root)
        {
            root = 0d;
            var scale = Math.Max(1d, Math.Max(Math.Abs(a),
                Math.Max(Math.Abs(b), Math.Abs(c))));
            var epsilon = scale * 1e-12;
            if (Math.Abs(a) <= epsilon)
            {
                if (Math.Abs(b) <= epsilon)
                    return Math.Abs(c) <= epsilon && Assign(minimum,
                        out root);
                var linear = -c / b;
                return Inside(linear, minimum, maximum) &&
                    Assign(Clamp(linear, minimum, maximum), out root);
            }
            var discriminant = b * b - 4d * a * c;
            var tolerance = 1e-12 * Math.Max(1d, b * b +
                Math.Abs(4d * a * c));
            if (discriminant < -tolerance) return false;
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
            return found && Assign(earliest, out root);
        }

        private static bool Assign(double value, out double root)
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

        private static bool NearlyEqual(float left, float right) =>
            !float.IsNaN(left) && !float.IsInfinity(left) &&
            Math.Abs(left - right) <= .0001f;

        private static bool Finite(Vec2 value) => !float.IsNaN(value.X) &&
            !float.IsInfinity(value.X) && !float.IsNaN(value.Y) &&
            !float.IsInfinity(value.Y);
    }
}
