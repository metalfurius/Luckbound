using System;
using System.Collections.Generic;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class QuickConsumableTransactionTests
    {
        [Test]
        public void ConsumptionUsesTheExpectedQuickInstanceAndReplaysWithoutRemovingAnotherUnit()
        {
            ItemCatalog catalog = CreateCatalog();
            ExpeditionInventory inventory = new ExpeditionInventory(catalog, "class.bellator");
            ItemInstanceId instanceId = ItemInstanceId.New();
            Assert.That(inventory.TryCollectItem(new InventoryItemStack(
                instanceId, "consumable.health_potion", ItemRarity.Common, 0, 2), TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.SetQuickConsumable(0, TransactionId.New()).Succeeded, Is.True);

            TransactionId firstUse = TransactionId.New();
            InventoryOperationOutcome consumed = inventory.TryConsumeQuickConsumable(
                instanceId, "consumable.health_potion", firstUse);
            InventoryOperationOutcome replay = inventory.TryConsumeQuickConsumable(
                instanceId, "consumable.health_potion", firstUse);

            Assert.That(consumed.Succeeded, Is.True);
            Assert.That(consumed.WasReplay, Is.False);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.WasReplay, Is.True);
            Assert.That(inventory.QuickConsumable.InstanceId, Is.EqualTo(instanceId));
            Assert.That(inventory.QuickConsumable.Quantity, Is.EqualTo(1));

            ExpeditionInventory restored = ExpeditionInventory.Restore(catalog, inventory.CreateSnapshot());
            Assert.That(restored.HasSuccessfulQuickConsumableConsumption(
                instanceId, "consumable.health_potion", firstUse), Is.True);
            InventoryOperationOutcome restoredReplay = restored.TryConsumeQuickConsumable(
                instanceId, "consumable.health_potion", firstUse);
            Assert.That(restoredReplay.Succeeded, Is.True);
            Assert.That(restoredReplay.WasReplay, Is.True);
            Assert.That(restored.QuickConsumable.Quantity, Is.EqualTo(1));

            InventoryOperationOutcome conflict = inventory.TryConsumeQuickConsumable(
                instanceId, "consumable.bomb", firstUse);
            Assert.That(conflict.Status, Is.EqualTo(InventoryOperationStatus.RejectedTransactionConflict));
            Assert.That(inventory.QuickConsumable.Quantity, Is.EqualTo(1));

            Assert.That(inventory.TryConsumeQuickConsumable(
                instanceId, "consumable.health_potion", TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.QuickConsumable, Is.Null);
            Assert.That(inventory.GetBackpackSlot(0), Is.Null);
        }

        [Test]
        public void ConsumptionRejectsAnInstanceOrItemThatIsNotCurrentlyInTheQuickSlot()
        {
            ItemCatalog catalog = CreateCatalog();
            ExpeditionInventory inventory = new ExpeditionInventory(catalog, "class.bellator");
            ItemInstanceId instanceId = ItemInstanceId.New();
            Assert.That(inventory.TryCollectItem(new InventoryItemStack(
                instanceId, "consumable.antidote", ItemRarity.Common, 0, 1), TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.SetQuickConsumable(0, TransactionId.New()).Succeeded, Is.True);

            InventoryOperationOutcome wrongInstance = inventory.TryConsumeQuickConsumable(
                ItemInstanceId.New(), "consumable.antidote", TransactionId.New());
            InventoryOperationOutcome wrongItem = inventory.TryConsumeQuickConsumable(
                instanceId, "consumable.bomb", TransactionId.New());

            Assert.That(wrongInstance.Status, Is.EqualTo(InventoryOperationStatus.RejectedInvalidQuickConsumable));
            Assert.That(wrongItem.Status, Is.EqualTo(InventoryOperationStatus.RejectedInvalidQuickConsumable));
            Assert.That(inventory.QuickConsumable.InstanceId, Is.EqualTo(instanceId));
            Assert.That(inventory.QuickConsumable.ItemId, Is.EqualTo("consumable.antidote"));
            Assert.That(inventory.QuickConsumable.Quantity, Is.EqualTo(1));
        }

        private static ItemCatalog CreateCatalog()
        {
            var definitions = new List<ItemDefinition>();
            foreach (string itemId in new[]
            {
                "consumable.health_potion", "consumable.energy_tonic", "consumable.antidote",
                "consumable.bomb", "consumable.fortune_brew", "consumable.torch"
            })
            {
                definitions.Add(new ItemDefinition(itemId, "test." + itemId,
                    "test." + itemId + ".description", ItemKind.Consumable, 3));
            }
            foreach (MaterialKind kind in Enum.GetValues(typeof(MaterialKind)))
            {
                string suffix = kind.ToString().ToLowerInvariant();
                definitions.Add(new ItemDefinition("material.test_" + suffix, "test.material." + suffix,
                    "test.material." + suffix + ".description", ItemKind.Material, 99, materialKind: kind));
            }
            return new ItemCatalog(definitions);
        }
    }
}
