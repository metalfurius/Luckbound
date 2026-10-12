using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Identifiers;
using UnityEngine;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Gameplay.Combat
{
    /// <summary>Room-owned set of rented projectiles; disposing the room returns every active projectile.</summary>
    public sealed class RoomProjectileScope : MonoBehaviour, IDisposable
    {
        private readonly HashSet<Projectile2D> _projectiles = new HashSet<Projectile2D>();
        private bool _initialized;
        private bool _disposed;
        private EntityId _roomEntityId;
        private ProjectilePool2D _pool;

        public EntityId RoomEntityId => _roomEntityId;

        public ProjectilePool2D Pool => _pool;

        public int ActiveProjectileCount => _projectiles.Count;

        public bool IsDisposed => _disposed;

        public void Initialize(EntityId roomEntityId, ProjectilePool2D pool)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("RoomProjectileScope can only be initialized once.");
            }

            if (!roomEntityId.IsValid)
            {
                throw new ArgumentException("Projectile scope requires a valid room entity ID.", nameof(roomEntityId));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            _roomEntityId = roomEntityId;
            _pool = pool;
            _initialized = true;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Projectile2D[] active = _projectiles.ToArray();
            for (int index = 0; index < active.Length; index++)
            {
                if (active[index] != null)
                {
                    _pool.Return(active[index]);
                }
            }

            _projectiles.Clear();
        }

        internal void Register(Projectile2D projectile)
        {
            if (!_initialized || _disposed)
            {
                throw new InvalidOperationException("Cannot add projectiles to an uninitialized or disposed room scope.");
            }

            if (projectile == null || !_projectiles.Add(projectile))
            {
                throw new InvalidOperationException("Room scope requires a unique, non-null projectile.");
            }
        }

        internal void Unregister(Projectile2D projectile)
        {
            if (projectile != null)
            {
                _projectiles.Remove(projectile);
            }
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
