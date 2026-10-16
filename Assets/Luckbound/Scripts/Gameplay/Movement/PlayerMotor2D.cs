using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Presentation;
using Luckbound.Content;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Venator;
using Luckbound.Gameplay.Rooms;
using UnityEngine;
using EntityId = Luckbound.Core.Identifiers.EntityId;

namespace Luckbound.Gameplay.Movement
{
    /// <summary>
    /// Moves a dynamic actor through Unity 2D physics using one actor-scoped command per fixed tick.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerMotor2D : MonoBehaviour, IPlayerVisualStateSource, IPlayerHudStateSource
    {
        public const float MaximumHorizontalSpeed = 6f;
        public const float GroundAcceleration = 60f;
        public const float GroundBraking = 80f;
        public const float AirControl = 0.85f;
        public const float JumpHeight = 3f;
        public const float TimeToJumpApex = 0.4f;
        public const float AscendingGravity = 37.5f;
        public const float DescendingGravity = 56.25f;
        public const float MaximumFallSpeed = 22f;
        public const float CoyoteDuration = 0.1f;
        public const float JumpBufferDuration = 0.12f;
        public const float JumpCutMultiplier = 0.5f;
        public const float DodgeSpeed = 12f;
        public const float DodgeDuration = 0.18f;
        public const float DodgeInvulnerabilityDuration = 0.12f;
        public const float DodgeCooldownDuration = 0.8f;
        public const int DirectHitProtectionTicks = 27;

        private const float GroundProbeDistance = 0.08f;
        private const float GroundNormalThreshold = 0.65f;
        private const float GroundedVerticalSpeedLimit = 0.05f;
        private const float MovementDeadZone = 0.001f;

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];
        private readonly Collider2D[] _meleeOverlaps = new Collider2D[16];

        private MeleeAttackState _meleeAttackState;
        private MeleeAttackSnapshot _meleeAttackSnapshot;
        private ActorDamageState _damageState;
        private HealthPotionUseState _healthPotionUseState;
        private SkillUseState _skillUseState;
        private VenatorCombatState _venatorCombatState;
        private VenatorSkillLoadout _venatorSkillLoadout;
        private VenatorCombatResult _lastVenatorCombatResult;
        private VenatorBasicAttackSnapshot _venatorBasicAttackSnapshot;
        private VenatorRoomBinding _venatorRoomBinding;
        private ActorId _actorId;
        private Collider2D _collider;
        private ContactFilter2D _groundContactFilter;
        private ContactFilter2D _meleeContactFilter;
        private LayerMask _meleeTargetLayers;
        private bool _initialized;
        private IPlayerInputSource _inputSource;
        private PhysicsMaterial2D _movementMaterial;
        private PhysicsMaterial2D _previousMaterial;
        private Rigidbody2D _body;
        private float _coyoteTimeRemaining;
        private float _dodgeCooldownRemaining;
        private float _dodgeDirection = 1f;
        private float _dodgeElapsed;
        private float _facingDirection = 1f;
        private float _jumpBufferRemaining;
        private LayerMask _venatorTargetLayers;
        private bool _dodgeActive;
        private bool _dodgeInvulnerabilityActive;
        private bool _dodgeSpeedRecoveryPending;
        private int _stunTicksRemaining;
        private long _tick;
        private long _nextVenatorEnergyRecoveryTick;

        public bool IsGrounded { get; private set; }

        public bool IsDodging { get; private set; }

        public bool IsInvulnerable => _dodgeInvulnerabilityActive ||
            (_damageState != null && _damageState.IsDirectImpactProtectedAt(Math.Max(0L, _tick - 1)));

        public bool IsDead => _damageState != null && _damageState.IsDead;

        public bool IsStunned => _stunTicksRemaining > 0;

        public int StunTicksRemaining => _stunTicksRemaining;

        public int CurrentHealth => _damageState != null ? _damageState.CurrentHealth : 0;

        public CombatStats CombatStats => _damageState != null ? _damageState.Stats : null;

        public SkillUseState SkillUses => _skillUseState;

        public bool IsVenator => _venatorCombatState != null;

        public bool IsPreparingVenatorSkill => IsVenator && _skillUseState.IsPreparing;

        public VenatorCombatResult LastVenatorCombatResult => _lastVenatorCombatResult;

        public VenatorBasicAttackSnapshot CurrentVenatorBasicAttack => _venatorBasicAttackSnapshot;

        public int VenatorBasicAttackActivationCount { get; private set; }

        public int VenatorSkillActivationCount { get; private set; }

        public int VenatorProjectileSpawnCount { get; private set; }

        public int VenatorRootTrapSpawnCount { get; private set; }

        public int CurrentEnergy => _skillUseState != null ? _skillUseState.CurrentEnergy : 0;

        public int MaximumEnergy => _skillUseState != null ? _skillUseState.MaximumEnergy : 0;

        public int HealthPotionCharges => _healthPotionUseState != null ? _healthPotionUseState.CurrentCharges : 0;

        public bool IsPreparingHealthPotion => _healthPotionUseState != null && _healthPotionUseState.IsPreparing;

        public int HealthPotionPreparationTicksRemaining =>
            _healthPotionUseState != null ? _healthPotionUseState.PreparationTicksRemaining : 0;

        public ActorId ActorId => _actorId;

        public long SimulationTick => Math.Max(0L, _tick - 1);

        public int SuccessfulJumpCount { get; private set; }

        public int DodgeActivationCount { get; private set; }

        public int MeleeAttackActivationCount { get; private set; }

        public bool IsAttacking { get; private set; }

        public MeleeAttackSnapshot CurrentMeleeAttack => _meleeAttackSnapshot;

        public MeleeHitWindow CurrentMeleeHitWindow => _meleeAttackSnapshot.HitWindow;

        public bool IsDodgeReady => !IsDead && !IsStunned && !_dodgeActive && _dodgeCooldownRemaining <= 0f;

        public float CoyoteTimeRemaining => _coyoteTimeRemaining;

        public float DodgeCooldownRemaining => _dodgeCooldownRemaining;

        public float JumpBufferRemaining => _jumpBufferRemaining;

        public Vector2 Velocity => _body != null ? _body.linearVelocity : Vector2.zero;

        public float FacingDirection => _facingDirection;

        public PlayerVisualSnapshot CaptureVisualState()
        {
            Vector2 velocity = Velocity;
            PlayerVisualState state = PlayerVisualStateResolver.Resolve(
                isDead: IsDead,
                isStunned: IsStunned,
                isDodging: IsDodging,
                isAttacking: IsAttacking,
                isGrounded: IsGrounded,
                horizontalVelocity: velocity.x);
            return new PlayerVisualSnapshot(state, velocity.x, velocity.y, _facingDirection);
        }

        public PlayerHudSnapshot CaptureHudState()
        {
            CombatStats stats = CombatStats;
            return new PlayerHudSnapshot(
                CurrentHealth,
                stats != null ? stats.MaximumHealth : 0,
                CurrentEnergy,
                MaximumEnergy,
                HealthPotionCharges,
                _healthPotionUseState != null ? _healthPotionUseState.MaximumCharges : 0,
                HealthPotionPreparationTicksRemaining);
        }

        public int MeleeImpactsQueuedCount { get; private set; }

        public void Initialize(ActorId actorId, IPlayerInputSource inputSource, LayerMask groundLayers, CombatStats combatStats = null, LayerMask enemyLayers = default)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("PlayerMotor2D can only be initialized once.");
            }

            if (!actorId.IsValid)
            {
                throw new ArgumentException("PlayerMotor2D requires a valid actor identifier.", nameof(actorId));
            }

            if (inputSource == null)
            {
                throw new ArgumentNullException(nameof(inputSource));
            }

            if (groundLayers.value == 0)
            {
                throw new ArgumentException("PlayerMotor2D requires at least one ground layer.", nameof(groundLayers));
            }

            if (Mathf.Abs(Physics2D.gravity.x) > 0.01f || Physics2D.gravity.y >= -0.01f)
            {
                throw new InvalidOperationException("PlayerMotor2D requires vertical downward 2D gravity with no horizontal component.");
            }

            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            if (_body == null || _collider == null)
            {
                throw new InvalidOperationException("PlayerMotor2D requires a Rigidbody2D and an independent Collider2D.");
            }

            _actorId = actorId;
            _inputSource = inputSource;
            _damageState = new ActorDamageState(
                combatStats ?? new CombatStats(maximumHealth: 100, maximumEnergy: 50, defense: 0),
                DirectHitProtectionTicks);
            _skillUseState = new SkillUseState(_damageState.Stats.MaximumEnergy, _damageState.Stats.MaximumEnergy);
            _healthPotionUseState = new HealthPotionUseState();
            _meleeAttackState = new MeleeAttackState(
                MeleeAttackProfile.BellatorWornSword,
                MeleeComboSequence.CreateBellator());
            _body.bodyType = RigidbodyType2D.Dynamic;
            _body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            _groundContactFilter = new ContactFilter2D();
            _groundContactFilter.SetLayerMask(groundLayers);
            _groundContactFilter.useTriggers = false;

            _meleeTargetLayers = enemyLayers.value != 0 ? enemyLayers : LayerMask.GetMask("Enemies");
            _meleeContactFilter = new ContactFilter2D();
            _meleeContactFilter.SetLayerMask(_meleeTargetLayers);
            _meleeContactFilter.useTriggers = false;

            _movementMaterial = new PhysicsMaterial2D("Luckbound Actor Surface")
            {
                friction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine2D.Minimum,
                bounceCombine = PhysicsMaterialCombine2D.Minimum
            };
            _previousMaterial = _collider.sharedMaterial;
            _collider.sharedMaterial = _movementMaterial;

            _initialized = true;
        }

        /// <summary>
        /// Binds the Venator combat adapter to this initialized actor and one live room.
        /// Skill values always come from the authoritative Skills.json catalog.
        /// </summary>
        public void ConfigureVenator(
            IEnumerable<string> selectedSkillIds,
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope,
            RoomLifetimeScope roomLifetimeScope,
            LayerMask enemyLayers = default)
        {
            TextAsset catalogAsset = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            if (catalogAsset == null)
            {
                throw new InvalidOperationException("The authoritative Skills.json catalog is missing from Resources.");
            }

            ConfigureVenator(
                SkillCatalogJsonLoader.Load(catalogAsset.text),
                selectedSkillIds,
                projectilePool,
                projectileScope,
                roomLifetimeScope,
                enemyLayers);
        }

        /// <summary>Test and composition overload for a catalog already loaded from Skills.json.</summary>
        public void ConfigureVenator(
            SkillCatalog catalog,
            IEnumerable<string> selectedSkillIds,
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope,
            RoomLifetimeScope roomLifetimeScope,
            LayerMask enemyLayers = default)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("PlayerMotor2D must be initialized before configuring Venator combat.");
            }

            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (projectilePool == null) throw new ArgumentNullException(nameof(projectilePool));
            if (projectileScope == null) throw new ArgumentNullException(nameof(projectileScope));
            if (roomLifetimeScope == null) throw new ArgumentNullException(nameof(roomLifetimeScope));
            if (projectileScope.IsDisposed || projectileScope.Pool != projectilePool)
            {
                throw new ArgumentException("Venator projectiles require a live room scope attached to the supplied pool.", nameof(projectileScope));
            }

            LayerMask targetLayers = enemyLayers.value != 0 ? enemyLayers : _meleeTargetLayers;
            if (targetLayers.value == 0)
            {
                throw new ArgumentException("Venator projectiles and traps require at least one enemy layer.", nameof(enemyLayers));
            }

            var loadout = new VenatorSkillLoadout(catalog, selectedSkillIds);
            if (_venatorSkillLoadout != null &&
                (!string.Equals(_venatorSkillLoadout.Skill1.Id, loadout.Skill1.Id, StringComparison.Ordinal) ||
                 !string.Equals(_venatorSkillLoadout.Skill2.Id, loadout.Skill2.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("A Venator loadout cannot change during an expedition.");
            }

            if (_venatorRoomBinding != null && !_venatorRoomBinding.IsDisposed)
            {
                throw new InvalidOperationException("The previous Venator room must be disposed before binding another room.");
            }

            var trapPoolObject = new GameObject("Venator Root Trap Pool");
            VenatorRootTrapPool2D trapPool = trapPoolObject.AddComponent<VenatorRootTrapPool2D>();
            trapPool.Initialize(_actorId, targetLayers);
            VenatorRoomBinding binding = null;
            try
            {
                roomLifetimeScope.OwnActor(trapPoolObject);
                binding = new VenatorRoomBinding(this, projectileScope, trapPool);
                roomLifetimeScope.OwnResource(binding);
            }
            catch
            {
                if (binding != null) binding.Dispose();
                else trapPool.Dispose();
                Destroy(trapPoolObject);
                throw;
            }

            if (_venatorCombatState == null)
            {
                _venatorSkillLoadout = loadout;
                _venatorCombatState = new VenatorCombatState(_actorId, loadout, _skillUseState);
                _nextVenatorEnergyRecoveryTick = _tick + 60;
            }

            _venatorRoomBinding = binding;
            _venatorTargetLayers = targetLayers;
            _venatorBasicAttackSnapshot = default;
            _lastVenatorCombatResult = default;
            _meleeAttackState.Interrupt();
            _meleeAttackSnapshot = default;
            IsAttacking = false;
        }

        public bool IsVenatorSkillReady(string skillId)
        {
            return IsVenator && _venatorSkillLoadout.Contains(skillId) &&
                _skillUseState.IsReady(skillId, SimulationTick);
        }

        public DirectImpactOutcome ReceiveDirectImpact(
            HitId hitId,
            DamageCalculationRequest request,
            long impactTick,
            Vector2 knockbackVelocity,
            int stunDurationTicks = 0)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("PlayerMotor2D must be initialized before it can receive damage.");
            }

            if (float.IsNaN(knockbackVelocity.x) || float.IsInfinity(knockbackVelocity.x) ||
                float.IsNaN(knockbackVelocity.y) || float.IsInfinity(knockbackVelocity.y))
            {
                throw new ArgumentOutOfRangeException(nameof(knockbackVelocity), "Knockback velocity must be finite.");
            }

            if (stunDurationTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stunDurationTicks));
            }

            DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                hitId,
                request,
                impactTick,
                additionalInvulnerability: _dodgeInvulnerabilityActive);
            if (outcome.Status != DirectImpactStatus.Applied)
            {
                return outcome;
            }

            _healthPotionUseState.CancelPreparation();
            if (_venatorCombatState != null && _skillUseState.IsPreparing)
            {
                _venatorCombatState.Interrupt();
            }

            _body.linearVelocity = knockbackVelocity;
            if (stunDurationTicks > 0 && !outcome.IsDead)
            {
                _stunTicksRemaining = Math.Max(_stunTicksRemaining, stunDurationTicks);
                InterruptActions();
            }

            if (outcome.IsDead)
            {
                _stunTicksRemaining = 0;
                InterruptActions();
            }

            return outcome;
        }

        private void OnDestroy()
        {
            if (_venatorRoomBinding != null)
            {
                _venatorRoomBinding.Dispose();
                _venatorRoomBinding = null;
            }

            if (_movementMaterial == null)
            {
                return;
            }

            if (_collider != null && _collider.sharedMaterial == _movementMaterial)
            {
                _collider.sharedMaterial = _previousMaterial;
            }

            Destroy(_movementMaterial);
            _movementMaterial = null;
            _previousMaterial = null;
        }

        private void FixedUpdate()
        {
            if (!_initialized || Time.timeScale <= 0f)
            {
                return;
            }

            long tick = _tick++;
            PlayerCommand command = _inputSource.Consume(tick);
            if (command.ActorId != _actorId)
            {
                throw new InvalidOperationException("The input command belongs to a different actor.");
            }

            IsGrounded = UpdateGrounded();
            float deltaTime = Time.fixedDeltaTime;

            if (IsGrounded)
            {
                _coyoteTimeRemaining = CoyoteDuration;
            }
            else if (_coyoteTimeRemaining > 0f)
            {
                _coyoteTimeRemaining = Mathf.Max(0f, _coyoteTimeRemaining - deltaTime);
            }

            if (_dodgeCooldownRemaining > 0f)
            {
                _dodgeCooldownRemaining = Mathf.Max(0f, _dodgeCooldownRemaining - deltaTime);
            }

            if (IsDead)
            {
                _healthPotionUseState.Update(tick, requested: false, currentHealth: CurrentHealth,
                    maximumHealth: CombatStats.MaximumHealth, interrupted: true);
                InterruptActions();
                TickVenatorCommand(command, canAct: false);
                ApplyRestrictedMotion(deltaTime);
                return;
            }

            if (IsStunned)
            {
                _healthPotionUseState.Update(tick, requested: false, currentHealth: CurrentHealth,
                    maximumHealth: CombatStats.MaximumHealth, interrupted: true);
                _stunTicksRemaining--;
                InterruptActions();
                TickVenatorCommand(command, canAct: false);
                ApplyRestrictedMotion(deltaTime);
                return;
            }

            bool dodgeStartedThisTick = command.WasPressed(PlayerAction.Dodge) && IsDodgeReady;
            HealthPotionUseOutcome potionOutcome = _healthPotionUseState.Update(
                tick,
                requested: command.WasPressed(PlayerAction.Consumable),
                currentHealth: CurrentHealth,
                maximumHealth: CombatStats.MaximumHealth,
                interrupted: dodgeStartedThisTick);
            if (potionOutcome.HealthRestored > 0)
            {
                _damageState.RestoreHealth(potionOutcome.HealthRestored);
            }

            if (command.WasPressed(PlayerAction.Jump))
            {
                _jumpBufferRemaining = JumpBufferDuration;
            }
            else if (_jumpBufferRemaining > 0f)
            {
                _jumpBufferRemaining = Mathf.Max(0f, _jumpBufferRemaining - deltaTime);
            }

            float input = command.MoveX;
            if (Mathf.Abs(input) > MovementDeadZone)
            {
                _facingDirection = Mathf.Sign(input);
            }

            if (dodgeStartedThisTick)
            {
                _dodgeActive = true;
                _dodgeElapsed = 0f;
                _dodgeDirection = Mathf.Abs(input) > MovementDeadZone ? Mathf.Sign(input) : _facingDirection;
                _dodgeCooldownRemaining = DodgeCooldownDuration;
                DodgeActivationCount++;
            }

            float attackMovementMultiplier;
            if (IsVenator)
            {
                bool higherPriorityActionActive = potionOutcome.Status == HealthPotionUseStatus.Preparing ||
                    potionOutcome.Status == HealthPotionUseStatus.Healed;
                TickVenatorCommand(command, canAct: !_dodgeActive && !higherPriorityActionActive);
                attackMovementMultiplier = IsAttacking ? (float)_venatorBasicAttackSnapshot.MovementSpeedMultiplier : 1f;
                _meleeAttackSnapshot = default;
            }
            else
            {
                _meleeAttackSnapshot = _meleeAttackState.Tick(
                    command.Tick,
                    attackPressed: command.WasPressed(PlayerAction.Attack) && !_dodgeActive,
                    dodgeAccepted: dodgeStartedThisTick);
                IsAttacking = _meleeAttackSnapshot.IsAttacking && !_dodgeActive;
                if (_meleeAttackSnapshot.WasStartedThisTick)
                {
                    MeleeAttackActivationCount++;
                }

                attackMovementMultiplier = IsAttacking ? (float)_meleeAttackSnapshot.MovementSpeedMultiplier : 1f;
            }

            if (_meleeAttackSnapshot.HitWindow != null && _meleeTargetLayers.value != 0)
            {
                CollectMeleeImpacts(_meleeAttackSnapshot.HitWindow);
            }

            float targetSpeed = input * MaximumHorizontalSpeed * attackMovementMultiplier;
            float acceleration = Mathf.Abs(input) > MovementDeadZone ? GroundAcceleration : GroundBraking;
            if (!IsGrounded)
            {
                acceleration *= AirControl;
            }

            Vector2 velocity = _body.linearVelocity;
            if (_dodgeActive)
            {
                IsDodging = true;
                _dodgeInvulnerabilityActive = _dodgeElapsed + (deltaTime * 0.5f) < DodgeInvulnerabilityDuration;
                velocity.x = _dodgeDirection * DodgeSpeed;
                _jumpBufferRemaining = 0f;
                _dodgeElapsed += deltaTime;
                if (_dodgeElapsed >= DodgeDuration)
                {
                    _dodgeActive = false;
                    _dodgeSpeedRecoveryPending = true;
                }
            }
            else
            {
                IsDodging = false;
                _dodgeInvulnerabilityActive = false;
                if (_dodgeSpeedRecoveryPending)
                {
                    velocity.x = _dodgeDirection * MaximumHorizontalSpeed;
                    _dodgeSpeedRecoveryPending = false;
                }

                velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * deltaTime);

                if (_jumpBufferRemaining > 0f && _coyoteTimeRemaining > 0f)
                {
                    velocity.y = Mathf.Sqrt(2f * AscendingGravity * JumpHeight);
                    _jumpBufferRemaining = 0f;
                    _coyoteTimeRemaining = 0f;
                    IsGrounded = false;
                    SuccessfulJumpCount++;
                }

                if (command.WasReleased(PlayerAction.Jump) && velocity.y > 0f)
                {
                    velocity.y *= JumpCutMultiplier;
                }
            }

            ApplyVerticalGravity(ref velocity, deltaTime);
            _body.linearVelocity = velocity;
        }

        private void InterruptActions()
        {
            if (_meleeAttackState != null)
            {
                _meleeAttackState.Interrupt();
            }

            _meleeAttackSnapshot = default;
            if (_venatorCombatState != null)
            {
                _venatorCombatState.Interrupt();
            }

            _venatorBasicAttackSnapshot = default;
            IsDodging = false;
            IsAttacking = false;
            _dodgeActive = false;
            _dodgeInvulnerabilityActive = false;
            _dodgeSpeedRecoveryPending = false;
            _jumpBufferRemaining = 0f;
        }

        private void TickVenatorCommand(PlayerCommand command, bool canAct)
        {
            if (_venatorCombatState == null) return;

            bool roomReady = _venatorRoomBinding != null && !_venatorRoomBinding.IsDisposed;
            VenatorCombatResult result = _venatorCombatState.Tick(command, isAlive: canAct && roomReady);
            _lastVenatorCombatResult = result;
            _venatorBasicAttackSnapshot = result.BasicAttack;
            IsAttacking = result.BasicAttack.IsAttacking && canAct && roomReady && !_dodgeActive;

            if (result.SkillOutcome.EnergySpent > 0)
            {
                _nextVenatorEnergyRecoveryTick = command.Tick + 60;
            }
            else if (roomReady && !IsDead)
            {
                RecoverVenatorEnergy(command.Tick);
            }

            if (result.BasicAttack.WasStartedThisTick)
            {
                SpawnVenatorProjectile(
                    VenatorBasicAttackProfile.Shortbow.Projectile,
                    0m);
                VenatorBasicAttackActivationCount++;
            }

            VenatorSkillActivation activation = result.SkillActivation;
            if (activation == null) return;

            VenatorSkillActivationCount++;
            if (activation.EffectKind == SkillEffectKind.RootTrap)
            {
                _venatorRoomBinding.RootTrapPool.Spawn(
                    activation.RootTrap,
                    RootTrapPosition());
                VenatorRootTrapSpawnCount++;
                return;
            }

            for (int index = 0; index < activation.Projectiles.Count; index++)
            {
                VenatorProjectileShot shot = activation.Projectiles[index];
                SpawnVenatorProjectile(shot.Definition, shot.AngleOffsetDegrees);
            }
        }

        private void RecoverVenatorEnergy(long tick)
        {
            if (_skillUseState.CurrentEnergy >= _skillUseState.MaximumEnergy)
            {
                _nextVenatorEnergyRecoveryTick = tick + 12;
                return;
            }

            if (tick < _nextVenatorEnergyRecoveryTick) return;

            long elapsedRecoveryTicks = tick - _nextVenatorEnergyRecoveryTick;
            int recoveryAmount = 1 + (int)(elapsedRecoveryTicks / 12);
            _skillUseState.SetCurrentEnergy(Math.Min(
                _skillUseState.MaximumEnergy,
                _skillUseState.CurrentEnergy + recoveryAmount));
            _nextVenatorEnergyRecoveryTick += recoveryAmount * 12L;
        }

        private void SpawnVenatorProjectile(ProjectileDefinition definition, decimal angleOffsetDegrees)
        {
            if (_venatorRoomBinding == null || _venatorRoomBinding.IsDisposed)
            {
                throw new InvalidOperationException("Venator projectiles require a live room binding.");
            }

            Vector2 direction = new Vector2(_facingDirection, 0f);
            float radians = (float)angleOffsetDegrees * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians);
            float sine = Mathf.Sin(radians);
            direction = new Vector2(
                (direction.x * cosine) - (direction.y * sine),
                (direction.x * sine) + (direction.y * cosine));
            Vector2 spawnPosition = new Vector2(
                transform.position.x + (_facingDirection * 0.55f),
                transform.position.y + 0.25f);

            _venatorRoomBinding.ProjectileScope.Pool.Spawn(
                definition,
                EntityId.New(),
                _actorId,
                spawnPosition,
                direction,
                _venatorTargetLayers,
                _venatorRoomBinding.ProjectileScope);
            VenatorProjectileSpawnCount++;
        }

        private Vector2 RootTrapPosition()
        {
            return new Vector2(
                transform.position.x + (_facingDirection * 1.2f),
                transform.position.y - 0.65f);
        }

        internal void OnVenatorRoomDisposed(VenatorRoomBinding binding)
        {
            if (_venatorRoomBinding != binding) return;

            _venatorCombatState?.Interrupt();
            _venatorRoomBinding = null;
            _venatorBasicAttackSnapshot = default;
            IsAttacking = false;
        }

        private void CollectMeleeImpacts(MeleeHitWindow hitWindow)
        {
            Vector2 center = new Vector2(
                transform.position.x + _facingDirection * 0.9f,
                transform.position.y + 0.9f);
            int overlapCount = Physics2D.OverlapBox(center, new Vector2(1.35f, 1.25f), 0f, _meleeContactFilter, _meleeOverlaps);
            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D overlap = _meleeOverlaps[index];
                if (overlap == null) continue;

                ProjectileImpactTarget2D target = overlap.GetComponentInParent<ProjectileImpactTarget2D>();
                if (target == null || !target.IsInitialized || target.ActorId == _actorId ||
                    hitWindow.TryRegisterTarget(target.ActorId) == false)
                {
                    continue;
                }

                var impact = new MeleeImpact(
                    hitWindow.Id,
                    _actorId,
                    target.ActorId,
                    hitWindow.BaseDamage * hitWindow.DamageMultiplier,
                    transform.position.x,
                    CurrentPhysicsTick());
                target.QueueMeleeImpact(impact);
                MeleeImpactsQueuedCount++;
            }
        }

        private static long CurrentPhysicsTick()
        {
            return Math.Max(0L, (long)Math.Round(Time.fixedTimeAsDouble / Time.fixedDeltaTime));
        }

        private void ApplyRestrictedMotion(float deltaTime)
        {
            Vector2 velocity = _body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, 0f, GroundBraking * deltaTime);
            ApplyVerticalGravity(ref velocity, deltaTime);
            _body.linearVelocity = velocity;
        }

        private void ApplyVerticalGravity(ref Vector2 velocity, float deltaTime)
        {
            float worldGravity = -Physics2D.gravity.y;
            if (velocity.y > 0f)
            {
                _body.gravityScale = AscendingGravity / worldGravity;
                return;
            }

            float nextFallSpeed = velocity.y - DescendingGravity * deltaTime;
            if (nextFallSpeed < -MaximumFallSpeed)
            {
                velocity.y = -MaximumFallSpeed;
                _body.gravityScale = 0f;
                return;
            }

            _body.gravityScale = DescendingGravity / worldGravity;
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
    }
}
