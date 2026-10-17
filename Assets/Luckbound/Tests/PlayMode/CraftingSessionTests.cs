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

                evidence.RecordInput(6, "TransactionReplay", "Submit the committed crafting TransactionId again");
                Task<CraftingCommitResult> replay = app.CraftActiveExpeditionAsync(HealthPotionRecipeId, committedCraftId);
                while (!replay.IsCompleted) yield return null;
                inventory = app.ActiveRun.Inventory;
                int successfulReceiptsAfterReplay = CountCraftReceipts(inventory.CreateSnapshot(),
                    CraftingOperationStatus.Succeeded);
                bool replayIsUnique = replay.Result.Persisted && replay.Result.Outcome != null &&
                    replay.Result.Outcome.Succeeded && inventory.QuickConsumable.Quantity == 3 &&
                    successfulReceiptsAfterReplay == 1 && app.ContractProgression.GetProgress("CT04") == 1;
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

                Task<SaveLoadResult<ProfileSaveEnvelope>> durableLoad = new SaveRepository(backingStore).LoadProfileAsync();
                while (!durableLoad.IsCompleted) yield return null;
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

                evidence.RecordInput(8, "ProcessRestart", "Destroy app, load Bootstrap from the same isolated profile");
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || ApplicationBootstrapper.Instance.ActiveRun == null ||
                    SceneManager.GetActiveScene().name != "Expedition"); frame++)
                    yield return null;
                app = ApplicationBootstrapper.Instance;
                ExpeditionInventory restoredInventory = app == null || app.ActiveRun == null ? null : app.ActiveRun.Inventory;
                ExpeditionSaveData restoredExpedition = app == null ? null : app.ActiveExpedition;
                ExpeditionInventorySnapshot restoredSnapshot = restoredInventory == null ? null : restoredInventory.CreateSnapshot();
                bool restartRestoresImmutableRoomEntry = app != null && app.SaveDataReady && restoredInventory != null &&
                    restoredExpedition != null && restoredExpedition.currentNodeId == roomEntry.currentNodeId &&
                    restoredInventory.QuickConsumable != null && restoredInventory.QuickConsumable.Quantity == 2 &&
                    restoredInventory.GetMaterialCount(MaterialKind.UmbraHerb) == 0 &&
                    restoredInventory.GetMaterialCount(MaterialKind.DarkResin) == 0 &&
                    app.ContractProgression.GetProgress("CT04") == 1 &&
                    !restoredSnapshot.transactions.Any(item => item.transactionId == committedCraftId.ToString()) &&
                    !restoredSnapshot.transactions.Any(item => item.transactionId == rejectedCraftId.ToString());
                evidence.RecordAssertion("Restart restores the immutable entry checkpoint for this unfinished room",
                    restartRestoresImmutableRoomEntry, "entry inventory has 2 potions, no gathered materials or in-room craft receipts",
                    restoredInventory == null ? "active run was not restored" :
                    "potions=" + restoredInventory.QuickConsumable?.Quantity +
                    "; herbs=" + restoredInventory.GetMaterialCount(MaterialKind.UmbraHerb) +
                    "; resin=" + restoredInventory.GetMaterialCount(MaterialKind.DarkResin));
                Assert.That(restartRestoresImmutableRoomEntry, Is.True);

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
