using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>Core-approved basic attack request for the motor's equipped-weapon projectile adapter.</summary>
    public readonly struct MagusBasicAttackRequest
    {
        public MagusBasicAttackRequest(ActorId actorId, MagusBasicAttackSnapshot snapshot, Vector2 origin, Vector2 direction)
        {
            ActorId = actorId;
            Snapshot = snapshot;
            Origin = origin;
            Direction = direction;
        }

        public ActorId ActorId { get; }

        public MagusBasicAttackSnapshot Snapshot { get; }

        public Vector2 Origin { get; }

        public Vector2 Direction { get; }
    }
}
