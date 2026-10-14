using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Progression
{
    /// <summary>Immutable, persistence-friendly permanent-upgrade state for one profile.</summary>
    public sealed class PermanentUpgradeSnapshot
    {
        public const int MaximumHealthUpgradeLevel = 2;
        public const int MaximumEnergyUpgradeLevel = 2;
        public const int MaximumPotionUpgradeLevel = 1;

        private readonly ReadOnlyCollection<PermanentUpgradePurchaseRecord> _purchaseRecords;

        public PermanentUpgradeSnapshot(
            int availableRenown,
            int healthUpgradeLevel,
            int energyUpgradeLevel,
            int additionalPotionUpgradeLevel,
            IEnumerable<PermanentUpgradePurchaseRecord> purchaseRecords = null)
        {
            if (availableRenown < 0) throw new ArgumentOutOfRangeException(nameof(availableRenown));
            if (healthUpgradeLevel < 0 || healthUpgradeLevel > MaximumHealthUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(healthUpgradeLevel));
            if (energyUpgradeLevel < 0 || energyUpgradeLevel > MaximumEnergyUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(energyUpgradeLevel));
            if (additionalPotionUpgradeLevel < 0 || additionalPotionUpgradeLevel > MaximumPotionUpgradeLevel)
                throw new ArgumentOutOfRangeException(nameof(additionalPotionUpgradeLevel));

            PermanentUpgradePurchaseRecord[] records = purchaseRecords == null
                ? Array.Empty<PermanentUpgradePurchaseRecord>()
                : purchaseRecords.ToArray();
            if (records.Any(record => record == null))
                throw new ArgumentException("Progression purchase receipts cannot contain null entries.", nameof(purchaseRecords));
            if (records.Select(record => record.TransactionId).Distinct().Count() != records.Length)
                throw new ArgumentException("Progression purchase receipts must have unique transaction IDs.", nameof(purchaseRecords));

            AvailableRenown = availableRenown;
            HealthUpgradeLevel = healthUpgradeLevel;
            EnergyUpgradeLevel = energyUpgradeLevel;
            AdditionalPotionUpgradeLevel = additionalPotionUpgradeLevel;
            _purchaseRecords = Array.AsReadOnly(records);
        }

        public int AvailableRenown { get; }
        public int HealthUpgradeLevel { get; }
        public int EnergyUpgradeLevel { get; }
        public int AdditionalPotionUpgradeLevel { get; }
        public IReadOnlyList<PermanentUpgradePurchaseRecord> PurchaseRecords => _purchaseRecords;

        public bool TryGetPurchase(TransactionId transactionId, out PermanentUpgradePurchaseRecord record)
        {
            record = _purchaseRecords.FirstOrDefault(item => item.TransactionId == transactionId);
            return record != null;
        }

        internal PermanentUpgradeSnapshot WithPurchase(PermanentUpgradePurchaseRecord record)
        {
            if (record == null) throw new ArgumentNullException(nameof(record));
            if (AvailableRenown < record.Cost) throw new InvalidOperationException("A purchase cannot make the Renown balance negative.");

            int healthLevel = HealthUpgradeLevel;
            int energyLevel = EnergyUpgradeLevel;
            int potionLevel = AdditionalPotionUpgradeLevel;
            switch (record.Kind)
            {
                case PermanentUpgradeKind.MaximumHealth: healthLevel++; break;
                case PermanentUpgradeKind.MaximumEnergy: energyLevel++; break;
                case PermanentUpgradeKind.AdditionalHealthPotion: potionLevel++; break;
                default: throw new ArgumentOutOfRangeException(nameof(record));
            }

            return new PermanentUpgradeSnapshot(AvailableRenown - record.Cost, healthLevel, energyLevel,
                potionLevel, _purchaseRecords.Concat(new[] { record }));
        }
    }
}
