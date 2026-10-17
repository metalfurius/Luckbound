using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Pacts;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class SkillUseStateTests
    {
        [Test]
        public void SkillCostsEnergyAndStartsItsCooldownWhenEffectBegins()
        {
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);

            SkillUseOutcome start = state.Update(0, CreateSkill("skill.bellator.cut", 8, 240), actionValid: true, interrupted: false);
            Assert.That(start.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(start.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(50));
            Assert.That(state.IsReady("skill.bellator.cut", 0), Is.True);

            for (long tick = 1; tick < 9; tick++)
            {
                SkillUseOutcome preparing = state.Update(tick, null, actionValid: true, interrupted: false);
                Assert.That(preparing.Status, Is.EqualTo(SkillUseStatus.Preparing));
                Assert.That(state.CurrentEnergy, Is.EqualTo(50));
            }

            SkillUseOutcome activated = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(8));
            Assert.That(activated.ReadyAtTick, Is.EqualTo(249));
            Assert.That(state.CurrentEnergy, Is.EqualTo(42));
            Assert.That(state.IsReady("skill.bellator.cut", 248), Is.False);
            Assert.That(state.IsReady("skill.bellator.cut", 249), Is.True);
        }

        [Test]
        public void InvalidAndUnaffordableRequestsDoNotConsumeEnergyOrCooldown()
        {
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 7);
            SkillDefinition skill = CreateSkill("skill.bellator.cut", 8, 240);

            SkillUseOutcome invalid = state.Update(0, skill, actionValid: false, interrupted: false);
            SkillUseOutcome unaffordable = state.Update(1, skill, actionValid: true, interrupted: false);

            Assert.That(invalid.Status, Is.EqualTo(SkillUseStatus.RejectedInvalid));
            Assert.That(unaffordable.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(state.CurrentEnergy, Is.EqualTo(7));
            Assert.That(state.IsReady(skill.Id, 1), Is.True);
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void FervorIncreasesSkillCostForAvailabilityAndEnergyConsumption()
        {
            var pacts = new PactState(baseMaximumHealth: 100, currentHealth: 100);
            Assert.That(pacts.Accept(PactIds.Fervor).Status, Is.EqualTo(PactOperationStatus.Accepted));
            SkillDefinition skill = CreateSkill("skill.bellator.cut", 8, 240);
            var insufficientState = new SkillUseState(maximumEnergy: 50, initialEnergy: 9, pactModifiers: pacts.Modifiers);

            SkillUseOutcome insufficient = insufficientState.Update(0, skill, actionValid: true, interrupted: false);

            Assert.That(insufficient.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(insufficientState.CurrentEnergy, Is.EqualTo(9));
            Assert.That(insufficientState.IsPreparing, Is.False);

            var activationState = new SkillUseState(maximumEnergy: 50, initialEnergy: 10, pactModifiers: pacts.Modifiers);
            SkillUseOutcome start = activationState.Update(0, skill, actionValid: true, interrupted: false);
            SkillUseOutcome activated = activationState.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(start.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(10));
            Assert.That(activationState.CurrentEnergy, Is.Zero);
        }

        [Test]
        public void FervorCostIsRecheckedWhenPreparedEffectBegins()
        {
            var pacts = new PactState(baseMaximumHealth: 100, currentHealth: 100);
            pacts.Accept(PactIds.Fervor);
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 10, pactModifiers: pacts.Modifiers);
            SkillDefinition skill = CreateSkill("skill.bellator.cut", 8, 240);
            state.Update(0, skill, actionValid: true, interrupted: false);
            state.SetCurrentEnergy(9);

            SkillUseOutcome result = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(result.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(result.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(9));
            Assert.That(state.IsReady(skill.Id, 9), Is.True);
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void PactModifiersAreOptionalAndExistingConstructorKeepsBaseEnergyCosts()
        {
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 8);
            SkillDefinition skill = CreateSkill("skill.bellator.cut", 8, 240);

            SkillUseOutcome start = state.Update(0, skill, actionValid: true, interrupted: false);
            SkillUseOutcome activated = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(start.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(8));
            Assert.That(state.CurrentEnergy, Is.Zero);
        }

        [TestCase(PlayableRaceRules.HumanRaceId, 8, 8)]
        [TestCase(PlayableRaceRules.HumanRaceId, 12, 12)]
        [TestCase(PlayableRaceRules.HumanRaceId, 16, 16)]
        [TestCase(PlayableRaceRules.OrciniRaceId, 8, 8)]
        [TestCase(PlayableRaceRules.OrciniRaceId, 12, 12)]
        [TestCase(PlayableRaceRules.OrciniRaceId, 16, 16)]
        [TestCase(PlayableRaceRules.AlvariRaceId, 8, 8)]
        [TestCase(PlayableRaceRules.AlvariRaceId, 12, 11)]
        [TestCase(PlayableRaceRules.AlvariRaceId, 16, 15)]
        [TestCase(PlayableRaceRules.DvergarRaceId, 8, 8)]
        [TestCase(PlayableRaceRules.DvergarRaceId, 12, 12)]
        [TestCase(PlayableRaceRules.DvergarRaceId, 16, 16)]
        public void ActivationConsumesTheRaceAdjustedSkillCostWithoutPacts(string raceId, int baseCost, int expectedCost)
        {
            var state = new SkillUseState(
                maximumEnergy: expectedCost,
                initialEnergy: expectedCost,
                pactModifiers: default(PactModifierTotals),
                raceId: raceId);
            SkillDefinition skill = CreateSkill("skill.bellator.race_cost", baseCost, 240);

            SkillUseOutcome started = state.Update(0, skill, actionValid: true, interrupted: false);
            SkillUseOutcome activated = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(started.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(expectedCost));
            Assert.That(state.CurrentEnergy, Is.Zero);
            Assert.That(state.RaceId, Is.EqualTo(raceId));
        }

        [TestCase(8, 10)]
        [TestCase(12, 14)]
        [TestCase(16, 19)]
        public void AlvariActivationAppliesFervorCostBeforeTheRaceDiscount(int baseCost, int expectedCost)
        {
            var pacts = new PactState(baseMaximumHealth: 100, currentHealth: 100);
            Assert.That(pacts.Accept(PactIds.Fervor).Status, Is.EqualTo(PactOperationStatus.Accepted));
            var state = new SkillUseState(
                maximumEnergy: expectedCost,
                initialEnergy: expectedCost,
                pactModifiers: pacts.Modifiers,
                raceId: PlayableRaceRules.AlvariRaceId);
            SkillDefinition skill = CreateSkill("skill.bellator.alvari_fervor", baseCost, 240);

            SkillUseOutcome started = state.Update(0, skill, actionValid: true, interrupted: false);
            SkillUseOutcome activated = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(started.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(expectedCost));
            Assert.That(state.CurrentEnergy, Is.Zero);
        }

        [Test]
        public void RaceIdDefaultsToHumanAndRejectsUnsupportedValues()
        {
            var defaultState = new SkillUseState(50, 50);
            var pactState = new SkillUseState(50, 50, default(PactModifierTotals));

            Assert.That(defaultState.RaceId, Is.EqualTo(PlayableRaceRules.HumanRaceId));
            Assert.That(pactState.RaceId, Is.EqualTo(PlayableRaceRules.HumanRaceId));
            Assert.Throws<ArgumentNullException>(() => new SkillUseState(50, 50, default(PactModifierTotals), raceId: null));
            Assert.Throws<ArgumentException>(() => new SkillUseState(50, 50, default(PactModifierTotals), raceId: "race.unknown"));
        }

        [Test]
        public void InterruptionBeforeEffectBeginsIsFree()
        {
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 20);
            SkillDefinition skill = CreateSkill("skill.bellator.guard", 12, 360);

            state.Update(0, skill, actionValid: true, interrupted: false);
            SkillUseOutcome interrupted = state.Update(5, null, actionValid: true, interrupted: true);

            Assert.That(interrupted.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(interrupted.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(20));
            Assert.That(state.IsReady(skill.Id, 5), Is.True);
            Assert.That(state.IsPreparing, Is.False);
        }

        [Test]
        public void EnergyIsRecheckedWhenPreparedEffectBegins()
        {
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 12);
            SkillDefinition skill = CreateSkill("skill.bellator.guard", 12, 360);
            state.Update(0, skill, actionValid: true, interrupted: false);
            state.SetCurrentEnergy(11);

            SkillUseOutcome result = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(result.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(result.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(11));
            Assert.That(state.IsReady(skill.Id, 9), Is.True);
        }

        [Test]
        public void CooldownsAreIndependentPerSkill()
        {
            var state = new SkillUseState(maximumEnergy: 50, initialEnergy: 50);
            SkillDefinition shortSkill = CreateSkill("skill.bellator.cut", 8, 240);
            SkillDefinition longSkill = CreateSkill("skill.bellator.guard", 12, 360);

            state.Update(0, shortSkill, actionValid: true, interrupted: false);
            state.Update(9, null, actionValid: true, interrupted: false);
            state.Update(10, longSkill, actionValid: true, interrupted: false);
            SkillUseOutcome secondActivated = state.Update(19, null, actionValid: true, interrupted: false);
            SkillUseOutcome coolingDown = state.Update(20, shortSkill, actionValid: true, interrupted: false);

            Assert.That(secondActivated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(secondActivated.EnergySpent, Is.EqualTo(12));
            Assert.That(coolingDown.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
            Assert.That(coolingDown.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(30));
            Assert.That(state.IsReady(shortSkill.Id, 19), Is.False);
            Assert.That(state.IsReady(longSkill.Id, 19), Is.False);
            Assert.That(state.IsReady(shortSkill.Id, 250), Is.True);
            Assert.That(state.IsReady(longSkill.Id, 250), Is.False);
        }

        [Test]
        public void SkillDefinitionsAndCatalogRejectInvalidOrDuplicateContent()
        {
            Assert.That(() => CreateSkill("skill.bellator.invalid", 0, 240), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SkillDefinition("skill.bellator.fast", "class.bellator", 1, 29, 9,
                SkillEffectKind.AreaStrike, baseDamage: 1m, areaRadius: 1m), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new SkillDefinition("skill.bellator.short_cast", "class.bellator", 1, 30, 8,
                SkillEffectKind.AreaStrike, baseDamage: 1m, areaRadius: 1m), Throws.TypeOf<ArgumentOutOfRangeException>());

            SkillDefinition skill = CreateSkill("skill.bellator.cut", 8, 240);
            Assert.That(() => new SkillCatalog(new[] { skill, skill }), Throws.ArgumentException);
        }

        private static SkillDefinition CreateSkill(string id, int energyCost, int cooldownTicks)
        {
            return new SkillDefinition(
                id,
                "class.bellator",
                energyCost,
                cooldownTicks,
                preparationTicks: 9,
                effectKind: SkillEffectKind.AreaStrike,
                baseDamage: 24m,
                areaRadius: 2m);
        }
    }
}
