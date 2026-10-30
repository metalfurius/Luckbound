using Luckbound.Core.Combat;
using Luckbound.Core.Relics;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Relics
{
    public sealed class SupportRelicHealingUseTests
    {
        [Test]
        public void DefaultPotionStillRestoresTwentyFiveAfterTwentyOneTicks()
        {
            var state = new HealthPotionUseState();

            HealthPotionUseOutcome healed = CompleteUse(state, startingHealth: 50, maximumHealth: 100);

            Assert.That(HealthPotionUseState.HealingAmount, Is.EqualTo(25));
            Assert.That(healed.Status, Is.EqualTo(HealthPotionUseStatus.Healed));
            Assert.That(healed.HealthRestored, Is.EqualTo(25));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
        }

        [Test]
        public void BlessedVialMakesTheConsumedPotionRestoreThirty()
        {
            var state = new HealthPotionUseState();
            state.ReplaceSupportRelicModifiers(BlessedVial());

            HealthPotionUseOutcome healed = CompleteUse(state, startingHealth: 50, maximumHealth: 100);

            Assert.That(healed.HealthRestored, Is.EqualTo(30));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
        }

        [Test]
        public void BlessedVialHealingClampsToMissingHealth()
        {
            var state = new HealthPotionUseState();
            state.ReplaceSupportRelicModifiers(BlessedVial());

            HealthPotionUseOutcome healed = CompleteUse(state, startingHealth: 95, maximumHealth: 100);

            Assert.That(healed.Status, Is.EqualTo(HealthPotionUseStatus.Healed));
            Assert.That(healed.HealthRestored, Is.EqualTo(5));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
        }

        [Test]
        public void FullHealthRejectsVialPotionWithoutStartingOrSpending()
        {
            var state = new HealthPotionUseState();
            state.ReplaceSupportRelicModifiers(BlessedVial());

            HealthPotionUseOutcome outcome = state.Update(
                0,
                requested: true,
                currentHealth: 100,
                maximumHealth: 100,
                interrupted: false);

            Assert.That(outcome.Status, Is.EqualTo(HealthPotionUseStatus.RejectedAtFullHealth));
            Assert.That(state.IsPreparing, Is.False);
            Assert.That(state.CurrentCharges, Is.EqualTo(2));
        }

        [Test]
        public void InterruptedVialPreparationCancelsWithoutConsumingCharge()
        {
            var state = new HealthPotionUseState();
            state.ReplaceSupportRelicModifiers(BlessedVial());
            state.Update(0, requested: true, currentHealth: 40, maximumHealth: 100, interrupted: false);

            HealthPotionUseOutcome cancelled = state.Update(
                1,
                requested: false,
                currentHealth: 30,
                maximumHealth: 100,
                interrupted: true);

            Assert.That(cancelled.Status, Is.EqualTo(HealthPotionUseStatus.Cancelled));
            Assert.That(cancelled.HealthRestored, Is.Zero);
            Assert.That(cancelled.RemainingCharges, Is.EqualTo(2));
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void EquippingVialDuringPreparationPreservesClockAndAppliesAtConsumption()
        {
            var state = new HealthPotionUseState();
            state.Update(0, requested: true, currentHealth: 40, maximumHealth: 100, interrupted: false);
            AdvancePreparationTo(state, lastTick: 8, currentHealth: 40, maximumHealth: 100);
            int ticksRemaining = state.PreparationTicksRemaining;

            state.ReplaceSupportRelicModifiers(BlessedVial());

            Assert.That(state.IsPreparing, Is.True);
            Assert.That(state.PreparationTicksRemaining, Is.EqualTo(ticksRemaining));
            Assert.That(state.CurrentCharges, Is.EqualTo(2));
            HealthPotionUseOutcome healed = CompleteExistingPreparation(
                state,
                firstTick: 9,
                currentHealth: 40,
                maximumHealth: 100);

            Assert.That(healed.Status, Is.EqualTo(HealthPotionUseStatus.Healed));
            Assert.That(healed.HealthRestored, Is.EqualTo(30));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
        }

        [Test]
        public void RemovingVialDuringPreparationPreservesClockAndUsesBaseHealing()
        {
            var state = new HealthPotionUseState();
            state.ReplaceSupportRelicModifiers(BlessedVial());
            state.Update(0, requested: true, currentHealth: 40, maximumHealth: 100, interrupted: false);
            AdvancePreparationTo(state, lastTick: 8, currentHealth: 40, maximumHealth: 100);
            int ticksRemaining = state.PreparationTicksRemaining;

            state.ReplaceSupportRelicModifiers(default);

            Assert.That(state.IsPreparing, Is.True);
            Assert.That(state.PreparationTicksRemaining, Is.EqualTo(ticksRemaining));
            Assert.That(state.CurrentCharges, Is.EqualTo(2));
            HealthPotionUseOutcome healed = CompleteExistingPreparation(
                state,
                firstTick: 9,
                currentHealth: 40,
                maximumHealth: 100);

            Assert.That(healed.Status, Is.EqualTo(HealthPotionUseStatus.Healed));
            Assert.That(healed.HealthRestored, Is.EqualTo(25));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedEquipAndRemovalDoNotHealOrRefillPotionCharges()
        {
            var state = new HealthPotionUseState(initialCharges: 1, maximumCharges: 3);
            int currentHealth = 61;
            HealthPotionUseOutcome started = state.Update(
                0,
                requested: true,
                currentHealth: currentHealth,
                maximumHealth: 100,
                interrupted: false);

            Assert.That(started.Status, Is.EqualTo(HealthPotionUseStatus.Preparing));
            Assert.That(started.HealthRestored, Is.Zero);
            Assert.That(started.RemainingCharges, Is.EqualTo(1));

            for (int index = 0; index < 5; index++)
            {
                state.ReplaceSupportRelicModifiers(BlessedVial());
                state.ReplaceSupportRelicModifiers(default);
                state.ReplaceSupportRelicModifiers(BlessedVial());

                Assert.That(state.IsPreparing, Is.True);
                Assert.That(state.PreparationTicksRemaining, Is.EqualTo(21));
                Assert.That(state.CurrentCharges, Is.EqualTo(1));
            }

            HealthPotionUseOutcome healed = CompleteExistingPreparation(
                state,
                firstTick: 1,
                currentHealth: currentHealth,
                maximumHealth: 100);

            Assert.That(currentHealth, Is.EqualTo(61));
            Assert.That(healed.HealthRestored, Is.EqualTo(30));
            Assert.That(healed.RemainingCharges, Is.Zero);
            Assert.That(state.CurrentCharges, Is.Zero);
            Assert.That(state.IsPreparing, Is.False);
            Assert.That(state.PreparationTicksRemaining, Is.Zero);
        }

        private static SupportRelicModifiers BlessedVial()
        {
            return SupportRelicResolver.Resolve(SupportRelicIds.BlessedVial, null);
        }

        private static HealthPotionUseOutcome CompleteUse(
            HealthPotionUseState state,
            int startingHealth,
            int maximumHealth)
        {
            state.Update(0, requested: true, currentHealth: startingHealth, maximumHealth: maximumHealth, interrupted: false);
            return CompleteExistingPreparation(state, firstTick: 1, currentHealth: startingHealth, maximumHealth: maximumHealth);
        }

        private static void AdvancePreparationTo(
            HealthPotionUseState state,
            long lastTick,
            int currentHealth,
            int maximumHealth)
        {
            for (long tick = 1; tick <= lastTick; tick++)
            {
                HealthPotionUseOutcome outcome = state.Update(
                    tick,
                    requested: false,
                    currentHealth: currentHealth,
                    maximumHealth: maximumHealth,
                    interrupted: false);
                Assert.That(outcome.Status, Is.EqualTo(HealthPotionUseStatus.Preparing));
            }
        }

        private static HealthPotionUseOutcome CompleteExistingPreparation(
            HealthPotionUseState state,
            long firstTick,
            int currentHealth,
            int maximumHealth)
        {
            for (long tick = firstTick; tick < HealthPotionUseState.PreparationDurationTicks; tick++)
            {
                HealthPotionUseOutcome preparing = state.Update(
                    tick,
                    requested: false,
                    currentHealth: currentHealth,
                    maximumHealth: maximumHealth,
                    interrupted: false);
                Assert.That(preparing.Status, Is.EqualTo(HealthPotionUseStatus.Preparing));
            }

            return state.Update(
                HealthPotionUseState.PreparationDurationTicks,
                requested: false,
                currentHealth: currentHealth,
                maximumHealth: maximumHealth,
                interrupted: false);
        }
    }
}
