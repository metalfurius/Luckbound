using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Luckbound.App.Compatibility
{
    internal enum SaveV6ChecksumStatus
    {
        NotVersion6,
        Malformed,
        ChecksumMismatch,
        ChecksumValid
    }

    internal sealed class SaveV6ChecksumInspection
    {
        private readonly byte[] _preimageUtf8;

        internal SaveV6ChecksumStatus Status { get; }
        internal string OriginalJson { get; }
        internal string StoredChecksum { get; }
        internal string ComputedChecksum { get; }

        private SaveV6ChecksumInspection(
            SaveV6ChecksumStatus status,
            string originalJson,
            string storedChecksum,
            string computedChecksum,
            byte[] preimageUtf8)
        {
            Status = status;
            OriginalJson = originalJson;
            StoredChecksum = storedChecksum;
            ComputedChecksum = computedChecksum;
            _preimageUtf8 = preimageUtf8 == null ? null : (byte[])preimageUtf8.Clone();
        }

        internal byte[] GetPreimageUtf8() => _preimageUtf8 == null ? null : (byte[])_preimageUtf8.Clone();

        internal static SaveV6ChecksumInspection CreateForInspector(
            object accessKey,
            SaveV6ChecksumStatus status,
            string originalJson,
            string storedChecksum,
            string computedChecksum,
            byte[] preimageUtf8)
        {
            if (!SaveV6ChecksumInspector.IsInspectionAccessKey(accessKey))
                throw new ArgumentException("Only the v6 checksum inspector can create an inspection result.",
                    nameof(accessKey));
            return new SaveV6ChecksumInspection(status, originalJson, storedChecksum, computedChecksum,
                preimageUtf8);
        }
    }

    internal static class SaveV6ChecksumInspector
    {
        private const int HistoricalSchemaVersion = 1;
        private const int HistoricalContentVersion = 6;
        private static readonly object AccessKey = new object();

        internal static bool IsInspectionAccessKey(object candidate) => ReferenceEquals(candidate, AccessKey);

        internal static byte[] GetProfileChecksumPreimage(LegacyProfileSaveEnvelopeV6 source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureVersion6(source.schemaVersion, source.contentVersion);

            var canonical = new LegacyProfileSaveEnvelopeV6
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

        internal static byte[] GetSettingsChecksumPreimage(LegacySettingsSaveEnvelopeV6 source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            EnsureVersion6(source.schemaVersion, source.contentVersion);

            var canonical = new LegacySettingsSaveEnvelopeV6
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

        internal static string ComputeProfileChecksum(LegacyProfileSaveEnvelopeV6 source) =>
            ComputeChecksum(GetProfileChecksumPreimage(source));

        internal static string ComputeSettingsChecksum(LegacySettingsSaveEnvelopeV6 source) =>
            ComputeChecksum(GetSettingsChecksumPreimage(source));

        internal static SaveV6ChecksumInspection InspectProfile(string originalJson)
        {
            if (string.IsNullOrWhiteSpace(originalJson))
                return Malformed(originalJson, null);

            LegacyProfileSaveEnvelopeV6 source;
            try
            {
                source = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV6>(originalJson);
            }
            catch (Exception)
            {
                return Malformed(originalJson, null);
            }

            if (source == null) return Malformed(originalJson, null);
            if (!IsVersion6(source.schemaVersion, source.contentVersion))
                return Create(SaveV6ChecksumStatus.NotVersion6, originalJson, source.checksum, null, null);

            try
            {
                byte[] preimage = GetProfileChecksumPreimage(source);
                string computedChecksum = ComputeChecksum(preimage);
                SaveV6ChecksumStatus status = string.Equals(source.checksum, computedChecksum, StringComparison.Ordinal)
                    ? SaveV6ChecksumStatus.ChecksumValid
                    : SaveV6ChecksumStatus.ChecksumMismatch;
                return Create(status, originalJson, source.checksum, computedChecksum, preimage);
            }
            catch (Exception)
            {
                return Malformed(originalJson, source.checksum);
            }
        }

        internal static SaveV6ChecksumInspection InspectSettings(string originalJson)
        {
            if (string.IsNullOrWhiteSpace(originalJson))
                return Malformed(originalJson, null);

            LegacySettingsSaveEnvelopeV6 source;
            try
            {
                source = JsonUtility.FromJson<LegacySettingsSaveEnvelopeV6>(originalJson);
            }
            catch (Exception)
            {
                return Malformed(originalJson, null);
            }

            if (source == null) return Malformed(originalJson, null);
            if (!IsVersion6(source.schemaVersion, source.contentVersion))
                return Create(SaveV6ChecksumStatus.NotVersion6, originalJson, source.checksum, null, null);

            try
            {
                byte[] preimage = GetSettingsChecksumPreimage(source);
                string computedChecksum = ComputeChecksum(preimage);
                SaveV6ChecksumStatus status = string.Equals(source.checksum, computedChecksum, StringComparison.Ordinal)
                    ? SaveV6ChecksumStatus.ChecksumValid
                    : SaveV6ChecksumStatus.ChecksumMismatch;
                return Create(status, originalJson, source.checksum, computedChecksum, preimage);
            }
            catch (Exception)
            {
                return Malformed(originalJson, source.checksum);
            }
        }

        private static LegacyExpeditionSaveDataV6 CloneExpeditionForChecksum(LegacyExpeditionSaveDataV6 source)
        {
            return new LegacyExpeditionSaveDataV6
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

        private static LegacyExpeditionRoomCompletionSaveDataV6[] CloneRoomResults(
            LegacyExpeditionRoomCompletionSaveDataV6[] source)
        {
            if (source == null) return null;
            var results = new LegacyExpeditionRoomCompletionSaveDataV6[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                LegacyExpeditionRoomCompletionSaveDataV6 result = source[i];
                results[i] = result == null ? null : new LegacyExpeditionRoomCompletionSaveDataV6
                {
                    roomNodeId = result.roomNodeId,
                    resultId = result.resultId,
                    resultJson = result.resultJson
                };
            }
            return results;
        }

        private static LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 CloneFrozenUpgrades(
            LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 source)
        {
            return new LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6
            {
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel
            };
        }

        private static bool HasFrozenUpgradeValues(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 source) =>
            source != null && (source.healthUpgradeLevel != 0 || source.energyUpgradeLevel != 0 ||
                source.additionalPotionUpgradeLevel != 0);

        private static LegacyFortuneOfferBookSnapshotV6 CloneFortuneBook(LegacyFortuneOfferBookSnapshotV6 source)
        {
            if (source == null) return null;
            return new LegacyFortuneOfferBookSnapshotV6
            {
                snapshotVersion = source.snapshotVersion,
                generationVersion = source.generationVersion,
                seed = source.seed,
                raceId = source.raceId,
                charges = source.charges,
                processedGuardianIds = Copy(source.processedGuardianIds),
                rerollReceipts = CloneRerollReceipts(source.rerollReceipts),
                fortuneBrewReceipts = CloneBrewReceipts(source.fortuneBrewReceipts),
                offers = CloneOffers(source.offers)
            };
        }

        private static LegacyFortuneRerollReceiptSnapshotV6[] CloneRerollReceipts(
            LegacyFortuneRerollReceiptSnapshotV6[] source)
        {
            if (source == null) return null;
            var receipts = new LegacyFortuneRerollReceiptSnapshotV6[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                LegacyFortuneRerollReceiptSnapshotV6 receipt = source[i];
                receipts[i] = receipt == null ? null : new LegacyFortuneRerollReceiptSnapshotV6
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
                };
            }
            return receipts;
        }

        private static LegacyFortuneBrewReceiptSnapshotV6[] CloneBrewReceipts(
            LegacyFortuneBrewReceiptSnapshotV6[] source)
        {
            if (source == null) return null;
            var receipts = new LegacyFortuneBrewReceiptSnapshotV6[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                LegacyFortuneBrewReceiptSnapshotV6 receipt = source[i];
                receipts[i] = receipt == null ? null : new LegacyFortuneBrewReceiptSnapshotV6
                {
                    transactionId = receipt.transactionId,
                    itemInstanceId = receipt.itemInstanceId,
                    status = receipt.status,
                    currentCharges = receipt.currentCharges
                };
            }
            return receipts;
        }

        private static LegacyFortuneOfferSnapshotV6[] CloneOffers(LegacyFortuneOfferSnapshotV6[] source)
        {
            if (source == null) return null;
            var offers = new LegacyFortuneOfferSnapshotV6[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                LegacyFortuneOfferSnapshotV6 offer = source[i];
                offers[i] = offer == null ? null : new LegacyFortuneOfferSnapshotV6
                {
                    offerId = offer.offerId,
                    contextId = offer.contextId,
                    eligibleOptionIds = Copy(offer.eligibleOptionIds),
                    selectedOptionId = offer.selectedOptionId,
                    claimed = offer.claimed,
                    history = CloneOfferHistory(offer.history)
                };
            }
            return offers;
        }

        private static LegacyFortuneOfferHistorySnapshotV6[] CloneOfferHistory(
            LegacyFortuneOfferHistorySnapshotV6[] source)
        {
            if (source == null) return null;
            var history = new LegacyFortuneOfferHistorySnapshotV6[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                LegacyFortuneOfferHistorySnapshotV6 entry = source[i];
                history[i] = entry == null ? null : new LegacyFortuneOfferHistorySnapshotV6
                {
                    optionIds = Copy(entry.optionIds)
                };
            }
            return history;
        }

        private static string[] Copy(string[] source) => source == null ? null : (string[])source.Clone();

        private static bool IsVersion6(int schemaVersion, int contentVersion) =>
            schemaVersion == HistoricalSchemaVersion && contentVersion == HistoricalContentVersion;

        private static void EnsureVersion6(int schemaVersion, int contentVersion)
        {
            if (!IsVersion6(schemaVersion, contentVersion))
                throw new ArgumentException("Checksum projection requires schema 1 and content version 6.");
        }

        private static string ComputeChecksum(byte[] preimageUtf8)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(preimageUtf8);
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static SaveV6ChecksumInspection Malformed(string originalJson, string storedChecksum) =>
            Create(SaveV6ChecksumStatus.Malformed, originalJson, storedChecksum, null, null);

        private static SaveV6ChecksumInspection Create(
            SaveV6ChecksumStatus status,
            string originalJson,
            string storedChecksum,
            string computedChecksum,
            byte[] preimageUtf8) =>
            SaveV6ChecksumInspection.CreateForInspector(AccessKey, status, originalJson, storedChecksum,
                computedChecksum, preimageUtf8);
    }
}
