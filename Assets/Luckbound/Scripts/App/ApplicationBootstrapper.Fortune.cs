using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        private PendingFortuneMutation _pendingFortuneMutation;

        public bool HasPendingFortuneMutation => _pendingFortuneMutation != null;
        public string PendingFortuneOperationKey => _pendingFortuneMutation == null
            ? string.Empty
            : _pendingFortuneMutation.OperationKey;

        /// <summary>Reads the currently committed offer from the active expedition.</summary>
        public FortuneOffer GetFortuneOffer(string offerId)
        {
            if (_activeExpedition == null || _activeExpedition.fortuneOfferBookSnapshot == null) return null;
            return FortuneOfferBook.Restore(_activeExpedition.fortuneOfferBookSnapshot).GetOffer(offerId);
        }

        /// <summary>Reads every committed offer in stable offer-ID order.</summary>
        public IReadOnlyList<FortuneOffer> GetFortuneOffers()
        {
            if (_activeExpedition == null || _activeExpedition.fortuneOfferBookSnapshot == null)
                return Array.Empty<FortuneOffer>();
            return FortuneOfferBook.Restore(_activeExpedition.fortuneOfferBookSnapshot).GetOffers();
        }

        /// <summary>Returns the committed offer's current reroll availability for presentation.</summary>
        public FortuneRerollAvailability GetFortuneRerollAvailability(string offerId)
        {
            if (_activeExpedition == null || _activeExpedition.fortuneOfferBookSnapshot == null)
                return FortuneRerollAvailability.UnknownOffer;
            return FortuneOfferBook.Restore(_activeExpedition.fortuneOfferBookSnapshot).GetRerollAvailability(offerId);
        }

        /// <summary>
        /// Creates a reproducible offer from caller-supplied eligible IDs. This layer assigns no
        /// reward values; content eligibility and reward application remain with the integrator.
        /// </summary>
        public Task<ExpeditionMutationCommitResult<FortuneOfferResult>> CreateFortuneOfferAsync(
            string offerId, string contextId, IEnumerable<string> eligibleOptionIds)
        {
            string[] eligible = eligibleOptionIds == null
                ? null
                : eligibleOptionIds.Distinct(StringComparer.Ordinal)
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray();
            string eligiblePayload = EncodeIdList(eligible);
            string operationKey = CreateFortuneOperationKey("offer.create", offerId, contextId, eligiblePayload);
            return ExecuteFortuneMutationAsync(operationKey,
                book => book.CreateOffer(offerId, contextId, eligible));
        }

        /// <summary>Repeats an offer once per transaction ID and commits the resulting charge and options.</summary>
        public Task<ExpeditionMutationCommitResult<FortuneRerollResult>> RepeatFortuneOfferAsync(
            string offerId, TransactionId transactionId)
        {
            string operationKey = CreateFortuneOperationKey("offer.repeat", offerId,
                transactionId.IsValid ? transactionId.ToString() : string.Empty);
            return ExecuteFortuneMutationAsync(operationKey, book => book.Reroll(offerId, transactionId));
        }

        /// <summary>Locks one current option and persists the choice before returning success.</summary>
        public Task<ExpeditionMutationCommitResult<FortuneChoiceResult>> ChooseFortuneOptionAsync(
            string offerId, string optionId)
        {
            string operationKey = CreateFortuneOperationKey("offer.choose", offerId, optionId);
            return ExecuteFortuneMutationAsync(operationKey, book => book.Choose(offerId, optionId));
        }

        /// <summary>
        /// Persists the one-time claim marker and returns its stable key. Reward flows should use
        /// ClaimFortuneOfferRewardAsync so the selected item and claim are committed together.
        /// </summary>
        public Task<ExpeditionMutationCommitResult<FortuneClaimResult>> ClaimFortuneOfferAsync(string offerId)
        {
            string operationKey = CreateFortuneOperationKey("offer.claim", offerId);
            return ExecuteFortuneMutationAsync(operationKey, book => book.Claim(offerId));
        }

        /// <summary>Persists the once-per-guardian Fortuna charge, capped by the core at three.</summary>
        public Task<ExpeditionMutationCommitResult<FortuneChargeResult>> GrantGuardianFortuneChargeAsync(
            string guardianId)
        {
            string operationKey = CreateFortuneOperationKey("guardian.grant-fortune", guardianId);
            return ExecuteFortuneMutationAsync(operationKey, book => book.GrantGuardianCharge(guardianId));
        }

        /// <summary>
        /// Retries the exact pending operation after a storage failure. Reissuing the same public
        /// operation with the same payload also takes this path automatically.
        /// </summary>
        public async Task<ExpeditionMutationCommitResult<T>> RetryPendingFortuneMutationAsync<T>(string operationKey)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingFortuneMutation == null)
                    return new ExpeditionMutationCommitResult<T>(default, false, false,
                        "There is no pending Fortune mutation.");
                if (!string.Equals(_pendingFortuneMutation.OperationKey, operationKey, StringComparison.Ordinal))
                    return new ExpeditionMutationCommitResult<T>(default, false, true,
                        "A different Fortune mutation is waiting for its exact save retry.");
                if (!(_pendingFortuneMutation.Outcome is T outcome))
                    return new ExpeditionMutationCommitResult<T>(default, false, true,
                        "The pending Fortune mutation has a different result type.");
                return await RetryPendingFortuneMutationUnderGateAsync(outcome);
            }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ExpeditionMutationCommitResult<T>> ExecuteFortuneMutationAsync<T>(
            string operationKey, Func<FortuneOfferBook, T> mutation)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingFortuneMutation != null)
                {
                    if (_pendingExpeditionCandidate == null && _pendingExpeditionSessionPublication == null)
                    {
                        // A generic expedition retry may have committed the pending Fortune snapshot
                        // without returning its typed result. The saved book now owns replay semantics.
                        _pendingFortuneMutation = null;
                    }
                    else
                    {
                        if (!string.Equals(_pendingFortuneMutation.OperationKey, operationKey, StringComparison.Ordinal))
                            return new ExpeditionMutationCommitResult<T>(default, false, true,
                                "A different Fortune mutation is waiting for its exact save retry.");
                        if (!(_pendingFortuneMutation.Outcome is T pendingOutcome))
                            return new ExpeditionMutationCommitResult<T>(default, false, true,
                                "The pending Fortune mutation has a different result type.");
                        return await RetryPendingFortuneMutationUnderGateAsync(pendingOutcome);
                    }
                }

                if (HasPendingExpeditionWrite)
                    return new ExpeditionMutationCommitResult<T>(default, false, true,
                        "Retry the pending expedition write before starting a Fortune mutation.");
                if (!CanContinueExpedition || ActiveRun == null || _activeExpedition == null ||
                    _activeExpedition.fortuneOfferBookSnapshot == null)
                    return new ExpeditionMutationCommitResult<T>(default, false, false,
                        "There is no safely resumable active expedition with a Fortuna offer book.");

                FortuneOfferBook book = FortuneOfferBook.Restore(_activeExpedition.fortuneOfferBookSnapshot);
                string before = JsonUtility.ToJson(book.CreateSnapshot(), false);
                T outcome = mutation(book);
                FortuneOfferBookSnapshot updatedBook = book.CreateSnapshot();
                string after = JsonUtility.ToJson(updatedBook, false);

                // Rejections and already-applied operations that add no receipt need no write;
                // their result refers to the state that was already committed.
                if (string.Equals(before, after, StringComparison.Ordinal))
                    return new ExpeditionMutationCommitResult<T>(outcome, true, false,
                        "The Fortune operation is already represented by the committed expedition state.");

                ExpeditionSaveData candidate = _activeExpedition.Clone();
                candidate.fortuneOfferBookSnapshot = updatedBook;
                // Preserve roomEntryCheckpointJson exactly. A restart during this room restores the
                // whole entry snapshot, including its offers, choices, receipts, and charge balance.
                SaveWriteResult save = await SaveExpeditionCandidateUnderGateAsync(
                    candidate, _activeExpeditionRevision, null);
                if (save.Succeeded)
                    return new ExpeditionMutationCommitResult<T>(outcome, true, false, save.Message);

                if (HasPendingExpeditionWrite)
                {
                    _pendingFortuneMutation = new PendingFortuneMutation(operationKey, outcome);
                    return new ExpeditionMutationCommitResult<T>(outcome, false, true, save.Message);
                }
                return new ExpeditionMutationCommitResult<T>(outcome, false, false, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ExpeditionMutationCommitResult<T>> RetryPendingFortuneMutationUnderGateAsync<T>(T outcome)
        {
            if (!HasPendingExpeditionWrite)
            {
                _pendingFortuneMutation = null;
                return new ExpeditionMutationCommitResult<T>(outcome, true, false,
                    "The Fortune mutation was already durably committed.");
            }

            SaveWriteResult save = await RetryPendingExpeditionWriteUnderGateAsync();
            if (save.Succeeded) _pendingFortuneMutation = null;
            return new ExpeditionMutationCommitResult<T>(outcome, save.Succeeded,
                !save.Succeeded && HasPendingExpeditionWrite, save.Message);
        }

        private static string CreateFortuneOperationKey(string operation, params string[] fields)
        {
            var builder = new StringBuilder();
            AppendKeyField(builder, operation);
            foreach (string field in fields) AppendKeyField(builder, field);
            return builder.ToString();
        }

        private static string EncodeIdList(string[] ids)
        {
            if (ids == null) return "N";
            var builder = new StringBuilder("A");
            builder.Append(ids.Length.ToString(CultureInfo.InvariantCulture)).Append(':');
            foreach (string id in ids) AppendKeyField(builder, id);
            return builder.ToString();
        }

        private static void AppendKeyField(StringBuilder builder, string field)
        {
            if (field == null)
            {
                builder.Append("-1:");
                return;
            }
            builder.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field);
        }

        private sealed class PendingFortuneMutation
        {
            public PendingFortuneMutation(string operationKey, object outcome)
            {
                OperationKey = operationKey ?? throw new ArgumentNullException(nameof(operationKey));
                Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            }

            public string OperationKey { get; }
            public object Outcome { get; }
        }
    }
}
