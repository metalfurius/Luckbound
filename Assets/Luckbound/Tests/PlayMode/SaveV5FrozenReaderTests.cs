using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Fortune;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveV5FrozenReaderTests
    {
        private const string ExpectedWrapperSha256 = "541a00ad2e952d6941bddeaf6fc2d5fbd5f57ae70b64ad3970825a42f669e656";
        private const string ExpectedNativeIndexSha256 = "8f61d83adea8cbaa6c54aaefa15a0ad21b82e3e345ea94ea543ec032d79ac2d9";
        private const string ExpectedAuthoritySha256 = "55745d6f5c687977c800a8baf45f48212efe773a5dd0d843164fe2d15954b4a7";
        private const int WaitFrameLimit = 600;
        private const string PrimaryCorruption = "not-json/v5-reader/primary/";
        private const string BackupCorruption = "not-json/v5-reader/backup/";
        private static readonly string[] CaseIds =
        {
            "P0-no-active",
            "P1-known-zero",
            "P1-known-nontrivial",
            "P2-unknown-null",
            "P2-unknown-inline",
            "P3-receipts-completed",
            "B0-arrays-null",
            "B1-arrays-empty",
            "M0-false-marker-payload",
            "M1-true-marker-null",
            "S0-settings",
            "N0-tampered"
        };
        private static readonly string[] Cultures = { "en-US", "es-ES" };
        private static readonly UTF8Encoding StrictUtf8NoBom = new UTF8Encoding(false, true);

        private FixtureManifest _manifest;
        private NativeFixtureIndex _nativeIndex;
        private string _fixtureRoot;
        private string _captureRoot;
        private string _temporaryDirectory;

        private static IEnumerable<TestCaseData> CodecCases()
        {
            foreach (string culture in Cultures)
            foreach (string caseId in CaseIds)
                yield return new TestCaseData(caseId, culture).SetName("V5Codec_" + caseId + "_" + culture);
        }

        [OneTimeSetUp]
        public void VerifyFrozenInputs()
        {
            _fixtureRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V5"));
            string wrapperPath = Path.Combine(_fixtureRoot, "reader-fixtures.json");
            byte[] wrapperBytes = File.ReadAllBytes(wrapperPath);
            Assert.That(Sha256(wrapperBytes), Is.EqualTo(ExpectedWrapperSha256), "Reader wrapper bytes are frozen.");
            _manifest = JsonUtility.FromJson<FixtureManifest>(StrictUtf8NoBom.GetString(wrapperBytes));
            Assert.That(_manifest, Is.Not.Null);
            Assert.That(_manifest.schemaVersion, Is.EqualTo(1));
            Assert.That(_manifest.captureRoot, Is.EqualTo("capture"));
            Assert.That(_manifest.nativeIndexSha256, Is.EqualTo(ExpectedNativeIndexSha256));
            Assert.That(_manifest.authoritySha256, Is.EqualTo(ExpectedAuthoritySha256));
            Assert.That(_manifest.authorityPath, Is.EqualTo("reader-authority.json"));

            string nativeIndexPath = Path.Combine(_fixtureRoot, _manifest.captureRoot, "fixture-index.json");
            byte[] nativeIndexBytes = File.ReadAllBytes(nativeIndexPath);
            Assert.That(Sha256(nativeIndexBytes), Is.EqualTo(ExpectedNativeIndexSha256), "Native R5 index bytes are frozen.");
            _nativeIndex = JsonUtility.FromJson<NativeFixtureIndex>(StrictUtf8NoBom.GetString(nativeIndexBytes));
            Assert.That(_nativeIndex, Is.Not.Null);
            Assert.That(_nativeIndex.cases, Is.Not.Null);
            Assert.That(_nativeIndex.cases.Length, Is.EqualTo(24));

            string authorityPath = Path.Combine(_fixtureRoot, _manifest.authorityPath);
            byte[] authorityBytes = File.ReadAllBytes(authorityPath);
            Assert.That(Sha256(authorityBytes), Is.EqualTo(ExpectedAuthoritySha256), "R5 authority bytes are frozen.");
            ReaderAuthority authority = JsonUtility.FromJson<ReaderAuthority>(StrictUtf8NoBom.GetString(authorityBytes));
            Assert.That(authority, Is.Not.Null);
            Assert.That(authority.authority, Is.EqualTo("Accepted"));
            Assert.That(authority.sourceIntegrity, Is.False);
            Assert.That(authority.originalCi.conclusion, Is.EqualTo("failure"));

            _captureRoot = ResolveDirectory(_fixtureRoot, _manifest.captureRoot);
            ValidateManifestAndNativeRows();
        }

        [TestCaseSource(nameof(CodecCases))]
        public void CodecMatchesAllFrozenV5Rows(string caseId, string cultureName)
        {
            FixtureRow row = FindRow(caseId, cultureName);
            byte[] rawBytes = ReadFixtureBytes(row.envelopePath);
            string rawJson = StrictUtf8NoBom.GetString(rawBytes);
            string observedStatus;
            int sourceContentVersion = 0;
            ProfileSaveEnvelope profile = null;
            SettingsSaveEnvelope settings = null;
            CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
            CultureInfo previousUiCulture = Thread.CurrentThread.CurrentUICulture;
            try
            {
                SetCulture(cultureName);
                if (row.document == "Settings")
                {
                    DecodeStatus status = SaveJsonCodec.Decode(rawJson, out settings);
                    observedStatus = status.ToString();
                }
                else
                {
                    DecodeStatus status = SaveJsonCodec.Decode(rawJson, out profile, out sourceContentVersion);
                    observedStatus = status.ToString();
                }

                LogRow("codec", row, "direct", observedStatus, "reached", row.envelopeSha256);
                Assert.That(observedStatus, Is.EqualTo(row.expectedDecode), RowLabel(row, "codec status"));

                if (row.document == "Settings")
                {
                    AssertSettingsDecode(row, rawJson, settings);
                }
                else if (row.expectedDecode == "Valid")
                {
                    Assert.That(sourceContentVersion, Is.EqualTo(5), RowLabel(row, "authenticated source content version"));
                    AssertProfileMapping(row, rawJson, profile);
                }
                else
                {
                    Assert.That(sourceContentVersion, Is.EqualTo(0), RowLabel(row, "invalid source content version remains unpublished"));
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }
        }

        [UnityTest]
        public IEnumerator RepositoryPrimarySlotCoversEveryFrozenRowWithoutWrites()
        {
            return RunRepositoryMatrix(primarySlot: true);
        }

        [UnityTest]
        public IEnumerator RepositoryBackupSlotCoversEveryFrozenRowWithoutWrites()
        {
            return RunRepositoryMatrix(primarySlot: false);
        }

        [UnityTest]
        public IEnumerator N0PrimaryFallsBackToP3BackupForBothCultures()
        {
            var failures = new List<string>();
            foreach (string culture in Cultures)
            {
                FixtureRow negative = FindRow("N0-tampered", culture);
                FixtureRow backupRow = FindRow("P3-receipts-completed", culture);
                string primary = ReadFixtureText(negative);
                string backup = ReadFixtureText(backupRow);
                var store = new RecordingSaveStore(profilePrimaryJson: primary, profileBackupJson: backup);
                var repository = new SaveRepository(store);
                CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
                CultureInfo previousUiCulture = Thread.CurrentThread.CurrentUICulture;
                SetCulture(culture);
                Task<SaveLoadResult<ProfileSaveEnvelope>> loadTask = repository.LoadProfileAsync();
                yield return WaitForCompletion(loadTask);
                if (!loadTask.IsCompleted || loadTask.IsFaulted || loadTask.IsCanceled)
                {
                    failures.Add(culture + ": profile fallback load did not complete successfully");
                    LogRow("fallback", negative, "N0-primary/P3-backup", "unavailable", "not-completed", negative.envelopeSha256);
                }
                else
                {
                    SaveLoadResult<ProfileSaveEnvelope> loaded = loadTask.GetAwaiter().GetResult();
                    RunChecks(failures, culture + " N0 primary → P3 backup",
                        () => Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup)),
                        () => Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadWrite)),
                        () => Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite)),
                        () => Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(backup)),
                        () => AssertProfileMapping(backupRow, backup, loaded.Data),
                        () => Assert.That(store.WriteCalls, Is.Empty),
                        () => Assert.That(store.ProfilePrimaryJson, Is.EqualTo(primary)),
                        () => Assert.That(store.ProfileBackupJson, Is.EqualTo(backup)),
                        () => Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null));
                    LogRow("fallback", negative, "N0-primary/P3-backup", loaded.Status.ToString(),
                        failures.Count == 0 ? "checked" : "assertion-failure", negative.envelopeSha256);
                }
                Thread.CurrentThread.CurrentCulture = previousCulture;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [UnityTest]
        public IEnumerator ValidSettingsRemainIndependentWhenProfileIsRejectedForBothCultures()
        {
            var failures = new List<string>();
            foreach (string culture in Cultures)
            {
                FixtureRow rejectedProfile = FindRow("N0-tampered", culture);
                FixtureRow settingsRow = FindRow("S0-settings", culture);
                string profilePrimary = ReadFixtureText(rejectedProfile);
                string profileBackup = ReadFixtureText(FindRow("M0-false-marker-payload", culture));
                string settingsPrimary = ReadFixtureText(settingsRow);
                string settingsBackup = BackupCorruption + "settings-independent/" + culture;
                var store = new RecordingSaveStore(profilePrimaryJson: profilePrimary, profileBackupJson: profileBackup,
                    settingsPrimaryJson: settingsPrimary, settingsBackupJson: settingsBackup);
                var repository = new SaveRepository(store);
                CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
                CultureInfo previousUiCulture = Thread.CurrentThread.CurrentUICulture;
                SetCulture(culture);

                Task<SaveLoadResult<ProfileSaveEnvelope>> profileTask = repository.LoadProfileAsync();
                yield return WaitForCompletion(profileTask);
                SaveLoadResult<ProfileSaveEnvelope> profileResult = null;
                if (profileTask.IsCompleted && !profileTask.IsFaulted && !profileTask.IsCanceled)
                {
                    profileResult = profileTask.GetAwaiter().GetResult();
                    if (profileResult == null) failures.Add(culture + ": rejected profile load returned null");
                }
                else
                    failures.Add(culture + ": rejected profile load did not complete");

                Task<SaveLoadResult<SettingsSaveEnvelope>> settingsTask = repository.LoadSettingsAsync();
                yield return WaitForCompletion(settingsTask);
                SaveLoadResult<SettingsSaveEnvelope> settingsResult = null;
                if (settingsTask.IsCompleted && !settingsTask.IsFaulted && !settingsTask.IsCanceled)
                {
                    settingsResult = settingsTask.GetAwaiter().GetResult();
                    if (settingsResult == null) failures.Add(culture + ": independent settings load returned null");
                }
                else
                    failures.Add(culture + ": independent settings load did not complete");

                if (profileResult != null && settingsResult != null)
                {
                    RunChecks(failures, culture + " settings independent from rejected profile",
                        () => Assert.That(profileResult.Status, Is.EqualTo(SaveLoadStatus.Corrupted)),
                        () => Assert.That(profileResult.Access, Is.EqualTo(SaveDataAccess.Unavailable)),
                        () => Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable)),
                        () => Assert.That(settingsResult.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary)),
                        () => Assert.That(settingsResult.Access, Is.EqualTo(SaveDataAccess.ReadWrite)),
                        () => AssertSettingsDecode(settingsRow, settingsPrimary, settingsResult.Data),
                        () => Assert.That(store.WriteCalls, Is.Empty),
                        () => Assert.That(store.ProfilePrimaryJson, Is.EqualTo(profilePrimary)),
                        () => Assert.That(store.ProfileBackupJson, Is.EqualTo(profileBackup)),
                        () => Assert.That(store.SettingsPrimaryJson, Is.EqualTo(settingsPrimary)),
                        () => Assert.That(store.SettingsBackupJson, Is.EqualTo(settingsBackup)),
                        () => Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null),
                        () => Assert.That(repository.ExportPendingJson(SaveDocument.Settings), Is.Null),
                        () => Assert.That(repository.PendingSettingsForRetry, Is.Null));
                    LogRow("settings-independent", settingsRow, "rejected-profile/valid-settings-primary",
                        settingsResult.Status.ToString(), failures.Count == 0 ? "checked" : "assertion-failure",
                        settingsRow.envelopeSha256);
                }

                Thread.CurrentThread.CurrentCulture = previousCulture;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [UnityTest]
        public IEnumerator FileSaveStoreUsesFourIsolatedFilesAndPreservesThemByteForByte()
        {
            FixtureRow invalidProfile = FindRow("N0-tampered", "en-US");
            FixtureRow invalidProfileBackup = FindRow("M0-false-marker-payload", "en-US");
            FixtureRow validSettings = FindRow("S0-settings", "en-US");
            string profilePrimary = ReadFixtureText(invalidProfile);
            string profileBackup = ReadFixtureText(invalidProfileBackup);
            string settingsPrimary = PrimaryCorruption + "file-store/settings-primary";
            string settingsBackup = ReadFixtureText(validSettings);
            byte[] profilePrimaryBytes = StrictUtf8NoBom.GetBytes(profilePrimary);
            byte[] profileBackupBytes = StrictUtf8NoBom.GetBytes(profileBackup);
            byte[] settingsPrimaryBytes = StrictUtf8NoBom.GetBytes(settingsPrimary);
            byte[] settingsBackupBytes = StrictUtf8NoBom.GetBytes(settingsBackup);

            _temporaryDirectory = CreateOwnedTemporaryDirectory();
            var syncer = new RecordingFileSystemSyncer();
            var fileStore = new FileSaveStore(_temporaryDirectory, syncer);
            Directory.CreateDirectory(_temporaryDirectory);
            File.WriteAllBytes(fileStore.GetPrimaryPath(SaveDocument.Profile), profilePrimaryBytes);
            File.WriteAllBytes(fileStore.GetBackupPath(SaveDocument.Profile), profileBackupBytes);
            File.WriteAllBytes(fileStore.GetPrimaryPath(SaveDocument.Settings), settingsPrimaryBytes);
            File.WriteAllBytes(fileStore.GetBackupPath(SaveDocument.Settings), settingsBackupBytes);

            var repository = new SaveRepository(fileStore);
            Task<SaveLoadResult<ProfileSaveEnvelope>> profileTask = repository.LoadProfileAsync();
            yield return WaitForCompletion(profileTask);
            Assert.That(profileTask.IsCompleted && !profileTask.IsFaulted && !profileTask.IsCanceled, Is.True,
                "FileSaveStore profile read must finish within the frame limit.");
            SaveLoadResult<ProfileSaveEnvelope> profileResult = profileTask.GetAwaiter().GetResult();

            Task<SaveLoadResult<SettingsSaveEnvelope>> settingsTask = repository.LoadSettingsAsync();
            yield return WaitForCompletion(settingsTask);
            Assert.That(settingsTask.IsCompleted && !settingsTask.IsFaulted && !settingsTask.IsCanceled, Is.True,
                "FileSaveStore settings read must finish within the frame limit.");
            SaveLoadResult<SettingsSaveEnvelope> settingsResult = settingsTask.GetAwaiter().GetResult();

            UnityEngine.Debug.Log("{\"phase\":\"file-store\",\"profile\":\"" + profileResult.Status +
                "\",\"settings\":\"" + settingsResult.Status + "\",\"files\":4}");
            Assert.That(profileResult.Status, Is.EqualTo(SaveLoadStatus.Corrupted));
            Assert.That(profileResult.Access, Is.EqualTo(SaveDataAccess.Unavailable));
            Assert.That(settingsResult.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(settingsResult.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            AssertSettingsDecode(validSettings, settingsBackup, settingsResult.Data);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(repository.ExportPendingJson(SaveDocument.Settings), Is.Null);
            Assert.That(repository.PendingSettingsForRetry, Is.Null);
            Assert.That(syncer.FlushCount, Is.EqualTo(0), "Loading must not flush or write the store.");
            Assert.That(syncer.PrepareForReadCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(profilePrimaryBytes, File.ReadAllBytes(fileStore.GetPrimaryPath(SaveDocument.Profile)));
            CollectionAssert.AreEqual(profileBackupBytes, File.ReadAllBytes(fileStore.GetBackupPath(SaveDocument.Profile)));
            CollectionAssert.AreEqual(settingsPrimaryBytes, File.ReadAllBytes(fileStore.GetPrimaryPath(SaveDocument.Settings)));
            CollectionAssert.AreEqual(settingsBackupBytes, File.ReadAllBytes(fileStore.GetBackupPath(SaveDocument.Settings)));
        }

        [TearDown]
        public void RemoveOwnedTemporaryDirectory()
        {
            if (string.IsNullOrEmpty(_temporaryDirectory) || !Directory.Exists(_temporaryDirectory)) return;
            string target = Path.GetFullPath(_temporaryDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string parent = Path.GetDirectoryName(target);
            string leaf = Path.GetFileName(target);
            if (!string.Equals(parent, tempRoot, StringComparison.OrdinalIgnoreCase) ||
                !leaf.StartsWith("LuckboundGateDV5ReaderTests-", StringComparison.Ordinal) || leaf.Length != 32 + "LuckboundGateDV5ReaderTests-".Length)
                throw new InvalidOperationException("Refusing to remove a temporary directory outside the owned Gate D test path.");

            Directory.Delete(target, recursive: true);
            _temporaryDirectory = null;
        }

        private void ValidateManifestAndNativeRows()
        {
            Assert.That(_manifest.rows, Is.Not.Null);
            Assert.That(_manifest.rows.Length, Is.EqualTo(24));
            Assert.That(_nativeIndex.cases.Length, Is.EqualTo(24));
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int validCount = 0;
            int corruptedCount = 0;
            int outputCount = 0;
            foreach (FixtureRow row in _manifest.rows)
            {
                string key = row.caseId + "|" + row.culture;
                Assert.That(seen.Add(key), Is.True, "Duplicate wrapper row " + key);
                Assert.That(CaseIds.Contains(row.caseId), Is.True, "Unexpected case ID " + row.caseId);
                Assert.That(Cultures.Contains(row.culture), Is.True, "Unexpected culture " + row.culture);
                Assert.That(row.document, Is.EqualTo(row.caseId == "S0-settings" ? "Settings" : "Profile"));
                Assert.That(row.expectedDecode, Is.EqualTo(IsCorruptedCase(row.caseId) ? "Corrupted" : "Valid"));
                if (row.expectedDecode == "Valid") validCount++;
                else corruptedCount++;
                if (row.document == "Profile")
                    Assert.That(row.sourceContentVersion, Is.EqualTo(row.expectedDecode == "Valid" ? 5 : 0));

                string expectedPath = "cultures/" + row.culture + "/" + row.caseId + "/envelope.json";
                Assert.That(row.envelopePath, Is.EqualTo(expectedPath), "Only fixed relative paths are allowed.");
                byte[] envelopeBytes = ReadFixtureBytes(row.envelopePath);
                string envelopeHash = Sha256(envelopeBytes);
                Assert.That(envelopeHash, Is.EqualTo(row.envelopeSha256), RowLabel(row, "wrapper envelope hash"));

                NativeFixtureCase native = FindNativeCase(row.caseId, row.culture);
                Assert.That(native.decode, Is.Not.Null);
                Assert.That(native.decode.status, Is.EqualTo(row.expectedDecode.ToLowerInvariant()), RowLabel(row, "native decode expectation"));
                NativeFixtureOutput nativeEnvelope = FindNativeOutput(native, row.envelopePath);
                Assert.That(nativeEnvelope.sha256, Is.EqualTo(row.envelopeSha256), RowLabel(row, "wrapper/native envelope hash"));
                Assert.That(nativeEnvelope.bytes, Is.EqualTo(envelopeBytes.Length), RowLabel(row, "envelope byte count"));

                if (row.document == "Profile")
                {
                    string activeStringsPath = "cultures/" + row.culture + "/" + row.caseId + "/active-strings.json";
                    NativeFixtureOutput nativeActiveStrings = FindNativeOutput(native, activeStringsPath);
                    byte[] activeStringsBytes = ReadCaptureBytes(activeStringsPath);
                    Assert.That(Sha256(activeStringsBytes), Is.EqualTo(nativeActiveStrings.sha256), RowLabel(row, "active strings native hash"));
                    Assert.That(activeStringsBytes.Length, Is.EqualTo(nativeActiveStrings.bytes), RowLabel(row, "active strings byte count"));
                }
            }

            var checkedOutputPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (NativeFixtureCase native in _nativeIndex.cases)
            foreach (NativeFixtureOutput output in native.outputs)
            {
                Assert.That(checkedOutputPaths.Add(output.path), Is.True, "Duplicate native output path " + output.path);
                byte[] outputBytes = ReadCaptureBytes(output.path);
                Assert.That(outputBytes.Length, Is.EqualTo(output.bytes), "Native output byte count " + output.path);
                Assert.That(Sha256(outputBytes), Is.EqualTo(output.sha256), "Native output hash " + output.path);
                outputCount++;
            }

            Assert.That(seen.Count, Is.EqualTo(24));
            Assert.That(validCount, Is.EqualTo(18));
            Assert.That(corruptedCount, Is.EqualTo(6));
            Assert.That(outputCount, Is.EqualTo(194), "Native R5 capture has exactly 194 outputs plus its index.");
        }

        private IEnumerator RunRepositoryMatrix(bool primarySlot)
        {
            var failures = new List<string>();
            string slot = primarySlot ? "primary" : "backup";
            foreach (FixtureRow row in _manifest.rows)
            {
                string fixture = ReadFixtureText(row);
                string syntheticPrimary = PrimaryCorruption + row.caseId + "/" + row.culture;
                string syntheticBackup = BackupCorruption + row.caseId + "/" + row.culture;
                string primary = primarySlot ? fixture : syntheticPrimary;
                string backup = primarySlot ? syntheticBackup : fixture;
                var store = new RecordingSaveStore(profilePrimaryJson: row.document == "Profile" ? primary : null,
                    profileBackupJson: row.document == "Profile" ? backup : null,
                    settingsPrimaryJson: row.document == "Settings" ? primary : null,
                    settingsBackupJson: row.document == "Settings" ? backup : null);
                var repository = new SaveRepository(store);
                CultureInfo previousCulture = Thread.CurrentThread.CurrentCulture;
                CultureInfo previousUiCulture = Thread.CurrentThread.CurrentUICulture;
                SetCulture(row.culture);

                SaveLoadStatus? observedStatus = null;
                if (row.document == "Profile")
                {
                    Task<SaveLoadResult<ProfileSaveEnvelope>> loadTask = repository.LoadProfileAsync();
                    yield return WaitForCompletion(loadTask);
                    SaveLoadResult<ProfileSaveEnvelope> loaded = null;
                    if (loadTask.IsCompleted && !loadTask.IsFaulted && !loadTask.IsCanceled)
                    {
                        try { loaded = loadTask.GetAwaiter().GetResult();
                            if (loaded == null) failures.Add(RowLabel(row, "load returned null")); }
                        catch (Exception exception) { failures.Add(RowLabel(row, "profile load exception: " + exception.Message)); }
                    }
                    else
                    {
                        failures.Add(RowLabel(row, "profile load did not complete within " + WaitFrameLimit + " frames"));
                    }

                    if (loaded != null)
                    {
                        observedStatus = loaded.Status;
                        var checks = new List<Action>
                        {
                            () => Assert.That(loaded.Status, Is.EqualTo(ExpectedRepositoryStatus(row, primarySlot)))
                        };
                        if (row.expectedDecode == "Valid")
                        {
                            checks.Add(() => Assert.That(loaded.Data, Is.Not.Null));
                            checks.Add(() => Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadWrite)));
                            checks.Add(() => Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite)));
                            checks.Add(() => Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(fixture)));
                            if (loaded.Data != null)
                                checks.Add(() => AssertProfileMapping(row, fixture, loaded.Data));
                        }
                        else
                        {
                            checks.Add(() => Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.Unavailable)));
                            checks.Add(() => Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable)));
                            checks.Add(() => Assert.That(repository.ExportLoadedProfileJson(), Is.Null));
                        }
                        RunChecks(failures, RowLabel(row, "profile load assertions"), checks.ToArray());
                    }

                    Task<SaveWriteResult> retryTask = repository.RetryPendingAsync(SaveDocument.Profile);
                    yield return WaitForCompletion(retryTask);
                    SaveWriteResult retryResult = null;
                    if (retryTask.IsCompleted && !retryTask.IsFaulted && !retryTask.IsCanceled)
                    {
                        try { retryResult = retryTask.GetAwaiter().GetResult();
                            if (retryResult == null) failures.Add(RowLabel(row, "retry returned null")); }
                        catch (Exception exception) { failures.Add(RowLabel(row, "profile retry exception: " + exception.Message)); }
                    }
                    else
                    {
                        failures.Add(RowLabel(row, "profile RetryPending did not complete within " + WaitFrameLimit + " frames"));
                    }
                    if (retryResult != null && retryResult.Succeeded)
                        failures.Add(RowLabel(row, "unexpected successful profile RetryPending with no pending write"));
                    if (repository.ExportPendingJson(SaveDocument.Profile) != null)
                        failures.Add(RowLabel(row, "profile pending JSON exists after read/retry"));
                }
                else
                {
                    Task<SaveLoadResult<SettingsSaveEnvelope>> loadTask = repository.LoadSettingsAsync();
                    yield return WaitForCompletion(loadTask);
                    SaveLoadResult<SettingsSaveEnvelope> loaded = null;
                    if (loadTask.IsCompleted && !loadTask.IsFaulted && !loadTask.IsCanceled)
                    {
                        try { loaded = loadTask.GetAwaiter().GetResult();
                            if (loaded == null) failures.Add(RowLabel(row, "load returned null")); }
                        catch (Exception exception) { failures.Add(RowLabel(row, "settings load exception: " + exception.Message)); }
                    }
                    else
                    {
                        failures.Add(RowLabel(row, "settings load did not complete within " + WaitFrameLimit + " frames"));
                    }

                    if (loaded != null)
                    {
                        observedStatus = loaded.Status;
                        var checks = new List<Action>
                        {
                            () => Assert.That(loaded.Status, Is.EqualTo(ExpectedRepositoryStatus(row, primarySlot))),
                            () => Assert.That(loaded.Data, Is.Not.Null),
                            () => Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadWrite))
                        };
                        if (loaded.Data != null)
                            checks.Add(() => AssertSettingsDecode(row, fixture, loaded.Data));
                        RunChecks(failures, RowLabel(row, "settings load assertions"), checks.ToArray());
                    }

                    Task<SaveWriteResult> retryTask = repository.RetryPendingAsync(SaveDocument.Settings);
                    yield return WaitForCompletion(retryTask);
                    SaveWriteResult retryResult = null;
                    if (retryTask.IsCompleted && !retryTask.IsFaulted && !retryTask.IsCanceled)
                    {
                        try { retryResult = retryTask.GetAwaiter().GetResult();
                            if (retryResult == null) failures.Add(RowLabel(row, "retry returned null")); }
                        catch (Exception exception) { failures.Add(RowLabel(row, "settings retry exception: " + exception.Message)); }
                    }
                    else
                    {
                        failures.Add(RowLabel(row, "settings RetryPending did not complete within " + WaitFrameLimit + " frames"));
                    }
                    if (retryResult != null && retryResult.Succeeded)
                        failures.Add(RowLabel(row, "unexpected successful settings RetryPending with no pending write"));
                    if (repository.PendingSettingsForRetry != null || repository.ExportPendingJson(SaveDocument.Settings) != null)
                        failures.Add(RowLabel(row, "settings pending payload exists after read/retry"));
                }

                if (store.WriteCalls.Count != 0)
                    failures.Add(RowLabel(row, "store WriteAsync was called during load/retry"));
                SaveDocument document = row.document == "Profile" ? SaveDocument.Profile : SaveDocument.Settings;
                if (!string.Equals(store.GetPrimary(document), primary, StringComparison.Ordinal) ||
                    !string.Equals(store.GetBackup(document), backup, StringComparison.Ordinal))
                    failures.Add(RowLabel(row, "selected document primary/backup strings changed"));
                if (store.ReadCount(document) != 1)
                    failures.Add(RowLabel(row, "expected exactly one read of the selected document"));

                LogRow("repository", row, slot, observedStatus.HasValue ? observedStatus.Value.ToString() : "unavailable",
                    failures.Count == 0 ? "reached" : "reached-with-failures", row.envelopeSha256);
                Thread.CurrentThread.CurrentCulture = previousCulture;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }

            Assert.That(failures, Is.Empty, "Repository " + slot + " matrix failures:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        private void AssertSettingsDecode(FixtureRow row, string rawJson, SettingsSaveEnvelope decoded)
        {
            Assert.That(decoded, Is.Not.Null, RowLabel(row, "decoded settings envelope"));
            SettingsSaveEnvelope raw = JsonUtility.FromJson<SettingsSaveEnvelope>(rawJson);
            Assert.That(raw, Is.Not.Null, RowLabel(row, "raw settings envelope"));
            Assert.That(raw.contentVersion, Is.EqualTo(5), RowLabel(row, "raw settings source version"));
            Assert.That(decoded.schemaVersion, Is.EqualTo(raw.schemaVersion));
            Assert.That(decoded.contentVersion, Is.EqualTo(6), RowLabel(row, "in-memory settings migration version"));
            Assert.That(decoded.generation, Is.EqualTo(raw.generation));
            Assert.That(decoded.sequence, Is.EqualTo(raw.sequence));
            Assert.That(decoded.savedAtUtc, Is.EqualTo(raw.savedAtUtc));
            Assert.That(decoded.settings, Is.Not.Null);
            Assert.That(raw.settings, Is.Not.Null);
            Assert.That(decoded.settings.languageId, Is.EqualTo(raw.settings.languageId));
            Assert.That(decoded.settings.masterVolume, Is.EqualTo(raw.settings.masterVolume));
            Assert.That(decoded.settings.musicVolume, Is.EqualTo(raw.settings.musicVolume));
            Assert.That(decoded.settings.effectsVolume, Is.EqualTo(raw.settings.effectsVolume));
            Assert.That(decoded.settings.ambienceVolume, Is.EqualTo(raw.settings.ambienceVolume));
        }

        private void AssertProfileMapping(FixtureRow row, string rawJson, ProfileSaveEnvelope decoded)
        {
            Assert.That(decoded, Is.Not.Null, RowLabel(row, "decoded profile envelope"));
            ProfileSaveEnvelope raw = JsonUtility.FromJson<ProfileSaveEnvelope>(rawJson);
            Assert.That(raw, Is.Not.Null, RowLabel(row, "raw profile envelope"));
            Assert.That(raw.contentVersion, Is.EqualTo(5), RowLabel(row, "raw profile source version"));
            Assert.That(decoded.schemaVersion, Is.EqualTo(raw.schemaVersion));
            Assert.That(decoded.contentVersion, Is.EqualTo(6), RowLabel(row, "in-memory profile migration version"));
            Assert.That(decoded.generation, Is.EqualTo(raw.generation));
            Assert.That(decoded.sequence, Is.EqualTo(raw.sequence));
            Assert.That(decoded.savedAtUtc, Is.EqualTo(raw.savedAtUtc));
            Assert.That(decoded.hasActiveExpedition, Is.EqualTo(raw.hasActiveExpedition));
            Assert.That(decoded.profile, Is.Not.Null);
            Assert.That(raw.profile, Is.Not.Null);
            AssertProfileDataEqual(raw.profile, decoded.profile, row);
            Assert.That(decoded.profile.processedContractProgressEvents, Is.Not.Null,
                RowLabel(row, "V5 migration initializes the later receipt collection"));
            Assert.That(decoded.profile.processedContractProgressEvents.Length, Is.EqualTo(0),
                RowLabel(row, "V5 migration does not invent contract progress receipts"));

            CapturedActiveStrings captured = ReadActiveStrings(row);
            Assert.That(decoded.hasActiveExpedition, Is.EqualTo(captured.hasActiveExpedition), RowLabel(row, "active marker"));
            Assert.That(captured.activePayloadPresent, Is.EqualTo(raw.activeExpedition != null),
                RowLabel(row, "raw active payload presence"));
            if (!captured.hasActiveExpedition)
            {
                Assert.That(decoded.activeExpedition, Is.Null, RowLabel(row, "inactive V5 payload normalizes to absent"));
                Assert.That(captured.routeSnapshotJson, Is.EqualTo(string.Empty));
                Assert.That(captured.inventorySnapshotJson, Is.EqualTo(string.Empty));
                Assert.That(captured.roomEntryCheckpointJson, Is.EqualTo(string.Empty));
                Assert.That(captured.completedRoomResults, Is.Empty);
                return;
            }

            Assert.That(raw.activeExpedition, Is.Not.Null, RowLabel(row, "raw active V5 payload"));
            Assert.That(decoded.activeExpedition, Is.Not.Null, RowLabel(row, "mapped active V5 payload"));
            AssertExpeditionDataEqual(raw.activeExpedition, decoded.activeExpedition, row);
            Assert.That(decoded.activeExpedition.routeSnapshotJson, Is.EqualTo(captured.routeSnapshotJson), RowLabel(row, "route snapshot string"));
            Assert.That(decoded.activeExpedition.inventorySnapshotJson, Is.EqualTo(captured.inventorySnapshotJson), RowLabel(row, "inventory snapshot string"));
            Assert.That(decoded.activeExpedition.roomEntryCheckpointJson, Is.EqualTo(captured.roomEntryCheckpointJson), RowLabel(row, "checkpoint string"));
            Assert.That(decoded.activeExpedition.completedRoomResults.Length, Is.EqualTo(captured.completedRoomResults.Length));
            for (int index = 0; index < captured.completedRoomResults.Length; index++)
            {
                Assert.That(decoded.activeExpedition.completedRoomResults[index].roomNodeId,
                    Is.EqualTo(captured.completedRoomResults[index].roomNodeId), RowLabel(row, "completed result room node"));
                Assert.That(decoded.activeExpedition.completedRoomResults[index].resultId,
                    Is.EqualTo(captured.completedRoomResults[index].resultId), RowLabel(row, "completed result ID"));
                Assert.That(decoded.activeExpedition.completedRoomResults[index].resultJson,
                    Is.EqualTo(captured.completedRoomResults[index].resultJson), RowLabel(row, "completed result raw JSON"));
            }
        }

        private static void AssertProfileDataEqual(ProfileSaveData expected, ProfileSaveData actual, FixtureRow row)
        {
            Assert.That(actual.availableRenown, Is.EqualTo(expected.availableRenown));
            Assert.That(actual.healthUpgradeLevel, Is.EqualTo(expected.healthUpgradeLevel));
            Assert.That(actual.energyUpgradeLevel, Is.EqualTo(expected.energyUpgradeLevel));
            Assert.That(actual.additionalPotionUpgradeLevel, Is.EqualTo(expected.additionalPotionUpgradeLevel));
            AssertStringArrays(expected.unlockedRaceIds, actual.unlockedRaceIds, "unlockedRaceIds");
            AssertStringArrays(expected.unlockedStartingKitIds, actual.unlockedStartingKitIds, "unlockedStartingKitIds");
            AssertStringArrays(expected.acceptedContractIds, actual.acceptedContractIds, "acceptedContractIds");
            AssertStringArrays(expected.claimedContractIds, actual.claimedContractIds, "claimedContractIds");
            AssertStringArrays(expected.readStoryFragmentIds, actual.readStoryFragmentIds, "readStoryFragmentIds");
            AssertStringArrays(expected.processedSettlementRunIds, actual.processedSettlementRunIds, "processedSettlementRunIds");
            AssertArray(expected.contractCounters, actual.contractCounters, "contractCounters", (left, right) =>
            {
                Assert.That(right.contractId, Is.EqualTo(left.contractId));
                Assert.That(right.progress, Is.EqualTo(left.progress));
            });
            AssertArray(expected.progressionPurchaseTransactions, actual.progressionPurchaseTransactions,
                "progressionPurchaseTransactions", (left, right) =>
                {
                    Assert.That(right.transactionId, Is.EqualTo(left.transactionId));
                    Assert.That(right.kind, Is.EqualTo(left.kind));
                    Assert.That(right.cost, Is.EqualTo(left.cost));
                });
        }

        private static void AssertExpeditionDataEqual(ExpeditionSaveData expected, ExpeditionSaveData actual, FixtureRow row)
        {
            Assert.That(actual.runId, Is.EqualTo(expected.runId));
            Assert.That(actual.seed, Is.EqualTo(expected.seed));
            Assert.That(actual.generationVersion, Is.EqualTo(expected.generationVersion));
            Assert.That(actual.contentVersion, Is.EqualTo(expected.contentVersion));
            Assert.That(actual.classId, Is.EqualTo(expected.classId));
            Assert.That(actual.raceId, Is.EqualTo(expected.raceId));
            AssertStringArrays(expected.selectedSkillIds, actual.selectedSkillIds, "selectedSkillIds");
            AssertStringArrays(expected.startingEquipmentInstanceIds, actual.startingEquipmentInstanceIds, "startingEquipmentInstanceIds");
            Assert.That(actual.routeSnapshotJson, Is.EqualTo(expected.routeSnapshotJson));
            Assert.That(actual.currentNodeId, Is.EqualTo(expected.currentNodeId));
            AssertStringArrays(expected.visitedNodeIds, actual.visitedNodeIds, "visitedNodeIds");
            AssertStringArrays(expected.completedRoomIds, actual.completedRoomIds, "completedRoomIds");
            Assert.That(actual.roomEntryCheckpointJson, Is.EqualTo(expected.roomEntryCheckpointJson));
            Assert.That(actual.inventorySnapshotJson, Is.EqualTo(expected.inventorySnapshotJson));
            Assert.That(actual.maximumHealth, Is.EqualTo(expected.maximumHealth));
            Assert.That(actual.currentHealth, Is.EqualTo(expected.currentHealth));
            Assert.That(actual.maximumEnergy, Is.EqualTo(expected.maximumEnergy));
            Assert.That(actual.currentEnergy, Is.EqualTo(expected.currentEnergy));
            Assert.That(actual.healthPotionCharges, Is.EqualTo(expected.healthPotionCharges));
            Assert.That(actual.hasFrozenPermanentUpgrades, Is.EqualTo(expected.hasFrozenPermanentUpgrades));
            Assert.That(actual.accumulatedRenown, Is.EqualTo(expected.accumulatedRenown));
            AssertStringArrays(expected.pactIds, actual.pactIds, "pactIds");
            AssertStringArrays(expected.collectedItemIds, actual.collectedItemIds, "collectedItemIds");
            AssertStringArrays(expected.claimedRewardIds, actual.claimedRewardIds, "claimedRewardIds");
            AssertArray(expected.completedRoomResults, actual.completedRoomResults, "completedRoomResults", (left, right) =>
            {
                Assert.That(right.roomNodeId, Is.EqualTo(left.roomNodeId));
                Assert.That(right.resultId, Is.EqualTo(left.resultId));
                Assert.That(right.resultJson, Is.EqualTo(left.resultJson));
            });
            if (expected.hasFrozenPermanentUpgrades)
            {
                Assert.That(expected.frozenPermanentUpgrades, Is.Not.Null, RowLabel(row, "known frozen upgrades have a payload"));
                AssertUpgradeSnapshot(expected.frozenPermanentUpgrades, actual.frozenPermanentUpgrades);
            }
            else
            {
                if (expected.frozenPermanentUpgrades != null)
                {
                    Assert.That(expected.frozenPermanentUpgrades.healthUpgradeLevel, Is.EqualTo(0),
                        RowLabel(row, "unknown raw health upgrade is zero"));
                    Assert.That(expected.frozenPermanentUpgrades.energyUpgradeLevel, Is.EqualTo(0),
                        RowLabel(row, "unknown raw energy upgrade is zero"));
                    Assert.That(expected.frozenPermanentUpgrades.additionalPotionUpgradeLevel, Is.EqualTo(0),
                        RowLabel(row, "unknown raw potion upgrade is zero"));
                }
                Assert.That(actual.frozenPermanentUpgrades, Is.Null,
                    RowLabel(row, "unknown zero upgrades normalize to a null in-memory snapshot"));
            }
            AssertFortuneSnapshot(expected.fortuneOfferBookSnapshot, actual.fortuneOfferBookSnapshot);
        }

        private static void AssertUpgradeSnapshot(ExpeditionPermanentUpgradeSnapshotSaveData expected,
            ExpeditionPermanentUpgradeSnapshotSaveData actual)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.healthUpgradeLevel, Is.EqualTo(expected.healthUpgradeLevel));
            Assert.That(actual.energyUpgradeLevel, Is.EqualTo(expected.energyUpgradeLevel));
            Assert.That(actual.additionalPotionUpgradeLevel, Is.EqualTo(expected.additionalPotionUpgradeLevel));
        }

        private static void AssertFortuneSnapshot(FortuneOfferBookSnapshot expected, FortuneOfferBookSnapshot actual)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null);
                return;
            }

            Assert.That(actual, Is.Not.Null);
            Assert.That(actual.snapshotVersion, Is.EqualTo(expected.snapshotVersion));
            Assert.That(actual.generationVersion, Is.EqualTo(expected.generationVersion));
            Assert.That(actual.seed, Is.EqualTo(expected.seed));
            Assert.That(actual.raceId, Is.EqualTo(expected.raceId));
            Assert.That(actual.charges, Is.EqualTo(expected.charges));
            AssertStringArrays(expected.processedGuardianIds, actual.processedGuardianIds, "processedGuardianIds");
            AssertArray(expected.offers, actual.offers, "fortune offers", (left, right) =>
            {
                Assert.That(right.offerId, Is.EqualTo(left.offerId));
                Assert.That(right.contextId, Is.EqualTo(left.contextId));
                AssertStringArrays(left.eligibleOptionIds, right.eligibleOptionIds, "eligibleOptionIds");
                Assert.That(right.selectedOptionId, Is.EqualTo(left.selectedOptionId));
                Assert.That(right.claimed, Is.EqualTo(left.claimed));
                AssertArray(left.history, right.history, "fortune history", (leftHistory, rightHistory) =>
                    AssertStringArrays(leftHistory.optionIds, rightHistory.optionIds, "history optionIds"));
            });
            AssertArray(expected.rerollReceipts, actual.rerollReceipts, "rerollReceipts", (left, right) =>
            {
                Assert.That(right.transactionId, Is.EqualTo(left.transactionId));
                Assert.That(right.offerId, Is.EqualTo(left.offerId));
                Assert.That(right.status, Is.EqualTo(left.status));
                Assert.That(right.contextId, Is.EqualTo(left.contextId));
                AssertStringArrays(left.optionIds, right.optionIds, "reroll optionIds");
                Assert.That(right.selectedOptionId, Is.EqualTo(left.selectedOptionId));
                Assert.That(right.rerollCount, Is.EqualTo(left.rerollCount));
                Assert.That(right.claimed, Is.EqualTo(left.claimed));
                Assert.That(right.remainingCharges, Is.EqualTo(left.remainingCharges));
                Assert.That(right.hasOffer, Is.EqualTo(left.hasOffer));
            });
            AssertArray(expected.fortuneBrewReceipts, actual.fortuneBrewReceipts, "fortuneBrewReceipts", (left, right) =>
            {
                Assert.That(right.transactionId, Is.EqualTo(left.transactionId));
                Assert.That(right.itemInstanceId, Is.EqualTo(left.itemInstanceId));
                Assert.That(right.status, Is.EqualTo(left.status));
                Assert.That(right.currentCharges, Is.EqualTo(left.currentCharges));
            });
        }

        private static void AssertStringArrays(string[] expected, string[] actual, string label)
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null, label + " preserves null");
                return;
            }
            Assert.That(actual, Is.Not.Null, label + " is present");
            CollectionAssert.AreEqual(expected, actual, label + " values and order");
        }

        private static void AssertArray<T>(T[] expected, T[] actual, string label, Action<T, T> compare)
            where T : class
        {
            if (expected == null)
            {
                Assert.That(actual, Is.Null, label + " preserves null");
                return;
            }
            Assert.That(actual, Is.Not.Null, label + " is present");
            Assert.That(actual.Length, Is.EqualTo(expected.Length), label + " length");
            for (int index = 0; index < expected.Length; index++)
            {
                if (expected[index] == null)
                {
                    Assert.That(actual[index], Is.Null, label + " null entry " + index);
                    continue;
                }
                Assert.That(actual[index], Is.Not.Null, label + " entry " + index);
                compare(expected[index], actual[index]);
            }
        }

        private static void RunChecks(List<string> failures, string label, params Action[] checks)
        {
            foreach (Action check in checks)
            {
                try { check(); }
                catch (Exception exception) { failures.Add(label + ": " + exception.Message); }
            }
        }

        private CapturedActiveStrings ReadActiveStrings(FixtureRow row)
        {
            string path = "cultures/" + row.culture + "/" + row.caseId + "/active-strings.json";
            CapturedActiveStrings captured = JsonUtility.FromJson<CapturedActiveStrings>(StrictUtf8NoBom.GetString(ReadCaptureBytes(path)));
            Assert.That(captured, Is.Not.Null, RowLabel(row, "frozen active string summary"));
            Assert.That(captured.caseId, Is.EqualTo(row.caseId));
            return captured;
        }

        private FixtureRow FindRow(string caseId, string culture)
        {
            FixtureRow[] matches = _manifest.rows.Where(row => row.caseId == caseId && row.culture == culture).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), caseId + "/" + culture + " must have exactly one wrapper row.");
            return matches[0];
        }

        private NativeFixtureCase FindNativeCase(string caseId, string culture)
        {
            NativeFixtureCase[] matches = _nativeIndex.cases.Where(row => row.caseId == caseId && row.culture == culture).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), caseId + "/" + culture + " must have exactly one native index row.");
            return matches[0];
        }

        private static NativeFixtureOutput FindNativeOutput(NativeFixtureCase row, string path)
        {
            NativeFixtureOutput[] matches = row.outputs.Where(output => output.path == path).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Native output must index " + path);
            return matches[0];
        }

        private byte[] ReadFixtureBytes(string relativeEnvelopePath)
        {
            return File.ReadAllBytes(ResolveFile(_captureRoot, relativeEnvelopePath));
        }

        private string ReadFixtureText(FixtureRow row)
        {
            return StrictUtf8NoBom.GetString(ReadFixtureBytes(row.envelopePath));
        }

        private byte[] ReadCaptureBytes(string relativePath)
        {
            return File.ReadAllBytes(ResolveFile(_captureRoot, relativePath));
        }

        private static string ResolveDirectory(string root, string relativePath)
        {
            string resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture directory escapes its root: " + relativePath);
            return resolved;
        }

        private static string ResolveFile(string root, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
                throw new InvalidOperationException("Fixture path must be relative: " + relativePath);
            string resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Fixture file escapes its root: " + relativePath);
            return resolved;
        }

        private static IEnumerator WaitForCompletion(Task task)
        {
            for (int frame = 0; frame < WaitFrameLimit && !task.IsCompleted; frame++)
                yield return null;
        }

        private static SaveLoadStatus ExpectedRepositoryStatus(FixtureRow row, bool primarySlot)
        {
            if (row.expectedDecode == "Corrupted") return SaveLoadStatus.Corrupted;
            return primarySlot ? SaveLoadStatus.LoadedPrimary : SaveLoadStatus.RecoveredBackup;
        }

        private static bool IsCorruptedCase(string caseId)
        {
            return caseId == "M0-false-marker-payload" || caseId == "M1-true-marker-null" || caseId == "N0-tampered";
        }

        private static string RowLabel(FixtureRow row, string label)
        {
            return row.caseId + "/" + row.culture + ": " + label;
        }

        private static void SetCulture(string cultureName)
        {
            CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        private static string Sha256(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void LogRow(string phase, FixtureRow row, string slot, string observed, string reached, string hash)
        {
            UnityEngine.Debug.Log("{\"phase\":\"" + phase + "\",\"caseId\":\"" + row.caseId +
                "\",\"culture\":\"" + row.culture + "\",\"document\":\"" + row.document + "\",\"slot\":\"" +
                slot + "\",\"expectedDecode\":\"" + row.expectedDecode + "\",\"observed\":\"" + observed +
                "\",\"reached\":\"" + reached + "\",\"envelopeSha256\":\"" + hash + "\"}");
        }

        private static string CreateOwnedTemporaryDirectory()
        {
            return Path.Combine(Path.GetFullPath(Path.GetTempPath()), "LuckboundGateDV5ReaderTests-" + Guid.NewGuid().ToString("N"));
        }

        [Serializable]
        private sealed class FixtureManifest
        {
            public int schemaVersion;
            public string captureRoot;
            public string nativeIndexSha256;
            public string authoritySha256;
            public string authorityPath;
            public FixtureRow[] rows;
        }

        [Serializable]
        private sealed class FixtureRow
        {
            public string caseId;
            public string culture;
            public string document;
            public string expectedDecode;
            public string envelopePath;
            public string envelopeSha256;
            public int sourceContentVersion;
        }

        [Serializable]
        private sealed class NativeFixtureIndex
        {
            public NativeFixtureCase[] cases;
        }

        [Serializable]
        private sealed class NativeFixtureCase
        {
            public string caseId;
            public string culture;
            public NativeDecodeResult decode;
            public NativeFixtureOutput[] outputs;
        }

        [Serializable]
        private sealed class NativeDecodeResult
        {
            public string status;
            public bool accepted;
        }

        [Serializable]
        private sealed class NativeFixtureOutput
        {
            public string path;
            public int bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class ReaderAuthority
        {
            public string authority;
            public bool sourceIntegrity;
            public ReaderAuthorityCi originalCi;
        }

        [Serializable]
        private sealed class ReaderAuthorityCi
        {
            public string conclusion;
        }

        [Serializable]
        private sealed class CapturedActiveStrings
        {
            public string caseId;
            public bool hasActiveExpedition;
            public bool activePayloadPresent;
            public string routeSnapshotJson;
            public string inventorySnapshotJson;
            public string roomEntryCheckpointJson;
            public CapturedRoomResult[] completedRoomResults = Array.Empty<CapturedRoomResult>();
        }

        [Serializable]
        private sealed class CapturedRoomResult
        {
            public string roomNodeId;
            public string resultId;
            public string resultJson;
        }

        private sealed class RecordingSaveStore : ISaveStore
        {
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
            public List<SaveDocument> ReadCalls { get; } = new List<SaveDocument>();
            public List<SaveDocument> WriteCalls { get; } = new List<SaveDocument>();

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                ReadCalls.Add(document);
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(ProfilePrimaryJson, ProfileBackupJson)
                    : new SaveStorageSnapshot(SettingsPrimaryJson, SettingsBackupJson));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                WriteCalls.Add(document);
                if (document == SaveDocument.Profile)
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) ProfileBackupJson = lastKnownGoodPrimaryJson;
                    ProfilePrimaryJson = json;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) SettingsBackupJson = lastKnownGoodPrimaryJson;
                    SettingsPrimaryJson = json;
                }
                return Task.CompletedTask;
            }

            public int ReadCount(SaveDocument document) => ReadCalls.Count(call => call == document);
            public string GetPrimary(SaveDocument document) => document == SaveDocument.Profile ? ProfilePrimaryJson : SettingsPrimaryJson;
            public string GetBackup(SaveDocument document) => document == SaveDocument.Profile ? ProfileBackupJson : SettingsBackupJson;
        }

        private sealed class RecordingFileSystemSyncer : IPersistentFileSystemSyncer
        {
            public int PrepareForReadCount { get; private set; }
            public int FlushCount { get; private set; }

            public Task PrepareForReadAsync()
            {
                PrepareForReadCount++;
                return Task.CompletedTask;
            }

            public Task FlushAsync()
            {
                FlushCount++;
                return Task.CompletedTask;
            }
        }
    }
}
