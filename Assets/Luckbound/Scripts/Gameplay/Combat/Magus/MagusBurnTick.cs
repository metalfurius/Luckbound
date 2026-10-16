using Luckbound.Core.Identifiers;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>One periodic Magus burn pulse for an enemy health adapter to resolve.</summary>
    public readonly struct MagusBurnTick
    {
        internal MagusBurnTick(
            HitId hitId,
            ActorId sourceActorId,
            ActorId targetActorId,
            decimal damage,
            long tick,
            float sourcePositionX)
        {
            HitId = hitId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Damage = damage;
            Tick = tick;
            SourcePositionX = sourcePositionX;
        }

        public HitId HitId { get; }

        public ActorId SourceActorId { get; }

        public ActorId TargetActorId { get; }

        public decimal Damage { get; }

        public long Tick { get; }

        public float SourcePositionX { get; }
    }
}
