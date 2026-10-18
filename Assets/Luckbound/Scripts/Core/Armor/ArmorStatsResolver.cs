using System;
using Luckbound.Core.Inventory;

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
            int currentEnergy,
            ItemRarity rarity = ItemRarity.Common)
        {
            if (armor == null) throw new ArgumentNullException(nameof(armor));
            if (!Enum.IsDefined(typeof(ItemRarity), rarity)) throw new ArgumentOutOfRangeException(nameof(rarity));
            if (baseMaximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
            if (baseMaximumEnergy <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumEnergy));
            if (currentHealth < 0) throw new ArgumentOutOfRangeException(nameof(currentHealth));
            if (currentEnergy < 0) throw new ArgumentOutOfRangeException(nameof(currentEnergy));

            int rarityDefenseBonus;
            switch (rarity)
            {
                case ItemRarity.Common: rarityDefenseBonus = 0; break;
                case ItemRarity.Rare: rarityDefenseBonus = 1; break;
                case ItemRarity.Epic: rarityDefenseBonus = 2; break;
                default: throw new ArgumentOutOfRangeException(nameof(rarity));
            }

            int maximumEnergy = checked(baseMaximumEnergy + armor.MaximumEnergyBonus);
            int defense = checked(armor.GetDefenseAtUpgradeLevel(upgradeLevel) + rarityDefenseBonus);
            return new ArmorResolution(
                armor,
                upgradeLevel,
                rarity,
                defense,
                baseMaximumHealth,
                maximumEnergy,
                Math.Min(currentHealth, baseMaximumHealth),
                Math.Min(currentEnergy, maximumEnergy));
        }
    }
}
