using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Gameplay.Combat
{
    /// <summary>Player melee hit collected during one simulation tick and resolved by the target on a later tick.</summary>
    public readonly struct MeleeImpact
    {
        public MeleeImpact(HitId hitId, ActorId sourceActorId, ActorId targetActorId, decimal damage, float sourcePositionX, long queuedAtTick)
        {
            if (!hitId.IsValid) throw new ArgumentException("Melee impact requires a valid hit ID.", nameof(hitId));
            if (!sourceActorId.IsValid) throw new ArgumentException("Melee impact requires a valid source actor.", nameof(sourceActorId));
            if (!targetActorId.IsValid) throw new ArgumentException("Melee impact requires a valid target actor.", nameof(targetActorId));
            if (sourceActorId == targetActorId) throw new ArgumentException("Melee impact source and target must differ.");
            if (damage <= 0m) throw new ArgumentOutOfRangeException(nameof(damage));
            if (float.IsNaN(sourcePositionX) || float.IsInfinity(sourcePositionX)) throw new ArgumentOutOfRangeException(nameof(sourcePositionX));
            if (queuedAtTick < 0) throw new ArgumentOutOfRangeException(nameof(queuedAtTick));

            HitId = hitId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Damage = damage;
            SourcePositionX = sourcePositionX;
            QueuedAtTick = queuedAtTick;
        }

        public HitId HitId { get; }
        public ActorId SourceActorId { get; }
        public ActorId TargetActorId { get; }
        public decimal Damage { get; }
        public float SourcePositionX { get; }
        public long QueuedAtTick { get; }
    }
}
