using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Luckbound.Core.Fortune;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Fortune
{
    public sealed class FortuneRelicResolverTests
    {
        [TestCase(FortuneRelicIds.FourEchoCrystal, 1, false, 0, 0, 0, 0, 0, 0)]
        [TestCase(FortuneRelicIds.MarkedCoin, 0, true, 0, 0, 0, 0, 0, 0)]
        [TestCase(FortuneRelicIds.VitalDie, 0, false, 5, 0, 0, 0, 0, 0)]
        [TestCase(FortuneRelicIds.LuckyBranch, 0, false, 0, 1, 0, 0, 0, 0)]
        [TestCase(FortuneRelicIds.HiddenPouch, 0, false, 0, 0, 25, 0, 0, 0)]
        [TestCase(FortuneRelicIds.MerchantCounterweight, 0, false, 0, 0, 0, 50, 0, 0)]
        [TestCase(FortuneRelicIds.ConcordSigil, 0, false, 0, 0, 0, 0, 25, 0)]
        [TestCase(FortuneRelicIds.OathFragment, 0, false, 0, 0, 0, 0, 0, 1)]
        public void EachFortuneRelicResolvesItsExactPlannedParameters(
            string relicId,
            int expectedAdditionalOfferOptionCount,
            bool expectedFreeFirstRerollPerSector,
            int expectedHealthRestoredOnReroll,
            int expectedAdditionalFortunePerGuardian,
            int expectedContainerGoldBonusPercent,
            int expectedSaleValueBonusPercent,
            int expectedPactBenefitAmplificationPercent,
            int expectedFortuneGrantedOnPactAcceptance)
        {
            AssertModifiers(
                FortuneRelicResolver.Resolve(relicId, null),
                expectedAdditionalOfferOptionCount,
                expectedFreeFirstRerollPerSector,
                expectedHealthRestoredOnReroll,
                expectedAdditionalFortunePerGuardian,
                expectedContainerGoldBonusPercent,
                expectedSaleValueBonusPercent,
                expectedPactBenefitAmplificationPercent,
                expectedFortuneGrantedOnPactAcceptance);
        }

        [Test]
        public void CatalogContainsExactlyTheEightUniqueSupportedFortuneIds()
        {
            string[] ids = GetCataloguedIds();

            Assert.That(ids.Length, Is.EqualTo(8));
            CollectionAssert.AllItemsAreUnique(ids);

            int declaredIdCount = typeof(FortuneRelicIds).GetFields(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Count(field => field.IsLiteral && field.FieldType == typeof(string));
            Assert.That(declaredIdCount, Is.EqualTo(8));
            foreach (string id in ids)
                Assert.That(FortuneRelicIds.IsSupported(id), Is.True, id);

            string itemsJson = File.ReadAllText("Assets/Resources/Luckbound/Data/Items/Items.json");
            string[] catalogIds = Regex.Matches(itemsJson, "\"id\"\\s*:\\s*\"([^\"]+)\"")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Where(FortuneRelicIds.IsSupported)
                .ToArray();

            Assert.That(catalogIds.Length, Is.EqualTo(8));
            CollectionAssert.AllItemsAreUnique(catalogIds);
            CollectionAssert.AreEquivalent(ids, catalogIds);
        }

        [Test]
        public void EveryFortuneRelicHasSpanishAndEnglishNameAndDescriptionKeys()
        {
            string localizedJson = File.ReadAllText("Assets/Resources/Luckbound/Localization/Initial.json");
            var localizedKeys = new HashSet<string>(
                Regex.Matches(localizedJson, "\"key\"\\s*:\\s*\"([^\"]+)\"")
                    .Cast<Match>()
                    .Select(match => match.Groups[1].Value),
                StringComparer.Ordinal);

            foreach (string relicId in GetCataloguedIds())
            {
                string suffix = relicId.Substring("relic.".Length);
                Assert.That(localizedKeys.Contains("item.relic." + suffix + ".name"), Is.True, relicId + " name");
                Assert.That(
                    localizedKeys.Contains("item.relic." + suffix + ".description"),
                    Is.True,
                    relicId + " description");
            }
        }

        [Test]
        public void EmptySlotsAreAcceptedAndEitherSlotMayBeNull()
        {
            AssertModifiers(FortuneRelicResolver.Resolve(null, null), 0, false, 0, 0, 0, 0, 0, 0);

            foreach (string relicId in GetCataloguedIds())
            {
                AssertEquivalent(
                    FortuneRelicResolver.Resolve(relicId, null),
                    FortuneRelicResolver.Resolve(null, relicId));
            }
        }

        [TestCase("relic.unknown", null, "relicOneId")]
        [TestCase(null, "relic.unknown", "relicTwoId")]
        [TestCase("relic.pale_bone", null, "relicOneId")]
        [TestCase(null, "relic.umbra_fang", "relicTwoId")]
        [TestCase("", null, "relicOneId")]
        [TestCase(null, "", "relicTwoId")]
        public void UnknownAndOtherFamilyIdsAreRejected(
            string relicOneId,
            string relicTwoId,
            string parameterName)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => FortuneRelicResolver.Resolve(relicOneId, relicTwoId));

            Assert.That(exception.ParamName, Is.EqualTo(parameterName));
        }

        [TestCase(FortuneRelicIds.FourEchoCrystal)]
        [TestCase(FortuneRelicIds.MarkedCoin)]
        [TestCase(FortuneRelicIds.VitalDie)]
        [TestCase(FortuneRelicIds.LuckyBranch)]
        [TestCase(FortuneRelicIds.HiddenPouch)]
        [TestCase(FortuneRelicIds.MerchantCounterweight)]
        [TestCase(FortuneRelicIds.ConcordSigil)]
        [TestCase(FortuneRelicIds.OathFragment)]
        public void TheSameRelicCannotOccupyBothSlots(string relicId)
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => FortuneRelicResolver.Resolve(relicId, relicId));

            Assert.That(exception.ParamName, Is.EqualTo("relicTwoId"));
        }

        [Test]
        public void DistinctSlotsKeepEffectsInTheirOwnCategories()
        {
            AssertModifiers(
                FortuneRelicResolver.Resolve(FortuneRelicIds.FourEchoCrystal, FortuneRelicIds.VitalDie),
                1, false, 5, 0, 0, 0, 0, 0);
            AssertModifiers(
                FortuneRelicResolver.Resolve(FortuneRelicIds.LuckyBranch, FortuneRelicIds.OathFragment),
                0, false, 0, 1, 0, 0, 0, 1);
            AssertModifiers(
                FortuneRelicResolver.Resolve(FortuneRelicIds.HiddenPouch, FortuneRelicIds.MerchantCounterweight),
                0, false, 0, 0, 25, 50, 0, 0);
            AssertModifiers(
                FortuneRelicResolver.Resolve(FortuneRelicIds.ConcordSigil, FortuneRelicIds.MarkedCoin),
                0, true, 0, 0, 0, 0, 25, 0);
        }

        [Test]
        public void MatchingPercentageCategoriesAreAddedBeforeConsumerApplication()
        {
            FortuneRelicModifiers gold = FortuneRelicResolver.Resolve(FortuneRelicIds.HiddenPouch, null);
            FortuneRelicModifiers sale = FortuneRelicResolver.Resolve(FortuneRelicIds.MerchantCounterweight, null);
            FortuneRelicModifiers pact = FortuneRelicResolver.Resolve(FortuneRelicIds.ConcordSigil, null);

            FortuneRelicModifiers combinedGold = gold.Add(gold);
            FortuneRelicModifiers combinedSale = sale.Add(sale);
            FortuneRelicModifiers combinedPact = pact.Add(pact);

            Assert.That(combinedGold.ContainerGoldBonusPercent, Is.EqualTo(50));
            Assert.That(combinedGold.SaleValueBonusPercent, Is.Zero);
            Assert.That(combinedGold.PositivePactBenefitAmplificationPercent, Is.Zero);
            Assert.That(combinedSale.SaleValueBonusPercent, Is.EqualTo(100));
            Assert.That(combinedSale.ContainerGoldBonusPercent, Is.Zero);
            Assert.That(combinedPact.PositivePactBenefitAmplificationPercent, Is.EqualTo(50));
            Assert.That(combinedPact.ContainerGoldBonusPercent, Is.Zero);
        }

        [Test]
        public void CurrencyAndRewardRoundingUsesFloor()
        {
            Assert.That(FortuneRelicModifiers.RoundCurrencyOrRewardDown(0.99m), Is.Zero);
            Assert.That(FortuneRelicModifiers.RoundCurrencyOrRewardDown(8.75m), Is.EqualTo(8));
            Assert.That(FortuneRelicModifiers.RoundCurrencyOrRewardDown(9m), Is.EqualTo(9));
        }

        [Test]
        public void CurrencyAndRewardRoundingRejectsNegativeAndOverflowValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => FortuneRelicModifiers.RoundCurrencyOrRewardDown(-0.01m));
            Assert.Throws<OverflowException>(
                () => FortuneRelicModifiers.RoundCurrencyOrRewardDown((decimal)int.MaxValue + 1m));
        }

        private static string[] GetCataloguedIds()
        {
            return new[]
            {
                FortuneRelicIds.FourEchoCrystal,
                FortuneRelicIds.MarkedCoin,
                FortuneRelicIds.VitalDie,
                FortuneRelicIds.LuckyBranch,
                FortuneRelicIds.HiddenPouch,
                FortuneRelicIds.MerchantCounterweight,
                FortuneRelicIds.ConcordSigil,
                FortuneRelicIds.OathFragment
            };
        }

        private static void AssertEquivalent(FortuneRelicModifiers left, FortuneRelicModifiers right)
        {
            AssertModifiers(
                left,
                right.AdditionalOfferOptionCount,
                right.GrantsFreeFirstRerollPerSector,
                right.HealthRestoredOnReroll,
                right.AdditionalFortunePerGuardian,
                right.ContainerGoldBonusPercent,
                right.SaleValueBonusPercent,
                right.PositivePactBenefitAmplificationPercent,
                right.FortuneGrantedOnPactAcceptance);
        }

        private static void AssertModifiers(
            FortuneRelicModifiers modifiers,
            int expectedAdditionalOfferOptionCount,
            bool expectedFreeFirstRerollPerSector,
            int expectedHealthRestoredOnReroll,
            int expectedAdditionalFortunePerGuardian,
            int expectedContainerGoldBonusPercent,
            int expectedSaleValueBonusPercent,
            int expectedPactBenefitAmplificationPercent,
            int expectedFortuneGrantedOnPactAcceptance)
        {
            Assert.That(modifiers.AdditionalOfferOptionCount, Is.EqualTo(expectedAdditionalOfferOptionCount));
            Assert.That(modifiers.GrantsFreeFirstRerollPerSector, Is.EqualTo(expectedFreeFirstRerollPerSector));
            Assert.That(modifiers.HealthRestoredOnReroll, Is.EqualTo(expectedHealthRestoredOnReroll));
            Assert.That(modifiers.AdditionalFortunePerGuardian, Is.EqualTo(expectedAdditionalFortunePerGuardian));
            Assert.That(modifiers.ContainerGoldBonusPercent, Is.EqualTo(expectedContainerGoldBonusPercent));
            Assert.That(modifiers.SaleValueBonusPercent, Is.EqualTo(expectedSaleValueBonusPercent));
            Assert.That(
                modifiers.PositivePactBenefitAmplificationPercent,
                Is.EqualTo(expectedPactBenefitAmplificationPercent));
            Assert.That(
                modifiers.FortuneGrantedOnPactAcceptance,
                Is.EqualTo(expectedFortuneGrantedOnPactAcceptance));
        }
    }
}
