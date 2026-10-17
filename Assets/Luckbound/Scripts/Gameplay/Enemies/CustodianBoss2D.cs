using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Magus;
using Luckbound.Gameplay.Movement;
using UnityEngine;

namespace Luckbound.Gameplay.Enemies
{
    public enum CustodianPhase
    {
        PhaseOne = 1,
        Transition = 2,
        PhaseTwo = 3,
        Defeated = 4
    }

    public enum CustodianAttackPattern
    {
        Cut = 0,
        Wave = 1,
        Charge = 2
    }

    public enum CustodianBehaviorState
    {
        Dormant = 0,
        Telegraphing = 1,
        Executing = 2,
        Recovery = 3,
        PhaseTransition = 4,
        Dead = 5
    }

    /// <summary>First sector boss prototype; deterministic 60 Hz telegraphs and attacks feed normal combat damage.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D), typeof(SpriteRenderer))]
    public sealed class CustodianBoss2D : MonoBehaviour
    {
        public const int AttackTelegraphMinimumTicks = 33;
        public const int PhaseTransitionTicks = 120;
        public const float DetectionRadius = 8f;
        public const float ChargeSpeed = 8f;
        public const int CutActiveTicks = 12;
        public const int WaveSecondSpawnDelayTicks = 24;
        public const int WaveActiveTicks = 36;
        public const int ChargeMaximumTicks = 36;
        public const float WaveSpeed = 8f;
        public const int WaveLifetimeTicks = 150;
        public const float ArenaMinimumX = -15f;
        public const float ArenaMaximumX = 15f;

        private static readonly CustodianAttackPattern[] PatternOrder =
        {
            CustodianAttackPattern.Cut,
            CustodianAttackPattern.Wave,
            CustodianAttackPattern.Charge
        };

        private readonly Collider2D[] _cutOverlaps = new Collider2D[8];
        private readonly Queue<CustodianWaveHazard2D> _activeWaves = new Queue<CustodianWaveHazard2D>();
        private ActorDamageState _damageState;
        private ActorId _actorId;
        private Collider2D _collider;
        private CustodianBossDefinition _definition;
        private bool _initialized;
        private bool _cutResolved;
        private bool _chargeContactOccurred;
        private int _playerLayerMask;
        private int _telegraphTicksRemaining;
        private int _phaseTransitionTicksRemaining;
        private int _recoveryTicksRemaining;
        private int _recoveryDurationTicks;
        private int _attackElapsedTicks;
        private int _nextPatternIndex;
        private int _waveSpawnCount;
        private int _phaseTwoWaveSpawnCount;
        private int _secondWaveTicksRemaining;
        private long _simulationTick;
        private float _facingDirection = 1f;
        private PlayerMotor2D _player;
        private Rigidbody2D _body;
        private SpriteRenderer _renderer;
        private GameObject _wavePrefab;
        private Transform _waveParent;

        public CustodianPhase Phase { get; private set; } = CustodianPhase.PhaseOne;
        public CustodianAttackPattern CurrentPattern { get; private set; } = CustodianAttackPattern.Cut;
        public CustodianBehaviorState State { get; private set; } = CustodianBehaviorState.Dormant;
        public string BossId => _definition == null ? string.Empty : _definition.BossId;
        public int MaximumHealth => _definition == null ? 0 : _definition.MaximumHealth;
        public int CurrentHealth => _damageState == null ? 0 : _damageState.CurrentHealth;
        public int Defense => _definition == null ? 0 : _definition.Defense;
        public int TelegraphTicksRemaining => _telegraphTicksRemaining;
        public int PhaseTransitionTicksRemaining => _phaseTransitionTicksRemaining;
        public int RecoveryTicksRemaining => _recoveryTicksRemaining;
        public int CurrentRecoveryDurationTicks => _recoveryDurationTicks;
        public int CutPatternCount { get; private set; }
        public int WavePatternCount { get; private set; }
        public int ChargePatternCount { get; private set; }
        public int PhaseTwoWavePatternCount { get; private set; }
        public int WaveSpawnCount => _waveSpawnCount;
        public int PhaseTwoWaveSpawnCount => _phaseTwoWaveSpawnCount;
        public int TelegraphStartCount { get; private set; }
        public int MeleeImpactCount { get; private set; }
        public int ProjectileImpactCount { get; private set; }
        public int SuccessfulPlayerHitCount { get; private set; }
        public bool IsDead => State == CustodianBehaviorState.Dead;

        public void Initialize(
            ActorId actorId,
            PlayerMotor2D player,
            CustodianBossDefinition definition,
            GameObject wavePrefab,
            Transform waveParent)
        {
            if (_initialized) throw new InvalidOperationException("CustodianBoss2D can only be initialized once.");
            if (!actorId.IsValid) throw new ArgumentException("CustodianBoss2D requires a valid actor identifier.", nameof(actorId));
            if (player == null || player.CombatStats == null) throw new ArgumentNullException(nameof(player));
            if (actorId == player.ActorId) throw new ArgumentException("Boss and player actor identifiers must be different.", nameof(actorId));
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _wavePrefab = wavePrefab != null ? wavePrefab : throw new ArgumentNullException(nameof(wavePrefab));
            _waveParent = waveParent != null ? waveParent : throw new ArgumentNullException(nameof(waveParent));
            if (Physics2D.gravity.y >= -0.01f) throw new InvalidOperationException("CustodianBoss2D requires downward 2D gravity.");

            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _renderer = GetComponent<SpriteRenderer>();
            if (_body == null || _collider == null || _renderer == null)
                throw new InvalidOperationException("CustodianBoss2D requires Rigidbody2D, Collider2D and SpriteRenderer components.");

            _actorId = actorId;
            _player = player;
            _playerLayerMask = 1 << player.gameObject.layer;
            _damageState = new ActorDamageState(
                new CombatStats(definition.MaximumHealth, maximumEnergy: 0, defense: definition.Defense),
                directImpactProtectionTicks: 0);
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.gravityScale = 3f;

            ProjectileImpactTarget2D impactTarget = GetComponent<ProjectileImpactTarget2D>();
            if (impactTarget == null) impactTarget = gameObject.AddComponent<ProjectileImpactTarget2D>();
            if (impactTarget.IsInitialized && impactTarget.ActorId != actorId)
                throw new InvalidOperationException("Projectile target actor ID does not match the Custodian actor ID.");
            if (!impactTarget.IsInitialized) impactTarget.Initialize(actorId);
            _initialized = true;
            UpdateVisualState();
        }

        private void OnDestroy()
        {
            ProjectileImpactTarget2D target = GetComponent<ProjectileImpactTarget2D>();
            if (target != null) target.ClearPendingImpacts();
            ClearActiveWaves();
        }

        private void FixedUpdate()
        {
            if (!_initialized || Time.timeScale <= 0f || IsDead) return;

            long tick = _simulationTick++;
            ResolveMeleeImpacts(tick);
            if (IsDead) return;
            ResolveProjectileImpacts(tick);
            if (IsDead) return;
            if (TryBeginPhaseTransition())
            {
                _body.linearVelocity = Vector2.zero;
                UpdateVisualState();
                return;
            }

            if (State == CustodianBehaviorState.PhaseTransition)
            {
                TickPhaseTransition();
                UpdateVisualState();
                return;
            }

            Vector2 velocity = _body.linearVelocity;
            switch (State)
            {
                case CustodianBehaviorState.Dormant:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * Time.fixedDeltaTime);
                    if (CanDetectPlayer()) BeginTelegraph();
                    break;

                case CustodianBehaviorState.Telegraphing:
                    FacePlayer();
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * Time.fixedDeltaTime);
                    if (_telegraphTicksRemaining > 0) _telegraphTicksRemaining--;
                    if (_telegraphTicksRemaining == 0) BeginPattern();
                    break;

                case CustodianBehaviorState.Executing:
                    TickPattern(ref velocity);
                    break;

                case CustodianBehaviorState.Recovery:
                    velocity.x = Mathf.MoveTowards(velocity.x, 0f, PlayerMotor2D.GroundBraking * Time.fixedDeltaTime);
                    if (_recoveryTicksRemaining > 0) _recoveryTicksRemaining--;
                    if (_recoveryTicksRemaining == 0)
                    {
                        if (CanDetectPlayer()) BeginTelegraph();
                        else State = CustodianBehaviorState.Dormant;
                    }
                    break;
            }

            if (State == CustodianBehaviorState.Executing && CurrentPattern == CustodianAttackPattern.Charge)
            {
                velocity.x *= (float)GetWeaponSlowMultiplier(_player.SimulationTick);
            }

            _body.linearVelocity = velocity;
            UpdateVisualState();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!_initialized || State != CustodianBehaviorState.Executing ||
                CurrentPattern != CustodianAttackPattern.Charge || _chargeContactOccurred || collision == null) return;

            PlayerMotor2D player = collision.collider.GetComponentInParent<PlayerMotor2D>();
            if (player != _player || player.IsDead || player.CombatStats == null) return;
            _chargeContactOccurred = true;
            ApplyPlayerHit(_definition.ChargeDamage, HitId.New(), new Vector2(_facingDirection * 3f, 1f));
        }

        private void BeginTelegraph()
        {
            if (!CanDetectPlayer())
            {
                State = CustodianBehaviorState.Dormant;
                return;
            }

            CurrentPattern = PatternOrder[_nextPatternIndex % PatternOrder.Length];
            _nextPatternIndex++;
            FacePlayer();
            _telegraphTicksRemaining = _definition.TelegraphTicks;
            TelegraphStartCount++;
            _attackElapsedTicks = 0;
            _cutResolved = false;
            _chargeContactOccurred = false;
            _secondWaveTicksRemaining = -1;
            State = CustodianBehaviorState.Telegraphing;
        }

        private void BeginPattern()
        {
            State = CustodianBehaviorState.Executing;
            _attackElapsedTicks = 0;
            switch (CurrentPattern)
            {
                case CustodianAttackPattern.Cut:
                    CutPatternCount++;
                    break;
                case CustodianAttackPattern.Wave:
                    WavePatternCount++;
                    if (Phase == CustodianPhase.PhaseTwo) PhaseTwoWavePatternCount++;
                    SpawnWave();
                    _secondWaveTicksRemaining = Phase == CustodianPhase.PhaseTwo
                        ? WaveSecondSpawnDelayTicks
                        : -1;
                    break;
                case CustodianAttackPattern.Charge:
                    ChargePatternCount++;
                    _body.linearVelocity = new Vector2(_facingDirection * ChargeSpeed, 0f);
                    break;
            }
        }

        private void TickPattern(ref Vector2 velocity)
        {
            _attackElapsedTicks++;
            switch (CurrentPattern)
            {
                case CustodianAttackPattern.Cut:
                    if (!_cutResolved)
                    {
                        _cutResolved = true;
                        ResolveCutOverlap();
                    }
                    if (_attackElapsedTicks >= CutActiveTicks) BeginRecovery();
                    break;

                case CustodianAttackPattern.Wave:
                    if (_secondWaveTicksRemaining > 0)
                    {
                        _secondWaveTicksRemaining--;
                        if (_secondWaveTicksRemaining == 0) SpawnWave();
                    }
                    if (_attackElapsedTicks >= WaveActiveTicks) BeginRecovery();
                    break;

                case CustodianAttackPattern.Charge:
                    if (_chargeContactOccurred || _attackElapsedTicks >= ChargeMaximumTicks)
                    {
                        velocity.x = 0f;
                        BeginRecovery();
                    }
                    else
                    {
                        velocity.x = _facingDirection * ChargeSpeed;
                    }
                    break;
            }
        }

        private void ResolveCutOverlap()
        {
            Vector2 center = new Vector2(transform.position.x + _facingDirection * 1.1f, transform.position.y + 0.65f);
            var filter = new ContactFilter2D();
            filter.SetLayerMask(_playerLayerMask);
            filter.useTriggers = false;
            int count = Physics2D.OverlapBox(center, new Vector2(1.5f, 1.35f), 0f, filter, _cutOverlaps);
            for (int index = 0; index < count; index++)
            {
                Collider2D overlap = _cutOverlaps[index];
                if (overlap == null) continue;
                PlayerMotor2D player = overlap.GetComponentInParent<PlayerMotor2D>();
                if (player != _player) continue;
                ApplyPlayerHit(_definition.CutDamage, HitId.New(), new Vector2(_facingDirection * 2f, 1f));
                return;
            }
        }

        private void SpawnWave()
        {
            float direction = _facingDirection;
            Vector3 position = new Vector3(transform.position.x + direction * 0.8f, -3.25f, 0f);
            GameObject waveObject = Instantiate(_wavePrefab, position, Quaternion.identity, _waveParent);
            CustodianWaveHazard2D wave = waveObject.GetComponent<CustodianWaveHazard2D>();
            if (wave == null)
            {
                Destroy(waveObject);
                throw new InvalidOperationException("Custodian wave prefab is missing CustodianWaveHazard2D.");
            }

            wave.Initialize(_player, _actorId, _definition.WaveDamage, direction, WaveSpeed,
                WaveLifetimeTicks, ArenaMinimumX, ArenaMaximumX);
            _activeWaves.Enqueue(wave);
            _waveSpawnCount++;
            if (Phase == CustodianPhase.PhaseTwo) _phaseTwoWaveSpawnCount++;
        }

        private bool ApplyPlayerHit(int damage, HitId hitId, Vector2 knockback)
        {
            DirectImpactOutcome outcome = _player.ReceiveDirectImpact(
                hitId,
                CreateDamageRequest(_player.CombatStats, damage),
                _player.SimulationTick,
                knockback);
            if (outcome.Status != DirectImpactStatus.Applied) return false;
            SuccessfulPlayerHitCount++;
            return true;
        }

        private void BeginRecovery()
        {
            _body.linearVelocity = new Vector2(0f, _body.linearVelocity.y);
            _recoveryDurationTicks = Phase == CustodianPhase.PhaseTwo
                ? _definition.PhaseTwoRecoveryTicks
                : _definition.PhaseOneRecoveryTicks;
            _recoveryTicksRemaining = _recoveryDurationTicks;
            State = CustodianBehaviorState.Recovery;
        }

        private bool TryBeginPhaseTransition()
        {
            if (Phase != CustodianPhase.PhaseOne || CurrentHealth > MaximumHealth / 2) return false;
            Phase = CustodianPhase.Transition;
            State = CustodianBehaviorState.PhaseTransition;
            _phaseTransitionTicksRemaining = _definition.PhaseTransitionTicks;
            _telegraphTicksRemaining = 0;
            _recoveryTicksRemaining = 0;
            _attackElapsedTicks = 0;
            return true;
        }

        private void TickPhaseTransition()
        {
            if (_phaseTransitionTicksRemaining > 0) _phaseTransitionTicksRemaining--;
            if (_phaseTransitionTicksRemaining > 0) return;
            Phase = CustodianPhase.PhaseTwo;
            State = CustodianBehaviorState.Dormant;
        }

        private void EnterDeadState()
        {
            State = CustodianBehaviorState.Dead;
            Phase = CustodianPhase.Defeated;
            _telegraphTicksRemaining = 0;
            _phaseTransitionTicksRemaining = 0;
            _recoveryTicksRemaining = 0;
            ProjectileImpactTarget2D target = GetComponent<ProjectileImpactTarget2D>();
            if (target != null) target.ClearPendingImpacts();
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget != null) statusTarget.ResetRoomEffects();
            _collider.enabled = false;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;
            ClearActiveWaves();
            UpdateVisualState();
        }

        private void ResolveMeleeImpacts(long tick)
        {
            ProjectileImpactTarget2D target = GetComponent<ProjectileImpactTarget2D>();
            long physicsTick = Math.Max(0L, (long)Math.Round(Time.fixedTimeAsDouble / Time.fixedDeltaTime));
            while (target.TryDequeueMeleeImpactForTick(physicsTick, out MeleeImpact impact))
            {
                DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                    impact.HitId,
                    CreateDamageRequest(_damageState.Stats, impact.Damage),
                    tick);
                if (outcome.Status == DirectImpactStatus.Applied) MeleeImpactCount++;
                if (outcome.DeathOccurred) EnterDeadState();
                if (IsDead) return;
            }
        }

        private void ResolveProjectileImpacts(long tick)
        {
            ProjectileImpactTarget2D target = GetComponent<ProjectileImpactTarget2D>();
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget != null) statusTarget.QueuePendingBurnTicksAsImpacts();

            while (target.TryDequeueImpact(out ProjectileImpact impact))
            {
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
                if (outcome.DeathOccurred) EnterDeadState();
                if (IsDead) return;
            }
        }

        private bool CanDetectPlayer()
        {
            return _player != null && !_player.IsDead &&
                Vector2.Distance(transform.position, _player.transform.position) <= DetectionRadius;
        }

        private void FacePlayer()
        {
            float dx = _player.transform.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.001f) _facingDirection = dx < 0f ? -1f : 1f;
        }

        private void UpdateVisualState()
        {
            if (_renderer == null) return;
            if (Phase == CustodianPhase.Transition)
            {
                _renderer.color = new Color(0.72f, 0.48f, 0.92f, 1f);
                return;
            }
            if (Phase == CustodianPhase.PhaseTwo)
            {
                _renderer.color = State == CustodianBehaviorState.Telegraphing
                    ? new Color(1f, 0.72f, 0.28f, 1f)
                    : new Color(0.84f, 0.43f, 0.25f, 1f);
                return;
            }
            _renderer.color = State == CustodianBehaviorState.Telegraphing
                ? new Color(1f, 0.8f, 0.38f, 1f)
                : State == CustodianBehaviorState.Dead
                    ? new Color(0.32f, 0.34f, 0.42f, 0.7f)
                    : new Color(0.42f, 0.62f, 0.38f, 1f);
        }

        private void ClearActiveWaves()
        {
            while (_activeWaves.Count > 0)
            {
                CustodianWaveHazard2D wave = _activeWaves.Dequeue();
                if (wave != null) Destroy(wave.gameObject);
            }
        }

        private MagusStatusEffectTarget2D GetOrCreateWeaponStatusTarget()
        {
            MagusStatusEffectTarget2D statusTarget = GetComponent<MagusStatusEffectTarget2D>();
            if (statusTarget == null) statusTarget = gameObject.AddComponent<MagusStatusEffectTarget2D>();
            if (!statusTarget.IsInitialized) statusTarget.Initialize(GetComponent<ProjectileImpactTarget2D>());
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
            CombatStats targetStats,
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
                targetStats: targetStats,
                targetIsInvulnerable: false);
        }
    }

}
