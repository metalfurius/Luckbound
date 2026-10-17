using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode.Economy
{
    public sealed class RestStopServicesPanelTests
    {
        [UnityTest]
        public IEnumerator E03_ClickAndKeyboardSubmitPersistServicesAndRetryTheSamePurchase()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", Guid.NewGuid().ToString("N"));
            var store = new FailOnceProfileWriteStore(new FileSaveStore(saveDirectory, null));
            var evidence = new ScenarioEvidenceRecorder("E03", "E03_AccessibleServicesClickKeyboardRetry", 11235813);
            try
            {
                evidence.RecordInput(0, "LoadScene", "Load Bootstrap with isolated durable storage");
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                yield return LoadBootstrap();

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                evidence.RecordInput(1, "BeginExpedition", "Start from a valid rest-room fixture with 100 gold and upgrade materials");
                Assert.That(run.Inventory.TryAddGold(100, TransactionId.New()).Succeeded, Is.True);
                Assert.That(run.Inventory.TryAddMaterial(MaterialKind.Ore, 3, TransactionId.New()).Succeeded, Is.True);
                Assert.That(run.Inventory.TryAddMaterial(MaterialKind.ShadowWood, 2, TransactionId.New()).Succeeded, Is.True);
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);

                ExpeditionSaveData damagedRoom = app.ActiveExpedition;
                damagedRoom.currentHealth = 20;
                Task<SaveWriteResult> damage = app.SaveActiveExpeditionStateAsync(damagedRoom);
                while (!damage.IsCompleted) yield return null;
                Assert.That(damage.Result.Succeeded, Is.True, damage.Result.Message);

                RestStopServicesPanelController controller = null;
                yield return LoadExpedition(value => controller = value);
                app = ApplicationBootstrapper.Instance;
                evidence.RecordAssertion("A valid rest room offers the service entry point", controller.IsServiceOffered,
                    "service entry point visible and enabled", controller.IsServiceOffered ? "offered" : "hidden");
                Assert.That(controller.IsServiceOffered, Is.True,
                    "The player-facing service entry should be offered in a valid rest room.");
                evidence.RecordInput(2, "PointerClick", "Open the service panel from its accessible entry button");
                SimulatePointerClick(controller.AccessButton);
                evidence.RecordAssertion("Pointer click opens the service panel", controller.IsPanelOpen,
                    "panel open", controller.IsPanelOpen ? "open" : "closed");
                Assert.That(controller.IsPanelOpen, Is.True, "Pointer click should open the service panel.");

                Button enterRestButton = FindButton(controller, "EnterRestStop");
                Assert.That(enterRestButton, Is.Not.Null);
                evidence.RecordInput(3, "PointerClick", "Enter the rest stop and save its fixed merchant stock");
                SimulatePointerClick(enterRestButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(40), "Rest entry restores health once and saves fixed stock.");
                Assert.That(app.GetActiveMerchantStock().offers.Length, Is.EqualTo(11));
                evidence.RecordAssertion("Rest entry commits one recovery and fixed stock",
                    app.ActiveExpedition.currentHealth == 40 && app.GetActiveMerchantStock().offers.Length == 11,
                    "health 40 and 11 saved offers", "health=" + app.ActiveExpedition.currentHealth + "; offers=" + app.GetActiveMerchantStock().offers.Length);

                Button buyPotionButton = FindButton(controller, "Buy_consumable_health_potion");
                Assert.That(buyPotionButton, Is.Not.Null);
                store.FailNextProfileWrite();
                evidence.RecordInput(4, "PointerClick", "Attempt one health-potion purchase while the profile write fails once");
                SimulatePointerClick(buyPotionButton);
                yield return WaitUntil(() => !controller.IsBusy && app.HasPendingEconomyMutation);
                TransactionId pendingPurchaseId = controller.LastTransactionId;
                string pendingOperationKey = controller.LastOperationKey;
                bool failedWriteDidNotPublishCandidate = pendingPurchaseId.IsValid && app.ActiveRun.Inventory.Gold == 100 &&
                    app.ActiveRun.Inventory.QuickConsumable.Quantity == 2 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 2;
                Assert.That(failedWriteDidNotPublishCandidate, Is.True,
                    "A failed write must retain the original inventory and stock while leaving the exact operation pending.");
                evidence.RecordAssertion("Failed purchase save remains pending without publishing its candidate",
                    failedWriteDidNotPublishCandidate, "100 gold, 2 potions and 2 stock remain committed",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; potions=" + app.ActiveRun.Inventory.QuickConsumable.Quantity +
                    "; stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity);

                Button retryPurchaseButton = FindButton(controller, "Buy_consumable_health_potion");
                Assert.That(retryPurchaseButton, Is.Not.Null);
                evidence.RecordInput(5, "KeyboardSubmit", "Submit the focused purchase action to retry the exact pending operation");
                SimulateKeyboardSubmit(retryPurchaseButton);
                yield return WaitUntil(() => !controller.IsBusy && !app.HasPendingEconomyMutation);
                Assert.That(controller.LastTransactionId, Is.EqualTo(pendingPurchaseId),
                    "Keyboard submit retries the original transaction instead of creating a second purchase.");
                Assert.That(pendingOperationKey, Does.Contain(pendingPurchaseId.ToString()));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(82));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(3));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity,
                    Is.EqualTo(1));
                evidence.RecordAssertion("Keyboard submit retries the same purchase and persists inventory and stock",
                    controller.LastTransactionId == pendingPurchaseId && app.ActiveRun.Inventory.Gold == 82 &&
                    app.ActiveRun.Inventory.QuickConsumable.Quantity == 3 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1,
                    "same transaction; 82 gold; 3 potions; 1 offer remains",
                    "sameTransaction=" + (controller.LastTransactionId == pendingPurchaseId) + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; potions=" + app.ActiveRun.Inventory.QuickConsumable.Quantity +
                    "; stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity);

                Button sellPotionButton = FindButton(controller, "SellItem_" + app.ActiveRun.Inventory.QuickConsumable.InstanceId);
                Assert.That(sellPotionButton, Is.Not.Null, "The merchant should expose sales for owned item instances.");
                evidence.RecordInput(6, "PointerClick", "Sell one owned item and one carried material through merchant actions");
                SimulatePointerClick(sellPotionButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(86));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));

                Button sellOreButton = FindButton(controller, "SellMaterial_Ore");
                Assert.That(sellOreButton, Is.Not.Null, "The merchant should expose sales for carried materials.");
                SimulatePointerClick(sellOreButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(88));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));

                Button smithingTab = FindButton(controller, "Tab_Smithing");
                evidence.RecordInput(7, "PointerClick", "Open smithing and apply the consecutive weapon +1 recipe");
                SimulatePointerClick(smithingTab);
                Button upgradeWeapon = FindButton(controller, "Upgrade_Weapon");
                Assert.That(upgradeWeapon, Is.Not.Null, "Smithing should offer the next consecutive weapon recipe.");
                SimulatePointerClick(upgradeWeapon);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(1));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(78));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.Zero);

                Button restTab = FindButton(controller, "Tab_Rest");
                SimulatePointerClick(restTab);
                Button taliaButton = FindButton(controller, "UseTalia");
                Assert.That(taliaButton, Is.Not.Null);
                evidence.RecordInput(8, "PointerClick", "Use Talia once for the canonical 20-gold recovery");
                SimulatePointerClick(taliaButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(90));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(58));

                RoomInstanceId savedRoomId = app.ActiveRoomInstanceId;
                evidence.RecordInput(9, "ProcessRestart", "Reload Bootstrap and the saved expedition before inspecting services again");
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                yield return LoadBootstrap();
                app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.ActiveRoomInstanceId, Is.EqualTo(savedRoomId));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(90));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(58));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(1));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.Zero);
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity,
                    Is.EqualTo(1), "The purchase must remain deducted from the same persisted stock after restart.");
                bool servicesRestored = app.ActiveExpedition.currentHealth == 90 && app.ActiveRun.Inventory.Gold == 58 &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 1 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 0 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity == 1;
                evidence.RecordAssertion("Shop, smithing and Talia state survive restart",
                    servicesRestored, "health 90; gold 58; weapon +1; ore 0; one health potion remains in fixed stock",
                    "health=" + app.ActiveExpedition.currentHealth + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; weapon +" + app.ActiveRun.Inventory.Weapon.UpgradeLevel +
                    "; ore=" + app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) +
                    "; stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "consumable.health_potion").remainingQuantity);
                Assert.That(servicesRestored, Is.True);

                yield return LoadExpedition(value => controller = value);
                Assert.That(controller.IsServiceOffered, Is.True);
                SimulatePointerClick(controller.AccessButton);
                Assert.That(FindButton(controller, "EnterRestStop"), Is.Null,
                    "The once-only rest-entry service must no longer be offered after restart.");
                Button spentTaliaButton = FindButton(controller, "UseTalia");
                Assert.That(spentTaliaButton, Is.Not.Null);
                bool oneTimeServicesRemainSpent = FindButton(controller, "EnterRestStop") == null && !spentTaliaButton.interactable;
                evidence.RecordAssertion("One-time rest entry and Talia remain spent after restart",
                    oneTimeServicesRemainSpent, "entry absent and Talia disabled", "entry=" +
                    (FindButton(controller, "EnterRestStop") == null ? "absent" : "present") + "; Talia interactable=" + spentTaliaButton.interactable);
                Assert.That(spentTaliaButton.interactable, Is.False,
                    "Talia's once-per-rest service must remain spent after restart.");
                Assert.That(evidence.Complete(), Is.Zero, "E03 player-facing services scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        [UnityTest]
        public IEnumerator E03_DoesNotOfferRestServicesOutsideAValidRestRoom()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", Guid.NewGuid().ToString("N"));
            var store = new FileSaveStore(saveDirectory, null);
            var evidence = new ScenarioEvidenceRecorder("E03", "E03_NoServiceEntryOutsideRest", 11235813);
            try
            {
                evidence.RecordInput(0, "LoadScene", "Load Bootstrap with isolated durable storage");
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                yield return LoadBootstrap();

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                evidence.RecordInput(1, "BeginExpedition", "Start a normal Bellator run at its first non-rest route node");
                ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                    ExpeditionRunConfiguration.HumanRaceId,
                    new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);
                ExpeditionRouteNode initialNode = app.ActiveRun.Route.Nodes.Single(node =>
                    node.NodeId == app.ActiveExpedition.currentNodeId);
                Assert.That(initialNode.Kind, Is.Not.EqualTo(ExpeditionRouteNodeKind.Rest));

                RestStopServicesPanelController controller = null;
                yield return LoadExpedition(value => controller = value);
                Assert.That(controller, Is.Not.Null);
                bool hiddenOutsideRest = !controller.IsServiceOffered && !controller.AccessButton.gameObject.activeInHierarchy;
                evidence.RecordAssertion("Service entry remains hidden outside a valid rest room", hiddenOutsideRest,
                    "entry hidden and unavailable", hiddenOutsideRest ? "hidden" : "offered");
                Assert.That(controller.IsServiceOffered, Is.False,
                    "The service entry point must stay hidden unless the current route node is a valid rest room.");
                Assert.That(controller.AccessButton.gameObject.activeInHierarchy, Is.False);
                Assert.That(evidence.Complete(), Is.Zero, "E03 rest-room boundary scenario failed. Evidence: " + evidence.ReportPath);
            }
            finally
            {
                evidence.Dispose();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        private static IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                !ApplicationBootstrapper.Instance.SaveDataReady ||
                (ApplicationBootstrapper.Instance.ActiveRun != null && SceneManager.GetActiveScene().name != "Expedition") ||
                (ApplicationBootstrapper.Instance.ActiveRun == null && SceneManager.GetActiveScene().name != "Menu")); frame++)
                yield return null;
            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
        }

        private static IEnumerator LoadExpedition(Action<RestStopServicesPanelController> setController)
        {
            RestStopServicesPanelController controller = null;
            SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
            for (int frame = 0; frame < 90 && (SceneManager.GetActiveScene().name != "Expedition" ||
                (controller = UnityEngine.Object.FindAnyObjectByType<RestStopServicesPanelController>()) == null); frame++)
                yield return null;
            Assert.That(controller, Is.Not.Null, "ExpeditionRunSessionPresenter must create the E03 service panel.");
            setController(controller);
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> predicate)
        {
            for (int frame = 0; frame < 180 && !predicate(); frame++) yield return null;
            Assert.That(predicate(), Is.True, "The player-facing E03 action did not finish in time.");
        }

        private static ExpeditionRunConfiguration CreateRunStartingAtRest()
        {
            ExpeditionRunConfiguration generated = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            string[] lines = generated.Route.CanonicalForm.Split('\n');
            int firstNodeLine = Array.FindIndex(lines, line => line.StartsWith(
                "N|" + generated.Route.MandatoryNodeIds[0] + "|", StringComparison.Ordinal));
            ExpeditionRouteNode restNode = generated.Route.MandatoryNodeIds.Skip(1)
                .Select(nodeId => generated.Route.Nodes.Single(node => node.NodeId == nodeId))
                .FirstOrDefault(node => node.Kind == ExpeditionRouteNodeKind.Rest);
            Assert.That(restNode, Is.Not.Null);
            int restNodeLine = Array.FindIndex(lines, line => line.StartsWith("N|" + restNode.NodeId + "|", StringComparison.Ordinal));
            Assert.That(firstNodeLine, Is.GreaterThanOrEqualTo(0));
            Assert.That(restNodeLine, Is.GreaterThanOrEqualTo(0));

            string[] first = lines[firstNodeLine].Split('|');
            string[] laterRest = lines[restNodeLine].Split('|');
            string firstSector = first[2];
            string firstCombatTemplate = first[4];
            first[2] = string.Empty;
            first[3] = ExpeditionRouteNodeKind.Rest.ToString();
            first[4] = laterRest[4];
            laterRest[2] = firstSector;
            laterRest[3] = ExpeditionRouteNodeKind.Combat.ToString();
            laterRest[4] = firstCombatTemplate;
            lines[firstNodeLine] = string.Join("|", first);
            lines[restNodeLine] = string.Join("|", laterRest);
            ExpeditionRoutePlan route = ExpeditionRoutePlan.ParseCanonical(string.Join("\n", lines));
            return new ExpeditionRunConfiguration(generated.RunId, generated.ClassId, generated.RaceId,
                generated.SkillIds, generated.Seed, route, generated.Inventory, generated.StartingEquipmentInstanceIds);
        }

        private static Button FindButton(RestStopServicesPanelController controller, string name)
        {
            Button[] buttons = controller.GetComponentsInChildren<Button>(true);
            for (int index = buttons.Length - 1; index >= 0; index--)
                if (buttons[index].gameObject.name == name) return buttons[index];
            return null;
        }

        private static void SimulatePointerClick(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True, button.gameObject.name + " should be interactable.");
            Assert.That(EventSystem.current, Is.Not.Null, "Expedition scene must provide an input EventSystem.");
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void SimulateKeyboardSubmit(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.interactable, Is.True);
            Assert.That(EventSystem.current, Is.Not.Null);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        private static void DeleteIsolatedSaveDirectory(string directory)
        {
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullDirectory = Path.GetFullPath(directory);
            if (!fullDirectory.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to delete a save directory outside the system temp root.");
            if (Directory.Exists(fullDirectory)) Directory.Delete(fullDirectory, recursive: true);
        }

        private sealed class FailOnceProfileWriteStore : ISaveStore
        {
            private readonly ISaveStore _inner;
            private bool _failNextProfileWrite;

            public FailOnceProfileWriteStore(ISaveStore inner) { _inner = inner; }
            public void FailNextProfileWrite() => _failNextProfileWrite = true;
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document) => _inner.ReadAsync(document);

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile && _failNextProfileWrite)
                {
                    _failNextProfileWrite = false;
                    throw new IOException("Injected E03 profile write failure.");
                }
                await _inner.WriteAsync(document, json, lastKnownGoodPrimaryJson);
            }
        }
    }
}
