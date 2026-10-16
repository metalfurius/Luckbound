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

                TransactionId firstUpgradeId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<SmithingOutcome>> firstUpgrade = app.UpgradeEquipmentAsync(
                    roomId, "recipe.upgrade.weapon.1", weaponInstanceId, firstUpgradeId);
                while (!firstUpgrade.IsCompleted) yield return null;
                Assert.That(firstUpgrade.Result.Persisted, Is.True, firstUpgrade.Result.Message);
                Assert.That(firstUpgrade.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Weapon.InstanceId, Is.EqualTo(weaponInstanceId));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(1));

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

                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> repeatedEntry = app.EnterRestStopAsync(roomId);
                while (!repeatedEntry.IsCompleted) yield return null;
                Assert.That(repeatedEntry.Result.Persisted, Is.True, repeatedEntry.Result.Message);
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1), "Re-entering the same rest room must not replenish fixed stock.");

                string runId = app.ActiveRun.RunId.ToString();
                string nodeId = app.ActiveExpedition.currentNodeId;
                evidence.RecordInput(4, "ProcessRestart", "Resume the saved rest room and verify its confirmed merchant and smithing transactions");
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
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(154));
                Assert.That(app.ActiveRun.Inventory.Weapon.InstanceId, Is.EqualTo(weaponInstanceId));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));
                Assert.That(FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity, Is.EqualTo(3));
                MerchantStockSnapshot stockAfterRestart = app.GetActiveMerchantStock();
                Assert.That(stockAfterRestart, Is.Not.Null);
                Assert.That(stockAfterRestart.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity,
                    Is.EqualTo(1));
                Assert.That(stockAfterRestart.roomInstanceId, Is.EqualTo(roomId.ToString()));
                evidence.RecordAssertion("Confirmed rest-room commerce, smithing and fixed stock survive restart",
                    app.ActiveExpedition.currentHealth == 40 && app.ActiveRun.Inventory.Gold == 154 &&
                    app.ActiveRun.Inventory.Weapon.InstanceId == weaponInstanceId &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 2 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 2 &&
                    FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity == 3 &&
                    stockAfterRestart.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1,
                    "health 40, gold 154, same weapon at +2, three owned potions and one remaining in stock",
                    "health=" + app.ActiveExpedition.currentHealth + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; weapon=" + app.ActiveRun.Inventory.Weapon.InstanceId + "+" + app.ActiveRun.Inventory.Weapon.UpgradeLevel +
                    "; stock=" + stockAfterRestart.offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity);

                evidence.RecordInput(5, "ReplayTransactions", "Replay the same purchase, sale and upgrade transaction IDs after restart");
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

                Assert.That(replayedPurchase.Result.Persisted, Is.True);
                Assert.That(replayedSale.Result.Persisted, Is.True);
                Assert.That(replayedFirstUpgrade.Result.Persisted, Is.True);
                Assert.That(replayedSecondUpgrade.Result.Persisted, Is.True);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(154));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(2));
                Assert.That(FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity, Is.EqualTo(3));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));
                bool replayWasIdempotent = app.ActiveRun.Inventory.Gold == 154 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 2 &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 2 &&
                    FindBackpackItem(app.ActiveRun.Inventory, "consumable.health_potion").Quantity == 3 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1;
                evidence.RecordAssertion("Replayed transaction IDs preserve committed balances, stock and upgrade level",
                    replayWasIdempotent, "gold 154, ore 2, weapon +2, three owned potions and one in stock",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; ore=" + app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) +
                    "; weaponUpgrade=" + app.ActiveRun.Inventory.Weapon.UpgradeLevel);

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
