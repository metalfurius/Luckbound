using System;

namespace Luckbound.Core.Armor
{
    /// <summary>Immutable defense and equipment modifiers for one armor item.</summary>
    public sealed class ArmorProfile
    {
        public const int MaximumUpgradeLevel = 3;

        public ArmorProfile(
            string itemId,
            int baseDefense,
            int maximumEnergyBonus = 0,
            int movementSpeedPercentModifier = 0,
            int energyRegenerationPerSecondBonus = 0,
            int harmfulStatusDurationReductionPercent = 0)
        {
            ValidateItemId(itemId);
            if (baseDefense <= 0) throw new ArgumentOutOfRangeException(nameof(baseDefense));
            if (maximumEnergyBonus < 0) throw new ArgumentOutOfRangeException(nameof(maximumEnergyBonus));
            if (energyRegenerationPerSecondBonus < 0)
                throw new ArgumentOutOfRangeException(nameof(energyRegenerationPerSecondBonus));
            if (harmfulStatusDurationReductionPercent < 0 || harmfulStatusDurationReductionPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(harmfulStatusDurationReductionPercent));

            ItemId = itemId;
            BaseDefense = baseDefense;
            MaximumEnergyBonus = maximumEnergyBonus;
            MovementSpeedPercentModifier = movementSpeedPercentModifier;
            EnergyRegenerationPerSecondBonus = energyRegenerationPerSecondBonus;
            HarmfulStatusDurationReductionPercent = harmfulStatusDurationReductionPercent;
        }

        public string ItemId { get; }
        public int BaseDefense { get; }
        public int MaximumEnergyBonus { get; }
        public int MovementSpeedPercentModifier { get; }
        public int EnergyRegenerationPerSecondBonus { get; }
        public int HarmfulStatusDurationReductionPercent { get; }

        public int GetDefenseAtUpgradeLevel(int upgradeLevel)
        {
            if (upgradeLevel < 0 || upgradeLevel > MaximumUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel), "Armor upgrades must be between +0 and +3.");
            return BaseDefense + upgradeLevel;
        }

        private static void ValidateItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId) ||
                !itemId.StartsWith("armor.", StringComparison.Ordinal) || itemId.Length <= 6)
                throw new ArgumentException("Armor profiles must use a stable armor. content ID.", nameof(itemId));

            for (int index = 0; index < itemId.Length; index++)
            {
                char character = itemId[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') &&
                    character != '.' && character != '_')
                    throw new ArgumentException(
                        "Armor IDs may contain only lowercase letters, digits, dots, and underscores.", nameof(itemId));
            }
        }
    }
}
