using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityRandom = UnityEngine.Random;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveLegacyReadOnlyApplicationTests
    {
        private const string LegacyDiagnostic = "LegacyEncounterPinMissing";
        private const string LegacyDiagnosticMessage =
            "La expedición guardada se conserva, pero no puede continuarse porque falta la referencia de su catálogo de encuentros.";
        private const string SettingsFixture = "V6-S0-settings.Frozen.envelope.json";
        private const string SettingsFixtureSha256 = "ed872b2bd66dfab35fd2b0ca44a32cac8b20dbf1c9a209b2e6660f0ab2331139";

        private static readonly FrozenProfileCase[] FrozenProfiles =
        {
            new FrozenProfileCase("V6-P0-profile-no-active.Frozen.envelope.json",
                "601421132919a20b2f46e2f7e635ec81c7413180164e6be42de78b95646a1405", hasActiveExpedition: false),
            new FrozenProfileCase("V6-P1-active-known-nontrivial.Frozen.envelope.json",
                "6814ec3b42d7e754622dac845339b53175ec79f95f6354e7a2fb57c3e03493f3", hasActiveExpedition: true),
            new FrozenProfileCase("V6-P2-active-unknown-null.Frozen.envelope.json",
                "4e9fa1737207471c28fa1d4dd361f9b29ed670ea602887f2e43b41b41d58a5ce", hasActiveExpedition: true),
            new FrozenProfileCase("V6-P3-receipts-full.Frozen.envelope.json",
                "2b3b77e26dff116d1b99521546bf720fd305a89be73ce5263718dc37c796386c", hasActiveExpedition: true)
        };

        [UnityTest]
        public IEnumerator BootstrapKeepsFrozenP0P1P2P3ReadableAndRoutesToMenu()
        {
            UnityRandom.State previousRandomState = UnityRandom.state;
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            bool previousAudioPaused = AudioListener.pause;
            float previousTimeScale = Time.timeScale;
            CountingSaveStore store = null;

            try
            {
                SilenceTestAudioAndStabilizeStartup();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;

                foreach (FrozenProfileCase profileCase in FrozenProfiles)
                {
                    string frozenProfileJson = ReadFrozenFixture(profileCase.FileName, profileCase.Sha256);
                    store = NewStore(frozenProfileJson, backupProfile: null);
                    ApplicationBootstrapper app = null;
                    yield return StartBootstrapAndWaitForMenu(value => app = value);

                    ProfileSaveEnvelope frozen = JsonUtility.FromJson<ProfileSaveEnvelope>(frozenProfileJson);
                    Assert.That(frozen, Is.Not.Null, profileCase.FileName);
                    Assert.That(app.SaveDataReady, Is.True, profileCase.FileName);
                    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"), profileCase.FileName);
                    Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary), profileCase.FileName);
                    Assert.That(app.Profile.availableRenown, Is.EqualTo(frozen.profile.availableRenown), profileCase.FileName);
                    Assert.That(app.HasActiveExpedition, Is.EqualTo(profileCase.HasActiveExpedition), profileCase.FileName);
                    Assert.That(app.ActiveRun, Is.Null, profileCase.FileName);
                    Assert.That(app.CanContinueExpedition, Is.False, profileCase.FileName);

                    if (profileCase.HasActiveExpedition)
                    {
                        Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing), profileCase.FileName);
                        Assert.That(app.CanWriteProfile, Is.False, profileCase.FileName);
                        Assert.That(app.CanStartNewExpedition, Is.False, profileCase.FileName);
                        Assert.That(app.ActiveExpedition, Is.Not.Null, profileCase.FileName);
                        Assert.That(app.ActiveExpedition.runId, Is.EqualTo(frozen.activeExpedition.runId), profileCase.FileName);
                        Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(frozen.activeExpedition.currentNodeId), profileCase.FileName);
                        Assert.That(app.ActiveExpeditionLoadError, Is.EqualTo(LegacyDiagnostic), profileCase.FileName);
                        Assert.That(app.ProfileLoadMessage, Is.EqualTo(LegacyDiagnosticMessage), profileCase.FileName);
                    }
                    else
                    {
                        Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite), profileCase.FileName);
                        Assert.That(app.CanWriteProfile, Is.True, profileCase.FileName);
                        Assert.That(app.CanStartNewExpedition, Is.True, profileCase.FileName);
                        Assert.That(app.ActiveExpedition, Is.Null, profileCase.FileName);
                        Assert.That(app.ActiveExpeditionLoadError, Is.Empty, profileCase.FileName);
                    }

                    // Exercise the repository's real source export against the same frozen store;
                    // the exported JSON must preserve the selected document byte-for-byte as text.
                    var repository = new SaveRepository(store);
                    SaveLoadResult<ProfileSaveEnvelope> repositoryLoad = null;
                    yield return Complete(repository.LoadProfileAsync(), value => repositoryLoad = value);
                    Assert.That(repositoryLoad.HasUsableData, Is.True, profileCase.FileName);
                    Assert.That(repositoryLoad.Access, Is.EqualTo(profileCase.HasActiveExpedition
                        ? SaveDataAccess.ReadOnlyLegacyEncounterPinMissing
                        : SaveDataAccess.ReadWrite), profileCase.FileName);
                    Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(frozenProfileJson), profileCase.FileName);

                    Assert.That(store.ProfileWriteAttempts, Is.Zero, profileCase.FileName);
                    Assert.That(store.ProfilePrimary, Is.EqualTo(frozenProfileJson), profileCase.FileName);
                    Assert.That(store.ProfileBackup, Is.Null, profileCase.FileName);
                    Assert.That(store.SettingsPrimary, Is.EqualTo(ReadFrozenFixture(SettingsFixture, SettingsFixtureSha256)), profileCase.FileName);

                    DestroyCurrentApplication();
                    yield return null;
                }
            }
            finally
            {
                DestroyCurrentApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                AudioListener.pause = previousAudioPaused;
                Time.timeScale = previousTimeScale;
                UnityRandom.state = previousRandomState;
            }
        }

        [UnityTest]
        public IEnumerator ProtectedPrimaryBlocksApplicationMutationGatewaysAndHubEntry()
        {
            UnityRandom.State previousRandomState = UnityRandom.state;
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            bool previousAudioPaused = AudioListener.pause;
            float previousTimeScale = Time.timeScale;
            string frozenPrimary = ReadFrozenFixture(FrozenProfiles[1].FileName, FrozenProfiles[1].Sha256);
            string frozenBackup = ReadFrozenFixture(FrozenProfiles[2].FileName, FrozenProfiles[2].Sha256);
            string frozenSettings = ReadFrozenFixture(SettingsFixture, SettingsFixtureSha256);
            var store = NewStore(frozenPrimary, frozenBackup);

            try
            {
                SilenceTestAudioAndStabilizeStartup();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                ApplicationBootstrapper app = null;
                yield return StartBootstrapAndWaitForMenu(value => app = value);
                AssertProtectedApplicationState(app, SaveLoadStatus.LoadedPrimary, frozenPrimary);

                SceneManager.LoadScene("Hub", LoadSceneMode.Single);
                for (int frame = 0; frame < 60 && SceneManager.GetActiveScene().name != "Hub"; frame++) yield return null;
                for (int frame = 0; frame < 3; frame++) yield return null;

                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
                Text status = GameObject.Find("HubPreparationStatus")?.GetComponent<Text>();
                Button startButton = GameObject.Find("StartExpeditionButton")?.GetComponent<Button>();
                Assert.That(status, Is.Not.Null, "The existing hub diagnostic status element should be present.");
                Assert.That(status.text, Does.Contain("La expedición guardada necesita revisión y se conserva"));
                Assert.That(status.text, Does.Contain(LegacyDiagnostic));
                Assert.That(startButton, Is.Not.Null);
                Assert.That(startButton.interactable, Is.False, "The Hub must not offer a new or continued run for a protected profile.");

                string profileBefore = JsonUtility.ToJson(app.Profile, false);
                string expeditionBefore = JsonUtility.ToJson(app.ActiveExpedition, false);
                int renownBefore = app.AvailableRenown;
                long revisionBefore = app.ActiveExpeditionRevision;

                SaveWriteResult start = null;
                yield return Complete(app.BeginExpeditionAsync(null), value => start = value);
                Assert.That(start.Succeeded, Is.False);
                Assert.That(start.Message, Does.Contain(LegacyDiagnosticMessage));

                Assert.Throws<InvalidOperationException>(() => app.ContinueActiveExpedition());
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"), "Rejected continue must not switch scenes.");

                SaveWriteResult snapshotSave = null;
                yield return Complete(app.SaveActiveExpeditionStateAsync(app.ActiveExpedition), value => snapshotSave = value);
                Assert.That(snapshotSave.Succeeded, Is.False);
                SaveWriteResult roomComplete = null;
                yield return Complete(app.CompleteCurrentRoomAsync("test.legacy.blocked", "{\"diagnostic\":\"blocked\"}"),
                    value => roomComplete = value);
                Assert.That(roomComplete.Succeeded, Is.False);
                SaveWriteResult roomAdvance = null;
                yield return Complete(app.AdvanceToRoomAsync("node.legacy.blocked"), value => roomAdvance = value);
                Assert.That(roomAdvance.Succeeded, Is.False);
                RoomAdvanceCommitResult roomRetry = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => roomRetry = value);
                Assert.That(roomRetry.Succeeded, Is.False);
                Assert.That(roomRetry.AlreadyApplied, Is.False);
                Assert.That(roomRetry.Activated, Is.False);
                SaveWriteResult expeditionRetry = null;
                yield return Complete(app.RetryPendingExpeditionWriteAsync(), value => expeditionRetry = value);
                Assert.That(expeditionRetry.Succeeded, Is.False);

                ExpeditionSettlementCommitResult settlement = null;
                yield return Complete(app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0), value => settlement = value);
                Assert.That(settlement.Receipt, Is.Null, "The rejection must not calculate a settlement receipt.");
                Assert.That(settlement.Persisted, Is.False);
                Assert.That(settlement.Pending, Is.False);
                ExpeditionSettlementCommitResult settlementRetry = null;
                yield return Complete(app.RetryPendingSettlementAsync(), value => settlementRetry = value);
                Assert.That(settlementRetry.Receipt, Is.Null);
                Assert.That(settlementRetry.Persisted, Is.False);

                ContractProgressionCommitResult accept = null;
                yield return Complete(app.AcceptContractAsync("CT01"), value => accept = value);
                ContractProgressionCommitResult claim = null;
                yield return Complete(app.ClaimContractRewardAsync("CT01"), value => claim = value);
                ContractProgressionCommitResult contractRetry = null;
                yield return Complete(app.RetryPendingContractProgressionAsync(), value => contractRetry = value);
                Assert.That(accept.Persisted, Is.False);
                Assert.That(claim.Persisted, Is.False);
                Assert.That(claim.Reward, Is.Null, "A blocked claim must not expose or pay a reward.");
                Assert.That(contractRetry.Persisted, Is.False);

                ProgressionPurchaseCommitResult purchase = null;
                yield return Complete(app.PurchasePermanentUpgradeAsync(PermanentUpgradeKind.MaximumHealth,
                    TransactionId.New()), value => purchase = value);
                ProgressionPurchaseCommitResult purchaseRetry = null;
                yield return Complete(app.RetryPendingPermanentUpgradePurchaseAsync(), value => purchaseRetry = value);
                Assert.That(purchase.Persisted, Is.False);
                Assert.That(purchase.Purchase, Is.Null);
                Assert.That(purchaseRetry.Persisted, Is.False);

                CraftingCommitResult craft = null;
                yield return Complete(app.CraftActiveExpeditionAsync("recipe.health_potion", TransactionId.New()),
                    value => craft = value);
                CraftingCommitResult craftRetry = null;
                yield return Complete(app.RetryPendingCraftingAsync(), value => craftRetry = value);
                Assert.That(craft.Persisted, Is.False);
                Assert.That(craft.Pending, Is.False);
                Assert.That(craft.Outcome, Is.Null);
                Assert.That(craftRetry.Persisted, Is.False);

                RoomInstanceId roomId = RoomInstanceId.New();
                ExpeditionMutationCommitResult<EconomyTransactionOutcome> talia = null;
                yield return Complete(app.UseTaliaAsync(roomId, TransactionId.New()), value => talia = value);
                ExpeditionMutationCommitResult<EconomyTransactionOutcome> economyRetry = null;
                yield return Complete(app.RetryPendingEconomyMutationAsync<EconomyTransactionOutcome>("legacy.blocked"),
                    value => economyRetry = value);
                Assert.That(talia.Persisted, Is.False);
                Assert.That(talia.Pending, Is.False);
                Assert.That(talia.Outcome, Is.Null);
                Assert.That(economyRetry.Persisted, Is.False);

                ExpeditionMutationCommitResult<FortuneOfferResult> fortune = null;
                yield return Complete(app.CreateFortuneOfferAsync("legacy.blocked.offer", "legacy.blocked.context",
                    new[] { "consumable.health_potion" }), value => fortune = value);
                ExpeditionMutationCommitResult<FortuneOfferResult> fortuneRetry = null;
                yield return Complete(app.RetryPendingFortuneMutationAsync<FortuneOfferResult>("legacy.blocked"),
                    value => fortuneRetry = value);
                Assert.That(fortune.Persisted, Is.False);
                Assert.That(fortune.Pending, Is.False);
                Assert.That(fortune.Outcome, Is.Null);
                Assert.That(fortuneRetry.Persisted, Is.False);

                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> brew = null;
                yield return Complete(app.ConsumeFortuneBrewAsync(default, TransactionId.New()), value => brew = value);
                ExpeditionMutationCommitResult<FortuneBrewApplicationOutcome> brewRetry = null;
                yield return Complete(app.RetryPendingFortuneMutationAsync<FortuneBrewApplicationOutcome>("legacy.blocked"),
                    value => brewRetry = value);
                Assert.That(brew.Persisted, Is.False);
                Assert.That(brew.Outcome, Is.Null);
                Assert.That(brewRetry.Persisted, Is.False);

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> reward = null;
                yield return Complete(app.ClaimFortuneOfferRewardAsync("legacy.blocked.offer"), value => reward = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> rewardRetry = null;
                yield return Complete(app.RetryPendingFortuneMutationAsync<FortuneRewardApplicationOutcome>("legacy.blocked"),
                    value => rewardRetry = value);
                Assert.That(reward.Persisted, Is.False);
                Assert.That(reward.Outcome, Is.Null);
                Assert.That(rewardRetry.Persisted, Is.False);

                for (int frame = 0; frame < 2; frame++) yield return null;
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.CanWriteProfile, Is.False);
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(app.ActiveExpeditionLoadError, Is.EqualTo(LegacyDiagnostic));
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileBefore));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(expeditionBefore));
                Assert.That(app.AvailableRenown, Is.EqualTo(renownBefore));
                Assert.That(app.ActiveExpeditionRevision, Is.EqualTo(revisionBefore));
                Assert.That(app.HasPendingSettlement, Is.False);
                Assert.That(app.HasPendingContractProgression, Is.False);
                Assert.That(app.HasPendingPermanentUpgradePurchase, Is.False);
                Assert.That(app.HasPendingCraftingWrite, Is.False);
                Assert.That(app.HasPendingEconomyMutation, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
                Assert.That(store.ProfileWriteAttempts, Is.Zero);
                Assert.That(store.ProfilePrimary, Is.EqualTo(frozenPrimary));
                Assert.That(store.ProfileBackup, Is.EqualTo(frozenBackup));
                Assert.That(store.SettingsPrimary, Is.EqualTo(frozenSettings));
            }
            finally
            {
                DestroyCurrentApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                AudioListener.pause = previousAudioPaused;
                Time.timeScale = previousTimeScale;
                UnityRandom.state = previousRandomState;
            }
        }

        [UnityTest]
        public IEnumerator RecoveredBackupStaysReadOnlyWhileSettingsWriteAndRetryRemainIndependent()
        {
            UnityRandom.State previousRandomState = UnityRandom.state;
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            bool previousAudioPaused = AudioListener.pause;
            float previousTimeScale = Time.timeScale;
            string invalidPrimary = "{malformed-primary";
            string frozenBackup = ReadFrozenFixture(FrozenProfiles[3].FileName, FrozenProfiles[3].Sha256);
            string frozenSettings = ReadFrozenFixture(SettingsFixture, SettingsFixtureSha256);
            var store = NewStore(invalidPrimary, frozenBackup);
            store.FailNextSettingsWrite = true;

            try
            {
                SilenceTestAudioAndStabilizeStartup();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                ApplicationBootstrapper app = null;
                yield return StartBootstrapAndWaitForMenu(value => app = value);
                AssertProtectedApplicationState(app, SaveLoadStatus.RecoveredBackup, frozenBackup);

                var repository = new SaveRepository(store);
                SaveLoadResult<ProfileSaveEnvelope> recoveredExport = null;
                yield return Complete(repository.LoadProfileAsync(), value => recoveredExport = value);
                Assert.That(recoveredExport.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
                Assert.That(recoveredExport.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(repository.ExportLoadedProfileJson(), Is.EqualTo(frozenBackup),
                    "The backup export must be the exact selected JSON source, not a DTO reserialization.");

                Assert.That(store.ProfilePrimary, Is.EqualTo(invalidPrimary));
                Assert.That(store.ProfileBackup, Is.EqualTo(frozenBackup));
                Assert.That(store.ProfileWriteAttempts, Is.Zero, "Recovery itself must not repair or rotate either profile file.");

                string profileJsonBefore = JsonUtility.ToJson(app.Profile, false);
                string expeditionJsonBefore = JsonUtility.ToJson(app.ActiveExpedition, false);
                int renownBefore = app.AvailableRenown;
                var changedSettings = new GameSettingsSaveData
                {
                    languageId = "en",
                    masterVolume = 0.5f,
                    musicVolume = 0.4f,
                    effectsVolume = 0.3f,
                    ambienceVolume = 0.2f
                };

                SaveWriteResult firstSettingsWrite = null;
                yield return Complete(app.SaveSettingsAsync(changedSettings), value => firstSettingsWrite = value);
                Assert.That(firstSettingsWrite.Succeeded, Is.False);
                Assert.That(app.PendingSettingsForRetry, Is.Not.Null);
                Assert.That(store.SettingsWriteAttempts, Is.EqualTo(1));
                Assert.That(store.FailedSettingsJson, Is.Not.Null.And.Not.Empty);
                Assert.That(store.ProfileWriteAttempts, Is.Zero);

                SaveWriteResult settingsRetry = null;
                yield return Complete(app.RetryPendingSettingsAsync(), value => settingsRetry = value);
                Assert.That(settingsRetry.Succeeded, Is.True, settingsRetry.Message);
                Assert.That(store.SettingsWriteAttempts, Is.EqualTo(2));
                Assert.That(store.FailedSettingsJson, Is.EqualTo(store.SuccessfulSettingsJson[0]),
                    "The settings retry must send the exact JSON payload first attempted.");
                Assert.That(store.SuccessfulSettingsLastKnownGoodPrimaryJson[0], Is.EqualTo(frozenSettings));
                Assert.That(app.PendingSettingsForRetry, Is.Null);

                for (int frame = 0; frame < 2; frame++) yield return null;
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.CanWriteProfile, Is.False);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileJsonBefore));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(expeditionJsonBefore));
                Assert.That(app.AvailableRenown, Is.EqualTo(renownBefore));
                Assert.That(store.ProfileWriteAttempts, Is.Zero);
                Assert.That(store.ProfilePrimary, Is.EqualTo(invalidPrimary));
                Assert.That(store.ProfileBackup, Is.EqualTo(frozenBackup));
            }
            finally
            {
                DestroyCurrentApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                AudioListener.pause = previousAudioPaused;
                Time.timeScale = previousTimeScale;
                UnityRandom.state = previousRandomState;
            }
        }

        private static void AssertProtectedApplicationState(
            ApplicationBootstrapper app, SaveLoadStatus expectedLoadStatus, string frozenProfileJson)
        {
            ProfileSaveEnvelope frozen = JsonUtility.FromJson<ProfileSaveEnvelope>(frozenProfileJson);
            Assert.That(app, Is.Not.Null);
            Assert.That(app.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            Assert.That(app.ProfileLoadStatus, Is.EqualTo(expectedLoadStatus));
            Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(app.CanWriteProfile, Is.False);
            Assert.That(app.CanStartNewExpedition, Is.False);
            Assert.That(app.CanContinueExpedition, Is.False);
            Assert.That(app.HasActiveExpedition, Is.True);
            Assert.That(app.ActiveExpedition, Is.Not.Null);
            Assert.That(app.ActiveExpedition.runId, Is.EqualTo(frozen.activeExpedition.runId));
            Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(frozen.activeExpedition.currentNodeId));
            Assert.That(app.ActiveRun, Is.Null);
            Assert.That(app.ActiveExpeditionLoadError, Is.EqualTo(LegacyDiagnostic));
            Assert.That(app.ProfileLoadMessage, Is.EqualTo(LegacyDiagnosticMessage));
        }

        private static CountingSaveStore NewStore(string profilePrimary, string backupProfile)
        {
            return new CountingSaveStore(profilePrimary, backupProfile,
                ReadFrozenFixture(SettingsFixture, SettingsFixtureSha256));
        }

        private static string ReadFrozenFixture(string fileName, string expectedSha256)
        {
            string path = Path.Combine(Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V6", fileName);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(ComputeSha256(bytes), Is.EqualTo(expectedSha256), fileName + " must match its measured frozen bytes.");
            return new UTF8Encoding(false).GetString(bytes);
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void SilenceTestAudioAndStabilizeStartup()
        {
            UnityRandom.InitState(20261030);
            AudioListener.pause = true;
            Time.timeScale = 1f;
        }

        private static IEnumerator StartBootstrapAndWaitForMenu(Action<ApplicationBootstrapper> ready)
        {
            DestroyCurrentApplication();
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                yield return null;

            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            ready(ApplicationBootstrapper.Instance);
        }

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private static void DestroyCurrentApplication()
        {
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
        }

        private sealed class FrozenProfileCase
        {
            public FrozenProfileCase(string fileName, string sha256, bool hasActiveExpedition)
            {
                FileName = fileName;
                Sha256 = sha256;
                HasActiveExpedition = hasActiveExpedition;
            }

            public string FileName { get; }
            public string Sha256 { get; }
            public bool HasActiveExpedition { get; }
        }

        private sealed class CountingSaveStore : ISaveStore
        {
            private readonly List<WriteAttempt> _attempts = new List<WriteAttempt>();

            public CountingSaveStore(string profilePrimary, string backupProfile, string settingsPrimary)
            {
                ProfilePrimary = profilePrimary;
                ProfileBackup = backupProfile;
                SettingsPrimary = settingsPrimary;
            }

            public string ProfilePrimary { get; private set; }
            public string ProfileBackup { get; private set; }
            public string SettingsPrimary { get; private set; }
            public string SettingsBackup { get; private set; }
            public bool FailNextSettingsWrite { get; set; }
            public int ProfileReadAttempts { get; private set; }
            public int SettingsReadAttempts { get; private set; }
            public int ProfileWriteAttempts { get; private set; }
            public int SettingsWriteAttempts { get; private set; }
            public string FailedSettingsJson { get; private set; }
            public IReadOnlyList<string> SuccessfulSettingsJson => _attempts.FindAll(attempt =>
                attempt.Document == SaveDocument.Settings && !attempt.Failed).ConvertAll(attempt => attempt.Json);
            public IReadOnlyList<string> SuccessfulSettingsLastKnownGoodPrimaryJson => _attempts.FindAll(attempt =>
                attempt.Document == SaveDocument.Settings && !attempt.Failed).ConvertAll(attempt => attempt.LastKnownGoodPrimaryJson);

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileReadAttempts++;
                    return Task.FromResult(new SaveStorageSnapshot(ProfilePrimary, ProfileBackup));
                }

                SettingsReadAttempts++;
                return Task.FromResult(new SaveStorageSnapshot(SettingsPrimary, SettingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                _attempts.Add(new WriteAttempt(document, json, lastKnownGoodPrimaryJson));
                if (document == SaveDocument.Profile)
                {
                    ProfileWriteAttempts++;
                    if (lastKnownGoodPrimaryJson != null) ProfileBackup = lastKnownGoodPrimaryJson;
                    ProfilePrimary = json;
                    return Task.CompletedTask;
                }

                SettingsWriteAttempts++;
                if (FailNextSettingsWrite)
                {
                    FailNextSettingsWrite = false;
                    FailedSettingsJson = json;
                    _attempts[_attempts.Count - 1].Failed = true;
                    throw new IOException("simulated settings-only storage failure");
                }

                if (lastKnownGoodPrimaryJson != null) SettingsBackup = lastKnownGoodPrimaryJson;
                SettingsPrimary = json;
                return Task.CompletedTask;
            }
        }

        private sealed class WriteAttempt
        {
            public WriteAttempt(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                Document = document;
                Json = json;
                LastKnownGoodPrimaryJson = lastKnownGoodPrimaryJson;
            }

            public SaveDocument Document { get; }
            public string Json { get; }
            public string LastKnownGoodPrimaryJson { get; }
            public bool Failed { get; set; }
        }
    }
}
