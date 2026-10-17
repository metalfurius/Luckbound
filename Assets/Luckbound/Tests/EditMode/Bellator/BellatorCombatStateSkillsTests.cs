using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Bellator
{
    public sealed class BellatorCombatStateSkillsTests
    {
        [Test]
        public void LoadoutAcceptsEachTwoSkillKitAndRejectsInvalidSelections()
        {
            SkillCatalog catalog = CreateCatalog();
            string[][] kits =
            {
                new[] { BellatorSkillLoadout.CircularCutId, BellatorSkillLoadout.GuardId },
                new[] { BellatorSkillLoadout.CircularCutId, BellatorSkillLoadout.ChargeId },
                new[] { BellatorSkillLoadout.GuardId, BellatorSkillLoadout.ChargeId }
            };

            foreach (string[] kit in kits)
            {
                var loadout = new BellatorSkillLoadout(catalog, kit);
                Assert.That(loadout.Skill1.Id, Is.EqualTo(kit[0]));
                Assert.That(loadout.Skill2.Id, Is.EqualTo(kit[1]));
                Assert.That(loadout.SelectedSkillIds, Is.EqualTo(kit));
            }

            Assert.That(() => new BellatorSkillLoadout(catalog, Array.Empty<string>()), Throws.ArgumentException);
            Assert.That(() => new BellatorSkillLoadout(catalog, new[] { BellatorSkillLoadout.GuardId }), Throws.ArgumentException);
            Assert.That(() => new BellatorSkillLoadout(catalog, new[]
            {
                BellatorSkillLoadout.GuardId,
                BellatorSkillLoadout.GuardId
            }), Throws.ArgumentException);
            Assert.That(() => new BellatorSkillLoadout(catalog, new[]
            {
                BellatorSkillLoadout.GuardId,
                "skill.venator.piercing_arrow"
            }), Throws.ArgumentException);
        }

        [Test]
        public void CircularCutAndGuardActivateAfterNineTicksWithIndependentCatalogCostsAndCooldowns()
        {
            ActorId actor = ActorId.New();
            var energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new BellatorCombatState(
                actor,
                CreateLoadout(BellatorSkillLoadout.CircularCutId, BellatorSkillLoadout.GuardId),
                energy);

            BellatorCombatResult circularCut = Cast(state, actor, tick: 0, PlayerAction.Skill1);
            Assert.That(circularCut.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(circularCut.SkillOutcome.EnergySpent, Is.EqualTo(8));
            Assert.That(circularCut.SkillOutcome.ReadyAtTick, Is.EqualTo(249));
            Assert.That(circularCut.ActivatedSkill.Id, Is.EqualTo(BellatorSkillLoadout.CircularCutId));
            Assert.That(circularCut.ActivatedSkill.BaseDamage, Is.EqualTo(24m));
            Assert.That(circularCut.ActivatedSkill.AreaRadius, Is.EqualTo(2m));
            Assert.That(circularCut.ActivatedCharge, Is.Null);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(42));

            BellatorCombatResult guard = Cast(state, actor, tick: 10, PlayerAction.Skill2);
            Assert.That(guard.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(guard.SkillOutcome.EnergySpent, Is.EqualTo(12));
            Assert.That(guard.SkillOutcome.ReadyAtTick, Is.EqualTo(379));
            Assert.That(guard.ActivatedSkill.Id, Is.EqualTo(BellatorSkillLoadout.GuardId));
            Assert.That(guard.ActivatedSkill.DamageReductionPercent, Is.EqualTo(0.6m));
            Assert.That(guard.ActivatedSkill.EffectDurationTicks, Is.EqualTo(90));
            Assert.That(guard.ActivatedCharge, Is.Null);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(30));
        }

        [Test]
        public void InterruptedGuardPreparationAndEnergyLossBeforeActivationDoNotSpendOrStartCooldown()
        {
            ActorId interruptedActor = ActorId.New();
            var interruptedEnergy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var interruptedState = new BellatorCombatState(
                interruptedActor,
                CreateLoadout(BellatorSkillLoadout.CircularCutId, BellatorSkillLoadout.GuardId),
                interruptedEnergy);

            BellatorCombatResult starting = interruptedState.Tick(
                Command(interruptedActor, 0, PlayerAction.Skill2), isAlive: true);
            BellatorCombatResult interrupted = interruptedState.Tick(
                Command(interruptedActor, 5, PlayerAction.Dodge), isAlive: true);

            Assert.That(starting.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(interrupted.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(interrupted.ActivatedSkill, Is.Null);
            Assert.That(interruptedEnergy.CurrentEnergy, Is.EqualTo(50));
            Assert.That(interruptedEnergy.IsReady(BellatorSkillLoadout.GuardId, 5), Is.True);

            ActorId drainedActor = ActorId.New();
            var drainedEnergy = new SkillUseState(maximumEnergy: 50, initialEnergy: 12);
            var drainedState = new BellatorCombatState(
                drainedActor,
                CreateLoadout(BellatorSkillLoadout.CircularCutId, BellatorSkillLoadout.GuardId),
                drainedEnergy);
            drainedState.Tick(Command(drainedActor, 0, PlayerAction.Skill2), isAlive: true);
            drainedEnergy.SetCurrentEnergy(11);
            BellatorCombatResult rejected = default;
            for (long tick = 1; tick <= 9; tick++)
            {
                rejected = drainedState.Tick(Command(drainedActor, tick), isAlive: true);
            }

            Assert.That(rejected.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(rejected.SkillOutcome.EnergySpent, Is.Zero);
            Assert.That(rejected.ActivatedSkill, Is.Null);
            Assert.That(drainedEnergy.CurrentEnergy, Is.EqualTo(11));
            Assert.That(drainedEnergy.IsReady(BellatorSkillLoadout.GuardId, 9), Is.True);
        }

        [Test]
        public void ChargeStillActivatesFromTheSelectedLoadoutWithItsExistingTimingAndCost()
        {
            ActorId actor = ActorId.New();
            var energy = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            var state = new BellatorCombatState(
                actor,
                CreateLoadout(BellatorSkillLoadout.GuardId, BellatorSkillLoadout.ChargeId),
                energy);

            BellatorCombatResult charge = Cast(state, actor, tick: 0, PlayerAction.Skill2);

            Assert.That(charge.SkillOutcome.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(charge.SkillOutcome.EnergySpent, Is.EqualTo(16));
            Assert.That(charge.SkillOutcome.ReadyAtTick, Is.EqualTo(489));
            Assert.That(charge.ActivatedSkill.Id, Is.EqualTo(BellatorSkillLoadout.ChargeId));
            Assert.That(charge.ActivatedCharge, Is.SameAs(charge.ActivatedSkill));
            Assert.That(charge.ActivatedCharge.EffectDurationTicks, Is.EqualTo(21));
            Assert.That(charge.ActivatedCharge.ChargeSpeed, Is.EqualTo(12m));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(34));
        }

        private static BellatorCombatResult Cast(
            BellatorCombatState state,
            ActorId actor,
            long tick,
            PlayerAction slot)
        {
            state.Tick(Command(actor, tick, slot), isAlive: true);
            BellatorCombatResult result = default;
            for (long current = tick + 1; current <= tick + 9; current++)
            {
                result = state.Tick(Command(actor, current), isAlive: true);
            }

            return result;
        }

        private static BellatorSkillLoadout CreateLoadout(string first, string second)
        {
            return new BellatorSkillLoadout(CreateCatalog(), new[] { first, second });
        }

        private static PlayerCommand Command(ActorId actor, long tick, params PlayerAction[] actions)
        {
            var input = new PlayerInputBuffer(actor);
            foreach (PlayerAction action in actions)
            {
                if (action != PlayerAction.None) input.Press(action);
            }

            return input.Consume(tick);
        }

        private static SkillCatalog CreateCatalog()
        {
            return new SkillCatalog(new[]
            {
                new SkillDefinition(
                    BellatorSkillLoadout.CircularCutId,
                    BellatorSkillLoadout.ClassId,
                    energyCost: 8,
                    cooldownTicks: 240,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.AreaStrike,
                    baseDamage: 24m,
                    areaRadius: 2m),
                new SkillDefinition(
                    BellatorSkillLoadout.GuardId,
                    BellatorSkillLoadout.ClassId,
                    energyCost: 12,
                    cooldownTicks: 360,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.Guard,
                    effectDurationTicks: 90,
                    damageReductionPercent: 0.6m),
                new SkillDefinition(
                    BellatorSkillLoadout.ChargeId,
                    BellatorSkillLoadout.ClassId,
                    energyCost: 16,
                    cooldownTicks: 480,
                    preparationTicks: 9,
                    effectKind: SkillEffectKind.Charge,
                    baseDamage: 30m,
                    effectDurationTicks: 21,
                    knockbackTier: SkillKnockbackTier.Strong,
                    chargeSpeed: 12m,
                    knockbackHorizontalSpeed: 6m,
                    knockbackVerticalSpeed: 3m,
                    targetAiControlSuspensionTicks: 18)
            });
        }
    }
}
