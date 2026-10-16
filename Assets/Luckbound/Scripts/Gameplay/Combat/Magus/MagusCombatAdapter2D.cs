using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>
    /// Executes Core Magus activations against Unity 2D colliders. The player motor calls Tick once
    /// per simulation command and consumes the returned basic-attack snapshot for movement state.
    /// </summary>
    public sealed class MagusCombatAdapter2D : IDisposable
    {
        private readonly ActorId _actorId;
        private readonly MagusCombatState _combatState;
        private readonly ProjectilePool2D _projectilePool;
        private readonly RoomProjectileScope _projectileScope;
        private readonly MagusRoomEffectsScope2D _roomEffects;
        private readonly LayerMask _targetLayers;
        private long _lastTick = -1;
        private bool _disposed;

        public MagusCombatAdapter2D(
            ActorId actorId,
            MagusCombatState combatState,
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope,
            RoomLifetimeScope roomLifetimeScope,
            LayerMask targetLayers)
        {
            if (!actorId.IsValid) throw new ArgumentException("Magus gameplay requires a valid actor ID.", nameof(actorId));
            _actorId = actorId;
            _combatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
            _projectilePool = projectilePool ?? throw new ArgumentNullException(nameof(projectilePool));
            _projectileScope = projectileScope ?? throw new ArgumentNullException(nameof(projectileScope));
            if (_projectileScope.IsDisposed) throw new ArgumentException("Magus gameplay requires an active room projectile scope.", nameof(projectileScope));
            if (roomLifetimeScope == null) throw new ArgumentNullException(nameof(roomLifetimeScope));
            if (roomLifetimeScope.IsDisposed) throw new ArgumentException("Magus gameplay requires an active room lifetime scope.", nameof(roomLifetimeScope));
            if (targetLayers.value == 0) throw new ArgumentException("Magus gameplay requires at least one target layer.", nameof(targetLayers));

            _targetLayers = targetLayers;
            _roomEffects = new MagusRoomEffectsScope2D(actorId, targetLayers);
            roomLifetimeScope.OwnResource(_roomEffects);
        }

        /// <summary>
        /// Invoked once for each accepted Core basic-attack start. The motor supplies its equipped
        /// weapon data to create the physical projectile; this adapter does not invent travel speed.
        /// </summary>
        public event Action<MagusBasicAttackRequest> BasicAttackRequested;

        public int ActiveArcaneSealCount => _roomEffects.ActiveSealCount;

        public long LastTick => _lastTick;

        public bool IsDisposed => _disposed;

        /// <summary>Consumes one simulated player command and executes any resulting Magus effect.</summary>
        public MagusCombatResult Tick(
            PlayerCommand command,
            bool isAlive,
            bool interrupted,
            Vector2 actorPosition,
            Vector2 projectileOrigin,
            Vector2 aimDirection)
        {
            EnsureActive();
            if (command.ActorId != _actorId) throw new ArgumentException("Player command belongs to a different Magus actor.", nameof(command));
            if (command.Tick < 0 || command.Tick <= _lastTick)
            {
                throw new InvalidOperationException("Magus gameplay must consume one command per increasing simulation tick.");
            }

            ValidateVector(actorPosition, nameof(actorPosition));
            ValidateVector(projectileOrigin, nameof(projectileOrigin));
            ValidateVector(aimDirection, nameof(aimDirection));
            if (aimDirection.sqrMagnitude <= 0.000001f) throw new ArgumentOutOfRangeException(nameof(aimDirection));

            _lastTick = command.Tick;
            _roomEffects.Tick(command.Tick);
            MagusCombatResult result = _combatState.Tick(command, isAlive, interrupted);

            if (result.SkillActivation != null)
            {
                ExecuteSkill(result.SkillActivation, command.Tick, actorPosition, projectileOrigin, aimDirection.normalized);
            }

            if (result.BasicAttack.WasStartedThisTick)
            {
                BasicAttackRequested?.Invoke(new MagusBasicAttackRequest(
                    _actorId,
                    result.BasicAttack,
                    projectileOrigin,
                    aimDirection.normalized));
            }

            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _roomEffects.Dispose();
            BasicAttackRequested = null;
        }

        private void ExecuteSkill(
            MagusSkillActivation activation,
            long tick,
            Vector2 actorPosition,
            Vector2 projectileOrigin,
            Vector2 aimDirection)
        {
            switch (activation.EffectKind)
            {
                case SkillEffectKind.ExplosiveProjectile:
                    SpawnFireball(activation.Fireball, projectileOrigin, aimDirection);
                    break;
                case SkillEffectKind.FrostNova:
                    _roomEffects.ApplyFrostNova(activation.FrostNova, actorPosition, actorPosition.x, tick);
                    break;
                case SkillEffectKind.DamageZone:
                    _roomEffects.AddArcaneSeal(activation.ArcaneSeal, actorPosition, actorPosition.x, tick);
                    break;
                default:
                    throw new InvalidOperationException("Magus Core emitted an unsupported gameplay effect.");
            }
        }

        private void SpawnFireball(MagusFireballActivation activation, Vector2 origin, Vector2 direction)
        {
            Projectile2D projectile = _projectilePool.Spawn(
                activation.Projectile,
                EntityId.New(),
                _actorId,
                origin,
                direction,
                _targetLayers,
                _projectileScope);

            Action<ProjectileImpact> callback = null;
            callback = impact =>
            {
                try
                {
                    if (!_disposed && !_roomEffects.IsDisposed)
                    {
                        _roomEffects.ResolveFireballImpact(projectile, activation, impact, _lastTick);
                    }
                }
                finally
                {
                    _roomEffects.UntrackFireball(projectile, callback);
                }
            };
            _roomEffects.TrackFireball(projectile, callback);
        }

        private static void ValidateVector(Vector2 value, string parameterName)
        {
            if (float.IsNaN(value.x) || float.IsInfinity(value.x) || float.IsNaN(value.y) || float.IsInfinity(value.y))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Magus gameplay positions and aim must be finite.");
            }
        }

        private void EnsureActive()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MagusCombatAdapter2D));
        }
    }
}
