using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        /// <summary>
        /// Atomically claims a selected item offer and adds its catalog item to the expedition
        /// backpack. The stable claim-derived inventory IDs make a committed claim replay-safe.
        /// </summary>
        public async Task<ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>>
            ClaimFortuneOfferRewardAsync(string offerId)
        {
            string operationKey = CreateFortuneOperationKey("offer.claim.reward", offerId);
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingFortuneMutation != null)
                {
                    if (_pendingExpeditionCandidate == null)
                    {
                        // A generic expedition retry may have committed the combined candidate.
                        _pendingFortuneMutation = null;
                    }
                    else
                    {
                        if (!string.Equals(_pendingFortuneMutation.OperationKey, operationKey, StringComparison.Ordinal))
                            return RewardCommitFailure("A different Fortune mutation is waiting for its exact save retry.", pending: true);
                        if (!(_pendingFortuneMutation.Outcome is FortuneRewardApplicationOutcome pendingOutcome))
                            return RewardCommitFailure("The pending Fortune mutation has a different result type.", pending: true);
                        return await RetryPendingFortuneMutationUnderGateAsync(pendingOutcome);
                    }
                }

                if (HasPendingExpeditionWrite || HasPendingEconomyMutation || HasPendingCraftingWrite ||
                    HasPendingPermanentUpgradePurchase || HasPendingContractProgression || HasPendingSettlement)
                    return RewardCommitFailure("Retry the pending save or profile transaction before claiming another Fortune reward.", pending: true);
                if (!CanContinueExpedition || ActiveRun == null || _activeExpedition == null ||
                    _activeExpedition.fortuneOfferBookSnapshot == null)
                    return RewardCommitFailure("There is no safely resumable active expedition with a Fortuna offer book.");

                FortuneOfferBook book = FortuneOfferBook.Restore(_activeExpedition.fortuneOfferBookSnapshot);
                FortuneOffer offer = book.GetOffer(offerId);
                if (offer == null || string.IsNullOrEmpty(offer.SelectedOptionId))
                {
                    FortuneClaimResult rejectedClaim = book.Claim(offerId);
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(
                        new FortuneRewardApplicationOutcome(rejectedClaim, null, null), true, false,
                        "The Fortune offer must exist and have a selected option before it can be claimed.");
                }

                ItemCatalog catalog = ActiveRun.Inventory.Catalog;
                string claimId = offer.ClaimId;
                TransactionId rewardTransactionId = CreateStableFortuneTransactionId(claimId);
                InventoryItemStack reward = new InventoryItemStack(
                    CreateStableFortuneItemInstanceId(claimId), offer.SelectedOptionId, ItemRarity.Common, 0, 1);

                if (catalog.TryGet(reward.ItemId, out ItemDefinition definition) &&
                    definition.Kind == ItemKind.Weapon && !definition.IsCompatibleWithClass(ActiveRun.ClassId))
                {
                    var incompatible = new InventoryOperationOutcome(rewardTransactionId,
                        InventoryOperationStatus.RejectedIncompatibleClass, itemId: reward.ItemId);
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(
                        new FortuneRewardApplicationOutcome(null, reward, incompatible), true, false,
                        "The selected weapon is incompatible with this expedition class; the offer remains unclaimed.");
                }

                ExpeditionInventory candidateInventory = ExpeditionInventory.Restore(
                    catalog, ActiveRun.Inventory.CreateSnapshot());
                InventoryOperationOutcome inventoryOutcome = candidateInventory.TryCollectItem(reward, rewardTransactionId);
                if (!inventoryOutcome.Succeeded)
                {
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(
                        new FortuneRewardApplicationOutcome(null, reward, inventoryOutcome), true, false,
                        "The selected item could not be added to the backpack; the offer remains unclaimed.");
                }

                FortuneClaimResult claim = book.Claim(offerId);
                if (!claim.Succeeded)
                {
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(
                        new FortuneRewardApplicationOutcome(claim, reward, inventoryOutcome), true, false,
                        "The Fortune claim could not be applied; no expedition state was changed.");
                }

                FortuneOfferBookSnapshot updatedBook = book.CreateSnapshot();
                string updatedInventory = JsonUtility.ToJson(candidateInventory.CreateSnapshot(), false);
                bool bookChanged = !string.Equals(
                    JsonUtility.ToJson(_activeExpedition.fortuneOfferBookSnapshot, false),
                    JsonUtility.ToJson(updatedBook, false), StringComparison.Ordinal);
                bool inventoryChanged = !string.Equals(
                    _activeExpedition.inventorySnapshotJson, updatedInventory, StringComparison.Ordinal);
                var outcome = new FortuneRewardApplicationOutcome(claim, reward, inventoryOutcome);

                if (!bookChanged && !inventoryChanged)
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(outcome, true, false,
                        "The Fortune reward is already represented by the committed expedition state.");

                ExpeditionSaveData candidateSave = _activeExpedition.Clone();
                candidateSave.fortuneOfferBookSnapshot = updatedBook;
                candidateSave.inventorySnapshotJson = updatedInventory;
                InventoryItemStack quickConsumable = candidateInventory.QuickConsumable;
                candidateSave.healthPotionCharges = quickConsumable == null ? 0 : quickConsumable.Quantity;
                ExpeditionRunConfiguration candidateRun = ActiveRun.WithInventory(candidateInventory);

                SaveWriteResult save = await SaveExpeditionCandidateUnderGateAsync(
                    candidateSave, _activeExpeditionRevision, candidateRun);
                if (save.Succeeded)
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(outcome, true, false, save.Message);

                if (HasPendingExpeditionWrite)
                {
                    _pendingFortuneMutation = new PendingFortuneMutation(operationKey, outcome);
                    return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(outcome, false, true, save.Message);
                }

                return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(outcome, false, false, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        private static TransactionId CreateStableFortuneTransactionId(string claimId)
        {
            return new TransactionId(CreateStableFortuneGuid("transaction", claimId));
        }

        private static ItemInstanceId CreateStableFortuneItemInstanceId(string claimId)
        {
            return new ItemInstanceId(CreateStableFortuneGuid("item-instance", claimId));
        }

        private static Guid CreateStableFortuneGuid(string purpose, string claimId)
        {
            byte[] input = Encoding.UTF8.GetBytes("luckbound-fortune-reward-v1|" + purpose + "|" + claimId);
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create()) hash = algorithm.ComputeHash(input);
            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, guidBytes.Length);
            var value = new Guid(guidBytes);
            if (value != Guid.Empty) return value;
            guidBytes[0] = 1;
            return new Guid(guidBytes);
        }

        private static ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> RewardCommitFailure(
            string message, bool pending = false)
        {
            return new ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome>(null, false, pending, message);
        }
    }

    /// <summary>The selected claim and its corresponding inventory operation result.</summary>
    public sealed class FortuneRewardApplicationOutcome
    {
        public FortuneRewardApplicationOutcome(
            FortuneClaimResult claim,
            InventoryItemStack reward,
            InventoryOperationOutcome inventoryOperation)
        {
            Claim = claim;
            Reward = reward;
            InventoryOperation = inventoryOperation;
        }

        public FortuneClaimResult Claim { get; }
        public InventoryItemStack Reward { get; }
        public InventoryOperationOutcome InventoryOperation { get; }
        public bool Succeeded => Claim != null && Claim.Succeeded &&
            InventoryOperation != null && InventoryOperation.Succeeded;
    }
}
