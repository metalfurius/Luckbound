using System;

namespace Luckbound.App.Compatibility
{
    [Serializable]
    internal sealed class LegacyProfileSaveEnvelopeV6
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public LegacyProfileSaveDataV6 profile;
        public bool hasActiveExpedition;
        public LegacyExpeditionSaveDataV6 activeExpedition;
    }

    [Serializable]
    internal sealed class LegacySettingsSaveEnvelopeV6
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public LegacyGameSettingsSaveDataV6 settings;
    }

    [Serializable]
    internal sealed class LegacyProfileSaveDataV6
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
        public LegacyContractCounterSaveDataV6[] contractCounters = Array.Empty<LegacyContractCounterSaveDataV6>();
        public string[] processedSettlementRunIds = Array.Empty<string>();
        public LegacyContractProgressEventReceiptSaveDataV6[] processedContractProgressEvents =
            Array.Empty<LegacyContractProgressEventReceiptSaveDataV6>();
        public LegacyPermanentUpgradePurchaseSaveDataV6[] progressionPurchaseTransactions =
            Array.Empty<LegacyPermanentUpgradePurchaseSaveDataV6>();
    }

    [Serializable]
    internal sealed class LegacyContractCounterSaveDataV6
    {
        public string contractId;
        public int progress;
    }

    [Serializable]
    internal sealed class LegacyContractProgressEventReceiptSaveDataV6
    {
        public string eventId;
        public int progressEvent;
    }

    [Serializable]
    internal sealed class LegacyPermanentUpgradePurchaseSaveDataV6
    {
        public string transactionId;
        public int kind;
        public int cost;
    }

    [Serializable]
    internal sealed class LegacyExpeditionSaveDataV6
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
        public LegacyExpeditionRoomCompletionSaveDataV6[] completedRoomResults =
            Array.Empty<LegacyExpeditionRoomCompletionSaveDataV6>();
        public string roomEntryCheckpointJson;
        public string inventorySnapshotJson;
        public int maximumHealth = 100;
        public int currentHealth = 100;
        public int maximumEnergy = 50;
        public int currentEnergy = 50;
        public int healthPotionCharges = 2;
        public LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6 frozenPermanentUpgrades;
        public bool hasFrozenPermanentUpgrades;
        public int accumulatedRenown;
        public string[] pactIds = Array.Empty<string>();
        public LegacyFortuneOfferBookSnapshotV6 fortuneOfferBookSnapshot;
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class LegacyExpeditionPermanentUpgradeSnapshotSaveDataV6
    {
        public int healthUpgradeLevel;
        public int energyUpgradeLevel;
        public int additionalPotionUpgradeLevel;
    }

    [Serializable]
    internal sealed class LegacyExpeditionRoomCheckpointSaveDataV6
    {
        public string roomNodeId;
        public string inventorySnapshotJson;
        public int maximumHealth;
        public int currentHealth;
        public int maximumEnergy;
        public int currentEnergy;
        public int healthPotionCharges;
        public int accumulatedRenown;
        public LegacyFortuneOfferBookSnapshotV6 fortuneOfferBookSnapshot;
        public string[] pactIds = Array.Empty<string>();
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class LegacyExpeditionRoomCompletionSaveDataV6
    {
        public string roomNodeId;
        public string resultId;
        public string resultJson;
    }

    [Serializable]
    internal sealed class LegacyFortuneOfferBookSnapshotV6
    {
        public int snapshotVersion;
        public int generationVersion;
        public uint seed;
        public string raceId;
        public int charges;
        public LegacyFortuneOfferSnapshotV6[] offers = Array.Empty<LegacyFortuneOfferSnapshotV6>();
        public string[] processedGuardianIds = Array.Empty<string>();
        public LegacyFortuneRerollReceiptSnapshotV6[] rerollReceipts =
            Array.Empty<LegacyFortuneRerollReceiptSnapshotV6>();
        public LegacyFortuneBrewReceiptSnapshotV6[] fortuneBrewReceipts =
            Array.Empty<LegacyFortuneBrewReceiptSnapshotV6>();
    }

    [Serializable]
    internal sealed class LegacyFortuneOfferSnapshotV6
    {
        public string offerId;
        public string contextId;
        public string[] eligibleOptionIds = Array.Empty<string>();
        public LegacyFortuneOfferHistorySnapshotV6[] history = Array.Empty<LegacyFortuneOfferHistorySnapshotV6>();
        public string selectedOptionId;
        public bool claimed;
    }

    [Serializable]
    internal sealed class LegacyFortuneOfferHistorySnapshotV6
    {
        public string[] optionIds = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class LegacyFortuneRerollReceiptSnapshotV6
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
    internal sealed class LegacyFortuneBrewReceiptSnapshotV6
    {
        public string transactionId;
        public string itemInstanceId;
        public int status;
        public int currentCharges;
    }

    [Serializable]
    internal sealed class LegacyGameSettingsSaveDataV6
    {
        public string languageId = "es";
        public float masterVolume = 1f;
        public float musicVolume = 0.65f;
        public float effectsVolume = 0.8f;
        public float ambienceVolume = 0.45f;
    }
}
