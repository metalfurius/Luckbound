using Luckbound.Content;
using Luckbound.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SkillCatalogContentTests
    {
        [Test]
        public void SkillsJsonLoadsAllNineClassAbilitiesAndTheirTimingData()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(source, Is.Not.Null, "Skills.json must be available as a Resources TextAsset.");

            SkillCatalog catalog = SkillCatalogJsonLoader.Load(source.text);

            Assert.That(catalog.Count, Is.EqualTo(9));
            Assert.That(catalog.Get("skill.bellator.circular_cut").BaseDamage, Is.EqualTo(24m));
            Assert.That(catalog.Get("skill.bellator.guard").DamageReductionPercent, Is.EqualTo(0.6m));
            Assert.That(catalog.Get("skill.bellator.guard").EffectDurationTicks, Is.EqualTo(90));
            Assert.That(catalog.Get("skill.bellator.charge").EffectDurationTicks, Is.EqualTo(21));

            SkillDefinition piercingArrow = catalog.Get("skill.venator.piercing_arrow");
            Assert.That(piercingArrow.MaxTargets, Is.EqualTo(3));
            Assert.That(piercingArrow.ProjectileSpeed, Is.EqualTo(18m));
            Assert.That(piercingArrow.ProjectileLifetimeTicks, Is.EqualTo(120));

            SkillDefinition tripleShot = catalog.Get("skill.venator.triple_shot");
            Assert.That(tripleShot.ProjectileCount, Is.EqualTo(3));
            Assert.That(tripleShot.AngleStepDegrees, Is.EqualTo(12m));
            Assert.That(catalog.Get("skill.venator.root_trap").EffectDurationTicks, Is.EqualTo(480));

            SkillDefinition fireball = catalog.Get("skill.magus.fireball");
            Assert.That(fireball.AreaRadius, Is.EqualTo(1.5m));
            Assert.That(fireball.StatusKind, Is.EqualTo(SkillStatusKind.Burn));
            Assert.That(fireball.StatusPower, Is.EqualTo(3m));
            Assert.That(fireball.StatusDurationTicks, Is.EqualTo(180));
            Assert.That(catalog.Get("skill.magus.frost_nova").StatusDurationTicks, Is.EqualTo(60));
            Assert.That(catalog.Get("skill.magus.arcane_seal").DamagePerSecond, Is.EqualTo(8m));
        }

        [Test]
        public void SkillDataLoaderRejectsWrongTickRateAndUnknownEffects()
        {
            const string wrongRate = "{\"schemaVersion\":1,\"tickRate\":50,\"skills\":[]}";
            const string unknownEffect = "{\"schemaVersion\":1,\"tickRate\":60,\"skills\":[{" +
                "\"id\":\"skill.bellator.bad\",\"classId\":\"class.bellator\",\"energyCost\":1," +
                "\"cooldownTicks\":30,\"preparationTicks\":9,\"effectKind\":\"Unknown\"}]}";

            Assert.That(() => SkillCatalogJsonLoader.Load(wrongRate), Throws.TypeOf<System.FormatException>());
            Assert.That(() => SkillCatalogJsonLoader.Load(unknownEffect), Throws.TypeOf<System.FormatException>());
        }
    }
}
