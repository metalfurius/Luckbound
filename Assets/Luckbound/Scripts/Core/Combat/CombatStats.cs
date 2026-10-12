using System;

namespace Luckbound.Core.Combat
{
    /// <summary>
    /// Immutable maximum combat attributes shared by players and enemies.
    /// Current health and energy belong to the actor state added by combat damage handling.
    /// </summary>
    public sealed class CombatStats
    {
        public CombatStats(int maximumHealth, int maximumEnergy, int defense)
        {
            if (maximumHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth), "Maximum health must be greater than zero.");
            }

            if (maximumEnergy < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEnergy), "Maximum energy cannot be negative.");
            }

            if (defense < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defense), "Defense cannot be negative.");
            }

            MaximumHealth = maximumHealth;
            MaximumEnergy = maximumEnergy;
            Defense = defense;
        }

        public int MaximumHealth { get; }

        public int MaximumEnergy { get; }

        public int Defense { get; }
    }
}
