using System;

namespace Chaite.Core
{
    public enum WeaponAimStatus { UnsupportedWeapon, Ready, InvalidInput, NoIntercept, BeyondLifetime, BeyondPredictionHorizon }

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
    /// No RNG simulation, gravity/homing fallback, weapon switching or input calls.
    /// The caller must still check native LOS and actual weapon-use eligibility.
    /// Origin must be the expected native shot origin. A pre-physics Player.Center
    /// is an approximation: player movement, MountedCenter, gfxOffY and item-use
    /// animation can change it before ItemCheck_Shoot. Likewise the caller owns
    /// projecting AnimationRemainingAtShot across the native update boundary.
    /// Random component spread is reported as uncertainty, never "cancelled" by
    /// consuming the game's RNG. Accelerating targets and liquid paths are not
    /// modeled; this solver is not a hit-probability or safe-shot guarantee.
    /// </summary>
    public static class WeaponAimSolver
    {
        public static WeaponAimSolution Solve(WeaponProfileEvaluation weapon, Vec2 origin, Vec2 target,
            Vec2 targetVelocity, float maxLeadTicks = 90f)
        {
            var result = new WeaponAimSolution { AimWorld = target };
            if (!weapon.IsSupported) return result;
            if (!Finite(origin) || !Finite(target) || !Finite(targetVelocity) ||
                !WeaponProfileCatalog.FinitePositive(weapon.SpeedPixelsPerTick) ||
                !WeaponProfileCatalog.FinitePositive(weapon.MaxFlightTicks) ||
                !WeaponProfileCatalog.FiniteNonnegative(weapon.SpreadSpeedPixelsPerTick) ||
                !WeaponProfileCatalog.FinitePositive(maxLeadTicks))
            { result.Status = WeaponAimStatus.InvalidInput; return result; }

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

        private static bool Finite(Vec2 value) => !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y);
    }
}
