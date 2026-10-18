using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Consumables;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Pacts;
using Luckbound.Core.Presentation;
using Luckbound.Content;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Combat.Magus;
using Luckbound.Gameplay.Combat.Venator;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Rooms;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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

        public const string HealthPotionItemId = "consumable.health_potion";
        public const string EnergyTonicItemId = "consumable.energy_tonic";
        public const string AntidoteItemId = "consumable.antidote";
        public const string BombItemId = "consumable.bomb";
        public const string FortuneBrewItemId = "consumable.fortune_brew";
        public const string TorchItemId = "consumable.torch";

        private const float GroundProbeDistance = 0.08f;
        private const float GroundNormalThreshold = 0.65f;
        private const float GroundedVerticalSpeedLimit = 0.05f;
        private const float MovementDeadZone = 0.001f;
        private const float MagusAimAssistConeDegrees = 35f;
        private static readonly float MagusAimAssistMinimumDot = Mathf.Cos(MagusAimAssistConeDegrees * Mathf.Deg2Rad);

        private readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];
        private readonly RaycastHit2D[] _chargeWallHits = new RaycastHit2D[4];
        private readonly RaycastHit2D[] _magusLineOfSightHits = new RaycastHit2D[1];
        private readonly Collider2D[] _meleeOverlaps = new Collider2D[16];
        private Collider2D[] _bellatorAreaOverlaps = new Collider2D[16];
        private Collider2D[] _magusAimOverlaps = new Collider2D[16];
        private readonly List<ProjectileImpactTarget2D> _bellatorAreaTargets = new List<ProjectileImpactTarget2D>();
        private readonly HashSet<ActorId> _bellatorAreaTargetIds = new HashSet<ActorId>();

        private MeleeAttackState _meleeAttackState;
        private MeleeAttackSnapshot _meleeAttackSnapshot;
        private ActorDamageState _damageState;
        private HealthPotionUseState _healthPotionUseState;
        private EnergyTonicUseState _energyTonicUseState;
        private IPlayerFortuneBrewRuntime _fortuneBrewRuntime;
        private Task<PlayerFortuneBrewCommitOutcome> _fortuneBrewCommitTask;
        private ItemInstanceId _fortuneBrewPendingItemInstanceId;
        private TransactionId _fortuneBrewPendingTransactionId;
        private bool _fortuneBrewCompletionReportedThisTick;
        private ItemCollector2D _itemCollector;
        private ItemInstanceId _healthPreparationInstanceId;
        private ItemInstanceId _energyPreparationInstanceId;
        private AntidoteEffectResolution? _antidoteProtection;
        private TorchLightActivation? _torchActivation;
        private Light2D _torchLight;
        private SkillUseState _skillUseState;
        private BellatorCombatState _bellatorCombatState;
        private BellatorSkillLoadout _bellatorSkillLoadout;
        private BellatorCombatResult _lastBellatorCombatResult;
        private VenatorCombatState _venatorCombatState;
        private VenatorSkillLoadout _venatorSkillLoadout;
        private VenatorCombatResult _lastVenatorCombatResult;
        private VenatorBasicAttackSnapshot _venatorBasicAttackSnapshot;
        private VenatorRoomBinding _venatorRoomBinding;
        private MagusCombatState _magusCombatState;
        private MagusSkillLoadout _magusSkillLoadout;
        private MagusCombatResult _lastMagusCombatResult;
        private MagusBasicAttackSnapshot _magusBasicAttackSnapshot;
        private MagusRoomBinding _magusRoomBinding;
        private string _configuredClassId;
        private WeaponProfile _equippedWeaponProfile;
        private WeaponBasicAttackProfile _weaponBasicAttackProfile;
        private ActorId _actorId;
        private string _raceId;
        private Collider2D _collider;
        private ContactFilter2D _groundContactFilter;
        private ContactFilter2D _meleeContactFilter;
        private ContactFilter2D _magusAimContactFilter;
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
        private LayerMask _magusTargetLayers;
        private LayerMask _groundLayers;
        private readonly HashSet<ActorId> _chargeHitTargets = new HashSet<ActorId>();
        private SkillDefinition _bellatorChargeSkill;
        private SkillDefinition _bellatorGuardSkill;
        private HitId _lastBellatorChargeHitId;
        private HitId _lastBellatorCircularCutHitId;
        private float _bellatorChargeDirection = 1f;
        private int _bellatorChargeTicksRemaining;
        private long _bellatorGuardStartsAtTick;
        private long _bellatorGuardExpiresAtTick;
        private long _nextBellatorEnergyRecoveryTick;
        private bool _dodgeActive;
        private bool _dodgeInvulnerabilityActive;
        private bool _dodgeSpeedRecoveryPending;
        private bool _chargeCollisionActiveThisPhysicsStep;
        private int _stunTicksRemaining;
        private long _tick;
        private long _nextVenatorEnergyRecoveryTick;
        private long _nextMagusEnergyRecoveryTick;
        private long _nextArmorEnergyRecoveryTick;
        private ArmorResolution _equippedArmorResolution;
        private float _armorMovementSpeedMultiplier = 1f;
        private int _armorEnergyRegenerationPerSecondBonus;
        private int _harmfulStatusDurationReductionPercent;

        public bool IsGrounded { get; private set; }

        public bool IsDodging { get; private set; }

        public bool IsInvulnerable => _dodgeInvulnerabilityActive ||
            (_damageState != null && _damageState.IsDirectImpactProtectedAt(Math.Max(0L, _tick - 1)));

        public bool IsDead => _damageState != null && _damageState.IsDead;

        public bool IsStunned => _stunTicksRemaining > 0;

        public int StunTicksRemaining => _stunTicksRemaining;

        public long AntidoteProtectionEndsAtTickExclusive => _antidoteProtection.HasValue
            ? _antidoteProtection.Value.ProtectionEndsAtTickExclusive
            : 0L;

        public bool IsProtectedFromStunAt(long tick) => _antidoteProtection.HasValue &&
            _antidoteProtection.Value.ProtectsAt(HarmfulStatusKind.Stun, tick);

        public int CurrentHealth => _damageState != null ? _damageState.CurrentHealth : 0;

        public CombatStats CombatStats => _damageState != null ? _damageState.Stats : null;

        public ArmorResolution EquippedArmorResolution => _equippedArmorResolution;

        public float ArmorMovementSpeedMultiplier => _armorMovementSpeedMultiplier;

        public float EffectiveMaximumHorizontalSpeed => MaximumHorizontalSpeed * _armorMovementSpeedMultiplier;

        public int ArmorEnergyRegenerationPerSecondBonus => _armorEnergyRegenerationPerSecondBonus;

        public int HarmfulStatusDurationReductionPercent => _harmfulStatusDurationReductionPercent;

        public SkillUseState SkillUses => _skillUseState;

        public bool IsBellator => _bellatorCombatState != null;

        public bool IsVenator => _venatorCombatState != null;

        public bool IsMagus => _magusCombatState != null;

        public bool IsPreparingBellatorCharge => _bellatorCombatState != null && _bellatorCombatState.IsPreparingCharge;

        public bool IsPreparingBellatorSkill => _bellatorCombatState != null && _bellatorCombatState.IsPreparingSkill;

        public bool IsBellatorGuardActive => IsBellatorGuardActiveAt(SimulationTick);

        public BellatorCombatResult LastBellatorCombatResult => _lastBellatorCombatResult;

        public int BellatorChargeActivationCount { get; private set; }

        public int BellatorCircularCutActivationCount { get; private set; }

        public int BellatorGuardActivationCount { get; private set; }

        public int BellatorSkillActivationCount { get; private set; }

        public bool IsCharging => _bellatorChargeTicksRemaining > 0;

        public int BellatorChargeTicksRemaining => _bellatorChargeTicksRemaining;

        public HitId LastBellatorChargeHitId => _lastBellatorChargeHitId;

        public HitId LastBellatorCircularCutHitId => _lastBellatorCircularCutHitId;

        public bool IsPreparingVenatorSkill => IsVenator && _skillUseState.IsPreparing;

        public VenatorCombatResult LastVenatorCombatResult => _lastVenatorCombatResult;

        public VenatorBasicAttackSnapshot CurrentVenatorBasicAttack => _venatorBasicAttackSnapshot;

        public int VenatorBasicAttackActivationCount { get; private set; }

        public int VenatorSkillActivationCount { get; private set; }

        public int VenatorProjectileSpawnCount { get; private set; }

        public int VenatorRootTrapSpawnCount { get; private set; }

        public bool IsPreparingMagusSkill => IsMagus && _skillUseState.IsPreparing;

        public MagusCombatResult LastMagusCombatResult => _lastMagusCombatResult;

        public MagusBasicAttackSnapshot CurrentMagusBasicAttack => _magusBasicAttackSnapshot;

        public int MagusBasicAttackActivationCount { get; private set; }

        public int MagusSkillActivationCount { get; private set; }

        public int MagusBasicAttackProjectileSpawnCount { get; private set; }

        public int CurrentEnergy => _skillUseState != null ? _skillUseState.CurrentEnergy : 0;

        public int MaximumEnergy => _skillUseState != null ? _skillUseState.MaximumEnergy : 0;

        public int HealthPotionCharges => _healthPotionUseState != null ? _healthPotionUseState.CurrentCharges : 0;

        public int EnergyTonicCharges => _energyTonicUseState != null ? _energyTonicUseState.CurrentCharges : 0;

        public PlayerConsumableUseOutcome LastConsumableUseOutcome { get; private set; }

        public bool IsTorchLightActive => _torchActivation.HasValue && _torchActivation.Value.IsActiveAt(SimulationTick);

        public long TorchLightEndsAtTickExclusive => _torchActivation.HasValue
            ? _torchActivation.Value.EndsAtTickExclusive
            : 0L;

        public bool IsPreparingHealthPotion => _healthPotionUseState != null && _healthPotionUseState.IsPreparing;

        public int HealthPotionPreparationTicksRemaining =>
            _healthPotionUseState != null ? _healthPotionUseState.PreparationTicksRemaining : 0;

        public ActorId ActorId => _actorId;

        public string RaceId => _raceId;

        public WeaponProfile EquippedWeaponProfile => _equippedWeaponProfile;
        public WeaponBasicAttackProfile EquippedWeaponBasicAttackProfile => _weaponBasicAttackProfile;

        public long SimulationTick => Math.Max(0L, _tick - 1);

        public int SuccessfulJumpCount { get; private set; }

        public int DodgeActivationCount { get; private set; }

        public int MeleeAttackActivationCount { get; private set; }

        public bool IsAttacking { get; private set; }

        public MeleeAttackSnapshot CurrentMeleeAttack => _meleeAttackSnapshot;

        /// <summary>
        /// Binds or updates the weapon selected by the expedition inventory. The profile must come from
        /// the authoritative Weapons.json catalog; an accepted attack and its reload remain unchanged.
        /// </summary>
        public void ConfigureEquippedWeapon(WeaponProfile weaponProfile, ItemRarity rarity = ItemRarity.Common,
            int upgradeLevel = 0)
        {
            if (!_initialized)
                throw new InvalidOperationException("PlayerMotor2D must be initialized before configuring its weapon.");
            if (weaponProfile == null) throw new ArgumentNullException(nameof(weaponProfile));
            if (_configuredClassId != null &&
                !string.Equals(_configuredClassId, weaponProfile.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The equipped weapon is incompatible with the configured player class.", nameof(weaponProfile));
            }

            WeaponBasicAttackProfile runtimeProfile = WeaponBasicAttackProfile.Create(weaponProfile, rarity, upgradeLevel);
            _equippedWeaponProfile = weaponProfile;
            _weaponBasicAttackProfile = runtimeProfile;
            ApplyWeaponBasicAttackProfile();
        }

        /// <summary>
        /// Binds the weapon selected in this run's equipped inventory slot. The profile is resolved and
        /// validated before any motor weapon state changes, so invalid run/catalog combinations are atomic.
        /// </summary>
        public void ConfigureEquippedWeapon(ExpeditionRunConfiguration run, WeaponCatalog weaponCatalog)
        {
            if (!_initialized)
                throw new InvalidOperationException("PlayerMotor2D must be initialized before configuring its weapon.");

            WeaponProfile equippedProfile = ResolveEquippedWeaponProfile(run, weaponCatalog);
            ConfigureEquippedWeapon(equippedProfile, run.Inventory.Weapon.Rarity, run.Inventory.Weapon.UpgradeLevel);
        }

        /// <summary>Resolves the authoritative profile for the weapon currently equipped by a run.</summary>
        public static WeaponProfile ResolveEquippedWeaponProfile(
            ExpeditionRunConfiguration run,
            WeaponCatalog weaponCatalog)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (weaponCatalog == null) throw new ArgumentNullException(nameof(weaponCatalog));
            if (run.Inventory == null ||
                !string.Equals(run.Inventory.ClassId, run.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The run must carry its own inventory for the selected class.", nameof(run));
            }

            InventoryItemStack equippedWeapon = run.Inventory.Weapon;
            if (equippedWeapon == null ||
                !run.Inventory.Catalog.TryGet(equippedWeapon.ItemId, out ItemDefinition weaponItem) ||
                weaponItem.Kind != ItemKind.Weapon || !weaponItem.IsCompatibleWithClass(run.ClassId))
            {
                throw new ArgumentException("The run's equipped item must be a weapon compatible with its class.", nameof(run));
            }

            if (!weaponCatalog.TryGet(equippedWeapon.ItemId, out WeaponProfile equippedProfile) ||
                !string.Equals(equippedProfile.ItemId, equippedWeapon.ItemId, StringComparison.Ordinal) ||
                !string.Equals(equippedProfile.ClassId, run.ClassId, StringComparison.Ordinal) ||
                !string.Equals(equippedProfile.ClassId, run.Inventory.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The authoritative weapon profile must match the equipped item and run class.", nameof(weaponCatalog));
            }

            // Build the complete runtime mapping before a caller can mutate PlayerMotor2D.
            _ = WeaponBasicAttackProfile.Create(equippedProfile, equippedWeapon.Rarity, equippedWeapon.UpgradeLevel);
            return equippedProfile;
        }

        public MeleeHitWindow CurrentMeleeHitWindow => _meleeAttackSnapshot.HitWindow;

        public bool IsDodgeReady => !IsDead && !IsStunned && !_dodgeActive && !IsCharging && _dodgeCooldownRemaining <= 0f;

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

        public void Initialize(
            ActorId actorId,
            IPlayerInputSource inputSource,
            LayerMask groundLayers,
            CombatStats combatStats = null,
            LayerMask enemyLayers = default,
            string raceId = ExpeditionRunConfiguration.HumanRaceId,
            ArmorResolution armorResolution = null)
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

            if (!ExpeditionRunConfiguration.IsKnownRaceId(raceId))
            {
                throw new ArgumentException("PlayerMotor2D requires one of the four known race identifiers.", nameof(raceId));
            }

            CombatStats resolvedCombatStats = combatStats ?? new CombatStats(maximumHealth: 100, maximumEnergy: 50, defense: 0);
            ValidateArmorResolution(armorResolution, resolvedCombatStats);

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
            _raceId = raceId;
            _inputSource = inputSource;
            _groundLayers = groundLayers;
            _damageState = new ActorDamageState(
                resolvedCombatStats,
                DirectHitProtectionTicks,
                armorResolution?.HealthAfterMaximumClamp);
            _skillUseState = new SkillUseState(
                _damageState.Stats.MaximumEnergy,
                armorResolution?.EnergyAfterMaximumClamp ?? _damageState.Stats.MaximumEnergy,
                default(PactModifierTotals),
                raceId);
            _equippedArmorResolution = armorResolution;
            _armorMovementSpeedMultiplier = armorResolution == null
                ? 1f
                : (100f + armorResolution.MovementSpeedPercentModifier) / 100f;
            _armorEnergyRegenerationPerSecondBonus = armorResolution?.EnergyRegenerationPerSecondBonus ?? 0;
            _harmfulStatusDurationReductionPercent = armorResolution?.HarmfulStatusDurationReductionPercent ?? 0;
            _nextArmorEnergyRecoveryTick = _tick + 60;
            _healthPotionUseState = new HealthPotionUseState(initialCharges: 0);
            _energyTonicUseState = new EnergyTonicUseState();
            _itemCollector = GetComponent<ItemCollector2D>();
            CreateTorchLight();
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

        private static void ValidateArmorResolution(ArmorResolution armorResolution, CombatStats combatStats)
        {
            if (armorResolution == null) return;
            if (combatStats.MaximumHealth != armorResolution.MaximumHealth ||
                combatStats.MaximumEnergy != armorResolution.MaximumEnergy ||
                combatStats.Defense < armorResolution.Defense)
            {
                throw new ArgumentException("Combat stats must include the resolved equipped armor values.", nameof(combatStats));
            }
            if (armorResolution.HealthAfterMaximumClamp < 0 ||
                armorResolution.HealthAfterMaximumClamp > combatStats.MaximumHealth ||
                armorResolution.EnergyAfterMaximumClamp < 0 ||
                armorResolution.EnergyAfterMaximumClamp > combatStats.MaximumEnergy)
            {
                throw new ArgumentException("Resolved current resources must be within their equipped maximums.", nameof(armorResolution));
            }
            if (armorResolution.MovementSpeedPercentModifier <= -100)
                throw new ArgumentOutOfRangeException(nameof(armorResolution), "Armor cannot reduce movement speed to zero or below.");
            if (armorResolution.EnergyRegenerationPerSecondBonus < 0 ||
                armorResolution.HarmfulStatusDurationReductionPercent < 0 ||
                armorResolution.HarmfulStatusDurationReductionPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(armorResolution), "Armor runtime modifiers are outside their supported range.");
            }
        }

        /// <summary>Binds the App service that atomically persists Fortune charges and the quick stack.</summary>
        public void ConfigureFortuneBrewRuntime(IPlayerFortuneBrewRuntime runtime)
        {
            if (!_initialized)
                throw new InvalidOperationException("PlayerMotor2D must be initialized before configuring Fortune Brew use.");
            if (_fortuneBrewCommitTask != null)
                throw new InvalidOperationException("Fortune Brew runtime cannot change while a durable use is pending.");
            _fortuneBrewRuntime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        /// <summary>
        /// Binds Bellator's selected skills to this actor. Skill effects are read from the authoritative Skills.json catalog.
        /// </summary>
        public void ConfigureBellator(IEnumerable<string> selectedSkillIds)
        {
            TextAsset catalogAsset = Resources.Load<TextAsset>("Luckbound/Data/Skills");
            if (catalogAsset == null)
            {
                throw new InvalidOperationException("The authoritative Skills.json catalog is missing from Resources.");
            }

            ConfigureBellator(SkillCatalogJsonLoader.Load(catalogAsset.text), selectedSkillIds);
        }

        /// <summary>Test and composition overload for a catalog already loaded from Skills.json.</summary>
        public void ConfigureBellator(SkillCatalog catalog, IEnumerable<string> selectedSkillIds)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("PlayerMotor2D must be initialized before configuring Bellator combat.");
            }

            if (_venatorCombatState != null || _magusCombatState != null)
            {
                throw new InvalidOperationException("A player cannot use Bellator, Venator, and Magus combat in the same expedition.");
            }

            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            var loadout = new BellatorSkillLoadout(catalog, selectedSkillIds);
            if (_bellatorSkillLoadout != null &&
                (!string.Equals(_bellatorSkillLoadout.Skill1.Id, loadout.Skill1.Id, StringComparison.Ordinal) ||
                 !string.Equals(_bellatorSkillLoadout.Skill2.Id, loadout.Skill2.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("A Bellator loadout cannot change during an expedition.");
            }

            ConfigureClass("class.bellator");

            if (_bellatorCombatState == null)
            {
                _bellatorSkillLoadout = loadout;
                _bellatorCombatState = new BellatorCombatState(_actorId, loadout, _skillUseState);
                ApplyWeaponBasicAttackProfile();
                _nextBellatorEnergyRecoveryTick = _tick + 60;
            }

            _lastBellatorCombatResult = default;
            _meleeAttackState.Interrupt();
            _meleeAttackSnapshot = default;
            IsAttacking = false;
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

            if (_bellatorCombatState != null || _magusCombatState != null)
            {
                throw new InvalidOperationException("A player cannot use Bellator, Venator, and Magus combat in the same expedition.");
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

            ConfigureClass("class.venator");

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
                ApplyWeaponBasicAttackProfile();
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

        /// <summary>
        /// Binds the Magus combat adapter to this initialized actor and one live room.
        /// Skill values always come from the authoritative Skills.json catalog.
        /// </summary>
        public void ConfigureMagus(
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

            ConfigureMagus(
                SkillCatalogJsonLoader.Load(catalogAsset.text),
                selectedSkillIds,
                projectilePool,
                projectileScope,
                roomLifetimeScope,
                enemyLayers);
        }

        /// <summary>Test and composition overload for a catalog already loaded from Skills.json.</summary>
        public void ConfigureMagus(
            SkillCatalog catalog,
            IEnumerable<string> selectedSkillIds,
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope,
            RoomLifetimeScope roomLifetimeScope,
            LayerMask enemyLayers = default)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("PlayerMotor2D must be initialized before configuring Magus combat.");
            }

            if (_bellatorCombatState != null || _venatorCombatState != null)
            {
                throw new InvalidOperationException("A player cannot use Bellator, Venator, and Magus combat in the same expedition.");
            }

            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (projectilePool == null) throw new ArgumentNullException(nameof(projectilePool));
            if (projectileScope == null) throw new ArgumentNullException(nameof(projectileScope));
            if (roomLifetimeScope == null) throw new ArgumentNullException(nameof(roomLifetimeScope));
            if (projectileScope.IsDisposed || projectileScope.Pool != projectilePool)
            {
                throw new ArgumentException("Magus projectiles require a live room scope attached to the supplied pool.", nameof(projectileScope));
            }

            LayerMask targetLayers = enemyLayers.value != 0 ? enemyLayers : _meleeTargetLayers;
            if (targetLayers.value == 0)
            {
                throw new ArgumentException("Magus effects require at least one enemy layer.", nameof(enemyLayers));
            }

            var loadout = new MagusSkillLoadout(catalog, selectedSkillIds);
            if (_magusSkillLoadout != null &&
                (!string.Equals(_magusSkillLoadout.Skill1.Id, loadout.Skill1.Id, StringComparison.Ordinal) ||
                 !string.Equals(_magusSkillLoadout.Skill2.Id, loadout.Skill2.Id, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("A Magus loadout cannot change during an expedition.");
            }

            if (_magusRoomBinding != null && !_magusRoomBinding.IsDisposed)
            {
                throw new InvalidOperationException("The previous Magus room must be disposed before binding another room.");
            }

            ConfigureClass("class.magus");

            MagusCombatState combatState = _magusCombatState ?? new MagusCombatState(_actorId, loadout, _skillUseState);
            MagusCombatAdapter2D adapter = null;
            MagusRoomBinding binding = null;
            try
            {
                adapter = new MagusCombatAdapter2D(
                    _actorId,
                    combatState,
                    projectilePool,
                    projectileScope,
                    roomLifetimeScope,
                    targetLayers);
                adapter.BasicAttackRequested += HandleMagusBasicAttackRequested;
                binding = new MagusRoomBinding(this, adapter, projectileScope);
                roomLifetimeScope.OwnResource(binding);
            }
            catch
            {
                if (binding != null)
                {
                    binding.Dispose();
                }
                else
                {
                    if (adapter != null)
                    {
                        adapter.BasicAttackRequested -= HandleMagusBasicAttackRequested;
                        adapter.Dispose();
                    }

                    projectileScope.Dispose();
                }

                throw;
            }

            if (_magusCombatState == null)
            {
                _magusSkillLoadout = loadout;
                _magusCombatState = combatState;
                ApplyWeaponBasicAttackProfile();
                _nextMagusEnergyRecoveryTick = _tick + 60;
            }

            _magusRoomBinding = binding;
            _magusTargetLayers = targetLayers;
            _magusAimContactFilter = new ContactFilter2D();
            _magusAimContactFilter.SetLayerMask(targetLayers);
            _magusAimContactFilter.useTriggers = false;
            _magusBasicAttackSnapshot = default;
            _lastMagusCombatResult = default;
            _meleeAttackState.Interrupt();
            _meleeAttackSnapshot = default;
            IsAttacking = false;
        }

        public bool IsVenatorSkillReady(string skillId)
        {
            return IsVenator && _venatorSkillLoadout.Contains(skillId) &&
                _skillUseState.IsReady(skillId, SimulationTick);
        }

        public bool IsMagusSkillReady(string skillId)
        {
            return IsMagus && _magusSkillLoadout.Contains(skillId) &&
                _skillUseState.IsReady(skillId, SimulationTick);
        }

        public bool IsBellatorSkillReady(string skillId)
        {
            return IsBellator && _bellatorSkillLoadout.Contains(skillId) &&
                _skillUseState.IsReady(skillId, SimulationTick);
        }

        private void ConfigureClass(string classId)
        {
            if (_configuredClassId != null)
            {
                if (!string.Equals(_configuredClassId, classId, StringComparison.Ordinal))
                    throw new InvalidOperationException("A player combat class cannot change during an expedition.");
                return;
            }

            if (_weaponBasicAttackProfile != null &&
                !string.Equals(_weaponBasicAttackProfile.ClassId, classId, StringComparison.Ordinal))
            {
                throw new ArgumentException("The equipped weapon is incompatible with the configured player class.", nameof(classId));
            }

            _configuredClassId = classId;
        }

        private void ApplyWeaponBasicAttackProfile()
        {
            if (_weaponBasicAttackProfile == null || _configuredClassId == null) return;

            if (string.Equals(_configuredClassId, "class.bellator", StringComparison.Ordinal))
            {
                _meleeAttackState.ConfigureProfile(_weaponBasicAttackProfile.CreateMeleeAttackProfile());
            }
            else if (string.Equals(_configuredClassId, "class.venator", StringComparison.Ordinal))
            {
                _venatorCombatState?.ConfigureBasicAttackProfile(_weaponBasicAttackProfile.CreateVenatorBasicAttackProfile());
            }
            else if (string.Equals(_configuredClassId, "class.magus", StringComparison.Ordinal))
            {
                _magusCombatState?.ConfigureBasicAttackProfile(_weaponBasicAttackProfile.CreateMagusBasicAttackProfile());
            }
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

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            DamageCalculationRequest guardedRequest = IsBellatorGuardActiveAt(impactTick)
                ? ApplyBellatorGuardReduction(request)
                : request;

            DirectImpactOutcome outcome = _damageState.ReceiveDirectImpact(
                hitId,
                guardedRequest,
                impactTick,
                additionalInvulnerability: _dodgeInvulnerabilityActive);
            if (outcome.Status != DirectImpactStatus.Applied)
            {
                return outcome;
            }

            bool interruptedConsumablePreparation = _healthPotionUseState.IsPreparing ||
                _energyTonicUseState.IsPreparing;
            _healthPotionUseState.CancelPreparation();
            _energyTonicUseState.CancelPreparation();
            if (interruptedConsumablePreparation)
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Cancelled);
            if (_skillUseState.IsPreparing)
            {
                _bellatorCombatState?.Interrupt();
                _venatorCombatState?.Interrupt();
                _magusCombatState?.Interrupt();
            }

            _body.linearVelocity = knockbackVelocity;
            int resolvedStunDurationTicks = ApplyHarmfulStatusDurationReduction(stunDurationTicks);
            bool protectedFromStun = _antidoteProtection.HasValue &&
                _antidoteProtection.Value.ProtectsAt(HarmfulStatusKind.Stun, impactTick);
            if (resolvedStunDurationTicks > 0 && !outcome.IsDead && !protectedFromStun)
            {
                _stunTicksRemaining = Math.Max(_stunTicksRemaining, resolvedStunDurationTicks);
                InterruptActions();
            }

            if (outcome.IsDead)
            {
                _stunTicksRemaining = 0;
                InterruptActions();
            }

            return outcome;
        }

        private PlayerConsumableUseOutcome UpdateConsumableUse(
            long tick,
            bool requested,
            bool interrupted,
            bool stunnedAtStart)
        {
            if (_itemCollector == null) _itemCollector = GetComponent<ItemCollector2D>();
            ExpeditionInventory inventory = _itemCollector == null ? null : _itemCollector.Inventory;
            InventoryItemStack quick = inventory == null ? null : inventory.QuickConsumable;
            string itemId = quick == null ? string.Empty : quick.ItemId;
            bool hasQuick = quick != null && quick.Quantity > 0 && quick.InstanceId.IsValid &&
                !string.IsNullOrWhiteSpace(quick.ItemId);
            if (!hasQuick) itemId = string.Empty;

            if (_fortuneBrewCommitTask != null)
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
                return LastConsumableUseOutcome;
            }

            if (_fortuneBrewPendingTransactionId.IsValid)
            {
                if (requested && !interrupted && hasQuick && itemId == FortuneBrewItemId &&
                    quick.InstanceId == _fortuneBrewPendingItemInstanceId)
                {
                    LastConsumableUseOutcome = BeginFortuneBrewUse(quick);
                }
                else
                {
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
                }

                return LastConsumableUseOutcome;
            }

            if (_fortuneBrewCompletionReportedThisTick && !requested)
                return LastConsumableUseOutcome;

            bool healthSelected = hasQuick && string.Equals(itemId, HealthPotionItemId, StringComparison.Ordinal);
            bool energySelected = hasQuick && string.Equals(itemId, EnergyTonicItemId, StringComparison.Ordinal);

            bool wasPreparingHealth = _healthPotionUseState.IsPreparing;
            bool healthStackMatchesPreparation = wasPreparingHealth && healthSelected &&
                quick.InstanceId == _healthPreparationInstanceId;
            bool interruptHealth = interrupted || (wasPreparingHealth && !healthStackMatchesPreparation);
            _healthPotionUseState.SynchronizeCharges(healthSelected ? quick.Quantity : 0);
            HealthPotionUseOutcome healthOutcome = _healthPotionUseState.Update(
                tick,
                requested: requested && healthSelected,
                currentHealth: CurrentHealth,
                maximumHealth: CombatStats.MaximumHealth,
                interrupted: interruptHealth);
            if (!wasPreparingHealth && _healthPotionUseState.IsPreparing)
                _healthPreparationInstanceId = quick.InstanceId;

            if (healthOutcome.Status == HealthPotionUseStatus.Healed)
            {
                ItemInstanceId preparedInstanceId = _healthPreparationInstanceId;
                _healthPreparationInstanceId = default;
                if (!TryConsumeQuickConsumable(preparedInstanceId, HealthPotionItemId))
                {
                    SynchronizeConsumableCharges();
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedInventoryChanged, HealthPotionItemId);
                    return LastConsumableUseOutcome;
                }

                int restored = _damageState.RestoreHealth(healthOutcome.HealthRestored);
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Consumed, HealthPotionItemId, healthRestored: restored);
                return LastConsumableUseOutcome;
            }

            if (healthOutcome.Status != HealthPotionUseStatus.Preparing)
                _healthPreparationInstanceId = default;

            bool wasPreparingEnergy = _energyTonicUseState.IsPreparing;
            bool energyStackMatchesPreparation = wasPreparingEnergy && energySelected &&
                quick.InstanceId == _energyPreparationInstanceId;
            bool interruptEnergy = interrupted || (wasPreparingEnergy && !energyStackMatchesPreparation);
            _energyTonicUseState.SynchronizeCharges(energySelected ? quick.Quantity : 0);
            EnergyTonicUseOutcome energyOutcome = _energyTonicUseState.Update(
                tick,
                requested: requested && energySelected,
                currentEnergy: CurrentEnergy,
                maximumEnergy: MaximumEnergy,
                interrupted: interruptEnergy);
            if (!wasPreparingEnergy && _energyTonicUseState.IsPreparing)
                _energyPreparationInstanceId = quick.InstanceId;

            if (energyOutcome.Status == EnergyTonicUseStatus.Energized)
            {
                ItemInstanceId preparedInstanceId = _energyPreparationInstanceId;
                _energyPreparationInstanceId = default;
                if (!TryConsumeQuickConsumable(preparedInstanceId, EnergyTonicItemId))
                {
                    SynchronizeConsumableCharges();
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedInventoryChanged, EnergyTonicItemId);
                    return LastConsumableUseOutcome;
                }

                int restored = Math.Min(energyOutcome.EnergyRestored, MaximumEnergy - CurrentEnergy);
                _skillUseState.SetCurrentEnergy(CurrentEnergy + restored);
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Consumed, EnergyTonicItemId, energyRestored: restored);
                return LastConsumableUseOutcome;
            }

            if (energyOutcome.Status != EnergyTonicUseStatus.Preparing)
                _energyPreparationInstanceId = default;

            if (healthOutcome.Status == HealthPotionUseStatus.Preparing ||
                energyOutcome.Status == EnergyTonicUseStatus.Preparing)
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Preparing, itemId);
                return LastConsumableUseOutcome;
            }

            if (requested && interrupted && !stunnedAtStart)
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Cancelled, itemId);
                return LastConsumableUseOutcome;
            }

            bool antidoteRequested = requested && hasQuick &&
                string.Equals(itemId, AntidoteItemId, StringComparison.Ordinal);
            if (!antidoteRequested &&
                (healthOutcome.Status == HealthPotionUseStatus.Cancelled ||
                 energyOutcome.Status == EnergyTonicUseStatus.Cancelled))
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Cancelled, itemId);
                return LastConsumableUseOutcome;
            }

            if (requested)
            {
                if (!hasQuick)
                {
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedWithoutQuickConsumable);
                    return LastConsumableUseOutcome;
                }

                if (stunnedAtStart && !string.Equals(itemId, AntidoteItemId, StringComparison.Ordinal))
                {
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedWhileStunned, itemId);
                    return LastConsumableUseOutcome;
                }

                if (string.Equals(itemId, HealthPotionItemId, StringComparison.Ordinal) &&
                    healthOutcome.Status == HealthPotionUseStatus.RejectedAtFullHealth)
                {
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedAtFullResource, itemId);
                    return LastConsumableUseOutcome;
                }

                if (string.Equals(itemId, EnergyTonicItemId, StringComparison.Ordinal) &&
                    energyOutcome.Status == EnergyTonicUseStatus.RejectedAtFullEnergy)
                {
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedAtFullResource, itemId);
                    return LastConsumableUseOutcome;
                }

                if (healthSelected && healthOutcome.Status == HealthPotionUseStatus.RejectedWithoutCharges ||
                    energySelected && energyOutcome.Status == EnergyTonicUseStatus.RejectedWithoutCharges)
                {
                    LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                        PlayerConsumableUseStatus.RejectedWithoutQuickConsumable, itemId);
                    return LastConsumableUseOutcome;
                }

                LastConsumableUseOutcome = UseImmediateConsumable(tick, quick, itemId);
                return LastConsumableUseOutcome;
            }

            LastConsumableUseOutcome = new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Idle, itemId);
            return LastConsumableUseOutcome;
        }

        private PlayerConsumableUseOutcome UseImmediateConsumable(long tick, InventoryItemStack quick, string itemId)
        {
            if (string.Equals(itemId, FortuneBrewItemId, StringComparison.Ordinal))
            {
                return BeginFortuneBrewUse(quick);
            }

            BombEffectResolution bombResolution = null;
            Dictionary<ActorId, ProjectileImpactTarget2D> bombTargets = null;
            AntidoteEffectResolution? antidoteResolution = null;
            TorchLightActivation? torchActivation = null;
            if (string.Equals(itemId, BombItemId, StringComparison.Ordinal))
            {
                if (_meleeTargetLayers.value == 0)
                    return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.RejectedUnsupportedSubsystem, itemId);
                BuildBombResolution(out bombResolution, out bombTargets);
            }
            else if (string.Equals(itemId, AntidoteItemId, StringComparison.Ordinal))
            {
                antidoteResolution = AntidoteEffect.Apply(tick);
            }
            else if (string.Equals(itemId, TorchItemId, StringComparison.Ordinal) && _torchLight == null)
            {
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.RejectedUnsupportedSubsystem, itemId);
            }
            else if (string.Equals(itemId, TorchItemId, StringComparison.Ordinal))
            {
                torchActivation = TorchEffect.Activate(tick);
            }
            else if (!string.Equals(itemId, AntidoteItemId, StringComparison.Ordinal) &&
                !string.Equals(itemId, TorchItemId, StringComparison.Ordinal))
            {
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.RejectedUnsupportedSubsystem, itemId);
            }

            if (!TryConsumeQuickConsumable(quick.InstanceId, itemId))
            {
                SynchronizeConsumableCharges();
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.RejectedInventoryChanged, itemId);
            }

            if (string.Equals(itemId, AntidoteItemId, StringComparison.Ordinal))
            {
                AntidoteEffectResolution effect = antidoteResolution.Value;
                HarmfulStatusKind activeStatuses = _stunTicksRemaining > 0
                    ? HarmfulStatusKind.Stun
                    : HarmfulStatusKind.None;
                HarmfulStatusKind remainingStatuses = effect.Clear(activeStatuses);
                if ((remainingStatuses & HarmfulStatusKind.Stun) == 0) _stunTicksRemaining = 0;
                _antidoteProtection = effect;
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Consumed, itemId);
            }

            if (string.Equals(itemId, BombItemId, StringComparison.Ordinal))
            {
                foreach (ActorId targetId in bombResolution.TargetActorIds)
                {
                    ProjectileImpactTarget2D target = bombTargets[targetId];
                    target.QueueImpact(new ProjectileImpact(
                        EntityId.New(),
                        HitId.New(),
                        _actorId,
                        targetId,
                        BombEffect.Damage,
                        transform.position.x));
                }

                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Consumed, itemId);
            }

            _torchActivation = torchActivation.Value;
            UpdateTorchLight(tick);
            return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Consumed, itemId);
        }

        private bool TryConsumeQuickConsumable(ItemInstanceId expectedInstanceId, string expectedItemId)
        {
            ExpeditionInventory inventory = _itemCollector == null ? null : _itemCollector.Inventory;
            if (inventory == null) return false;
            InventoryOperationOutcome outcome = inventory.TryConsumeQuickConsumable(
                expectedInstanceId, expectedItemId, TransactionId.New());
            return outcome.Succeeded;
        }

        private PlayerConsumableUseOutcome BeginFortuneBrewUse(InventoryItemStack quick)
        {
            if (_fortuneBrewRuntime == null)
            {
                return new PlayerConsumableUseOutcome(
                    _fortuneBrewPendingTransactionId.IsValid
                        ? PlayerConsumableUseStatus.Pending
                        : PlayerConsumableUseStatus.RejectedUnsupportedSubsystem,
                    FortuneBrewItemId);
            }
            if (quick == null || quick.ItemId != FortuneBrewItemId || quick.Quantity <= 0 || !quick.InstanceId.IsValid)
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.RejectedInventoryChanged, FortuneBrewItemId);

            bool retryingPendingMutation = _fortuneBrewPendingTransactionId.IsValid;
            if (retryingPendingMutation && quick.InstanceId != _fortuneBrewPendingItemInstanceId)
            {
                return new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
            }

            TransactionId transactionId = retryingPendingMutation
                ? _fortuneBrewPendingTransactionId
                : TransactionId.New();
            _fortuneBrewPendingItemInstanceId = quick.InstanceId;
            _fortuneBrewPendingTransactionId = transactionId;

            Task<PlayerFortuneBrewCommitOutcome> commitTask;
            try
            {
                commitTask = _fortuneBrewRuntime.ConsumeFortuneBrewAsync(quick.InstanceId, transactionId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
            }

            if (commitTask == null)
                return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Pending, FortuneBrewItemId);

            _fortuneBrewCommitTask = commitTask;
            return new PlayerConsumableUseOutcome(PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
        }

        private void PollFortuneBrewCommit()
        {
            _fortuneBrewCompletionReportedThisTick = false;
            Task<PlayerFortuneBrewCommitOutcome> completedTask = _fortuneBrewCommitTask;
            if (completedTask == null || !completedTask.IsCompleted) return;

            ItemInstanceId itemInstanceId = _fortuneBrewPendingItemInstanceId;
            TransactionId transactionId = _fortuneBrewPendingTransactionId;
            _fortuneBrewCommitTask = null;
            _fortuneBrewCompletionReportedThisTick = true;

            if (completedTask.IsCanceled || completedTask.IsFaulted)
            {
                if (completedTask.Exception != null) Debug.LogException(completedTask.Exception.Flatten());
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
                return;
            }

            PlayerFortuneBrewCommitOutcome outcome = completedTask.Result;
            if (outcome == null)
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
                return;
            }

            if (outcome.Pending)
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.Pending, FortuneBrewItemId);
                return;
            }

            ClearPendingFortuneBrewIdentity();

            if (outcome.Persisted)
                _itemCollector?.PublishCommittedInventory(outcome.CommittedInventory);

            if (outcome.Status == Luckbound.Core.Fortune.FortuneOperationStatus.RejectedAtCapacity)
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.RejectedAtFullResource, FortuneBrewItemId);
                return;
            }

            if (!outcome.Succeeded || _itemCollector == null ||
                !outcome.CommittedInventory.HasSuccessfulQuickConsumableConsumption(
                    itemInstanceId, FortuneBrewItemId, transactionId))
            {
                LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                    PlayerConsumableUseStatus.RejectedDurableMutation, FortuneBrewItemId);
                return;
            }

            LastConsumableUseOutcome = new PlayerConsumableUseOutcome(
                PlayerConsumableUseStatus.Consumed, FortuneBrewItemId);
        }

        private void ClearPendingFortuneBrewIdentity()
        {
            _fortuneBrewPendingItemInstanceId = default;
            _fortuneBrewPendingTransactionId = default;
        }

        /// <summary>Refreshes the HUD charges after binding the run inventory, without consuming any item.</summary>
        public void RefreshConsumableChargesFromInventory()
        {
            if (!_initialized) throw new InvalidOperationException("Initialize the player before binding consumable charges.");
            SynchronizeConsumableCharges();
        }

        private void SynchronizeConsumableCharges()
        {
            if (_itemCollector == null) _itemCollector = GetComponent<ItemCollector2D>();
            InventoryItemStack quick = _itemCollector == null || _itemCollector.Inventory == null
                ? null
                : _itemCollector.Inventory.QuickConsumable;
            _healthPotionUseState.SynchronizeCharges(
                quick != null && quick.ItemId == HealthPotionItemId ? quick.Quantity : 0);
            _energyTonicUseState.SynchronizeCharges(
                quick != null && quick.ItemId == EnergyTonicItemId ? quick.Quantity : 0);
        }

        private void BuildBombResolution(
            out BombEffectResolution resolution,
            out Dictionary<ActorId, ProjectileImpactTarget2D> targetMap)
        {
            Vector2 center = transform.position;
            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                center, (float)BombEffect.Radius, _meleeTargetLayers);
            var candidates = new List<BombTarget>(overlaps.Length);
            targetMap = new Dictionary<ActorId, ProjectileImpactTarget2D>();
            foreach (Collider2D overlap in overlaps)
            {
                if (overlap == null) continue;
                ProjectileImpactTarget2D target = overlap.GetComponentInParent<ProjectileImpactTarget2D>();
                if (target == null || !target.IsInitialized || targetMap.ContainsKey(target.ActorId)) continue;
                Vector3 position = target.transform.position;
                if (float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                    float.IsNaN(position.y) || float.IsInfinity(position.y)) continue;
                targetMap.Add(target.ActorId, target);
                candidates.Add(new BombTarget(target.ActorId, (decimal)position.x, (decimal)position.y));
            }

            resolution = BombEffect.ResolveTargets((decimal)center.x, (decimal)center.y, candidates);
        }

        private void CreateTorchLight()
        {
            var lightObject = new GameObject("Luckbound Player Torch Light");
            lightObject.transform.SetParent(transform, worldPositionStays: false);
            _torchLight = lightObject.AddComponent<Light2D>();
            _torchLight.lightType = Light2D.LightType.Point;
            _torchLight.pointLightInnerRadius = 0.5f;
            _torchLight.pointLightOuterRadius = 3f;
            _torchLight.intensity = 0.8f;
            _torchLight.color = new Color(1f, 0.76f, 0.45f, 1f);
            _torchLight.enabled = false;
        }

        private void UpdateTorchLight(long tick)
        {
            if (_torchLight == null) return;
            bool active = _torchActivation.HasValue && _torchActivation.Value.IsActiveAt(tick);
            _torchLight.enabled = active;
            if (_torchActivation.HasValue && tick >= _torchActivation.Value.EndsAtTickExclusive)
                _torchActivation = null;
        }

        private static bool IsConsumableActionActive(PlayerConsumableUseOutcome outcome)
        {
            return outcome.Status == PlayerConsumableUseStatus.Preparing ||
                outcome.Status == PlayerConsumableUseStatus.Pending || outcome.WasConsumed;
        }

        private int ApplyHarmfulStatusDurationReduction(int durationTicks)
        {
            if (durationTicks <= 0 || _harmfulStatusDurationReductionPercent <= 0) return durationTicks;
            decimal remainingPercent = 100m - _harmfulStatusDurationReductionPercent;
            decimal remainingTicks = durationTicks * remainingPercent / 100m;
            if (remainingTicks <= 0m) return 0;
            return Math.Max(1, decimal.ToInt32(decimal.Round(remainingTicks, 0, MidpointRounding.AwayFromZero)));
        }

        private bool IsBellatorGuardActiveAt(long tick)
        {
            return _bellatorGuardSkill != null &&
                tick >= _bellatorGuardStartsAtTick &&
                tick < _bellatorGuardExpiresAtTick;
        }

        private DamageCalculationRequest ApplyBellatorGuardReduction(DamageCalculationRequest request)
        {
            decimal guardReduction = _bellatorGuardSkill.DamageReductionPercent;
            decimal combinedReduction = request.IncomingDamageReductionPercent >= 1m - guardReduction
                ? 1m
                : request.IncomingDamageReductionPercent + guardReduction;
            return new DamageCalculationRequest(
                request.BaseDamage,
                request.RarityMultiplier,
                request.UpgradeMultiplier,
                request.OffensiveBonusPercent,
                request.DefensePenetration,
                combinedReduction,
                request.TargetStats,
                request.TargetIsInvulnerable,
                request.TargetDefenseBonus);
        }

        private void OnDestroy()
        {
            if (_venatorRoomBinding != null)
            {
                _venatorRoomBinding.Dispose();
                _venatorRoomBinding = null;
            }

            if (_magusRoomBinding != null)
            {
                _magusRoomBinding.Dispose();
                _magusRoomBinding = null;
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

            _chargeCollisionActiveThisPhysicsStep = false;
            long tick = _tick++;
            PlayerCommand command = _inputSource.Consume(tick);
            if (command.ActorId != _actorId)
            {
                throw new InvalidOperationException("The input command belongs to a different actor.");
            }

            PollFortuneBrewCommit();

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
                UpdateConsumableUse(tick, requested: false, interrupted: true, stunnedAtStart: true);
                UpdateTorchLight(tick);
                InterruptActions();
                TickBellatorCommand(command, canAct: false);
                TickVenatorCommand(command, canAct: false);
                TickMagusCommand(command, canAct: false);
                ApplyRestrictedMotion(deltaTime);
                return;
            }

            bool stunnedAtStart = IsStunned;
            bool dodgeStartedThisTick = !stunnedAtStart &&
                command.WasPressed(PlayerAction.Dodge) && IsDodgeReady;
            PlayerConsumableUseOutcome consumableOutcome = UpdateConsumableUse(
                tick,
                command.WasPressed(PlayerAction.Consumable),
                interrupted: dodgeStartedThisTick || stunnedAtStart,
                stunnedAtStart: stunnedAtStart);
            UpdateTorchLight(tick);

            if (stunnedAtStart)
            {
                if (consumableOutcome.WasConsumed && consumableOutcome.ItemId == AntidoteItemId)
                {
                    InterruptActions();
                    ApplyRestrictedMotion(deltaTime);
                    return;
                }

                _stunTicksRemaining--;
                InterruptActions();
                TickBellatorCommand(command, canAct: false);
                TickVenatorCommand(command, canAct: false);
                TickMagusCommand(command, canAct: false);
                ApplyRestrictedMotion(deltaTime);
                return;
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
                bool higherPriorityActionActive = IsConsumableActionActive(consumableOutcome);
                TickVenatorCommand(command, canAct: !_dodgeActive && !higherPriorityActionActive);
                attackMovementMultiplier = IsAttacking ? (float)_venatorBasicAttackSnapshot.MovementSpeedMultiplier : 1f;
                _meleeAttackSnapshot = default;
            }
            else if (IsMagus)
            {
                bool higherPriorityActionActive = IsConsumableActionActive(consumableOutcome);
                TickMagusCommand(command, canAct: !_dodgeActive && !higherPriorityActionActive);
                attackMovementMultiplier = IsAttacking ? (float)_magusBasicAttackSnapshot.MovementSpeedMultiplier : 1f;
                _meleeAttackSnapshot = default;
            }
            else if (IsBellator)
            {
                bool higherPriorityActionActive = IsConsumableActionActive(consumableOutcome);
                TickBellatorCommand(command, canAct: !_dodgeActive && !higherPriorityActionActive);
                bool skillActionOwnsTick = _lastBellatorCombatResult.SkillActionOwnsTick || IsCharging;
                if (skillActionOwnsTick)
                {
                    _meleeAttackState.Interrupt();
                    _meleeAttackSnapshot = default;
                    IsAttacking = false;
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
                }

                attackMovementMultiplier = IsAttacking ? (float)_meleeAttackSnapshot.MovementSpeedMultiplier : 1f;
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

            float targetSpeed = input * EffectiveMaximumHorizontalSpeed * attackMovementMultiplier;
            float acceleration = Mathf.Abs(input) > MovementDeadZone ? GroundAcceleration : GroundBraking;
            if (!IsGrounded)
            {
                acceleration *= AirControl;
            }

            Vector2 velocity = _body.linearVelocity;
            bool chargeOwnsMovement = !_dodgeActive && ApplyBellatorChargeMovement(ref velocity, deltaTime);
            if (_dodgeActive)
            {
                IsDodging = true;
                _dodgeInvulnerabilityActive = _dodgeElapsed + (deltaTime * 0.5f) < DodgeInvulnerabilityDuration;
                velocity.x = _dodgeDirection * DodgeSpeed * _armorMovementSpeedMultiplier;
                _jumpBufferRemaining = 0f;
                _dodgeElapsed += deltaTime;
                if (_dodgeElapsed >= DodgeDuration)
                {
                    _dodgeActive = false;
                    _dodgeSpeedRecoveryPending = true;
                }
            }
            else if (chargeOwnsMovement)
            {
                _jumpBufferRemaining = 0f;
                _coyoteTimeRemaining = 0f;
            }
            else
            {
                IsDodging = false;
                _dodgeInvulnerabilityActive = false;
                if (_dodgeSpeedRecoveryPending)
                {
                    velocity.x = _dodgeDirection * EffectiveMaximumHorizontalSpeed;
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

        private void OnCollisionEnter2D(Collision2D collision)
        {
            QueueBellatorChargeImpact(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            QueueBellatorChargeImpact(collision);
        }

        private void QueueBellatorChargeImpact(Collision2D collision)
        {
            if (!_chargeCollisionActiveThisPhysicsStep || _bellatorChargeSkill == null || collision == null) return;

            ProjectileImpactTarget2D target = collision.collider.GetComponentInParent<ProjectileImpactTarget2D>();
            if (target == null || !target.IsInitialized || target.ActorId == _actorId ||
                !_chargeHitTargets.Add(target.ActorId))
            {
                return;
            }

            float multiplier = (float)PlayableRaceRules.GetOutgoingKnockbackMultiplier(_raceId);
            Vector2 knockback = target.GetComponentInParent<CustodianBoss2D>() != null
                ? Vector2.zero
                : new Vector2(
                    _bellatorChargeDirection * (float)_bellatorChargeSkill.KnockbackHorizontalSpeed * multiplier,
                    (float)_bellatorChargeSkill.KnockbackVerticalSpeed * multiplier);
            var impact = new MeleeImpact(
                _lastBellatorChargeHitId,
                _actorId,
                target.ActorId,
                _bellatorChargeSkill.BaseDamage,
                transform.position.x,
                CurrentPhysicsTick(),
                knockback,
                knockback == Vector2.zero ? 0 : _bellatorChargeSkill.TargetAiControlSuspensionTicks);
            target.QueueMeleeImpact(impact);
        }

        private void StartBellatorCharge(SkillDefinition charge)
        {
            if (charge == null || charge.EffectKind != SkillEffectKind.Charge)
            {
                throw new ArgumentException("Bellator can only activate a catalog Charge definition.", nameof(charge));
            }

            _bellatorChargeSkill = charge;
            _bellatorChargeTicksRemaining = charge.EffectDurationTicks;
            _bellatorChargeDirection = Mathf.Abs(_facingDirection) > MovementDeadZone
                ? Mathf.Sign(_facingDirection)
                : 1f;
            _lastBellatorChargeHitId = HitId.New();
            _chargeHitTargets.Clear();
            BellatorChargeActivationCount++;
        }

        private bool ApplyBellatorChargeMovement(ref Vector2 velocity, float deltaTime)
        {
            if (_bellatorChargeSkill == null) return false;
            if (_bellatorChargeTicksRemaining <= 0)
            {
                _bellatorChargeSkill = null;
                return false;
            }

            _chargeCollisionActiveThisPhysicsStep = true;
            float direction = _bellatorChargeDirection;
            float speed = (float)_bellatorChargeSkill.ChargeSpeed;
            int hitCount = _collider.Cast(
                new Vector2(direction, 0f),
                _groundContactFilter,
                _chargeWallHits,
                speed * deltaTime);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit2D hit = _chargeWallHits[index];
                if (hit.collider == null || hit.normal.x * direction >= -0.4f) continue;

                velocity.x = 0f;
                _bellatorChargeTicksRemaining = 0;
                return true;
            }

            velocity.x = direction * speed;
            _bellatorChargeTicksRemaining--;
            return true;
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

            if (_magusCombatState != null)
            {
                _magusCombatState.Interrupt();
            }

            if (_bellatorCombatState != null)
            {
                _bellatorCombatState.Interrupt();
            }

            _bellatorChargeSkill = null;
            _bellatorChargeTicksRemaining = 0;
            _chargeCollisionActiveThisPhysicsStep = false;

            _venatorBasicAttackSnapshot = default;
            _magusBasicAttackSnapshot = default;
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
                _nextArmorEnergyRecoveryTick = command.Tick + 60;
            }
            else if (roomReady && !IsDead)
            {
                RecoverVenatorEnergy(command.Tick);
            }

            if (result.BasicAttack.WasStartedThisTick)
            {
                SpawnVenatorProjectile(
                    result.BasicAttack.Projectile ?? throw new InvalidOperationException("An accepted Venator attack requires its equipped weapon projectile."),
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

        private void TickBellatorCommand(PlayerCommand command, bool canAct)
        {
            if (_bellatorCombatState == null) return;

            BellatorCombatResult result = _bellatorCombatState.Tick(command, isAlive: canAct);
            _lastBellatorCombatResult = result;
            if (result.SkillOutcome.EnergySpent > 0)
            {
                _nextBellatorEnergyRecoveryTick = command.Tick + 60;
                _nextArmorEnergyRecoveryTick = command.Tick + 60;
            }
            else if (!IsDead)
            {
                RecoverBellatorEnergy(command.Tick);
            }

            SkillDefinition activatedSkill = result.ActivatedSkill;
            if (activatedSkill == null)
            {
                return;
            }

            BellatorSkillActivationCount++;
            switch (activatedSkill.EffectKind)
            {
                case SkillEffectKind.AreaStrike:
                    StartBellatorCircularCut(activatedSkill);
                    break;
                case SkillEffectKind.Guard:
                    StartBellatorGuard(activatedSkill, command.Tick);
                    break;
                case SkillEffectKind.Charge:
                    StartBellatorCharge(activatedSkill);
                    break;
                default:
                    throw new InvalidOperationException("Bellator activation returned an unsupported skill effect.");
            }
        }

        private void StartBellatorCircularCut(SkillDefinition circularCut)
        {
            if (circularCut == null || circularCut.EffectKind != SkillEffectKind.AreaStrike)
            {
                throw new ArgumentException("Bellator Circular Cut requires a catalog AreaStrike definition.", nameof(circularCut));
            }

            _lastBellatorCircularCutHitId = HitId.New();
            BellatorCircularCutActivationCount++;
            CollectBellatorAreaImpacts(circularCut, _lastBellatorCircularCutHitId);
        }

        private void StartBellatorGuard(SkillDefinition guard, long activationTick)
        {
            if (guard == null || guard.EffectKind != SkillEffectKind.Guard)
            {
                throw new ArgumentException("Bellator Guard requires a catalog Guard definition.", nameof(guard));
            }

            if (activationTick < 0 || activationTick > long.MaxValue - guard.EffectDurationTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(activationTick));
            }

            _bellatorGuardSkill = guard;
            _bellatorGuardStartsAtTick = activationTick;
            _bellatorGuardExpiresAtTick = activationTick + guard.EffectDurationTicks;
            BellatorGuardActivationCount++;
        }

        private void CollectBellatorAreaImpacts(SkillDefinition areaStrike, HitId hitId)
        {
            _bellatorAreaTargets.Clear();
            _bellatorAreaTargetIds.Clear();

            Vector2 center = transform.position;
            float radius = (float)areaStrike.AreaRadius;
            int overlapCount = Physics2D.OverlapCircle(center, radius, _meleeContactFilter, _bellatorAreaOverlaps);
            while (overlapCount >= _bellatorAreaOverlaps.Length)
            {
                Array.Resize(ref _bellatorAreaOverlaps, checked(_bellatorAreaOverlaps.Length * 2));
                overlapCount = Physics2D.OverlapCircle(center, radius, _meleeContactFilter, _bellatorAreaOverlaps);
            }

            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D overlap = _bellatorAreaOverlaps[index];
                if (overlap == null) continue;

                ProjectileImpactTarget2D target = overlap.GetComponentInParent<ProjectileImpactTarget2D>();
                if (target == null || !target.IsInitialized || target.ActorId == _actorId ||
                    !_bellatorAreaTargetIds.Add(target.ActorId))
                {
                    continue;
                }

                _bellatorAreaTargets.Add(target);
            }

            _bellatorAreaTargets.Sort(CompareBellatorTargetsByActorId);
            long queuedAtTick = CurrentPhysicsTick();
            for (int index = 0; index < _bellatorAreaTargets.Count; index++)
            {
                ProjectileImpactTarget2D target = _bellatorAreaTargets[index];
                if (target == null || !target.IsInitialized || target.ActorId == _actorId) continue;

                target.QueueMeleeImpact(new MeleeImpact(
                    hitId,
                    _actorId,
                    target.ActorId,
                    areaStrike.BaseDamage,
                    transform.position.x,
                    queuedAtTick));
                MeleeImpactsQueuedCount++;
            }
        }

        private static int CompareBellatorTargetsByActorId(ProjectileImpactTarget2D left, ProjectileImpactTarget2D right)
        {
            return left.ActorId.Value.CompareTo(right.ActorId.Value);
        }

        private void RecoverBellatorEnergy(long tick)
        {
            RecoverEnergy(tick, ref _nextBellatorEnergyRecoveryTick);
        }

        private void RecoverVenatorEnergy(long tick)
        {
            RecoverEnergy(tick, ref _nextVenatorEnergyRecoveryTick);
        }

        private void TickMagusCommand(PlayerCommand command, bool canAct)
        {
            if (_magusCombatState == null) return;

            bool roomReady = _magusRoomBinding != null && !_magusRoomBinding.IsDisposed;
            MagusCombatResult result;
            if (roomReady)
            {
                Vector2 projectileOrigin = MagusProjectileOrigin();
                Vector2 aimDirection = ResolveRequestedFireballAimDirection(
                    command,
                    projectileOrigin,
                    new Vector2(_facingDirection, 0f));
                result = _magusRoomBinding.Adapter.Tick(
                    command,
                    isAlive: canAct,
                    interrupted: !canAct,
                    actorPosition: transform.position,
                    projectileOrigin: projectileOrigin,
                    aimDirection: aimDirection);
            }
            else
            {
                result = _magusCombatState.Tick(command, isAlive: false, interrupted: true);
            }

            _lastMagusCombatResult = result;
            _magusBasicAttackSnapshot = result.BasicAttack;
            IsAttacking = result.BasicAttack.IsAttacking && canAct && roomReady && !_dodgeActive;

            if (result.SkillOutcome.EnergySpent > 0)
            {
                _nextMagusEnergyRecoveryTick = command.Tick + 60;
                _nextArmorEnergyRecoveryTick = command.Tick + 60;
            }
            else if (roomReady && !IsDead)
            {
                RecoverMagusEnergy(command.Tick);
            }

            if (result.BasicAttack.WasStartedThisTick)
            {
                MagusBasicAttackActivationCount++;
            }

            if (result.SkillActivation != null)
            {
                MagusSkillActivationCount++;
            }
        }

        private void RecoverMagusEnergy(long tick)
        {
            RecoverEnergy(tick, ref _nextMagusEnergyRecoveryTick);
        }

        private void RecoverEnergy(long tick, ref long nextBaseRecoveryTick)
        {
            int currentEnergy = _skillUseState.CurrentEnergy;
            int maximumEnergy = _skillUseState.MaximumEnergy;
            if (currentEnergy >= maximumEnergy)
            {
                nextBaseRecoveryTick = tick + 12;
                _nextArmorEnergyRecoveryTick = tick + 60;
                return;
            }

            long baseRecoveryAmount = 0;
            if (tick >= nextBaseRecoveryTick)
            {
                long elapsedRecoveryTicks = tick - nextBaseRecoveryTick;
                baseRecoveryAmount = 1 + elapsedRecoveryTicks / 12;
                nextBaseRecoveryTick += baseRecoveryAmount * 12L;
            }

            long armorRecoveryAmount = 0;
            if (_armorEnergyRegenerationPerSecondBonus > 0 && tick >= _nextArmorEnergyRecoveryTick)
            {
                long elapsedArmorTicks = tick - _nextArmorEnergyRecoveryTick;
                long recoveryIntervals = 1 + elapsedArmorTicks / 60;
                armorRecoveryAmount = recoveryIntervals * _armorEnergyRegenerationPerSecondBonus;
                _nextArmorEnergyRecoveryTick += recoveryIntervals * 60L;
            }

            long totalRecovery = baseRecoveryAmount + armorRecoveryAmount;
            if (totalRecovery > 0)
            {
                _skillUseState.SetCurrentEnergy((int)Math.Min(
                    maximumEnergy,
                    (long)currentEnergy + totalRecovery));
            }
        }

        private Vector2 MagusProjectileOrigin()
        {
            return new Vector2(
                transform.position.x + (_facingDirection * 0.55f),
                transform.position.y + 0.25f);
        }

        private void HandleMagusBasicAttackRequested(MagusBasicAttackRequest request)
        {
            if (request.ActorId != _actorId)
            {
                throw new InvalidOperationException("Magus basic-attack request belongs to a different actor.");
            }

            if (_magusRoomBinding == null || _magusRoomBinding.IsDisposed)
            {
                throw new InvalidOperationException("Magus basic attacks require a live room binding.");
            }

            if (request.Snapshot.Projectile == null)
            {
                throw new InvalidOperationException("An accepted Magus basic attack must include its weapon projectile definition.");
            }

            Vector2 aimDirection = ResolveMagusAimDirection(
                request.Origin,
                new Vector2(_facingDirection, 0f),
                ProjectileTravelDistance(request.Snapshot.Projectile));
            _magusRoomBinding.ProjectileScope.Pool.Spawn(
                request.Snapshot.Projectile,
                EntityId.New(),
                request.ActorId,
                request.Origin,
                aimDirection,
                _magusTargetLayers,
                _magusRoomBinding.ProjectileScope);
            MagusBasicAttackProjectileSpawnCount++;
        }

        private Vector2 ResolveRequestedFireballAimDirection(
            PlayerCommand command,
            Vector2 projectileOrigin,
            Vector2 fallbackDirection)
        {
            SkillDefinition fireballSkill = null;
            if (_skillUseState.IsPreparing &&
                string.Equals(_skillUseState.PreparingSkillId, MagusSkillLoadout.FireballId, StringComparison.Ordinal))
            {
                fireballSkill = GetSelectedMagusSkill(MagusSkillLoadout.FireballId);
            }
            else if (command.WasPressed(PlayerAction.Skill1) &&
                     string.Equals(_magusSkillLoadout.Skill1.Id, MagusSkillLoadout.FireballId, StringComparison.Ordinal))
            {
                fireballSkill = _magusSkillLoadout.Skill1;
            }
            else if (command.WasPressed(PlayerAction.Skill2) &&
                     string.Equals(_magusSkillLoadout.Skill2.Id, MagusSkillLoadout.FireballId, StringComparison.Ordinal))
            {
                fireballSkill = _magusSkillLoadout.Skill2;
            }

            if (fireballSkill == null) return fallbackDirection.normalized;

            float projectileRange = ProjectileTravelDistance(
                fireballSkill.ProjectileSpeed,
                fireballSkill.ProjectileLifetimeTicks);
            return ResolveMagusAimDirection(projectileOrigin, fallbackDirection, projectileRange);
        }

        private SkillDefinition GetSelectedMagusSkill(string skillId)
        {
            if (string.Equals(_magusSkillLoadout.Skill1.Id, skillId, StringComparison.Ordinal)) return _magusSkillLoadout.Skill1;
            if (string.Equals(_magusSkillLoadout.Skill2.Id, skillId, StringComparison.Ordinal)) return _magusSkillLoadout.Skill2;
            return null;
        }

        private Vector2 ResolveMagusAimDirection(Vector2 origin, Vector2 fallbackDirection, float maximumRange)
        {
            Vector2 fallback = new Vector2(_facingDirection, 0f);
            if (fallbackDirection.sqrMagnitude > 0.000001f)
            {
                fallback = new Vector2(Mathf.Sign(fallbackDirection.x), 0f);
            }

            if (maximumRange <= 0f) return fallback;

            int overlapCount = Physics2D.OverlapCircle(
                origin,
                maximumRange,
                _magusAimContactFilter,
                _magusAimOverlaps);
            while (overlapCount >= _magusAimOverlaps.Length)
            {
                Array.Resize(ref _magusAimOverlaps, checked(_magusAimOverlaps.Length * 2));
                overlapCount = Physics2D.OverlapCircle(
                    origin,
                    maximumRange,
                    _magusAimContactFilter,
                    _magusAimOverlaps);
            }

            float maximumRangeSquared = maximumRange * maximumRange;
            float nearestDistanceSquared = float.PositiveInfinity;
            ActorId nearestActorId = default;
            Vector2 nearestPoint = default;
            bool foundTarget = false;

            for (int index = 0; index < overlapCount; index++)
            {
                Collider2D targetCollider = _magusAimOverlaps[index];
                if (targetCollider == null) continue;

                ProjectileImpactTarget2D target = targetCollider.GetComponentInParent<ProjectileImpactTarget2D>();
                if (target == null || !target.IsInitialized || target.ActorId == _actorId) continue;

                Vector2 targetPoint = targetCollider.ClosestPoint(origin);
                Vector2 toTarget = targetPoint - origin;
                float distanceSquared = toTarget.sqrMagnitude;
                if (distanceSquared <= 0.000001f || distanceSquared > maximumRangeSquared) continue;

                Vector2 direction = toTarget / Mathf.Sqrt(distanceSquared);
                float facingDot = Vector2.Dot(fallback, direction);
                if (facingDot <= 0f || facingDot < MagusAimAssistMinimumDot) continue;

                int obstructionCount = Physics2D.Linecast(
                    origin,
                    targetPoint,
                    _groundContactFilter,
                    _magusLineOfSightHits);
                if (obstructionCount > 0 && _magusLineOfSightHits[0].collider != null) continue;

                if (foundTarget && !IsPreferredAimCandidate(
                        distanceSquared,
                        target.ActorId,
                        targetPoint,
                        nearestDistanceSquared,
                        nearestActorId,
                        nearestPoint))
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                nearestActorId = target.ActorId;
                nearestPoint = targetPoint;
                foundTarget = true;
            }

            return foundTarget ? (nearestPoint - origin).normalized : fallback;
        }

        private static bool IsPreferredAimCandidate(
            float candidateDistanceSquared,
            ActorId candidateActorId,
            Vector2 candidatePoint,
            float nearestDistanceSquared,
            ActorId nearestActorId,
            Vector2 nearestPoint)
        {
            if (candidateDistanceSquared < nearestDistanceSquared) return true;
            if (candidateDistanceSquared > nearestDistanceSquared) return false;

            int actorOrder = candidateActorId.Value.CompareTo(nearestActorId.Value);
            if (actorOrder != 0) return actorOrder < 0;
            if (candidatePoint.x != nearestPoint.x) return candidatePoint.x < nearestPoint.x;
            return candidatePoint.y < nearestPoint.y;
        }

        private static float ProjectileTravelDistance(ProjectileDefinition projectile)
        {
            return projectile == null
                ? 0f
                : ProjectileTravelDistance(projectile.SpeedUnitsPerSecond, projectile.LifetimeTicks);
        }

        private static float ProjectileTravelDistance(decimal speedUnitsPerSecond, int lifetimeTicks)
        {
            return (float)speedUnitsPerSecond * lifetimeTicks * Time.fixedDeltaTime;
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

        internal void OnMagusRoomDisposed(MagusRoomBinding binding)
        {
            if (_magusRoomBinding != binding) return;

            _magusCombatState?.Interrupt();
            _magusRoomBinding = null;
            _magusBasicAttackSnapshot = default;
            IsAttacking = false;
        }

        private void CollectMeleeImpacts(MeleeHitWindow hitWindow)
        {
            WeaponMeleeParameters melee = _weaponBasicAttackProfile != null
                ? _weaponBasicAttackProfile.MeleeParameters
                : null;
            float centerOffsetX = melee != null ? melee.CenterOffsetX : 0.9f;
            float centerOffsetY = melee != null ? melee.CenterOffsetY : 0.9f;
            Vector2 size = melee != null
                ? new Vector2(melee.Width, melee.Height)
                : new Vector2(1.35f, 1.25f);
            Vector2 center = new Vector2(
                transform.position.x + _facingDirection * centerOffsetX,
                transform.position.y + centerOffsetY);
            int overlapCount = Physics2D.OverlapBox(center, size, 0f, _meleeContactFilter, _meleeOverlaps);
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

                Vector2 knockback = Vector2.zero;
                int aiControlSuspensionTicks = 0;
                bool targetIsBoss = target.GetComponentInParent<CustodianBoss2D>() != null;
                if (!targetIsBoss && melee != null && melee.AiControlSuspensionTicks > 0 &&
                    (melee.KnockbackHorizontalSpeed > 0f || melee.KnockbackVerticalSpeed > 0f))
                {
                    float targetOffsetX = target.transform.position.x - transform.position.x;
                    float directionAwayFromAttacker = Mathf.Abs(targetOffsetX) > MovementDeadZone
                        ? Mathf.Sign(targetOffsetX)
                        : _facingDirection;
                    float raceMultiplier = (float)PlayableRaceRules.GetOutgoingKnockbackMultiplier(_raceId);
                    knockback = new Vector2(
                        directionAwayFromAttacker * melee.KnockbackHorizontalSpeed * raceMultiplier,
                        melee.KnockbackVerticalSpeed * raceMultiplier);
                    aiControlSuspensionTicks = melee.AiControlSuspensionTicks;
                }

                var impact = new MeleeImpact(
                    hitWindow.Id,
                    _actorId,
                    target.ActorId,
                    hitWindow.BaseDamage * hitWindow.DamageMultiplier,
                    transform.position.x,
                    CurrentPhysicsTick(),
                    knockback,
                    aiControlSuspensionTicks);
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
