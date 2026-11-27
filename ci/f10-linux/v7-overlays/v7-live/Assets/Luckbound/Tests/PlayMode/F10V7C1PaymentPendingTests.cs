#if UNITY_EDITOR
using Luckbound.App;
using System;
using System.Linq;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class F10V7C1PaymentPendingTests
    {
        [Test]
        public void PendingAllowsOnlyOneOperationAndIgnoresStaleRelease()
        {
            var pending = new F10V7C1CombatPaymentPending();

            Assert.That(pending.TryAcquireOperation(out object first), Is.True);
            Assert.That(pending.TryAcquireOperation(out object duplicate), Is.False);
            Assert.That(duplicate, Is.Null);

            pending.ReleaseOperation(new object());
            Assert.That(pending.TryAcquireOperation(out _), Is.False);

            pending.ReleaseOperation(first);
            Assert.That(pending.TryAcquireOperation(out object retry), Is.True);
            pending.ReleaseOperation(first);
            Assert.That(pending.TryAcquireOperation(out _), Is.False);

            pending.ReleaseOperation(retry);
            Assert.That(pending.TryAcquireOperation(out object next), Is.True);
            pending.ReleaseOperation(next);
        }

        [Test]
        public void HistoricalConfirmationRetainsIdentityButAlwaysRequiresCurrentReadback()
        {
            var pending = new F10V7C1CombatPaymentPending();
            pending.CaptureRepositoryCommitResult(F10V7SessionCommitResult.Rejected(
                "synchronous write remains uncertain", pending: true));
            Assert.That(pending.NextRepositoryAction, Is.EqualTo(F10V7C1RepositoryAction.Retry));

            F10V7SessionCommitResult confirmed = F10V7SessionCommitResult.Confirmed(
                "exact-confirmed-json", "read back");
            pending.CaptureRepositoryCommitResult(confirmed);
            Assert.That(pending.NextRepositoryAction, Is.EqualTo(F10V7C1RepositoryAction.Retry));
            Assert.That(pending.ConfirmedCommitResult, Is.SameAs(confirmed));

            pending.CaptureRepositoryCommitResult(F10V7SessionCommitResult.Confirmed(
                "different-json", "conflicting result"));
            Assert.That(pending.NextRepositoryAction, Is.EqualTo(F10V7C1RepositoryAction.Retry));
            Assert.That(pending.RepositoryPending, Is.True);
            Assert.That(pending.ConfirmedCommitResult, Is.SameAs(confirmed));
            Assert.That(pending.ConfirmedJson, Is.EqualTo("exact-confirmed-json"));
        }

        [Test]
        public void FreshReadbackFailureDoesNotEraseHistoricalConfirmationOrPermitSkippingRetry()
        {
            var pending = new F10V7C1CombatPaymentPending();
            var confirmed = F10V7SessionCommitResult.Confirmed("exact-json", "confirmed");
            pending.CaptureRepositoryCommitResult(confirmed);
            Assert.That(pending.TryAcquireOperation(out object operation), Is.True);
            Assert.That(pending.CommitResult, Is.Null);
            Assert.That(pending.NextRepositoryAction, Is.EqualTo(F10V7C1RepositoryAction.Retry));
            var failedReadback = F10V7SessionCommitResult.Rejected("readback failed", pending: true);
            pending.CaptureRepositoryCommitResult(failedReadback);
            Assert.That(pending.CommitResult, Is.SameAs(failedReadback));
            Assert.That(pending.ConfirmedCommitResult, Is.SameAs(confirmed));
            Assert.That(pending.RepositoryPending, Is.True);
            Assert.That(pending.NextRepositoryAction, Is.EqualTo(F10V7C1RepositoryAction.Retry));
            pending.ReleaseOperation(operation);
        }

        [Test]
        public void DistinctReadinessViewsMatchStableReferencesButRejectChangedCatalogOrReadiness()
        {
            ItemCatalog catalog = Catalog();
            ExpeditionReadinessCompositionView first = View(catalog, true);
            ExpeditionReadinessCompositionView second = View(catalog, true);
            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(F10V7C1PaymentResourceCapture.HasSameReadinessIdentity(first, second), Is.True);
            Assert.That(F10V7C1PaymentResourceCapture.HasSameReadinessIdentity(first,
                View(Catalog(), true)), Is.False);
            Assert.That(F10V7C1PaymentResourceCapture.HasSameReadinessIdentity(first,
                View(catalog, false)), Is.False);
            Assert.That(F10V7C1PaymentResourceCapture.HasSameReadinessIdentity(first, null), Is.False);
        }

        [Test]
        public void PaymentGateExemptionRequiresTheExactNonNullOwnerAndTokenPair()
        {
            object owner = new object();
            object token = new object();
            Assert.That(ApplicationBootstrapper.MatchesF10V7C1PaymentGateIdentity(
                owner, token, owner, token), Is.True);
            Assert.That(ApplicationBootstrapper.MatchesF10V7C1PaymentGateIdentity(
                owner, new object(), owner, token), Is.False);
            Assert.That(ApplicationBootstrapper.MatchesF10V7C1PaymentGateIdentity(
                new object(), token, owner, token), Is.False);
            Assert.That(ApplicationBootstrapper.MatchesF10V7C1PaymentGateIdentity(
                owner, null, owner, token), Is.False);
        }

        [Test]
        public void ConfirmedPaymentWitnessReplayRequiresTheSameRunStateAndRevision()
        {
            object witness = new object();
            object run = new object();
            var confirmed = new ExpeditionSaveData
            {
                runId = "run.confirmed",
                currentNodeId = "sample.patio.exploration"
            };

            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, run, 12, 12, confirmed, confirmed.Clone()), Is.True);
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, new object(), run, run, 12, 12, confirmed, confirmed), Is.False);
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, new object(), 12, 12, confirmed, confirmed), Is.False);
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, run, 12, 13, confirmed, confirmed), Is.False);

            ExpeditionSaveData changed = confirmed.Clone();
            changed.currentNodeId = "sample.patio.combat";
            Assert.That(ExpeditionReadinessPreparedPublication.MatchesConfirmedReplaySnapshot(
                witness, witness, run, run, 12, 12, confirmed, changed), Is.False);
        }

        // Identity-only DTO fixture: no actor, binding or physical permission is issued.
        private static ExpeditionReadinessCompositionView View(ItemCatalog catalog, bool ready) =>
            new ExpeditionReadinessCompositionView(null, null, null, null, null, null,
                null, null, null, null, null, null, null, catalog, null, null, ready);

        private static ItemCatalog Catalog() => new ItemCatalog(
            Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>().Select((kind, index) =>
                new ItemDefinition("material.identity_fixture." + index, "fixture.name", "fixture.description",
                    ItemKind.Material, 99, materialKind: kind)));
    }
}
#endif
