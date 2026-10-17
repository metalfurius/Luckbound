using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    /// <summary>
    /// Exercises the serialized legacy profile shapes through the production file store and repository.
    /// The private DTOs below mirror the former on-disk fields; checksums use the production canonical JSON shape.
    /// </summary>
    public sealed class E04LegacyFortuneMigrationAcceptanceTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "Luckbound-E04-Legacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_directory) && Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        [TestCase(1)]
        [TestCase(2)]
        public void AuthenticLegacyExpeditionMigratesFortuneBookAndCanBePersistedAsCurrentFormat(int legacyContentVersion)
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData legacySource = ExpeditionSaveMapper.Create(run);
            string legacyJson = CreateLegacyDocument(legacySource, legacyContentVersion, includeUnrepresentableOffer: false);
            byte[] originalBytes = new UTF8Encoding(false).GetBytes(legacyJson);
            FileSaveStore store = NewIsolatedStore();
            string profilePath = store.GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllBytes(profilePath, originalBytes);

            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> migrated = Wait(repository.LoadProfileAsync());

            Assert.That(migrated.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), migrated.Message);
            Assert.That(migrated.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(migrated.Data.hasActiveExpedition, Is.True);
            Assert.That(migrated.Data.activeExpedition, Is.Not.Null);
            FortuneOfferBookSnapshot migratedBook = migrated.Data.activeExpedition.fortuneOfferBookSnapshot;
            Assert.That(migratedBook, Is.Not.Null);
            Assert.That(migratedBook.snapshotVersion, Is.EqualTo(FortuneOfferBook.SnapshotVersion));
            Assert.That(migratedBook.generationVersion, Is.EqualTo(run.Route.GenerationVersion));
            Assert.That(migratedBook.seed, Is.EqualTo(run.Seed));
            Assert.That(migratedBook.raceId, Is.EqualTo(run.RaceId));
            Assert.That(migratedBook.charges, Is.EqualTo(run.StartingFortunaCharges));
            Assert.That(migratedBook.offers, Is.Empty);

            ExpeditionRunConfiguration restoredRun = ExpeditionSaveMapper.Restore(
                migrated.Data.activeExpedition, out ExpeditionSaveData migratedEntryCheckpoint);
            Assert.That(restoredRun.RunId, Is.EqualTo(run.RunId));
            Assert.That(restoredRun.Route.CanonicalForm, Is.EqualTo(run.Route.CanonicalForm));
            Assert.That(migratedEntryCheckpoint.fortunaCharges, Is.EqualTo(run.StartingFortunaCharges - 1),
                "Legacy checkpoint charges migrate independently from the active room snapshot.");
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(profilePath),
                "Reading the legacy profile migrates it in memory; persistence remains an explicit repository write.");

            SaveWriteResult persisted = Wait(repository.SaveProfileAsync(
                migrated.Data.profile, migrated.Data.activeExpedition));
            Assert.That(persisted.Succeeded, Is.True, persisted.Message);

            string currentJson = File.ReadAllText(profilePath, Encoding.UTF8);
            ProfileSaveEnvelope persistedEnvelope = JsonUtility.FromJson<ProfileSaveEnvelope>(currentJson);
            Assert.That(persistedEnvelope.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(currentJson, Does.Contain("fortuneOfferBookSnapshot"));
            Assert.That(currentJson, Does.Not.Contain("pendingOfferIds"));
            Assert.That(currentJson, Does.Not.Contain("offerRerollCounts"));
            Assert.That(File.ReadAllText(store.GetBackupPath(SaveDocument.Profile), Encoding.UTF8), Is.EqualTo(legacyJson),
                "The repository keeps the original legacy primary as the known-good backup when it writes the migrated profile.");

            SaveLoadResult<ProfileSaveEnvelope> afterRestart = Wait(new SaveRepository(store).LoadProfileAsync());
            Assert.That(afterRestart.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), afterRestart.Message);
            Assert.That(afterRestart.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(afterRestart.Data.activeExpedition.fortuneOfferBookSnapshot.seed, Is.EqualTo(run.Seed));
            Assert.That(afterRestart.Data.activeExpedition.fortuneOfferBookSnapshot.charges,
                Is.EqualTo(run.StartingFortunaCharges));
        }

        [Test]
        public void LegacyPendingOfferSummaryCannotMigrateAndRepositoryPreservesOriginalBytes()
        {
            ExpeditionSaveData legacySource = ExpeditionSaveMapper.Create(CreateRun());
            string legacyJson = CreateLegacyDocument(legacySource, 2, includeUnrepresentableOffer: true);
            byte[] originalBytes = new UTF8Encoding(false).GetBytes(legacyJson);
            FileSaveStore store = NewIsolatedStore();
            string profilePath = store.GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllBytes(profilePath, originalBytes);

            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> load = Wait(repository.LoadProfileAsync());
            SaveWriteResult attemptedOverwrite = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile()));

            Assert.That(load.Status, Is.EqualTo(SaveLoadStatus.Corrupted), load.Message);
            Assert.That(load.Data, Is.Null);
            Assert.That(attemptedOverwrite.Succeeded, Is.False,
                "The repository blocks writes when the legacy offer summary cannot be represented safely.");
            CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(profilePath),
                "Neither failed migration nor a subsequent save attempt may replace or alter the original bytes.");
            Assert.That(File.Exists(store.GetBackupPath(SaveDocument.Profile)), Is.False);
        }

        private static ExpeditionRunConfiguration CreateRun() => ExpeditionRunConfigurationFactory.Create(
            "class.bellator", "race.human", new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });

        private FileSaveStore NewIsolatedStore()
        {
            string directory = Path.GetFullPath(_directory);
            string persistentDirectory = Path.GetFullPath(Application.persistentDataPath);
            Assert.That(string.Equals(directory, persistentDirectory, StringComparison.OrdinalIgnoreCase), Is.False,
                "Acceptance fixtures must never target Unity's real profile directory.");

            var store = new FileSaveStore(directory, null);
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(store.GetPrimaryPath(SaveDocument.Profile))),
                Is.EqualTo(directory));
            return store;
        }

        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private static string CreateLegacyDocument(ExpeditionSaveData state, int version,
            bool includeUnrepresentableOffer)
        {
            LegacyExpeditionSaveData legacyExpedition = ToLegacyExpedition(state, version, includeUnrepresentableOffer);
            if (version == 1)
            {
                var envelope = new LegacyProfileEnvelopeV1
                {
                    schemaVersion = SaveFormat.SchemaVersion,
                    contentVersion = version,
                    generation = 4,
                    sequence = 11,
                    savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    checksum = string.Empty,
                    profile = LegacyProfileV1.Create(),
                    hasActiveExpedition = true,
                    activeExpedition = legacyExpedition
                };
                envelope.checksum = Hash(JsonUtility.ToJson(envelope, false));
                return JsonUtility.ToJson(envelope, true);
            }

            var v2 = new LegacyProfileEnvelopeV2
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = version,
                generation = 4,
                sequence = 11,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                profile = LegacyProfileV2.Create(),
                hasActiveExpedition = true,
                activeExpedition = legacyExpedition
            };
            v2.checksum = Hash(JsonUtility.ToJson(v2, false));
            return JsonUtility.ToJson(v2, true);
        }

        private static LegacyExpeditionSaveData ToLegacyExpedition(ExpeditionSaveData state, int version,
            bool includeUnrepresentableOffer)
        {
            ExpeditionRoomCheckpointSaveData checkpoint =
                JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(state.roomEntryCheckpointJson);
            var legacyCheckpoint = new LegacyRoomCheckpoint
            {
                roomNodeId = checkpoint.roomNodeId,
                inventorySnapshotJson = checkpoint.inventorySnapshotJson,
                maximumHealth = checkpoint.maximumHealth,
                currentHealth = checkpoint.currentHealth,
                maximumEnergy = checkpoint.maximumEnergy,
                currentEnergy = checkpoint.currentEnergy,
                healthPotionCharges = checkpoint.healthPotionCharges,
                accumulatedRenown = checkpoint.accumulatedRenown,
                fortunaCharges = checkpoint.fortuneOfferBookSnapshot.charges - 1,
                pactIds = checkpoint.pactIds,
                pendingOfferIds = Array.Empty<string>(),
                offerRerollCounts = Array.Empty<int>(),
                collectedItemIds = checkpoint.collectedItemIds,
                claimedRewardIds = checkpoint.claimedRewardIds
            };

            return new LegacyExpeditionSaveData
            {
                runId = state.runId,
                seed = state.seed,
                generationVersion = state.generationVersion,
                contentVersion = version,
                classId = state.classId,
                raceId = state.raceId,
                selectedSkillIds = state.selectedSkillIds,
                startingEquipmentInstanceIds = state.startingEquipmentInstanceIds,
                routeSnapshotJson = state.routeSnapshotJson,
                currentNodeId = state.currentNodeId,
                visitedNodeIds = state.visitedNodeIds,
                completedRoomIds = state.completedRoomIds,
                completedRoomResults = state.completedRoomResults,
                roomEntryCheckpointJson = JsonUtility.ToJson(legacyCheckpoint),
                inventorySnapshotJson = state.inventorySnapshotJson,
                maximumHealth = state.maximumHealth,
                currentHealth = state.currentHealth,
                maximumEnergy = state.maximumEnergy,
                currentEnergy = state.currentEnergy,
                healthPotionCharges = state.healthPotionCharges,
                accumulatedRenown = state.accumulatedRenown,
                pactIds = state.pactIds,
                fortunaCharges = state.fortunaCharges,
                pendingOfferIds = includeUnrepresentableOffer ? new[] { "offer.legacy" } : Array.Empty<string>(),
                offerRerollCounts = includeUnrepresentableOffer ? new[] { 1 } : Array.Empty<int>(),
                collectedItemIds = state.collectedItemIds,
                claimedRewardIds = state.claimedRewardIds
            };
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class LegacyProfileEnvelopeV1
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileV1 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveData activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileEnvelopeV2
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileV2 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveData activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileV1
        {
            public int availableRenown;
            public int healthUpgradeLevel;
            public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel;
            public string[] unlockedRaceIds;
            public string[] unlockedStartingKitIds;
            public string[] acceptedContractIds;
            public string[] claimedContractIds;
            public string[] readStoryFragmentIds;
            public ContractCounterSaveData[] contractCounters;
            public string[] processedSettlementRunIds;

            public static LegacyProfileV1 Create() => new LegacyProfileV1
            {
                unlockedRaceIds = new[] { "race.human" },
                unlockedStartingKitIds = new[]
                {
                    "weapon.bellator.rusted_sword", "weapon.venator.short_bow", "weapon.magus.ember_staff"
                },
                acceptedContractIds = Array.Empty<string>(),
                claimedContractIds = Array.Empty<string>(),
                readStoryFragmentIds = Array.Empty<string>(),
                contractCounters = Array.Empty<ContractCounterSaveData>(),
                processedSettlementRunIds = Array.Empty<string>()
            };
        }

        [Serializable]
        private sealed class LegacyProfileV2
        {
            public int availableRenown;
            public int healthUpgradeLevel;
            public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel;
            public string[] unlockedRaceIds;
            public string[] unlockedStartingKitIds;
            public string[] acceptedContractIds;
            public string[] claimedContractIds;
            public string[] readStoryFragmentIds;
            public ContractCounterSaveData[] contractCounters;
            public string[] processedSettlementRunIds;
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions;

            public static LegacyProfileV2 Create() => new LegacyProfileV2
            {
                unlockedRaceIds = new[] { "race.human" },
                unlockedStartingKitIds = new[]
                {
                    "weapon.bellator.rusted_sword", "weapon.venator.short_bow", "weapon.magus.ember_staff"
                },
                acceptedContractIds = Array.Empty<string>(),
                claimedContractIds = Array.Empty<string>(),
                readStoryFragmentIds = Array.Empty<string>(),
                contractCounters = Array.Empty<ContractCounterSaveData>(),
                processedSettlementRunIds = Array.Empty<string>(),
                progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>()
            };
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveData
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
            public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
            public string roomEntryCheckpointJson;
            public string inventorySnapshotJson;
            public int maximumHealth = 100;
            public int currentHealth = 100;
            public int maximumEnergy = 50;
            public int currentEnergy = 50;
            public int healthPotionCharges = 2;
            public int accumulatedRenown;
            public string[] pactIds = Array.Empty<string>();
            public int fortunaCharges;
            public string[] pendingOfferIds = Array.Empty<string>();
            public int[] offerRerollCounts = Array.Empty<int>();
            public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyRoomCheckpoint
        {
            public string roomNodeId;
            public string inventorySnapshotJson;
            public int maximumHealth;
            public int currentHealth;
            public int maximumEnergy;
            public int currentEnergy;
            public int healthPotionCharges;
            public int accumulatedRenown;
            public int fortunaCharges;
            public string[] pactIds = Array.Empty<string>();
            public string[] pendingOfferIds = Array.Empty<string>();
            public int[] offerRerollCounts = Array.Empty<int>();
            public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
        }
    }
}
