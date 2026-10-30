using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ApplicationFortuneRewardTests
    {
        private static readonly string[] RelicRewardOptions =
        {
            "relic.marked_coin", "relic.hidden_pouch", "consumable.energy_tonic"
        };

        [UnityTest]
        public IEnumerator E04_RewardCommitRetryKeepsLegacySnapshotReadOnlyAfterReload()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app, FillBackpack: false);

                ExpeditionMutationCommitResult<FortuneOfferResult> created = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.reward", "context.reward", RelicRewardOptions),
                    value => created = value);
                Assert.That(created.Persisted, Is.True, created.Message);
                string[] originalOptions = created.Outcome.Offer.OptionIds.ToArray();

                ExpeditionMutationCommitResult<FortuneChoiceResult> choice = null;
                yield return Complete(app.ChooseFortuneOptionAsync("offer.reward", "relic.marked_coin"),
                    value => choice = value);
                Assert.That(choice.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));

                string inventoryBeforeClaim = app.ActiveExpedition.inventorySnapshotJson;
                string checkpointBeforeClaim = app.ActiveExpedition.roomEntryCheckpointJson;
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> failed = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reward"), value => failed = value);

                Assert.That(failed.Persisted, Is.False);
                Assert.That(failed.Pending, Is.True);
                Assert.That(failed.Outcome.Succeeded, Is.True);
                Assert.That(app.GetFortuneOffer("offer.reward").IsClaimed, Is.False,
                    "The failed save must not publish the claim marker.");
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.Zero,
                    "The failed save must not publish the reward item.");
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBeforeClaim));
                Assert.That(app.ActiveExpedition.roomEntryCheckpointJson, Is.EqualTo(checkpointBeforeClaim));

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> retried = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reward"), value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.Pending, Is.False);
                Assert.That(retried.Outcome.Succeeded, Is.True);
                Assert.That(retried.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(app.GetFortuneOffer("offer.reward").IsClaimed, Is.True);
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.EqualTo(1));

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> replayedInReadWriteSession = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reward"),
                    value => replayedInReadWriteSession = value);
                Assert.That(replayedInReadWriteSession.Persisted, Is.True, replayedInReadWriteSession.Message);
                Assert.That(replayedInReadWriteSession.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.EqualTo(1));

                string committedExpeditionJson = JsonUtility.ToJson(app.ActiveExpedition, false);
                SaveStorageSnapshot committedStorage = null;
                yield return Complete(store.ReadAsync(SaveDocument.Profile), value => committedStorage = value);
                string committedProfileJson = committedStorage.PrimaryJson;

                DestroyApplication();
                yield return null;
                yield return StartApplication(value => app = value, "Menu");
                Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.CanWriteProfile, Is.False);
                Assert.That(app.CanStartNewExpedition, Is.False);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(app.ActiveExpeditionLoadError, Is.EqualTo("LegacyEncounterPinMissing"));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(committedExpeditionJson));

                var readOnlyRepository = new SaveRepository(store);
                SaveLoadResult<ProfileSaveEnvelope> loaded = null;
                yield return Complete(readOnlyRepository.LoadProfileAsync(), value => loaded = value);
                Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
                Assert.That(loaded.HasUsableData, Is.True);
                Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(readOnlyRepository.ExportLoadedProfileJson(), Is.EqualTo(committedProfileJson));
                ProfileSaveEnvelope durableEnvelope = loaded.Data;
                Assert.That(durableEnvelope.hasActiveExpedition, Is.True);
                Assert.That(durableEnvelope.activeExpedition.inventorySnapshotJson, Does.Contain("relic.marked_coin"),
                    "The durable write contains the reward item and claim together with the D09 checkpoint.");
                FortuneOfferBook durableBook = FortuneOfferBook.Restore(
                    durableEnvelope.activeExpedition.fortuneOfferBookSnapshot);
                Assert.That(durableBook.GetOffer("offer.reward").IsClaimed, Is.True);
                Assert.That(durableBook.GetOffer("offer.reward").OptionIds, Is.EqualTo(originalOptions));
                Assert.That(durableEnvelope.activeExpedition.roomEntryCheckpointJson, Is.EqualTo(checkpointBeforeClaim),
                    "The atomic reward write preserves the D09 room-entry checkpoint.");

                ExpeditionInventorySnapshot savedInventory = JsonUtility.FromJson<ExpeditionInventorySnapshot>(
                    durableEnvelope.activeExpedition.inventorySnapshotJson);
                ItemCatalog catalog = ItemCatalogJsonLoader.Load(
                    Resources.Load<TextAsset>("Luckbound/Data/Items/Items").text);
                ExpeditionInventory restoredInventory = ExpeditionInventory.Restore(catalog, savedInventory);
                InventoryStackSnapshot savedReward = savedInventory.backpackSlots.Single(item =>
                    item != null && item.itemId == "relic.marked_coin");
                InventoryTransactionSnapshot rewardReceipt = savedInventory.transactions.Single(transaction =>
                    transaction.itemId == "relic.marked_coin" &&
                    transaction.signature.StartsWith("collect|", StringComparison.Ordinal));
                var replayedStack = new InventoryItemStack(ItemInstanceId.Parse(savedReward.instanceId),
                    savedReward.itemId, (ItemRarity)savedReward.rarity, savedReward.upgradeLevel, savedReward.quantity);
                InventoryOperationOutcome replayedReceipt = restoredInventory.TryCollectItem(replayedStack,
                    TransactionId.Parse(rewardReceipt.transactionId));
                Assert.That(replayedReceipt.Succeeded, Is.True,
                    "The same persisted transaction receipt replays successfully against the inventory snapshot.");
                Assert.That(restoredInventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.EqualTo(1));

                ExpeditionInventorySnapshot appSavedInventory = JsonUtility.FromJson<ExpeditionInventorySnapshot>(
                    app.ActiveExpedition.inventorySnapshotJson);
                Assert.That(appSavedInventory.backpackSlots.Count(item => item?.itemId == "relic.marked_coin"),
                    Is.EqualTo(1));
                Assert.Throws<InvalidOperationException>(() => app.ContinueActiveExpedition());
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
                string profileDataBeforeBlockedClaim = JsonUtility.ToJson(app.Profile, false);
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> blockedClaim = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reward"), value => blockedClaim = value);
                Assert.That(blockedClaim.Persisted, Is.False);
                Assert.That(blockedClaim.Pending, Is.False);
                Assert.That(blockedClaim.Outcome, Is.Null,
                    "The protected App does not report an already-applied claim as a successful payout.");
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
                Assert.That(app.PendingProfileExport, Is.Null);
                Assert.That(store.FailNextProfileWrite, Is.True,
                    "A protected claim must not call the profile store.");
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileDataBeforeBlockedClaim));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(committedExpeditionJson));
                Assert.That(readOnlyRepository.ExportLoadedProfileJson(), Is.EqualTo(committedProfileJson));
                SaveStorageSnapshot afterBlockedClaimStorage = null;
                yield return Complete(store.ReadAsync(SaveDocument.Profile), value => afterBlockedClaimStorage = value);
                Assert.That(afterBlockedClaimStorage.PrimaryJson, Is.EqualTo(committedStorage.PrimaryJson));
                Assert.That(afterBlockedClaimStorage.BackupJson, Is.EqualTo(committedStorage.BackupJson));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator E04_ClaimRewardReplayIsIdempotentBeforeReloadAndBlockedAfterReload()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app, FillBackpack: false);

                string completedNodeId = app.ActiveExpedition.currentNodeId;
                SaveWriteResult completedRoom = null;
                yield return Complete(app.CompleteCurrentRoomAsync("result.e04.replay.completed-room",
                    "{\"outcome\":\"cleared\",\"renown\":0}"),
                    value => completedRoom = value);
                Assert.That(completedRoom.Succeeded, Is.True, completedRoom.Message);
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(completedNodeId));
                Assert.That(app.ActiveExpedition.completedRoomIds, Does.Contain(completedNodeId));
                Assert.That(app.ActiveExpedition.roomEntryCheckpointJson, Is.Empty,
                    "A committed room result clears its checkpoint, so D09 has no in-room state to roll back.");

                ExpeditionMutationCommitResult<FortuneOfferResult> created = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.reload", "context.reload", RelicRewardOptions),
                    value => created = value);
                Assert.That(created.Persisted, Is.True, created.Message);
                ExpeditionMutationCommitResult<FortuneChoiceResult> chosen = null;
                yield return Complete(app.ChooseFortuneOptionAsync("offer.reload", "relic.marked_coin"),
                    value => chosen = value);
                Assert.That(chosen.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> claimed = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reload"), value => claimed = value);
                Assert.That(claimed.Persisted, Is.True, claimed.Message);
                Assert.That(claimed.Outcome.Succeeded, Is.True);
                Assert.That(claimed.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(app.GetFortuneOffer("offer.reload").IsClaimed, Is.True);
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"),
                    Is.EqualTo(1));

                string committedExpeditionJson = JsonUtility.ToJson(app.ActiveExpedition, false);
                string committedProfileJson = store.ProfilePrimary;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> replayedBeforeReload = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reload"),
                    value => replayedBeforeReload = value);
                Assert.That(replayedBeforeReload.Persisted, Is.True, replayedBeforeReload.Message);
                Assert.That(replayedBeforeReload.Pending, Is.False);
                Assert.That(replayedBeforeReload.Outcome.Succeeded, Is.True);
                Assert.That(replayedBeforeReload.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
                Assert.That(replayedBeforeReload.Outcome.Reward.ItemId, Is.EqualTo("relic.marked_coin"));
                Assert.That(replayedBeforeReload.Outcome.InventoryOperation.Status,
                    Is.EqualTo(InventoryOperationStatus.Succeeded));
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"),
                    Is.EqualTo(1), "The same-session replay does not create a second item.");
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(committedExpeditionJson));
                Assert.That(store.ProfilePrimary, Is.EqualTo(committedProfileJson),
                    "The already-claimed result leaves the saved expedition unchanged.");

                SaveStorageSnapshot committedStorage = null;
                yield return Complete(store.ReadAsync(SaveDocument.Profile), value => committedStorage = value);
                committedProfileJson = committedStorage.PrimaryJson;
                ApplicationBootstrapper previousApplication = app;

                DestroyApplication();
                yield return null;
                app = null;
                yield return StartApplication(value => app = value, "Menu");

                Assert.That(object.ReferenceEquals(app, previousApplication), Is.False,
                    "The protected profile is loaded by a reconstructed application and repository.");
                Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.CanWriteProfile, Is.False);
                Assert.That(app.CanStartNewExpedition, Is.False);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(app.ActiveExpeditionLoadError, Is.EqualTo("LegacyEncounterPinMissing"));
                Assert.That(store.ProfilePrimary, Is.EqualTo(committedProfileJson));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(committedExpeditionJson));
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(completedNodeId));
                Assert.That(app.ActiveExpedition.completedRoomIds, Does.Contain(completedNodeId));
                Assert.That(app.ActiveExpedition.roomEntryCheckpointJson, Is.Empty);

                var readOnlyRepository = new SaveRepository(store);
                SaveLoadResult<ProfileSaveEnvelope> loaded = null;
                yield return Complete(readOnlyRepository.LoadProfileAsync(), value => loaded = value);
                Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
                Assert.That(loaded.HasUsableData, Is.True);
                Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(readOnlyRepository.ExportLoadedProfileJson(), Is.EqualTo(committedProfileJson));
                FortuneOfferBook durableBook = FortuneOfferBook.Restore(
                    loaded.Data.activeExpedition.fortuneOfferBookSnapshot);
                Assert.That(durableBook.GetOffer("offer.reload").IsClaimed, Is.True);
                Assert.That(durableBook.GetOffer("offer.reload").OptionIds, Is.EqualTo(created.Outcome.Offer.OptionIds));
                Assert.That(loaded.Data.activeExpedition.inventorySnapshotJson,
                    Is.EqualTo(app.ActiveExpedition.inventorySnapshotJson));
                ExpeditionInventorySnapshot savedInventory = JsonUtility.FromJson<ExpeditionInventorySnapshot>(
                    loaded.Data.activeExpedition.inventorySnapshotJson);
                Assert.That(savedInventory.backpackSlots.Count(item => item?.itemId == "relic.marked_coin"),
                    Is.EqualTo(1));

                Assert.Throws<InvalidOperationException>(() => app.ContinueActiveExpedition());
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
                string profileDataBeforeBlockedClaim = JsonUtility.ToJson(app.Profile, false);
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> blockedClaim = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.reload"), value => blockedClaim = value);
                Assert.That(blockedClaim.Persisted, Is.False);
                Assert.That(blockedClaim.Pending, Is.False);
                Assert.That(blockedClaim.Outcome, Is.Null,
                    "The protected profile rejects the replay before it can report AlreadyApplied or pay again.");
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
                Assert.That(app.PendingProfileExport, Is.Null);
                Assert.That(store.FailNextProfileWrite, Is.True,
                    "A protected claim must not call the profile store.");
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileDataBeforeBlockedClaim));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(committedExpeditionJson));
                Assert.That(readOnlyRepository.ExportLoadedProfileJson(), Is.EqualTo(committedProfileJson));
                SaveStorageSnapshot afterBlockedClaimStorage = null;
                yield return Complete(store.ReadAsync(SaveDocument.Profile), value => afterBlockedClaimStorage = value);
                Assert.That(afterBlockedClaimStorage.PrimaryJson, Is.EqualTo(committedStorage.PrimaryJson));
                Assert.That(afterBlockedClaimStorage.BackupJson, Is.EqualTo(committedStorage.BackupJson));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator E04_FullBackpackInvalidChoiceAndIncompatibleWeaponLeaveClaimAndInventoryUntouched()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app, FillBackpack: true);
                string inventoryBefore = app.ActiveExpedition.inventorySnapshotJson;
                int goldBefore = app.ActiveRun.Inventory.Gold;

                ExpeditionMutationCommitResult<FortuneOfferResult> fullOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.full", "context.full", RelicRewardOptions),
                    value => fullOffer = value);
                ExpeditionMutationCommitResult<FortuneChoiceResult> fullChoice = null;
                yield return Complete(app.ChooseFortuneOptionAsync("offer.full", "relic.hidden_pouch"),
                    value => fullChoice = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> fullReward = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.full"), value => fullReward = value);
                Assert.That(fullReward.Outcome.InventoryOperation.Status, Is.EqualTo(InventoryOperationStatus.RejectedFull));
                Assert.That(fullReward.Outcome.Claim, Is.Null);
                Assert.That(app.GetFortuneOffer("offer.full").IsClaimed, Is.False);
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(goldBefore));

                ExpeditionMutationCommitResult<FortuneOfferResult> invalidOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.invalid", "context.invalid", RelicRewardOptions),
                    value => invalidOffer = value);
                ExpeditionMutationCommitResult<FortuneChoiceResult> invalidChoice = null;
                yield return Complete(app.ChooseFortuneOptionAsync("offer.invalid", "relic.not_in_offer"),
                    value => invalidChoice = value);
                Assert.That(invalidChoice.Outcome.Status, Is.EqualTo(FortuneOperationStatus.RejectedInvalidChoice));
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> invalidReward = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.invalid"), value => invalidReward = value);
                Assert.That(invalidReward.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.RejectedNoChoice));
                Assert.That(app.GetFortuneOffer("offer.invalid").IsClaimed, Is.False);
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));

                string[] incompatibleOptions =
                {
                    "weapon.venator.short_bow", "relic.marked_coin", "relic.hidden_pouch"
                };
                ExpeditionMutationCommitResult<FortuneOfferResult> incompatibleOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.incompatible", "context.incompatible", incompatibleOptions),
                    value => incompatibleOffer = value);
                ExpeditionMutationCommitResult<FortuneChoiceResult> incompatibleChoice = null;
                yield return Complete(app.ChooseFortuneOptionAsync("offer.incompatible", "weapon.venator.short_bow"),
                    value => incompatibleChoice = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> incompatibleReward = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.incompatible"),
                    value => incompatibleReward = value);
                Assert.That(incompatibleReward.Outcome.InventoryOperation.Status,
                    Is.EqualTo(InventoryOperationStatus.RejectedIncompatibleClass));
                Assert.That(incompatibleReward.Outcome.Claim, Is.Null);
                Assert.That(app.GetFortuneOffer("offer.incompatible").IsClaimed, Is.False);
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(goldBefore));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator E04_ApplicationCompletesAndDeduplicatesAClaimCommittedBeforeRewardDelivery()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app, FillBackpack: false);

                ExpeditionMutationCommitResult<FortuneOfferResult> created = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.replay", "context.replay", RelicRewardOptions),
                    value => created = value);
                ExpeditionMutationCommitResult<FortuneChoiceResult> chosen = null;
                yield return Complete(app.ChooseFortuneOptionAsync("offer.replay", "consumable.energy_tonic"),
                    value => chosen = value);
                ExpeditionMutationCommitResult<FortuneClaimResult> claimOnly = null;
                yield return Complete(app.ClaimFortuneOfferAsync("offer.replay"), value => claimOnly = value);
                Assert.That(claimOnly.Persisted, Is.True, claimOnly.Message);
                Assert.That(app.GetFortuneOffer("offer.replay").IsClaimed, Is.True);
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "consumable.energy_tonic"), Is.Zero);

                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> interrupted = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.replay"), value => interrupted = value);
                Assert.That(interrupted.Persisted, Is.False);
                Assert.That(interrupted.Pending, Is.True);
                Assert.That(interrupted.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "consumable.energy_tonic"), Is.Zero);

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> retried = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.replay"), value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.Outcome.Succeeded, Is.True);
                Assert.That(retried.Outcome.Claim.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "consumable.energy_tonic"), Is.EqualTo(1));

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> repeated = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("offer.replay"), value => repeated = value);
                Assert.That(repeated.Outcome.Succeeded, Is.True);
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "consumable.energy_tonic"), Is.EqualTo(1));
            }
            finally
            {
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        private static IEnumerator BeginExpedition(ApplicationBootstrapper app, bool FillBackpack)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            if (FillBackpack)
            {
                for (int index = 0; index < ExpeditionInventory.BackpackCapacity - 1; index++)
                {
                    var item = new InventoryItemStack(ItemInstanceId.New(), "relic.marked_coin", ItemRarity.Common, 0, 1);
                    Assert.That(run.Inventory.TryCollectItem(item, TransactionId.New()).Succeeded, Is.True);
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
