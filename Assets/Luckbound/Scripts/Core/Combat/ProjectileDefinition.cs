using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Immutable physics-independent projectile travel and impact limits.</summary>
    public sealed class ProjectileDefinition
    {
        public ProjectileDefinition(
            decimal baseDamage,
            decimal speedUnitsPerSecond,
            int lifetimeTicks,
            int maximumTargets,
            int defensePenetration = 0,
            int slowPercent = 0,
            int slowDurationTicks = 0,
            string element = "")
        {
            if (baseDamage <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage));
            }

            if (speedUnitsPerSecond <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(speedUnitsPerSecond));
            }

            if (lifetimeTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lifetimeTicks));
            }

            if (maximumTargets <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumTargets));
            }

            if (defensePenetration < 0) throw new ArgumentOutOfRangeException(nameof(defensePenetration));
            if (slowPercent < 0 || slowPercent > 100) throw new ArgumentOutOfRangeException(nameof(slowPercent));
            if ((slowPercent == 0) != (slowDurationTicks == 0) || slowDurationTicks < 0)
                throw new ArgumentException("Projectile slow requires both a positive percent and duration, or neither.");
            if (element == null) throw new ArgumentNullException(nameof(element));

            BaseDamage = baseDamage;
            SpeedUnitsPerSecond = speedUnitsPerSecond;
            LifetimeTicks = lifetimeTicks;
            MaximumTargets = maximumTargets;
            DefensePenetration = defensePenetration;
            SlowPercent = slowPercent;
            SlowDurationTicks = slowDurationTicks;
            Element = element;
        }

        public decimal BaseDamage { get; }

        public decimal SpeedUnitsPerSecond { get; }

        public int LifetimeTicks { get; }

        public int MaximumTargets { get; }

        /// <summary>Target defense points ignored by this projectile's direct hit.</summary>
        public int DefensePenetration { get; }

        /// <summary>Optional on-hit movement slow, expressed as a percent for 60 Hz ticks.</summary>
        public int SlowPercent { get; }

        public int SlowDurationTicks { get; }

        /// <summary>Presentation-facing element label; it does not itself add burn or other status effects.</summary>
        public string Element { get; }
    }
}
