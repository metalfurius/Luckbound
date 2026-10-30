using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Crafting;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class CraftingSessionTests
    {
        private const string HealthPotionRecipeId = "recipe.consumable.health_potion";
        private const int ScenarioSeed = 27182818;
        private static readonly Vector2 IsolatedPickupPosition = new Vector2(1000f, 1000f);

        [UnityTest]
        public IEnumerator E02_PhysicalPickupCraftRetryRejectionAndRoomCheckpointRestart()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE02", Guid.NewGuid().ToString("N"));
            var backingStore = new FileSaveStore(saveDirectory, null);
            var failingStore = new FailOnceProfileWriteStore(backingStore);
            var createdObjects = new System.Collections.Generic.List<GameObject>();
            var evidence = new ScenarioEvidenceRecorder("E02", "E02_PhysicalCraftCommitRetryAndRestart", ScenarioSeed);
            try
            {
                UnityEngine.Random.InitState(ScenarioSeed);
                Time.timeScale = 1f;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => failingStore;
                if (ApplicationBootstrapper.Instance != null)
                    UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;

                evidence.RecordInput(0, "LoadScene", "Bootstrap with isolated durable storage");
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                bool profileReady = app != null && app.SaveDataReady && app.CanStartNewExpedition;
                evidence.RecordAssertion("Bootstrap loads an isolated profile before a new expedition can start",
                    profileReady, "ready profile with no active run", app == null ? "missing app" :
                    "ready=" + app.SaveDataReady + "; canStart=" + app.CanStartNewExpedition);
                Assert.That(profileReady, Is.True);

                ContractProgressionCommitResult acceptedCraftContract = null;
                yield return Complete(app.AcceptContractAsync("CT04"), value => acceptedCraftContract = value);
                Assert.That(acceptedCraftContract.Succeeded, Is.True, acceptedCraftContract.Message);
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.Zero);

                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId,
                    new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                evidence.RecordInput(1, "BeginExpedition", "Start Bellator with its normal initial inventory");
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);
                ExpeditionSaveData roomEntry = app.ActiveExpedition;
                evidence.RecordAssertion("The expedition and immutable room-entry checkpoint are durable",
                    app.ActiveRun != null && roomEntry != null && !string.IsNullOrWhiteSpace(roomEntry.roomEntryCheckpointJson),
                    "active Bellator run and room-entry checkpoint", roomEntry == null ? "missing save" : roomEntry.runId);
                Assert.That(roomEntry, Is.Not.Null);

                evidence.RecordInput(2, "LoadScene", "Enter the expedition crafting interaction");
                SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
                ExpeditionRunSessionPresenter presenter = null;
                Button craftButton = null;
                for (int frame = 0; frame < 90 && (SceneManager.GetActiveScene().name != "Expedition" ||
                    (presenter = UnityEngine.Object.FindAnyObjectByType<ExpeditionRunSessionPresenter>()) == null ||
                    (craftButton = FindButton("CraftHealthPotionButton")) == null); frame++)
                    yield return null;
                Assert.That(presenter, Is.Not.Null, "The Expedition scene must contain its session presenter.");
                Assert.That(craftButton, Is.Not.Null, "The player-facing craft intent must exist in the Expedition scene.");

                ExpeditionInventory inventory = app.ActiveRun.Inventory;
                ItemCatalog catalog = inventory.Catalog;
                CreatePhysicalCollector(inventory, catalog, createdObjects);
                WorldMaterialPickup2D herbs = CreateWorldMaterialPickup("E02UmbraHerbPickup", MaterialKind.UmbraHerb, 2,
                    IsolatedPickupPosition, catalog, createdObjects);
                WorldMaterialPickup2D resin = CreateWorldMaterialPickup("E02DarkResinPickup", MaterialKind.DarkResin, 1,
                    IsolatedPickupPosition, catalog, createdObjects);
                Physics2D.SyncTransforms();
                evidence.RecordInput(3, "Physics2DTrigger", "Collect 2 UmbraHerb and 1 DarkResin from overlapping world pickups");
                yield return new WaitForFixedUpdate();
                bool pickedUpMaterials = herbs.IsConsumed && resin.IsConsumed &&
                    inventory.GetMaterialCount(MaterialKind.UmbraHerb) == 2 &&
                    inventory.GetMaterialCount(MaterialKind.DarkResin) == 1;
                evidence.RecordAssertion("Physical pickups place the exact recipe cost in the separate material bag",
                    pickedUpMaterials, "herbs=2; resin=1; both world pickups consumed",
                    "herbs consumed=" + herbs.IsConsumed + "; resin consumed=" + resin.IsConsumed +
                    "; bag=" + inventory.GetMaterialCount(MaterialKind.UmbraHerb) + "/" +
                    inventory.GetMaterialCount(MaterialKind.DarkResin));
                Assert.That(pickedUpMaterials, Is.True);

                failingStore.FailNextProfileWrite();
                evidence.RecordInput(4, "UIButton", "CraftHealthPotionButton.onClick; first profile write fails once");
                craftButton.onClick.Invoke();
                for (int frame = 0; frame < 120 && !app.HasPendingCraftingWrite; frame++) yield return null;
                ExpeditionInventorySnapshot beforeRetrySnapshot = ReadInventory(roomEntry);
                bool failedWriteKeepsCandidatePending = app.HasPendingCraftingWrite &&
                    inventory.QuickConsumable != null && inventory.QuickConsumable.Quantity == 2 &&
                    inventory.GetMaterialCount(MaterialKind.UmbraHerb) == 2 &&
                    inventory.GetMaterialCount(MaterialKind.DarkResin) == 1 &&
                    beforeRetrySnapshot.transactions.All(item => item.transactionId != presenter.LastCraftTransactionId.ToString());
                evidence.RecordAssertion("A failed save keeps the exact craft pending without publishing its candidate",
                    failedWriteKeepsCandidatePending, "2 potions and materials remain live; no craft receipt is published",
                    "pending=" + app.HasPendingCraftingWrite + "; potions=" + inventory.QuickConsumable?.Quantity +
                    "; herbs=" + inventory.GetMaterialCount(MaterialKind.UmbraHerb) +
                    "; resin=" + inventory.GetMaterialCount(MaterialKind.DarkResin));
                Assert.That(failedWriteKeepsCandidatePending, Is.True);

                TransactionId committedCraftId = presenter.LastCraftTransactionId;
                evidence.RecordInput(5, "UIButton", "Repeat the same craft button to retry the original pending operation");
                craftButton.onClick.Invoke();
                for (int frame = 0; frame < 120 && (presenter.IsCrafting || app.HasPendingCraftingWrite); frame++)
                    yield return null;
                inventory = app.ActiveRun.Inventory;
                bool retryCommitsExactRecipe = !app.HasPendingCraftingWrite && presenter.LastCraftTransactionId == committedCraftId &&
                    inventory.QuickConsumable != null && inventory.QuickConsumable.Quantity == 3 &&
                    inventory.GetMaterialCount(MaterialKind.UmbraHerb) == 0 &&
                    inventory.GetMaterialCount(MaterialKind.DarkResin) == 0 &&
                    presenter.CraftStatusText.Contains("FABRICADO");
                evidence.RecordAssertion("Retry commits one potion and consumes exactly the recipe cost",
                    retryCommitsExactRecipe, "3 potions; herbs=0; resin=0; same TransactionId; success visible in session UI",
                    "pending=" + app.HasPendingCraftingWrite + "; potions=" + inventory.QuickConsumable?.Quantity +
                    "; herbs=" + inventory.GetMaterialCount(MaterialKind.UmbraHerb) +
                    "; resin=" + inventory.GetMaterialCount(MaterialKind.DarkResin) +
                    "; status=" + presenter.CraftStatusText);
                Assert.That(retryCommitsExactRecipe, Is.True);
                for (int frame = 0; frame < 120 && app.ContractProgression.GetProgress("CT04") != 1; frame++) yield return null;
                Assert.That(app.ContractProgression.GetProgress("CT04"), Is.EqualTo(1),
                    "Only the persisted successful crafting transaction should advance CT04.");
                ProfileSaveEnvelope committedEnvelope = JsonUtility.FromJson<ProfileSaveEnvelope>(
                    failingStore.LastSuccessfulProfileJson);
                ExpeditionInventorySnapshot committedInventory = JsonUtility.FromJson<ExpeditionInventorySnapshot>(
                    committedEnvelope.activeExpedition.inventorySnapshotJson);
                bool transactionAndContractReceiptShareCommit =
                    committedInventory.transactions.Any(transaction =>
                        transaction.transactionId == committedCraftId.ToString() &&
                        transaction.craftingStatus == (int)CraftingOperationStatus.Succeeded) &&
                    committedEnvelope.profile.contractCounters.Any(counter =>
                        counter.contractId == "CT04" && counter.progress == 1) &&
                    committedEnvelope.profile.processedContractProgressEvents.Any(receipt =>
                        receipt.eventId == "tx/" + committedCraftId &&
                        receipt.progressEvent == (int)ContractProgressEvent.ItemCrafted);
                Assert.That(transactionAndContractReceiptShareCommit, Is.True,
                    "The inventory transaction, CT04 counter and stable event receipt must be in one profile envelope.");
                Assert.That(failingStore.LastFailedProfileJson, Is.EqualTo(failingStore.LastSuccessfulProfileJson),
                    "Retrying the v6 CT04 craft must commit the exact envelope that failed on its first write.");

                evidence.RecordInput(6, "TransactionReplay", "Submit the committed crafting TransactionId again");
                Task<CraftingCommitResult> replay = app.CraftActiveExpeditionAsync(HealthPotionRecipeId, committedCraftId);
                while (!replay.IsCompleted) yield return null;
                inventory = app.ActiveRun.Inventory;
                int successfulReceiptsAfterReplay = CountCraftReceipts(inventory.CreateSnapshot(),
                    CraftingOperationStatus.Succeeded);
                bool replayIsUnique = replay.Result.Persisted && replay.Result.Outcome != null &&
                    replay.Result.Outcome.Succeeded && inventory.QuickConsumable.Quantity == 3 &&
                    successfulReceiptsAfterReplay == 1 && app.ContractProgression.GetProgress("CT04") == 1 &&
                    app.Profile.processedContractProgressEvents.Count(receipt =>
                        receipt.eventId == "tx/" + committedCraftId) == 1;
                evidence.RecordAssertion("Replaying the same transaction returns success without a second potion",
                    replayIsUnique, "same success result; potion quantity stays 3; exactly one success receipt",
                    "persisted=" + replay.Result.Persisted + "; potions=" + inventory.QuickConsumable.Quantity +
                    "; successReceipts=" + successfulReceiptsAfterReplay);
                Assert.That(replayIsUnique, Is.True, replay.Result.Message);

                TransactionId rejectedCraftId = default;
                evidence.RecordInput(7, "UIButton", "Click the craft intent again after its materials were consumed");
                craftButton.onClick.Invoke();
                for (int frame = 0; frame < 120 && presenter.IsCrafting; frame++) yield return null;
                rejectedCraftId = presenter.LastCraftTransactionId;
                inventory = app.ActiveRun.Inventory;
                ExpeditionInventorySnapshot afterRejectionSnapshot = inventory.CreateSnapshot();
                bool rejectionPreservesGameState = presenter.CraftStatusText.Contains("RECHAZADO") &&
                    rejectedCraftId.IsValid && rejectedCraftId != committedCraftId &&
                    inventory.QuickConsumable != null && inventory.QuickConsumable.Quantity == 3 &&
                    inventory.GetMaterialCount(MaterialKind.UmbraHerb) == 0 &&
                    inventory.GetMaterialCount(MaterialKind.DarkResin) == 0 &&
                    app.ContractProgression.GetProgress("CT04") == 1 &&
                    CountCraftReceipts(afterRejectionSnapshot, CraftingOperationStatus.Succeeded) == 1 &&
                    CountCraftReceipts(afterRejectionSnapshot, CraftingOperationStatus.RejectedInsufficientMaterials) == 1;
                evidence.RecordAssertion("A second player intent is rejected without changing functional inventory",
                    rejectionPreservesGameState, "3 potions; no materials; one success and one rejection receipt",
                    "status=" + presenter.CraftStatusText + "; potions=" + inventory.QuickConsumable.Quantity +
                    "; herbs=" + inventory.GetMaterialCount(MaterialKind.UmbraHerb) +
                    "; resin=" + inventory.GetMaterialCount(MaterialKind.DarkResin));
                Assert.That(rejectionPreservesGameState, Is.True);

                var diagnosticRepository = new SaveRepository(backingStore);
                Task<SaveLoadResult<ProfileSaveEnvelope>> durableLoad = diagnosticRepository.LoadProfileAsync();
                while (!durableLoad.IsCompleted) yield return null;
                Assert.That(durableLoad.Result.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing),
                    "A fresh repository loading the active V6 bytes must expose the protected capability.");
                ProfileSaveEnvelope durableProfile = durableLoad.Result.Data;
                ExpeditionSaveData durableExpedition = durableProfile == null ? null : durableProfile.activeExpedition;
                ExpeditionInventorySnapshot durableSnapshot = durableExpedition == null ? null :
                    JsonUtility.FromJson<ExpeditionInventorySnapshot>(durableExpedition.inventorySnapshotJson);
                bool durableReceiptsUnique = durableProfile != null && durableProfile.hasActiveExpedition &&
                    CountCraftReceipts(durableSnapshot, CraftingOperationStatus.Succeeded) == 1 &&
                    CountCraftReceipts(durableSnapshot, CraftingOperationStatus.RejectedInsufficientMaterials) == 1 &&
                    durableExpedition.healthPotionCharges == 3;
                evidence.RecordAssertion("The durable active-room snapshot contains one success and one rejection receipt",
                    durableReceiptsUnique, "persisted potion count=3 and unique transaction results",
                    "hasRun=" + (durableProfile != null && durableProfile.hasActiveExpedition) +
                    "; potions=" + (durableExpedition == null ? -1 : durableExpedition.healthPotionCharges) +
                    "; successReceipts=" + CountCraftReceipts(durableSnapshot, CraftingOperationStatus.Succeeded) +
                    "; rejectionReceipts=" + CountCraftReceipts(durableSnapshot, CraftingOperationStatus.RejectedInsufficientMaterials));
                Assert.That(durableReceiptsUnique, Is.True);
                string durableProfileJson = diagnosticRepository.ExportLoadedProfileJson();
                Task<SaveStorageSnapshot> storedBeforeRestartTask = backingStore.ReadAsync(SaveDocument.Profile);
                while (!storedBeforeRestartTask.IsCompleted) yield return null;
                SaveStorageSnapshot storedBeforeRestart = storedBeforeRestartTask.Result;
                Assert.That(durableProfileJson, Is.EqualTo(storedBeforeRestart.PrimaryJson),
                    "The selected durable profile text must match the saved active snapshot exactly.");
                int profileWritesBeforeRestart = failingStore.ProfileWriteAttempts;

                evidence.RecordInput(8, "ProcessRestart", "Destroy app, load the active V6 profile read-only from the same isolated store");
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;
                app = ApplicationBootstrapper.Instance;
                ExpeditionSaveData restoredExpedition = app == null ? null : app.ActiveExpedition;
                const string legacyDiagnostic = "LegacyEncounterPinMissing";
                const string legacyMessage = "La expedición guardada se conserva, pero no puede continuarse porque falta la referencia de su catálogo de encuentros.";
                ExpeditionInventorySnapshot restoredSnapshot = restoredExpedition == null ? null :
                    JsonUtility.FromJson<ExpeditionInventorySnapshot>(restoredExpedition.inventorySnapshotJson);
                bool restartPreservesWrittenSnapshot = app != null && app.SaveDataReady &&
                    app.ProfileLoadStatus == SaveLoadStatus.LoadedPrimary &&
                    app.ProfileAccess == SaveDataAccess.ReadOnlyLegacyEncounterPinMissing && !app.CanWriteProfile &&
                    !app.CanStartNewExpedition && !app.CanContinueExpedition && app.HasActiveExpedition && app.ActiveRun == null &&
                    app.ActiveExpeditionLoadError == legacyDiagnostic && app.ProfileLoadMessage == legacyMessage &&
                    SceneManager.GetActiveScene().name == "Menu" && restoredExpedition != null && durableExpedition != null &&
                    JsonUtility.ToJson(restoredExpedition, false) == JsonUtility.ToJson(durableExpedition, false) &&
                    restoredExpedition.currentNodeId == roomEntry.currentNodeId &&
                    durableExpedition.roomEntryCheckpointJson == roomEntry.roomEntryCheckpointJson &&
                    restoredExpedition.roomEntryCheckpointJson == durableExpedition.roomEntryCheckpointJson &&
                    restoredExpedition.healthPotionCharges == 3 && restoredSnapshot != null &&
                    CountCraftReceipts(restoredSnapshot, CraftingOperationStatus.Succeeded) == 1 &&
                    CountCraftReceipts(restoredSnapshot, CraftingOperationStatus.RejectedInsufficientMaterials) == 1 &&
                    app.ContractProgression.GetProgress("CT04") == 1;
                evidence.RecordAssertion("Restart preserves the active V6 craft receipts and entry checkpoint without applying App rollback",
                    restartPreservesWrittenSnapshot, "Menu with no ActiveRun; 3 potions and both receipts remain in the stored snapshot",
                    restoredExpedition == null ? "saved snapshot missing" :
                    "scene=" + SceneManager.GetActiveScene().name + "; access=" + app.ProfileAccess +
                    "; run=" + (app.ActiveRun == null ? "none" : app.ActiveRun.RunId.ToString()) +
                    "; potions=" + restoredExpedition.healthPotionCharges +
                    "; successReceipts=" + CountCraftReceipts(restoredSnapshot, CraftingOperationStatus.Succeeded) +
                    "; rejectedReceipts=" + CountCraftReceipts(restoredSnapshot, CraftingOperationStatus.RejectedInsufficientMaterials));
                Assert.That(restartPreservesWrittenSnapshot, Is.True);

                var postRestartRepository = new SaveRepository(backingStore);
                Task<SaveLoadResult<ProfileSaveEnvelope>> postRestartLoad = postRestartRepository.LoadProfileAsync();
                while (!postRestartLoad.IsCompleted) yield return null;
                Assert.That(postRestartLoad.Result.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(postRestartRepository.ExportLoadedProfileJson(), Is.EqualTo(durableProfileJson));
                Task<SaveStorageSnapshot> storedAfterRestartTask = backingStore.ReadAsync(SaveDocument.Profile);
                while (!storedAfterRestartTask.IsCompleted) yield return null;
                SaveStorageSnapshot storedAfterRestart = storedAfterRestartTask.Result;
                Assert.That(storedAfterRestart.PrimaryJson, Is.EqualTo(storedBeforeRestart.PrimaryJson));
                Assert.That(storedAfterRestart.BackupJson, Is.EqualTo(storedBeforeRestart.BackupJson));
                Assert.That(failingStore.ProfileWriteAttempts, Is.EqualTo(profileWritesBeforeRestart),
                    "Loading the protected profile must not write, repair, or rotate the active snapshot.");

                string profileBeforeBlockedCalls = JsonUtility.ToJson(app.Profile, false);
                string expeditionBeforeBlockedCalls = JsonUtility.ToJson(app.ActiveExpedition, false);
                long revisionBeforeBlockedCalls = app.ActiveExpeditionRevision;
                int renownBeforeBlockedCalls = app.AvailableRenown;
                Task<SaveWriteResult> blockedStart = app.BeginExpeditionAsync(null);
                while (!blockedStart.IsCompleted) yield return null;
                Assert.That(blockedStart.Result.Succeeded, Is.False);
                Assert.Throws<InvalidOperationException>(() => app.ContinueActiveExpedition());
                Task<SaveWriteResult> blockedSnapshot = app.SaveActiveExpeditionStateAsync(app.ActiveExpedition);
                while (!blockedSnapshot.IsCompleted) yield return null;
                Task<CraftingCommitResult> blockedCraft = app.CraftActiveExpeditionAsync(
                    HealthPotionRecipeId, TransactionId.New());
                while (!blockedCraft.IsCompleted) yield return null;
                Task<CraftingCommitResult> blockedCraftRetry = app.RetryPendingCraftingAsync();
                while (!blockedCraftRetry.IsCompleted) yield return null;
                Task<SaveWriteResult> blockedWriteRetry = app.RetryPendingExpeditionWriteAsync();
                while (!blockedWriteRetry.IsCompleted) yield return null;
                Task<ExpeditionSettlementCommitResult> blockedSettlement = app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0);
                while (!blockedSettlement.IsCompleted) yield return null;
                Task<ExpeditionSettlementCommitResult> blockedSettlementRetry = app.RetryPendingSettlementAsync();
                while (!blockedSettlementRetry.IsCompleted) yield return null;
                Task<SaveStorageSnapshot> storedAfterCallsTask = backingStore.ReadAsync(SaveDocument.Profile);
                while (!storedAfterCallsTask.IsCompleted) yield return null;
                SaveStorageSnapshot storedAfterCalls = storedAfterCallsTask.Result;
                bool protectedApisDidNotMutate = !blockedSnapshot.Result.Succeeded &&
                    !blockedCraft.Result.Persisted && !blockedCraft.Result.Pending && blockedCraft.Result.Outcome == null &&
                    !blockedCraftRetry.Result.Persisted && !blockedCraftRetry.Result.Pending && blockedCraftRetry.Result.Outcome == null &&
                    !blockedWriteRetry.Result.Succeeded && blockedSettlement.Result.Receipt == null &&
                    !blockedSettlement.Result.Persisted && !blockedSettlement.Result.Pending &&
                    blockedSettlementRetry.Result.Receipt == null && !blockedSettlementRetry.Result.Persisted &&
                    !blockedSettlementRetry.Result.Pending &&
                    JsonUtility.ToJson(app.Profile, false) == profileBeforeBlockedCalls &&
                    JsonUtility.ToJson(app.ActiveExpedition, false) == expeditionBeforeBlockedCalls &&
                    app.ActiveExpeditionRevision == revisionBeforeBlockedCalls && app.AvailableRenown == renownBeforeBlockedCalls &&
                    app.HasActiveExpedition && app.ActiveRun == null && !app.HasPendingExpeditionWrite &&
                    !app.HasPendingCraftingWrite && !app.HasPendingSettlement &&
                    SceneManager.GetActiveScene().name == "Menu" &&
                    storedAfterCalls.PrimaryJson == storedBeforeRestart.PrimaryJson &&
                    storedAfterCalls.BackupJson == storedBeforeRestart.BackupJson &&
                    failingStore.ProfileWriteAttempts == profileWritesBeforeRestart;
                evidence.RecordAssertion("Protected crafting and settlement APIs reject without changing receipts, inventory, ledger, or stored bytes",
                    protectedApisDidNotMutate, "no outcome/receipt/payment; active snapshot and both profile files stay exact",
                    "start=" + blockedStart.Result.Succeeded + "; snapshot=" + blockedSnapshot.Result.Succeeded +
                    "; craft=" + blockedCraft.Result.Persisted + "; settlement=" + blockedSettlement.Result.Persisted +
                    "; writes=" + (failingStore.ProfileWriteAttempts - profileWritesBeforeRestart));
                Assert.That(protectedApisDidNotMutate, Is.True);

                Assert.That(evidence.Complete(), Is.Zero, "E02 crafting scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null)
                    UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                foreach (GameObject createdObject in createdObjects)
                    if (createdObject != null) UnityEngine.Object.Destroy(createdObject);
                Time.timeScale = 1f;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private static Button FindButton(string objectName)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(button => button != null && button.name == objectName);
        }

        private static void CreatePhysicalCollector(ExpeditionInventory inventory, ItemCatalog catalog,
            System.Collections.Generic.List<GameObject> createdObjects)
        {
            var player = new GameObject("E02CraftingPlayer");
            player.transform.position = IsolatedPickupPosition;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            player.AddComponent<BoxCollider2D>();
            ItemCollector2D collector = player.AddComponent<ItemCollector2D>();
            collector.Configure(inventory, catalog);
            createdObjects.Add(player);
        }

        private static WorldMaterialPickup2D CreateWorldMaterialPickup(string objectName, MaterialKind kind,
            int amount, Vector2 position, ItemCatalog catalog, System.Collections.Generic.List<GameObject> createdObjects)
        {
            var pickupObject = new GameObject(objectName);
            pickupObject.transform.position = position;
            Rigidbody2D body = pickupObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            WorldMaterialPickup2D pickup = pickupObject.AddComponent<WorldMaterialPickup2D>();
            pickup.Initialize(kind, amount, catalog);
            createdObjects.Add(pickupObject);
            return pickup;
        }

        private static ExpeditionInventorySnapshot ReadInventory(ExpeditionSaveData expedition)
        {
            Assert.That(expedition, Is.Not.Null);
            ExpeditionInventorySnapshot snapshot = JsonUtility.FromJson<ExpeditionInventorySnapshot>(expedition.inventorySnapshotJson);
            Assert.That(snapshot, Is.Not.Null);
            return snapshot;
        }

        private static int CountCraftReceipts(ExpeditionInventorySnapshot snapshot, CraftingOperationStatus status)
        {
            return snapshot == null || snapshot.transactions == null ? 0 : snapshot.transactions.Count(transaction => transaction != null &&
                transaction.craftingRecipeId == HealthPotionRecipeId && transaction.craftingStatus == (int)status);
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
            public string LastSuccessfulProfileJson { get; private set; }
            public string LastFailedProfileJson { get; private set; }
            public int ProfileWriteAttempts { get; private set; }
            public void FailNextProfileWrite() => _failNextProfileWrite = true;
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileWriteAttempts++;
                    if (_failNextProfileWrite)
                    {
                        _failNextProfileWrite = false;
                        LastFailedProfileJson = json;
                        throw new IOException("simulated profile storage failure");
                    }
                }
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
                if (document == SaveDocument.Profile)
                {
                    LastSuccessfulProfileJson = json;
                }
            }
        }
    }
}
