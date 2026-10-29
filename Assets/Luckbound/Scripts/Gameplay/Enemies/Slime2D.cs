using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Magus;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.Gameplay.Enemies
{
    public enum SlimeBehaviorState
    {
        Dormant = 0,
        Chase = 1,
        Telegraph = 2,
        Attack = 3,
        Recovery = 4,
        Dead = 5
    }

    /// <summary>Grounded Slime baseline: chase, telegraph, short leap, recovery, and queued combat damage.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Slime2D : MonoBehaviour
    {
        public const int MaximumHealth = 24;
        public const int AttackDamage = 8;
        public const int TelegraphDurationTicks = 27;
        public const int LeapTimeoutTicks = 60;
        public const int RecoveryDurationTicks = 30;
        public const float DetectionRadius = 8f;
        public const float ChaseSpeed = 2f;
        public const float AttackRange = 1.25f;
        public const float LeapHorizontalSpeed = 4f;
        public const float LeapVerticalSpeed = 4.5f;
        public const float GravityScale = 3f;

        private const float GroundProbeDistance = 0.08f;
        private const float GroundNormalThreshold = 0.65f;
        private const float GroundedVerticalSpeedLimit = 0.05f;

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

        private ActorDamageState _damageState;
        private ActorId _actorId;
        private Collider2D _collider;
        private ContactFilter2D _groundContactFilter;
        private bool _hasHitPlayerThisAttack;
        private bool _initialized;
        private bool _defeatEventRaised;
        private HitId _attackHitId;
        private PlayerMotor2D _player;
        private ProjectileImpactTarget2D _impactTarget;
        private Rigidbody2D _body;
        private float _facingDirection = 1f;
        private int _attackElapsedTicks;
        private int _recoveryTicksRemaining;
        private int _telegraphTicksRemaining;
        private int _aiControlSuspensionTicksRemaining;
        private long _simulationTick;

        public SlimeBehaviorState State { get; private set; } = SlimeBehaviorState.Dormant;

        public bool IsDead => State == SlimeBehaviorState.Dead;

        public ActorId ActorId => _actorId;

        /// <summary>Raised once after this initialized actor reaches Dead through a resolved combat impact.</summary>
        public event Action<ActorId> Defeated;

        /// <summary>Raised when this initialized component becomes unavailable; it is never a defeat signal.</summary>
        public event Action<ActorId> BecameUnavailable;

        public int CurrentHealth => _damageState != null ? _damageState.CurrentHealth : 0;

        public CombatStats CombatStats => _damageState != null ? _damageState.Stats : null;

        public int TelegraphTicksRemaining => _telegraphTicksRemaining;

        public int LeapAttackCount { get; private set; }

        public int ProjectileImpactCount { get; private set; }

        public int MeleeImpactCount { get; private set; }

        public int AiControlSuspensionTicksRemaining => _aiControlSuspensionTicksRemaining;

        public Vector2 LastMeleeKnockbackVelocity { get; private set; }

        public int SuccessfulPlayerHitCount { get; private set; }

        public bool IsGrounded { get; private set; }

        public void Initialize(ActorId actorId, PlayerMotor2D player, LayerMask groundLayers)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Slime2D can only be initialized once.");
            }

            if (!actorId.IsValid)
            {
                throw new ArgumentException("Slime2D requires a valid actor identifier.", nameof(actorId));
            }

            if (player == null || player.CombatStats == null)
            {
                throw new ArgumentNullException(nameof(player), "Slime2D requires an initialized player motor.");
            }

            if (actorId == player.ActorId)
            {
                throw new ArgumentException("Slime and player actor identifiers must be different.", nameof(actorId));
            }

            if (groundLayers.value == 0)
            {
                throw new ArgumentException("Slime2D requires at least one ground layer.", nameof(groundLayers));
            }

            if (Physics2D.gravity.y >= -0.01f)
            {
                throw new InvalidOperationException("Slime2D requires downward 2D gravity.");
            }

            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (_body == null || _collider == null)
            {
                throw new InvalidOperationException("Slime2D requires a Rigidbody2D and Collider2D.");
            }

            _actorId = actorId;
            _player = player;
            _damageState = new ActorDamageState(
                new CombatStats(maximumHealth: MaximumHealth, maximumEnergy: 0, defense: 0),
                directImpactProtectionTicks: 0);
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.gravityScale = GravityScale;

            _groundContactFilter = new ContactFilter2D();
            _groundContactFilter.SetLayerMask(groundLayers);
            _groundContactFilter.useTriggers = false;

            _impactTarget = GetComponent<ProjectileImpactTarget2D>();
            if (_impactTarget == null)
            {
                _impactTarget = gameObject.AddComponent<ProjectileImpactTarget2D>();
            }

            if (_impactTarget.IsInitialized && _impactTarget.ActorId != actorId)
            {
                throw new InvalidOperationException("Projectile target actor ID does not match the Slime actor ID.");
            }

            if (!_impactTarget.IsInitialized)
            {
                _impactTarget.Initialize(actorId);
            }

            _initialized = true;
        }

        private void OnDisable()
        {
            if (_initialized && _actorId.IsValid)
                BecameUnavailable?.Invoke(_actorId);
        }

        private void OnDestroy()
        {
            if (_initialized && _actorId.IsValid)
                BecameUnavailable?.Invoke(_actorId);
            if (_impactTarget != null)
            {
                _impactTarget.ClearPendingImpacts();
            }
            BecameUnavailable = null;
            Defeated = null;
        }

        private void FixedUpdate()
        {
            if (!_initialized || Time.timeScale <= 0f || State == SlimeBehaviorState.Dead)
            {
                return;
            }

            long tick = _simulationTick++;
            ResolveMeleeImpacts(tick);
            if (State == SlimeBehaviorState.Dead)
            {
                return;
            }

            ResolveProjectileImpacts(tick);
            if (State == SlimeBehaviorState.Dead)
            {
                return;
            }

            IsGrounded = UpdateGrounded();
            float deltaTime = Time.fixedDeltaTime;
            Vector2 velocity = _body.linearVelocity;
            bool aiControlSuspended = _aiControlSuspensionTicksRemaining > 0;
            float horizontalVelocityDuringSuspension = velocity.x;

            switch (State)
            {
                case SlimeBehaviorState.Dormant:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    if (CanDetectPlayer())
                    {
                        State = SlimeBehaviorState.Chase;
                    }
                    break;

                case SlimeBehaviorState.Chase:
                    if (!CanDetectPlayer())
                    {
                        velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                        State = SlimeBehaviorState.Dormant;
                        break;
                    }

                    float horizontalDistance = _player.transform.position.x - transform.position.x;
                    if (IsGrounded && Mathf.Abs(horizontalDistance) <= AttackRange)
                    {
                        velocity.x = 0f;
                        _facingDirection = horizontalDistance < 0f ? -1f : 1f;
                        _telegraphTicksRemaining = TelegraphDurationTicks;
                        State = SlimeBehaviorState.Telegraph;
                    }
                    else
                    {
                        _facingDirection = horizontalDistance < 0f ? -1f : 1f;
                        velocity.x = _facingDirection * ChaseSpeed;
                    }
                    break;

                case SlimeBehaviorState.Telegraph:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    if (_telegraphTicksRemaining > 0)
                    {
                        _telegraphTicksRemaining--;
                    }

                    if (_telegraphTicksRemaining == 0)
                    {
                        BeginLeapAttack();
                        velocity = _body.linearVelocity;
                    }
                    break;

                case SlimeBehaviorState.Attack:
                    _attackElapsedTicks++;
                    if (_attackElapsedTicks >= LeapTimeoutTicks || (_attackElapsedTicks > 1 && IsGrounded))
                    {
                        BeginRecovery();
                        velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    }
                    else
                    {
                        velocity.x = _facingDirection * LeapHorizontalSpeed;
                    }
                    break;

                case SlimeBehaviorState.Recovery:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    if (_recoveryTicksRemaining > 0)
                    {
                        _recoveryTicksRemaining--;
                    }

                    if (_recoveryTicksRemaining == 0)
                    {
                        State = CanDetectPlayer() ? SlimeBehaviorState.Chase : SlimeBehaviorState.Dormant;
                    }
                    break;
            }

            if (aiControlSuspended)
            {
                velocity.x = horizontalVelocityDuringSuspension;
                _aiControlSuspensionTicksRemaining--;
            }
            else if (State == SlimeBehaviorState.Chase || State == SlimeBehaviorState.Attack)
            {
                velocity.x *= (float)GetWeaponSlowMultiplier(_player.SimulationTick);
            }

            _body.linearVelocity = velocity;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (State != SlimeBehaviorState.Attack || _hasHitPlayerThisAttack || collision == null)
            {
                return;
            }

            PlayerMotor2D player = collision.collider.GetComponentInParent<PlayerMotor2D>();
            if (player != _player || player.IsDead || player.CombatStats == null)
            {
                return;
            }

            _hasHitPlayerThisAttack = true;
            DamageCalculationRequest request = CreateDamageRequest(player.CombatStats, AttackDamage);
            DirectImpactOutcome outcome = player.ReceiveDirectImpact(
                _attackHitId,
                request,
                player.SimulationTick,
                Vector2.zero);
            if (outcome.Status == DirectImpactStatus.Applied)
            {
                SuccessfulPlayerHitCount++;
            }
        }

        private void ResolveProjectileImpacts(long tick)
        {
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget != null) statusTarget.QueuePendingBurnTicksAsImpacts();

            while (_impactTarget.TryDequeueImpact(out ProjectileImpact impact))
            {
                if (State == SlimeBehaviorState.Dead)
                {
                    _impactTarget.ClearPendingImpacts();
                    return;
                }

                DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                    impact.HitId,
                    CreateDamageRequest(_damageState.Stats, impact.BaseDamage, impact.DefensePenetration),
                    tick);
                if (outcome.Status == DirectImpactStatus.Applied)
                {
                    ProjectileImpactCount++;
                    if (impact.SlowPercent > 0)
                    {
                        GetOrCreateWeaponStatusTarget().ApplySlow(
                            _player.SimulationTick,
                            impact.SlowPercent,
                            impact.SlowDurationTicks);
                    }
                }

                if (outcome.DeathOccurred)
                {
                    EnterDeadState();
                }
            }
        }

        private void ResolveMeleeImpacts(long tick)
        {
            long physicsTick = Math.Max(0L, (long)Math.Round(Time.fixedTimeAsDouble / Time.fixedDeltaTime));
            while (_impactTarget.TryDequeueMeleeImpactForTick(physicsTick, out MeleeImpact impact))
            {
                if (State == SlimeBehaviorState.Dead)
                {
                    _impactTarget.ClearPendingImpacts();
                    return;
                }

                DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                    impact.HitId,
                    CreateDamageRequest(_damageState.Stats, impact.Damage),
                    tick);
                if (outcome.Status == DirectImpactStatus.Applied)
                {
                    MeleeImpactCount++;
                    ApplyMeleeKnockback(impact);
                }

                if (outcome.DeathOccurred)
                {
                    EnterDeadState();
                }
            }
        }

        private void BeginLeapAttack()
        {
            if (!CanDetectPlayer())
            {
                State = SlimeBehaviorState.Recovery;
                _recoveryTicksRemaining = RecoveryDurationTicks;
                return;
            }

            float horizontalDistance = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(horizontalDistance) > 0.001f)
            {
                _facingDirection = horizontalDistance < 0f ? -1f : 1f;
            }

            _attackHitId = HitId.New();
            _hasHitPlayerThisAttack = false;
            _attackElapsedTicks = 0;
            LeapAttackCount++;
            State = SlimeBehaviorState.Attack;
            if (_aiControlSuspensionTicksRemaining <= 0)
            {
                _body.linearVelocity = new Vector2(_facingDirection * LeapHorizontalSpeed, LeapVerticalSpeed);
            }
        }

        private void ApplyMeleeKnockback(MeleeImpact impact)
        {
            if (!impact.HasKnockback || impact.AiControlSuspensionTicks <= 0) return;

            LastMeleeKnockbackVelocity = impact.KnockbackVelocity;
            _body.linearVelocity = impact.KnockbackVelocity;
            _aiControlSuspensionTicksRemaining = Math.Max(
                _aiControlSuspensionTicksRemaining,
                impact.AiControlSuspensionTicks);
        }

        private void BeginRecovery()
        {
            State = SlimeBehaviorState.Recovery;
            _recoveryTicksRemaining = RecoveryDurationTicks;
        }

        private void EnterDeadState()
        {
            if (State == SlimeBehaviorState.Dead) return;
            State = SlimeBehaviorState.Dead;
            _impactTarget.ClearPendingImpacts();
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget != null) statusTarget.ResetRoomEffects();
            _collider.enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            if (!_defeatEventRaised && _initialized && _actorId.IsValid)
            {
                _defeatEventRaised = true;
                Defeated?.Invoke(_actorId);
            }
        }

        private bool CanDetectPlayer()
        {
            return _player != null && !_player.IsDead &&
                Vector2.Distance(transform.position, _player.transform.position) <= DetectionRadius;
        }

        private bool UpdateGrounded()
        {
            if (_body.linearVelocity.y > GroundedVerticalSpeedLimit)
            {
                return false;
            }

            int hitCount = _collider.Cast(
                Vector2.down,
                _groundContactFilter,
                _groundHits,
                GroundProbeDistance);
            for (int index = 0; index < hitCount; index++)
            {
                if (_groundHits[index].normal.y >= GroundNormalThreshold)
                {
                    return true;
                }
            }

            return false;
        }

        private MagusStatusEffectTarget2D GetOrCreateWeaponStatusTarget()
        {
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget == null) statusTarget = gameObject.AddComponent<MagusStatusEffectTarget2D>();
            if (!statusTarget.IsInitialized) statusTarget.Initialize(_impactTarget);
            return statusTarget;
        }

        private decimal GetWeaponSlowMultiplier(long tick)
        {
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            return statusTarget != null && statusTarget.IsInitialized
                ? statusTarget.GetMovementSpeedMultiplierAt(tick)
                : 1m;
        }

        private static DamageCalculationRequest CreateDamageRequest(
            CombatStats stats,
            decimal damage,
            int defensePenetration = 0)
        {
            return new DamageCalculationRequest(
                baseDamage: damage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: defensePenetration,
                incomingDamageReductionPercent: 0m,
                targetStats: stats,
                targetIsInvulnerable: false);
        }
    }
}
