using System;

namespace Luckbound.Core.Combat
{
    /// <summary>
    /// Complete, explicit inputs for one direct-damage impact.
    /// Percent values are fractions: 0.15 means fifteen percent.
    /// Callers sum modifiers belonging to the same category before creating the request.
    /// </summary>
    public sealed class DamageCalculationRequest
    {
        public DamageCalculationRequest(
            decimal baseDamage,
            decimal rarityMultiplier,
            decimal upgradeMultiplier,
            decimal offensiveBonusPercent,
            int defensePenetration,
            decimal incomingDamageReductionPercent,
            CombatStats targetStats,
            bool targetIsInvulnerable)
        {
            if (baseDamage <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), "Base damage must be greater than zero.");
            }

            if (rarityMultiplier <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(rarityMultiplier), "The rarity multiplier must be greater than zero.");
            }

            if (upgradeMultiplier <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(upgradeMultiplier), "The upgrade multiplier must be greater than zero.");
            }

            if (offensiveBonusPercent < -1m)
            {
                throw new ArgumentOutOfRangeException(nameof(offensiveBonusPercent), "Offensive bonuses cannot reduce damage below zero.");
            }

            if (defensePenetration < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(defensePenetration), "Defense penetration cannot be negative.");
            }

            if (incomingDamageReductionPercent < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(incomingDamageReductionPercent), "Damage reduction cannot be negative.");
            }

            TargetStats = targetStats ?? throw new ArgumentNullException(nameof(targetStats));
            BaseDamage = baseDamage;
            RarityMultiplier = rarityMultiplier;
            UpgradeMultiplier = upgradeMultiplier;
            OffensiveBonusPercent = offensiveBonusPercent;
            DefensePenetration = defensePenetration;
            IncomingDamageReductionPercent = incomingDamageReductionPercent;
            TargetIsInvulnerable = targetIsInvulnerable;
        }

        public decimal BaseDamage { get; }

        public decimal RarityMultiplier { get; }

        public decimal UpgradeMultiplier { get; }

        public decimal OffensiveBonusPercent { get; }

        public int DefensePenetration { get; }

        public decimal IncomingDamageReductionPercent { get; }

        public CombatStats TargetStats { get; }

        public bool TargetIsInvulnerable { get; }
    }
}
