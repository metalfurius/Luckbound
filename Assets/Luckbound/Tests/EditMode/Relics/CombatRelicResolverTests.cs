using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Luckbound.Core.Relics;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Relics
{
    public sealed class CombatRelicResolverTests
    {
        [TestCase(CombatRelicIds.UmbraFang, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0)]
        [TestCase(CombatRelicIds.ShatteredClock, 0, -8, 0, 0, 0, 0, 0, 0, 0, 0)]
        [TestCase(CombatRelicIds.ArrowFeather, 0, 0, 20, 0, 0, 0, 0, 0, 0, 0)]
        [TestCase(CombatRelicIds.InlaidEmber, 0, 0, 0, 2, 3, 0, 0, 0, 0, 0)]
        [TestCase(CombatRelicIds.FirstEchoMedallion, 0, 0, 0, 0, 0, 25, 0, 0, 0, 0)]
        [TestCase(CombatRelicIds.BlackNeedle, 0, 0, 0, 0, 0, 0, 2, 0, 0, 0)]
        [TestCase(CombatRelicIds.HunterRing, 0, 0, 0, 0, 0, 0, 0, 2, 500, 0)]
        [TestCase(CombatRelicIds.OakMace, 0, 0, 0, 0, 0, 0, 0, 0, 0, 25)]
        public void EachCombatRelicResolvesItsExactPlannedValues(
            string relicId,
            int expectedBasicAttackDamageBonusPercent,
            int expectedBasicAttackIntervalModifierPercent,
            int expectedProjectileSpeedBonusPercent,
            int expectedBasicAttackBurnDamagePerSecond,
            int expectedBasicAttackBurnDurationSeconds,
            int expectedFirstImpactPerEnemyDamageBonusPercent,
            int expectedDefensePenetration,
            int expectedKillHealAmount,
            int expectedKillHealMinimumIntervalMilliseconds,
            int expectedKnockbackBonusPercent)
        {
            CombatRelicModifiers modifiers = CombatRelicResolver.Resolve(relicId, null);

            AssertModifiers(
                modifiers,
                expectedBasicAttackDamageBonusPercent,
                expectedBasicAttackIntervalModifierPercent,
                expectedProjectileSpeedBonusPercent,
                expectedBasicAttackBurnDamagePerSecond,
                expectedBasicAttackBurnDurationSeconds,
                expectedFirstImpactPerEnemyDamageBonusPercent,
                expectedDefensePenetration,
                expectedKillHealAmount,
                expectedKillHealMinimumIntervalMilliseconds,
                expectedKnockbackBonusPercent);
        }

        [Test]
        public void CatalogContainsExactlyTheEightPlannedCombatRelicIds()
        {
            string[] ids =
            {
                CombatRelicIds.UmbraFang,
                CombatRelicIds.ShatteredClock,
                CombatRelicIds.ArrowFeather,
                CombatRelicIds.InlaidEmber,
                CombatRelicIds.FirstEchoMedallion,
                CombatRelicIds.BlackNeedle,
                CombatRelicIds.HunterRing,
                CombatRelicIds.OakMace
            };

            Assert.That(ids.Length, Is.EqualTo(8));
            CollectionAssert.AllItemsAreUnique(ids);
            int declaredIdCount = 0;
            foreach (FieldInfo field in typeof(CombatRelicIds).GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                    declaredIdCount++;
            }
            Assert.That(declaredIdCount, Is.EqualTo(8));
            foreach (string id in ids)
                Assert.That(CombatRelicIds.IsSupported(id), Is.True, id);

            string itemsJson = File.ReadAllText("Assets/Resources/Luckbound/Data/Items/Items.json");
            string[] catalogCombatIds = Regex.Matches(itemsJson, "\"id\"\\s*:\\s*\"([^\"]+)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Where(CombatRelicIds.IsSupported)
                .ToArray();

            Assert.That(catalogCombatIds.Length, Is.EqualTo(8));
            CollectionAssert.AllItemsAreUnique(catalogCombatIds);
            CollectionAssert.AreEquivalent(ids, catalogCombatIds);

            Assert.That(CombatRelicIds.IsSupported(null), Is.False);
            Assert.That(CombatRelicIds.IsSupported("relic.unknown"), Is.False);
            Assert.That(CombatRelicIds.IsSupported("relic.pale_bone"), Is.False);
            Assert.That(CombatRelicIds.IsSupported("relic.marked_coin"), Is.False);
        }

        [Test]
        public void BothSlotsMayBeEmptyAndEitherSlotMayBeNullForEveryRelic()
        {
            AssertModifiers(CombatRelicResolver.Resolve(null, null), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            string[] ids =
            {
                CombatRelicIds.UmbraFang,
                CombatRelicIds.ShatteredClock,
                CombatRelicIds.ArrowFeather,
                CombatRelicIds.InlaidEmber,
                CombatRelicIds.FirstEchoMedallion,
                CombatRelicIds.BlackNeedle,
                CombatRelicIds.HunterRing,
                CombatRelicIds.OakMace
            };

            foreach (string id in ids)
            {
                AssertEquivalent(CombatRelicResolver.Resolve(id, null), CombatRelicResolver.Resolve(null, id));
            }
        }

        [TestCase("relic.unknown", null, "relicOneId")]
        [TestCase(null, "relic.unknown", "relicTwoId")]
        [TestCase("relic.pale_bone", null, "relicOneId")]
        [TestCase(null, "relic.pale_bone", "relicTwoId")]
        [TestCase("relic.marked_coin", null, "relicOneId")]
        [TestCase(null, "relic.marked_coin", "relicTwoId")]
        [TestCase("", null, "relicOneId")]
        [TestCase(null, "", "relicTwoId")]
        public void UnknownAndOtherFamilyIdsAreRejected(
            string relicOneId,
            string relicTwoId,
            string parameterName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => CombatRelicResolver.Resolve(relicOneId, relicTwoId));

            Assert.That(exception.ParamName, Is.EqualTo(parameterName));
        }

        [TestCase(CombatRelicIds.UmbraFang)]
        [TestCase(CombatRelicIds.ShatteredClock)]
        [TestCase(CombatRelicIds.ArrowFeather)]
        [TestCase(CombatRelicIds.InlaidEmber)]
        [TestCase(CombatRelicIds.FirstEchoMedallion)]
        [TestCase(CombatRelicIds.BlackNeedle)]
        [TestCase(CombatRelicIds.HunterRing)]
        [TestCase(CombatRelicIds.OakMace)]
        public void TheSameRelicCannotOccupyBothSlots(string relicId)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => CombatRelicResolver.Resolve(relicId, relicId));

            Assert.That(exception.ParamName, Is.EqualTo("relicTwoId"));
        }

        [Test]
        public void DistinctSlotsAggregateTheirEffectsWithoutCrossingCategories()
        {
            AssertModifiers(
                CombatRelicResolver.Resolve(CombatRelicIds.UmbraFang, CombatRelicIds.ShatteredClock),
                10, -8, 0, 0, 0, 0, 0, 0, 0, 0);
            AssertModifiers(
                CombatRelicResolver.Resolve(CombatRelicIds.ArrowFeather, CombatRelicIds.OakMace),
                0, 0, 20, 0, 0, 0, 0, 0, 0, 25);
            AssertModifiers(
                CombatRelicResolver.Resolve(CombatRelicIds.InlaidEmber, CombatRelicIds.FirstEchoMedallion),
                0, 0, 0, 2, 3, 25, 0, 0, 0, 0);
            AssertModifiers(
                CombatRelicResolver.Resolve(CombatRelicIds.BlackNeedle, CombatRelicIds.HunterRing),
                0, 0, 0, 0, 0, 0, 2, 2, 500, 0);
        }

        [Test]
        public void SlotOrderDoesNotChangeTheAggregatedModifiers()
        {
            AssertEquivalent(
                CombatRelicResolver.Resolve(CombatRelicIds.InlaidEmber, CombatRelicIds.FirstEchoMedallion),
                CombatRelicResolver.Resolve(CombatRelicIds.FirstEchoMedallion, CombatRelicIds.InlaidEmber));
        }

        [Test]
        public void RepeatedResolutionHasNoPerEnemyOrKillCooldownStateOrOtherSideEffects()
        {
            CombatRelicModifiers before = CombatRelicResolver.Resolve(
                CombatRelicIds.FirstEchoMedallion,
                CombatRelicIds.HunterRing);

            _ = CombatRelicResolver.Resolve(CombatRelicIds.InlaidEmber, CombatRelicIds.BlackNeedle);

            CombatRelicModifiers after = CombatRelicResolver.Resolve(
                CombatRelicIds.FirstEchoMedallion,
                CombatRelicIds.HunterRing);

            AssertEquivalent(before, after);
            Assert.That(after.FirstImpactPerEnemyDamageBonusPercent, Is.EqualTo(25));
            Assert.That(after.KillHealAmount, Is.EqualTo(2));
            Assert.That(after.KillHealMinimumIntervalMilliseconds, Is.EqualTo(500));
        }

        private static void AssertEquivalent(CombatRelicModifiers left, CombatRelicModifiers right)
        {
            AssertModifiers(
                left,
                right.BasicAttackDamageBonusPercent,
                right.BasicAttackIntervalModifierPercent,
                right.ProjectileSpeedBonusPercent,
                right.BasicAttackBurnDamagePerSecond,
                right.BasicAttackBurnDurationSeconds,
                right.FirstImpactPerEnemyDamageBonusPercent,
                right.DefensePenetration,
                right.KillHealAmount,
                right.KillHealMinimumIntervalMilliseconds,
                right.KnockbackBonusPercent);
        }

        private static void AssertModifiers(
            CombatRelicModifiers modifiers,
            int expectedBasicAttackDamageBonusPercent,
            int expectedBasicAttackIntervalModifierPercent,
            int expectedProjectileSpeedBonusPercent,
            int expectedBasicAttackBurnDamagePerSecond,
            int expectedBasicAttackBurnDurationSeconds,
            int expectedFirstImpactPerEnemyDamageBonusPercent,
            int expectedDefensePenetration,
            int expectedKillHealAmount,
            int expectedKillHealMinimumIntervalMilliseconds,
            int expectedKnockbackBonusPercent)
        {
            Assert.That(modifiers.BasicAttackDamageBonusPercent, Is.EqualTo(expectedBasicAttackDamageBonusPercent));
            Assert.That(modifiers.BasicAttackIntervalModifierPercent, Is.EqualTo(expectedBasicAttackIntervalModifierPercent));
            Assert.That(modifiers.ProjectileSpeedBonusPercent, Is.EqualTo(expectedProjectileSpeedBonusPercent));
            Assert.That(modifiers.BasicAttackBurnDamagePerSecond, Is.EqualTo(expectedBasicAttackBurnDamagePerSecond));
            Assert.That(modifiers.BasicAttackBurnDurationSeconds, Is.EqualTo(expectedBasicAttackBurnDurationSeconds));
            Assert.That(
                modifiers.FirstImpactPerEnemyDamageBonusPercent,
                Is.EqualTo(expectedFirstImpactPerEnemyDamageBonusPercent));
            Assert.That(modifiers.DefensePenetration, Is.EqualTo(expectedDefensePenetration));
            Assert.That(modifiers.KillHealAmount, Is.EqualTo(expectedKillHealAmount));
            Assert.That(
                modifiers.KillHealMinimumIntervalMilliseconds,
                Is.EqualTo(expectedKillHealMinimumIntervalMilliseconds));
            Assert.That(modifiers.KnockbackBonusPercent, Is.EqualTo(expectedKnockbackBonusPercent));
        }
    }
}
