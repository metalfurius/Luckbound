using System;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.Gameplay.Enemies
{
    public enum HuesoguardiaBehaviorState
    {
        Dormant = 0,
        Chase = 1,
        Telegraph = 2,
        Attack = 3,
        Recovery = 4,
        Dead = 5
    }

    /// <summary>Shielded melee enemy with a directional guard and a committed, recoverable lunge.</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class Huesoguardia2D : MonoBehaviour
    {
        public const int MaximumHealth = 48;
        public const int BaseDefense = 2;
        public const int GuardDefenseBonus = 4;
        public const int AttackDamage = 10;
        public const int TelegraphDurationTicks = 30;
        public const int AttackDurationTicks = 18;
        public const int RecoveryDurationTicks = 30;
        public const float DetectionRadius = 8f;
        public const float ChaseSpeed = 1.5f;
        public const float AttackRange = 1.4f;
        public const float AttackSpeed = 5f;
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
        private bool _attackContactOccurred;
        private HitId _attackHitId;
        private PlayerMotor2D _player;
        private ProjectileImpactTarget2D _impactTarget;
        private Rigidbody2D _body;
        private float _facingDirection = 1f;
        private int _attackElapsedTicks;
        private int _recoveryTicksRemaining;
        private int _telegraphTicksRemaining;
        private long _simulationTick;

        public HuesoguardiaBehaviorState State { get; private set; } = HuesoguardiaBehaviorState.Dormant;

        public bool IsDead => State == HuesoguardiaBehaviorState.Dead;

        public bool IsGuarding => State == HuesoguardiaBehaviorState.Chase ||
            State == HuesoguardiaBehaviorState.Telegraph;

        public ActorId ActorId => _actorId;

        public CombatStats CombatStats => _damageState != null ? _damageState.Stats : null;

        public int CurrentHealth => _damageState != null ? _damageState.CurrentHealth : 0;

        public float FacingDirection => _facingDirection;

        public int TelegraphTicksRemaining => _telegraphTicksRemaining;

        public int RecoveryTicksRemaining => _recoveryTicksRemaining;

        public int SuccessfulPlayerHitCount { get; private set; }

        public int ProjectileImpactCount { get; private set; }

        public bool IsGrounded { get; private set; }

        public void Initialize(ActorId actorId, PlayerMotor2D player, LayerMask groundLayers)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("Huesoguardia2D can only be initialized once.");
            }

            if (!actorId.IsValid)
            {
                throw new ArgumentException("Huesoguardia2D requires a valid actor identifier.", nameof(actorId));
            }

            if (player == null || player.CombatStats == null)
            {
                throw new ArgumentNullException(nameof(player), "Huesoguardia2D requires an initialized player motor.");
            }

            if (actorId == player.ActorId)
            {
                throw new ArgumentException("Enemy and player actor identifiers must be different.", nameof(actorId));
            }

            if (groundLayers.value == 0)
            {
                throw new ArgumentException("Huesoguardia2D requires at least one ground layer.", nameof(groundLayers));
            }

            if (Physics2D.gravity.y >= -0.01f)
            {
                throw new InvalidOperationException("Huesoguardia2D requires downward 2D gravity.");
            }

            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (_body == null || _collider == null)
            {
                throw new InvalidOperationException("Huesoguardia2D requires a Rigidbody2D and Collider2D.");
            }

            _actorId = actorId;
            _player = player;
            _damageState = new ActorDamageState(
                new CombatStats(maximumHealth: MaximumHealth, maximumEnergy: 0, defense: BaseDefense),
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
                throw new InvalidOperationException("Projectile target actor ID does not match the Huesoguardia actor ID.");
            }

            if (!_impactTarget.IsInitialized)
            {
                _impactTarget.Initialize(actorId);
            }

            _initialized = true;
        }

        private void OnDestroy()
        {
            if (_impactTarget != null)
            {
                _impactTarget.ClearPendingImpacts();
            }
        }

        private void FixedUpdate()
        {
            if (!_initialized || Time.timeScale <= 0f || IsDead)
            {
                return;
            }

            ResolveProjectileImpacts(_simulationTick++);
            if (IsDead)
            {
                return;
            }

            IsGrounded = UpdateGrounded();
            float deltaTime = Time.fixedDeltaTime;
            Vector2 velocity = _body.linearVelocity;

            switch (State)
            {
                case HuesoguardiaBehaviorState.Dormant:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    if (CanDetectPlayer())
                    {
                        State = HuesoguardiaBehaviorState.Chase;
                        FacePlayer();
                    }
                    break;

                case HuesoguardiaBehaviorState.Chase:
                    if (!CanDetectPlayer())
                    {
                        velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                        State = HuesoguardiaBehaviorState.Dormant;
                        break;
                    }

                    FacePlayer();
                    float horizontalDistance = _player.transform.position.x - transform.position.x;
                    if (IsGrounded && Mathf.Abs(horizontalDistance) <= AttackRange)
                    {
                        velocity.x = 0f;
                        _telegraphTicksRemaining = TelegraphDurationTicks;
                        State = HuesoguardiaBehaviorState.Telegraph;
                    }
                    else
                    {
                        velocity.x = _facingDirection * ChaseSpeed;
                    }
                    break;

                case HuesoguardiaBehaviorState.Telegraph:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    if (_telegraphTicksRemaining > 0)
                    {
                        _telegraphTicksRemaining--;
                    }

                    if (_telegraphTicksRemaining == 0)
                    {
                        BeginAttack();
                        velocity = _body.linearVelocity;
                    }
                    break;

                case HuesoguardiaBehaviorState.Attack:
                    _attackElapsedTicks++;
                    if (_attackContactOccurred || _attackElapsedTicks >= AttackDurationTicks)
                    {
                        BeginRecovery();
                        velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    }
                    else
                    {
                        velocity.x = _facingDirection * AttackSpeed;
                    }
                    break;

                case HuesoguardiaBehaviorState.Recovery:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * deltaTime);
                    if (_recoveryTicksRemaining > 0)
                    {
                        _recoveryTicksRemaining--;
                    }

                    if (_recoveryTicksRemaining == 0)
                    {
                        State = CanDetectPlayer() ? HuesoguardiaBehaviorState.Chase : HuesoguardiaBehaviorState.Dormant;
                    }
                    break;
            }

            _body.linearVelocity = velocity;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (State != HuesoguardiaBehaviorState.Attack || _hasHitPlayerThisAttack || collision == null)
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
                new Vector2(_facingDirection * 2f, 0f));
            if (outcome.Status == DirectImpactStatus.Applied)
            {
                SuccessfulPlayerHitCount++;
                _attackContactOccurred = true;
            }
        }

        private void ResolveProjectileImpacts(long tick)
        {
            while (_impactTarget.TryDequeueImpact(out ProjectileImpact impact))
            {
                if (IsDead)
                {
                    _impactTarget.ClearPendingImpacts();
                    return;
                }

                int defenseBonus = IsGuarding && IsInFrontArc(impact.SourcePositionX)
                    ? GuardDefenseBonus
                    : 0;
                DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                    impact.HitId,
                    CreateDamageRequest(_damageState.Stats, impact.BaseDamage, defenseBonus),
                    tick);
                if (outcome.Status == DirectImpactStatus.Applied)
                {
                    ProjectileImpactCount++;
                }

                if (outcome.DeathOccurred)
                {
                    EnterDeadState();
                }
            }
        }

        private void BeginAttack()
        {
            if (!CanDetectPlayer())
            {
                BeginRecovery();
                return;
            }

            FacePlayer();
            _attackHitId = HitId.New();
            _hasHitPlayerThisAttack = false;
            _attackContactOccurred = false;
            _attackElapsedTicks = 0;
            State = HuesoguardiaBehaviorState.Attack;
            _body.linearVelocity = new Vector2(_facingDirection * AttackSpeed, 0f);
        }

        private void BeginRecovery()
        {
            State = HuesoguardiaBehaviorState.Recovery;
            _recoveryTicksRemaining = RecoveryDurationTicks;
            _attackContactOccurred = false;
        }

        private void EnterDeadState()
        {
            State = HuesoguardiaBehaviorState.Dead;
            _impactTarget.ClearPendingImpacts();
            _collider.enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
        }

        private bool CanDetectPlayer()
        {
            return _player != null && !_player.IsDead &&
                Vector2.Distance(transform.position, _player.transform.position) <= DetectionRadius;
        }

        private bool IsInFrontArc(float sourcePositionX)
        {
            float horizontalOffset = sourcePositionX - transform.position.x;
            return Mathf.Abs(horizontalOffset) <= 0.001f || horizontalOffset * _facingDirection > 0f;
        }

        private void FacePlayer()
        {
            float horizontalDistance = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(horizontalDistance) > 0.001f)
            {
                _facingDirection = horizontalDistance < 0f ? -1f : 1f;
            }
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

        private static DamageCalculationRequest CreateDamageRequest(CombatStats stats, decimal damage, int targetDefenseBonus = 0)
        {
            return new DamageCalculationRequest(
                baseDamage: damage,
                rarityMultiplier: 1m,
                upgradeMultiplier: 1m,
                offensiveBonusPercent: 0m,
                defensePenetration: 0,
                incomingDamageReductionPercent: 0m,
                targetStats: stats,
                targetIsInvulnerable: false,
                targetDefenseBonus: targetDefenseBonus);
        }
    }
}
