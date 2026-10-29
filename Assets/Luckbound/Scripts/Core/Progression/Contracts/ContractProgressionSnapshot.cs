using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Luckbound.Core.Progression.Contracts
{
    /// <summary>Stable identity and type for one processed profile-progress event.</summary>
    public sealed class ContractProgressEventReceipt
    {
        public ContractProgressEventReceipt(string eventId, ContractProgressEvent @event)
        {
            if (!IsValidEventId(eventId))
                throw new ArgumentException("A progress event ID must contain 1-160 ASCII letters, digits, '.', '_', ':', '/', or '-'.", nameof(eventId));
            if (!Enum.IsDefined(typeof(ContractProgressEvent), @event))
                throw new ArgumentOutOfRangeException(nameof(@event));

            EventId = eventId;
            Event = @event;
        }

        public string EventId { get; }
        public ContractProgressEvent Event { get; }

        private static bool IsValidEventId(string eventId)
        {
            if (string.IsNullOrEmpty(eventId) || eventId.Length > 160) return false;

            foreach (char value in eventId)
            {
                bool allowed = value >= 'A' && value <= 'Z' ||
                               value >= 'a' && value <= 'z' ||
                               value >= '0' && value <= '9' ||
                               value == '.' || value == '_' || value == ':' || value == '/' || value == '-';
                if (!allowed) return false;
            }

            return true;
        }
    }

    /// <summary>One persisted profile counter, keyed by the stable contract ID.</summary>
    public sealed class ContractProgressCounter
    {
        public ContractProgressCounter(string contractId, int progress)
        {
            if (string.IsNullOrWhiteSpace(contractId))
                throw new ArgumentException("A contract counter requires a stable contract ID.", nameof(contractId));
            if (progress < 0) throw new ArgumentOutOfRangeException(nameof(progress));

            ContractId = contractId;
            Progress = progress;
        }

        public string ContractId { get; }
        public int Progress { get; }
    }

    /// <summary>
    /// Immutable E10 projection of profile progression, including processed event receipts. The save
    /// DTO mapping and persistence transaction remain owned by the coordinator.
    /// </summary>
    public sealed class ContractProgressionSnapshot
    {
        private readonly ReadOnlyCollection<string> _unlockedRaceIds;
        private readonly ReadOnlyCollection<string> _unlockedStartingKitIds;
        private readonly ReadOnlyCollection<string> _acceptedContractIds;
        private readonly ReadOnlyCollection<string> _claimedContractIds;
        private readonly ReadOnlyCollection<ContractProgressCounter> _contractCounters;
        private readonly ReadOnlyCollection<string> _activeContractIds;
        private readonly ReadOnlyCollection<ContractProgressEventReceipt> _processedEvents;

        public ContractProgressionSnapshot(int availableRenown,
            IEnumerable<string> unlockedRaceIds,
            IEnumerable<string> unlockedStartingKitIds,
            IEnumerable<string> acceptedContractIds,
            IEnumerable<string> claimedContractIds,
            IEnumerable<ContractProgressCounter> contractCounters,
            IEnumerable<ContractProgressEventReceipt> processedEvents = null)
        {
            if (availableRenown < 0) throw new ArgumentOutOfRangeException(nameof(availableRenown));

            string[] races = NormalizeIds(unlockedRaceIds, nameof(unlockedRaceIds));
            string[] kits = NormalizeIds(unlockedStartingKitIds, nameof(unlockedStartingKitIds));
            string[] accepted = NormalizeIds(acceptedContractIds, nameof(acceptedContractIds));
            string[] claimed = NormalizeIds(claimedContractIds, nameof(claimedContractIds));
            if (claimed.Except(accepted, StringComparer.Ordinal).Any())
                throw new ArgumentException("Claimed contracts must also be in the accepted-contract set.", nameof(claimedContractIds));

            string[] active = accepted.Except(claimed, StringComparer.Ordinal).ToArray();
            if (active.Length > ContractProgressionRules.MaximumActiveContracts)
                throw new ArgumentException("A profile cannot have more than three active contracts.", nameof(acceptedContractIds));

            ContractProgressCounter[] counters = NormalizeCounters(contractCounters);
            ContractProgressEventReceipt[] receipts = NormalizeProcessedEvents(
                processedEvents ?? Array.Empty<ContractProgressEventReceipt>());

            AvailableRenown = availableRenown;
            _unlockedRaceIds = Array.AsReadOnly(races);
            _unlockedStartingKitIds = Array.AsReadOnly(kits);
            _acceptedContractIds = Array.AsReadOnly(accepted);
            _claimedContractIds = Array.AsReadOnly(claimed);
            _contractCounters = Array.AsReadOnly(counters);
            _activeContractIds = Array.AsReadOnly(active);
            _processedEvents = Array.AsReadOnly(receipts);
        }

        public int AvailableRenown { get; }
        public IReadOnlyList<string> UnlockedRaceIds => _unlockedRaceIds;
        public IReadOnlyList<string> UnlockedStartingKitIds => _unlockedStartingKitIds;
        public IReadOnlyList<string> AcceptedContractIds => _acceptedContractIds;
        public IReadOnlyList<string> ClaimedContractIds => _claimedContractIds;
        public IReadOnlyList<ContractProgressCounter> ContractCounters => _contractCounters;
        public IReadOnlyList<string> ActiveContractIds => _activeContractIds;
        public IReadOnlyList<ContractProgressEventReceipt> ProcessedEvents => _processedEvents;

        public int GetProgress(string contractId)
        {
            if (contractId == null) throw new ArgumentNullException(nameof(contractId));
            ContractProgressCounter counter = _contractCounters.FirstOrDefault(value =>
                string.Equals(value.ContractId, contractId, StringComparison.Ordinal));
            return counter == null ? 0 : counter.Progress;
        }

        public bool IsAccepted(string contractId) => Contains(_acceptedContractIds, contractId);
        public bool IsClaimed(string contractId) => Contains(_claimedContractIds, contractId);
        public bool IsActive(string contractId) => IsAccepted(contractId) && !IsClaimed(contractId);

        public bool IsComplete(string contractId)
        {
            return ContractCatalog.TryGet(contractId, out ContractDefinition definition) &&
                   GetProgress(contractId) >= definition.RequiredProgress;
        }

        /// <summary>Checks ownership for race, kit, and claimed-contract narrative unlock IDs.</summary>
        public bool IsUnlockGranted(string unlockId)
        {
            if (string.IsNullOrWhiteSpace(unlockId)) throw new ArgumentException("An unlock ID is required.", nameof(unlockId));
            if (Contains(_unlockedRaceIds, unlockId) || Contains(_unlockedStartingKitIds, unlockId)) return true;

            foreach (string contractId in _claimedContractIds)
            {
                if (!ContractCatalog.TryGet(contractId, out ContractDefinition definition)) continue;
                if (definition.Reward.Unlocks.Any(unlock => string.Equals(unlock.UnlockId, unlockId, StringComparison.Ordinal)))
                    return true;
            }

            return false;
        }

        internal ContractProgressionSnapshot WithProgress(string contractId, int progress)
        {
            var counters = _contractCounters.Where(counter =>
                !string.Equals(counter.ContractId, contractId, StringComparison.Ordinal)).ToList();
            counters.Add(new ContractProgressCounter(contractId, progress));
            return Copy(contractCounters: counters);
        }

        internal ContractProgressionSnapshot WithProcessedEvent(ContractProgressEventReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            return Copy(processedEvents: _processedEvents.Concat(new[] { receipt }));
        }

        internal ContractProgressionSnapshot WithAcceptedContract(string contractId)
        {
            return Copy(acceptedContractIds: _acceptedContractIds.Concat(new[] { contractId }));
        }

        internal ContractProgressionSnapshot WithClaimedContract(string contractId, ContractReward reward)
        {
            if (reward == null) throw new ArgumentNullException(nameof(reward));

            string[] races = _unlockedRaceIds.Concat(reward.Unlocks
                .Where(unlock => unlock.Kind == ContractUnlockKind.PlayableRace)
                .Select(unlock => unlock.UnlockId)).Distinct(StringComparer.Ordinal).ToArray();
            string[] kits = _unlockedStartingKitIds.Concat(reward.Unlocks
                .Where(unlock => unlock.Kind == ContractUnlockKind.StartingKit)
                .Select(unlock => unlock.UnlockId)).Distinct(StringComparer.Ordinal).ToArray();

            return new ContractProgressionSnapshot(checked(AvailableRenown + reward.Renown), races, kits,
                _acceptedContractIds, _claimedContractIds.Concat(new[] { contractId }), _contractCounters,
                _processedEvents);
        }

        private ContractProgressionSnapshot Copy(
            int? availableRenown = null,
            IEnumerable<string> unlockedRaceIds = null,
            IEnumerable<string> unlockedStartingKitIds = null,
            IEnumerable<string> acceptedContractIds = null,
            IEnumerable<string> claimedContractIds = null,
            IEnumerable<ContractProgressCounter> contractCounters = null,
            IEnumerable<ContractProgressEventReceipt> processedEvents = null)
        {
            return new ContractProgressionSnapshot(availableRenown ?? AvailableRenown,
                unlockedRaceIds ?? _unlockedRaceIds,
                unlockedStartingKitIds ?? _unlockedStartingKitIds,
                acceptedContractIds ?? _acceptedContractIds,
                claimedContractIds ?? _claimedContractIds,
                contractCounters ?? _contractCounters,
                processedEvents ?? _processedEvents);
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return values.Contains(value, StringComparer.Ordinal);
        }

        private static string[] NormalizeIds(IEnumerable<string> values, string parameterName)
        {
            if (values == null) throw new ArgumentNullException(parameterName);
            string[] result = values.ToArray();
            if (result.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Profile IDs cannot be null, empty, or whitespace.", parameterName);
            if (result.Distinct(StringComparer.Ordinal).Count() != result.Length)
                throw new ArgumentException("Profile IDs must be unique.", parameterName);
            return result.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private static ContractProgressCounter[] NormalizeCounters(IEnumerable<ContractProgressCounter> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            ContractProgressCounter[] source = values.ToArray();
            if (source.Any(counter => counter == null))
                throw new ArgumentException("Contract counters cannot contain null entries.", nameof(values));
            if (source.Select(counter => counter.ContractId).Distinct(StringComparer.Ordinal).Count() != source.Length)
                throw new ArgumentException("Contract counters must be unique by contract ID.", nameof(values));

            return source.Select(counter =>
            {
                int progress = counter.Progress;
                if (ContractCatalog.TryGet(counter.ContractId, out ContractDefinition definition))
                    progress = Math.Min(progress, definition.RequiredProgress);
                return new ContractProgressCounter(counter.ContractId, progress);
            }).OrderBy(counter => counter.ContractId, StringComparer.Ordinal).ToArray();
        }

        private static ContractProgressEventReceipt[] NormalizeProcessedEvents(
            IEnumerable<ContractProgressEventReceipt> values)
        {
            ContractProgressEventReceipt[] source = values.ToArray();
            if (source.Any(receipt => receipt == null))
                throw new ArgumentException("Processed progress events cannot contain null entries.", nameof(values));
            if (source.Select(receipt => receipt.EventId).Distinct(StringComparer.Ordinal).Count() != source.Length)
                throw new ArgumentException("Processed progress event IDs must be unique.", nameof(values));

            return source.OrderBy(receipt => receipt.EventId, StringComparer.Ordinal).ToArray();
        }
    }
}
