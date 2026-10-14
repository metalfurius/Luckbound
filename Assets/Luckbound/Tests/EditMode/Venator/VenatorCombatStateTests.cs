using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class VenatorCombatStateTests
    {
        [Test]
        public void LoadoutRequiresExactlyTwoDistinctVenatorSkillsAndMapsSlotsInSelectionOrder()
        {
            SkillCatalog catalog = CreateCatalog();
            Assert.That(() => new VenatorSkillLoadout(catalog, Array.Empty<string>()), Throws.ArgumentException);
            Assert.That(() => new VenatorSkillLoadout(catalog, new[] { VenatorSkillLoadout.PiercingArrowId }), Throws.ArgumentException);
            Assert.That(() => new VenatorSkillLoadout(catalog, new[]
            {
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.TripleShotId,
                VenatorSkillLoadout.RootTrapId
            }), Throws.ArgumentException);
            Assert.That(() => new VenatorSkillLoadout(catalog, new[]
            {
                VenatorSkillLoadout.PiercingArrowId,
                VenatorSkillLoadout.PiercingArrowId
            }), Throws.ArgumentException);

            var loadout = new VenatorSkillLoadout(catalog, new[]
            {
                VenatorSkillLoadout.RootTrapId,
                VenatorSkillLoadout.PiercingArrowId
            });

            Assert.That(loadout.Skill1.Id, Is.EqualTo(VenatorSkillLoadout.RootTrapId));
            Assert.That(loadout.Skill2.Id, Is.EqualTo(VenatorSkillLoadout.PiercingArrowId));
            Assert.That(loadout.Contains(VenatorSkillLoadout.TripleShotId), Is.False);
            Assert.That(() => loadout.GetSkill(PlayerAction.Attack), Throws.ArgumentOutOfRangeException);
        }

        [Test]
        public void BasicAttackUsesShortbowValuesAndDoesNotSpendEnergy()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId), energy);

            VenatorCombatResult first = state.Tick(Command(actor, 0, PlayerAction.Attack), isAlive: true);
            VenatorCombatResult earlyRetry = state.Tick(Command(actor, 1, PlayerAction.Attack), isAlive: true);
            VenatorCombatResult beforeReady = state.Tick(Command(actor, 23, PlayerAction.Attack), isAlive: true);
            VenatorCombatResult ready = state.Tick(Command(actor, 24, PlayerAction.Attack), isAlive: true);

            Assert.That(first.BasicAttack.WasStartedThisTick, Is.True);
            Assert.That(first.BasicAttack.Projectile.BaseDamage, Is.EqualTo(10m));
            Assert.That(first.BasicAttack.Projectile.SpeedUnitsPerSecond, Is.EqualTo(18m));
            Assert.That(first.BasicAttack.Projectile.LifetimeTicks, Is.EqualTo(120));
            Assert.That(first.BasicAttack.Projectile.MaximumTargets, Is.EqualTo(1));
            Assert.That(first.BasicAttack.MovementSpeedMultiplier, Is.EqualTo(0.65m));
            Assert.That(earlyRetry.BasicAttack.WasStartedThisTick, Is.False);
            Assert.That(beforeReady.BasicAttack.WasStartedThisTick, Is.False);
            Assert.That(ready.BasicAttack.WasStartedThisTick, Is.True);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(50));
        }

        [Test]
        public void PiercingArrowPreparesForNineTicksThenSpendsEnergyAndBuildsThreeTargetProjectile()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId), energy);

            VenatorCombatResult start = state.Tick(Command(actor, 0, PlayerAction.Skill1, PlayerAction.Attack), isAlive: true);
            Assert.That(start.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(start.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(start.BasicAttack.WasStartedThisTick, Is.False, "Skill priority suppresses a simultaneous basic attack.");

            VenatorCombatResult tickEight = default;
            for (long tick = 1; tick <= 8; tick++)
            {
                tickEight = state.Tick(Command(actor, tick), isAlive: true);
            }

            VenatorCombatResult activation = state.Tick(Command(actor, 9), isAlive: true);

            Assert.That(tickEight.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(activation.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(8));
            Assert.That(activation.SkillOutcome.ReadyAtTick, Is.EqualTo(249));
            Assert.That(activation.SkillActivation.SkillId, Is.EqualTo(VenatorSkillLoadout.PiercingArrowId));
            Assert.That(activation.SkillActivation.Projectiles, Has.Count.EqualTo(1));
            Assert.That(activation.SkillActivation.Projectiles[0].Definition.BaseDamage, Is.EqualTo(24m));
            Assert.That(activation.SkillActivation.Projectiles[0].Definition.MaximumTargets, Is.EqualTo(3));
            Assert.That(activation.SkillActivation.Projectiles[0].AngleOffsetDegrees, Is.Zero);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(42));
        }

        [Test]
        public void TripleShotProducesThreeIndependentSingleTargetArrowsAtTheSpecifiedAngles()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.RootTrapId, VenatorSkillLoadout.TripleShotId), energy);

            VenatorCombatResult activation = Cast(state, actor, tick: 0, slot: PlayerAction.Skill2);

            Assert.That(activation.SkillActivation.SkillId, Is.EqualTo(VenatorSkillLoadout.TripleShotId));
            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(12));
            Assert.That(activation.SkillOutcome.ReadyAtTick, Is.EqualTo(369));
            Assert.That(activation.SkillActivation.Projectiles, Has.Count.EqualTo(3));
            Assert.That(activation.SkillActivation.Projectiles[0].AngleOffsetDegrees, Is.EqualTo(-12m));
            Assert.That(activation.SkillActivation.Projectiles[1].AngleOffsetDegrees, Is.Zero);
            Assert.That(activation.SkillActivation.Projectiles[2].AngleOffsetDegrees, Is.EqualTo(12m));
            foreach (VenatorProjectileShot shot in activation.SkillActivation.Projectiles)
            {
                Assert.That(shot.Definition.BaseDamage, Is.EqualTo(8m));
                Assert.That(shot.Definition.MaximumTargets, Is.EqualTo(1));
                Assert.That(shot.Definition.SpeedUnitsPerSecond, Is.EqualTo(18m));
                Assert.That(shot.Definition.LifetimeTicks, Is.EqualTo(120));
            }

            Assert.That(energy.CurrentEnergy, Is.EqualTo(38));
        }

        [Test]
        public void RootTrapActivationCarriesOnlyTheSpecifiedDamageAndDurations()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.RootTrapId), energy);

            VenatorCombatResult activation = Cast(state, actor, tick: 0, slot: PlayerAction.Skill2);

            Assert.That(activation.SkillOutcome.EnergySpent, Is.EqualTo(16));
            Assert.That(activation.SkillOutcome.ReadyAtTick, Is.EqualTo(489));
            Assert.That(activation.SkillActivation.Projectiles, Is.Empty);
            Assert.That(activation.SkillActivation.RootTrap.BaseDamage, Is.EqualTo(16m));
            Assert.That(activation.SkillActivation.RootTrap.LifetimeTicks, Is.EqualTo(480));
            Assert.That(activation.SkillActivation.RootTrap.RootDurationTicks, Is.EqualTo(75));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(34));
        }

        [Test]
        public void InsufficientEnergyCooldownAndIndependentSkillCooldownsDoNotCrossCharge()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 7);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId), energy);

            VenatorCombatResult poor = state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);
            Assert.That(poor.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(poor.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(7));

            energy.SetCurrentEnergy(50);
            Cast(state, actor, tick: 1, slot: PlayerAction.Skill1);
            VenatorCombatResult otherSkill = state.Tick(Command(actor, 11, PlayerAction.Skill2), isAlive: true);
            Assert.That(otherSkill.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
            for (long tick = 12; tick <= 19; tick++) state.Tick(Command(actor, tick), isAlive: true);
            VenatorCombatResult otherActivated = state.Tick(Command(actor, 20), isAlive: true);
            VenatorCombatResult firstStillCooling = state.Tick(Command(actor, 21, PlayerAction.Skill1), isAlive: true);

            Assert.That(otherActivated.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(firstStillCooling.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
            Assert.That(firstStillCooling.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(30));
        }

        [Test]
        public void DodgeDamageAndDeathCancelPreparationWithoutCostOrBasicAttack()
        {
            AssertCancelledWithoutCost(PlayerAction.Dodge, isAlive: true, externalInterrupt: false);
            AssertCancelledWithoutCost(PlayerAction.None, isAlive: true, externalInterrupt: true);
            AssertCancelledWithoutCost(PlayerAction.None, isAlive: false, externalInterrupt: false);
        }

        [Test]
        public void AttackCanCancelPreparationAndExecuteTheFreeBasicAttack()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId), energy);
            state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);

            VenatorCombatResult cancelled = state.Tick(Command(actor, 1, PlayerAction.Attack), isAlive: true);

            Assert.That(cancelled.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(cancelled.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(cancelled.BasicAttack.WasStartedThisTick, Is.True);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(50));
        }

        [Test]
        public void CommandsMustBelongToTheActorAndAdvanceOneTickAtATime()
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId), energy);
            state.Tick(Command(actor, 0), isAlive: true);

            Assert.That(() => state.Tick(Command(ActorId.New(), 1), isAlive: true), Throws.ArgumentException);
            Assert.That(() => state.Tick(Command(actor, 0), isAlive: true), Throws.InvalidOperationException);
        }

        private static void AssertCancelledWithoutCost(PlayerAction interruptingAction, bool isAlive, bool externalInterrupt)
        {
            ActorId actor = ActorId.New();
            SkillUseState energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new VenatorCombatState(actor, CreateLoadout(VenatorSkillLoadout.PiercingArrowId, VenatorSkillLoadout.TripleShotId), energy);
            state.Tick(Command(actor, 0, PlayerAction.Skill1), isAlive: true);

            VenatorCombatResult result = state.Tick(Command(actor, 1, interruptingAction), isAlive, externalInterrupt);

            Assert.That(result.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(result.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(result.BasicAttack.WasStartedThisTick, Is.False);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(50));
        }

        private static VenatorCombatResult Cast(VenatorCombatState state, ActorId actor, long tick, PlayerAction slot)
        {
            state.Tick(Command(actor, tick, slot), isAlive: true);
            VenatorCombatResult result = default;
            for (long current = tick + 1; current < tick + 9; current++)
            {
                result = state.Tick(Command(actor, current), isAlive: true);
            }

            return state.Tick(Command(actor, tick + 9), isAlive: true);
        }

        private static VenatorSkillLoadout CreateLoadout(string first, string second)
        {
            return new VenatorSkillLoadout(CreateCatalog(), new[] { first, second });
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
                    VenatorSkillLoadout.PiercingArrowId,
                    VenatorSkillLoadout.ClassId,
                    energyCost: 8,
                    cooldownTicks: 240,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.PiercingProjectile,
                    baseDamage: 24m,
                    maxTargets: 3,
                    projectileCount: 1,
                    projectileSpeed: 18m,
                    projectileLifetimeTicks: 120),
                new SkillDefinition(
                    VenatorSkillLoadout.TripleShotId,
                    VenatorSkillLoadout.ClassId,
                    energyCost: 12,
                    cooldownTicks: 360,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.ProjectileVolley,
                    baseDamage: 8m,
                    maxTargets: 1,
                    projectileCount: 3,
                    angleStepDegrees: 12m,
                    projectileSpeed: 18m,
                    projectileLifetimeTicks: 120),
                new SkillDefinition(
                    VenatorSkillLoadout.RootTrapId,
                    VenatorSkillLoadout.ClassId,
                    energyCost: 16,
                    cooldownTicks: 480,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.RootTrap,
                    baseDamage: 16m,
                    effectDurationTicks: 480,
                    statusKind: SkillStatusKind.Root,
                    statusDurationTicks: 75)
            });
        }
    }
}
