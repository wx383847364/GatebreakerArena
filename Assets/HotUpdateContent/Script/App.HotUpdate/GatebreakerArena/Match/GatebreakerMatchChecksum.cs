using System;

namespace App.HotUpdate.GatebreakerArena.Match
{
    public readonly struct GatebreakerMatchChecksum : IEquatable<GatebreakerMatchChecksum>
    {
        public GatebreakerMatchChecksum(int frameIndex, uint value)
        {
            FrameIndex = frameIndex;
            Value = value;
        }

        public int FrameIndex { get; }
        public uint Value { get; }

        public bool Equals(GatebreakerMatchChecksum other)
        {
            return FrameIndex == other.FrameIndex && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is GatebreakerMatchChecksum other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FrameIndex * 397) ^ (int)Value;
            }
        }

        public override string ToString()
        {
            return $"{FrameIndex}:{Value:X8}";
        }

        public static bool operator ==(GatebreakerMatchChecksum left, GatebreakerMatchChecksum right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GatebreakerMatchChecksum left, GatebreakerMatchChecksum right)
        {
            return !left.Equals(right);
        }
    }
}
