using Luckbound.Core.Armor;
using NUnit.Framework;

namespace Luckbound.Tests.Armor
{
    public sealed class ArmorReplacementResolverTests
    {
        [Test]
        public void ReplacingHighBonusArmorWithLowerMaximumCapsEnergyWithoutChangingHealth()
        {
            ArmorProfile highBonusArmor = new ArmorProfile("armor.test.high", 2, maximumEnergyBonus: 5);
            ArmorProfile noBonusArmor = new ArmorProfile("armor.test.low", 2);
            ArmorResolution current = ArmorStatsResolver.Resolve(
                highBonusArmor, 0, baseMaximumHealth: 100, baseMaximumEnergy: 50,
                currentHealth: 62, currentEnergy: 53);

            ArmorResolution replacement = ArmorReplacementResolver.Resolve(
                current, noBonusArmor, nextUpgradeLevel: 0, currentHealth: 62, currentEnergy: 53);

            Assert.That(replacement.MaximumHealth, Is.EqualTo(100));
            Assert.That(replacement.MaximumEnergy, Is.EqualTo(50));
            Assert.That(replacement.HealthAfterMaximumClamp, Is.EqualTo(62));
            Assert.That(replacement.EnergyAfterMaximumClamp, Is.EqualTo(50));
        }

        [Test]
        public void ReplacingLowBonusArmorWithHigherMaximumDoesNotHealEnergy()
        {
            ArmorProfile noBonusArmor = new ArmorProfile("armor.test.low", 2);
            ArmorProfile highBonusArmor = new ArmorProfile("armor.test.high", 2, maximumEnergyBonus: 5);
            ArmorResolution current = ArmorStatsResolver.Resolve(
                noBonusArmor, 0, baseMaximumHealth: 100, baseMaximumEnergy: 50,
                currentHealth: 62, currentEnergy: 31);

            ArmorResolution replacement = ArmorReplacementResolver.Resolve(
                current, highBonusArmor, nextUpgradeLevel: 0, currentHealth: 62, currentEnergy: 31);

            Assert.That(replacement.MaximumEnergy, Is.EqualTo(55));
            Assert.That(replacement.HealthAfterMaximumClamp, Is.EqualTo(62));
            Assert.That(replacement.EnergyAfterMaximumClamp, Is.EqualTo(31));
        }

        [Test]
        public void ReapplyingSameArmorDoesNotAccumulateItsMaximumEnergyBonus()
        {
            ArmorProfile highBonusArmor = new ArmorProfile("armor.test.high", 2, maximumEnergyBonus: 5);
            ArmorResolution current = ArmorStatsResolver.Resolve(
                highBonusArmor, 0, baseMaximumHealth: 100, baseMaximumEnergy: 50,
                currentHealth: 62, currentEnergy: 53);

            ArmorResolution firstReplacement = ArmorReplacementResolver.Resolve(
                current, highBonusArmor, nextUpgradeLevel: 0, currentHealth: 62, currentEnergy: 53);
            ArmorResolution secondReplacement = ArmorReplacementResolver.Resolve(
                firstReplacement, highBonusArmor, nextUpgradeLevel: 0,
                currentHealth: firstReplacement.HealthAfterMaximumClamp,
                currentEnergy: firstReplacement.EnergyAfterMaximumClamp);

            Assert.That(firstReplacement.MaximumEnergy, Is.EqualTo(55));
            Assert.That(secondReplacement.MaximumEnergy, Is.EqualTo(55));
            Assert.That(secondReplacement.HealthAfterMaximumClamp, Is.EqualTo(62));
            Assert.That(secondReplacement.EnergyAfterMaximumClamp, Is.EqualTo(53));
        }
    }
}
