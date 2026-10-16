using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;

namespace Luckbound.Gameplay.Combat.Venator
{
    /// <summary>Room-local pool for Root Trap instances, owned by the room lifetime scope.</summary>
    public sealed class VenatorRootTrapPool2D : MonoBehaviour, IDisposable
    {
        private readonly Stack<VenatorRootTrap2D> _available = new Stack<VenatorRootTrap2D>();
        private readonly HashSet<VenatorRootTrap2D> _active = new HashSet<VenatorRootTrap2D>();
        private ActorId _sourceActorId;
        private LayerMask _impactLayers;
        private bool _initialized;
        private bool _disposed;

        public int ActiveTrapCount => _active.Count;

        public int PooledTrapCount => _available.Count;

        public bool IsDisposed => _disposed;

        internal void Initialize(ActorId sourceActorId, LayerMask impactLayers)
        {
            if (_initialized) throw new InvalidOperationException("VenatorRootTrapPool2D can only be initialized once.");
            if (!sourceActorId.IsValid) throw new ArgumentException("Root Trap requires a valid source actor.", nameof(sourceActorId));
            if (impactLayers.value == 0) throw new ArgumentException("Root Trap requires at least one target layer.", nameof(impactLayers));

            _sourceActorId = sourceActorId;
            _impactLayers = impactLayers;
            _initialized = true;
        }

        internal VenatorRootTrap2D Spawn(VenatorRootTrapActivation activation, Vector2 position)
        {
            if (!_initialized || _disposed) throw new InvalidOperationException("Root Trap pool must belong to a live room.");
            if (activation.BaseDamage <= 0m || activation.LifetimeTicks <= 0 || activation.RootDurationTicks <= 0)
            {
                throw new ArgumentException("Root Trap activation data must contain positive damage and durations.", nameof(activation));
            }
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y))
            {
                throw new ArgumentOutOfRangeException(nameof(position), "Root Trap position must be finite.");
            }

            VenatorRootTrap2D trap = Rent();
            trap.transform.position = position;
            trap.Initialize(
                this,
                _sourceActorId,
                _impactLayers,
                activation,
                EntityId.New(),
                HitId.New());
            trap.gameObject.SetActive(true);
            _active.Add(trap);
            return trap;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            VenatorRootTrap2D[] active = new VenatorRootTrap2D[_active.Count];
            _active.CopyTo(active);
            for (int index = 0; index < active.Length; index++)
            {
                if (active[index] != null) Return(active[index]);
            }

            _active.Clear();
            _available.Clear();
        }

        internal void Return(VenatorRootTrap2D trap)
        {
            if (trap == null || !_active.Remove(trap)) return;

            trap.ResetForPool();
            trap.gameObject.SetActive(false);
            if (!_disposed) _available.Push(trap);
        }

        private VenatorRootTrap2D Rent()
        {
            while (_available.Count > 0)
            {
                VenatorRootTrap2D pooled = _available.Pop();
                if (pooled != null) return pooled;
            }

            var trapObject = new GameObject("Venator Root Trap");
            trapObject.transform.SetParent(transform, worldPositionStays: false);
            Rigidbody2D body = trapObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            CircleCollider2D collider = trapObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.65f;
            collider.isTrigger = true;
            VenatorRootTrap2D trap = trapObject.AddComponent<VenatorRootTrap2D>();
            trapObject.SetActive(false);
            return trap;
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
