using System;
using Luckbound.Core.Combat;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class DamageResolverTests
    {
        [Test]
        public void ResolveAppliesRarityUpgradeOffenseDefenseAndReductionInOrder()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 3);
            var request = new DamageCalculationRequest(
                baseDamage: 12m,
                rarityMultiplier: 1.15m,
                upgradeMultiplier: 1.10m,
                offensiveBonusPercent: 0.20m,
                defensePenetration: 1,
                incomingDamageReductionPercent: 0.25m,
                targetStats: target,
                targetIsInvulnerable: false);

            DamageResolution result = DamageResolver.Resolve(request);

            Assert.That(result.Damage, Is.EqualTo(12));
            Assert.That(result.WasPreventedByInvulnerability, Is.False);
        }

        [Test]
        public void HighDefenseReducesDamageToMinimumInsteadOfHealingTarget()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 100);
            var request = CreateRequest(target, baseDamage: 10m);

            DamageResolution result = DamageResolver.Resolve(request);

            Assert.That(result.Damage, Is.EqualTo(1));
            Assert.That(result.WasPreventedByInvulnerability, Is.False);
        }

        [Test]
        public void DefensePenetrationCannotMakeEffectiveDefenseNegative()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 5);
            var request = CreateRequest(target, baseDamage: 10m, defensePenetration: 20);

            Assert.That(DamageResolver.Resolve(request).Damage, Is.EqualTo(10));
        }

        [Test]
        public void TemporaryDefenseBonusStacksBeforePenetration()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 2);
            var request = CreateRequest(target, baseDamage: 12m, defensePenetration: 1, targetDefenseBonus: 4);

            Assert.That(DamageResolver.Resolve(request).Damage, Is.EqualTo(7));
        }

        [Test]
        public void TemporaryDefenseBonusCannotBeNegative()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 2);

            Assert.That(() => CreateRequest(target, targetDefenseBonus: -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ReductionIsClampedAndDamageStillHasMinimumOfOne()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 0);
            var request = CreateRequest(target, baseDamage: 10m, incomingDamageReductionPercent: 1.25m);

            Assert.That(DamageResolver.Resolve(request).Damage, Is.EqualTo(1));
        }

        [Test]
        public void MidpointRoundsAwayFromZero()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 0);
            var request = CreateRequest(target, baseDamage: 1.5m);

            Assert.That(DamageResolver.Resolve(request).Damage, Is.EqualTo(2));
        }

        [Test]
        public void InvulnerabilityPreventsImpactAndReturnsNoDamage()
        {
            var target = new CombatStats(maximumHealth: 100, maximumEnergy: 0, defense: 0);
            var request = CreateRequest(target, baseDamage: 50m, targetIsInvulnerable: true);

            DamageResolution result = DamageResolver.Resolve(request);

            Assert.That(result.Damage, Is.Zero);
            Assert.That(result.WasPreventedByInvulnerability, Is.True);
        }

        [Test]
        public void CombatStatsRejectInvalidMaximumsAndDefense()
        {
            Assert.That(() => new CombatStats(0, 0, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CombatStats(100, -1, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new CombatStats(100, 50, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static DamageCalculationRequest CreateRequest(
            CombatStats target,
            decimal baseDamage = 10m,
            decimal rarityMultiplier = 1m,
            decimal upgradeMultiplier = 1m,
            decimal offensiveBonusPercent = 0m,
            int defensePenetration = 0,
            decimal incomingDamageReductionPercent = 0m,
            bool targetIsInvulnerable = false,
            int targetDefenseBonus = 0)
        {
            return new DamageCalculationRequest(
                baseDamage,
                rarityMultiplier,
                upgradeMultiplier,
                offensiveBonusPercent,
                defensePenetration,
                incomingDamageReductionPercent,
                target,
                targetIsInvulnerable,
                targetDefenseBonus);
        }
    }
}
