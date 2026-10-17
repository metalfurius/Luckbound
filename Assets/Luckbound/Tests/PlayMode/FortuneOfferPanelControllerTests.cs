using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class FortuneOfferPanelControllerTests
    {
        private static readonly string[] ClaimableOptions =
        {
            "relic.marked_coin", "relic.hidden_pouch", "consumable.energy_tonic", "consumable.health_potion"
        };

        private static readonly string[] InsufficientOptionPool =
        {
            "relic.marked_coin", "relic.hidden_pouch", "consumable.energy_tonic"
        };

        [UnityTest]
        public IEnumerator DisplaysThreePersistedOptionsLocksChoiceAndClaimsRewardOnlyOnce()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            PanelFixture panel = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app);

                ExpeditionMutationCommitResult<FortuneOfferResult> created = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.claim", "context.panel.claim", ClaimableOptions),
                    value => created = value);
                Assert.That(created.Persisted, Is.True, created.Message);

                panel = CreatePanel(app, "offer.panel.claim");
                FortuneOffer offer = app.GetFortuneOffer("offer.panel.claim");
                Assert.That(panel.OptionLabels.Select(label => label.text), Is.EqualTo(offer.OptionIds));
                Assert.That(panel.OptionButtons.Count(button => button.interactable), Is.EqualTo(3));
                Assert.That(panel.RepeatButton.interactable, Is.True);
                Assert.That(panel.RepeatAvailabilityText.text, Is.EqualTo("Available"));
                Assert.That(panel.ClaimButton.interactable, Is.False);

                string selectedItemId = offer.OptionIds[1];
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneChoiceResult> pendingChoice = null;
                yield return Complete(panel.Controller.ChooseOptionAsync(1), value => pendingChoice = value);
                Assert.That(pendingChoice.Persisted, Is.False);
                Assert.That(pendingChoice.Pending, Is.True);
                Assert.That(panel.ClaimButton.interactable, Is.False);
                Assert.That(panel.OptionButtons[1].interactable, Is.True,
                    "The same option remains available as the exact save retry.");

                panel.Destroy();
                panel = CreatePanel(app, "offer.panel.claim");
                Assert.That(panel.OptionButtons[1].interactable, Is.True,
                    "Recreated panels expose only the option captured by the pending save.");
                Assert.That(panel.OptionButtons.Where((button, index) => index != 1 && button.interactable), Is.Empty);
                Assert.That(panel.ClaimButton.interactable, Is.False);
                StringAssert.Contains("GUARDADO PENDIENTE", panel.Controller.StatusText);

                ExpeditionMutationCommitResult<FortuneChoiceResult> choice = null;
                ExpeditionMutationCommitResult<FortuneChoiceResult> wrongChoice = null;
                yield return Complete(panel.Controller.ChooseOptionAsync(0), value => wrongChoice = value);
                Assert.That(wrongChoice, Is.Null,
                    "A recreated panel must reject a different option while the exact choice write is pending.");
                yield return Complete(panel.Controller.ChooseOptionAsync(1), value => choice = value);
                Assert.That(choice.Persisted, Is.True, choice.Message);
                Assert.That(choice.Outcome.Succeeded, Is.True);
                Assert.That(panel.Controller.CurrentOffer.SelectedOptionId, Is.EqualTo(selectedItemId));
                Assert.That(panel.RepeatButton.interactable, Is.False);
                StringAssert.Contains("OfferLocked", panel.RepeatAvailabilityText.text);
                Assert.That(panel.ClaimButton.interactable, Is.True);

                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> pendingClaim = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => pendingClaim = value);
                Assert.That(pendingClaim.Persisted, Is.False);
                Assert.That(pendingClaim.Pending, Is.True);
                Assert.That(app.GetFortuneOffer("offer.panel.claim").IsClaimed, Is.False);
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == selectedItemId), Is.Zero);

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> claim = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => claim = value);
                Assert.That(claim.Persisted, Is.True, claim.Message);
                Assert.That(claim.Outcome.Succeeded, Is.True);
                Assert.That(app.GetFortuneOffer("offer.panel.claim").IsClaimed, Is.True);
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == selectedItemId), Is.EqualTo(1));
                Assert.That(panel.ClaimButton.interactable, Is.False);

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> duplicateClaim = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => duplicateClaim = value);
                Assert.That(duplicateClaim, Is.Null, "A claimed offer must not be claimable from the panel again.");
                Assert.That(app.ActiveRun.Inventory.Backpack.Count(item => item?.ItemId == selectedItemId), Is.EqualTo(1));
            }
            finally
            {
                if (panel != null) panel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator ShowsEveryCoreRerollUnavailableReason()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            PanelFixture insufficientPanel = null;
            PanelFixture noChargesPanel = null;
            PanelFixture unknownPanel = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app);

                ExpeditionMutationCommitResult<FortuneOfferResult> insufficientOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.insufficient", "context.panel.insufficient", InsufficientOptionPool),
                    value => insufficientOffer = value);
                Assert.That(insufficientOffer.Persisted, Is.True, insufficientOffer.Message);
                insufficientPanel = CreatePanel(app, "offer.panel.insufficient");
                Assert.That(insufficientPanel.Controller.RerollAvailability,
                    Is.EqualTo(FortuneRerollAvailability.InsufficientOptions));
                Assert.That(insufficientPanel.RepeatButton.interactable, Is.False);
                StringAssert.Contains("InsufficientOptions", insufficientPanel.RepeatAvailabilityText.text);

                ExpeditionMutationCommitResult<FortuneOfferResult> chargeOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.no-charges", "context.panel.no-charges", ClaimableOptions),
                    value => chargeOffer = value);
                Assert.That(chargeOffer.Persisted, Is.True, chargeOffer.Message);
                noChargesPanel = CreatePanel(app, "offer.panel.no-charges");
                for (int repeatIndex = 0; repeatIndex < 2; repeatIndex++)
                {
                    ExpeditionMutationCommitResult<FortuneRerollResult> repeated = null;
                    yield return Complete(noChargesPanel.Controller.RepeatOfferAsync(), value => repeated = value);
                    Assert.That(repeated.Persisted, Is.True, repeated.Message);
                    Assert.That(repeated.Outcome.Succeeded, Is.True);
                }
                Assert.That(noChargesPanel.Controller.RerollAvailability, Is.EqualTo(FortuneRerollAvailability.NoCharges));
                Assert.That(noChargesPanel.RepeatButton.interactable, Is.False);
                StringAssert.Contains("NoCharges", noChargesPanel.RepeatAvailabilityText.text);

                unknownPanel = CreatePanel(app, "offer.panel.missing");
                Assert.That(unknownPanel.Controller.RerollAvailability, Is.EqualTo(FortuneRerollAvailability.UnknownOffer));
                Assert.That(unknownPanel.RepeatButton.interactable, Is.False);
                StringAssert.Contains("UnknownOffer", unknownPanel.RepeatAvailabilityText.text);
            }
            finally
            {
                if (insufficientPanel != null) insufficientPanel.Destroy();
                if (noChargesPanel != null) noChargesPanel.Destroy();
                if (unknownPanel != null) unknownPanel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator PendingRerollRetryReusesItsTransactionIdAndCommitsOneNewOffer()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            PanelFixture panel = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app);

                ExpeditionMutationCommitResult<FortuneOfferResult> created = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.retry", "context.panel.retry", ClaimableOptions),
                    value => created = value);
                Assert.That(created.Persisted, Is.True, created.Message);
                string[] beforeRetry = created.Outcome.Offer.OptionIds.ToArray();
                panel = CreatePanel(app, "offer.panel.retry");

                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRerollResult> firstAttempt = null;
                yield return Complete(panel.Controller.RepeatOfferAsync(), value => firstAttempt = value);
                Assert.That(firstAttempt.Persisted, Is.False);
                Assert.That(firstAttempt.Pending, Is.True);
                Assert.That(panel.Controller.HasPendingRerollRetry, Is.True);
                TransactionId originalTransactionId = panel.Controller.LastRerollTransactionId;
                Assert.That(originalTransactionId.IsValid, Is.True);
                Assert.That(app.GetFortuneOffer("offer.panel.retry").OptionIds, Is.EqualTo(beforeRetry));

                panel.Destroy();
                panel = CreatePanel(app, "offer.panel.retry");
                ExpeditionMutationCommitResult<FortuneRerollResult> retry = null;
                yield return Complete(panel.Controller.RepeatOfferAsync(), value => retry = value);
                Assert.That(panel.Controller.LastRerollTransactionId, Is.EqualTo(originalTransactionId));
                Assert.That(retry.Persisted, Is.True, retry.Message);
                Assert.That(retry.Outcome.Succeeded, Is.True);
                Assert.That(panel.Controller.HasPendingRerollRetry, Is.False);
                Assert.That(panel.Controller.CurrentOffer.RerollCount, Is.EqualTo(1));
                Assert.That(panel.Controller.CurrentOffer.OptionIds, Is.Not.EqualTo(beforeRetry));
            }
            finally
            {
                if (panel != null) panel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        private static PanelFixture CreatePanel(ApplicationBootstrapper app, string offerId)
        {
            var optionButtons = new Button[3];
            var optionLabels = new Text[3];
            for (int index = 0; index < 3; index++)
            {
                optionButtons[index] = new GameObject("FortuneOptionButton" + index).AddComponent<Button>();
                optionLabels[index] = new GameObject("FortuneOptionLabel" + index).AddComponent<Text>();
            }
            Button repeatButton = new GameObject("FortuneRepeatButton").AddComponent<Button>();
            Button claimButton = new GameObject("FortuneClaimButton").AddComponent<Button>();
            Text repeatAvailabilityText = new GameObject("FortuneRepeatAvailability").AddComponent<Text>();
            Text statusText = new GameObject("FortuneStatus").AddComponent<Text>();
            var controllerObject = new GameObject("FortuneOfferPanelController");
            FortuneOfferPanelController controller = controllerObject.AddComponent<FortuneOfferPanelController>();
            controller.Initialize(app, offerId, optionButtons, optionLabels, repeatButton, claimButton,
                repeatAvailabilityText, statusText);
            return new PanelFixture(controller, optionButtons, optionLabels, repeatButton, claimButton,
                repeatAvailabilityText, statusText, controllerObject);
        }

        private static IEnumerator BeginExpedition(ApplicationBootstrapper app)
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.Create("class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            SaveWriteResult result = null;
            yield return Complete(app.BeginExpeditionAsync(run), value => result = value);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static IEnumerator StartApplication(Action<ApplicationBootstrapper> ready)
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
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

        private static void DestroyApplication()
        {
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
        }

        private sealed class PanelFixture
        {
            private readonly GameObject _controllerObject;
            private readonly Text _repeatAvailabilityText;
            private readonly Text _statusText;

            public PanelFixture(FortuneOfferPanelController controller, Button[] optionButtons,
                Text[] optionLabels, Button repeatButton, Button claimButton, Text repeatAvailabilityText,
                Text statusText, GameObject controllerObject)
            {
                Controller = controller;
                OptionButtons = optionButtons;
                OptionLabels = optionLabels;
                RepeatButton = repeatButton;
                ClaimButton = claimButton;
                _repeatAvailabilityText = repeatAvailabilityText;
                _statusText = statusText;
                _controllerObject = controllerObject;
            }

            public FortuneOfferPanelController Controller { get; }
            public Button[] OptionButtons { get; }
            public Text[] OptionLabels { get; }
            public Button RepeatButton { get; }
            public Button ClaimButton { get; }
            public Text RepeatAvailabilityText => _repeatAvailabilityText;

            public void Destroy()
            {
                UnityEngine.Object.Destroy(_controllerObject);
                foreach (Button button in OptionButtons) UnityEngine.Object.Destroy(button.gameObject);
                foreach (Text label in OptionLabels) UnityEngine.Object.Destroy(label.gameObject);
                UnityEngine.Object.Destroy(RepeatButton.gameObject);
                UnityEngine.Object.Destroy(ClaimButton.gameObject);
                UnityEngine.Object.Destroy(_repeatAvailabilityText.gameObject);
                UnityEngine.Object.Destroy(_statusText.gameObject);
            }
        }

        private sealed class MemorySaveStore : ISaveStore
        {
            private string _profilePrimary;
            private string _profileBackup;
            private string _settingsPrimary;
            private string _settingsBackup;

            public bool FailNextProfileWrite { get; set; }

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(_profilePrimary, _profileBackup)
                    : new SaveStorageSnapshot(_settingsPrimary, _settingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile && FailNextProfileWrite)
                {
                    FailNextProfileWrite = false;
                    throw new IOException("simulated profile write failure");
                }

                if (document == SaveDocument.Profile)
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _profileBackup = lastKnownGoodPrimaryJson;
                    _profilePrimary = json;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _settingsBackup = lastKnownGoodPrimaryJson;
                    _settingsPrimary = json;
                }
                return Task.CompletedTask;
            }
        }
    }
}
