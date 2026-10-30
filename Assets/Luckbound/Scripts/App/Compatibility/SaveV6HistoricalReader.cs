using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.App.Compatibility
{
    /// <summary>
    /// Decodes the frozen version-6 wire shape. Checksum verification always precedes normalization
    /// and validation, and no live save or Fortune DTO participates in this path.
    /// </summary>
    internal static class SaveV6HistoricalReader
    {
        private const int HistoricalSchemaVersion = 1;
        private const int HistoricalContentVersion = 6;
        private const int MaximumActiveContracts = 3;
        private const int MaximumFortuneCharges = 3;

        internal static DecodeStatus ReadProfile(string json, out LegacyProfileSaveEnvelopeV6 verified)
        {
            verified = null;
            if (string.IsNullOrWhiteSpace(json)) return DecodeStatus.Missing;

            LegacyProfileSaveEnvelopeV6 source;
            try { source = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV6>(json); }
            catch (Exception) { return DecodeStatus.Corrupted; }
            if (source == null) return DecodeStatus.Corrupted;
            if (!IsHistoricalHeader(source.schemaVersion, source.contentVersion))
                return DecodeStatus.Incompatible;

            try
            {
                byte[] preimage = GetProfileChecksumPreimage(source);
                if (!string.Equals(source.checksum, ComputeChecksum(preimage), StringComparison.Ordinal))
                    return DecodeStatus.Corrupted;
            }
            catch (Exception) { return DecodeStatus.Corrupted; }

            try
            {
                if (!NormalizeProfile(source) || !ValidateProfileEnvelope(source))
                    return DecodeStatus.Corrupted;
            }
            catch (Exception) { return DecodeStatus.Corrupted; }

            verified = source;
            return DecodeStatus.Valid;
        }

        internal static DecodeStatus ReadSettings(string json, out LegacySettingsSaveEnvelopeV6 verified)
        {
            verified = null;
            if (string.IsNullOrWhiteSpace(json)) return DecodeStatus.Missing;

            LegacySettingsSaveEnvelopeV6 source;
            try { source = JsonUtility.FromJson<LegacySettingsSaveEnvelopeV6>(json); }
            catch (Exception) { return DecodeStatus.Corrupted; }
            if (source == null) return DecodeStatus.Corrupted;
            if (!IsHistoricalHeader(source.schemaVersion, source.contentVersion))
                return DecodeStatus.Incompatible;

            try
            {
                byte[] preimage = GetSettingsChecksumPreimage(source);
                if (!string.Equals(source.checksum, ComputeChecksum(preimage), StringComparison.Ordinal))
                    return DecodeStatus.Corrupted;
            }
            catch (Exception) { return DecodeStatus.Corrupted; }

            try
            {
                if (!ValidateSettingsEnvelope(source)) return DecodeStatus.Corrupted;
            }
            catch (Exception) { return DecodeStatus.Corrupted; }
            verified = source;
            return DecodeStatus.Valid;
        }

        internal static byte[] GetProfileChecksumPreimage(LegacyProfileSaveEnvelopeV6 source) =>
            SaveV6ChecksumInspector.GetProfileChecksumPreimage(source);

        internal static byte[] GetSettingsChecksumPreimage(LegacySettingsSaveEnvelopeV6 source) =>
            SaveV6ChecksumInspector.GetSettingsChecksumPreimage(source);

        private static bool IsHistoricalHeader(int schemaVersion, int contentVersion) =>
            schemaVersion == HistoricalSchemaVersion && contentVersion == HistoricalContentVersion;

        private static string ComputeChecksum(byte[] preimageUtf8)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(preimageUtf8);
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static bool NormalizeProfile(LegacyProfileSaveEnvelopeV6 envelope)
        {
            if (envelope.hasActiveExpedition)
            {
                if (envelope.activeExpedition == null) return false;
            }
            else
            {
                if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
                envelope.activeExpedition = null;
            }

            LegacyExpeditionSaveDataV6 active = envelope.activeExpedition;
            if (active != null && !active.hasFrozenPermanentUpgrades)
            {
                if (HasFrozenUpgradeValues(active.frozenPermanentUpgrades)) return false;
                active.frozenPermanentUpgrades = null;
            }
            return true;
        }

        // This intentionally matches the frozen v6 marker guard. Health, energy, charges, and
        // accumulated Renown alone do not turn an otherwise empty false-marker payload into an active run.
        private static bool HasActiveExpeditionPayload(LegacyExpeditionSaveDataV6 expedition)
        {
            if (expedition == null) return false;
            LegacyFortuneOfferBookSnapshotV6 fortune = expedition.fortuneOfferBookSnapshot;
            bool hasFortune = fortune != null &&
                (fortune.snapshotVersion != 0 || fortune.generationVersion != 0 || fortune.seed != 0 ||
                 !string.IsNullOrWhiteSpace(fortune.raceId) || fortune.charges != 0 ||
                 HasEntries(fortune.offers) || HasEntries(fortune.processedGuardianIds) ||
                 HasEntries(fortune.rerollReceipts) || HasEntries(fortune.fortuneBrewReceipts));

            return !string.IsNullOrWhiteSpace(expedition.runId) || expedition.seed != 0 ||
                expedition.generationVersion != 0 || expedition.contentVersion != 0 ||
                !string.IsNullOrWhiteSpace(expedition.classId) || !string.IsNullOrWhiteSpace(expedition.raceId) ||
                !string.IsNullOrWhiteSpace(expedition.routeSnapshotJson) ||
                !string.IsNullOrWhiteSpace(expedition.currentNodeId) ||
                !string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson) ||
                !string.IsNullOrWhiteSpace(expedition.inventorySnapshotJson) ||
                HasEntries(expedition.selectedSkillIds) || HasEntries(expedition.startingEquipmentInstanceIds) ||
                HasEntries(expedition.visitedNodeIds) || HasEntries(expedition.completedRoomIds) ||
                HasEntries(expedition.completedRoomResults) || HasEntries(expedition.pactIds) ||
                HasEntries(expedition.collectedItemIds) || HasEntries(expedition.claimedRewardIds) ||
                expedition.hasFrozenPermanentUpgrades || HasFrozenUpgradeValues(expedition.frozenPermanentUpgrades) ||
                hasFortune;
        }

        private static bool HasFrozenUpgradeValues(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 snapshot) =>
            snapshot != null && (snapshot.healthUpgradeLevel != 0 || snapshot.energyUpgradeLevel != 0 ||
                snapshot.additionalPotionUpgradeLevel != 0);

        private static bool HasEntries<T>(T[] values) => values != null && values.Length != 0;

        private static bool ValidateProfileEnvelope(LegacyProfileSaveEnvelopeV6 envelope)
        {
            if (!ValidateCommon(envelope.schemaVersion, envelope.contentVersion, envelope.generation,
                    envelope.sequence, envelope.savedAtUtc, envelope.checksum) ||
                envelope.hasActiveExpedition != (envelope.activeExpedition != null) ||
                !ValidateProfile(envelope.profile))
                return false;
            return ValidateExpedition(envelope.activeExpedition);
        }

        private static bool ValidateSettingsEnvelope(LegacySettingsSaveEnvelopeV6 envelope) =>
            ValidateCommon(envelope.schemaVersion, envelope.contentVersion, envelope.generation,
                envelope.sequence, envelope.savedAtUtc, envelope.checksum) && ValidateSettings(envelope.settings);

        private static bool ValidateCommon(int schemaVersion, int contentVersion, int generation, long sequence,
            string savedAtUtc, string checksum)
        {
            if (schemaVersion != HistoricalSchemaVersion || contentVersion != HistoricalContentVersion ||
                generation < 1 || sequence < 1 ||
                !DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) ||
                !IsSha256(checksum))
                return false;
            return true;
        }

        private static bool ValidateProfile(LegacyProfileSaveDataV6 profile)
        {
            if (profile == null || profile.availableRenown < 0 ||
                profile.healthUpgradeLevel < 0 || profile.healthUpgradeLevel > 2 ||
                profile.energyUpgradeLevel < 0 || profile.energyUpgradeLevel > 2 ||
                profile.additionalPotionUpgradeLevel < 0 || profile.additionalPotionUpgradeLevel > 1 ||
                !ValidateIds(profile.unlockedRaceIds) || !ValidateIds(profile.unlockedStartingKitIds) ||
                !ValidateIds(profile.acceptedContractIds) || !ValidateIds(profile.claimedContractIds) ||
                !ValidateIds(profile.readStoryFragmentIds) || !ValidateIds(profile.processedSettlementRunIds))
                return false;

            foreach (string runId in profile.processedSettlementRunIds)
                if (!RunId.TryParse(runId, out _)) return false;

            if (profile.acceptedContractIds.All(value => !string.IsNullOrWhiteSpace(value)) &&
                profile.claimedContractIds.All(value => !string.IsNullOrWhiteSpace(value)))
            {
                var accepted = new HashSet<string>(profile.acceptedContractIds, StringComparer.Ordinal);
                var claimed = new HashSet<string>(profile.claimedContractIds, StringComparer.Ordinal);
                if (claimed.Any(contractId => !accepted.Contains(contractId)) ||
                    accepted.Except(claimed).Count() > MaximumActiveContracts)
                    return false;
            }

            if (profile.contractCounters == null) return false;
            var counterIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyContractCounterSaveDataV6 counter in profile.contractCounters)
            {
                if (counter == null || string.IsNullOrWhiteSpace(counter.contractId) ||
                    !counterIds.Add(counter.contractId) || counter.progress < 0)
                    return false;
            }

            if (profile.processedContractProgressEvents == null) return false;
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyContractProgressEventReceiptSaveDataV6 receipt in profile.processedContractProgressEvents)
            {
                if (receipt == null || !IsValidProgressEventId(receipt.eventId) || !eventIds.Add(receipt.eventId) ||
                    receipt.progressEvent < 1 || receipt.progressEvent > 12)
                    return false;
            }

            if (profile.progressionPurchaseTransactions == null) return false;
            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyPermanentUpgradePurchaseSaveDataV6 transaction in profile.progressionPurchaseTransactions)
            {
                if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out _) ||
                    !transactionIds.Add(transaction.transactionId) || transaction.kind < 0 || transaction.kind > 2 ||
                    transaction.cost <= 0)
                    return false;
            }

            return true;
        }

        private static bool ValidateExpedition(LegacyExpeditionSaveDataV6 expedition)
        {
            if (expedition == null) return true;
            if (!RunId.TryParse(expedition.runId, out _) || expedition.seed == 0 ||
                expedition.generationVersion < 1 || expedition.contentVersion < 1 ||
                string.IsNullOrWhiteSpace(expedition.classId) || string.IsNullOrWhiteSpace(expedition.raceId) ||
                !ValidateIds(expedition.selectedSkillIds, allowEmpty: false) || expedition.selectedSkillIds.Length != 2 ||
                !ValidateIds(expedition.startingEquipmentInstanceIds, allowEmpty: false) ||
                expedition.startingEquipmentInstanceIds.Length != 2)
                return false;

            foreach (string itemInstanceId in expedition.startingEquipmentInstanceIds)
                if (!ItemInstanceId.TryParse(itemInstanceId, out _)) return false;

            ExpeditionRoutePlan route;
            try
            {
                if (string.IsNullOrWhiteSpace(expedition.routeSnapshotJson) ||
                    !HasOnlyFrozenRouteNodeKinds(expedition.routeSnapshotJson))
                    return false;
                route = ExpeditionRoutePlan.ParseCanonical(expedition.routeSnapshotJson);
            }
            catch (Exception) { return false; }

            if (string.IsNullOrWhiteSpace(expedition.currentNodeId) ||
                !ValidateIds(expedition.visitedNodeIds, allowEmpty: false) ||
                expedition.visitedNodeIds.Length == 0 ||
                !string.Equals(expedition.visitedNodeIds[expedition.visitedNodeIds.Length - 1],
                    expedition.currentNodeId, StringComparison.Ordinal) ||
                route.Seed != expedition.seed || route.GenerationVersion != expedition.generationVersion)
                return false;

            try
            {
                var traversal = new ExpeditionRouteTraversal(route, expedition.visitedNodeIds);
                if (!string.Equals(traversal.CurrentNodeId, expedition.currentNodeId, StringComparison.Ordinal))
                    return false;
            }
            catch (Exception) { return false; }

            if (!ValidateIds(expedition.completedRoomIds)) return false;
            var routeNodeIds = new HashSet<string>(route.Nodes.Select(node => node.NodeId), StringComparer.Ordinal);
            if (expedition.completedRoomIds.Any(id => !routeNodeIds.Contains(id)) ||
                expedition.completedRoomIds.Any(id => !expedition.visitedNodeIds.Contains(id)))
                return false;

            if (expedition.completedRoomResults == null) return false;
            var resultRooms = new HashSet<string>(StringComparer.Ordinal);
            var resultIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyExpeditionRoomCompletionSaveDataV6 result in expedition.completedRoomResults)
            {
                if (result == null || string.IsNullOrWhiteSpace(result.roomNodeId) ||
                    string.IsNullOrWhiteSpace(result.resultId) || string.IsNullOrWhiteSpace(result.resultJson) ||
                    !resultRooms.Add(result.roomNodeId) || !resultIds.Add(result.resultId) ||
                    !expedition.completedRoomIds.Contains(result.roomNodeId))
                    return false;
            }
            if (resultRooms.Count != expedition.completedRoomIds.Length) return false;
            bool currentRoomCompleted = expedition.completedRoomIds.Contains(expedition.currentNodeId);
            if (currentRoomCompleted && !string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson)) return false;
            if (!currentRoomCompleted && string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson)) return false;

            if (string.IsNullOrWhiteSpace(expedition.inventorySnapshotJson) || expedition.accumulatedRenown < 0 ||
                !ValidateFortuneBook(expedition.fortuneOfferBookSnapshot, expedition.seed,
                    expedition.generationVersion, expedition.raceId) ||
                expedition.maximumHealth < 1 || expedition.currentHealth < 0 ||
                expedition.currentHealth > expedition.maximumHealth || expedition.maximumEnergy < 1 ||
                expedition.currentEnergy < 0 || expedition.currentEnergy > expedition.maximumEnergy ||
                expedition.healthPotionCharges < 0)
                return false;

            LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 frozen = expedition.frozenPermanentUpgrades;
            if (expedition.hasFrozenPermanentUpgrades && frozen == null) return false;
            if (expedition.hasFrozenPermanentUpgrades &&
                (frozen.healthUpgradeLevel < 0 || frozen.healthUpgradeLevel > 2 ||
                 frozen.energyUpgradeLevel < 0 || frozen.energyUpgradeLevel > 2 ||
                 frozen.additionalPotionUpgradeLevel < 0 || frozen.additionalPotionUpgradeLevel > 1))
                return false;
            if (!expedition.hasFrozenPermanentUpgrades && HasFrozenUpgradeValues(frozen)) return false;

            return ValidateIds(expedition.pactIds) && ValidateIds(expedition.collectedItemIds) &&
                ValidateIds(expedition.claimedRewardIds) && ValidateRoomEntryCheckpoint(expedition);
        }

        private static bool ValidateRoomEntryCheckpoint(LegacyExpeditionSaveDataV6 expedition)
        {
            if (!ValidateFortuneBook(expedition.fortuneOfferBookSnapshot, expedition.seed,
                    expedition.generationVersion, expedition.raceId))
                return false;
            if (string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson)) return true;

            LegacyExpeditionRoomCheckpointSaveDataV6 checkpoint;
            try { checkpoint = JsonUtility.FromJson<LegacyExpeditionRoomCheckpointSaveDataV6>(expedition.roomEntryCheckpointJson); }
            catch (Exception) { return false; }
            if (checkpoint == null || !string.Equals(checkpoint.roomNodeId, expedition.currentNodeId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(checkpoint.inventorySnapshotJson) || checkpoint.maximumHealth < 1 ||
                checkpoint.currentHealth < 0 || checkpoint.currentHealth > checkpoint.maximumHealth ||
                checkpoint.maximumEnergy < 0 || checkpoint.currentEnergy < 0 ||
                checkpoint.currentEnergy > checkpoint.maximumEnergy || checkpoint.healthPotionCharges < 0 ||
                checkpoint.accumulatedRenown < 0 || !ValidateIds(checkpoint.pactIds) ||
                !ValidateIds(checkpoint.collectedItemIds) || !ValidateIds(checkpoint.claimedRewardIds))
                return false;

            return ValidateFortuneBook(checkpoint.fortuneOfferBookSnapshot, expedition.seed,
                expedition.generationVersion, expedition.raceId);
        }

        private static bool ValidateFortuneBook(LegacyFortuneOfferBookSnapshotV6 snapshot, uint seed,
            int generationVersion, string raceId)
        {
            if (snapshot == null || (snapshot.snapshotVersion != 1 && snapshot.snapshotVersion != 2 &&
                    snapshot.snapshotVersion != 3) || snapshot.generationVersion <= 0 || snapshot.seed == 0 ||
                string.IsNullOrWhiteSpace(snapshot.raceId) || snapshot.charges < 0 ||
                snapshot.charges > MaximumFortuneCharges || snapshot.offers == null ||
                snapshot.processedGuardianIds == null ||
                (snapshot.snapshotVersion >= 2 && snapshot.rerollReceipts == null) ||
                (snapshot.snapshotVersion == 3 && snapshot.fortuneBrewReceipts == null) ||
                snapshot.seed != seed || snapshot.generationVersion != generationVersion ||
                !string.Equals(snapshot.raceId, raceId, StringComparison.Ordinal))
                return false;

            var guardianIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guardianId in snapshot.processedGuardianIds)
                if (string.IsNullOrWhiteSpace(guardianId) || !guardianIds.Add(guardianId)) return false;

            var offerIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyFortuneOfferSnapshotV6 offer in snapshot.offers)
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.offerId) ||
                    string.IsNullOrWhiteSpace(offer.contextId) || !offerIds.Add(offer.offerId) ||
                    offer.eligibleOptionIds == null || offer.history == null || offer.history.Length == 0)
                    return false;

                string[] eligible;
                if (!TryNormalizeUniqueIds(offer.eligibleOptionIds, out eligible) || eligible.Length < 3 ||
                    !eligible.SequenceEqual(offer.eligibleOptionIds, StringComparer.Ordinal))
                    return false;

                var historyKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (LegacyFortuneOfferHistorySnapshotV6 history in offer.history)
                {
                    string[] options;
                    if (history == null || !TryNormalizeUniqueIds(history.optionIds, out options) || options.Length != 3 ||
                        options.Any(id => Array.BinarySearch(eligible, id, StringComparer.Ordinal) < 0) ||
                        !historyKeys.Add(CanonicalSetKey(options)))
                        return false;
                }

                string selected = offer.selectedOptionId ?? string.Empty;
                string[] currentOptions;
                if (!TryNormalizeUniqueIds(offer.history[offer.history.Length - 1].optionIds, out currentOptions))
                    return false;
                if (!string.IsNullOrEmpty(selected) && !currentOptions.Contains(selected, StringComparer.Ordinal))
                    return false;
                if (offer.claimed && string.IsNullOrEmpty(selected)) return false;
            }

            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            LegacyFortuneRerollReceiptSnapshotV6[] rerollReceipts =
                snapshot.rerollReceipts ?? Array.Empty<LegacyFortuneRerollReceiptSnapshotV6>();
            foreach (LegacyFortuneRerollReceiptSnapshotV6 receipt in rerollReceipts)
            {
                if (!IsValidRerollReceipt(receipt) || !transactionIds.Add(receipt.transactionId) ||
                    receipt.hasOffer && !offerIds.Contains(receipt.offerId))
                    return false;
            }

            LegacyFortuneBrewReceiptSnapshotV6[] brewReceipts =
                snapshot.fortuneBrewReceipts ?? Array.Empty<LegacyFortuneBrewReceiptSnapshotV6>();
            foreach (LegacyFortuneBrewReceiptSnapshotV6 receipt in brewReceipts)
                if (!IsValidBrewReceipt(receipt) || !transactionIds.Add(receipt.transactionId)) return false;

            return true;
        }

        private static bool IsValidRerollReceipt(LegacyFortuneRerollReceiptSnapshotV6 receipt)
        {
            if (receipt == null || !IsCanonicalTransactionId(receipt.transactionId) || receipt.offerId == null ||
                receipt.remainingCharges < 0 || receipt.remainingCharges > MaximumFortuneCharges ||
                receipt.rerollCount < 0 || !IsKnownFortuneStatus(receipt.status))
                return false;

            // Frozen FortuneOperationStatus values from the version-6 reader contract.
            if (receipt.status == 1 || receipt.status == 2 || receipt.status == 5 ||
                receipt.status == 7 || receipt.status == 9)
                return false;
            if (!receipt.hasOffer)
                return receipt.status == 6 && receipt.optionIds != null && receipt.optionIds.Length == 0;
            if (receipt.status != 0 && receipt.status != 3 && receipt.status != 4 && receipt.status != 8)
                return false;
            if (string.IsNullOrWhiteSpace(receipt.offerId) || string.IsNullOrWhiteSpace(receipt.contextId) ||
                receipt.optionIds == null || receipt.optionIds.Length != 3 ||
                receipt.optionIds.Any(string.IsNullOrWhiteSpace) ||
                receipt.optionIds.Distinct(StringComparer.Ordinal).Count() != 3 ||
                (!string.IsNullOrEmpty(receipt.selectedOptionId) &&
                 !receipt.optionIds.Contains(receipt.selectedOptionId, StringComparer.Ordinal)) ||
                (receipt.claimed && string.IsNullOrEmpty(receipt.selectedOptionId)) ||
                (receipt.status == 0 && receipt.rerollCount == 0) ||
                (receipt.status == 8 && string.IsNullOrEmpty(receipt.selectedOptionId)))
                return false;
            return true;
        }

        private static bool IsValidBrewReceipt(LegacyFortuneBrewReceiptSnapshotV6 receipt)
        {
            if (receipt == null || !IsCanonicalTransactionId(receipt.transactionId) ||
                !IsCanonicalItemInstanceId(receipt.itemInstanceId) || receipt.currentCharges < 0 ||
                receipt.currentCharges > MaximumFortuneCharges)
                return false;
            if (receipt.status == 0) return receipt.currentCharges > 0;
            return receipt.status == 5 && receipt.currentCharges == MaximumFortuneCharges;
        }

        private static bool IsKnownFortuneStatus(int status) => status >= 0 && status <= 9;

        private static bool IsCanonicalTransactionId(string value) =>
            TransactionId.TryParse(value, out TransactionId parsed) &&
            string.Equals(parsed.ToString(), value, StringComparison.Ordinal);

        private static bool IsCanonicalItemInstanceId(string value) =>
            ItemInstanceId.TryParse(value, out ItemInstanceId parsed) &&
            string.Equals(parsed.ToString(), value, StringComparison.Ordinal);

        private static bool ValidateSettings(LegacyGameSettingsSaveDataV6 settings)
        {
            return settings != null && (settings.languageId == "es" || settings.languageId == "en") &&
                IsVolume(settings.masterVolume) && IsVolume(settings.musicVolume) &&
                IsVolume(settings.effectsVolume) && IsVolume(settings.ambienceVolume);
        }

        private static bool ValidateIds(string[] values, bool allowEmpty = true)
        {
            if (values == null || (!allowEmpty && values.Length == 0) || values.Any(string.IsNullOrWhiteSpace))
                return false;
            return values.Distinct(StringComparer.Ordinal).Count() == values.Length;
        }

        private static bool TryNormalizeUniqueIds(string[] values, out string[] sorted)
        {
            sorted = null;
            if (values == null || values.Any(string.IsNullOrWhiteSpace) ||
                values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                return false;
            sorted = values.OrderBy(id => id, StringComparer.Ordinal).ToArray();
            return true;
        }

        private static string CanonicalSetKey(IEnumerable<string> ids)
        {
            var key = new StringBuilder();
            foreach (string id in ids.OrderBy(value => value, StringComparer.Ordinal))
            {
                key.Append(id.Length.ToString(CultureInfo.InvariantCulture));
                key.Append(':');
                key.Append(id);
                key.Append('|');
            }
            return key.ToString();
        }

        private static bool IsValidProgressEventId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 160) return false;
            foreach (char character in value)
            {
                bool valid = character >= 'A' && character <= 'Z' || character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9' || character == '.' || character == '_' ||
                    character == ':' || character == '/' || character == '-';
                if (!valid) return false;
            }
            return true;
        }

        private static bool HasOnlyFrozenRouteNodeKinds(string canonicalRoute)
        {
            string[] lines = canonicalRoute.Split(new[] { '\n' }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                string[] fields = line.Split('|');
                if (fields.Length != 7 || fields[0] != "N") continue;
                string kind = fields[3];
                if (kind != "Combat" && kind != "Exploration" && kind != "Treasure" && kind != "Pact" &&
                    kind != "Narrative" && kind != "Guardian" && kind != "Rest" && kind != "FinalBoss" &&
                    kind != "OptionalCombat" && kind != "Tutorial" && kind != "SafeExtraction")
                    return false;
            }
            return true;
        }

        private static bool IsVolume(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char character in value)
                if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f'))
                    return false;
            return true;
        }
    }
}
