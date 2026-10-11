using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Sessions
{
    public sealed class GameSession
    {
        private readonly Dictionary<ActorId, IActorState> _actors = new Dictionary<ActorId, IActorState>();
        private readonly List<ActorId> _actorOrder = new List<ActorId>();

        public GameSession()
            : this(SessionId.New())
        {
        }

        public GameSession(SessionId id)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException("A game session requires a valid session identifier.", nameof(id));
            }

            Id = id;
        }

        public SessionId Id { get; }

        public int ActorCount => _actors.Count;

        public IReadOnlyCollection<IActorState> Actors
        {
            get
            {
                return Array.AsReadOnly(_actorOrder.Select(actorId => _actors[actorId]).ToArray());
            }
        }

        public void AddActor(IActorState actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (!actor.Id.IsValid)
            {
                throw new ArgumentException("An actor must have a valid identifier.", nameof(actor));
            }

            if (_actors.ContainsKey(actor.Id))
            {
                throw new InvalidOperationException("An actor with this identifier already belongs to the session.");
            }

            _actors.Add(actor.Id, actor);
            _actorOrder.Add(actor.Id);
        }

        public bool TryGetActor(ActorId actorId, out IActorState actor)
        {
            if (!actorId.IsValid)
            {
                actor = null;
                return false;
            }

            return _actors.TryGetValue(actorId, out actor);
        }

        public IActorState GetActor(ActorId actorId)
        {
            if (TryGetActor(actorId, out IActorState actor))
            {
                return actor;
            }

            throw new KeyNotFoundException("No actor with this identifier belongs to the session.");
        }

        public bool RemoveActor(ActorId actorId)
        {
            if (!actorId.IsValid || !_actors.Remove(actorId))
            {
                return false;
            }

            _actorOrder.Remove(actorId);
            return true;
        }
    }
}
