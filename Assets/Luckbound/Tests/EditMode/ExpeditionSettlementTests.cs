using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ExpeditionSettlementTests
    {
        [Test]
        public void ExtractionConvertsCatalogLootAndRepeatForTheSameRunAwardsNothing()
        {
            ExpeditionInventory inventory = CreateInventory(gold: 39);
            ItemInstanceId startingWeapon = Equip(inventory, "weapon.test.blade", EquipmentSlot.Weapon, ItemRarity.Common);
            ItemInstanceId startingArmor = Equip(inventory, "armor.test.coat", EquipmentSlot.Armor, ItemRarity.Common);
            Collect(inventory, "weapon.test.blade", ItemRarity.Rare);
            Collect(inventory, "armor.test.coat", ItemRarity.Epic);
            Collect(inventory, "relic.test.coin", ItemRarity.Common);
            Collect(inventory, "consumable.test.potion", ItemRarity.Common);
            inventory.TryAddMaterial(MaterialKind.ShadowWood, 5, TransactionId.New());
            inventory.TryAddMaterial(MaterialKind.Ore, 4, TransactionId.New());
            var ledger = new ExpeditionSettlementLedger(availableRenown: 12);
            RunId runId = RunId.New();

            ExpeditionSettlementReceipt first = ledger.Settle(runId, ExpeditionOutcome.Extracted, 213,
                inventory, new[] { startingWeapon, startingArmor });
            ExpeditionSettlementReceipt repeated = ledger.Settle(runId, ExpeditionOutcome.Death, 999,
                inventory: null);

            Assert.That(first.AlreadyProcessed, Is.False);
            Assert.That(first.AccumulatedRenown, Is.EqualTo(213));
            Assert.That(first.LootRenown, Is.EqualTo(27), "rare weapon 8 + epic armor 9 + relic 8 + 9 materials 1 + 39 gold 1; kit and potion are excluded");
            Assert.That(first.RenownAwarded, Is.EqualTo(240));
            Assert.That(first.BalanceAfter, Is.EqualTo(252));
            Assert.That(repeated.AlreadyProcessed, Is.True);
            Assert.That(repeated.Outcome, Is.EqualTo(ExpeditionOutcome.Extracted));
            Assert.That(repeated.RenownAwarded, Is.Zero);
            Assert.That(repeated.BalanceAfter, Is.EqualTo(252));
            Assert.That(ledger.ProcessedRunIds, Is.EquivalentTo(new[] { runId }));
        }

        [TestCase(0, 0)]
        [TestCase(3, 0)]
        [TestCase(39, 9)]
        [TestCase(213, 53)]
        [TestCase(214, 53)]
        public void DeathKeepsOnlyOneQuarterOfAccumulatedRenownRoundedDown(int accumulatedRenown, int expectedAward)
        {
            ExpeditionInventory inventory = CreateInventory(gold: 100);
            RunId runId = RunId.New();
            var ledger = new ExpeditionSettlementLedger();

            ExpeditionSettlementReceipt result = ledger.Settle(runId, ExpeditionOutcome.Death,
                accumulatedRenown, inventory);

            Assert.That(result.RenownAwarded, Is.EqualTo(expectedAward));
            Assert.That(result.LootRenown, Is.Zero, "death does not convert carried inventory");
            Assert.That(result.BalanceAfter, Is.EqualTo(expectedAward));
        }

        [TestCase(ExpeditionOutcome.Extracted)]
        [TestCase(ExpeditionOutcome.FinalVictory)]
        public void SafeReturnAndFinalVictoryBothConvertAllRenownAndLoot(ExpeditionOutcome outcome)
        {
            ExpeditionInventory inventory = CreateInventory(gold: 40);
            RunId runId = RunId.New();
            var ledger = new ExpeditionSettlementLedger();

            ExpeditionSettlementReceipt result = ledger.Settle(runId, outcome, 87, inventory);

            Assert.That(result.LootRenown, Is.EqualTo(2), "40 gold converts to two Renown");
            Assert.That(result.RenownAwarded, Is.EqualTo(89));
            Assert.That(ledger.AvailableRenown, Is.EqualTo(89));
        }

        [Test]
        public void AbandonmentProcessesTheRunWithoutGrantingRenownOrLootConversion()
        {
            ExpeditionInventory inventory = CreateInventory(gold: 100);
            RunId runId = RunId.New();
            var ledger = new ExpeditionSettlementLedger(availableRenown: 8);

            ExpeditionSettlementReceipt result = ledger.Settle(runId, ExpeditionOutcome.Abandoned, 213, inventory);

            Assert.That(result.LootRenown, Is.Zero);
            Assert.That(result.RenownAwarded, Is.Zero);
            Assert.That(result.BalanceAfter, Is.EqualTo(8));
            Assert.That(ledger.ProcessedRunIds, Is.EquivalentTo(new[] { runId }));
        }

        [Test]
        public void RestoredProfileRunIdsPreventASecondSettlementAfterRestart()
        {
            RunId processedRun = RunId.New();
            var ledger = new ExpeditionSettlementLedger(availableRenown: 41, processedRunIds: new[] { processedRun });

            ExpeditionSettlementReceipt repeated = ledger.Settle(processedRun, ExpeditionOutcome.FinalVictory,
                213, inventory: null);

            Assert.That(repeated.AlreadyProcessed, Is.True);
            Assert.That(repeated.RenownAwarded, Is.Zero);
            Assert.That(ledger.AvailableRenown, Is.EqualTo(41));
            Assert.That(ledger.ProcessedRunIds, Is.EquivalentTo(new[] { processedRun }));
        }

        [Test]
        public void InvalidResultAndOverflowLeaveTheLedgerUnchangedAndRetryable()
        {
            var ledger = new ExpeditionSettlementLedger(int.MaxValue);
            RunId runId = RunId.New();
            ExpeditionInventory inventory = CreateInventory();

            Assert.Throws<ArgumentOutOfRangeException>(() => ledger.Settle(runId, (ExpeditionOutcome)99, 1, inventory));
            Assert.Throws<OverflowException>(() => ledger.Settle(runId, ExpeditionOutcome.FinalVictory, 1, inventory));
            Assert.That(ledger.AvailableRenown, Is.EqualTo(int.MaxValue));
            Assert.That(ledger.ProcessedRunIds, Is.Empty);
        }

        [Test]
        public void ProcessedRunIdsAreUniqueAndStableForSaveSerialization()
        {
            RunId later = RunId.New();
            RunId earlier = RunId.New();
            RunId[] expected = new[] { earlier, later }.OrderBy(run => run.ToString(), StringComparer.Ordinal).ToArray();
            var ledger = new ExpeditionSettlementLedger(processedRunIds: new[] { later, earlier });

            Assert.That(ledger.ProcessedRunIds, Is.EqualTo(expected));
            Assert.Throws<ArgumentException>(() => new ExpeditionSettlementLedger(processedRunIds: new[] { earlier, earlier }));
        }

        private static ExpeditionInventory CreateInventory(int gold = 0)
        {
            ItemCatalog catalog = new ItemCatalog(new[]
            {
                new ItemDefinition("weapon.test.blade", "weapon.test.blade.name", "weapon.test.blade.description",
                    ItemKind.Weapon, 1, new[] { "class.bellator" }),
                new ItemDefinition("armor.test.coat", "armor.test.coat.name", "armor.test.coat.description", ItemKind.Armor, 1),
                new ItemDefinition("relic.test.coin", "relic.test.coin.name", "relic.test.coin.description", ItemKind.Relic, 1),
                new ItemDefinition("consumable.test.potion", "consumable.test.potion.name", "consumable.test.potion.description", ItemKind.Consumable, 3),
                Material("shadow_wood", MaterialKind.ShadowWood),
                Material("ore", MaterialKind.Ore),
                Material("umbra_herb", MaterialKind.UmbraHerb),
                Material("arcane_dust", MaterialKind.ArcaneDust),
                Material("dark_resin", MaterialKind.DarkResin),
                Material("vital_ember", MaterialKind.VitalEmber)
            });
            return new ExpeditionInventory(catalog, "class.bellator", gold);
        }

        private static ItemDefinition Material(string suffix, MaterialKind kind)
        {
            string id = "material." + suffix;
            return new ItemDefinition(id, id + ".name", id + ".description", ItemKind.Material, 99, materialKind: kind);
        }

        private static ItemInstanceId Equip(ExpeditionInventory inventory, string itemId, EquipmentSlot slot, ItemRarity rarity)
        {
            ItemInstanceId id = ItemInstanceId.New();
            Assert.That(inventory.TryCollectItem(new InventoryItemStack(id, itemId, rarity, 0, 1), TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.EquipBackpackItem(0, slot, TransactionId.New()).Succeeded, Is.True);
            return id;
        }

        private static void Collect(ExpeditionInventory inventory, string itemId, ItemRarity rarity)
        {
            Assert.That(inventory.TryCollectItem(new InventoryItemStack(ItemInstanceId.New(), itemId, rarity, 0, 1),
                TransactionId.New()).Succeeded, Is.True);
        }
    }
}
