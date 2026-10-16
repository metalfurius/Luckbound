using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Economy;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode.Economy
{
    public sealed class RestStopApplicationTests
    {
        [UnityTest]
        public IEnumerator E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", System.Guid.NewGuid().ToString("N"));
            var store = new FileSaveStore(saveDirectory, null);
            var evidence = new ScenarioEvidenceRecorder("E03", "E03_RestStopBoundariesAndIdentity", 11235813);
            try
            {
                Time.timeScale = 1f;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;

                evidence.RecordInput(0, "LoadScene", "Start Bootstrap with isolated durable storage");
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.CanStartNewExpedition, Is.True);

                evidence.RecordInput(1, "BeginExpedition", "Start a normal Bellator route");
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId,
                    new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                var begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);

                ExpeditionSaveData beforeRejectedCalls = app.ActiveExpedition;
                RoomInstanceId roomIdBeforeRestart = app.ActiveRoomInstanceId;
                Assert.That(roomIdBeforeRestart.IsValid, Is.True);
                Assert.That(app.GetActiveMerchantStock(), Is.Null,
                    "Reading the merchant view in a combat room must not create unpersisted stock.");

                evidence.RecordInput(2, "EnterRestStopAsync", "Attempt rest entry from the first route room");
                var enter = app.EnterRestStopAsync(roomIdBeforeRestart);
                while (!enter.IsCompleted) yield return null;
                Assert.That(enter.Result.Persisted, Is.False);
                Assert.That(enter.Result.Pending, Is.False);
                Assert.That(enter.Result.Outcome, Is.Null);

                evidence.RecordInput(3, "PurchaseFromMerchantAsync", "Attempt purchase from the first route room");
                var purchase = app.PurchaseFromMerchantAsync(roomIdBeforeRestart, "consumable.health_potion", 1, 0,
                    TransactionId.New());
                while (!purchase.IsCompleted) yield return null;
                Assert.That(purchase.Result.Persisted, Is.False);
                Assert.That(purchase.Result.Pending, Is.False);

                ExpeditionSaveData afterRejectedCalls = app.ActiveExpedition;
                bool noFunctionalMutation = string.Equals(JsonUtility.ToJson(beforeRejectedCalls),
                    JsonUtility.ToJson(afterRejectedCalls), System.StringComparison.Ordinal);
                evidence.RecordAssertion("Rest-stop entry and merchant operations reject outside rest without changing the save",
                    noFunctionalMutation, "both operations rejected and the expedition snapshot remains identical",
                    noFunctionalMutation ? "identical serialized expedition state" : "expedition state changed");
                Assert.That(noFunctionalMutation, Is.True);

                string runIdBeforeRestart = app.ActiveRun.RunId.ToString();
                string nodeIdBeforeRestart = afterRejectedCalls.currentNodeId;
                evidence.RecordInput(4, "ProcessRestart", "Reload the persisted expedition from Bootstrap");
                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || ApplicationBootstrapper.Instance.ActiveRun == null ||
                    SceneManager.GetActiveScene().name != "Expedition"); frame++)
                    yield return null;

                app = ApplicationBootstrapper.Instance;
                bool sameRoomAfterRestart = app != null && app.ActiveRun != null && app.ActiveExpedition != null &&
                    app.ActiveRun.RunId.ToString() == runIdBeforeRestart &&
                    app.ActiveExpedition.currentNodeId == nodeIdBeforeRestart &&
                    app.ActiveRoomInstanceId == roomIdBeforeRestart;
                evidence.RecordAssertion("Room identity is stable for the same expedition and route node after restart",
                    sameRoomAfterRestart, "same RunId, current route node and derived RoomInstanceId",
                    app == null ? "application did not restore" :
                    "run=" + app.ActiveRun?.RunId + "; node=" + app.ActiveExpedition?.currentNodeId +
                    "; room=" + app.ActiveRoomInstanceId);
                Assert.That(sameRoomAfterRestart, Is.True);

                Assert.That(evidence.Complete(), Is.Zero, "E03 boundary scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
            }
        }

        [UnityTest]
        public IEnumerator E03_PersistsRestEntryStockTaliaAndPurchaseAcrossExactRetryAndRestart()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", System.Guid.NewGuid().ToString("N"));
            var innerStore = new FileSaveStore(saveDirectory, null);
            var store = new FailOnceProfileWriteStore(innerStore);
            var evidence = new ScenarioEvidenceRecorder("E03", "E03_RestServicesDurableRetryAndRestart", 11235813);
            try
            {
                Time.timeScale = 1f;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;

                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                evidence.RecordInput(0, "BeginRestRoomFixture", "Start a valid expedition route whose initial room is a rest stop; no combat result is fabricated");
                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                InventoryOperationOutcome gold = run.Inventory.TryAddGold(100, TransactionId.New());
                Assert.That(gold.Succeeded, Is.True, "The test run starts with ordinary earned currency as a purchase precondition.");

                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);
                RoomInstanceId roomId = app.ActiveRoomInstanceId;
                Assert.That(roomId.IsValid, Is.True);
                Assert.That(app.GetActiveMerchantStock(), Is.Null,
                    "Stock remains absent until the rest-room entry and stock are saved together.");

                ExpeditionSaveData damagedState = app.ActiveExpedition;
                damagedState.currentHealth = 20;
                Task<SaveWriteResult> damageStateWrite = app.SaveActiveExpeditionStateAsync(damagedState);
                while (!damageStateWrite.IsCompleted) yield return null;
                Assert.That(damageStateWrite.Result.Succeeded, Is.True, damageStateWrite.Result.Message);

                evidence.RecordInput(1, "EnterRestStopAsync", "Fail the first profile write while preparing fixed stock and one-time recovery");
                store.FailNextProfileWrite();
                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> failedEntry = app.EnterRestStopAsync(roomId);
                while (!failedEntry.IsCompleted) yield return null;
                Assert.That(failedEntry.Result.Persisted, Is.False);
                Assert.That(failedEntry.Result.Pending, Is.True);
                Assert.That(app.HasPendingEconomyMutation, Is.True);
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(20),
                    "An uncommitted recovery result must not be published to the running game.");
                Assert.That(app.GetActiveMerchantStock(), Is.Null,
                    "An uncommitted stock candidate must not be published to the running game.");
                bool failureRemainsPrivate = app.ActiveExpedition.currentHealth == 20 && app.GetActiveMerchantStock() == null;
                evidence.RecordAssertion("Failed rest entry does not publish healing or stock before durable save",
                    failureRemainsPrivate, "health remains 20 and no merchant stock is visible",
                    failureRemainsPrivate ? "candidate retained for exact retry" : "uncommitted state was published");

                Task<SaveWriteResult> genericRetry = app.RetryPendingExpeditionWriteAsync();
                while (!genericRetry.IsCompleted) yield return null;
                Assert.That(genericRetry.Result.Succeeded, Is.False,
                    "A generic save retry cannot discard the exact typed economy result.");

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> competingAction =
                    app.UseTaliaAsync(roomId, TransactionId.New());
                while (!competingAction.IsCompleted) yield return null;
                Assert.That(competingAction.Result.Persisted, Is.False);
                Assert.That(competingAction.Result.Pending, Is.True);

                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> retriedEntry = app.EnterRestStopAsync(roomId);
                while (!retriedEntry.IsCompleted) yield return null;
                Assert.That(retriedEntry.Result.Persisted, Is.True, retriedEntry.Result.Message);
                Assert.That(retriedEntry.Result.Pending, Is.False);
                Assert.That(retriedEntry.Result.Outcome.RestOutcome.HealthAfter, Is.EqualTo(40));
                Assert.That(retriedEntry.Result.Outcome.RestOutcome.HealthRestored, Is.EqualTo(20));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(40));
                Assert.That(app.HasPendingEconomyMutation, Is.False);
                evidence.RecordAssertion("Exact rest-entry retry durably commits one recovery and fixed stock",
                    retriedEntry.Result.Persisted && retriedEntry.Result.Outcome.RestOutcome.HealthAfter == 40 &&
                    app.ActiveExpedition.currentHealth == 40 && app.GetActiveMerchantStock() != null,
                    "same operation key returns 20 restored health and 11 canonical offers",
                    "health=" + app.ActiveExpedition.currentHealth + "; offers=" + (app.GetActiveMerchantStock()?.offers.Length ?? 0));

                MerchantStockSnapshot stockAfterEntry = app.GetActiveMerchantStock();
                Assert.That(stockAfterEntry, Is.Not.Null);
                Assert.That(stockAfterEntry.offers.Length, Is.EqualTo(11));

                TransactionId taliaId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> talia = app.UseTaliaAsync(roomId, taliaId);
                while (!talia.IsCompleted) yield return null;
                Assert.That(talia.Result.Persisted, Is.True, talia.Result.Message);
                Assert.That(talia.Result.Outcome.Succeeded, Is.True);
                Assert.That(talia.Result.Outcome.HealthRestored, Is.EqualTo(50));
                Assert.That(talia.Result.Outcome.ResultingHealth, Is.EqualTo(90));

                evidence.RecordInput(2, "MerchantPurchaseAsync", "Purchase one stocked health potion with its own transaction ID");
                TransactionId purchaseId = TransactionId.New();
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> purchase =
                    app.PurchaseFromMerchantAsync(roomId, "consumable.health_potion", 1, 0, purchaseId);
                while (!purchase.IsCompleted) yield return null;
                Assert.That(purchase.Result.Persisted, Is.True, purchase.Result.Message);
                Assert.That(purchase.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));

                evidence.RecordInput(3, "PersistLaterHealthState", "Persist the expedition's later health value before replaying an older Talia receipt");
                ExpeditionSaveData laterHealthState = app.ActiveExpedition;
                laterHealthState.currentHealth = 60;
                Task<SaveWriteResult> laterHealthWrite = app.SaveActiveExpeditionStateAsync(laterHealthState);
                while (!laterHealthWrite.IsCompleted) yield return null;
                Assert.That(laterHealthWrite.Result.Succeeded, Is.True, laterHealthWrite.Result.Message);
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(60));

                string runId = app.ActiveRun.RunId.ToString();
                string nodeId = app.ActiveExpedition.currentNodeId;
                evidence.RecordInput(4, "ProcessRestart", "Reload the saved rest room, health, merchant stock, and transaction receipts");
                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || ApplicationBootstrapper.Instance.ActiveRun == null ||
                    SceneManager.GetActiveScene().name != "Expedition"); frame++)
                    yield return null;

                app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.ActiveRun.RunId.ToString(), Is.EqualTo(runId));
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(nodeId));
                Assert.That(app.ActiveRoomInstanceId, Is.EqualTo(roomId));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(60));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));
                evidence.RecordAssertion("Rest room state survives process restart with stable room identity",
                    app.ActiveRun.RunId.ToString() == runId && app.ActiveExpedition.currentNodeId == nodeId &&
                    app.ActiveRoomInstanceId == roomId && app.ActiveExpedition.currentHealth == 60 &&
                    app.ActiveRun.Inventory.Gold == 62 && app.GetActiveMerchantStock().offers.Single(
                        offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1,
                    "same run/node/room identity, saved health, gold and remaining stock",
                    "run=" + app.ActiveRun.RunId + "; node=" + nodeId + "; room=" + app.ActiveRoomInstanceId +
                    "; health=" + app.ActiveExpedition.currentHealth + "; gold=" + app.ActiveRun.Inventory.Gold);

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> repeatedTalia =
                    app.UseTaliaAsync(roomId, taliaId);
                while (!repeatedTalia.IsCompleted) yield return null;
                Assert.That(repeatedTalia.Result.Persisted, Is.True);
                Assert.That(repeatedTalia.Result.Outcome.ResultingHealth, Is.EqualTo(90));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(60),
                    "A cached Talia receipt must not overwrite health that changed after its original transaction.");
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62),
                    "A persisted Talia transaction ID must not charge again after restart.");

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> repeatedPurchase =
                    app.PurchaseFromMerchantAsync(roomId, "consumable.health_potion", 1, 0, purchaseId);
                while (!repeatedPurchase.IsCompleted) yield return null;
                Assert.That(repeatedPurchase.Result.Persisted, Is.True);
                Assert.That(repeatedPurchase.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62),
                    "A persisted purchase transaction ID must not charge again after restart.");
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));
                bool noDuplicateCharge = app.ActiveRun.Inventory.Gold == 62 && app.ActiveExpedition.currentHealth == 60 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                        .remainingQuantity == 1;
                evidence.RecordAssertion("Replaying Talia and purchase transaction IDs after restart does not repeat their effects",
                    noDuplicateCharge, "gold remains 62, health 60 and one potion remains in stock",
                    noDuplicateCharge ? "no duplicate health, stock or currency mutation" : "a replay mutated saved state");
                Assert.That(evidence.Complete(), Is.Zero, "E03 rest service scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        private static ExpeditionRunConfiguration CreateRunStartingAtRest()
        {
            // Keep the canonical route invariants but start this adapter fixture at a rest node. This avoids
            // manufacturing a preceding combat result merely to exercise rest-only application services.
            ExpeditionRunConfiguration generated = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            string[] lines = generated.Route.CanonicalForm.Split('\n');
            int firstNodeLine = System.Array.FindIndex(lines, line => line.StartsWith(
                "N|" + generated.Route.MandatoryNodeIds[0] + "|", System.StringComparison.Ordinal));
            ExpeditionRouteNode restNode = generated.Route.MandatoryNodeIds.Skip(1)
                .Select(nodeId => generated.Route.Nodes.Single(node =>
                    string.Equals(node.NodeId, nodeId, System.StringComparison.Ordinal)))
                .FirstOrDefault(node => node.Kind == ExpeditionRouteNodeKind.Rest);
            int restNodeLine = System.Array.FindIndex(lines, line => line.StartsWith(
                "N|" + restNode.NodeId + "|", System.StringComparison.Ordinal));
            if (firstNodeLine < 0 || restNodeLine < 0)
                throw new System.InvalidOperationException("The generated route is missing its first or mandatory rest node.");

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

        private static void DeleteIsolatedSaveDirectory(string directory)
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(tempRoot, System.StringComparison.OrdinalIgnoreCase))
                throw new System.InvalidOperationException("Refusing to delete a save directory outside the system temp root.");
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
