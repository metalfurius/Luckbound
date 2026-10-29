using System;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Armor
{
    /// <summary>Resolves replacing an equipped armor without carrying forward its energy bonus.</summary>
    public static class ArmorReplacementResolver
    {
        /// <summary>
        /// Resolves a new armor profile using the base maxima from the current resolution. Current
        /// health and energy are preserved unless the replacement lowers their corresponding maxima.
        /// </summary>
        public static ArmorResolution Resolve(
            ArmorResolution current,
            ArmorProfile nextArmor,
            int nextUpgradeLevel,
            int currentHealth,
            int currentEnergy,
            ItemRarity nextRarity = ItemRarity.Common)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));

            int baseMaximumEnergy = current.MaximumEnergy - current.Armor.MaximumEnergyBonus;
            return ArmorStatsResolver.Resolve(
                nextArmor,
                nextUpgradeLevel,
                current.MaximumHealth,
                baseMaximumEnergy,
                currentHealth,
                currentEnergy,
                nextRarity);
        }
    }
}
