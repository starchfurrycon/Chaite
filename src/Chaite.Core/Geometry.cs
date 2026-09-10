using System;

namespace Chaite.Core
{
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float LengthSquared => X * X + Y * Y;
        public float Length => (float)Math.Sqrt(LengthSquared);

        public Vec2 Normalized()
        {
            var length = Length;
            return length < 0.0001f ? new Vec2(0f, 0f) : this / length;
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;
        public static float DistanceSquared(Vec2 a, Vec2 b) => (a - b).LengthSquared;
        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator *(Vec2 value, float scale) => new Vec2(value.X * scale, value.Y * scale);
        public static Vec2 operator /(Vec2 value, float scale) => new Vec2(value.X / scale, value.Y / scale);
    }

    public struct RectF
    {
        public float X;
        public float Y;
        public float Width;
        public float Height;

        public RectF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public readonly float Left => X;
        public readonly float Right => X + Width;
        public readonly float Top => Y;
        public readonly float Bottom => Y + Height;
        public readonly Vec2 Center => new Vec2(X + Width * 0.5f, Y + Height * 0.5f);

        public readonly RectF Inflated(float amount) => new RectF(X - amount, Y - amount, Width + amount * 2f, Height + amount * 2f);

        public readonly bool Intersects(in RectF other)
        {
            return Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;
        }

        public readonly float SeparationSquared(in RectF other)
        {
            var dx = Math.Max(0f, Math.Max(other.Left - Right, Left - other.Right));
            var dy = Math.Max(0f, Math.Max(other.Top - Bottom, Top - other.Bottom));
            return dx * dx + dy * dy;
        }
    }
}
