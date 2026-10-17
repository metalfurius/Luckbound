using System;
using Luckbound.Core.Relics;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Relics
{
    public sealed class SupportRelicResolverTests
    {
        [TestCase(SupportRelicIds.PaleBone, 15, 0, 0, 0, 0, 0, 0, 0)]
        [TestCase(SupportRelicIds.ArcaneFlask, 0, 10, 0, 0, 0, 0, 0, 0)]
        [TestCase(SupportRelicIds.ApprenticeGear, 0, 0, 10, 0, 0, 0, 0, 0)]
        [TestCase(SupportRelicIds.AlvariLeaf, 0, 0, 0, 10, 0, 0, 0, 0)]
        [TestCase(SupportRelicIds.RouteCord, 0, 0, 0, 0, 8, 0, 0, 0)]
        [TestCase(SupportRelicIds.BlessedVial, 0, 0, 0, 0, 0, 20, 0, 0)]
        [TestCase(SupportRelicIds.SigiledBark, 0, 0, 0, 0, 0, 0, 25, 0)]
        [TestCase(SupportRelicIds.MerchantSeal, 0, 0, 0, 0, 0, 0, 0, 15)]
        public void EachSupportRelicResolvesItsSpecifiedModifier(
            string relicId,
            int expectedMaximumHealthBonus,
            int expectedMaximumEnergyBonus,
            int expectedSkillCooldownReductionPercent,
            int expectedSkillCostReductionPercent,
            int expectedMovementSpeedBonusPercent,
            int expectedConsumableHealingBonusPercent,
            int expectedHarmfulStatusDurationReductionPercent,
            int expectedPurchasePriceReductionPercent)
        {
            SupportRelicModifiers modifiers = SupportRelicResolver.Resolve(relicId, null);

            AssertModifiers(
                modifiers,
                expectedMaximumHealthBonus,
                expectedMaximumEnergyBonus,
                expectedSkillCooldownReductionPercent,
                expectedSkillCostReductionPercent,
                expectedMovementSpeedBonusPercent,
                expectedConsumableHealingBonusPercent,
                expectedHarmfulStatusDurationReductionPercent,
                expectedPurchasePriceReductionPercent);
        }

        [TestCase(SupportRelicIds.PaleBone, SupportRelicIds.ArcaneFlask, 15, 10, 0, 0, 0, 0, 0, 0)]
        [TestCase(SupportRelicIds.ApprenticeGear, SupportRelicIds.AlvariLeaf, 0, 0, 10, 10, 0, 0, 0, 0)]
        [TestCase(SupportRelicIds.RouteCord, SupportRelicIds.BlessedVial, 0, 0, 0, 0, 8, 20, 0, 0)]
        [TestCase(SupportRelicIds.SigiledBark, SupportRelicIds.MerchantSeal, 0, 0, 0, 0, 0, 0, 25, 15)]
        public void TwoDistinctValidSlotsCombineBonusesByTheirOwnCategory(
            string relicOneId,
            string relicTwoId,
            int expectedMaximumHealthBonus,
            int expectedMaximumEnergyBonus,
            int expectedSkillCooldownReductionPercent,
            int expectedSkillCostReductionPercent,
            int expectedMovementSpeedBonusPercent,
            int expectedConsumableHealingBonusPercent,
            int expectedHarmfulStatusDurationReductionPercent,
            int expectedPurchasePriceReductionPercent)
        {
            SupportRelicModifiers modifiers = SupportRelicResolver.Resolve(relicOneId, relicTwoId);

            AssertModifiers(
                modifiers,
                expectedMaximumHealthBonus,
                expectedMaximumEnergyBonus,
                expectedSkillCooldownReductionPercent,
                expectedSkillCostReductionPercent,
                expectedMovementSpeedBonusPercent,
                expectedConsumableHealingBonusPercent,
                expectedHarmfulStatusDurationReductionPercent,
                expectedPurchasePriceReductionPercent);
        }

        [Test]
        public void AddSumsMatchingPercentageCategoriesBeforeTheyAreApplied()
        {
            SupportRelicModifiers oneSource = SupportRelicResolver.Resolve(SupportRelicIds.ApprenticeGear, null);
            SupportRelicModifiers anotherSource = SupportRelicResolver.Resolve(SupportRelicIds.ApprenticeGear, null);

            SupportRelicModifiers stacked = oneSource.Add(anotherSource);

            Assert.That(stacked.SkillCooldownReductionPercent, Is.EqualTo(20));
            Assert.That(stacked.SkillCostReductionPercent, Is.Zero);
            Assert.That(stacked.MovementSpeedBonusPercent, Is.Zero);
            Assert.That(stacked.ConsumableHealingBonusPercent, Is.Zero);
            Assert.That(stacked.HarmfulStatusDurationReductionPercent, Is.Zero);
            Assert.That(stacked.PurchasePriceReductionPercent, Is.Zero);
        }

        [Test]
        public void EmptySlotsAreAcceptedAndRemovingOneSlotRemovesItsModifiers()
        {
            AssertModifiers(SupportRelicResolver.Resolve(null, null), 0, 0, 0, 0, 0, 0, 0, 0);
            AssertModifiers(
                SupportRelicResolver.Resolve(null, SupportRelicIds.PaleBone),
                15,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
            AssertModifiers(
                SupportRelicResolver.Resolve(SupportRelicIds.PaleBone, null),
                15,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        [TestCase("relic.unknown", null, "relicOneId")]
        [TestCase(null, "relic.unknown", "relicTwoId")]
        [TestCase("relic.marked_coin", null, "relicOneId")]
        [TestCase(null, "relic.hidden_pouch", "relicTwoId")]
        public void UnknownAndOtherFamilyRelicsAreRejected(
            string relicOneId,
            string relicTwoId,
            string parameterName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => SupportRelicResolver.Resolve(relicOneId, relicTwoId));

            Assert.That(exception.ParamName, Is.EqualTo(parameterName));
        }

        [TestCase(SupportRelicIds.PaleBone)]
        [TestCase(SupportRelicIds.ArcaneFlask)]
        [TestCase(SupportRelicIds.ApprenticeGear)]
        [TestCase(SupportRelicIds.AlvariLeaf)]
        [TestCase(SupportRelicIds.RouteCord)]
        [TestCase(SupportRelicIds.BlessedVial)]
        [TestCase(SupportRelicIds.SigiledBark)]
        [TestCase(SupportRelicIds.MerchantSeal)]
        public void TheSameRelicCannotOccupyBothSlots(string relicId)
        {
            Assert.Throws<ArgumentException>(() => SupportRelicResolver.Resolve(relicId, relicId));
        }

        [Test]
        public void SupportCatalogContainsExactlyTheEightPlannedIds()
        {
            string[] ids =
            {
                SupportRelicIds.PaleBone,
                SupportRelicIds.ArcaneFlask,
                SupportRelicIds.ApprenticeGear,
                SupportRelicIds.AlvariLeaf,
                SupportRelicIds.RouteCord,
                SupportRelicIds.BlessedVial,
                SupportRelicIds.SigiledBark,
                SupportRelicIds.MerchantSeal
            };

            Assert.That(ids.Length, Is.EqualTo(8));
            foreach (string id in ids)
                Assert.That(SupportRelicIds.IsSupported(id), Is.True, id);

            Assert.That(SupportRelicIds.IsSupported(null), Is.False);
            Assert.That(SupportRelicIds.IsSupported("relic.marked_coin"), Is.False);
            Assert.That(SupportRelicIds.IsSupported("relic.hidden_pouch"), Is.False);
        }

        [Test]
        public void MaximumResourceRelicsDoNotRefillCurrentResources()
        {
            int currentHealth = 27;
            int currentEnergy = 3;
            SupportRelicModifiers modifiers = SupportRelicResolver.Resolve(
                SupportRelicIds.PaleBone,
                SupportRelicIds.ArcaneFlask);

            int maximumHealth = modifiers.AddToMaximumHealth(50);
            int maximumEnergy = modifiers.AddToMaximumEnergy(10);

            Assert.That(maximumHealth, Is.EqualTo(65));
            Assert.That(maximumEnergy, Is.EqualTo(20));
            Assert.That(currentHealth, Is.EqualTo(27));
            Assert.That(currentEnergy, Is.EqualTo(3));
        }

        [Test]
        public void MaximumResourceBonusesRespectTheirInputAndOverflowLimits()
        {
            SupportRelicModifiers modifiers = SupportRelicResolver.Resolve(
                SupportRelicIds.PaleBone,
                SupportRelicIds.ArcaneFlask);

            Assert.Throws<ArgumentOutOfRangeException>(() => modifiers.AddToMaximumHealth(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => modifiers.AddToMaximumEnergy(-1));
            Assert.Throws<OverflowException>(() => modifiers.AddToMaximumHealth(int.MaxValue));
            Assert.Throws<OverflowException>(() => modifiers.AddToMaximumEnergy(int.MaxValue));
        }

        private static void AssertModifiers(
            SupportRelicModifiers modifiers,
            int expectedMaximumHealthBonus,
            int expectedMaximumEnergyBonus,
            int expectedSkillCooldownReductionPercent,
            int expectedSkillCostReductionPercent,
            int expectedMovementSpeedBonusPercent,
            int expectedConsumableHealingBonusPercent,
            int expectedHarmfulStatusDurationReductionPercent,
            int expectedPurchasePriceReductionPercent)
        {
            Assert.That(modifiers.MaximumHealthBonus, Is.EqualTo(expectedMaximumHealthBonus));
            Assert.That(modifiers.MaximumEnergyBonus, Is.EqualTo(expectedMaximumEnergyBonus));
            Assert.That(modifiers.SkillCooldownReductionPercent, Is.EqualTo(expectedSkillCooldownReductionPercent));
            Assert.That(modifiers.SkillCostReductionPercent, Is.EqualTo(expectedSkillCostReductionPercent));
            Assert.That(modifiers.MovementSpeedBonusPercent, Is.EqualTo(expectedMovementSpeedBonusPercent));
            Assert.That(modifiers.ConsumableHealingBonusPercent, Is.EqualTo(expectedConsumableHealingBonusPercent));
            Assert.That(
                modifiers.HarmfulStatusDurationReductionPercent,
                Is.EqualTo(expectedHarmfulStatusDurationReductionPercent));
            Assert.That(modifiers.PurchasePriceReductionPercent, Is.EqualTo(expectedPurchasePriceReductionPercent));
        }
    }
}
