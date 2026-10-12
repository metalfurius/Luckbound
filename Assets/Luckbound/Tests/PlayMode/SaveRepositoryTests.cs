using System;
using System.IO;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveRepositoryTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "LuckboundSaveTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        }

        [Test]
        public void SavesProfileAndSettingsSeparatelyAndReloadsThemInANewRepository()
        {
            var repository = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> newProfile = Wait(repository.LoadProfileAsync());
            SaveLoadResult<SettingsSaveEnvelope> newSettings = Wait(repository.LoadSettingsAsync());
            Assert.That(newProfile.Status, Is.EqualTo(SaveLoadStatus.Missing));
            Assert.That(newSettings.Status, Is.EqualTo(SaveLoadStatus.Missing));

            ProfileSaveData profile = newProfile.Data.profile.Clone();
            profile.availableRenown = 73;
            profile.healthUpgradeLevel = 1;
            profile.acceptedContractIds = new[] { "contract.ct02" };
            profile.processedSettlementRunIds = new[] { RunId.New().ToString() };
            SaveWriteResult profileWrite = Wait(repository.SaveProfileAsync(profile));

            GameSettingsSaveData settings = new GameSettingsSaveData
            {
                languageId = "en",
                masterVolume = 0.9f,
                musicVolume = 0.4f,
                effectsVolume = 0.7f
            };
            SaveWriteResult settingsWrite = Wait(repository.SaveSettingsAsync(settings));
            Assert.That(profileWrite.Succeeded, Is.True, profileWrite.Message);
            Assert.That(settingsWrite.Succeeded, Is.True, settingsWrite.Message);

            var restarted = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> loadedProfile = Wait(restarted.LoadProfileAsync());
            SaveLoadResult<SettingsSaveEnvelope> loadedSettings = Wait(restarted.LoadSettingsAsync());

            Assert.That(loadedProfile.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loadedProfile.Data.sequence, Is.EqualTo(1));
            Assert.That(loadedProfile.Data.profile.availableRenown, Is.EqualTo(73));
            Assert.That(loadedProfile.Data.profile.healthUpgradeLevel, Is.EqualTo(1));
            Assert.That(loadedProfile.Data.profile.acceptedContractIds, Is.EqualTo(new[] { "contract.ct02" }));
            Assert.That(loadedProfile.Data.profile.processedSettlementRunIds, Has.Length.EqualTo(1));
            Assert.That(loadedSettings.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loadedSettings.Data.settings.languageId, Is.EqualTo("en"));
            Assert.That(loadedSettings.Data.settings.musicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(Path.GetFileName(NewFileStore().GetPrimaryPath(SaveDocument.Profile)), Is.EqualTo(FileSaveStore.ProfileFileName));
            Assert.That(Path.GetFileName(NewFileStore().GetPrimaryPath(SaveDocument.Settings)), Is.EqualTo(FileSaveStore.SettingsFileName));
        }

        [Test]
        public void CorruptPrimaryRecoversBackupAndKeepsThatBackupWhenSavingTheRecovery()
        {
            var repository = NewRepository();
            ProfileSaveData first = ProfileSaveData.CreateNewProfile();
            first.availableRenown = 21;
            Assert.That(Wait(repository.SaveProfileAsync(first)).Succeeded, Is.True);
            ProfileSaveData second = first.Clone();
            second.availableRenown = 54;
            Assert.That(Wait(repository.SaveProfileAsync(second)).Succeeded, Is.True);

            FileSaveStore paths = NewFileStore();
            string primaryPath = paths.GetPrimaryPath(SaveDocument.Profile);
            string backupPath = paths.GetBackupPath(SaveDocument.Profile);
            string validBackup = File.ReadAllText(backupPath);
            File.WriteAllText(primaryPath, "{ broken profile", new System.Text.UTF8Encoding(false));

            var restarted = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> recovered = Wait(restarted.LoadProfileAsync());

            Assert.That(recovered.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(recovered.Data.profile.availableRenown, Is.EqualTo(21));
            ProfileSaveData recoveryMutation = recovered.Data.profile.Clone();
            recoveryMutation.availableRenown = 30;
            Assert.That(Wait(restarted.SaveProfileAsync(recoveryMutation)).Succeeded, Is.True);
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo(validBackup), "The corrupt primary must not replace a known-good backup.");

            File.WriteAllText(primaryPath, "{ broken again", new System.Text.UTF8Encoding(false));
            var secondRestart = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> recoveredAgain = Wait(secondRestart.LoadProfileAsync());
            Assert.That(recoveredAgain.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(recoveredAgain.Data.profile.availableRenown, Is.EqualTo(21));
        }

        [Test]
        public void EditedPrimaryPayloadFailsChecksumAndRecoversTheUntouchedBackup()
        {
            var repository = NewRepository();
            ProfileSaveData first = ProfileSaveData.CreateNewProfile();
            first.availableRenown = 21;
            Assert.That(Wait(repository.SaveProfileAsync(first)).Succeeded, Is.True);
            ProfileSaveData second = first.Clone();
            second.availableRenown = 54;
            Assert.That(Wait(repository.SaveProfileAsync(second)).Succeeded, Is.True);

            FileSaveStore paths = NewFileStore();
            string primaryPath = paths.GetPrimaryPath(SaveDocument.Profile);
            string tampered = File.ReadAllText(primaryPath).Replace("\"availableRenown\": 54", "\"availableRenown\": 99");
            Assert.That(tampered, Does.Contain("\"availableRenown\": 99"));
            File.WriteAllText(primaryPath, tampered, new System.Text.UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(loaded.Data.profile.availableRenown, Is.EqualTo(21));
            Assert.That(File.ReadAllText(paths.GetBackupPath(SaveDocument.Profile)), Does.Contain("\"availableRenown\": 21"));
        }

        [Test]
        public void UnrecoverableProfilePreservesBothFilesAndRejectsWrites()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore paths = NewFileStore();
            const string corruptPrimary = "{ damaged primary }";
            const string corruptBackup = "{ damaged backup }";
            File.WriteAllText(paths.GetPrimaryPath(SaveDocument.Profile), corruptPrimary, new System.Text.UTF8Encoding(false));
            File.WriteAllText(paths.GetBackupPath(SaveDocument.Profile), corruptBackup, new System.Text.UTF8Encoding(false));

            var repository = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());
            SaveWriteResult rejected = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile()));

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(loaded.Data, Is.Null);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(File.ReadAllText(paths.GetPrimaryPath(SaveDocument.Profile)), Is.EqualTo(corruptPrimary));
            Assert.That(File.ReadAllText(paths.GetBackupPath(SaveDocument.Profile)), Is.EqualTo(corruptBackup));
        }

        [Test]
        public void D08_ProfileRestartAndCorruptionScenarioWritesReport()
        {
            using (var evidence = new ScenarioEvidenceRecorder("D08", "D08_ProfileRestartAndBackupRecovery", 14142135))
            {
                var firstProcess = NewRepository();
                ProfileSaveData firstProfile = ProfileSaveData.CreateNewProfile();
                firstProfile.availableRenown = 41;
                firstProfile.processedSettlementRunIds = new[] { "000000000000000000000000000000d8" };
                SaveWriteResult firstWrite = Wait(firstProcess.SaveProfileAsync(firstProfile));
                ProfileSaveData secondProfile = firstProfile.Clone();
                secondProfile.availableRenown = 87;
                SaveWriteResult secondWrite = Wait(firstProcess.SaveProfileAsync(secondProfile));
                var afterRestart = NewRepository();
                SaveLoadResult<ProfileSaveEnvelope> latest = Wait(afterRestart.LoadProfileAsync());
                bool restartRestoredLatest = firstWrite.Succeeded && secondWrite.Succeeded &&
                    latest.Status == SaveLoadStatus.LoadedPrimary && latest.Data.profile.availableRenown == 87 &&
                    latest.Data.sequence == 2;
                evidence.RecordInput(0, "SaveProfile", "write Renown 41 then 87; restart repository from the same directory");
                evidence.RecordAssertion("A new repository restores the latest checksummed profile",
                    restartRestoredLatest, "primary has sequence 2 and 87 available Renown",
                    "status=" + latest.Status + "; sequence=" + latest.Data?.sequence + "; Renown=" + latest.Data?.profile?.availableRenown);

                FileSaveStore paths = NewFileStore();
                string primaryPath = paths.GetPrimaryPath(SaveDocument.Profile);
                string backupPath = paths.GetBackupPath(SaveDocument.Profile);
                string validBackup = File.ReadAllText(backupPath);
                File.WriteAllText(primaryPath, "corrupted primary", new System.Text.UTF8Encoding(false));
                var recoveryProcess = NewRepository();
                SaveLoadResult<ProfileSaveEnvelope> recovered = Wait(recoveryProcess.LoadProfileAsync());
                bool recoveredPriorValue = recovered.Status == SaveLoadStatus.RecoveredBackup &&
                    recovered.Data.profile.availableRenown == 41;
                evidence.RecordInput(1, "CorruptPrimary", "replace primary JSON bytes with invalid text, leave backup intact");
                evidence.RecordAssertion("A corrupted primary recovers the previous valid backup",
                    recoveredPriorValue, "backup value 41", "status=" + recovered.Status + "; Renown=" + recovered.Data?.profile?.availableRenown);

                if (recovered.Data != null)
                {
                    ProfileSaveData recoverySave = recovered.Data.profile.Clone();
                    recoverySave.availableRenown = 62;
                    Wait(recoveryProcess.SaveProfileAsync(recoverySave));
                }
                bool recoveryPreservedBackup = File.ReadAllText(backupPath) == validBackup;
                evidence.RecordAssertion("Saving recovered data never rotates corrupted primary over the valid backup",
                    recoveryPreservedBackup, "backup bytes remain unchanged", recoveryPreservedBackup ? "unchanged" : "changed");
                evidence.RecordEvent("restart=restored; corruptPrimary=recoveredFromBackup; backupPreserved=true");
                int exitCode = evidence.Complete();

                Assert.That(restartRestoredLatest, Is.True, "Restart did not restore the newest save. Evidence: " + evidence.ReportPath);
                Assert.That(recoveredPriorValue, Is.True, "Corruption recovery failed. Evidence: " + evidence.ReportPath);
                Assert.That(recoveryPreservedBackup, Is.True, "Recovery overwrote its backup. Evidence: " + evidence.ReportPath);
                Assert.That(exitCode, Is.Zero, "D08 scenario report contains a failure. Evidence: " + evidence.ReportPath);
            }
        }

        [Test]
        public void SettingsCorruptionRestoresDefaultsButPreservesBothInvalidFiles()
        {
            var repository = NewRepository();
            GameSettingsSaveData first = new GameSettingsSaveData { languageId = "en", masterVolume = 0.25f };
            Assert.That(Wait(repository.SaveSettingsAsync(first)).Succeeded, Is.True);
            GameSettingsSaveData second = first.Clone();
            second.masterVolume = 0.75f;
            Assert.That(Wait(repository.SaveSettingsAsync(second)).Succeeded, Is.True);

            FileSaveStore paths = NewFileStore();
            string primaryPath = paths.GetPrimaryPath(SaveDocument.Settings);
            string backupPath = paths.GetBackupPath(SaveDocument.Settings);
            File.WriteAllText(primaryPath, "corrupt primary", new System.Text.UTF8Encoding(false));
            File.WriteAllText(backupPath, "corrupt backup", new System.Text.UTF8Encoding(false));

            SaveLoadResult<SettingsSaveEnvelope> loaded = Wait(NewRepository().LoadSettingsAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(loaded.Data.settings.languageId, Is.EqualTo("es"));
            Assert.That(loaded.Data.settings.masterVolume, Is.EqualTo(1f));
            Assert.That(File.ReadAllText(primaryPath), Is.EqualTo("corrupt primary"));
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo("corrupt backup"));
        }

        [Test]
        public void NewerSchemaIsPreservedAndBlocksWritesInsteadOfFallingBackToOlderData()
        {
            var repository = NewRepository();
            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile())).Succeeded, Is.True);
            string profilePath = NewFileStore().GetPrimaryPath(SaveDocument.Profile);
            string newerSchema = File.ReadAllText(profilePath).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99");
            File.WriteAllText(profilePath, newerSchema, new System.Text.UTF8Encoding(false));

            var restarted = NewRepository();
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(restarted.LoadProfileAsync());
            SaveWriteResult rejected = Wait(restarted.SaveProfileAsync(ProfileSaveData.CreateNewProfile()));

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Incompatible));
            Assert.That(loaded.Data, Is.Null);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(File.ReadAllText(profilePath), Is.EqualTo(newerSchema));
        }

        [Test]
        public void FailedWriteKeepsOnePayloadForExplicitRetry()
        {
            var store = new FailOnceSaveStore(NewFileStore());
            var repository = new SaveRepository(store);
            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            profile.availableRenown = 87;

            SaveWriteResult firstAttempt = Wait(repository.SaveProfileAsync(profile));
            string pendingExport = repository.ExportPendingJson(SaveDocument.Profile);
            SaveWriteResult retry = Wait(repository.RetryPendingAsync(SaveDocument.Profile));
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(firstAttempt.Succeeded, Is.False);
            Assert.That(pendingExport, Does.Contain("\"availableRenown\": 87"));
            Assert.That(retry.Succeeded, Is.True, retry.Message);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.Data.profile.availableRenown, Is.EqualTo(87));
        }

        [Test]
        public void ActiveExpeditionMarkerAndPayloadSurviveARepositoryRestart()
        {
            var repository = NewRepository();
            var expedition = new ExpeditionSaveData
            {
                runId = RunId.New().ToString(),
                seed = 42,
                generationVersion = 1,
                contentVersion = 1,
                classId = "class.bellator",
                raceId = "race.human",
                selectedSkillIds = new[] { "skill.bellator.cleave" },
                routeSnapshotJson = "{}",
                currentNodeId = "node.01",
                accumulatedRenown = 13,
                fortunaCharges = 2
            };

            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), expedition)).Succeeded, Is.True);
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.Data.hasActiveExpedition, Is.True);
            Assert.That(loaded.Data.activeExpedition.runId, Is.EqualTo(expedition.runId));
            Assert.That(loaded.Data.activeExpedition.seed, Is.EqualTo(42));
            Assert.That(loaded.Data.activeExpedition.currentNodeId, Is.EqualTo("node.01"));
            Assert.That(loaded.Data.activeExpedition.accumulatedRenown, Is.EqualTo(13));
        }

        [Test]
        public void InvalidSettingsDoNotWriteOrChangeTheStoredDocument()
        {
            var repository = NewRepository();
            Assert.That(Wait(repository.SaveSettingsAsync(new GameSettingsSaveData())).Succeeded, Is.True);
            string path = NewFileStore().GetPrimaryPath(SaveDocument.Settings);
            string before = File.ReadAllText(path);

            SaveWriteResult result = Wait(repository.SaveSettingsAsync(new GameSettingsSaveData { languageId = "fr" }));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        private SaveRepository NewRepository() => new SaveRepository(NewFileStore());

        private FileSaveStore NewFileStore() => new FileSaveStore(_directory, null);

        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private sealed class FailOnceSaveStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _fail = true;

            public FailOnceSaveStore(ISaveStore inner) { _inner = inner; }
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (_fail)
                {
                    _fail = false;
                    throw new IOException("simulated storage failure");
                }
                return _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
