using System;

namespace Chaite.Core
{
    /// <summary>
    /// Source-locked production contract for Unholy Trident (Item 683 /
    /// Projectile 114) in Terraria 1.4.5.8.  This is deliberately a
    /// single-item route, rather than an aiStyle-27 classifier.
    ///
    /// Projectile 114 runs three native projectile updates per game tick.
    /// Its ai[0] is incremented before movement: updates 1..19 use launch
    /// velocity, then every update from 20 onward multiplies velocity and its
    /// stored launch-speed scalar by 0.98 before movement.  The scalar falls
    /// below one pixel/update on decay 127 (subupdate 146), where native AI
    /// kills the projectile.  The controller therefore credits at most the
    /// first 145 live movement segments, even though SetDefaults timeLeft is
    /// 180.  Tile collision and later hit effects remain native outcomes;
    /// direct DPS credits only the first impact.
    /// </summary>
    public static class UnholyTridentCatalog
    {
        public const int WeaponId = 683;
        public const int ProjectileId = 114;
        public const int DefaultDamage = 150;
        public const int WeakerVariantDamage = 77;
        public const int DefaultManaCost = 19;
        public const int WeakerVariantManaCost = 9;
        public const int UseTime = 27;
        public const int UseAnimation = 27;
        public const int ReuseDelay = 0;
        public const bool AutoReuse = true;
        public const float ShootSpeed = 13f;
        public const int ExtraUpdates = 2;
        public const int UpdatesPerTick = ExtraUpdates + 1;
        public const int LifetimeSubupdates = 180;
        public const int StraightSubupdates = 19;
        public const int FirstDecayedSubupdate = StraightSubupdates + 1;
        public const float VelocityMultiplierPerDecayedSubupdate = .98f;
        public const int FirstKilledSubupdate = 146;
        public const int LastDamagingSubupdate = FirstKilledSubupdate - 1;
        public const float MinimumStoredSpeedBeforeKill = 1f;
        // Same-frame liquid gate limits.  The facade calls it only after an
        // otherwise certified use-item pulse for this exact item/projectile
        // pair; it never scans for ordinary weapons or whole-world state.
        public const float DryTrajectoryGateMaximumPathPixels = 900f;
        public const int DryTrajectoryGateMaximumSamples = 225;
        public const float DryTrajectoryGateSampleSpacingPixels = 4f;
        public const float DryTrajectoryGateLiquidMarginPixels = 16f;
        // Each sample can cover no more than 3 x 3 tile cells at the fixed
        // 16px margin, including both endpoints.
        public const int DryTrajectoryGateMaximumTileReads =
            (DryTrajectoryGateMaximumSamples + 1) * 9;

        public static bool IsWeapon(int weaponId) => weaponId == WeaponId;

        public static bool RequiresDryTrajectoryGate(int weaponId,
            int projectileId) => weaponId == WeaponId &&
                projectileId == ProjectileId;

        /// <summary>
        /// Native SetDefaults has a weaker world variant.  The contract reads
        /// the raw item mana at runtime so it can distinguish that legitimate
        /// variant from an arbitrary resource observation.
        /// </summary>
        public static bool IsKnownBaseManaCost(int mana) =>
            mana == DefaultManaCost || mana == WeakerVariantManaCost;

        /// <summary>
        /// The localAI[0] recurrence used by the native early-despawn gate.
        /// It intentionally uses Single multiplication, matching the native
        /// field, rather than treating timeLeft as the whole usable path.
        /// </summary>
        public static float StoredSpeedAfterDecays(int decays)
        {
            if (decays < 0) return float.NaN;
            var speed = ShootSpeed;
            for (var index = 0; index < decays; index++)
                speed *= VelocityMultiplierPerDecayedSubupdate;
            return speed;
        }

        /// <summary>
        /// Sum of the source-locked velocity multipliers through a possibly
        /// fractional count of live projectile updates.  Fractional time is a
        /// partial native movement segment; no acceleration is invented
        /// between updates.  Values past the early native kill are invalid.
        /// </summary>
        public static double MovementFactor(double subupdates)
        {
            if (double.IsNaN(subupdates) || double.IsInfinity(subupdates) ||
                subupdates < 0d || subupdates > LastDamagingSubupdate)
                return double.NaN;
            if (subupdates <= StraightSubupdates) return subupdates;

            var decayedSubupdates = subupdates - StraightSubupdates;
            var multiplier = (double)VelocityMultiplierPerDecayedSubupdate;
            var complete = Math.Floor(decayedSubupdates);
            var fraction = decayedSubupdates - complete;
            var distance = StraightSubupdates + GeometricSum(multiplier,
                complete);
            if (fraction > 0d)
                distance += fraction * Math.Pow(multiplier, complete + 1d);
            return distance;
        }

        /// <summary>
        /// Validates every live value which controls the reviewed item route.
        /// Item damage remains a live modified value, but raw mana must be one
        /// of the two native variants and the observed effective cost must
        /// match that raw value and the read-only player mana multiplier.
        /// Identity, cadence, resources, and trajectory all fail closed.
        /// </summary>
        public static bool TryValidate(WeaponProfileInput input,
            WeaponProfile profile, out WeaponProfileStatus failure)
        {
            failure = WeaponProfileStatus.Supported;
            if (profile == null || profile.Key.WeaponId != WeaponId ||
                profile.Key.AmmoId != 0 ||
                profile.ProjectileId != ProjectileId ||
                profile.Ballistics != WeaponBallisticKind.
                    UnholyTridentDecay ||
                profile.ResourceKind != OutputResourceKind.Mana ||
                profile.DefaultWeaponShootSpeed != ShootSpeed ||
                profile.DefaultUseTime != UseTime ||
                profile.DefaultUseAnimation != UseAnimation ||
                profile.DefaultReuseDelay != ReuseDelay ||
                profile.DefaultAutoReuse != AutoReuse ||
                profile.DefaultExtraUpdates != ExtraUpdates ||
                profile.DefaultLifetimeSubupdates != LifetimeSubupdates ||
                profile.NativeDamagingUpdateLimit != LastDamagingSubupdate)
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

            // Magic ItemCheck_Shoot has no picked-ammunition contribution.
            // A nonzero ammo observation means a different native route was
            // read and must never be treated as the trident.
            if (input.AmmoId != 0 || input.AmmoBaseDamage != 0 ||
                input.AmmoShootSpeed != 0f || !input.HasAmmo)
            {
                failure = WeaponProfileStatus.ProjectileMismatch;
                return false;
            }

            int expectedMana;
            if (!input.ManaCostKnown || !input.ManaBaseCostKnown ||
                !input.ManaCostMultiplierKnown ||
                !IsKnownBaseManaCost(input.ManaBaseCost) ||
                !TryScaledManaCost(input.ManaBaseCost,
                    input.ManaCostMultiplier, out expectedMana) ||
                input.ManaCostPerUse != expectedMana)
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
        /// Intercepts against the exact native delayed-decay center path.  The
        /// target velocity supplied by combat code is per game tick, whereas
        /// native projectile AI advances three times per tick, so this solver
        /// works in subupdates and converts its answer back to game ticks.
        /// It is an unobstructed first-impact aim solution, not a hit or Boss
        /// clear guarantee.
        /// </summary>
        public static WeaponAimSolution Solve(WeaponProfileEvaluation weapon,
            Vec2 origin, Vec2 target, Vec2 targetVelocity,
            float maxLeadTicks)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported || weapon.Profile == null ||
                weapon.Profile.Ballistics != WeaponBallisticKind.
                    UnholyTridentDecay ||
                weapon.FirstTickProjectileUpdates != UpdatesPerTick ||
                weapon.SustainedProjectileUpdatesPerTick != UpdatesPerTick ||
                !NearlyEqual(weapon.InitialSpeedPixelsPerSubupdate,
                    ShootSpeed) || !NearlyEqual(weapon.MaxFlightTicks,
                    LastDamagingSubupdate / (float)UpdatesPerTick) ||
                !Finite(origin) || !Finite(target) || !Finite(targetVelocity) ||
                !WeaponProfileCatalog.FinitePositive(maxLeadTicks))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }

            var x = (double)target.X - origin.X;
            var y = (double)target.Y - origin.Y;
            if (x == 0d && y == 0d)
            {
                result.Status = WeaponAimStatus.Ready;
                result.LeadTicks = 0f;
                return result;
            }

            var limitTicks = Math.Min((double)weapon.MaxFlightTicks,
                (double)maxLeadTicks);
            var limit = Math.Min((double)LastDamagingSubupdate,
                limitTicks * UpdatesPerTick);
            if (!(limit > 0d) || double.IsNaN(limit) ||
                double.IsInfinity(limit))
            {
                result.Status = WeaponAimStatus.InvalidInput;
                return result;
            }

            // Target velocity is observed once per game tick; each native
            // projectile update only sees one third of that displacement.
            var vx = (double)targetVelocity.X / UpdatesPerTick;
            var vy = (double)targetVelocity.Y / UpdatesPerTick;
            var launchSpeed = (double)weapon.InitialSpeedPixelsPerSubupdate;
            var segments = (int)Math.Ceiling(limit);
            var factorAtStart = 0d;
            var velocityFactor = 1d;
            for (var segment = 0; segment < segments; segment++)
            {
                var start = (double)segment;
                var end = Math.Min(start + 1d, limit);
                var nativeSubupdate = segment + 1;
                if (nativeSubupdate >= FirstDecayedSubupdate)
                    velocityFactor *= VelocityMultiplierPerDecayedSubupdate;
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

                // Within a reviewed subupdate, p(t) is a fixed launch
                // direction times a + b*t.  Solve that exact interval rather
                // than sampling an ordinary constant-speed line.
                var b = launchSpeed * (factorAtEnd - factorAtStart) /
                    (end - start);
                var a = launchSpeed * factorAtStart - b * start;
                var quadraticA = vx * vx + vy * vy - b * b;
                var quadraticB = 2d * (x * vx + y * vy - a * b);
                var quadraticC = x * x + y * y - a * a;
                double subupdateTime;
                if (!TryFirstQuadraticRootInInterval(quadraticA,
                    quadraticB, quadraticC, start, end,
                    out subupdateTime))
                {
                    factorAtStart = factorAtEnd;
                    continue;
                }

                var leadTicks = subupdateTime / UpdatesPerTick;
                var aim = new Vec2((float)(target.X +
                    targetVelocity.X * leadTicks), (float)(target.Y +
                    targetVelocity.Y * leadTicks));
                if (!Finite(aim))
                {
                    result.Status = WeaponAimStatus.InvalidInput;
                    return result;
                }
                result.Status = WeaponAimStatus.Ready;
                result.AimWorld = aim;
                result.LeadTicks = (float)leadTicks;
                result.SpreadRadiusPixels = 0f;
                return result;
            }

            result.Status = maxLeadTicks < weapon.MaxFlightTicks ?
                WeaponAimStatus.BeyondPredictionHorizon :
                targetVelocity.X * targetVelocity.X +
                    targetVelocity.Y * targetVelocity.Y <= 1e-12f ?
                    WeaponAimStatus.BeyondLifetime : WeaponAimStatus.NoIntercept;
            return result;
        }

        private static bool TryScaledManaCost(int rawMana, float multiplier,
            out int scaledMana)
        {
            scaledMana = -1;
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) ||
                multiplier < 0f)
                return false;
            var scaled = rawMana * multiplier;
            if (float.IsNaN(scaled) || float.IsInfinity(scaled) ||
                scaled < 0f || scaled >= int.MaxValue)
                return false;
            scaledMana = (int)scaled;
            return true;
        }

        private static double GeometricSum(double multiplier, double count)
        {
            if (count <= 0d) return 0d;
            return multiplier * (1d - Math.Pow(multiplier, count)) /
                (1d - multiplier);
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
