using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Pacts;
using Luckbound.Core.Relics;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class SupportRelicSkillUseTests
    {
        [Test]
        public void CombinedCostUsesAddedPercentagesThenAlvariHalfUpAtEffectStart()
        {
            PactModifierTotals pacts = Accept(PactIds.Fervor);
            SupportRelicModifiers support = SupportRelicResolver.Resolve(null, SupportRelicIds.AlvariLeaf);
            var state = new SkillUseState(50, 50, pacts, PlayableRaceRules.AlvariRaceId);
            state.ReplaceCommittedModifiers(pacts, support);
            SkillDefinition skill = CreateSkill("skill.bellator.combined_cost", 12, 240);

            SkillUseOutcome started = state.Update(0, skill, actionValid: true, interrupted: false);
            SkillUseOutcome activated = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(started.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(started.EnergySpent, Is.Zero);
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(13));
            Assert.That(state.CurrentEnergy, Is.EqualTo(37));
        }

        [Test]
        public void SkillCostUsesCeilingAndPreservesOnePointMinimum()
        {
            SupportRelicModifiers support = SupportRelicResolver.Resolve(null, SupportRelicIds.AlvariLeaf);
            var state = new SkillUseState(50, 50);
            state.ReplaceCommittedModifiers(default(PactModifierTotals), support);
            SkillDefinition roundedSkill = CreateSkill("skill.bellator.rounded_cost", 11, 240);
            SkillDefinition minimumSkill = CreateSkill("skill.bellator.minimum_cost", 1, 240);

            state.Update(0, roundedSkill, actionValid: true, interrupted: false);
            SkillUseOutcome rounded = state.Update(9, null, actionValid: true, interrupted: false);
            state.Update(10, minimumSkill, actionValid: true, interrupted: false);
            SkillUseOutcome minimum = state.Update(19, null, actionValid: true, interrupted: false);

            Assert.That(rounded.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(rounded.EnergySpent, Is.EqualTo(10));
            Assert.That(minimum.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(minimum.EnergySpent, Is.EqualTo(1));
            Assert.That(state.CurrentEnergy, Is.EqualTo(39));
        }

        [Test]
        public void CombinedCooldownUsesCeilingAndThirtyTickMinimum()
        {
            PactModifierTotals pacts = Accept(PactIds.Silence);
            SupportRelicModifiers support = SupportRelicResolver.Resolve(null, SupportRelicIds.ApprenticeGear);
            var state = new SkillUseState(50, 50, pacts);
            state.ReplaceCommittedModifiers(pacts, support);
            SkillDefinition exactSkill = CreateSkill("skill.bellator.exact_cooldown", 1, 240);
            SkillDefinition roundedSkill = CreateSkill("skill.bellator.rounded_cooldown", 1, 241);
            SkillDefinition minimumSkill = CreateSkill("skill.bellator.minimum_cooldown", 1, 30);

            state.Update(0, exactSkill, actionValid: true, interrupted: false);
            SkillUseOutcome exact = state.Update(9, null, actionValid: true, interrupted: false);
            state.Update(10, roundedSkill, actionValid: true, interrupted: false);
            SkillUseOutcome rounded = state.Update(19, null, actionValid: true, interrupted: false);
            state.Update(20, minimumSkill, actionValid: true, interrupted: false);
            SkillUseOutcome minimum = state.Update(29, null, actionValid: true, interrupted: false);

            Assert.That(exact.ReadyAtTick, Is.EqualTo(177));
            Assert.That(rounded.ReadyAtTick, Is.EqualTo(188));
            Assert.That(minimum.ReadyAtTick, Is.EqualTo(59));
            Assert.That(state.IsReady(exactSkill.Id, 176), Is.False);
            Assert.That(state.IsReady(exactSkill.Id, 177), Is.True);
            Assert.That(state.IsReady(roundedSkill.Id, 187), Is.False);
            Assert.That(state.IsReady(roundedSkill.Id, 188), Is.True);
            Assert.That(state.IsReady(minimumSkill.Id, 58), Is.False);
            Assert.That(state.IsReady(minimumSkill.Id, 59), Is.True);
        }

        [Test]
        public void CooldownsRemainIndependentPerSkillWithCombinedModifiers()
        {
            PactModifierTotals pacts = Accept(PactIds.Silence);
            SupportRelicModifiers support = SupportRelicResolver.Resolve(null, SupportRelicIds.ApprenticeGear);
            var state = new SkillUseState(50, 50, pacts);
            state.ReplaceCommittedModifiers(pacts, support);
            SkillDefinition shortSkill = CreateSkill("skill.bellator.short_relic_cooldown", 1, 240);
            SkillDefinition longSkill = CreateSkill("skill.bellator.long_relic_cooldown", 1, 360);

            state.Update(0, shortSkill, actionValid: true, interrupted: false);
            SkillUseOutcome shortUse = state.Update(9, null, actionValid: true, interrupted: false);
            state.Update(10, longSkill, actionValid: true, interrupted: false);
            SkillUseOutcome longUse = state.Update(19, null, actionValid: true, interrupted: false);
            SkillUseOutcome shortCoolingDown = state.Update(20, shortSkill, actionValid: true, interrupted: false);

            Assert.That(shortUse.ReadyAtTick, Is.EqualTo(177));
            Assert.That(longUse.ReadyAtTick, Is.EqualTo(271));
            Assert.That(shortCoolingDown.Status, Is.EqualTo(SkillUseStatus.RejectedCooldown));
            Assert.That(state.IsReady(shortSkill.Id, 177), Is.True);
            Assert.That(state.IsReady(longSkill.Id, 270), Is.False);
            Assert.That(state.IsReady(longSkill.Id, 271), Is.True);
        }

        [Test]
        public void ReplacementsDuringPreparationApplyAtEffectWithoutRefillOrClockReset()
        {
            PactModifierTotals pacts = Accept(PactIds.Fervor, PactIds.Silence);
            SupportRelicModifiers support = SupportRelicResolver.Resolve(
                SupportRelicIds.AlvariLeaf,
                SupportRelicIds.ApprenticeGear);
            var state = new SkillUseState(50, 50);
            SkillDefinition skill = CreateSkill("skill.bellator.replace_during_prep", 12, 240);

            SkillUseOutcome started = state.Update(100, skill, actionValid: true, interrupted: false);
            state.ReplaceCommittedModifiers(pacts, support);
            state.ReplaceCommittedModifiers(pacts, support);
            SkillUseOutcome stillPreparing = state.Update(108, null, actionValid: true, interrupted: false);
            SkillUseOutcome activated = state.Update(109, null, actionValid: true, interrupted: false);

            Assert.That(started.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(state.CurrentEnergy, Is.EqualTo(36));
            Assert.That(stillPreparing.Status, Is.EqualTo(SkillUseStatus.Preparing));
            Assert.That(activated.Status, Is.EqualTo(SkillUseStatus.Activated));
            Assert.That(activated.EnergySpent, Is.EqualTo(14));
            Assert.That(activated.ReadyAtTick, Is.EqualTo(277));
            Assert.That(state.IsPreparing, Is.False);
            Assert.Throws<InvalidOperationException>(() => state.Update(109, null, actionValid: true, interrupted: false));

            state.ReplaceCommittedModifiers(default(PactModifierTotals), default(SupportRelicModifiers));
            state.ReplaceCommittedModifiers(default(PactModifierTotals), default(SupportRelicModifiers));

            Assert.That(state.CurrentEnergy, Is.EqualTo(36));
            Assert.That(state.IsReady(skill.Id, 276), Is.False);
            Assert.That(state.IsReady(skill.Id, 277), Is.True);
            Assert.That(state.Update(110, null, actionValid: true, interrupted: false).Status, Is.EqualTo(SkillUseStatus.Idle));
        }

        [Test]
        public void InterruptedPreparationRemainsFreeAfterModifierReplacement()
        {
            var state = new SkillUseState(50, 20);
            SkillDefinition skill = CreateSkill("skill.bellator.cancel_relic_prep", 12, 240);
            state.Update(0, skill, actionValid: true, interrupted: false);
            state.ReplaceCommittedModifiers(Accept(PactIds.Fervor), default(SupportRelicModifiers));

            SkillUseOutcome cancelled = state.Update(5, null, actionValid: true, interrupted: true);

            Assert.That(cancelled.Status, Is.EqualTo(SkillUseStatus.Cancelled));
            Assert.That(cancelled.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(20));
            Assert.That(state.IsPreparing, Is.False);
            Assert.That(state.IsReady(skill.Id, 5), Is.True);
        }

        [Test]
        public void EffectStartRevalidatesEnergyAgainstLatestCommittedModifiers()
        {
            var state = new SkillUseState(50, 12);
            SkillDefinition skill = CreateSkill("skill.bellator.revalidate_relic_cost", 12, 240);
            state.Update(0, skill, actionValid: true, interrupted: false);
            state.ReplaceCommittedModifiers(Accept(PactIds.Fervor), default(SupportRelicModifiers));

            SkillUseOutcome result = state.Update(9, null, actionValid: true, interrupted: false);

            Assert.That(result.Status, Is.EqualTo(SkillUseStatus.RejectedInsufficientEnergy));
            Assert.That(result.EnergySpent, Is.Zero);
            Assert.That(state.CurrentEnergy, Is.EqualTo(12));
            Assert.That(state.IsPreparing, Is.False);
            Assert.That(state.IsReady(skill.Id, 9), Is.True);
        }

        private static PactModifierTotals Accept(params string[] pactIds)
        {
            var pacts = new PactState(baseMaximumHealth: 100, currentHealth: 100);
            foreach (string pactId in pactIds)
            {
                Assert.That(pacts.Accept(pactId).Status, Is.EqualTo(PactOperationStatus.Accepted));
            }

            return pacts.Modifiers;
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
