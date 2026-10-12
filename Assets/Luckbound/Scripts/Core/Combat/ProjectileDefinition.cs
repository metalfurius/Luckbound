using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Immutable physics-independent projectile travel and impact limits.</summary>
    public sealed class ProjectileDefinition
    {
        public ProjectileDefinition(decimal baseDamage, decimal speedUnitsPerSecond, int lifetimeTicks, int maximumTargets)
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

            BaseDamage = baseDamage;
            SpeedUnitsPerSecond = speedUnitsPerSecond;
            LifetimeTicks = lifetimeTicks;
            MaximumTargets = maximumTargets;
        }

        public decimal BaseDamage { get; }

        public decimal SpeedUnitsPerSecond { get; }

        public int LifetimeTicks { get; }

        public int MaximumTargets { get; }
    }
}
