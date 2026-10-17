using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityRandom = UnityEngine.Random;

namespace Luckbound.Tests.PlayMode
{
    public sealed class HubPreparationTests
    {
        [TestCase("class.bellator", "weapon.bellator.rusted_sword", "armor.bellator.guild_cuirass", "skill.bellator.circular_cut")]
        [TestCase("class.venator", "weapon.venator.short_bow", "armor.venator.leather_vest", "skill.venator.piercing_arrow")]
        [TestCase("class.magus", "weapon.magus.ember_staff", "armor.magus.traveler_robe", "skill.magus.fireball")]
        public void D05RunFactoryBuildsTheClassKitAndAcceptsOnlyTwoClassSkills(
            string classId, string weaponId, string armorId, string firstSkillId)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create(classId,
                ExpeditionRunConfiguration.HumanRaceId, new[] { firstSkillId, SecondSkillFor(classId) });

            Assert.That(run.RunId.IsValid, Is.True);
            Assert.That(run.Inventory.ClassId, Is.EqualTo(classId));
            Assert.That(run.Inventory.Weapon.ItemId, Is.EqualTo(weaponId));
            Assert.That(run.Inventory.Armor.ItemId, Is.EqualTo(armorId));
            Assert.That(run.Inventory.QuickConsumable.ItemId, Is.EqualTo("consumable.health_potion"));
            Assert.That(run.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
            Assert.That(run.StartingEquipmentInstanceIds, Is.EquivalentTo(new[]
                { run.Inventory.Weapon.InstanceId, run.Inventory.Armor.InstanceId }));
            Assert.That(run.Route.Seed, Is.EqualTo(run.Seed));
            Assert.That(run.Route.Nodes.Count, Is.EqualTo(24));
            Assert.That(run.StartingHealth, Is.EqualTo(100));
            Assert.That(run.StartingEnergy, Is.EqualTo(50));
            Assert.That(run.StartingFortunaCharges, Is.EqualTo(2));
            Assert.Throws<System.ArgumentException>(() => ExpeditionRunConfigurationFactory.Create(classId,
                ExpeditionRunConfiguration.HumanRaceId, new[] { firstSkillId, firstSkillId }));
            Assert.Throws<System.ArgumentException>(() => ExpeditionRunConfigurationFactory.Create(classId,
                ExpeditionRunConfiguration.HumanRaceId, new[] { firstSkillId }));
            Assert.Throws<System.ArgumentException>(() => ExpeditionRunConfigurationFactory.Create(classId,
                ExpeditionRunConfiguration.HumanRaceId, new[] { firstSkillId,
                    classId == "class.bellator" ? "skill.venator.piercing_arrow" : "skill.bellator.guard" }));
            Assert.Throws<System.ArgumentException>(() => ExpeditionRunConfigurationFactory.Create(classId,
                "race.unknown", new[] { firstSkillId, SecondSkillFor(classId) }));
        }

        [TestCase(ExpeditionRunConfiguration.HumanRaceId, 2)]
        [TestCase(ExpeditionRunConfiguration.OrciniRaceId, 1)]
        [TestCase(ExpeditionRunConfiguration.AlvariRaceId, 1)]
        [TestCase(ExpeditionRunConfiguration.DvergarRaceId, 1)]
        public void E09RunConfigurationAcceptsKnownRacesAndAssignsStartingFortuna(string raceId, int expectedFortunaCharges)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator", raceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });

            Assert.That(run.RaceId, Is.EqualTo(raceId));
            Assert.That(run.StartingFortunaCharges, Is.EqualTo(expectedFortunaCharges));
        }

        [Test]
        public void E09RunConfigurationRejectsUnknownRaceIds()
        {
            Assert.Throws<System.ArgumentException>(() => ExpeditionRunConfigurationFactory.Create("class.bellator",
                "race.unknown", new[] { "skill.bellator.circular_cut", "skill.bellator.guard" }));
        }

        [UnityTest]
        public IEnumerator D05_TavernSelectionStartsANewRunAndDoesNotInheritPriorExpeditionState()
        {
            const int scenarioSeed = 27182819;
            UnityRandom.State previousRandomState = UnityRandom.state;
            var evidence = new ScenarioEvidenceRecorder("D05", "D05_TavernSelectionAndFreshRun", scenarioSeed);
            try
            {
                UnityRandom.InitState(scenarioSeed);
                Time.timeScale = 1f;
                evidence.RecordEvent("Load the real application bootstrap and enter Hub using the generated menu control.");
                evidence.RecordInput(0, "LoadScene", "Bootstrap");
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Menu"; frame++) yield return null;

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                evidence.RecordAssertion("Bootstrap owns the persistent application root", app != null,
                    "one ApplicationBootstrapper", app == null ? "missing" : "present");
                if (app == null)
                {
                    int failure = evidence.Complete();
                    Assert.That(failure, Is.Zero, "Application root missing. Evidence: " + evidence.ReportPath);
                    yield break;
                }

                ExpeditionRoomAmbienceRouter roomAmbienceRouter = app.GetComponent<ExpeditionRoomAmbienceRouter>();
                evidence.RecordAssertion("Bootstrap owns persistent room ambience routing", roomAmbienceRouter != null,
                    "one ExpeditionRoomAmbienceRouter", roomAmbienceRouter == null ? "missing" : "present");
                app.ClearActiveRun();

                Button tavernButton = FindButton("StartTavernButton");
                evidence.RecordAssertion("Menu presents the tavern preparation route", tavernButton != null,
                    "StartTavernButton", tavernButton == null ? "missing" : "present");
                if (tavernButton == null)
                {
                    int failure = evidence.Complete();
                    Assert.That(failure, Is.Zero, "Menu control missing. Evidence: " + evidence.ReportPath);
                    yield break;
                }
                evidence.RecordInput(1, "UIButton", "StartTavernButton.onClick");
                tavernButton.onClick.Invoke();
                for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Hub"; frame++) yield return null;

                HubPreparationController preparation = Object.FindAnyObjectByType<HubPreparationController>();
                evidence.RecordAssertion("Hub creates the class and skill selection controller", preparation != null,
                    "one HubPreparationController", preparation == null ? "missing" : "present");
                if (preparation == null)
                {
                    int failure = evidence.Complete();
                    Assert.That(failure, Is.Zero, "Hub controller missing. Evidence: " + evidence.ReportPath);
                    yield break;
                }

                Button[] lockedRaces = { FindButton("RaceOrciniButton"), FindButton("RaceAlvariButton"), FindButton("RaceDvergarButton") };
                bool racesLocked = lockedRaces.All(button => button != null && !button.interactable &&
                    button.GetComponentInChildren<Text>(true) != null &&
                    button.GetComponentInChildren<Text>(true).text.Contains("BLOQUEADA"));
                evidence.RecordAssertion("Uncontracted races remain locked", racesLocked,
                    "Orcini, Alvari and Dvergar buttons disabled", string.Join(", ", lockedRaces.Select(button => button == null ? "missing" : button.interactable.ToString())));
                bool defaultRaceIsHuman = preparation.SelectedRaceId == ExpeditionRunConfiguration.HumanRaceId &&
                    app.Profile.unlockedRaceIds.SequenceEqual(new[] { ExpeditionRunConfiguration.HumanRaceId });
                foreach (Button lockedRace in lockedRaces) lockedRace.onClick.Invoke();
                bool lockedRaceClicksRejected = preparation.SelectedRaceId == ExpeditionRunConfiguration.HumanRaceId &&
                    app.ActiveRun == null && SceneManager.GetActiveScene().name == "Hub";
                evidence.RecordAssertion("A new profile selects Human only and programmatic clicks on locked races do not start a run",
                    defaultRaceIsHuman && lockedRaceClicksRejected, "Human selection, no active run, still in Hub",
                    preparation.SelectedRaceId + "; active run=" + (app.ActiveRun != null) + "; scene=" + SceneManager.GetActiveScene().name);
                Text initialSummary = FindText("PreparationSelectionSummary");
                Text initialStatus = FindText("HubPreparationStatus");
                Assert.That(initialSummary != null && initialSummary.text.Contains("HUMANO"), Is.True);
                Assert.That(initialStatus != null && initialStatus.text.Contains("bloqueada"), Is.True,
                    "Rejected clicks on locked races should explain the block without changing the selected Human race.");

                evidence.RecordInput(2, "UIButton", "ClassMagusButton.onClick");
                FindButton("ClassMagusButton").onClick.Invoke();
                bool magusPairSelected = preparation.SelectedClassId == "class.magus" && preparation.SelectedSkillIds.Count == 2 &&
                    preparation.SelectedSkillIds.All(id => id.StartsWith("skill.magus.", System.StringComparison.Ordinal));
                evidence.RecordAssertion("Selecting Magus selects two of its own available skills", magusPairSelected,
                    "class.magus with exactly two skill.magus IDs", preparation.SelectedClassId + " [" + string.Join(",", preparation.SelectedSkillIds) + "]");
                evidence.RecordInput(3, "UIButton", "StartExpeditionButton.onClick");
                FindButton("StartExpeditionButton").onClick.Invoke();
                for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Expedition"; frame++) yield return null;

                ExpeditionRunConfiguration firstRun = app.ActiveRun;
                evidence.RecordAssertion("Starting the expedition applies the selected class and route", firstRun != null &&
                    SceneManager.GetActiveScene().name == "Expedition" && firstRun.ClassId == "class.magus" &&
                    firstRun.RaceId == ExpeditionRunConfiguration.HumanRaceId &&
                    app.ActiveExpedition.raceId == ExpeditionRunConfiguration.HumanRaceId && firstRun.Route.Nodes.Count == 24,
                    "Expedition with Magus and 24-node generated route",
                    firstRun == null ? "no active run" : SceneManager.GetActiveScene().name + " " + firstRun.ClassId +
                    " " + firstRun.RaceId + " " + firstRun.Route.Nodes.Count + " nodes");
                if (firstRun == null)
                {
                    int failure = evidence.Complete();
                    Assert.That(failure, Is.Zero, "First run missing. Evidence: " + evidence.ReportPath);
                    yield break;
                }

                InventoryItemStack firstPotion = firstRun.Inventory.QuickConsumable;
                bool magusKitApplied = firstRun.Inventory.Weapon.ItemId == "weapon.magus.ember_staff" &&
                    firstRun.Inventory.Armor.ItemId == "armor.magus.traveler_robe" && firstPotion != null && firstPotion.Quantity == 2;
                evidence.RecordAssertion("Magus receives the catalog-defined kit and two quick health potions", magusKitApplied,
                    "ember staff, traveler robe and 2 health potions",
                    firstRun.Inventory.Weapon.ItemId + ", " + firstRun.Inventory.Armor.ItemId + ", potions=" + (firstPotion == null ? 0 : firstPotion.Quantity));
                evidence.RecordAssertion("Selected skills and Human starting stats are part of the run", firstRun.SkillIds.Count == 2 &&
                    firstRun.SkillIds.All(id => id.StartsWith("skill.magus.", System.StringComparison.Ordinal)) &&
                    firstRun.StartingHealth == 100 && firstRun.StartingEnergy == 50 && firstRun.StartingFortunaCharges == 2,
                    "two Magus skills, 100 health, 50 energy, 2 Fortuna", string.Join(",", firstRun.SkillIds) +
                    "; health=" + firstRun.StartingHealth + "; energy=" + firstRun.StartingEnergy + "; Fortuna=" + firstRun.StartingFortunaCharges);

                RunId firstRunId = firstRun.RunId;
                ItemInstanceId firstWeaponId = firstRun.Inventory.Weapon.InstanceId;
                bool firstRunMutation = firstRun.Inventory.TryAddGold(73, TransactionId.New()).Succeeded &&
                    firstRun.Inventory.TryAddMaterial(MaterialKind.Ore, 4, TransactionId.New()).Succeeded;
                evidence.RecordAssertion("The first run can acquire mutable expedition-only state", firstRunMutation &&
                    firstRun.Inventory.Gold == 73 && firstRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 4,
                    "73 gold and 4 Ore in run one", "gold=" + firstRun.Inventory.Gold + "; ore=" + firstRun.Inventory.GetMaterialCount(MaterialKind.Ore));

                Button returnButton = FindButton("ReturnToTavernButton");
                evidence.RecordAssertion("The run receipt allows a return to the tavern", returnButton != null,
                    "ReturnToTavernButton", returnButton == null ? "missing" : "present");
                if (returnButton == null)
                {
                    int failure = evidence.Complete();
                    Assert.That(failure, Is.Zero, "Tavern return missing. Evidence: " + evidence.ReportPath);
                    yield break;
                }
                evidence.RecordInput(4, "UIButton", "ReturnToTavernButton.onClick");
                returnButton.onClick.Invoke();
                for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Hub"; frame++) yield return null;

                Task<ExpeditionSettlementCommitResult> abandonFirstRun = app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0);
                while (!abandonFirstRun.IsCompleted) yield return null;
                Assert.That(abandonFirstRun.Result.Persisted, Is.True, abandonFirstRun.Result.Message);
                app.ClearActiveRun();

                HubPreparationController secondPreparation = Object.FindAnyObjectByType<HubPreparationController>();
                bool resetSelection = secondPreparation != null && secondPreparation.SelectedClassId == "class.bellator" &&
                    secondPreparation.SelectedSkillIds.Count == 2 && secondPreparation.SelectedSkillIds.All(id => id.StartsWith("skill.bellator.", System.StringComparison.Ordinal));
                evidence.RecordAssertion("Returning to the tavern starts from its default preparation rather than reusing Magus selection", resetSelection,
                    "Bellator with two Bellator skills", secondPreparation == null ? "missing controller" : secondPreparation.SelectedClassId + " [" + string.Join(",", secondPreparation.SelectedSkillIds) + "]");
                if (!resetSelection)
                {
                    int failure = evidence.Complete();
                    Assert.That(failure, Is.Zero, "Second preparation did not reset. Evidence: " + evidence.ReportPath);
                    yield break;
                }

                evidence.RecordInput(5, "UIButton", "ClassVenatorButton.onClick");
                FindButton("ClassVenatorButton").onClick.Invoke();
                evidence.RecordInput(6, "UIButton", "StartExpeditionButton.onClick");
                FindButton("StartExpeditionButton").onClick.Invoke();
                for (int frame = 0; frame < 30 && SceneManager.GetActiveScene().name != "Expedition"; frame++) yield return null;

                ExpeditionRunConfiguration secondRun = app.ActiveRun;
                bool newRunCreated = secondRun != null && secondRun != firstRun && secondRun.RunId != firstRunId &&
                    secondRun.ClassId == "class.venator" && secondRun.SkillIds.Count == 2 &&
                    secondRun.SkillIds.All(id => id.StartsWith("skill.venator.", System.StringComparison.Ordinal));
                evidence.RecordAssertion("The next expedition replaces the previous run ID and applies the new class and skills", newRunCreated,
                    "fresh RunId, Venator, two skill.venator IDs", secondRun == null ? "no active run" : secondRun.RunId + " " + secondRun.ClassId + " [" + string.Join(",", secondRun.SkillIds) + "]");

                if (secondRun != null)
                {
                    InventoryItemStack secondPotion = secondRun.Inventory.QuickConsumable;
                    bool freshInventory = secondRun.Inventory.Gold == 0 && secondRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 0 &&
                        secondRun.Inventory.Weapon.ItemId == "weapon.venator.short_bow" &&
                        secondRun.Inventory.Armor.ItemId == "armor.venator.leather_vest" && secondPotion != null &&
                        secondPotion.Quantity == 2 && secondRun.Inventory.Weapon.InstanceId != firstWeaponId;
                    evidence.RecordAssertion("Gold, materials, equipment instances and consumables are created fresh", freshInventory,
                        "0 gold, 0 Ore, short bow, leather vest, new instance IDs and 2 potions",
                        "gold=" + secondRun.Inventory.Gold + "; ore=" + secondRun.Inventory.GetMaterialCount(MaterialKind.Ore) +
                        "; weapon=" + (secondRun.Inventory.Weapon?.ItemId ?? "none") + "; armor=" + (secondRun.Inventory.Armor?.ItemId ?? "none") +
                        "; potion=" + (secondPotion == null ? "none" : secondPotion.Quantity.ToString()));
                    evidence.RecordAssertion("The new route belongs to its new seed", secondRun.Route.Seed == secondRun.Seed && secondRun.Seed != 0,
                        "normalized run seed equals route seed", secondRun.Seed.ToString("X8") + " / " + secondRun.Route.Seed.ToString("X8"));
                }

                Task<ExpeditionSettlementCommitResult> abandonSecondRun = app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0);
                while (!abandonSecondRun.IsCompleted) yield return null;
                evidence.RecordAssertion("The scenario settles its final run so later tests start from a clean profile",
                    abandonSecondRun.Result.Persisted && !app.HasActiveExpedition, "no active run remains on disk", abandonSecondRun.Result.Message);

                int exitCode = evidence.Complete();
                Assert.That(exitCode, Is.Zero, "The tavern scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                if (ApplicationBootstrapper.Instance != null) ApplicationBootstrapper.Instance.ClearActiveRun();
                UnityRandom.state = previousRandomState;
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator E09_UnlockedRaceSelectionSurvivesClassChangeAndIsPersisted()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE09", System.Guid.NewGuid().ToString("N"));
            FileSaveStore testStore = new FileSaveStore(saveDirectory, null);
            System.Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
            Time.timeScale = 1f;
            try
            {
                ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
                profile.unlockedRaceIds = new[]
                {
                    ExpeditionRunConfiguration.HumanRaceId,
                    ExpeditionRunConfiguration.OrciniRaceId,
                    ExpeditionRunConfiguration.AlvariRaceId,
                    ExpeditionRunConfiguration.DvergarRaceId
                };
                var seedRepository = new SaveRepository(testStore);
                Task<SaveWriteResult> seededProfile = seedRepository.SaveProfileAsync(profile);
                while (!seededProfile.IsCompleted) yield return null;
                Assert.That(seededProfile.Result.Succeeded, Is.True, seededProfile.Result.Message);

                if (app != null) Object.Destroy(app.gameObject);
                yield return null;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => testStore;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 &&
                    (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady); frame++)
                    yield return null;
                app = ApplicationBootstrapper.Instance;
                Assert.That(app != null && app.SaveDataReady, Is.True, "Bootstrap should load the seeded profile.");
                Assert.That(app.Profile.unlockedRaceIds, Is.EquivalentTo(profile.unlockedRaceIds));

                SceneManager.LoadScene("Hub", LoadSceneMode.Single);
                HubPreparationController preparation = null;
                for (int frame = 0; frame < 60; frame++)
                {
                    preparation = Object.FindAnyObjectByType<HubPreparationController>();
                    if (preparation != null && preparation.SelectedSkillIds.Count == 2) break;
                    yield return null;
                }
                Assert.That(preparation, Is.Not.Null);
                Assert.That(preparation.SelectedRaceId, Is.EqualTo(ExpeditionRunConfiguration.HumanRaceId));

                Button orciniButton = FindButton("RaceOrciniButton");
                Button alvariButton = FindButton("RaceAlvariButton");
                Button dvergarButton = FindButton("RaceDvergarButton");
                Assert.That(new[] { orciniButton, alvariButton, dvergarButton }.All(button => button != null && button.interactable), Is.True);
                Assert.That(new[] { orciniButton, alvariButton, dvergarButton }.All(button =>
                    button.GetComponentInChildren<Text>(true) != null &&
                    button.GetComponentInChildren<Text>(true).text.Contains("DESBLOQUEADA")), Is.True);
                orciniButton.onClick.Invoke();
                Assert.That(preparation.SelectedRaceId, Is.EqualTo(ExpeditionRunConfiguration.OrciniRaceId));
                alvariButton.onClick.Invoke();
                Assert.That(preparation.SelectedRaceId, Is.EqualTo(ExpeditionRunConfiguration.AlvariRaceId));
                dvergarButton.onClick.Invoke();
                Assert.That(preparation.SelectedRaceId, Is.EqualTo(ExpeditionRunConfiguration.DvergarRaceId));

                FindButton("ClassMagusButton").onClick.Invoke();
                Assert.That(preparation.SelectedClassId, Is.EqualTo("class.magus"));
                Assert.That(preparation.SelectedRaceId, Is.EqualTo(ExpeditionRunConfiguration.DvergarRaceId));
                Text summary = FindText("PreparationSelectionSummary");
                Text status = FindText("HubPreparationStatus");
                Assert.That(summary != null && summary.text.Contains("DVERGAR"), Is.True);
                Assert.That(status != null && status.text.Contains("DVERGAR"), Is.True);

                FindButton("StartExpeditionButton").onClick.Invoke();
                for (int frame = 0; frame < 120 &&
                    (SceneManager.GetActiveScene().name != "Expedition" || app.ActiveRun == null); frame++)
                    yield return null;
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Expedition"));
                Assert.That(app.ActiveRun, Is.Not.Null);
                Assert.That(app.ActiveRun.RaceId, Is.EqualTo(ExpeditionRunConfiguration.DvergarRaceId));
                Assert.That(app.ActiveExpedition.raceId, Is.EqualTo(ExpeditionRunConfiguration.DvergarRaceId));
                ExpeditionRunSessionPresenter sessionPresenter = Object.FindAnyObjectByType<ExpeditionRunSessionPresenter>();
                Assert.That(sessionPresenter, Is.Not.Null, "Expedition scene should show the prepared run summary.");
                Assert.That(sessionPresenter.SummaryText, Does.Contain("RAZA · DVERGAR"));

                ProfileSaveEnvelope persisted = JsonUtility.FromJson<ProfileSaveEnvelope>(
                    File.ReadAllText(testStore.GetPrimaryPath(SaveDocument.Profile)));
                Assert.That(persisted.hasActiveExpedition, Is.True);
                Assert.That(persisted.activeExpedition.raceId, Is.EqualTo(ExpeditionRunConfiguration.DvergarRaceId));

                Task<ExpeditionSettlementCommitResult> cleanup = app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0);
                while (!cleanup.IsCompleted) yield return null;
                Assert.That(cleanup.Result.Persisted, Is.True, cleanup.Result.Message);
            }
            finally
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
                app = ApplicationBootstrapper.Instance;
                if (app != null) Object.Destroy(app.gameObject);
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
                Time.timeScale = 1f;
            }
        }

        [UnityTest]
        public IEnumerator D09_BootstrapRestoresCheckpointCompletionAndNextRoomAcrossRestarts()
        {
            const int scenarioSeed = 27182818;
            UnityRandom.State previousRandomState = UnityRandom.state;
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundD09", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(saveDirectory);
            var evidence = new ScenarioEvidenceRecorder("D09", "D09_BootstrapResumeAndRoomProgress", scenarioSeed);
            var testStore = new BlockingProfileWriteStore(new FileSaveStore(saveDirectory, null));
            try
            {
                UnityRandom.InitState(scenarioSeed);
                Time.timeScale = 1f;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => testStore;
                if (ApplicationBootstrapper.Instance != null) Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;

                evidence.RecordInput(0, "LoadScene", "Bootstrap with isolated durable store");
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 &&
                    (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady); frame++) yield return null;
                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                evidence.RecordAssertion("Bootstrap finishes profile hydration before allowing a new expedition",
                    app != null && app.SaveDataReady && app.CanStartNewExpedition, "ready profile and no active run",
                    app == null ? "missing app" : app.ProfileLoadStatus + "; ready=" + app.SaveDataReady);
                Assert.That(app != null && app.CanStartNewExpedition, Is.True);

                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId, new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);
                ExpeditionSaveData entry = app.ActiveExpedition;
                evidence.RecordAssertion("The run is attached only after its complete initial snapshot reaches durable storage",
                    app.ActiveRun != null && entry != null && entry.runId == run.RunId.ToString() &&
                    !string.IsNullOrWhiteSpace(entry.roomEntryCheckpointJson), "persisted RunId and entry checkpoint", entry?.runId);

                ExpeditionInventory changedInventory = ExpeditionInventory.Restore(run.Inventory.Catalog, run.Inventory.CreateSnapshot());
                Assert.That(changedInventory.TryAddGold(19, TransactionId.New()).Succeeded, Is.True);
                ExpeditionSaveData duringRoom = entry.Clone();
                duringRoom.inventorySnapshotJson = JsonUtility.ToJson(changedInventory.CreateSnapshot());
                duringRoom.currentHealth = 21;
                duringRoom.accumulatedRenown = 52;
                duringRoom.fortunaCharges = 0;
                ExpeditionSaveData staleRoom = duringRoom.Clone();
                staleRoom.currentHealth = 20;
                testStore.BlockNextProfileWrite();
                Task<SaveWriteResult> update = app.SaveActiveExpeditionStateAsync(duringRoom);
                for (int frame = 0; frame < 120 && !testStore.ProfileWriteStarted.IsCompleted; frame++) yield return null;
                Assert.That(testStore.ProfileWriteStarted.IsCompleted, Is.True, "The first in-room write should reach the delayed store.");
                Task<SaveWriteResult> staleUpdate = app.SaveActiveExpeditionStateAsync(staleRoom);
                testStore.ReleaseBlockedProfileWrite();
                while (!update.IsCompleted || !staleUpdate.IsCompleted) yield return null;
                Assert.That(update.Result.Succeeded, Is.True, update.Result.Message);
                bool staleUpdateRejected = !staleUpdate.Result.Succeeded && app.ActiveExpedition.currentHealth == duringRoom.currentHealth &&
                    app.ActiveExpedition.inventorySnapshotJson == duringRoom.inventorySnapshotJson;
                evidence.RecordAssertion("An older queued room snapshot cannot overwrite a newer committed snapshot",
                    staleUpdateRejected, "health 21 and the first snapshot inventory remain durable",
                    "first=" + update.Result.Succeeded + "; stale=" + staleUpdate.Result.Succeeded + "; health=" + app.ActiveExpedition.currentHealth);
                Assert.That(staleUpdateRejected, Is.True);
                string firstRoomId = entry.currentNodeId;

                evidence.RecordInput(1, "ProcessRestart", "destroy application root; reload Bootstrap from same save directory");
                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 &&
                    (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady ||
                     ApplicationBootstrapper.Instance.ActiveRun == null || SceneManager.GetActiveScene().name != "Expedition"); frame++) yield return null;
                app = ApplicationBootstrapper.Instance;
                ExpeditionSaveData resumedEntry = app == null ? null : app.ActiveExpedition;
                ExpeditionRunConfiguration resumedRun = app == null ? null : app.ActiveRun;
                bool rollbackWorked = app != null && app.CanContinueExpedition && SceneManager.GetActiveScene().name == "Expedition" &&
                    resumedRun != null && resumedRun.RunId == run.RunId && resumedEntry.currentNodeId == firstRoomId &&
                    resumedEntry.currentHealth == entry.currentHealth && resumedEntry.accumulatedRenown == entry.accumulatedRenown &&
                    resumedRun.Inventory.Gold == run.Inventory.Gold;
                evidence.RecordAssertion("A real Bootstrap restart returns to the same expedition at the room-entry checkpoint",
                    rollbackWorked, "same RunId/node and entry health, Renown, and gold", rollbackWorked
                        ? resumedRun.RunId + " / " + resumedEntry.currentNodeId + " / " + resumedEntry.currentHealth + " / " + resumedRun.Inventory.Gold
                        : "scene=" + SceneManager.GetActiveScene().name + "; run=" + (resumedRun == null ? "missing" : resumedRun.RunId.ToString()) +
                          "; save=" + (resumedEntry == null ? "missing" : resumedEntry.currentNodeId + "/" + resumedEntry.currentHealth + "/" + resumedEntry.accumulatedRenown) +
                          "; loadError=" + (app == null ? "missing app" : app.ActiveExpeditionLoadError));
                Assert.That(rollbackWorked, Is.True);

                const string resultId = "result.d09.integration.room0";
                const string resultJson = "{\"outcome\":\"cleared\",\"renown\":12}";
                testStore.BlockNextProfileWrite();
                Task<SaveWriteResult> completion = app.CompleteCurrentRoomAsync(resultId, resultJson);
                for (int frame = 0; frame < 120 && !testStore.ProfileWriteStarted.IsCompleted; frame++) yield return null;
                Assert.That(testStore.ProfileWriteStarted.IsCompleted, Is.True, "The first completion should reach the delayed store.");
                Task<SaveWriteResult> duplicateCompletion = app.CompleteCurrentRoomAsync(resultId, resultJson);
                testStore.ReleaseBlockedProfileWrite();
                while (!completion.IsCompleted || !duplicateCompletion.IsCompleted) yield return null;
                Assert.That(completion.Result.Succeeded, Is.True, completion.Result.Message);
                Assert.That(duplicateCompletion.Result.Succeeded, Is.True, duplicateCompletion.Result.Message);
                Assert.That(app.ActiveExpedition.completedRoomResults.Length, Is.EqualTo(1));

                Object.Destroy(app.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 &&
                    (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady ||
                     ApplicationBootstrapper.Instance.ActiveRun == null || SceneManager.GetActiveScene().name != "Expedition"); frame++) yield return null;
                app = ApplicationBootstrapper.Instance;
                ExpeditionSaveData completed = app == null ? null : app.ActiveExpedition;
                bool resultSurvived = app != null && app.ActiveRun != null && completed != null &&
                    completed.completedRoomIds.Contains(firstRoomId) && completed.completedRoomResults.Length == 1 &&
                    completed.completedRoomResults[0].resultId == resultId && completed.completedRoomResults[0].resultJson == resultJson &&
                    string.IsNullOrEmpty(completed.roomEntryCheckpointJson);
                evidence.RecordAssertion("A completed room retains exactly one stable result after another process restart",
                    resultSurvived, "one committed result and no combat-entry checkpoint",
                    resultSurvived ? completed.completedRoomResults[0].resultId : "result missing");
                Assert.That(resultSurvived, Is.True);

                string nextNode = app.ActiveRun.Route.MandatoryNodeIds[1];
                Task<SaveWriteResult> advance = app.AdvanceToRoomAsync(nextNode);
                while (!advance.IsCompleted) yield return null;
                bool enteredNextRoom = advance.Result.Succeeded && app.ActiveExpedition.currentNodeId == nextNode &&
                    app.ActiveExpedition.visitedNodeIds.Length == 2 && !string.IsNullOrWhiteSpace(app.ActiveExpedition.roomEntryCheckpointJson);
                evidence.RecordAssertion("Advancing after completion persists the route cursor and a new entry checkpoint",
                    enteredNextRoom, "second visited node and new checkpoint", advance.Result.Message);
                Assert.That(enteredNextRoom, Is.True);

                Task<ExpeditionSettlementCommitResult> cleanup = app.SettleActiveRunAsync(ExpeditionOutcome.Abandoned, 0);
                while (!cleanup.IsCompleted) yield return null;
                evidence.RecordAssertion("The integration test settles its isolated run before cleanup", cleanup.Result.Persisted && !app.HasActiveExpedition,
                    "no active expedition in isolated store", cleanup.Result.Message);
                int failures = evidence.Complete();
                Assert.That(failures, Is.Zero, "D09 integration evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null) Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                UnityRandom.state = previousRandomState;
                Time.timeScale = 1f;
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
            }
        }

        private sealed class BlockingProfileWriteStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _blockNextProfileWrite;
            private TaskCompletionSource<bool> _profileWriteStarted = new TaskCompletionSource<bool>();
            private TaskCompletionSource<bool> _continueProfileWrite = new TaskCompletionSource<bool>();

            public BlockingProfileWriteStore(ISaveStore inner) { _inner = inner; }
            public Task ProfileWriteStarted => _profileWriteStarted.Task;

            public void BlockNextProfileWrite()
            {
                _profileWriteStarted = new TaskCompletionSource<bool>();
                _continueProfileWrite = new TaskCompletionSource<bool>();
                _blockNextProfileWrite = true;
            }

            public void ReleaseBlockedProfileWrite() => _continueProfileWrite.TrySetResult(true);
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile && _blockNextProfileWrite)
                {
                    _blockNextProfileWrite = false;
                    _profileWriteStarted.TrySetResult(true);
                    await _continueProfileWrite.Task;
                }
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }

        private static string SecondSkillFor(string classId)
        {
            switch (classId)
            {
                case "class.bellator": return "skill.bellator.guard";
                case "class.venator": return "skill.venator.triple_shot";
                case "class.magus": return "skill.magus.frost_nova";
                default: throw new System.ArgumentOutOfRangeException(nameof(classId));
            }
        }

        private static Button FindButton(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            return gameObject == null ? null : gameObject.GetComponent<Button>();
        }

        private static Text FindText(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            return gameObject == null ? null : gameObject.GetComponent<Text>();
        }
    }
}
