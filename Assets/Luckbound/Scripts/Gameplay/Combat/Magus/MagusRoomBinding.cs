using System;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Movement;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>Owns Magus combat resources for one room and notifies the actor when they end.</summary>
    internal sealed class MagusRoomBinding : IDisposable
    {
        private readonly PlayerMotor2D _owner;
        private bool _disposed;

        public MagusRoomBinding(
            PlayerMotor2D owner,
            MagusCombatAdapter2D adapter,
            RoomProjectileScope projectileScope)
        {
            _owner = owner != null ? owner : throw new ArgumentNullException(nameof(owner));
            Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
            ProjectileScope = projectileScope ?? throw new ArgumentNullException(nameof(projectileScope));
        }

        public MagusCombatAdapter2D Adapter { get; }

        public RoomProjectileScope ProjectileScope { get; }

        public bool IsDisposed => _disposed || Adapter.IsDisposed || ProjectileScope.IsDisposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Adapter.Dispose();
            ProjectileScope.Dispose();
            if (_owner != null) _owner.OnMagusRoomDisposed(this);
        }
    }
}
