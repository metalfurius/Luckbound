using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class MagusCombatStateTests
    {
        [Test]
        public void LoadoutRequiresExactlyTwoDistinctMagusSkillsAndMapsSlotsInSelectionOrder()
        {
            SkillCatalog catalog = CreateCatalog();
            Assert.That(() => new MagusSkillLoadout(catalog, Array.Empty<string>()), Throws.ArgumentException);
            Assert.That(() => new MagusSkillLoadout(catalog, new[] { MagusSkillLoadout.FireballId }), Throws.ArgumentException);
            Assert.That(() => new MagusSkillLoadout(catalog, new[]
            {
                MagusSkillLoadout.FireballId,
                MagusSkillLoadout.FrostNovaId,
                MagusSkillLoadout.ArcaneSealId
            }), Throws.ArgumentException);
            Assert.That(() => new MagusSkillLoadout(catalog, new[]
            {
                MagusSkillLoadout.FireballId,
                MagusSkillLoadout.FireballId
            }), Throws.ArgumentException);

            var loadout = new MagusSkillLoadout(catalog, new[]
            {
                MagusSkillLoadout.ArcaneSealId,
                MagusSkillLoadout.FireballId
            });

            Assert.That(loadout.Skill1.Id, Is.EqualTo(MagusSkillLoadout.ArcaneSealId));
            Assert.That(loadout.Skill2.Id, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(loadout.Contains(MagusSkillLoadout.FrostNovaId), Is.False);
            Assert.That(() => loadout.GetSkill(PlayerAction.Attack), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void BasicAttackUsesEmberStaffDamageAndTimingWithoutSpendingEnergy()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new MagusCombatState(
                actor,
                CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId),
                energy);

            MagusCombatResult first = state.Tick(Command(actor, 0, PlayerAction.Attack), isAlive: true);
            MagusCombatResult earlyRetry = state.Tick(Command(actor, 1, PlayerAction.Attack), isAlive: true);
            MagusCombatResult beforeReady = state.Tick(Command(actor, 29, PlayerAction.Attack), isAlive: true);
            MagusCombatResult ready = state.Tick(Command(actor, 30, PlayerAction.Attack), isAlive: true);

            Assert.That(first.BasicAttack.WasStartedThisTick, Is.True);
            Assert.That(first.BasicAttack.BaseDamage, Is.EqualTo(12m));
            Assert.That(first.BasicAttack.ProjectileElement, Is.EqualTo("fire"));
            Assert.That(first.BasicAttack.MovementSpeedMultiplier, Is.EqualTo(0.65m));
            Assert.That(earlyRetry.BasicAttack.WasStartedThisTick, Is.False);
            Assert.That(beforeReady.BasicAttack.WasStartedThisTick, Is.False);
            Assert.That(ready.BasicAttack.WasStartedThisTick, Is.True);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(50));
        }

        [Test]
        public void FireballUsesCatalogValuesAndSpendsEnergyWhenItsEffectStarts()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new MagusCombatState(actor, CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.FrostNovaId), energy);

            MagusCombatResult start = state.Tick(Command(actor, 0, PlayerAction.Skill1, PlayerAction.Attack), isAlive: true);
            Assert.That(start.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(start.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(start.BasicAttack.WasStartedThisTick, Is.False);
            for (long tick = 1; tick <= 8; tick++) state.Tick(Command(actor, tick), isAlive: true);

            MagusCombatResult activation = state.Tick(Command(actor, 9), isAlive: true);

            Assert.That(activation.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(8));
            Assert.That(activation.SkillOutcome.ReadyAtTick, Is.EqualTo(249));
            Assert.That(activation.SkillActivation.SkillId, Is.EqualTo(MagusSkillLoadout.FireballId));
            Assert.That(activation.SkillActivation.Fireball.Projectile.BaseDamage, Is.EqualTo(24m));
            Assert.That(activation.SkillActivation.Fireball.Projectile.SpeedUnitsPerSecond, Is.EqualTo(10m));
            Assert.That(activation.SkillActivation.Fireball.Projectile.LifetimeTicks, Is.EqualTo(180));
            Assert.That(activation.SkillActivation.Fireball.Projectile.MaximumTargets, Is.EqualTo(1));
            Assert.That(activation.SkillActivation.Fireball.ExplosionRadius, Is.EqualTo(1.5m));
            Assert.That(activation.SkillActivation.Fireball.BurnDamagePerSecond, Is.EqualTo(3m));
            Assert.That(activation.SkillActivation.Fireball.BurnDurationTicks, Is.EqualTo(180));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(42));
        }

        [Test]
        public void FrostNovaAndArcaneSealCarryTheirFixedAreaAndStatusValues()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new MagusCombatState(actor, CreateLoadout(MagusSkillLoadout.FrostNovaId, MagusSkillLoadout.ArcaneSealId), energy);

            MagusCombatResult frost = Cast(state, actor, tick: 0, slot: PlayerAction.Skill1);

            Assert.That(frost.SkillOutcome.EnergySpent, Is.EqualTo(12));
            Assert.That(frost.SkillOutcome.ReadyAtTick, Is.EqualTo(369));
            Assert.That(frost.SkillActivation.FrostNova.BaseDamage, Is.EqualTo(18m));
            Assert.That(frost.SkillActivation.FrostNova.Radius, Is.EqualTo(2.5m));
            Assert.That(frost.SkillActivation.FrostNova.FreezeDurationTicks, Is.EqualTo(60));
            Assert.That(frost.SkillActivation.FrostNova.FreezeProtectionTicks, Is.EqualTo(120));

            MagusCombatResult seal = Cast(state, actor, tick: 10, slot: PlayerAction.Skill2);

            Assert.That(seal.SkillOutcome.EnergySpent, Is.EqualTo(16));
            Assert.That(seal.SkillOutcome.ReadyAtTick, Is.EqualTo(499));
            Assert.That(seal.SkillActivation.ArcaneSeal.Radius, Is.EqualTo(2.5m));
            Assert.That(seal.SkillActivation.ArcaneSeal.DurationTicks, Is.EqualTo(240));
            Assert.That(seal.SkillActivation.ArcaneSeal.DamagePerSecond, Is.EqualTo(8m));
            Assert.That(seal.SkillActivation.ArcaneSeal.DamagePulseIntervalTicks, Is.EqualTo(60));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(22));
        }

        [Test]
        public void InsufficientEnergyAndIndependentCooldownsDoNotChargeAnotherSkill()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 7);
            var state = new MagusCombatState(actor, CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.ArcaneSealId), energy);

            MagusCombatResult poor = state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);
            Assert.That(poor.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(poor.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(7));

            energy.SetCurrentEnergy(50);
            Cast(state, actor, tick: 1, slot: PlayerAction.Skill1);
            MagusCombatResult otherSkill = state.Tick(Command(actor, 11, PlayerAction.Skill2), isAlive: true);
            Assert.That(otherSkill.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
            for (long tick = 12; tick <= 19; tick++) state.Tick(Command(actor, tick), isAlive: true);
            MagusCombatResult otherActivated = state.Tick(Command(actor, 20), isAlive: true);
            MagusCombatResult firstStillCooling = state.Tick(Command(actor, 21, PlayerAction.Skill1), isAlive: true);

            Assert.That(otherActivated.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(otherActivated.SkillOutcome.EnergySpent, Is.EqualTo(16));
            Assert.That(firstStillCooling.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
            Assert.That(firstStillCooling.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(26));
        }

        [Test]
        public void EnergyLossDuringPreparationRejectsAtActivationWithoutStartingCooldown()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new MagusCombatState(actor, CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.ArcaneSealId), energy);
            state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);
            energy.SetCurrentEnergy(0);

            MagusCombatResult result = default;
            for (long tick = 1; tick <= 9; tick++) result = state.Tick(Command(actor, tick), isAlive: true);

            Assert.That(result.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(result.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(result.SkillActivation, Is.Null);
            Assert.That(state.SkillUses.IsReady(MagusSkillLoadout.FireballId, 9), Is.True);
            Assert.That(energy.CurrentEnergy, Is.Zero);
        }

        [Test]
        public void DodgeDeathAndInterruptCancelPreparationWithoutCost()
        {
            AssertCancelledWithoutCost(PlayerAction.Dodge, isAlive: true, externalInterrupt: false);
            AssertCancelledWithoutCost(PlayerAction.None, isAlive: true, externalInterrupt: true);
            AssertCancelledWithoutCost(PlayerAction.None, isAlive: false, externalInterrupt: false);
        }

        [Test]
        public void AttackCanCancelPreparationAndStartTheFreeBasicAttack()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new MagusCombatState(actor, CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.ArcaneSealId), energy);
            state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);

            MagusCombatResult cancelled = state.Tick(Command(actor, 1, PlayerAction.Attack), isAlive: true);

            Assert.That(cancelled.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(cancelled.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(cancelled.BasicAttack.WasStartedThisTick, Is.True);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(50));
        }

        [Test]
        public void CommandsMustBelongToTheActorAndAdvanceOncePerTick()
        {
            ActorId actor = ActorId.New();
            var state = new MagusCombatState(
                actor,
                CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.ArcaneSealId),
                new SkillUseState(maximumEnergy: 50, initialEnergy: 50));
            state.Tick(Command(actor, 0), isAlive: true);

            Assert.That(() => state.Tick(Command(ActorId.New(), 1), isAlive: true), Throws.ArgumentException);
            Assert.That(() => state.Tick(Command(actor, 0), isAlive: true), Throws.InvalidOperationException);
        }

        private static void AssertCancelledWithoutCost(PlayerAction interruptingAction, bool isAlive, bool externalInterrupt)
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new MagusCombatState(actor, CreateLoadout(MagusSkillLoadout.FireballId, MagusSkillLoadout.ArcaneSealId), energy);
            state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);

            MagusCombatResult result = state.Tick(Command(actor, 1, interruptingAction), isAlive, externalInterrupt);

            Assert.That(result.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(result.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(result.BasicAttack.WasStartedThisTick, Is.False);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(50));
        }

        private static MagusCombatResult Cast(MagusCombatState state, ActorId actor, long tick, PlayerAction slot)
        {
            state.Tick(Command(actor, tick, slot), isAlive: true);
            for (long current = tick + 1; current < tick + 9; current++)
            {
                state.Tick(Command(actor, current), isAlive: true);
            }

            return state.Tick(Command(actor, tick + 9), isAlive: true);
        }

        private static MagusSkillLoadout CreateLoadout(string first, string second)
        {
            return new MagusSkillLoadout(CreateCatalog(), new[] { first, second });
        }

        private static PlayerCommand Command(ActorId actor, long tick, params PlayerAction[] actions)
        {
            var buffer = new PlayerInputBuffer(actor);
            foreach (PlayerAction action in actions)
            {
                if (action != PlayerAction.None) buffer.Press(action);
            }

            return buffer.Consume(tick);
        }

        private static SkillCatalog CreateCatalog()
        {
            return new SkillCatalog(new[]
            {
                new SkillDefinition(
                    MagusSkillLoadout.FireballId,
                    MagusSkillLoadout.ClassId,
                    energyCost: 8,
                    cooldownTicks: 240,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.ExplosiveProjectile,
                    baseDamage: 24m,
                    areaRadius: 1.5m,
                    maxTargets: 1,
                    projectileCount: 1,
                    projectileSpeed: 10m,
                    projectileLifetimeTicks: 180,
                    statusKind: SkillStatusKind.Burn,
                    statusPower: 3m,
                    statusDurationTicks: 180),
                new SkillDefinition(
                    MagusSkillLoadout.FrostNovaId,
                    MagusSkillLoadout.ClassId,
                    energyCost: 12,
                    cooldownTicks: 360,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.FrostNova,
                    baseDamage: 18m,
                    areaRadius: 2.5m,
                    statusKind: SkillStatusKind.Freeze,
                    statusDurationTicks: 60),
                new SkillDefinition(
                    MagusSkillLoadout.ArcaneSealId,
                    MagusSkillLoadout.ClassId,
                    energyCost: 16,
                    cooldownTicks: 480,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.DamageZone,
                    areaRadius: 2.5m,
                    effectDurationTicks: 240,
                    damagePerSecond: 8m,
                    damagePulseIntervalTicks: 60)
            });
        }
    }
}
