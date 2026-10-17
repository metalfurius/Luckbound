using System;
using Luckbound.Core.Relics;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Relics
{
    public sealed class SupportRelicResolverTests
    {
        [TestCase(SupportRelicIds.PaleBone, 15, 0, 0)]
        [TestCase(SupportRelicIds.ArcaneFlask, 0, 10, 0)]
        [TestCase(SupportRelicIds.ApprenticeGear, 0, 0, 10)]
        public void EachSupportedRelicResolvesItsSpecifiedModifier(
            string relicId,
            int expectedMaximumHealthBonus,
            int expectedMaximumEnergyBonus,
            int expectedSkillCooldownReductionPercent)
        {
            SupportRelicModifiers modifiers = SupportRelicResolver.Resolve(relicId, null);

            AssertModifiers(
                modifiers,
                expectedMaximumHealthBonus,
                expectedMaximumEnergyBonus,
                expectedSkillCooldownReductionPercent);
        }

        [TestCase(SupportRelicIds.PaleBone, SupportRelicIds.ArcaneFlask, 15, 10, 0)]
        [TestCase(SupportRelicIds.PaleBone, SupportRelicIds.ApprenticeGear, 15, 0, 10)]
        [TestCase(SupportRelicIds.ArcaneFlask, SupportRelicIds.ApprenticeGear, 0, 10, 10)]
        public void TwoDistinctValidSlotsCombineOnlyTheirOwnEffects(
            string relicOneId,
            string relicTwoId,
            int expectedMaximumHealthBonus,
            int expectedMaximumEnergyBonus,
            int expectedSkillCooldownReductionPercent)
        {
            SupportRelicModifiers modifiers = SupportRelicResolver.Resolve(relicOneId, relicTwoId);

            AssertModifiers(
                modifiers,
                expectedMaximumHealthBonus,
                expectedMaximumEnergyBonus,
                expectedSkillCooldownReductionPercent);
        }

        [Test]
        public void EmptySlotsProduceNoModifiersAndEitherSlotCanBeEmpty()
        {
            AssertModifiers(SupportRelicResolver.Resolve(null, null), 0, 0, 0);
            AssertModifiers(SupportRelicResolver.Resolve(null, SupportRelicIds.PaleBone), 15, 0, 0);
        }

        [TestCase("relic.unknown", null, "relicOneId")]
        [TestCase(null, "relic.unknown", "relicTwoId")]
        public void UnknownRelicIdsAreRejected(string relicOneId, string relicTwoId, string parameterName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => SupportRelicResolver.Resolve(relicOneId, relicTwoId));

            Assert.That(exception.ParamName, Is.EqualTo(parameterName));
        }

        [Test]
        public void TheSameDefinitionCannotBeEquippedInBothSlots()
        {
            Assert.Throws<ArgumentException>(() =>
                SupportRelicResolver.Resolve(SupportRelicIds.PaleBone, SupportRelicIds.PaleBone));
        }

        [Test]
        public void MaximumResourceRelicsReturnBonusesWithoutRefillingCurrentResources()
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
            Assert.That(modifiers.MaximumHealthBonus, Is.EqualTo(15));
            Assert.That(modifiers.MaximumEnergyBonus, Is.EqualTo(10));
            Assert.That(currentHealth, Is.EqualTo(27));
            Assert.That(currentEnergy, Is.EqualTo(3));
        }

        private static void AssertModifiers(
            SupportRelicModifiers modifiers,
            int expectedMaximumHealthBonus,
            int expectedMaximumEnergyBonus,
            int expectedSkillCooldownReductionPercent)
        {
            Assert.That(modifiers.MaximumHealthBonus, Is.EqualTo(expectedMaximumHealthBonus));
            Assert.That(modifiers.MaximumEnergyBonus, Is.EqualTo(expectedMaximumEnergyBonus));
            Assert.That(modifiers.SkillCooldownReductionPercent, Is.EqualTo(expectedSkillCooldownReductionPercent));
        }
    }
}
