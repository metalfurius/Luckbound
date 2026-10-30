using System;

namespace Luckbound.App.Compatibility
{
    [Serializable]
    internal sealed class LegacyProfileSaveEnvelopeV5
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public LegacyProfileSaveDataV5 profile;
        public bool hasActiveExpedition;
        public LegacyExpeditionSaveDataV5 activeExpedition;
    }

    [Serializable]
    internal sealed class LegacySettingsSaveEnvelopeV5
    {
        public int schemaVersion;
        public int contentVersion;
        public int generation;
        public long sequence;
        public string savedAtUtc;
        public string checksum;
        public LegacyGameSettingsSaveDataV5 settings;
    }

    [Serializable]
    internal sealed class LegacyProfileSaveDataV5
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
        public LegacyContractCounterSaveDataV5[] contractCounters = Array.Empty<LegacyContractCounterSaveDataV5>();
        public string[] processedSettlementRunIds = Array.Empty<string>();
        public LegacyPermanentUpgradePurchaseSaveDataV5[] progressionPurchaseTransactions =
            Array.Empty<LegacyPermanentUpgradePurchaseSaveDataV5>();
    }

    [Serializable]
    internal sealed class LegacyContractCounterSaveDataV5
    {
        public string contractId;
        public int progress;
    }

    [Serializable]
    internal sealed class LegacyPermanentUpgradePurchaseSaveDataV5
    {
        public string transactionId;
        public int kind;
        public int cost;
    }

    [Serializable]
    internal sealed class LegacyExpeditionSaveDataV5
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
        public LegacyExpeditionRoomCompletionSaveDataV5[] completedRoomResults =
            Array.Empty<LegacyExpeditionRoomCompletionSaveDataV5>();
        public string roomEntryCheckpointJson;
        public string inventorySnapshotJson;
        public int maximumHealth = 100;
        public int currentHealth = 100;
        public int maximumEnergy = 50;
        public int currentEnergy = 50;
        public int healthPotionCharges = 2;
        public LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5 frozenPermanentUpgrades;
        public bool hasFrozenPermanentUpgrades;
        public int accumulatedRenown;
        public string[] pactIds = Array.Empty<string>();
        public LegacyFortuneOfferBookSnapshotV5 fortuneOfferBookSnapshot;
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class LegacyExpeditionPermanentUpgradeSnapshotSaveDataV5
    {
        public int healthUpgradeLevel;
        public int energyUpgradeLevel;
        public int additionalPotionUpgradeLevel;
    }

    [Serializable]
    internal sealed class LegacyExpeditionRoomCheckpointSaveDataV5
    {
        public string roomNodeId;
        public string inventorySnapshotJson;
        public int maximumHealth;
        public int currentHealth;
        public int maximumEnergy;
        public int currentEnergy;
        public int healthPotionCharges;
        public int accumulatedRenown;
        public LegacyFortuneOfferBookSnapshotV5 fortuneOfferBookSnapshot;
        public string[] pactIds = Array.Empty<string>();
        public string[] collectedItemIds = Array.Empty<string>();
        public string[] claimedRewardIds = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class LegacyExpeditionRoomCompletionSaveDataV5
    {
        public string roomNodeId;
        public string resultId;
        public string resultJson;
    }

    [Serializable]
    internal sealed class LegacyFortuneOfferBookSnapshotV5
    {
        public int snapshotVersion;
        public int generationVersion;
        public uint seed;
        public string raceId;
        public int charges;
        public LegacyFortuneOfferSnapshotV5[] offers = Array.Empty<LegacyFortuneOfferSnapshotV5>();
        public string[] processedGuardianIds = Array.Empty<string>();
        public LegacyFortuneRerollReceiptSnapshotV5[] rerollReceipts =
            Array.Empty<LegacyFortuneRerollReceiptSnapshotV5>();
        public LegacyFortuneBrewReceiptSnapshotV5[] fortuneBrewReceipts =
            Array.Empty<LegacyFortuneBrewReceiptSnapshotV5>();
    }

    [Serializable]
    internal sealed class LegacyFortuneOfferSnapshotV5
    {
        public string offerId;
        public string contextId;
        public string[] eligibleOptionIds = Array.Empty<string>();
        public LegacyFortuneOfferHistorySnapshotV5[] history = Array.Empty<LegacyFortuneOfferHistorySnapshotV5>();
        public string selectedOptionId;
        public bool claimed;
    }

    [Serializable]
    internal sealed class LegacyFortuneOfferHistorySnapshotV5
    {
        public string[] optionIds = Array.Empty<string>();
    }

    [Serializable]
    internal sealed class LegacyFortuneRerollReceiptSnapshotV5
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
    internal sealed class LegacyFortuneBrewReceiptSnapshotV5
    {
        public string transactionId;
        public string itemInstanceId;
        public int status;
        public int currentCharges;
    }

    [Serializable]
    internal sealed class LegacyGameSettingsSaveDataV5
    {
        public string languageId = "es";
        public float masterVolume = 1f;
        public float musicVolume = 0.65f;
        public float effectsVolume = 0.8f;
        public float ambienceVolume = 0.45f;
    }
}
