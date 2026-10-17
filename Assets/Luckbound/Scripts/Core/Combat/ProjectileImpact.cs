using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Combat
{
    /// <summary>Physics impact record emitted at collection time for resolution on the next simulation tick.</summary>
    public readonly struct ProjectileImpact
    {
        public ProjectileImpact(
            EntityId projectileId,
            HitId hitId,
            ActorId sourceActorId,
            ActorId targetActorId,
            decimal baseDamage,
            float sourcePositionX,
            int defensePenetration = 0,
            int slowPercent = 0,
            int slowDurationTicks = 0)
        {
            if (!projectileId.IsValid) throw new ArgumentException("Projectile impact requires a valid entity ID.", nameof(projectileId));
            if (!hitId.IsValid) throw new ArgumentException("Projectile impact requires a valid hit ID.", nameof(hitId));
            if (!sourceActorId.IsValid) throw new ArgumentException("Projectile impact requires a valid source actor.", nameof(sourceActorId));
            if (!targetActorId.IsValid) throw new ArgumentException("Projectile impact requires a valid target actor.", nameof(targetActorId));
            if (baseDamage <= 0m) throw new ArgumentOutOfRangeException(nameof(baseDamage));
            if (float.IsNaN(sourcePositionX) || float.IsInfinity(sourcePositionX))
            {
                throw new ArgumentOutOfRangeException(nameof(sourcePositionX), "Impact source position must be finite.");
            }
            if (defensePenetration < 0) throw new ArgumentOutOfRangeException(nameof(defensePenetration));
            if (slowPercent < 0 || slowPercent > 100) throw new ArgumentOutOfRangeException(nameof(slowPercent));
            if ((slowPercent == 0) != (slowDurationTicks == 0) || slowDurationTicks < 0)
                throw new ArgumentException("Projectile impact slow requires both a positive percent and duration, or neither.");

            ProjectileId = projectileId;
            HitId = hitId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            BaseDamage = baseDamage;
            SourcePositionX = sourcePositionX;
            DefensePenetration = defensePenetration;
            SlowPercent = slowPercent;
            SlowDurationTicks = slowDurationTicks;
        }

        public EntityId ProjectileId { get; }

        public HitId HitId { get; }

        public ActorId SourceActorId { get; }

        public ActorId TargetActorId { get; }

        public decimal BaseDamage { get; }

        /// <summary>Horizontal point where the projectile was launched.</summary>
        public float SourcePositionX { get; }

        public int DefensePenetration { get; }

        public int SlowPercent { get; }

        public int SlowDurationTicks { get; }
    }
}
