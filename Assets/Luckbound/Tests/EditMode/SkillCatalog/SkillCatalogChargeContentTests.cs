using System;
using Luckbound.Content;
using Luckbound.Core.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.Content
{
    public sealed class SkillCatalogChargeContentTests
    {
        private TextAsset _skillsJson;
        private SkillCatalog _catalog;

        [SetUp]
        public void SetUp()
        {
            _skillsJson = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            Assert.That(_skillsJson, Is.Not.Null, "Skills.json is the authoritative skill-value source.");
            _catalog = SkillCatalogJsonLoader.Load(_skillsJson.text);
        }

        [Test]
        public void BellatorChargeLoadsAllRuntimeValuesFromSkillsJson()
        {
            SkillDefinition charge = _catalog.Get("skill.bellator.charge");

            Assert.That(charge.EffectKind, Is.EqualTo(SkillEffectKind.Charge));
            Assert.That(charge.EffectDurationTicks, Is.EqualTo(21));
            Assert.That(charge.BaseDamage, Is.EqualTo(30m));
            Assert.That(charge.ChargeSpeed, Is.EqualTo(12m));
            Assert.That(charge.KnockbackHorizontalSpeed, Is.EqualTo(6m));
            Assert.That(charge.KnockbackVerticalSpeed, Is.EqualTo(3m));
            Assert.That(charge.TargetAiControlSuspensionTicks, Is.EqualTo(18));
        }

        [Test]
        public void NonChargeDefinitionsKeepTheirExistingZeroDefaults()
        {
            foreach (SkillDefinition skill in _catalog.Definitions)
            {
                if (skill.EffectKind == SkillEffectKind.Charge) continue;

                Assert.That(skill.ChargeSpeed, Is.Zero, skill.Id);
                Assert.That(skill.KnockbackHorizontalSpeed, Is.Zero, skill.Id);
                Assert.That(skill.KnockbackVerticalSpeed, Is.Zero, skill.Id);
                Assert.That(skill.TargetAiControlSuspensionTicks, Is.Zero, skill.Id);
            }
        }

        [TestCase("\"chargeSpeed\": 12", "\"chargeSpeed\": 0")]
        [TestCase("\"knockbackHorizontalSpeed\": 6", "\"knockbackHorizontalSpeed\": 0")]
        [TestCase("\"knockbackVerticalSpeed\": 3", "\"knockbackVerticalSpeed\": 0")]
        [TestCase("\"targetAiControlSuspensionTicks\": 18", "\"targetAiControlSuspensionTicks\": 0")]
        public void ChargeRejectsMissingRuntimeTuning(string expectedField, string zeroedField)
        {
            Assert.That(_skillsJson.text, Does.Contain(expectedField));
            string invalidContent = _skillsJson.text.Replace(expectedField, zeroedField);

            Assert.Throws<ArgumentException>(() => SkillCatalogJsonLoader.Load(invalidContent));
        }
    }
}
