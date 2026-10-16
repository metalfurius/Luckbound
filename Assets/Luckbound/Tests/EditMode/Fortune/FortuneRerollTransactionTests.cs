using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class FortuneRerollTransactionTests
    {
        [Test]
        public void RestoredSuccessfulTransactionReplaysOriginalOfferWithoutSpendingAgain()
        {
            var book = new FortuneOfferBook(1, 7281u, FortuneOfferBook.HumanRaceId);
            FortuneOfferResult created = book.CreateOffer("offer.chest", "context.chest",
                new[] { "relic.ash", "relic.ember", "relic.iron", "relic.mirror", "relic.moon" });
            TransactionId transactionId = TransactionId.New();

            FortuneRerollResult first = book.Reroll(created.Offer.OfferId, transactionId);
            FortuneOfferBook restored = FortuneOfferBook.Restore(book.CreateSnapshot());
            FortuneRerollResult replay = restored.Reroll(created.Offer.OfferId, transactionId);

            Assert.That(first.Status, Is.EqualTo(FortuneOperationStatus.Succeeded));
            Assert.That(replay.Status, Is.EqualTo(FortuneOperationStatus.AlreadyApplied));
            Assert.That(replay.RemainingCharges, Is.EqualTo(first.RemainingCharges));
            Assert.That(restored.CurrentCharges, Is.EqualTo(first.RemainingCharges));
            CollectionAssert.AreEqual(first.Offer.OptionIds, replay.Offer.OptionIds);
            Assert.That(replay.Offer.RerollCount, Is.EqualTo(first.Offer.RerollCount));
        }

        [Test]
        public void RejectedRerollTransactionKeepsItsResultAfterLaterChargeGrantAndRestore()
        {
            var book = new FortuneOfferBook(1, 9127u, "race.orcini");
            FortuneOfferResult created = book.CreateOffer("offer.small", "context.treasure",
                new[] { "relic.ash", "relic.ember", "relic.iron" });
            TransactionId transactionId = TransactionId.New();
            FortuneRerollResult rejected = book.Reroll(created.Offer.OfferId, transactionId);
            Assert.That(rejected.Status, Is.EqualTo(FortuneOperationStatus.RejectedInsufficientOptions));

            FortuneOfferBook restored = FortuneOfferBook.Restore(book.CreateSnapshot());
            Assert.That(restored.GrantGuardianCharge("guardian.one").Succeeded, Is.True);
            FortuneOfferBook afterGrantReload = FortuneOfferBook.Restore(restored.CreateSnapshot());
            FortuneRerollResult replay = afterGrantReload.Reroll(created.Offer.OfferId, transactionId);

            Assert.That(replay.Status, Is.EqualTo(FortuneOperationStatus.RejectedInsufficientOptions));
            Assert.That(afterGrantReload.CurrentCharges, Is.EqualTo(2));
            Assert.That(replay.RemainingCharges, Is.EqualTo(1));
            CollectionAssert.AreEqual(rejected.Offer.OptionIds, replay.Offer.OptionIds);
        }

        [Test]
        public void TransactionIdCannotBeReusedForAnotherOffer()
        {
            var book = new FortuneOfferBook(1, 3371u, FortuneOfferBook.HumanRaceId);
            book.CreateOffer("offer.one", "context.one", new[] { "a", "b", "c", "d" });
            book.CreateOffer("offer.two", "context.two", new[] { "e", "f", "g", "h" });
            TransactionId transactionId = TransactionId.New();
            int chargesBefore = book.CurrentCharges;

            FortuneRerollResult first = book.Reroll("offer.one", transactionId);
            FortuneRerollResult conflict = book.Reroll("offer.two", transactionId);

            Assert.That(first.Succeeded, Is.True);
            Assert.That(conflict.Status, Is.EqualTo(FortuneOperationStatus.RejectedConflict));
            Assert.That(book.CurrentCharges, Is.EqualTo(chargesBefore - 1));
        }

        [Test]
        public void VersionOneOfferBookSnapshotsRestoreWithoutRerollReceipts()
        {
            var book = new FortuneOfferBook(1, 4429u, FortuneOfferBook.HumanRaceId);
            book.CreateOffer("offer.legacy", "context.legacy", new[] { "a", "b", "c", "d" });
            FortuneOfferBookSnapshot legacy = book.CreateSnapshot();
            legacy.snapshotVersion = 1;
            legacy.rerollReceipts = null;

            FortuneOfferBook restored = FortuneOfferBook.Restore(legacy);

            Assert.That(restored.CurrentCharges, Is.EqualTo(2));
            Assert.That(restored.GetOffer("offer.legacy"), Is.Not.Null);
            Assert.That(restored.GetOffer("offer.legacy").RerollCount, Is.Zero);
            Assert.That(restored.CreateSnapshot().snapshotVersion, Is.EqualTo(FortuneOfferBook.SnapshotVersion));
        }
    }
}
