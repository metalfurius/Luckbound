using System;
using Luckbound.Content;
using Luckbound.Core.Crafting;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.Crafting
{
    public sealed class CraftingTransactionTests
    {
        private ItemCatalog _itemCatalog;
        private RecipeCatalog _recipeCatalog;

        [SetUp]
        public void SetUp()
        {
            TextAsset items = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            TextAsset recipes = Resources.Load<TextAsset>("Luckbound/Data/Items/Recipes");
            Assert.That(items, Is.Not.Null, "Items.json must be available as a Resources asset.");
            Assert.That(recipes, Is.Not.Null, "Recipes.json must be available as a Resources asset.");
            _itemCatalog = ItemCatalogJsonLoader.Load(items.text);
            _recipeCatalog = RecipeCatalogJsonLoader.Load(recipes.text, _itemCatalog);
        }

        [Test]
        public void ConsumableCraftDebitsExactMaterialsAndMergesIntoAnExistingPile()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator", initialGold: 7);
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            Assert.That(inventory.TryCollectWorldMaterial(MaterialKind.Ore, 4, true, TransactionId.New()).Succeeded, Is.True);
            InventoryItemStack pile = Collect(inventory, "consumable.health_potion", quantity: 2);

            CraftingOutcome outcome = inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", TransactionId.New());

            Assert.That(outcome.Status, Is.EqualTo(CraftingOperationStatus.Succeeded));
            Assert.That(outcome.ResultStacks.Count, Is.EqualTo(1));
            Assert.That(outcome.ResultStack.InstanceId, Is.EqualTo(pile.InstanceId));
            Assert.That(outcome.ResultStack.Quantity, Is.EqualTo(3));
            Assert.That(inventory.GetBackpackSlot(0).Quantity, Is.EqualTo(3));
            Assert.That(inventory.GetMaterialCount(MaterialKind.UmbraHerb), Is.Zero);
            Assert.That(inventory.GetMaterialCount(MaterialKind.DarkResin), Is.Zero);
            Assert.That(inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(4));
            Assert.That(inventory.GetWorldMaterialRemainder(MaterialKind.Ore), Is.EqualTo(4));
            Assert.That(inventory.Gold, Is.EqualTo(7));
        }

        [Test]
        public void FullBackpackCanCraftIntoACompatibleStackWithRoom()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            InventoryItemStack potionPile = null;
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
            {
                InventoryItemStack item = index == 4
                    ? NewStack("consumable.health_potion", quantity: 2)
                    : NewStack("relic.marked_coin");
                Assert.That(inventory.TryCollectItem(item, TransactionId.New()).Succeeded, Is.True);
                if (index == 4) potionPile = item;
            }

            CraftingOutcome outcome = inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", TransactionId.New());

            Assert.That(outcome.Status, Is.EqualTo(CraftingOperationStatus.Succeeded));
            Assert.That(inventory.Backpack.Count, Is.EqualTo(12));
            Assert.That(inventory.GetBackpackSlot(4).InstanceId, Is.EqualTo(potionPile.InstanceId));
            Assert.That(inventory.GetBackpackSlot(4).Quantity, Is.EqualTo(3));
            Assert.That(outcome.ResultStack.InstanceId, Is.EqualTo(potionPile.InstanceId));
        }

        [Test]
        public void ConsumableCraftRejectsWhenNoStackOrBackpackSlotCanHoldTheResult()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                Assert.That(inventory.TryCollectItem(NewStack("relic.marked_coin"), TransactionId.New()).Succeeded, Is.True);
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();
            TransactionId id = TransactionId.New();

            CraftingOutcome rejected = inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", id);
            ExpeditionInventorySnapshot after = inventory.CreateSnapshot();

            Assert.That(rejected.Status, Is.EqualTo(CraftingOperationStatus.RejectedBackpackFull));
            AssertGameplaySnapshotEqual(before, after);
            Assert.That(after.transactions.Length, Is.EqualTo(before.transactions.Length + 1));
            Assert.That(inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", id), Is.SameAs(rejected));
            AssertGameplaySnapshotEqual(after, inventory.CreateSnapshot());
        }

        [Test]
        public void InsufficientMaterialsRejectWithoutChangingGameplaySnapshot()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();

            CraftingOutcome rejected = inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", TransactionId.New());

            Assert.That(rejected.Status, Is.EqualTo(CraftingOperationStatus.RejectedInsufficientMaterials));
            AssertGameplaySnapshotEqual(before, inventory.CreateSnapshot());
        }

        [Test]
        public void InsufficientGoldRejectsUpgradeWithoutChangingGameplaySnapshot()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator", initialGold: 9);
            SeedRecipeMaterials(inventory, "recipe.upgrade.weapon.1");
            InventoryItemStack sword = Collect(inventory, "weapon.bellator.rusted_sword");
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();

            CraftingOutcome rejected = inventory.TryCraft(_recipeCatalog, "recipe.upgrade.weapon.1", TransactionId.New(), sword.InstanceId);

            Assert.That(rejected.Status, Is.EqualTo(CraftingOperationStatus.RejectedInsufficientGold));
            AssertGameplaySnapshotEqual(before, inventory.CreateSnapshot());
        }

        [Test]
        public void UnknownRecipeIdIsRejectedWithoutChangingGameplaySnapshot()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();

            CraftingOutcome rejected = inventory.TryCraft(_recipeCatalog, "recipe.missing", TransactionId.New());

            Assert.That(rejected.Status, Is.EqualTo(CraftingOperationStatus.RejectedUnknownRecipe));
            AssertGameplaySnapshotEqual(before, inventory.CreateSnapshot());
        }

        [Test]
        public void UpgradeRejectsMissingTargetUnexpectedTargetWrongCategoryClassAndInvalidLevelsAtomically()
        {
            AssertRejectedUpgrade("recipe.upgrade.weapon.1", null, 0, CraftingOperationStatus.RejectedMissingTarget);
            AssertRejectedUpgrade("recipe.upgrade.weapon.1", "armor.bellator.guild_cuirass", 0,
                CraftingOperationStatus.RejectedWrongTargetKind);
            AssertRejectedUpgrade("recipe.upgrade.weapon.1", "weapon.venator.short_bow", 0,
                CraftingOperationStatus.RejectedIncompatibleClass);
            AssertRejectedUpgrade("recipe.upgrade.weapon.2", "weapon.bellator.rusted_sword", 0,
                CraftingOperationStatus.RejectedInvalidUpgradeLevel);
            AssertRejectedUpgrade("recipe.upgrade.weapon.3", "weapon.bellator.rusted_sword", 0,
                CraftingOperationStatus.RejectedInvalidUpgradeLevel);
            AssertRejectedUpgrade("recipe.upgrade.weapon.3", "weapon.bellator.rusted_sword", 3,
                CraftingOperationStatus.RejectedUpgradeLimit);

            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator", initialGold: 100);
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            InventoryItemStack target = Collect(inventory, "weapon.bellator.rusted_sword");
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();
            CraftingOutcome unexpectedTarget = inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion",
                TransactionId.New(), target.InstanceId);
            Assert.That(unexpectedTarget.Status, Is.EqualTo(CraftingOperationStatus.RejectedUnexpectedTarget));
            AssertGameplaySnapshotEqual(before, inventory.CreateSnapshot());
        }

        [Test]
        public void WeaponAndArmorUpgradesApplyOnlyConsecutiveLevelsAndExactCosts()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator", initialGold: 140);
            AddMaterial(inventory, MaterialKind.ShadowWood, 1);
            AddMaterial(inventory, MaterialKind.ArcaneDust, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            AddMaterial(inventory, MaterialKind.VitalEmber, 2);
            Assert.That(inventory.TryCollectWorldMaterial(MaterialKind.Ore, 4, true, TransactionId.New()).Succeeded, Is.True);
            AddMaterial(inventory, MaterialKind.Ore, 12);
            InventoryItemStack sword = Collect(inventory, "weapon.bellator.rusted_sword");
            InventoryItemStack armor = Collect(inventory, "armor.bellator.guild_cuirass");

            foreach (string target in new[] { "weapon", "armor" })
            {
                ItemInstanceId targetId = target == "weapon" ? sword.InstanceId : armor.InstanceId;
                string prefix = "recipe.upgrade." + target + ".";
                for (int level = 1; level <= 3; level++)
                {
                    CraftingOutcome upgraded = inventory.TryCraft(_recipeCatalog, prefix + level, TransactionId.New(), targetId);
                    Assert.That(upgraded.Status, Is.EqualTo(CraftingOperationStatus.Succeeded), prefix + level);
                    Assert.That(upgraded.ResultStack.InstanceId, Is.EqualTo(targetId));
                    Assert.That(upgraded.ResultStack.UpgradeLevel, Is.EqualTo(level));
                }
            }

            Assert.That(inventory.GetMaterialCount(MaterialKind.Ore), Is.Zero);
            Assert.That(inventory.GetMaterialCount(MaterialKind.ShadowWood), Is.Zero);
            Assert.That(inventory.GetMaterialCount(MaterialKind.ArcaneDust), Is.Zero);
            Assert.That(inventory.GetMaterialCount(MaterialKind.DarkResin), Is.Zero);
            Assert.That(inventory.GetMaterialCount(MaterialKind.VitalEmber), Is.Zero);
            Assert.That(inventory.GetWorldMaterialRemainder(MaterialKind.Ore), Is.EqualTo(4));
            Assert.That(inventory.Gold, Is.Zero);
            Assert.That(inventory.GetBackpackSlot(0).UpgradeLevel, Is.EqualTo(3));
            Assert.That(inventory.GetBackpackSlot(1).UpgradeLevel, Is.EqualTo(3));
        }

        [Test]
        public void SuccessfulAndRejectedTransactionResultsReplayAfterSnapshotRestore()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            InventoryItemStack pile = Collect(inventory, "consumable.health_potion", quantity: 2);
            TransactionId successId = TransactionId.New();
            CraftingOutcome successful = inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", successId);
            ExpeditionInventorySnapshot craftedSnapshot = inventory.CreateSnapshot();
            Assert.That(inventory.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", successId), Is.SameAs(successful));

            ExpeditionInventory restored = ExpeditionInventory.Restore(_itemCatalog, craftedSnapshot);
            ExpeditionInventorySnapshot beforeReplay = restored.CreateSnapshot();
            CraftingOutcome restoredReplay = restored.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", successId);

            Assert.That(restoredReplay.Succeeded, Is.True);
            Assert.That(restoredReplay.ResultStack.InstanceId, Is.EqualTo(pile.InstanceId));
            Assert.That(restoredReplay.ResultStack.Quantity, Is.EqualTo(3));
            Assert.That(restored.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", successId),
                Is.SameAs(restoredReplay));
            CraftingOutcome conflictingReplay = restored.TryCraft(_recipeCatalog, "recipe.consumable.energy_tonic", successId);
            Assert.That(conflictingReplay.Status, Is.EqualTo(CraftingOperationStatus.RejectedTransactionConflict));
            AssertGameplaySnapshotEqual(beforeReplay, restored.CreateSnapshot());
            Assert.That(restored.CreateSnapshot().transactions.Length, Is.EqualTo(beforeReplay.transactions.Length));

            var rejectedInventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            TransactionId rejectedId = TransactionId.New();
            CraftingOutcome insufficient = rejectedInventory.TryCraft(_recipeCatalog,
                "recipe.consumable.health_potion", rejectedId);
            ExpeditionInventory rejectedRestored = ExpeditionInventory.Restore(_itemCatalog, rejectedInventory.CreateSnapshot());
            AddMaterial(rejectedRestored, MaterialKind.UmbraHerb, 2);
            AddMaterial(rejectedRestored, MaterialKind.DarkResin, 1);
            ExpeditionInventorySnapshot beforeRejectedReplay = rejectedRestored.CreateSnapshot();
            CraftingOutcome rejectedReplay = rejectedRestored.TryCraft(_recipeCatalog,
                "recipe.consumable.health_potion", rejectedId);
            Assert.That(rejectedRestored.TryCraft(_recipeCatalog, "recipe.consumable.health_potion", rejectedId),
                Is.SameAs(rejectedReplay));

            Assert.That(insufficient.Status, Is.EqualTo(CraftingOperationStatus.RejectedInsufficientMaterials));
            Assert.That(rejectedReplay.Status, Is.EqualTo(CraftingOperationStatus.RejectedInsufficientMaterials));
            AssertGameplaySnapshotEqual(beforeRejectedReplay, rejectedRestored.CreateSnapshot());
            Assert.That(rejectedRestored.GetMaterialCount(MaterialKind.UmbraHerb), Is.EqualTo(2));
            Assert.That(rejectedRestored.GetMaterialCount(MaterialKind.DarkResin), Is.EqualTo(1));
        }

        [Test]
        public void VersionTwoSnapshotRestoresDvergarProgressAndLegacyTransactionReceipts()
        {
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator");
            Assert.That(inventory.TryCollectWorldMaterial(MaterialKind.Ore, 3, true, TransactionId.New()).Succeeded, Is.True);
            TransactionId materialTransaction = TransactionId.New();
            Assert.That(inventory.TryAddMaterial(MaterialKind.ShadowWood, 1, materialTransaction).Succeeded, Is.True);
            ExpeditionInventorySnapshot versionTwo = inventory.CreateSnapshot();
            versionTwo.snapshotVersion = 2;
            foreach (InventoryTransactionSnapshot transaction in versionTwo.transactions)
            {
                transaction.craftingStatus = 0;
                transaction.craftingRecipeId = null;
                transaction.craftingResultStacks = null;
            }

            ExpeditionInventory restored = ExpeditionInventory.Restore(_itemCatalog, versionTwo);
            Assert.That(restored.GetWorldMaterialRemainder(MaterialKind.Ore), Is.EqualTo(3));
            Assert.That(restored.GetMaterialCount(MaterialKind.ShadowWood), Is.EqualTo(1));
            Assert.That(restored.TryAddMaterial(MaterialKind.ShadowWood, 1, materialTransaction).Succeeded, Is.True);
            Assert.That(restored.GetMaterialCount(MaterialKind.ShadowWood), Is.EqualTo(1));
        }

        private void AssertRejectedUpgrade(
            string recipeId,
            string targetItemId,
            int currentLevel,
            CraftingOperationStatus expectedStatus)
        {
            RecipeDefinition recipe = _recipeCatalog.Get(recipeId);
            var inventory = new ExpeditionInventory(_itemCatalog, "class.bellator", initialGold: 100);
            SeedRecipeMaterials(inventory, recipeId);
            InventoryItemStack target = null;
            if (targetItemId != null)
            {
                target = new InventoryItemStack(ItemInstanceId.New(), targetItemId, ItemRarity.Common, currentLevel, 1);
                Assert.That(inventory.TryCollectItem(target, TransactionId.New()).Succeeded, Is.True);
            }
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();

            CraftingOutcome rejected = inventory.TryCraft(_recipeCatalog, recipeId, TransactionId.New(),
                target == null ? default : target.InstanceId);

            Assert.That(rejected.Status, Is.EqualTo(expectedStatus), recipe.Id);
            AssertGameplaySnapshotEqual(before, inventory.CreateSnapshot());
        }

        private void SeedRecipeMaterials(ExpeditionInventory inventory, string recipeId)
        {
            foreach (RecipeMaterialCost cost in _recipeCatalog.Get(recipeId).MaterialCosts)
                AddMaterial(inventory, cost.MaterialKind, cost.Quantity);
        }

        private static void AddMaterial(ExpeditionInventory inventory, MaterialKind kind, int quantity)
        {
            Assert.That(inventory.TryAddMaterial(kind, quantity, TransactionId.New()).Succeeded, Is.True, kind.ToString());
        }

        private static InventoryItemStack Collect(ExpeditionInventory inventory, string itemId, int quantity = 1)
        {
            InventoryItemStack item = NewStack(itemId, quantity);
            Assert.That(inventory.TryCollectItem(item, TransactionId.New()).Succeeded, Is.True, itemId);
            return item;
        }

        private static InventoryItemStack NewStack(string itemId, int quantity = 1)
        {
            return new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, quantity);
        }

        private static void AssertGameplaySnapshotEqual(ExpeditionInventorySnapshot expected, ExpeditionInventorySnapshot actual)
        {
            Assert.That(actual.snapshotVersion, Is.EqualTo(expected.snapshotVersion));
            Assert.That(actual.classId, Is.EqualTo(expected.classId));
            Assert.That(actual.gold, Is.EqualTo(expected.gold));
            AssertStackEqual(expected.weapon, actual.weapon);
            AssertStackEqual(expected.armor, actual.armor);
            AssertStackEqual(expected.relicOne, actual.relicOne);
            AssertStackEqual(expected.relicTwo, actual.relicTwo);
            Assert.That(actual.quickConsumableInstanceId, Is.EqualTo(expected.quickConsumableInstanceId));
            Assert.That(actual.backpackSlots.Length, Is.EqualTo(expected.backpackSlots.Length));
            for (int index = 0; index < expected.backpackSlots.Length; index++)
                AssertStackEqual(expected.backpackSlots[index], actual.backpackSlots[index]);
            Assert.That(actual.materials.Length, Is.EqualTo(expected.materials.Length));
            for (int index = 0; index < expected.materials.Length; index++)
            {
                Assert.That(actual.materials[index].kind, Is.EqualTo(expected.materials[index].kind));
                Assert.That(actual.materials[index].count, Is.EqualTo(expected.materials[index].count));
            }
            Assert.That(actual.worldMaterialRemainders.Length, Is.EqualTo(expected.worldMaterialRemainders.Length));
            for (int index = 0; index < expected.worldMaterialRemainders.Length; index++)
            {
                Assert.That(actual.worldMaterialRemainders[index].kind, Is.EqualTo(expected.worldMaterialRemainders[index].kind));
                Assert.That(actual.worldMaterialRemainders[index].remainder, Is.EqualTo(expected.worldMaterialRemainders[index].remainder));
            }
        }

        private static void AssertStackEqual(InventoryStackSnapshot expected, InventoryStackSnapshot actual)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.instanceId, Is.EqualTo(expected.instanceId));
            Assert.That(actual.itemId, Is.EqualTo(expected.itemId));
            Assert.That(actual.rarity, Is.EqualTo(expected.rarity));
            Assert.That(actual.upgradeLevel, Is.EqualTo(expected.upgradeLevel));
            Assert.That(actual.quantity, Is.EqualTo(expected.quantity));
        }
    }
}
