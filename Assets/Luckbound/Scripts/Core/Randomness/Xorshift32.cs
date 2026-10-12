using System;
using System.Globalization;
using System.Text;

namespace Luckbound.Core.Randomness
{
    /// <summary>Version 1 deterministic random stream used by content generation.</summary>
    public sealed class Xorshift32
    {
        public const uint ZeroSeedSubstitute = 0x6D2B79F5u;
        private const uint RandomDomainSize = uint.MaxValue;
        private uint _state;

        public Xorshift32(uint seed)
        {
            _state = NormalizeSeed(seed);
        }

        public uint State => _state;

        public static uint NormalizeSeed(uint seed)
        {
            return seed == 0u ? ZeroSeedSubstitute : seed;
        }

        public uint NextUInt()
        {
            uint value = _state;
            unchecked
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
            }

            _state = value;
            return value;
        }

        /// <summary>Returns a value from the inclusive minimum to the exclusive maximum without modulo bias.</summary>
        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (maximumExclusive <= minimumInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive), "The exclusive maximum must exceed the minimum.");
            }

            uint range = (uint)((long)maximumExclusive - minimumInclusive);
            // Xorshift32 has 2^32 - 1 states; shifting its nonzero output down by one
            // gives a uniform domain [0, 2^32 - 2], so reject its incomplete prefix.
            uint rejectionThreshold = RandomDomainSize % range;
            uint sample;
            do
            {
                sample = NextUInt() - 1u;
            }
            while (sample < rejectionThreshold);

            return (int)((long)minimumInclusive + sample % range);
        }
    }

    /// <summary>Creates independent versioned random streams from a root expedition seed.</summary>
    public static class SeedDeriver
    {
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static uint Derive(int generationVersion, uint rootSeed, string domain, params string[] identifiers)
        {
            if (generationVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generationVersion));
            if (string.IsNullOrEmpty(domain)) throw new ArgumentException("A stable random domain is required.", nameof(domain));

            var canonical = new StringBuilder();
            canonical.Append("luckbound-rng-v1|");
            canonical.Append(generationVersion.ToString(CultureInfo.InvariantCulture));
            canonical.Append('|');
            canonical.Append(Xorshift32.NormalizeSeed(rootSeed).ToString("X8", CultureInfo.InvariantCulture));
            AppendField(canonical, domain);
            int count = identifiers == null ? 0 : identifiers.Length;
            canonical.Append('|');
            canonical.Append(count.ToString(CultureInfo.InvariantCulture));
            for (int index = 0; index < count; index++)
            {
                if (identifiers[index] == null)
                {
                    throw new ArgumentException("Random stream identifiers cannot be null.", nameof(identifiers));
                }

                AppendField(canonical, identifiers[index]);
            }

            return HashUtf8(canonical.ToString());
        }

        public static uint HashUtf8(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            uint hash = FnvOffsetBasis;
            unchecked
            {
                for (int index = 0; index < bytes.Length; index++)
                {
                    hash ^= bytes[index];
                    hash *= FnvPrime;
                }
            }

            return hash;
        }

        private static void AppendField(StringBuilder builder, string value)
        {
            builder.Append('|');
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
        }
    }
}
