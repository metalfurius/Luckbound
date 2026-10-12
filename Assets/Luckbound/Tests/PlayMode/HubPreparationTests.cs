using System.Collections;
using System.Linq;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
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
                "race.orcini", new[] { firstSkillId, SecondSkillFor(classId) }));
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
                bool racesLocked = lockedRaces.All(button => button != null && !button.interactable);
                evidence.RecordAssertion("Uncontracted races remain locked", racesLocked,
                    "Orcini, Alvari and Dvergar buttons disabled", string.Join(", ", lockedRaces.Select(button => button == null ? "missing" : button.interactable.ToString())));

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
                    SceneManager.GetActiveScene().name == "Expedition" && firstRun.ClassId == "class.magus" && firstRun.Route.Nodes.Count == 24,
                    "Expedition with Magus and 24-node generated route",
                    firstRun == null ? "no active run" : SceneManager.GetActiveScene().name + " " + firstRun.ClassId + " " + firstRun.Route.Nodes.Count + " nodes");
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
    }
}
