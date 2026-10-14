using System;
using Luckbound.Core.Combat;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class MagusStatusStateTests
    {
        [Test]
        public void BurnPulsesAtSixtyTickIntervalsRenewsDurationAndKeepsTheStrongerPower()
        {
            var state = new MagusStatusState();
            state.ApplyBurn(tick: 0, damagePerSecond: 3m, durationTicks: 180);

            MagusStatusSnapshot first = state.Tick(0);
            Assert.That(first.IsBurning, Is.True);
            Assert.That(first.BurnDamagePerSecond, Is.EqualTo(3m));
            Assert.That(first.BurnDamageDue, Is.Zero);

            Assert.That(state.Tick(60).BurnDamageDue, Is.EqualTo(3m));
            MagusStatusSnapshot renewed = state.Tick(120);
            Assert.That(renewed.BurnDamageDue, Is.EqualTo(3m));
            state.ApplyBurn(tick: 120, damagePerSecond: 2m, durationTicks: 180);

            MagusStatusSnapshot nextPulse = state.Tick(180);
            Assert.That(nextPulse.BurnDamageDue, Is.EqualTo(3m));
            Assert.That(nextPulse.BurnDamagePerSecond, Is.EqualTo(3m));
            Assert.That(nextPulse.BurnEndsAtTick, Is.EqualTo(300));
            Assert.That(state.Tick(240).BurnDamageDue, Is.EqualTo(3m));
            MagusStatusSnapshot finalPulse = state.Tick(300);
            Assert.That(finalPulse.IsBurning, Is.True);
            Assert.That(finalPulse.BurnDamageDue, Is.EqualTo(3m));
            Assert.That(state.Tick(301).IsBurning, Is.False);
            Assert.That(state.Tick(360).BurnDamageDue, Is.Zero);
        }

        [Test]
        public void AStrongerBurnReplacesPowerAndDoesNotRestartThePulseCadence()
        {
            var state = new MagusStatusState();
            state.ApplyBurn(tick: 0, damagePerSecond: 2m, durationTicks: 180);
            state.Tick(0);
            state.ApplyBurn(tick: 30, damagePerSecond: 4m, durationTicks: 180);

            MagusStatusSnapshot pulse = state.Tick(60);

            Assert.That(pulse.BurnDamageDue, Is.EqualTo(4m));
            Assert.That(pulse.BurnDamagePerSecond, Is.EqualTo(4m));
            Assert.That(pulse.BurnEndsAtTick, Is.EqualTo(210));
        }

        [Test]
        public void FreezeLastsSixtyTicksThenProtectsAgainstAnotherFreezeForOneHundredTwenty()
        {
            var state = new MagusStatusState();
            Assert.That(state.ApplyFreeze(tick: 0, durationTicks: 60), Is.True);
            Assert.That(state.Tick(0).IsFrozen, Is.True);
            Assert.That(state.Tick(59).IsFrozen, Is.True);

            MagusStatusSnapshot thawed = state.Tick(60);
            Assert.That(thawed.IsFrozen, Is.False);
            Assert.That(thawed.IsFreezeProtected, Is.True);
            Assert.That(thawed.FreezeProtectionEndsAtTick, Is.EqualTo(180));
            Assert.That(state.ApplyFreeze(tick: 60, durationTicks: 60), Is.False);
            Assert.That(state.Tick(179).IsFreezeProtected, Is.True);
            Assert.That(state.Tick(180).IsFreezeProtected, Is.False);
            Assert.That(state.ApplyFreeze(tick: 180, durationTicks: 60), Is.True);
            Assert.That(state.Tick(181).IsFrozen, Is.True);
        }

        [Test]
        public void InvalidStatusValuesAndOutOfOrderTicksAreRejected()
        {
            var state = new MagusStatusState();
            Assert.That(() => state.ApplyBurn(0, 0m, 180), Throws.ArgumentOutOfRangeException);
            Assert.That(() => state.ApplyBurn(0, 3m, 0), Throws.ArgumentOutOfRangeException);
            Assert.That(() => state.ApplyFreeze(0, 0), Throws.ArgumentOutOfRangeException);

            state.Tick(2);
            Assert.That(() => state.ApplyBurn(1, 3m, 180), Throws.ArgumentOutOfRangeException);
            Assert.That(() => state.Tick(2), Throws.InvalidOperationException);
        }
    }
}
