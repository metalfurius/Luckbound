using System;
using System.Collections;
using System.IO;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ApplicationFortuneBrewTests
    {
        [UnityTest]
        public IEnumerator SuccessConsumesOneFromAStackAndReplayAfterRestartDoesNotRepeat()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> noRun = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(ItemInstanceId.New(), TransactionId.New()),
                    value => noRun = value);
                Assert.That(noRun.Persisted, Is.False);
                Assert.That(noRun.Outcome, Is.Null,
                    "The operation requires an active resumable expedition.");

                ItemInstanceId brewInstanceId = default;
                yield return BeginExpedition(app, quickBrewQuantity: 2,
                    configuredItem => brewInstanceId = configuredItem);
                TransactionId transactionId = TransactionId.New();
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> committed = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => committed = value);
                Assert.That(committed.Persisted, Is.True, committed.Message);
                Assert.That(committed.Pending, Is.False);
                Assert.That(committed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(committed.Outcome.InventoryOperation.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.InstanceId, Is.EqualTo(brewInstanceId));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.healthPotionCharges, Is.Zero,
                    "A remaining Fortune Brew is not a health potion charge.");
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore + 1));

                // Complete the room before restarting so D09's immutable entry checkpoint has
                // been retired and the committed candidate is the normal resume state.
                SaveWriteResult completedRoom = null;
                yield return Complete(app.CompleteCurrentRoomAsync("result.fortune-brew.committed",
                    "{\"outcome\":\"cleared\",\"renown\":0}"), value => completedRoom = value);
                Assert.That(completedRoom.Succeeded, Is.True, completedRoom.Message);
                Assert.That(app.ActiveExpedition.roomEntryCheckpointJson, Is.Empty);
                string committedInventoryJson = app.ActiveExpedition.inventorySnapshotJson;
                string committedProfileJson = store.ProfilePrimary;

                DestroyApplication();
                yield return null;
                app = null;
                yield return StartApplication(value => app = value, "Expedition");
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.InstanceId, Is.EqualTo(brewInstanceId));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore + 1));
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(committedInventoryJson));

                SaveLoadResult<ProfileSaveEnvelope> loaded = null;
                yield return Complete(new SaveRepository(store).LoadProfileAsync(), value => loaded = value);
                Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
                Assert.That(loaded.Data.activeExpedition.fortuneOfferBookSnapshot.fortuneBrewReceipts.Length,
                    Is.EqualTo(1), "The clone and saved document retain the durable brew receipt.");
                Assert.That(loaded.Data.activeExpedition.healthPotionCharges, Is.Zero,
                    "The durable potion counter must remain zero after a restart with a brew equipped.");

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> replayed = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => replayed = value);
                Assert.That(replayed.Persisted, Is.True, replayed.Message);
                Assert.That(replayed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore + 1));
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(committedInventoryJson));
                Assert.That(store.ProfilePrimary, Is.EqualTo(committedProfileJson),
                    "An already committed transaction replay performs no second write.");
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator CapacityRejectsWithoutConsumingAndKeepsTheRejectionStable()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                ItemInstanceId brewInstanceId = default;
                yield return BeginExpedition(app, quickBrewQuantity: 2,
                    configuredItem => brewInstanceId = configuredItem);

                ExpeditionMutationCommitResult<FortuneChargeResult> guardianGrant = null;
                yield return Complete(app.GrantGuardianFortuneChargeAsync("guardian.fortune-brew-capacity"),
                    value => guardianGrant = value);
                Assert.That(guardianGrant.Persisted, Is.True, guardianGrant.Message);
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges,
                    Is.EqualTo(FortuneOfferBook.MaximumCharges));

                string inventoryBefore = app.ActiveExpedition.inventorySnapshotJson;
                int quantityBefore = app.ActiveRun.Inventory.QuickConsumable.Quantity;
                TransactionId transactionId = TransactionId.New();
                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> rejected = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => rejected = value);

                Assert.That(rejected.Persisted, Is.True, rejected.Message);
                Assert.That(rejected.Outcome.Status, Is.EqualTo(FortuneOperationStatus.RejectedAtCapacity));
                Assert.That(rejected.Outcome.InventoryOperation, Is.Null,
                    "A full Fortune balance rejects before inventory consumption.");
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(quantityBefore));
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges,
                    Is.EqualTo(FortuneOfferBook.MaximumCharges));

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> replayed = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => replayed = value);
                Assert.That(replayed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.RejectedAtCapacity));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(quantityBefore));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges,
                    Is.EqualTo(FortuneOfferBook.MaximumCharges));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator WrongOrMissingQuickItemLeavesInventoryAndFortuneUnchanged()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                ItemInstanceId brewInBackpack = default;
                yield return BeginExpedition(app, quickBrewQuantity: 0,
                    configuredItem => brewInBackpack = configuredItem);

                ItemInstanceId wrongQuickInstanceId = app.ActiveRun.Inventory.QuickConsumable.InstanceId;
                string inventoryBefore = app.ActiveExpedition.inventorySnapshotJson;
                string bookBefore = JsonUtility.ToJson(app.ActiveExpedition.fortuneOfferBookSnapshot, false);
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> wrongKind = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(wrongQuickInstanceId, TransactionId.New()),
                    value => wrongKind = value);
                Assert.That(wrongKind.Persisted, Is.True, wrongKind.Message);
                Assert.That(wrongKind.Outcome.Status, Is.EqualTo(FortuneOperationStatus.RejectedInvalidRequest));

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> notQuickSlot = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInBackpack, TransactionId.New()),
                    value => notQuickSlot = value);
                Assert.That(notQuickSlot.Persisted, Is.True, notQuickSlot.Message);
                Assert.That(notQuickSlot.Outcome.Status, Is.EqualTo(FortuneOperationStatus.RejectedInvalidRequest));

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> missingItem = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(ItemInstanceId.New(), TransactionId.New()),
                    value => missingItem = value);
                Assert.That(missingItem.Persisted, Is.True, missingItem.Message);
                Assert.That(missingItem.Outcome.Status, Is.EqualTo(FortuneOperationStatus.RejectedInvalidRequest));

                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition.fortuneOfferBookSnapshot, false), Is.EqualTo(bookBefore));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator FailedSaveDoesNotPublishAndOnlyExactRetryCommitsTheCandidate()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                ItemInstanceId brewInstanceId = default;
                yield return BeginExpedition(app, quickBrewQuantity: 2,
                    configuredItem => brewInstanceId = configuredItem);
                string inventoryBefore = app.ActiveExpedition.inventorySnapshotJson;
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;
                TransactionId transactionId = TransactionId.New();
                store.FailNextProfileWrite = true;

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> failed = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => failed = value);
                Assert.That(failed.Persisted, Is.False);
                Assert.That(failed.Pending, Is.True);
                Assert.That(failed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(app.HasPendingFortuneMutation, Is.True);
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore));

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> differentRetry = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, TransactionId.New()),
                    value => differentRetry = value);
                Assert.That(differentRetry.Persisted, Is.False);
                Assert.That(differentRetry.Pending, Is.True);
                Assert.That(differentRetry.Outcome, Is.Null);
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> retried = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.Pending, Is.False);
                Assert.That(retried.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore + 1));
                Assert.That(app.HasPendingFortuneMutation, Is.False);
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator RuntimeAdapterCarriesPendingAndCommitsOnlyTheExactRetry()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                ItemInstanceId brewInstanceId = default;
                yield return BeginExpedition(app, quickBrewQuantity: 2,
                    configuredItem => brewInstanceId = configuredItem);

                IPlayerFortuneBrewRuntime runtime = app;
                TransactionId transactionId = TransactionId.New();
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;
                store.FailNextProfileWrite = true;

                PlayerFortuneBrewCommitOutcome failed = null;
                yield return Complete(runtime.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => failed = value);
                Assert.That(failed.Persisted, Is.False);
                Assert.That(failed.Pending, Is.True,
                    "The Gameplay adapter must preserve App's exact-retry state.");
                Assert.That(failed.CommittedInventory, Is.Null);
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore));

                PlayerFortuneBrewCommitOutcome retried = null;
                yield return Complete(runtime.ConsumeFortuneBrewAsync(brewInstanceId, transactionId),
                    value => retried = value);
                Assert.That(retried.Persisted, Is.True);
                Assert.That(retried.Pending, Is.False);
                Assert.That(retried.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(retried.CommittedInventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore + 1));
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator InterruptedRoomResumeRestoresTheD09EntryBrewAndChargeSnapshot()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                ItemInstanceId brewInstanceId = default;
                yield return BeginExpedition(app, quickBrewQuantity: 2,
                    configuredItem => brewInstanceId = configuredItem);
                string entryInventory = app.ActiveExpedition.inventorySnapshotJson;
                string entryCheckpoint = app.ActiveExpedition.roomEntryCheckpointJson;
                int entryCharges = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> used = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(brewInstanceId, TransactionId.New()),
                    value => used = value);
                Assert.That(used.Persisted, Is.True, used.Message);
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(entryCharges + 1));
                Assert.That(app.ActiveExpedition.roomEntryCheckpointJson, Is.EqualTo(entryCheckpoint));

                DestroyApplication();
                yield return null;
                app = null;
                yield return StartApplication(value => app = value, "Expedition");
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.InstanceId, Is.EqualTo(brewInstanceId));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(entryInventory));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(entryCharges));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        private static IEnumerator BeginExpedition(
            ApplicationBootstrapper app, int quickBrewQuantity, Action<ItemInstanceId> configuredItem)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });

            if (quickBrewQuantity >= 0)
            {
                var brew = new InventoryItemStack(ItemInstanceId.New(), "consumable.fortune_brew",
                    ItemRarity.Common, 0, quickBrewQuantity == 0 ? 1 : quickBrewQuantity);
                InventoryOperationOutcome collected = run.Inventory.TryCollectItem(brew, TransactionId.New());
                Assert.That(collected.Succeeded, Is.True, collected.Status.ToString());
                configuredItem(brew.InstanceId);

                if (quickBrewQuantity > 0)
                {
                    int backpackIndex = -1;
                    for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                    {
                        if (run.Inventory.GetBackpackSlot(index)?.InstanceId == brew.InstanceId)
                        {
                            backpackIndex = index;
                            break;
                        }
                    }
                    Assert.That(backpackIndex, Is.GreaterThanOrEqualTo(0));
                    InventoryOperationOutcome setQuick = run.Inventory.SetQuickConsumable(backpackIndex, TransactionId.New());
                    Assert.That(setQuick.Succeeded, Is.True, setQuick.Status.ToString());
                }
            }

            SaveWriteResult result = null;
            yield return Complete(app.BeginExpeditionAsync(run), value => result = value);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static IEnumerator StartApplication(Action<ApplicationBootstrapper> ready, string expectedScene = "Menu")
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != expectedScene); frame++)
                yield return null;

            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(expectedScene));
            ready(ApplicationBootstrapper.Instance);
        }

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private static void DestroyApplication()
        {
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
        }

        private sealed class MemorySaveStore : ISaveStore
        {
            private string _profilePrimary;
            private string _profileBackup;
            private string _settingsPrimary;
            private string _settingsBackup;

            public bool FailNextProfileWrite { get; set; }
            public string ProfilePrimary => _profilePrimary;

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(_profilePrimary, _profileBackup)
                    : new SaveStorageSnapshot(_settingsPrimary, _settingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile && FailNextProfileWrite)
                {
                    FailNextProfileWrite = false;
                    throw new IOException("simulated profile write failure");
                }

                if (document == SaveDocument.Profile)
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _profileBackup = lastKnownGoodPrimaryJson;
                    _profilePrimary = json;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _settingsBackup = lastKnownGoodPrimaryJson;
                    _settingsPrimary = json;
                }
                return Task.CompletedTask;
            }
        }
    }
}
