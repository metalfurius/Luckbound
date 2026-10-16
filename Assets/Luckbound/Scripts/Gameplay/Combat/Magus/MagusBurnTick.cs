using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>One periodic Magus burn pulse forwarded into its target's ordinary impact queue.</summary>
    public readonly struct MagusBurnTick
    {
        internal MagusBurnTick(
            EntityId effectId,
            HitId hitId,
            ActorId sourceActorId,
            ActorId targetActorId,
            decimal damage,
            long tick,
            float sourcePositionX)
        {
            if (!effectId.IsValid) throw new ArgumentException("A burn pulse requires a valid effect ID.", nameof(effectId));
            if (!hitId.IsValid) throw new ArgumentException("A burn pulse requires a unique valid hit ID.", nameof(hitId));
            if (!sourceActorId.IsValid) throw new ArgumentException("A burn pulse requires a valid source actor.", nameof(sourceActorId));
            if (!targetActorId.IsValid || sourceActorId == targetActorId)
            {
                throw new ArgumentException("A burn pulse requires a distinct valid target actor.", nameof(targetActorId));
            }
            if (damage <= 0m) throw new ArgumentOutOfRangeException(nameof(damage));
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            if (float.IsNaN(sourcePositionX) || float.IsInfinity(sourcePositionX))
            {
                throw new ArgumentOutOfRangeException(nameof(sourcePositionX));
            }

            EffectId = effectId;
            HitId = hitId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Damage = damage;
            Tick = tick;
            SourcePositionX = sourcePositionX;
        }

        public EntityId EffectId { get; }

        public HitId HitId { get; }

        public ActorId SourceActorId { get; }

        public ActorId TargetActorId { get; }

        public decimal Damage { get; }

        public long Tick { get; }

        public float SourcePositionX { get; }
    }
}
