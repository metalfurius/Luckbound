using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Combat
{
    /// <summary>Physics impact record emitted at collection time for resolution on the next simulation tick.</summary>
    public readonly struct ProjectileImpact
    {
        public ProjectileImpact(EntityId projectileId, HitId hitId, ActorId sourceActorId, ActorId targetActorId, decimal baseDamage)
        {
            if (!projectileId.IsValid) throw new ArgumentException("Projectile impact requires a valid entity ID.", nameof(projectileId));
            if (!hitId.IsValid) throw new ArgumentException("Projectile impact requires a valid hit ID.", nameof(hitId));
            if (!sourceActorId.IsValid) throw new ArgumentException("Projectile impact requires a valid source actor.", nameof(sourceActorId));
            if (!targetActorId.IsValid) throw new ArgumentException("Projectile impact requires a valid target actor.", nameof(targetActorId));
            if (baseDamage <= 0m) throw new ArgumentOutOfRangeException(nameof(baseDamage));

            ProjectileId = projectileId;
            HitId = hitId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            BaseDamage = baseDamage;
        }

        public EntityId ProjectileId { get; }

        public HitId HitId { get; }

        public ActorId SourceActorId { get; }

        public ActorId TargetActorId { get; }

        public decimal BaseDamage { get; }
    }
}
