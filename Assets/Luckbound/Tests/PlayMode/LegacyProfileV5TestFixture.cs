using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Luckbound.App;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    internal static class LegacyProfileV5TestFixture
    {
        public static string WriteProfile(
            FileSaveStore store, ProfileSaveData profile, ExpeditionSaveData activeExpedition)
        {
            if (store == null) throw new ArgumentNullException(nameof(store));
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (activeExpedition == null) throw new ArgumentNullException(nameof(activeExpedition));

            var envelope = new LegacyProfileSaveEnvelopeV5
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 5,
                generation = 1,
                sequence = 1,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                profile = new LegacyProfileSaveDataV5
                {
                    availableRenown = profile.availableRenown,
                    healthUpgradeLevel = profile.healthUpgradeLevel,
                    energyUpgradeLevel = profile.energyUpgradeLevel,
                    additionalPotionUpgradeLevel = profile.additionalPotionUpgradeLevel,
                    unlockedRaceIds = profile.unlockedRaceIds,
                    unlockedStartingKitIds = profile.unlockedStartingKitIds,
                    acceptedContractIds = profile.acceptedContractIds,
                    claimedContractIds = profile.claimedContractIds,
                    readStoryFragmentIds = profile.readStoryFragmentIds,
                    contractCounters = profile.contractCounters,
                    processedSettlementRunIds = profile.processedSettlementRunIds,
                    progressionPurchaseTransactions = profile.progressionPurchaseTransactions
                },
                hasActiveExpedition = true,
                activeExpedition = activeExpedition.Clone()
            };

            using (SHA256 sha = SHA256.Create())
            {
                byte[] checksum = sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope, false)));
                envelope.checksum = BitConverter.ToString(checksum).Replace("-", string.Empty).ToLowerInvariant();
            }

            string json = JsonUtility.ToJson(envelope, true);
            File.WriteAllText(store.GetPrimaryPath(SaveDocument.Profile), json, new UTF8Encoding(false));
            string backupPath = store.GetBackupPath(SaveDocument.Profile);
            if (File.Exists(backupPath)) File.Delete(backupPath);
            return json;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV5
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileSaveDataV5 profile;
            public bool hasActiveExpedition;
            public ExpeditionSaveData activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveDataV5
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
            public ContractCounterSaveData[] contractCounters = Array.Empty<ContractCounterSaveData>();
            public string[] processedSettlementRunIds = Array.Empty<string>();
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions =
                Array.Empty<PermanentUpgradePurchaseSaveData>();
        }
    }
}
