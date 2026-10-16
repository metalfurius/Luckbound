using System;
using System.Collections.Generic;

namespace Luckbound.Core.Pacts
{
    /// <summary>Deterministic additive percentage modifiers supplied by accepted pacts.</summary>
    public readonly struct PactModifierTotals
    {
        internal PactModifierTotals(
            int goldRewardBonusPercent,
            int basicAttackDamageBonusPercent,
            int maximumHealthReductionPercent,
            int skillCostIncreasePercent)
        {
            GoldRewardBonusPercent = goldRewardBonusPercent;
            BasicAttackDamageBonusPercent = basicAttackDamageBonusPercent;
            MaximumHealthReductionPercent = maximumHealthReductionPercent;
            SkillCostIncreasePercent = skillCostIncreasePercent;
        }

        public int GoldRewardBonusPercent { get; }
        public int BasicAttackDamageBonusPercent { get; }
        public int MaximumHealthReductionPercent { get; }
        public int SkillCostIncreasePercent { get; }

        /// <summary>Sums each percentage category after validating a unique, supported pact set.</summary>
        public static PactModifierTotals Sum(IEnumerable<string> pactIds)
        {
            if (pactIds == null) throw new ArgumentNullException(nameof(pactIds));

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            int gold = 0;
            int basicDamage = 0;
            int maximumHealthReduction = 0;
            int skillCost = 0;

            foreach (string pactId in pactIds)
            {
                if (!PactIds.IsKnown(pactId))
                    throw new ArgumentException("Pact IDs must refer to a supported pact.", nameof(pactIds));
                if (!uniqueIds.Add(pactId))
                    throw new ArgumentException("A pact ID cannot appear more than once.", nameof(pactIds));
                if (uniqueIds.Count > PactState.MaximumPactsPerExpedition)
                    throw new ArgumentException("An expedition cannot contain more than two pacts.", nameof(pactIds));

                PactModifierTotals pact = PactIds.GetModifiers(pactId);
                gold = checked(gold + pact.GoldRewardBonusPercent);
                basicDamage = checked(basicDamage + pact.BasicAttackDamageBonusPercent);
                maximumHealthReduction = checked(maximumHealthReduction + pact.MaximumHealthReductionPercent);
                skillCost = checked(skillCost + pact.SkillCostIncreasePercent);
            }

            return new PactModifierTotals(gold, basicDamage, maximumHealthReduction, skillCost);
        }

        /// <summary>Applies a gold reward bonus and rounds the resulting reward down.</summary>
        public int ApplyGoldReward(int baseReward)
        {
            if (baseReward < 0) throw new ArgumentOutOfRangeException(nameof(baseReward));
            return ScaleDown(baseReward, 100 + GoldRewardBonusPercent);
        }

        /// <summary>Applies a skill-cost increase, rounds up, and preserves the one-point minimum.</summary>
        public int ApplySkillCost(int baseCost)
        {
            if (baseCost <= 0) throw new ArgumentOutOfRangeException(nameof(baseCost));
            return Math.Max(1, ScaleUp(baseCost, 100 + SkillCostIncreasePercent));
        }

        /// <summary>Applies the maximum-health penalty using the game's integer-stat rounding rule.</summary>
        public int ApplyMaximumHealth(int baseMaximumHealth)
        {
            if (baseMaximumHealth <= 0) throw new ArgumentOutOfRangeException(nameof(baseMaximumHealth));
            if (MaximumHealthReductionPercent < 0 || MaximumHealthReductionPercent >= 100)
                throw new InvalidOperationException("Maximum-health reduction must be between zero and 99 percent.");

            return Scale(baseMaximumHealth, 100 - MaximumHealthReductionPercent, MidpointRounding.AwayFromZero);
        }

        private static int Scale(int value, int percent, MidpointRounding rounding)
        {
            decimal scaled = (decimal)value * percent / 100m;
            decimal rounded = decimal.Round(scaled, 0, rounding);
            if (rounded > int.MaxValue) throw new OverflowException("Pact-adjusted value exceeds the supported integer range.");
            return (int)rounded;
        }

        private static int ScaleDown(int value, int percent)
        {
            decimal scaled = (decimal)value * percent / 100m;
            decimal rounded = decimal.Floor(scaled);
            if (rounded > int.MaxValue) throw new OverflowException("Pact-adjusted value exceeds the supported integer range.");
            return (int)rounded;
        }

        private static int ScaleUp(int value, int percent)
        {
            decimal scaled = (decimal)value * percent / 100m;
            decimal rounded = decimal.Ceiling(scaled);
            if (rounded > int.MaxValue) throw new OverflowException("Pact-adjusted value exceeds the supported integer range.");
            return (int)rounded;
        }
    }
}
