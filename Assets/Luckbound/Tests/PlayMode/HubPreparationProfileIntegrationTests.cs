using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class HubPreparationProfileIntegrationTests
    {
        [UnityTest]
        public IEnumerator E09_HubStartAppliesSavedPermanentUpgradesToFreshClassRun()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE09", Guid.NewGuid().ToString("N"));
            var saveStore = new FileSaveStore(saveDirectory, null);
            UnityAction<Scene, LoadSceneMode> sceneLoadedHandler = null;
            ApplicationBootstrapper app = null;
            Time.timeScale = 1f;
            bool sawExpeditionSceneLoaded = false;
            bool sceneCommittedRun = false;
            ExpeditionSaveData committedStateAtSceneLoad = null;
            try
            {
                ProfileSaveData seededProfile = CreateFullyUpgradedProfile();
                Task<SaveWriteResult> seedSave = new SaveRepository(saveStore).SaveProfileAsync(seededProfile);
                while (!seedSave.IsCompleted) yield return null;
                Assert.That(seedSave.Result.Succeeded, Is.True, seedSave.Result.Message);

                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => saveStore;
                if (ApplicationBootstrapper.Instance != null) Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
                for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                    !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != "Menu"); frame++)
                    yield return null;

                app = ApplicationBootstrapper.Instance;
                Assert.That(app != null && app.SaveDataReady && app.CanStartNewExpedition, Is.True,
                    app == null ? "Application profile did not load." : app.ProfileLoadMessage);
                Assert.That(app.Profile.healthUpgradeLevel, Is.EqualTo(2));
                Assert.That(app.Profile.energyUpgradeLevel, Is.EqualTo(2));
                Assert.That(app.Profile.additionalPotionUpgradeLevel, Is.EqualTo(1));
                Assert.That(app.Profile.availableRenown, Is.Zero);
                Assert.That(app.Profile.progressionPurchaseTransactions.Length, Is.EqualTo(5));
                Assert.That(app.Profile.progressionPurchaseTransactions.Sum(transaction => transaction.cost), Is.EqualTo(510));
                Assert.That(app.HasActiveExpedition, Is.False, "The seeded profile must not carry an active run.");

                Button enterTavern = FindButton("StartTavernButton");
                Assert.That(enterTavern, Is.Not.Null, "Menu is missing StartTavernButton.");
                enterTavern.onClick.Invoke();
                for (int frame = 0; frame < 60 && (SceneManager.GetActiveScene().name != "Hub" ||
                    Object.FindAnyObjectByType<HubPreparationController>() == null ||
                    Object.FindAnyObjectByType<HubPreparationController>().SelectedSkillIds.Count != 2); frame++) yield return null;

                HubPreparationController preparation = Object.FindAnyObjectByType<HubPreparationController>();
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
                Assert.That(preparation, Is.Not.Null, "The Hub did not create its preparation controller.");
                Button venatorClass = FindButton("ClassVenatorButton");
                Assert.That(venatorClass, Is.Not.Null, "Hub is missing ClassVenatorButton.");
                venatorClass.onClick.Invoke();
                Assert.That(preparation.SelectedClassId, Is.EqualTo("class.venator"));
                Assert.That(preparation.SelectedSkillIds.Count, Is.EqualTo(2));
                Assert.That(preparation.SelectedSkillIds.All(id => id.StartsWith("skill.venator.", StringComparison.Ordinal)), Is.True);
                Assert.That(app.CanStartNewExpedition, Is.True);

                sceneLoadedHandler = (loadedScene, mode) =>
                {
                    if (loadedScene.name != "Expedition") return;
                    ExpeditionSaveData state = app.ActiveExpedition;
                    sceneCommittedRun = state != null && app.ActiveRun != null &&
                        string.Equals(state.runId, app.ActiveRun.RunId.ToString(), StringComparison.Ordinal) &&
                        state.maximumHealth == 110 && state.maximumEnergy == 55 && state.healthPotionCharges == 3;
                    committedStateAtSceneLoad = state;
                    sawExpeditionSceneLoaded = true;
                };
                SceneManager.sceneLoaded += sceneLoadedHandler;
                Button startExpedition = FindButton("StartExpeditionButton");
                Assert.That(startExpedition, Is.Not.Null, "Hub is missing StartExpeditionButton.");
                startExpedition.onClick.Invoke();
                for (int frame = 0; frame < 120 && SceneManager.GetActiveScene().name != "Expedition"; frame++) yield return null;
                SceneManager.sceneLoaded -= sceneLoadedHandler;
                sceneLoadedHandler = null;

                Assert.That(sawExpeditionSceneLoaded, Is.True, "The Hub did not load the Expedition scene.");
                Assert.That(sceneCommittedRun, Is.True,
                    "The active expedition was not committed with upgraded values before the scene load completed.");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Expedition"));

                var run = app.ActiveRun;
                ExpeditionSaveData active = app.ActiveExpedition;
                Assert.That(run, Is.Not.Null);
                Assert.That(active, Is.Not.Null);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(run.RunId.IsValid, Is.True);
                Assert.That(run.ClassId, Is.EqualTo("class.venator"));
                Assert.That(run.StartingHealth, Is.EqualTo(110));
                Assert.That(run.StartingEnergy, Is.EqualTo(55));
                Assert.That(run.StartingHealthPotions, Is.EqualTo(3));
                Assert.That(run.SkillIds.Count, Is.EqualTo(2));
                Assert.That(run.SkillIds.All(id => id.StartsWith("skill.venator.", StringComparison.Ordinal)), Is.True);

                InventoryItemStack potionStack = run.Inventory.QuickConsumable;
                Assert.That(run.Inventory.ClassId, Is.EqualTo("class.venator"));
                Assert.That(run.Inventory.Weapon.ItemId, Is.EqualTo("weapon.venator.short_bow"));
                Assert.That(run.Inventory.Armor.ItemId, Is.EqualTo("armor.venator.leather_vest"));
                Assert.That(potionStack, Is.Not.Null);
                Assert.That(potionStack.ItemId, Is.EqualTo("consumable.health_potion"));
                Assert.That(potionStack.Quantity, Is.EqualTo(3));
                Assert.That(run.Inventory.Gold, Is.Zero);
                Assert.That(run.Inventory.GetMaterialCount(MaterialKind.Ore), Is.Zero);

                Assert.That(active.runId, Is.EqualTo(run.RunId.ToString()));
                Assert.That(active.seed, Is.Not.Zero);
                Assert.That(run.Route.Nodes.Count, Is.EqualTo(24));
                Assert.That(active.classId, Is.EqualTo("class.venator"));
                Assert.That(active.maximumHealth, Is.EqualTo(110));
                Assert.That(active.currentHealth, Is.EqualTo(110));
                Assert.That(active.maximumEnergy, Is.EqualTo(55));
                Assert.That(active.currentEnergy, Is.EqualTo(55));
                Assert.That(active.healthPotionCharges, Is.EqualTo(3));
                Assert.That(active.completedRoomIds, Is.Empty);
                Assert.That(active.visitedNodeIds.Length, Is.EqualTo(1));
                Assert.That(active.currentNodeId, Is.EqualTo(active.visitedNodeIds[0]));
                Assert.That(active.accumulatedRenown, Is.Zero);
                Assert.That(committedStateAtSceneLoad.runId, Is.EqualTo(active.runId));
                Assert.That(committedStateAtSceneLoad.maximumHealth, Is.EqualTo(active.maximumHealth));
                Assert.That(committedStateAtSceneLoad.maximumEnergy, Is.EqualTo(active.maximumEnergy));
                Assert.That(committedStateAtSceneLoad.healthPotionCharges, Is.EqualTo(active.healthPotionCharges));

                Task<SaveLoadResult<ProfileSaveEnvelope>> persistedRead = new SaveRepository(saveStore).LoadProfileAsync();
                while (!persistedRead.IsCompleted) yield return null;
                Assert.That(persistedRead.Result.HasUsableData, Is.True, persistedRead.Result.Message);
                ProfileSaveEnvelope persistedEnvelope = persistedRead.Result.Data;
                Assert.That(persistedEnvelope.hasActiveExpedition, Is.True);
                Assert.That(persistedEnvelope.activeExpedition, Is.Not.Null);
                Assert.That(persistedEnvelope.activeExpedition.runId, Is.EqualTo(run.RunId.ToString()));
                Assert.That(persistedEnvelope.activeExpedition.maximumHealth, Is.EqualTo(110));
                Assert.That(persistedEnvelope.activeExpedition.maximumEnergy, Is.EqualTo(55));
                Assert.That(persistedEnvelope.activeExpedition.healthPotionCharges, Is.EqualTo(3));
                Assert.That(persistedEnvelope.profile.healthUpgradeLevel, Is.EqualTo(2));
                Assert.That(persistedEnvelope.profile.energyUpgradeLevel, Is.EqualTo(2));
                Assert.That(persistedEnvelope.profile.additionalPotionUpgradeLevel, Is.EqualTo(1));
            }
            finally
            {
                if (sceneLoadedHandler != null) SceneManager.sceneLoaded -= sceneLoadedHandler;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null) Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
            }
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
                transactionId = Luckbound.Core.Identifiers.TransactionId.New().ToString(),
                kind = (int)kind,
                cost = cost
            };
        }

        private static Button FindButton(string name)
        {
            return new System.Collections.Generic.List<Button>(Object.FindObjectsByType<Button>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)).FirstOrDefault(button => button.name == name);
        }

    }
}
