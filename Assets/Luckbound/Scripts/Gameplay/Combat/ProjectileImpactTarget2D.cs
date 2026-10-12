using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.Gameplay.Combat
{
    /// <summary>Actor identity adapter on a combatant root for queued projectile and melee impacts.</summary>
    public sealed class ProjectileImpactTarget2D : MonoBehaviour
    {
        private readonly Queue<ProjectileImpact> _pendingImpacts = new Queue<ProjectileImpact>();
        private readonly Queue<MeleeImpact> _pendingMeleeImpacts = new Queue<MeleeImpact>();

        public ActorId ActorId { get; private set; }

        public bool IsInitialized { get; private set; }

        public int PendingImpactCount => _pendingImpacts.Count;

        public int PendingMeleeImpactCount => _pendingMeleeImpacts.Count;

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

        internal void QueueMeleeImpact(MeleeImpact impact)
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("Melee target must be initialized before impacts can be queued.");
            }

            if (impact.TargetActorId != ActorId)
            {
                throw new ArgumentException("Melee impact belongs to a different target actor.", nameof(impact));
            }

            _pendingMeleeImpacts.Enqueue(impact);
        }

        public bool TryDequeueMeleeImpactForTick(long targetTick, out MeleeImpact impact)
        {
            if (targetTick < 0) throw new ArgumentOutOfRangeException(nameof(targetTick));
            if (_pendingMeleeImpacts.Count == 0 || _pendingMeleeImpacts.Peek().QueuedAtTick >= targetTick)
            {
                impact = default;
                return false;
            }

            impact = _pendingMeleeImpacts.Dequeue();
            return true;
        }

        public void ClearPendingImpacts()
        {
            _pendingImpacts.Clear();
            _pendingMeleeImpacts.Clear();
        }
    }
}
