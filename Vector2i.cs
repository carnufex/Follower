using System;
using SharpDX;

namespace Follower
{
    public struct Vector2i : IEquatable<Vector2i>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public Vector2i(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Vector2i operator +(Vector2i a, Vector2i b)
        {
            return new Vector2i(a.X + b.X, a.Y + b.Y);
        }

        public static Vector2i operator -(Vector2i a, Vector2i b)
        {
            return new Vector2i(a.X - b.X, a.Y - b.Y);
        }

        public static bool operator ==(Vector2i a, Vector2i b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(Vector2i a, Vector2i b)
        {
            return !(a == b);
        }

        public bool Equals(Vector2i other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is Vector2i other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public float DistanceF(Vector2i other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        public double Distance(Vector2i other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }

        // Conversion methods for interop with Vector3
        public static Vector2i FromVector3(Vector3 vector, float scale = 23f)
        {
            return new Vector2i((int)(vector.X / scale), (int)(vector.Y / scale));
        }

        public Vector3 ToVector3(float scale = 23f)
        {
            return new Vector3(X * scale, Y * scale, 0);
        }

        public static implicit operator Vector2i((int x, int y) tuple)
        {
            return new Vector2i(tuple.x, tuple.y);
        }
    }
} 