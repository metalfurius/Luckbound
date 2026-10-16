using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.App
{
    public sealed class ApplicationFortuneMutationTests
    {
        private MemorySaveStore _store;
        private SaveRepository _repository;
        private ExpeditionRunConfiguration _run;
        private ExpeditionSaveData _initialState;
        private GameObject _gameObject;
        private ApplicationBootstrapper _application;

        [SetUp]
        public void SetUp()
        {
            _store = new MemorySaveStore();
            _repository = new SaveRepository(_store);
            _run = ExpeditionRunConfigurationFactory.Create("class.bellator", "race.human",
                new[] { "skill.bellator.circular_cut", "skill.bellator.guard" });
            _initialState = ExpeditionSaveMapper.Create(_run);
            SaveWriteResult initialSave = Wait(_repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile(), _initialState));
            Assert.That(initialSave.Succeeded, Is.True, initialSave.Message);

            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.DestroyImmediate(ApplicationBootstrapper.Instance.gameObject);
            _gameObject = new GameObject("ApplicationFortuneMutationTests");
            _application = _gameObject.AddComponent<ApplicationBootstrapper>();
            SetField("_saveRepository", _repository);
            SetField("_profileData", ProfileSaveData.CreateNewProfile());
            SetField("_activeExpedition", _initialState.Clone());
            SetField("_activeExpeditionRevision", 1L);
            SetProperty("ActiveRun", _run);
            SetProperty("SaveDataReady", true);
            SetProperty("ProfileLoadStatus", SaveLoadStatus.LoadedPrimary);
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null) UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void FailedRerollRetriesSamePayloadAndRoomResumeUsesEntryBook()
        {
            string[] eligible = { "relic.ash", "relic.ember", "relic.iron", "relic.mirror", "relic.moon" };
            ExpeditionMutationCommitResult<FortuneOfferResult> created = Wait(
                _application.CreateFortuneOfferAsync("offer.chest", "context.chest", eligible));
            Assert.That(created.Persisted, Is.True, created.Message);
            Assert.That(created.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(_application.GetFortuneOffers().Count, Is.EqualTo(1));
            FortuneOffer offerBeforeRepeat = _application.GetFortuneOffer("offer.chest");
            Assert.That(offerBeforeRepeat, Is.Not.Null);
            Assert.That(_application.GetFortuneRerollAvailability("offer.chest"),
                Is.EqualTo(FortuneRerollAvailability.Available));

            string entryCheckpoint = _application.ActiveExpedition.roomEntryCheckpointJson;
            TransactionId transactionId = TransactionId.New();
            _store.FailNextProfileWrite = true;
            ExpeditionMutationCommitResult<FortuneRerollResult> failed = Wait(
                _application.RepeatFortuneOfferAsync("offer.chest", transactionId));

            Assert.That(failed.Persisted, Is.False);
            Assert.That(failed.Pending, Is.True);
            Assert.That(failed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(_application.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(2),
                "The failed write must not publish its charge spend.");
            Assert.That(_application.GetFortuneOffer("offer.chest").RerollCount, Is.Zero);
            Assert.That(_application.ActiveExpedition.roomEntryCheckpointJson, Is.EqualTo(entryCheckpoint));

            ExpeditionMutationCommitResult<FortuneRerollResult> retried = Wait(
                _application.RepeatFortuneOfferAsync("offer.chest", transactionId));
            ExpeditionMutationCommitResult<FortuneRerollResult> replayed = Wait(
                _application.RepeatFortuneOfferAsync("offer.chest", transactionId));

            Assert.That(retried.Persisted, Is.True, retried.Message);
            Assert.That(retried.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(replayed.Persisted, Is.True, replayed.Message);
            Assert.That(replayed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(_application.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(1));
            Assert.That(_application.GetFortuneOffer("offer.chest").RerollCount, Is.EqualTo(1));
            Assert.That(_application.ActiveExpedition.roomEntryCheckpointJson, Is.EqualTo(entryCheckpoint),
                "A Fortune mutation keeps the immutable room-entry checkpoint intact.");

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(new SaveRepository(_store).LoadProfileAsync());
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary), loaded.Message);
            Assert.That(loaded.Data.activeExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(1));
            Assert.That(loaded.Data.activeExpedition.fortuneOfferBookSnapshot.rerollReceipts.Length, Is.EqualTo(1));

            ExpeditionSaveMapper.Restore(loaded.Data.activeExpedition, out ExpeditionSaveData resumed);
            Assert.That(resumed.fortuneOfferBookSnapshot.charges, Is.EqualTo(2));
            Assert.That(FortuneOfferBook.Restore(resumed.fortuneOfferBookSnapshot).GetOffer("offer.chest"), Is.Null,
                "A restart during this room restores the complete room-entry Fortuna state.");
        }

        [Test]
        public void GenericExpeditionRetryDoesNotBlockAnotherFortuneOperationOrReplayTheCharge()
        {
            string[] eligible = { "relic.ash", "relic.ember", "relic.iron", "relic.mirror", "relic.moon" };
            ExpeditionMutationCommitResult<FortuneOfferResult> firstOffer = Wait(
                _application.CreateFortuneOfferAsync("offer.chest", "context.chest", eligible));
            Assert.That(firstOffer.Persisted, Is.True, firstOffer.Message);

            TransactionId transactionId = TransactionId.New();
            _store.FailNextProfileWrite = true;
            ExpeditionMutationCommitResult<FortuneRerollResult> failedReroll = Wait(
                _application.RepeatFortuneOfferAsync("offer.chest", transactionId));
            Assert.That(failedReroll.Persisted, Is.False);
            Assert.That(failedReroll.Pending, Is.True);

            SaveWriteResult genericRetry = Wait(_application.RetryPendingExpeditionWriteAsync());
            Assert.That(genericRetry.Succeeded, Is.True, genericRetry.Message);
            Assert.That(_application.HasPendingFortuneMutation, Is.True,
                "The generic save retry cannot return the original typed Fortune result.");

            ExpeditionMutationCommitResult<FortuneOfferResult> secondOffer = Wait(
                _application.CreateFortuneOfferAsync("offer.followup", "context.followup", eligible));
            Assert.That(secondOffer.Persisted, Is.True, secondOffer.Message);
            Assert.That(secondOffer.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));

            ExpeditionMutationCommitResult<FortuneRerollResult> replayedReroll = Wait(
                _application.RepeatFortuneOfferAsync("offer.chest", transactionId));
            Assert.That(replayedReroll.Persisted, Is.True, replayedReroll.Message);
            Assert.That(replayedReroll.Outcome.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(_application.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(1));
            Assert.That(_application.GetFortuneOffer("offer.chest").RerollCount, Is.EqualTo(1));
            Assert.That(_application.GetFortuneOffers().Count, Is.EqualTo(2));
            Assert.That(_application.HasPendingFortuneMutation, Is.False);
        }

        [Test]
        public void FailedOfferCreationRetriesWithTheSameOptionSetInAnyOrder()
        {
            string[] initialOrder =
            {
                "relic.ash", "relic.ember", "relic.iron", "relic.mirror", "relic.moon", "relic.ash"
            };
            _store.FailNextProfileWrite = true;

            ExpeditionMutationCommitResult<FortuneOfferResult> failed = Wait(
                _application.CreateFortuneOfferAsync("offer.chest", "context.chest", initialOrder));

            Assert.That(failed.Persisted, Is.False);
            Assert.That(failed.Pending, Is.True);
            Assert.That(_application.GetFortuneOffers(), Is.Empty,
                "A failed offer write must not publish its candidate book.");

            string[] retryOrder =
            {
                "relic.moon", "relic.mirror", "relic.iron", "relic.ember", "relic.ash"
            };
            ExpeditionMutationCommitResult<FortuneOfferResult> retried = Wait(
                _application.CreateFortuneOfferAsync("offer.chest", "context.chest", retryOrder));
            ExpeditionMutationCommitResult<FortuneOfferResult> replayed = Wait(
                _application.CreateFortuneOfferAsync("offer.chest", "context.chest", initialOrder));

            Assert.That(retried.Persisted, Is.True, retried.Message);
            Assert.That(retried.Outcome.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(replayed.Persisted, Is.True, replayed.Message);
            Assert.That(replayed.Outcome.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(_application.GetFortuneOffers().Count, Is.EqualTo(1));
            Assert.That(_application.HasPendingFortuneMutation, Is.False);
        }

        [Test]
        public void ChoiceClaimAndGuardianGrantPersistAndReturnStableIdentifiers()
        {
            string[] eligible = { "relic.ash", "relic.ember", "relic.iron", "relic.mirror" };
            ExpeditionMutationCommitResult<FortuneOfferResult> created = Wait(
                _application.CreateFortuneOfferAsync("offer.guardian", "context.guardian", eligible));
            Assert.That(created.Persisted, Is.True, created.Message);
            string selectedOption = created.Outcome.Offer.OptionIds[0];

            ExpeditionMutationCommitResult<FortuneChoiceResult> chosen = Wait(
                _application.ChooseFortuneOptionAsync("offer.guardian", selectedOption));
            ExpeditionMutationCommitResult<FortuneClaimResult> claimed = Wait(
                _application.ClaimFortuneOfferAsync("offer.guardian"));
            ExpeditionMutationCommitResult<FortuneClaimResult> claimedAgain = Wait(
                _application.ClaimFortuneOfferAsync("offer.guardian"));
            ExpeditionMutationCommitResult<FortuneChargeResult> granted = Wait(
                _application.GrantGuardianFortuneChargeAsync("guardian.chestkeeper"));
            ExpeditionMutationCommitResult<FortuneChargeResult> grantedAgain = Wait(
                _application.GrantGuardianFortuneChargeAsync("guardian.chestkeeper"));

            Assert.That(chosen.Persisted, Is.True, chosen.Message);
            Assert.That(claimed.Persisted, Is.True, claimed.Message);
            Assert.That(claimed.Outcome.ClaimId, Is.EqualTo("fortune.claim.offer.guardian"));
            Assert.That(claimedAgain.Outcome.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(claimedAgain.Outcome.ClaimId, Is.EqualTo(claimed.Outcome.ClaimId));
            Assert.That(granted.Persisted, Is.True, granted.Message);
            Assert.That(granted.Outcome.CurrentCharges, Is.EqualTo(3));
            Assert.That(grantedAgain.Outcome.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(_application.ActiveExpedition.fortuneOfferBookSnapshot.charges, Is.EqualTo(3));
            Assert.That(_application.GetFortuneOffer("offer.guardian").SelectedOptionId, Is.EqualTo(selectedOption));
            Assert.That(_application.GetFortuneOffer("offer.guardian").IsClaimed, Is.True);

            SaveLoadResult<ProfileSaveEnvelope> loaded = Wait(new SaveRepository(_store).LoadProfileAsync());
            FortuneOfferBook restored = FortuneOfferBook.Restore(loaded.Data.activeExpedition.fortuneOfferBookSnapshot);
            Assert.That(restored.GetOffer("offer.guardian").ClaimId, Is.EqualTo(claimed.Outcome.ClaimId));
            Assert.That(restored.CurrentCharges, Is.EqualTo(3));
            Assert.That(restored.GrantGuardianCharge("guardian.chestkeeper").Status,
                Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
        }

        private void SetField(string name, object value)
        {
            FieldInfo field = typeof(ApplicationBootstrapper).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Expected application field " + name + ".");
            field.SetValue(_application, value);
        }

        private void SetProperty(string name, object value)
        {
            PropertyInfo property = typeof(ApplicationBootstrapper).GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, "Expected application property " + name + ".");
            property.GetSetMethod(true).Invoke(_application, new[] { value });
        }

        private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

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
