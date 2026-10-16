using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class FortuneOfferBookTests
    {
        private static readonly string[] ConsumableIds =
        {
            "consumable.health_potion",
            "consumable.energy_tonic",
            "consumable.antidote",
            "consumable.bomb",
            "consumable.fortune_brew",
            "consumable.torch"
        };

        [Test]
        public void SameSeedAndSavedStateRestoreTheSameOfferAndNextReroll()
        {
            const uint seed = 17320508u;
            var firstBook = new FortuneOfferBook(1, seed, FortuneOfferBook.HumanRaceId);
            var sameSeedBook = new FortuneOfferBook(1, seed, FortuneOfferBook.HumanRaceId);
            FortuneOffer first = Create(firstBook, "patio.guardian", "patio.guardian");
            FortuneOfferResult sameSeedResult = sameSeedBook.CreateOffer("patio.guardian", "patio.guardian", ConsumableIds.Reverse());
            FortuneOffer sameSeed = sameSeedResult.Offer;
            Assert.That(sameSeedResult.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            CollectionAssert.AreEqual(first.OptionIds, sameSeed.OptionIds);

            FortuneOfferResult repeatedCreate = firstBook.CreateOffer(first.OfferId, first.ContextId, ConsumableIds.Reverse());
            Assert.That(repeatedCreate.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            CollectionAssert.AreEqual(first.OptionIds, repeatedCreate.Offer.OptionIds);

            FortuneRerollResult firstReroll = firstBook.Reroll(first.OfferId);
            Assert.That(firstReroll.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            FortuneOfferBook restoredBook = FortuneOfferBook.Restore(firstBook.CreateSnapshot());
            FortuneOffer restoredCurrent = restoredBook.GetOffer(first.OfferId);
            AssertOfferEqual(firstReroll.Offer, restoredCurrent);

            FortuneRerollResult uninterruptedNext = firstBook.Reroll(first.OfferId);
            FortuneRerollResult restoredNext = restoredBook.Reroll(first.OfferId);
            Assert.That(uninterruptedNext.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(restoredNext.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            CollectionAssert.AreEqual(uninterruptedNext.Offer.OptionIds, restoredNext.Offer.OptionIds);
            Assert.That(restoredNext.Offer.RerollCount, Is.EqualTo(uninterruptedNext.Offer.RerollCount));
            Assert.That(restoredBook.CurrentCharges, Is.EqualTo(firstBook.CurrentCharges));
        }

        [Test]
        public void OffersAlwaysHaveThreeUniqueChoicesAndEachRerollIsFresh()
        {
            var book = new FortuneOfferBook(1, 991827u, FortuneOfferBook.HumanRaceId);
            FortuneOffer offer = Create(book, "archivo.special", "archivo.special");
            var seenSets = new HashSet<string>(StringComparer.Ordinal);
            AssertFreshSet(offer, seenSets);

            for (int index = 0; index < 2; index++)
            {
                FortuneRerollResult reroll = book.Reroll(offer.OfferId);
                Assert.That(reroll.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                offer = reroll.Offer;
                AssertFreshSet(offer, seenSets);
            }
        }

        [Test]
        public void RerollingDoesNotChangeTheRouteRandomStream()
        {
            const uint seed = 381734u;
            ExpeditionRoutePlan before = ExpeditionRouteGenerator.Generate(seed, CreateRouteCatalog());
            var book = new FortuneOfferBook(ExpeditionRouteGenerator.GenerationVersion, seed, FortuneOfferBook.HumanRaceId);
            FortuneOffer offer = Create(book, "forjas.guardian", "forjas.guardian");
            Assert.That(book.Reroll(offer.OfferId).Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            ExpeditionRoutePlan after = ExpeditionRouteGenerator.Generate(seed, CreateRouteCatalog());

            Assert.That(after.CanonicalForm, Is.EqualTo(before.CanonicalForm));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void InsufficientPoolRejectsRerollWithoutSpendingAChargeOrChangingTheOffer()
        {
            var book = new FortuneOfferBook(1, 22u, FortuneOfferBook.HumanRaceId);
            FortuneOffer offer = book.CreateOffer("patio.special", "patio.special", ConsumableIds.Take(3)).Offer;
            Assert.That(offer, Is.Not.Null);
            string[] priorOptions = offer.OptionIds.ToArray();
            int priorCharges = book.CurrentCharges;

            FortuneRerollResult reroll = book.Reroll(offer.OfferId);

            Assert.That(reroll.Status, Is.EqualTo(FortuneOperationStatus.RejectedInsufficientOptions));
            Assert.That(book.GetRerollAvailability(offer.OfferId), Is.EqualTo(FortuneRerollAvailability.InsufficientOptions));
            Assert.That(book.CurrentCharges, Is.EqualTo(priorCharges));
            CollectionAssert.AreEqual(priorOptions, book.GetOffer(offer.OfferId).OptionIds);
        }

        [Test]
        public void InitialOfferWithFewerThanThreeEligibleOptionsIsRejectedWithoutCreatingState()
        {
            var book = new FortuneOfferBook(1, 223u, FortuneOfferBook.HumanRaceId);
            int priorCharges = book.CurrentCharges;

            FortuneOfferResult result = book.CreateOffer("patio.special", "patio.special", ConsumableIds.Take(2));

            Assert.That(result.Status, Is.EqualTo(FortuneOperationStatus.RejectedInsufficientOptions));
            Assert.That(result.Offer, Is.Null);
            Assert.That(book.GetOffer("patio.special"), Is.Null);
            Assert.That(book.CreateSnapshot().offers, Is.Empty);
            Assert.That(book.CurrentCharges, Is.EqualTo(priorCharges));
        }

        [Test]
        public void ReusingAnOfferIdWithDifferentContextOrPoolCannotReplaceItsSavedOffer()
        {
            var book = new FortuneOfferBook(1, 7381u, FortuneOfferBook.HumanRaceId);
            FortuneOffer original = Create(book, "patio.special", "patio.special");
            string[] originalOptions = original.OptionIds.ToArray();
            int priorCharges = book.CurrentCharges;

            FortuneOfferResult changedContext = book.CreateOffer(original.OfferId, "archivo.special", ConsumableIds);
            FortuneOfferResult changedPool = book.CreateOffer(original.OfferId, original.ContextId,
                ConsumableIds.Concat(new[] { "relic.marked_coin" }));

            Assert.That(changedContext.Status, Is.EqualTo(FortuneOperationStatus.RejectedConflict));
            Assert.That(changedPool.Status, Is.EqualTo(FortuneOperationStatus.RejectedConflict));
            Assert.That(book.CurrentCharges, Is.EqualTo(priorCharges));
            AssertOfferEqual(original, book.GetOffer(original.OfferId));
            CollectionAssert.AreEqual(originalOptions, book.GetOffer(original.OfferId).OptionIds);
        }

        [Test]
        public void ChosenOfferRejectsRerollsAndKeepsItsChoiceAndChargeBalance()
        {
            var book = new FortuneOfferBook(1, 947u, FortuneOfferBook.HumanRaceId);
            FortuneOffer offer = Create(book, "archivo.guardian", "archivo.guardian");
            string selectedOption = offer.OptionIds[0];
            Assert.That(book.Choose(offer.OfferId, selectedOption).Status,
                Is.EqualTo(FortuneOperationStatus.Succeeded));
            int priorCharges = book.CurrentCharges;

            FortuneRerollResult reroll = book.Reroll(offer.OfferId);

            Assert.That(reroll.Status, Is.EqualTo(FortuneOperationStatus.RejectedConflict));
            Assert.That(reroll.Offer.SelectedOptionId, Is.EqualTo(selectedOption));
            Assert.That(reroll.Offer.RerollCount, Is.Zero);
            Assert.That(book.CurrentCharges, Is.EqualTo(priorCharges));
        }

        [Test]
        public void SnapshotCopiesOfferHistoryAndRestoreRejectsRepeatedOptions()
        {
            var book = new FortuneOfferBook(1, 125u, FortuneOfferBook.HumanRaceId);
            FortuneOffer offer = Create(book, "forjas.special", "forjas.special");
            string[] originalOptions = offer.OptionIds.ToArray();
            FortuneOfferBookSnapshot snapshot = book.CreateSnapshot();

            snapshot.offers[0].history[0].optionIds[0] = "relic.tampered";

            CollectionAssert.AreEqual(originalOptions, book.GetOffer(offer.OfferId).OptionIds);
            FortuneOfferBookSnapshot invalidSnapshot = book.CreateSnapshot();
            invalidSnapshot.offers[0].history[0].optionIds[1] = invalidSnapshot.offers[0].history[0].optionIds[0];

            Assert.Throws<FormatException>(() => FortuneOfferBook.Restore(invalidSnapshot));
            CollectionAssert.AreEqual(originalOptions, FortuneOfferBook.Restore(book.CreateSnapshot())
                .GetOffer(offer.OfferId).OptionIds);
        }

        [Test]
        public void HumanStartsWithTwoChargesGuardianGrantCapsAtThreeAndRerollConsumesOne()
        {
            var book = new FortuneOfferBook(1, 443u, FortuneOfferBook.HumanRaceId);
            Assert.That(book.CurrentCharges, Is.EqualTo(2));
            Assert.That(new FortuneOfferBook(1, 443u, "race.dvergar").CurrentCharges, Is.EqualTo(1));
            Assert.That(book.GrantGuardianCharge("patio.guardian").CurrentCharges, Is.EqualTo(3));

            FortuneChargeResult atCapacity = book.GrantGuardianCharge("archivo.guardian");
            Assert.That(atCapacity.Status, Is.EqualTo(FortuneOperationStatus.RejectedAtCapacity));
            Assert.That(book.CurrentCharges, Is.EqualTo(FortuneOfferBook.MaximumCharges));

            FortuneOffer offer = Create(book, "forjas.guardian", "forjas.guardian");
            for (int index = 0; index < FortuneOfferBook.MaximumCharges; index++)
            {
                FortuneRerollResult reroll = book.Reroll(offer.OfferId);
                Assert.That(reroll.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
                Assert.That(reroll.RemainingCharges, Is.EqualTo(FortuneOfferBook.MaximumCharges - index - 1));
            }

            FortuneRerollResult noCharge = book.Reroll(offer.OfferId);
            Assert.That(noCharge.Status, Is.EqualTo(FortuneOperationStatus.RejectedNoCharges));
            Assert.That(noCharge.RemainingCharges, Is.Zero);
            Assert.That(book.GrantGuardianCharge("archivo.guardian").Status,
                Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(book.CurrentCharges, Is.Zero);
        }

        [Test]
        public void ChoiceAndRewardClaimAreIdempotentAcrossRestore()
        {
            var book = new FortuneOfferBook(1, 91732u, FortuneOfferBook.HumanRaceId);
            FortuneOffer offer = Create(book, "patio.special", "patio.special");
            string selected = offer.OptionIds[0];

            Assert.That(book.Choose(offer.OfferId, selected).Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(book.Choose(offer.OfferId, selected).Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(book.Choose(offer.OfferId, offer.OptionIds[1]).Status,
                Is.EqualTo(FortuneOperationStatus.RejectedConflict));

            FortuneClaimResult firstClaim = book.Claim(offer.OfferId);
            FortuneClaimResult duplicateClaim = book.Claim(offer.OfferId);
            Assert.That(firstClaim.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(duplicateClaim.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(duplicateClaim.ClaimId, Is.EqualTo(firstClaim.ClaimId));
            Assert.That(firstClaim.OptionId, Is.EqualTo(selected));

            FortuneOfferBook restored = FortuneOfferBook.Restore(book.CreateSnapshot());
            FortuneClaimResult afterRestore = restored.Claim(offer.OfferId);
            Assert.That(afterRestore.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(afterRestore.ClaimId, Is.EqualTo(firstClaim.ClaimId));
            Assert.That(afterRestore.OptionId, Is.EqualTo(selected));
            Assert.That(restored.GetOffer(offer.OfferId).IsClaimed, Is.True);
        }

        [Test]
        public void RepeatedInputIdsAreCollapsedBeforeSelectingOptions()
        {
            var book = new FortuneOfferBook(1, 781u, FortuneOfferBook.HumanRaceId);
            IEnumerable<string> candidates = new[]
            {
                "relic.marked_coin", "relic.marked_coin", "relic.hidden_pouch",
                "consumable.health_potion", "consumable.health_potion"
            };

            FortuneOfferResult result = book.CreateOffer("patio.special", "patio.special", candidates);

            Assert.That(result.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(result.Offer.OptionIds.Count, Is.EqualTo(3));
            Assert.That(result.Offer.OptionIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
        }

        private static FortuneOffer Create(FortuneOfferBook book, string offerId, string contextId)
        {
            FortuneOfferResult result = book.CreateOffer(offerId, contextId, ConsumableIds);
            Assert.That(result.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            return result.Offer;
        }

        private static void AssertFreshSet(FortuneOffer offer, ISet<string> seenSets)
        {
            Assert.That(offer.OptionIds.Count, Is.EqualTo(3));
            Assert.That(offer.OptionIds.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
            string key = string.Join("|", offer.OptionIds.OrderBy(id => id, StringComparer.Ordinal));
            Assert.That(seenSets.Add(key), Is.True, "A complete offer must not be repeated.");
        }

        private static void AssertOfferEqual(FortuneOffer expected, FortuneOffer actual)
        {
            Assert.That(actual.OfferId, Is.EqualTo(expected.OfferId));
            Assert.That(actual.ContextId, Is.EqualTo(expected.ContextId));
            CollectionAssert.AreEqual(expected.OptionIds, actual.OptionIds);
            Assert.That(actual.SelectedOptionId, Is.EqualTo(expected.SelectedOptionId));
            Assert.That(actual.RerollCount, Is.EqualTo(expected.RerollCount));
            Assert.That(actual.IsClaimed, Is.EqualTo(expected.IsClaimed));
        }

        private static ExpeditionRouteCatalog CreateRouteCatalog()
        {
            return new ExpeditionRouteCatalog(new[]
            {
                CreateSector("patio"),
                CreateSector("archivo"),
                CreateSector("forjas")
            }, "room.hub.rest", "room.final.arena");
        }

        private static ExpeditionRouteSectorCatalog CreateSector(string sectorId)
        {
            return new ExpeditionRouteSectorCatalog(
                sectorId,
                Enumerable.Range(1, 5).Select(index => "room." + sectorId + ".c" + index),
                Enumerable.Range(1, 2).Select(index => "room." + sectorId + ".e" + index),
                "room." + sectorId + ".t",
                "room." + sectorId + ".p",
                "room." + sectorId + ".n",
                "room." + sectorId + ".guardian");
        }
    }
}
