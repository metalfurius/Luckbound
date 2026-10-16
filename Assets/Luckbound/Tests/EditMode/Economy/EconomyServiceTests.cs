using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Crafting;
using Luckbound.Core.Economy;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Economy
{
    public sealed class EconomyServiceTests
    {
        private ItemCatalog _items;
        private RecipeCatalog _recipes;

        [SetUp]
        public void SetUp()
        {
            _items = CreateCatalog();
            _recipes = CreateUpgradeRecipes();
        }

        [Test]
        public void RestStockHasElevenFixedOffersAndSurvivesReopenAndRestore()
        {
            RunId runId = RunId.New();
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 100);

            MerchantStockSnapshot initial = MerchantService.EnsureStock(inventory, runId, roomId);
            RoomInstanceId secondRoomId = RoomInstanceId.New();
            MerchantStockSnapshot secondRoomStock = MerchantService.EnsureStock(inventory, runId, secondRoomId);
            Assert.That(initial.offers.Length, Is.EqualTo(11));
            Assert.That(Offer(secondRoomStock, "consumable.health_potion").remainingQuantity, Is.EqualTo(2));
            AssertOffer(initial, "consumable.health_potion", 2);
            AssertOffer(initial, "consumable.energy_tonic", 1);
            AssertOffer(initial, "material.shadow_wood", 2);
            AssertOffer(initial, "material.ore", 2);
            AssertOffer(initial, "material.umbra_herb", 2);
            AssertOffer(initial, "material.arcane_dust", 2);
            AssertOffer(initial, "material.dark_resin", 2);
            AssertOffer(initial, "material.vital_ember", 2);
            AssertOffer(initial, "weapon.bellator.rusted_sword", 1);
            AssertOffer(initial, "armor.bellator.guild_cuirass", 1);
            AssertOffer(initial, "relic.marked_coin", 1);

            EconomyTransactionOutcome purchase = MerchantService.TryPurchase(inventory, runId, roomId,
                "consumable.health_potion", 1, 0, TransactionId.New());
            Assert.That(purchase.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(inventory.Gold, Is.EqualTo(82));

            MerchantStockSnapshot reopened = MerchantService.EnsureStock(inventory, runId, roomId);
            Assert.That(Offer(reopened, "consumable.health_potion").remainingQuantity, Is.EqualTo(1));
            Assert.That(reopened.offers.Length, Is.EqualTo(11));

            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            MerchantStockSnapshot afterRestore = MerchantService.EnsureStock(restored, runId, roomId);
            Assert.That(afterRestore.offers.Length, Is.EqualTo(11));
            Assert.That(Offer(afterRestore, "consumable.health_potion").remainingQuantity, Is.EqualTo(1));
            MerchantStockSnapshot restoredSecondRoom = MerchantService.EnsureStock(restored, runId, secondRoomId);
            Assert.That(Offer(restoredSecondRoom, "consumable.health_potion").remainingQuantity, Is.EqualTo(2));
            Assert.That(restored.Gold, Is.EqualTo(82));
            Assert.Throws<System.InvalidOperationException>(() => MerchantService.EnsureStock(restored, RunId.New(), roomId));
        }

        [Test]
        public void PurchasesRejectNoSpaceAndNoGoldWithoutChangingGameplayOrStock()
        {
            RunId runId = RunId.New();
            RoomInstanceId roomId = RoomInstanceId.New();
            var fullInventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 100);
            MerchantService.EnsureStock(fullInventory, runId, roomId);
            for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                Assert.That(fullInventory.TryCollectItem(NewStack("relic.marked_coin"), TransactionId.New()).Succeeded, Is.True);

            int fullGold = fullInventory.Gold;
            int fullReceipts = fullInventory.CreateSnapshot().transactions.Length;
            EconomyTransactionOutcome noSpace = MerchantService.TryPurchase(fullInventory, runId, roomId,
                "consumable.health_potion", 1, 0, TransactionId.New());

            Assert.That(noSpace.Status, Is.EqualTo(EconomyOperationStatus.RejectedBackpackFull));
            Assert.That(fullInventory.Gold, Is.EqualTo(fullGold));
            Assert.That(Offer(MerchantService.EnsureStock(fullInventory, runId, roomId), "consumable.health_potion").remainingQuantity,
                Is.EqualTo(2));
            Assert.That(fullInventory.CreateSnapshot().transactions.Length, Is.EqualTo(fullReceipts + 1));

            var poorInventory = new ExpeditionInventory(_items, "class.bellator");
            MerchantService.EnsureStock(poorInventory, runId, roomId);
            ExpeditionInventorySnapshot before = poorInventory.CreateSnapshot();
            TransactionId id = TransactionId.New();
            EconomyTransactionOutcome noGold = MerchantService.TryPurchase(poorInventory, runId, roomId,
                "consumable.health_potion", 1, 0, id);

            Assert.That(noGold.Status, Is.EqualTo(EconomyOperationStatus.RejectedInsufficientGold));
            Assert.That(poorInventory.Gold, Is.EqualTo(before.gold));
            Assert.That(Offer(MerchantService.EnsureStock(poorInventory, runId, roomId), "consumable.health_potion").remainingQuantity,
                Is.EqualTo(2));
            Assert.That(poorInventory.Backpack[0], Is.Null);
            ExpeditionInventory restoredPoorInventory = ExpeditionInventory.Restore(_items, poorInventory.CreateSnapshot());
            EconomyTransactionOutcome replay = MerchantService.TryPurchase(restoredPoorInventory, runId, roomId,
                "consumable.health_potion", 1, 0, id);
            Assert.That(replay.Status, Is.EqualTo(EconomyOperationStatus.RejectedInsufficientGold));
            Assert.That(restoredPoorInventory.Gold, Is.Zero);
            Assert.That(Offer(MerchantService.EnsureStock(restoredPoorInventory, runId, roomId), "consumable.health_potion").remainingQuantity,
                Is.EqualTo(2));
        }

        [Test]
        public void TransactionReplayAfterRestoreIsIdempotentAndConflictingReuseDoesNotMutate()
        {
            RunId runId = RunId.New();
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 100);
            MerchantService.EnsureStock(inventory, runId, roomId);
            TransactionId transactionId = TransactionId.New();

            EconomyTransactionOutcome first = MerchantService.TryPurchase(inventory, runId, roomId,
                "consumable.health_potion", 1, 30, transactionId);
            Assert.That(first.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            int goldBeforeReplay = restored.Gold;
            int stockBeforeReplay = Offer(MerchantService.EnsureStock(restored, runId, roomId), "consumable.health_potion").remainingQuantity;

            EconomyTransactionOutcome replay = MerchantService.TryPurchase(restored, runId, roomId,
                "consumable.health_potion", 1, 30, transactionId);
            Assert.That(replay.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(replay.ItemInstanceId, Is.EqualTo(first.ItemInstanceId));
            Assert.That(restored.Gold, Is.EqualTo(goldBeforeReplay));
            Assert.That(Offer(MerchantService.EnsureStock(restored, runId, roomId), "consumable.health_potion").remainingQuantity,
                Is.EqualTo(stockBeforeReplay));

            EconomyTransactionOutcome conflict = MerchantService.TryPurchase(restored, runId, roomId,
                "consumable.energy_tonic", 1, 30, transactionId);
            Assert.That(conflict.Status, Is.EqualTo(EconomyOperationStatus.RejectedTransactionConflict));
            Assert.That(restored.Gold, Is.EqualTo(goldBeforeReplay));
            Assert.That(Offer(MerchantService.EnsureStock(restored, runId, roomId), "consumable.energy_tonic").remainingQuantity,
                Is.EqualTo(1));
        }

        [Test]
        public void PriceTableUsesPlanValuesAndCappedDiscountsCannotCreateArbitrage()
        {
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("weapon.bellator.rusted_sword"), ItemRarity.Common), Is.EqualTo(30));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("weapon.bellator.rusted_sword"), ItemRarity.Rare), Is.EqualTo(45));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("weapon.bellator.rusted_sword"), ItemRarity.Epic), Is.EqualTo(65));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("armor.bellator.guild_cuirass"), ItemRarity.Common), Is.EqualTo(25));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("armor.bellator.guild_cuirass"), ItemRarity.Rare), Is.EqualTo(40));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("armor.bellator.guild_cuirass"), ItemRarity.Epic), Is.EqualTo(60));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("relic.marked_coin"), ItemRarity.Common), Is.EqualTo(50));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("consumable.health_potion"), ItemRarity.Common), Is.EqualTo(18));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("consumable.energy_tonic"), ItemRarity.Common), Is.EqualTo(18));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("consumable.antidote"), ItemRarity.Common), Is.EqualTo(14));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("consumable.bomb"), ItemRarity.Common), Is.EqualTo(20));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("consumable.fortune_brew"), ItemRarity.Common), Is.EqualTo(35));
            Assert.That(MerchantPricing.GetBasePrice(_items.Get("consumable.torch"), ItemRarity.Common), Is.EqualTo(10));
            Assert.That(MerchantPricing.GetBasePrice(_items.GetMaterial(MaterialKind.ShadowWood), ItemRarity.Common), Is.EqualTo(6));
            Assert.That(MerchantPricing.GetBasePrice(_items.GetMaterial(MaterialKind.Ore), ItemRarity.Common), Is.EqualTo(8));
            Assert.That(MerchantPricing.GetBasePrice(_items.GetMaterial(MaterialKind.UmbraHerb), ItemRarity.Common), Is.EqualTo(6));
            Assert.That(MerchantPricing.GetBasePrice(_items.GetMaterial(MaterialKind.ArcaneDust), ItemRarity.Common), Is.EqualTo(10));
            Assert.That(MerchantPricing.GetBasePrice(_items.GetMaterial(MaterialKind.DarkResin), ItemRarity.Common), Is.EqualTo(8));
            Assert.That(MerchantPricing.GetBasePrice(_items.GetMaterial(MaterialKind.VitalEmber), ItemRarity.Common), Is.EqualTo(15));
            Assert.That(MerchantPricing.GetSaleUnitPrice(_items.Get("weapon.bellator.rusted_sword"), ItemRarity.Common), Is.EqualTo(7));
            Assert.That(MerchantPricing.GetSaleUnitPrice(_items.Get("armor.bellator.guild_cuirass"), ItemRarity.Common), Is.EqualTo(6));
            Assert.That(MerchantPricing.GetSaleUnitPrice(_items.Get("consumable.health_potion"), ItemRarity.Common), Is.EqualTo(4));
            Assert.That(MerchantPricing.GetSaleUnitPrice(_items.GetMaterial(MaterialKind.ShadowWood), ItemRarity.Common), Is.EqualTo(1));

            int capped = MerchantPricing.GetPurchaseUnitPrice(_items.Get("consumable.health_potion"), ItemRarity.Common, 80);
            Assert.That(capped, Is.EqualTo(12));
            Assert.That(capped, Is.EqualTo(MerchantPricing.GetPurchaseUnitPrice(
                _items.Get("consumable.health_potion"), ItemRarity.Common, MerchantPricing.MaximumDiscountPercent)));

            foreach (ItemDefinition item in _items.Definitions)
            foreach (ItemRarity rarity in System.Enum.GetValues(typeof(ItemRarity)))
            {
                int buyPrice = MerchantPricing.GetPurchaseUnitPrice(item, rarity, MerchantPricing.MaximumDiscountPercent);
                int salePrice = MerchantPricing.GetSaleUnitPrice(item, rarity);
                Assert.That(salePrice, Is.LessThanOrEqualTo(buyPrice), item.Id + " / " + rarity);
            }
        }

        [Test]
        public void SellingRoundsDownAndSellingPurchasedGoodsLosesGoldWithoutRestocking()
        {
            RunId runId = RunId.New();
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 100);
            MerchantService.EnsureStock(inventory, runId, roomId);

            EconomyTransactionOutcome bought = MerchantService.TryPurchase(inventory, runId, roomId,
                "consumable.health_potion", 1, 80, TransactionId.New());
            Assert.That(bought.GoldDelta, Is.EqualTo(-12));
            int stockAfterBuy = Offer(MerchantService.EnsureStock(inventory, runId, roomId), "consumable.health_potion").remainingQuantity;

            EconomyTransactionOutcome sold = MerchantService.TrySellItem(inventory, runId, roomId,
                bought.ItemInstanceId, 1, TransactionId.New());
            Assert.That(sold.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(sold.GoldDelta, Is.EqualTo(4));
            Assert.That(inventory.Gold, Is.EqualTo(92));
            Assert.That(Offer(MerchantService.EnsureStock(inventory, runId, roomId), "consumable.health_potion").remainingQuantity,
                Is.EqualTo(stockAfterBuy));

            InventoryItemStack armor = NewStack("armor.bellator.guild_cuirass");
            Assert.That(inventory.TryCollectItem(armor, TransactionId.New()).Succeeded, Is.True);
            EconomyTransactionOutcome armorSale = MerchantService.TrySellItem(inventory, runId, roomId,
                armor.InstanceId, 1, TransactionId.New());
            Assert.That(armorSale.GoldDelta, Is.EqualTo(6), "25 % of 25 gold rounds down to 6.");

            AddMaterial(inventory, MaterialKind.ShadowWood, 1);
            EconomyTransactionOutcome materialSale = MerchantService.TrySellMaterial(inventory, runId, roomId,
                MaterialKind.ShadowWood, 1, TransactionId.New());
            Assert.That(materialSale.GoldDelta, Is.EqualTo(1), "25 % of 6 gold rounds down to 1.");
        }

        [Test]
        public void RejectedPurchaseWithEmptyItemIdCanBeRestoredAndReplayed()
        {
            RunId runId = RunId.New();
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 100);
            MerchantService.EnsureStock(inventory, runId, roomId);
            TransactionId transactionId = TransactionId.New();

            EconomyTransactionOutcome rejected = MerchantService.TryPurchase(inventory, runId, roomId,
                string.Empty, 1, 0, transactionId);
            Assert.That(rejected.Status, Is.EqualTo(EconomyOperationStatus.RejectedUnknownOffer));

            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            EconomyTransactionOutcome replay = MerchantService.TryPurchase(restored, runId, roomId,
                string.Empty, 1, 0, transactionId);
            Assert.That(replay.Status, Is.EqualTo(EconomyOperationStatus.RejectedUnknownOffer));
            Assert.That(restored.Gold, Is.EqualTo(100));
            Assert.That(Offer(MerchantService.EnsureStock(restored, runId, roomId), "consumable.health_potion").remainingQuantity,
                Is.EqualTo(2));
        }

        [Test]
        public void SmithingAppliesOnlyConsecutiveRecipeLevelsToTheSameCompatibleInstance()
        {
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 70);
            AddMaterial(inventory, MaterialKind.Ore, 8);
            AddMaterial(inventory, MaterialKind.ShadowWood, 1);
            AddMaterial(inventory, MaterialKind.ArcaneDust, 1);
            AddMaterial(inventory, MaterialKind.VitalEmber, 1);
            InventoryItemStack sword = NewStack("weapon.bellator.rusted_sword");
            Assert.That(inventory.TryCollectItem(sword, TransactionId.New()).Succeeded, Is.True);

            SmithingOutcome first = SmithingService.TryUpgrade(inventory, _recipes,
                "recipe.upgrade.weapon.1", sword.InstanceId, TransactionId.New());
            SmithingOutcome second = SmithingService.TryUpgrade(inventory, _recipes,
                "recipe.upgrade.weapon.2", sword.InstanceId, TransactionId.New());
            TransactionId thirdId = TransactionId.New();
            SmithingOutcome third = SmithingService.TryUpgrade(inventory, _recipes,
                "recipe.upgrade.weapon.3", sword.InstanceId, thirdId);

            Assert.That(first.Succeeded && second.Succeeded && third.Succeeded, Is.True);
            Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(sword.InstanceId));
            Assert.That(inventory.GetBackpackSlot(0).UpgradeLevel, Is.EqualTo(3));
            Assert.That(inventory.Gold, Is.Zero);

            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            SmithingOutcome replay = SmithingService.TryUpgrade(restored, _recipes,
                "recipe.upgrade.weapon.3", sword.InstanceId, thirdId);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.CraftingOutcome.ResultStack.InstanceId, Is.EqualTo(sword.InstanceId));
            Assert.That(restored.GetBackpackSlot(0).UpgradeLevel, Is.EqualTo(3));

            var invalidInventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 70);
            AddMaterial(invalidInventory, MaterialKind.Ore, 8);
            AddMaterial(invalidInventory, MaterialKind.VitalEmber, 1);
            InventoryItemStack unupgraded = NewStack("weapon.bellator.rusted_sword");
            Assert.That(invalidInventory.TryCollectItem(unupgraded, TransactionId.New()).Succeeded, Is.True);
            TransactionId skippedLevelId = TransactionId.New();
            SmithingOutcome skippedLevel = SmithingService.TryUpgrade(invalidInventory, _recipes,
                "recipe.upgrade.weapon.3", unupgraded.InstanceId, skippedLevelId);
            Assert.That(skippedLevel.CraftingOutcome.Status, Is.EqualTo(CraftingOperationStatus.RejectedInvalidUpgradeLevel));
            Assert.That(invalidInventory.Gold, Is.EqualTo(70));
            Assert.That(invalidInventory.GetBackpackSlot(0).UpgradeLevel, Is.Zero);
            Assert.That(SmithingService.TryUpgrade(invalidInventory, _recipes,
                "recipe.upgrade.weapon.3", unupgraded.InstanceId, skippedLevelId), Is.SameAs(skippedLevel));

            ExpeditionInventory restoredInvalidInventory = ExpeditionInventory.Restore(_items, invalidInventory.CreateSnapshot());
            SmithingOutcome restoredSkippedLevel = SmithingService.TryUpgrade(restoredInvalidInventory, _recipes,
                "recipe.upgrade.weapon.3", unupgraded.InstanceId, skippedLevelId);
            Assert.That(restoredSkippedLevel.CraftingOutcome.Status,
                Is.EqualTo(CraftingOperationStatus.RejectedInvalidUpgradeLevel));
            Assert.That(restoredInvalidInventory.Gold, Is.EqualTo(70));
            Assert.That(restoredInvalidInventory.GetBackpackSlot(0).UpgradeLevel, Is.Zero);

            SmithingOutcome incompatible = SmithingService.TryUpgrade(invalidInventory, _recipes,
                "recipe.upgrade.weapon.1", NewStack("weapon.venator.short_bow").InstanceId, TransactionId.New());
            Assert.That(incompatible.CraftingOutcome.Status, Is.EqualTo(CraftingOperationStatus.RejectedMissingTarget));
        }

        [Test]
        public void SmithingRejectsConsumableRecipesWithItsOwnPersistentReceipt()
        {
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 25);
            AddMaterial(inventory, MaterialKind.UmbraHerb, 2);
            AddMaterial(inventory, MaterialKind.DarkResin, 1);
            TransactionId smithingId = TransactionId.New();

            SmithingOutcome rejected = SmithingService.TryUpgrade(inventory, _recipes,
                "recipe.consumable.health_potion", default, smithingId);
            Assert.That(rejected.RejectedNonUpgradeRecipe, Is.True);
            Assert.That(rejected.CraftingOutcome.Status, Is.EqualTo(CraftingOperationStatus.RejectedInvalidRecipe));
            Assert.That(inventory.Gold, Is.EqualTo(25));
            Assert.That(inventory.GetMaterialCount(MaterialKind.UmbraHerb), Is.EqualTo(2));
            Assert.That(inventory.GetMaterialCount(MaterialKind.DarkResin), Is.EqualTo(1));
            Assert.That(inventory.Backpack[0], Is.Null);
            Assert.That(SmithingService.TryUpgrade(inventory, _recipes,
                "recipe.consumable.health_potion", default, smithingId), Is.SameAs(rejected));

            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            SmithingOutcome replay = SmithingService.TryUpgrade(restored, _recipes,
                "recipe.consumable.health_potion", default, smithingId);
            Assert.That(replay.RejectedNonUpgradeRecipe, Is.True);
            Assert.That(replay.CraftingOutcome.Status, Is.EqualTo(CraftingOperationStatus.RejectedInvalidRecipe));
            Assert.That(restored.Gold, Is.EqualTo(25));
            Assert.That(restored.GetMaterialCount(MaterialKind.UmbraHerb), Is.EqualTo(2));
            Assert.That(restored.GetMaterialCount(MaterialKind.DarkResin), Is.EqualTo(1));
            Assert.That(restored.Backpack[0], Is.Null);

            CraftingOutcome conflictedCraft = restored.TryCraft(_recipes,
                "recipe.consumable.health_potion", smithingId);
            Assert.That(conflictedCraft.Status, Is.EqualTo(CraftingOperationStatus.RejectedTransactionConflict));
            Assert.That(restored.GetMaterialCount(MaterialKind.UmbraHerb), Is.EqualTo(2));
            Assert.That(restored.GetMaterialCount(MaterialKind.DarkResin), Is.EqualTo(1));
            Assert.That(restored.Backpack[0], Is.Null);

            CraftingOutcome explicitCraft = restored.TryCraft(_recipes,
                "recipe.consumable.health_potion", TransactionId.New());
            Assert.That(explicitCraft.Status, Is.EqualTo(CraftingOperationStatus.Succeeded));
            Assert.That(restored.Backpack[0].ItemId, Is.EqualTo("consumable.health_potion"));
        }

        [Test]
        public void RestAndTaliaRecoverOnceAndTheirFlagsAndReceiptsSurviveRestore()
        {
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 50);

            RestStopOutcome firstEntry = RestStopService.Enter(inventory, roomId, currentHealth: 10, maximumHealth: 100);
            Assert.That(firstEntry.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(firstEntry.HealthRestored, Is.EqualTo(20));
            Assert.That(firstEntry.HealthAfter, Is.EqualTo(30));

            EconomyTransactionOutcome firstTalia = RestStopService.TryUseTalia(inventory, roomId,
                currentHealth: firstEntry.HealthAfter, maximumHealth: 100, TransactionId.New());
            Assert.That(firstTalia.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(firstTalia.HealthRestored, Is.EqualTo(50));
            Assert.That(firstTalia.ResultingHealth, Is.EqualTo(80));
            Assert.That(firstTalia.GoldDelta, Is.EqualTo(-20));
            Assert.That(inventory.Gold, Is.EqualTo(30));

            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            RestStopOutcome repeatedEntry = RestStopService.Enter(restored, roomId, currentHealth: 80, maximumHealth: 100);
            Assert.That(repeatedEntry.Status, Is.EqualTo(EconomyOperationStatus.RejectedAlreadyUsed));
            Assert.That(repeatedEntry.HealthRestored, Is.Zero);
            Assert.That(repeatedEntry.HealthAfter, Is.EqualTo(80));

            EconomyTransactionOutcome repeatedTalia = RestStopService.TryUseTalia(restored, roomId,
                currentHealth: firstEntry.HealthAfter, maximumHealth: 100, firstTalia.TransactionId);
            Assert.That(repeatedTalia.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(repeatedTalia.ResultingHealth, Is.EqualTo(80));
            Assert.That(restored.Gold, Is.EqualTo(30));

            EconomyTransactionOutcome secondTransaction = RestStopService.TryUseTalia(restored, roomId,
                currentHealth: 80, maximumHealth: 100, TransactionId.New());
            Assert.That(secondTransaction.Status, Is.EqualTo(EconomyOperationStatus.RejectedAlreadyUsed));
            Assert.That(restored.Gold, Is.EqualTo(30));
        }

        [Test]
        public void RejectedTaliaDoesNotConsumeTheServiceOrChargeGold()
        {
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator");
            Assert.That(RestStopService.Enter(inventory, roomId, 20, 100).Succeeded, Is.True);
            ExpeditionInventorySnapshot before = inventory.CreateSnapshot();

            EconomyTransactionOutcome noGold = RestStopService.TryUseTalia(inventory, roomId, 40, 100, TransactionId.New());
            Assert.That(noGold.Status, Is.EqualTo(EconomyOperationStatus.RejectedInsufficientGold));
            Assert.That(inventory.Gold, Is.EqualTo(before.gold));

            Assert.That(inventory.TryAddGold(20, TransactionId.New()).Succeeded, Is.True);
            EconomyTransactionOutcome nowAffordable = RestStopService.TryUseTalia(inventory, roomId, 40, 100, TransactionId.New());
            Assert.That(nowAffordable.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(nowAffordable.GoldDelta, Is.EqualTo(-20));
            Assert.That(inventory.Gold, Is.Zero);

            RoomInstanceId fullHealthRoom = RoomInstanceId.New();
            Assert.That(RestStopService.Enter(inventory, fullHealthRoom, 100, 100).Succeeded, Is.True);
            EconomyTransactionOutcome fullHealth = RestStopService.TryUseTalia(inventory, fullHealthRoom,
                100, 100, TransactionId.New());
            Assert.That(fullHealth.Status, Is.EqualTo(EconomyOperationStatus.RejectedFullHealth));
            Assert.That(inventory.Gold, Is.Zero);
        }

        [Test]
        public void TaliaReceiptReplaysAfterRestoreWithUpdatedHealthAndConflictsAcrossRooms()
        {
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 50);
            Assert.That(RestStopService.Enter(inventory, roomId, currentHealth: 10, maximumHealth: 100).Succeeded, Is.True);
            TransactionId transactionId = TransactionId.New();

            EconomyTransactionOutcome first = RestStopService.TryUseTalia(inventory, roomId,
                currentHealth: 30, maximumHealth: 100, transactionId);
            Assert.That(first.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, inventory.CreateSnapshot());
            ExpeditionInventorySnapshot beforeReplays = restored.CreateSnapshot();

            EconomyTransactionOutcome changedCurrentHealth = RestStopService.TryUseTalia(restored, roomId,
                currentHealth: 31, maximumHealth: 100, transactionId);
            EconomyTransactionOutcome changedMaximumHealth = RestStopService.TryUseTalia(restored, roomId,
                currentHealth: 30, maximumHealth: 101, transactionId);
            EconomyTransactionOutcome differentRoom = RestStopService.TryUseTalia(restored, RoomInstanceId.New(),
                currentHealth: 30, maximumHealth: 100, transactionId);
            ExpeditionInventorySnapshot afterReplays = restored.CreateSnapshot();

            Assert.That(changedCurrentHealth.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(changedMaximumHealth.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));
            Assert.That(changedCurrentHealth.ResultingHealth, Is.EqualTo(first.ResultingHealth));
            Assert.That(changedMaximumHealth.ResultingHealth, Is.EqualTo(first.ResultingHealth));
            Assert.That(changedCurrentHealth.HealthRestored, Is.EqualTo(first.HealthRestored));
            Assert.That(changedMaximumHealth.HealthRestored, Is.EqualTo(first.HealthRestored));
            Assert.That(changedCurrentHealth.GoldDelta, Is.EqualTo(first.GoldDelta));
            Assert.That(changedMaximumHealth.GoldDelta, Is.EqualTo(first.GoldDelta));
            Assert.That(differentRoom.Status, Is.EqualTo(EconomyOperationStatus.RejectedTransactionConflict));
            Assert.That(differentRoom.ResultingHealth, Is.EqualTo(-1));
            Assert.That(afterReplays.gold, Is.EqualTo(beforeReplays.gold));
            Assert.That(afterReplays.restStops.Length, Is.EqualTo(beforeReplays.restStops.Length));
            Assert.That(afterReplays.restStops[0].entered, Is.EqualTo(beforeReplays.restStops[0].entered));
            Assert.That(afterReplays.restStops[0].entryHealthRestored,
                Is.EqualTo(beforeReplays.restStops[0].entryHealthRestored));
            Assert.That(afterReplays.restStops[0].taliaUsed, Is.EqualTo(beforeReplays.restStops[0].taliaUsed));
            Assert.That(afterReplays.restStops[0].taliaHealthRestored,
                Is.EqualTo(beforeReplays.restStops[0].taliaHealthRestored));
            Assert.That(afterReplays.restStops[0].taliaHealthAfter,
                Is.EqualTo(beforeReplays.restStops[0].taliaHealthAfter));
            Assert.That(afterReplays.transactions.Length, Is.EqualTo(beforeReplays.transactions.Length));
            Assert.That(afterReplays.transactions[0].transactionId,
                Is.EqualTo(beforeReplays.transactions[0].transactionId));
            Assert.That(afterReplays.transactions[0].signature, Is.EqualTo(beforeReplays.transactions[0].signature));
            Assert.That(afterReplays.transactions[0].economyStatus,
                Is.EqualTo(beforeReplays.transactions[0].economyStatus));
            Assert.That(afterReplays.transactions[0].economyGoldDelta,
                Is.EqualTo(beforeReplays.transactions[0].economyGoldDelta));
            Assert.That(afterReplays.transactions[0].economyResultingHealth,
                Is.EqualTo(beforeReplays.transactions[0].economyResultingHealth));
        }

        [Test]
        public void LegacyTaliaReceiptWithHealthInSignatureReplaysAfterRestore()
        {
            RoomInstanceId roomId = RoomInstanceId.New();
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 50);
            Assert.That(RestStopService.Enter(inventory, roomId, currentHealth: 10, maximumHealth: 100).Succeeded, Is.True);
            TransactionId transactionId = TransactionId.New();
            EconomyTransactionOutcome first = RestStopService.TryUseTalia(inventory, roomId,
                currentHealth: 30, maximumHealth: 100, transactionId);
            Assert.That(first.Status, Is.EqualTo(EconomyOperationStatus.Succeeded));

            ExpeditionInventorySnapshot legacySnapshot = inventory.CreateSnapshot();
            InventoryTransactionSnapshot legacyReceipt = legacySnapshot.transactions.Single(
                transaction => transaction.transactionId == transactionId.ToString());
            legacyReceipt.signature = "economy|talia|" + roomId + "|30|100";
            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, legacySnapshot);

            EconomyTransactionOutcome replay = RestStopService.TryUseTalia(restored, roomId,
                currentHealth: first.ResultingHealth, maximumHealth: 100, transactionId);

            Assert.That(restored.HasRecordedEconomyTransaction(transactionId), Is.True);
            Assert.That(replay.Status, Is.EqualTo(first.Status));
            Assert.That(replay.ResultingHealth, Is.EqualTo(first.ResultingHealth));
            Assert.That(replay.HealthRestored, Is.EqualTo(first.HealthRestored));
            Assert.That(replay.GoldDelta, Is.EqualTo(first.GoldDelta));
            Assert.That(restored.Gold, Is.EqualTo(inventory.Gold));
            Assert.That(restored.CreateSnapshot().transactions.Single(
                transaction => transaction.transactionId == transactionId.ToString()).signature,
                Is.EqualTo(legacyReceipt.signature));
        }

        [Test]
        public void VersionThreeInventorySnapshotMigratesWithNoInventedE03State()
        {
            var inventory = new ExpeditionInventory(_items, "class.bellator", initialGold: 7);
            ExpeditionInventorySnapshot legacy = inventory.CreateSnapshot();
            legacy.snapshotVersion = 3;
            legacy.merchantRunId = string.Empty;
            legacy.merchantStocks = null;
            legacy.restStops = null;

            ExpeditionInventory restored = ExpeditionInventory.Restore(_items, legacy);
            Assert.That(restored.Gold, Is.EqualTo(7));
            Assert.That(restored.CreateSnapshot().merchantStocks.Length, Is.Zero);
            Assert.That(restored.CreateSnapshot().restStops.Length, Is.Zero);
        }

        private static ItemCatalog CreateCatalog()
        {
            var definitions = new List<ItemDefinition>
            {
                Weapon("weapon.bellator.rusted_sword", "class.bellator"),
                Weapon("weapon.venator.short_bow", "class.venator"),
                Weapon("weapon.magus.ember_staff", "class.magus"),
                Armor("armor.bellator.guild_cuirass"),
                Armor("armor.venator.leather_vest"),
                Armor("armor.magus.traveler_robe"),
                Relic("relic.marked_coin"),
                Relic("relic.hidden_pouch"),
                Consumable("consumable.health_potion"),
                Consumable("consumable.energy_tonic"),
                Consumable("consumable.antidote"),
                Consumable("consumable.bomb"),
                Consumable("consumable.fortune_brew"),
                Consumable("consumable.torch"),
                Material("material.shadow_wood", MaterialKind.ShadowWood),
                Material("material.ore", MaterialKind.Ore),
                Material("material.umbra_herb", MaterialKind.UmbraHerb),
                Material("material.arcane_dust", MaterialKind.ArcaneDust),
                Material("material.dark_resin", MaterialKind.DarkResin),
                Material("material.vital_ember", MaterialKind.VitalEmber)
            };
            var loadouts = new[]
            {
                new StartingLoadout("class.bellator", "weapon.bellator.rusted_sword", "armor.bellator.guild_cuirass"),
                new StartingLoadout("class.venator", "weapon.venator.short_bow", "armor.venator.leather_vest"),
                new StartingLoadout("class.magus", "weapon.magus.ember_staff", "armor.magus.traveler_robe")
            };
            return new ItemCatalog(definitions, loadouts);
        }

        private static RecipeCatalog CreateUpgradeRecipes()
        {
            return new RecipeCatalog(new[]
            {
                new RecipeDefinition("recipe.consumable.health_potion", new[]
                {
                    new RecipeMaterialCost(MaterialKind.UmbraHerb, 2), new RecipeMaterialCost(MaterialKind.DarkResin, 1)
                }, 0, CraftingResultKind.Consumable, resultItemId: "consumable.health_potion", resultQuantity: 1),
                new RecipeDefinition("recipe.upgrade.weapon.1", new[]
                {
                    new RecipeMaterialCost(MaterialKind.Ore, 2), new RecipeMaterialCost(MaterialKind.ShadowWood, 1)
                }, 10, CraftingResultKind.Upgrade, upgradeTargetKind: ItemKind.Weapon,
                    requiredCurrentUpgradeLevel: 0, resultUpgradeLevel: 1),
                new RecipeDefinition("recipe.upgrade.weapon.2", new[]
                {
                    new RecipeMaterialCost(MaterialKind.Ore, 3), new RecipeMaterialCost(MaterialKind.ArcaneDust, 1)
                }, 20, CraftingResultKind.Upgrade, upgradeTargetKind: ItemKind.Weapon,
                    requiredCurrentUpgradeLevel: 1, resultUpgradeLevel: 2),
                new RecipeDefinition("recipe.upgrade.weapon.3", new[]
                {
                    new RecipeMaterialCost(MaterialKind.Ore, 3), new RecipeMaterialCost(MaterialKind.VitalEmber, 1)
                }, 40, CraftingResultKind.Upgrade, upgradeTargetKind: ItemKind.Weapon,
                    requiredCurrentUpgradeLevel: 2, resultUpgradeLevel: 3)
            });
        }

        private static ItemDefinition Weapon(string id, string classId)
        {
            return new ItemDefinition(id, id + ".name", id + ".description", ItemKind.Weapon, 1,
                new[] { classId });
        }

        private static ItemDefinition Armor(string id)
        {
            return new ItemDefinition(id, id + ".name", id + ".description", ItemKind.Armor, 1);
        }

        private static ItemDefinition Relic(string id)
        {
            return new ItemDefinition(id, id + ".name", id + ".description", ItemKind.Relic, 1);
        }

        private static ItemDefinition Consumable(string id)
        {
            return new ItemDefinition(id, id + ".name", id + ".description", ItemKind.Consumable, 3);
        }

        private static ItemDefinition Material(string id, MaterialKind kind)
        {
            return new ItemDefinition(id, id + ".name", id + ".description", ItemKind.Material, 99, materialKind: kind);
        }

        private static InventoryItemStack NewStack(string itemId)
        {
            return new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, 1);
        }

        private static void AddMaterial(ExpeditionInventory inventory, MaterialKind kind, int quantity)
        {
            Assert.That(inventory.TryAddMaterial(kind, quantity, TransactionId.New()).Succeeded, Is.True);
        }

        private static MerchantOfferSnapshot Offer(MerchantStockSnapshot stock, string itemId)
        {
            foreach (MerchantOfferSnapshot offer in stock.offers)
                if (offer.itemId == itemId) return offer;
            Assert.Fail("Expected merchant offer " + itemId + " to exist.");
            return null;
        }

        private static void AssertOffer(MerchantStockSnapshot stock, string itemId, int quantity)
        {
            MerchantOfferSnapshot offer = Offer(stock, itemId);
            Assert.That(offer.initialQuantity, Is.EqualTo(quantity), itemId);
            Assert.That(offer.remainingQuantity, Is.EqualTo(quantity), itemId);
        }
    }
}
