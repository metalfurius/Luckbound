using System;
using System.Collections.Generic;
using Luckbound.Core.Consumables;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ConsumableEffectTests
    {
        [Test]
        public void AntidoteClearsEveryHarmfulStatusAndProtectsForExactlySixHundredTicks()
        {
            AntidoteEffectResolution effect = AntidoteEffect.Apply(20);

            Assert.That(effect.ClearsAllHarmfulStatuses, Is.True);
            Assert.That(effect.ClearedStatuses, Is.EqualTo(HarmfulStatusKind.All));
            Assert.That(effect.Clear(HarmfulStatusKind.All), Is.EqualTo(HarmfulStatusKind.None));
            Assert.That(effect.Clear(HarmfulStatusKind.None), Is.EqualTo(HarmfulStatusKind.None));
            Assert.That(effect.ActivatedAtTick, Is.EqualTo(20));
            Assert.That(effect.ProtectionEndsAtTickExclusive, Is.EqualTo(620));
            foreach (HarmfulStatusKind status in new[]
            {
                HarmfulStatusKind.Burn,
                HarmfulStatusKind.Poison,
                HarmfulStatusKind.Slow,
                HarmfulStatusKind.Root,
                HarmfulStatusKind.Freeze,
                HarmfulStatusKind.Stun
            })
            {
                Assert.That(effect.ProtectsAt(status, 20), Is.True, status.ToString());
                Assert.That(effect.ProtectsAt(status, 619), Is.True, status.ToString());
                Assert.That(effect.ProtectsAt(status, 620), Is.False, status.ToString());
            }
            Assert.That(effect.ProtectsAt(HarmfulStatusKind.All, 100), Is.True);
            Assert.That(effect.ProtectsAt(HarmfulStatusKind.Burn, 19), Is.False);
            Assert.That(() => effect.Clear((HarmfulStatusKind)64), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void AntidoteRejectsInvalidTicksAndTickOverflow()
        {
            Assert.That(() => AntidoteEffect.Apply(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => AntidoteEffect.Apply(long.MaxValue), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => AntidoteEffect.Apply(0).ProtectsAt(HarmfulStatusKind.None, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void BombUsesInclusiveEuclideanRadiusAndReturnsUniqueSortedActorIds()
        {
            ActorId second = ActorId.Parse("00000000000000000000000000000002");
            ActorId first = ActorId.Parse("00000000000000000000000000000001");
            ActorId outside = ActorId.Parse("00000000000000000000000000000003");
            var candidates = new[]
            {
                new BombTarget(second, 1.2m, 1.6m), // Exactly on the two-unit boundary.
                new BombTarget(first, 1m, 1m),
                new BombTarget(second, 0m, 0m), // Duplicate actor / collider.
                new BombTarget(outside, 1.5m, 1.5m) // Inside the square, outside the circle.
            };

            BombEffectResolution effect = BombEffect.ResolveTargets(0m, 0m, candidates);
            BombEffectResolution reversed = BombEffect.ResolveTargets(0m, 0m, Reverse(candidates));

            Assert.That(effect.Damage, Is.EqualTo(40));
            Assert.That(effect.Radius, Is.EqualTo(2m));
            CollectionAssert.AreEqual(new[] { first, second }, effect.TargetActorIds);
            CollectionAssert.AreEqual(effect.TargetActorIds, reversed.TargetActorIds);
        }

        [Test]
        public void BombRejectsMissingCandidatesAndIncludesTheCenter()
        {
            ActorId center = ActorId.New();
            BombEffectResolution centered = BombEffect.ResolveTargets(4m, -3m, new[] { new BombTarget(center, 4m, -3m) });
            CollectionAssert.AreEqual(new[] { center }, centered.TargetActorIds);
            Assert.That(() => BombEffect.ResolveTargets(0m, 0m, null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => new BombTarget(default, 0m, 0m), Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void FortuneBrewGrantsOneChargeUpToTheExistingFortuneMaximum()
        {
            Assert.That(FortuneBrewEffect.MaximumCharges, Is.EqualTo(FortuneOfferBook.MaximumCharges));
            FortuneBrewUseResult fromZero = FortuneBrewEffect.Apply(0);
            FortuneBrewUseResult toMaximum = FortuneBrewEffect.Apply(FortuneBrewEffect.MaximumCharges - 1);
            FortuneBrewUseResult atMaximum = FortuneBrewEffect.Apply(FortuneBrewEffect.MaximumCharges);

            Assert.That(fromZero.Status, Is.EqualTo(FortuneBrewUseStatus.Granted));
            Assert.That(fromZero.PreviousCharges, Is.Zero);
            Assert.That(fromZero.CurrentCharges, Is.EqualTo(1));
            Assert.That(fromZero.WasConsumed, Is.True);
            Assert.That(toMaximum.CurrentCharges, Is.EqualTo(FortuneBrewEffect.MaximumCharges));
            Assert.That(toMaximum.WasConsumed, Is.True);
            Assert.That(atMaximum.Status, Is.EqualTo(FortuneBrewUseStatus.RejectedAtMaximum));
            Assert.That(atMaximum.PreviousCharges, Is.EqualTo(FortuneBrewEffect.MaximumCharges));
            Assert.That(atMaximum.CurrentCharges, Is.EqualTo(FortuneBrewEffect.MaximumCharges));
            Assert.That(atMaximum.WasConsumed, Is.False);
        }

        [Test]
        public void FortuneBrewRejectsChargesOutsideTheDomain()
        {
            Assert.That(() => FortuneBrewEffect.Apply(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => FortuneBrewEffect.Apply(FortuneBrewEffect.MaximumCharges + 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TorchProvidesAdditionalLightOnAnExactThreeThousandSixHundredTickInterval()
        {
            TorchLightActivation effect = TorchEffect.Activate(10);

            Assert.That(effect.DurationTicks, Is.EqualTo(3600));
            Assert.That(effect.StartsAtTick, Is.EqualTo(10));
            Assert.That(effect.EndsAtTickExclusive, Is.EqualTo(3610));
            Assert.That(effect.IsActiveAt(9), Is.False);
            Assert.That(effect.IsActiveAt(10), Is.True);
            Assert.That(effect.IsActiveAt(3609), Is.True);
            Assert.That(effect.IsActiveAt(3610), Is.False);
        }

        [Test]
        public void TorchRejectsInvalidTicksAndTickOverflow()
        {
            Assert.That(() => TorchEffect.Activate(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TorchEffect.Activate(long.MaxValue), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => TorchEffect.Activate(0).IsActiveAt(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static IEnumerable<BombTarget> Reverse(BombTarget[] targets)
        {
            for (int index = targets.Length - 1; index >= 0; index--)
                yield return targets[index];
        }
    }
}
