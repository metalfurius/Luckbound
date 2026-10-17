using System;

namespace Luckbound.Core.Armor
{
    /// <summary>Pure calculation of armor defense, modifiers, maxima, and downward resource caps.</summary>
    public static class ArmorStatsResolver
    {
        /// <summary>
        /// Resolves armor against maxima that already include all non-armor modifiers. Current resources
        /// are values from before the equipment change; the returned values only cap them downward.
        /// </summary>
        public static ArmorResolution Resolve(
            ArmorProfile armor,
            int upgradeLevel,
            int baseMaximumHealth,
            int baseMaximumEnergy,
            int currentHealth,
            int currentEnergy)
        {
            if (armor == null) throw new ArgumentNullException(nameof(armor));
            if (baseMaximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
            if (baseMaximumEnergy <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumEnergy));
            if (currentHealth < 0) throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (currentEnergy < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergy));

            int maximumEnergy = checked(baseMaximumEnergy + armor.MaximumEnergyBonus);
            return new ArmorResolution(
                armor,
                upgradeLevel,
                baseMaximumHealth,
                maximumEnergy,
                Math.Min(currentHealth, baseMaximumHealth),
                Math.Min(currentEnergy, maximumEnergy));
        }
    }
}
