using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveLegacyReadOnlyRepositoryTests
    {
        private const string P0Fixture = "V6-P0-profile-no-active.Frozen.envelope.json";
        private const string P1Fixture = "V6-P1-active-known-nontrivial.Frozen.envelope.json";
        private const string P2NullFixture = "V6-P2-active-unknown-null.Frozen.envelope.json";
        private const string P2InlineEmptyFixture = "V6-P2-active-unknown-inline-empty.Frozen.envelope.json";
        private const string P3Fixture = "V6-P3-receipts-full.Frozen.envelope.json";
        private const string P4Fixture = "V6-P4-null-empty-boundary.Frozen.envelope.json";
        private const string P5Fixture = "V6-P5-flag-payload-conflict.Frozen.envelope.json";
        private const string SettingsFixture = "V6-S0-settings.Frozen.envelope.json";
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "LuckboundLegacyReadOnlyRepositoryTests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [TestCase(P1Fixture)]
        [TestCase(P2NullFixture)]
        [TestCase(P2InlineEmptyFixture)]
        [TestCase(P3Fixture)]
        public void ValidActiveV6PrimaryIsUsableReadOnlyAndExportsTheSelectedBytes(string fixtureName)
        {
            string primary = ReadFixture(fixtureName);
            string olderBackup = ReadFixture(P0Fixture);
            var store = new RecordingSaveStore(profilePrimaryJson: primary, profileBackupJson: olderBackup);
            var repository = new SaveRepository(store);

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.Data, Is.Not.Null);
            Assert.That(loaded.HasUsableData, Is.True);
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(primary));
            Assert.That(store.ProfilePrimaryJson, Is.EqualTo(primary));
            Assert.That(store.ProfileBackupJson, Is.EqualTo(olderBackup));
            AssertNoProfileWrites(store);
        }

        [TestCase(P1Fixture)]
        [TestCase(P2NullFixture)]
        [TestCase(P2InlineEmptyFixture)]
        [TestCase(P3Fixture)]
        public void ValidActiveV6BackupIsRecoveredReadOnlyWithoutRepair(string fixtureName)
        {
            const string corruptPrimary = "{ corrupt primary bytes";
            string backup = ReadFixture(fixtureName);
            var store = new RecordingSaveStore(profilePrimaryJson: corruptPrimary, profileBackupJson: backup);
            var repository = new SaveRepository(store);

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(loaded.Data, Is.Not.Null);
            Assert.That(loaded.HasUsableData, Is.True);
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(backup));
            Assert.That(store.ProfilePrimaryJson, Is.EqualTo(corruptPrimary));
            Assert.That(store.ProfileBackupJson, Is.EqualTo(backup));
            AssertNoProfileWrites(store);
        }

        [Test]
        public void P0AndMissingProfilesRemainReadWriteAndMissingHasNoLoadedJson()
        {
            string p0 = ReadFixture(P0Fixture);
            var loadedStore = new RecordingSaveStore(profilePrimaryJson: p0);
            var loadedRepository = new SaveRepository(loadedStore);

            Assert.That(loadedRepository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(loadedRepository.ExportLoadedProfileJson(), Is.Null);
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(loadedRepository.LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.HasUsableData, Is.True);
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(loadedRepository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(loadedRepository.ExportLoadedProfileJson(), Is.EqualTo(p0));

            var missingRepository = new SaveRepository(new RecordingSaveStore());
            Assert.That(missingRepository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable));
            SaveLoadResult<ProfileSaveEnvelope> missing = Wait(missingRepository.LoadProfileAsync());
            Assert.That(missing.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(missing.HasUsableData, Is.True);
            Assert.That(missing.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(missingRepository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(missingRepository.ExportLoadedProfileJson(), Is.Null);
            SaveLoadResult<SettingsSaveEnvelope> missingSettings = Wait(missingRepository.LoadSettingsAsync());
            Assert.That(missingSettings.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(missingSettings.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(missingSettings.Data, Is.Not.Null);
        }

        [Test]
        public void ActiveV6CapabilitySurvivesMutableDtoChangesAndRejectsEveryProfileWriteShape()
        {
            string sourceJson = ReadFixture(P1Fixture);
            string olderBackup = ReadFixture(P0Fixture);
            var store = new RecordingSaveStore(profilePrimaryJson: sourceJson, profileBackupJson: olderBackup);
            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());
            ExpeditionSaveData sameExpedition = loaded.Data.activeExpedition.Clone();
            ProfileSaveData attemptedProfile = loaded.Data.profile.Clone();
            Array.Reverse(attemptedProfile.unlockedRaceIds);
            string attemptedProfileBefore = JsonUtility.ToJson(attemptedProfile, false);
            int generationBefore = loaded.Data.generation;
            long sequenceBefore = loaded.Data.sequence;

            // These mutable fields came from the public DTO. They cannot rewrite the source-based capability.
            loaded.Data.schemaVersion = 0;
            loaded.Data.contentVersion = 5;
            loaded.Data.hasActiveExpedition = false;
            loaded.Data.activeExpedition = null;

            ExpeditionSaveData modifiedExpedition = sameExpedition.Clone();
            modifiedExpedition.currentHealth = Math.Max(1, modifiedExpedition.currentHealth - 1);
            ExpeditionSaveData substitutedExpedition = sameExpedition.Clone();
            substitutedExpedition.runId = "000000000000000000000000000000b2";
            ProfileSaveData modifiedProfile = attemptedProfile.Clone();
            modifiedProfile.availableRenown = checked(modifiedProfile.availableRenown + 1);

            SaveWriteResult[] attempts =
            {
                Wait(repository.SaveProfileAsync(attemptedProfile)),
                Wait(repository.SaveProfileAsync(attemptedProfile, null)),
                Wait(repository.SaveProfileAsync(attemptedProfile, sameExpedition)),
                Wait(repository.SaveProfileAsync(attemptedProfile, modifiedExpedition)),
                Wait(repository.SaveProfileAsync(attemptedProfile, substitutedExpedition)),
                Wait(repository.SaveProfileAsync(modifiedProfile)),
                Wait(repository.SaveProfileAsync(null, null)),
                Wait(repository.RetryPendingAsync(SaveDocument.Profile))
            };

            Assert.That(attempts.All(result => !result.Succeeded), Is.True);
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(sourceJson));
            Assert.That(JsonUtility.ToJson(attemptedProfile, false), Is.EqualTo(attemptedProfileBefore));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(repository.CurrentProfile.generation, Is.EqualTo(generationBefore));
            Assert.That(repository.CurrentProfile.sequence, Is.EqualTo(sequenceBefore));
            Assert.That(store.ProfilePrimaryJson, Is.EqualTo(sourceJson));
            Assert.That(store.ProfileBackupJson, Is.EqualTo(olderBackup));
            AssertNoProfileWrites(store);
        }

        [TestCase(P4Fixture)]
        [TestCase(P5Fixture)]
        public void SemanticallyInvalidV6WithValidChecksumRemainsUnavailable(string fixtureName)
        {
            string invalidV6 = ReadFixture(fixtureName);
            var store = new RecordingSaveStore(profilePrimaryJson: invalidV6);
            var repository = new SaveRepository(store);

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());
            SaveWriteResult write = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile()));

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(loaded.Data, Is.Null);
            Assert.That(loaded.HasUsableData, Is.False);
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(repository.ExportLoadedProfileJson(), Is.Null);
            Assert.That(write.Succeeded, Is.False);
            Assert.That(store.ProfilePrimaryJson, Is.EqualTo(invalidV6));
            AssertNoProfileWrites(store);
        }

        [Test]
        public void CurrentWriterKeepsReadWriteUntilActiveV6IsReloaded()
        {
            var store = new RecordingSaveStore();
            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> missing = Wait(repository.LoadProfileAsync());
            ProfileSaveEnvelope measuredActiveV6 = JsonUtility.FromJson<ProfileSaveEnvelope>(ReadFixture(P1Fixture));

            Assert.That(missing.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(measuredActiveV6, Is.Not.Null);
            SaveWriteResult write = Wait(repository.SaveProfileAsync(measuredActiveV6.profile.Clone(),
                measuredActiveV6.activeExpedition.Clone()));

            Assert.That(write.Succeeded, Is.True, write.Message);
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(store.ProfilePrimaryJson, Is.Not.Null.And.Not.Empty);
            var restartedRepository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> reloaded = Wait(restartedRepository.LoadProfileAsync());

            Assert.That(reloaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(reloaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(restartedRepository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(restartedRepository.ExportLoadedProfileJson(), Is.EqualTo(store.ProfilePrimaryJson));
        }

        [Test]
        public void SettingsFailureAndRetryAreIndependentFromProtectedProfileAndReuseExactJson()
        {
            string protectedProfile = ReadFixture(P1Fixture);
            string backup = ReadFixture(P0Fixture);
            string settings = ReadFixture(SettingsFixture);
            var store = new RecordingSaveStore(profilePrimaryJson: protectedProfile,
                profileBackupJson: backup, settingsPrimaryJson: settings);
            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> profileLoad = Wait(repository.LoadProfileAsync());
            SaveLoadResult<SettingsSaveEnvelope> settingsLoad = Wait(repository.LoadSettingsAsync());
            GameSettingsSaveData changedSettings = settingsLoad.Data.settings.Clone();
            changedSettings.languageId = "fr";
            store.FailNextSettingsWrite = true;

            SaveWriteResult failed = Wait(repository.SaveSettingsAsync(changedSettings));
            string pendingJson = repository.ExportPendingJson(SaveDocument.Settings);

            Assert.That(profileLoad.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(settingsLoad.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(pendingJson, Is.Not.Null.And.Not.Empty);
            Assert.That(store.WriteCalls.Count, Is.EqualTo(1));
            Assert.That(store.WriteCalls[0].Document, Is.EqualTo(SaveDocument.Settings));
            Assert.That(store.WriteCalls[0].Json, Is.EqualTo(pendingJson));
            AssertNoProfileWrites(store);

            SaveWriteResult retried = Wait(repository.RetryPendingAsync(SaveDocument.Settings));

            Assert.That(retried.Succeeded, Is.True, retried.Message);
            Assert.That(store.WriteCalls.Count, Is.EqualTo(2));
            Assert.That(store.WriteCalls[1].Document, Is.EqualTo(SaveDocument.Settings));
            Assert.That(store.WriteCalls[1].Json, Is.EqualTo(pendingJson));
            Assert.That(store.WriteCalls[1].LastKnownGoodPrimaryJson, Is.EqualTo(settings));
            Assert.That(store.SettingsBackupJson, Is.EqualTo(settings));
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(protectedProfile));
            Assert.That(store.ProfilePrimaryJson, Is.EqualTo(protectedProfile));
            Assert.That(store.ProfileBackupJson, Is.EqualTo(backup));
            Assert.That(repository.ExportPendingJson(SaveDocument.Settings), Is.Null);
        }

        [Test]
        public void FileSaveStoreKeepsBothProtectedProfileFilesByteExactWhileSettingsWrites()
        {
            Directory.CreateDirectory(_directory);
            string primary = ReadFixture(P1Fixture);
            string backup = ReadFixture(P0Fixture);
            string settings = ReadFixture(SettingsFixture);
            var store = new FileSaveStore(_directory, null);
            string profilePrimaryPath = store.GetPrimaryPath(SaveDocument.Profile);
            string profileBackupPath = store.GetBackupPath(SaveDocument.Profile);
            string settingsPrimaryPath = store.GetPrimaryPath(SaveDocument.Settings);
            File.WriteAllBytes(profilePrimaryPath, Utf8NoBom.GetBytes(primary));
            File.WriteAllBytes(profileBackupPath, Utf8NoBom.GetBytes(backup));
            File.WriteAllBytes(settingsPrimaryPath, Utf8NoBom.GetBytes(settings));
            byte[] profilePrimaryBefore = File.ReadAllBytes(profilePrimaryPath);
            byte[] profileBackupBefore = File.ReadAllBytes(profileBackupPath);

            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> profileLoad = Wait(repository.LoadProfileAsync());
            SaveLoadResult<SettingsSaveEnvelope> settingsLoad = Wait(repository.LoadSettingsAsync());
            SaveWriteResult rejectedProfileWrite = Wait(repository.SaveProfileAsync(profileLoad.Data.profile.Clone(),
                profileLoad.Data.activeExpedition.Clone()));
            GameSettingsSaveData changedSettings = settingsLoad.Data.settings.Clone();
            changedSettings.musicVolume = 0.4f;
            SaveWriteResult settingsWrite = Wait(repository.SaveSettingsAsync(changedSettings));

            Assert.That(profileLoad.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(settingsLoad.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(rejectedProfileWrite.Succeeded, Is.False);
            Assert.That(settingsWrite.Succeeded, Is.True, settingsWrite.Message);
            CollectionAssert.AreEqual(profilePrimaryBefore, File.ReadAllBytes(profilePrimaryPath));
            CollectionAssert.AreEqual(profileBackupBefore, File.ReadAllBytes(profileBackupPath));
            Assert.That(File.ReadAllText(store.GetBackupPath(SaveDocument.Settings), Encoding.UTF8), Is.EqualTo(settings));

            var restartedRepository = new SaveRepository(new FileSaveStore(_directory, null));
            SaveLoadResult<ProfileSaveEnvelope> reloaded = Wait(restartedRepository.LoadProfileAsync());
            Assert.That(reloaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(restartedRepository.ExportLoadedProfileJson(), Is.EqualTo(primary));
            CollectionAssert.AreEqual(profilePrimaryBefore, File.ReadAllBytes(profilePrimaryPath));
            CollectionAssert.AreEqual(profileBackupBefore, File.ReadAllBytes(profileBackupPath));
        }

        [UnityTest]
        public IEnumerator PendingReadWriteProfileBlocksConcurrentReloadThenRetriesSameJson()
        {
            var store = new RecordingSaveStore();
            var repository = new SaveRepository(store);
            Task<SaveLoadResult<ProfileSaveEnvelope>> initialLoadTask = repository.LoadProfileAsync();
            yield return WaitForTaskCompletion(initialLoadTask, "Initial profile load did not complete within the frame limit.");
            SaveLoadResult<ProfileSaveEnvelope> missing = initialLoadTask.GetAwaiter().GetResult();
            int profileReadsBeforeSave = store.ReadCount(SaveDocument.Profile);
            int generationBeforeSave = repository.CurrentProfile.generation;
            long sequenceBeforeSave = repository.CurrentProfile.sequence;
            store.HoldAndFailNextProfileWrite();

            Task<SaveWriteResult> saveTask = repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile());
            yield return WaitForTaskCompletion(store.HeldProfileWriteEntered, "The profile write did not reach its TCS barrier within the frame limit.");
            store.HeldProfileWriteEntered.GetAwaiter().GetResult();
            Task<SaveLoadResult<ProfileSaveEnvelope>> reloadTask = repository.LoadProfileAsync();
            store.ReleaseHeldProfileWrite();
            yield return WaitForTaskCompletion(saveTask, "The failed profile save did not complete within the frame limit.");
            SaveWriteResult failed = saveTask.GetAwaiter().GetResult();
            string pendingJson = repository.ExportPendingJson(SaveDocument.Profile);

            Assert.That(missing.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(failed.Succeeded, Is.False);
            Assert.That(pendingJson, Is.Not.Null.And.Not.Empty);
            Assert.That(store.WriteCalls.Count, Is.EqualTo(1));
            Assert.That(store.WriteCalls[0].Json, Is.EqualTo(pendingJson));
            Assert.That(repository.CurrentProfile.generation, Is.EqualTo(generationBeforeSave));
            Assert.That(repository.CurrentProfile.sequence, Is.EqualTo(sequenceBeforeSave));
            yield return WaitForTaskCompletion(reloadTask, "The concurrent profile reload did not complete within the frame limit.");
            Assert.Throws<InvalidOperationException>(() => reloadTask.GetAwaiter().GetResult());
            Assert.That(store.ReadCount(SaveDocument.Profile), Is.EqualTo(profileReadsBeforeSave));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.EqualTo(pendingJson));

            Task<SaveWriteResult> retryTask = repository.RetryPendingAsync(SaveDocument.Profile);
            yield return WaitForTaskCompletion(retryTask, "The exact profile retry did not complete within the frame limit.");
            SaveWriteResult retry = retryTask.GetAwaiter().GetResult();

            Assert.That(retry.Succeeded, Is.True, retry.Message);
            Assert.That(store.WriteCalls.Count, Is.EqualTo(2));
            Assert.That(store.WriteCalls[1].Json, Is.EqualTo(pendingJson));
            Assert.That(store.WriteCalls[1].LastKnownGoodPrimaryJson, Is.Null);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(repository.CurrentProfile.sequence, Is.EqualTo(sequenceBeforeSave + 1));
        }

        [Test]
        public void IncompatibleSettingsReturnUnavailableWithoutChangingTheProfilePolicy()
        {
            string profile = ReadFixture(P1Fixture);
            const string newerSettings = "{\"schemaVersion\":1,\"contentVersion\":7}";
            var store = new RecordingSaveStore(profilePrimaryJson: profile, settingsPrimaryJson: newerSettings);
            var repository = new SaveRepository(store);
            Wait(repository.LoadProfileAsync());

            SaveLoadResult<SettingsSaveEnvelope> settings = Wait(repository.LoadSettingsAsync());
            SaveWriteResult rejected = Wait(repository.SaveSettingsAsync(new GameSettingsSaveData()));

            Assert.That(settings.Status, Is.EqualTo(SaveLoadStatus.Incompatible));
            Assert.That(settings.Access, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(settings.Data, Is.Null);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(store.SettingsPrimaryJson, Is.EqualTo(newerSettings));
            Assert.That(store.WriteCalls, Is.Empty);
        }

        [Test]
        public void CorruptSettingsFallbackUsesWritableDefaults()
        {
            const string corruptSettings = "{ broken settings";
            var store = new RecordingSaveStore(settingsPrimaryJson: corruptSettings);
            var repository = new SaveRepository(store);

            SaveLoadResult<SettingsSaveEnvelope> loaded = Wait(repository.LoadSettingsAsync());
            SaveWriteResult write = Wait(repository.SaveSettingsAsync(loaded.Data.settings.Clone()));

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(loaded.Data, Is.Not.Null);
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(write.Succeeded, Is.True, write.Message);
            Assert.That(store.WriteCalls.Count, Is.EqualTo(1));
            Assert.That(store.WriteCalls[0].Document, Is.EqualTo(SaveDocument.Settings));
        }

        private static string ReadFixture(string fileName)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, "Assets", "Luckbound", "Tests", "Fixtures", "Saves", "V6", fileName);
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static void AssertNoProfileWrites(RecordingSaveStore store)
        {
            Assert.That(store.WriteCalls.Any(call => call.Document == SaveDocument.Profile), Is.False,
                "A protected or unavailable profile must not reach the store write boundary.");
        }

        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private const int AsyncTaskFrameLimit = 600;

        private static IEnumerator WaitForTaskCompletion(Task task, string failureMessage)
        {
            for (int frame = 0; frame < AsyncTaskFrameLimit && !task.IsCompleted; frame++)
                yield return null;

            Assert.That(task.IsCompleted, Is.True, failureMessage);
        }

        private sealed class RecordingSaveStore : ISaveStore
        {
            private readonly TaskCompletionSource<bool> _heldProfileWriteEntered =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _releaseHeldProfileWrite =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private bool _holdAndFailNextProfileWrite;

            public RecordingSaveStore(string profilePrimaryJson = null, string profileBackupJson = null,
                string settingsPrimaryJson = null, string settingsBackupJson = null)
            {
                ProfilePrimaryJson = profilePrimaryJson;
                ProfileBackupJson = profileBackupJson;
                SettingsPrimaryJson = settingsPrimaryJson;
                SettingsBackupJson = settingsBackupJson;
            }

            public string ProfilePrimaryJson { get; private set; }
            public string ProfileBackupJson { get; private set; }
            public string SettingsPrimaryJson { get; private set; }
            public string SettingsBackupJson { get; private set; }
            public bool FailNextProfileWrite { get; set; }
            public bool FailNextSettingsWrite { get; set; }
            public List<SaveDocument> ReadCalls { get; } = new List<SaveDocument>();
            public List<StoreWriteCall> WriteCalls { get; } = new List<StoreWriteCall>();
            public Task HeldProfileWriteEntered => _heldProfileWriteEntered.Task;

            public int ReadCount(SaveDocument document) => ReadCalls.Count(call => call == document);

            public void HoldAndFailNextProfileWrite()
            {
                _holdAndFailNextProfileWrite = true;
            }

            public void ReleaseHeldProfileWrite()
            {
                _releaseHeldProfileWrite.TrySetResult(true);
            }

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                ReadCalls.Add(document);
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(ProfilePrimaryJson, ProfileBackupJson)
                    : new SaveStorageSnapshot(SettingsPrimaryJson, SettingsBackupJson));
            }

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                WriteCalls.Add(new StoreWriteCall(document, json, lastKnownGoodPrimaryJson));
                if (document == SaveDocument.Profile && _holdAndFailNextProfileWrite)
                {
                    _holdAndFailNextProfileWrite = false;
                    _heldProfileWriteEntered.TrySetResult(true);
                    await _releaseHeldProfileWrite.Task.ConfigureAwait(false);
                    throw new IOException("Injected first profile write failure.");
                }

                if (document == SaveDocument.Profile && FailNextProfileWrite)
                {
                    FailNextProfileWrite = false;
                    throw new IOException("Injected profile write failure.");
                }
                if (document == SaveDocument.Settings && FailNextSettingsWrite)
                {
                    FailNextSettingsWrite = false;
                    throw new IOException("Injected settings write failure.");
                }

                if (document == SaveDocument.Profile)
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson))
                        ProfileBackupJson = lastKnownGoodPrimaryJson;
                    ProfilePrimaryJson = json;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson))
                        SettingsBackupJson = lastKnownGoodPrimaryJson;
                    SettingsPrimaryJson = json;
                }
            }
        }

        private sealed class StoreWriteCall
        {
            public StoreWriteCall(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                Document = document;
                Json = json;
                LastKnownGoodPrimaryJson = lastKnownGoodPrimaryJson;
            }

            public SaveDocument Document { get; }
            public string Json { get; }
            public string LastKnownGoodPrimaryJson { get; }
        }
    }
}
