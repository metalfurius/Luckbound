using System;
using Luckbound.Core.Expeditions;

namespace Luckbound.Core.Saves.V7
{
    public enum SaveV7ErrorCode
    {
        None = 0,
        InvalidData = 1,
        Corrupted = 2,
        Incompatible = 3,
        PlanMismatch = 4,
        CatalogRevisionUnavailable = 5,
        LegacyEncounterPinMissing = 6,
        ProtocolConflict = 7,
        WritePending = 8,
        MissingResolutionAuthority = 9
    }

    public readonly struct SaveV7Error
    {
        private readonly string _path;
        private readonly string _message;

        public SaveV7Error(SaveV7ErrorCode code, string path, string message)
        {
            Code = code;
            _path = path ?? string.Empty;
            _message = message ?? string.Empty;
        }

        public SaveV7ErrorCode Code { get; }
        public string Path => _path ?? string.Empty;
        public string Message => _message ?? string.Empty;
    }

    public sealed class SaveV7ValidationException : Exception
    {
        public SaveV7ValidationException(SaveV7Error error)
            : base(error.Message)
        {
            if (error.Code == SaveV7ErrorCode.None)
                throw new ArgumentException("A V7 validation exception requires an error code.", nameof(error));
            Error = error;
        }

        public SaveV7Error Error { get; }
    }

    [Serializable]
    public sealed class ProfileSaveEnvelopeV7
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public ProfileSaveDataV7 profile;
        public bool hasActiveExpedition;
        public ExpeditionSaveDataV7 activeExpedition;
    }

    [Serializable]
    public sealed class SettingsSaveEnvelopeV7
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public GameSettingsSaveDataV7 settings;
    }

    [Serializable]
    public sealed class ProfileSaveDataV7
    {
        public int availableRenown;
        public int healthUpgradeLevel;
        public int energyUpgradeLevel;
        public int additionalPotionUpgradeLevel;
        public string[] unlockedRaceIds = Array.Empty<string>();
        public string[] unlockedStartingKitIds = Array.Empty<string>();
        public string[] acceptedContractIds = Array.Empty<string>();
        public string[] claimedContractIds = Array.Empty<string>();
        public string[] readStoryFragmentIds = Array.Empty<string>();
        public ContractCounterSaveDataV7[] contractCounters = Array.Empty<ContractCounterSaveDataV7>();
        public string[] processedSettlementRunIds = Array.Empty<string>();
        public ContractProgressEventReceiptSaveDataV7[] processedContractProgressEvents =
            Array.Empty<ContractProgressEventReceiptSaveDataV7>();
        public PermanentUpgradePurchaseSaveDataV7[] progressionPurchaseTransactions =
            Array.Empty<PermanentUpgradePurchaseSaveDataV7>();
    }

    [Serializable]
    public sealed class ContractCounterSaveDataV7
    {
        public string contractId;
        public int progress;
    }

    [Serializable]
    public sealed class ContractProgressEventReceiptSaveDataV7
    {
        public string eventId;
        public int progressEvent;
    }

    [Serializable]
    public sealed class PermanentUpgradePurchaseSaveDataV7
    {
        public string transactionId;
        public int kind;
        public int cost;
    }

    [Serializable]
    public sealed class ExpeditionSaveDataV7
    {
        public string runId;
        public uint seed;
        public int generationVersion;
        public int contentVersion;
        public string classId;
        public string raceId;
        public string[] selectedSkillIds = Array.Empty<string>();
        public string[] startingEquipmentInstanceIds = Array.Empty<string>();
        public string routeSnapshotJson;
        public string currentNodeId;
        public string[] visitedNodeIds = Array.Empty<string>();
        public string[] completedRoomIds = Array.Empty<string>();
        public ExpeditionRoomCompletionSaveDataV7[] completedRoomResults =
            Array.Empty<ExpeditionRoomCompletionSaveDataV7>();
        public string roomEntryCheckpointJson;
        public string inventorySnapshotJson;
        public int maximumHealth = 100;
        public int currentHealth = 100;
        public int maximumEnergy = 50;
        public int currentEnergy = 50;
        public int healthPotionCharges = 2;
        public ExpeditionPermanentUpgradeSnapshotSaveDataV7 frozenPermanentUpgrades;
        public bool hasFrozenPermanentUpgrades;
        public int accumulatedRenown;
        public string[] pactIds = Array.Empty<string>();
        public FortuneOfferBookSnapshotV7 fortuneOfferBookSnapshot;
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
        public EncounterPlanPinSaveDataV7 encounterPlanPin;
    }

    [Serializable]
    public sealed class ExpeditionPermanentUpgradeSnapshotSaveDataV7
    {
        public int healthUpgradeLevel;
        public int energyUpgradeLevel;
        public int additionalPotionUpgradeLevel;
    }

    [Serializable]
    public sealed class ExpeditionRoomCheckpointSaveDataV7
    {
        public string roomNodeId;
        public string inventorySnapshotJson;
        public int maximumHealth;
        public int currentHealth;
        public int maximumEnergy;
        public int currentEnergy;
        public int healthPotionCharges;
        public int accumulatedRenown;
        public FortuneOfferBookSnapshotV7 fortuneOfferBookSnapshot;
        public string[] pactIds = Array.Empty<string>();
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
        public EncounterPlanEntryReferenceSaveDataV7 encounterEntry;
    }

    [Serializable]
    public sealed class ExpeditionRoomCompletionSaveDataV7
    {
        public string roomNodeId;
        public string resultId;
        public string resultJson;
    }

    [Serializable]
    public sealed class FortuneOfferBookSnapshotV7
    {
        public int snapshotVersion;
        public int generationVersion;
        public uint seed;
        public string raceId;
        public int charges;
        public FortuneOfferSnapshotV7[] offers = Array.Empty<FortuneOfferSnapshotV7>();
        public string[] processedGuardianIds = Array.Empty<string>();
        public FortuneRerollReceiptSnapshotV7[] rerollReceipts =
            Array.Empty<FortuneRerollReceiptSnapshotV7>();
        public FortuneBrewReceiptSnapshotV7[] fortuneBrewReceipts =
            Array.Empty<FortuneBrewReceiptSnapshotV7>();
    }

    [Serializable]
    public sealed class FortuneOfferSnapshotV7
    {
        public string offerId;
        public string contextId;
        public string[] eligibleOptionIds = Array.Empty<string>();
        public FortuneOfferHistorySnapshotV7[] history = Array.Empty<FortuneOfferHistorySnapshotV7>();
        public string selectedOptionId;
        public bool claimed;
    }

    [Serializable]
    public sealed class FortuneOfferHistorySnapshotV7
    {
        public string[] optionIds = Array.Empty<string>();
    }

    [Serializable]
    public sealed class FortuneRerollReceiptSnapshotV7
    {
        public string transactionId;
        public string offerId;
        public int status;
        public string contextId;
        public string[] optionIds = Array.Empty<string>();
        public string selectedOptionId;
        public int rerollCount;
        public bool claimed;
        public int remainingCharges;
        public bool hasOffer;
    }

    [Serializable]
    public sealed class FortuneBrewReceiptSnapshotV7
    {
        public string transactionId;
        public string itemInstanceId;
        public int status;
        public int currentCharges;
    }

    [Serializable]
    public sealed class GameSettingsSaveDataV7
    {
        public string languageId = "es";
        public float masterVolume = 1f;
        public float musicVolume = 0.65f;
        public float effectsVolume = 0.8f;
        public float ambienceVolume = 0.45f;
    }

    [Serializable]
    public sealed class EncounterPlanPinSaveDataV7
    {
        public int schemaVersion;
        public string runId;
        public string catalogId;
        public int catalogRevision;
        public string catalogSha256;
        public string routeSha256;
        public string identityCodec;
        public EncounterPlanNodeBindingSaveDataV7[] bindings = Array.Empty<EncounterPlanNodeBindingSaveDataV7>();
        public EncounterPlanEntryReferenceSaveDataV7 entry;
    }

    [Serializable]
    public sealed class EncounterPlanNodeBindingSaveDataV7
    {
        public string nodeId;
        public int generationVersion;
        public string templateId;
        public string roomInstanceId;
        public EncounterCatalogBindingMode mode;
        public string encounterId;
        public int definitionRevision;
        public string definitionSha256;
        public string variantId;
        public string[] contextPactIds = Array.Empty<string>();
        public int calendarOrdinal;
        public string resolvedPlanSha256;
    }

    [Serializable]
    public sealed class EncounterPlanEntryReferenceSaveDataV7
    {
        public string nodeId;
        public string resolvedPlanSha256;
    }

    public static class SaveV7Clone
    {
        public static ProfileSaveEnvelopeV7 Clone(ProfileSaveEnvelopeV7 value)
        {
            if (value == null) return null;
            return new ProfileSaveEnvelopeV7
            {
                schemaVersion = value.schemaVersion,
                contentVersion = value.contentVersion,
                generation = value.generation,
                sequence = value.sequence,
                savedAtUtc = value.savedAtUtc,
                checksum = value.checksum,
                profile = Clone(value.profile),
                hasActiveExpedition = value.hasActiveExpedition,
                activeExpedition = Clone(value.activeExpedition)
            };
        }

        public static SettingsSaveEnvelopeV7 Clone(SettingsSaveEnvelopeV7 value)
        {
            if (value == null) return null;
            return new SettingsSaveEnvelopeV7
            {
                schemaVersion = value.schemaVersion,
                contentVersion = value.contentVersion,
                generation = value.generation,
                sequence = value.sequence,
                savedAtUtc = value.savedAtUtc,
                checksum = value.checksum,
                settings = Clone(value.settings)
            };
        }

        public static ProfileSaveDataV7 Clone(ProfileSaveDataV7 value)
        {
            if (value == null) return null;
            return new ProfileSaveDataV7
            {
                availableRenown = value.availableRenown,
                healthUpgradeLevel = value.healthUpgradeLevel,
                energyUpgradeLevel = value.energyUpgradeLevel,
                additionalPotionUpgradeLevel = value.additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(value.unlockedRaceIds),
                unlockedStartingKitIds = Copy(value.unlockedStartingKitIds),
                acceptedContractIds = Copy(value.acceptedContractIds),
                claimedContractIds = Copy(value.claimedContractIds),
                readStoryFragmentIds = Copy(value.readStoryFragmentIds),
                contractCounters = Clone(value.contractCounters),
                processedSettlementRunIds = Copy(value.processedSettlementRunIds),
                processedContractProgressEvents = Clone(value.processedContractProgressEvents),
                progressionPurchaseTransactions = Clone(value.progressionPurchaseTransactions)
            };
        }

        public static ContractCounterSaveDataV7 Clone(ContractCounterSaveDataV7 value)
        {
            if (value == null) return null;
            return new ContractCounterSaveDataV7 { contractId = value.contractId, progress = value.progress };
        }

        public static ContractProgressEventReceiptSaveDataV7 Clone(ContractProgressEventReceiptSaveDataV7 value)
        {
            if (value == null) return null;
            return new ContractProgressEventReceiptSaveDataV7
            {
                eventId = value.eventId,
                progressEvent = value.progressEvent
            };
        }

        public static PermanentUpgradePurchaseSaveDataV7 Clone(PermanentUpgradePurchaseSaveDataV7 value)
        {
            if (value == null) return null;
            return new PermanentUpgradePurchaseSaveDataV7
            {
                transactionId = value.transactionId,
                kind = value.kind,
                cost = value.cost
            };
        }

        public static ExpeditionSaveDataV7 Clone(ExpeditionSaveDataV7 value)
        {
            if (value == null) return null;
            return new ExpeditionSaveDataV7
            {
                runId = value.runId,
                seed = value.seed,
                generationVersion = value.generationVersion,
                contentVersion = value.contentVersion,
                classId = value.classId,
                raceId = value.raceId,
                selectedSkillIds = Copy(value.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(value.startingEquipmentInstanceIds),
                routeSnapshotJson = value.routeSnapshotJson,
                currentNodeId = value.currentNodeId,
                visitedNodeIds = Copy(value.visitedNodeIds),
                completedRoomIds = Copy(value.completedRoomIds),
                completedRoomResults = Clone(value.completedRoomResults),
                roomEntryCheckpointJson = value.roomEntryCheckpointJson,
                inventorySnapshotJson = value.inventorySnapshotJson,
                maximumHealth = value.maximumHealth,
                currentHealth = value.currentHealth,
                maximumEnergy = value.maximumEnergy,
                currentEnergy = value.currentEnergy,
                healthPotionCharges = value.healthPotionCharges,
                frozenPermanentUpgrades = Clone(value.frozenPermanentUpgrades),
                hasFrozenPermanentUpgrades = value.hasFrozenPermanentUpgrades,
                accumulatedRenown = value.accumulatedRenown,
                pactIds = Copy(value.pactIds),
                fortuneOfferBookSnapshot = Clone(value.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(value.collectedItemIds),
                claimedRewardIds = Copy(value.claimedRewardIds),
                encounterPlanPin = Clone(value.encounterPlanPin)
            };
        }

        public static ExpeditionPermanentUpgradeSnapshotSaveDataV7 Clone(
            ExpeditionPermanentUpgradeSnapshotSaveDataV7 value)
        {
            if (value == null) return null;
            return new ExpeditionPermanentUpgradeSnapshotSaveDataV7
            {
                healthUpgradeLevel = value.healthUpgradeLevel,
                energyUpgradeLevel = value.energyUpgradeLevel,
                additionalPotionUpgradeLevel = value.additionalPotionUpgradeLevel
            };
        }

        public static ExpeditionRoomCheckpointSaveDataV7 Clone(ExpeditionRoomCheckpointSaveDataV7 value)
        {
            if (value == null) return null;
            return new ExpeditionRoomCheckpointSaveDataV7
            {
                roomNodeId = value.roomNodeId,
                inventorySnapshotJson = value.inventorySnapshotJson,
                maximumHealth = value.maximumHealth,
                currentHealth = value.currentHealth,
                maximumEnergy = value.maximumEnergy,
                currentEnergy = value.currentEnergy,
                healthPotionCharges = value.healthPotionCharges,
                accumulatedRenown = value.accumulatedRenown,
                fortuneOfferBookSnapshot = Clone(value.fortuneOfferBookSnapshot),
                pactIds = Copy(value.pactIds),
                collectedItemIds = Copy(value.collectedItemIds),
                claimedRewardIds = Copy(value.claimedRewardIds),
                encounterEntry = Clone(value.encounterEntry)
            };
        }

        public static ExpeditionRoomCompletionSaveDataV7 Clone(ExpeditionRoomCompletionSaveDataV7 value)
        {
            if (value == null) return null;
            return new ExpeditionRoomCompletionSaveDataV7
            {
                roomNodeId = value.roomNodeId,
                resultId = value.resultId,
                resultJson = value.resultJson
            };
        }

        public static FortuneOfferBookSnapshotV7 Clone(FortuneOfferBookSnapshotV7 value)
        {
            if (value == null) return null;
            return new FortuneOfferBookSnapshotV7
            {
                snapshotVersion = value.snapshotVersion,
                generationVersion = value.generationVersion,
                seed = value.seed,
                raceId = value.raceId,
                charges = value.charges,
                offers = Clone(value.offers),
                processedGuardianIds = Copy(value.processedGuardianIds),
                rerollReceipts = Clone(value.rerollReceipts),
                fortuneBrewReceipts = Clone(value.fortuneBrewReceipts)
            };
        }

        public static FortuneOfferSnapshotV7 Clone(FortuneOfferSnapshotV7 value)
        {
            if (value == null) return null;
            return new FortuneOfferSnapshotV7
            {
                offerId = value.offerId,
                contextId = value.contextId,
                eligibleOptionIds = Copy(value.eligibleOptionIds),
                history = Clone(value.history),
                selectedOptionId = value.selectedOptionId,
                claimed = value.claimed
            };
        }

        public static FortuneOfferHistorySnapshotV7 Clone(FortuneOfferHistorySnapshotV7 value)
        {
            if (value == null) return null;
            return new FortuneOfferHistorySnapshotV7 { optionIds = Copy(value.optionIds) };
        }

        public static FortuneRerollReceiptSnapshotV7 Clone(FortuneRerollReceiptSnapshotV7 value)
        {
            if (value == null) return null;
            return new FortuneRerollReceiptSnapshotV7
            {
                transactionId = value.transactionId,
                offerId = value.offerId,
                status = value.status,
                contextId = value.contextId,
                optionIds = Copy(value.optionIds),
                selectedOptionId = value.selectedOptionId,
                rerollCount = value.rerollCount,
                claimed = value.claimed,
                remainingCharges = value.remainingCharges,
                hasOffer = value.hasOffer
            };
        }

        public static FortuneBrewReceiptSnapshotV7 Clone(FortuneBrewReceiptSnapshotV7 value)
        {
            if (value == null) return null;
            return new FortuneBrewReceiptSnapshotV7
            {
                transactionId = value.transactionId,
                itemInstanceId = value.itemInstanceId,
                status = value.status,
                currentCharges = value.currentCharges
            };
        }

        public static GameSettingsSaveDataV7 Clone(GameSettingsSaveDataV7 value)
        {
            if (value == null) return null;
            return new GameSettingsSaveDataV7
            {
                languageId = value.languageId,
                masterVolume = value.masterVolume,
                musicVolume = value.musicVolume,
                effectsVolume = value.effectsVolume,
                ambienceVolume = value.ambienceVolume
            };
        }

        public static EncounterPlanPinSaveDataV7 Clone(EncounterPlanPinSaveDataV7 value)
        {
            if (value == null) return null;
            return new EncounterPlanPinSaveDataV7
            {
                schemaVersion = value.schemaVersion,
                runId = value.runId,
                catalogId = value.catalogId,
                catalogRevision = value.catalogRevision,
                catalogSha256 = value.catalogSha256,
                routeSha256 = value.routeSha256,
                identityCodec = value.identityCodec,
                bindings = Clone(value.bindings),
                entry = Clone(value.entry)
            };
        }

        public static EncounterPlanNodeBindingSaveDataV7 Clone(EncounterPlanNodeBindingSaveDataV7 value)
        {
            if (value == null) return null;
            return new EncounterPlanNodeBindingSaveDataV7
            {
                nodeId = value.nodeId,
                generationVersion = value.generationVersion,
                templateId = value.templateId,
                roomInstanceId = value.roomInstanceId,
                mode = value.mode,
                encounterId = value.encounterId,
                definitionRevision = value.definitionRevision,
                definitionSha256 = value.definitionSha256,
                variantId = value.variantId,
                contextPactIds = Copy(value.contextPactIds),
                calendarOrdinal = value.calendarOrdinal,
                resolvedPlanSha256 = value.resolvedPlanSha256
            };
        }

        public static EncounterPlanEntryReferenceSaveDataV7 Clone(EncounterPlanEntryReferenceSaveDataV7 value)
        {
            if (value == null) return null;
            return new EncounterPlanEntryReferenceSaveDataV7
            {
                nodeId = value.nodeId,
                resolvedPlanSha256 = value.resolvedPlanSha256
            };
        }

        private static string[] Copy(string[] values)
        {
            if (values == null) return null;
            var copy = new string[values.Length];
            Array.Copy(values, copy, values.Length);
            return copy;
        }

        private static ContractCounterSaveDataV7[] Clone(ContractCounterSaveDataV7[] values)
        {
            if (values == null) return null;
            var copy = new ContractCounterSaveDataV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static ContractProgressEventReceiptSaveDataV7[] Clone(ContractProgressEventReceiptSaveDataV7[] values)
        {
            if (values == null) return null;
            var copy = new ContractProgressEventReceiptSaveDataV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static PermanentUpgradePurchaseSaveDataV7[] Clone(PermanentUpgradePurchaseSaveDataV7[] values)
        {
            if (values == null) return null;
            var copy = new PermanentUpgradePurchaseSaveDataV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static ExpeditionRoomCompletionSaveDataV7[] Clone(ExpeditionRoomCompletionSaveDataV7[] values)
        {
            if (values == null) return null;
            var copy = new ExpeditionRoomCompletionSaveDataV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static FortuneOfferSnapshotV7[] Clone(FortuneOfferSnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneOfferSnapshotV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static FortuneOfferHistorySnapshotV7[] Clone(FortuneOfferHistorySnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneOfferHistorySnapshotV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static FortuneRerollReceiptSnapshotV7[] Clone(FortuneRerollReceiptSnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneRerollReceiptSnapshotV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static FortuneBrewReceiptSnapshotV7[] Clone(FortuneBrewReceiptSnapshotV7[] values)
        {
            if (values == null) return null;
            var copy = new FortuneBrewReceiptSnapshotV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }

        private static EncounterPlanNodeBindingSaveDataV7[] Clone(EncounterPlanNodeBindingSaveDataV7[] values)
        {
            if (values == null) return null;
            var copy = new EncounterPlanNodeBindingSaveDataV7[values.Length];
            for (int index = 0; index < values.Length; index++) copy[index] = Clone(values[index]);
            return copy;
        }
    }

}
