using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Randomness;

namespace Luckbound.Core.Fortune
{
    /// <summary>
    /// Deterministic, saveable offer state for one expedition. Callers supply only option IDs that
    /// are valid for the offer's context; this core owns selection, reroll charges, and idempotency.
    /// </summary>
    public sealed class FortuneOfferBook
    {
        public const int SnapshotVersion = 2;
        private const int LegacySnapshotVersion = 1;
        public const int MaximumCharges = 3;
        public const string HumanRaceId = "race.human";
        private const string OfferRandomDomain = "fortune.offers";
        private const string RerollRandomDomain = "fortune.offers.reroll";
        private const string RerollFallbackRandomDomain = "fortune.offers.reroll.fallback";

        private readonly Dictionary<string, OfferRecord> _offers = new Dictionary<string, OfferRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> _processedGuardianIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, FortuneRerollReceiptSnapshot> _rerollReceipts =
            new Dictionary<string, FortuneRerollReceiptSnapshot>(StringComparer.Ordinal);

        public FortuneOfferBook(int generationVersion, uint seed, string raceId)
            : this(generationVersion, seed, raceId, InitialChargesForRace(raceId))
        {
        }

        private FortuneOfferBook(int generationVersion, uint seed, string raceId, int charges)
        {
            if (generationVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generationVersion));
            if (string.IsNullOrWhiteSpace(raceId)) throw new ArgumentException("A race ID is required.", nameof(raceId));
            if (charges < 0 || charges > MaximumCharges) throw new ArgumentOutOfRangeException(nameof(charges));

            GenerationVersion = generationVersion;
            Seed = Xorshift32.NormalizeSeed(seed);
            RaceId = raceId;
            CurrentCharges = charges;
        }

        public int GenerationVersion { get; }
        public uint Seed { get; }
        public string RaceId { get; }
        public int CurrentCharges { get; private set; }

        public static int InitialChargesForRace(string raceId)
        {
            if (string.IsNullOrWhiteSpace(raceId)) throw new ArgumentException("A race ID is required.", nameof(raceId));
            return string.Equals(raceId, HumanRaceId, StringComparison.Ordinal) ? 2 : 1;
        }

        /// <summary>
        /// Creates one stable offer. The caller must pass a complete, context-valid set of IDs from
        /// the current content data; this method deliberately assigns no reward values or balance.
        /// </summary>
        public FortuneOfferResult CreateOffer(string offerId, string contextId, IEnumerable<string> eligibleOptionIds)
        {
            if (!IsStableId(offerId) || !IsStableId(contextId) || eligibleOptionIds == null)
                return new FortuneOfferResult(FortuneOperationStatus.RejectedInvalidRequest, null);

            string[] options;
            try
            {
                options = NormalizeOptionIds(eligibleOptionIds);
            }
            catch (ArgumentException)
            {
                return new FortuneOfferResult(FortuneOperationStatus.RejectedInvalidRequest, null);
            }

            if (_offers.TryGetValue(offerId, out OfferRecord existing))
            {
                bool sameContext = string.Equals(existing.ContextId, contextId, StringComparison.Ordinal);
                bool samePool = existing.EligibleOptionIds.SequenceEqual(options, StringComparer.Ordinal);
                return sameContext && samePool
                    ? new FortuneOfferResult(FortuneOperationStatus.AlreadyApplied, ToView(existing))
                    : new FortuneOfferResult(FortuneOperationStatus.RejectedConflict, null);
            }

            if (options.Length < 3)
                return new FortuneOfferResult(FortuneOperationStatus.RejectedInsufficientOptions, null);

            string[] firstOptions = SelectOffer(options, offerId, contextId, 0, 0, OfferRandomDomain);
            var record = new OfferRecord(offerId, contextId, options);
            record.History.Add(firstOptions);
            _offers.Add(offerId, record);
            return new FortuneOfferResult(FortuneOperationStatus.Succeeded, ToView(record));
        }

        public FortuneOffer GetOffer(string offerId)
        {
            return offerId != null && _offers.TryGetValue(offerId, out OfferRecord record) ? ToView(record) : null;
        }

        public IReadOnlyList<FortuneOffer> GetOffers()
        {
            return Array.AsReadOnly(_offers.Values.OrderBy(record => record.OfferId, StringComparer.Ordinal)
                .Select(ToView).ToArray());
        }

        public FortuneRerollAvailability GetRerollAvailability(string offerId)
        {
            if (offerId == null || !_offers.TryGetValue(offerId, out OfferRecord record))
                return FortuneRerollAvailability.UnknownOffer;
            if (!string.IsNullOrEmpty(record.SelectedOptionId))
                return FortuneRerollAvailability.OfferLocked;
            if (!TryBuildRerollOptions(record, out _))
                return FortuneRerollAvailability.InsufficientOptions;
            return CurrentCharges > 0
                ? FortuneRerollAvailability.Available
                : FortuneRerollAvailability.NoCharges;
        }

        /// <summary>Produces a fresh full offer and spends one charge only after a fresh set exists.</summary>
        public FortuneRerollResult Reroll(string offerId)
        {
            if (offerId == null || !_offers.TryGetValue(offerId, out OfferRecord record))
                return new FortuneRerollResult(FortuneOperationStatus.RejectedUnknownOffer, null, CurrentCharges);
            if (!string.IsNullOrEmpty(record.SelectedOptionId))
                return new FortuneRerollResult(FortuneOperationStatus.RejectedConflict, ToView(record), CurrentCharges);
            if (!TryBuildRerollOptions(record, out string[] options))
                return new FortuneRerollResult(FortuneOperationStatus.RejectedInsufficientOptions, ToView(record), CurrentCharges);
            if (CurrentCharges <= 0)
                return new FortuneRerollResult(FortuneOperationStatus.RejectedNoCharges, ToView(record), CurrentCharges);

            // All failure conditions are checked before changing either the offer or its balance.
            CurrentCharges--;
            record.History.Add(options);
            return new FortuneRerollResult(FortuneOperationStatus.Succeeded, ToView(record), CurrentCharges);
        }

        /// <summary>
        /// Repeats an offer with a durable transaction receipt. Replaying the same transaction and
        /// offer returns the original result without spending another charge; reusing its ID for a
        /// different offer is a conflict. Rejections are also recorded so they remain stable after
        /// later grants or reloads.
        /// </summary>
        public FortuneRerollResult Reroll(string offerId, TransactionId transactionId)
        {
            if (!transactionId.IsValid)
                return new FortuneRerollResult(FortuneOperationStatus.RejectedInvalidRequest,
                    offerId == null ? null : GetOffer(offerId), CurrentCharges);

            string transactionKey = transactionId.ToString();
            string requestedOfferId = offerId ?? string.Empty;
            if (_rerollReceipts.TryGetValue(transactionKey, out FortuneRerollReceiptSnapshot existing))
            {
                if (!string.Equals(existing.offerId, requestedOfferId, StringComparison.Ordinal))
                    return new FortuneRerollResult(FortuneOperationStatus.RejectedConflict,
                        offerId == null ? null : GetOffer(offerId), CurrentCharges);
                FortuneOperationStatus replayStatus = (FortuneOperationStatus)existing.status == FortuneOperationStatus.Succeeded
                    ? FortuneOperationStatus.AlreadyApplied
                    : (FortuneOperationStatus)existing.status;
                return new FortuneRerollResult(replayStatus, OfferFromReceipt(existing), existing.remainingCharges);
            }

            FortuneRerollResult result = Reroll(offerId);
            _rerollReceipts.Add(transactionKey, CreateReceipt(transactionKey, requestedOfferId, result));
            return result;
        }

        public FortuneChoiceResult Choose(string offerId, string optionId)
        {
            if (offerId == null || !_offers.TryGetValue(offerId, out OfferRecord record))
                return new FortuneChoiceResult(FortuneOperationStatus.RejectedUnknownOffer, null);
            if (!IsStableId(optionId) || !record.CurrentOptions.Contains(optionId, StringComparer.Ordinal))
                return new FortuneChoiceResult(FortuneOperationStatus.RejectedInvalidChoice, ToView(record));
            if (!string.IsNullOrEmpty(record.SelectedOptionId))
            {
                FortuneOperationStatus status = string.Equals(record.SelectedOptionId, optionId, StringComparison.Ordinal)
                    ? FortuneOperationStatus.AlreadyApplied
                    : FortuneOperationStatus.RejectedConflict;
                return new FortuneChoiceResult(status, ToView(record));
            }

            record.SelectedOptionId = optionId;
            return new FortuneChoiceResult(FortuneOperationStatus.Succeeded, ToView(record));
        }

        /// <summary>Returns a stable claim key so the integrating reward transaction can be replayed safely.</summary>
        public FortuneClaimResult Claim(string offerId)
        {
            if (offerId == null || !_offers.TryGetValue(offerId, out OfferRecord record))
                return new FortuneClaimResult(FortuneOperationStatus.RejectedUnknownOffer, string.Empty, offerId, string.Empty);
            if (string.IsNullOrEmpty(record.SelectedOptionId))
                return new FortuneClaimResult(FortuneOperationStatus.RejectedNoChoice, string.Empty, offerId, string.Empty);

            string claimId = "fortune.claim." + record.OfferId;
            if (record.Claimed)
                return new FortuneClaimResult(FortuneOperationStatus.AlreadyApplied, claimId, record.OfferId, record.SelectedOptionId);

            record.Claimed = true;
            return new FortuneClaimResult(FortuneOperationStatus.Succeeded, claimId, record.OfferId, record.SelectedOptionId);
        }

        /// <summary>Applies a guardian charge once per stable guardian ID, up to the three-charge cap.</summary>
        public FortuneChargeResult GrantGuardianCharge(string guardianId)
        {
            if (!IsStableId(guardianId))
                return new FortuneChargeResult(FortuneOperationStatus.RejectedInvalidRequest, CurrentCharges);
            if (!_processedGuardianIds.Add(guardianId))
                return new FortuneChargeResult(FortuneOperationStatus.AlreadyApplied, CurrentCharges);
            if (CurrentCharges >= MaximumCharges)
                return new FortuneChargeResult(FortuneOperationStatus.RejectedAtCapacity, CurrentCharges);

            CurrentCharges++;
            return new FortuneChargeResult(FortuneOperationStatus.Succeeded, CurrentCharges);
        }

        public FortuneOfferBookSnapshot CreateSnapshot()
        {
            return new FortuneOfferBookSnapshot
            {
                snapshotVersion = SnapshotVersion,
                generationVersion = GenerationVersion,
                seed = Seed,
                raceId = RaceId,
                charges = CurrentCharges,
                offers = _offers.Values.OrderBy(record => record.OfferId, StringComparer.Ordinal)
                    .Select(CreateSnapshot).ToArray(),
                processedGuardianIds = _processedGuardianIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                rerollReceipts = _rerollReceipts.Values.OrderBy(receipt => receipt.transactionId, StringComparer.Ordinal)
                    .Select(CloneReceipt).ToArray()
            };
        }

        public static FortuneOfferBook Restore(FortuneOfferBookSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.snapshotVersion != SnapshotVersion && snapshot.snapshotVersion != LegacySnapshotVersion)
                throw new FormatException("Fortune offer snapshot version is unsupported.");
            if (snapshot.generationVersion <= 0 || snapshot.seed == 0u || string.IsNullOrWhiteSpace(snapshot.raceId) ||
                snapshot.charges < 0 || snapshot.charges > MaximumCharges || snapshot.offers == null || snapshot.processedGuardianIds == null ||
                (snapshot.snapshotVersion == SnapshotVersion && snapshot.rerollReceipts == null))
                throw new FormatException("Fortune offer snapshot has invalid root state.");

            var restored = new FortuneOfferBook(snapshot.generationVersion, snapshot.seed, snapshot.raceId, snapshot.charges);
            foreach (string guardianId in snapshot.processedGuardianIds)
            {
                if (!IsStableId(guardianId) || !restored._processedGuardianIds.Add(guardianId))
                    throw new FormatException("Fortune snapshot contains an invalid or repeated guardian grant ID.");
            }

            foreach (FortuneOfferSnapshot savedOffer in snapshot.offers)
            {
                if (savedOffer == null || !IsStableId(savedOffer.offerId) || !IsStableId(savedOffer.contextId) ||
                    savedOffer.eligibleOptionIds == null || savedOffer.history == null || savedOffer.history.Length == 0 ||
                    savedOffer.history.Any(entry => entry == null || entry.optionIds == null))
                    throw new FormatException("Fortune snapshot contains an incomplete offer.");

                string[] eligible = NormalizeSnapshotIds(savedOffer.eligibleOptionIds, "eligible option IDs");
                if (eligible.Length < 3 || !eligible.SequenceEqual(savedOffer.eligibleOptionIds, StringComparer.Ordinal))
                    throw new FormatException("Fortune snapshot eligible option IDs must be sorted, unique, and include at least three IDs.");

                var record = new OfferRecord(savedOffer.offerId, savedOffer.contextId, eligible);
                var historyKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (FortuneOfferHistorySnapshot historyEntry in savedOffer.history)
                {
                    string[] options = NormalizeSnapshotIds(historyEntry.optionIds, "offered option IDs");
                    if (options.Length != 3 || options.Any(id => Array.BinarySearch(eligible, id, StringComparer.Ordinal) < 0))
                        throw new FormatException("Fortune snapshot must contain three distinct options from its eligible pool.");
                    if (!historyKeys.Add(CanonicalSetKey(options)))
                        throw new FormatException("Fortune snapshot repeats a complete offer.");
                    // Preserve display order; the normalized copy above is only for validation and set comparison.
                    record.History.Add((string[])historyEntry.optionIds.Clone());
                }

                string selected = savedOffer.selectedOptionId ?? string.Empty;
                if (!string.IsNullOrEmpty(selected) && !record.CurrentOptions.Contains(selected, StringComparer.Ordinal))
                    throw new FormatException("Fortune snapshot choice is not one of the current options.");
                if (savedOffer.claimed && string.IsNullOrEmpty(selected))
                    throw new FormatException("A claimed fortune offer must have a selected option.");
                record.SelectedOptionId = selected;
                record.Claimed = savedOffer.claimed;
                if (restored._offers.ContainsKey(record.OfferId))
                    throw new FormatException("Fortune snapshot repeats an offer ID.");
                restored._offers.Add(record.OfferId, record);
            }

            FortuneRerollReceiptSnapshot[] receipts = snapshot.rerollReceipts ?? Array.Empty<FortuneRerollReceiptSnapshot>();
            foreach (FortuneRerollReceiptSnapshot receipt in receipts)
            {
                if (!IsValidReceipt(receipt))
                    throw new FormatException("Fortune snapshot contains an invalid reroll receipt.");
                if (receipt.hasOffer && !restored._offers.ContainsKey(receipt.offerId))
                    throw new FormatException("Fortune reroll receipt refers to an unknown offer.");
                if (restored._rerollReceipts.ContainsKey(receipt.transactionId))
                    throw new FormatException("Fortune snapshot repeats a reroll transaction ID.");
                restored._rerollReceipts.Add(receipt.transactionId, CloneReceipt(receipt));
            }

            return restored;
        }

        private static FortuneRerollReceiptSnapshot CreateReceipt(string transactionId, string offerId,
            FortuneRerollResult result)
        {
            FortuneOffer offer = result.Offer;
            return new FortuneRerollReceiptSnapshot
            {
                transactionId = transactionId,
                offerId = offerId,
                status = (int)result.Status,
                contextId = offer == null ? string.Empty : offer.ContextId,
                optionIds = offer == null ? Array.Empty<string>() : offer.OptionIds.ToArray(),
                selectedOptionId = offer == null ? string.Empty : offer.SelectedOptionId,
                rerollCount = offer == null ? 0 : offer.RerollCount,
                claimed = offer != null && offer.IsClaimed,
                remainingCharges = result.RemainingCharges,
                hasOffer = offer != null
            };
        }

        private static FortuneOffer OfferFromReceipt(FortuneRerollReceiptSnapshot receipt)
        {
            return receipt.hasOffer
                ? new FortuneOffer(receipt.offerId, receipt.contextId, receipt.optionIds,
                    receipt.selectedOptionId, receipt.rerollCount, receipt.claimed)
                : null;
        }

        private static FortuneRerollReceiptSnapshot CloneReceipt(FortuneRerollReceiptSnapshot source)
        {
            return new FortuneRerollReceiptSnapshot
            {
                transactionId = source.transactionId,
                offerId = source.offerId,
                status = source.status,
                contextId = source.contextId,
                optionIds = source.optionIds == null ? null : (string[])source.optionIds.Clone(),
                selectedOptionId = source.selectedOptionId,
                rerollCount = source.rerollCount,
                claimed = source.claimed,
                remainingCharges = source.remainingCharges,
                hasOffer = source.hasOffer
            };
        }

        private static bool IsValidReceipt(FortuneRerollReceiptSnapshot receipt)
        {
            if (receipt == null || !TransactionId.TryParse(receipt.transactionId, out TransactionId parsedId) ||
                !string.Equals(parsedId.ToString(), receipt.transactionId, StringComparison.Ordinal) || receipt.offerId == null ||
                receipt.remainingCharges < 0 || receipt.remainingCharges > MaximumCharges || receipt.rerollCount < 0 ||
                !Enum.IsDefined(typeof(FortuneOperationStatus), receipt.status))
                return false;

            FortuneOperationStatus status = (FortuneOperationStatus)receipt.status;
            if (status == FortuneOperationStatus.AlreadyApplied || status == FortuneOperationStatus.RejectedInvalidRequest ||
                status == FortuneOperationStatus.RejectedAtCapacity || status == FortuneOperationStatus.RejectedInvalidChoice ||
                status == FortuneOperationStatus.RejectedNoChoice)
                return false;
            if (!receipt.hasOffer)
                return status == FortuneOperationStatus.RejectedUnknownOffer && receipt.optionIds != null && receipt.optionIds.Length == 0;
            if (status != FortuneOperationStatus.Succeeded && status != FortuneOperationStatus.RejectedInsufficientOptions &&
                status != FortuneOperationStatus.RejectedNoCharges && status != FortuneOperationStatus.RejectedConflict)
                return false;
            if (string.IsNullOrWhiteSpace(receipt.offerId) || string.IsNullOrWhiteSpace(receipt.contextId) ||
                receipt.optionIds == null || receipt.optionIds.Length != 3 ||
                receipt.optionIds.Any(string.IsNullOrWhiteSpace) ||
                receipt.optionIds.Distinct(StringComparer.Ordinal).Count() != 3 ||
                receipt.remainingCharges < 0 || receipt.remainingCharges > MaximumCharges ||
                (!string.IsNullOrEmpty(receipt.selectedOptionId) &&
                    !receipt.optionIds.Contains(receipt.selectedOptionId, StringComparer.Ordinal)) ||
                (receipt.claimed && string.IsNullOrEmpty(receipt.selectedOptionId)) ||
                (status == FortuneOperationStatus.Succeeded && receipt.rerollCount == 0) ||
                (status == FortuneOperationStatus.RejectedConflict && string.IsNullOrEmpty(receipt.selectedOptionId)))
                return false;
            return true;
        }

        private bool TryBuildRerollOptions(OfferRecord record, out string[] options)
        {
            int nextReroll = record.RerollCount + 1;
            var seen = new HashSet<string>(record.History.Select(entry => CanonicalSetKey(entry)), StringComparer.Ordinal);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string[] sampled = SelectOffer(record.EligibleOptionIds, record.OfferId, record.ContextId,
                    nextReroll, attempt, RerollRandomDomain);
                if (!seen.Contains(CanonicalSetKey(sampled)))
                {
                    options = sampled;
                    return true;
                }
            }

            // Random retries can land on already seen sets. A shuffled exhaustive walk guarantees
            // a new set whenever one exists, while keeping the fallback stable for a restored run.
            string[] candidates = (string[])record.EligibleOptionIds.Clone();
            var fallback = new Xorshift32(DeriveOfferSeed(record, nextReroll, 0, RerollFallbackRandomDomain));
            Shuffle(candidates, fallback);
            for (int first = 0; first < candidates.Length - 2; first++)
            {
                for (int second = first + 1; second < candidates.Length - 1; second++)
                {
                    for (int third = second + 1; third < candidates.Length; third++)
                    {
                        string[] candidate = { candidates[first], candidates[second], candidates[third] };
                        if (seen.Contains(CanonicalSetKey(candidate))) continue;
                        options = candidate;
                        return true;
                    }
                }
            }

            options = null;
            return false;
        }

        private uint DeriveOfferSeed(OfferRecord record, int rerollNumber, int attempt, string domain)
        {
            return SeedDeriver.Derive(GenerationVersion, Seed, domain, record.OfferId, record.ContextId,
                rerollNumber.ToString(CultureInfo.InvariantCulture), attempt.ToString(CultureInfo.InvariantCulture));
        }

        private string[] SelectOffer(string[] eligibleOptions, string offerId, string contextId,
            int rerollNumber, int attempt, string domain)
        {
            string[] shuffled = (string[])eligibleOptions.Clone();
            uint derivedSeed = SeedDeriver.Derive(GenerationVersion, Seed, domain, offerId, contextId,
                rerollNumber.ToString(CultureInfo.InvariantCulture), attempt.ToString(CultureInfo.InvariantCulture));
            Shuffle(shuffled, new Xorshift32(derivedSeed));
            return new[] { shuffled[0], shuffled[1], shuffled[2] };
        }

        private static void Shuffle<T>(T[] values, Xorshift32 random)
        {
            for (int index = values.Length - 1; index > 0; index--)
            {
                int other = random.NextInt(0, index + 1);
                T value = values[index];
                values[index] = values[other];
                values[other] = value;
            }
        }

        private static string[] NormalizeOptionIds(IEnumerable<string> optionIds)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string optionId in optionIds)
            {
                if (!IsStableId(optionId)) throw new ArgumentException("Option IDs must be non-empty stable IDs.", nameof(optionIds));
                unique.Add(optionId);
            }
            return unique.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        }

        private static string[] NormalizeSnapshotIds(IEnumerable<string> optionIds, string description)
        {
            if (optionIds == null) throw new FormatException("Fortune snapshot is missing " + description + ".");
            var result = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string optionId in optionIds)
            {
                if (!IsStableId(optionId) || !unique.Add(optionId))
                    throw new FormatException("Fortune snapshot contains an invalid or repeated ID in " + description + ".");
                result.Add(optionId);
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        private static bool IsStableId(string value) => !string.IsNullOrWhiteSpace(value);

        private static string CanonicalSetKey(IEnumerable<string> optionIds)
        {
            var result = new StringBuilder();
            foreach (string optionId in optionIds.OrderBy(id => id, StringComparer.Ordinal))
            {
                result.Append(optionId.Length.ToString(CultureInfo.InvariantCulture));
                result.Append(':');
                result.Append(optionId);
                result.Append('|');
            }
            return result.ToString();
        }

        private static FortuneOffer ToView(OfferRecord record)
        {
            return new FortuneOffer(record.OfferId, record.ContextId, record.CurrentOptions,
                record.SelectedOptionId, record.RerollCount, record.Claimed);
        }

        private static FortuneOfferSnapshot CreateSnapshot(OfferRecord record)
        {
            return new FortuneOfferSnapshot
            {
                offerId = record.OfferId,
                contextId = record.ContextId,
                eligibleOptionIds = (string[])record.EligibleOptionIds.Clone(),
                history = record.History.Select(options => new FortuneOfferHistorySnapshot
                {
                    optionIds = (string[])options.Clone()
                }).ToArray(),
                selectedOptionId = record.SelectedOptionId,
                claimed = record.Claimed
            };
        }

        private sealed class OfferRecord
        {
            public OfferRecord(string offerId, string contextId, string[] eligibleOptionIds)
            {
                OfferId = offerId;
                ContextId = contextId;
                EligibleOptionIds = (string[])eligibleOptionIds.Clone();
            }

            public string OfferId { get; }
            public string ContextId { get; }
            public string[] EligibleOptionIds { get; }
            public List<string[]> History { get; } = new List<string[]>();
            public string SelectedOptionId { get; set; } = string.Empty;
            public bool Claimed { get; set; }
            public string[] CurrentOptions => History[History.Count - 1];
            public int RerollCount => History.Count - 1;
        }
    }
}
