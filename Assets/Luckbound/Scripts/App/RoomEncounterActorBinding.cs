using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    internal enum RoomEncounterActorBindingPhase
    {
        PreparedInactive,
        Activating,
        Live,
        DefeatObserved,
        DefeatAccepted,
        Disposed
    }

    /// <summary>
    /// Private adapter for the concrete Slime2D combat-death signal. Object removal or the separate availability
    /// signal can only fail the encounter closed; neither can be translated into a defeat.
    /// </summary>
    internal sealed class RoomEncounterActorBinding : IDisposable
    {
        private RoomEncounterRuntime _owner;
        private readonly Action<ActorId> _defeatedHandler;
        private readonly Action<ActorId> _unavailableHandler;
        private readonly Collider2D[] _combatColliders;
        private readonly IReadOnlyList<Collider2D> _combatCollidersView;
        private bool _subscribed;
        private bool _unavailabilityObserved;
        private bool _physicalDeathObserved;
        private bool _defeatAccepted;
        private long _capturedSequence;
        private long _processedDrainSequence;
        private int _capturedFrame;
        private int _processedDrainFrame;
        private double _capturedFixedTime;
        private double _processedFixedTime;

        internal RoomEncounterActorBinding(RoomEncounterRuntime owner, string spawnKey, int waveNumber,
            long generation, RoomLifetimeScope scope, GameObject root, Slime2D slime,
            ProjectileImpactTarget2D impactTarget, RoomEncounterActorLifetimeRelay2D relay,
            Rigidbody2D body, Collider2D combatCollider, Collider2D[] combatColliders,
            bool inactiveBeforeOwnership)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(spawnKey)) throw new ArgumentException("A spawn key is required.", nameof(spawnKey));
            if (waveNumber < 1) throw new ArgumentOutOfRangeException(nameof(waveNumber));
            if (generation <= 0) throw new ArgumentOutOfRangeException(nameof(generation));
            Scope = scope != null ? scope : throw new ArgumentNullException(nameof(scope));
            Root = root != null ? root : throw new ArgumentNullException(nameof(root));
            Slime = slime != null ? slime : throw new ArgumentNullException(nameof(slime));
            ImpactTarget = impactTarget != null ? impactTarget : throw new ArgumentNullException(nameof(impactTarget));
            Relay = relay != null ? relay : throw new ArgumentNullException(nameof(relay));
            Body = body != null ? body : throw new ArgumentNullException(nameof(body));
            CombatCollider = combatCollider != null ? combatCollider : throw new ArgumentNullException(nameof(combatCollider));
            if (combatColliders == null || combatColliders.Length == 0)
                throw new ArgumentException("At least one concrete combat collider is required.", nameof(combatColliders));
            _combatColliders = (Collider2D[])combatColliders.Clone();
            _combatCollidersView = Array.AsReadOnly(_combatColliders);
            SpawnKey = spawnKey;
            WaveNumber = waveNumber;
            Generation = generation;
            WasInactiveBeforeOwnership = inactiveBeforeOwnership;
            ActorId = slime.ActorId;
            _defeatedHandler = HandleDefeated;
            _unavailableHandler = HandleUnavailable;
            Phase = RoomEncounterActorBindingPhase.PreparedInactive;
        }

        internal string SpawnKey { get; }
        internal int WaveNumber { get; }
        internal long Generation { get; }
        internal ActorId ActorId { get; }
        internal RoomLifetimeScope Scope { get; }
        internal GameObject Root { get; }
        internal Slime2D Slime { get; }
        internal ProjectileImpactTarget2D ImpactTarget { get; }
        internal RoomEncounterActorLifetimeRelay2D Relay { get; }
        internal Rigidbody2D Body { get; }
        internal Collider2D CombatCollider { get; }
        internal IReadOnlyList<Collider2D> CombatColliders => _combatCollidersView;
        internal bool WasInactiveBeforeOwnership { get; }
        internal RoomEncounterActorBindingPhase Phase { get; private set; }
        internal bool PhysicalDeathObserved => _physicalDeathObserved;
        internal bool IsDefeatAccepted => _defeatAccepted;
        internal bool UnavailabilityObserved => _unavailabilityObserved;
        internal long CapturedSequence => _capturedSequence;
        internal long ProcessedDrainSequence => _processedDrainSequence;
        internal int CapturedFrame => _capturedFrame;
        internal int ProcessedDrainFrame => _processedDrainFrame;
        internal double CapturedFixedTime => _capturedFixedTime;
        internal double ProcessedFixedTime => _processedFixedTime;

        internal void Subscribe()
        {
            if (_owner == null || _subscribed || !WasInactiveBeforeOwnership || Root.activeSelf ||
                !ReferenceEquals(Slime.gameObject, Root) || !ReferenceEquals(ImpactTarget.gameObject, Root) ||
                !ReferenceEquals(Relay.gameObject, Root) || !ReferenceEquals(Root.transform.parent, Scope.transform))
                throw new InvalidOperationException("A Slime binding must subscribe while its owned actor root is inactive and exact.");

            Relay.Initialize(this);
            Slime.Defeated += _defeatedHandler;
            Slime.BecameUnavailable += _unavailableHandler;
            _subscribed = true;
        }

        internal void BeginActivation()
        {
            if (_owner == null || Phase != RoomEncounterActorBindingPhase.PreparedInactive ||
                !WasInactiveBeforeOwnership || Root == null || Root.activeSelf || !ReferenceEquals(Root.transform.parent, Scope.transform))
                throw new InvalidOperationException("Only a prepared inactive Slime binding can enter activation.");

            Phase = RoomEncounterActorBindingPhase.Activating;
            Relay.ArmForActivation();
        }

        internal bool BeginActivationRoot()
        {
            if (_owner == null || Phase != RoomEncounterActorBindingPhase.Activating || _unavailabilityObserved ||
                Root == null || Root.activeSelf || !ReferenceEquals(Root.transform.parent, Scope.transform))
                return false;

            Root.SetActive(true);
            if (_unavailabilityObserved || !IsExactLiveOwnedActor()) return false;
            Phase = RoomEncounterActorBindingPhase.Live;
            return true;
        }

        /// <summary>Event handler entry point; tests may call it only for explicitly synthetic stale-callback cases.</summary>
        internal void HandleDefeated(ActorId reportedActorId)
        {
            RoomEncounterRuntime owner = _owner;
            if (owner == null || !_subscribed || _physicalDeathObserved || _defeatAccepted ||
                !owner.IsCurrentBinding(this, Generation))
                return;

            if (reportedActorId != ActorId || Phase != RoomEncounterActorBindingPhase.Live ||
                _unavailabilityObserved || !owner.ValidateActorAtDeathSignal(this, reportedActorId))
            {
                owner.EnqueueInvalidSignal(this, reportedActorId);
                return;
            }

            _physicalDeathObserved = true;
            Phase = RoomEncounterActorBindingPhase.DefeatObserved;
            _capturedFrame = Time.frameCount;
            _capturedFixedTime = Time.fixedTimeAsDouble;
            _capturedSequence = owner.EnqueueVerifiedDefeat(this, reportedActorId);
            if (_capturedSequence <= 0)
            {
                _physicalDeathObserved = false;
                Phase = RoomEncounterActorBindingPhase.Live;
                owner.EnqueueInvalidSignal(this, reportedActorId);
            }
        }

        /// <summary>Slime2D's own component availability signal. It is never treated as death.</summary>
        internal void HandleUnavailable(ActorId reportedActorId)
        {
            RoomEncounterRuntime owner = _owner;
            if (owner == null || !_subscribed || _physicalDeathObserved || _defeatAccepted ||
                !owner.IsCurrentBinding(this, Generation) || _unavailabilityObserved)
                return;

            _unavailabilityObserved = true;
            if (reportedActorId != ActorId)
                owner.EnqueueInvalidSignal(this, reportedActorId);
            else
                owner.EnqueueUnavailable(this, reportedActorId);
        }

        internal void HandleRootUnavailable(RoomEncounterActorLifetimeRelay2D relay)
        {
            RoomEncounterRuntime owner = _owner;
            if (owner == null || !_subscribed || _physicalDeathObserved || _defeatAccepted ||
                !ReferenceEquals(relay, Relay) || !owner.IsCurrentBinding(this, Generation) ||
                _unavailabilityObserved)
                return;

            _unavailabilityObserved = true;
            owner.EnqueueUnavailable(this, ActorId);
        }

        internal bool IsExactLiveOwnedActor()
        {
            return IsExactActiveOwnedActor() && HasAvailableCombatPhysics() &&
                !Slime.IsDead && Slime.CurrentHealth > 0;
        }

        internal bool IsExactActiveOwnedActor()
        {
            if (_owner == null || Scope == null || Root == null || Slime == null || ImpactTarget == null || Relay == null ||
                Body == null || CombatCollider == null)
                return false;
            if (_physicalDeathObserved || _defeatAccepted || _unavailabilityObserved || !WasInactiveBeforeOwnership ||
                Scope.IsDisposed || !Scope.isActiveAndEnabled || !ReferenceEquals(Root.transform.parent, Scope.transform) ||
                !Root.activeSelf || !Root.activeInHierarchy || !ReferenceEquals(Slime.gameObject, Root) ||
                !ReferenceEquals(ImpactTarget.gameObject, Root) || !ReferenceEquals(Relay.gameObject, Root) ||
                !Slime.enabled || !Slime.isActiveAndEnabled || !ImpactTarget.enabled || !ImpactTarget.isActiveAndEnabled ||
                !Relay.enabled || !Relay.isActiveAndEnabled ||
                Slime.CombatStats == null || Slime.CombatStats.MaximumHealth <= 0 ||
                Slime.CurrentHealth > Slime.CombatStats.MaximumHealth || Slime.ActorId != ActorId ||
                !ImpactTarget.IsInitialized || ImpactTarget.ActorId != ActorId)
                return false;

            Slime2D[] slimes = Root.GetComponents<Slime2D>();
            ProjectileImpactTarget2D[] targets = Root.GetComponents<ProjectileImpactTarget2D>();
            RoomEncounterActorLifetimeRelay2D[] relays = Root.GetComponents<RoomEncounterActorLifetimeRelay2D>();
            return slimes.Length == 1 && ReferenceEquals(slimes[0], Slime) &&
                targets.Length == 1 && ReferenceEquals(targets[0], ImpactTarget) &&
                relays.Length == 1 && ReferenceEquals(relays[0], Relay) &&
                ReferenceEquals(Root.GetComponent<Rigidbody2D>(), Body) &&
                ReferenceEquals(Root.GetComponent<Collider2D>(), CombatCollider) &&
                HasExactCombatColliderReferences();
        }

        private bool HasAvailableCombatPhysics()
        {
            if (Body == null || !Body.simulated || CombatCollider == null)
                return false;
            foreach (Collider2D collider in _combatColliders)
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                    collider.attachedRigidbody != Body)
                    return false;
            return true;
        }

        private bool HasExactCombatColliderReferences()
        {
            if (_combatColliders.Length == 0 || !ReferenceEquals(Root.GetComponent<Collider2D>(), CombatCollider))
                return false;

            Collider2D[] current = Root.GetComponentsInChildren<Collider2D>(true);
            int matchingCount = 0;
            foreach (Collider2D collider in current)
            {
                if (collider == null || collider.attachedRigidbody != Body) continue;
                matchingCount++;
                bool found = false;
                foreach (Collider2D captured in _combatColliders)
                {
                    if (ReferenceEquals(collider, captured))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            return matchingCount == _combatColliders.Length && _combatColliders.Contains(CombatCollider);
        }

        internal void MarkDefeatAccepted(long capturedSequence, long processedDrainSequence,
            int capturedFrame, int processedDrainFrame, double capturedFixedTime, double processedFixedTime)
        {
            if (Phase != RoomEncounterActorBindingPhase.DefeatObserved || !_physicalDeathObserved ||
                _defeatAccepted || capturedSequence != _capturedSequence || capturedFrame != _capturedFrame ||
                Math.Abs(capturedFixedTime - _capturedFixedTime) > 0.000001d ||
                processedDrainSequence <= capturedSequence)
                throw new InvalidOperationException("Only the exact queued physical defeat can be accepted once.");

            _defeatAccepted = true;
            _processedDrainSequence = processedDrainSequence;
            _capturedFrame = capturedFrame;
            _processedDrainFrame = processedDrainFrame;
            _capturedFixedTime = capturedFixedTime;
            _processedFixedTime = processedFixedTime;
            Phase = RoomEncounterActorBindingPhase.DefeatAccepted;
            UnsubscribeHandlers();
        }

        private void UnsubscribeHandlers()
        {
            if (!_subscribed) return;
            if (Slime != null)
            {
                Slime.Defeated -= _defeatedHandler;
                Slime.BecameUnavailable -= _unavailableHandler;
            }
            if (Relay != null) Relay.Detach(this);
            _subscribed = false;
        }

        public void Dispose()
        {
            if (_owner == null)
            {
                Phase = RoomEncounterActorBindingPhase.Disposed;
                return;
            }

            // The runtime invalidates its generation before this method is called during scope teardown.
            UnsubscribeHandlers();
            Phase = RoomEncounterActorBindingPhase.Disposed;
            _owner = null;
        }
    }
}
