using System;

namespace Chaite.Core
{
    public static class InterceptSolver
    {
        public static Vec2 PredictAim(Vec2 origin, Vec2 target, Vec2 targetVelocity, float projectileSpeed, float maxLeadTicks = 90f)
        {
            if (projectileSpeed < 0.1f)
                return target;

            var relative = target - origin;
            var a = targetVelocity.LengthSquared - projectileSpeed * projectileSpeed;
            var b = 2f * Vec2.Dot(relative, targetVelocity);
            var c = relative.LengthSquared;
            float time;

            if (Math.Abs(a) < 0.0001f)
            {
                time = Math.Abs(b) < 0.0001f ? 0f : -c / b;
            }
            else
            {
                var discriminant = b * b - 4f * a * c;
                if (discriminant < 0f)
                    return target;
                var root = (float)Math.Sqrt(discriminant);
                var first = (-b - root) / (2f * a);
                var second = (-b + root) / (2f * a);
                time = SmallestPositive(first, second);
            }

            if (time < 0f || float.IsNaN(time) || float.IsInfinity(time))
                time = 0f;
            if (time > maxLeadTicks)
                time = maxLeadTicks;
            return target + targetVelocity * time;
        }

        private static float SmallestPositive(float a, float b)
        {
            if (a >= 0f && b >= 0f)
                return Math.Min(a, b);
            if (a >= 0f)
                return a;
            return b >= 0f ? b : -1f;
        }
    }
}

