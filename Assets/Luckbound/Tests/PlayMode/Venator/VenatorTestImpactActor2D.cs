using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using UnityEngine;

namespace Luckbound.Tests.PlayMode.Venator
{
    /// <summary>Minimal physics target that resolves only impacts queued by real 2D trigger contacts.</summary>
    public sealed class VenatorTestImpactActor2D : MonoBehaviour
    {
        private ActorDamageState _damageState;
        private ProjectileImpactTarget2D _impactTarget;
        private Rigidbody2D _body;
        private Collider2D[] _colliders;
        private long _simulationTick;

        public int CurrentHealth => _damageState != null ? _damageState.CurrentHealth : 0;

        public int MaximumHealth => _damageState != null ? _damageState.Stats.MaximumHealth : 0;

        public int AppliedImpactCount { get; private set; }

        public int DuplicateImpactCount { get; private set; }

        public bool IsDead => _damageState != null && _damageState.IsDead;

        public ProjectileImpactTarget2D ImpactTarget => _impactTarget;

        public Rigidbody2D Body => _body;

        public void Initialize(ActorId actorId, int maximumHealth, int targetLayer, bool multipleColliders = false)
        {
            if (!actorId.IsValid) throw new System.ArgumentException("Test actors require a valid ID.", nameof(actorId));
            if (maximumHealth <= 0) throw new System.ArgumentOutOfRangeException(nameof(maximumHealth));

            _body = GetComponent<Rigidbody2D>();
            if (_body == null) _body = gameObject.AddComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.gravityScale = 0f;
            _body.constraints = RigidbodyConstraints2D.FreezeRotation;
            _body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            _impactTarget = gameObject.AddComponent<ProjectileImpactTarget2D>();
            _impactTarget.Initialize(actorId);
            _damageState = new ActorDamageState(
                new CombatStats(maximumHealth, maximumEnergy: 0, defense: 0),
                directImpactProtectionTicks: 0);

            if (multipleColliders)
            {
                CreateChildCollider("Left Collider", new Vector2(-0.18f, 0f), targetLayer);
                CreateChildCollider("Right Collider", new Vector2(0.18f, 0f), targetLayer);
            }
            else
            {
                gameObject.layer = targetLayer;
                CircleCollider2D collider = gameObject.AddComponent<CircleCollider2D>();
                collider.radius = 0.38f;
            }

            _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
        }

        private void FixedUpdate()
        {
            if (_damageState == null || _impactTarget == null || _damageState.IsDead) return;

            while (_impactTarget.TryDequeueImpact(out ProjectileImpact impact))
            {
                DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                    impact.HitId,
                    CreateRequest(impact.BaseDamage),
                    _simulationTick++);
                if (outcome.Status == DirectImpactStatus.Applied)
                {
                    AppliedImpactCount++;
                }
                else if (outcome.Status == DirectImpactStatus.DuplicateHit)
                {
                    DuplicateImpactCount++;
                }

                if (outcome.DeathOccurred)
                {
                    _impactTarget.ClearPendingImpacts();
                    for (int index = 0; index < _colliders.Length; index++)
                    {
                        if (_colliders[index] != null) _colliders[index].enabled = false;
                    }

                    _body.linearVelocity = Vector2.zero;
                    _body.simulated = false;
                    return;
                }
            }
        }

        private void CreateChildCollider(string childName, Vector2 localPosition, int targetLayer)
        {
            var child = new GameObject(childName);
            child.layer = targetLayer;
            child.transform.SetParent(transform, worldPositionStays: false);
            child.transform.localPosition = localPosition;
            BoxCollider2D collider = child.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.45f, 0.9f);
        }

        private DamageCalculationRequest CreateRequest(decimal damage)
        {
            return new DamageCalculationRequest(
                baseDamage: damage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats: _damageState.Stats,
                targetIsInvulnerable: false);
        }
    }
}
