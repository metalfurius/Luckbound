using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Gameplay.Combat
{
    /// <summary>Reusable projectile instances shared by rooms; a room scope owns each active rental.</summary>
    public sealed class ProjectilePool2D : MonoBehaviour
    {
        private readonly Stack<Projectile2D> _available = new Stack<Projectile2D>();
        private GameObject _projectilePrefab;
        private bool _initialized;

        public int PooledCount => _available.Count;

        public void Initialize(GameObject projectilePrefab)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("ProjectilePool2D can only be initialized once.");
            }

            if (projectilePrefab == null || projectilePrefab.GetComponent<Projectile2D>() == null ||
                projectilePrefab.GetComponent<Rigidbody2D>() == null || projectilePrefab.GetComponent<Collider2D>() == null)
            {
                throw new ArgumentException("Projectile prefab requires Projectile2D, Rigidbody2D, and Collider2D components.", nameof(projectilePrefab));
            }

            _projectilePrefab = projectilePrefab;
            _initialized = true;
        }

        public Projectile2D Spawn(
            ProjectileDefinition definition,
            EntityId projectileId,
            ActorId sourceActorId,
            Vector2 position,
            Vector2 direction,
            LayerMask impactLayers,
            RoomProjectileScope roomScope)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("ProjectilePool2D must be initialized before it can spawn.");
            }

            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!projectileId.IsValid) throw new ArgumentException("Projectile requires a valid entity ID.", nameof(projectileId));
            if (!sourceActorId.IsValid) throw new ArgumentException("Projectile requires a valid source actor.", nameof(sourceActorId));
            if (direction.sqrMagnitude <= 0.000001f) throw new ArgumentOutOfRangeException(nameof(direction));
            if (impactLayers.value == 0) throw new ArgumentException("Projectile requires at least one impact layer.", nameof(impactLayers));
            if (roomScope == null) throw new ArgumentNullException(nameof(roomScope));
            if (roomScope.IsDisposed || roomScope.Pool != this) throw new InvalidOperationException("Projectile must be owned by a live room scope using this pool.");

            Projectile2D projectile = Rent();
            projectile.transform.SetParent(roomScope.transform, worldPositionStays: true);
            projectile.transform.position = position;
            // Awake must run before Initialize, and Unity must have activated the body
            // before its initial velocity is assigned; inactive pooled bodies can reset it.
            projectile.gameObject.SetActive(true);
            projectile.Initialize(this, roomScope, definition, projectileId, sourceActorId, direction, impactLayers);
            roomScope.Register(projectile);
            return projectile;
        }

        internal void Return(Projectile2D projectile)
        {
            if (projectile == null || projectile.IsPooled)
            {
                return;
            }

            RoomProjectileScope owner = projectile.RoomScope;
            if (owner != null)
            {
                owner.Unregister(projectile);
            }

            projectile.ResetForPool();
            projectile.transform.SetParent(transform, worldPositionStays: false);
            projectile.gameObject.SetActive(false);
            _available.Push(projectile);
        }

        private Projectile2D Rent()
        {
            while (_available.Count > 0)
            {
                Projectile2D pooled = _available.Pop();
                if (pooled != null)
                {
                    return pooled;
                }
            }

            GameObject instance = Instantiate(_projectilePrefab, transform);
            instance.name = _projectilePrefab.name + " (Pooled)";
            instance.SetActive(false);
            return instance.GetComponent<Projectile2D>();
        }
    }
}
