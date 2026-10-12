using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.Gameplay.Combat
{
    /// <summary>Actor identity adapter placed on a combatant root for projectile collider resolution.</summary>
    public sealed class ProjectileImpactTarget2D : MonoBehaviour
    {
        private readonly Queue<ProjectileImpact> _pendingImpacts = new Queue<ProjectileImpact>();

        public ActorId ActorId { get; private set; }

        public bool IsInitialized { get; private set; }

        public int PendingImpactCount => _pendingImpacts.Count;

        public void Initialize(ActorId actorId)
        {
            if (IsInitialized)
            {
                throw new InvalidOperationException("ProjectileImpactTarget2D can only be initialized once.");
            }

            if (!actorId.IsValid)
            {
                throw new ArgumentException("Projectile target requires a valid actor identifier.", nameof(actorId));
            }

            ActorId = actorId;
            IsInitialized = true;
        }

        internal void QueueImpact(ProjectileImpact impact)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Projectile target must be initialized before impacts can be queued.");
            }

            if (impact.TargetActorId != ActorId)
            {
                throw new ArgumentException("Projectile impact belongs to a different target actor.", nameof(impact));
            }

            _pendingImpacts.Enqueue(impact);
        }

        public bool TryDequeueImpact(out ProjectileImpact impact)
        {
            if (_pendingImpacts.Count == 0)
            {
                impact = default;
                return false;
            }

            impact = _pendingImpacts.Dequeue();
            return true;
        }

        public void ClearPendingImpacts()
        {
            _pendingImpacts.Clear();
        }
    }
}
