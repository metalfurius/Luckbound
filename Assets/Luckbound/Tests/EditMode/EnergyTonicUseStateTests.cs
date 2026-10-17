using System;
using Luckbound.Core.Combat;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class EnergyTonicUseStateTests
    {
        [Test]
        public void TonicRestoresTwentyFiveAfterTwentyOneTicksAndConsumesExactlyOneCharge()
        {
            var state = new EnergyTonicUseState(initialCharges: 2);

            EnergyTonicUseOutcome started = state.Update(0, requested: true, currentEnergy: 50, maximumEnergy: 100, interrupted: false);
            Assert.That(started.Status, Is.EqualTo(EnergyTonicUseStatus.Preparing));
            Assert.That(state.PreparationTicksRemaining, Is.EqualTo(21));
            Assert.That(state.CurrentCharges, Is.EqualTo(2));

            for (long tick = 1; tick < EnergyTonicUseState.PreparationDurationTicks; tick++)
            {
                EnergyTonicUseOutcome preparing = state.Update(tick, requested: false, currentEnergy: 50, maximumEnergy: 100, interrupted: false);
                Assert.That(preparing.Status, Is.EqualTo(EnergyTonicUseStatus.Preparing));
                Assert.That(preparing.EnergyRestored, Is.Zero);
                Assert.That(preparing.RemainingCharges, Is.EqualTo(2));
            }

            EnergyTonicUseOutcome energized = state.Update(
                EnergyTonicUseState.PreparationDurationTicks,
                requested: false,
                currentEnergy: 50,
                maximumEnergy: 100,
                interrupted: false);

            Assert.That(energized.Status, Is.EqualTo(EnergyTonicUseStatus.Energized));
            Assert.That(energized.EnergyRestored, Is.EqualTo(25));
            Assert.That(energized.RemainingCharges, Is.EqualTo(1));
            Assert.That(state.CurrentCharges, Is.EqualTo(1));
            Assert.That(state.IsPreparing, Is.False);

            EnergyTonicUseOutcome nextTick = state.Update(22, requested: false, currentEnergy: 75, maximumEnergy: 100, interrupted: false);
            Assert.That(nextTick.Status, Is.EqualTo(EnergyTonicUseStatus.Idle));
            Assert.That(state.CurrentCharges, Is.EqualTo(1));
        }

        [Test]
        public void RestorationAndChargeCapacityAreCapped()
        {
            var state = new EnergyTonicUseState(initialCharges: 2);
            state.Update(0, requested: true, currentEnergy: 95, maximumEnergy: 100, interrupted: false);
            for (long tick = 1; tick < EnergyTonicUseState.PreparationDurationTicks; tick++)
            {
                state.Update(tick, requested: false, currentEnergy: 95, maximumEnergy: 100, interrupted: false);
            }

            EnergyTonicUseOutcome energized = state.Update(
                EnergyTonicUseState.PreparationDurationTicks,
                requested: false,
                currentEnergy: 95,
                maximumEnergy: 100,
                interrupted: false);
            state.AddCharges(int.MaxValue);

            Assert.That(energized.EnergyRestored, Is.EqualTo(5));
            Assert.That(energized.RemainingCharges, Is.EqualTo(1));
            Assert.That(state.CurrentCharges, Is.EqualTo(3));
            Assert.That(state.CurrentCharges, Is.LessThanOrEqualTo(state.MaximumCharges));
        }

        [Test]
        public void FullEnergyRejectsStartAndCompletionWithoutConsumingACharge()
        {
            var state = new EnergyTonicUseState(initialCharges: 1);
            EnergyTonicUseOutcome full = state.Update(0, requested: true, currentEnergy: 100, maximumEnergy: 100, interrupted: false);

            Assert.That(full.Status, Is.EqualTo(EnergyTonicUseStatus.RejectedAtFullEnergy));
            Assert.That(state.IsPreparing, Is.False);
            Assert.That(state.CurrentCharges, Is.EqualTo(1));

            EnergyTonicUseOutcome zeroCapacity = state.Update(1, requested: true, currentEnergy: 0, maximumEnergy: 0, interrupted: false);
            Assert.That(zeroCapacity.Status, Is.EqualTo(EnergyTonicUseStatus.RejectedAtFullEnergy));
            Assert.That(state.CurrentCharges, Is.EqualTo(1));

            state.Update(2, requested: true, currentEnergy: 50, maximumEnergy: 100, interrupted: false);
            for (long tick = 3; tick < 2 + EnergyTonicUseState.PreparationDurationTicks; tick++)
            {
                state.Update(tick, requested: false, currentEnergy: 50, maximumEnergy: 100, interrupted: false);
            }

            EnergyTonicUseOutcome becameFull = state.Update(
                2 + EnergyTonicUseState.PreparationDurationTicks,
                requested: false,
                currentEnergy: 100,
                maximumEnergy: 100,
                interrupted: false);

            Assert.That(becameFull.Status, Is.EqualTo(EnergyTonicUseStatus.RejectedAtFullEnergy));
            Assert.That(becameFull.EnergyRestored, Is.Zero);
            Assert.That(becameFull.RemainingCharges, Is.EqualTo(1));
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void InterruptionCancelsPreparationWithoutSpendingTheCharge()
        {
            var state = new EnergyTonicUseState(initialCharges: 1);
            state.Update(0, requested: true, currentEnergy: 40, maximumEnergy: 100, interrupted: false);

            EnergyTonicUseOutcome cancelled = state.Update(1, requested: false, currentEnergy: 30, maximumEnergy: 100, interrupted: true);

            Assert.That(cancelled.Status, Is.EqualTo(EnergyTonicUseStatus.Cancelled));
            Assert.That(cancelled.EnergyRestored, Is.Zero);
            Assert.That(cancelled.RemainingCharges, Is.EqualTo(1));
            Assert.That(state.CurrentCharges, Is.EqualTo(1));
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void MissingChargesRejectStartingAndChargesCanBeAddedUpToThree()
        {
            var state = new EnergyTonicUseState();
            EnergyTonicUseOutcome empty = state.Update(0, requested: true, currentEnergy: 40, maximumEnergy: 100, interrupted: false);

            Assert.That(empty.Status, Is.EqualTo(EnergyTonicUseStatus.RejectedWithoutCharges));
            Assert.That(empty.RemainingCharges, Is.Zero);

            state.AddCharges(2);
            Assert.That(state.CurrentCharges, Is.EqualTo(2));
            Assert.That(state.MaximumCharges, Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.AddCharges(-1));
        }

        [Test]
        public void InvalidChargeEnergyAndTickValuesAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyTonicUseState(initialCharges: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyTonicUseState(initialCharges: 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyTonicUseState(initialCharges: 1, maximumCharges: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnergyTonicUseState(initialCharges: 0, maximumCharges: 4));

            var state = new EnergyTonicUseState(initialCharges: 1);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(-1, true, 10, 100, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(0, true, -1, 100, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(0, true, 101, 100, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(0, true, 0, -1, false));

            Assert.That(state.Update(2, true, 10, 100, false).Status, Is.EqualTo(EnergyTonicUseStatus.Preparing));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(2, false, 10, 100, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(1, false, 10, 100, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(0, false, 10, 100, false));
        }
    }
}
