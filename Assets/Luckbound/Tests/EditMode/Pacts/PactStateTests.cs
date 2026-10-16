using System;
using System.Collections.Generic;
using Luckbound.Core.Pacts;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class PactStateTests
    {
        [Test]
        public void AvariceAndFervorExposeTheirCanonicalEffects()
        {
            PactModifierTotals avarice = PactModifierTotals.Sum(new[] { PactIds.Avarice });
            PactModifierTotals fervor = PactModifierTotals.Sum(new[] { PactIds.Fervor });

            Assert.That(avarice.GoldRewardBonusPercent, Is.EqualTo(35));
            Assert.That(avarice.BasicAttackDamageBonusPercent, Is.Zero);
            Assert.That(avarice.MaximumHealthReductionPercent, Is.EqualTo(15));
            Assert.That(avarice.SkillCostIncreasePercent, Is.Zero);
            Assert.That(avarice.ApplyGoldReward(100), Is.EqualTo(135));
            Assert.That(avarice.ApplyMaximumHealth(100), Is.EqualTo(85));

            Assert.That(fervor.GoldRewardBonusPercent, Is.Zero);
            Assert.That(fervor.BasicAttackDamageBonusPercent, Is.EqualTo(20));
            Assert.That(fervor.MaximumHealthReductionPercent, Is.Zero);
            Assert.That(fervor.SkillCostIncreasePercent, Is.EqualTo(25));
            Assert.That(fervor.ApplyBasicAttackDamage(24m), Is.EqualTo(28.8m));
            Assert.That(fervor.ApplySkillCost(5), Is.EqualTo(7));
        }

        [Test]
        public void PreviewCombinesEffectsIndependentlyOfPactAcceptanceOrder()
        {
            var avariceFirst = new PactState(100, 92);
            PactActionResult avaricePreview = avariceFirst.PreviewAcceptance(PactIds.Avarice);
            Assert.That(avaricePreview.Status, Is.EqualTo(PactOperationStatus.ReadyToAccept));
            Assert.That(avaricePreview.Preview.MaximumHealth, Is.EqualTo(85));
            Assert.That(avaricePreview.Preview.ProjectedCurrentHealth, Is.EqualTo(85));
            Assert.That(avariceFirst.CurrentHealth, Is.EqualTo(92), "A preview must not mutate health.");

            Assert.That(avariceFirst.Accept(PactIds.Avarice).Status, Is.EqualTo(PactOperationStatus.Accepted));
            PactPreview combined = avariceFirst.PreviewAcceptance(PactIds.Fervor).Preview;
            Assert.That(combined.PactIds, Is.EqualTo(new[] { PactIds.Avarice, PactIds.Fervor }));
            Assert.That(combined.Modifiers.GoldRewardBonusPercent, Is.EqualTo(35));
            Assert.That(combined.Modifiers.BasicAttackDamageBonusPercent, Is.EqualTo(20));
            Assert.That(combined.Modifiers.MaximumHealthReductionPercent, Is.EqualTo(15));
            Assert.That(combined.Modifiers.SkillCostIncreasePercent, Is.EqualTo(25));
            Assert.That(combined.PreviewGoldReward(3), Is.EqualTo(4));
            Assert.That(combined.Modifiers.BasicAttackDamageBonusPercent, Is.EqualTo(20));
            Assert.That(combined.PreviewSkillCost(5), Is.EqualTo(7));

            var fervorFirst = new PactState(100, 92);
            fervorFirst.Accept(PactIds.Fervor);
            fervorFirst.Accept(PactIds.Avarice);
            Assert.That(avariceFirst.Accept(PactIds.Fervor).Status, Is.EqualTo(PactOperationStatus.Accepted));
            PactSnapshot firstSnapshot = avariceFirst.CreateSnapshot();
            PactSnapshot secondSnapshot = fervorFirst.CreateSnapshot();
            CollectionAssert.AreEqual(firstSnapshot.AcceptedPactIds, secondSnapshot.AcceptedPactIds);
            Assert.That(firstSnapshot.CurrentHealth, Is.EqualTo(secondSnapshot.CurrentHealth));
            Assert.That(avariceFirst.CurrentHealth, Is.EqualTo(85));
            Assert.That(fervorFirst.CurrentHealth, Is.EqualTo(85));
        }

        [Test]
        public void PactCapacityAndDuplicateRulesKeepTheAcceptedSetUnique()
        {
            var state = new PactState(100, 100);
            Assert.That(state.Accept(PactIds.Avarice).Status, Is.EqualTo(PactOperationStatus.Accepted));
            Assert.That(state.PreviewAcceptance(PactIds.Avarice).Status, Is.EqualTo(PactOperationStatus.AlreadyAccepted));
            Assert.That(state.Accept(PactIds.Fervor).Status, Is.EqualTo(PactOperationStatus.Accepted));

            Assert.That(PactState.MaximumPactsPerExpedition, Is.EqualTo(2));
            Assert.That(state.AcceptedPactIds, Has.Count.EqualTo(PactState.MaximumPactsPerExpedition));
            Assert.That(state.PreviewAcceptance("pact.unimplemented").Status,
                Is.EqualTo(PactOperationStatus.RejectedUnknownPact));
            Assert.That(state.PreviewAcceptance(PactIds.Fervor).Status, Is.EqualTo(PactOperationStatus.AlreadyAccepted));
            CollectionAssert.AreEqual(new[] { PactIds.Avarice, PactIds.Fervor }, state.AcceptedPactIds);
        }

        [Test]
        public void CostsRoundUpRewardsRoundDownAndDamageBonusRemainsAnAdditiveCategory()
        {
            PactModifierTotals combined = PactModifierTotals.Sum(new[] { PactIds.Avarice, PactIds.Fervor });

            Assert.That(combined.ApplySkillCost(3), Is.EqualTo(4), "3 × 1.25 = 3.75 rounds up.");
            Assert.That(combined.ApplyGoldReward(3), Is.EqualTo(4), "3 × 1.35 = 4.05 rounds down.");
            Assert.That(combined.BasicAttackDamageBonusPercent, Is.EqualTo(20));
            Assert.That(combined.ApplyBasicAttackDamage(24m), Is.EqualTo(28.8m));
            Assert.That(combined.ApplySkillCost(1), Is.EqualTo(2));
            Assert.That(combined.ApplyGoldReward(0), Is.Zero);
        }

        [Test]
        public void BasicAttackDamagePreservesFractionalResultsAndRejectsNegativeDamage()
        {
            PactModifierTotals fervor = PactModifierTotals.Sum(new[] { PactIds.Fervor });

            Assert.That(fervor.ApplyBasicAttackDamage(24m), Is.EqualTo(28.8m));
            Assert.That(fervor.ApplyBasicAttackDamage(0m), Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => fervor.ApplyBasicAttackDamage(-1m));
        }

        [Test]
        public void ReducingMaximumHealthClampsCurrentHealthAndRemovingItDoesNotRefill()
        {
            var state = new PactState(100, 95);

            PactActionResult applied = state.Accept(PactIds.Avarice);
            Assert.That(applied.Status, Is.EqualTo(PactOperationStatus.Accepted));
            Assert.That(state.MaximumHealth, Is.EqualTo(85));
            Assert.That(state.CurrentHealth, Is.EqualTo(85));

            PactActionResult removed = state.Remove(PactIds.Avarice);
            Assert.That(removed.Status, Is.EqualTo(PactOperationStatus.Removed));
            Assert.That(state.MaximumHealth, Is.EqualTo(100));
            Assert.That(state.CurrentHealth, Is.EqualTo(85), "Removing a health penalty must not heal.");

            Assert.That(state.Remove(PactIds.Avarice).Status, Is.EqualTo(PactOperationStatus.NotAccepted));
        }

        [Test]
        public void SnapshotIsImmutableValidatedAndRestoresPactsAndHealthExactly()
        {
            var original = new List<string> { PactIds.Avarice, PactIds.Fervor };
            var snapshot = new PactSnapshot(PactSnapshot.CurrentVersion, 100, 85, original);
            original.Clear();

            Assert.That(snapshot.AcceptedPactIds, Has.Count.EqualTo(2));
            IList<string> exposedIds = snapshot.AcceptedPactIds as IList<string>;
            Assert.That(exposedIds, Is.Not.Null);
            Assert.That(exposedIds.IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => exposedIds[0] = "pact.changed");

            PactState restored = PactState.Restore(snapshot);
            Assert.That(restored.BaseMaximumHealth, Is.EqualTo(100));
            Assert.That(restored.MaximumHealth, Is.EqualTo(85));
            Assert.That(restored.CurrentHealth, Is.EqualTo(85));
            CollectionAssert.AreEqual(new[] { PactIds.Avarice, PactIds.Fervor }, restored.AcceptedPactIds);
            CollectionAssert.AreEqual(snapshot.AcceptedPactIds, restored.CreateSnapshot().AcceptedPactIds);

            Assert.That(restored.Remove(PactIds.Avarice).Status, Is.EqualTo(PactOperationStatus.Removed));
            Assert.That(restored.MaximumHealth, Is.EqualTo(100));
            Assert.That(restored.CurrentHealth, Is.EqualTo(85), "Restoring and then removing a pact must not refill health.");
        }

        [Test]
        public void InvalidSnapshotsAreRejectedAtConstruction()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new PactSnapshot(2, 100, 100, Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new PactSnapshot(PactSnapshot.CurrentVersion, 100, 100,
                new[] { "pact.unimplemented" }));
            Assert.Throws<ArgumentException>(() => new PactSnapshot(PactSnapshot.CurrentVersion, 100, 85,
                new[] { PactIds.Avarice, PactIds.Avarice }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PactSnapshot(PactSnapshot.CurrentVersion, 100, 86,
                new[] { PactIds.Avarice }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PactSnapshot(PactSnapshot.CurrentVersion, 100, -1,
                Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>(() => new PactSnapshot(PactSnapshot.CurrentVersion, 100, 100, null));
            Assert.Throws<ArgumentNullException>(() => PactState.Restore(null));
        }
    }
}
