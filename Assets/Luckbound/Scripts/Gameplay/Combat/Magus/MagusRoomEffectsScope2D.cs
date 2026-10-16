using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using UnityEngine;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Gameplay.Combat.Magus
{
    /// <summary>Room-owned Magus zones, status clocks, and Fireball impact subscriptions.</summary>
    internal sealed class MagusRoomEffectsScope2D : IDisposable
    {
        private readonly ActorId _sourceActorId;
        private readonly LayerMask _targetLayers;
        private readonly HashSet<MagusStatusEffectTarget2D> _statusTargets = new HashSet<MagusStatusEffectTarget2D>();
        private readonly List<ArcaneSealEffect> _seals = new List<ArcaneSealEffect>();
        private readonly List<FireballBinding> _fireballBindings = new List<FireballBinding>();
        private bool _disposed;
        private long _lastTick = -1;

        public MagusRoomEffectsScope2D(ActorId sourceActorId, LayerMask targetLayers)
        {
            if (!sourceActorId.IsValid) throw new ArgumentException("Magus room effects require a valid source actor.", nameof(sourceActorId));
            if (targetLayers.value == 0) throw new ArgumentException("Magus room effects require target layers.", nameof(targetLayers));
            _sourceActorId = sourceActorId;
            _targetLayers = targetLayers;
        }

        public int ActiveSealCount => _seals.Count;

        public int TrackedStatusTargetCount => _statusTargets.Count;

        public bool IsDisposed => _disposed;

        public void Tick(long tick)
        {
            EnsureActive();
            if (tick < 0 || tick <= _lastTick)
            {
                throw new InvalidOperationException("Magus room effects must advance once per increasing simulation tick.");
            }

            _lastTick = tick;
            TickStatusTargets(tick);
            PruneFireballBindings();

            for (int index = _seals.Count - 1; index >= 0; index--)
            {
                if (_seals[index].Tick(tick, _sourceActorId, _targetLayers)) _seals.RemoveAt(index);
            }
        }

        public MagusStatusEffectTarget2D GetOrCreateStatusTarget(ProjectileImpactTarget2D impactTarget)
        {
            EnsureActive();
            if (impactTarget == null || !impactTarget.IsInitialized)
            {
                throw new ArgumentException("Magus effects require an initialized impact target.", nameof(impactTarget));
            }

            MagusStatusEffectTarget2D statusTarget = impactTarget.GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget == null) statusTarget = impactTarget.gameObject.AddComponent<MagusStatusEffectTarget2D>();
            if (!statusTarget.IsInitialized) statusTarget.Initialize(impactTarget);
            if (statusTarget.ActorId != impactTarget.ActorId)
            {
                throw new InvalidOperationException("Magus status and projectile targets must share one ActorId.");
            }

            _statusTargets.Add(statusTarget);
            return statusTarget;
        }

        public void ApplyFrostNova(
            MagusFrostNovaActivation activation,
            Vector2 center,
            float sourcePositionX,
            long tick)
        {
            EnsureActive();
            HitId hitId = HitId.New();
            EntityId effectId = EntityId.New();
            var affectedActors = new HashSet<ActorId>();
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, (float)activation.Radius, _targetLayers.value);
            for (int index = 0; index < overlaps.Length; index++)
            {
                ProjectileImpactTarget2D target = GetImpactTarget(overlaps[index]);
                if (!CanAffect(target, affectedActors)) continue;

                target.QueueImpact(new ProjectileImpact(
                    effectId,
                    hitId,
                    _sourceActorId,
                    target.ActorId,
                    activation.BaseDamage,
                    sourcePositionX));

                GetOrCreateStatusTarget(target).ApplyFreeze(tick, activation.FreezeDurationTicks);
            }
        }

        public void AddArcaneSeal(MagusArcaneSealActivation activation, Vector2 center, float sourcePositionX, long startTick)
        {
            EnsureActive();
            var seal = new ArcaneSealEffect(activation, center, sourcePositionX, startTick);
            _seals.Add(seal);
            // Damage zones pulse immediately and then at the catalog interval until their room lifetime ends.
            seal.Tick(startTick, _sourceActorId, _targetLayers);
        }

        public void TrackFireball(Projectile2D projectile, Action<ProjectileImpact> callback)
        {
            EnsureActive();
            if (projectile == null) throw new ArgumentNullException(nameof(projectile));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            projectile.Impacted += callback;
            _fireballBindings.Add(new FireballBinding(projectile, callback));
        }

        public void UntrackFireball(Projectile2D projectile, Action<ProjectileImpact> callback)
        {
            if (projectile != null) projectile.Impacted -= callback;
            for (int index = _fireballBindings.Count - 1; index >= 0; index--)
            {
                FireballBinding binding = _fireballBindings[index];
                if (binding.Projectile == projectile && binding.Callback == callback)
                {
                    _fireballBindings.RemoveAt(index);
                    return;
                }
            }
        }

        public void ResolveFireballImpact(
            Projectile2D projectile,
            MagusFireballActivation activation,
            ProjectileImpact impact,
            long tick)
        {
            EnsureActive();
            if (projectile == null) return;

            Vector2 center = projectile.transform.position;
            long statusTick = tick < _lastTick ? _lastTick : tick;
            var affectedActors = new HashSet<ActorId>();
            bool directTargetReceivedBurn = false;

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(center, (float)activation.ExplosionRadius, _targetLayers.value);
            for (int index = 0; index < overlaps.Length; index++)
            {
                ProjectileImpactTarget2D target = GetImpactTarget(overlaps[index]);
                if (!CanAffect(target, affectedActors)) continue;

                if (target.ActorId != impact.TargetActorId)
                {
                    target.QueueImpact(new ProjectileImpact(
                        projectile.ProjectileId,
                        impact.HitId,
                        _sourceActorId,
                        target.ActorId,
                        activation.Projectile.BaseDamage,
                        impact.SourcePositionX));
                }

                GetOrCreateStatusTarget(target).ApplyBurn(
                    statusTick,
                    _sourceActorId,
                    activation.BurnDamagePerSecond,
                    activation.BurnDurationTicks,
                    impact.SourcePositionX);

                if (target.ActorId == impact.TargetActorId) directTargetReceivedBurn = true;
            }

            // A direct collider can be outside the sampled overlap when it is unusually large or offset.
            if (!directTargetReceivedBurn)
            {
                ProjectileImpactTarget2D directTarget = FindImpactTarget(impact.TargetActorId);
                if (directTarget != null && directTarget.IsInitialized && directTarget.ActorId != _sourceActorId)
                {
                    GetOrCreateStatusTarget(directTarget).ApplyBurn(
                        statusTick,
                        _sourceActorId,
                        activation.BurnDamagePerSecond,
                        activation.BurnDurationTicks,
                        impact.SourcePositionX);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (int index = 0; index < _fireballBindings.Count; index++)
            {
                FireballBinding binding = _fireballBindings[index];
                if (binding.Projectile != null) binding.Projectile.Impacted -= binding.Callback;
            }

            _fireballBindings.Clear();
            _seals.Clear();
            foreach (MagusStatusEffectTarget2D statusTarget in _statusTargets)
            {
                if (statusTarget != null) statusTarget.ResetRoomEffects();
            }

            _statusTargets.Clear();
        }

        private void TickStatusTargets(long tick)
        {
            MagusStatusEffectTarget2D[] activeTargets = new MagusStatusEffectTarget2D[_statusTargets.Count];
            _statusTargets.CopyTo(activeTargets);
            for (int index = 0; index < activeTargets.Length; index++)
            {
                MagusStatusEffectTarget2D target = activeTargets[index];
                if (target == null)
                {
                    _statusTargets.Remove(target);
                    continue;
                }

                target.Tick(tick);
                if (!target.StatusSnapshot.IsBurning && !target.StatusSnapshot.IsFrozen && !target.StatusSnapshot.IsFreezeProtected &&
                    target.PendingBurnTickCount == 0)
                {
                    _statusTargets.Remove(target);
                }
            }
        }

        private void PruneFireballBindings()
        {
            for (int index = _fireballBindings.Count - 1; index >= 0; index--)
            {
                FireballBinding binding = _fireballBindings[index];
                if (binding.Projectile == null || binding.Projectile.IsPooled)
                {
                    if (binding.Projectile != null) binding.Projectile.Impacted -= binding.Callback;
                    _fireballBindings.RemoveAt(index);
                }
            }
        }

        private bool CanAffect(ProjectileImpactTarget2D target, HashSet<ActorId> affectedActors)
        {
            return target != null && target.IsInitialized && target.ActorId != _sourceActorId && affectedActors.Add(target.ActorId);
        }

        private static ProjectileImpactTarget2D GetImpactTarget(Collider2D collider)
        {
            return collider == null ? null : collider.GetComponentInParent<ProjectileImpactTarget2D>();
        }

        private static ProjectileImpactTarget2D FindImpactTarget(ActorId actorId)
        {
            ProjectileImpactTarget2D[] targets = UnityEngine.Object.FindObjectsByType<ProjectileImpactTarget2D>(FindObjectsSortMode.None);
            for (int index = 0; index < targets.Length; index++)
            {
                ProjectileImpactTarget2D target = targets[index];
                if (target != null && target.IsInitialized && target.ActorId == actorId) return target;
            }

            return null;
        }

        private void EnsureActive()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MagusRoomEffectsScope2D));
        }

        private readonly struct FireballBinding
        {
            public FireballBinding(Projectile2D projectile, Action<ProjectileImpact> callback)
            {
                Projectile = projectile;
                Callback = callback;
            }

            public Projectile2D Projectile { get; }

            public Action<ProjectileImpact> Callback { get; }
        }

        private sealed class ArcaneSealEffect
        {
            private readonly Vector2 _center;
            private readonly float _sourcePositionX;
            private readonly decimal _damage;
            private readonly int _durationTicks;
            private readonly int _pulseIntervalTicks;
            private readonly float _radius;
            private readonly long _endTick;
            private readonly EntityId _effectId = EntityId.New();
            private long _nextPulseTick;

            public ArcaneSealEffect(MagusArcaneSealActivation activation, Vector2 center, float sourcePositionX, long startTick)
            {
                if (startTick < 0 || startTick > long.MaxValue - activation.DurationTicks)
                {
                    throw new ArgumentOutOfRangeException(nameof(startTick), "Arcane Seal lifetime must fit the simulation tick range.");
                }

                _center = center;
                _sourcePositionX = sourcePositionX;
                _damage = activation.DamagePerSecond;
                _durationTicks = activation.DurationTicks;
                _pulseIntervalTicks = activation.DamagePulseIntervalTicks;
                _radius = (float)activation.Radius;
                _nextPulseTick = startTick;
                _endTick = startTick + _durationTicks;
            }

            /// <returns>True when the room-owned seal has expired.</returns>
            public bool Tick(long tick, ActorId sourceActorId, LayerMask targetLayers)
            {
                if (tick >= _endTick) return true;
                while (_nextPulseTick < _endTick && tick >= _nextPulseTick)
                {
                    Pulse(sourceActorId, targetLayers);
                    _nextPulseTick += _pulseIntervalTicks;
                }

                return false;
            }

            private void Pulse(ActorId sourceActorId, LayerMask targetLayers)
            {
                var affectedActors = new HashSet<ActorId>();
                HitId hitId = HitId.New();
                Collider2D[] overlaps = Physics2D.OverlapCircleAll(_center, _radius, targetLayers.value);
                for (int index = 0; index < overlaps.Length; index++)
                {
                    ProjectileImpactTarget2D target = GetImpactTarget(overlaps[index]);
                    if (target == null || !target.IsInitialized || target.ActorId == sourceActorId || !affectedActors.Add(target.ActorId)) continue;

                    target.QueueImpact(new ProjectileImpact(
                        _effectId,
                        hitId,
                        sourceActorId,
                        target.ActorId,
                        _damage,
                        _sourcePositionX));
                }
            }
        }
    }
}
