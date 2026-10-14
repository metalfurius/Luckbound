using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class PermanentUpgradeRulesTests
    {
        [Test]
        public void ThreePermanentUpgradesCostExactly510AndReachAllCaps()
        {
            var current = new PermanentUpgradeSnapshot(510, 0, 0, 0);
            int totalCost = 0;

            PermanentUpgradePurchaseResult healthOne = Buy(current, PermanentUpgradeKind.MaximumHealth, 1);
            current = healthOne.Snapshot;
            totalCost += healthOne.Cost;
            PermanentUpgradePurchaseResult healthTwo = Buy(current, PermanentUpgradeKind.MaximumHealth, 2);
            current = healthTwo.Snapshot;
            totalCost += healthTwo.Cost;
            PermanentUpgradePurchaseResult energyOne = Buy(current, PermanentUpgradeKind.MaximumEnergy, 3);
            current = energyOne.Snapshot;
            totalCost += energyOne.Cost;
            PermanentUpgradePurchaseResult energyTwo = Buy(current, PermanentUpgradeKind.MaximumEnergy, 4);
            current = energyTwo.Snapshot;
            totalCost += energyTwo.Cost;
            PermanentUpgradePurchaseResult potion = Buy(current, PermanentUpgradeKind.AdditionalHealthPotion, 5);
            current = potion.Snapshot;
            totalCost += potion.Cost;

            Assert.That(new[] { healthOne.Cost, healthTwo.Cost, energyOne.Cost, energyTwo.Cost, potion.Cost },
                Is.EqualTo(new[] { 60, 120, 60, 120, 150 }));
            Assert.That(totalCost, Is.EqualTo(510));
            Assert.That(current.AvailableRenown, Is.Zero);
            Assert.That(current.HealthUpgradeLevel, Is.EqualTo(2));
            Assert.That(current.EnergyUpgradeLevel, Is.EqualTo(2));
            Assert.That(current.AdditionalPotionUpgradeLevel, Is.EqualTo(1));
            Assert.That(current.PurchaseRecords, Has.Count.EqualTo(5));
        }

        [Test]
        public void PurchaseOutsideTheTavernDoesNotChangeProfile()
        {
            var current = new PermanentUpgradeSnapshot(510, 0, 0, 0);
            PermanentUpgradePurchaseResult result = PermanentUpgradeRules.PreparePurchase(current,
                PermanentUpgradeKind.MaximumHealth, TransactionId.New(), isInTavern: false);

            Assert.That(result.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.RejectedNotInTavern));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Snapshot, Is.SameAs(current));
            Assert.That(result.Snapshot.AvailableRenown, Is.EqualTo(510));
            Assert.That(result.Snapshot.HealthUpgradeLevel, Is.Zero);
        }

        [Test]
        public void InsufficientRenownReportsExactNextCostWithoutCharging()
        {
            var current = new PermanentUpgradeSnapshot(119, 1, 0, 0);
            PermanentUpgradePurchaseResult result = PermanentUpgradeRules.PreparePurchase(current,
                PermanentUpgradeKind.MaximumHealth, TransactionId.New(), isInTavern: true);

            Assert.That(result.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.RejectedInsufficientRenown));
            Assert.That(result.Cost, Is.EqualTo(120));
            Assert.That(result.Snapshot, Is.SameAs(current));
            Assert.That(result.Snapshot.AvailableRenown, Is.EqualTo(119));
            Assert.That(result.Snapshot.HealthUpgradeLevel, Is.EqualTo(1));
        }

        [TestCase(PermanentUpgradeKind.MaximumHealth, 2)]
        [TestCase(PermanentUpgradeKind.MaximumEnergy, 2)]
        [TestCase(PermanentUpgradeKind.AdditionalHealthPotion, 1)]
        public void CappedUpgradeRejectsPurchaseWithoutCharging(PermanentUpgradeKind kind, int level)
        {
            var current = new PermanentUpgradeSnapshot(510,
                kind == PermanentUpgradeKind.MaximumHealth ? level : 0,
                kind == PermanentUpgradeKind.MaximumEnergy ? level : 0,
                kind == PermanentUpgradeKind.AdditionalHealthPotion ? level : 0);
            PermanentUpgradePurchaseResult result = PermanentUpgradeRules.PreparePurchase(current,
                kind, TransactionId.New(), isInTavern: true);

            Assert.That(result.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.RejectedMaximumLevel));
            Assert.That(result.Snapshot, Is.SameAs(current));
            Assert.That(result.Snapshot.AvailableRenown, Is.EqualTo(510));
        }

        [Test]
        public void ReplayingCommittedTransactionDoesNotChargeAgainEvenAfterLeavingTavern()
        {
            var current = new PermanentUpgradeSnapshot(60, 0, 0, 0);
            TransactionId id = TransactionId.New();
            PermanentUpgradePurchaseResult first = PermanentUpgradeRules.PreparePurchase(current,
                PermanentUpgradeKind.MaximumHealth, id, isInTavern: true);
            PermanentUpgradePurchaseResult replay = PermanentUpgradeRules.PreparePurchase(first.Snapshot,
                PermanentUpgradeKind.MaximumHealth, id, isInTavern: false);

            Assert.That(first.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.Purchased));
            Assert.That(replay.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.AlreadyProcessed));
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.ChangesProfile, Is.False);
            Assert.That(replay.Snapshot.AvailableRenown, Is.EqualTo(0));
            Assert.That(replay.Snapshot.HealthUpgradeLevel, Is.EqualTo(1));
            Assert.That(replay.Snapshot.PurchaseRecords, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReusingTransactionIdForDifferentUpgradeIsRejectedAsConflict()
        {
            var current = new PermanentUpgradeSnapshot(60, 0, 0, 0);
            TransactionId id = TransactionId.New();
            PermanentUpgradePurchaseResult first = PermanentUpgradeRules.PreparePurchase(current,
                PermanentUpgradeKind.MaximumHealth, id, isInTavern: true);
            PermanentUpgradePurchaseResult conflict = PermanentUpgradeRules.PreparePurchase(first.Snapshot,
                PermanentUpgradeKind.MaximumEnergy, id, isInTavern: true);

            Assert.That(conflict.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.RejectedTransactionConflict));
            Assert.That(conflict.Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(conflict.Snapshot.EnergyUpgradeLevel, Is.Zero);
            Assert.That(conflict.Snapshot.AvailableRenown, Is.Zero);
        }

        [Test]
        public void StartingHealthAndEnergyUseAdditiveBonusesAndRoundHalfUp()
        {
            Assert.That(PermanentUpgradeRules.ApplyFivePercentPerLevel(100, 1), Is.EqualTo(105));
            Assert.That(PermanentUpgradeRules.ApplyFivePercentPerLevel(100, 2), Is.EqualTo(110));
            Assert.That(PermanentUpgradeRules.ApplyFivePercentPerLevel(50, 1), Is.EqualTo(53));
            Assert.That(PermanentUpgradeRules.ApplyFivePercentPerLevel(50, 2), Is.EqualTo(55));
        }

        [Test]
        public void DefaultTransactionIdCannotPurchase()
        {
            var current = new PermanentUpgradeSnapshot(60, 0, 0, 0);
            PermanentUpgradePurchaseResult result = PermanentUpgradeRules.PreparePurchase(current,
                PermanentUpgradeKind.MaximumHealth, default, isInTavern: true);

            Assert.That(result.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.RejectedInvalidTransaction));
            Assert.That(result.Snapshot, Is.SameAs(current));
        }

        private static PermanentUpgradePurchaseResult Buy(PermanentUpgradeSnapshot current,
            PermanentUpgradeKind kind, int transactionSeed)
        {
            var bytes = new byte[16];
            bytes[0] = (byte)transactionSeed;
            bytes[15] = 1;
            TransactionId id = new TransactionId(new System.Guid(bytes));
            PermanentUpgradePurchaseResult result = PermanentUpgradeRules.PreparePurchase(current, kind, id, isInTavern: true);
            Assert.That(result.Status, Is.EqualTo(PermanentUpgradePurchaseStatus.Purchased));
            return result;
        }
    }
}
