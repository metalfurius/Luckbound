using System;
using Luckbound.Core.Combat;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class CustodianBossDefinitionTests
    {
        [Test]
        public void Definition_ExposesTheD06CombatContractWithoutUnityDependencies()
        {
            var definition = new CustodianBossDefinition(
                "boss.custodian_of_the_oaks", 900, 3, 45, 120, 90, 54, 14, 12, 16, 2);

            Assert.That(definition.BossId, Is.EqualTo("boss.custodian_of_the_oaks"));
            Assert.That(definition.MaximumHealth, Is.EqualTo(900));
            Assert.That(definition.Defense, Is.EqualTo(3));
            Assert.That(definition.TelegraphTicks, Is.GreaterThanOrEqualTo(33));
            Assert.That(definition.PhaseTransitionTicks, Is.EqualTo(120));
            Assert.That(definition.PhaseOneRecoveryTicks, Is.EqualTo(90));
            Assert.That(definition.PhaseTwoRecoveryTicks, Is.LessThan(definition.PhaseOneRecoveryTicks));
            Assert.That(definition.CutDamage, Is.EqualTo(14));
            Assert.That(definition.WaveDamage, Is.EqualTo(12));
            Assert.That(definition.ChargeDamage, Is.EqualTo(16));
            Assert.That(definition.PhaseTwoWaveCount, Is.EqualTo(2));
        }

        [Test]
        public void Definition_RejectsWarningsShorterThanThirtyThreeTicks()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CustodianBossDefinition(
                "boss.custodian_of_the_oaks", 900, 3, 32, 120, 90, 54, 14, 12, 16, 2));
        }

        [Test]
        public void Definition_RejectsARecoveryThatDoesNotAccelerateInPhaseTwo()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CustodianBossDefinition(
                "boss.custodian_of_the_oaks", 900, 3, 45, 120, 54, 54, 14, 12, 16, 2));
        }
    }
}
