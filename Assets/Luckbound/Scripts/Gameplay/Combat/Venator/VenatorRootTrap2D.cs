using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using UnityEngine;

namespace Luckbound.Gameplay.Combat.Venator
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public sealed class VenatorRootTrap2D : MonoBehaviour
    {
        private readonly HashSet<ActorId> _registeredTargets = new HashSet<ActorId>();
        private VenatorRootTrapPool2D _pool;
        private decimal _baseDamage;
        private int _lifetimeTicks;
        private ActorId _sourceActorId;
        private EntityId _entityId;
        private HitId _hitId;
        private LayerMask _impactLayers;
        private int _rootDurationTicks;
        private int _ageTicks;
        private bool _initialized;

        public int AgeTicks => _ageTicks;

        public int RegisteredTargetCount => _registeredTargets.Count;

        public bool IsActive { get; private set; }

        internal void Initialize(
            VenatorRootTrapPool2D pool,
            ActorId sourceActorId,
            LayerMask impactLayers,
            VenatorRootTrapActivation activation,
            EntityId entityId,
            HitId hitId)
        {
            if (_initialized) throw new System.InvalidOperationException("A pooled Root Trap must be reset before reuse.");

            _pool = pool;
            _sourceActorId = sourceActorId;
            _impactLayers = impactLayers;
            _baseDamage = activation.BaseDamage;
            _lifetimeTicks = activation.LifetimeTicks;
            _rootDurationTicks = activation.RootDurationTicks;
            _entityId = entityId;
            _hitId = hitId;
            _ageTicks = 0;
            _registeredTargets.Clear();
            _initialized = true;
            IsActive = true;

            Rigidbody2D body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.enabled = true;
        }

        internal void ResetForPool()
        {
            _registeredTargets.Clear();
            _pool = null;
            _baseDamage = 0m;
            _lifetimeTicks = 0;
            _sourceActorId = default;
            _entityId = default;
            _hitId = default;
            _impactLayers = default;
            _rootDurationTicks = 0;
            _ageTicks = 0;
            _initialized = false;
            IsActive = false;
            CircleCollider2D collider = GetComponent<CircleCollider2D>();
            if (collider != null) collider.enabled = false;
        }

        private void FixedUpdate()
        {
            if (!_initialized || Time.timeScale <= 0f) return;

            if (++_ageTicks >= _lifetimeTicks)
            {
                _pool.Return(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || !IsActive || other == null ||
                (_impactLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            ProjectileImpactTarget2D target = other.GetComponentInParent<ProjectileImpactTarget2D>();
            if (target == null || !target.IsInitialized || target.ActorId == _sourceActorId ||
                !_registeredTargets.Add(target.ActorId))
            {
                return;
            }

            Rigidbody2D targetBody = target.GetComponent<Rigidbody2D>();
            if (targetBody == null) targetBody = target.GetComponentInParent<Rigidbody2D>();
            VenatorRootedMovement2D.Apply(targetBody, _rootDurationTicks);

            target.QueueImpact(new ProjectileImpact(
                _entityId,
                _hitId,
                _sourceActorId,
                target.ActorId,
                _baseDamage,
                transform.position.x));
        }
    }
}
