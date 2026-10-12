using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Gameplay.Combat
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Projectile2D : MonoBehaviour
    {
        private readonly HashSet<ActorId> _registeredTargets = new HashSet<ActorId>();
        private Rigidbody2D _body;
        private Collider2D _collider;
        private ProjectileDefinition _definition;
        private ProjectilePool2D _pool;
        private RoomProjectileScope _roomScope;
        private EntityId _projectileId;
        private ActorId _sourceActorId;
        private HitId _hitId;
        private LayerMask _impactLayers;
        private int _ageTicks;
        private bool _initialized;
        private bool _returnPending;

        public event Action<ProjectileImpact> Impacted;

        public EntityId ProjectileId => _projectileId;

        public ActorId SourceActorId => _sourceActorId;

        public HitId HitId => _hitId;

        public int AgeTicks => _ageTicks;

        public int RegisteredTargetCount => _registeredTargets.Count;

        public bool IsPooled { get; private set; } = true;

        public RoomProjectileScope RoomScope => _roomScope;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
        }

        internal void Initialize(
            ProjectilePool2D pool,
            RoomProjectileScope roomScope,
            ProjectileDefinition definition,
            EntityId projectileId,
            ActorId sourceActorId,
            Vector2 direction,
            LayerMask impactLayers)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("A rented projectile must be reset before it is initialized again.");
            }

            if (_body == null) _body = GetComponent<Rigidbody2D>();
            if (_collider == null) _collider = GetComponent<Collider2D>();
            if (_body == null || _collider == null)
            {
                throw new InvalidOperationException("Projectile2D requires a Rigidbody2D and Collider2D.");
            }

            _pool = pool;
            _roomScope = roomScope;
            _definition = definition;
            _projectileId = projectileId;
            _sourceActorId = sourceActorId;
            _hitId = HitId.New();
            _impactLayers = impactLayers;
            _ageTicks = 0;
            _returnPending = false;
            _registeredTargets.Clear();
            _initialized = true;
            IsPooled = false;

            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.linearVelocity = direction.normalized * (float)definition.SpeedUnitsPerSecond;
            _collider.isTrigger = true;
        }

        internal void ResetForPool()
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector2.zero;
                _body.angularVelocity = 0f;
            }

            _registeredTargets.Clear();
            Impacted = null;
            _definition = null;
            _pool = null;
            _roomScope = null;
            _projectileId = default;
            _sourceActorId = default;
            _hitId = default;
            _impactLayers = default;
            _ageTicks = 0;
            _returnPending = false;
            _initialized = false;
            IsPooled = true;
        }

        private void FixedUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            if (_returnPending || ++_ageTicks >= _definition.LifetimeTicks)
            {
                _pool.Return(this);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!_initialized || _returnPending || other == null ||
                (_impactLayers.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }

            ProjectileImpactTarget2D target = other.GetComponentInParent<ProjectileImpactTarget2D>();
            if (target == null || !target.IsInitialized)
            {
                _returnPending = true;
                return;
            }

            if (target.ActorId == _sourceActorId || !_registeredTargets.Add(target.ActorId))
            {
                return;
            }

            ProjectileDefinition definition = _definition;
            Impacted?.Invoke(new ProjectileImpact(
                _projectileId,
                _hitId,
                _sourceActorId,
                target.ActorId,
                definition.BaseDamage));

            if (!_initialized || _returnPending)
            {
                return;
            }

            if (_registeredTargets.Count >= definition.MaximumTargets)
            {
                _returnPending = true;
            }
        }
    }
}
