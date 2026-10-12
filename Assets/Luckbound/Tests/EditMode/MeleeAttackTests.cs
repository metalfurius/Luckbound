using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class MeleeAttackTests
    {
        [Test]
        public void BellatorSwordUsesConfiguredWindowAndAttackInterval()
        {
            var state = CreateAttackState();

            MeleeAttackSnapshot started = state.Tick(0, attackPressed: true, dodgeAccepted: false);
            MeleeAttackSnapshot beforeWindow = state.Tick(5, attackPressed: false, dodgeAccepted: false);
            MeleeAttackSnapshot firstActiveTick = state.Tick(6, attackPressed: false, dodgeAccepted: false);
            MeleeAttackSnapshot lastActiveTick = state.Tick(11, attackPressed: false, dodgeAccepted: false);
            MeleeAttackSnapshot firstRecoveryTick = state.Tick(12, attackPressed: false, dodgeAccepted: false);
            MeleeAttackSnapshot lastIntervalTick = state.Tick(26, attackPressed: false, dodgeAccepted: false);
            MeleeAttackSnapshot nextAttack = state.Tick(27, attackPressed: true, dodgeAccepted: false);

            Assert.That(started.WasStartedThisTick, Is.True);
            Assert.That(started.IsAttacking, Is.True);
            Assert.That(started.IsDamageWindowOpen, Is.False);
            Assert.That(started.MovementSpeedMultiplier, Is.EqualTo(0.65m));
            Assert.That(beforeWindow.IsDamageWindowOpen, Is.False);
            Assert.That(firstActiveTick.IsDamageWindowOpen, Is.True);
            Assert.That(lastActiveTick.IsDamageWindowOpen, Is.True);
            Assert.That(firstActiveTick.HitWindow.Id, Is.EqualTo(lastActiveTick.HitWindow.Id));
            Assert.That(firstActiveTick.HitWindow.BaseDamage, Is.EqualTo(12m));
            Assert.That(firstActiveTick.HitWindow.DamageMultiplier, Is.EqualTo(1m));
            Assert.That(firstRecoveryTick.IsDamageWindowOpen, Is.False);
            Assert.That(firstRecoveryTick.IsAttacking, Is.True);
            Assert.That(lastIntervalTick.IsAttacking, Is.True);
            Assert.That(nextAttack.WasStartedThisTick, Is.True);
            Assert.That(nextAttack.DamageMultiplier, Is.EqualTo(1m));
        }

        [Test]
        public void HitWindowAllowsEachActorOnceAndNewWindowCanHitItAgain()
        {
            var state = CreateAttackState();
            var firstTarget = new ActorId(Guid.Parse("10000000-0000-4000-8000-000000000001"));
            var secondTarget = new ActorId(Guid.Parse("20000000-0000-4000-8000-000000000002"));

            MeleeHitWindow firstWindow = state.Tick(0, true, false).HitWindow;
            Assert.That(firstWindow, Is.Null);
            firstWindow = state.Tick(6, false, false).HitWindow;
            HitId firstHitId = firstWindow.Id;

            Assert.That(firstWindow.TryRegisterTarget(firstTarget), Is.True);
            Assert.That(firstWindow.TryRegisterTarget(firstTarget), Is.False, "A second collider on the same actor must be ignored.");
            Assert.That(firstWindow.TryRegisterTarget(secondTarget), Is.True);
            Assert.That(firstWindow.TryRegisterTarget(default), Is.False);
            Assert.That(firstWindow.RegisteredTargetCount, Is.EqualTo(2));

            state.Tick(27, true, false);
            MeleeHitWindow secondWindow = state.Tick(33, false, false).HitWindow;

            Assert.That(secondWindow.Id, Is.Not.EqualTo(firstHitId));
            Assert.That(secondWindow.TryRegisterTarget(firstTarget), Is.True);
        }

        [Test]
        public void BellatorComboAdvancesOnAcceptedAttacksAndResetsAfterOneSecond()
        {
            var combo = MeleeComboSequence.CreateBellator();

            Assert.That(combo.AcceptAttack(0), Is.EqualTo(1m));
            Assert.That(combo.AcceptAttack(27), Is.EqualTo(1m));
            Assert.That(combo.AcceptAttack(54), Is.EqualTo(1.25m));
            Assert.That(combo.AcceptAttack(114), Is.EqualTo(1m));
        }

        [Test]
        public void RejectedAttackDuringCooldownDoesNotAdvanceCombo()
        {
            var state = CreateAttackState();

            MeleeAttackSnapshot first = state.Tick(0, true, false);
            MeleeAttackSnapshot rejected = state.Tick(26, true, false);
            MeleeAttackSnapshot second = state.Tick(27, true, false);

            Assert.That(first.DamageMultiplier, Is.EqualTo(1m));
            Assert.That(rejected.WasStartedThisTick, Is.False);
            Assert.That(second.WasStartedThisTick, Is.True);
            Assert.That(second.DamageMultiplier, Is.EqualTo(1m));
        }

        [Test]
        public void AcceptedDodgeCancelsAttackButLeavesWeaponCooldownAndComboStep()
        {
            var state = CreateAttackState();

            state.Tick(0, true, false);
            MeleeAttackSnapshot interrupted = state.Tick(12, false, true);
            MeleeAttackSnapshot stillCoolingDown = state.Tick(26, true, false);
            MeleeAttackSnapshot nextAttack = state.Tick(27, true, false);

            Assert.That(interrupted.IsAttacking, Is.False);
            Assert.That(interrupted.IsDamageWindowOpen, Is.False);
            Assert.That(stillCoolingDown.WasStartedThisTick, Is.False);
            Assert.That(nextAttack.WasStartedThisTick, Is.True);
            Assert.That(nextAttack.DamageMultiplier, Is.EqualTo(1m));
        }

        [Test]
        public void AttackAndDodgeOnSameTickAcceptsOnlyDodge()
        {
            var state = CreateAttackState();

            MeleeAttackSnapshot result = state.Tick(0, attackPressed: true, dodgeAccepted: true);

            Assert.That(result.WasStartedThisTick, Is.False);
            Assert.That(result.IsAttacking, Is.False);
            Assert.That(result.IsDamageWindowOpen, Is.False);
        }

        [Test]
        public void HitIdentifiersAreValidAndDistinctFromActorIdentifiers()
        {
            HitId first = HitId.New();
            HitId second = HitId.New();

            Assert.That(default(HitId).IsValid, Is.False);
            Assert.That(first.IsValid, Is.True);
            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first.ToString(), Has.Length.EqualTo(32));
        }

        private static MeleeAttackState CreateAttackState()
        {
            return new MeleeAttackState(MeleeAttackProfile.BellatorWornSword, MeleeComboSequence.CreateBellator());
        }
    }
}
