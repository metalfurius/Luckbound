using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Progression.Contracts;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Progression.Contracts
{
    public sealed class ContractProgressionRulesTests
    {
        [Test]
        public void IdentifiedEventProgressesOnceAndMatchingReplayIsAnIdentityNoOp()
        {
            ContractProgressionSnapshot current = Empty();

            ContractProgressOutcome first = ContractProgressionRules.RecordProgress(
                current, ContractProgressEvent.CombatRoomCompleted, "run-1/room-1/victory");
            ContractProgressOutcome replay = ContractProgressionRules.RecordProgress(
                first.Snapshot, ContractProgressEvent.CombatRoomCompleted, "run-1/room-1/victory");

            Assert.That(first.Status, Is.EqualTo(ContractProgressStatus.Progressed));
            Assert.That(first.ChangesProfile, Is.True);
            Assert.That(first.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(1));
            Assert.That(first.Snapshot.ProcessedEvents, Has.Count.EqualTo(1));
            Assert.That(first.Snapshot.ProcessedEvents[0].EventId, Is.EqualTo("run-1/room-1/victory"));
            Assert.That(first.Snapshot.ProcessedEvents[0].Event, Is.EqualTo(ContractProgressEvent.CombatRoomCompleted));
            Assert.That(replay.Status, Is.EqualTo(ContractProgressStatus.DuplicateEvent));
            Assert.That(replay.ChangesProfile, Is.False);
            Assert.That(replay.Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(replay.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(1));
        }

        [Test]
        public void ReusingAnEventIdForAnotherTypeFailsClosedWithoutChangingTheSnapshot()
        {
            ContractProgressionSnapshot current = Empty();
            ContractProgressionSnapshot recorded = ContractProgressionRules.RecordProgress(
                current, ContractProgressEvent.CombatRoomCompleted, "run-1/room-1/victory").Snapshot;

            ContractProgressOutcome conflict = ContractProgressionRules.RecordProgress(
                recorded, ContractProgressEvent.CustodianDefeated, "run-1/room-1/victory");

            Assert.That(conflict.Status, Is.EqualTo(ContractProgressStatus.EventIdConflict));
            Assert.That(conflict.ChangesProfile, Is.False);
            Assert.That(conflict.Snapshot, Is.SameAs(recorded));
            Assert.That(conflict.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(1));
            Assert.That(conflict.Snapshot.GetProgress(ContractCatalog.Ct02), Is.Zero);
            Assert.That(conflict.Snapshot.ProcessedEvents, Has.Count.EqualTo(1));
            Assert.That(conflict.Snapshot.ProcessedEvents[0].Event,
                Is.EqualTo(ContractProgressEvent.CombatRoomCompleted));
        }

        [Test]
        public void UniqueEventsAtCounterMaximumStillRecordTheirReceipts()
        {
            ContractProgressionSnapshot current = Empty(counters: new[]
            {
                new ContractProgressCounter(ContractCatalog.Ct01, 3)
            });

            ContractProgressOutcome first = ContractProgressionRules.RecordProgress(
                current, ContractProgressEvent.CombatRoomCompleted, "run-1/room-4/victory");
            ContractProgressOutcome replay = ContractProgressionRules.RecordProgress(
                first.Snapshot, ContractProgressEvent.CombatRoomCompleted, "run-1/room-4/victory");
            ContractProgressOutcome another = ContractProgressionRules.RecordProgress(
                first.Snapshot, ContractProgressEvent.CombatRoomCompleted, "run-1/room-5/victory");

            Assert.That(first.Status, Is.EqualTo(ContractProgressStatus.RecordedWithoutProgress));
            Assert.That(first.ChangesProfile, Is.True);
            Assert.That(first.Snapshot, Is.Not.SameAs(current));
            Assert.That(first.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(3));
            Assert.That(first.Snapshot.ProcessedEvents, Has.Count.EqualTo(1));
            Assert.That(replay.Status, Is.EqualTo(ContractProgressStatus.DuplicateEvent));
            Assert.That(replay.ChangesProfile, Is.False);
            Assert.That(replay.Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(another.Status, Is.EqualTo(ContractProgressStatus.RecordedWithoutProgress));
            Assert.That(another.ChangesProfile, Is.True);
            Assert.That(another.Snapshot.ProcessedEvents, Has.Count.EqualTo(2));
        }

        [Test]
        public void ReceiptsAreImmutableSortedAndPreservedByProgressAcceptAndClaimCopies()
        {
            ContractProgressEventReceipt later = new ContractProgressEventReceipt(
                "run-1/room-2/victory", ContractProgressEvent.CombatRoomCompleted);
            ContractProgressEventReceipt earlier = new ContractProgressEventReceipt(
                "run-1/room-1/victory", ContractProgressEvent.CombatRoomCompleted);
            ContractProgressionSnapshot directlyCreated = new ContractProgressionSnapshot(0,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<ContractProgressCounter>(), new[] { later, earlier });

            Assert.That(directlyCreated.ProcessedEvents.Select(receipt => receipt.EventId),
                Is.EqualTo(new[] { earlier.EventId, later.EventId }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ContractProgressEventReceipt>)directlyCreated.ProcessedEvents).Add(later));

            ContractProgressionSnapshot current = Empty();
            for (int index = 1; index <= 3; index++)
            {
                current = ContractProgressionRules.RecordProgress(current,
                    ContractProgressEvent.CombatRoomCompleted, "run-2/room-" + index + "/victory").Snapshot;
            }

            string[] receiptIds = current.ProcessedEvents.Select(receipt => receipt.EventId).ToArray();
            ContractProgressionSnapshot accepted = ContractProgressionRules.Accept(
                current, ContractCatalog.Ct01).Snapshot;
            ContractProgressionSnapshot claimed = ContractProgressionRules.Claim(
                accepted, ContractCatalog.Ct01).Snapshot;
            ContractProgressionSnapshot copiedByAnotherEvent = ContractProgressionRules.RecordProgress(
                claimed, ContractProgressEvent.CombatRoomCompleted, "run-2/room-4/victory").Snapshot;

            CollectionAssert.AreEqual(receiptIds, accepted.ProcessedEvents.Select(receipt => receipt.EventId));
            CollectionAssert.AreEqual(receiptIds, claimed.ProcessedEvents.Select(receipt => receipt.EventId));
            CollectionAssert.AreEqual(receiptIds.Concat(new[] { "run-2/room-4/victory" }),
                copiedByAnotherEvent.ProcessedEvents.Select(receipt => receipt.EventId));
        }

        [Test]
        public void EventReceiptIdsAndSnapshotReceiptSetsRejectInvalidOrRepeatedValues()
        {
            Assert.Throws<ArgumentException>(() =>
                new ContractProgressEventReceipt("", ContractProgressEvent.CombatRoomCompleted));
            Assert.Throws<ArgumentException>(() =>
                new ContractProgressEventReceipt("has space", ContractProgressEvent.CombatRoomCompleted));
            Assert.Throws<ArgumentException>(() =>
                new ContractProgressEventReceipt(new string('a', 161), ContractProgressEvent.CombatRoomCompleted));
            Assert.That(new ContractProgressEventReceipt(new string('a', 160),
                ContractProgressEvent.CombatRoomCompleted).EventId, Has.Length.EqualTo(160));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ContractProgressEventReceipt("valid-id", (ContractProgressEvent)999));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ContractProgressionRules.RecordProgress(Empty(), (ContractProgressEvent)999, "valid-id"));

            var first = new ContractProgressEventReceipt("same-id", ContractProgressEvent.CombatRoomCompleted);
            var second = new ContractProgressEventReceipt("same-id", ContractProgressEvent.CustodianDefeated);
            Assert.Throws<ArgumentException>(() => new ContractProgressionSnapshot(0,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                Array.Empty<ContractProgressCounter>(), new[] { first, second }));

            ContractProgressionSnapshot caseSensitive = ContractProgressionRules.RecordProgress(
                Empty(), ContractProgressEvent.CombatRoomCompleted, "Event-A").Snapshot;
            caseSensitive = ContractProgressionRules.RecordProgress(
                caseSensitive, ContractProgressEvent.CombatRoomCompleted, "event-a").Snapshot;
            Assert.That(caseSensitive.ProcessedEvents.Select(receipt => receipt.EventId),
                Is.EqualTo(new[] { "Event-A", "event-a" }));
        }

        [Test]
        public void CatalogAddsTheEightFixedContractsWithoutDroppingTheExistingFour()
        {
            Assert.That(ContractCatalog.ImplementedContracts, Has.Count.EqualTo(12));
            Assert.That((int)ContractUnlockKind.GuildDecoration, Is.EqualTo(4));
            AssertDefinition(ContractCatalog.Ct05, ContractGoalType.StoryFragmentsFound, 3, 30,
                "race.alvari", "armor.runic_mantle");
            AssertDefinition(ContractCatalog.Ct06, ContractGoalType.ExpeditionsExtractedAfterGuardian, 1, 40,
                "weapon.venator.long_bow");
            AssertDefinition(ContractCatalog.Ct07, ContractGoalType.MagistersDefeated, 1, 60,
                "weapon.venator.ruins_crossbow");
            AssertDefinition(ContractCatalog.Ct08, ContractGoalType.SkillHitsApplied, 30, 30,
                "weapon.magus.frost_staff");
            AssertDefinition(ContractCatalog.Ct09, ContractGoalType.PactsAcceptedBetweenExpeditions, 3, 40,
                "weapon.magus.shadow_staff");
            AssertDefinition(ContractCatalog.Ct10, ContractGoalType.WeaponsUpgradedToPlusThree, 1, 50,
                "weapon.bellator.umbra_greatsword");
            AssertDefinition(ContractCatalog.Ct11, ContractGoalType.EndingsCompleted, 1, 80,
                "armor.bark_armor");
            AssertDefinition(ContractCatalog.Ct12, ContractGoalType.StoryFragmentsFound, 12, 60,
                ContractCatalog.GuildOathPlaqueUnlockId);
        }

        [TestCase(ContractCatalog.Ct05, ContractProgressEvent.StoryFragmentFound, 3, 30,
            ContractUnlockKind.PlayableRace, "race.alvari", "armor.runic_mantle")]
        [TestCase(ContractCatalog.Ct06, ContractProgressEvent.ExpeditionExtractedAfterGuardian, 1, 40,
            ContractUnlockKind.StartingKit, "weapon.venator.long_bow", null)]
        [TestCase(ContractCatalog.Ct07, ContractProgressEvent.MagisterDefeated, 1, 60,
            ContractUnlockKind.StartingKit, "weapon.venator.ruins_crossbow", null)]
        [TestCase(ContractCatalog.Ct08, ContractProgressEvent.SkillHitApplied, 30, 30,
            ContractUnlockKind.StartingKit, "weapon.magus.frost_staff", null)]
        [TestCase(ContractCatalog.Ct09, ContractProgressEvent.PactAcceptedBetweenExpeditions, 3, 40,
            ContractUnlockKind.StartingKit, "weapon.magus.shadow_staff", null)]
        [TestCase(ContractCatalog.Ct10, ContractProgressEvent.WeaponUpgradedToPlusThree, 1, 50,
            ContractUnlockKind.StartingKit, "weapon.bellator.umbra_greatsword", null)]
        [TestCase(ContractCatalog.Ct11, ContractProgressEvent.EndingCompleted, 1, 80,
            ContractUnlockKind.StartingKit, "armor.bark_armor", null)]
        [TestCase(ContractCatalog.Ct12, ContractProgressEvent.StoryFragmentFound, 12, 60,
            ContractUnlockKind.GuildDecoration, ContractCatalog.GuildOathPlaqueUnlockId, null)]
        public void EachNewContractAcceptsPriorMilestonesAndClaimsItsRewardExactlyOnce(
            string contractId, ContractProgressEvent progressEvent, int requiredProgress, int expectedRenown,
            ContractUnlockKind expectedUnlockKind, string expectedUnlockId, string secondaryStartingKitId)
        {
            ContractProgressionSnapshot current = Empty();
            for (int i = 0; i < requiredProgress; i++)
                current = Record(current, progressEvent);

            Assert.That(current.GetProgress(contractId), Is.EqualTo(requiredProgress));
            ContractProgressionSnapshot afterExtraMilestone = Record(current, progressEvent);
            Assert.That(afterExtraMilestone.GetProgress(contractId), Is.EqualTo(requiredProgress));
            ContractAcceptOutcome accepted = ContractProgressionRules.Accept(afterExtraMilestone, contractId);
            Assert.That(accepted.Status, Is.EqualTo(ContractAcceptStatus.Accepted));
            Assert.That(accepted.Snapshot.IsComplete(contractId), Is.True);

            ContractClaimOutcome claimed = ContractProgressionRules.Claim(accepted.Snapshot, contractId);
            Assert.That(claimed.Status, Is.EqualTo(ContractClaimStatus.Claimed));
            Assert.That(claimed.Reward.Renown, Is.EqualTo(expectedRenown));
            AssertUnlock(claimed.Reward, expectedUnlockKind, expectedUnlockId);
            Assert.That(claimed.Snapshot.IsUnlockGranted(expectedUnlockId), Is.True);
            Assert.That(claimed.Snapshot.AvailableRenown, Is.EqualTo(expectedRenown));
            if (secondaryStartingKitId != null)
            {
                AssertUnlock(claimed.Reward, ContractUnlockKind.StartingKit, secondaryStartingKitId);
                Assert.That(claimed.Snapshot.UnlockedRaceIds, Does.Contain("race.alvari"));
                Assert.That(claimed.Snapshot.UnlockedStartingKitIds, Does.Contain(secondaryStartingKitId));
            }
            else if (expectedUnlockKind == ContractUnlockKind.StartingKit)
            {
                Assert.That(claimed.Snapshot.UnlockedStartingKitIds, Does.Contain(expectedUnlockId));
            }

            ContractClaimOutcome repeated = ContractProgressionRules.Claim(claimed.Snapshot, contractId);
            Assert.That(repeated.Status, Is.EqualTo(ContractClaimStatus.AlreadyClaimed));
            Assert.That(repeated.ChangesProfile, Is.False);
            Assert.That(repeated.Reward, Is.Null);
            Assert.That(repeated.Snapshot, Is.SameAs(claimed.Snapshot));
            Assert.That(repeated.Snapshot.AvailableRenown, Is.EqualTo(expectedRenown));
        }

        [Test]
        public void FragmentOutcomesFeedBothContractsAndClampTheirCountersIndependently()
        {
            ContractProgressionSnapshot current = Empty();
            for (int fragmentCount = 1; fragmentCount <= 12; fragmentCount++)
            {
                ContractProgressOutcome outcome = ContractProgressionRules.RecordProgress(
                    current, ContractProgressEvent.StoryFragmentFound, "story/fragment-" + fragmentCount);
                Assert.That(outcome.Status, Is.EqualTo(ContractProgressStatus.Progressed));
                current = outcome.Snapshot;
                Assert.That(current.GetProgress(ContractCatalog.Ct05), Is.EqualTo(Math.Min(fragmentCount, 3)));
                Assert.That(current.GetProgress(ContractCatalog.Ct12), Is.EqualTo(fragmentCount));
            }

            ContractProgressOutcome extraFragment = ContractProgressionRules.RecordProgress(
                current, ContractProgressEvent.StoryFragmentFound, "story/fragment-13");
            Assert.That(extraFragment.Status, Is.EqualTo(ContractProgressStatus.RecordedWithoutProgress));
            Assert.That(extraFragment.ChangesProfile, Is.True);
            Assert.That(extraFragment.Snapshot, Is.Not.SameAs(current));
            Assert.That(current.GetProgress(ContractCatalog.Ct05), Is.EqualTo(3));
            Assert.That(current.GetProgress(ContractCatalog.Ct12), Is.EqualTo(12));
        }

        [Test]
        public void AcceptingUsesProgressAlreadyPresentInTheProfileCounter()
        {
            var current = Empty(counters: new[] { new ContractProgressCounter(ContractCatalog.Ct01, 2) });

            ContractAcceptOutcome accepted = ContractProgressionRules.Accept(current, ContractCatalog.Ct01);

            Assert.That(accepted.Status, Is.EqualTo(ContractAcceptStatus.Accepted));
            Assert.That(accepted.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(2));
            Assert.That(accepted.Snapshot.IsComplete(ContractCatalog.Ct01), Is.False);

            ContractProgressOutcome completed = ContractProgressionRules.RecordProgress(
                accepted.Snapshot, ContractProgressEvent.CombatRoomCompleted, "room/final-victory");

            Assert.That(completed.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(3));
            Assert.That(completed.Snapshot.IsComplete(ContractCatalog.Ct01), Is.True);
        }

        [Test]
        public void ValidMilestonesEarnedBeforeAcceptanceAreRecognized()
        {
            ContractProgressionSnapshot current = Empty();
            current = Record(current, ContractProgressEvent.CustodianDefeated);
            current = Record(current, ContractProgressEvent.BoneguardDefeated);
            current = Record(current, ContractProgressEvent.ItemCrafted);
            current = Record(current, ContractProgressEvent.ItemUpgraded);

            ContractAcceptOutcome custodian = ContractProgressionRules.Accept(current, ContractCatalog.Ct02);
            ContractAcceptOutcome boneguard = ContractProgressionRules.Accept(custodian.Snapshot, ContractCatalog.Ct03);

            Assert.That(boneguard.Snapshot.GetProgress(ContractCatalog.Ct02), Is.EqualTo(1));
            Assert.That(boneguard.Snapshot.IsComplete(ContractCatalog.Ct02), Is.True);
            Assert.That(boneguard.Snapshot.GetProgress(ContractCatalog.Ct03), Is.EqualTo(1));
            Assert.That(boneguard.Snapshot.GetProgress(ContractCatalog.Ct04), Is.EqualTo(2));
        }

        [Test]
        public void ContractCountersAdvanceOnlyForTheirGoalEventAndStopAtTheGoal()
        {
            ContractProgressionSnapshot current = Empty();

            current = Record(current, ContractProgressEvent.CombatRoomCompleted);
            Assert.That(current.GetProgress(ContractCatalog.Ct01), Is.EqualTo(1));
            Assert.That(current.GetProgress(ContractCatalog.Ct02), Is.Zero);

            current = Record(current, ContractProgressEvent.ItemCrafted);
            current = Record(current, ContractProgressEvent.ItemUpgraded);
            current = Record(current, ContractProgressEvent.ItemCrafted);
            current = Record(current, ContractProgressEvent.ItemUpgraded);

            Assert.That(current.GetProgress(ContractCatalog.Ct04), Is.EqualTo(3));
            Assert.That(current.IsComplete(ContractCatalog.Ct04), Is.True);
            Assert.That(current.ProcessedEvents, Has.Count.EqualTo(5));
#pragma warning disable CS0618
            ContractProgressOutcome compatibilityReplay = ContractProgressionRules.RecordProgress(
                current, ContractProgressEvent.ItemCrafted);
#pragma warning restore CS0618
            Assert.That(compatibilityReplay.Status, Is.EqualTo(ContractProgressStatus.NoProgressChange));
            Assert.That(compatibilityReplay.ChangesProfile, Is.False);
            Assert.That(compatibilityReplay.Snapshot, Is.SameAs(current));
            Assert.That(compatibilityReplay.Snapshot.ProcessedEvents, Has.Count.EqualTo(5));
            Assert.That(current.GetProgress(ContractCatalog.Ct04), Is.EqualTo(3));
        }

        [Test]
        public void ThreeActiveContractsAreAllowedAndClaimingOneOpensASlot()
        {
            var current = Empty(counters: new[] { new ContractProgressCounter(ContractCatalog.Ct01, 3) });
            current = ContractProgressionRules.Accept(current, ContractCatalog.Ct01).Snapshot;
            current = ContractProgressionRules.Accept(current, ContractCatalog.Ct02).Snapshot;
            current = ContractProgressionRules.Accept(current, ContractCatalog.Ct03).Snapshot;

            ContractAcceptOutcome duplicate = ContractProgressionRules.Accept(current, ContractCatalog.Ct02);
            ContractAcceptOutcome capped = ContractProgressionRules.Accept(current, ContractCatalog.Ct04);

            Assert.That(current.ActiveContractIds, Has.Count.EqualTo(3));
            Assert.That(duplicate.Status, Is.EqualTo(ContractAcceptStatus.AlreadyAccepted));
            Assert.That(duplicate.ChangesProfile, Is.False);
            Assert.That(duplicate.Snapshot, Is.SameAs(current));
            Assert.That(capped.Status, Is.EqualTo(ContractAcceptStatus.RejectedActiveLimit));
            Assert.That(capped.Snapshot, Is.SameAs(current));

            ContractClaimOutcome claim = ContractProgressionRules.Claim(current, ContractCatalog.Ct01);
            ContractAcceptOutcome opened = ContractProgressionRules.Accept(claim.Snapshot, ContractCatalog.Ct04);

            Assert.That(claim.Status, Is.EqualTo(ContractClaimStatus.Claimed));
            Assert.That(claim.Snapshot.ActiveContractIds, Has.Count.EqualTo(2));
            Assert.That(opened.Status, Is.EqualTo(ContractAcceptStatus.Accepted));
            Assert.That(opened.Snapshot.ActiveContractIds, Has.Count.EqualTo(3));
        }

        [Test]
        public void ClaimingEachImplementedContractGrantsTheSpecifiedRenownAndUnlocks()
        {
            ContractClaimOutcome ct01 = CompleteAndClaim(ContractCatalog.Ct01,
                ContractProgressEvent.CombatRoomCompleted, 3);
            Assert.That(ct01.Reward.Renown, Is.EqualTo(20));
            AssertUnlock(ct01.Reward, ContractUnlockKind.StartingKit, "weapon.bellator.trail_saber");
            Assert.That(ct01.Snapshot.UnlockedStartingKitIds, Does.Contain("weapon.bellator.trail_saber"));

            ContractClaimOutcome ct02 = CompleteAndClaim(ContractCatalog.Ct02,
                ContractProgressEvent.CustodianDefeated, 1);
            Assert.That(ct02.Reward.Renown, Is.EqualTo(30));
            AssertUnlock(ct02.Reward, ContractUnlockKind.GuildRecognition, ContractCatalog.GuildRecognitionUnlockId);
            Assert.That(ct02.Snapshot.IsUnlockGranted(ContractCatalog.GuildRecognitionUnlockId), Is.True);

            ContractClaimOutcome ct03 = CompleteAndClaim(ContractCatalog.Ct03,
                ContractProgressEvent.BoneguardDefeated, 10);
            Assert.That(ct03.Reward.Renown, Is.EqualTo(30));
            AssertUnlock(ct03.Reward, ContractUnlockKind.PlayableRace, "race.orcini");
            Assert.That(ct03.Snapshot.UnlockedRaceIds, Does.Contain("race.orcini"));

            ContractClaimOutcome ct04 = CompleteAndClaim(ContractCatalog.Ct04,
                ContractProgressEvent.ItemCrafted, 3,
                secondEvent: ContractProgressEvent.ItemUpgraded);
            Assert.That(ct04.Reward.Renown, Is.EqualTo(30));
            AssertUnlock(ct04.Reward, ContractUnlockKind.PlayableRace, "race.dvergar");
            AssertUnlock(ct04.Reward, ContractUnlockKind.StartingKit, "armor.bellator.guild_cuirass");
            Assert.That(ct04.Snapshot.UnlockedRaceIds, Does.Contain("race.dvergar"));
            Assert.That(ct04.Snapshot.UnlockedStartingKitIds, Does.Contain("armor.bellator.guild_cuirass"));

            Assert.That(ct01.Snapshot.AvailableRenown, Is.EqualTo(20));
            Assert.That(ct02.Snapshot.AvailableRenown, Is.EqualTo(30));
            Assert.That(ct03.Snapshot.AvailableRenown, Is.EqualTo(30));
            Assert.That(ct04.Snapshot.AvailableRenown, Is.EqualTo(30));
        }

        [Test]
        public void RepeatingAClaimReturnsAlreadyClaimedWithoutRepeatingItsReward()
        {
            ContractClaimOutcome first = CompleteAndClaim(ContractCatalog.Ct02,
                ContractProgressEvent.CustodianDefeated, 1);
            ContractClaimOutcome repeated = ContractProgressionRules.Claim(first.Snapshot, ContractCatalog.Ct02);

            Assert.That(repeated.Status, Is.EqualTo(ContractClaimStatus.AlreadyClaimed));
            Assert.That(repeated.Succeeded, Is.True);
            Assert.That(repeated.ChangesProfile, Is.False);
            Assert.That(repeated.Reward, Is.Null);
            Assert.That(repeated.Snapshot, Is.SameAs(first.Snapshot));
            Assert.That(repeated.Snapshot.AvailableRenown, Is.EqualTo(30));
        }

        [Test]
        public void UnknownUnacceptedAndIncompleteClaimsDoNotChangeTheProfile()
        {
            ContractProgressionSnapshot current = Empty();

            ContractClaimOutcome unknown = ContractProgressionRules.Claim(current, "CT99");
            ContractClaimOutcome notAccepted = ContractProgressionRules.Claim(current, ContractCatalog.Ct01);
            ContractProgressionSnapshot accepted = ContractProgressionRules.Accept(current, ContractCatalog.Ct01).Snapshot;
            ContractClaimOutcome incomplete = ContractProgressionRules.Claim(accepted, ContractCatalog.Ct01);

            Assert.That(unknown.Status, Is.EqualTo(ContractClaimStatus.RejectedUnknownContract));
            Assert.That(notAccepted.Status, Is.EqualTo(ContractClaimStatus.RejectedNotAccepted));
            Assert.That(incomplete.Status, Is.EqualTo(ContractClaimStatus.RejectedIncomplete));
            Assert.That(unknown.Snapshot, Is.SameAs(current));
            Assert.That(notAccepted.Snapshot, Is.SameAs(current));
            Assert.That(incomplete.Snapshot, Is.SameAs(accepted));
            Assert.That(incomplete.Reward, Is.Null);
        }

        [Test]
        public void InvalidStateAndProgressInputsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Empty(renown: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ContractProgressCounter(ContractCatalog.Ct01, -1));
            Assert.Throws<ArgumentNullException>(() => new ContractProgressionSnapshot(0, null,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<ContractProgressCounter>()));
            Assert.Throws<ArgumentException>(() => new ContractProgressionSnapshot(0,
                Array.Empty<string>(), Array.Empty<string>(),
                new[] { ContractCatalog.Ct01, ContractCatalog.Ct02, ContractCatalog.Ct03, ContractCatalog.Ct04 },
                Array.Empty<string>(), Array.Empty<ContractProgressCounter>()));
            Assert.Throws<ArgumentException>(() => new ContractProgressionSnapshot(0,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                new[] { ContractCatalog.Ct01 }, Array.Empty<ContractProgressCounter>()));

            Assert.Throws<ArgumentNullException>(() => ContractProgressionRules.Accept(null, ContractCatalog.Ct01));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContractProgressionRules.RecordProgress(
                Empty(), (ContractProgressEvent)999, "invalid/event"));
            Assert.That(ContractProgressionRules.Accept(Empty(), " ").Status,
                Is.EqualTo(ContractAcceptStatus.RejectedUnknownContract));
        }

        [Test]
        public void RenownOverflowRejectsTheWholeClaimWithoutGrantingUnlocks()
        {
            ContractProgressionSnapshot current = Empty(renown: int.MaxValue,
                counters: new[] { new ContractProgressCounter(ContractCatalog.Ct01, 3) });
            current = ContractProgressionRules.Accept(current, ContractCatalog.Ct01).Snapshot;

            ContractClaimOutcome result = ContractProgressionRules.Claim(current, ContractCatalog.Ct01);

            Assert.That(result.Status, Is.EqualTo(ContractClaimStatus.RejectedRenownOverflow));
            Assert.That(result.Snapshot, Is.SameAs(current));
            Assert.That(result.Snapshot.IsClaimed(ContractCatalog.Ct01), Is.False);
            Assert.That(result.Snapshot.IsUnlockGranted("weapon.bellator.trail_saber"), Is.False);
        }

        private static void AssertDefinition(string contractId, ContractGoalType goalType, int requiredProgress,
            int renown, params string[] expectedUnlockIds)
        {
            ContractDefinition definition = ContractCatalog.Get(contractId);
            Assert.That(definition.GoalType, Is.EqualTo(goalType));
            Assert.That(definition.RequiredProgress, Is.EqualTo(requiredProgress));
            Assert.That(definition.TitleLocalizationKey, Is.EqualTo("contract." + contractId.ToLowerInvariant() + ".title"));
            Assert.That(definition.ObjectiveLocalizationKey, Is.EqualTo("contract." + contractId.ToLowerInvariant() + ".objective"));
            Assert.That(definition.Reward.Renown, Is.EqualTo(renown));
            CollectionAssert.AreEquivalent(expectedUnlockIds, definition.Reward.Unlocks.Select(unlock => unlock.UnlockId));
        }

        private static ContractProgressionSnapshot Empty(int renown = 0,
            ContractProgressCounter[] counters = null,
            ContractProgressEventReceipt[] processedEvents = null)
        {
            return new ContractProgressionSnapshot(renown,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                counters ?? Array.Empty<ContractProgressCounter>(), processedEvents);
        }

        private static ContractProgressionSnapshot Record(ContractProgressionSnapshot current, ContractProgressEvent progressEvent)
        {
            return ContractProgressionRules.RecordProgress(current, progressEvent,
                "test/rules/" + current.ProcessedEvents.Count).Snapshot;
        }

        private static ContractClaimOutcome CompleteAndClaim(string contractId,
            ContractProgressEvent progressEvent, int count, ContractProgressEvent? secondEvent = null)
        {
            ContractProgressionSnapshot current = Empty();
            for (int i = 0; i < count; i++)
                current = Record(current, i == 1 && secondEvent.HasValue ? secondEvent.Value : progressEvent);
            current = ContractProgressionRules.Accept(current, contractId).Snapshot;
            return ContractProgressionRules.Claim(current, contractId);
        }

        private static void AssertUnlock(ContractReward reward, ContractUnlockKind kind, string unlockId)
        {
            Assert.That(reward.Unlocks.Count(unlock => unlock.Kind == kind && unlock.UnlockId == unlockId), Is.EqualTo(1));
        }
    }
}
