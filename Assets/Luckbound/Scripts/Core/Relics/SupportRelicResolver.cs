using System;

namespace Luckbound.Core.Relics
{
    /// <summary>Stable IDs for the support relics implemented by this module.</summary>
    public static class SupportRelicIds
    {
        public const string PaleBone = "relic.pale_bone";
        public const string ArcaneFlask = "relic.arcane_flask";
        public const string ApprenticeGear = "relic.apprentice_gear";

        /// <summary>Returns whether the ID names one of the three supported relics.</summary>
        public static bool IsSupported(string relicId)
        {
            return string.Equals(relicId, PaleBone, StringComparison.Ordinal) ||
                string.Equals(relicId, ArcaneFlask, StringComparison.Ordinal) ||
                string.Equals(relicId, ApprenticeGear, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Immutable additive modifiers from the supported support relics. These are deltas to maximum
    /// values and a cooldown percentage only; current health or energy is never changed here.
    /// The consumer applies the plan's final skill-cooldown floor.
    /// </summary>
    public readonly struct SupportRelicModifiers
    {
        internal SupportRelicModifiers(
            int maximumHealthBonus,
            int maximumEnergyBonus,
            int skillCooldownReductionPercent)
        {
            MaximumHealthBonus = maximumHealthBonus;
            MaximumEnergyBonus = maximumEnergyBonus;
            SkillCooldownReductionPercent = skillCooldownReductionPercent;
        }

        public int MaximumHealthBonus { get; }
        public int MaximumEnergyBonus { get; }
        public int SkillCooldownReductionPercent { get; }

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
            return new SupportRelicModifiers(
                checked(first.MaximumHealthBonus + second.MaximumHealthBonus),
                checked(first.MaximumEnergyBonus + second.MaximumEnergyBonus),
                checked(first.SkillCooldownReductionPercent + second.SkillCooldownReductionPercent));
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
                    return new SupportRelicModifiers(15, 0, 0);
                case SupportRelicIds.ArcaneFlask:
                    return new SupportRelicModifiers(0, 10, 0);
                case SupportRelicIds.ApprenticeGear:
                    return new SupportRelicModifiers(0, 0, 10);
                case null:
                    return default;
                default:
                    throw new ArgumentException("Relic ID must name a supported support relic.", nameof(relicId));
            }
        }
    }
}
