using System;
using System.Threading.Tasks;
using Luckbound.Core.Consumables;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Inventory;
using UnityEngine;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper : IPlayerFortuneBrewRuntime
    {
        private const string FortuneBrewItemId = "consumable.fortune_brew";

        /// <summary>
        /// Consumes the exact quick-slot Fortune Brew and commits its charge with the inventory
        /// snapshot. The live run is published only after the complete expedition candidate saves.
        /// </summary>
        public async Task<ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>> ConsumeFortuneBrewAsync(
            ItemInstanceId itemInstanceId, TransactionId transactionId)
        {
            string operationKey = CreateFortuneOperationKey("consumable.fortune-brew",
                itemInstanceId.IsValid ? itemInstanceId.ToString() : string.Empty,
                transactionId.IsValid ? transactionId.ToString() : string.Empty);
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingFortuneMutation != null)
                {
                    if (_pendingExpeditionCandidate == null)
                    {
                        // A generic retry may have persisted the exact candidate without returning
                        // the Fortune result. The durable book receipt handles the subsequent replay.
                        _pendingFortuneMutation = null;
                    }
                    else
                    {
                        if (!string.Equals(_pendingFortuneMutation.OperationKey, operationKey, StringComparison.Ordinal))
                            return BrewCommitFailure("A different Fortune mutation is waiting for its exact save retry.", pending: true);
                        if (!(_pendingFortuneMutation.Outcome is FortuneBrewApplicationOutcome pendingOutcome))
                            return BrewCommitFailure("The pending Fortune mutation has a different result type.", pending: true);
                        return await RetryPendingFortuneMutationUnderGateAsync(pendingOutcome);
                    }
                }

                if (HasPendingExpeditionWrite || HasPendingEconomyMutation || HasPendingCraftingWrite ||
                    HasPendingPermanentUpgradePurchase || HasPendingContractProgression || HasPendingSettlement)
                    return BrewCommitFailure("Retry the pending save or profile transaction before using a Fortune Brew.", pending: true);
                if (!CanContinueExpedition || ActiveRun == null || _activeExpedition == null ||
                    _activeExpedition.fortuneOfferBookSnapshot == null)
                    return BrewCommitFailure("There is no safely resumable active expedition with a Fortuna offer book.");

                int committedCharges = _activeExpedition.fortuneOfferBookSnapshot.charges;
                if (!transactionId.IsValid)
                {
                    var invalidTransaction = RejectedBrewOutcome(itemInstanceId, committedCharges, ActiveRun.Inventory);
                    return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                        invalidTransaction, persisted: true, pending: false,
                        "A valid transaction ID is required; the expedition was unchanged.");
                }

                FortuneOfferBook book = FortuneOfferBook.Restore(_activeExpedition.fortuneOfferBookSnapshot);
                if (book.TryGetFortuneBrewTransaction(transactionId, itemInstanceId,
                    out FortuneChargeResult previousBrewResult))
                {
                    var replay = new FortuneBrewApplicationOutcome(
                        itemInstanceId, previousBrewResult.Status, previousBrewResult.CurrentCharges,
                        ActiveRun.Inventory, null);
                    return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                        replay, persisted: true, pending: false,
                        "The Fortune Brew transaction is already represented by the committed expedition state.");
                }

                if (!itemInstanceId.IsValid)
                {
                    var invalidIdentity = RejectedBrewOutcome(itemInstanceId, committedCharges, ActiveRun.Inventory);
                    return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                        invalidIdentity, persisted: true, pending: false,
                        "A valid quick-consumable instance is required; the expedition was unchanged.");
                }

                ExpeditionInventory candidateInventory = ExpeditionInventory.Restore(
                    ActiveRun.Inventory.Catalog, ActiveRun.Inventory.CreateSnapshot());
                InventoryItemStack quickConsumable = candidateInventory.QuickConsumable;
                if (quickConsumable == null || quickConsumable.InstanceId != itemInstanceId ||
                    !string.Equals(quickConsumable.ItemId, FortuneBrewItemId, StringComparison.Ordinal) ||
                    !candidateInventory.Catalog.TryGet(quickConsumable.ItemId, out ItemDefinition definition) ||
                    definition.Kind != ItemKind.Consumable)
                {
                    var wrongQuickItem = RejectedBrewOutcome(itemInstanceId, committedCharges, ActiveRun.Inventory);
                    return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                        wrongQuickItem, persisted: true, pending: false,
                        "The requested instance is not the active quick-slot Fortune Brew; the expedition was unchanged.");
                }

                FortuneChargeResult charge = book.GrantFortuneBrewCharge(itemInstanceId, transactionId);
                InventoryOperationOutcome inventoryOperation = null;
                if (charge.Status == FortuneOperationStatus.Succeeded)
                {
                    inventoryOperation = candidateInventory.TryConsumeQuickConsumable(
                        itemInstanceId, FortuneBrewItemId, transactionId);
                    if (!inventoryOperation.Succeeded || inventoryOperation.WasReplay)
                    {
                        FortuneOperationStatus rejectedStatus = inventoryOperation.Status ==
                            InventoryOperationStatus.RejectedTransactionConflict || inventoryOperation.WasReplay
                                ? FortuneOperationStatus.RejectedConflict
                                : FortuneOperationStatus.RejectedInvalidRequest;
                        var rejectedInventoryOperation = new FortuneBrewApplicationOutcome(
                            itemInstanceId, rejectedStatus, committedCharges, ActiveRun.Inventory,
                            inventoryOperation);
                        return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                            rejectedInventoryOperation, persisted: true, pending: false,
                            inventoryOperation.WasReplay
                                ? "An inventory receipt exists without its atomic Fortune receipt; the brew was rejected without publication."
                                : "The quick-consumable inventory transaction was rejected; the expedition was unchanged.");
                    }
                }

                FortuneOfferBookSnapshot updatedBook = book.CreateSnapshot();
                ExpeditionSaveData candidateSave = _activeExpedition.Clone();
                candidateSave.fortuneOfferBookSnapshot = updatedBook;
                candidateSave.inventorySnapshotJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot(), false);
                candidateSave.healthPotionCharges = ExpeditionSaveMapper.GetHealthPotionCharges(candidateInventory);
                // D09 keeps the in-room entry checkpoint immutable. An interrupted-room resume
                // intentionally restores that checkpoint; completed-room saves retain this mutation.
                ExpeditionRunConfiguration candidateRun = ActiveRun.WithInventory(candidateInventory);
                var outcome = new FortuneBrewApplicationOutcome(
                    itemInstanceId, charge.Status, charge.CurrentCharges, candidateInventory, inventoryOperation);

                SaveWriteResult save = await SaveExpeditionCandidateUnderGateAsync(
                    candidateSave, _activeExpeditionRevision, candidateRun);
                if (save.Succeeded)
                    return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                        outcome, persisted: true, pending: false, save.Message);

                if (HasPendingExpeditionWrite)
                {
                    _pendingFortuneMutation = new PendingFortuneMutation(operationKey, outcome);
                    return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                        outcome, persisted: false, pending: true, save.Message);
                }

                return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(
                    outcome, persisted: false, pending: false, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        async Task<PlayerFortuneBrewCommitOutcome> IPlayerFortuneBrewRuntime.ConsumeFortuneBrewAsync(
            ItemInstanceId itemInstanceId, TransactionId transactionId)
        {
            ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> result =
                await ConsumeFortuneBrewAsync(itemInstanceId, transactionId);
            int currentCharges = result.Persisted && result.Outcome != null
                ? result.Outcome.CurrentCharges
                : GetCommittedFortuneCharges();
            return new PlayerFortuneBrewCommitOutcome(
                result.Persisted,
                result.Outcome == null ? FortuneOperationStatus.RejectedInvalidRequest : result.Outcome.Status,
                currentCharges,
                result.Persisted && result.Outcome != null ? result.Outcome.Inventory : null,
                pending: result.Pending);
        }

        private int GetCommittedFortuneCharges()
        {
            return _activeExpedition?.fortuneOfferBookSnapshot == null
                ? 0
                : _activeExpedition.fortuneOfferBookSnapshot.charges;
        }

        private static FortuneBrewApplicationOutcome RejectedBrewOutcome(
            ItemInstanceId itemInstanceId, int currentCharges, ExpeditionInventory inventory)
        {
            return new FortuneBrewApplicationOutcome(itemInstanceId,
                FortuneOperationStatus.RejectedInvalidRequest, currentCharges, inventory, null);
        }

        private static ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> BrewCommitFailure(
            string message, bool pending = false)
        {
            return new ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome>(null, false, pending, message);
        }
    }

    /// <summary>Fortune charge result and candidate inventory for one durable brew operation.</summary>
    public sealed class FortuneBrewApplicationOutcome
    {
        public FortuneBrewApplicationOutcome(
            ItemInstanceId itemInstanceId,
            FortuneOperationStatus status,
            int currentCharges,
            ExpeditionInventory inventory,
            InventoryOperationOutcome inventoryOperation)
        {
            ItemInstanceId = itemInstanceId;
            Status = status;
            CurrentCharges = currentCharges;
            Inventory = inventory;
            InventoryOperation = inventoryOperation;
        }

        public ItemInstanceId ItemInstanceId { get; }
        public FortuneOperationStatus Status { get; }
        public int CurrentCharges { get; }
        public ExpeditionInventory Inventory { get; }
        public InventoryOperationOutcome InventoryOperation { get; }
        public bool Succeeded => Status == FortuneOperationStatus.Succeeded ||
            Status == FortuneOperationStatus.AlreadyApplied;
    }
}
