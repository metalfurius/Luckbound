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

        [Test]
        public void HasteAndSilenceExposeStableIdsAndTheirCanonicalEffects()
        {
            Assert.That(PactIds.Haste, Is.EqualTo("pact.haste"));
            Assert.That(PactIds.Silence, Is.EqualTo("pact.silence"));
            Assert.That(PactIds.IsKnown(PactIds.Haste), Is.True);
            Assert.That(PactIds.IsKnown(PactIds.Silence), Is.True);

            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });
            Assert.That(haste.HorizontalSpeedBonusPercent, Is.EqualTo(15));
            Assert.That(haste.DefenseReduction, Is.EqualTo(2));
            Assert.That(haste.SkillCooldownReductionPercent, Is.Zero);
            Assert.That(haste.PurchasePriceIncreasePercent, Is.Zero);
            Assert.That(haste.ApplyHorizontalSpeed(6m), Is.EqualTo(6.9m));
            Assert.That(haste.ApplyDefense(5), Is.EqualTo(3));

            PactModifierTotals silence = PactModifierTotals.Sum(new[] { PactIds.Silence });
            Assert.That(silence.SkillCooldownReductionPercent, Is.EqualTo(20));
            Assert.That(silence.PurchasePriceIncreasePercent, Is.EqualTo(25));
            Assert.That(silence.HorizontalSpeedBonusPercent, Is.Zero);
            Assert.That(silence.DefenseReduction, Is.Zero);
            Assert.That(silence.ApplySkillCooldown(1.5m), Is.EqualTo(1.2m));
            Assert.That(silence.ApplyPurchasePrice(3), Is.EqualTo(4));
        }

        [Test]
        public void NewPactEffectsAccumulateWithExistingPactsAndRehydrateFromSnapshots()
        {
            PactModifierTotals avariceAndHaste =
                PactModifierTotals.Sum(new[] { PactIds.Avarice, PactIds.Haste });
            Assert.That(avariceAndHaste.GoldRewardBonusPercent, Is.EqualTo(35));
            Assert.That(avariceAndHaste.MaximumHealthReductionPercent, Is.EqualTo(15));
            Assert.That(avariceAndHaste.HorizontalSpeedBonusPercent, Is.EqualTo(15));
            Assert.That(avariceAndHaste.DefenseReduction, Is.EqualTo(2));

            var hasteState = new PactState(100, 90);
            Assert.That(hasteState.Accept(PactIds.Avarice).Status, Is.EqualTo(PactOperationStatus.Accepted));
            PactActionResult hastePreview = hasteState.PreviewAcceptance(PactIds.Haste);
            Assert.That(hastePreview.Status, Is.EqualTo(PactOperationStatus.ReadyToAccept));
            CollectionAssert.AreEqual(new[] { PactIds.Avarice, PactIds.Haste }, hastePreview.Preview.PactIds);
            Assert.That(hastePreview.Preview.PreviewHorizontalSpeed(7m), Is.EqualTo(8.05m));
            Assert.That(hastePreview.Preview.PreviewDefense(1), Is.Zero);
            Assert.That(hasteState.AcceptedPactIds, Has.Count.EqualTo(1));
            Assert.That(hasteState.CurrentHealth, Is.EqualTo(85), "Previewing Haste must not mutate health.");
            Assert.That(hasteState.Accept(PactIds.Haste).Status, Is.EqualTo(PactOperationStatus.Accepted));

            PactState restoredHaste = PactState.Restore(hasteState.CreateSnapshot());
            CollectionAssert.AreEqual(new[] { PactIds.Avarice, PactIds.Haste }, restoredHaste.AcceptedPactIds);
            Assert.That(restoredHaste.Modifiers.ApplyHorizontalSpeed(7m), Is.EqualTo(8.05m));
            Assert.That(restoredHaste.Modifiers.ApplyDefense(1), Is.Zero);
            Assert.That(restoredHaste.CurrentHealth, Is.EqualTo(hasteState.CurrentHealth));

            PactModifierTotals fervorAndSilence =
                PactModifierTotals.Sum(new[] { PactIds.Fervor, PactIds.Silence });
            Assert.That(fervorAndSilence.BasicAttackDamageBonusPercent, Is.EqualTo(20));
            Assert.That(fervorAndSilence.SkillCostIncreasePercent, Is.EqualTo(25));
            Assert.That(fervorAndSilence.SkillCooldownReductionPercent, Is.EqualTo(20));
            Assert.That(fervorAndSilence.PurchasePriceIncreasePercent, Is.EqualTo(25));
            Assert.That(fervorAndSilence.ApplySkillCost(3), Is.EqualTo(4));
            Assert.That(fervorAndSilence.ApplyPurchasePrice(3), Is.EqualTo(4));

            var silenceState = new PactState(100, 100);
            silenceState.Accept(PactIds.Fervor);
            PactPreview silencePreview = silenceState.PreviewAcceptance(PactIds.Silence).Preview;
            Assert.That(silencePreview.PreviewSkillCooldown(1.5m), Is.EqualTo(1.2m));
            Assert.That(silencePreview.PreviewPurchasePrice(3), Is.EqualTo(4));
            Assert.That(silenceState.AcceptedPactIds, Has.Count.EqualTo(1));
            Assert.That(silenceState.Accept(PactIds.Silence).Status, Is.EqualTo(PactOperationStatus.Accepted));

            PactState restoredSilence = PactState.Restore(silenceState.CreateSnapshot());
            CollectionAssert.AreEqual(new[] { PactIds.Fervor, PactIds.Silence }, restoredSilence.AcceptedPactIds);
            Assert.That(restoredSilence.Modifiers.ApplySkillCooldown(1.5m), Is.EqualTo(1.2m));
            Assert.That(restoredSilence.Modifiers.ApplyPurchasePrice(3), Is.EqualTo(4));
            Assert.That(restoredSilence.PreviewAcceptance(PactIds.Haste).Status,
                Is.EqualTo(PactOperationStatus.RejectedAtCapacity));
        }

        [Test]
        public void NewEffectsRespectGlobalLimitsAndRoundPurchasePricesUp()
        {
            PactModifierTotals none = PactModifierTotals.Sum(Array.Empty<string>());
            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });
            PactModifierTotals silence = PactModifierTotals.Sum(new[] { PactIds.Silence });

            Assert.That(none.ApplyHorizontalSpeed(9m), Is.EqualTo(PactModifierTotals.MaximumHorizontalSpeed));
            Assert.That(haste.ApplyHorizontalSpeed(8m), Is.EqualTo(PactModifierTotals.MaximumHorizontalSpeed));
            Assert.That(none.ApplySkillCooldown(0.25m),
                Is.EqualTo(PactModifierTotals.MinimumSkillCooldownSeconds));
            Assert.That(silence.ApplySkillCooldown(0.25m),
                Is.EqualTo(PactModifierTotals.MinimumSkillCooldownSeconds));
            Assert.That(haste.ApplyDefense(1), Is.Zero);
            Assert.That(haste.ApplyDefense(0), Is.Zero);
            Assert.That(silence.ApplyPurchasePrice(1), Is.EqualTo(2));
            Assert.That(silence.ApplyPurchasePrice(3), Is.EqualTo(4));
            Assert.That(silence.ApplyPurchasePrice(0), Is.Zero);
        }

        [Test]
        public void NewEffectCalculatorsRejectInvalidInputsAndPactSets()
        {
            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });
            PactModifierTotals silence = PactModifierTotals.Sum(new[] { PactIds.Silence });

            Assert.Throws<ArgumentOutOfRangeException>(() => haste.ApplyHorizontalSpeed(-0.1m));
            Assert.Throws<ArgumentOutOfRangeException>(() => haste.ApplyDefense(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => silence.ApplySkillCooldown(0m));
            Assert.Throws<ArgumentOutOfRangeException>(() => silence.ApplyPurchasePrice(-1));
            Assert.Throws<OverflowException>(() => silence.ApplyPurchasePrice(int.MaxValue));

            Assert.Throws<ArgumentNullException>(() => PactModifierTotals.Sum(null));
            Assert.Throws<ArgumentException>(() => PactModifierTotals.Sum(new[] { "pact.unknown" }));
            Assert.Throws<ArgumentException>(() => PactModifierTotals.Sum(new[] { PactIds.Haste, PactIds.Haste }));
            Assert.Throws<ArgumentException>(() =>
                PactModifierTotals.Sum(new[] { PactIds.Avarice, PactIds.Fervor, PactIds.Haste }));
        }
    }
}
