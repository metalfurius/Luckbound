using System;
using System.Linq;
using Luckbound.Core.Progression.Contracts;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Progression.Contracts
{
    public sealed class ContractProgressionRulesTests
    {
        [Test]
        public void AcceptingUsesProgressAlreadyPresentInTheProfileCounter()
        {
            var current = Empty(counters: new[] { new ContractProgressCounter(ContractCatalog.Ct01, 2) });

            ContractAcceptOutcome accepted = ContractProgressionRules.Accept(current, ContractCatalog.Ct01);

            Assert.That(accepted.Status, Is.EqualTo(ContractAcceptStatus.Accepted));
            Assert.That(accepted.Snapshot.GetProgress(ContractCatalog.Ct01), Is.EqualTo(2));
            Assert.That(accepted.Snapshot.IsComplete(ContractCatalog.Ct01), Is.False);

            ContractProgressOutcome completed = ContractProgressionRules.RecordProgress(
                accepted.Snapshot, ContractProgressEvent.CombatRoomCompleted);

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
            Assert.That(ContractProgressionRules.RecordProgress(current, ContractProgressEvent.ItemCrafted).ChangesProfile,
                Is.False);
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
            Assert.Throws<ArgumentOutOfRangeException>(() => ContractProgressionRules.RecordProgress(Empty(), (ContractProgressEvent)999));
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

        private static ContractProgressionSnapshot Empty(int renown = 0,
            ContractProgressCounter[] counters = null)
        {
            return new ContractProgressionSnapshot(renown,
                Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
                counters ?? Array.Empty<ContractProgressCounter>());
        }

        private static ContractProgressionSnapshot Record(ContractProgressionSnapshot current, ContractProgressEvent progressEvent)
        {
            return ContractProgressionRules.RecordProgress(current, progressEvent).Snapshot;
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
