using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

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
                effectsVolume = 0.7f,
                ambienceVolume = 0.3f
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
            Assert.That(loadedSettings.Data.settings.ambienceVolume, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(Path.GetFileName(NewFileStore().GetPrimaryPath(SaveDocument.Profile)), Is.EqualTo(FileSaveStore.ProfileFileName));
            Assert.That(Path.GetFileName(NewFileStore().GetPrimaryPath(SaveDocument.Settings)), Is.EqualTo(FileSaveStore.SettingsFileName));
        }

        [Test]
        public void ProfileValidationRejectsClaimedContractThatWasNotAccepted()
        {
            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            profile.claimedContractIds = new[] { "contract.ct01" };

            IReadOnlyList<string> errors = SaveDataValidation.Validate(profile);

            Assert.That(errors, Does.Contain("Claimed contracts must also be accepted."));
        }

        [Test]
        public void ProfileValidationRejectsMoreThanThreeActiveContracts()
        {
            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            profile.acceptedContractIds = new[]
            {
                "contract.ct01", "contract.ct02", "contract.ct03", "contract.ct04"
            };

            IReadOnlyList<string> errors = SaveDataValidation.Validate(profile);

            Assert.That(errors, Does.Contain("A profile cannot have more than three active contracts."));
        }

        [Test]
        public void SemanticallyInvalidChecksummedPrimaryRecoversValidBackup()
        {
            var repository = NewRepository();
            ProfileSaveData first = ProfileSaveData.CreateNewProfile();
            first.availableRenown = 21;
            Assert.That(Wait(repository.SaveProfileAsync(first)).Succeeded, Is.True);
            ProfileSaveData second = first.Clone();
            second.availableRenown = 54;
            Assert.That(Wait(repository.SaveProfileAsync(second)).Succeeded, Is.True);

            string primaryPath = NewFileStore().GetPrimaryPath(SaveDocument.Profile);
            ProfileSaveEnvelope invalidPrimary = JsonUtility.FromJson<ProfileSaveEnvelope>(File.ReadAllText(primaryPath));
            invalidPrimary.profile.acceptedContractIds = new[]
            {
                "contract.ct01", "contract.ct02", "contract.ct03", "contract.ct04"
            };
            Type codecType = typeof(SaveRepository).Assembly.GetType("Luckbound.App.SaveJsonCodec", true);
            MethodInfo encode = codecType.GetMethod("Encode", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(ProfileSaveEnvelope) }, null);
            Assert.That(encode, Is.Not.Null);
            string invalidJson = (string)encode.Invoke(null, new object[] { invalidPrimary });
            Assert.That(invalidPrimary.checksum, Does.Match("^[0-9a-f]{64}$"));
            File.WriteAllText(primaryPath, invalidJson, new System.Text.UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> recovered = Wait(NewRepository().LoadProfileAsync());

            Assert.That(recovered.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(recovered.Data.profile.availableRenown, Is.EqualTo(21));
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
        public void ContentVersionOneProfileMigratesRenownAndUpgradesWithoutInventingPurchaseReceipts()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            ProfileSaveData oldProfile = ProfileSaveData.CreateNewProfile();
            oldProfile.availableRenown = 247;
            oldProfile.healthUpgradeLevel = 1;
            oldProfile.energyUpgradeLevel = 2;
            oldProfile.additionalPotionUpgradeLevel = 1;
            oldProfile.processedSettlementRunIds = new[] { RunId.New().ToString() };
            string legacyJson = EncodeLegacyProfileV1(oldProfile);
            File.WriteAllText(store.GetPrimaryPath(SaveDocument.Profile), legacyJson, new UTF8Encoding(false));

            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.profile.availableRenown, Is.EqualTo(247));
            Assert.That(loaded.Data.profile.healthUpgradeLevel, Is.EqualTo(1));
            Assert.That(loaded.Data.profile.energyUpgradeLevel, Is.EqualTo(2));
            Assert.That(loaded.Data.profile.additionalPotionUpgradeLevel, Is.EqualTo(1));
            Assert.That(loaded.Data.profile.processedSettlementRunIds, Is.EqualTo(oldProfile.processedSettlementRunIds));
            Assert.That(loaded.Data.profile.progressionPurchaseTransactions, Is.Empty);

            Assert.That(Wait(repository.SaveProfileAsync(loaded.Data.profile)).Succeeded, Is.True);
            var restarted = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> afterRestart = Wait(restarted.LoadProfileAsync());
            Assert.That(afterRestart.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), afterRestart.Message);
            Assert.That(afterRestart.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(afterRestart.Data.profile.availableRenown, Is.EqualTo(247));
        }

        [Test]
        public void ContentVersionOneSettingsMigrateWithoutChangingPlayerChoices()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            var legacy = new LegacySettingsSaveEnvelope
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 1,
                generation = 1,
                sequence = 4,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                settings = new LegacyGameSettingsSaveData
                {
                    languageId = "en",
                    masterVolume = 0.31f,
                    musicVolume = 0.46f,
                    effectsVolume = 0.72f
                }
            };
            using (SHA256 sha = SHA256.Create())
                legacy.checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy, false))))
                    .Replace("-", string.Empty).ToLowerInvariant();
            File.WriteAllText(store.GetPrimaryPath(SaveDocument.Settings), JsonUtility.ToJson(legacy, true),
                new UTF8Encoding(false));

            var repository = new SaveRepository(store);
            SaveLoadResult<SettingsSaveEnvelope> loaded = Wait(repository.LoadSettingsAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.sequence, Is.EqualTo(4));
            Assert.That(loaded.Data.settings.languageId, Is.EqualTo("en"));
            Assert.That(loaded.Data.settings.masterVolume, Is.EqualTo(0.31f).Within(0.0001f));
            Assert.That(loaded.Data.settings.musicVolume, Is.EqualTo(0.46f).Within(0.0001f));
            Assert.That(loaded.Data.settings.effectsVolume, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(loaded.Data.settings.ambienceVolume, Is.EqualTo(0.45f).Within(0.0001f));

            Assert.That(Wait(repository.SaveSettingsAsync(loaded.Data.settings)).Succeeded, Is.True);
            var restarted = new SaveRepository(store);
            SaveLoadResult<SettingsSaveEnvelope> afterRestart = Wait(restarted.LoadSettingsAsync());
            Assert.That(afterRestart.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(afterRestart.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(afterRestart.Data.settings.musicVolume, Is.EqualTo(0.46f).Within(0.0001f));
            Assert.That(afterRestart.Data.settings.ambienceVolume, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void ContentVersionThreeSettingsMigrateWithThePlannedAmbienceDefault()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            var legacy = new LegacySettingsSaveEnvelope
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 3,
                generation = 2,
                sequence = 11,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                settings = new LegacyGameSettingsSaveData
                {
                    languageId = "es",
                    masterVolume = 0.8f,
                    musicVolume = 0.5f,
                    effectsVolume = 0.6f
                }
            };
            using (SHA256 sha = SHA256.Create())
                legacy.checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(legacy, false))))
                    .Replace("-", string.Empty).ToLowerInvariant();
            File.WriteAllText(store.GetPrimaryPath(SaveDocument.Settings), JsonUtility.ToJson(legacy, true),
                new UTF8Encoding(false));

            SaveLoadResult<SettingsSaveEnvelope> loaded = Wait(new SaveRepository(store).LoadSettingsAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.sequence, Is.EqualTo(11));
            Assert.That(loaded.Data.settings.masterVolume, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(loaded.Data.settings.musicVolume, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(loaded.Data.settings.effectsVolume, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(loaded.Data.settings.ambienceVolume, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void ContentVersionThreeProfileMigratesWithoutChangingItsPayload()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            // Content v3 used the pre-brew expedition DTO. Serializing the current DTO with a
            // v3 header includes new inline fields even when JsonUtility receives a null run.
            var legacy = new LegacyProfileSaveEnvelopeV4
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 3,
                generation = 2,
                sequence = 11,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                profile = ToLegacyProfileSaveDataV5(ProfileSaveData.CreateNewProfile()),
                hasActiveExpedition = false,
                activeExpedition = null
            };
            legacy.profile.availableRenown = 91;
            legacy.checksum = ComputeTestChecksum(legacy);
            string legacyJson = JsonUtility.ToJson(legacy, true);
            string path = store.GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllText(path, legacyJson, new UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(new SaveRepository(store).LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.sequence, Is.EqualTo(11));
            Assert.That(loaded.Data.profile.availableRenown, Is.EqualTo(91));
            Assert.That(loaded.Data.profile.processedContractProgressEvents, Is.Empty);
            Assert.That(loaded.Data.hasActiveExpedition, Is.False);
            Assert.That(loaded.Data.activeExpedition, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo(legacyJson));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ContentVersionFourEnvelopeMigratesWithItsExactHistoricalShapeAndKeepsUnknownRunState(bool includesFortuneBrewReceipts)
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.magus", "race.human",
                new[] { "skill.magus.fireball", "skill.magus.frost_nova" },
                healthUpgradeLevel: 2, energyUpgradeLevel: 2, additionalPotionUpgradeLevel: 1);
            ExpeditionSaveData expedition = ExpeditionSaveMapper.Create(run);
            // This exact legacy value is ambiguous: old maximumEnergy could be 50 despite the equipped tunic's +5.
            // Migration must preserve the durable value and must not infer or subtract an armor bonus.
            expedition.maximumHealth = 110;
            expedition.currentHealth = 64;
            expedition.maximumEnergy = 50;
            expedition.currentEnergy = 37;
            expedition.roomEntryCheckpointJson = ExpeditionSaveMapper.SerializeCheckpoint(
                ExpeditionSaveMapper.CreateCheckpoint(expedition));

            string brewTransactionId = string.Empty;
            if (includesFortuneBrewReceipts)
            {
                FortuneOfferBook book = FortuneOfferBook.Restore(expedition.fortuneOfferBookSnapshot);
                ItemInstanceId brewItem = ItemInstanceId.New();
                TransactionId brewTransaction = TransactionId.New();
                Assert.That(book.GrantFortuneBrewCharge(brewItem, brewTransaction).Succeeded, Is.True);
                brewTransactionId = brewTransaction.ToString();
                expedition.fortuneOfferBookSnapshot = book.CreateSnapshot();
                expedition.roomEntryCheckpointJson = ExpeditionSaveMapper.SerializeCheckpoint(
                    ExpeditionSaveMapper.CreateCheckpoint(expedition));
            }

            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            profile.healthUpgradeLevel = 2;
            profile.energyUpgradeLevel = 2;
            profile.additionalPotionUpgradeLevel = 1;
            string legacyJson = EncodeLegacyProfileV4(profile, expedition, includesFortuneBrewReceipts);
            string path = store.GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllText(path, legacyJson, new UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.profile.healthUpgradeLevel, Is.EqualTo(2));
            Assert.That(loaded.Data.profile.energyUpgradeLevel, Is.EqualTo(2));
            Assert.That(loaded.Data.profile.additionalPotionUpgradeLevel, Is.EqualTo(1));
            Assert.That(loaded.Data.profile.processedContractProgressEvents, Is.Empty);
            Assert.That(loaded.Data.activeExpedition.frozenPermanentUpgrades, Is.Null);
            Assert.That(loaded.Data.activeExpedition.maximumHealth, Is.EqualTo(110));
            Assert.That(loaded.Data.activeExpedition.currentHealth, Is.EqualTo(64));
            Assert.That(loaded.Data.activeExpedition.maximumEnergy, Is.EqualTo(50));
            Assert.That(loaded.Data.activeExpedition.currentEnergy, Is.EqualTo(37));
            Assert.That(File.ReadAllText(path), Is.EqualTo(legacyJson), "Reading a historical save must not rewrite its original bytes.");

            ExpeditionRunConfiguration restored = ExpeditionSaveMapper.Restore(loaded.Data.activeExpedition,
                out ExpeditionSaveData resumed);
            Assert.That(restored.HasKnownStartingLoadout, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = restored.StartingHealth);
            Assert.Throws<InvalidOperationException>(() => _ = restored.StartingEnergy);
            Assert.Throws<InvalidOperationException>(() => _ = restored.StartingHealthPotions);
            Assert.That(resumed.maximumEnergy, Is.EqualTo(50));
            Assert.That(resumed.currentEnergy, Is.EqualTo(37));
            ExpeditionRunConfiguration inventoryUpdated = restored.WithInventory(restored.Inventory);
            Assert.That(inventoryUpdated.HasKnownStartingLoadout, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = inventoryUpdated.StartingEnergy);
            if (includesFortuneBrewReceipts)
            {
                Assert.That(resumed.fortuneOfferBookSnapshot.fortuneBrewReceipts, Has.Length.EqualTo(1));
                Assert.That(resumed.fortuneOfferBookSnapshot.fortuneBrewReceipts[0].transactionId,
                    Is.EqualTo(brewTransactionId));
            }

            SaveWriteResult resave = Wait(NewRepository().SaveProfileAsync(loaded.Data.profile, loaded.Data.activeExpedition));
            Assert.That(resave.Succeeded, Is.True, resave.Message);
            SaveLoadResult<ProfileSaveEnvelope> afterRestart = Wait(NewRepository().LoadProfileAsync());
            Assert.That(afterRestart.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), afterRestart.Message);
            Assert.That(afterRestart.Data.activeExpedition.frozenPermanentUpgrades, Is.Null,
                "Resaving a legacy run must keep its loadout unknown rather than serializing an invented zero snapshot.");
            ExpeditionRunConfiguration restoredAgain = ExpeditionSaveMapper.Restore(afterRestart.Data.activeExpedition,
                out ExpeditionSaveData resumedAgain);
            Assert.That(restoredAgain.HasKnownStartingLoadout, Is.False);
            Assert.Throws<InvalidOperationException>(() => _ = restoredAgain.StartingHealth);
            Assert.That(resumedAgain.maximumEnergy, Is.EqualTo(50));
            Assert.That(resumedAgain.currentEnergy, Is.EqualTo(37));
        }

        [Test]
        public void ContentVersionFourSettingsKeepTheirSavedAmbienceValue()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            var legacy = new LegacySettingsSaveEnvelopeV4
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 4,
                generation = 3,
                sequence = 9,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                settings = new LegacyGameSettingsSaveDataV4
                {
                    languageId = "es",
                    masterVolume = 0.8f,
                    musicVolume = 0.5f,
                    effectsVolume = 0.6f,
                    ambienceVolume = 0.27f
                }
            };
            legacy.checksum = ComputeTestChecksum(legacy);
            string legacyJson = JsonUtility.ToJson(legacy, true);
            File.WriteAllText(store.GetPrimaryPath(SaveDocument.Settings), legacyJson, new UTF8Encoding(false));

            SaveLoadResult<SettingsSaveEnvelope> loaded = Wait(NewRepository().LoadSettingsAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.settings.ambienceVolume, Is.EqualTo(0.27f).Within(0.0001f));
            Assert.That(File.ReadAllText(store.GetPrimaryPath(SaveDocument.Settings)), Is.EqualTo(legacyJson));
        }

        [Test]
        public void ContentVersionFiveProfileMigratesWithoutInventingContractEventReceipts()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            ProfileSaveData oldProfile = ProfileSaveData.CreateNewProfile();
            oldProfile.availableRenown = 74;
            oldProfile.acceptedContractIds = new[] { "CT01" };
            oldProfile.contractCounters = new[] { new ContractCounterSaveData { contractId = "CT01", progress = 2 } };
            string legacyJson = EncodeLegacyProfileV5(oldProfile);
            string path = store.GetPrimaryPath(SaveDocument.Profile);
            File.WriteAllText(path, legacyJson, new UTF8Encoding(false));

            var repository = new SaveRepository(store);
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(repository.LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.profile.availableRenown, Is.EqualTo(74));
            Assert.That(loaded.Data.profile.acceptedContractIds, Does.Contain("CT01"));
            Assert.That(loaded.Data.profile.contractCounters.Single(counter => counter.contractId == "CT01").progress,
                Is.EqualTo(2));
            Assert.That(loaded.Data.profile.processedContractProgressEvents, Is.Empty);
            Assert.That(File.ReadAllText(path), Is.EqualTo(legacyJson),
                "Loading a historical v5 profile must not rewrite its original bytes.");

            Assert.That(Wait(repository.SaveProfileAsync(loaded.Data.profile)).Succeeded, Is.True);
            SaveLoadResult<ProfileSaveEnvelope> afterRestart = Wait(new SaveRepository(store).LoadProfileAsync());
            Assert.That(afterRestart.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), afterRestart.Message);
            Assert.That(afterRestart.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(afterRestart.Data.profile.availableRenown, Is.EqualTo(74));
            Assert.That(afterRestart.Data.profile.processedContractProgressEvents, Is.Empty);
        }

        [Test]
        public void ContentVersionFiveSettingsMigrateAndPreserveTheirValues()
        {
            Directory.CreateDirectory(_directory);
            FileSaveStore store = NewFileStore();
            var legacy = new LegacySettingsSaveEnvelopeV5
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 5,
                generation = 2,
                sequence = 9,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                settings = new GameSettingsSaveData
                {
                    languageId = "en",
                    masterVolume = 0.8f,
                    musicVolume = 0.4f,
                    effectsVolume = 0.7f,
                    ambienceVolume = 0.3f
                }
            };
            legacy.checksum = ComputeTestChecksum(legacy);
            string legacyJson = JsonUtility.ToJson(legacy, true);
            string path = store.GetPrimaryPath(SaveDocument.Settings);
            File.WriteAllText(path, legacyJson, new UTF8Encoding(false));

            SaveLoadResult<SettingsSaveEnvelope> loaded = Wait(new SaveRepository(store).LoadSettingsAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(loaded.Data.settings.languageId, Is.EqualTo("en"));
            Assert.That(loaded.Data.settings.masterVolume, Is.EqualTo(0.8f));
            Assert.That(loaded.Data.settings.musicVolume, Is.EqualTo(0.4f));
            Assert.That(loaded.Data.settings.effectsVolume, Is.EqualTo(0.7f));
            Assert.That(loaded.Data.settings.ambienceVolume, Is.EqualTo(0.3f));
            Assert.That(File.ReadAllText(path), Is.EqualTo(legacyJson),
                "Loading a historical v5 settings file must not rewrite its original bytes.");
        }

        [Test]
        public void CorruptFrozenUpgradeSnapshotIsRejectedWithoutChangingItsBytes()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.magus", "race.human",
                new[] { "skill.magus.fireball", "skill.magus.frost_nova" },
                healthUpgradeLevel: 2, energyUpgradeLevel: 2, additionalPotionUpgradeLevel: 1);
            ExpeditionSaveData expedition = ExpeditionSaveMapper.Create(run);
            var repository = NewRepository();
            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), expedition)).Succeeded, Is.True);
            string path = NewFileStore().GetPrimaryPath(SaveDocument.Profile);
            string original = File.ReadAllText(path);
            string corrupted = original.Replace("\"healthUpgradeLevel\": 2", "\"healthUpgradeLevel\": 3");
            Assert.That(corrupted, Is.Not.EqualTo(original), "The test must actually alter the frozen snapshot field.");
            File.WriteAllText(path, corrupted, new UTF8Encoding(false));

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(File.ReadAllText(path), Is.EqualTo(corrupted));
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
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId, new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            ExpeditionSaveData expedition = ExpeditionSaveMapper.Create(run);
            expedition.accumulatedRenown = 13;

            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), expedition)).Succeeded, Is.True);
            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(NewRepository().LoadProfileAsync());

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.Data.hasActiveExpedition, Is.True);
            Assert.That(loaded.Data.activeExpedition.runId, Is.EqualTo(expedition.runId));
            Assert.That(loaded.Data.activeExpedition.seed, Is.EqualTo(run.Seed));
            Assert.That(loaded.Data.activeExpedition.currentNodeId, Is.EqualTo(run.Route.MandatoryNodeIds[0]));
            Assert.That(loaded.Data.activeExpedition.accumulatedRenown, Is.EqualTo(13));
        }

        [Test]
        public void D09_RestartRollsBackInterruptedRoomAndKeepsCommittedRoomResult()
        {
            const int scenarioSeed = 31415926;
            using (var evidence = new ScenarioEvidenceRecorder("D09", "D09_RoomCheckpointRestartAndCompletedResult", scenarioSeed))
            {
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId, new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                ExpeditionSaveData entry = ExpeditionSaveMapper.Create(run);
                string directory = Path.Combine(_directory, "D09Restart");
                Directory.CreateDirectory(directory);
                var firstProcess = new SaveRepository(new FileSaveStore(directory, null));
                SaveWriteResult start = Wait(firstProcess.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), entry));
                evidence.RecordAssertion("A new run persists its route, inventory, and room-entry checkpoint before play", start.Succeeded &&
                    !string.IsNullOrWhiteSpace(entry.roomEntryCheckpointJson) && entry.visitedNodeIds.Length == 1,
                    "saved active expedition with entry checkpoint", start.Message);
                Assert.That(start.Succeeded, Is.True, start.Message);

                ExpeditionInventory changedInventory = ExpeditionInventory.Restore(run.Inventory.Catalog, run.Inventory.CreateSnapshot());
                InventoryOperationOutcome loot = changedInventory.TryAddGold(17, TransactionId.New());
                Assert.That(loot.Succeeded, Is.True);
                ExpeditionSaveData duringCombat = entry.Clone();
                duringCombat.inventorySnapshotJson = JsonUtility.ToJson(changedInventory.CreateSnapshot());
                duringCombat.currentHealth = 23;
                duringCombat.healthPotionCharges = 1;
                duringCombat.accumulatedRenown = 40;
                duringCombat.fortunaCharges = 0;
                SaveWriteResult combatWrite = Wait(firstProcess.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), duringCombat));
                evidence.RecordInput(1, "SaveWrite", "persist changed state during current room, retain entry checkpoint");
                evidence.RecordAssertion("Mid-room gains are durably recorded separately from the immutable rollback point", combatWrite.Succeeded &&
                    duringCombat.roomEntryCheckpointJson == entry.roomEntryCheckpointJson,
                    "room-entry checkpoint unchanged", combatWrite.Message);
                Assert.That(combatWrite.Succeeded, Is.True, combatWrite.Message);

                var restartedProcess = new SaveRepository(new FileSaveStore(directory, null));
                SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(restartedProcess.LoadProfileAsync());
                ExpeditionRunConfiguration resumed = ExpeditionSaveMapper.Restore(loaded.Data.activeExpedition, out ExpeditionSaveData rolledBack);
                evidence.RecordAssertion("Restart returns to the room-entry snapshot rather than keeping interrupted gains",
                    loaded.Status == SaveLoadStatus.LoadedPrimary && resumed.RunId == run.RunId &&
                    rolledBack.currentHealth == entry.currentHealth && rolledBack.accumulatedRenown == entry.accumulatedRenown &&
                    resumed.Inventory.Gold == run.Inventory.Gold,
                    "same RunId, entry health/Renown/gold", rolledBack.currentHealth + "/" + rolledBack.accumulatedRenown + "/" + resumed.Inventory.Gold);

                ExpeditionSaveData completed = rolledBack.Clone();
                string resultJson = "{\"outcome\":\"cleared\",\"renown\":12,\"lootId\":\"reward.d09.test\"}";
                completed.completedRoomIds = new[] { completed.currentNodeId };
                completed.completedRoomResults = new[]
                {
                    new ExpeditionRoomCompletionSaveData
                    {
                        roomNodeId = completed.currentNodeId,
                        resultId = "result." + completed.currentNodeId,
                        resultJson = resultJson
                    }
                };
                completed.roomEntryCheckpointJson = string.Empty;
                SaveWriteResult completeWrite = Wait(restartedProcess.SaveProfileAsync(loaded.Data.profile, completed));
                Assert.That(completeWrite.Succeeded, Is.True, completeWrite.Message);
                SaveLoadResult<ProfileSaveEnvelope> afterCompletionRestart = Wait(new SaveRepository(new FileSaveStore(directory, null)).LoadProfileAsync());
                ExpeditionSaveMapper.Restore(afterCompletionRestart.Data.activeExpedition, out ExpeditionSaveData completionState);
                bool completedResultSurvives = completionState.completedRoomIds.Contains(completionState.currentNodeId) &&
                    completionState.completedRoomResults.Length == 1 && completionState.completedRoomResults[0].resultJson == resultJson &&
                    string.IsNullOrWhiteSpace(completionState.roomEntryCheckpointJson);
                evidence.RecordAssertion("A completed room reloads its committed result without generating a fresh entry checkpoint",
                    completedResultSurvives, "one stable result, completed room, no entry rollback", completionState.completedRoomResults.Length.ToString());
                Assert.That(completedResultSurvives, Is.True);
                int failedAssertions = evidence.Complete();
                Assert.That(failedAssertions, Is.Zero, "D09 scenario evidence: " + evidence.ReportPath);
            }
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

        private static string EncodeLegacyProfileV4(ProfileSaveData profile, ExpeditionSaveData expedition,
            bool includesFortuneBrewReceipts)
        {
            if (includesFortuneBrewReceipts)
            {
                var envelope = new LegacyProfileSaveEnvelopeV4Brew
                {
                    schemaVersion = SaveFormat.SchemaVersion,
                    contentVersion = 4,
                    generation = 3,
                    sequence = 18,
                    savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    checksum = string.Empty,
                    profile = ToLegacyProfileSaveDataV5(profile),
                    hasActiveExpedition = true,
                    activeExpedition = new LegacyExpeditionSaveDataV4Brew(expedition)
                };
                envelope.checksum = ComputeTestChecksum(envelope);
                return JsonUtility.ToJson(envelope, true);
            }

            var legacy = new LegacyProfileSaveEnvelopeV4
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 4,
                generation = 3,
                sequence = 17,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                profile = ToLegacyProfileSaveDataV5(profile),
                hasActiveExpedition = true,
                activeExpedition = new LegacyExpeditionSaveDataV4(expedition)
            };
            legacy.checksum = ComputeTestChecksum(legacy);
            return JsonUtility.ToJson(legacy, true);
        }

        private static string EncodeLegacyProfileV5(ProfileSaveData profile)
        {
            var envelope = new LegacyProfileSaveEnvelopeV5
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 5,
                generation = 2,
                sequence = 12,
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
                hasActiveExpedition = false,
                activeExpedition = null
            };
            envelope.checksum = ComputeTestChecksum(envelope);
            return JsonUtility.ToJson(envelope, true);
        }

        private static LegacyProfileSaveDataV5 ToLegacyProfileSaveDataV5(ProfileSaveData profile)
        {
            return new LegacyProfileSaveDataV5
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
            };
        }

        private static string ComputeTestChecksum(object envelope)
        {
            FieldInfo checksumField = envelope.GetType().GetField("checksum", BindingFlags.Public | BindingFlags.Instance);
            Assert.That(checksumField, Is.Not.Null);
            checksumField.SetValue(envelope, string.Empty);
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope, false))))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        private SaveRepository NewRepository() => new SaveRepository(NewFileStore());

        private FileSaveStore NewFileStore() => new FileSaveStore(_directory, null);

        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

        private static string EncodeLegacyProfileV1(ProfileSaveData source)
        {
            var envelope = new LegacyProfileEnvelopeV1
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = 1,
                generation = 1,
                sequence = 1,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                profile = new LegacyProfileDataV1
                {
                    availableRenown = source.availableRenown,
                    healthUpgradeLevel = source.healthUpgradeLevel,
                    energyUpgradeLevel = source.energyUpgradeLevel,
                    additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                    unlockedRaceIds = source.unlockedRaceIds,
                    unlockedStartingKitIds = source.unlockedStartingKitIds,
                    acceptedContractIds = source.acceptedContractIds,
                    claimedContractIds = source.claimedContractIds,
                    readStoryFragmentIds = source.readStoryFragmentIds,
                    contractCounters = source.contractCounters,
                    processedSettlementRunIds = source.processedSettlementRunIds
                },
                hasActiveExpedition = false,
                activeExpedition = null
            };
            using (SHA256 sha = SHA256.Create())
                envelope.checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope, false))))
                    .Replace("-", string.Empty).ToLowerInvariant();
            return JsonUtility.ToJson(envelope, true);
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
            public LegacyProfileDataV1 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveDataV2 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileDataV1
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
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveDataV2
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
        private sealed class LegacySettingsSaveEnvelope
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyGameSettingsSaveData settings;
        }

        [Serializable]
        private sealed class LegacyGameSettingsSaveData
        {
            public string languageId;
            public float masterVolume;
            public float musicVolume;
            public float effectsVolume;
        }

        [Serializable]
        private sealed class LegacySettingsSaveEnvelopeV4
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyGameSettingsSaveDataV4 settings;
        }

        [Serializable]
        private sealed class LegacyGameSettingsSaveDataV4
        {
            public string languageId;
            public float masterVolume;
            public float musicVolume;
            public float effectsVolume;
            public float ambienceVolume;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV4
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileSaveDataV5 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveDataV4 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV4Brew
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileSaveDataV5 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveDataV4Brew activeExpedition;
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
        private sealed class LegacySettingsSaveEnvelopeV5
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public GameSettingsSaveData settings;
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
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>();
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveDataV4
        {
            public string runId;
            public uint seed;
            public int generationVersion;
            public int contentVersion;
            public string classId;
            public string raceId;
            public string[] selectedSkillIds;
            public string[] startingEquipmentInstanceIds;
            public string routeSnapshotJson;
            public string currentNodeId;
            public string[] visitedNodeIds;
            public string[] completedRoomIds;
            public ExpeditionRoomCompletionSaveData[] completedRoomResults;
            public string roomEntryCheckpointJson;
            public string inventorySnapshotJson;
            // JsonUtility serializes an inline null with these historical field initializers.
            public int maximumHealth = 100;
            public int currentHealth = 100;
            public int maximumEnergy = 50;
            public int currentEnergy = 50;
            public int healthPotionCharges = 2;
            public int accumulatedRenown;
            public string[] pactIds;
            public LegacyFortuneOfferBookSnapshotV4 fortuneOfferBookSnapshot;
            public string[] collectedItemIds;
            public string[] claimedRewardIds;

            public LegacyExpeditionSaveDataV4() { }

            public LegacyExpeditionSaveDataV4(ExpeditionSaveData source)
            {
                runId = source.runId;
                seed = source.seed;
                generationVersion = source.generationVersion;
                contentVersion = 4;
                classId = source.classId;
                raceId = source.raceId;
                selectedSkillIds = source.selectedSkillIds;
                startingEquipmentInstanceIds = source.startingEquipmentInstanceIds;
                routeSnapshotJson = source.routeSnapshotJson;
                currentNodeId = source.currentNodeId;
                visitedNodeIds = source.visitedNodeIds;
                completedRoomIds = source.completedRoomIds;
                completedRoomResults = source.completedRoomResults;
                roomEntryCheckpointJson = EncodeLegacyCheckpointV4(source, includesFortuneBrewReceipts: false);
                inventorySnapshotJson = source.inventorySnapshotJson;
                maximumHealth = source.maximumHealth;
                currentHealth = source.currentHealth;
                maximumEnergy = source.maximumEnergy;
                currentEnergy = source.currentEnergy;
                healthPotionCharges = source.healthPotionCharges;
                accumulatedRenown = source.accumulatedRenown;
                pactIds = source.pactIds;
                fortuneOfferBookSnapshot = new LegacyFortuneOfferBookSnapshotV4(source.fortuneOfferBookSnapshot);
                collectedItemIds = source.collectedItemIds;
                claimedRewardIds = source.claimedRewardIds;
            }
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveDataV4Brew
        {
            public string runId;
            public uint seed;
            public int generationVersion;
            public int contentVersion;
            public string classId;
            public string raceId;
            public string[] selectedSkillIds;
            public string[] startingEquipmentInstanceIds;
            public string routeSnapshotJson;
            public string currentNodeId;
            public string[] visitedNodeIds;
            public string[] completedRoomIds;
            public ExpeditionRoomCompletionSaveData[] completedRoomResults;
            public string roomEntryCheckpointJson;
            public string inventorySnapshotJson;
            public int maximumHealth = 100;
            public int currentHealth = 100;
            public int maximumEnergy = 50;
            public int currentEnergy = 50;
            public int healthPotionCharges = 2;
            public int accumulatedRenown;
            public string[] pactIds;
            public LegacyFortuneOfferBookSnapshotV4Brew fortuneOfferBookSnapshot;
            public string[] collectedItemIds;
            public string[] claimedRewardIds;

            public LegacyExpeditionSaveDataV4Brew() { }

            public LegacyExpeditionSaveDataV4Brew(ExpeditionSaveData source)
            {
                runId = source.runId;
                seed = source.seed;
                generationVersion = source.generationVersion;
                contentVersion = 4;
                classId = source.classId;
                raceId = source.raceId;
                selectedSkillIds = source.selectedSkillIds;
                startingEquipmentInstanceIds = source.startingEquipmentInstanceIds;
                routeSnapshotJson = source.routeSnapshotJson;
                currentNodeId = source.currentNodeId;
                visitedNodeIds = source.visitedNodeIds;
                completedRoomIds = source.completedRoomIds;
                completedRoomResults = source.completedRoomResults;
                roomEntryCheckpointJson = EncodeLegacyCheckpointV4(source, includesFortuneBrewReceipts: true);
                inventorySnapshotJson = source.inventorySnapshotJson;
                maximumHealth = source.maximumHealth;
                currentHealth = source.currentHealth;
                maximumEnergy = source.maximumEnergy;
                currentEnergy = source.currentEnergy;
                healthPotionCharges = source.healthPotionCharges;
                accumulatedRenown = source.accumulatedRenown;
                pactIds = source.pactIds;
                fortuneOfferBookSnapshot = new LegacyFortuneOfferBookSnapshotV4Brew(source.fortuneOfferBookSnapshot);
                collectedItemIds = source.collectedItemIds;
                claimedRewardIds = source.claimedRewardIds;
            }
        }

        private static string EncodeLegacyCheckpointV4(ExpeditionSaveData source, bool includesFortuneBrewReceipts)
        {
            if (includesFortuneBrewReceipts)
            {
                return JsonUtility.ToJson(new LegacyExpeditionRoomCheckpointSaveDataV4Brew
                {
                    roomNodeId = source.currentNodeId,
                    inventorySnapshotJson = source.inventorySnapshotJson,
                    maximumHealth = source.maximumHealth,
                    currentHealth = source.currentHealth,
                    maximumEnergy = source.maximumEnergy,
                    currentEnergy = source.currentEnergy,
                    healthPotionCharges = source.healthPotionCharges,
                    accumulatedRenown = source.accumulatedRenown,
                    fortuneOfferBookSnapshot = new LegacyFortuneOfferBookSnapshotV4Brew(source.fortuneOfferBookSnapshot),
                    pactIds = source.pactIds,
                    collectedItemIds = source.collectedItemIds,
                    claimedRewardIds = source.claimedRewardIds
                });
            }
            return JsonUtility.ToJson(new LegacyExpeditionRoomCheckpointSaveDataV4
            {
                roomNodeId = source.currentNodeId,
                inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges,
                accumulatedRenown = source.accumulatedRenown,
                fortuneOfferBookSnapshot = new LegacyFortuneOfferBookSnapshotV4(source.fortuneOfferBookSnapshot),
                pactIds = source.pactIds,
                collectedItemIds = source.collectedItemIds,
                claimedRewardIds = source.claimedRewardIds
            });
        }

        [Serializable]
        private sealed class LegacyExpeditionRoomCheckpointSaveDataV4
        {
            public string roomNodeId;
            public string inventorySnapshotJson;
            public int maximumHealth;
            public int currentHealth;
            public int maximumEnergy;
            public int currentEnergy;
            public int healthPotionCharges;
            public int accumulatedRenown;
            public LegacyFortuneOfferBookSnapshotV4 fortuneOfferBookSnapshot;
            public string[] pactIds;
            public string[] collectedItemIds;
            public string[] claimedRewardIds;
        }

        [Serializable]
        private sealed class LegacyExpeditionRoomCheckpointSaveDataV4Brew
        {
            public string roomNodeId;
            public string inventorySnapshotJson;
            public int maximumHealth;
            public int currentHealth;
            public int maximumEnergy;
            public int currentEnergy;
            public int healthPotionCharges;
            public int accumulatedRenown;
            public LegacyFortuneOfferBookSnapshotV4Brew fortuneOfferBookSnapshot;
            public string[] pactIds;
            public string[] collectedItemIds;
            public string[] claimedRewardIds;
        }

        [Serializable]
        private sealed class LegacyFortuneOfferBookSnapshotV4
        {
            public int snapshotVersion;
            public int generationVersion;
            public uint seed;
            public string raceId;
            public int charges;
            public FortuneOfferSnapshot[] offers;
            public string[] processedGuardianIds;
            public FortuneRerollReceiptSnapshot[] rerollReceipts;

            public LegacyFortuneOfferBookSnapshotV4(FortuneOfferBookSnapshot source)
            {
                snapshotVersion = 2;
                generationVersion = source.generationVersion;
                seed = source.seed;
                raceId = source.raceId;
                charges = source.charges;
                offers = source.offers;
                processedGuardianIds = source.processedGuardianIds;
                rerollReceipts = source.rerollReceipts;
            }
        }

        [Serializable]
        private sealed class LegacyFortuneOfferBookSnapshotV4Brew
        {
            public int snapshotVersion;
            public int generationVersion;
            public uint seed;
            public string raceId;
            public int charges;
            public FortuneOfferSnapshot[] offers;
            public string[] processedGuardianIds;
            public FortuneRerollReceiptSnapshot[] rerollReceipts;
            public FortuneBrewReceiptSnapshot[] fortuneBrewReceipts;

            public LegacyFortuneOfferBookSnapshotV4Brew(FortuneOfferBookSnapshot source)
            {
                snapshotVersion = 3;
                generationVersion = source.generationVersion;
                seed = source.seed;
                raceId = source.raceId;
                charges = source.charges;
                offers = source.offers;
                processedGuardianIds = source.processedGuardianIds;
                rerollReceipts = source.rerollReceipts;
                fortuneBrewReceipts = source.fortuneBrewReceipts;
            }
        }

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
