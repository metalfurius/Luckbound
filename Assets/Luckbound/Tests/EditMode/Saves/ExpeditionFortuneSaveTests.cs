using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.App
{
    public sealed class ExpeditionFortuneSaveTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "Luckbound-ExpeditionFortune-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrWhiteSpace(_directory) && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [Test]
        public void MapperCloneAndRestorePreserveCompleteOfferBookAndItsNextReroll()
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData state = CreatePreparedExpedition(run, out FortuneOfferBookSnapshot expected);

            ExpeditionRunConfiguration restoredRun = ExpeditionSaveMapper.Restore(state, out ExpeditionSaveData resumed);

            AssertSnapshot(expected, resumed.fortuneOfferBookSnapshot);
            Assert.That(restoredRun.RunId, Is.EqualTo(run.RunId));
            Assert.That(restoredRun.Route.CanonicalForm, Is.EqualTo(run.Route.CanonicalForm),
                "Fortune restore must leave the independently seeded route unchanged.");
            FortuneRerollResult expectedNext = FortuneOfferBook.Restore(expected).Reroll("offer.pending");
            FortuneRerollResult restoredNext = FortuneOfferBook.Restore(resumed.fortuneOfferBookSnapshot).Reroll("offer.pending");
            Assert.That(restoredNext.Status, Is.EqualTo(expectedNext.Status));
            Assert.That(restoredNext.RemainingCharges, Is.EqualTo(expectedNext.RemainingCharges));
            CollectionAssert.AreEqual(expectedNext.Offer.OptionIds, restoredNext.Offer.OptionIds);
        }

        [Test]
        public void RepositoryRestartPreservesCompleteBookAfterRerollChoiceAndClaim()
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData state = CreatePreparedExpedition(run, out FortuneOfferBookSnapshot expected);
            var repository = NewRepository();
            SaveWriteResult write = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), state));
            Assert.That(write.Succeeded, Is.True, write.Message);
            string savedJson = File.ReadAllText(NewFileStore().GetPrimaryPath(SaveDocument.Profile));
            Assert.That(savedJson, Does.Contain("fortuneOfferBookSnapshot"));
            Assert.That(savedJson, Does.Not.Contain("pendingOfferIds"));
            Assert.That(savedJson, Does.Not.Contain("offerRerollCounts"));
            Assert.That(savedJson, Does.Not.Contain("\"fortunaCharges\""));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            AssertSnapshot(expected, loaded.Data.activeExpedition.fortuneOfferBookSnapshot);
            ExpeditionSaveMapper.Restore(loaded.Data.activeExpedition, out ExpeditionSaveData resumed);
            AssertSnapshot(expected, resumed.fortuneOfferBookSnapshot);
        }

        [Test]
        public void RepositoryRetryUsesTheSameSerializedFortunePayload()
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData state = CreatePreparedExpedition(run, out FortuneOfferBookSnapshot expected);
            var storage = new RecordingFailOnceStore(NewFileStore());
            var repository = new SaveRepository(storage);

            SaveWriteResult first = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), state));
            string pending = repository.ExportPendingJson(SaveDocument.Profile);
            SaveWriteResult retry = Wait(repository.RetryPendingAsync(SaveDocument.Profile));
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(first.Succeeded, Is.False);
            Assert.That(retry.Succeeded, Is.True, retry.Message);
            Assert.That(storage.Payloads.Count, Is.EqualTo(2));
            Assert.That(storage.Payloads[0], Is.EqualTo(storage.Payloads[1]));
            Assert.That(storage.Payloads[0], Is.EqualTo(pending));
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            AssertSnapshot(expected, loaded.Data.activeExpedition.fortuneOfferBookSnapshot);
        }

        [Test]
        public void RepositoryRestartRestoresTheCommittedBookFromTheRoomEntryCheckpoint()
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData entry = CreatePreparedExpedition(run, out FortuneOfferBookSnapshot committed);
            var repository = NewRepository();
            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), entry)).Succeeded, Is.True);

            ExpeditionSaveData duringRoom = entry.Clone();
            FortuneOfferBook midRoomBook = FortuneOfferBook.Restore(duringRoom.fortuneOfferBookSnapshot);
            Assert.That(midRoomBook.GrantGuardianCharge("guardian.during-room").Succeeded, Is.True);
            duringRoom.fortuneOfferBookSnapshot = midRoomBook.CreateSnapshot();
            duringRoom.accumulatedRenown = 31;
            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), duringRoom)).Succeeded, Is.True);

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            FortuneOfferBookSnapshot savedMidRoom = loaded.Data.activeExpedition.fortuneOfferBookSnapshot;
            ExpeditionSaveMapper.Restore(loaded.Data.activeExpedition, out ExpeditionSaveData resumed);

            AssertSnapshot(committed, resumed.fortuneOfferBookSnapshot);
            Assert.That(savedMidRoom.processedGuardianIds, Does.Contain("guardian.during-room"));
            Assert.That(resumed.fortuneOfferBookSnapshot.processedGuardianIds, Does.Not.Contain("guardian.during-room"));
            Assert.That(resumed.accumulatedRenown, Is.EqualTo(entry.accumulatedRenown));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void LegacyProfileDocumentsMigrateChargesIntoOneBookAndKeepCheckpointState(int oldContentVersion)
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData state = ExpeditionSaveMapper.Create(run);
            string legacyJson = CreateLegacyDocument(state, oldContentVersion, includeIncompleteOffer: false);
            string profilePath = NewFileStore().GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllText(profilePath, legacyJson, new UTF8Encoding(false));

            SaveRepository repository = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> migrated = Wait(repository.LoadProfileAsync());

            Assert.That(migrated.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), migrated.Message);
            Assert.That(migrated.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(migrated.Data.activeExpedition.fortunaCharges, Is.EqualTo(run.StartingFortunaCharges));
            Assert.That(migrated.Data.activeExpedition.fortuneOfferBookSnapshot.seed, Is.EqualTo(run.Seed));
            Assert.That(migrated.Data.activeExpedition.fortuneOfferBookSnapshot.generationVersion, Is.EqualTo(run.Route.GenerationVersion));
            Assert.That(migrated.Data.activeExpedition.fortuneOfferBookSnapshot.raceId, Is.EqualTo(run.RaceId));
            Assert.That(migrated.Data.activeExpedition.fortuneOfferBookSnapshot.offers, Is.Empty);
            ExpeditionSaveMapper.Restore(migrated.Data.activeExpedition, out ExpeditionSaveData migratedEntry);
            Assert.That(migratedEntry.fortunaCharges, Is.EqualTo(run.StartingFortunaCharges - 1),
                "Legacy checkpoint charges migrate independently from the in-room snapshot.");

            Assert.That(Wait(repository.SaveProfileAsync(migrated.Data.profile, migrated.Data.activeExpedition)).Succeeded, Is.True);
            SaveLoadResult<ProfileSaveEnvelope> afterRestart = Wait(NewRepository().LoadProfileAsync());
            Assert.That(afterRestart.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), afterRestart.Message);
            Assert.That(afterRestart.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(afterRestart.Data.activeExpedition.fortunaCharges, Is.EqualTo(run.StartingFortunaCharges));
        }

        [Test]
        public void LegacyOfferSummaryWithoutOptionsIsPreservedAsCorruptedInsteadOfInventingAnOffer()
        {
            ExpeditionSaveData state = ExpeditionSaveMapper.Create(CreateRun());
            string legacyJson = CreateLegacyDocument(state, 2, includeIncompleteOffer: true);
            string profilePath = NewFileStore().GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllText(profilePath, legacyJson, new UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(legacyJson));
        }

        [Test]
        public void ChecksummedButStructurallyInvalidFortuneSnapshotIsRejectedAndPreserved()
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionSaveData state = CreatePreparedExpedition(run, out _);
            state.fortuneOfferBookSnapshot.offers[0].history[0].optionIds[1] =
                state.fortuneOfferBookSnapshot.offers[0].history[0].optionIds[0];
            var envelope = NewCurrentEnvelope(state);
            string json = EncodeChecksummed(envelope);
            string profilePath = NewFileStore().GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllText(profilePath, json, new UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(json));
        }

        private static ExpeditionRunConfiguration CreateRun() => ExpeditionRunConfigurationFactory.Create(
            "class.bellator", "race.human", new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });

        private static ExpeditionSaveData CreatePreparedExpedition(ExpeditionRunConfiguration run,
            out FortuneOfferBookSnapshot snapshot)
        {
            var book = new FortuneOfferBook(run.Route.GenerationVersion, run.Seed, run.RaceId);
            Assert.That(book.GrantGuardianCharge("guardian.reward").Succeeded, Is.True);
            string[] options = { "relic.cinder", "relic.iron", "relic.mirror", "relic.ember", "relic.ivory", "relic.ash", "relic.moon", "relic.root" };
            FortuneOfferResult claimedOffer = book.CreateOffer("offer.claimed", "context.guardian", options);
            Assert.That(claimedOffer.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Reroll("offer.claimed").Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Choose("offer.claimed", book.GetOffer("offer.claimed").OptionIds[0]).Succeeded, Is.True);
            Assert.That(book.Claim("offer.claimed").Succeeded, Is.True);

            FortuneOfferResult pending = book.CreateOffer("offer.pending", "context.treasure",
                options.Select(option => option + ".second"));
            Assert.That(pending.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Reroll("offer.pending").Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            snapshot = book.CreateSnapshot();

            ExpeditionSaveData state = ExpeditionSaveMapper.Create(run);
            state.fortuneOfferBookSnapshot = snapshot;
            state.roomEntryCheckpointJson = ExpeditionSaveMapper.SerializeCheckpoint(
                ExpeditionSaveMapper.CreateCheckpoint(state));
            return state;
        }

        private SaveRepository NewRepository() => new SaveRepository(NewFileStore());
        private FileSaveStore NewFileStore() => new FileSaveStore(_directory, null);
        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private static void AssertSnapshot(FortuneOfferBookSnapshot expected, FortuneOfferBookSnapshot actual)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.snapshotVersion, Is.EqualTo(expected.snapshotVersion));
            Assert.That(actual.generationVersion, Is.EqualTo(expected.generationVersion));
            Assert.That(actual.seed, Is.EqualTo(expected.seed));
            Assert.That(actual.raceId, Is.EqualTo(expected.raceId));
            Assert.That(actual.charges, Is.EqualTo(expected.charges));
            CollectionAssert.AreEqual(expected.processedGuardianIds, actual.processedGuardianIds);
            Assert.That(actual.offers.Length, Is.EqualTo(expected.offers.Length));
            for (int index = 0; index < expected.offers.Length; index++)
            {
                FortuneOfferSnapshot left = expected.offers[index];
                FortuneOfferSnapshot right = actual.offers[index];
                Assert.That(right.offerId, Is.EqualTo(left.offerId));
                Assert.That(right.contextId, Is.EqualTo(left.contextId));
                CollectionAssert.AreEqual(left.eligibleOptionIds, right.eligibleOptionIds);
                Assert.That(right.selectedOptionId, Is.EqualTo(left.selectedOptionId));
                Assert.That(right.claimed, Is.EqualTo(left.claimed));
                Assert.That(right.history.Length, Is.EqualTo(left.history.Length));
                for (int historyIndex = 0; historyIndex < left.history.Length; historyIndex++)
                    CollectionAssert.AreEqual(left.history[historyIndex].optionIds, right.history[historyIndex].optionIds);
            }
        }

        private static ProfileSaveEnvelope NewCurrentEnvelope(ExpeditionSaveData expedition) => new ProfileSaveEnvelope
        {
            schemaVersion = SaveFormat.SchemaVersion,
            contentVersion = SaveFormat.ContentVersion,
            generation = 1,
            sequence = 1,
            savedAtUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            checksum = string.Empty,
            profile = ProfileSaveData.CreateNewProfile(),
            hasActiveExpedition = true,
            activeExpedition = expedition
        };

        private static string EncodeChecksummed(ProfileSaveEnvelope envelope)
        {
            envelope.checksum = string.Empty;
            envelope.checksum = Hash(JsonUtility.ToJson(envelope, false));
            return JsonUtility.ToJson(envelope, true);
        }

        private static string CreateLegacyDocument(ExpeditionSaveData state, int version, bool includeIncompleteOffer)
        {
            LegacyExpeditionSaveData legacyExpedition = ToLegacyExpedition(state, version, includeIncompleteOffer);
            if (version == 1)
            {
                var envelope = new LegacyProfileEnvelopeV1
                {
                    schemaVersion = SaveFormat.SchemaVersion,
                    contentVersion = version,
                    generation = 4,
                    sequence = 11,
                    savedAtUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
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
                savedAtUtc = DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                checksum = string.Empty,
                profile = LegacyProfileV2.Create(),
                hasActiveExpedition = true,
                activeExpedition = legacyExpedition
            };
            v2.checksum = Hash(JsonUtility.ToJson(v2, false));
            return JsonUtility.ToJson(v2, true);
        }

        private static LegacyExpeditionSaveData ToLegacyExpedition(ExpeditionSaveData state, int version,
            bool includeIncompleteOffer)
        {
            ExpeditionRoomCheckpointSaveData checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(state.roomEntryCheckpointJson);
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
                pendingOfferIds = includeIncompleteOffer ? new[] { "offer.legacy" } : Array.Empty<string>(),
                offerRerollCounts = Array.Empty<int>(),
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

        private sealed class RecordingFailOnceStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _fail = true;
            public RecordingFailOnceStore(ISaveStore inner) { _inner = inner; }
            public System.Collections.Generic.List<string> Payloads { get; } = new System.Collections.Generic.List<string>();
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);
            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                Payloads.Add(json);
                if (_fail)
                {
                    _fail = false;
                    throw new IOException("simulated storage failure");
                }
                return _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
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
                unlockedStartingKitIds = new[] { "weapon.bellator.rusted_sword", "weapon.venator.short_bow", "weapon.magus.ember_staff" },
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
                unlockedStartingKitIds = new[] { "weapon.bellator.rusted_sword", "weapon.venator.short_bow", "weapon.magus.ember_staff" },
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
