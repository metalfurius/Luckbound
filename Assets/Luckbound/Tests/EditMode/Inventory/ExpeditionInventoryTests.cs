using System;
using System.Collections.Generic;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ExpeditionInventoryTests
    {
        [Test]
        public void EquippingReturnsPreviousItemToTheSameBackpackIndex()
        {
            ItemCatalog catalog = CreateCatalog();
            var inventory = new ExpeditionInventory(catalog, "class.bellator");
            InventoryItemStack first = Stack("weapon.bellator.sword");
            InventoryItemStack second = Stack("weapon.bellator.sword");

            Assert.That(inventory.TryCollectItem(first, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.TryCollectItem(second, TransactionId.New()).Succeeded, Is.True);
            InventoryOperationOutcome swap = inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New());

            Assert.That(swap.Succeeded, Is.True);
            Assert.That(inventory.Weapon.InstanceId, Is.EqualTo(second.InstanceId));
            Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(first.InstanceId));
            Assert.That(swap.DisplacedItem.InstanceId, Is.EqualTo(first.InstanceId));
        }

        [Test]
        public void IncompatibleWeaponsAndWrongEquipmentSlotsLeaveStateUntouched()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack bow = Stack("weapon.venator.bow");
            Assert.That(inventory.TryCollectItem(bow, TransactionId.New()).Succeeded, Is.True);

            InventoryOperationOutcome incompatible = inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New());
            InventoryOperationOutcome wrongSlot = inventory.EquipBackpackItem(0, EquipmentSlot.Armor, TransactionId.New());

            Assert.That(incompatible.Status, Is.EqualTo(InventoryOperationStatus.RejectedIncompatibleClass));
            Assert.That(wrongSlot.Status, Is.EqualTo(InventoryOperationStatus.RejectedWrongEquipmentSlot));
            Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(bow.InstanceId));
            Assert.That(inventory.Weapon, Is.Null);
            Assert.That(inventory.Armor, Is.Null);
        }

        [Test]
        public void QuickConsumableReferencesOneStackAndClearsWhenThatStackIsReplaced()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack firstPile = Stack("consumable.health_potion", quantity: 2);
            Assert.That(inventory.TryCollectItem(firstPile, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.SetQuickConsumable(0, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.QuickConsumable.InstanceId, Is.EqualTo(firstPile.InstanceId));

            InventoryItemStack secondPile = Stack("consumable.health_potion", quantity: 1);
            Assert.That(inventory.TryCollectItem(secondPile, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.GetBackpackSlot(0).Quantity, Is.EqualTo(3));
            Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(firstPile.InstanceId));
            Assert.That(inventory.QuickConsumableInstanceId, Is.EqualTo(firstPile.InstanceId));

            InventoryItemStack replacement = Stack("consumable.energy_tonic");
            InventoryOperationOutcome outcome = inventory.TryReplaceBackpackSlot(0, replacement, TransactionId.New());
            Assert.That(outcome.Succeeded, Is.True);
            Assert.That(outcome.DisplacedItem.InstanceId, Is.EqualTo(firstPile.InstanceId));
            Assert.That(inventory.QuickConsumable, Is.Null);
            Assert.That(inventory.QuickConsumableInstanceId.IsValid, Is.False);
        }

        [Test]
        public void ConsumablesStackToThreeAndUseAnotherIndexForRemainder()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            Assert.That(inventory.TryCollectItem(Stack("consumable.health_potion", quantity: 3), TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.TryCollectItem(Stack("consumable.health_potion", quantity: 2), TransactionId.New()).Succeeded, Is.True);

            Assert.That(inventory.GetBackpackSlot(0).Quantity, Is.EqualTo(3));
            Assert.That(inventory.GetBackpackSlot(1).Quantity, Is.EqualTo(2));
            Assert.That(inventory.GetBackpackSlot(2), Is.Null);
        }

        [Test]
        public void OnlyWeaponsAndArmorAcceptUpgradeLevelsAndConsumablesMustRemainCommon()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack upgradedWeapon = new InventoryItemStack(
                ItemInstanceId.New(), "weapon.bellator.sword", ItemRarity.Epic, 3, 1);
            InventoryItemStack upgradedArmor = new InventoryItemStack(
                ItemInstanceId.New(), "armor.bellator.cuirass", ItemRarity.Rare, 1, 1);
            InventoryItemStack upgradedRelic = new InventoryItemStack(
                ItemInstanceId.New(), "relic.marked_coin", ItemRarity.Rare, 1, 1);
            InventoryItemStack rarePotion = new InventoryItemStack(
                ItemInstanceId.New(), "consumable.health_potion", ItemRarity.Rare, 0, 1);

            Assert.That(inventory.TryCollectItem(upgradedWeapon, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.TryCollectItem(upgradedArmor, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.TryCollectItem(upgradedRelic, TransactionId.New()).Status,
                Is.EqualTo(InventoryOperationStatus.RejectedInvalidItem));
            Assert.That(inventory.TryCollectItem(rarePotion, TransactionId.New()).Status,
                Is.EqualTo(InventoryOperationStatus.RejectedInvalidItem));
            Assert.That(inventory.GetBackpackSlot(0).UpgradeLevel, Is.EqualTo(3));
            Assert.That(inventory.GetBackpackSlot(1).UpgradeLevel, Is.EqualTo(1));
            Assert.That(inventory.GetBackpackSlot(2), Is.Null);
        }

        [Test]
        public void RelicSlotsRejectTheSameDefinitionWithoutMovingTheSecondCopy()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack first = Stack("relic.marked_coin");
            InventoryItemStack second = Stack("relic.marked_coin");
            Assert.That(inventory.TryCollectItem(first, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.EquipBackpackItem(0, EquipmentSlot.RelicOne, TransactionId.New()).Succeeded, Is.True);
            Assert.That(inventory.TryCollectItem(second, TransactionId.New()).Succeeded, Is.True);

            InventoryOperationOutcome duplicate = inventory.EquipBackpackItem(0, EquipmentSlot.RelicTwo, TransactionId.New());
            Assert.That(duplicate.Status, Is.EqualTo(InventoryOperationStatus.RejectedDuplicateRelic));
            Assert.That(inventory.RelicOne.InstanceId, Is.EqualTo(first.InstanceId));
            Assert.That(inventory.RelicTwo, Is.Null);
            Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(second.InstanceId));
        }

        [Test]
        public void MaterialsHaveIndependentNinetyNineLimitAndGoldUsesItsOwnCounter()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator", initialGold: 11);
            InventoryOperationOutcome add = inventory.TryAddMaterial(MaterialKind.Ore, 99, TransactionId.New());
            InventoryOperationOutcome overflow = inventory.TryAddMaterial(MaterialKind.Ore, 1, TransactionId.New());
            InventoryOperationOutcome gold = inventory.TryAddGold(25, TransactionId.New());

            Assert.That(add.Succeeded, Is.True);
            Assert.That(overflow.Status, Is.EqualTo(InventoryOperationStatus.RejectedMaterialCapacity));
            Assert.That(inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(99));
            Assert.That(inventory.GetMaterialCount(MaterialKind.ShadowWood), Is.Zero);
            Assert.That(gold.Succeeded, Is.True);
            Assert.That(inventory.Gold, Is.EqualTo(36));
            Assert.That(inventory.Backpack.Count, Is.EqualTo(ExpeditionInventory.BackpackCapacity));
            Assert.That(inventory.Backpack[0], Is.Null);
        }

        [Test]
        public void FullBackpackRejectsPickupAndExplicitReplacementReturnsDisplacedInstance()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack[] original = Fill(inventory);
            InventoryItemStack rejectedPickup = Stack("relic.hidden_pouch");
            InventoryOperationOutcome rejected = inventory.TryCollectItem(rejectedPickup, TransactionId.New());
            Assert.That(rejected.Status, Is.EqualTo(InventoryOperationStatus.RejectedFull));
            AssertBackpackMatches(inventory, original);

            InventoryItemStack incoming = Stack("relic.hidden_pouch");
            InventoryOperationOutcome swap = inventory.TryReplaceBackpackSlot(5, incoming, TransactionId.New());
            Assert.That(swap.Succeeded, Is.True);
            Assert.That(swap.DisplacedItem.InstanceId, Is.EqualTo(original[5].InstanceId));
            Assert.That(inventory.GetBackpackSlot(5).InstanceId, Is.EqualTo(incoming.InstanceId));
            Assert.That(inventory.Backpack.Count, Is.EqualTo(12));
        }

        [Test]
        public void RepeatedTransactionsReplaySuccessOrRejectionWithoutApplyingAgain()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack[] original = Fill(inventory);
            InventoryItemStack blocked = Stack("relic.hidden_pouch");
            TransactionId rejectedId = TransactionId.New();
            InventoryOperationOutcome firstRejection = inventory.TryCollectItem(blocked, rejectedId);
            Assert.That(firstRejection.Status, Is.EqualTo(InventoryOperationStatus.RejectedFull));

            InventoryItemStack removedByEquip = inventory.GetBackpackSlot(0);
            Assert.That(inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New()).Succeeded, Is.True);
            InventoryOperationOutcome retry = inventory.TryCollectItem(blocked, rejectedId);
            InventoryOperationOutcome collision = inventory.TryCollectItem(Stack("consumable.health_potion"), rejectedId);

            Assert.That(retry, Is.SameAs(firstRejection));
            Assert.That(collision.Status, Is.EqualTo(InventoryOperationStatus.RejectedTransactionConflict));
            Assert.That(inventory.GetBackpackSlot(0), Is.Null);
            Assert.That(inventory.Weapon.InstanceId, Is.EqualTo(removedByEquip.InstanceId));
            for (int index = 1; index < original.Length; index++)
            {
                Assert.That(inventory.GetBackpackSlot(index).InstanceId, Is.EqualTo(original[index].InstanceId));
            }

            InventoryItemStack successfulItem = Stack("consumable.health_potion");
            TransactionId successId = TransactionId.New();
            InventoryOperationOutcome success = inventory.TryCollectItem(successfulItem, successId);
            Assert.That(success.Succeeded, Is.True);
            Assert.That(inventory.TryCollectItem(successfulItem, successId), Is.SameAs(success));
        }

        [Test]
        public void InvalidTransactionsItemsAndSlotsAreRejectedWithoutChangingState()
        {
            var inventory = new ExpeditionInventory(CreateCatalog(), "class.bellator");
            InventoryItemStack item = Stack("weapon.bellator.sword");

            Assert.That(inventory.TryCollectItem(item, default).Status,
                Is.EqualTo(InventoryOperationStatus.RejectedInvalidTransaction));
            Assert.That(inventory.TryCollectItem(Stack("item.missing"), TransactionId.New()).Status,
                Is.EqualTo(InventoryOperationStatus.RejectedUnknownItem));
            Assert.That(inventory.TryReplaceBackpackSlot(-1, item, TransactionId.New()).Status,
                Is.EqualTo(InventoryOperationStatus.RejectedInvalidSlot));
            Assert.That(inventory.GetBackpackSlot(0), Is.Null);
            Assert.That(inventory.Gold, Is.Zero);
            Assert.That(inventory.Weapon, Is.Null);
        }

        private static InventoryItemStack[] Fill(ExpeditionInventory inventory)
        {
            var stacks = new InventoryItemStack[ExpeditionInventory.BackpackCapacity];
            for (int index = 0; index < stacks.Length; index++)
            {
                stacks[index] = Stack(index % 2 == 0 ? "weapon.bellator.sword" : "relic.hidden_pouch");
                InventoryOperationOutcome outcome = inventory.TryCollectItem(stacks[index], TransactionId.New());
                Assert.That(outcome.Succeeded, Is.True, "Could not fill backpack slot " + index + ".");
            }
            return stacks;
        }

        private static void AssertBackpackMatches(ExpeditionInventory inventory, InventoryItemStack[] expected)
        {
            Assert.That(inventory.Backpack.Count, Is.EqualTo(expected.Length));
            for (int index = 0; index < expected.Length; index++)
            {
                Assert.That(inventory.GetBackpackSlot(index).InstanceId, Is.EqualTo(expected[index].InstanceId));
            }
        }

        private static InventoryItemStack Stack(string itemId, int quantity = 1)
        {
            return new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, quantity);
        }

        private static ItemCatalog CreateCatalog()
        {
            var definitions = new List<ItemDefinition>
            {
                Definition("weapon.bellator.sword", ItemKind.Weapon, 1, "class.bellator"),
                Definition("weapon.venator.bow", ItemKind.Weapon, 1, "class.venator"),
                Definition("weapon.magus.staff", ItemKind.Weapon, 1, "class.magus"),
                Definition("armor.bellator.cuirass", ItemKind.Armor, 1),
                Definition("armor.venator.vest", ItemKind.Armor, 1),
                Definition("armor.magus.robe", ItemKind.Armor, 1),
                Definition("relic.marked_coin", ItemKind.Relic, 1),
                Definition("relic.hidden_pouch", ItemKind.Relic, 1),
                Definition("consumable.health_potion", ItemKind.Consumable, 3),
                Definition("consumable.energy_tonic", ItemKind.Consumable, 3),
                Definition("material.shadow_wood", ItemKind.Material, 99, materialKind: MaterialKind.ShadowWood),
                Definition("material.ore", ItemKind.Material, 99, materialKind: MaterialKind.Ore),
                Definition("material.umbra_herb", ItemKind.Material, 99, materialKind: MaterialKind.UmbraHerb),
                Definition("material.arcane_dust", ItemKind.Material, 99, materialKind: MaterialKind.ArcaneDust),
                Definition("material.dark_resin", ItemKind.Material, 99, materialKind: MaterialKind.DarkResin),
                Definition("material.vital_ember", ItemKind.Material, 99, materialKind: MaterialKind.VitalEmber)
            };
            return new ItemCatalog(definitions);
        }

        private static ItemDefinition Definition(
            string id,
            ItemKind kind,
            int maxStack,
            string compatibleClassId = null,
            MaterialKind? materialKind = null)
        {
            return new ItemDefinition(id, "name." + id, "description." + id, kind, maxStack,
                compatibleClassId == null ? Array.Empty<string>() : new[] { compatibleClassId }, materialKind);
        }
    }
}
