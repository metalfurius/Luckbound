using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class HubPreparationLocalizationTests : LuckboundInputTestFixture
    {
        private static readonly string[] ClassIds = { "class.bellator", "class.venator", "class.magus" };
        private static readonly string[] ClassButtonNames =
        {
            "ClassBellatorButton", "ClassVenatorButton", "ClassMagusButton"
        };
        private static readonly string[] SkillButtonNames =
        {
            "SkillOptionOneButton", "SkillOptionTwoButton", "SkillOptionThreeButton"
        };
        private static readonly string[] RaceButtonNames =
        {
            "RaceOrciniButton", "RaceAlvariButton", "RaceDvergarButton"
        };
        private static readonly string[][] SkillIds =
        {
            new[] { "skill.bellator.circular_cut", "skill.bellator.guard", "skill.bellator.charge" },
            new[] { "skill.venator.piercing_arrow", "skill.venator.triple_shot", "skill.venator.root_trap" },
            new[] { "skill.magus.fireball", "skill.magus.frost_nova", "skill.magus.arcane_seal" }
        };
        private static readonly int[][] SkillCosts =
        {
            new[] { 8, 12, 16 }, new[] { 8, 12, 16 }, new[] { 8, 12, 16 }
        };
        private static readonly string[][] SpanishSkillNames =
        {
            new[] { "CORTE CIRCULAR", "GUARDIA", "EMBESTIDA" },
            new[] { "FLECHA PENETRANTE", "DISPARO TRIPLE", "TRAMPA DE RAÍCES" },
            new[] { "BOLA DE FUEGO", "NOVA DE ESCARCHA", "SELLO ARCANO" }
        };
        private static readonly string[][] EnglishSkillNames =
        {
            new[] { "CIRCULAR CUT", "GUARD", "CHARGE" },
            new[] { "PIERCING ARROW", "TRIPLE SHOT", "ROOT TRAP" },
            new[] { "FIREBALL", "FROST NOVA", "ARCANE SEAL" }
        };
        private static readonly string[] PrimaryTextObjectNames =
        {
            "HubTitle", "HubSubtitle", "ClassSectionTitle", "SkillSectionTitle",
            "PreparationSelectionSummary", "HubPreparationStatus", "ClassBellatorButtonLabel",
            "ClassVenatorButtonLabel", "ClassMagusButtonLabel", "SkillOptionOneButtonLabel",
            "SkillOptionTwoButtonLabel", "SkillOptionThreeButtonLabel", "RaceHumanButtonLabel",
            "RaceOrciniButtonLabel", "RaceAlvariButtonLabel", "RaceDvergarButtonLabel",
            "StartExpeditionButtonLabel", "HubReturnToMenuButtonLabel",
            "OpenPermanentUpgradesButtonLabel", "OpenGuildIntroButtonLabel"
        };

        [UnityTest]
        public IEnumerator PrimaryPreparationChangesLanguageInPlaceWithoutChangingSelectionOrState()
        {
            string saveDirectory = NewSaveDirectory("LuckboundE10HubPrimaryLocalization");
            var store = new TrackingSaveStore(new FileSaveStore(saveDirectory, null));
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper app = null;
            Time.timeScale = 1f;
            try
            {
                yield return LoadBootstrappedApplication(store, value => app = value);
                Assert.That(app != null && app.SaveDataReady && app.CanWriteProfile, Is.True);
                yield return LoadHub();
                LogPrimaryTextDiagnostics("es");
                LogSkillContentOverrideDiagnostics("es");
                LogSkillContentOverrideDiagnostics("en");

                HubPreparationController preparation = UnityEngine.Object.FindAnyObjectByType<HubPreparationController>();
                Canvas canvas = FindHubCanvas();
                Assert.That(preparation, Is.Not.Null);
                Assert.That(canvas, Is.Not.Null);
                UiElementSnapshot[] layout = CapturePrimaryUiLayout();
                string profileBefore = JsonUtility.ToJson(app.Profile, false);
                int profileWritesBefore = store.ProfileWriteAttempts;
                Task<SaveStorageSnapshot> profileSnapshotTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileSnapshotTask, "read writable Hub profile before localization navigation");
                SaveStorageSnapshot profileBeforeSnapshot = profileSnapshotTask.GetAwaiter().GetResult();

                AssertPrimaryStaticLabels("es");
                AssertPrimaryRaceLabels("es");
                AssertGuildOpenerLabel("es");
                VisitAllClassSkills(preparation, "es");
                SimulatePointerClick(FindButton("ClassBellatorButton"));
                SimulatePointerClick(FindButton("SkillOptionThreeButton"));
                Assert.That(preparation.SelectedClassId, Is.EqualTo("class.bellator"));
                Assert.That(preparation.SelectedSkillIds,
                    Is.EqualTo(new[] { "skill.bellator.circular_cut", "skill.bellator.guard" }));
                AssertText(FindText("HubPreparationStatus"), "Quita una habilidad antes de elegir otra.", wrapped: true);
                Assert.That(FindButton("StartExpeditionButton").interactable, Is.True,
                    "A selection warning must not change the valid two-skill preparation or its action gate.");
                AssertButtonLabel(FindButton("StartExpeditionButton"), "PARTIR", wrapped: true);

                OpenGuildLanguageList();
                Button languageButton = FindButton("GuildIntroListLanguageButton");
                SimulateSubmit(languageButton);
                yield return WaitForLanguage(app, "en");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(languageButton.gameObject),
                    "The product language selector keeps focus after its language callback refreshes the Hub.");
                Assert.That(preparation.SelectedClassId, Is.EqualTo("class.bellator"));
                Assert.That(preparation.SelectedSkillIds,
                    Is.EqualTo(new[] { "skill.bellator.circular_cut", "skill.bellator.guard" }));
                CloseGuildLanguageList();
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(FindButton("OpenGuildIntroButton").gameObject));
                AssertText(FindText("HubPreparationStatus"), "Remove a skill before choosing another.", wrapped: true);
                AssertPrimaryStaticLabels("en");
                AssertPrimaryRaceLabels("en");
                AssertGuildOpenerLabel("en");
                AssertLayoutUnchanged(layout);

                VisitAllClassSkills(preparation, "en");
                SimulatePointerClick(FindButton("ClassBellatorButton"));
                SimulatePointerClick(FindButton("SkillOptionOneButton"));
                Assert.That(preparation.SelectedSkillIds, Is.EqualTo(new[] { "skill.bellator.guard" }));
                AssertText(FindText("HubPreparationStatus"), "Choose two skills before departing.", wrapped: true);
                AssertButtonLabel(FindButton("StartExpeditionButton"), "DEPART", wrapped: true);
                Assert.That(FindButton("StartExpeditionButton").interactable, Is.False);

                layout = CapturePrimaryUiLayout();
                OpenGuildLanguageList();
                languageButton = FindButton("GuildIntroListLanguageButton");
                SimulateSubmit(languageButton);
                yield return WaitForLanguage(app, "es");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(languageButton.gameObject));
                Assert.That(preparation.SelectedClassId, Is.EqualTo("class.bellator"));
                Assert.That(preparation.SelectedSkillIds, Is.EqualTo(new[] { "skill.bellator.guard" }));
                CloseGuildLanguageList();
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(FindButton("OpenGuildIntroButton").gameObject));
                AssertText(FindText("HubPreparationStatus"), "Elige dos habilidades para salir.", wrapped: true);
                AssertPrimaryStaticLabels("es", singleSelectedSkill: true);
                AssertPrimaryRaceLabels("es");
                AssertGuildOpenerLabel("es");
                AssertLayoutUnchanged(layout);

                Assert.That(app.Settings.languageId, Is.EqualTo("es"));
                Assert.That(store.SettingsWriteAttempts, Is.GreaterThanOrEqualTo(2));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(profileWritesBefore),
                    "Class, skill, warning and language navigation must not write Profile.");
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileBefore));
                Task<SaveStorageSnapshot> profileAfterTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileAfterTask, "read writable Hub profile after localization navigation");
                SaveStorageSnapshot profileAfterSnapshot = profileAfterTask.GetAwaiter().GetResult();
                Assert.That(profileAfterSnapshot.PrimaryJson, Is.EqualTo(profileBeforeSnapshot.PrimaryJson));
                Assert.That(profileAfterSnapshot.BackupJson, Is.EqualTo(profileBeforeSnapshot.BackupJson));
                AssertLayoutUnchanged(layout);
            }
            finally
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                app = ApplicationBootstrapper.Instance;
                if (app != null) UnityEngine.Object.Destroy(app.gameObject);
                Time.timeScale = 1f;
                DeleteSaveDirectory(saveDirectory);
            }
        }

        [UnityTest]
        public IEnumerator PrimaryPreparationLoadsPersistedEnglishWithReadOnlyProfileAndKeepsSettingsIndependent()
        {
            string saveDirectory = NewSaveDirectory("LuckboundE10HubPrimaryReadonlyLocalization");
            var fileStore = new FileSaveStore(saveDirectory, null);
            var settingsSeeder = new SaveRepository(fileStore);
            var store = new TrackingSaveStore(fileStore);
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper app = null;
            Time.timeScale = 1f;
            try
            {
                Task<SaveWriteResult> seedSettingsTask = settingsSeeder.SaveSettingsAsync(new GameSettingsSaveData
                {
                    languageId = "en"
                });
                yield return WaitForTask(seedSettingsTask, "persist English Settings before application bootstrap");
                Assert.That(seedSettingsTask.GetAwaiter().GetResult().Succeeded, Is.True);
                Task<SaveStorageSnapshot> seededSettingsTask = fileStore.ReadAsync(SaveDocument.Settings);
                yield return WaitForTask(seededSettingsTask, "verify persisted English Settings before application bootstrap");
                SettingsSaveEnvelope seededSettings = JsonUtility.FromJson<SettingsSaveEnvelope>(
                    seededSettingsTask.GetAwaiter().GetResult().PrimaryJson);
                Assert.That(seededSettings.settings.languageId, Is.EqualTo("en"));

                yield return LoadReadOnlyActiveExpeditionProfile(store, value => app = value);
                Assert.That(app.Settings.languageId, Is.EqualTo("en"));
                Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.SaveDataReady && !app.CanWriteProfile, Is.True);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(app.ActiveRun, Is.Null);
                yield return LoadHub();
                LogPrimaryTextDiagnostics("en");

                HubPreparationController preparation = UnityEngine.Object.FindAnyObjectByType<HubPreparationController>();
                Assert.That(preparation, Is.Not.Null);
                UiElementSnapshot[] layout = CapturePrimaryUiLayout();
                string profileBefore = JsonUtility.ToJson(app.Profile, false);
                int profileWritesBefore = store.ProfileWriteAttempts;
                Task<SaveStorageSnapshot> profileSnapshotTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileSnapshotTask, "read authentic readonly Profile before Hub language changes");
                SaveStorageSnapshot profileBeforeSnapshot = profileSnapshotTask.GetAwaiter().GetResult();
                string activeExpeditionDiagnostic = app.ActiveExpeditionLoadError;

                AssertPrimaryStaticLabels("en");
                AssertPrimaryRaceLabels("en");
                AssertGuildOpenerLabel("en");
                VisitAllClassSkills(preparation, "en",
                    "The saved expedition needs review and has been preserved: " + activeExpeditionDiagnostic);
                SimulatePointerClick(FindButton("ClassBellatorButton"));
                AssertText(FindText("HubPreparationStatus"),
                    "The saved expedition needs review and has been preserved: " + activeExpeditionDiagnostic,
                    wrapped: true);
                AssertButtonLabel(FindButton("StartExpeditionButton"), "DEPART", wrapped: true);
                Assert.That(FindButton("StartExpeditionButton").interactable, Is.False,
                    "The active legacy expedition remains blocked for review in readonly mode.");
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(!app.CanWriteProfile && app.HasActiveExpedition && app.ActiveRun == null, Is.True);

                OpenGuildLanguageList();
                Button languageButton = FindButton("GuildIntroListLanguageButton");
                SimulateSubmit(languageButton);
                yield return WaitForLanguage(app, "es");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(languageButton.gameObject));
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(!app.CanWriteProfile && app.HasActiveExpedition && app.ActiveRun == null, Is.True);
                CloseGuildLanguageList();
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(FindButton("OpenGuildIntroButton").gameObject));
                AssertPrimaryStaticLabels("es");
                AssertPrimaryRaceLabels("es");
                AssertGuildOpenerLabel("es");
                AssertText(FindText("HubPreparationStatus"),
                    "La expedición guardada necesita revisión y se conserva: " + activeExpeditionDiagnostic,
                    wrapped: true);
                AssertButtonLabel(FindButton("StartExpeditionButton"), "PARTIR", wrapped: true);
                Assert.That(FindButton("StartExpeditionButton").interactable, Is.False);
                AssertLayoutUnchanged(layout);

                OpenGuildLanguageList();
                languageButton = FindButton("GuildIntroListLanguageButton");
                SimulateSubmit(languageButton);
                yield return WaitForLanguage(app, "en");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(languageButton.gameObject));
                CloseGuildLanguageList();
                Assert.That(EventSystem.current.currentSelectedGameObject,
                    Is.SameAs(FindButton("OpenGuildIntroButton").gameObject));
                AssertPrimaryStaticLabels("en");
                AssertPrimaryRaceLabels("en");
                AssertGuildOpenerLabel("en");
                AssertText(FindText("HubPreparationStatus"),
                    "The saved expedition needs review and has been preserved: " + activeExpeditionDiagnostic,
                    wrapped: true);
                AssertButtonLabel(FindButton("StartExpeditionButton"), "DEPART", wrapped: true);
                Assert.That(FindButton("StartExpeditionButton").interactable, Is.False);
                AssertLayoutUnchanged(layout);

                Assert.That(app.Settings.languageId, Is.EqualTo("en"));
                Assert.That(store.SettingsWriteAttempts, Is.GreaterThanOrEqualTo(2));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(profileWritesBefore),
                    "The Guild language selector may save Settings but must not write the readonly Profile.");
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileBefore));
                Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(!app.CanWriteProfile && app.HasActiveExpedition && app.ActiveRun == null, Is.True);
                Task<SaveStorageSnapshot> profileAfterTask = store.ReadAsync(SaveDocument.Profile);
                yield return WaitForTask(profileAfterTask, "read authentic readonly Profile after Hub language changes");
                SaveStorageSnapshot profileAfterSnapshot = profileAfterTask.GetAwaiter().GetResult();
                Assert.That(profileAfterSnapshot.PrimaryJson, Is.EqualTo(profileBeforeSnapshot.PrimaryJson));
                Assert.That(profileAfterSnapshot.BackupJson, Is.EqualTo(profileBeforeSnapshot.BackupJson));
                AssertLayoutUnchanged(layout);
            }
            finally
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                app = ApplicationBootstrapper.Instance;
                if (app != null) UnityEngine.Object.Destroy(app.gameObject);
                Time.timeScale = 1f;
                DeleteSaveDirectory(saveDirectory);
            }
        }

        private static void VisitAllClassSkills(
            HubPreparationController preparation, string language, string statusOverride = null)
        {
            Assert.That(language, Is.EqualTo("es").Or.EqualTo("en"));
            string[][] expectedNames = language == "es" ? SpanishSkillNames : EnglishSkillNames;
            for (int classIndex = 0; classIndex < ClassIds.Length; classIndex++)
            {
                SimulatePointerClick(FindButton(ClassButtonNames[classIndex]));
                Assert.That(preparation.SelectedClassId, Is.EqualTo(ClassIds[classIndex]));
                Assert.That(preparation.SelectedSkillIds,
                    Is.EqualTo(new[] { SkillIds[classIndex][0], SkillIds[classIndex][1] }));
                for (int skillIndex = 0; skillIndex < SkillButtonNames.Length; skillIndex++)
                {
                    string marker = skillIndex < 2 ? "✓ " : "＋ ";
                    string expected = marker + expectedNames[classIndex][skillIndex] +
                        "  ·  " + SkillCosts[classIndex][skillIndex] + " EN";
                    AssertButtonLabel(FindButton(SkillButtonNames[skillIndex]), expected, wrapped: true);
                }
                SimulatePointerClick(FindButton(SkillButtonNames[0]));
                SimulatePointerClick(FindButton(SkillButtonNames[2]));
                Assert.That(preparation.SelectedSkillIds,
                    Is.EqualTo(new[] { SkillIds[classIndex][1], SkillIds[classIndex][2] }),
                    "The third named skill must be selectable through its actual Hub control.");
                string expectedStatus = statusOverride ?? (language == "es"
                    ? "Preparación lista · HUMANO · 100 SALUD · 50 ENERGÍA · 2 pociones"
                    : "Ready · HUMAN · 100 HEALTH · 50 ENERGY · 2 potions");
                AssertText(FindText("HubPreparationStatus"), expectedStatus, wrapped: true);
            }
        }

        private static void AssertPrimaryStaticLabels(string language, bool singleSelectedSkill = false)
        {
            bool english = language == "en";
            AssertText(FindText("HubTitle"), english ? "THE TAVERN" : "LA TABERNA");
            AssertText(FindText("HubSubtitle"), english
                ? "Choose your path. The castle is already awake."
                : "Elige tu senda. El castillo ya está despierto.", wrapped: true);
            AssertText(FindText("ClassSectionTitle"), english ? "CLASS" : "CLASE");
            AssertText(FindText("SkillSectionTitle"), english
                ? "CHOOSE TWO SKILLS · KEEP THEM THROUGHOUT THE EXPEDITION"
                : "ELIGE DOS HABILIDADES · SE CONSERVAN DURANTE LA EXPEDICIÓN", wrapped: true);
            AssertButtonLabel(FindButton("HubReturnToMenuButton"), english ? "BACK TO MENU" : "VOLVER AL MENÚ", wrapped: true);
            AssertButtonLabel(FindButton("OpenPermanentUpgradesButton"), english ? "UPGRADES" : "MEJORAS", wrapped: true);
            string skillSummary = english
                ? singleSelectedSkill ? "GUARD" : "CIRCULAR CUT  ·  GUARD"
                : singleSelectedSkill ? "GUARDIA" : "CORTE CIRCULAR  ·  GUARDIA";
            AssertText(FindText("PreparationSelectionSummary"), english
                ? "CLASS  BELLATOR     RACE  HUMAN\nSKILLS  " + skillSummary
                : "CLASE  BELLATOR     RAZA  HUMANO\nHABILIDADES  " + skillSummary, wrapped: true);
        }

        private static void AssertPrimaryRaceLabels(string language)
        {
            bool english = language == "en";
            AssertButtonLabel(FindButton("RaceHumanButton"), english
                ? "◆ HUMAN · AVAILABLE · DEFAULT · 2 FORTUNE"
                : "◆ HUMANO · DISPONIBLE · INICIAL · 2 FORTUNA", wrapped: true);
            string[] expectedRaces = english
                ? new[] { "LOCKED · ORCINI · COMBAT CONTRACT", "LOCKED · ALVARI · STORY CONTRACT", "LOCKED · DVERGAR · CRAFTING CONTRACT" }
                : new[] { "BLOQUEADA · ORCINI · CONTRATO DE COMBATE", "BLOQUEADA · ALVARI · CONTRATO DE HISTORIA", "BLOQUEADA · DVERGAR · CONTRATO DE FABRICACIÓN" };
            for (int index = 0; index < RaceButtonNames.Length; index++)
            {
                Button button = FindButton(RaceButtonNames[index]);
                Assert.That(button.interactable, Is.False,
                    "This acceptance must preserve the authentic locked race classification.");
                AssertButtonLabel(button, expectedRaces[index], wrapped: true);
            }
        }

        private static void AssertGuildOpenerLabel(string language)
        {
            AssertButtonLabel(FindButton("OpenGuildIntroButton"), language == "en" ? "GUILD" : "GREMIO", wrapped: true);
        }

        private static void OpenGuildLanguageList()
        {
            SimulatePointerClick(FindButton("OpenGuildIntroButton"));
            GameObject root = GameObject.Find("GuildIntroRoot");
            Assert.That(root != null && root.activeSelf, Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject,
                Is.SameAs(FindButton("GuildCharacterButton0").gameObject));
        }

        private static void CloseGuildLanguageList()
        {
            GameObject root = GameObject.Find("GuildIntroRoot");
            Assert.That(root != null && root.activeSelf, Is.True,
                "The language modal root must be active before its close action.");
            SimulateSubmit(FindButton("GuildIntroListCloseButton"));
            Assert.That(root, Is.Not.Null,
                "The same language modal root must remain available after closing.");
            Assert.That(root.activeSelf, Is.False,
                "The captured language modal root must be inactive after its close action.");
        }

        private static IEnumerator WaitForLanguage(ApplicationBootstrapper app, string languageId)
        {
            string expectedTitle = languageId == "en" ? "THE TAVERN" : "LA TABERNA";
            string expectedLanguageChoice = languageId == "en" ? "Spanish" : "Inglés";
            for (int frame = 0; frame < 300 &&
                (app.Settings.languageId != languageId || FindText("HubTitle").text != expectedTitle ||
                 FindButton("GuildIntroListLanguageButton").GetComponentInChildren<Text>(true).text != expectedLanguageChoice); frame++)
                yield return null;
            Assert.That(app.Settings.languageId, Is.EqualTo(languageId));
            Assert.That(FindText("HubTitle").text, Is.EqualTo(expectedTitle));
            AssertText(FindButton("GuildIntroListLanguageButton").GetComponentInChildren<Text>(true),
                expectedLanguageChoice, wrapped: true);
        }

        private static IEnumerator LoadBootstrappedApplication(ISaveStore store, Action<ApplicationBootstrapper> ready)
        {
            ApplicationBootstrapper old = ApplicationBootstrapper.Instance;
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            yield return null;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 &&
                (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady); frame++)
                yield return null;
            ready(ApplicationBootstrapper.Instance);
        }

        private static IEnumerator LoadReadOnlyActiveExpeditionProfile(
            TrackingSaveStore store, Action<ApplicationBootstrapper> ready)
        {
            ApplicationBootstrapper app = null;
            yield return LoadBootstrappedApplication(store, value => app = value);
            Assert.That(app != null && app.SaveDataReady && app.CanWriteProfile, Is.True,
                "The authentic readonly fixture begins with the normal writable bootstrap profile.");
            Task<ContractProgressionCommitResult> accepted = app.AcceptContractAsync(ContractCatalog.Ct01);
            yield return WaitForTask(accepted, "accept CT01 in the historical readonly fixture setup");
            Assert.That(accepted.GetAwaiter().GetResult().Persisted, Is.True);
            for (int index = 0; index < 3; index++)
            {
                Task<ContractProgressionCommitResult> progress = app.RecordContractProgressEventAsync(
                    ContractProgressEvent.CombatRoomCompleted, "test/e10/hub-readonly-room-" + index);
                yield return WaitForTask(progress, "persist CT01 progress in readonly fixture setup");
                Assert.That(progress.GetAwaiter().GetResult().Persisted, Is.True);
            }

            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
            yield return WaitForTask(begin, "persist the active expedition used by the readonly fixture");
            Assert.That(begin.GetAwaiter().GetResult().Succeeded, Is.True);
            Assert.That(app.HasActiveExpedition, Is.True);

            UnityEngine.Object.Destroy(app.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 &&
                (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady ||
                 SceneManager.GetActiveScene().name != "Menu"); frame++)
                yield return null;
            app = ApplicationBootstrapper.Instance;
            Assert.That(app != null && app.SaveDataReady, Is.True);
            Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(app.CanWriteProfile, Is.False);
            Assert.That(app.ActiveRun, Is.Null);
            Assert.That(app.ContractProgression.IsComplete(ContractCatalog.Ct01), Is.True);
            ready(app);
        }

        private static IEnumerator LoadHub()
        {
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
            HubPreparationController preparation = null;
            for (int frame = 0; frame < 60 && preparation == null; frame++)
            {
                preparation = UnityEngine.Object.FindAnyObjectByType<HubPreparationController>();
                if (preparation == null) yield return null;
            }
            Assert.That(preparation, Is.Not.Null);
            for (int frame = 0; frame < 2; frame++) yield return null;
        }

        private static IEnumerator WaitForTask<T>(Task<T> task, string operation)
        {
            Assert.That(task, Is.Not.Null, operation + " returned no task.");
            for (int frame = 0; frame < 300 && !task.IsCompleted; frame++) yield return null;
            Assert.That(task.IsCompleted, Is.True, operation + " did not complete within 300 frames.");
        }

        private static void SimulatePointerClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True, button.gameObject.name + " should be interactable.");
            Assert.That(EventSystem.current, Is.Not.Null, "Hub should provide an EventSystem.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void SimulateSubmit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True, button.gameObject.name + " should be interactable.");
            Assert.That(EventSystem.current, Is.Not.Null, "Hub should provide an EventSystem.");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        private static Button FindButton(string name)
        {
            GameObject found = GameObject.Find(name);
            Assert.That(found, Is.Not.Null, "Could not find active Hub button " + name + ".");
            Button button = found.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, name + " needs a Button component.");
            return button;
        }

        private static Text FindText(string name)
        {
            GameObject found = GameObject.Find(name);
            Assert.That(found, Is.Not.Null, "Could not find active Hub text " + name + ".");
            Text text = found.GetComponent<Text>();
            Assert.That(text, Is.Not.Null, name + " needs a Text component.");
            return text;
        }

        private static Canvas FindHubCanvas()
        {
            GameObject canvas = GameObject.Find("HubPreparationCanvas");
            Assert.That(canvas, Is.Not.Null);
            Canvas result = canvas.GetComponent<Canvas>();
            Assert.That(result, Is.Not.Null);
            return result;
        }

        private static void AssertButtonLabel(Button button, string expected, bool wrapped = false)
        {
            Assert.That(button, Is.Not.Null);
            Text label = button.GetComponentInChildren<Text>(true);
            Assert.That(label, Is.Not.Null, button.gameObject.name + " needs a Text label.");
            AssertText(label, expected, wrapped);
        }

        private static void AssertText(Text text, string expected, bool wrapped = false)
        {
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Is.EqualTo(expected), text.name + " localization literal");
            AssertTextMetrics(text, expected, wrapped);
        }

        private static void AssertTextMetrics(Text text, string content, bool wrapped)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = text.rectTransform;
            Vector2 extents = rect.rect.size;
            HubTextMetric metric = CaptureHubTextMetric(text, text.name, content,
                "actualText", string.Empty, ActiveLanguageId());
            int visibleCharacters = content.Count(character => character != '\n' && character != '\r');
            Debug.Log("[E10.HUB.PRIMARY.TEXT-METRIC] " + JsonUtility.ToJson(metric));
            Assert.That(IsFinite(metric.pixelsPerUnit) && metric.pixelsPerUnit > 0f, Is.True,
                text.name + " pixels-per-unit scale must be finite and positive.");
            Assert.That(IsFinite(metric.preferredWidth) && metric.preferredWidth >= 0f, Is.True,
                text.name + " preferred width metric must be finite.");
            Assert.That(IsFinite(metric.preferredHeight) && metric.preferredHeight >= 0f, Is.True,
                text.name + " preferred height metric must be finite.");
            if (!wrapped)
                Assert.That(metric.preferredWidth, Is.LessThanOrEqualTo(extents.x + 0.5f),
                    text.name + " preferred width must fit its existing rect.");
            Assert.That(metric.preferredHeight, Is.LessThanOrEqualTo(extents.y + 0.5f),
                text.name + " preferred height must fit its existing rect.");
            Assert.That(metric.visibleCount, Is.GreaterThanOrEqualTo(visibleCharacters),
                text.name + " must display every authored character with its existing overflow mode.");
        }

        private static void LogPrimaryTextDiagnostics(string language)
        {
            Canvas.ForceUpdateCanvases();
            foreach (string name in PrimaryTextObjectNames)
            {
                GameObject target = GameObject.Find(name);
                Text text = target == null ? null : target.GetComponent<Text>();
                if (text == null && target != null) text = target.GetComponentInChildren<Text>(true);
                string content = text == null ? string.Empty : text.text;
                Debug.Log("[E10.HUB.PRIMARY.TEXT-METRIC] " + JsonUtility.ToJson(
                    CaptureHubTextMetric(text, name, content, "actualText", string.Empty, language)));
            }
        }

        private static void LogSkillContentOverrideDiagnostics(string language)
        {
            Canvas.ForceUpdateCanvases();
            string[][] expectedNames = language == "es" ? SpanishSkillNames : EnglishSkillNames;
            for (int classIndex = 0; classIndex < ClassIds.Length; classIndex++)
            {
                for (int skillIndex = 0; skillIndex < SkillButtonNames.Length; skillIndex++)
                {
                    GameObject buttonObject = GameObject.Find(SkillButtonNames[skillIndex]);
                    Text text = buttonObject == null ? null : buttonObject.GetComponentInChildren<Text>(true);
                    string marker = skillIndex < 2 ? "✓ " : "＋ ";
                    string content = marker + expectedNames[classIndex][skillIndex] + "  ·  " +
                        SkillCosts[classIndex][skillIndex] + " EN";
                    string name = text == null ? SkillButtonNames[skillIndex] + "Label" : text.name;
                    Debug.Log("[E10.HUB.PRIMARY.TEXT-METRIC] " + JsonUtility.ToJson(
                        CaptureHubTextMetric(text, name, content, "contentOverride",
                            SkillIds[classIndex][skillIndex], language)));
                }
            }
        }

        private static HubTextMetric CaptureHubTextMetric(
            Text text, string name, string content, string measurementKind, string skillId, string language)
        {
            var metric = new HubTextMetric
            {
                name = name ?? string.Empty,
                content = content ?? string.Empty,
                measurementKind = measurementKind ?? string.Empty,
                skillId = skillId ?? string.Empty,
                language = language ?? string.Empty,
                fontName = string.Empty,
                fontSize = -1,
                anchoredX = -1f,
                anchoredY = -1f,
                rectWidth = -1f,
                rectHeight = -1f,
                pixelsPerUnit = -1f,
                preferredWidth = -1f,
                preferredHeight = -1f,
                visibleCount = -1,
                expectedVisibleCount = -1
            };
            if (text == null) return metric;

            RectTransform rect = text.rectTransform;
            if (rect == null) return metric;
            Vector2 extents = rect.rect.size;
            string measuredContent = content ?? string.Empty;
            float pixelsPerUnit = text.pixelsPerUnit;
            metric.fontName = text.font == null ? string.Empty : text.font.name;
            metric.fontSize = text.fontSize;
            metric.anchoredX = FiniteOrSentinel(rect.anchoredPosition.x);
            metric.anchoredY = FiniteOrSentinel(rect.anchoredPosition.y);
            metric.rectWidth = FiniteOrSentinel(extents.x);
            metric.rectHeight = FiniteOrSentinel(extents.y);
            metric.pixelsPerUnit = FiniteOrSentinel(pixelsPerUnit);

            TextGenerationSettings settings = text.GetGenerationSettings(extents);
            if (IsFinite(pixelsPerUnit) && pixelsPerUnit > 0f)
            {
                var preferredGenerator = new TextGenerator();
                preferredGenerator.Populate(measuredContent, settings);
                metric.preferredWidth = FiniteOrSentinel(
                    preferredGenerator.GetPreferredWidth(measuredContent, settings) / pixelsPerUnit);
                metric.preferredHeight = FiniteOrSentinel(
                    preferredGenerator.GetPreferredHeight(measuredContent, settings) / pixelsPerUnit);
            }

            var visibilityGenerator = new TextGenerator();
            visibilityGenerator.Populate(measuredContent, settings);
            metric.visibleCount = visibilityGenerator.characterCountVisible;
            metric.expectedVisibleCount = measuredContent.Count(character => character != '\n' && character != '\r');
            if (metric.visibleCount < 0) metric.visibleCount = -1;
            return metric;
        }

        private static string ActiveLanguageId()
        {
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            return app == null ? string.Empty : app.Settings.languageId ?? string.Empty;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float FiniteOrSentinel(float value)
        {
            return IsFinite(value) ? value : -1f;
        }

        private static UiElementSnapshot[] CapturePrimaryUiLayout()
        {
            string[] names =
            {
                "HubTitle", "HubSubtitle", "ClassSectionTitle", "SkillSectionTitle",
                "PreparationSelectionSummary", "HubPreparationStatus", "ClassBellatorButton",
                "ClassVenatorButton", "ClassMagusButton", "ClassBellatorButtonLabel",
                "ClassVenatorButtonLabel", "ClassMagusButtonLabel", "SkillOptionOneButton",
                "SkillOptionTwoButton", "SkillOptionThreeButton", "SkillOptionOneButtonLabel",
                "SkillOptionTwoButtonLabel", "SkillOptionThreeButtonLabel", "RaceHumanButton",
                "RaceHumanButtonLabel", "RaceOrciniButton", "RaceOrciniButtonLabel", "RaceAlvariButton",
                "RaceAlvariButtonLabel", "RaceDvergarButton", "RaceDvergarButtonLabel",
                "StartExpeditionButton", "StartExpeditionButtonLabel", "HubReturnToMenuButton",
                "HubReturnToMenuButtonLabel", "OpenPermanentUpgradesButton", "OpenPermanentUpgradesButtonLabel",
                "OpenGuildIntroButton", "OpenGuildIntroButtonLabel"
            };
            return names.Select(CaptureUiElement).ToArray();
        }

        private static UiElementSnapshot CaptureUiElement(string name)
        {
            GameObject target = GameObject.Find(name);
            Assert.That(target, Is.Not.Null, "Could not capture Hub layout object " + name + ".");
            RectTransform rect = target.GetComponent<RectTransform>();
            Assert.That(rect, Is.Not.Null, name + " must retain its scene RectTransform.");
            return new UiElementSnapshot(name, target, rect);
        }

        private static void AssertLayoutUnchanged(UiElementSnapshot[] snapshots)
        {
            foreach (UiElementSnapshot snapshot in snapshots)
            {
                Assert.That(GameObject.Find(snapshot.Name), Is.SameAs(snapshot.Target), snapshot.Name + " scene object");
                Assert.That(snapshot.Target.GetComponent<RectTransform>(), Is.SameAs(snapshot.Rect), snapshot.Name + " rect object");
                Assert.That(snapshot.Rect.anchoredPosition, Is.EqualTo(snapshot.AnchoredPosition), snapshot.Name + " position");
                Assert.That(snapshot.Rect.sizeDelta, Is.EqualTo(snapshot.SizeDelta), snapshot.Name + " size");
                Assert.That(snapshot.Rect.anchorMin, Is.EqualTo(snapshot.AnchorMin), snapshot.Name + " anchor min");
                Assert.That(snapshot.Rect.anchorMax, Is.EqualTo(snapshot.AnchorMax), snapshot.Name + " anchor max");
                if (snapshot.Button != null)
                {
                    Assert.That(snapshot.Target.GetComponent<Button>(), Is.SameAs(snapshot.Button), snapshot.Name + " Button component");
                    Assert.That(snapshot.Button.interactable, Is.EqualTo(snapshot.Interactable), snapshot.Name + " interactable state");
                    ColorBlock colors = snapshot.Button.colors;
                    Assert.That(colors.normalColor, Is.EqualTo(snapshot.Colors.normalColor), snapshot.Name + " normal color");
                    Assert.That(colors.highlightedColor, Is.EqualTo(snapshot.Colors.highlightedColor), snapshot.Name + " highlighted color");
                    Assert.That(colors.pressedColor, Is.EqualTo(snapshot.Colors.pressedColor), snapshot.Name + " pressed color");
                    Assert.That(colors.selectedColor, Is.EqualTo(snapshot.Colors.selectedColor), snapshot.Name + " selected color");
                    Assert.That(colors.disabledColor, Is.EqualTo(snapshot.Colors.disabledColor), snapshot.Name + " disabled color");
                }
                if (snapshot.Text != null)
                {
                    Assert.That(snapshot.Target.GetComponent<Text>(), Is.SameAs(snapshot.Text), snapshot.Name + " Text component");
                    Assert.That(snapshot.Text.font, Is.SameAs(snapshot.Font), snapshot.Name + " font");
                    Assert.That(snapshot.Text.fontSize, Is.EqualTo(snapshot.FontSize), snapshot.Name + " font size");
                }
            }
        }

        private static string NewSaveDirectory(string prefix)
        {
            string directory = Path.Combine(Path.GetTempPath(), prefix, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteSaveDirectory(string directory)
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a save directory outside the system temp root.");
            if (Directory.Exists(fullDirectory)) Directory.Delete(fullDirectory, recursive: true);
        }

        [Serializable]
        private sealed class HubTextMetric
        {
            public string name;
            public string content;
            public string measurementKind;
            public string skillId;
            public string language;
            public string fontName;
            public int fontSize;
            public float anchoredX;
            public float anchoredY;
            public float rectWidth;
            public float rectHeight;
            public float pixelsPerUnit;
            public float preferredWidth;
            public float preferredHeight;
            public int visibleCount;
            public int expectedVisibleCount;
        }

        private sealed class UiElementSnapshot
        {
            public UiElementSnapshot(string name, GameObject target, RectTransform rect)
            {
                Name = name;
                Target = target;
                Rect = rect;
                Button = target.GetComponent<Button>();
                Interactable = Button != null && Button.interactable;
                Colors = Button == null ? default : Button.colors;
                Text = target.GetComponent<Text>();
                Font = Text == null ? null : Text.font;
                FontSize = Text == null ? -1 : Text.fontSize;
                AnchoredPosition = rect.anchoredPosition;
                SizeDelta = rect.sizeDelta;
                AnchorMin = rect.anchorMin;
                AnchorMax = rect.anchorMax;
            }

            public string Name { get; }
            public GameObject Target { get; }
            public RectTransform Rect { get; }
            public Button Button { get; }
            public bool Interactable { get; }
            public ColorBlock Colors { get; }
            public Text Text { get; }
            public Font Font { get; }
            public int FontSize { get; }
            public Vector2 AnchoredPosition { get; }
            public Vector2 SizeDelta { get; }
            public Vector2 AnchorMin { get; }
            public Vector2 AnchorMax { get; }
        }

        private sealed class TrackingSaveStore : ISaveStore
        {
            private readonly FileSaveStore _inner;

            public TrackingSaveStore(FileSaveStore inner) { _inner = inner; }

            public int ProfileWriteAttempts { get; private set; }
            public int SettingsWriteAttempts { get; private set; }
            public string GetPrimaryPath(SaveDocument document) => _inner.GetPrimaryPath(document);
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile) ProfileWriteAttempts++;
                else SettingsWriteAttempts++;
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
