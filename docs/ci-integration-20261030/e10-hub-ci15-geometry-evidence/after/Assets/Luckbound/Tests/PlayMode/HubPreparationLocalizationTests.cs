using System;
using System.Collections;
using System.Collections.Generic;
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
        private static readonly string[] SummaryClassLabels = { "BELLATOR", "VENATOR", "MAGUS" };
        private static readonly int[,] SummarySkillPairs = { { 0, 1 }, { 0, 2 }, { 1, 2 } };
        private static readonly string[] SummarySpanishRaceLabels = { "HUMANO", "ORCINI", "ALVARI", "DVERGAR" };
        private static readonly string[] SummaryEnglishRaceLabels = { "HUMAN", "ORCINI", "ALVARI", "DVERGAR" };
        private static readonly string[,] PrimaryActionCases =
        {
            { "depart", "PARTIR", "DEPART" },
            { "retry_settlement", "REINTENTAR LIQUIDACIÓN", "RETRY SETTLEMENT" },
            { "retry_save", "REINTENTAR GUARDADO", "RETRY SAVE" },
            { "continue", "CONTINUAR EXPEDICIÓN", "CONTINUE EXPEDITION" }
        };
        private static readonly string[,] PrimaryRaceCases =
        {
            { "orcini", "RaceOrciniButton", "ORCINI", "CONTRATO DE COMBATE", "COMBAT CONTRACT" },
            { "alvari", "RaceAlvariButton", "ALVARI", "CONTRATO DE HISTORIA", "STORY CONTRACT" },
            { "dvergar", "RaceDvergarButton", "DVERGAR", "CONTRATO DE FABRICACIÓN", "CRAFTING CONTRACT" }
        };
        private static readonly string[,] PrimaryStatusCases =
        {
            { "ui.hub.selection.exact_two_count", "Elige exactamente dos habilidades (1/2).", "Choose exactly two skills (1/2)." },
            { "ui.hub.selection.choose_two_before_departure", "Elige dos habilidades para salir.", "Choose two skills before departing." },
            { "ui.hub.selection.remove_before_choosing", "Quita una habilidad antes de elegir otra.", "Remove a skill before choosing another." },
            { "ui.hub.selection.exact_two_required", "Debes elegir exactamente dos habilidades.", "You must choose exactly two skills." },
            { "ui.hub.race.locked_profile", "Esa raza sigue bloqueada en tu perfil.", "That race is still locked in your profile." },
            { "ui.hub.race.selected_locked_profile", "La raza seleccionada ya no está desbloqueada en tu perfil.", "The selected race is no longer unlocked in your profile." },
            { "ui.hub.preparation.ready", "Preparación lista · HUMANO · 100 SALUD · 50 ENERGÍA · 2 pociones", "Ready · HUMAN · 100 HEALTH · 50 ENERGY · 2 potions" },
            { "ui.hub.status.save_pending", "El guardado sigue pendiente: LegacyEncounterPinMissing", "The save is still pending: LegacyEncounterPinMissing" },
            { "ui.hub.error.retry_save", "No se pudo reintentar el guardado: LegacyEncounterPinMissing", "Could not retry the save: LegacyEncounterPinMissing" },
            { "ui.hub.settlement.saved", "La liquidación se guardó. Puedes preparar otra expedición.", "Settlement saved. You can prepare another expedition." },
            { "ui.hub.settlement.pending", "La liquidación sigue pendiente: LegacyEncounterPinMissing", "Settlement is still pending: LegacyEncounterPinMissing" },
            { "ui.hub.error.retry_settlement", "No se pudo reintentar la liquidación: LegacyEncounterPinMissing", "Could not retry settlement: LegacyEncounterPinMissing" },
            { "ui.hub.error.continue_expedition", "No se pudo reanudar: LegacyEncounterPinMissing", "Could not resume: LegacyEncounterPinMissing" },
            { "ui.hub.error.save_expedition", "No se pudo guardar la expedición: LegacyEncounterPinMissing", "Could not save the expedition: LegacyEncounterPinMissing" },
            { "ui.hub.error.start_expedition", "No se pudo iniciar: LegacyEncounterPinMissing", "Could not start: LegacyEncounterPinMissing" },
            { "ui.hub.error.preparation_unavailable", "No se puede preparar la expedición: LegacyEncounterPinMissing", "Cannot prepare the expedition: LegacyEncounterPinMissing" },
            { "ui.hub.profile.unavailable_yet", "El perfil todavía no está disponible. Vuelve a intentarlo en unos segundos.", "The profile is not available yet. Try again in a few seconds." },
            { "ui.hub.profile.settlement_pending", "Hay una liquidación pendiente de guardar. Reinténtala o exporta el guardado antes de iniciar otra expedición.", "A settlement is waiting to be saved. Retry it or export the save before starting another expedition." },
            { "ui.hub.profile.expedition_write_pending", "La expedición no se ha guardado. Reintenta la escritura antes de continuar.", "The expedition was not saved. Retry the write before continuing." },
            { "ui.hub.profile.continue_expedition", "Hay una expedición guardada. Continúa para volver al punto de entrada de la sala actual.", "A saved expedition is ready. Continue to return to the current room's entry point." },
            { "ui.hub.profile.expedition_review", "La expedición guardada necesita revisión y se conserva: LegacyEncounterPinMissing", "The saved expedition needs review and has been preserved: LegacyEncounterPinMissing" },
            { "ui.hub.profile.incompatible", "El perfil pertenece a una versión más nueva. Se conserva sin cambios y no se iniciará una partida nueva.", "This profile belongs to a newer version. It is preserved unchanged and a new run cannot start." },
            { "ui.hub.profile.recovered_backup", "El perfil principal estaba dañado; se recuperó la copia de respaldo. Revisa el Renombre y vuelve a guardar cuando puedas.", "The primary profile was damaged; the backup was recovered. Review your Renown and save again when you can." },
            { "ui.hub.profile.corrupted", "El perfil y su respaldo no se pudieron leer. Se conservaron y no se sobrescribirán.", "The profile and its backup could not be read. They were preserved and will not be overwritten." },
            { "ui.hub.profile.unavailable", "El perfil no está disponible; no se iniciará una expedición.", "The profile is unavailable; an expedition cannot start." }
        };
        private static readonly PrimaryGeometryCase[] PrimaryGeometryCases =
        {
            new PrimaryGeometryCase("SkillOptionOneButton", new Vector2(-72f, 2f), new Vector2(246f, 22f), true),
            new PrimaryGeometryCase("SkillOptionTwoButton", new Vector2(-72f, -24f), new Vector2(246f, 22f), true),
            new PrimaryGeometryCase("SkillOptionThreeButton", new Vector2(-72f, -50f), new Vector2(246f, 22f), true),
            new PrimaryGeometryCase("RaceHumanButton", new Vector2(126f, 1f), new Vector2(138f, 24f), true),
            new PrimaryGeometryCase("RaceOrciniButton", new Vector2(126f, -33f), new Vector2(138f, 38f), true),
            new PrimaryGeometryCase("RaceAlvariButton", new Vector2(126f, -73f), new Vector2(138f, 38f), true),
            new PrimaryGeometryCase("RaceDvergarButton", new Vector2(126f, -113f), new Vector2(138f, 38f), true),
            new PrimaryGeometryCase("StartExpeditionButton", new Vector2(-112f, -77f), new Vector2(166f, 26f), true),
            new PrimaryGeometryCase("OpenPermanentUpgradesButton", new Vector2(13f, -77f), new Vector2(76f, 22f), true),
            new PrimaryGeometryCase("PreparationSelectionSummary", new Vector2(-72f, -100f), new Vector2(246f, 16f), false),
            new PrimaryGeometryCase("HubPreparationStatus", new Vector2(-72f, -122f), new Vector2(246f, 24f), false)
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
                AssertInitialPrimaryTextMatrix("es");
                AssertApprovedPrimaryGeometry(FindHubCanvas());

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
                AssertInitialPrimaryTextMatrix("en");
                AssertApprovedPrimaryGeometry(FindHubCanvas());

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

        private static void AssertInitialPrimaryTextMatrix(string currentLanguage)
        {
            Canvas.ForceUpdateCanvases();
            var records = new List<HubTextMetric>(204);

            foreach (string name in PrimaryTextObjectNames)
            {
                Text text = FindExistingText(name);
                string content = text == null ? string.Empty : text.text;
                AddPrimaryTextMetric(records, text, name, content, "actualText", string.Empty,
                    currentLanguage, PrimaryTextControlName(name), "actual." + name + "." + currentLanguage,
                    name == "HubTitle" || name == "ClassSectionTitle" || name == "StartExpeditionButtonLabel");
            }

            for (int languageIndex = 0; languageIndex < 2; languageIndex++)
            {
                string language = languageIndex == 0 ? "es" : "en";
                string[][] expectedNames = languageIndex == 0 ? SpanishSkillNames : EnglishSkillNames;
                for (int classIndex = 0; classIndex < ClassIds.Length; classIndex++)
                {
                    for (int skillIndex = 0; skillIndex < SkillButtonNames.Length; skillIndex++)
                    {
                        string marker = skillIndex < 2 ? "✓ " : "＋ ";
                        string content = marker + expectedNames[classIndex][skillIndex] + "  ·  " +
                            SkillCosts[classIndex][skillIndex] + " EN";
                        AddPrimaryTextMetric(records, FindExistingText(SkillButtonNames[skillIndex]),
                            SkillButtonNames[skillIndex] + "Label", content, "contentOverride",
                            SkillIds[classIndex][skillIndex], language, SkillButtonNames[skillIndex],
                            "skill." + ClassIds[classIndex] + "." + skillIndex + "." + language, false);
                    }
                }
            }

            AddPrimaryActionMetrics(records);
            AddPrimaryRaceMetrics(records);
            AddPrimarySummaryMetrics(records);
            AddPrimaryStatusMetrics(records);

            LogPrimaryGeometryDiagnostics();
            foreach (HubTextMetric metric in records)
                Debug.Log("[E10.HUB.PRIMARY.TEXT-METRIC] " + JsonUtility.ToJson(metric));

            Assert.That(records.Count, Is.LessThanOrEqualTo(256),
                "The finite initial measurement block must remain bounded per UnityTest.");
            Assert.That(records.Count, Is.EqualTo(204),
                "The complete initial matrix contains 20 actual texts, 18 skill overrides and 166 finite contract cases.");
            Assert.That(records.Count(metric => metric.measurementKind == "actualText"), Is.EqualTo(20));
            Assert.That(records.Count(metric => metric.measurementKind == "contentOverride"), Is.EqualTo(184));
            Assert.That(records.Count(metric => !string.IsNullOrEmpty(metric.skillId)), Is.EqualTo(18));
            Assert.That(records.Count(metric => metric.caseId.StartsWith("action.", StringComparison.Ordinal)), Is.EqualTo(8));
            Assert.That(records.Count(metric => metric.caseId.StartsWith("race.", StringComparison.Ordinal)), Is.EqualTo(26));
            Assert.That(records.Count(metric => metric.caseId.StartsWith("summary.", StringComparison.Ordinal)), Is.EqualTo(72));
            Assert.That(records.Count(metric => metric.caseId.StartsWith("status.", StringComparison.Ordinal)), Is.EqualTo(60));

            foreach (HubTextMetric metric in records)
            {
                string caseName = metric.caseId + " / " + metric.controlName;
                Assert.That(metric.name.Length, Is.LessThanOrEqualTo(512), caseName + " name is bounded.");
                Assert.That(metric.controlName.Length, Is.LessThanOrEqualTo(512), caseName + " control name is bounded.");
                Assert.That(metric.caseId.Length, Is.LessThanOrEqualTo(512), caseName + " case id is bounded.");
                Assert.That(metric.content.Length, Is.LessThanOrEqualTo(512), caseName + " literal is bounded.");
                Assert.That(metric.measurementKind, Is.EqualTo("actualText").Or.EqualTo("contentOverride"), caseName);
                Assert.That(metric.language, Is.EqualTo("es").Or.EqualTo("en"), caseName + " language");
                Assert.That(metric.fontName, Is.Not.Empty, caseName + " font asset");
                Assert.That(metric.fontSource, Is.Not.Empty, caseName + " font source");
                Assert.That(metric.preferredGenerator, Is.EqualTo("TextGenerator.GetPreferredWidth/GetPreferredHeight"), caseName);
                Assert.That(metric.visibilityGenerator, Is.EqualTo("TextGenerator.characterCountVisible"), caseName);
                Assert.That(metric.fontSize, Is.GreaterThan(0), caseName + " font size");
                Assert.That(IsFinite(metric.anchoredX) && IsFinite(metric.anchoredY), Is.True,
                    caseName + " text position is finite.");
                Assert.That(IsFinite(metric.rectWidth) && metric.rectWidth > 0f, Is.True,
                    caseName + " text rect width is finite and positive.");
                Assert.That(IsFinite(metric.rectHeight) && metric.rectHeight > 0f, Is.True,
                    caseName + " text rect height is finite and positive.");
                Assert.That(IsFinite(metric.canvasMinX) && IsFinite(metric.canvasMinY) &&
                    IsFinite(metric.canvasMaxX) && IsFinite(metric.canvasMaxY), Is.True,
                    caseName + " transformed text bounds are finite in the Canvas space.");
                Assert.That(metric.canvasMaxX > metric.canvasMinX && metric.canvasMaxY > metric.canvasMinY, Is.True,
                    caseName + " transformed text bounds are measurable and nonempty.");
                Assert.That(IsFinite(metric.pixelsPerUnit) && metric.pixelsPerUnit > 0f, Is.True,
                    caseName + " pixels-per-unit scale is finite and positive.");
                Assert.That(IsFinite(metric.preferredWidth) && metric.preferredWidth >= 0f, Is.True,
                    caseName + " preferred width is finite.");
                Assert.That(IsFinite(metric.preferredHeight) && metric.preferredHeight >= 0f, Is.True,
                    caseName + " preferred height is finite.");
                Assert.That(metric.expectedVisibleCount,
                    Is.EqualTo(metric.content.Count(character => character != '\n' && character != '\r')),
                    caseName + " expected character count");
                Assert.That(metric.preferredHeight, Is.LessThanOrEqualTo(metric.rectHeight + 0.5f),
                    caseName + " preferred height fits its existing rect.");
                if (metric.requiresSingleLine)
                    Assert.That(metric.preferredWidth, Is.LessThanOrEqualTo(metric.rectWidth + 0.5f),
                        caseName + " preferred width fits its existing single-line rect.");
                Assert.That(metric.visibleCount, Is.GreaterThanOrEqualTo(metric.expectedVisibleCount),
                    caseName + " must display every authored character with its existing overflow mode.");
            }
        }

        private static void AddPrimaryTextMetric(
            List<HubTextMetric> records, Text text, string fallbackName, string content,
            string measurementKind, string skillId, string language, string controlName,
            string caseId, bool requiresSingleLine)
        {
            string name = text == null ? fallbackName : text.name;
            records.Add(CaptureHubTextMetric(text, name, content, measurementKind, skillId,
                language, controlName, caseId, requiresSingleLine));
        }

        private static void AddPrimaryActionMetrics(List<HubTextMetric> records)
        {
            Text text = FindExistingText("StartExpeditionButton");
            for (int actionIndex = 0; actionIndex < PrimaryActionCases.GetLength(0); actionIndex++)
            {
                for (int languageIndex = 0; languageIndex < 2; languageIndex++)
                {
                    string language = languageIndex == 0 ? "es" : "en";
                    AddPrimaryTextMetric(records, text, "StartExpeditionButtonLabel",
                        PrimaryActionCases[actionIndex, languageIndex + 1], "contentOverride", string.Empty,
                        language, "StartExpeditionButton",
                        "action." + PrimaryActionCases[actionIndex, 0] + "." + language, true);
                }
            }
        }

        private static void AddPrimaryRaceMetrics(List<HubTextMetric> records)
        {
            Text humanText = FindExistingText("RaceHumanButton");
            for (int languageIndex = 0; languageIndex < 2; languageIndex++)
            {
                bool english = languageIndex == 1;
                string language = english ? "en" : "es";
                string human = english ? "HUMAN" : "HUMANO";
                string available = english ? "AVAILABLE" : "DISPONIBLE";
                string locked = english ? "LOCKED" : "BLOQUEADO";
                string defaultState = english ? "DEFAULT" : "INICIAL";
                string fortune = english ? "FORTUNE" : "FORTUNA";
                for (int unlockedIndex = 0; unlockedIndex < 2; unlockedIndex++)
                {
                    bool unlocked = unlockedIndex == 1;
                    for (int selectedIndex = 0; selectedIndex < 2; selectedIndex++)
                    {
                        bool selected = selectedIndex == 1;
                        string content = (selected ? "◆ " : string.Empty) + human + " · " +
                            (unlocked ? available : locked) +
                            (unlocked ? " · " + defaultState + " · 2 " + fortune : string.Empty);
                        AddPrimaryTextMetric(records, humanText, "RaceHumanButtonLabel", content,
                            "contentOverride", string.Empty, language, "RaceHumanButton",
                            "race.human." + (unlocked ? "available" : "locked") + "." +
                            (selected ? "selected" : "unselected") + "." + language, false);
                    }
                }

                for (int raceIndex = 0; raceIndex < PrimaryRaceCases.GetLength(0); raceIndex++)
                {
                    string raceId = PrimaryRaceCases[raceIndex, 0];
                    string control = PrimaryRaceCases[raceIndex, 1];
                    string race = PrimaryRaceCases[raceIndex, 2];
                    string condition = PrimaryRaceCases[raceIndex, english ? 4 : 3];
                    string lockedContent = (english ? "LOCKED" : "BLOQUEADA") + " · " + race + " · " + condition;
                    string unlockedContent = race + " · " + (english ? "UNLOCKED" : "DESBLOQUEADA") + " · " + condition;
                    string selectedContent = "◆ " + unlockedContent;
                    Text text = FindExistingText(control);
                    AddPrimaryTextMetric(records, text, control + "Label", lockedContent,
                        "contentOverride", string.Empty, language, control,
                        "race." + raceId + ".locked." + language, false);
                    AddPrimaryTextMetric(records, text, control + "Label", unlockedContent,
                        "contentOverride", string.Empty, language, control,
                        "race." + raceId + ".unlocked." + language, false);
                    AddPrimaryTextMetric(records, text, control + "Label", selectedContent,
                        "contentOverride", string.Empty, language, control,
                        "race." + raceId + ".unlocked.selected." + language, false);
                }
            }
        }

        private static void AddPrimarySummaryMetrics(List<HubTextMetric> records)
        {
            Text text = FindExistingText("PreparationSelectionSummary");
            for (int languageIndex = 0; languageIndex < 2; languageIndex++)
            {
                bool english = languageIndex == 1;
                string language = english ? "en" : "es";
                string[] raceNames = english ? SummaryEnglishRaceLabels : SummarySpanishRaceLabels;
                for (int classIndex = 0; classIndex < SummaryClassLabels.Length; classIndex++)
                {
                    string[] skillNames = english ? EnglishSkillNames[classIndex] : SpanishSkillNames[classIndex];
                    for (int pairIndex = 0; pairIndex < SummarySkillPairs.GetLength(0); pairIndex++)
                    {
                        string skills = skillNames[SummarySkillPairs[pairIndex, 0]] + "  ·  " +
                            skillNames[SummarySkillPairs[pairIndex, 1]];
                        for (int raceIndex = 0; raceIndex < raceNames.Length; raceIndex++)
                        {
                            string content = (english ? "CLASS  " : "CLASE  ") + SummaryClassLabels[classIndex] +
                                (english ? "     RACE  " : "     RAZA  ") + raceNames[raceIndex] + "\n" +
                                (english ? "SKILLS  " : "HABILIDADES  ") + skills;
                            AddPrimaryTextMetric(records, text, "PreparationSelectionSummary", content,
                                "contentOverride", string.Empty, language, "PreparationSelectionSummary",
                                "summary." + language + ".class-" + classIndex + ".pair-" + pairIndex +
                                ".race-" + raceIndex, false);
                        }
                    }
                }
            }
        }

        private static void AddPrimaryStatusMetrics(List<HubTextMetric> records)
        {
            Text text = FindExistingText("HubPreparationStatus");
            for (int caseIndex = 0; caseIndex < PrimaryStatusCases.GetLength(0); caseIndex++)
            {
                for (int languageIndex = 0; languageIndex < 2; languageIndex++)
                {
                    string language = languageIndex == 0 ? "es" : "en";
                    AddPrimaryTextMetric(records, text, "HubPreparationStatus",
                        PrimaryStatusCases[caseIndex, languageIndex + 1], "contentOverride", string.Empty,
                        language, "HubPreparationStatus",
                        "status." + PrimaryStatusCases[caseIndex, 0] + "." + language, false);
                }
            }

            string[,] exactTwoCases =
            {
                { "zero", "Elige exactamente dos habilidades (0/2).", "Choose exactly two skills (0/2)." },
                { "two", "Elige exactamente dos habilidades (2/2).", "Choose exactly two skills (2/2)." }
            };
            for (int countIndex = 0; countIndex < exactTwoCases.GetLength(0); countIndex++)
            {
                for (int languageIndex = 0; languageIndex < 2; languageIndex++)
                {
                    string language = languageIndex == 0 ? "es" : "en";
                    AddPrimaryTextMetric(records, text, "HubPreparationStatus",
                        exactTwoCases[countIndex, languageIndex + 1], "contentOverride", string.Empty,
                        language, "HubPreparationStatus",
                        "status.exact_two_count." + exactTwoCases[countIndex, 0] + "." + language, false);
                }
            }

            string[,] otherReadyCases =
            {
                { "orcini", "Preparación lista · ORCINI · 110 SALUD · 55 ENERGÍA · 3 pociones", "Ready · ORCINI · 110 HEALTH · 55 ENERGY · 3 potions" },
                { "alvari", "Preparación lista · ALVARI · 110 SALUD · 55 ENERGÍA · 3 pociones", "Ready · ALVARI · 110 HEALTH · 55 ENERGY · 3 potions" },
                { "dvergar", "Preparación lista · DVERGAR · 110 SALUD · 55 ENERGÍA · 3 pociones", "Ready · DVERGAR · 110 HEALTH · 55 ENERGY · 3 potions" }
            };
            for (int raceIndex = 0; raceIndex < otherReadyCases.GetLength(0); raceIndex++)
            {
                for (int languageIndex = 0; languageIndex < 2; languageIndex++)
                {
                    string language = languageIndex == 0 ? "es" : "en";
                    AddPrimaryTextMetric(records, text, "HubPreparationStatus",
                        otherReadyCases[raceIndex, languageIndex + 1], "contentOverride", string.Empty,
                        language, "HubPreparationStatus",
                        "status.preparation.ready." + otherReadyCases[raceIndex, 0] + "." + language, false);
                }
            }
        }

        private static void LogPrimaryGeometryDiagnostics()
        {
            Canvas canvas = FindHubCanvasRaw();
            RectTransform canvasRect = canvas == null ? null : canvas.transform as RectTransform;
            Rect canvasBounds;
            bool hasCanvasBounds = TryGetCanvasSpaceBounds(canvasRect, canvasRect, out canvasBounds);
            CanvasScaler scaler = canvas == null ? null : canvas.GetComponent<CanvasScaler>();
            var canvasMetric = new HubLayoutMetric
            {
                target = "HubPreparationCanvas",
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                canvasPixelX = canvas == null ? -1f : FiniteOrSentinel(canvas.pixelRect.x),
                canvasPixelY = canvas == null ? -1f : FiniteOrSentinel(canvas.pixelRect.y),
                canvasPixelWidth = canvas == null ? -1f : FiniteOrSentinel(canvas.pixelRect.width),
                canvasPixelHeight = canvas == null ? -1f : FiniteOrSentinel(canvas.pixelRect.height),
                canvasScaleFactor = canvas == null ? -1f : FiniteOrSentinel(canvas.scaleFactor),
                referenceWidth = scaler == null ? -1f : FiniteOrSentinel(scaler.referenceResolution.x),
                referenceHeight = scaler == null ? -1f : FiniteOrSentinel(scaler.referenceResolution.y),
                canvasRectWidth = canvasRect == null ? -1f : FiniteOrSentinel(canvasRect.rect.width),
                canvasRectHeight = canvasRect == null ? -1f : FiniteOrSentinel(canvasRect.rect.height),
                boundsAvailable = hasCanvasBounds,
                minX = hasCanvasBounds ? FiniteOrSentinel(canvasBounds.xMin) : -1f,
                minY = hasCanvasBounds ? FiniteOrSentinel(canvasBounds.yMin) : -1f,
                maxX = hasCanvasBounds ? FiniteOrSentinel(canvasBounds.xMax) : -1f,
                maxY = hasCanvasBounds ? FiniteOrSentinel(canvasBounds.yMax) : -1f
            };
            Debug.Log("[E10.HUB.PRIMARY.LAYOUT-METRIC] " + JsonUtility.ToJson(canvasMetric));

            foreach (PrimaryGeometryCase expected in PrimaryGeometryCases)
                LogPrimaryRectDiagnostic(expected.Name, canvasRect, "approved-control");

            string[] neighbors =
            {
                "HubTitle", "HubSubtitle", "ClassSectionTitle", "SkillSectionTitle",
                "ClassBellatorButton", "ClassVenatorButton", "ClassMagusButton", "OpenGuildIntroButton",
                "OpenContractsButton", "HubReturnToMenuButton", "GuildIntroBackButton"
            };
            foreach (string name in neighbors)
                LogPrimaryRectDiagnostic(name, canvasRect, "preserved-neighbor");

            foreach (PrimaryGeometryCase expected in PrimaryGeometryCases.Where(item => item.IsButton))
            {
                RectTransform buttonRect = FindHubRect(expected.Name);
                Text label = FindExistingText(expected.Name);
                RectTransform labelRect = label == null ? null : label.rectTransform;
                Rect buttonBounds;
                Rect labelBounds;
                bool buttonAvailable = TryGetCanvasSpaceBounds(buttonRect, canvasRect, out buttonBounds);
                bool labelAvailable = TryGetCanvasSpaceBounds(labelRect, canvasRect, out labelBounds);
                var labelMetric = new HubLayoutMetric
                {
                    target = expected.Name + ".label",
                    boundsAvailable = buttonAvailable && labelAvailable,
                    minX = labelAvailable ? FiniteOrSentinel(labelBounds.xMin) : -1f,
                    minY = labelAvailable ? FiniteOrSentinel(labelBounds.yMin) : -1f,
                    maxX = labelAvailable ? FiniteOrSentinel(labelBounds.xMax) : -1f,
                    maxY = labelAvailable ? FiniteOrSentinel(labelBounds.yMax) : -1f
                };
                Debug.Log("[E10.HUB.PRIMARY.LAYOUT-METRIC] " + JsonUtility.ToJson(labelMetric));
            }
        }

        private static void LogPrimaryRectDiagnostic(string name, RectTransform canvasRect, string kind)
        {
            RectTransform rect = FindHubRect(name);
            Rect bounds;
            bool available = TryGetCanvasSpaceBounds(rect, canvasRect, out bounds);
            var metric = new HubLayoutMetric
            {
                target = name,
                kind = kind,
                boundsAvailable = available,
                anchoredX = rect == null ? -1f : FiniteOrSentinel(rect.anchoredPosition.x),
                anchoredY = rect == null ? -1f : FiniteOrSentinel(rect.anchoredPosition.y),
                rectWidth = rect == null ? -1f : FiniteOrSentinel(rect.rect.width),
                rectHeight = rect == null ? -1f : FiniteOrSentinel(rect.rect.height),
                minX = available ? FiniteOrSentinel(bounds.xMin) : -1f,
                minY = available ? FiniteOrSentinel(bounds.yMin) : -1f,
                maxX = available ? FiniteOrSentinel(bounds.xMax) : -1f,
                maxY = available ? FiniteOrSentinel(bounds.yMax) : -1f
            };
            Debug.Log("[E10.HUB.PRIMARY.LAYOUT-METRIC] " + JsonUtility.ToJson(metric));
        }

        private static void AssertApprovedPrimaryGeometry(Canvas canvas)
        {
            Assert.That(canvas, Is.Not.Null, "The Hub primary Canvas is required for transformed geometry checks.");
            RectTransform canvasRect = canvas.transform as RectTransform;
            Assert.That(canvasRect, Is.Not.Null, "The Hub Canvas must expose its real RectTransform.");
            Rect canvasBounds;
            Assert.That(TryGetCanvasSpaceBounds(canvasRect, canvasRect, out canvasBounds), Is.True,
                "The real Canvas bounds must be measurable in its own transformed space.");
            Assert.That(IsFinite(canvasBounds.width) && canvasBounds.width > 0f &&
                IsFinite(canvasBounds.height) && canvasBounds.height > 0f, Is.True,
                "The real Canvas bounds must be finite and positive.");

            RectTransform skillTitleRect = FindHubRect("SkillSectionTitle");
            Assert.That(skillTitleRect, Is.Not.Null, "SkillSectionTitle must remain a real scene control.");
            Rect skillTitleBounds;
            Assert.That(TryGetCanvasSpaceBounds(skillTitleRect, canvasRect, out skillTitleBounds), Is.True);
            var placedBounds = new List<Rect>(PrimaryGeometryCases.Length);
            foreach (PrimaryGeometryCase expected in PrimaryGeometryCases)
            {
                RectTransform rect = FindHubRect(expected.Name);
                Assert.That(rect, Is.Not.Null, expected.Name + " must reuse its existing scene RectTransform.");
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)), expected.Name + " anchor min");
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)), expected.Name + " anchor max");
                Assert.That(rect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)), expected.Name + " pivot");
                Assert.That(Mathf.Abs(rect.anchoredPosition.x - expected.Position.x), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " anchored x");
                Assert.That(Mathf.Abs(rect.anchoredPosition.y - expected.Position.y), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " anchored y");
                Assert.That(Mathf.Abs(rect.rect.width - expected.Size.x), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " local width");
                Assert.That(Mathf.Abs(rect.rect.height - expected.Size.y), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " local height");

                Rect bounds;
                Assert.That(TryGetCanvasSpaceBounds(rect, canvasRect, out bounds), Is.True,
                    expected.Name + " transformed bounds must be measured in Canvas space.");
                Assert.That(Mathf.Abs(bounds.center.x - expected.Position.x), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " Canvas-space center x");
                Assert.That(Mathf.Abs(bounds.center.y - expected.Position.y), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " Canvas-space center y");
                Assert.That(Mathf.Abs(bounds.width - expected.Size.x), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " Canvas-space width");
                Assert.That(Mathf.Abs(bounds.height - expected.Size.y), Is.LessThanOrEqualTo(0.5f),
                    expected.Name + " Canvas-space height");
                AssertRectContained(bounds, canvasBounds, 0.5f,
                    expected.Name + " must remain inside the real Canvas bounds.");
                AssertRectsSeparated(bounds, skillTitleBounds,
                    expected.Name + " must remain separated from SkillSectionTitle.");
                foreach (Rect previous in placedBounds)
                    AssertRectsSeparated(bounds, previous,
                        expected.Name + " must remain separated from every other approved control.");
                placedBounds.Add(bounds);

                if (expected.IsButton)
                {
                    Text label = FindExistingText(expected.Name);
                    Assert.That(label, Is.Not.Null, expected.Name + " must retain its existing child label.");
                    Rect labelBounds;
                    Assert.That(TryGetCanvasSpaceBounds(label.rectTransform, canvasRect, out labelBounds), Is.True,
                        expected.Name + " label bounds must use the same Canvas space.");
                    AssertRectContained(labelBounds, bounds, 0.5f,
                        expected.Name + " child label must remain inside its button bounds.");
                }
            }
        }

        private static void AssertRectContained(Rect inner, Rect outer, float tolerance, string message)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), message + " left");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), message + " bottom");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), message + " right");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), message + " top");
        }

        private static void AssertRectsSeparated(Rect first, Rect second, string message)
        {
            bool separated = first.xMax <= second.xMin || second.xMax <= first.xMin ||
                first.yMax <= second.yMin || second.yMax <= first.yMin;
            Assert.That(separated, Is.True, message);
        }

        private static bool TryGetCanvasSpaceBounds(
            RectTransform rect, RectTransform canvasRoot, out Rect bounds)
        {
            bounds = new Rect(-1f, -1f, -1f, -1f);
            if (rect == null || canvasRoot == null) return false;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector3 first = canvasRoot.InverseTransformPoint(corners[0]);
            float minX = first.x;
            float maxX = first.x;
            float minY = first.y;
            float maxY = first.y;
            for (int index = 1; index < corners.Length; index++)
            {
                Vector3 point = canvasRoot.InverseTransformPoint(corners[index]);
                if (!IsFinite(point.x) || !IsFinite(point.y)) return false;
                minX = Mathf.Min(minX, point.x);
                maxX = Mathf.Max(maxX, point.x);
                minY = Mathf.Min(minY, point.y);
                maxY = Mathf.Max(maxY, point.y);
            }
            bounds = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return IsFinite(minX) && IsFinite(minY) && IsFinite(maxX) && IsFinite(maxY);
        }

        private static Text FindExistingText(string name)
        {
            RectTransform rect = FindHubRect(name);
            GameObject target = rect == null ? GameObject.Find(name) : rect.gameObject;
            if (target == null) return null;
            Text text = target.GetComponent<Text>();
            return text != null ? text : target.GetComponentInChildren<Text>(true);
        }

        private static RectTransform FindHubRect(string name)
        {
            Canvas canvas = FindHubCanvasRaw();
            if (canvas == null) return null;
            return canvas.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(rect => rect.gameObject.name == name);
        }

        private static Canvas FindHubCanvasRaw()
        {
            GameObject target = GameObject.Find("HubPreparationCanvas");
            if (target != null) return target.GetComponent<Canvas>();
            return UnityEngine.Object.FindAnyObjectByType<Canvas>();
        }

        private static string PrimaryTextControlName(string textObjectName)
        {
            return textObjectName != null && textObjectName.EndsWith("Label", StringComparison.Ordinal)
                ? textObjectName.Substring(0, textObjectName.Length - "Label".Length)
                : textObjectName;
        }

        private static HubTextMetric CaptureHubTextMetric(
            Text text, string name, string content, string measurementKind, string skillId, string language,
            string controlName = null, string caseId = null, bool requiresSingleLine = false)
        {
            var metric = new HubTextMetric
            {
                name = name ?? string.Empty,
                content = content ?? string.Empty,
                measurementKind = measurementKind ?? string.Empty,
                skillId = skillId ?? string.Empty,
                language = language ?? string.Empty,
                controlName = controlName ?? name ?? string.Empty,
                caseId = caseId ?? (measurementKind ?? string.Empty) + "." + (name ?? string.Empty),
                requiresSingleLine = requiresSingleLine,
                fontName = string.Empty,
                fontSource = string.Empty,
                preferredGenerator = "TextGenerator.GetPreferredWidth/GetPreferredHeight",
                visibilityGenerator = "TextGenerator.characterCountVisible",
                fontSize = -1,
                anchoredX = -1f,
                anchoredY = -1f,
                rectWidth = -1f,
                rectHeight = -1f,
                canvasMinX = -1f,
                canvasMinY = -1f,
                canvasMaxX = -1f,
                canvasMaxY = -1f,
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
            metric.fontSource = text.font == null ? string.Empty : "Text.font:" + text.font.name;
            metric.fontSize = text.fontSize;
            metric.anchoredX = FiniteOrSentinel(rect.anchoredPosition.x);
            metric.anchoredY = FiniteOrSentinel(rect.anchoredPosition.y);
            metric.rectWidth = FiniteOrSentinel(extents.x);
            metric.rectHeight = FiniteOrSentinel(extents.y);
            metric.pixelsPerUnit = FiniteOrSentinel(pixelsPerUnit);
            Canvas canvas = FindHubCanvasRaw();
            RectTransform canvasRoot = canvas == null ? null : canvas.transform as RectTransform;
            Rect canvasBounds;
            if (TryGetCanvasSpaceBounds(rect, canvasRoot, out canvasBounds))
            {
                metric.canvasMinX = FiniteOrSentinel(canvasBounds.xMin);
                metric.canvasMinY = FiniteOrSentinel(canvasBounds.yMin);
                metric.canvasMaxX = FiniteOrSentinel(canvasBounds.xMax);
                metric.canvasMaxY = FiniteOrSentinel(canvasBounds.yMax);
            }

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
                "OpenGuildIntroButton", "OpenGuildIntroButtonLabel", "OpenContractsButton", "GuildIntroBackButton"
            };
            return names.Select(CaptureUiElement).ToArray();
        }

        private static UiElementSnapshot CaptureUiElement(string name)
        {
            RectTransform rect = FindHubRect(name);
            GameObject target = rect == null ? null : rect.gameObject;
            Assert.That(target, Is.Not.Null, "Could not capture Hub layout object " + name + ".");
            Assert.That(rect, Is.Not.Null, name + " must retain its scene RectTransform.");
            return new UiElementSnapshot(name, target, rect);
        }

        private static void AssertLayoutUnchanged(UiElementSnapshot[] snapshots)
        {
            foreach (UiElementSnapshot snapshot in snapshots)
            {
                RectTransform currentRect = FindHubRect(snapshot.Name);
                Assert.That(currentRect == null ? null : currentRect.gameObject,
                    Is.SameAs(snapshot.Target), snapshot.Name + " scene object");
                Assert.That(currentRect, Is.SameAs(snapshot.Rect), snapshot.Name + " rect object");
                Assert.That(snapshot.Rect.anchoredPosition, Is.EqualTo(snapshot.AnchoredPosition), snapshot.Name + " position");
                Assert.That(snapshot.Rect.sizeDelta, Is.EqualTo(snapshot.SizeDelta), snapshot.Name + " size");
                Assert.That(snapshot.Rect.anchorMin, Is.EqualTo(snapshot.AnchorMin), snapshot.Name + " anchor min");
                Assert.That(snapshot.Rect.anchorMax, Is.EqualTo(snapshot.AnchorMax), snapshot.Name + " anchor max");
                Assert.That(snapshot.Rect.pivot, Is.EqualTo(snapshot.Pivot), snapshot.Name + " pivot");
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
        private sealed class HubLayoutMetric
        {
            public string target;
            public string kind;
            public int screenWidth;
            public int screenHeight;
            public float canvasPixelX;
            public float canvasPixelY;
            public float canvasPixelWidth;
            public float canvasPixelHeight;
            public float canvasScaleFactor;
            public float referenceWidth;
            public float referenceHeight;
            public float canvasRectWidth;
            public float canvasRectHeight;
            public float anchoredX;
            public float anchoredY;
            public float rectWidth;
            public float rectHeight;
            public bool boundsAvailable;
            public float minX;
            public float minY;
            public float maxX;
            public float maxY;
        }

        private sealed class PrimaryGeometryCase
        {
            public PrimaryGeometryCase(string name, Vector2 position, Vector2 size, bool isButton)
            {
                Name = name;
                Position = position;
                Size = size;
                IsButton = isButton;
            }

            public string Name { get; }
            public Vector2 Position { get; }
            public Vector2 Size { get; }
            public bool IsButton { get; }
        }

        [Serializable]
        private sealed class HubTextMetric
        {
            public string name;
            public string content;
            public string measurementKind;
            public string skillId;
            public string language;
            public string controlName;
            public string caseId;
            public bool requiresSingleLine;
            public string fontName;
            public string fontSource;
            public string preferredGenerator;
            public string visibilityGenerator;
            public int fontSize;
            public float anchoredX;
            public float anchoredY;
            public float rectWidth;
            public float rectHeight;
            public float canvasMinX;
            public float canvasMinY;
            public float canvasMaxX;
            public float canvasMaxY;
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
                Pivot = rect.pivot;
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
            public Vector2 Pivot { get; }
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
