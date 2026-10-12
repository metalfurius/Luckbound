using Luckbound.Core.Combat;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class HealthPotionUseStateTests
    {
        [Test]
        public void PotionRestoresTwentyFiveAfterTwentyOneTicksAndConsumesOneCharge()
        {
            var state = new HealthPotionUseState();

            HealthPotionUseOutcome started = state.Update(0, requested: true, currentHealth: 50, maximumHealth: 100, interrupted: false);
            Assert.That(started.Status, Is.EqualTo(HealthPotionUseStatus.Preparing));
            Assert.That(state.PreparationTicksRemaining, Is.EqualTo(21));
            Assert.That(state.CurrentCharges, Is.EqualTo(2));

            for (long tick = 1; tick < HealthPotionUseState.PreparationDurationTicks; tick++)
            {
                HealthPotionUseOutcome preparing = state.Update(tick, requested: false, currentHealth: 50, maximumHealth: 100, interrupted: false);
                Assert.That(preparing.Status, Is.EqualTo(HealthPotionUseStatus.Preparing));
                Assert.That(preparing.RemainingCharges, Is.EqualTo(2));
            }

            HealthPotionUseOutcome healed = state.Update(
                HealthPotionUseState.PreparationDurationTicks,
                requested: false,
                currentHealth: 50,
                maximumHealth: 100,
                interrupted: false);

            Assert.That(healed.Status, Is.EqualTo(HealthPotionUseStatus.Healed));
            Assert.That(healed.HealthRestored, Is.EqualTo(25));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void AcceptedHitCanInterruptPreparationWithoutSpendingPotion()
        {
            var state = new HealthPotionUseState();
            state.Update(0, requested: true, currentHealth: 40, maximumHealth: 100, interrupted: false);

            HealthPotionUseOutcome cancelled = state.Update(1, requested: false, currentHealth: 30, maximumHealth: 100, interrupted: true);

            Assert.That(cancelled.Status, Is.EqualTo(HealthPotionUseStatus.Cancelled));
            Assert.That(cancelled.HealthRestored, Is.Zero);
            Assert.That(cancelled.RemainingCharges, Is.EqualTo(2));
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void FullHealthAndEmptyChargesRejectWithoutStartingOrConsuming()
        {
            var full = new HealthPotionUseState();
            HealthPotionUseOutcome fullResult = full.Update(0, requested: true, currentHealth: 100, maximumHealth: 100, interrupted: false);
            Assert.That(fullResult.Status, Is.EqualTo(HealthPotionUseStatus.RejectedAtFullHealth));
            Assert.That(full.CurrentCharges, Is.EqualTo(2));

            var empty = new HealthPotionUseState(initialCharges: 0);
            HealthPotionUseOutcome emptyResult = empty.Update(0, requested: true, currentHealth: 50, maximumHealth: 100, interrupted: false);
            Assert.That(emptyResult.Status, Is.EqualTo(HealthPotionUseStatus.RejectedWithoutCharges));
            Assert.That(empty.CurrentCharges, Is.Zero);
        }

        [Test]
        public void HealingStopsAtMaximumAndChargeCapacityIsThree()
        {
            var state = new HealthPotionUseState(initialCharges: 2);
            state.Update(0, requested: true, currentHealth: 90, maximumHealth: 100, interrupted: false);
            for (long tick = 1; tick < HealthPotionUseState.PreparationDurationTicks; tick++)
            {
                state.Update(tick, requested: false, currentHealth: 90, maximumHealth: 100, interrupted: false);
            }

            HealthPotionUseOutcome healed = state.Update(
                HealthPotionUseState.PreparationDurationTicks,
                requested: false,
                currentHealth: 90,
                maximumHealth: 100,
                interrupted: false);
            state.AddCharges(10);

            Assert.That(healed.HealthRestored, Is.EqualTo(10));
            Assert.That(healed.RemainingCharges, Is.EqualTo(1));
            Assert.That(state.CurrentCharges, Is.EqualTo(3));
        }
    }
}
