namespace Luckbound.Core.Armor
{
    /// <summary>Resolved armor stats and resource caps for one equipment change.</summary>
    public sealed class ArmorResolution
    {
        internal ArmorResolution(
            ArmorProfile armor,
            int upgradeLevel,
            int maximumHealth,
            int maximumEnergy,
            int healthAfterMaximumClamp,
            int energyAfterMaximumClamp)
        {
            Armor = armor;
            UpgradeLevel = upgradeLevel;
            Defense = armor.GetDefenseAtUpgradeLevel(upgradeLevel);
            MaximumHealth = maximumHealth;
            MaximumEnergy = maximumEnergy;
            HealthAfterMaximumClamp = healthAfterMaximumClamp;
            EnergyAfterMaximumClamp = energyAfterMaximumClamp;
        }

        public ArmorProfile Armor { get; }
        public int UpgradeLevel { get; }
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
