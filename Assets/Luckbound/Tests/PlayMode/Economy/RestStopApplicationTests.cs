using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Economy;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode.Economy
{
    public sealed class RestStopApplicationTests
    {
        [UnityTest]
        public IEnumerator E03_RejectsRestServicesOutsideRestAndRestoresStableRoomIdentity()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", System.Guid.NewGuid().ToString("N"));
            var store = new FailOnceProfileWriteStore(new FileSaveStore(saveDirectory, null));
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
                bool roomIdentityStableInWriteSession = app.ActiveRun.RunId.ToString() == runIdBeforeRestart &&
                    app.ActiveExpedition.currentNodeId == nodeIdBeforeRestart && app.ActiveRoomInstanceId == roomIdBeforeRestart;
                evidence.RecordAssertion("Run, route node and room identity remain stable in the read-write session",
                    roomIdentityStableInWriteSession, "same RunId, node and derived RoomInstanceId before reload",
                    "run=" + app.ActiveRun.RunId + "; node=" + app.ActiveExpedition.currentNodeId +
                    "; room=" + app.ActiveRoomInstanceId);
                Assert.That(roomIdentityStableInWriteSession, Is.True);
                string expeditionJsonBeforeRestart = JsonUtility.ToJson(app.ActiveExpedition, false);
                int profileWritesBeforeRestart = store.ProfileWriteAttempts;
                Task<SaveStorageSnapshot> storageBeforeRestartTask = store.ReadAsync(SaveDocument.Profile);
                while (!storageBeforeRestartTask.IsCompleted) yield return null;
                SaveStorageSnapshot storageBeforeRestart = storageBeforeRestartTask.Result;
                evidence.RecordInput(4, "ProcessRestart", "Reload the persisted expedition from Bootstrap");
                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                app = ApplicationBootstrapper.Instance;
                bool preservedReadOnlySnapshot = app != null && app.SaveDataReady &&
                    app.ProfileAccess == SaveDataAccess.ReadOnlyLegacyEncounterPinMissing && !app.CanWriteProfile &&
                    !app.CanStartNewExpedition && !app.CanContinueExpedition && app.HasActiveExpedition && app.ActiveRun == null &&
                    app.ActiveExpedition != null && app.ActiveExpeditionLoadError == "LegacyEncounterPinMissing" &&
                    SceneManager.GetActiveScene().name == "Menu" &&
                    app.ActiveExpedition.runId == runIdBeforeRestart && app.ActiveExpedition.currentNodeId == nodeIdBeforeRestart &&
                    JsonUtility.ToJson(app.ActiveExpedition, false) == expeditionJsonBeforeRestart &&
                    store.ProfileWriteAttempts == profileWritesBeforeRestart;
                evidence.RecordAssertion("V6 restart preserves the active snapshot for diagnostics and blocks continuation",
                    preservedReadOnlySnapshot, "Menu, no ActiveRun, read-only V6, same run/node snapshot and no load-time profile write",
                    app == null ? "application did not load" :
                    "scene=" + SceneManager.GetActiveScene().name + "; access=" + app.ProfileAccess +
                    "; activeRun=" + (app.ActiveRun != null) + "; error=" + app.ActiveExpeditionLoadError +
                    "; run=" + app.ActiveExpedition?.runId + "; node=" + app.ActiveExpedition?.currentNodeId +
                    "; writes=" + (store.ProfileWriteAttempts - profileWritesBeforeRestart));
                Assert.That(preservedReadOnlySnapshot, Is.True);

                Task<SaveStorageSnapshot> storageAfterLoadTask = store.ReadAsync(SaveDocument.Profile);
                while (!storageAfterLoadTask.IsCompleted) yield return null;
                SaveStorageSnapshot storageAfterLoad = storageAfterLoadTask.Result;
                Assert.That(storageAfterLoad.PrimaryJson, Is.EqualTo(storageBeforeRestart.PrimaryJson));
                Assert.That(storageAfterLoad.BackupJson, Is.EqualTo(storageBeforeRestart.BackupJson));

                string profileBeforeBlockedCalls = JsonUtility.ToJson(app.Profile, false);
                string expeditionBeforeBlockedCalls = JsonUtility.ToJson(app.ActiveExpedition, false);
                long revisionBeforeBlockedCalls = app.ActiveExpeditionRevision;
                Task<SaveWriteResult> blockedStart = app.BeginExpeditionAsync(null);
                while (!blockedStart.IsCompleted) yield return null;
                Assert.That(blockedStart.Result.Succeeded, Is.False);
                Assert.Throws<InvalidOperationException>(() => app.ContinueActiveExpedition());
                Task<SaveWriteResult> blockedSnapshot = app.SaveActiveExpeditionStateAsync(app.ActiveExpedition);
                while (!blockedSnapshot.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> blockedEntry = app.EnterRestStopAsync(roomIdBeforeRestart);
                while (!blockedEntry.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> blockedPurchase =
                    app.PurchaseFromMerchantAsync(roomIdBeforeRestart, "consumable.health_potion", 1, 0, TransactionId.New());
                while (!blockedPurchase.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> blockedTalia =
                    app.UseTaliaAsync(roomIdBeforeRestart, TransactionId.New());
                while (!blockedTalia.IsCompleted) yield return null;
                Assert.That(blockedSnapshot.Result.Succeeded, Is.False);
                Assert.That(blockedEntry.Result.Persisted, Is.False);
                Assert.That(blockedEntry.Result.Pending, Is.False);
                Assert.That(blockedEntry.Result.Outcome, Is.Null);
                Assert.That(blockedPurchase.Result.Persisted, Is.False);
                Assert.That(blockedPurchase.Result.Pending, Is.False);
                Assert.That(blockedPurchase.Result.Outcome, Is.Null);
                Assert.That(blockedTalia.Result.Persisted, Is.False);
                Assert.That(blockedTalia.Result.Pending, Is.False);
                Assert.That(blockedTalia.Result.Outcome, Is.Null);
                Task<SaveStorageSnapshot> storageAfterCallsTask = store.ReadAsync(SaveDocument.Profile);
                while (!storageAfterCallsTask.IsCompleted) yield return null;
                SaveStorageSnapshot storageAfterCalls = storageAfterCallsTask.Result;
                bool blockedCallsPreserveProtectedState =
                    JsonUtility.ToJson(app.Profile, false) == profileBeforeBlockedCalls &&
                    JsonUtility.ToJson(app.ActiveExpedition, false) == expeditionBeforeBlockedCalls &&
                    app.ActiveExpeditionRevision == revisionBeforeBlockedCalls && app.ActiveRun == null &&
                    app.HasActiveExpedition && !app.HasPendingExpeditionWrite && !app.HasPendingEconomyMutation &&
                    storageAfterCalls.PrimaryJson == storageBeforeRestart.PrimaryJson &&
                    storageAfterCalls.BackupJson == storageBeforeRestart.BackupJson &&
                    store.ProfileWriteAttempts == profileWritesBeforeRestart;
                evidence.RecordAssertion("Protected V6 rejects start, continuation, save and rest services without writes",
                    blockedCallsPreserveProtectedState, "snapshot and primary/backup bytes unchanged; zero profile writes",
                    "run=" + app.ActiveExpedition.runId + "; node=" + app.ActiveExpedition.currentNodeId +
                    "; writes=" + (store.ProfileWriteAttempts - profileWritesBeforeRestart));
                Assert.That(blockedCallsPreserveProtectedState, Is.True);

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

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> repeatedTalia =
                    app.UseTaliaAsync(roomId, taliaId);
                while (!repeatedTalia.IsCompleted) yield return null;
                Assert.That(repeatedTalia.Result.Persisted, Is.True);
                Assert.That(repeatedTalia.Result.Outcome.ResultingHealth, Is.EqualTo(90));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(60),
                    "A cached Talia receipt must not overwrite health that changed after its original transaction.");
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62),
                    "Replaying a Talia receipt in the original read-write session must not charge again.");

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> repeatedPurchase =
                    app.PurchaseFromMerchantAsync(roomId, "consumable.health_potion", 1, 0, purchaseId);
                while (!repeatedPurchase.IsCompleted) yield return null;
                Assert.That(repeatedPurchase.Result.Persisted, Is.True);
                Assert.That(repeatedPurchase.Result.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62),
                    "Replaying the purchase transaction in the original read-write session must not charge again.");
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                    .remainingQuantity, Is.EqualTo(1));
                bool noDuplicateCharge = app.ActiveRun.Inventory.Gold == 62 && app.ActiveExpedition.currentHealth == 60 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion")
                        .remainingQuantity == 1;
                evidence.RecordAssertion("Replaying Talia and purchase transaction IDs in the read-write session is idempotent",
                    noDuplicateCharge, "gold remains 62, health 60 and one potion remains in stock",
                    noDuplicateCharge ? "no duplicate health, stock or currency mutation" : "a replay mutated saved state");
                Assert.That(noDuplicateCharge, Is.True);

                string runId = app.ActiveRun.RunId.ToString();
                string nodeId = app.ActiveExpedition.currentNodeId;
                bool roomIdentityStableInWriteSession = app.ActiveRun.RunId.ToString() == runId &&
                    app.ActiveExpedition.currentNodeId == nodeId && app.ActiveRoomInstanceId == roomId;
                evidence.RecordAssertion("Run, route node and room identity remain stable in the read-write session",
                    roomIdentityStableInWriteSession, "same RunId, node and RoomInstanceId through rest services",
                    "run=" + app.ActiveRun.RunId + "; node=" + app.ActiveExpedition.currentNodeId +
                    "; room=" + app.ActiveRoomInstanceId);
                Assert.That(roomIdentityStableInWriteSession, Is.True);
                string expeditionJsonBeforeRestart = JsonUtility.ToJson(app.ActiveExpedition, false);
                int profileWritesBeforeRestart = store.ProfileWriteAttempts;
                Task<SaveStorageSnapshot> storageBeforeRestartTask = store.ReadAsync(SaveDocument.Profile);
                while (!storageBeforeRestartTask.IsCompleted) yield return null;
                SaveStorageSnapshot storageBeforeRestart = storageBeforeRestartTask.Result;
                evidence.RecordInput(4, "ProcessRestart", "Reload the saved rest room, health, merchant stock, and transaction receipts");
                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                bool preservedReadOnlySnapshot = app.SaveDataReady &&
                    app.ProfileAccess == SaveDataAccess.ReadOnlyLegacyEncounterPinMissing && !app.CanWriteProfile &&
                    !app.CanStartNewExpedition && !app.CanContinueExpedition && app.HasActiveExpedition && app.ActiveRun == null &&
                    app.ActiveExpedition != null && app.ActiveExpeditionLoadError == "LegacyEncounterPinMissing" &&
                    SceneManager.GetActiveScene().name == "Menu" && app.ActiveExpedition.runId == runId &&
                    app.ActiveExpedition.currentNodeId == nodeId &&
                    JsonUtility.ToJson(app.ActiveExpedition, false) == expeditionJsonBeforeRestart &&
                    store.ProfileWriteAttempts == profileWritesBeforeRestart;
                evidence.RecordAssertion("V6 restart preserves the active rest snapshot for diagnostics and blocks continuation",
                    preservedReadOnlySnapshot, "Menu, no ActiveRun, read-only V6, same run/node snapshot and no load-time profile write",
                    "scene=" + SceneManager.GetActiveScene().name + "; access=" + app.ProfileAccess +
                    "; activeRun=" + (app.ActiveRun != null) + "; error=" + app.ActiveExpeditionLoadError +
                    "; run=" + app.ActiveExpedition?.runId + "; node=" + app.ActiveExpedition?.currentNodeId +
                    "; writes=" + (store.ProfileWriteAttempts - profileWritesBeforeRestart));
                Assert.That(preservedReadOnlySnapshot, Is.True);

                Task<SaveStorageSnapshot> storageAfterLoadTask = store.ReadAsync(SaveDocument.Profile);
                while (!storageAfterLoadTask.IsCompleted) yield return null;
                SaveStorageSnapshot storageAfterLoad = storageAfterLoadTask.Result;
                Assert.That(storageAfterLoad.PrimaryJson, Is.EqualTo(storageBeforeRestart.PrimaryJson));
                Assert.That(storageAfterLoad.BackupJson, Is.EqualTo(storageBeforeRestart.BackupJson));

                string profileBeforeBlockedCalls = JsonUtility.ToJson(app.Profile, false);
                string expeditionBeforeBlockedCalls = JsonUtility.ToJson(app.ActiveExpedition, false);
                long revisionBeforeBlockedCalls = app.ActiveExpeditionRevision;
                Task<SaveWriteResult> blockedStart = app.BeginExpeditionAsync(null);
                while (!blockedStart.IsCompleted) yield return null;
                Assert.That(blockedStart.Result.Succeeded, Is.False);
                Assert.Throws<InvalidOperationException>(() => app.ContinueActiveExpedition());
                Task<SaveWriteResult> blockedSnapshot = app.SaveActiveExpeditionStateAsync(app.ActiveExpedition);
                while (!blockedSnapshot.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> blockedEntry = app.EnterRestStopAsync(roomId);
                while (!blockedEntry.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> blockedTalia =
                    app.UseTaliaAsync(roomId, TransactionId.New());
                while (!blockedTalia.IsCompleted) yield return null;
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> blockedPurchase =
                    app.PurchaseFromMerchantAsync(roomId, "consumable.health_potion", 1, 0, TransactionId.New());
                while (!blockedPurchase.IsCompleted) yield return null;
                Assert.That(blockedSnapshot.Result.Succeeded, Is.False);
                Assert.That(blockedEntry.Result.Persisted, Is.False);
                Assert.That(blockedEntry.Result.Pending, Is.False);
                Assert.That(blockedEntry.Result.Outcome, Is.Null);
                Assert.That(blockedTalia.Result.Persisted, Is.False);
                Assert.That(blockedTalia.Result.Pending, Is.False);
                Assert.That(blockedTalia.Result.Outcome, Is.Null);
                Assert.That(blockedPurchase.Result.Persisted, Is.False);
                Assert.That(blockedPurchase.Result.Pending, Is.False);
                Assert.That(blockedPurchase.Result.Outcome, Is.Null);
                Task<SaveStorageSnapshot> storageAfterCallsTask = store.ReadAsync(SaveDocument.Profile);
                while (!storageAfterCallsTask.IsCompleted) yield return null;
                SaveStorageSnapshot storageAfterCalls = storageAfterCallsTask.Result;
                bool blockedCallsPreserveProtectedState =
                    JsonUtility.ToJson(app.Profile, false) == profileBeforeBlockedCalls &&
                    JsonUtility.ToJson(app.ActiveExpedition, false) == expeditionBeforeBlockedCalls &&
                    app.ActiveExpeditionRevision == revisionBeforeBlockedCalls && app.ActiveRun == null &&
                    app.HasActiveExpedition && !app.HasPendingExpeditionWrite && !app.HasPendingEconomyMutation &&
                    storageAfterCalls.PrimaryJson == storageBeforeRestart.PrimaryJson &&
                    storageAfterCalls.BackupJson == storageBeforeRestart.BackupJson &&
                    store.ProfileWriteAttempts == profileWritesBeforeRestart;
                evidence.RecordAssertion("Protected V6 rejects start, continuation, save and rest services without writes",
                    blockedCallsPreserveProtectedState, "snapshot and primary/backup bytes unchanged; zero profile writes",
                    "run=" + app.ActiveExpedition.runId + "; node=" + app.ActiveExpedition.currentNodeId +
                    "; writes=" + (store.ProfileWriteAttempts - profileWritesBeforeRestart));
                Assert.That(blockedCallsPreserveProtectedState, Is.True);
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

        [UnityTest]
        public IEnumerator F09_RestAndTaliaHealthReachBoundActorOnlyAfterDurablePublicationRetry()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundF09Session", System.Guid.NewGuid().ToString("N"));
            GameObject player = null;
            GameObject pauseOwner = null;
            ApplicationBootstrapper app = null;
            IDisposable pauseBinding = null;
            IDisposable publicationBinding = null;
            int leasesAcquired = 0;
            int leasesReleased = 0;
            int captures = 0;
            int preparations = 0;
            int publicationAttempts = 0;
            bool failNextPublication = false;
            float previousTimeScale = Time.timeScale;
            SimulationMode2D previousPhysicsMode = Physics2D.simulationMode;
            try
            {
                Time.timeScale = 1f;
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => new FileSaveStore(saveDirectory, null);
                if (ApplicationBootstrapper.Instance != null)
                    Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;

                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;
                app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);

                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                Assert.That(run.Inventory.TryAddGold(100, TransactionId.New()).Succeeded, Is.True);
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);

                ExpeditionSaveData injured = app.ActiveExpedition.Clone();
                injured.currentHealth = 20;
                Task<SaveWriteResult> injury = app.SaveActiveExpeditionStateAsync(injured);
                while (!injury.IsCompleted) yield return null;
                Assert.That(injury.Result.Succeeded, Is.True, injury.Result.Message);

                var actorId = ActorId.New();
                var input = new PlayerInputBuffer(actorId);
                player = new GameObject("F09 Rest Session Projection Player");
                Rigidbody2D body = player.AddComponent<Rigidbody2D>();
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
                CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(0.75f, 1.5f);
                PlayerMotor2D motor = player.AddComponent<PlayerMotor2D>();
                ItemCollector2D collector = player.AddComponent<ItemCollector2D>();

                TextAsset weaponsSource = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
                TextAsset skillsSource = Resources.Load<TextAsset>("Luckbound/Data/Skills");
                TextAsset armorsSource = Resources.Load<TextAsset>("Luckbound/Data/Items/Armors");
                Assert.That(weaponsSource, Is.Not.Null);
                Assert.That(skillsSource, Is.Not.Null);
                Assert.That(armorsSource, Is.Not.Null);
                WeaponCatalog weapons = WeaponCatalogJsonLoader.Load(weaponsSource.text, run.Inventory.Catalog);
                SkillCatalog skills = SkillCatalogJsonLoader.Load(skillsSource.text);
                ArmorCatalog armors = ArmorCatalogJsonLoader.Load(armorsSource.text, run.Inventory.Catalog);
                int groundLayer = LayerMask.NameToLayer("Ground");
                int enemyLayer = LayerMask.NameToLayer("Enemies");
                Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0));
                Assert.That(enemyLayer, Is.GreaterThanOrEqualTo(0));
                ExpeditionPlayerRunBinder.BindFromCheckpoint(run, app.ActiveExpedition, actorId, input,
                    1 << groundLayer, 1 << enemyLayer, 0, motor, collector, run.Inventory.Catalog,
                    weapons, skills, armorCatalog: armors);
                Assert.That(motor.CurrentHealth, Is.EqualTo(20));

                pauseOwner = new GameObject("F09 Rest Session Pause Owner");
                var pauseLabelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                pauseLabelObject.transform.SetParent(pauseOwner.transform, false);
                ExpeditionPauseController pauseController = pauseOwner.AddComponent<ExpeditionPauseController>();
                pauseController.Initialize(pauseLabelObject.GetComponent<Text>());
                pauseBinding = app.BindExpeditionSimulationPauseLeaseProvider(pauseController, () =>
                {
                    leasesAcquired++;
                    IDisposable lease = pauseController.AcquireSimulationPauseLease();
                    return new CountingPauseLease(() =>
                    {
                        lease.Dispose();
                        leasesReleased++;
                    });
                });
                publicationBinding = app.BindExpeditionSessionMutationPublication(pauseController,
                    (candidateRun, candidateSave) =>
                    {
                        captures++;
                        Assert.That(candidateRun.RunId, Is.EqualTo(run.RunId));
                        Assert.That(pauseController.IsSimulationPaused, Is.True);
                        Assert.That(pauseController.IsPaused, Is.False,
                            "The durable session lease must stay separate from the manual pause state.");
                        Assert.That(leasesReleased, Is.LessThan(leasesAcquired),
                            "The actor must be captured while its mutation lease is held.");
                        candidateSave.maximumHealth = motor.CombatStats.MaximumHealth;
                        candidateSave.currentHealth = motor.CurrentHealth;
                        candidateSave.maximumEnergy = motor.MaximumEnergy;
                        candidateSave.currentEnergy = motor.CurrentEnergy;
                    },
                    (candidateRun, candidateSave) =>
                    {
                        preparations++;
                        ExpeditionPlayerRunBinder.PreparedCommittedRunPublication prepared =
                            ExpeditionPlayerRunBinder.PrepareCommittedRunPublication(
                                candidateRun, candidateSave, motor, collector, weapons, armors);
                        prepared.ApplyToSave(candidateSave);
                        return () =>
                        {
                            publicationAttempts++;
                            Assert.That(pauseController.IsSimulationPaused, Is.True);
                            Assert.That(leasesReleased, Is.LessThan(leasesAcquired),
                                "The prepared actor must publish before its mutation lease is released.");
                            if (failNextPublication)
                            {
                                failNextPublication = false;
                                throw new System.InvalidOperationException("simulated actor publication fault");
                            }
                            prepared.Publish();
                        };
                    });

                RoomInstanceId roomId = app.ActiveRoomInstanceId;
                failNextPublication = true;
                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> entry = app.EnterRestStopAsync(roomId);
                while (!entry.IsCompleted) yield return null;
                Assert.That(entry.Result.Persisted, Is.False);
                Assert.That(entry.Result.Pending, Is.True, entry.Result.Message);
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(40),
                    "The recovery is durable before publication, while the simulation remains paused.");
                Assert.That(motor.CurrentHealth, Is.EqualTo(20),
                    "A failed publisher must not partly apply recovery to the actor.");
                Assert.That(leasesAcquired, Is.EqualTo(1));
                Assert.That(leasesReleased, Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(0f));

                Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> entryRetry = app.EnterRestStopAsync(roomId);
                while (!entryRetry.IsCompleted) yield return null;
                Assert.That(entryRetry.Result.Persisted, Is.True, entryRetry.Result.Message);
                Assert.That(entryRetry.Result.Pending, Is.False);
                Assert.That(motor.CurrentHealth, Is.EqualTo(40));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(motor.CurrentHealth));
                Assert.That(leasesReleased, Is.EqualTo(1));
                Assert.That(Time.timeScale, Is.EqualTo(1f));

                int energyBeforeTalia = motor.CurrentEnergy;
                int goldBeforeTalia = app.ActiveRun.Inventory.Gold;
                TransactionId taliaId = TransactionId.New();
                failNextPublication = true;
                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> talia =
                    app.UseTaliaAsync(roomId, taliaId);
                while (!talia.IsCompleted) yield return null;
                Assert.That(talia.Result.Persisted, Is.False);
                Assert.That(talia.Result.Pending, Is.True, talia.Result.Message);
                Assert.That(talia.Result.Outcome.Succeeded, Is.True);
                int taliaHealth = talia.Result.Outcome.ResultingHealth;
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(taliaHealth));
                Assert.That(motor.CurrentHealth, Is.EqualTo(40),
                    "Talia's durable health delta must not apply before its actor publisher succeeds.");
                Assert.That(collector.Inventory.Gold, Is.EqualTo(goldBeforeTalia),
                    "The composed actor inventory must remain at its last published snapshot while paused.");
                Assert.That(leasesAcquired, Is.EqualTo(2));
                Assert.That(leasesReleased, Is.EqualTo(1));
                Assert.That(Time.timeScale, Is.EqualTo(0f));

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> taliaRetry =
                    app.UseTaliaAsync(roomId, taliaId);
                while (!taliaRetry.IsCompleted) yield return null;
                Assert.That(taliaRetry.Result.Persisted, Is.True, taliaRetry.Result.Message);
                Assert.That(taliaRetry.Result.Outcome.ResultingHealth, Is.EqualTo(taliaHealth));
                Assert.That(motor.CurrentHealth, Is.EqualTo(taliaHealth));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(taliaHealth));
                Assert.That(motor.CurrentEnergy, Is.EqualTo(energyBeforeTalia));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(goldBeforeTalia - RestStopService.TaliaGoldCost));
                Assert.That(collector.Inventory.Gold, Is.EqualTo(app.ActiveRun.Inventory.Gold));
                Assert.That(leasesReleased, Is.EqualTo(2));
                Assert.That(captures, Is.EqualTo(2), "Exact retries must reuse the original captured state.");
                Assert.That(preparations, Is.EqualTo(2), "Exact retries must reuse the prepared publication.");
                Assert.That(publicationAttempts, Is.EqualTo(4));
                Assert.That(app.HasPendingEconomyMutation, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                publicationBinding?.Dispose();
                pauseBinding?.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (player != null) Object.DestroyImmediate(player);
                if (pauseOwner != null) Object.DestroyImmediate(pauseOwner);
                if (app != null) Object.DestroyImmediate(app.gameObject);
                else if (ApplicationBootstrapper.Instance != null)
                    Object.DestroyImmediate(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = previousTimeScale;
                Physics2D.simulationMode = previousPhysicsMode;
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

        private sealed class CountingPauseLease : System.IDisposable
        {
            private System.Action _onDispose;

            public CountingPauseLease(System.Action onDispose) { _onDispose = onDispose; }

            public void Dispose()
            {
                System.Action callback = _onDispose;
                _onDispose = null;
                callback?.Invoke();
            }
        }

        private sealed class FailOnceProfileWriteStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _failNextProfileWrite;

            public FailOnceProfileWriteStore(ISaveStore inner) { _inner = inner; }
            public int ProfileWriteAttempts { get; private set; }
            public void FailNextProfileWrite() => _failNextProfileWrite = true;
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile) ProfileWriteAttempts++;
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
