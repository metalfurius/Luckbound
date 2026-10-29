using System;

namespace Luckbound.Core.Fortune
{
    /// <summary>Stable item IDs for the eight Fortune relics in the catalog.</summary>
    public static class FortuneRelicIds
    {
        public const string FourEchoCrystal = "relic.four_echo_crystal";
        public const string MarkedCoin = "relic.marked_coin";
        public const string VitalDie = "relic.vital_die";
        public const string LuckyBranch = "relic.lucky_branch";
        public const string HiddenPouch = "relic.hidden_pouch";
        public const string MerchantCounterweight = "relic.merchant_counterweight";
        public const string ConcordSigil = "relic.concord_sigil";
        public const string OathFragment = "relic.oath_fragment";

        /// <summary>Returns whether the ID names one of the eight Fortune relics.</summary>
        public static bool IsSupported(string relicId)
        {
            return string.Equals(relicId, FourEchoCrystal, StringComparison.Ordinal) ||
                string.Equals(relicId, MarkedCoin, StringComparison.Ordinal) ||
                string.Equals(relicId, VitalDie, StringComparison.Ordinal) ||
                string.Equals(relicId, LuckyBranch, StringComparison.Ordinal) ||
                string.Equals(relicId, HiddenPouch, StringComparison.Ordinal) ||
                string.Equals(relicId, MerchantCounterweight, StringComparison.Ordinal) ||
                string.Equals(relicId, ConcordSigil, StringComparison.Ordinal) ||
                string.Equals(relicId, OathFragment, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Immutable parameters from the supported Fortune relics. This value contains effect
    /// parameters only; FortuneOfferBook, consumers, and the expedition own all mutable state.
    /// </summary>
    public readonly struct FortuneRelicModifiers
    {
        internal FortuneRelicModifiers(
            int additionalOfferOptionCount,
            bool grantsFreeFirstRerollPerSector,
            int healthRestoredOnReroll,
            int additionalFortunePerGuardian,
            int containerGoldBonusPercent,
            int saleValueBonusPercent,
            int positivePactBenefitAmplificationPercent,
            int fortuneGrantedOnPactAcceptance)
        {
            AdditionalOfferOptionCount = additionalOfferOptionCount;
            GrantsFreeFirstRerollPerSector = grantsFreeFirstRerollPerSector;
            HealthRestoredOnReroll = healthRestoredOnReroll;
            AdditionalFortunePerGuardian = additionalFortunePerGuardian;
            ContainerGoldBonusPercent = containerGoldBonusPercent;
            SaleValueBonusPercent = saleValueBonusPercent;
            PositivePactBenefitAmplificationPercent = positivePactBenefitAmplificationPercent;
            FortuneGrantedOnPactAcceptance = fortuneGrantedOnPactAcceptance;
        }

        public int AdditionalOfferOptionCount { get; }

        /// <summary>
        /// True when the Marked Coin is equipped. FortuneOfferBook must own and persist the
        /// per-sector free-reroll consumption state so unequipping cannot reset it.
        /// </summary>
        public bool GrantsFreeFirstRerollPerSector { get; }

        public int HealthRestoredOnReroll { get; }
        public int AdditionalFortunePerGuardian { get; }
        public int ContainerGoldBonusPercent { get; }
        public int SaleValueBonusPercent { get; }
        public int PositivePactBenefitAmplificationPercent { get; }
        public int FortuneGrantedOnPactAcceptance { get; }

        /// <summary>
        /// Adds independent modifier sources by category. Percentage categories stay separate and
        /// are summed before a consumer applies them. No gameplay state is read or changed here.
        /// </summary>
        public FortuneRelicModifiers Add(FortuneRelicModifiers other)
        {
            return new FortuneRelicModifiers(
                checked(AdditionalOfferOptionCount + other.AdditionalOfferOptionCount),
                GrantsFreeFirstRerollPerSector || other.GrantsFreeFirstRerollPerSector,
                checked(HealthRestoredOnReroll + other.HealthRestoredOnReroll),
                checked(AdditionalFortunePerGuardian + other.AdditionalFortunePerGuardian),
                checked(ContainerGoldBonusPercent + other.ContainerGoldBonusPercent),
                checked(SaleValueBonusPercent + other.SaleValueBonusPercent),
                checked(PositivePactBenefitAmplificationPercent + other.PositivePactBenefitAmplificationPercent),
                checked(FortuneGrantedOnPactAcceptance + other.FortuneGrantedOnPactAcceptance));
        }

        /// <summary>
        /// Rounds an already-calculated nonnegative currency or reward amount down as specified by
        /// DEVELOPMENT_PLAN.md section 3.12. This helper does not calculate or grant any reward.
        /// </summary>
        public static int RoundCurrencyOrRewardDown(decimal calculatedAmount)
        {
            if (calculatedAmount < 0m)
                throw new ArgumentOutOfRangeException(nameof(calculatedAmount));

            decimal rounded = decimal.Floor(calculatedAmount);
            if (rounded > int.MaxValue)
                throw new OverflowException("Currency or reward exceeds the supported integer range.");

            return decimal.ToInt32(rounded);
        }
    }

    /// <summary>Resolves passive parameters from two equipped Fortune relic definition IDs.</summary>
    public static class FortuneRelicResolver
    {
        /// <summary>
        /// Resolves two slots; null means empty. Unknown IDs and duplicate relics are rejected.
        /// This resolver does not apply effects or mutate offers, sector counters, or player state.
        /// </summary>
        public static FortuneRelicModifiers Resolve(string relicOneId, string relicTwoId)
        {
            ValidateSlot(relicOneId, nameof(relicOneId));
            ValidateSlot(relicTwoId, nameof(relicTwoId));

            if (relicOneId != null && string.Equals(relicOneId, relicTwoId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The same Fortune relic definition cannot occupy both equipment slots.",
                    nameof(relicTwoId));
            }

            FortuneRelicModifiers first = GetModifiers(relicOneId);
            FortuneRelicModifiers second = GetModifiers(relicTwoId);
            return first.Add(second);
        }

        private static void ValidateSlot(string relicId, string parameterName)
        {
            if (relicId != null && !FortuneRelicIds.IsSupported(relicId))
            {
                throw new ArgumentException("Relic ID must name a supported Fortune relic.", parameterName);
            }
        }

        private static FortuneRelicModifiers GetModifiers(string relicId)
        {
            switch (relicId)
            {
                case FortuneRelicIds.FourEchoCrystal:
                    return new FortuneRelicModifiers(1, false, 0, 0, 0, 0, 0, 0);
                case FortuneRelicIds.MarkedCoin:
                    return new FortuneRelicModifiers(0, true, 0, 0, 0, 0, 0, 0);
                case FortuneRelicIds.VitalDie:
                    return new FortuneRelicModifiers(0, false, 5, 0, 0, 0, 0, 0);
                case FortuneRelicIds.LuckyBranch:
                    return new FortuneRelicModifiers(0, false, 0, 1, 0, 0, 0, 0);
                case FortuneRelicIds.HiddenPouch:
                    return new FortuneRelicModifiers(0, false, 0, 0, 25, 0, 0, 0);
                case FortuneRelicIds.MerchantCounterweight:
                    return new FortuneRelicModifiers(0, false, 0, 0, 0, 50, 0, 0);
                case FortuneRelicIds.ConcordSigil:
                    return new FortuneRelicModifiers(0, false, 0, 0, 0, 0, 25, 0);
                case FortuneRelicIds.OathFragment:
                    return new FortuneRelicModifiers(0, false, 0, 0, 0, 0, 0, 1);
                case null:
                    return default;
                default:
                    throw new ArgumentException("Relic ID must name a supported Fortune relic.", nameof(relicId));
            }
        }
    }
}
