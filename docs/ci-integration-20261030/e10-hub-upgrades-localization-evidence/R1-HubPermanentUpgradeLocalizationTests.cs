using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode
{
    public sealed class HubPermanentUpgradeLocalizationTests : LuckboundInputTestFixture
    {
        private static readonly string[] UpgradeTextNames =
        {
            "PermanentUpgradeTitle",
            "PermanentUpgradeBalance",
            "PermanentUpgradeStatus",
            "PermanentHealthUpgradeButtonLabel",
            "PermanentEnergyUpgradeButtonLabel",
            "PermanentPotionUpgradeButtonLabel",
            "ClosePermanentUpgradesButtonLabel"
        };

        private static readonly string[] UpgradeButtonNames =
        {
            "PermanentHealthUpgradeButton",
            "PermanentEnergyUpgradeButton",
            "PermanentPotionUpgradeButton",
            "ClosePermanentUpgradesButton"
        };

        [UnityTest]
        public IEnumerator UpgradePanelLoadsEnglishAndTranslatesCompleteStatesWithoutProfileWrites()
        {
            string saveDirectory = NewSaveDirectory();
            var backingStore = new FileSaveStore(saveDirectory, null);
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper app = null;
            Time.timeScale = 1f;
            try
            {
                yield return SeedProfileAndSettings(backingStore, CreateFullyUpgradedProfile(), "en");
                var store = new TrackingSaveStore(backingStore);
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                yield return LoadBootstrappedApplication(value => app = value);
                Assert.That(app != null && app.SaveDataReady && app.CanWriteProfile, Is.True,
                    app == null ? "Application profile did not load." : app.ProfileLoadMessage);
                Assert.That(app.Settings.languageId, Is.EqualTo("en"));
                Assert.That(app.HasActiveExpedition, Is.False);
                Assert.That(app.Profile.healthUpgradeLevel, Is.EqualTo(2));
                Assert.That(app.Profile.energyUpgradeLevel, Is.EqualTo(2));
                Assert.That(app.Profile.additionalPotionUpgradeLevel, Is.EqualTo(1));
                Assert.That(app.Profile.availableRenown, Is.Zero);
                Assert.That(app.Profile.progressionPurchaseTransactions.Length, Is.EqualTo(5));

                yield return LoadHubScene();
                OpenUpgradePanel();
                Text[] panelTexts = FindUpgradeTexts();
                Button[] panelButtons = FindUpgradeButtons();
                UpgradeTextMetric[] englishMetrics = RecordUpgradePanelTextMetrics(panelTexts, "en", "fully-upgraded-en");
                AssertUpgradeTextFits(englishMetrics);
                Assert.That(panelButtons.All(button => button != null), Is.True,
                    "The seven text panel needs its three purchase buttons and close button.");
                UpgradePanelSnapshot panelSnapshot = CaptureUpgradePanelSnapshot(panelTexts, panelButtons);

                AssertUpgradeText(panelTexts, "PermanentUpgradeTitle", "PERMANENT UPGRADES · RENOWN");
                AssertUpgradeText(panelTexts, "PermanentUpgradeBalance", "AVAILABLE RENOWN · 0");
                AssertUpgradeText(panelTexts, "PermanentHealthUpgradeButtonLabel",
                    "MAXIMUM HEALTH · +5% · 2/2 · COMPLETE");
                AssertUpgradeText(panelTexts, "PermanentEnergyUpgradeButtonLabel",
                    "MAXIMUM ENERGY · +5% · 2/2 · COMPLETE");
                AssertUpgradeText(panelTexts, "PermanentPotionUpgradeButtonLabel",
                    "STARTING POTION · +1 · 1/1 · COMPLETE");
                AssertUpgradeText(panelTexts, "ClosePermanentUpgradesButtonLabel", "BACK");
                AssertUpgradeText(panelTexts, "PermanentUpgradeStatus", string.Empty);
                Assert.That(panelButtons[0].interactable, Is.False);
                Assert.That(panelButtons[1].interactable, Is.False);
                Assert.That(panelButtons[2].interactable, Is.False);

                string profileBefore = JsonUtility.ToJson(app.Profile, false);
                int profileWritesBefore = store.ProfileWriteAttempts;
                int settingsWritesBefore = store.SettingsWriteAttempts;
                Task<SaveStorageSnapshot> profileBeforeTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileBeforeTask, "read the fully-upgraded Profile before locale navigation");
                SaveStorageSnapshot profileBeforeSnapshot = profileBeforeTask.GetAwaiter().GetResult();

                OpenGuildLanguageList();
                yield return SwitchGuildLanguage(app, store, "es", settingsWritesBefore + 1);
                Assert.That(panelTexts[2].gameObject.activeInHierarchy, Is.False,
                    "Guild hides the upgrade panel while the language selector is used.");
                CloseGuildLanguageList();
                OpenUpgradePanel();

                Text[] reopenedTexts = FindUpgradeTexts();
                Button[] reopenedButtons = FindUpgradeButtons();
                AssertUpgradePanelSnapshotUnchanged(panelSnapshot, reopenedTexts, reopenedButtons,
                    "Spanish reopen after Guild language change");
                UpgradeTextMetric[] spanishMetrics = RecordUpgradePanelTextMetrics(reopenedTexts, "es", "fully-upgraded-es");
                AssertUpgradeTextFits(spanishMetrics);

                AssertUpgradeText(reopenedTexts, "PermanentUpgradeTitle", "MEJORAS PERMANENTES · RENOMBRE");
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeBalance", "RENOMBRE DISPONIBLE · 0");
                AssertUpgradeText(reopenedTexts, "PermanentHealthUpgradeButtonLabel",
                    "SALUD MÁXIMA · +5 % · 2/2 · COMPLETA");
                AssertUpgradeText(reopenedTexts, "PermanentEnergyUpgradeButtonLabel",
                    "ENERGÍA MÁXIMA · +5 % · 2/2 · COMPLETA");
                AssertUpgradeText(reopenedTexts, "PermanentPotionUpgradeButtonLabel",
                    "POCIÓN INICIAL · +1 · 1/1 · COMPLETA");
                AssertUpgradeText(reopenedTexts, "ClosePermanentUpgradesButtonLabel", "VOLVER");
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeStatus", string.Empty);
                Assert.That(app.Settings.languageId, Is.EqualTo("es"));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(profileWritesBefore),
                    "Opening, closing and changing language must not write Profile.");
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileBefore));

                Task<SaveStorageSnapshot> profileAfterTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileAfterTask, "read the fully-upgraded Profile after locale navigation");
                SaveStorageSnapshot profileAfterSnapshot = profileAfterTask.GetAwaiter().GetResult();
                Assert.That(profileAfterSnapshot.PrimaryJson, Is.EqualTo(profileBeforeSnapshot.PrimaryJson));
                Assert.That(profileAfterSnapshot.BackupJson, Is.EqualTo(profileBeforeSnapshot.BackupJson));
            }
            finally
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                app = ApplicationBootstrapper.Instance;
                if (app != null) Object.Destroy(app.gameObject);
                Time.timeScale = 1f;
                DeleteSaveDirectory(saveDirectory);
            }
        }

        [UnityTest]
        public IEnumerator UpgradePendingPurchaseChangesLanguageAndRetriesTheExactPayload()
        {
            string saveDirectory = NewSaveDirectory();
            var backingStore = new FileSaveStore(saveDirectory, null);
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper app = null;
            Time.timeScale = 1f;
            try
            {
                ProfileSaveData seededProfile = ProfileSaveData.CreateNewProfile();
                seededProfile.availableRenown = 510;
                yield return SeedProfileAndSettings(backingStore, seededProfile, "es");
                var store = new TrackingSaveStore(backingStore);
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                yield return LoadBootstrappedApplication(value => app = value);
                Assert.That(app != null && app.SaveDataReady && app.CanWriteProfile, Is.True,
                    app == null ? "Application profile did not load." : app.ProfileLoadMessage);
                Assert.That(app.Settings.languageId, Is.EqualTo("es"));
                Assert.That(app.HasActiveExpedition, Is.False);
                Assert.That(app.Profile.availableRenown, Is.EqualTo(510));
                Assert.That(app.Profile.healthUpgradeLevel, Is.Zero);
                Assert.That(app.Profile.progressionPurchaseTransactions, Is.Empty);

                yield return LoadHubScene();
                OpenUpgradePanel();
                Text[] panelTexts = FindUpgradeTexts();
                Button[] panelButtons = FindUpgradeButtons();
                UpgradeTextMetric[] initialMetrics = RecordUpgradePanelTextMetrics(panelTexts, "es", "purchase-ready-es");
                AssertUpgradeTextFits(initialMetrics);
                Assert.That(panelButtons.All(button => button != null), Is.True,
                    "The seven text panel needs its three purchase buttons and close button.");
                UpgradePanelSnapshot panelSnapshot = CaptureUpgradePanelSnapshot(panelTexts, panelButtons);

                AssertUpgradeText(panelTexts, "PermanentUpgradeTitle", "MEJORAS PERMANENTES · RENOMBRE");
                AssertUpgradeText(panelTexts, "PermanentUpgradeBalance", "RENOMBRE DISPONIBLE · 510");
                AssertUpgradeText(panelTexts, "PermanentHealthUpgradeButtonLabel",
                    "SALUD MÁXIMA · +5 % · 0/2 · 60 RENOMBRE");
                AssertUpgradeText(panelTexts, "PermanentEnergyUpgradeButtonLabel",
                    "ENERGÍA MÁXIMA · +5 % · 0/2 · 60 RENOMBRE");
                AssertUpgradeText(panelTexts, "PermanentPotionUpgradeButtonLabel",
                    "POCIÓN INICIAL · +1 · 0/1 · 150 RENOMBRE");
                Assert.That(panelButtons[0].interactable, Is.True);
                Assert.That(panelButtons[1].interactable, Is.True);
                Assert.That(panelButtons[2].interactable, Is.True);

                Task<SaveStorageSnapshot> profileBeforeTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileBeforeTask, "read Profile before the real upgrade-button purchase");
                SaveStorageSnapshot profileBeforeSnapshot = profileBeforeTask.GetAwaiter().GetResult();
                string profileBefore = JsonUtility.ToJson(app.Profile, false);
                int settingsWritesBefore = store.SettingsWriteAttempts;

                store.FailNextProfileWrite();
                SimulatePointerClick(panelButtons[0]);
                yield return WaitForPendingUpgrade(app, store, expectedWrites: 1);
                Assert.That(store.FailedProfileWriteCount, Is.EqualTo(1));
                Assert.That(app.HasPendingPermanentUpgradePurchase, Is.True);
                Assert.That(app.PendingPermanentUpgradeKind, Is.EqualTo(PermanentUpgradeKind.MaximumHealth));
                Assert.That(app.Profile.availableRenown, Is.EqualTo(510));
                Assert.That(app.Profile.healthUpgradeLevel, Is.Zero);
                Assert.That(app.Profile.progressionPurchaseTransactions, Is.Empty);
                Assert.That(store.ProfileWritePayloads.Count, Is.EqualTo(1));
                string failedPayload = store.FailedProfilePayload;
                Assert.That(failedPayload, Is.Not.Empty);

                UpgradeTextMetric[] pendingSpanishMetrics = RecordUpgradePanelTextMetrics(panelTexts, "es", "pending-es");
                AssertUpgradeTextFits(pendingSpanishMetrics);
                AssertUpgradeText(panelTexts, "PermanentUpgradeStatus",
                    "Guardado pendiente. Pulsa la mejora marcada para reintentar.");
                AssertUpgradeText(panelTexts, "PermanentHealthUpgradeButtonLabel",
                    "REINTENTAR · SALUD MÁXIMA · +5 % · 0/2 · 60 RENOMBRE");
                Assert.That(panelButtons[0].interactable, Is.True);
                Assert.That(panelButtons[1].interactable, Is.False);
                Assert.That(panelButtons[2].interactable, Is.False);
                AssertUpgradePanelSnapshotUnchanged(panelSnapshot, panelTexts, panelButtons,
                    "Spanish pending state after the failed Profile write", compareInteractability: false);
                UpgradePanelSnapshot pendingSnapshot = CaptureUpgradePanelSnapshot(panelTexts, panelButtons);
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileBefore));

                Task<SaveStorageSnapshot> failedProfileTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(failedProfileTask, "verify the failed write preserved both Profile files");
                SaveStorageSnapshot failedProfileSnapshot = failedProfileTask.GetAwaiter().GetResult();
                Assert.That(failedProfileSnapshot.PrimaryJson, Is.EqualTo(profileBeforeSnapshot.PrimaryJson));
                Assert.That(failedProfileSnapshot.BackupJson, Is.EqualTo(profileBeforeSnapshot.BackupJson));

                OpenGuildLanguageList();
                yield return SwitchGuildLanguage(app, store, "en", settingsWritesBefore + 1);
                Assert.That(panelTexts[2].gameObject.activeInHierarchy, Is.False,
                    "The captured upgrade Text remains inactive while Guild changes language.");
                UpgradeTextMetric[] pendingEnglishMetrics = RecordUpgradePanelTextMetrics(panelTexts, "en", "pending-en-hidden");
                AssertUpgradeTextFits(pendingEnglishMetrics);
                AssertUpgradePanelSnapshotUnchanged(pendingSnapshot, panelTexts, panelButtons,
                    "English pending state while the panel is inactive");
                AssertUpgradeText(panelTexts, "PermanentUpgradeTitle", "PERMANENT UPGRADES · RENOWN");
                AssertUpgradeText(panelTexts, "PermanentUpgradeBalance", "AVAILABLE RENOWN · 510");
                AssertUpgradeText(panelTexts, "PermanentHealthUpgradeButtonLabel",
                    "RETRY · MAXIMUM HEALTH · +5% · 0/2 · 60 RENOWN");
                AssertUpgradeText(panelTexts, "PermanentEnergyUpgradeButtonLabel",
                    "MAXIMUM ENERGY · +5% · 0/2 · 60 RENOWN");
                AssertUpgradeText(panelTexts, "PermanentPotionUpgradeButtonLabel",
                    "STARTING POTION · +1 · 0/1 · 150 RENOWN");
                AssertUpgradeText(panelTexts, "ClosePermanentUpgradesButtonLabel", "BACK");
                AssertUpgradeText(panelTexts, "PermanentUpgradeStatus",
                    "Save pending. Select the marked upgrade to retry.");
                Assert.That(app.HasPendingPermanentUpgradePurchase, Is.True);
                Assert.That(app.PendingPermanentUpgradeKind, Is.EqualTo(PermanentUpgradeKind.MaximumHealth));
                Assert.That(app.Profile.availableRenown, Is.EqualTo(510));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(1));

                CloseGuildLanguageList();
                OpenUpgradePanel();
                Text[] reopenedTexts = FindUpgradeTexts();
                Button[] reopenedButtons = FindUpgradeButtons();
                AssertUpgradePanelSnapshotUnchanged(pendingSnapshot, reopenedTexts, reopenedButtons,
                    "English retry panel reopen");
                UpgradeTextMetric[] retryEnglishMetrics = RecordUpgradePanelTextMetrics(reopenedTexts, "en", "retry-en");
                AssertUpgradeTextFits(retryEnglishMetrics);
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeStatus", string.Empty);
                AssertUpgradeText(reopenedTexts, "PermanentHealthUpgradeButtonLabel",
                    "RETRY · MAXIMUM HEALTH · +5% · 0/2 · 60 RENOWN");
                Assert.That(reopenedButtons[0].interactable, Is.True);
                Assert.That(app.HasPendingPermanentUpgradePurchase, Is.True);
                Assert.That(app.PendingPermanentUpgradeKind, Is.EqualTo(PermanentUpgradeKind.MaximumHealth));

                SimulatePointerClick(reopenedButtons[0]);
                yield return WaitForCommittedUpgrade(app, store, expectedWrites: 2);
                Assert.That(app.HasPendingPermanentUpgradePurchase, Is.False);
                Assert.That(app.Profile.availableRenown, Is.EqualTo(450));
                Assert.That(app.Profile.healthUpgradeLevel, Is.EqualTo(1));
                Assert.That(app.Profile.energyUpgradeLevel, Is.Zero);
                Assert.That(app.Profile.additionalPotionUpgradeLevel, Is.Zero);
                Assert.That(app.Profile.progressionPurchaseTransactions.Length, Is.EqualTo(1));
                Assert.That(app.Profile.progressionPurchaseTransactions[0].kind,
                    Is.EqualTo((int)PermanentUpgradeKind.MaximumHealth));
                Assert.That(app.Profile.progressionPurchaseTransactions[0].cost, Is.EqualTo(60));
                Assert.That(store.FailedProfileWriteCount, Is.EqualTo(1));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(2));
                Assert.That(store.ProfileWritePayloads.Count, Is.EqualTo(2));
                Assert.That(store.ProfileWritePayloads[0], Is.EqualTo(failedPayload),
                    "The captured first Profile write must be the failed purchase payload.");
                Assert.That(store.ProfileWritePayloads[1], Is.EqualTo(failedPayload),
                    "Retry must write the byte-for-byte same pending Profile payload.");
                AssertUpgradePanelSnapshotUnchanged(pendingSnapshot, reopenedTexts, reopenedButtons,
                    "English committed state after the durable retry", compareInteractability: false);

                UpgradeTextMetric[] committedEnglishMetrics = RecordUpgradePanelTextMetrics(reopenedTexts, "en", "committed-en");
                AssertUpgradeTextFits(committedEnglishMetrics);
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeStatus",
                    "Upgrade saved. Available Renown: 450.");
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeBalance", "AVAILABLE RENOWN · 450");
                AssertUpgradeText(reopenedTexts, "PermanentHealthUpgradeButtonLabel",
                    "MAXIMUM HEALTH · +5% · 1/2 · 120 RENOWN");
                Assert.That(app.HasActiveExpedition, Is.False);

                UpgradePanelSnapshot committedSnapshot = CaptureUpgradePanelSnapshot(reopenedTexts, reopenedButtons);
                string profileAfterPurchase = JsonUtility.ToJson(app.Profile, false);
                int profileWritesAfterPurchase = store.ProfileWriteAttempts;
                OpenGuildLanguageList();
                yield return SwitchGuildLanguage(app, store, "es", settingsWritesBefore + 2);
                UpgradeTextMetric[] committedSpanishMetrics = RecordUpgradePanelTextMetrics(reopenedTexts, "es", "committed-es-hidden");
                AssertUpgradeTextFits(committedSpanishMetrics);
                AssertUpgradePanelSnapshotUnchanged(committedSnapshot, reopenedTexts, reopenedButtons,
                    "Spanish committed status while the panel is inactive");
                Assert.That(reopenedTexts[2].gameObject.activeInHierarchy, Is.False);
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeStatus",
                    "Mejora guardada. Renombre disponible: 450.");
                AssertUpgradeText(reopenedTexts, "PermanentUpgradeBalance", "RENOMBRE DISPONIBLE · 450");
                Assert.That(app.Settings.languageId, Is.EqualTo("es"));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(profileWritesAfterPurchase));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(2));
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileAfterPurchase));

                Task<SaveStorageSnapshot> profileAfterTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileAfterTask, "verify language changes did not rewrite Profile");
                SaveStorageSnapshot profileAfterSnapshot = profileAfterTask.GetAwaiter().GetResult();
                Assert.That(profileAfterSnapshot.PrimaryJson, Is.EqualTo(store.ProfileWritePayloads[1]));
            }
            finally
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                app = ApplicationBootstrapper.Instance;
                if (app != null) Object.Destroy(app.gameObject);
                Time.timeScale = 1f;
                DeleteSaveDirectory(saveDirectory);
            }
        }

        private static IEnumerator SeedProfileAndSettings(ISaveStore store, ProfileSaveData profile, string languageId)
        {
            var repository = new SaveRepository(store);
            Task<SaveWriteResult> profileTask = repository.SaveProfileAsync(profile);
            yield return WaitForTask(profileTask, "seed Profile through validated SaveRepository");
            Assert.That(profileTask.GetAwaiter().GetResult().Succeeded, Is.True,
                profileTask.GetAwaiter().GetResult().Message);

            Task<SaveWriteResult> settingsTask = repository.SaveSettingsAsync(new GameSettingsSaveData
            {
                languageId = languageId
            });
            yield return WaitForTask(settingsTask, "seed Settings through SaveRepository before bootstrap");
            Assert.That(settingsTask.GetAwaiter().GetResult().Succeeded, Is.True,
                settingsTask.GetAwaiter().GetResult().Message);
        }

        private static IEnumerator LoadBootstrappedApplication(Action<ApplicationBootstrapper> onReady)
        {
            if (ApplicationBootstrapper.Instance != null) Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                 !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                yield return null;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            Assert.That(app != null && app.SaveDataReady, Is.True,
                app == null ? "Application bootstrap did not finish." : app.ProfileLoadMessage);
            onReady?.Invoke(app);
        }

        private static IEnumerator LoadHubScene()
        {
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            for (int frame = 0; frame < 60 && (SceneManager.GetActiveScene().name != "Hub" ||
                 Object.FindAnyObjectByType<HubPreparationController>() == null); frame++)
                yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
            Assert.That(Object.FindAnyObjectByType<HubPreparationController>(), Is.Not.Null,
                "The Hub scene must create its production preparation controller.");
        }

        private static void OpenUpgradePanel()
        {
            Button openUpgrades = FindButton("OpenPermanentUpgradesButton");
            Assert.That(openUpgrades, Is.Not.Null, "Hub is missing OpenPermanentUpgradesButton.");
            SimulatePointerClick(openUpgrades);
        }

        private static void OpenGuildLanguageList()
        {
            Button openGuild = FindButton("OpenGuildIntroButton");
            Assert.That(openGuild, Is.Not.Null, "Hub is missing its production Guild button.");
            SimulatePointerClick(openGuild);
            Assert.That(FindButton("GuildIntroListLanguageButton"), Is.Not.Null,
                "Guild must expose its existing language control.");
        }

        private static void CloseGuildLanguageList()
        {
            Button closeGuild = FindButton("GuildIntroListCloseButton");
            Assert.That(closeGuild, Is.Not.Null, "Guild is missing its production close button.");
            SimulatePointerClick(closeGuild);
        }

        private static IEnumerator SwitchGuildLanguage(ApplicationBootstrapper app, TrackingSaveStore store,
            string languageId, int expectedSettingsWrites)
        {
            Button languageButton = FindButton("GuildIntroListLanguageButton");
            Assert.That(languageButton, Is.Not.Null);
            SimulateSubmit(languageButton);
            string expectedChoice = languageId == "en" ? "Spanish" : "Inglés";
            Text languageLabel = languageButton.GetComponentInChildren<Text>(true);
            for (int frame = 0; frame < 120 && (app.Settings.languageId != languageId ||
                 store.SettingsWriteAttempts < expectedSettingsWrites || languageLabel == null ||
                 languageLabel.text != expectedChoice); frame++)
                yield return null;
            Assert.That(app.Settings.languageId, Is.EqualTo(languageId));
            Assert.That(store.SettingsWriteAttempts, Is.GreaterThanOrEqualTo(expectedSettingsWrites));
            Assert.That(languageLabel, Is.Not.Null);
            Assert.That(languageLabel.text, Is.EqualTo(expectedChoice));
        }

        private static IEnumerator WaitForPendingUpgrade(ApplicationBootstrapper app, TrackingSaveStore store,
            int expectedWrites)
        {
            for (int frame = 0; frame < 120 && (app == null || !app.HasPendingPermanentUpgradePurchase ||
                 store.ProfileWriteAttempts < expectedWrites); frame++)
                yield return null;
            Assert.That(app != null && app.HasPendingPermanentUpgradePurchase, Is.True,
                "The first failed durable purchase write must leave the purchase pending.");
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(expectedWrites));
        }

        private static IEnumerator WaitForCommittedUpgrade(ApplicationBootstrapper app, TrackingSaveStore store,
            int expectedWrites)
        {
            for (int frame = 0; frame < 120 && (app == null || app.HasPendingPermanentUpgradePurchase ||
                 store.ProfileWriteAttempts < expectedWrites); frame++)
                yield return null;
            Assert.That(app != null && !app.HasPendingPermanentUpgradePurchase, Is.True,
                "The exact retry must clear pending state only after durable save.");
            Assert.That(store.ProfileWriteAttempts, Is.EqualTo(expectedWrites));
        }

        private static IEnumerator WaitForTask(Task task, string operation)
        {
            for (int frame = 0; frame < 120 && !task.IsCompleted; frame++) yield return null;
            Assert.That(task.IsCompleted, Is.True, operation + " did not finish within the bounded wait.");
            if (task.IsFaulted) throw task.Exception;
        }

        private static ProfileSaveData CreateFullyUpgradedProfile()
        {
            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            profile.availableRenown = 0;
            profile.healthUpgradeLevel = 2;
            profile.energyUpgradeLevel = 2;
            profile.additionalPotionUpgradeLevel = 1;
            profile.progressionPurchaseTransactions = new[]
            {
                CreateReceipt(PermanentUpgradeKind.MaximumHealth, 60),
                CreateReceipt(PermanentUpgradeKind.MaximumHealth, 120),
                CreateReceipt(PermanentUpgradeKind.MaximumEnergy, 60),
                CreateReceipt(PermanentUpgradeKind.MaximumEnergy, 120),
                CreateReceipt(PermanentUpgradeKind.AdditionalHealthPotion, 150)
            };
            return profile;
        }

        private static PermanentUpgradePurchaseSaveData CreateReceipt(PermanentUpgradeKind kind, int cost)
        {
            return new PermanentUpgradePurchaseSaveData
            {
                transactionId = TransactionId.New().ToString(),
                kind = (int)kind,
                cost = cost
            };
        }

        private static Text[] FindUpgradeTexts()
        {
            Text[] sceneTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return UpgradeTextNames.Select(name => sceneTexts.FirstOrDefault(text => text != null && text.name == name))
                .ToArray();
        }

        private static Button[] FindUpgradeButtons()
        {
            Button[] sceneButtons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return UpgradeButtonNames.Select(name => sceneButtons.FirstOrDefault(button => button != null && button.name == name))
                .ToArray();
        }

        private static Text FindText(string name)
        {
            return Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(text => text != null && text.name == name);
        }

        private static Button FindButton(string name)
        {
            return Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(button => button != null && button.name == name);
        }

        private static UpgradeTextMetric[] RecordUpgradePanelTextMetrics(Text[] texts, string language, string checkpoint)
        {
            Canvas.ForceUpdateCanvases();
            var records = new UpgradeTextMetric[UpgradeTextNames.Length];
            for (int index = 0; index < records.Length; index++)
            {
                Text text = texts != null && index < texts.Length ? texts[index] : null;
                records[index] = CaptureUpgradeTextMetric(text, UpgradeTextNames[index], language, checkpoint);
            }

            foreach (UpgradeTextMetric record in records)
                Debug.Log("[E10.HUB.UPGRADES.TEXT-METRIC] " + JsonUtility.ToJson(record));
            return records;
        }

        private static UpgradeTextMetric CaptureUpgradeTextMetric(Text text, string fallbackName,
            string language, string checkpoint)
        {
            var metric = new UpgradeTextMetric
            {
                name = text == null ? fallbackName : text.name,
                checkpoint = checkpoint,
                language = language ?? string.Empty,
                sourceComponent = "UnityEngine.UI.Text",
                content = text == null ? string.Empty : text.text ?? string.Empty,
                fontName = string.Empty,
                fontSource = string.Empty,
                preferredGenerator = "TextGenerator.GetPreferredWidth/GetPreferredHeight",
                visibilityGenerator = "TextGenerator.characterCountVisible",
                horizontalOverflow = string.Empty,
                verticalOverflow = string.Empty,
                fontSize = -1,
                fontInstanceId = -1,
                activeInHierarchy = false,
                available = false,
                anchoredX = -1f,
                anchoredY = -1f,
                rectWidth = -1f,
                rectHeight = -1f,
                pixelsPerUnit = -1f,
                preferredWidth = -1f,
                preferredHeight = -1f,
                canvasMinX = -1f,
                canvasMinY = -1f,
                canvasMaxX = -1f,
                canvasMaxY = -1f,
                canvasPixelX = -1f,
                canvasPixelY = -1f,
                canvasPixelWidth = -1f,
                canvasPixelHeight = -1f,
                canvasScaleFactor = -1f,
                referenceWidth = -1f,
                referenceHeight = -1f,
                canvasRectWidth = -1f,
                canvasRectHeight = -1f,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                visibleCount = -1,
                expectedVisibleCount = -1
            };
            if (text == null || text.rectTransform == null) return metric;

            RectTransform rect = text.rectTransform;
            string content = text.text ?? string.Empty;
            float pixelsPerUnit = text.pixelsPerUnit;
            metric.available = true;
            metric.activeInHierarchy = text.gameObject.activeInHierarchy;
            metric.fontName = text.font == null ? string.Empty : text.font.name;
            metric.fontSource = text.font == null ? string.Empty : "Text.font:" + text.font.name;
            metric.fontSize = text.fontSize;
            metric.fontInstanceId = text.font == null ? -1 : text.font.GetInstanceID();
            metric.horizontalOverflow = text.horizontalOverflow.ToString();
            metric.verticalOverflow = text.verticalOverflow.ToString();
            metric.anchoredX = FiniteOrSentinel(rect.anchoredPosition.x);
            metric.anchoredY = FiniteOrSentinel(rect.anchoredPosition.y);
            metric.rectWidth = FiniteOrSentinel(rect.rect.width);
            metric.rectHeight = FiniteOrSentinel(rect.rect.height);
            metric.pixelsPerUnit = FiniteOrSentinel(pixelsPerUnit);
            metric.expectedVisibleCount = content.Count(character => character != '\n' && character != '\r');

            Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.name == "HubPreparationCanvas");
            RectTransform canvasRect = canvas == null ? null : canvas.transform as RectTransform;
            if (canvas != null)
            {
                metric.canvasPixelX = FiniteOrSentinel(canvas.pixelRect.x);
                metric.canvasPixelY = FiniteOrSentinel(canvas.pixelRect.y);
                metric.canvasPixelWidth = FiniteOrSentinel(canvas.pixelRect.width);
                metric.canvasPixelHeight = FiniteOrSentinel(canvas.pixelRect.height);
                metric.canvasScaleFactor = FiniteOrSentinel(canvas.scaleFactor);
            }
            CanvasScaler scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                metric.referenceWidth = FiniteOrSentinel(scaler.referenceResolution.x);
                metric.referenceHeight = FiniteOrSentinel(scaler.referenceResolution.y);
            }
            if (canvasRect != null)
            {
                metric.canvasRectWidth = FiniteOrSentinel(canvasRect.rect.width);
                metric.canvasRectHeight = FiniteOrSentinel(canvasRect.rect.height);
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Vector3 first = canvasRect.InverseTransformPoint(corners[0]);
                Vector3 second = canvasRect.InverseTransformPoint(corners[1]);
                Vector3 third = canvasRect.InverseTransformPoint(corners[2]);
                Vector3 fourth = canvasRect.InverseTransformPoint(corners[3]);
                metric.canvasMinX = FiniteOrSentinel(Mathf.Min(first.x, second.x, third.x, fourth.x));
                metric.canvasMinY = FiniteOrSentinel(Mathf.Min(first.y, second.y, third.y, fourth.y));
                metric.canvasMaxX = FiniteOrSentinel(Mathf.Max(first.x, second.x, third.x, fourth.x));
                metric.canvasMaxY = FiniteOrSentinel(Mathf.Max(first.y, second.y, third.y, fourth.y));
            }

            TextGenerationSettings settings = text.GetGenerationSettings(rect.rect.size);
            if (IsFinite(pixelsPerUnit) && pixelsPerUnit > 0f)
            {
                var preferredWidthGenerator = new TextGenerator();
                preferredWidthGenerator.Populate(content, settings);
                metric.preferredWidth = FiniteOrSentinel(
                    preferredWidthGenerator.GetPreferredWidth(content, settings) / pixelsPerUnit);

                var preferredHeightGenerator = new TextGenerator();
                preferredHeightGenerator.Populate(content, settings);
                metric.preferredHeight = FiniteOrSentinel(
                    preferredHeightGenerator.GetPreferredHeight(content, settings) / pixelsPerUnit);
            }

            var visibilityGenerator = new TextGenerator();
            visibilityGenerator.Populate(content, settings);
            metric.visibleCount = visibilityGenerator.characterCountVisible;
            if (metric.visibleCount < 0) metric.visibleCount = -1;
            return metric;
        }

        private static void AssertUpgradeTextFits(UpgradeTextMetric[] records)
        {
            Assert.That(records, Has.Length.EqualTo(UpgradeTextNames.Length),
                "Every actual panel Text must have a metric record before fit assertions.");
            foreach (UpgradeTextMetric metric in records)
            {
                string context = metric.name + " / " + metric.checkpoint;
                Assert.That(metric.available, Is.True, context + " must be an actual panel Text.");
                Assert.That(metric.fontName, Is.Not.Empty, context + " font source must be present.");
                Assert.That(metric.fontSource, Is.Not.Empty, context + " Text.font binding must be recorded.");
                Assert.That(metric.fontSize, Is.GreaterThan(0), context + " must retain its source font size.");
                Assert.That(metric.fontInstanceId, Is.Not.EqualTo(0), context + " font identity must be captured.");
                Assert.That(IsFinite(metric.rectWidth) && metric.rectWidth > 0f, Is.True,
                    context + " rect width must be finite and positive.");
                Assert.That(IsFinite(metric.rectHeight) && metric.rectHeight > 0f, Is.True,
                    context + " rect height must be finite and positive.");
                Assert.That(IsFinite(metric.pixelsPerUnit) && metric.pixelsPerUnit > 0f, Is.True,
                    context + " real Text pixels-per-unit must be finite and positive.");
                Assert.That(IsFinite(metric.preferredWidth) && metric.preferredWidth >= 0f, Is.True,
                    context + " preferred width must be finite.");
                Assert.That(IsFinite(metric.preferredHeight) && metric.preferredHeight >= 0f, Is.True,
                    context + " preferred height must be finite.");
                Assert.That(metric.expectedVisibleCount,
                    Is.EqualTo(metric.content.Count(character => character != '\n' && character != '\r')),
                    context + " full-character count must use the actual string.");
                Assert.That(metric.visibleCount, Is.GreaterThanOrEqualTo(metric.expectedVisibleCount),
                    context + " must display every authored character with the existing overflow mode.");
                if (!string.Equals(metric.horizontalOverflow, HorizontalWrapMode.Wrap.ToString(), StringComparison.Ordinal))
                    Assert.That(metric.preferredWidth, Is.LessThanOrEqualTo(metric.rectWidth + 0.5f),
                        context + " preferred width must fit the unchanged rect within 0.5 units.");
                if (string.Equals(metric.verticalOverflow, VerticalWrapMode.Truncate.ToString(), StringComparison.Ordinal))
                    Assert.That(metric.preferredHeight, Is.LessThanOrEqualTo(metric.rectHeight + 0.5f),
                        context + " preferred height must fit the unchanged rect within 0.5 units.");
            }
        }

        private static void AssertUpgradeText(Text[] texts, string name, string expected)
        {
            int index = Array.IndexOf(UpgradeTextNames, name);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Unknown permanent-upgrade Text name.");
            Assert.That(texts[index], Is.Not.Null, name + " Text must be present.");
            Assert.That(texts[index].text, Is.EqualTo(expected), name + " literal.");
        }

        private static void SimulatePointerClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True, button.gameObject.name + " should be interactable.");
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.gameObject.name + " should be active.");
            Assert.That(EventSystem.current, Is.Not.Null, "Hub should provide an EventSystem.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void SimulateSubmit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True, button.gameObject.name + " should be interactable.");
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.gameObject.name + " should be active.");
            Assert.That(EventSystem.current, Is.Not.Null, "Hub should provide an EventSystem.");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        private static UpgradePanelSnapshot CaptureUpgradePanelSnapshot(Text[] texts, Button[] buttons)
        {
            return new UpgradePanelSnapshot
            {
                texts = texts.ToArray(),
                appearances = texts.Select(CaptureTextAppearance).ToArray(),
                buttons = buttons.ToArray(),
                interactable = buttons.Select(button => button != null && button.interactable).ToArray()
            };
        }

        private static UpgradeTextAppearance CaptureTextAppearance(Text text)
        {
            RectTransform rect = text == null ? null : text.rectTransform;
            return new UpgradeTextAppearance
            {
                rect = rect,
                font = text == null ? null : text.font,
                fontSize = text == null ? -1 : text.fontSize,
                fontStyle = text == null ? FontStyle.Normal : text.fontStyle,
                alignment = text == null ? TextAnchor.UpperLeft : text.alignment,
                horizontalOverflow = text == null ? HorizontalWrapMode.Wrap : text.horizontalOverflow,
                verticalOverflow = text == null ? VerticalWrapMode.Truncate : text.verticalOverflow,
                resizeTextForBestFit = text != null && text.resizeTextForBestFit,
                resizeTextMinSize = text == null ? -1 : text.resizeTextMinSize,
                resizeTextMaxSize = text == null ? -1 : text.resizeTextMaxSize,
                lineSpacing = text == null ? -1f : text.lineSpacing,
                supportRichText = text != null && text.supportRichText,
                anchorMin = rect == null ? Vector2.zero : rect.anchorMin,
                anchorMax = rect == null ? Vector2.zero : rect.anchorMax,
                pivot = rect == null ? Vector2.zero : rect.pivot,
                anchoredPosition = rect == null ? Vector2.zero : rect.anchoredPosition,
                sizeDelta = rect == null ? Vector2.zero : rect.sizeDelta,
                offsetMin = rect == null ? Vector2.zero : rect.offsetMin,
                offsetMax = rect == null ? Vector2.zero : rect.offsetMax
            };
        }

        private static void AssertUpgradePanelSnapshotUnchanged(UpgradePanelSnapshot snapshot, Text[] texts,
            Button[] buttons, string checkpoint, bool compareInteractability = true)
        {
            Assert.That(texts, Has.Length.EqualTo(snapshot.texts.Length), checkpoint + " panel Text count.");
            Assert.That(buttons, Has.Length.EqualTo(snapshot.buttons.Length), checkpoint + " panel Button count.");
            for (int index = 0; index < texts.Length; index++)
            {
                Text text = texts[index];
                UpgradeTextAppearance appearance = snapshot.appearances[index];
                Assert.That(text, Is.SameAs(snapshot.texts[index]), checkpoint + " preserves " + UpgradeTextNames[index] + " instance.");
                Assert.That(text.rectTransform, Is.SameAs(appearance.rect), checkpoint + " preserves rect instance.");
                Assert.That(text.font, Is.SameAs(appearance.font), checkpoint + " preserves font asset.");
                Assert.That(text.fontSize, Is.EqualTo(appearance.fontSize), checkpoint + " preserves font size.");
                Assert.That(text.fontStyle, Is.EqualTo(appearance.fontStyle), checkpoint + " preserves font style.");
                Assert.That(text.alignment, Is.EqualTo(appearance.alignment), checkpoint + " preserves alignment.");
                Assert.That(text.horizontalOverflow, Is.EqualTo(appearance.horizontalOverflow), checkpoint + " preserves horizontal overflow.");
                Assert.That(text.verticalOverflow, Is.EqualTo(appearance.verticalOverflow), checkpoint + " preserves vertical overflow.");
                Assert.That(text.resizeTextForBestFit, Is.EqualTo(appearance.resizeTextForBestFit), checkpoint + " preserves Best Fit.");
                Assert.That(text.resizeTextMinSize, Is.EqualTo(appearance.resizeTextMinSize), checkpoint + " preserves minimum font size.");
                Assert.That(text.resizeTextMaxSize, Is.EqualTo(appearance.resizeTextMaxSize), checkpoint + " preserves maximum font size.");
                Assert.That(text.lineSpacing, Is.EqualTo(appearance.lineSpacing), checkpoint + " preserves line spacing.");
                Assert.That(text.supportRichText, Is.EqualTo(appearance.supportRichText), checkpoint + " preserves rich-text setting.");
                RectTransform rect = text.rectTransform;
                Assert.That(rect.anchorMin, Is.EqualTo(appearance.anchorMin), checkpoint + " preserves anchorMin.");
                Assert.That(rect.anchorMax, Is.EqualTo(appearance.anchorMax), checkpoint + " preserves anchorMax.");
                Assert.That(rect.pivot, Is.EqualTo(appearance.pivot), checkpoint + " preserves pivot.");
                Assert.That(rect.anchoredPosition, Is.EqualTo(appearance.anchoredPosition), checkpoint + " preserves position.");
                Assert.That(rect.sizeDelta, Is.EqualTo(appearance.sizeDelta), checkpoint + " preserves size.");
                Assert.That(rect.offsetMin, Is.EqualTo(appearance.offsetMin), checkpoint + " preserves offsetMin.");
                Assert.That(rect.offsetMax, Is.EqualTo(appearance.offsetMax), checkpoint + " preserves offsetMax.");
            }

            for (int index = 0; index < buttons.Length; index++)
            {
                Assert.That(buttons[index], Is.SameAs(snapshot.buttons[index]),
                    checkpoint + " preserves " + UpgradeButtonNames[index] + " instance.");
                if (compareInteractability)
                    Assert.That(buttons[index].interactable, Is.EqualTo(snapshot.interactable[index]),
                        checkpoint + " preserves " + UpgradeButtonNames[index] + " interactability.");
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float FiniteOrSentinel(float value) => IsFinite(value) ? value : -1f;

        private static string NewSaveDirectory()
        {
            return Path.Combine(Path.GetTempPath(), "LuckboundE10HubUpgradesLocalization", Guid.NewGuid().ToString("N"));
        }

        private static void DeleteSaveDirectory(string saveDirectory)
        {
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
        }

        [Serializable]
        private sealed class UpgradeTextMetric
        {
            public string name;
            public string checkpoint;
            public string language;
            public string sourceComponent;
            public string content;
            public string fontName;
            public string fontSource;
            public string preferredGenerator;
            public string visibilityGenerator;
            public string horizontalOverflow;
            public string verticalOverflow;
            public int fontSize;
            public int fontInstanceId;
            public bool activeInHierarchy;
            public bool available;
            public int screenWidth;
            public int screenHeight;
            public float anchoredX;
            public float anchoredY;
            public float rectWidth;
            public float rectHeight;
            public float pixelsPerUnit;
            public float preferredWidth;
            public float preferredHeight;
            public float canvasMinX;
            public float canvasMinY;
            public float canvasMaxX;
            public float canvasMaxY;
            public float canvasPixelX;
            public float canvasPixelY;
            public float canvasPixelWidth;
            public float canvasPixelHeight;
            public float canvasScaleFactor;
            public float referenceWidth;
            public float referenceHeight;
            public float canvasRectWidth;
            public float canvasRectHeight;
            public int visibleCount;
            public int expectedVisibleCount;
        }

        private sealed class UpgradePanelSnapshot
        {
            public Text[] texts;
            public UpgradeTextAppearance[] appearances;
            public Button[] buttons;
            public bool[] interactable;
        }

        private sealed class UpgradeTextAppearance
        {
            public RectTransform rect;
            public Font font;
            public int fontSize;
            public FontStyle fontStyle;
            public TextAnchor alignment;
            public HorizontalWrapMode horizontalOverflow;
            public VerticalWrapMode verticalOverflow;
            public bool resizeTextForBestFit;
            public int resizeTextMinSize;
            public int resizeTextMaxSize;
            public float lineSpacing;
            public bool supportRichText;
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 offsetMin;
            public Vector2 offsetMax;
        }

        private sealed class TrackingSaveStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private readonly List<string> _profileWritePayloads = new List<string>();
            private bool _failNextProfileWrite;

            public TrackingSaveStore(ISaveStore inner) { _inner = inner; }

            public int ProfileWriteAttempts { get; private set; }
            public int SettingsWriteAttempts { get; private set; }
            public int FailedProfileWriteCount { get; private set; }
            public string FailedProfilePayload { get; private set; } = string.Empty;
            public IReadOnlyList<string> ProfileWritePayloads => _profileWritePayloads;
            public string GetPrimaryPath(SaveDocument document) => _inner.GetPrimaryPath(document);
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);
            public void FailNextProfileWrite() => _failNextProfileWrite = true;

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileWriteAttempts++;
                    _profileWritePayloads.Add(json);
                    if (_failNextProfileWrite)
                    {
                        _failNextProfileWrite = false;
                        FailedProfileWriteCount++;
                        FailedProfilePayload = json;
                        throw new IOException("simulated profile storage failure");
                    }
                }
                else if (document == SaveDocument.Settings)
                {
                    SettingsWriteAttempts++;
                }

                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
