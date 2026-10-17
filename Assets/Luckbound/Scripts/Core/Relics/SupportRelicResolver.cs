using System;

namespace Luckbound.Core.Relics
{
    /// <summary>Stable IDs for the support relics implemented by this module.</summary>
    public static class SupportRelicIds
    {
        public const string PaleBone = "relic.pale_bone";
        public const string ArcaneFlask = "relic.arcane_flask";
        public const string ApprenticeGear = "relic.apprentice_gear";
        public const string AlvariLeaf = "relic.alvari_leaf";
        public const string RouteCord = "relic.route_cord";
        public const string BlessedVial = "relic.blessed_vial";
        public const string SigiledBark = "relic.sigiled_bark";
        public const string MerchantSeal = "relic.merchant_seal";

        /// <summary>Returns whether the ID names one of the eight support relics.</summary>
        public static bool IsSupported(string relicId)
        {
            return string.Equals(relicId, PaleBone, StringComparison.Ordinal) ||
                string.Equals(relicId, ArcaneFlask, StringComparison.Ordinal) ||
                string.Equals(relicId, ApprenticeGear, StringComparison.Ordinal) ||
                string.Equals(relicId, AlvariLeaf, StringComparison.Ordinal) ||
                string.Equals(relicId, RouteCord, StringComparison.Ordinal) ||
                string.Equals(relicId, BlessedVial, StringComparison.Ordinal) ||
                string.Equals(relicId, SigiledBark, StringComparison.Ordinal) ||
                string.Equals(relicId, MerchantSeal, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Immutable additive modifiers from the supported support relics. Percentage values are
    /// accumulated by category before a consumer applies them. Maximum-resource bonuses are deltas;
    /// resolving them never changes current health or energy.
    /// </summary>
    public readonly struct SupportRelicModifiers
    {
        internal SupportRelicModifiers(
            int maximumHealthBonus,
            int maximumEnergyBonus,
            int skillCooldownReductionPercent,
            int skillCostReductionPercent,
            int movementSpeedBonusPercent,
            int consumableHealingBonusPercent,
            int harmfulStatusDurationReductionPercent,
            int purchasePriceReductionPercent)
        {
            MaximumHealthBonus = maximumHealthBonus;
            MaximumEnergyBonus = maximumEnergyBonus;
            SkillCooldownReductionPercent = skillCooldownReductionPercent;
            SkillCostReductionPercent = skillCostReductionPercent;
            MovementSpeedBonusPercent = movementSpeedBonusPercent;
            ConsumableHealingBonusPercent = consumableHealingBonusPercent;
            HarmfulStatusDurationReductionPercent = harmfulStatusDurationReductionPercent;
            PurchasePriceReductionPercent = purchasePriceReductionPercent;
        }

        public int MaximumHealthBonus { get; }
        public int MaximumEnergyBonus { get; }
        public int SkillCooldownReductionPercent { get; }
        public int SkillCostReductionPercent { get; }
        public int MovementSpeedBonusPercent { get; }
        public int ConsumableHealingBonusPercent { get; }
        public int HarmfulStatusDurationReductionPercent { get; }
        public int PurchasePriceReductionPercent { get; }

        /// <summary>
        /// Adds independent modifier sources by category. Percentage categories stay separate and
        /// are summed before the eventual stat or price calculation.
        /// </summary>
        public SupportRelicModifiers Add(SupportRelicModifiers other)
        {
            return new SupportRelicModifiers(
                checked(MaximumHealthBonus + other.MaximumHealthBonus),
                checked(MaximumEnergyBonus + other.MaximumEnergyBonus),
                checked(SkillCooldownReductionPercent + other.SkillCooldownReductionPercent),
                checked(SkillCostReductionPercent + other.SkillCostReductionPercent),
                checked(MovementSpeedBonusPercent + other.MovementSpeedBonusPercent),
                checked(ConsumableHealingBonusPercent + other.ConsumableHealingBonusPercent),
                checked(HarmfulStatusDurationReductionPercent + other.HarmfulStatusDurationReductionPercent),
                checked(PurchasePriceReductionPercent + other.PurchasePriceReductionPercent));
        }

        /// <summary>Adds the relic bonus to a positive maximum health value.</summary>
        public int AddToMaximumHealth(int currentMaximumHealth)
        {
            if (currentMaximumHealth <= 0)
                throw new ArgumentOutOfRangeException(nameof(currentMaximumHealth));

            return checked(currentMaximumHealth + MaximumHealthBonus);
        }

        /// <summary>Adds the relic bonus to a nonnegative maximum energy value.</summary>
        public int AddToMaximumEnergy(int currentMaximumEnergy)
        {
            if (currentMaximumEnergy < 0)
                throw new ArgumentOutOfRangeException(nameof(currentMaximumEnergy));

            return checked(currentMaximumEnergy + MaximumEnergyBonus);
        }
    }

    /// <summary>Resolves the passive modifiers from the two equipped relic definition IDs.</summary>
    public static class SupportRelicResolver
    {
        /// <summary>
        /// Resolves two equipped relic slots. A null ID represents an empty slot. Unknown IDs and
        /// duplicate definitions are rejected so invalid equipment cannot silently affect stats.
        /// </summary>
        public static SupportRelicModifiers Resolve(string relicOneId, string relicTwoId)
        {
            ValidateSlot(relicOneId, nameof(relicOneId));
            ValidateSlot(relicTwoId, nameof(relicTwoId));

            if (relicOneId != null && string.Equals(relicOneId, relicTwoId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The same relic definition cannot occupy both equipment slots.", nameof(relicTwoId));
            }

            SupportRelicModifiers first = GetModifiers(relicOneId);
            SupportRelicModifiers second = GetModifiers(relicTwoId);
            return first.Add(second);
        }

        private static void ValidateSlot(string relicId, string parameterName)
        {
            if (relicId != null && !SupportRelicIds.IsSupported(relicId))
            {
                throw new ArgumentException("Relic ID must name a supported support relic.", parameterName);
            }
        }

        private static SupportRelicModifiers GetModifiers(string relicId)
        {
            switch (relicId)
            {
                case SupportRelicIds.PaleBone:
                    return new SupportRelicModifiers(15, 0, 0, 0, 0, 0, 0, 0);
                case SupportRelicIds.ArcaneFlask:
                    return new SupportRelicModifiers(0, 10, 0, 0, 0, 0, 0, 0);
                case SupportRelicIds.ApprenticeGear:
                    return new SupportRelicModifiers(0, 0, 10, 0, 0, 0, 0, 0);
                case SupportRelicIds.AlvariLeaf:
                    return new SupportRelicModifiers(0, 0, 0, 10, 0, 0, 0, 0);
                case SupportRelicIds.RouteCord:
                    return new SupportRelicModifiers(0, 0, 0, 0, 8, 0, 0, 0);
                case SupportRelicIds.BlessedVial:
                    return new SupportRelicModifiers(0, 0, 0, 0, 0, 20, 0, 0);
                case SupportRelicIds.SigiledBark:
                    return new SupportRelicModifiers(0, 0, 0, 0, 0, 0, 25, 0);
                case SupportRelicIds.MerchantSeal:
                    return new SupportRelicModifiers(0, 0, 0, 0, 0, 0, 0, 15);
                case null:
                    return default;
                default:
                    throw new ArgumentException("Relic ID must name a supported support relic.", nameof(relicId));
            }
        }
    }
}
