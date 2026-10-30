using System;
using Luckbound.Core.Pacts;
using Luckbound.Core.Relics;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Relics
{
    public sealed class SupportRelicActorRulesTests
    {
        [Test]
        public void HorizontalSpeedAddsArmorPactAndTwoSlotSupportPercentagesOnce()
        {
            SupportRelicModifiers cord = SupportRelicResolver.Resolve(
                SupportRelicIds.RouteCord, SupportRelicIds.BlessedVial);
            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });

            Assert.That(SupportRelicActorRules.ResolveHorizontalSpeed(6m, 3, default, cord), Is.EqualTo(6.66m));
            Assert.That(SupportRelicActorRules.ResolveHorizontalSpeed(6m, -5, default, cord), Is.EqualTo(6.18m));
            Assert.That(SupportRelicActorRules.ResolveHorizontalSpeed(6m, 3, haste, cord), Is.EqualTo(7.56m));
        }

        [Test]
        public void HorizontalSpeedUsesDecimalArithmeticAndCapsAtEightPointFive()
        {
            SupportRelicModifiers cord = SupportRelicResolver.Resolve(SupportRelicIds.RouteCord, null);
            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });

            Assert.That(SupportRelicActorRules.ResolveHorizontalSpeed(6.01m, 3, default, cord), Is.EqualTo(6.6711m));
            Assert.That(SupportRelicActorRules.ResolveHorizontalSpeed(8m, 20, haste, cord), Is.EqualTo(8.5m));
            Assert.That(SupportRelicActorRules.ResolveHorizontalSpeed(0m, 3, haste, cord), Is.Zero);
        }

        [Test]
        public void HorizontalSpeedRejectsNegativeBaseAndCheckedOverflow()
        {
            PactModifierTotals haste = PactModifierTotals.Sum(new[] { PactIds.Haste });

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SupportRelicActorRules.ResolveHorizontalSpeed(-0.01m, 0, default, default));
            Assert.Throws<OverflowException>(() =>
                SupportRelicActorRules.ResolveHorizontalSpeed(decimal.MaxValue, 100, haste, default));
        }

        [Test]
        public void HarmfulDurationAddsArmorAndBarkBeforeHalfUpRounding()
        {
            SupportRelicModifiers bark = SupportRelicResolver.Resolve(SupportRelicIds.SigiledBark, null);

            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(60, 15, bark), Is.EqualTo(36));
            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(5, 10, default), Is.EqualTo(5),
                "4.5 ticks rounds half-up to 5.");
            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(60, -10, bark), Is.EqualTo(51));
        }

        [Test]
        public void HarmfulDurationClampsReductionAndPreservesPositiveMinimum()
        {
            SupportRelicModifiers bark = SupportRelicResolver.Resolve(SupportRelicIds.SigiledBark, null);

            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(1, 99, default), Is.EqualTo(1));
            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(60, 75, bark), Is.Zero);
            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(60, 120, default), Is.Zero);
            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(60, -20, default), Is.EqualTo(60));
            Assert.That(SupportRelicActorRules.ResolveHarmfulDurationTicks(0, 100, bark), Is.Zero);
        }

        [Test]
        public void HarmfulDurationRejectsNegativeBaseAndCheckedPercentageOverflow()
        {
            SupportRelicModifiers bark = SupportRelicResolver.Resolve(SupportRelicIds.SigiledBark, null);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SupportRelicActorRules.ResolveHarmfulDurationTicks(-1, 0, default));
            Assert.Throws<OverflowException>(() =>
                SupportRelicActorRules.ResolveHarmfulDurationTicks(30, int.MaxValue, bark));
        }
    }
}
