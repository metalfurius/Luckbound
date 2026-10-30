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

                int selectedItemQuantityBeforeClaim = GetBackpackItemQuantity(app, selectedItemId);
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> pendingClaim = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => pendingClaim = value);
                Assert.That(pendingClaim.Persisted, Is.False);
                Assert.That(pendingClaim.Pending, Is.True);
                Assert.That(app.GetFortuneOffer("offer.panel.claim").IsClaimed, Is.False);
                Assert.That(GetBackpackItemQuantity(app, selectedItemId), Is.EqualTo(selectedItemQuantityBeforeClaim),
                    "A failed save must leave the selected item quantity unchanged, including an item already carried.");

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> claim = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => claim = value);
                Assert.That(claim.Persisted, Is.True, claim.Message);
                Assert.That(claim.Outcome.Succeeded, Is.True);
                Assert.That(app.GetFortuneOffer("offer.panel.claim").IsClaimed, Is.True);
                Assert.That(GetBackpackItemQuantity(app, selectedItemId), Is.EqualTo(selectedItemQuantityBeforeClaim + 1));
                Assert.That(panel.ClaimButton.interactable, Is.False);

                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> duplicateClaim = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => duplicateClaim = value);
                Assert.That(duplicateClaim, Is.Null, "A claimed offer must not be claimable from the panel again.");
                Assert.That(GetBackpackItemQuantity(app, selectedItemId), Is.EqualTo(selectedItemQuantityBeforeClaim + 1));
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

        [UnityTest]
        public IEnumerator ReadOnlyV6ProfileDisablesFreshPanelActionsForUnclaimedAndChosenOffers()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            PanelFixture unclaimedPanel = null;
            PanelFixture chosenPanel = null;
            try
            {
                ApplicationBootstrapper app = null;
                string selectedOptionId = null;
                yield return CreateReadOnlyV6Offers(store, (value, selected) =>
                {
                    app = value;
                    selectedOptionId = selected;
                });

                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.CanWriteProfile, Is.False);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(app.GetFortuneOffer("offer.panel.v6.unclaimed").IsClaimed, Is.False);
                Assert.That(app.GetFortuneOffer("offer.panel.v6.chosen").SelectedOptionId, Is.EqualTo(selectedOptionId));

                unclaimedPanel = CreatePanel(app, "offer.panel.v6.unclaimed");
                chosenPanel = CreatePanel(app, "offer.panel.v6.chosen");
                Assert.That(unclaimedPanel.OptionButtons.Count(button => button.interactable), Is.Zero);
                Assert.That(unclaimedPanel.RepeatButton.interactable, Is.False);
                Assert.That(unclaimedPanel.ClaimButton.interactable, Is.False);
                Assert.That(chosenPanel.OptionButtons.Count(button => button.interactable), Is.Zero);
                Assert.That(chosenPanel.RepeatButton.interactable, Is.False);
                Assert.That(chosenPanel.ClaimButton.interactable, Is.False);

                string primaryBefore = store.ProfilePrimary;
                string backupBefore = store.ProfileBackup;
                string profileBefore = JsonUtility.ToJson(app.Profile, false);
                string expeditionBefore = JsonUtility.ToJson(app.ActiveExpedition, false);
                int writesBefore = store.ProfileWriteAttempts;
                store.FailNextProfileWrite = true;

                ExpeditionMutationCommitResult<FortuneChoiceResult> blockedUnclaimedChoice = null;
                yield return Complete(unclaimedPanel.Controller.ChooseOptionAsync(0), value => blockedUnclaimedChoice = value);
                ExpeditionMutationCommitResult<FortuneRerollResult> blockedUnclaimedReroll = null;
                yield return Complete(unclaimedPanel.Controller.RepeatOfferAsync(), value => blockedUnclaimedReroll = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> blockedUnclaimedClaim = null;
                yield return Complete(unclaimedPanel.Controller.ClaimRewardAsync(), value => blockedUnclaimedClaim = value);
                ExpeditionMutationCommitResult<FortuneChoiceResult> blockedChosenChoice = null;
                yield return Complete(chosenPanel.Controller.ChooseOptionAsync(0), value => blockedChosenChoice = value);
                ExpeditionMutationCommitResult<FortuneRerollResult> blockedChosenReroll = null;
                yield return Complete(chosenPanel.Controller.RepeatOfferAsync(), value => blockedChosenReroll = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> blockedChosenClaim = null;
                yield return Complete(chosenPanel.Controller.ClaimRewardAsync(), value => blockedChosenClaim = value);

                Assert.That(blockedUnclaimedChoice, Is.Null);
                Assert.That(blockedUnclaimedReroll, Is.Null);
                Assert.That(blockedUnclaimedClaim, Is.Null);
                Assert.That(blockedChosenChoice, Is.Null);
                Assert.That(blockedChosenReroll, Is.Null);
                Assert.That(blockedChosenClaim, Is.Null);
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBefore));
                Assert.That(store.FailNextProfileWrite, Is.True);
                Assert.That(store.ProfilePrimary, Is.EqualTo(primaryBefore));
                Assert.That(store.ProfileBackup, Is.EqualTo(backupBefore));
                Assert.That(JsonUtility.ToJson(app.Profile, false), Is.EqualTo(profileBefore));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(expeditionBefore));
                Assert.That(app.PendingProfileExport, Is.Null);
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.PendingFortuneOperationKey, Is.Empty);
                Assert.That(unclaimedPanel.Controller.HasPendingRerollRetry, Is.False);
                Assert.That(unclaimedPanel.Controller.LastRerollTransactionId.IsValid, Is.False);
                Assert.That(chosenPanel.Controller.LastRerollTransactionId.IsValid, Is.False);
                Assert.That(unclaimedPanel.Controller.StatusText, Is.Not.Empty);
                Assert.That(chosenPanel.Controller.StatusText, Is.Not.Empty);
            }
            finally
            {
                if (unclaimedPanel != null) unclaimedPanel.Destroy();
                if (chosenPanel != null) chosenPanel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator PendingFortuneMutationForAnotherOfferBlocksDestinationPanelUntilApiRetry()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            PanelFixture destinationPanel = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app);

                ExpeditionMutationCommitResult<FortuneOfferResult> destination = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.destination", "context.panel.destination", ClaimableOptions),
                    value => destination = value);
                ExpeditionMutationCommitResult<FortuneOfferResult> other = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.other", "context.panel.other", ClaimableOptions),
                    value => other = value);
                Assert.That(destination.Persisted, Is.True, destination.Message);
                Assert.That(other.Persisted, Is.True, other.Message);

                string[] destinationOptions = app.GetFortuneOffer("offer.panel.destination").OptionIds.ToArray();
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;
                string profileBeforePending = store.ProfilePrimary;
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneChoiceResult> pendingOtherChoice = null;
                string otherOptionId = app.GetFortuneOffer("offer.panel.other").OptionIds[1];
                yield return Complete(app.ChooseFortuneOptionAsync("offer.panel.other", otherOptionId),
                    value => pendingOtherChoice = value);
                Assert.That(pendingOtherChoice.Persisted, Is.False);
                Assert.That(pendingOtherChoice.Pending, Is.True);

                string pendingKey = app.PendingFortuneOperationKey;
                string pendingJson = app.PendingProfileExport;
                int writesBeforePanel = store.ProfileWriteAttempts;
                destinationPanel = CreatePanel(app, "offer.panel.destination");
                Assert.That(destinationPanel.OptionButtons.Count(button => button.interactable), Is.Zero);
                Assert.That(destinationPanel.RepeatButton.interactable, Is.False);
                Assert.That(destinationPanel.ClaimButton.interactable, Is.False);

                ExpeditionMutationCommitResult<FortuneChoiceResult> blockedChoice = null;
                yield return Complete(destinationPanel.Controller.ChooseOptionAsync(0), value => blockedChoice = value);
                ExpeditionMutationCommitResult<FortuneRerollResult> blockedReroll = null;
                yield return Complete(destinationPanel.Controller.RepeatOfferAsync(), value => blockedReroll = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> blockedClaim = null;
                yield return Complete(destinationPanel.Controller.ClaimRewardAsync(), value => blockedClaim = value);
                Assert.That(blockedChoice, Is.Null);
                Assert.That(blockedReroll, Is.Null);
                Assert.That(blockedClaim, Is.Null);
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforePanel));
                Assert.That(store.ProfilePrimary, Is.EqualTo(profileBeforePending));
                Assert.That(app.PendingFortuneOperationKey, Is.EqualTo(pendingKey));
                Assert.That(app.PendingProfileExport, Is.EqualTo(pendingJson));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore));
                Assert.That(app.GetFortuneOffer("offer.panel.destination").OptionIds, Is.EqualTo(destinationOptions));

                ExpeditionMutationCommitResult<FortuneChoiceResult> resolved = null;
                yield return Complete(app.RetryPendingFortuneMutationAsync<FortuneChoiceResult>(pendingKey),
                    value => resolved = value);
                Assert.That(resolved.Persisted, Is.True, resolved.Message);
                Assert.That(resolved.Pending, Is.False);
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.GetFortuneOffer("offer.panel.other").SelectedOptionId, Is.EqualTo(otherOptionId));
            }
            finally
            {
                if (destinationPanel != null) destinationPanel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator OwnPendingChoiceRerollAndClaimExposeOnlyTheirExactRetry()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            PanelFixture choicePanel = null;
            PanelFixture rerollPanel = null;
            PanelFixture publicationPanel = null;
            IDisposable pauseProviderBinding = null;
            IDisposable publicationBinding = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value);
                yield return BeginExpedition(app);

                ExpeditionMutationCommitResult<FortuneOfferResult> claimOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.own-claim", "context.panel.own-claim", ClaimableOptions),
                    value => claimOffer = value);
                ExpeditionMutationCommitResult<FortuneOfferResult> rerollOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.own-reroll", "context.panel.own-reroll", ClaimableOptions),
                    value => rerollOffer = value);
                Assert.That(claimOffer.Persisted, Is.True, claimOffer.Message);
                Assert.That(rerollOffer.Persisted, Is.True, rerollOffer.Message);

                choicePanel = CreatePanel(app, "offer.panel.own-claim");
                string selectedOptionId = choicePanel.Controller.CurrentOffer.OptionIds[1];
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneChoiceResult> pendingChoice = null;
                yield return Complete(choicePanel.Controller.ChooseOptionAsync(1), value => pendingChoice = value);
                Assert.That(pendingChoice.Pending, Is.True);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.True);
                string choiceKey = app.PendingFortuneOperationKey;
                string choicePendingJson = app.PendingProfileExport;
                Assert.That(choicePanel.OptionButtons[1].interactable, Is.True);
                Assert.That(choicePanel.OptionButtons.Where((button, index) => index != 1 && button.interactable), Is.Empty);
                Assert.That(choicePanel.RepeatButton.interactable, Is.False);
                Assert.That(choicePanel.ClaimButton.interactable, Is.False);

                choicePanel.Destroy();
                choicePanel = CreatePanel(app, "offer.panel.own-claim");
                Assert.That(app.PendingFortuneOperationKey, Is.EqualTo(choiceKey));
                Assert.That(app.PendingProfileExport, Is.EqualTo(choicePendingJson));
                Assert.That(choicePanel.OptionButtons[1].interactable, Is.True);
                Assert.That(choicePanel.OptionButtons.Where((button, index) => index != 1 && button.interactable), Is.Empty);
                ExpeditionMutationCommitResult<FortuneChoiceResult> wrongChoice = null;
                yield return Complete(choicePanel.Controller.ChooseOptionAsync(0), value => wrongChoice = value);
                Assert.That(wrongChoice, Is.Null);
                int writesBeforeChoiceRetry = store.ProfileWriteAttempts;
                ExpeditionMutationCommitResult<FortuneChoiceResult> chosen = null;
                yield return Complete(choicePanel.Controller.ChooseOptionAsync(1), value => chosen = value);
                Assert.That(chosen.Persisted, Is.True, chosen.Message);
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.GetFortuneOffer("offer.panel.own-claim").SelectedOptionId, Is.EqualTo(selectedOptionId));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeChoiceRetry + 1));

                rerollPanel = CreatePanel(app, "offer.panel.own-reroll");
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRerollResult> pendingReroll = null;
                yield return Complete(rerollPanel.Controller.RepeatOfferAsync(), value => pendingReroll = value);
                Assert.That(pendingReroll.Pending, Is.True);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.True);
                string rerollKey = app.PendingFortuneOperationKey;
                string rerollPendingJson = app.PendingProfileExport;
                TransactionId originalTransactionId = rerollPanel.Controller.LastRerollTransactionId;
                Assert.That(originalTransactionId.IsValid, Is.True);
                Assert.That(rerollPanel.Controller.HasPendingRerollRetry, Is.True);
                Assert.That(rerollPanel.RepeatButton.interactable, Is.True);
                Assert.That(rerollPanel.OptionButtons.Count(button => button.interactable), Is.Zero);
                Assert.That(rerollPanel.ClaimButton.interactable, Is.False);

                rerollPanel.Destroy();
                rerollPanel = CreatePanel(app, "offer.panel.own-reroll");
                Assert.That(app.PendingFortuneOperationKey, Is.EqualTo(rerollKey));
                Assert.That(app.PendingProfileExport, Is.EqualTo(rerollPendingJson));
                Assert.That(rerollPanel.Controller.LastRerollTransactionId, Is.EqualTo(originalTransactionId));
                ExpeditionMutationCommitResult<FortuneChoiceResult> competingChoice = null;
                yield return Complete(rerollPanel.Controller.ChooseOptionAsync(0), value => competingChoice = value);
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> competingClaim = null;
                yield return Complete(rerollPanel.Controller.ClaimRewardAsync(), value => competingClaim = value);
                Assert.That(competingChoice, Is.Null);
                Assert.That(competingClaim, Is.Null);
                int writesBeforeRerollRetry = store.ProfileWriteAttempts;
                ExpeditionMutationCommitResult<FortuneRerollResult> rerolled = null;
                yield return Complete(rerollPanel.Controller.RepeatOfferAsync(), value => rerolled = value);
                Assert.That(rerolled.Persisted, Is.True, rerolled.Message);
                Assert.That(rerollPanel.Controller.LastRerollTransactionId, Is.EqualTo(originalTransactionId));
                Assert.That(rerollPanel.Controller.HasPendingRerollRetry, Is.False);
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore - 1));
                Assert.That(rerollPanel.Controller.CurrentOffer.RerollCount, Is.EqualTo(1));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeRerollRetry + 1));

                string chosenItemId = selectedOptionId;
                int rewardQuantityBefore = GetBackpackItemQuantity(app, chosenItemId);
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> pendingClaim = null;
                yield return Complete(choicePanel.Controller.ClaimRewardAsync(), value => pendingClaim = value);
                Assert.That(pendingClaim.Pending, Is.True);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.True);
                string claimKey = app.PendingFortuneOperationKey;
                string claimPendingJson = app.PendingProfileExport;
                Assert.That(choicePanel.ClaimButton.interactable, Is.True);
                Assert.That(choicePanel.OptionButtons.Count(button => button.interactable), Is.Zero);
                Assert.That(choicePanel.RepeatButton.interactable, Is.False);

                choicePanel.Destroy();
                choicePanel = CreatePanel(app, "offer.panel.own-claim");
                Assert.That(app.PendingFortuneOperationKey, Is.EqualTo(claimKey));
                Assert.That(app.PendingProfileExport, Is.EqualTo(claimPendingJson));
                Assert.That(choicePanel.ClaimButton.interactable, Is.True);
                Assert.That(choicePanel.OptionButtons.Count(button => button.interactable), Is.Zero);
                int writesBeforeCompetingClaimActions = store.ProfileWriteAttempts;
                ExpeditionMutationCommitResult<FortuneChoiceResult> competingClaimChoice = null;
                yield return Complete(choicePanel.Controller.ChooseOptionAsync(0),
                    value => competingClaimChoice = value);
                ExpeditionMutationCommitResult<FortuneRerollResult> competingClaimReroll = null;
                yield return Complete(choicePanel.Controller.RepeatOfferAsync(),
                    value => competingClaimReroll = value);
                Assert.That(competingClaimChoice, Is.Null);
                Assert.That(competingClaimReroll, Is.Null);
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeCompetingClaimActions));
                int writesBeforeClaimRetry = store.ProfileWriteAttempts;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> claimed = null;
                yield return Complete(choicePanel.Controller.ClaimRewardAsync(), value => claimed = value);
                Assert.That(claimed.Persisted, Is.True, claimed.Message);
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.GetFortuneOffer("offer.panel.own-claim").IsClaimed, Is.True);
                Assert.That(GetBackpackItemQuantity(app, chosenItemId), Is.EqualTo(rewardQuantityBefore + 1));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeClaimRetry + 1));

                ExpeditionMutationCommitResult<FortuneOfferResult> publicationOffer = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.own-publication",
                    "context.panel.own-publication", ClaimableOptions), value => publicationOffer = value);
                Assert.That(publicationOffer.Persisted, Is.True, publicationOffer.Message);
                publicationPanel = CreatePanel(app, "offer.panel.own-publication");
                int publicationOptionIndex = 1;
                string publicationOptionId = publicationPanel.Controller.CurrentOffer.OptionIds[publicationOptionIndex];
                object publisherOwner = new object();
                int pauseLeasesAcquired = 0;
                int pauseLeasesReleased = 0;
                int captureCalls = 0;
                int preparationCalls = 0;
                int publicationAttempts = 0;
                int successfulPublications = 0;
                bool failFirstPublication = true;
                string publishedOptionId = string.Empty;
                pauseProviderBinding = app.BindExpeditionSimulationPauseLeaseProvider(publisherOwner, () =>
                {
                    pauseLeasesAcquired++;
                    return new CallbackDisposable(() => pauseLeasesReleased++);
                });
                publicationBinding = app.BindExpeditionSessionMutationPublication(publisherOwner,
                    (candidateRun, candidateSave) =>
                    {
                        captureCalls++;
                        Assert.That(candidateRun, Is.Not.Null);
                        Assert.That(candidateSave, Is.Not.Null);
                        Assert.That(candidateRun.RunId, Is.EqualTo(app.ActiveRun.RunId));
                    },
                    (candidateRun, candidateSave) =>
                    {
                        preparationCalls++;
                        Assert.That(candidateRun, Is.Not.Null);
                        Assert.That(candidateSave, Is.Not.Null);
                        FortuneOffer candidateOffer = FortuneOfferBook.Restore(
                            candidateSave.fortuneOfferBookSnapshot).GetOffer("offer.panel.own-publication");
                        Assert.That(candidateOffer.SelectedOptionId, Is.EqualTo(publicationOptionId));
                        string preparedOptionId = candidateOffer.SelectedOptionId;
                        return () =>
                        {
                            publicationAttempts++;
                            if (failFirstPublication)
                            {
                                failFirstPublication = false;
                                throw new InvalidOperationException("simulated session publication fault");
                            }
                            publishedOptionId = preparedOptionId;
                            successfulPublications++;
                        };
                    });

                int writesBeforePublication = store.ProfileWriteAttempts;
                ExpeditionMutationCommitResult<FortuneChoiceResult> failedPublication = null;
                yield return Complete(publicationPanel.Controller.ChooseOptionAsync(publicationOptionIndex),
                    value => failedPublication = value);
                Assert.That(failedPublication.Persisted, Is.False);
                Assert.That(failedPublication.Pending, Is.True, failedPublication.Message);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.True);
                Assert.That(app.HasPendingFortuneMutation, Is.True);
                string publicationKey = app.PendingFortuneOperationKey;
                StringAssert.Contains(publicationOptionId, publicationKey);
                Assert.That(app.PendingProfileExport, Is.Null,
                    "The save already reached durable storage; only its session publication is pending.");
                Assert.That(app.GetFortuneOffer("offer.panel.own-publication").SelectedOptionId,
                    Is.EqualTo(publicationOptionId));
                string durableExpeditionJson = JsonUtility.ToJson(app.ActiveExpedition, false);
                string durableProfileJson = store.ProfilePrimary;
                string durableBackupJson = store.ProfileBackup;
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforePublication + 1));
                Assert.That(pauseLeasesAcquired, Is.EqualTo(1));
                Assert.That(pauseLeasesReleased, Is.Zero);
                Assert.That(captureCalls, Is.EqualTo(1));
                Assert.That(preparationCalls, Is.EqualTo(1));
                Assert.That(publicationAttempts, Is.EqualTo(1));
                Assert.That(successfulPublications, Is.Zero);

                publicationPanel.Destroy();
                publicationPanel = CreatePanel(app, "offer.panel.own-publication");
                Assert.That(app.PendingFortuneOperationKey, Is.EqualTo(publicationKey));
                Assert.That(publicationPanel.Controller.CurrentOffer.SelectedOptionId,
                    Is.EqualTo(publicationOptionId));
                Assert.That(publicationPanel.OptionButtons[publicationOptionIndex].interactable, Is.True);
                Assert.That(publicationPanel.OptionButtons.Where((button, index) => index != publicationOptionIndex &&
                    button.interactable), Is.Empty);
                Assert.That(publicationPanel.RepeatButton.interactable, Is.False);
                Assert.That(publicationPanel.ClaimButton.interactable, Is.False);

                int otherPublicationOptionIndex = (publicationOptionIndex + 1) % 3;
                ExpeditionMutationCommitResult<FortuneChoiceResult> wrongPublicationRetry = null;
                yield return Complete(publicationPanel.Controller.ChooseOptionAsync(otherPublicationOptionIndex),
                    value => wrongPublicationRetry = value);
                Assert.That(wrongPublicationRetry, Is.Null);
                Assert.That(app.PendingFortuneOperationKey, Is.EqualTo(publicationKey));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforePublication + 1));
                Assert.That(publicationAttempts, Is.EqualTo(1));

                int writesBeforePublicationRetry = store.ProfileWriteAttempts;
                ExpeditionMutationCommitResult<FortuneChoiceResult> publishedChoice = null;
                yield return Complete(publicationPanel.Controller.ChooseOptionAsync(publicationOptionIndex),
                    value => publishedChoice = value);
                Assert.That(publishedChoice.Persisted, Is.True, publishedChoice.Message);
                Assert.That(publishedChoice.Pending, Is.False);
                Assert.That(publishedChoice.Outcome.Succeeded, Is.True);
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
                Assert.That(app.PendingFortuneOperationKey, Is.Empty);
                Assert.That(app.PendingProfileExport, Is.Null);
                Assert.That(app.CanContinueExpedition, Is.True);
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforePublicationRetry),
                    "Retrying a durable choice republishes the session without a second save write.");
                Assert.That(store.ProfilePrimary, Is.EqualTo(durableProfileJson));
                Assert.That(store.ProfileBackup, Is.EqualTo(durableBackupJson));
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(durableExpeditionJson));
                Assert.That(publicationAttempts, Is.EqualTo(2));
                Assert.That(successfulPublications, Is.EqualTo(1));
                Assert.That(publishedOptionId, Is.EqualTo(publicationOptionId));
                Assert.That(captureCalls, Is.EqualTo(1));
                Assert.That(preparationCalls, Is.EqualTo(1));
                Assert.That(pauseLeasesAcquired, Is.EqualTo(1));
                Assert.That(pauseLeasesReleased, Is.EqualTo(1));

                publicationBinding.Dispose();
                publicationBinding = null;
                pauseProviderBinding.Dispose();
                pauseProviderBinding = null;
            }
            finally
            {
                publicationBinding?.Dispose();
                pauseProviderBinding?.Dispose();
                if (choicePanel != null) choicePanel.Destroy();
                if (rerollPanel != null) rerollPanel.Destroy();
                if (publicationPanel != null) publicationPanel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator DefinitiveUnmaterializableClaimDoesNotBecomePendingRetryOrWrite()
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

                string[] incompatibleOptions =
                {
                    "weapon.venator.short_bow", "relic.marked_coin", "relic.hidden_pouch"
                };
                ExpeditionMutationCommitResult<FortuneOfferResult> created = null;
                yield return Complete(app.CreateFortuneOfferAsync("offer.panel.invalid-claim", "context.panel.invalid-claim",
                    incompatibleOptions), value => created = value);
                Assert.That(created.Persisted, Is.True, created.Message);
                panel = CreatePanel(app, "offer.panel.invalid-claim");

                int invalidOptionIndex = Array.IndexOf(panel.Controller.CurrentOffer.OptionIds.ToArray(),
                    "weapon.venator.short_bow");
                Assert.That(invalidOptionIndex, Is.GreaterThanOrEqualTo(0));
                ExpeditionMutationCommitResult<FortuneChoiceResult> chosen = null;
                yield return Complete(panel.Controller.ChooseOptionAsync(invalidOptionIndex), value => chosen = value);
                Assert.That(chosen.Persisted, Is.True, chosen.Message);

                string inventoryBefore = app.ActiveExpedition.inventorySnapshotJson;
                int chargesBefore = app.ActiveExpedition.fortuneOfferBookSnapshot.charges;
                int weaponQuantityBefore = GetBackpackItemQuantity(app, "weapon.venator.short_bow");
                string primaryBeforeClaim = store.ProfilePrimary;
                int writesBeforeClaim = store.ProfileWriteAttempts;
                store.FailNextProfileWrite = true;
                ExpeditionMutationCommitResult<FortuneRewardApplicationOutcome> rejected = null;
                yield return Complete(panel.Controller.ClaimRewardAsync(), value => rejected = value);

                Assert.That(rejected.Persisted, Is.True,
                    "The definitive incompatible-class result is represented by the committed unchanged state; no save write was needed.");
                Assert.That(rejected.Pending, Is.False);
                Assert.That(rejected.Outcome.Succeeded, Is.False);
                Assert.That(rejected.Outcome.InventoryOperation.Status,
                    Is.EqualTo(InventoryOperationStatus.RejectedIncompatibleClass));
                Assert.That(app.HasPendingFortuneMutation, Is.False);
                Assert.That(app.PendingFortuneOperationKey, Is.Empty);
                Assert.That(app.PendingProfileExport, Is.Null);
                Assert.That(app.GetFortuneOffer("offer.panel.invalid-claim").IsClaimed, Is.False);
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(inventoryBefore));
                Assert.That(app.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(chargesBefore));
                Assert.That(GetBackpackItemQuantity(app, "weapon.venator.short_bow"), Is.EqualTo(weaponQuantityBefore));
                Assert.That(store.ProfilePrimary, Is.EqualTo(primaryBeforeClaim));
                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writesBeforeClaim));
                Assert.That(store.FailNextProfileWrite, Is.True);
                Assert.That(panel.Controller.IsBusy, Is.False);
                Assert.That(panel.Controller.HasPendingRerollRetry, Is.False);
                Assert.That(panel.Controller.LastRerollTransactionId.IsValid, Is.False);
                Assert.That(panel.Controller.StatusText, Does.Not.Contain("GUARDADO PENDIENTE"));
            }
            finally
            {
                if (panel != null) panel.Destroy();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        private static IEnumerator CreateReadOnlyV6Offers(MemorySaveStore store,
            Action<ApplicationBootstrapper, string> ready)
        {
            // Synthetic legacy V6 data is built through the real App APIs in an isolated store.
            // Rebootstrapping that saved profile supplies the genuine readonly access flags.
            ApplicationBootstrapper app = null;
            yield return StartApplication(value => app = value);
            yield return BeginExpedition(app);

            ExpeditionMutationCommitResult<FortuneOfferResult> unclaimed = null;
            yield return Complete(app.CreateFortuneOfferAsync("offer.panel.v6.unclaimed", "context.panel.v6.unclaimed",
                ClaimableOptions), value => unclaimed = value);
            Assert.That(unclaimed.Persisted, Is.True, unclaimed.Message);
            ExpeditionMutationCommitResult<FortuneOfferResult> chosen = null;
            yield return Complete(app.CreateFortuneOfferAsync("offer.panel.v6.chosen", "context.panel.v6.chosen",
                ClaimableOptions), value => chosen = value);
            Assert.That(chosen.Persisted, Is.True, chosen.Message);
            string selectedOptionId = chosen.Outcome.Offer.OptionIds[1];
            ExpeditionMutationCommitResult<FortuneChoiceResult> selection = null;
            yield return Complete(app.ChooseFortuneOptionAsync("offer.panel.v6.chosen", selectedOptionId),
                value => selection = value);
            Assert.That(selection.Persisted, Is.True, selection.Message);

            SaveStorageSnapshot storedProfile = null;
            yield return Complete(store.ReadAsync(SaveDocument.Profile), value => storedProfile = value);
            string savedProfileJson = storedProfile.PrimaryJson;
            string savedExpeditionJson = JsonUtility.ToJson(app.ActiveExpedition, false);
            Assert.That(savedProfileJson, Is.Not.Null.And.Not.Empty);
            Assert.That(savedExpeditionJson, Is.Not.Null.And.Not.Empty);

            DestroyApplication();
            yield return null;
            yield return StartApplication(value => app = value);
            Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(app.CanWriteProfile, Is.False);
            Assert.That(app.CanContinueExpedition, Is.False);
            Assert.That(app.HasActiveExpedition, Is.True);
            Assert.That(app.ActiveRun, Is.Null);
            Assert.That(store.ProfilePrimary, Is.EqualTo(savedProfileJson));
            Assert.That(JsonUtility.ToJson(app.ActiveExpedition, false), Is.EqualTo(savedExpeditionJson));
            ready(app, selectedOptionId);
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

        private static int GetBackpackItemQuantity(ApplicationBootstrapper app, string itemId)
        {
            return app.ActiveRun.Inventory.Backpack
                .Where(item => item != null && item.ItemId == itemId)
                .Sum(item => item.Quantity);
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
            const int maximumWaitFrames = 300;
            for (int frame = 0; frame < maximumWaitFrames && !task.IsCompleted; frame++) yield return null;
            if (!task.IsCompleted) throw new TimeoutException("A Fortune panel fixture operation did not complete within 300 frames.");
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

        private sealed class CallbackDisposable : IDisposable
        {
            private Action _onDispose;

            public CallbackDisposable(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                Action callback = _onDispose;
                _onDispose = null;
                callback?.Invoke();
            }
        }

        private sealed class MemorySaveStore : ISaveStore
        {
            private string _profilePrimary;
            private string _profileBackup;
            private string _settingsPrimary;
            private string _settingsBackup;

            public bool FailNextProfileWrite { get; set; }
            public int ProfileWriteAttempts { get; private set; }
            public string ProfilePrimary => _profilePrimary;
            public string ProfileBackup => _profileBackup;

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(_profilePrimary, _profileBackup)
                    : new SaveStorageSnapshot(_settingsPrimary, _settingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile) ProfileWriteAttempts++;
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
