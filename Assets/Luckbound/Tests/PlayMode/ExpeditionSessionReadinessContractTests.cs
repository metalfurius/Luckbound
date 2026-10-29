using System;
using Luckbound.App;
using Luckbound.Core.Saves;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionSessionReadinessContractTests
    {
        [Test]
        public void CheckpointArgumentsKeepAnIndependentCheckpointCopy()
        {
            var checkpoint = new ExpeditionSaveData
            {
                runId = "run.readiness",
                classId = "class.bellator",
                raceId = "race.human",
                currentNodeId = "F10.A"
            };
            var arguments = new ExpeditionCheckpointBindingArguments(
                null, checkpoint, default, null, default, default, 0,
                null, null, null, null, null);

            checkpoint.currentNodeId = "F10.B";
            ExpeditionSaveData copy = arguments.Checkpoint;
            copy.currentNodeId = "F10.C";

            Assert.That(arguments.Checkpoint.currentNodeId, Is.EqualTo("F10.A"));
        }

        [Test]
        public void AuthorityCaptureCopiesTheActiveSaveAndRetainsTheObservedRevision()
        {
            var active = new ExpeditionSaveData
            {
                runId = "run.readiness",
                classId = "class.bellator",
                raceId = "race.human",
                currentNodeId = "F10.A"
            };
            var capture = new ExpeditionReadinessAuthorityCapture(
                ExpeditionReadinessAuthorityState.Unavailable, null, active, 17,
                null, null, false, false, false, false, false, false);

            active.currentNodeId = "F10.B";
            ExpeditionSaveData returned = capture.ActiveExpedition;
            returned.currentNodeId = "F10.C";

            Assert.That(capture.State, Is.EqualTo(ExpeditionReadinessAuthorityState.Unavailable));
            Assert.That(capture.ActiveExpeditionRevision, Is.EqualTo(17));
            Assert.That(capture.ActiveExpedition.currentNodeId, Is.EqualTo("F10.A"));
            Assert.That(capture.HasPendingSessionMutation, Is.False);
        }

        [Test]
        public void PublicationCheckRetainsTheExactConfirmedStateAndRevision()
        {
            var confirmed = new ExpeditionSaveData
            {
                runId = "run.readiness",
                classId = "class.bellator",
                raceId = "race.human",
                currentNodeId = "F10.B"
            };
            var check = new ExpeditionReadinessPublicationCheck(
                ExpeditionReadinessPublicationStatus.Confirmed, 23, confirmed);

            confirmed.currentNodeId = "F10.C";
            ExpeditionSaveData returned = check.ConfirmedExpedition;
            returned.currentNodeId = "F10.D";

            Assert.That(check.Status, Is.EqualTo(ExpeditionReadinessPublicationStatus.Confirmed));
            Assert.That(check.ConfirmedRevision, Is.EqualTo(23));
            Assert.That(check.ConfirmedExpedition.currentNodeId, Is.EqualTo("F10.B"));
        }

        [Test]
        public void ViewsCannotExpressReadinessWithoutRealOwnerReferences()
        {
            var composition = new ExpeditionReadinessCompositionView(
                null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, false);
            var transition = new ExpeditionReadinessTransitionView(
                null, null, null, null, null, null, false, false, false, false);

            Assert.That(composition.IsReady, Is.False);
            Assert.That(composition.Owner, Is.Null);
            Assert.That(composition.ReadinessRegistration, Is.Null);
            Assert.That(transition.IsInitialized, Is.False);
            Assert.That(transition.IsTransitioning, Is.False);
            Assert.That(transition.HasPreparedTransition, Is.False);
        }

        [Test]
        public void ProductionConstructionPathsRejectMissingSessionOwnership()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ExpeditionSessionReadinessRegistration.Create(null, null));
            Assert.Throws<ArgumentNullException>(() =>
                new ExpeditionEncounterSessionSnapshotSource(null, null));
            Assert.Throws<ArgumentNullException>(() =>
                ExpeditionRoomActivationRegistration.CreateFromSession(
                    null, null, null, default, null, null, default, new object()));
        }

        [Test]
        public void RoomBindingEvidenceRequiresTheCompletedOperationReferences()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ExpeditionReadinessRoomBindingResult(
                    null, null, null, null, null, null, null, null, null, null));
        }

        [Test]
        public void RestoredSessionRequiresTheCheckpointRevisionAndWholeSaveToMatch()
        {
            var checkpoint = new ExpeditionSaveData
            {
                runId = "run.readiness",
                classId = "class.bellator",
                raceId = "race.human",
                currentNodeId = "F10.A"
            };
            ExpeditionSaveData sameState = checkpoint.Clone();

            Assert.That(ExpeditionEncounterSessionSnapshotSource.MatchesRestoredSessionCheckpoint(
                17, checkpoint, 17, sameState), Is.True);
            Assert.That(ExpeditionEncounterSessionSnapshotSource.MatchesRestoredSessionCheckpoint(
                17, checkpoint, 18, sameState), Is.False);

            ExpeditionSaveData changedState = sameState.Clone();
            changedState.currentNodeId = "F10.B";
            Assert.That(ExpeditionEncounterSessionSnapshotSource.MatchesRestoredSessionCheckpoint(
                17, checkpoint, 17, changedState), Is.False);
        }

        [Test]
        public void ConfirmedReplayRequiresTheExactWitnessRunRevisionAndWholeSave()
        {
            object witness = new object();
            object run = new object();
            var confirmed = new ExpeditionSaveData
            {
                runId = "run.readiness",
                classId = "class.bellator",
                raceId = "race.human",
                currentNodeId = "F10.B"
            };
            ExpeditionSaveData sameState = confirmed.Clone();

            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, run, 23, 23, confirmed, sameState), Is.True);
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, run, 23, 24, confirmed, sameState), Is.False);

            object replacementRun = new object();
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, replacementRun, 23, 23, confirmed, sameState), Is.False);
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, new object(), run, run, 23, 23, confirmed, sameState), Is.False);

            ExpeditionSaveData changedState = sameState.Clone();
            changedState.currentNodeId = "F10.C";
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, run, 23, 23, confirmed, changedState), Is.False);
        }

        [Test]
        public void DisabledCompositionOwnerIsUnavailableUntilReenabled()
        {
            var host = new GameObject("Readiness owner availability test");
            try
            {
                ExpeditionGameplayComposition owner = host.AddComponent<ExpeditionGameplayComposition>();
                Assert.That(ExpeditionEncounterSessionSnapshotSource.GetOwnerAvailability(owner),
                    Is.EqualTo(ExpeditionReadinessOwnerAvailability.Active));

                owner.enabled = false;
                Assert.That(ExpeditionEncounterSessionSnapshotSource.GetOwnerAvailability(owner),
                    Is.EqualTo(ExpeditionReadinessOwnerAvailability.Disabled));

                owner.enabled = true;
                Assert.That(ExpeditionEncounterSessionSnapshotSource.GetOwnerAvailability(owner),
                    Is.EqualTo(ExpeditionReadinessOwnerAvailability.Active));
                Assert.That(owner, Is.SameAs(host.GetComponent<ExpeditionGameplayComposition>()));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
