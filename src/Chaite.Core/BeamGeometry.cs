using System;

namespace Chaite.Core
{
    public struct BeamLobe
    {
        public Vec2 Center;
        public Vec2 Axis;
        public float HalfLength;
        public float HalfWidth;
    }

    public struct BeamSample
    {
        public int Count;
        public BeamLobe First;
        public BeamLobe Second;
        public BeamLobe Third;
        public RectF Bounds;
        public bool Active => Count > 0;
    }

    /// <summary>
    /// Version-locked 1.4.5.8 beam geometry. Instant samples reproduce the native
    /// thick segments without its per-query arrays/trigonometry. Future source
    /// motion is a local linear estimate, not an exact replay of the owner's AI.
    /// </summary>
    public static class BeamGeometry
    {
        private const float SunSweep = .3490659f;

        public static BeamSample AtTime(in ThreatSnapshot threat, float ticks)
        {
            var age = threat.BeamAge + ticks;
            if (threat.Geometry == ThreatGeometry.Body || age < 0f || age >= 180f ||
                threat.TimeLeft > 0 && ticks >= threat.TimeLeft) return default(BeamSample);
            var scale = Scale(threat, ticks);
            if (!Active(threat, age) || scale <= 0f)
                return WithDeathrayBody(threat, default(BeamSample), ticks, ticks);
            var angle = Angle(threat, ticks);
            var direction = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            // Preserve the observed Moon Lord unit vector at t=0 to avoid introducing
            // atan/sin round-trip differences into the actual collision geometry.
            if (ticks == 0f && threat.Geometry == ThreatGeometry.MoonLordDeathray)
                direction = SafeDirection(threat.BeamDirection);
            var origin = threat.BeamOrigin + threat.BeamSourceVelocity * ticks;
            return WithDeathrayBody(threat, Create(threat, origin, direction, scale, Length(threat, ticks), 0f, 0f, 0f), ticks, ticks);
        }

        public static BeamSample Sweep(in ThreatSnapshot threat, float fromTicks, float toTicks)
        {
            var warmup = threat.Geometry == ThreatGeometry.MoonLordDeathray ? 20f : 61f;
            var from = Math.Max(fromTicks, Math.Max(0f, -threat.BeamAge));
            var to = Math.Min(toTicks, 179f - threat.BeamAge);
            if (threat.TimeLeft > 0) to = Math.Min(to, threat.TimeLeft - 1f);
            if (to < from || threat.Geometry == ThreatGeometry.Body) return default(BeamSample);
            if (to == from) return AtTime(threat, from);
            var sourceFrom = from;
            from = Math.Max(from, warmup - threat.BeamAge);
            if (to < from) return WithDeathrayBody(threat, default(BeamSample), sourceFrom, to);
            var middle = (from + to) * .5f;
            var angleFrom = Angle(threat, from);
            var angleTo = Angle(threat, to);
            var halfAngle = Math.Abs(angleTo - angleFrom) * .5f;
            var angle = (angleFrom + angleTo) * .5f;
            var axis = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
            var scale = Math.Max(Scale(threat, from), Scale(threat, to));
            var peakTick = threat.Geometry == ThreatGeometry.MoonLordDeathray ? 90f - threat.BeamAge : 120f - threat.BeamAge;
            if (peakTick >= from && peakTick <= to) scale = Math.Max(scale, Scale(threat, peakTick));
            var origin = threat.BeamOrigin + threat.BeamSourceVelocity * middle;
            var length = Length(threat, to);
            var sourceMovement = threat.BeamSourceVelocity * ((to - from) * .5f);
            var parallel = Math.Abs(Vec2.Dot(sourceMovement, axis));
            var perpendicular = Math.Abs(sourceMovement.X * axis.Y - sourceMovement.Y * axis.X);
            if (halfAngle >= Math.PI * .5f)
            {
                // Unusual/custom angular rates: enclose the full sector rather than
                // quietly aliasing a >180-degree sweep to a narrow midpoint beam.
                var radius = (threat.Geometry == ThreatGeometry.MoonLordDeathray ? length + 18f * scale : 835f * scale)
                    + sourceMovement.Length;
                var lobe = new BeamLobe { Center = origin, Axis = new Vec2(1, 0), HalfLength = radius, HalfWidth = radius };
                return WithDeathrayBody(threat, new BeamSample { Count = 1, First = lobe, Bounds = Bounds(lobe) }, sourceFrom, to);
            }
            return WithDeathrayBody(threat, Create(threat, origin, axis, scale, length, (float)Math.Sin(halfAngle), parallel, perpendicular), sourceFrom, to);
        }

        public static RectF ConservativeBounds(in ThreatSnapshot threat, float horizon)
        {
            var travel = threat.BeamSourceVelocity * horizon;
            var reach = threat.Geometry == ThreatGeometry.MoonLordDeathray ? 2440f : 840f * Math.Max(1f, threat.BeamScale);
            return new RectF(threat.BeamOrigin.X + Math.Min(0f, travel.X) - reach,
                threat.BeamOrigin.Y + Math.Min(0f, travel.Y) - reach,
                Math.Abs(travel.X) + reach * 2f, Math.Abs(travel.Y) + reach * 2f);
        }

        public static bool Intersects(in RectF player, in BeamSample beam, float safetyMargin = 0f)
        {
            return beam.Count > 0 && (Intersects(player, beam.First, safetyMargin) ||
                beam.Count > 1 && Intersects(player, beam.Second, safetyMargin) ||
                beam.Count > 2 && Intersects(player, beam.Third, safetyMargin));
        }

        public static float SeparationSquared(in RectF player, in BeamSample beam)
        {
            if (!beam.Active) return float.MaxValue;
            var best = Separation(player, beam.First);
            if (beam.Count > 1) best = Math.Min(best, Separation(player, beam.Second));
            if (beam.Count > 2) best = Math.Min(best, Separation(player, beam.Third));
            return best;
        }

        private static bool Active(in ThreatSnapshot threat, float age) => threat.Geometry != ThreatGeometry.Body &&
            age < 180f && age >= (threat.Geometry == ThreatGeometry.MoonLordDeathray ? 20f : 61f);

        private static float Scale(in ThreatSnapshot threat, float ticks)
        {
            if (ticks == 0f) return Math.Max(0f, threat.BeamScale);
            var age = threat.BeamAge + ticks;
            if (threat.Geometry == ThreatGeometry.EmpressSunDance)
                return Clamp01(age / 20f) * Clamp01((180f - age) / 60f);
            var limit = threat.BeamScaleLimit > 0f ? threat.BeamScaleLimit : 1f;
            return Math.Max(0f, Math.Min(limit, (float)Math.Sin(age * 3.141593f / 180f) * 10f * limit));
        }

        private static float Angle(in ThreatSnapshot threat, float ticks)
        {
            if (threat.Geometry == ThreatGeometry.EmpressSunDance)
                return ticks == 0f ? threat.BeamAngle : threat.BeamBaseAngle +
                    SunSweep * Clamp01((threat.BeamAge + ticks - 50f) / 130f);
            var direction = SafeDirection(threat.BeamDirection);
            return (float)Math.Atan2(direction.Y, direction.X) + threat.BeamAngularVelocity * ticks;
        }

        private static float Length(in ThreatSnapshot threat, float ticks)
        {
            if (threat.Geometry != ThreatGeometry.MoonLordDeathray) return 0f;
            var observed = Math.Max(0f, Math.Min(2400f, threat.BeamLength));
            // Native smoothing is .5, or .75 for the head's through-wall fallback.
            // Future tiles/target LOS are unknown here: use the fastest extension to
            // the 2400 cap, never assume a currently short beam stays wall-blocked.
            return ticks <= 0f ? observed : 2400f - (2400f - observed) * (float)Math.Pow(.25f, ticks);
        }

        private static BeamSample Create(in ThreatSnapshot threat, Vec2 origin, Vec2 axis, float scale,
            float length, float sinHalfAngle, float parallelMotion, float perpendicularMotion)
        {
            var result = new BeamSample { Count = threat.Geometry == ThreatGeometry.EmpressSunDance ? 3 : 1 };
            if (result.Count == 1)
                result.First = Lobe(origin, axis, length, 18f * scale, sinHalfAngle, parallelMotion, perpendicularMotion);
            else
            {
                result.First = Lobe(origin, axis, 510f * scale, 35f * scale, sinHalfAngle, parallelMotion, perpendicularMotion);
                result.Second = Lobe(origin, axis, 660f * scale, 21f * scale, sinHalfAngle, parallelMotion, perpendicularMotion);
                result.Third = Lobe(origin, axis, 800f * scale, 3.5f * scale, sinHalfAngle, parallelMotion, perpendicularMotion);
            }
            result.Bounds = Bounds(result.First);
            if (result.Count > 1) result.Bounds = Union(result.Bounds, Bounds(result.Second));
            if (result.Count > 2) result.Bounds = Union(result.Bounds, Bounds(result.Third));
            return result;
        }

        private static BeamSample WithDeathrayBody(in ThreatSnapshot threat, BeamSample sample, float fromTicks, float toTicks)
        {
            if (threat.Geometry != ThreatGeometry.MoonLordDeathray) return sample;
            // Native Colliding IL_14dd tests the projectile's unscaled 36x36 AABB
            // BEFORE the deathray branch (including its age-20 line warmup gate).
            // Sun Dance returns earlier and must NOT acquire this extra hitbox.
            var width = threat.Width > 0 ? threat.Width : 36;
            var height = threat.Height > 0 ? threat.Height : 36;
            var start = SourceBounds(threat, fromTicks, width, height);
            var bounds = fromTicks == toTicks ? start : Union(start, SourceBounds(threat, toTicks, width, height));
            var body = new BeamLobe { Center = bounds.Center, Axis = new Vec2(1, 0),
                HalfLength = bounds.Width * .5f, HalfWidth = bounds.Height * .5f };
            if (sample.Count == 0) sample.First = body;
            else sample.Second = body;
            sample.Bounds = sample.Count == 0 ? bounds : Union(sample.Bounds, bounds);
            sample.Count++;
            return sample;
        }

        private static RectF SourceBounds(in ThreatSnapshot threat, float ticks, int width, int height)
        {
            var origin = threat.BeamOrigin + threat.BeamSourceVelocity * ticks;
            // Entity.Hitbox truncates position to integers, without projectile.scale.
            return new RectF((int)(origin.X - width * .5f), (int)(origin.Y - height * .5f), width, height);
        }

        private static BeamLobe Lobe(Vec2 origin, Vec2 axis, float length, float halfWidth,
            float sinHalfAngle, float parallelMotion, float perpendicularMotion)
        {
            return new BeamLobe
            {
                Center = origin + axis * (length * .5f), Axis = axis,
                HalfLength = length * .5f + halfWidth * sinHalfAngle + parallelMotion,
                HalfWidth = halfWidth + length * sinHalfAngle + perpendicularMotion
            };
        }

        private static bool Intersects(in RectF player, in BeamLobe beam, float margin)
        {
            var x = player.Center.X - beam.Center.X;
            var y = player.Center.Y - beam.Center.Y;
            var px = player.Width * .5f + margin;
            var py = player.Height * .5f + margin;
            var ax = Math.Abs(beam.Axis.X);
            var ay = Math.Abs(beam.Axis.Y);
            // Separating-axis test for AABB versus thick finite oriented segment.
            return Math.Abs(x) <= px + beam.HalfLength * ax + beam.HalfWidth * ay &&
                Math.Abs(y) <= py + beam.HalfLength * ay + beam.HalfWidth * ax &&
                Math.Abs(x * beam.Axis.X + y * beam.Axis.Y) <= beam.HalfLength + px * ax + py * ay &&
                Math.Abs(-x * beam.Axis.Y + y * beam.Axis.X) <= beam.HalfWidth + px * ay + py * ax;
        }

        private static float Separation(in RectF player, in BeamLobe beam)
        {
            var delta = player.Center - beam.Center;
            var ax = Math.Abs(beam.Axis.X);
            var ay = Math.Abs(beam.Axis.Y);
            var along = Math.Max(0f, Math.Abs(Vec2.Dot(delta, beam.Axis)) - beam.HalfLength - player.Width * .5f * ax - player.Height * .5f * ay);
            var across = Math.Max(0f, Math.Abs(delta.X * beam.Axis.Y - delta.Y * beam.Axis.X) - beam.HalfWidth - player.Width * .5f * ay - player.Height * .5f * ax);
            return along * along + across * across;
        }

        private static RectF Bounds(in BeamLobe beam)
        {
            var x = Math.Abs(beam.Axis.X) * beam.HalfLength + Math.Abs(beam.Axis.Y) * beam.HalfWidth;
            var y = Math.Abs(beam.Axis.Y) * beam.HalfLength + Math.Abs(beam.Axis.X) * beam.HalfWidth;
            return new RectF(beam.Center.X - x, beam.Center.Y - y, x * 2f, y * 2f);
        }

        public static RectF Union(in RectF first, in RectF second)
        {
            var left = Math.Min(first.Left, second.Left);
            var top = Math.Min(first.Top, second.Top);
            return new RectF(left, top, Math.Max(first.Right, second.Right) - left, Math.Max(first.Bottom, second.Bottom) - top);
        }

        private static Vec2 SafeDirection(Vec2 direction) => direction.LengthSquared < .00001f ? new Vec2(0, -1) : direction.Normalized();
        private static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
