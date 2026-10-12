using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ActorDamageStateTests
    {
        [Test]
        public void DirectHitProtectionRejectsRepeatedImpactsUntilItsExpiryTick()
        {
            var stats = new CombatStats(100, 50, 0);
            var state = new ActorDamageState(stats, directImpactProtectionTicks: 27);

            DirectImpactOutcome first = state.ReceiveDirectImpact(HitId.New(), Request(stats, 20m), 10);
            DirectImpactOutcome sameTickImpact = state.ReceiveDirectImpact(HitId.New(), Request(stats, 20m), 10);
            DirectImpactOutcome protectedHit = state.ReceiveDirectImpact(HitId.New(), Request(stats, 20m), 37);
            DirectImpactOutcome afterExpiry = state.ReceiveDirectImpact(HitId.New(), Request(stats, 20m), 38);

            Assert.That(first.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(first.CurrentHealth, Is.EqualTo(80));
            Assert.That(first.ProtectedUntilTick, Is.EqualTo(38));
            Assert.That(sameTickImpact.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(sameTickImpact.CurrentHealth, Is.EqualTo(60));
            Assert.That(protectedHit.Status, Is.EqualTo(DirectImpactStatus.PreventedByInvulnerability));
            Assert.That(protectedHit.DamageApplied, Is.Zero);
            Assert.That(protectedHit.CurrentHealth, Is.EqualTo(60));
            Assert.That(afterExpiry.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(afterExpiry.CurrentHealth, Is.EqualTo(40));
        }

        [Test]
        public void DuplicateHitIdCannotDamageTheSameActorTwice()
        {
            var stats = new CombatStats(100, 50, 0);
            var state = new ActorDamageState(stats, directImpactProtectionTicks: 0);
            HitId hitId = HitId.New();

            DirectImpactOutcome first = state.ReceiveDirectImpact(hitId, Request(stats, 10m), 0);
            DirectImpactOutcome duplicate = state.ReceiveDirectImpact(hitId, Request(stats, 10m), 1);

            Assert.That(first.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(DirectImpactStatus.DuplicateHit));
            Assert.That(duplicate.DamageApplied, Is.Zero);
            Assert.That(state.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void LethalImpactClampsHealthToZeroAndDeadActorsIgnoreLaterHits()
        {
            var stats = new CombatStats(25, 50, 0);
            var state = new ActorDamageState(stats, directImpactProtectionTicks: 27);

            DirectImpactOutcome lethal = state.ReceiveDirectImpact(HitId.New(), Request(stats, 100m), 0);
            DirectImpactOutcome afterDeath = state.ReceiveDirectImpact(HitId.New(), Request(stats, 10m), 1);

            Assert.That(lethal.Status, Is.EqualTo(DirectImpactStatus.Applied));
            Assert.That(lethal.DamageApplied, Is.EqualTo(100));
            Assert.That(lethal.CurrentHealth, Is.Zero);
            Assert.That(lethal.IsDead, Is.True);
            Assert.That(lethal.DeathOccurred, Is.True);
            Assert.That(afterDeath.Status, Is.EqualTo(DirectImpactStatus.IgnoredBecauseDead));
            Assert.That(afterDeath.CurrentHealth, Is.Zero);
            Assert.That(afterDeath.DeathOccurred, Is.False);
        }

        [Test]
        public void RequestInvulnerabilityPreventsDamageWithoutStartingPostHitProtection()
        {
            var stats = new CombatStats(100, 50, 0);
            var state = new ActorDamageState(stats, directImpactProtectionTicks: 27);

            DirectImpactOutcome result = state.ReceiveDirectImpact(
                HitId.New(),
                Request(stats, 10m, targetIsInvulnerable: true),
                5);

            Assert.That(result.Status, Is.EqualTo(DirectImpactStatus.PreventedByInvulnerability));
            Assert.That(result.CurrentHealth, Is.EqualTo(100));
            Assert.That(result.ProtectedUntilTick, Is.Zero);
        }

        private static DamageCalculationRequest Request(CombatStats stats, decimal damage, bool targetIsInvulnerable = false)
        {
            return new DamageCalculationRequest(
                baseDamage: damage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats: stats,
                targetIsInvulnerable: targetIsInvulnerable);
        }
    }
}
