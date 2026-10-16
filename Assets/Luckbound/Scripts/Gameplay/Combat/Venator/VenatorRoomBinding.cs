using System;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.Gameplay.Combat.Venator
{
    /// <summary>Room-owned Venator resources. Disposing a room cancels preparation and returns effects.</summary>
    internal sealed class VenatorRoomBinding : IDisposable
    {
        private readonly PlayerMotor2D _owner;
        private bool _disposed;

        public VenatorRoomBinding(
            PlayerMotor2D owner,
            RoomProjectileScope projectileScope,
            VenatorRootTrapPool2D rootTrapPool)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            ProjectileScope = projectileScope != null ? projectileScope : throw new ArgumentNullException(nameof(projectileScope));
            RootTrapPool = rootTrapPool != null ? rootTrapPool : throw new ArgumentNullException(nameof(rootTrapPool));
        }

        public RoomProjectileScope ProjectileScope { get; }

        public VenatorRootTrapPool2D RootTrapPool { get; }

        public bool IsDisposed => _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            RootTrapPool.Dispose();
            ProjectileScope.Dispose();
            if (_owner != null) _owner.OnVenatorRoomDisposed(this);
        }
    }
}
