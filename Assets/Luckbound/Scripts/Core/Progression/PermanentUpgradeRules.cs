using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Progression
{
    /// <summary>Pure rules for purchases made between expeditions in the tavern.</summary>
    public static class PermanentUpgradeRules
    {
        public static PermanentUpgradePurchaseResult PreparePurchase(
            PermanentUpgradeSnapshot current,
            PermanentUpgradeKind kind,
            TransactionId transactionId,
            bool isInTavern)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!Enum.IsDefined(typeof(PermanentUpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (!transactionId.IsValid)
                return Result(transactionId, kind, PermanentUpgradePurchaseStatus.RejectedInvalidTransaction, 0, current);

            if (current.TryGetPurchase(transactionId, out PermanentUpgradePurchaseRecord prior))
            {
                return Result(transactionId, kind,
                    prior.Kind == kind ? PermanentUpgradePurchaseStatus.AlreadyProcessed : PermanentUpgradePurchaseStatus.RejectedTransactionConflict,
                    prior.Kind == kind ? prior.Cost : 0, current);
            }

            if (!isInTavern)
                return Result(transactionId, kind, PermanentUpgradePurchaseStatus.RejectedNotInTavern, 0, current);

            int currentLevel = GetLevel(current, kind);
            int maximumLevel = GetMaximumLevel(kind);
            if (currentLevel >= maximumLevel)
                return Result(transactionId, kind, PermanentUpgradePurchaseStatus.RejectedMaximumLevel, 0, current);

            int cost = GetNextCost(kind, currentLevel);
            if (current.AvailableRenown < cost)
                return Result(transactionId, kind, PermanentUpgradePurchaseStatus.RejectedInsufficientRenown, cost, current);

            var record = new PermanentUpgradePurchaseRecord(transactionId, kind, cost);
            return Result(transactionId, kind, PermanentUpgradePurchaseStatus.Purchased, cost, current.WithPurchase(record));
        }

        public static int GetNextCost(PermanentUpgradeKind kind, int currentLevel)
        {
            if (!Enum.IsDefined(typeof(PermanentUpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (currentLevel < 0 || currentLevel > GetMaximumLevel(kind)) throw new ArgumentOutOfRangeException(nameof(currentLevel));
            if (currentLevel == GetMaximumLevel(kind)) return 0;
            switch (kind)
            {
                case PermanentUpgradeKind.MaximumHealth:
                case PermanentUpgradeKind.MaximumEnergy:
                    return currentLevel == 0 ? 60 : 120;
                case PermanentUpgradeKind.AdditionalHealthPotion:
                    return 150;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static int GetMaximumLevel(PermanentUpgradeKind kind)
        {
            switch (kind)
            {
                case PermanentUpgradeKind.MaximumHealth: return PermanentUpgradeSnapshot.MaximumHealthUpgradeLevel;
                case PermanentUpgradeKind.MaximumEnergy: return PermanentUpgradeSnapshot.MaximumEnergyUpgradeLevel;
                case PermanentUpgradeKind.AdditionalHealthPotion: return PermanentUpgradeSnapshot.MaximumPotionUpgradeLevel;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static int GetLevel(PermanentUpgradeSnapshot snapshot, PermanentUpgradeKind kind)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            switch (kind)
            {
                case PermanentUpgradeKind.MaximumHealth: return snapshot.HealthUpgradeLevel;
                case PermanentUpgradeKind.MaximumEnergy: return snapshot.EnergyUpgradeLevel;
                case PermanentUpgradeKind.AdditionalHealthPotion: return snapshot.AdditionalPotionUpgradeLevel;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static int ApplyFivePercentPerLevel(int baseValue, int upgradeLevel)
        {
            if (baseValue < 0) throw new ArgumentOutOfRangeException(nameof(baseValue));
            if (upgradeLevel < 0 || upgradeLevel > PermanentUpgradeSnapshot.MaximumHealthUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(upgradeLevel));

            // Positive integer division with +50 rounds halves upward; both upgrades remain additive.
            long numerator = (long)baseValue * (100 + upgradeLevel * 5) + 50;
            long result = numerator / 100;
            if (result > int.MaxValue) throw new OverflowException("The upgraded starting stat exceeds Int32.");
            return (int)result;
        }

        private static PermanentUpgradePurchaseResult Result(TransactionId transactionId,
            PermanentUpgradeKind kind, PermanentUpgradePurchaseStatus status, int cost, PermanentUpgradeSnapshot snapshot)
        {
            return new PermanentUpgradePurchaseResult(transactionId, kind, status, cost, snapshot);
        }
    }
}
