using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode.Economy
{
    public sealed class MerchantApplicationTests
    {
        [UnityTest]
        public IEnumerator E03_PersistsMerchantTransactionsAndConsecutiveUpgradesAcrossRetryAndRestRestart()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", Guid.NewGuid().ToString("N"));
            var store = new FailOnceProfileWriteStore(new FileSaveStore(saveDirectory, null));
            var evidence = new ScenarioEvidenceRecorder("E03", "E03_MerchantTransactionsAndSmithingRestart", 27182818);
            try
            {
                Time.timeScale = 1f;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null)
                    UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;

                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.CanStartNewExpedition, Is.True);
                Task<ContractProgressionCommitResult> acceptedCraftingContract = app.AcceptContractAsync("CT04");
                while (!acceptedCraftingContract.IsCompleted) yield return null;
                Assert.That(acceptedCraftingContract.Result.Succeeded, Is.True, acceptedCraftingContract.Result.Message);

                evidence.RecordInput(0, "BeginRestRoomFixture", "Start a valid saved run directly in a rest room without claiming a combat victory");
                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                RequireSuccess(run.Inventory.TryAddGold(200, TransactionId.New()), "seed test purchase funds");
                RequireSuccess(run.Inventory.TryAddMaterial(MaterialKind.Ore, 8, TransactionId.New()), "seed upgrade ore");
                RequireSuccess(run.Inventory.TryAddMaterial(MaterialKind.ShadowWood, 1, TransactionId.New()), "seed first upgrade wood");
                RequireSuccess(run.Inventory.TryAddMaterial(MaterialKind.ArcaneDust, 1, TransactionId.New()), "seed second upgrade dust");
                ItemInstanceId weaponInstanceId = run.Inventory.Weapon.InstanceId;

                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);
                RoomInstanceId roomId = app.ActiveRoomInstanceId;
                Assert.That(roomId.IsValid, Is.True);
                Assert.That(app.GetActiveMerchantStock(), Is.Null,
                    "A merchant stock snapshot is created and saved as part of entering the rest room.");

                ExpeditionSaveData damagedState = app.ActiveExpedition;
                damagedState.currentHealth = 20;
                Task<SaveWriteResult> damageWrite = app.SaveActiveExpeditionStateAsync(damagedState);
                while (!damageWrite.IsCompleted) yield return null;
                Assert.That(damageWrite.Result.Succeeded, Is.True, damageWrite.Result.Message);

                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> entry = app.EnterRestStopAsync(roomId);
                while (!entry.IsCompleted) yield return null;
                Assert.That(entry.Result.Persisted, Is.True, entry.Result.Message);
                Assert.That(entry.Result.Outcome.RestOutcome.HealthAfter, Is.EqualTo(40));
                MerchantStockSnapshot stockOnEntry = app.GetActiveMerchantStock();
                Assert.That(stockOnEntry, Is.Not.Null);
                Assert.That(stockOnEntry.offers.Length, Is.EqualTo(11));
                Assert.That(stockOnEntry.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity,
                    Is.EqualTo(2));
                Assert.That(FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity, Is.EqualTo(2),
                    "A new expedition starts with the two health potions defined by the product rules.");

                evidence.RecordInput(1, "PurchaseFromMerchantAsync", "Fail then repeat the exact stocked purchase with its original transaction ID");
                TransactionId purchaseId = TransactionId.New();
                store.FailNextProfileWrite();
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> failedPurchase =
                    app.PurchaseFromMerchantAsync(roomId, "consumable.health_potion", 1, 0, purchaseId);
                while (!failedPurchase.IsCompleted) yield return null;
                Assert.That(failedPurchase.Result.Persisted, Is.False);
                Assert.That(failedPurchase.Result.Pending, Is.True);
                Assert.That(failedPurchase.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(200),
                    "The candidate gold deduction remains private until the profile write succeeds.");
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(2), "An uncommitted purchase cannot reduce visible stock.");
                Assert.That(FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity, Is.EqualTo(2),
                    "An uncommitted purchase cannot publish the added consumable.");
                evidence.RecordAssertion("Failed purchase keeps gold, stock and inventory at the last committed state",
                    app.ActiveRun.Inventory.Gold == 200 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 2 &&
                    FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity == 2,
                    "200 gold, two potions in stock and two owned potions remain visible",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; stock=" +
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity +
                    "; owned=" + FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity);

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> retriedPurchase =
                    app.PurchaseFromMerchantAsync(roomId, "consumable.health_potion", 1, 0, purchaseId);
                while (!retriedPurchase.IsCompleted) yield return null;
                Assert.That(retriedPurchase.Result.Persisted, Is.True, retriedPurchase.Result.Message);
                Assert.That(retriedPurchase.Result.Outcome.TransactionId, Is.EqualTo(purchaseId));
                Assert.That(retriedPurchase.Result.Outcome.ItemInstanceId, Is.EqualTo(failedPurchase.Result.Outcome.ItemInstanceId));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(182));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));
                Assert.That(FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity, Is.EqualTo(3));

                evidence.RecordInput(2, "SellMaterialToMerchantAsync", "Sell one ore and persist the canonical rounded sale price");
                TransactionId saleId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> sale =
                    app.SellMaterialToMerchantAsync(roomId, MaterialKind.Ore, 1, saleId);
                while (!sale.IsCompleted) yield return null;
                Assert.That(sale.Result.Persisted, Is.True, sale.Result.Message);
                Assert.That(sale.Result.Outcome.Succeeded, Is.True);
                Assert.That(sale.Result.Outcome.GoldDelta, Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(7));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(184));

                evidence.RecordInput(3, "UpgradeEquipmentAsync", "Reject a skipped upgrade, then apply weapon +1 and +2 to the same instance");
                TransactionId skippedUpgradeId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<SmithingOutcome>> skippedUpgrade = app.UpgradeEquipmentAsync(
                    roomId, "recipe.upgrade.weapon.2", weaponInstanceId, skippedUpgradeId);
                while (!skippedUpgrade.IsCompleted) yield return null;
                Assert.That(skippedUpgrade.Result.Persisted, Is.True, skippedUpgrade.Result.Message);
                Assert.That(skippedUpgrade.Result.Outcome.Succeeded, Is.False,
                    "The +2 recipe cannot be applied before the +1 recipe.");
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(0));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(184));
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.Zero,
                    "A rejected upgrade must not advance CT04.");

                TransactionId firstUpgradeId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<SmithingOutcome>> firstUpgrade = app.UpgradeEquipmentAsync(
                    roomId, "recipe.upgrade.weapon.1", weaponInstanceId, firstUpgradeId);
                while (!firstUpgrade.IsCompleted) yield return null;
                Assert.That(firstUpgrade.Result.Persisted, Is.True, firstUpgrade.Result.Message);
                Assert.That(firstUpgrade.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Weapon.InstanceId, Is.EqualTo(weaponInstanceId));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(1));
                for (int frame = 0; frame < 120 && app.ContractProgression.GetProgress("CT04") != 1; frame++) yield return null;
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(1),
                    "A persisted successful smithing transaction advances CT04 once.");

                TransactionId secondUpgradeId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<SmithingOutcome>> secondUpgrade = app.UpgradeEquipmentAsync(
                    roomId, "recipe.upgrade.weapon.2", weaponInstanceId, secondUpgradeId);
                while (!secondUpgrade.IsCompleted) yield return null;
                Assert.That(secondUpgrade.Result.Persisted, Is.True, secondUpgrade.Result.Message);
                Assert.That(secondUpgrade.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Weapon.InstanceId, Is.EqualTo(weaponInstanceId));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(154));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.ShadowWood), Is.Zero);
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.ArcaneDust), Is.Zero);
                for (int frame = 0; frame < 120 && app.ContractProgression.GetProgress("CT04") != 2; frame++) yield return null;
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(2));

                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> repeatedEntry = app.EnterRestStopAsync(roomId);
                while (!repeatedEntry.IsCompleted) yield return null;
                Assert.That(repeatedEntry.Result.Persisted, Is.True, repeatedEntry.Result.Message);
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1), "Re-entering the same rest room must not replenish fixed stock.");

                evidence.RecordInput(4, "SellItemToMerchantAsync", "Fail then repeat the exact sale of the owned health-potion stack");
                InventoryItemStack potionStack = FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion");
                Assert.That(potionStack.Quantity, Is.EqualTo(3));
                ItemInstanceId potionStackId = potionStack.InstanceId;
                const int potionSaleQuantity = 3;
                const int expectedPotionSaleGold = 12; // 18 base gold × 25%, floored to 4 per potion.
                TransactionId potionSaleId = TransactionId.New();
                store.FailNextProfileWrite();
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> failedPotionSale =
                    app.SellItemToMerchantAsync(roomId, potionStackId, potionSaleQuantity, potionSaleId);
                while (!failedPotionSale.IsCompleted) yield return null;
                Assert.That(failedPotionSale.Result.Persisted, Is.False);
                Assert.That(failedPotionSale.Result.Pending, Is.True);
                Assert.That(failedPotionSale.Result.Outcome.Succeeded, Is.True);
                Assert.That(failedPotionSale.Result.Outcome.ItemId, Is.EqualTo("consumable.health_potion"));
                Assert.That(failedPotionSale.Result.Outcome.ItemInstanceId, Is.EqualTo(potionStackId));
                Assert.That(failedPotionSale.Result.Outcome.Quantity, Is.EqualTo(potionSaleQuantity));
                Assert.That(failedPotionSale.Result.Outcome.GoldDelta, Is.EqualTo(expectedPotionSaleGold));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(154),
                    "A failed profile write cannot publish the sale proceeds.");
                Assert.That(FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity,
                    Is.EqualTo(potionSaleQuantity), "A failed profile write cannot remove the owned item.");
                evidence.RecordAssertion("Failed item-sale write leaves the committed gold and item stack unchanged",
                    app.ActiveRun.Inventory.Gold == 154 &&
                    FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity == potionSaleQuantity,
                    "gold 154 and all three owned health potions remain visible",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; potions=" +
                    FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity);

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> retriedPotionSale =
                    app.SellItemToMerchantAsync(roomId, potionStackId, potionSaleQuantity, potionSaleId);
                while (!retriedPotionSale.IsCompleted) yield return null;
                Assert.That(retriedPotionSale.Result.Persisted, Is.True, retriedPotionSale.Result.Message);
                Assert.That(retriedPotionSale.Result.Outcome.Succeeded, Is.True);
                Assert.That(retriedPotionSale.Result.Outcome.TransactionId, Is.EqualTo(potionSaleId));
                Assert.That(retriedPotionSale.Result.Outcome.ItemId, Is.EqualTo("consumable.health_potion"));
                Assert.That(retriedPotionSale.Result.Outcome.ItemInstanceId, Is.EqualTo(potionStackId));
                Assert.That(retriedPotionSale.Result.Outcome.Quantity, Is.EqualTo(potionSaleQuantity));
                Assert.That(retriedPotionSale.Result.Outcome.GoldDelta, Is.EqualTo(expectedPotionSaleGold));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(166));
                Assert.That(HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"), Is.False,
                    "A confirmed sale removes the entire owned potion stack.");
                evidence.RecordAssertion("Confirmed item sale credits the floored value and removes the stack",
                    app.ActiveRun.Inventory.Gold == 166 &&
                    !HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion") &&
                    retriedPotionSale.Result.Outcome.GoldDelta == expectedPotionSaleGold,
                    "gold 166, no health-potion stack and sale proceeds of 12 (4 per potion)",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; hasPotions=" +
                    HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion") +
                    "; saleGold=" + retriedPotionSale.Result.Outcome.GoldDelta);

                evidence.RecordInput(5, "SellItemToMerchantAsync", "Attempt to sell an invalid item instance and preserve committed gameplay state");
                TransactionId rejectedSaleId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> rejectedSale =
                    app.SellItemToMerchantAsync(roomId, default(ItemInstanceId), 1, rejectedSaleId);
                while (!rejectedSale.IsCompleted) yield return null;
                Assert.That(rejectedSale.Result.Persisted, Is.True, rejectedSale.Result.Message);
                Assert.That(rejectedSale.Result.Outcome.Succeeded, Is.False);
                Assert.That(rejectedSale.Result.Outcome.Status, Is.EqualTo(EconomyOperationStatus.RejectedInvalidQuantity));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(166));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.Weapon.InstanceId, Is.EqualTo(weaponInstanceId));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(2));
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(2),
                    "The two successful smithing operations remain in the profile.");
                Assert.That(HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"), Is.False);
                evidence.RecordAssertion("Rejected item sale leaves functional inventory and gold unchanged",
                    !rejectedSale.Result.Outcome.Succeeded && app.ActiveRun.Inventory.Gold == 166 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 2 &&
                    app.ActiveRun.Inventory.Weapon.InstanceId == weaponInstanceId &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 2 &&
                    !HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"),
                    "invalid-quantity rejection, gold 166, ore 2, same weapon at +2 and no health-potion stack",
                    rejectedSale.Result.Outcome.Status + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; ore=" + app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) +
                    "; weaponUpgrade=" + app.ActiveRun.Inventory.Weapon.UpgradeLevel +
                    "; hasPotions=" + HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"));

                string runId = app.ActiveRun.RunId.ToString();
                string nodeId = app.ActiveExpedition.currentNodeId;
                evidence.RecordInput(6, "ProcessRestart", "Resume the saved rest room and verify confirmed merchant sales and smithing transactions");
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || ApplicationBootstrapper.Instance.ActiveRun == null ||
                    SceneManager.GetActiveScene().name != "Expedition"); frame++)
                    yield return null;

                app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.ActiveRun, Is.Not.Null);
                Assert.That(app.ActiveRun.RunId.ToString(), Is.EqualTo(runId));
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(nodeId));
                Assert.That(app.ActiveRoomInstanceId, Is.EqualTo(roomId));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(40));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(166));
                Assert.That(app.ActiveRun.Inventory.Weapon.InstanceId, Is.EqualTo(weaponInstanceId));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(2),
                    "The two successful upgrades remain in the profile after restart.");
                Assert.That(HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"), Is.False);
                MerchantStockSnapshot stockAfterRestart = app.GetActiveMerchantStock();
                Assert.That(stockAfterRestart, Is.Not.Null);
                Assert.That(stockAfterRestart.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity,
                    Is.EqualTo(1));
                Assert.That(stockAfterRestart.roomInstanceId, Is.EqualTo(roomId.ToString()));
                evidence.RecordAssertion("Confirmed rest-room commerce, smithing and fixed stock survive restart",
                    app.ActiveExpedition.currentHealth == 40 && app.ActiveRun.Inventory.Gold == 166 &&
                    app.ActiveRun.Inventory.Weapon.InstanceId == weaponInstanceId &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 2 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 2 &&
                    !HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion") &&
                    stockAfterRestart.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1,
                    "health 40, gold 166, same weapon at +2, no owned potions and one remaining in stock",
                    "health=" + app.ActiveExpedition.currentHealth + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; weapon=" + app.ActiveRun.Inventory.Weapon.InstanceId + "+" + app.ActiveRun.Inventory.Weapon.UpgradeLevel +
                    "; stock=" + stockAfterRestart.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity);

                evidence.RecordInput(7, "ReplayTransactions", "Replay the same purchase, sales and upgrade transaction IDs after restart");
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> replayedPurchase = app.PurchaseFromMerchantAsync(
                    roomId, "consumable.health_potion", 1, 0, purchaseId);
                while (!replayedPurchase.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> replayedSale = app.SellMaterialToMerchantAsync(
                    roomId, MaterialKind.Ore, 1, saleId);
                while (!replayedSale.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<SmithingOutcome>> replayedFirstUpgrade = app.UpgradeEquipmentAsync(
                    roomId, "recipe.upgrade.weapon.1", weaponInstanceId, firstUpgradeId);
                while (!replayedFirstUpgrade.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<SmithingOutcome>> replayedSecondUpgrade = app.UpgradeEquipmentAsync(
                    roomId, "recipe.upgrade.weapon.2", weaponInstanceId, secondUpgradeId);
                while (!replayedSecondUpgrade.IsCompleted) yield return null;

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> replayedPotionSale = app.SellItemToMerchantAsync(
                    roomId, potionStackId, potionSaleQuantity, potionSaleId);
                while (!replayedPotionSale.IsCompleted) yield return null;

                Assert.That(replayedPurchase.Result.Persisted, Is.True);
                Assert.That(replayedSale.Result.Persisted, Is.True);
                Assert.That(replayedFirstUpgrade.Result.Persisted, Is.True);
                Assert.That(replayedSecondUpgrade.Result.Persisted, Is.True);
                Assert.That(replayedPotionSale.Result.Persisted, Is.True);
                Assert.That(replayedPotionSale.Result.Outcome.Succeeded, Is.True);
                Assert.That(replayedPotionSale.Result.Outcome.ItemId, Is.EqualTo("consumable.health_potion"));
                Assert.That(replayedPotionSale.Result.Outcome.TransactionId, Is.EqualTo(potionSaleId));
                Assert.That(replayedPotionSale.Result.Outcome.ItemInstanceId, Is.EqualTo(potionStackId));
                Assert.That(replayedPotionSale.Result.Outcome.Quantity, Is.EqualTo(potionSaleQuantity));
                Assert.That(replayedPotionSale.Result.Outcome.GoldDelta, Is.EqualTo(expectedPotionSaleGold));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(166));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(2));
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(2),
                    "Replaying successful smithing transaction IDs must not count either upgrade again.");
                Assert.That(HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"), Is.False);
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));
                bool replayWasIdempotent = app.ActiveRun.Inventory.Gold == 166 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 2 &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 2 &&
                    !HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion") &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1;
                evidence.RecordAssertion("Replayed transaction IDs preserve committed balances, sales, stock and upgrade level",
                    replayWasIdempotent, "gold 166, ore 2, weapon +2, no owned potions and one in stock",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; ore=" + app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) +
                    "; weaponUpgrade=" + app.ActiveRun.Inventory.Weapon.UpgradeLevel +
                    "; hasPotions=" + HasBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion"));

                Assert.That(evidence.Complete(), Is.Zero, "E03 merchant application scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null)
                    UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        private static ExpeditionRunConfiguration CreateRunStartingAtRest()
        {
            // Preserve a generated, valid route while placing the fixture at a rest node. No combat
            // outcome is manufactured to reach the services under test.
            ExpeditionRunConfiguration generated = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            string[] lines = generated.Route.CanonicalForm.Split('\n');
            int firstNodeLine = Array.FindIndex(lines, line => line.StartsWith(
                "N|" + generated.Route.MandatoryNodeIds[0] + "|", StringComparison.Ordinal));
            ExpeditionRouteNode restNode = generated.Route.MandatoryNodeIds.Skip(1)
                .Select(nodeId => generated.Route.Nodes.Single(node =>
                    string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)))
                .FirstOrDefault(node => node.Kind == ExpeditionRouteNodeKind.Rest);
            int restNodeLine = Array.FindIndex(lines, line => line.StartsWith(
                "N|" + restNode.NodeId + "|", StringComparison.Ordinal));
            if (firstNodeLine < 0 || restNodeLine < 0)
                throw new InvalidOperationException("The generated route is missing its first or mandatory rest node.");

            string[] first = lines[firstNodeLine].Split('|');
            string[] laterRest = lines[restNodeLine].Split('|');
            string firstSector = first[2];
            string firstCombatTemplate = first[4];
            first[2] = string.Empty;
            first[3] = ExpeditionRouteNodeKind.Rest.ToString();
            first[4] = laterRest[4];
            laterRest[2] = firstSector;
            laterRest[3] = ExpeditionRouteNodeKind.Combat.ToString();
            laterRest[4] = firstCombatTemplate;
            lines[firstNodeLine] = string.Join("|", first);
            lines[restNodeLine] = string.Join("|", laterRest);

            ExpeditionRoutePlan route = ExpeditionRoutePlan.ParseCanonical(string.Join("\n", lines));
            return new ExpeditionRunConfiguration(generated.RunId, generated.ClassId, generated.RaceId,
                generated.SkillIds, generated.Seed, route, generated.Inventory,
                generated.StartingEquipmentInstanceIds);
        }

        private static InventoryItemStack FindBackpackItem(ExpeditionInventory inventory, string itemId)
        {
            return inventory.Backpack.Single(item => item != null && string.Equals(item.ItemId, itemId, StringComparison.Ordinal));
        }

        private static bool HasBackpackItem(ExpeditionInventory inventory, string itemId)
        {
            return inventory.Backpack.Any(item => item != null && string.Equals(item.ItemId, itemId, StringComparison.Ordinal));
        }

        private static void RequireSuccess(InventoryOperationOutcome outcome, string action)
        {
            Assert.That(outcome.Succeeded, Is.True, "Could not " + action + ": " + outcome.Status);
        }

        private static void DeleteIsolatedSaveDirectory(string directory)
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a save directory outside the system temp root.");
            if (Directory.Exists(fullDirectory)) Directory.Delete(fullDirectory, recursive: true);
        }

        private sealed class FailOnceProfileWriteStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _failNextProfileWrite;

            public FailOnceProfileWriteStore(ISaveStore inner) { _inner = inner; }
            public void FailNextProfileWrite() => _failNextProfileWrite = true;
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile && _failNextProfileWrite)
                {
                    _failNextProfileWrite = false;
                    throw new IOException("simulated profile storage failure");
                }
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
