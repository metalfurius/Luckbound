using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.App.Compatibility
{
    internal static class SaveV5HistoricalReader
    {
        private const int HistoricalSchemaVersion = 1;
        private const int HistoricalContentVersion = 5;
        private const int MaximumActiveContracts = 3;
        private const int MaximumHealthUpgradeLevel = 2;
        private const int MaximumEnergyUpgradeLevel = 2;
        private const int MaximumPotionUpgradeLevel = 1;
        private const int MaximumFortuneCharges = 3;

        private const int FortuneSucceeded = 0;
        private const int FortuneAlreadyApplied = 1;
        private const int FortuneRejectedInvalidRequest = 2;
        private const int FortuneRejectedInsufficientOptions = 3;
        private const int FortuneRejectedNoCharges = 4;
        private const int FortuneRejectedAtCapacity = 5;
        private const int FortuneRejectedUnknownOffer = 6;
        private const int FortuneRejectedInvalidChoice = 7;
        private const int FortuneRejectedConflict = 8;
        private const int FortuneRejectedNoChoice = 9;

        private const int FortuneLegacySnapshotVersion = 1;
        private const int FortuneRerollReceiptSnapshotVersion = 2;
        private const int FortuneSnapshotVersion = 3;

        [Serializable]
        private sealed class HistoricalSaveVersionHeader
        {
            public int schemaVersion;
            public int contentVersion;
        }

        internal static DecodeStatus ReadProfile(string json, out LegacyProfileSaveEnvelopeV5 verified)
        {
            verified = null;
            if (string.IsNullOrWhiteSpace(json)) return global::Luckbound.App.DecodeStatus.Missing;

            HistoricalSaveVersionHeader header;
            try { header = JsonUtility.FromJson<HistoricalSaveVersionHeader>(json); }
            catch (Exception) { return global::Luckbound.App.DecodeStatus.Corrupted; }
            if (header == null) return global::Luckbound.App.DecodeStatus.Corrupted;
            if (!IsHistoricalVersion(header.schemaVersion, header.contentVersion))
                return global::Luckbound.App.DecodeStatus.Incompatible;

            LegacyProfileSaveEnvelopeV5 source;
            try { source = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV5>(json); }
            catch (Exception) { return global::Luckbound.App.DecodeStatus.Corrupted; }
            if (source == null) return global::Luckbound.App.DecodeStatus.Corrupted;
            if (!IsHistoricalVersion(source.schemaVersion, source.contentVersion))
                return global::Luckbound.App.DecodeStatus.Incompatible;

            try
            {
                byte[] preimage = GetProfileChecksumPreimage(source);
                if (!string.Equals(source.checksum, ComputeChecksum(preimage), StringComparison.Ordinal))
                    return global::Luckbound.App.DecodeStatus.Corrupted;
                if (!ValidateProfileEnvelope(source)) return global::Luckbound.App.DecodeStatus.Corrupted;
            }
            catch (Exception)
            {
                return global::Luckbound.App.DecodeStatus.Corrupted;
            }

            verified = source;
            return global::Luckbound.App.DecodeStatus.Valid;
        }

        internal static DecodeStatus ReadSettings(string json, out LegacySettingsSaveEnvelopeV5 verified)
        {
            verified = null;
            if (string.IsNullOrWhiteSpace(json)) return global::Luckbound.App.DecodeStatus.Missing;

            HistoricalSaveVersionHeader header;
            try { header = JsonUtility.FromJson<HistoricalSaveVersionHeader>(json); }
            catch (Exception) { return global::Luckbound.App.DecodeStatus.Corrupted; }
            if (header == null) return global::Luckbound.App.DecodeStatus.Corrupted;
            if (!IsHistoricalVersion(header.schemaVersion, header.contentVersion))
                return global::Luckbound.App.DecodeStatus.Incompatible;

            LegacySettingsSaveEnvelopeV5 source;
            try { source = JsonUtility.FromJson<LegacySettingsSaveEnvelopeV5>(json); }
            catch (Exception) { return global::Luckbound.App.DecodeStatus.Corrupted; }
            if (source == null) return global::Luckbound.App.DecodeStatus.Corrupted;
            if (!IsHistoricalVersion(source.schemaVersion, source.contentVersion))
                return global::Luckbound.App.DecodeStatus.Incompatible;

            try
            {
                byte[] preimage = GetSettingsChecksumPreimage(source);
                if (!string.Equals(source.checksum, ComputeChecksum(preimage), StringComparison.Ordinal) ||
                    !ValidateSettingsEnvelope(source))
                    return global::Luckbound.App.DecodeStatus.Corrupted;
            }
            catch (Exception)
            {
                return global::Luckbound.App.DecodeStatus.Corrupted;
            }

            verified = source;
            return global::Luckbound.App.DecodeStatus.Valid;
        }

        internal static byte[] GetProfileChecksumPreimage(LegacyProfileSaveEnvelopeV5 source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureHistoricalVersion(source.schemaVersion, source.contentVersion);

            var canonical = new LegacyProfileSaveEnvelopeV5
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                profile = source.profile,
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = source.hasActiveExpedition && source.activeExpedition != null
                    ? CloneExpeditionForChecksum(source.activeExpedition)
                    : null
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(canonical, false));
        }

        internal static byte[] GetSettingsChecksumPreimage(LegacySettingsSaveEnvelopeV5 source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureHistoricalVersion(source.schemaVersion, source.contentVersion);

            var canonical = new LegacySettingsSaveEnvelopeV5
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                settings = source.settings
            };
            return Encoding.UTF8.GetBytes(JsonUtility.ToJson(canonical, false));
        }

        private static bool IsHistoricalVersion(int schemaVersion, int contentVersion) =>
            schemaVersion == HistoricalSchemaVersion && contentVersion == HistoricalContentVersion;

        private static void EnsureHistoricalVersion(int schemaVersion, int contentVersion)
        {
            if (!IsHistoricalVersion(schemaVersion, contentVersion))
                throw new ArgumentException("The frozen V5 projection requires schema 1 and content 5.");
        }

        private static string ComputeChecksum(byte[] preimage)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(preimage);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static LegacyExpeditionSaveDataV5 CloneExpeditionForChecksum(LegacyExpeditionSaveDataV5 source)
        {
            return new LegacyExpeditionSaveDataV5
            {
                runId = source.runId,
                seed = source.seed,
                generationVersion = source.generationVersion,
                contentVersion = source.contentVersion,
                classId = source.classId,
                raceId = source.raceId,
                selectedSkillIds = Copy(source.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(source.startingEquipmentInstanceIds),
                routeSnapshotJson = source.routeSnapshotJson,
                currentNodeId = source.currentNodeId,
                visitedNodeIds = Copy(source.visitedNodeIds),
                completedRoomIds = Copy(source.completedRoomIds),
                completedRoomResults = CloneRoomResults(source.completedRoomResults),
                roomEntryCheckpointJson = source.roomEntryCheckpointJson,
                inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges,
                frozenPermanentUpgrades = source.frozenPermanentUpgrades == null ||
                    (!source.hasFrozenPermanentUpgrades && !HasFrozenUpgradeValues(source.frozenPermanentUpgrades))
                    ? null
                    : CloneFrozenUpgrades(source.frozenPermanentUpgrades),
                hasFrozenPermanentUpgrades = source.hasFrozenPermanentUpgrades,
                accumulatedRenown = source.accumulatedRenown,
                pactIds = Copy(source.pactIds),
                fortuneOfferBookSnapshot = CloneFortuneBook(source.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(source.collectedItemIds),
                claimedRewardIds = Copy(source.claimedRewardIds)
            };
        }

        private static LegacyExpeditionRoomCompletionSaveDataV5[] CloneRoomResults(
            LegacyExpeditionRoomCompletionSaveDataV5[] source)
        {
            if (source == null) return null;
            return source.Select(result => result == null ? null : new LegacyExpeditionRoomCompletionSaveDataV5
            {
                roomNodeId = result.roomNodeId,
                resultId = result.resultId,
                resultJson = result.resultJson
            }).ToArray();
        }

        private static LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5 CloneFrozenUpgrades(
            LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5 source)
        {
            return new LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5
            {
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel
            };
        }

        private static LegacyFortuneOfferBookSnapshotV5 CloneFortuneBook(LegacyFortuneOfferBookSnapshotV5 source)
        {
            if (source == null) return null;
            return new LegacyFortuneOfferBookSnapshotV5
            {
                snapshotVersion = source.snapshotVersion,
                generationVersion = source.generationVersion,
                seed = source.seed,
                raceId = source.raceId,
                charges = source.charges,
                offers = source.offers == null ? null : source.offers.Select(offer => offer == null ? null :
                    new LegacyFortuneOfferSnapshotV5
                    {
                        offerId = offer.offerId,
                        contextId = offer.contextId,
                        eligibleOptionIds = Copy(offer.eligibleOptionIds),
                        history = offer.history == null ? null : offer.history.Select(entry => entry == null ? null :
                            new LegacyFortuneOfferHistorySnapshotV5
                            {
                                optionIds = Copy(entry.optionIds)
                            }).ToArray(),
                        selectedOptionId = offer.selectedOptionId,
                        claimed = offer.claimed
                    }).ToArray(),
                processedGuardianIds = Copy(source.processedGuardianIds),
                rerollReceipts = source.rerollReceipts == null ? null : source.rerollReceipts
                    .Select(receipt => receipt == null ? null : new LegacyFortuneRerollReceiptSnapshotV5
                    {
                        transactionId = receipt.transactionId,
                        offerId = receipt.offerId,
                        status = receipt.status,
                        contextId = receipt.contextId,
                        optionIds = Copy(receipt.optionIds),
                        selectedOptionId = receipt.selectedOptionId,
                        rerollCount = receipt.rerollCount,
                        claimed = receipt.claimed,
                        remainingCharges = receipt.remainingCharges,
                        hasOffer = receipt.hasOffer
                    }).ToArray(),
                fortuneBrewReceipts = source.fortuneBrewReceipts == null ? null : source.fortuneBrewReceipts
                    .Select(receipt => receipt == null ? null : new LegacyFortuneBrewReceiptSnapshotV5
                    {
                        transactionId = receipt.transactionId,
                        itemInstanceId = receipt.itemInstanceId,
                        status = receipt.status,
                        currentCharges = receipt.currentCharges
                    }).ToArray()
            };
        }

        private static string[] Copy(string[] values) => values == null ? null : (string[])values.Clone();

        private static bool HasFrozenUpgradeValues(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5 snapshot) =>
            snapshot != null && (snapshot.healthUpgradeLevel != 0 || snapshot.energyUpgradeLevel != 0 ||
                snapshot.additionalPotionUpgradeLevel != 0);

        private static bool ValidateProfileEnvelope(LegacyProfileSaveEnvelopeV5 envelope)
        {
            if (envelope == null || !NormalizeProfileEnvelope(envelope)) return false;
            if (!ValidateCommon(envelope.schemaVersion, envelope.contentVersion, envelope.generation,
                    envelope.sequence, envelope.savedAtUtc, envelope.checksum) ||
                !ValidateProfile(envelope.profile) || !ValidateExpedition(envelope.activeExpedition))
                return false;
            return envelope.activeExpedition == null || ValidateRoomEntryCheckpoint(envelope.activeExpedition);
        }

        private static bool ValidateSettingsEnvelope(LegacySettingsSaveEnvelopeV5 envelope)
        {
            return envelope != null &&
                ValidateCommon(envelope.schemaVersion, envelope.contentVersion, envelope.generation,
                    envelope.sequence, envelope.savedAtUtc, envelope.checksum) &&
                ValidateSettings(envelope.settings);
        }

        private static bool ValidateCommon(int schemaVersion, int contentVersion, int generation, long sequence,
            string savedAtUtc, string checksum)
        {
            return schemaVersion == HistoricalSchemaVersion && contentVersion == HistoricalContentVersion &&
                generation >= 1 && sequence >= 1 &&
                DateTime.TryParse(savedAtUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _) &&
                IsSha256(checksum);
        }

        private static bool ValidateProfile(LegacyProfileSaveDataV5 profile)
        {
            if (profile == null || profile.availableRenown < 0 ||
                profile.healthUpgradeLevel < 0 || profile.healthUpgradeLevel > MaximumHealthUpgradeLevel ||
                profile.energyUpgradeLevel < 0 || profile.energyUpgradeLevel > MaximumEnergyUpgradeLevel ||
                profile.additionalPotionUpgradeLevel < 0 || profile.additionalPotionUpgradeLevel > MaximumPotionUpgradeLevel ||
                !ValidateIds(profile.unlockedRaceIds) || !ValidateIds(profile.unlockedStartingKitIds) ||
                !ValidateIds(profile.acceptedContractIds) || !ValidateIds(profile.claimedContractIds) ||
                !ValidateIds(profile.readStoryFragmentIds) || !ValidateIds(profile.processedSettlementRunIds))
                return false;

            if (profile.acceptedContractIds.All(IsStableId) && profile.claimedContractIds.All(IsStableId))
            {
                var acceptedContracts = new HashSet<string>(profile.acceptedContractIds, StringComparer.Ordinal);
                var claimedContracts = new HashSet<string>(profile.claimedContractIds, StringComparer.Ordinal);
                if (claimedContracts.Any(contractId => !acceptedContracts.Contains(contractId))) return false;
                int activeContractCount = acceptedContracts.Except(claimedContracts).Count();
                if (activeContractCount > MaximumActiveContracts) return false;
            }

            if (profile.processedSettlementRunIds.Any(value => !RunId.TryParse(value, out _))) return false;
            if (profile.progressionPurchaseTransactions == null) return false;
            var transactionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyPermanentUpgradePurchaseSaveDataV5 transaction in profile.progressionPurchaseTransactions)
            {
                if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out _) ||
                    !transactionIds.Add(transaction.transactionId) || transaction.kind < 0 || transaction.kind > 2 ||
                    transaction.cost <= 0)
                    return false;
            }

            if (profile.contractCounters == null) return false;
            var contractIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyContractCounterSaveDataV5 counter in profile.contractCounters)
            {
                if (counter == null || !IsStableId(counter.contractId) || !contractIds.Add(counter.contractId) ||
                    counter.progress < 0)
                    return false;
            }
            return true;
        }

        private static bool ValidateExpedition(LegacyExpeditionSaveDataV5 expedition)
        {
            if (expedition == null) return true;
            if (!RunId.TryParse(expedition.runId, out _) || expedition.seed == 0 ||
                expedition.generationVersion < 1 || expedition.contentVersion < 1 ||
                !IsStableId(expedition.classId) || !IsStableId(expedition.raceId) ||
                !ValidateIds(expedition.selectedSkillIds, allowEmpty: false) || expedition.selectedSkillIds.Length != 2 ||
                !ValidateIds(expedition.startingEquipmentInstanceIds, allowEmpty: false) ||
                expedition.startingEquipmentInstanceIds.Length != 2 ||
                expedition.startingEquipmentInstanceIds.Any(value => !ItemInstanceId.TryParse(value, out _)) ||
                !IsStableId(expedition.currentNodeId) ||
                !ValidateIds(expedition.visitedNodeIds, allowEmpty: false) ||
                !string.Equals(expedition.visitedNodeIds[expedition.visitedNodeIds.Length - 1],
                    expedition.currentNodeId, StringComparison.Ordinal) ||
                !ValidateIds(expedition.completedRoomIds) ||
                !ValidateRoomResults(expedition) ||
                !IsStableId(expedition.inventorySnapshotJson) || expedition.accumulatedRenown < 0 ||
                !ValidateFortuneBook(expedition.fortuneOfferBookSnapshot, expedition.seed,
                    expedition.generationVersion, expedition.raceId) ||
                expedition.maximumHealth < 1 || expedition.currentHealth < 0 ||
                expedition.currentHealth > expedition.maximumHealth ||
                expedition.maximumEnergy < 1 || expedition.currentEnergy < 0 ||
                expedition.currentEnergy > expedition.maximumEnergy || expedition.healthPotionCharges < 0 ||
                !ValidateFrozenUpgrades(expedition) || !ValidateIds(expedition.pactIds) ||
                !ValidateIds(expedition.collectedItemIds) || !ValidateIds(expedition.claimedRewardIds))
                return false;

            ExpeditionRoutePlan route;
            try { route = ExpeditionRoutePlan.ParseCanonical(expedition.routeSnapshotJson); }
            catch (Exception) { return false; }
            if (route.Seed != expedition.seed || route.GenerationVersion != expedition.generationVersion) return false;
            try
            {
                var traversal = new ExpeditionRouteTraversal(route, expedition.visitedNodeIds);
                if (!string.Equals(traversal.CurrentNodeId, expedition.currentNodeId, StringComparison.Ordinal)) return false;
            }
            catch (Exception) { return false; }

            var routeNodeIds = new HashSet<string>(route.Nodes.Select(node => node.NodeId), StringComparer.Ordinal);
            if (expedition.completedRoomIds.Any(id => !routeNodeIds.Contains(id)) ||
                expedition.completedRoomIds.Any(id => !expedition.visitedNodeIds.Contains(id, StringComparer.Ordinal)))
                return false;
            return true;
        }

        private static bool ValidateFrozenUpgrades(LegacyExpeditionSaveDataV5 expedition)
        {
            LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5 upgrades = expedition.frozenPermanentUpgrades;
            if (expedition.hasFrozenPermanentUpgrades && upgrades == null) return false;
            if (expedition.hasFrozenPermanentUpgrades && upgrades != null)
            {
                if (upgrades.healthUpgradeLevel < 0 || upgrades.healthUpgradeLevel > MaximumHealthUpgradeLevel ||
                    upgrades.energyUpgradeLevel < 0 || upgrades.energyUpgradeLevel > MaximumEnergyUpgradeLevel ||
                    upgrades.additionalPotionUpgradeLevel < 0 ||
                    upgrades.additionalPotionUpgradeLevel > MaximumPotionUpgradeLevel)
                    return false;
            }
            return expedition.hasFrozenPermanentUpgrades || !HasFrozenUpgradeValues(upgrades);
        }

        private static bool ValidateRoomResults(LegacyExpeditionSaveDataV5 expedition)
        {
            if (expedition.completedRoomResults == null) return false;
            var resultRooms = new HashSet<string>(StringComparer.Ordinal);
            var resultIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (LegacyExpeditionRoomCompletionSaveDataV5 result in expedition.completedRoomResults)
            {
                if (result == null || !IsStableId(result.roomNodeId) || !IsStableId(result.resultId) ||
                    !IsStableId(result.resultJson) || !resultRooms.Add(result.roomNodeId) ||
                    !resultIds.Add(result.resultId) || expedition.completedRoomIds == null ||
                    !expedition.completedRoomIds.Contains(result.roomNodeId, StringComparer.Ordinal))
                    return false;
            }

            if (expedition.completedRoomIds != null && resultRooms.Count != expedition.completedRoomIds.Length)
                return false;
            if (expedition.completedRoomIds != null &&
                expedition.completedRoomIds.Contains(expedition.currentNodeId, StringComparer.Ordinal) &&
                !string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson))
                return false;
            if (expedition.completedRoomIds == null) return false;
            bool currentRoomCompleted = expedition.completedRoomIds.Contains(expedition.currentNodeId,
                StringComparer.Ordinal);
            return currentRoomCompleted
                ? string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson)
                : !string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson);
        }

        private static bool ValidateRoomEntryCheckpoint(LegacyExpeditionSaveDataV5 expedition)
        {
            if (!ValidateFortuneBook(expedition.fortuneOfferBookSnapshot, expedition.seed,
                    expedition.generationVersion, expedition.raceId))
                return false;
            if (string.IsNullOrWhiteSpace(expedition.roomEntryCheckpointJson)) return true;

            LegacyExpeditionRoomCheckpointSaveDataV5 checkpoint;
            try { checkpoint = JsonUtility.FromJson<LegacyExpeditionRoomCheckpointSaveDataV5>(expedition.roomEntryCheckpointJson); }
            catch (Exception) { return false; }
            if (checkpoint == null || !string.Equals(checkpoint.roomNodeId, expedition.currentNodeId, StringComparison.Ordinal))
                return false;
            return ValidateCheckpoint(checkpoint, expedition);
        }

        private static bool ValidateCheckpoint(LegacyExpeditionRoomCheckpointSaveDataV5 checkpoint,
            LegacyExpeditionSaveDataV5 expedition)
        {
            return checkpoint != null && IsStableId(checkpoint.inventorySnapshotJson) &&
                checkpoint.maximumHealth >= 1 && checkpoint.currentHealth >= 0 &&
                checkpoint.currentHealth <= checkpoint.maximumHealth && checkpoint.maximumEnergy >= 0 &&
                checkpoint.currentEnergy >= 0 && checkpoint.currentEnergy <= checkpoint.maximumEnergy &&
                checkpoint.healthPotionCharges >= 0 && checkpoint.accumulatedRenown >= 0 &&
                ValidateIds(checkpoint.pactIds) && ValidateIds(checkpoint.collectedItemIds) &&
                ValidateIds(checkpoint.claimedRewardIds) &&
                ValidateFortuneBook(checkpoint.fortuneOfferBookSnapshot, expedition.seed,
                    expedition.generationVersion, expedition.raceId);
        }

        private static bool ValidateFortuneBook(LegacyFortuneOfferBookSnapshotV5 snapshot, uint seed,
            int generationVersion, string raceId)
        {
            return ValidateFortuneBookStructure(snapshot) && snapshot.seed == seed &&
                snapshot.generationVersion == generationVersion &&
                string.Equals(snapshot.raceId, raceId, StringComparison.Ordinal);
        }

        private static bool ValidateFortuneBookStructure(LegacyFortuneOfferBookSnapshotV5 snapshot)
        {
            if (snapshot == null ||
                (snapshot.snapshotVersion != FortuneLegacySnapshotVersion &&
                 snapshot.snapshotVersion != FortuneRerollReceiptSnapshotVersion &&
                 snapshot.snapshotVersion != FortuneSnapshotVersion) ||
                snapshot.generationVersion <= 0 || snapshot.seed == 0 || !IsStableId(snapshot.raceId) ||
                snapshot.charges < 0 || snapshot.charges > MaximumFortuneCharges || snapshot.offers == null ||
                snapshot.processedGuardianIds == null ||
                (snapshot.snapshotVersion >= FortuneRerollReceiptSnapshotVersion && snapshot.rerollReceipts == null) ||
                (snapshot.snapshotVersion == FortuneSnapshotVersion && snapshot.fortuneBrewReceipts == null))
                return false;

            var guardianIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guardianId in snapshot.processedGuardianIds)
                if (!IsStableId(guardianId) || !guardianIds.Add(guardianId)) return false;

            var offers = new Dictionary<string, LegacyFortuneOfferSnapshotV5>(StringComparer.Ordinal);
            foreach (LegacyFortuneOfferSnapshotV5 offer in snapshot.offers)
            {
                if (offer == null || !IsStableId(offer.offerId) || !IsStableId(offer.contextId) ||
                    offer.eligibleOptionIds == null || offer.history == null || offer.history.Length == 0 ||
                    offer.history.Any(entry => entry == null || entry.optionIds == null))
                    return false;

                string[] eligible;
                if (!TryNormalizeFortuneIds(offer.eligibleOptionIds, out eligible) || eligible.Length < 3 ||
                    !eligible.SequenceEqual(offer.eligibleOptionIds, StringComparer.Ordinal))
                    return false;

                var historyKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (LegacyFortuneOfferHistorySnapshotV5 historyEntry in offer.history)
                {
                    string[] options;
                    if (!TryNormalizeFortuneIds(historyEntry.optionIds, out options) || options.Length != 3 ||
                        options.Any(id => Array.BinarySearch(eligible, id, StringComparer.Ordinal) < 0) ||
                        !historyKeys.Add(CanonicalSetKey(options)))
                        return false;
                }

                string selected = offer.selectedOptionId ?? string.Empty;
                string[] currentOptions = offer.history[offer.history.Length - 1].optionIds;
                if ((!string.IsNullOrEmpty(selected) &&
                     !currentOptions.Contains(selected, StringComparer.Ordinal)) ||
                    (offer.claimed && string.IsNullOrEmpty(selected)) ||
                    !offers.TryAdd(offer.offerId, offer))
                    return false;
            }

            var rerollTransactionIds = new HashSet<string>(StringComparer.Ordinal);
            LegacyFortuneRerollReceiptSnapshotV5[] rerollReceipts = snapshot.rerollReceipts ??
                Array.Empty<LegacyFortuneRerollReceiptSnapshotV5>();
            foreach (LegacyFortuneRerollReceiptSnapshotV5 receipt in rerollReceipts)
            {
                if (!IsValidRerollReceipt(receipt) ||
                    (receipt.hasOffer && !offers.ContainsKey(receipt.offerId)) ||
                    !rerollTransactionIds.Add(receipt.transactionId))
                    return false;
            }

            var brewTransactionIds = new HashSet<string>(StringComparer.Ordinal);
            LegacyFortuneBrewReceiptSnapshotV5[] brewReceipts = snapshot.fortuneBrewReceipts ??
                Array.Empty<LegacyFortuneBrewReceiptSnapshotV5>();
            foreach (LegacyFortuneBrewReceiptSnapshotV5 receipt in brewReceipts)
            {
                if (!IsValidBrewReceipt(receipt) || rerollTransactionIds.Contains(receipt.transactionId) ||
                    !brewTransactionIds.Add(receipt.transactionId))
                    return false;
            }
            return true;
        }

        private static bool IsValidRerollReceipt(LegacyFortuneRerollReceiptSnapshotV5 receipt)
        {
            if (receipt == null || !IsCanonicalTransactionId(receipt.transactionId) || receipt.offerId == null ||
                receipt.remainingCharges < 0 || receipt.remainingCharges > MaximumFortuneCharges ||
                receipt.rerollCount < 0 || !IsKnownFortuneStatus(receipt.status))
                return false;

            if (receipt.status == FortuneAlreadyApplied || receipt.status == FortuneRejectedInvalidRequest ||
                receipt.status == FortuneRejectedAtCapacity || receipt.status == FortuneRejectedInvalidChoice ||
                receipt.status == FortuneRejectedNoChoice)
                return false;
            if (!receipt.hasOffer)
                return receipt.status == FortuneRejectedUnknownOffer && receipt.optionIds != null &&
                    receipt.optionIds.Length == 0;

            if (receipt.status != FortuneSucceeded && receipt.status != FortuneRejectedInsufficientOptions &&
                receipt.status != FortuneRejectedNoCharges && receipt.status != FortuneRejectedConflict)
                return false;
            return IsStableId(receipt.offerId) && IsStableId(receipt.contextId) && receipt.optionIds != null &&
                receipt.optionIds.Length == 3 && receipt.optionIds.All(IsStableId) &&
                receipt.optionIds.Distinct(StringComparer.Ordinal).Count() == 3 &&
                (string.IsNullOrEmpty(receipt.selectedOptionId) ||
                 receipt.optionIds.Contains(receipt.selectedOptionId, StringComparer.Ordinal)) &&
                (!receipt.claimed || !string.IsNullOrEmpty(receipt.selectedOptionId)) &&
                (receipt.status != FortuneSucceeded || receipt.rerollCount != 0) &&
                (receipt.status != FortuneRejectedConflict || !string.IsNullOrEmpty(receipt.selectedOptionId));
        }

        private static bool IsValidBrewReceipt(LegacyFortuneBrewReceiptSnapshotV5 receipt)
        {
            if (receipt == null || !IsCanonicalTransactionId(receipt.transactionId) ||
                !IsCanonicalItemInstanceId(receipt.itemInstanceId) || receipt.currentCharges < 0 ||
                receipt.currentCharges > MaximumFortuneCharges)
                return false;
            if (receipt.status == FortuneSucceeded) return receipt.currentCharges > 0;
            return receipt.status == FortuneRejectedAtCapacity && receipt.currentCharges == MaximumFortuneCharges;
        }

        private static bool IsKnownFortuneStatus(int status) =>
            status >= FortuneSucceeded && status <= FortuneRejectedNoChoice;

        private static bool IsCanonicalTransactionId(string value) =>
            TransactionId.TryParse(value, out TransactionId parsed) &&
            string.Equals(parsed.ToString(), value, StringComparison.Ordinal);

        private static bool IsCanonicalItemInstanceId(string value) =>
            ItemInstanceId.TryParse(value, out ItemInstanceId parsed) &&
            string.Equals(parsed.ToString(), value, StringComparison.Ordinal);

        private static bool TryNormalizeFortuneIds(string[] values, out string[] normalized)
        {
            normalized = null;
            if (values == null) return false;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<string>(values.Length);
            foreach (string value in values)
            {
                if (!IsStableId(value) || !unique.Add(value)) return false;
                result.Add(value);
            }
            result.Sort(StringComparer.Ordinal);
            normalized = result.ToArray();
            return true;
        }

        private static string CanonicalSetKey(IEnumerable<string> values)
        {
            var result = new StringBuilder();
            foreach (string value in values.OrderBy(id => id, StringComparer.Ordinal))
            {
                result.Append(value.Length.ToString(CultureInfo.InvariantCulture));
                result.Append(':');
                result.Append(value);
                result.Append('|');
            }
            return result.ToString();
        }

        private static bool NormalizeProfileEnvelope(LegacyProfileSaveEnvelopeV5 envelope)
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

            if (envelope.activeExpedition != null && !envelope.activeExpedition.hasFrozenPermanentUpgrades)
            {
                if (HasFrozenUpgradeValues(envelope.activeExpedition.frozenPermanentUpgrades)) return false;
                envelope.activeExpedition.frozenPermanentUpgrades = null;
            }
            return true;
        }

        private static bool HasActiveExpeditionPayload(LegacyExpeditionSaveDataV5 expedition)
        {
            if (expedition == null) return false;
            bool hasFortune = HasActiveFortunePayload(expedition.fortuneOfferBookSnapshot);
            return HasActiveExpeditionPayload(expedition.runId, expedition.seed, expedition.generationVersion,
                    expedition.contentVersion, expedition.classId, expedition.raceId, expedition.routeSnapshotJson,
                    expedition.currentNodeId, expedition.roomEntryCheckpointJson, expedition.inventorySnapshotJson) ||
                HasEntries(expedition.selectedSkillIds) || HasEntries(expedition.startingEquipmentInstanceIds) ||
                HasEntries(expedition.visitedNodeIds) || HasEntries(expedition.completedRoomIds) ||
                HasEntries(expedition.completedRoomResults) || HasEntries(expedition.pactIds) ||
                HasEntries(expedition.collectedItemIds) || HasEntries(expedition.claimedRewardIds) ||
                expedition.hasFrozenPermanentUpgrades || HasFrozenUpgradeValues(expedition.frozenPermanentUpgrades) ||
                hasFortune;
        }

        private static bool HasActiveExpeditionPayload(string runId, uint seed, int generationVersion,
            int contentVersion, string classId, string raceId, string routeSnapshotJson, string currentNodeId,
            string roomEntryCheckpointJson, string inventorySnapshotJson)
        {
            return !string.IsNullOrWhiteSpace(runId) || seed != 0 || generationVersion != 0 || contentVersion != 0 ||
                !string.IsNullOrWhiteSpace(classId) || !string.IsNullOrWhiteSpace(raceId) ||
                !string.IsNullOrWhiteSpace(routeSnapshotJson) || !string.IsNullOrWhiteSpace(currentNodeId) ||
                !string.IsNullOrWhiteSpace(roomEntryCheckpointJson) || !string.IsNullOrWhiteSpace(inventorySnapshotJson);
        }

        private static bool HasActiveFortunePayload(LegacyFortuneOfferBookSnapshotV5 snapshot)
        {
            return snapshot != null && (snapshot.snapshotVersion != 0 || snapshot.generationVersion != 0 ||
                snapshot.seed != 0 || !string.IsNullOrWhiteSpace(snapshot.raceId) || snapshot.charges != 0 ||
                HasEntries(snapshot.offers) || HasEntries(snapshot.processedGuardianIds) ||
                HasEntries(snapshot.rerollReceipts) || HasEntries(snapshot.fortuneBrewReceipts));
        }

        private static bool HasEntries(Array values) => values != null && values.Length != 0;

        private static bool ValidateSettings(LegacyGameSettingsSaveDataV5 settings)
        {
            return settings != null && (settings.languageId == "es" || settings.languageId == "en") &&
                IsVolume(settings.masterVolume) && IsVolume(settings.musicVolume) &&
                IsVolume(settings.effectsVolume) && IsVolume(settings.ambienceVolume);
        }

        private static bool ValidateIds(string[] values, bool allowEmpty = true)
        {
            if (values == null || (!allowEmpty && values.Length == 0)) return false;
            if (values.Any(string.IsNullOrWhiteSpace)) return false;
            return values.Distinct(StringComparer.Ordinal).Count() == values.Length;
        }

        private static bool IsStableId(string value) => !string.IsNullOrWhiteSpace(value);

        private static bool IsVolume(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            return value.All(character => character >= '0' && character <= '9' ||
                character >= 'a' && character <= 'f');
        }
    }
}
