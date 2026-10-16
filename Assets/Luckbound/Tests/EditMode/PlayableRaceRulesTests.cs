using System;
using Luckbound.Core.Expeditions;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class PlayableRaceRulesTests
    {
        [TestCase(PlayableRaceRules.HumanRaceId)]
        [TestCase(PlayableRaceRules.OrciniRaceId)]
        [TestCase(PlayableRaceRules.AlvariRaceId)]
        [TestCase(PlayableRaceRules.DvergarRaceId)]
        public void SupportsOnlyTheFourPlayableRaceIds(string raceId)
        {
            Assert.That(PlayableRaceRules.IsSupportedRace(raceId), Is.True);
        }

        [Test]
        public void RaceMultipliersAreExactAndNeutralOutsideTheirRace()
        {
            Assert.That(PlayableRaceRules.OrciniOutgoingKnockbackMultiplier, Is.EqualTo(1.10m));
            Assert.That(PlayableRaceRules.AlvariSkillCostMultiplier, Is.EqualTo(0.95m));
            Assert.That(PlayableRaceRules.GetOutgoingKnockbackMultiplier(PlayableRaceRules.OrciniRaceId), Is.EqualTo(1.10m));
            Assert.That(PlayableRaceRules.GetSkillCostMultiplier(PlayableRaceRules.AlvariRaceId), Is.EqualTo(0.95m));

            Assert.That(PlayableRaceRules.GetOutgoingKnockbackMultiplier(PlayableRaceRules.HumanRaceId), Is.EqualTo(1m));
            Assert.That(PlayableRaceRules.GetOutgoingKnockbackMultiplier(PlayableRaceRules.AlvariRaceId), Is.EqualTo(1m));
            Assert.That(PlayableRaceRules.GetOutgoingKnockbackMultiplier(PlayableRaceRules.DvergarRaceId), Is.EqualTo(1m));
            Assert.That(PlayableRaceRules.GetSkillCostMultiplier(PlayableRaceRules.HumanRaceId), Is.EqualTo(1m));
            Assert.That(PlayableRaceRules.GetSkillCostMultiplier(PlayableRaceRules.OrciniRaceId), Is.EqualTo(1m));
            Assert.That(PlayableRaceRules.GetSkillCostMultiplier(PlayableRaceRules.DvergarRaceId), Is.EqualTo(1m));
        }

        [TestCase(8, 8)]
        [TestCase(12, 11)]
        [TestCase(16, 15)]
        public void AlvariSkillCostsUseHalfUpIntegerRounding(int baseCost, int expectedCost)
        {
            Assert.That(PlayableRaceRules.ApplySkillCost(PlayableRaceRules.AlvariRaceId, baseCost), Is.EqualTo(expectedCost));
        }

        [Test]
        public void OtherRacesKeepTheirPositiveSkillCostUnchanged()
        {
            Assert.That(PlayableRaceRules.ApplySkillCost(PlayableRaceRules.HumanRaceId, 12), Is.EqualTo(12));
            Assert.That(PlayableRaceRules.ApplySkillCost(PlayableRaceRules.OrciniRaceId, 12), Is.EqualTo(12));
            Assert.That(PlayableRaceRules.ApplySkillCost(PlayableRaceRules.DvergarRaceId, 12), Is.EqualTo(12));
        }

        [TestCase(PlayableRaceRules.HumanRaceId, false)]
        [TestCase(PlayableRaceRules.OrciniRaceId, false)]
        [TestCase(PlayableRaceRules.AlvariRaceId, false)]
        [TestCase(PlayableRaceRules.DvergarRaceId, true)]
        public void OnlyDvergarReceivesTheWorldPickupBonus(string raceId, bool expected)
        {
            Assert.That(PlayableRaceRules.HasWorldPickupBonus(raceId), Is.EqualTo(expected));
        }

        [TestCase(PlayableRaceRules.HumanRaceId, 2)]
        [TestCase(PlayableRaceRules.OrciniRaceId, 1)]
        [TestCase(PlayableRaceRules.AlvariRaceId, 1)]
        [TestCase(PlayableRaceRules.DvergarRaceId, 1)]
        public void StartingFortuneChargesMatchTheExistingOfferBookBehavior(string raceId, int expectedCharges)
        {
            Assert.That(PlayableRaceRules.GetStartingFortuneCharges(raceId), Is.EqualTo(expectedCharges));
        }

        [Test]
        public void NullRaceIdIsRejectedByRuleQueries()
        {
            Assert.That(PlayableRaceRules.IsSupportedRace(null), Is.False);
            Assert.Throws<ArgumentNullException>(() => PlayableRaceRules.GetOutgoingKnockbackMultiplier(null));
            Assert.Throws<ArgumentNullException>(() => PlayableRaceRules.GetSkillCostMultiplier(null));
            Assert.Throws<ArgumentNullException>(() => PlayableRaceRules.ApplySkillCost(null, 8));
            Assert.Throws<ArgumentNullException>(() => PlayableRaceRules.HasWorldPickupBonus(null));
            Assert.Throws<ArgumentNullException>(() => PlayableRaceRules.GetStartingFortuneCharges(null));
        }

        [Test]
        public void UnknownRaceIdIsRejectedByRuleQueries()
        {
            const string unknownRaceId = "race.unknown";
            Assert.That(PlayableRaceRules.IsSupportedRace(unknownRaceId), Is.False);
            Assert.Throws<ArgumentException>(() => PlayableRaceRules.GetOutgoingKnockbackMultiplier(unknownRaceId));
            Assert.Throws<ArgumentException>(() => PlayableRaceRules.GetSkillCostMultiplier(unknownRaceId));
            Assert.Throws<ArgumentException>(() => PlayableRaceRules.ApplySkillCost(unknownRaceId, 8));
            Assert.Throws<ArgumentException>(() => PlayableRaceRules.HasWorldPickupBonus(unknownRaceId));
            Assert.Throws<ArgumentException>(() => PlayableRaceRules.GetStartingFortuneCharges(unknownRaceId));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonPositiveSkillCostsAreRejected(int invalidCost)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayableRaceRules.ApplySkillCost(PlayableRaceRules.AlvariRaceId, invalidCost));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayableRaceRules.ApplySkillCost(PlayableRaceRules.HumanRaceId, invalidCost));
        }
    }
}
