using Luckbound.Core.Inventory;

namespace Luckbound.Core.Armor
{
    /// <summary>Resolved armor stats and resource caps for one equipment change.</summary>
    public sealed class ArmorResolution
    {
        internal ArmorResolution(
            ArmorProfile armor,
            int upgradeLevel,
            ItemRarity rarity,
            int defense,
            int maximumHealth,
            int maximumEnergy,
            int healthAfterMaximumClamp,
            int energyAfterMaximumClamp)
        {
            Armor = armor;
            UpgradeLevel = upgradeLevel;
            Rarity = rarity;
            Defense = defense;
            MaximumHealth = maximumHealth;
            MaximumEnergy = maximumEnergy;
            HealthAfterMaximumClamp = healthAfterMaximumClamp;
            EnergyAfterMaximumClamp = energyAfterMaximumClamp;
        }

        public ArmorProfile Armor { get; }
        public int UpgradeLevel { get; }
        public ItemRarity Rarity { get; }
        public int Defense { get; }
        public int MaximumHealth { get; }
        public int MaximumEnergy { get; }
        public int HealthAfterMaximumClamp { get; }
        public int EnergyAfterMaximumClamp { get; }
        public int MovementSpeedPercentModifier => Armor.MovementSpeedPercentModifier;
        public int EnergyRegenerationPerSecondBonus => Armor.EnergyRegenerationPerSecondBonus;
        public int HarmfulStatusDurationReductionPercent => Armor.HarmfulStatusDurationReductionPercent;
    }
}
