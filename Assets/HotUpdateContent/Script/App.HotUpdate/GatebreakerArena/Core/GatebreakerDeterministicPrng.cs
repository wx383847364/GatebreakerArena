using System;

namespace App.HotUpdate.GatebreakerArena.Core
{
    /// <summary>
    /// Small gameplay-only PRNG with an explicit, serializable state. It deliberately
    /// avoids System.Random so lockstep results remain stable across Unity runtimes.
    /// </summary>
    public struct GatebreakerDeterministicPrng
    {
        private const uint ZeroSeedFallback = 0x6D2B79F5u;
        private uint _state;

        public GatebreakerDeterministicPrng(uint seed)
        {
            _state = seed == 0 ? ZeroSeedFallback : seed;
        }

        public uint State => _state;

        public uint NextUInt()
        {
            uint value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            }

            uint bound = (uint)exclusiveMax;
            uint limit = uint.MaxValue - (uint.MaxValue % bound);
            uint value;
            do
            {
                value = NextUInt();
            }
            while (value >= limit);

            return (int)(value % bound);
        }
    }
}
