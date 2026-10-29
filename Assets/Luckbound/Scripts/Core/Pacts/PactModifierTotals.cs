using System;
using System.Collections.Generic;

namespace Luckbound.Core.Pacts
{
    /// <summary>Deterministic additive percentage modifiers supplied by accepted pacts.</summary>
    public readonly struct PactModifierTotals
    {
        public const decimal MaximumHorizontalSpeed = 8.5m;
        public const decimal MinimumSkillCooldownSeconds = 0.5m;

        internal PactModifierTotals(
            int goldRewardBonusPercent,
            int basicAttackDamageBonusPercent,
            int maximumHealthReductionPercent,
            int skillCostIncreasePercent,
            int horizontalSpeedBonusPercent,
            int defenseReduction,
            int skillCooldownReductionPercent,
            int purchasePriceIncreasePercent)
        {
            GoldRewardBonusPercent = goldRewardBonusPercent;
            BasicAttackDamageBonusPercent = basicAttackDamageBonusPercent;
            MaximumHealthReductionPercent = maximumHealthReductionPercent;
            SkillCostIncreasePercent = skillCostIncreasePercent;
            HorizontalSpeedBonusPercent = horizontalSpeedBonusPercent;
            DefenseReduction = defenseReduction;
            SkillCooldownReductionPercent = skillCooldownReductionPercent;
            PurchasePriceIncreasePercent = purchasePriceIncreasePercent;
        }

        public int GoldRewardBonusPercent { get; }
        public int BasicAttackDamageBonusPercent { get; }
        public int MaximumHealthReductionPercent { get; }
        public int SkillCostIncreasePercent { get; }
        public int HorizontalSpeedBonusPercent { get; }
        public int DefenseReduction { get; }
        public int SkillCooldownReductionPercent { get; }
        public int PurchasePriceIncreasePercent { get; }

        /// <summary>Applies the summed horizontal-speed bonus and global maximum.</summary>
        public decimal ApplyHorizontalSpeed(decimal baseSpeed)
        {
            if (baseSpeed < 0m) throw new ArgumentOutOfRangeException(nameof(baseSpeed));

            decimal multiplier = (100m + HorizontalSpeedBonusPercent) / 100m;
            if (multiplier <= 0m) return 0m;
            if (baseSpeed >= MaximumHorizontalSpeed / multiplier) return MaximumHorizontalSpeed;
            return baseSpeed * multiplier;
        }

        /// <summary>Applies the flat defense penalty and global zero minimum.</summary>
        public int ApplyDefense(int baseDefense)
        {
            if (baseDefense < 0) throw new ArgumentOutOfRangeException(nameof(baseDefense));
            return Math.Max(0, baseDefense - DefenseReduction);
        }

        /// <summary>Applies the summed cooldown reduction and global minimum in seconds.</summary>
        public decimal ApplySkillCooldown(decimal baseCooldownSeconds)
        {
            if (baseCooldownSeconds <= 0m) throw new ArgumentOutOfRangeException(nameof(baseCooldownSeconds));

            decimal multiplier = (100m - SkillCooldownReductionPercent) / 100m;
            if (multiplier <= 0m) return MinimumSkillCooldownSeconds;
            if (baseCooldownSeconds <= MinimumSkillCooldownSeconds / multiplier)
                return MinimumSkillCooldownSeconds;
            return baseCooldownSeconds * multiplier;
        }

        /// <summary>Applies the summed purchase-price increase and rounds the price up.</summary>
        public int ApplyPurchasePrice(int basePrice)
        {
            if (basePrice < 0) throw new ArgumentOutOfRangeException(nameof(basePrice));
            return ScaleUp(basePrice, 100 + PurchasePriceIncreasePercent);
        }

        /// <summary>Applies the summed basic-attack damage bonus without rounding fractional damage.</summary>
        public decimal ApplyBasicAttackDamage(decimal baseDamage)
        {
            if (baseDamage < 0m) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            return baseDamage * (100m + BasicAttackDamageBonusPercent) / 100m;
        }

        /// <summary>Sums each percentage category after validating a unique, supported pact set.</summary>
        public static PactModifierTotals Sum(IEnumerable<string> pactIds)
        {
            if (pactIds == null) throw new ArgumentNullException(nameof(pactIds));

            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            int gold = 0;
            int basicDamage = 0;
            int maximumHealthReduction = 0;
            int skillCost = 0;
            int horizontalSpeed = 0;
            int defenseReduction = 0;
            int skillCooldownReduction = 0;
            int purchasePriceIncrease = 0;

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
                horizontalSpeed = checked(horizontalSpeed + pact.HorizontalSpeedBonusPercent);
                defenseReduction = checked(defenseReduction + pact.DefenseReduction);
                skillCooldownReduction = checked(skillCooldownReduction + pact.SkillCooldownReductionPercent);
                purchasePriceIncrease = checked(purchasePriceIncrease + pact.PurchasePriceIncreasePercent);
            }

            return new PactModifierTotals(
                gold,
                basicDamage,
                maximumHealthReduction,
                skillCost,
                horizontalSpeed,
                defenseReduction,
                skillCooldownReduction,
                purchasePriceIncrease);
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
