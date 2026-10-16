using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>
    /// Magus-side status contract attached to an actor that already has a projectile impact target.
    /// Enemy controllers forward burn ticks into the actor's ordinary projectile-impact queue.
    /// </summary>
    public sealed class MagusStatusEffectTarget2D : MonoBehaviour
    {
        private readonly Queue<MagusBurnTick> _pendingBurnTicks = new Queue<MagusBurnTick>();
        private MagusStatusState _state = new MagusStatusState();
        private ProjectileImpactTarget2D _impactTarget;
        private EntityId _burnEffectId;
        private ActorId _burnSourceActorId;
        private float _burnSourcePositionX;
        private bool _initialized;
        private long _lastTick = -1;
        private bool _isFrozen;

        public ActorId ActorId { get; private set; }

        public bool IsInitialized => _initialized;

        public bool IsFrozen => _isFrozen;

        public bool IsFreezeProtected => StatusSnapshot.IsFreezeProtected;

        public MagusStatusSnapshot StatusSnapshot { get; private set; }

        public int PendingBurnTickCount => _pendingBurnTicks.Count;

        public void Initialize(ProjectileImpactTarget2D impactTarget)
        {
            if (_initialized) throw new InvalidOperationException("Magus status target can only be initialized once.");
            if (impactTarget == null || !impactTarget.IsInitialized)
            {
                throw new ArgumentException("Magus status target requires an initialized projectile impact target.", nameof(impactTarget));
            }

            _impactTarget = impactTarget;
            ActorId = impactTarget.ActorId;
            _initialized = true;
        }

        public void ApplyBurn(long tick, ActorId sourceActorId, decimal damagePerSecond, int durationTicks, float sourcePositionX)
        {
            EnsureInitialized();
            ValidateSource(sourceActorId);
            ValidateSourcePosition(sourcePositionX);
            _state.ApplyBurn(tick, damagePerSecond, durationTicks);
            _burnEffectId = EntityId.New();
            _burnSourceActorId = sourceActorId;
            _burnSourcePositionX = sourcePositionX;
        }

        public bool ApplyFreeze(long tick, int durationTicks)
        {
            EnsureInitialized();
            bool applied = _state.ApplyFreeze(tick, durationTicks);
            if (applied) _isFrozen = true;
            return applied;
        }

        /// <summary>Advance this actor's status clock once for a simulation tick.</summary>
        public MagusStatusSnapshot Tick(long tick)
        {
            EnsureInitialized();
            if (tick <= _lastTick)
            {
                throw new InvalidOperationException("Magus status target must be evaluated once per increasing simulation tick.");
            }

            _lastTick = tick;
            StatusSnapshot = _state.Tick(tick);
            _isFrozen = StatusSnapshot.IsFrozen;
            if (StatusSnapshot.BurnDamageDue > 0m)
            {
                if (!_burnSourceActorId.IsValid)
                {
                    throw new InvalidOperationException("A burn pulse must retain its source actor.");
                }

                _pendingBurnTicks.Enqueue(new MagusBurnTick(
                    _burnEffectId,
                    HitId.New(),
                    _burnSourceActorId,
                    ActorId,
                    StatusSnapshot.BurnDamageDue,
                    tick,
                    _burnSourcePositionX));
            }

            return StatusSnapshot;
        }

        /// <summary>
        /// Dequeue one pending status pulse for the owning enemy controller.
        /// </summary>
        public bool TryDequeueBurnTick(out MagusBurnTick burnTick)
        {
            if (_pendingBurnTicks.Count == 0)
            {
                burnTick = default;
                return false;
            }

            burnTick = _pendingBurnTicks.Dequeue();
            return true;
        }

        /// <summary>Move queued burn pulses into the actor's standard impact queue for normal health resolution.</summary>
        internal void QueuePendingBurnTicksAsImpacts()
        {
            EnsureInitialized();

            while (TryDequeueBurnTick(out MagusBurnTick burnTick))
            {
                if (burnTick.TargetActorId != ActorId || burnTick.SourceActorId == ActorId)
                {
                    throw new InvalidOperationException("A burn pulse must retain its distinct source and target actors.");
                }

                _impactTarget.QueueImpact(new ProjectileImpact(
                    burnTick.EffectId,
                    burnTick.HitId,
                    burnTick.SourceActorId,
                    burnTick.TargetActorId,
                    burnTick.Damage,
                    burnTick.SourcePositionX));
            }
        }

        internal void ResetRoomEffects()
        {
            _state = new MagusStatusState();
            _pendingBurnTicks.Clear();
            _burnEffectId = default;
            _burnSourceActorId = default;
            _burnSourcePositionX = 0f;
            _lastTick = -1;
            StatusSnapshot = default;
            _isFrozen = false;
        }

        private void EnsureInitialized()
        {
            if (!_initialized || _impactTarget == null || !_impactTarget.IsInitialized || _impactTarget.ActorId != ActorId)
            {
                throw new InvalidOperationException("Magus status target must be initialized alongside its projectile impact target.");
            }
        }

        private static void ValidateSource(ActorId sourceActorId)
        {
            if (!sourceActorId.IsValid) throw new ArgumentException("Magus status requires a valid source actor.", nameof(sourceActorId));
        }

        private static void ValidateSourcePosition(float sourcePositionX)
        {
            if (float.IsNaN(sourcePositionX) || float.IsInfinity(sourcePositionX))
            {
                throw new ArgumentOutOfRangeException(nameof(sourcePositionX), "Status source position must be finite.");
            }
        }
    }
}
