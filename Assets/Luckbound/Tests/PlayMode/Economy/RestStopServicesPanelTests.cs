using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Relics;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
                EquipRelic(run.Inventory, SupportRelicIds.MerchantSeal, EquipmentSlot.RelicOne);
                EquipRelic(run.Inventory, "relic.marked_coin", EquipmentSlot.RelicTwo);
                evidence.RecordInput(1, "BeginExpedition", "Start with the Merchant Seal and an outside-family relic equipped, plus 100 gold and upgrade materials");
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

                Button buyMarkedCoinButton = FindButton(controller, "Buy_relic_marked_coin");
                Assert.That(buyMarkedCoinButton, Is.Not.Null);
                string discountedPriceLabel = buyMarkedCoinButton.GetComponentInChildren<Text>(true).text;
                StringAssert.Contains("42", discountedPriceLabel,
                    "The Merchant Seal should display the 50-gold offer at the canonical rounded price of 42.");
                evidence.RecordAssertion("Merchant Seal shows 42 for the 50-gold relic offer",
                    discountedPriceLabel.Contains("42"), "42 gold", discountedPriceLabel);
                store.FailNextProfileWrite();
                int effectCountBeforePendingPurchase = app.AudioRouter.PendingEffectRequestCount;
                evidence.RecordInput(4, "PointerClick", "Attempt the discounted 50-gold relic offer while the profile write fails once");
                SimulatePointerClick(buyMarkedCoinButton);
                yield return WaitUntil(() => !controller.IsBusy && app.HasPendingEconomyMutation);
                Assert.That(app.AudioRouter.PendingEffectRequestCount, Is.EqualTo(effectCountBeforePendingPurchase),
                    "A failed write is pending, not a terminal UI rejection or a confirmed purchase.");
                TransactionId pendingPurchaseId = controller.LastTransactionId;
                string pendingOperationKey = controller.LastOperationKey;
                Assert.That(app.PendingEconomyOperationKey, Is.EqualTo(pendingOperationKey),
                    "The durable application operation key must include the same discount captured from the displayed offer.");
                bool failedWriteDidNotPublishCandidate = pendingPurchaseId.IsValid && app.ActiveRun.Inventory.Gold == 100 &&
                    app.ActiveRun.Inventory.QuickConsumable.Quantity == 2 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity == 1;
                Assert.That(failedWriteDidNotPublishCandidate, Is.True,
                    "A failed write must retain the original inventory and stock while leaving the exact operation pending.");
                evidence.RecordAssertion("Failed purchase save remains pending without publishing its candidate",
                    failedWriteDidNotPublishCandidate, "100 gold, 2 potions and 1 relic offer remain committed",
                    "gold=" + app.ActiveRun.Inventory.Gold + "; potions=" + app.ActiveRun.Inventory.QuickConsumable.Quantity +
                    "; stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity);

                Button retryPurchaseButton = FindButton(controller, "Buy_relic_marked_coin");
                Assert.That(retryPurchaseButton, Is.Not.Null);
                evidence.RecordInput(5, "KeyboardSubmit", "Submit the focused purchase action to retry the exact pending operation");
                SimulateKeyboardSubmit(retryPurchaseButton);
                yield return WaitUntil(() => !controller.IsBusy && !app.HasPendingEconomyMutation);
                Assert.That(controller.LastTransactionId, Is.EqualTo(pendingPurchaseId),
                    "Keyboard submit retries the original transaction instead of creating a second purchase.");
                Assert.That(controller.LastOperationKey, Is.EqualTo(pendingOperationKey));
                StringAssert.Contains(pendingPurchaseId.ToString(), pendingOperationKey);
                StringAssert.EndsWith("|15", pendingOperationKey,
                    "The requested discount must remain part of the operation key used for retry.");
                Assert.That(app.PendingEconomyOperationKey, Is.Empty);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(58));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(2));
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.EqualTo(1));
                Assert.That(app.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.MerchantSeal));
                Assert.That(app.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo("relic.marked_coin"));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity,
                    Is.Zero);
                Assert.That(app.AudioRouter.PendingEffectRequestCount, Is.EqualTo(effectCountBeforePendingPurchase + 1),
                    "The successful exact retry emits the purchase cue once and no error cue.");
                Assert.That(PendingEffectIdAt(app.AudioRouter, effectCountBeforePendingPurchase),
                    Is.EqualTo(ApplicationAudioRouter.UiBuyEffectId));
                evidence.RecordAssertion("Keyboard submit retries the same purchase and persists inventory and stock",
                    controller.LastTransactionId == pendingPurchaseId && app.ActiveRun.Inventory.Gold == 58 &&
                    app.ActiveRun.Inventory.QuickConsumable.Quantity == 2 &&
                    app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin") == 1 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity == 0,
                    "same transaction; 42 gold debited; 2 potions unchanged; relic offer exhausted",
                    "sameTransaction=" + (controller.LastTransactionId == pendingPurchaseId) + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; potions=" + app.ActiveRun.Inventory.QuickConsumable.Quantity +
                    "; relics=" + app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin") +
                    "; stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity);

                Button sellPotionButton = FindButton(controller, "SellItem_" + app.ActiveRun.Inventory.QuickConsumable.InstanceId);
                Assert.That(sellPotionButton, Is.Not.Null, "The merchant should expose sales for owned item instances.");
                StringAssert.Contains("+4", sellPotionButton.GetComponentInChildren<Text>(true).text,
                    "The purchase seal must not change item sale pricing.");
                evidence.RecordInput(6, "PointerClick", "Sell one owned item and one carried material through merchant actions");
                SimulatePointerClick(sellPotionButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(62));
                Assert.That(app.ActiveRun.Inventory.QuickConsumable.Quantity, Is.EqualTo(1));

                Button sellOreButton = FindButton(controller, "SellMaterial_Ore");
                Assert.That(sellOreButton, Is.Not.Null, "The merchant should expose sales for carried materials.");
                StringAssert.Contains("+2", sellOreButton.GetComponentInChildren<Text>(true).text,
                    "The purchase seal must not change material sale pricing.");
                SimulatePointerClick(sellOreButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(64));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(2));

                Button smithingTab = FindButton(controller, "Tab_Smithing");
                evidence.RecordInput(7, "PointerClick", "Open smithing and apply the consecutive weapon +1 recipe");
                SimulatePointerClick(smithingTab);
                Button upgradeWeapon = FindButton(controller, "Upgrade_Weapon");
                Assert.That(upgradeWeapon, Is.Not.Null, "Smithing should offer the next consecutive weapon recipe.");
                SimulatePointerClick(upgradeWeapon);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(1));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(54));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.Zero);

                Button restTab = FindButton(controller, "Tab_Rest");
                SimulatePointerClick(restTab);
                Button taliaButton = FindButton(controller, "UseTalia");
                Assert.That(taliaButton, Is.Not.Null);
                evidence.RecordInput(8, "PointerClick", "Use Talia once for the canonical 20-gold recovery");
                SimulatePointerClick(taliaButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(90));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(34));

                RoomInstanceId savedRoomId = app.ActiveRoomInstanceId;
                evidence.RecordInput(9, "ProcessRestart", "Reload Bootstrap and the saved expedition before inspecting services again");
                UnityEngine.Object.Destroy(app.gameObject);
                yield return null;
                yield return LoadBootstrap();
                app = ApplicationBootstrapper.Instance;
                Assert.That(app, Is.Not.Null);
                Assert.That(app.ActiveRoomInstanceId, Is.EqualTo(savedRoomId));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(90));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(34));
                Assert.That(app.ActiveRun.Inventory.RelicOne.ItemId, Is.EqualTo(SupportRelicIds.MerchantSeal));
                Assert.That(app.ActiveRun.Inventory.RelicTwo.ItemId, Is.EqualTo("relic.marked_coin"));
                Assert.That(app.ActiveRun.Inventory.Weapon.UpgradeLevel, Is.EqualTo(1));
                Assert.That(app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore), Is.Zero);
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity,
                    Is.Zero, "The purchase must remain deducted from the same persisted stock after restart.");
                bool servicesRestored = app.ActiveExpedition.currentHealth == 90 && app.ActiveRun.Inventory.Gold == 34 &&
                    app.ActiveRun.Inventory.Weapon.UpgradeLevel == 1 &&
                    app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) == 0 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity == 0;
                evidence.RecordAssertion("Shop, smithing and Talia state survive restart",
                    servicesRestored, "health 90; gold 34; weapon +1; ore 0; marked-coin offer remains exhausted",
                    "health=" + app.ActiveExpedition.currentHealth + "; gold=" + app.ActiveRun.Inventory.Gold +
                    "; weapon +" + app.ActiveRun.Inventory.Weapon.UpgradeLevel +
                    "; ore=" + app.ActiveRun.Inventory.GetMaterialCount(MaterialKind.Ore) +
                    "; marked-coin stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity);
                Assert.That(servicesRestored, Is.True);

                yield return LoadExpedition(value => controller = value);
                ExpeditionSaveData reloadedExpedition = app.ActiveExpedition;
                ExpeditionRouteNode reloadedNode = app.ActiveRun.Route.Nodes.FirstOrDefault(node =>
                    node.NodeId == reloadedExpedition.currentNodeId);
                Button reloadedAccessButton = controller.AccessButton;
                string reloadedServiceState = "scene=" + SceneManager.GetActiveScene().name +
                    "; ready=" + app.SaveDataReady + "; activeRun=" + (app.ActiveRun != null) +
                    "; loadError=" + app.ActiveExpeditionLoadError + "; node=" + reloadedExpedition.currentNodeId +
                    "; kind=" + (reloadedNode == null ? "missing" : reloadedNode.Kind.ToString()) +
                    "; offered=" + controller.IsServiceOffered +
                    "; accessActive=" + (reloadedAccessButton != null && reloadedAccessButton.gameObject.activeInHierarchy);
                evidence.RecordAssertion("The rest stop remains available after restart", controller.IsServiceOffered,
                    "service entry point visible and enabled", reloadedServiceState);
                Assert.That(controller.IsServiceOffered, Is.True, reloadedServiceState);
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
        public IEnumerator I06_TerminalRejectedMerchantActionQueuesOneSilentUiError()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundI06", Guid.NewGuid().ToString("N"));
            var store = new FileSaveStore(saveDirectory, null);
            bool previousAudioListenerPause = AudioListener.pause;
            AudioListener.pause = true;
            try
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                yield return LoadBootstrap();

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                Assert.That(run.Inventory.TryAddGold(100, TransactionId.New()).Succeeded, Is.True);
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);

                RestStopServicesPanelController controller = null;
                yield return LoadExpedition(value => controller = value);
                SimulatePointerClick(controller.AccessButton);
                Button enterRest = FindButton(controller, "EnterRestStop");
                Assert.That(enterRest, Is.Not.Null);
                SimulatePointerClick(enterRest);
                yield return WaitUntil(() => !controller.IsBusy);

                ApplicationAudioRouter router = app.AudioRouter;
                Assert.That(router, Is.Not.Null);
                Assert.That(router.UserGestureObserved, Is.False,
                    "The test keeps audio locked behind the browser-safe first-input gate.");
                TextAsset effectCatalog = Resources.Load<TextAsset>("Luckbound/Data/AudioEffects");
                Assert.That(effectCatalog, Is.Not.Null);
                StringAssert.Contains("\"ui_error\"", effectCatalog.text);
                int effectsBeforeDirectPurchase = router.PendingEffectRequestCount;

                Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> directPurchase =
                    app.PurchaseFromMerchantAsync(app.ActiveRoomInstanceId, "relic.marked_coin", 1, 0, TransactionId.New());
                while (!directPurchase.IsCompleted) yield return null;
                Assert.That(directPurchase.Result.Persisted, Is.True, directPurchase.Result.Message);
                Assert.That(directPurchase.Result.Outcome.Succeeded, Is.True, "The setup purchase should consume the visible offer.");
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(effectsBeforeDirectPurchase),
                    "A confirmed write outside this panel does not emit panel feedback.");

                Button stalePurchase = FindButton(controller, "Buy_relic_marked_coin");
                Assert.That(stalePurchase, Is.Not.Null);
                Assert.That(stalePurchase.interactable, Is.True,
                    "The test submits a real action through the panel's still-visible offer after stock changed.");
                SimulatePointerClick(stalePurchase);
                yield return WaitUntil(() => !controller.IsBusy);

                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(50));
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.EqualTo(1));
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(effectsBeforeDirectPurchase + 1),
                    "A terminal rejection queues one error request without a purchase cue.");
                Assert.That(PendingEffectIdAt(router, effectsBeforeDirectPurchase),
                    Is.EqualTo(ApplicationAudioRouter.UiErrorEffectId));
                Assert.That(router.UserGestureObserved, Is.False,
                    "The queued UI error is inspected without unlocking or playing audio.");
            }
            finally
            {
                AudioListener.pause = previousAudioListenerPause;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = 1f;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        [UnityTest]
        public IEnumerator I06_ExplicitCancelAndCloseRoutesRemainDistinct()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundI06", Guid.NewGuid().ToString("N"));
            var store = new FileSaveStore(saveDirectory, null);
            Keyboard keyboard = null;
            bool previousAudioListenerPause = AudioListener.pause;
            float previousTimeScale = Time.timeScale;
            AudioListener.pause = true;
            try
            {
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null)
                    UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                yield return LoadBootstrap();

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);

                RestStopServicesPanelController controller = null;
                yield return LoadExpedition(value => controller = value);
                ApplicationAudioRouter router = app.AudioRouter;
                Assert.That(router.UserGestureObserved, Is.False,
                    "The test inspects queued cues without unlocking audio.");
                MethodInfo stopListening = typeof(ApplicationAudioRouter).GetMethod("StopListeningForInput",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(stopListening, Is.Not.Null);
                stopListening.Invoke(router, null);

                keyboard = InputSystem.AddDevice<Keyboard>();

                SimulatePointerClick(controller.AccessButton);
                Assert.That(controller.IsPanelOpen, Is.True);
                int cancelStart = router.PendingEffectRequestCount;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                InputSystem.Update();
                MethodInfo update = typeof(RestStopServicesPanelController).GetMethod("Update",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(update, Is.Not.Null);
                update.Invoke(controller, null);

                Assert.That(controller.IsPanelOpen, Is.False);
                int afterCancel = router.PendingEffectRequestCount;
                bool cancelQueued = false;
                for (int index = cancelStart; index < afterCancel; index++)
                {
                    string effectId = PendingEffectIdAt(router, index);
                    if (effectId == ApplicationAudioRouter.UiCancelEffectId) cancelQueued = true;
                    Assert.That(effectId, Is.Not.EqualTo(ApplicationAudioRouter.UiCloseEffectId),
                        "Escape cancels the panel and must not also emit its close cue.");
                }
                Assert.That(cancelQueued, Is.True, "Escape over the open panel should queue ui_cancel.");
                Assert.That(router.UserGestureObserved, Is.False);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.Update();

                SimulatePointerClick(controller.AccessButton);
                int buttonCloseStart = router.PendingEffectRequestCount;
                Button closeButton = FindButton(controller, "E03ServicesCloseButton");
                SimulatePointerClick(closeButton);
                Assert.That(controller.IsPanelOpen, Is.False);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(buttonCloseStart + 1));
                Assert.That(PendingEffectIdAt(router, buttonCloseStart),
                    Is.EqualTo(ApplicationAudioRouter.UiCloseEffectId),
                    "The visible CERRAR button remains a close action.");

                SimulatePointerClick(controller.AccessButton);
                Assert.That(controller.IsPanelOpen, Is.True);
                int automaticCloseStart = router.PendingEffectRequestCount;
                string nextNodeId = app.ActiveRun.Route.MandatoryNodeIds[1];
                Task<SaveWriteResult> completeRoom = app.CompleteCurrentRoomAsync(
                    "result.i06.cancel.auto-close", "{\"outcome\":\"cleared\",\"renown\":0}");
                while (!completeRoom.IsCompleted) yield return null;
                Assert.That(completeRoom.Result.Succeeded, Is.True, completeRoom.Result.Message);
                Task<SaveWriteResult> advance = app.AdvanceToRoomAsync(nextNodeId);
                while (!advance.IsCompleted) yield return null;
                Assert.That(advance.Result.Succeeded, Is.True, advance.Result.Message);
                update.Invoke(controller, null);

                Assert.That(controller.IsPanelOpen, Is.False);
                Assert.That(router.PendingEffectRequestCount, Is.EqualTo(automaticCloseStart + 1));
                Assert.That(PendingEffectIdAt(router, automaticCloseStart),
                    Is.EqualTo(ApplicationAudioRouter.UiCloseEffectId),
                    "Losing rest-stop availability closes the panel without ui_cancel.");
                Assert.That(router.UserGestureObserved, Is.False);
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                AudioListener.pause = previousAudioListenerPause;
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
                if (ApplicationBootstrapper.Instance != null)
                    UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                Time.timeScale = previousTimeScale;
                DeleteIsolatedSaveDirectory(saveDirectory);
            }
        }

        [UnityTest]
        public IEnumerator E03_MerchantOfferKeepsBasePriceWithoutMerchantSeal()
        {
            string saveDirectory = Path.Combine(Path.GetTempPath(), "LuckboundE03", Guid.NewGuid().ToString("N"));
            var store = new FileSaveStore(saveDirectory, null);
            var evidence = new ScenarioEvidenceRecorder("E03", "E03_MerchantBasePriceWithoutSeal", 11235813);
            try
            {
                evidence.RecordInput(0, "LoadScene", "Load Bootstrap with isolated durable storage");
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
                if (ApplicationBootstrapper.Instance != null) UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
                yield return null;
                yield return LoadBootstrap();

                ApplicationBootstrapper app = ApplicationBootstrapper.Instance;
                ExpeditionRunConfiguration run = CreateRunStartingAtRest();
                Assert.That(run.Inventory.RelicOne, Is.Null);
                Assert.That(run.Inventory.RelicTwo, Is.Null);
                Assert.That(run.Inventory.TryAddGold(100, TransactionId.New()).Succeeded, Is.True);
                evidence.RecordInput(1, "BeginExpedition", "Start a rest-room fixture with no equipped support relic and 100 gold");
                Task<SaveWriteResult> begin = app.BeginExpeditionAsync(run);
                while (!begin.IsCompleted) yield return null;
                Assert.That(begin.Result.Succeeded, Is.True, begin.Result.Message);

                RestStopServicesPanelController controller = null;
                yield return LoadExpedition(value => controller = value);
                SimulatePointerClick(controller.AccessButton);
                Button enterRestButton = FindButton(controller, "EnterRestStop");
                Assert.That(enterRestButton, Is.Not.Null);
                SimulatePointerClick(enterRestButton);
                yield return WaitUntil(() => !controller.IsBusy);

                Button buyMarkedCoinButton = FindButton(controller, "Buy_relic_marked_coin");
                Assert.That(buyMarkedCoinButton, Is.Not.Null);
                string basePriceLabel = buyMarkedCoinButton.GetComponentInChildren<Text>(true).text;
                StringAssert.Contains("50", basePriceLabel,
                    "Without the Merchant Seal, the 50-gold offer should retain its base purchase price.");
                evidence.RecordAssertion("Without the Merchant Seal the offer shows its 50-gold base price",
                    basePriceLabel.Contains("50"), "50 gold", basePriceLabel);
                evidence.RecordInput(2, "PointerClick", "Buy the 50-gold relic offer through the real rest-stop panel");
                SimulatePointerClick(buyMarkedCoinButton);
                yield return WaitUntil(() => !controller.IsBusy);
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(50),
                    "The durable merchant purchase should debit the undiscounted 50-gold amount.");
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin"), Is.EqualTo(1));
                Assert.That(app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity,
                    Is.Zero);
                evidence.RecordAssertion("The undiscounted durable purchase debits 50 gold once",
                    app.ActiveRun.Inventory.Gold == 50 &&
                    app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin") == 1 &&
                    app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity == 0,
                    "50 gold remains and one relic was purchased", "gold=" + app.ActiveRun.Inventory.Gold +
                    "; marked coins=" + app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == "relic.marked_coin") +
                    "; stock=" + app.GetActiveMerchantStock().offers.Single(offer => offer.itemId == "relic.marked_coin").remainingQuantity);
                Assert.That(evidence.Complete(), Is.Zero, "E03 base-price scenario failed. Evidence: " + evidence.ReportPath);
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
            for (int frame = 0; frame < 90; frame++)
            {
                yield return null;
                if (SceneManager.GetActiveScene().name != "Expedition") continue;
                controller = UnityEngine.Object.FindAnyObjectByType<RestStopServicesPanelController>();
                if (controller != null && controller.AccessButton != null &&
                    controller.AccessButton.gameObject.scene == SceneManager.GetActiveScene()) break;
            }
            Assert.That(controller, Is.Not.Null, "ExpeditionRunSessionPresenter must create the E03 service panel.");
            Assert.That(controller.AccessButton, Is.Not.Null, "The E03 service panel must finish creating its scene-owned entry button.");
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

        private static void EquipRelic(ExpeditionInventory inventory, string relicId, EquipmentSlot equipmentSlot)
        {
            var relic = new InventoryItemStack(ItemInstanceId.New(), relicId, ItemRarity.Common, 0, 1);
            Assert.That(inventory.TryCollectItem(relic, TransactionId.New()).Succeeded, Is.True,
                "The test fixture should collect the support relic before equipping it.");
            int backpackIndex = inventory.Backpack.ToList().FindIndex(item => item?.InstanceId == relic.InstanceId);
            Assert.That(backpackIndex, Is.GreaterThanOrEqualTo(0));
            InventoryOperationOutcome equipped = inventory.EquipBackpackItem(backpackIndex, equipmentSlot, TransactionId.New());
            Assert.That(equipped.Succeeded, Is.True, "The support relic should occupy the selected fixture equipment slot.");
        }

        private static Button FindButton(RestStopServicesPanelController controller, string name)
        {
            if (controller == null || controller.AccessButton == null || controller.AccessButton.transform.parent == null)
                return null;
            Button[] buttons = controller.AccessButton.transform.parent.GetComponentsInChildren<Button>(true);
            for (int index = buttons.Length - 1; index >= 0; index--)
                if (buttons[index].gameObject.name == name) return buttons[index];
            return null;
        }

        private static string PendingEffectIdAt(ApplicationAudioRouter router, int index)
        {
            FieldInfo field = typeof(ApplicationAudioRouter).GetField("_pendingEffectRequests",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var requests = (IList)field.GetValue(router);
            object request = requests[index];
            return (string)request.GetType().GetProperty("EffectId").GetValue(request, null);
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
