using System;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Sessions;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ActorSessionTests
    {
        [Test]
        public void SessionKeepsTwoActorsAndTheirMutableStateIndependent()
        {
            var session = new GameSession(new SessionId(Guid.Parse("90000000-0000-4000-8000-000000000001")));
            var first = new TestActorState(new ActorId(Guid.Parse("10000000-0000-4000-8000-000000000001")), 80);
            var second = new TestActorState(new ActorId(Guid.Parse("20000000-0000-4000-8000-000000000002")), 35);

            session.AddActor(first);
            session.AddActor(second);
            first.Health -= 20;

            Assert.That(session.Id.ToString(), Is.EqualTo("90000000000040008000000000000001"));
            Assert.That(session.ActorCount, Is.EqualTo(2));
            Assert.That(session.GetActor(first.Id), Is.SameAs(first));
            Assert.That(session.GetActor(second.Id), Is.SameAs(second));
            Assert.That(first.Health, Is.EqualTo(60));
            Assert.That(second.Health, Is.EqualTo(35));
            Assert.That(session.Actors, Has.Count.EqualTo(2));
            Assert.That(session.Actors, Is.EqualTo(new IActorState[] { first, second }));
        }

        [Test]
        public void DuplicateActorIdDoesNotReplaceSessionOwner()
        {
            var session = new GameSession();
            var id = new ActorId(Guid.Parse("10000000-0000-4000-8000-000000000001"));
            var original = new TestActorState(id, 80);
            var duplicate = new TestActorState(id, 10);

            session.AddActor(original);

            Assert.That(() => session.AddActor(duplicate), Throws.InvalidOperationException);
            Assert.That(session.GetActor(id), Is.SameAs(original));
            Assert.That(session.ActorCount, Is.EqualTo(1));
        }

        [Test]
        public void SessionRejectsInvalidIdentifiersAndActorStates()
        {
            Assert.That(default(SessionId).IsValid, Is.False);
            Assert.That(() => new GameSession(default), Throws.ArgumentException);

            var session = new GameSession();
            Assert.That(() => session.AddActor(new TestActorState(default, 100)), Throws.ArgumentException);
            Assert.That(() => session.AddActor(null), Throws.ArgumentNullException);
            Assert.That(session.TryGetActor(default, out _), Is.False);
            Assert.That(session.RemoveActor(default), Is.False);
        }

        [Test]
        public void RemovingActorReleasesOnlyItsSessionEntry()
        {
            var session = new GameSession();
            var first = new TestActorState(new ActorId(Guid.Parse("20000000-0000-4000-8000-000000000002")), 80);
            var second = new TestActorState(new ActorId(Guid.Parse("10000000-0000-4000-8000-000000000001")), 35);
            session.AddActor(first);
            session.AddActor(second);

            Assert.That(session.RemoveActor(first.Id), Is.True);
            Assert.That(session.RemoveActor(first.Id), Is.False);
            Assert.That(session.TryGetActor(first.Id, out _), Is.False);
            Assert.That(session.GetActor(second.Id), Is.SameAs(second));
            Assert.That(session.Actors, Is.EqualTo(new IActorState[] { second }));
            Assert.That(session.ActorCount, Is.EqualTo(1));
        }

        [Test]
        public void TypedIdsRoundTripCanonicalNonEmptyGuids()
        {
            const string actorText = "abcdefabcdef4abc8defabcdefabcdef";
            Assert.That(ActorId.TryParse(actorText, out ActorId actor), Is.True);
            Assert.That(actor.ToString(), Is.EqualTo(actorText));
            Assert.That(ActorId.Parse(actorText), Is.EqualTo(actor));
            Assert.That(ActorId.TryParse("00000000000000000000000000000000", out ActorId empty), Is.False);
            Assert.That(empty.IsValid, Is.False);
            Assert.That(default(ActorId).ToString(), Is.Empty);
            Assert.That(default(SessionId).IsValid, Is.False);
            Assert.That(default(EntityId).IsValid, Is.False);
            Assert.That(default(RunId).IsValid, Is.False);
            Assert.That(default(RoomInstanceId).IsValid, Is.False);
            Assert.That(default(ItemInstanceId).IsValid, Is.False);
            Assert.That(default(TransactionId).IsValid, Is.False);
            Assert.That(TransactionId.TryParse("not-an-id", out _), Is.False);

            EntityId entity = EntityId.Parse(actorText);
            SessionId session = SessionId.Parse(actorText);
            RunId run = RunId.Parse(actorText);
            RoomInstanceId room = RoomInstanceId.Parse(actorText);
            ItemInstanceId item = ItemInstanceId.Parse(actorText);
            TransactionId transaction = TransactionId.Parse(actorText);
            Assert.That(entity.ToString(), Is.EqualTo(actorText));
            Assert.That(session.ToString(), Is.EqualTo(actorText));
            Assert.That(run.ToString(), Is.EqualTo(actorText));
            Assert.That(room.ToString(), Is.EqualTo(actorText));
            Assert.That(item.ToString(), Is.EqualTo(actorText));
            Assert.That(transaction.ToString(), Is.EqualTo(actorText));
            var identifierTypes = new System.Collections.Generic.HashSet<Type>
            {
                typeof(SessionId), typeof(ActorId), typeof(EntityId), typeof(RunId),
                typeof(RoomInstanceId), typeof(ItemInstanceId), typeof(TransactionId)
            };
            Assert.That(identifierTypes.Count, Is.EqualTo(7));
        }

        private sealed class TestActorState : IActorState
        {
            public TestActorState(ActorId id, int health)
            {
                Id = id;
                Health = health;
            }

            public ActorId Id { get; }

            public int Health { get; set; }
        }
    }
}
