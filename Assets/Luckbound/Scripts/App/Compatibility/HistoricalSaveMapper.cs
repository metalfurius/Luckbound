using System;
using Luckbound.Core.Fortune;
using Luckbound.Core.Saves;

namespace Luckbound.App.Compatibility
{
    /// <summary>Deep copies authenticated historical documents after their versioned reader succeeds.</summary>
    internal static class HistoricalSaveMapper
    {
        internal static ProfileSaveEnvelope ToLive(LegacyProfileSaveEnvelopeV5 source)
        {
            if (source == null) return null;
            var result = new ProfileSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                profile = ToLive(source.profile),
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = ToLive(source.activeExpedition)
            };
            // Compatibility migrates only in memory after authenticating the V5 bytes.
            result.contentVersion = 6;
            return result;
        }

        internal static SettingsSaveEnvelope ToLive(LegacySettingsSaveEnvelopeV5 source)
        {
            if (source == null) return null;
            var result = new SettingsSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                settings = ToLive(source.settings)
            };
            // Compatibility migrates only in memory after authenticating the V5 bytes.
            result.contentVersion = 6;
            return result;
        }

        private static ProfileSaveData ToLive(LegacyProfileSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new ProfileSaveData
            {
                availableRenown = source.availableRenown,
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(source.unlockedRaceIds),
                unlockedStartingKitIds = Copy(source.unlockedStartingKitIds),
                acceptedContractIds = Copy(source.acceptedContractIds),
                claimedContractIds = Copy(source.claimedContractIds),
                readStoryFragmentIds = Copy(source.readStoryFragmentIds),
                contractCounters = source.contractCounters == null ? null : Array.ConvertAll(source.contractCounters, value => ToLive(value)),
                processedSettlementRunIds = Copy(source.processedSettlementRunIds),
                progressionPurchaseTransactions = source.progressionPurchaseTransactions == null ? null : Array.ConvertAll(source.progressionPurchaseTransactions, value => ToLive(value)),
                processedContractProgressEvents = Array.Empty<ContractProgressEventReceiptSaveData>()
            };
            return result;
        }

        private static ContractCounterSaveData ToLive(LegacyContractCounterSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new ContractCounterSaveData
            {
                contractId = source.contractId,
                progress = source.progress
            };
            return result;
        }

        private static PermanentUpgradePurchaseSaveData ToLive(LegacyPermanentUpgradePurchaseSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new PermanentUpgradePurchaseSaveData
            {
                transactionId = source.transactionId,
                kind = source.kind,
                cost = source.cost
            };
            return result;
        }

        private static ExpeditionSaveData ToLive(LegacyExpeditionSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new ExpeditionSaveData
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
                completedRoomResults = source.completedRoomResults == null ? null : Array.ConvertAll(source.completedRoomResults, value => ToLive(value)),
                roomEntryCheckpointJson = source.roomEntryCheckpointJson,
                inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges,
                frozenPermanentUpgrades = ToLive(source.frozenPermanentUpgrades),
                hasFrozenPermanentUpgrades = source.hasFrozenPermanentUpgrades,
                accumulatedRenown = source.accumulatedRenown,
                pactIds = Copy(source.pactIds),
                fortuneOfferBookSnapshot = ToLive(source.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(source.collectedItemIds),
                claimedRewardIds = Copy(source.claimedRewardIds)
            };
            return result;
        }

        private static ExpeditionPermanentUpgradeSnapshotSaveData ToLive(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new ExpeditionPermanentUpgradeSnapshotSaveData
            {
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel
            };
            return result;
        }

        private static ExpeditionRoomCompletionSaveData ToLive(LegacyExpeditionRoomCompletionSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new ExpeditionRoomCompletionSaveData
            {
                roomNodeId = source.roomNodeId,
                resultId = source.resultId,
                resultJson = source.resultJson
            };
            return result;
        }

        private static FortuneOfferBookSnapshot ToLive(LegacyFortuneOfferBookSnapshotV5 source)
        {
            if (source == null) return null;
            var result = new FortuneOfferBookSnapshot
            {
                snapshotVersion = source.snapshotVersion,
                generationVersion = source.generationVersion,
                seed = source.seed,
                raceId = source.raceId,
                charges = source.charges,
                offers = source.offers == null ? null : Array.ConvertAll(source.offers, value => ToLive(value)),
                processedGuardianIds = Copy(source.processedGuardianIds),
                rerollReceipts = source.rerollReceipts == null ? null : Array.ConvertAll(source.rerollReceipts, value => ToLive(value)),
                fortuneBrewReceipts = source.fortuneBrewReceipts == null ? null : Array.ConvertAll(source.fortuneBrewReceipts, value => ToLive(value))
            };
            return result;
        }

        private static FortuneOfferSnapshot ToLive(LegacyFortuneOfferSnapshotV5 source)
        {
            if (source == null) return null;
            var result = new FortuneOfferSnapshot
            {
                offerId = source.offerId,
                contextId = source.contextId,
                eligibleOptionIds = Copy(source.eligibleOptionIds),
                history = source.history == null ? null : Array.ConvertAll(source.history, value => ToLive(value)),
                selectedOptionId = source.selectedOptionId,
                claimed = source.claimed
            };
            return result;
        }

        private static FortuneOfferHistorySnapshot ToLive(LegacyFortuneOfferHistorySnapshotV5 source)
        {
            if (source == null) return null;
            var result = new FortuneOfferHistorySnapshot
            {
                optionIds = Copy(source.optionIds)
            };
            return result;
        }

        private static FortuneRerollReceiptSnapshot ToLive(LegacyFortuneRerollReceiptSnapshotV5 source)
        {
            if (source == null) return null;
            var result = new FortuneRerollReceiptSnapshot
            {
                transactionId = source.transactionId,
                offerId = source.offerId,
                status = source.status,
                contextId = source.contextId,
                optionIds = Copy(source.optionIds),
                selectedOptionId = source.selectedOptionId,
                rerollCount = source.rerollCount,
                claimed = source.claimed,
                remainingCharges = source.remainingCharges,
                hasOffer = source.hasOffer
            };
            return result;
        }

        private static FortuneBrewReceiptSnapshot ToLive(LegacyFortuneBrewReceiptSnapshotV5 source)
        {
            if (source == null) return null;
            var result = new FortuneBrewReceiptSnapshot
            {
                transactionId = source.transactionId,
                itemInstanceId = source.itemInstanceId,
                status = source.status,
                currentCharges = source.currentCharges
            };
            return result;
        }

        private static GameSettingsSaveData ToLive(LegacyGameSettingsSaveDataV5 source)
        {
            if (source == null) return null;
            var result = new GameSettingsSaveData
            {
                languageId = source.languageId,
                masterVolume = source.masterVolume,
                musicVolume = source.musicVolume,
                effectsVolume = source.effectsVolume,
                ambienceVolume = source.ambienceVolume
            };
            return result;
        }

        internal static ProfileSaveEnvelope ToLive(LegacyProfileSaveEnvelopeV6 source)
        {
            if (source == null) return null;
            var result = new ProfileSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                profile = ToLive(source.profile),
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = ToLive(source.activeExpedition)
            };
            return result;
        }

        internal static SettingsSaveEnvelope ToLive(LegacySettingsSaveEnvelopeV6 source)
        {
            if (source == null) return null;
            var result = new SettingsSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = source.checksum,
                settings = ToLive(source.settings)
            };
            return result;
        }

        private static ProfileSaveData ToLive(LegacyProfileSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new ProfileSaveData
            {
                availableRenown = source.availableRenown,
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(source.unlockedRaceIds),
                unlockedStartingKitIds = Copy(source.unlockedStartingKitIds),
                acceptedContractIds = Copy(source.acceptedContractIds),
                claimedContractIds = Copy(source.claimedContractIds),
                readStoryFragmentIds = Copy(source.readStoryFragmentIds),
                contractCounters = source.contractCounters == null ? null : Array.ConvertAll(source.contractCounters, value => ToLive(value)),
                processedSettlementRunIds = Copy(source.processedSettlementRunIds),
                processedContractProgressEvents = source.processedContractProgressEvents == null ? null : Array.ConvertAll(source.processedContractProgressEvents, value => ToLive(value)),
                progressionPurchaseTransactions = source.progressionPurchaseTransactions == null ? null : Array.ConvertAll(source.progressionPurchaseTransactions, value => ToLive(value))
            };
            return result;
        }

        private static ContractCounterSaveData ToLive(LegacyContractCounterSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new ContractCounterSaveData
            {
                contractId = source.contractId,
                progress = source.progress
            };
            return result;
        }

        private static ContractProgressEventReceiptSaveData ToLive(LegacyContractProgressEventReceiptSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new ContractProgressEventReceiptSaveData
            {
                eventId = source.eventId,
                progressEvent = source.progressEvent
            };
            return result;
        }

        private static PermanentUpgradePurchaseSaveData ToLive(LegacyPermanentUpgradePurchaseSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new PermanentUpgradePurchaseSaveData
            {
                transactionId = source.transactionId,
                kind = source.kind,
                cost = source.cost
            };
            return result;
        }

        private static ExpeditionSaveData ToLive(LegacyExpeditionSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new ExpeditionSaveData
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
                completedRoomResults = source.completedRoomResults == null ? null : Array.ConvertAll(source.completedRoomResults, value => ToLive(value)),
                roomEntryCheckpointJson = source.roomEntryCheckpointJson,
                inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges,
                frozenPermanentUpgrades = ToLive(source.frozenPermanentUpgrades),
                hasFrozenPermanentUpgrades = source.hasFrozenPermanentUpgrades,
                accumulatedRenown = source.accumulatedRenown,
                pactIds = Copy(source.pactIds),
                fortuneOfferBookSnapshot = ToLive(source.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(source.collectedItemIds),
                claimedRewardIds = Copy(source.claimedRewardIds)
            };
            return result;
        }

        private static ExpeditionPermanentUpgradeSnapshotSaveData ToLive(LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new ExpeditionPermanentUpgradeSnapshotSaveData
            {
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel
            };
            return result;
        }

        private static ExpeditionRoomCompletionSaveData ToLive(LegacyExpeditionRoomCompletionSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new ExpeditionRoomCompletionSaveData
            {
                roomNodeId = source.roomNodeId,
                resultId = source.resultId,
                resultJson = source.resultJson
            };
            return result;
        }

        private static FortuneOfferBookSnapshot ToLive(LegacyFortuneOfferBookSnapshotV6 source)
        {
            if (source == null) return null;
            var result = new FortuneOfferBookSnapshot
            {
                snapshotVersion = source.snapshotVersion,
                generationVersion = source.generationVersion,
                seed = source.seed,
                raceId = source.raceId,
                charges = source.charges,
                offers = source.offers == null ? null : Array.ConvertAll(source.offers, value => ToLive(value)),
                processedGuardianIds = Copy(source.processedGuardianIds),
                rerollReceipts = source.rerollReceipts == null ? null : Array.ConvertAll(source.rerollReceipts, value => ToLive(value)),
                fortuneBrewReceipts = source.fortuneBrewReceipts == null ? null : Array.ConvertAll(source.fortuneBrewReceipts, value => ToLive(value))
            };
            return result;
        }

        private static FortuneOfferSnapshot ToLive(LegacyFortuneOfferSnapshotV6 source)
        {
            if (source == null) return null;
            var result = new FortuneOfferSnapshot
            {
                offerId = source.offerId,
                contextId = source.contextId,
                eligibleOptionIds = Copy(source.eligibleOptionIds),
                history = source.history == null ? null : Array.ConvertAll(source.history, value => ToLive(value)),
                selectedOptionId = source.selectedOptionId,
                claimed = source.claimed
            };
            return result;
        }

        private static FortuneOfferHistorySnapshot ToLive(LegacyFortuneOfferHistorySnapshotV6 source)
        {
            if (source == null) return null;
            var result = new FortuneOfferHistorySnapshot
            {
                optionIds = Copy(source.optionIds)
            };
            return result;
        }

        private static FortuneRerollReceiptSnapshot ToLive(LegacyFortuneRerollReceiptSnapshotV6 source)
        {
            if (source == null) return null;
            var result = new FortuneRerollReceiptSnapshot
            {
                transactionId = source.transactionId,
                offerId = source.offerId,
                status = source.status,
                contextId = source.contextId,
                optionIds = Copy(source.optionIds),
                selectedOptionId = source.selectedOptionId,
                rerollCount = source.rerollCount,
                claimed = source.claimed,
                remainingCharges = source.remainingCharges,
                hasOffer = source.hasOffer
            };
            return result;
        }

        private static FortuneBrewReceiptSnapshot ToLive(LegacyFortuneBrewReceiptSnapshotV6 source)
        {
            if (source == null) return null;
            var result = new FortuneBrewReceiptSnapshot
            {
                transactionId = source.transactionId,
                itemInstanceId = source.itemInstanceId,
                status = source.status,
                currentCharges = source.currentCharges
            };
            return result;
        }

        private static GameSettingsSaveData ToLive(LegacyGameSettingsSaveDataV6 source)
        {
            if (source == null) return null;
            var result = new GameSettingsSaveData
            {
                languageId = source.languageId,
                masterVolume = source.masterVolume,
                musicVolume = source.musicVolume,
                effectsVolume = source.effectsVolume,
                ambienceVolume = source.ambienceVolume
            };
            return result;
        }

        private static string[] Copy(string[] source) =>
            source == null ? null : (string[])source.Clone();
    }
}
