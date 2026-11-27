using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Input;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Composes one saved expedition actor and its active room without using B07 demo assets.</summary>
    public sealed class ExpeditionGameplayComposition : MonoBehaviour
    {
        private const string WeaponCatalogResource = "Luckbound/Data/Weapons";
        private const string SkillCatalogResource = "Luckbound/Data/Skills";

        [SerializeField, Tooltip("F03 production actor only. The B07 MovementDemoPlayer is deliberately unsupported here.")]
        private GameObject _productionPlayerPrefab;
        [SerializeField] private ExpeditionRoomPrefabEntry[] _roomPrefabs = Array.Empty<ExpeditionRoomPrefabEntry>();
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private RoomCameraRig2D _cameraRig;
        [SerializeField] private GameObject _gameplayHudRoot;
        [SerializeField] private Text _playerHudText;
        [SerializeField] private Text _pauseLabelText;
        [SerializeField] private Button _returnToTavernButton;
        [SerializeField] private ExpeditionRoomTransitionController _roomTransitions;

        private ApplicationBootstrapper _application;
        private ApplicationAudioRouter _audioRouter;
        private ExpeditionRunConfiguration _run;
        private ItemCatalog _itemCatalog;
        private WeaponCatalog _weaponCatalog;
        private ArmorCatalog _armorCatalog;
        private SkillCatalog _skillCatalog;
        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private ItemCollector2D _itemCollector;
        private PlayerHudPresenter _hudPresenter;
        private ExpeditionPauseController _pauseController;
        private ExpeditionSessionReadinessRegistration _readinessRegistration;
        private IDisposable _simulationPauseLeaseProviderBinding;
        private IDisposable _sessionMutationPublicationBinding;
        private Text _returnToTavernButtonLabel;
        private GameObject _playerObject;
        private ExpeditionRoomPrefabEntry[] _activeRoomPrefabs;
        private bool _attempted;
        private bool _cameraBound;
        private bool _retryingPendingRoomAdvance;
#if UNITY_EDITOR
        private ApplicationBootstrapper.F10V7SessionInitializationBinding _f10V7InitializationBinding;
        private IDisposable _f10V7InitializationPauseLease;
        private bool _f10V7InitializationPrepared;
        private bool _f10V7CleanupPlayerInactiveVerified;
        private V7InitializationCleanupReceipt _f10V7InitializationCleanupReceipt;
#endif

        public bool IsReady { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public PlayerMotor2D PlayerMotor => _playerMotor;
        public GameObject PlayerObject => _playerObject;
        public ExpeditionRoomTransitionController RoomTransitions => _roomTransitions;

        internal ExpeditionReadinessCompositionView CaptureReadinessCompositionView()
        {
            return new ExpeditionReadinessCompositionView(this, _application, _readinessRegistration, _run,
                _playerObject, _playerMotor, _itemCollector, _roomTransitions, _inputSource,
                _pauseController, _cameraRig, _projectilePrefab, _skillCatalog, _itemCatalog,
                _weaponCatalog, _armorCatalog, IsReady);
        }

#if UNITY_EDITOR
        internal ApplicationBootstrapper.F10V7SessionInitializationBinding CaptureV7InitializationBinding() =>
            _f10V7InitializationBinding;

        internal bool IsEligibleForAdmittedV7Initialization(ApplicationBootstrapper bootstrapper)
        {
            return ReferenceEquals(bootstrapper, ApplicationBootstrapper.Instance) &&
                isActiveAndEnabled && !_attempted && !IsReady &&
                _f10V7InitializationBinding == null && _f10V7InitializationPauseLease == null &&
                !_f10V7InitializationPrepared && _readinessRegistration == null &&
                _playerObject == null && _playerMotor == null && _itemCollector == null &&
                _inputSource == null && _sessionMutationPublicationBinding == null &&
                _simulationPauseLeaseProviderBinding == null &&
                HasNoActiveV7RoomTransition();
        }

        private bool HasNoActiveV7RoomTransition()
        {
            if (_roomTransitions == null) return true;
            ExpeditionReadinessTransitionView view = _roomTransitions.CaptureReadinessTransitionView();
            return view != null && !view.IsInitialized && !view.IsDisposed &&
                !view.IsTransitioning && !view.HasPreparedTransition &&
                view.ActiveScope == null && view.ActiveRoomRoot == null;
        }

        internal bool TryInstallAdmittedV7InitializationBinding(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding, out string error)
        {
            ApplicationBootstrapper bootstrapper = ApplicationBootstrapper.Instance;
            if (binding == null || !binding.IsOwnedBy(bootstrapper, this) ||
                !IsEligibleForAdmittedV7Initialization(bootstrapper) ||
                !bootstrapper.CanIssueV7SessionInitializationBinding(binding, this))
            {
                error = "La composición o la admisión App no están vacías para instalar el binding V7 exacto.";
                return false;
            }

            _f10V7InitializationCleanupReceipt = null;
            _f10V7CleanupPlayerInactiveVerified = false;
            _f10V7InitializationBinding = binding;
            if (binding.IsCurrentFor(bootstrapper, this))
            {
                error = string.Empty;
                return true;
            }

            _f10V7InitializationBinding = null;
            error = "El binding V7 dejó de ser vigente inmediatamente después de instalarse.";
            return false;
        }

        internal bool BeginAdmittedV7Initialization(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding,
            ExpeditionRunConfiguration run, ExpeditionSaveData readOnlyCheckpoint, out string error)
        {
            if (binding == null || !ReferenceEquals(binding, _f10V7InitializationBinding) ||
                !binding.IsCurrentFor(ApplicationBootstrapper.Instance, this) ||
                !isActiveAndEnabled || _attempted || IsReady)
            {
                error = "La composición perdió el binding instalado antes de iniciar la inicialización V7.";
                return false;
            }

            bool initialized = binding.VerifiedPhysicalInputs != null
                ? TryInitializeAdmittedV7Technical(binding, out error)
                : TryInitializeCore(run, readOnlyCheckpoint, _productionPlayerPrefab,
                    _roomPrefabs, false, out error);
            if (!initialized || !_f10V7InitializationPrepared)
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "La preparación V7 no terminó su bind inicial.";
                RollbackAdmittedV7Initialization(binding);
                return false;
            }
            return true;
        }

        internal bool FinalizeAdmittedV7Initialization(out string error)
        {
            if (_f10V7InitializationBinding == null || !_f10V7InitializationPrepared ||
                IsReady || !isActiveAndEnabled ||
                !_f10V7InitializationBinding.IsCurrentFor(_application, this))
            {
                error = "La composición perdió su binding V7 antes de la publicación final.";
                return false;
            }

            try
            {
                FinalizeGameplayPresentation(
                    technical: _f10V7InitializationBinding.VerifiedPhysicalInputs != null,
                    publishV6MutationBinding: false);
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                CleanupRuntime();
                error = "No se pudo finalizar la composición V7: " + exception.Message;
                LastError = error;
                return false;
            }
        }

        internal bool TryInitializeAdmittedV7Technical(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding, out string error)
        {
            if (binding == null || !ReferenceEquals(binding, _f10V7InitializationBinding) ||
                !binding.IsCurrentFor(ApplicationBootstrapper.Instance, this) ||
                !isActiveAndEnabled || _attempted || IsReady)
            {
                error = "Technical V7 composition requires the exact installed, current App session binding.";
                return false;
            }
            if (binding.VerifiedPhysicalInputs == null)
            {
                error = "Technical V7 composition requires the opaque verified physical-input receipt.";
                return false;
            }
            if (!ApplicationBootstrapper.TryRevalidateExactF10V7PhysicalInputs(
                    binding.VerifiedPhysicalInputs, out F10V7VerifiedPhysicalInputs freshInputs,
                    out error))
                return false;
            if (!TryResolveF10V7TechnicalAssets(binding.Run, freshInputs,
                    out GameObject playerPrefab, out ExpeditionRoomPrefabEntry[] rooms, out error))
                return false;

            return TryInitializeCore(binding.Run, binding.Checkpoint, playerPrefab, rooms,
                true, out error);
        }

        internal void CompleteAdmittedV7Publication(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding)
        {
            if (!ReferenceEquals(binding, _f10V7InitializationBinding) || !IsReady)
            {
                LastError = "El lease de pausa V7 se conserva porque el binding publicado ya no coincide.";
                return;
            }

            _f10V7InitializationPrepared = false;
            IDisposable lease = _f10V7InitializationPauseLease;
            if (lease != null)
            {
                try
                {
                    lease.Dispose();
                    _f10V7InitializationPauseLease = null;
                }
                catch
                {
                    // The pause controller removes the lease before notifying listeners. A
                    // listener exception can therefore be settled by disposing the same lease
                    // again without producing another resume notification.
                    try
                    {
                        lease.Dispose();
                        _f10V7InitializationPauseLease = null;
                    }
                    catch { LastError = "V7 quedó publicado; la notificación de reanudación falló."; }
                }
            }
            try
            {
                if (_pauseController != null && _inputSource != null)
                    _inputSource.SetGameplayActionsEnabled(!_pauseController.IsSimulationPaused);
            }
            catch
            {
                LastError = "V7 quedó publicado; no se pudo sincronizar el estado de entrada tras el lease.";
            }
        }

        internal void AbortAdmittedV7Initialization(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding, string reason)
        {
            if (binding != null && !ReferenceEquals(binding, _f10V7InitializationBinding)) return;
            if (binding != null)
                RollbackAdmittedV7Initialization(binding);
            else
            {
                CleanupRuntime();
                _f10V7InitializationBinding = null;
                _f10V7InitializationPrepared = false;
                _f10V7InitializationCleanupReceipt = null;
            }
            LastError = reason ?? "La inicialización V7 fue abortada.";
        }

        internal bool ConsumeVerifiedAdmittedV7InitializationCleanup(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding)
        {
            V7InitializationCleanupReceipt receipt = _f10V7InitializationCleanupReceipt;
            if (receipt == null || !ReferenceEquals(receipt.Binding, binding) ||
                !ReferenceEquals(receipt.Composition, this) ||
                !IsV7InitializationRuntimeClear())
                return false;

            _f10V7InitializationCleanupReceipt = null;
            return true;
        }

        private void RollbackAdmittedV7Initialization(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding)
        {
            _f10V7InitializationCleanupReceipt = null;
            CleanupRuntime();
            _f10V7InitializationBinding = null;
            _f10V7InitializationPrepared = false;

            ApplicationBootstrapper bootstrapper = binding?.Bootstrapper;
            if (binding != null && IsV7InitializationRuntimeClear() &&
                bootstrapper != null && bootstrapper.CanRecordV7SessionStartCleanup(binding, this))
                _f10V7InitializationCleanupReceipt = new V7InitializationCleanupReceipt(this, binding);
        }

        private bool IsV7InitializationRuntimeClear()
        {
            return !IsReady && _f10V7InitializationBinding == null &&
                _f10V7InitializationPauseLease == null && !_f10V7InitializationPrepared &&
                _f10V7CleanupPlayerInactiveVerified &&
                _readinessRegistration == null && _playerObject == null && _playerMotor == null &&
                _itemCollector == null && _inputSource == null &&
                _sessionMutationPublicationBinding == null && _simulationPauseLeaseProviderBinding == null &&
                (_roomTransitions == null || _roomTransitions.IsDisposed);
        }

        private sealed class V7InitializationCleanupReceipt
        {
            internal V7InitializationCleanupReceipt(ExpeditionGameplayComposition composition,
                ApplicationBootstrapper.F10V7SessionInitializationBinding binding)
            {
                Composition = composition;
                Binding = binding;
            }

            internal ExpeditionGameplayComposition Composition { get; }
            internal ApplicationBootstrapper.F10V7SessionInitializationBinding Binding { get; }
        }

        private void PrepareAdmittedV7SimulationPauseLease(bool technical)
        {
            _pauseController = GetComponent<ExpeditionPauseController>();
            if (_pauseController == null)
                _pauseController = gameObject.AddComponent<ExpeditionPauseController>();
            _pauseController.Initialize(_pauseLabelText, pauseOnFocusLoss: !technical);
            _f10V7InitializationPauseLease = _pauseController.AcquireSimulationPauseLease();
        }
#endif

        public bool TryInitialize(ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint, out string error)
        {
            return TryInitializeCore(run, checkpoint, _productionPlayerPrefab, _roomPrefabs, false, out error);
        }

#if UNITY_EDITOR
        internal void ConfigureF10TechnicalPresentation(RoomCameraRig2D cameraRig, GameObject gameplayHudRoot,
            Text playerHudText, Text pauseLabelText, Button returnButton)
        {
            ApplicationBootstrapper owner = ApplicationBootstrapper.Instance;
            if (_attempted || owner == null || !owner.OwnsF10DiagnosticRun(owner.ActiveRun))
                throw new InvalidOperationException("Technical presentation requires the uninitialized isolated F10 session.");
            if (cameraRig == null || gameplayHudRoot == null || playerHudText == null ||
                pauseLabelText == null || returnButton == null)
                throw new ArgumentException("Technical presentation requires its camera, HUD, labels and return button.");
            ConfigureTechnicalPresentationFields(cameraRig, gameplayHudRoot, playerHudText,
                pauseLabelText, returnButton);
        }

        internal void ConfigureAdmittedV7TechnicalPresentation(string exactPrimaryJson,
            RoomCameraRig2D cameraRig, GameObject gameplayHudRoot, Text playerHudText,
            Text pauseLabelText, Button returnButton)
        {
            ApplicationBootstrapper owner = ApplicationBootstrapper.Instance;
            if (_attempted || owner == null || !owner.OwnsExactF10V7AdmissionPrimaryJson(exactPrimaryJson))
                throw new InvalidOperationException(
                    "Technical V7 presentation requires the exact App-admitted primary JSON before composition starts.");
            if (cameraRig == null || gameplayHudRoot == null || playerHudText == null ||
                pauseLabelText == null || returnButton == null)
                throw new ArgumentException("Technical V7 presentation requires its camera, HUD, labels and return button.");
            ConfigureTechnicalPresentationFields(cameraRig, gameplayHudRoot, playerHudText,
                pauseLabelText, returnButton);
        }

        private void ConfigureTechnicalPresentationFields(RoomCameraRig2D cameraRig,
            GameObject gameplayHudRoot, Text playerHudText, Text pauseLabelText, Button returnButton)
        {
            _cameraRig = cameraRig;
            _gameplayHudRoot = gameplayHudRoot;
            _playerHudText = playerHudText;
            _pauseLabelText = pauseLabelText;
            _returnToTavernButton = returnButton;
        }

        /// <summary>Explicit internal entry for the isolated F10 diagnostic session; never enables the product prefab guard.</summary>
        internal bool TryInitializeF10Technical(ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint,
            GameObject technicalPlayerPrefab, ExpeditionRoomPrefabEntry[] technicalRooms, out string error)
        {
            ApplicationBootstrapper owner = ApplicationBootstrapper.Instance;
            if (owner == null || !owner.OwnsF10DiagnosticRun(run))
            {
                error = "La demo técnica requiere una sesión F10 aislada y guardada por su dueño.";
                return false;
            }
            if (technicalPlayerPrefab == null ||
                !string.Equals(technicalPlayerPrefab.name, "F10TechnicalDiagnosticPlayer", StringComparison.Ordinal) ||
                technicalRooms == null || run.Route == null || run.Route.GenerationVersion != 2)
            {
                error = "La demo técnica requiere el jugador diagnóstico y la ruta F10 explícitos.";
                return false;
            }
            return TryInitializeCore(run, checkpoint, technicalPlayerPrefab, technicalRooms, true, out error);
        }

        private static readonly string[] F10V7TechnicalNodeIds =
        {
            "sample.tutorial", "sample.patio.combat", "sample.patio.exploration",
            "sample.patio.guardian", "sample.safe_extraction"
        };
        private static readonly string[] F10V7TechnicalTemplateIds =
        {
            "room.sample.tutorial", "room.patio.c1", "room.patio.e1",
            "room.patio.guardian", "room.sample.extraction"
        };
        private const string F10V7TechnicalPlayerPrefabPath =
            "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab";

        private static bool TryResolveF10V7TechnicalAssets(ExpeditionRunConfiguration run,
            F10V7VerifiedPhysicalInputs inputs, out GameObject playerPrefab,
            out ExpeditionRoomPrefabEntry[] rooms, out string error)
        {
            playerPrefab = null;
            rooms = null;
            error = string.Empty;
            if (run == null || run.Route == null || inputs == null || run.Route.GenerationVersion != 2 ||
                run.ClassId != "class.bellator" || run.RaceId != ExpeditionRunConfiguration.HumanRaceId ||
                run.Route.Nodes.Count != F10V7TechnicalNodeIds.Length ||
                !run.Route.MandatoryNodeIds.SequenceEqual(F10V7TechnicalNodeIds))
            {
                error = "Technical V7 requires the exact five-node F10 Bellator/Human sample route.";
                return false;
            }

            if (!TryResolveVerifiedAsset(inputs, F10V7TechnicalPlayerPrefabPath,
                    out playerPrefab, out error)) return false;
            rooms = new ExpeditionRoomPrefabEntry[F10V7TechnicalNodeIds.Length];
            for (int i = 0; i < F10V7TechnicalNodeIds.Length; i++)
            {
                ExpeditionRouteNode node = run.Route.Nodes.SingleOrDefault(candidate =>
                    string.Equals(candidate.NodeId, F10V7TechnicalNodeIds[i], StringComparison.Ordinal));
                if (node == null || !string.Equals(node.TemplateId, F10V7TechnicalTemplateIds[i],
                        StringComparison.Ordinal) ||
                    !F10PhysicalDescriptorTableV1.TryGetDescriptor(node.TemplateId,
                        out F10PhysicalDescriptorV1 descriptor) ||
                    !string.Equals(descriptor.Kind, "room", StringComparison.Ordinal))
                {
                    error = "The admitted V7 route no longer matches the canonical F10 room descriptor table.";
                    return false;
                }
                if (!TryResolveVerifiedAsset(inputs, descriptor.RootAssetPath,
                        out GameObject roomPrefab, out error)) return false;
                rooms[i] = new ExpeditionRoomPrefabEntry
                {
                    templateId = descriptor.DescriptorId,
                    prefab = roomPrefab
                };
            }
            return true;
        }

        private static bool TryResolveVerifiedAsset<T>(F10V7VerifiedPhysicalInputs inputs,
            string exactPath, out T asset, out string error) where T : UnityEngine.Object
        {
            asset = null;
            error = string.Empty;
            try
            {
                F10V7VerifiedPhysicalInputs.AssetReference reference = inputs?.RequireAsset(exactPath);
                if (reference == null || !string.Equals(reference.AssetPath, exactPath, StringComparison.Ordinal) ||
                    !(reference.Asset is T resolved))
                {
                    error = "The verified F10 receipt has no exact Unity asset of the required type at " + exactPath + ".";
                    return false;
                }
                asset = resolved;
                return true;
            }
            catch (Exception exception)
            {
                error = "The verified F10 receipt could not resolve " + exactPath + ": " + exception.Message;
                return false;
            }
        }
#endif

        private bool TryInitializeCore(ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint,
            GameObject playerPrefab, ExpeditionRoomPrefabEntry[] roomPrefabs, bool technical, out string error)
        {
            if (_attempted)
            {
                error = IsReady ? string.Empty : LastError;
                return IsReady;
            }
            _attempted = true;

            try
            {
                _application = ApplicationBootstrapper.Instance;
                if (_application == null || run == null || checkpoint == null)
                    return Fail("No hay una expedición guardada disponible para iniciar.", out error);
                if (playerPrefab == null)
                    return Fail("F03 pendiente: falta registrar el prefab de jugador final; el jugador B07 es solo una referencia técnica.", out error);
                if (!technical && string.Equals(playerPrefab.name, "MovementDemoPlayer", StringComparison.Ordinal))
                    return Fail("El jugador B07 no se puede usar como personaje final de Expedición.", out error);
                PlayerMotor2D prefabMotor = playerPrefab.GetComponent<PlayerMotor2D>();
                ItemCollector2D prefabCollector = playerPrefab.GetComponent<ItemCollector2D>();
                PlayerVisualStatePresenter prefabVisual = playerPrefab.GetComponent<PlayerVisualStatePresenter>();
                if (prefabMotor == null || prefabCollector == null || prefabVisual == null ||
                    playerPrefab.GetComponent<Rigidbody2D>() == null ||
                    playerPrefab.GetComponent<Collider2D>() == null)
                    return Fail("El prefab F03 debe incluir motor, colector, presentación visual y física 2D.", out error);
                if (_cameraRig == null || _gameplayHudRoot == null || _playerHudText == null || _pauseLabelText == null ||
                    _returnToTavernButton == null)
                    return Fail("La escena Expedición no tiene completa la interfaz de juego y la cámara.", out error);
                if (!string.Equals(checkpoint.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                    checkpoint.generationVersion != run.Route.GenerationVersion || run.Route.Nodes.Count == 0)
                    return Fail("La ruta guardada no coincide con la configuración de esta expedición.", out error);

                ExpeditionRouteNode currentNode = run.Route.Nodes.SingleOrDefault(node =>
                    string.Equals(node.NodeId, checkpoint.currentNodeId, StringComparison.Ordinal));
                if (currentNode == null)
                    return Fail("La posición guardada no existe en la ruta activa.", out error);
                if (checkpoint.visitedNodeIds == null || checkpoint.visitedNodeIds.Length == 0 ||
                    !string.Equals(checkpoint.visitedNodeIds[checkpoint.visitedNodeIds.Length - 1],
                        currentNode.NodeId, StringComparison.Ordinal))
                    return Fail("El historial de salas no coincide con la posición guardada.", out error);

                _activeRoomPrefabs = (roomPrefabs ?? Array.Empty<ExpeditionRoomPrefabEntry>())
                    .Select(entry => entry == null ? null : new ExpeditionRoomPrefabEntry
                    {
                        templateId = entry.templateId,
                        prefab = entry.prefab
                    }).ToArray();
                foreach (ExpeditionRouteNode node in run.Route.Nodes)
                {
                    if (!ExpeditionRoomPrefabRegistry.TryResolve(node.TemplateId, _activeRoomPrefabs,
                            out _, out string registryError))
                        return Fail(registryError, out error);
                }

                bool requiresProjectiles = run.ClassId == "class.venator" || run.ClassId == "class.magus";
                if (requiresProjectiles && (_projectilePrefab == null ||
                    _projectilePrefab.GetComponent<Projectile2D>() == null ||
                    _projectilePrefab.GetComponent<Rigidbody2D>() == null ||
                    _projectilePrefab.GetComponent<Collider2D>() == null))
                    return Fail("Esta clase requiere el prefab de proyectil productivo con física y Projectile2D.", out error);

                TextAsset weaponsSource = Resources.Load<TextAsset>(WeaponCatalogResource);
                TextAsset skillsSource = Resources.Load<TextAsset>(SkillCatalogResource);
                if (weaponsSource == null || skillsSource == null || run.Inventory == null || run.Inventory.Catalog == null)
                    return Fail("Faltan catálogos obligatorios para componer la partida guardada.", out error);

                WeaponCatalog weapons = WeaponCatalogJsonLoader.Load(weaponsSource.text, run.Inventory.Catalog);
                _skillCatalog = SkillCatalogJsonLoader.Load(skillsSource.text);
                ArmorCatalog armors = ArmorCatalogJsonLoader.Load(
                    RequireResource("Luckbound/Data/Items/Armors").text, run.Inventory.Catalog);
                _itemCatalog = run.Inventory.Catalog;
                _weaponCatalog = weapons;
                _armorCatalog = armors;

                _run = run;
#if UNITY_EDITOR
                bool admittedV7Initialization = _f10V7InitializationBinding != null;
                if (admittedV7Initialization)
                {
                    if (!_f10V7InitializationBinding.IsCurrentFor(_application, this))
                        throw new InvalidOperationException("The admitted V7 owner changed before actor or room creation.");
                    PrepareAdmittedV7SimulationPauseLease(
                        _f10V7InitializationBinding.VerifiedPhysicalInputs != null);
                }
#endif
                ActorId actorId = ActorId.New();
                _inputSource = PlayerInputSource.CreateDefault(actorId);
                _playerObject = Instantiate(playerPrefab, transform.position, transform.rotation, transform);
                _playerObject.name = "ExpeditionPlayerSession";
                _playerMotor = _playerObject.GetComponent<PlayerMotor2D>();
                _itemCollector = _playerObject.GetComponent<ItemCollector2D>();
                PlayerVisualStatePresenter visualPresenter = _playerObject.GetComponent<PlayerVisualStatePresenter>();

                if (_roomTransitions == null) _roomTransitions = GetComponent<ExpeditionRoomTransitionController>();
                if (_roomTransitions == null) _roomTransitions = gameObject.AddComponent<ExpeditionRoomTransitionController>();
                _readinessRegistration = ExpeditionSessionReadinessRegistration.Create(_application, this);
                _roomTransitions.RoomActivated += OnRoomActivated;
                _roomTransitions.Initialize(run.RunId, run.Route, ResolveRoomPrefab, _playerObject.transform,
                    checkpoint.visitedNodeIds);

                ProjectilePool2D projectilePool = null;
                RoomProjectileScope projectileScope = null;
                if (requiresProjectiles)
                    CreateProjectileScope(_roomTransitions.ActiveScope, out projectilePool, out projectileScope);

                _readinessRegistration.BindFromCheckpoint(new ExpeditionCheckpointBindingArguments(
                    run, checkpoint, actorId, _inputSource, LayerMask.GetMask("Ground"), LayerMask.GetMask("Enemies"),
                    0, _playerMotor, _itemCollector, run.Inventory.Catalog, weapons, _skillCatalog,
                    projectilePool, projectileScope, requiresProjectiles ? _roomTransitions.ActiveScope : null,
                    fortuneBrewRuntime: _application as IPlayerFortuneBrewRuntime, armorCatalog: armors));
                _audioRouter = _application.AudioRouter;
                BindPlayerMagusAudio();
                visualPresenter.Initialize(_playerMotor);

#if UNITY_EDITOR
                if (admittedV7Initialization)
                {
                    _f10V7InitializationPrepared = true;
                    error = string.Empty;
                    LastError = string.Empty;
                    return true;
                }
#endif

                FinalizeGameplayPresentation(technical, publishV6MutationBinding: true);
                error = string.Empty;
                LastError = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                CleanupRuntime();
                return Fail("No se pudo componer la expedición: " + exception.Message, out error);
            }
        }

        private void FinalizeGameplayPresentation(bool technical, bool publishV6MutationBinding)
        {
            _gameplayHudRoot.SetActive(true);
            _hudPresenter = GetComponent<PlayerHudPresenter>();
            if (_hudPresenter == null) _hudPresenter = gameObject.AddComponent<PlayerHudPresenter>();
            _hudPresenter.Initialize(_playerMotor, _playerHudText);
            if (_pauseController == null)
                _pauseController = GetComponent<ExpeditionPauseController>();
            if (_pauseController == null)
                _pauseController = gameObject.AddComponent<ExpeditionPauseController>();
#if UNITY_EDITOR
            if (_f10V7InitializationBinding == null)
#endif
                _pauseController.Initialize(_pauseLabelText, pauseOnFocusLoss: !technical);
            _simulationPauseLeaseProviderBinding = _application.BindExpeditionSimulationPauseLeaseProvider(
                this, _pauseController.AcquireSimulationPauseLease);
            if (publishV6MutationBinding)
                _sessionMutationPublicationBinding = _application.BindExpeditionSessionMutationPublication(
                    this, CaptureCommittedRunState, PrepareCommittedRunMutation);
            _returnToTavernButtonLabel = _returnToTavernButton.GetComponentInChildren<Text>(true);
            _inputSource.PausePressed += _pauseController.TogglePause;
            _pauseController.PauseChanged += OnPauseChanged;
            _pauseController.SimulationPauseChanged += OnSimulationPauseChanged;
            _returnToTavernButton.onClick.AddListener(ReturnToTavern);
            _returnToTavernButton.gameObject.SetActive(false);
            IsReady = true;
        }

        private void BindPlayerMagusAudio()
        {
            if (_playerMotor == null) return;
            _playerMotor.FireballBurnApplied -= OnPlayerFireballBurnApplied;
            _playerMotor.FireballBurnApplied += OnPlayerFireballBurnApplied;
            _playerMotor.TorchActivated -= OnPlayerTorchActivated;
            _playerMotor.TorchActivated += OnPlayerTorchActivated;
        }

        private void OnPlayerFireballBurnApplied()
        {
            _audioRouter?.PlayEffect(ApplicationAudioRouter.BurnEffectId);
        }

        private void OnPlayerTorchActivated()
        {
            _audioRouter?.PlayEffect(ApplicationAudioRouter.TorchEffectId);
        }

        private GameObject ResolveRoomPrefab(string templateId)
        {
            if (ExpeditionRoomPrefabRegistry.TryResolve(templateId, _activeRoomPrefabs,
                    out GameObject prefab, out string error)) return prefab;
            throw new InvalidOperationException(error);
        }

        internal ExpeditionReadinessRoomBindingResult BindReadinessRoomServices(RoomLifetimeScope scope)
        {
            if (scope == null || scope.IsDisposed || !scope.isActiveAndEnabled ||
                _roomTransitions == null || _roomTransitions.IsDisposed ||
                !ReferenceEquals(_roomTransitions.ActiveScope, scope) ||
                !_roomTransitions.IsCurrentRoom(scope.RoomInstanceId))
                throw new InvalidOperationException("Readiness room services require the exact active room scope.");

            GameObject activeRoomRoot = _roomTransitions.ActiveRoomRoot;
            if (activeRoomRoot == null || !activeRoomRoot.activeInHierarchy)
                throw new InvalidOperationException("Readiness room services require the active room root.");
            RoomTemplatePrefab2D room = activeRoomRoot.GetComponentsInChildren<RoomTemplatePrefab2D>(true)
                .SingleOrDefault();
            if (room == null || room.CameraBounds == null)
                throw new InvalidOperationException("The active room has no unique RoomTemplatePrefab2D camera bounds.");

            if (_cameraBound) _cameraRig.RebindRoom(room.CameraBounds);
            else
            {
                Transform cameraTarget = _playerObject.transform.Find("CameraTarget");
                _cameraRig.Bind(cameraTarget != null ? cameraTarget : _playerObject.transform, room.CameraBounds);
                _cameraBound = true;
            }

            _application.BindExpeditionRoomAmbience(_roomTransitions);
            SynchronizeCommittedRunInventory();

            ProjectilePool2D projectilePool = null;
            RoomProjectileScope projectileScope = null;
            if (_playerMotor != null && _playerMotor.ActorId.IsValid &&
                (_playerMotor.IsVenator || _playerMotor.IsMagus))
                BindRangedClassToRoom(scope, out projectilePool, out projectileScope);

            return new ExpeditionReadinessRoomBindingResult(_application, _roomTransitions, scope,
                _cameraRig, room.CameraBounds, _playerMotor, _itemCollector, _run,
                projectilePool, projectileScope);
        }

#if UNITY_EDITOR
        internal bool TryPrepareAdmittedV7Publication(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding,
            ExpeditionSaveData candidateCommon, long expectedRevision,
            out ExpeditionReadinessPreparedPublication publication,
            out ExpeditionSaveData capturedCommonCandidate, out string error)
        {
            publication = null;
            capturedCommonCandidate = null;
            error = string.Empty;
            ApplicationBootstrapper app = _application;
            ExpeditionSessionReadinessRegistration registration = _readinessRegistration;
            if (binding == null || app == null || registration == null || candidateCommon == null ||
                !ReferenceEquals(binding.Composition, this) || !ReferenceEquals(binding.Bootstrapper, app) ||
                !ReferenceEquals(binding, _f10V7InitializationBinding) || !ReferenceEquals(registration.Owner, this) ||
                !ReferenceEquals(registration.Application, app) ||
                !ReferenceEquals(registration.V7InitializationBinding, binding) || !IsReady ||
                !isActiveAndEnabled || !binding.IsCurrentFor(app, this) ||
                !app.IsCurrentV7PublishedSession(binding, registration, expectedRevision) ||
                app.ActiveExpeditionRevision != expectedRevision || !ReferenceEquals(app.ActiveRun, _run) ||
                !ReferenceEquals(_run, binding.Run) || _playerMotor == null || _itemCollector == null ||
                _weaponCatalog == null || _armorCatalog == null)
            {
                error = "The V7 publication requires the exact active App, composition, registration, actor, and revision.";
                return false;
            }

            try
            {
                publication = registration.PrepareCommittedV7Publication(binding, candidateCommon,
                    _weaponCatalog, _armorCatalog, expectedRevision, out capturedCommonCandidate, out error);
                if (publication == null || capturedCommonCandidate == null ||
                    !ReferenceEquals(publication.Registration, registration) ||
                    !ReferenceEquals(publication.Run, _run) ||
                    !ReferenceEquals(publication.Activation, registration.CurrentActivation) ||
                    !ReferenceEquals(publication.CandidateSave, capturedCommonCandidate) ||
                    publication.PreparedAtRevision != expectedRevision ||
                    !app.IsCurrentV7PublishedSession(binding, registration, expectedRevision) ||
                    app.ActiveExpeditionRevision != expectedRevision)
                {
                    publication = null;
                    capturedCommonCandidate = null;
                    if (string.IsNullOrWhiteSpace(error))
                        error = "The actor capture lost the exact admitted V7 owner or base revision.";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                publication = null;
                capturedCommonCandidate = null;
                error = "The live actor could not prepare its V7 publication: " + exception.Message;
                return false;
            }
        }

        internal bool TryPrepareF10V7C1PaymentPublication(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding,
            ExpeditionRunConfiguration sourceRun, ExpeditionRunConfiguration candidateRun,
            ExpeditionSaveData candidateCommon, long expectedRevision,
            out ExpeditionReadinessPreparedPublication publication, out string error)
        {
            publication = null;
            error = string.Empty;
            ExpeditionSessionReadinessRegistration registration = _readinessRegistration;
            ExpeditionReadinessCompositionView view = CaptureReadinessCompositionView();
            if (binding == null || sourceRun == null || candidateRun == null || candidateCommon == null ||
                !ReferenceEquals(binding.Composition, this) || !ReferenceEquals(binding.Bootstrapper, _application) ||
                !ReferenceEquals(binding, _f10V7InitializationBinding) || registration == null ||
                registration.IsDisposed || !ReferenceEquals(registration.Owner, this) ||
                !ReferenceEquals(registration.Application, _application) ||
                !ReferenceEquals(registration.V7InitializationBinding, binding) ||
                !ReferenceEquals(view.ReadinessRegistration, registration) || !view.IsReady || !IsReady ||
                !isActiveAndEnabled || !ReferenceEquals(view.Run, sourceRun) ||
                !ReferenceEquals(_run, sourceRun) || !ReferenceEquals(_application.ActiveRun, sourceRun) ||
                !_application.IsCurrentV7PublishedSession(binding, registration, expectedRevision) ||
                _application.ActiveExpeditionRevision != expectedRevision)
            {
                error = "The C1 payment binder requires this exact ready V7 owner, source run, and revision.";
                return false;
            }

            publication = registration.PrepareF10V7C1PaymentPublication(binding, sourceRun,
                candidateRun, candidateCommon, expectedRevision, out error);
            if (publication == null || !ReferenceEquals(publication.Registration, registration) ||
                !ReferenceEquals(publication.Activation, registration.CurrentActivation) ||
                !ReferenceEquals(publication.Run, candidateRun) ||
                !ReferenceEquals(publication.CandidateSave, candidateCommon) ||
                publication.PreparedAtRevision != expectedRevision ||
                !ReferenceEquals(_application.ActiveRun, sourceRun) ||
                _application.ActiveExpeditionRevision != expectedRevision)
            {
                publication = null;
                if (string.IsNullOrWhiteSpace(error))
                    error = "The C1 payment binder lost its exact actor publication or base revision.";
                return false;
            }
            return true;
        }

        internal bool PublishF10V7C1PaymentRun(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding,
            ExpeditionRunConfiguration sourceRun, ExpeditionReadinessPreparedPublication publication,
            string confirmedJson, F10V7ExpeditionReadProjection projection,
            long expectedRevision, long nextRevision, out string error)
        {
            error = string.Empty;
            ExpeditionSessionReadinessRegistration registration = _readinessRegistration;
            ExpeditionRunConfiguration candidateRun = publication?.Run;
            if (binding == null || sourceRun == null || publication == null || candidateRun == null ||
                !ReferenceEquals(binding.Composition, this) || !ReferenceEquals(binding.Bootstrapper, _application) ||
                !ReferenceEquals(binding, _f10V7InitializationBinding) || registration == null ||
                registration.IsDisposed || !ReferenceEquals(registration.V7InitializationBinding, binding) ||
                !ReferenceEquals(publication.Registration, registration) ||
                !ReferenceEquals(publication.Run, candidateRun) ||
                publication.PreparedAtRevision != expectedRevision ||
                nextRevision != expectedRevision + 1 ||
                (!ReferenceEquals(_run, sourceRun) && !ReferenceEquals(_run, candidateRun)) ||
                !ReferenceEquals(_application.ActiveRun, candidateRun) ||
                _application.ActiveExpeditionRevision != nextRevision ||
                !_application.IsCurrentV7PublishedSession(binding, registration, nextRevision))
            {
                error = "The paid C1 actor publication no longer matches its App-owned V7 E1 projection.";
                return false;
            }

            _run = candidateRun;
            if (!publication.PublishConfirmedF10V7C1Payment(binding, confirmedJson, projection,
                    expectedRevision, nextRevision, out error)) return false;
            ExpeditionReadinessCompositionView after = CaptureReadinessCompositionView();
            if (after == null || !ReferenceEquals(after.Run, candidateRun) ||
                !ReferenceEquals(after.ItemCollector?.Inventory, candidateRun.Inventory))
            {
                error = "The C1 binder returned without publishing its exact paid run to composition.";
                return false;
            }
            return true;
        }
#endif

        private void OnRoomActivated(RoomLifetimeScope scope)
        {
            if (_readinessRegistration == null || _readinessRegistration.IsDisposed)
                throw new InvalidOperationException("The expedition session readiness registration is unavailable.");
            _readinessRegistration.BindActivatedRoom(scope);
        }

        private void SynchronizeCommittedRunInventory()
        {
            if (_application == null || _run == null || _playerMotor == null || _itemCollector == null ||
                !_playerMotor.ActorId.IsValid || _itemCollector.Inventory == null)
                return;

            ExpeditionRunConfiguration committedRun = _application.ActiveRun;
            if (committedRun == null || !committedRun.RunId.Equals(_run.RunId))
                throw new InvalidOperationException("The committed inventory belongs to a different expedition run.");

            ExpeditionPlayerRunBinder.PublishCommittedRunInventory(committedRun, _playerMotor, _itemCollector);
            _run = committedRun;
        }

        private Action PrepareCommittedRunMutation(ExpeditionRunConfiguration committedRun,
            ExpeditionSaveData candidateSave)
        {
#if UNITY_EDITOR
            if (_f10V7InitializationBinding != null)
                throw new InvalidOperationException("A V7 admitted session cannot enter the V6 composition mutation publisher.");
#endif
            if (!IsReady || _playerMotor == null || _itemCollector == null ||
                _itemCatalog == null || _weaponCatalog == null || _armorCatalog == null ||
                _readinessRegistration == null || _readinessRegistration.IsDisposed)
                throw new InvalidOperationException("The live expedition actor is not ready to prepare a committed mutation.");
            if (_run == null || committedRun == null || !committedRun.RunId.Equals(_run.RunId))
                throw new InvalidOperationException("A committed mutation must retain the live expedition RunId.");

            ExpeditionReadinessPreparedPublication prepared =
                _readinessRegistration.PrepareCommittedPublication(committedRun, candidateSave,
                    _weaponCatalog, _armorCatalog);
            prepared.ApplyToSave(candidateSave);
            return () =>
            {
                prepared.PublishConfirmed();
                _run = prepared.Run;
            };
        }

        private void CaptureCommittedRunState(ExpeditionRunConfiguration committedRun,
            ExpeditionSaveData candidateSave)
        {
#if UNITY_EDITOR
            if (_f10V7InitializationBinding != null)
                throw new InvalidOperationException("A V7 admitted session cannot capture a V6 profile payload.");
#endif
            if (!IsReady || _playerMotor == null || _itemCollector == null ||
                committedRun == null || candidateSave == null || _run == null ||
                !committedRun.RunId.Equals(_run.RunId) ||
                !string.Equals(candidateSave.runId, committedRun.RunId.ToString(), StringComparison.Ordinal))
                throw new InvalidOperationException("The live actor cannot capture a snapshot for another or inactive expedition.");
            if (_playerMotor.CombatStats == null || _itemCollector.Inventory == null)
                throw new InvalidOperationException("The live actor and collector must be bound before saving session state.");

            candidateSave.inventorySnapshotJson = JsonUtility.ToJson(committedRun.Inventory.CreateSnapshot());
            candidateSave.healthPotionCharges = ExpeditionSaveMapper.GetHealthPotionCharges(committedRun.Inventory);
            candidateSave.maximumHealth = _playerMotor.CombatStats.MaximumHealth;
            candidateSave.currentHealth = _playerMotor.CurrentHealth;
            candidateSave.maximumEnergy = _playerMotor.MaximumEnergy;
            candidateSave.currentEnergy = _playerMotor.CurrentEnergy;
        }

        private void BindRangedClassToRoom(RoomLifetimeScope roomLifetimeScope,
            out ProjectilePool2D pool, out RoomProjectileScope scope)
        {
            CreateProjectileScope(roomLifetimeScope, out pool, out scope);
            if (_run.ClassId == "class.venator")
                _playerMotor.ConfigureVenator(_skillCatalog, _run.SkillIds, pool, scope, roomLifetimeScope,
                    LayerMask.GetMask("Enemies"));
            else
                _playerMotor.ConfigureMagus(_skillCatalog, _run.SkillIds, pool, scope, roomLifetimeScope,
                    LayerMask.GetMask("Enemies"));
        }

        private void CreateProjectileScope(RoomLifetimeScope roomLifetimeScope, out ProjectilePool2D pool,
            out RoomProjectileScope projectileScope)
        {
            GameObject poolObject = new GameObject("ExpeditionProjectilePool");
            poolObject.transform.SetParent(roomLifetimeScope.transform, false);
            pool = poolObject.AddComponent<ProjectilePool2D>();
            pool.Initialize(_projectilePrefab);
            projectileScope = poolObject.AddComponent<RoomProjectileScope>();
            projectileScope.Initialize(Luckbound.Core.Identifiers.EntityId.New(), pool);
            roomLifetimeScope.OwnResource(projectileScope);
        }

        private void OnPauseChanged(bool paused)
        {
            if (_returnToTavernButton != null) _returnToTavernButton.gameObject.SetActive(paused);
            if (paused) RefreshReturnToTavernButtonLabel();
        }

        private void OnSimulationPauseChanged(bool paused)
        {
            _inputSource?.SetGameplayActionsEnabled(!paused);
        }

        private async void ReturnToTavern()
        {
            if (_application != null && _application.HasPendingRoomAdvance)
            {
                if (_retryingPendingRoomAdvance) return;
                RefreshReturnToTavernButtonLabel();
                _retryingPendingRoomAdvance = true;
                if (_returnToTavernButton != null) _returnToTavernButton.interactable = false;
                if (_pauseLabelText != null) _pauseLabelText.text = "Reintentando el guardado de la sala...";
                try
                {
                    RoomAdvanceCommitResult result = await _application.RetryPendingRoomAdvanceAsync();
                    if (_pauseLabelText != null)
                    {
                        _pauseLabelText.text = result.Pending || !result.Persisted
                            ? "El guardado sigue pendiente. Puedes reintentarlo desde aquí. " + result.Message
                            : result.Activated
                                ? "La sala y su avance se han guardado. " + result.Message
                                : "El avance se guardó, pero la sala no pudo activarse. " + result.Message;
                    }
                }
                catch (Exception exception)
                {
                    if (_pauseLabelText != null)
                        _pauseLabelText.text = "No se pudo reintentar el guardado. Puedes volver a intentarlo. " + exception.Message;
                }
                finally
                {
                    _retryingPendingRoomAdvance = false;
                    if (_returnToTavernButton != null) _returnToTavernButton.interactable = true;
                    RefreshReturnToTavernButtonLabel();
                }
                return;
            }

            if (_application != null && _application.HasPendingSessionMutation)
            {
                if (_pauseLabelText != null)
                    _pauseLabelText.text = "No se puede salir mientras haya un guardado en curso o pendiente. Resuelve la operación y vuelve a intentarlo.";
                return;
            }

            if (_pauseController != null) _pauseController.SetPaused(false);
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
        }

        private void RefreshReturnToTavernButtonLabel()
        {
            if (_returnToTavernButtonLabel == null) return;
            _returnToTavernButtonLabel.text = _application != null && _application.HasPendingRoomAdvance
                ? "Reintentar guardado"
                : _application != null && _application.IsRoomAdvanceInFlight
                    ? "Guardando sala…"
                    : "Volver a la Taberna";
        }

        private bool Fail(string message, out string error)
        {
            LastError = message;
            error = message;
            return false;
        }

        private static TextAsset RequireResource(string path)
        {
            TextAsset asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Required data resource is missing: " + path + ".");
            return asset;
        }

        private void CleanupRuntime()
        {
#if UNITY_EDITOR
            _f10V7CleanupPlayerInactiveVerified = false;
            // Unity destroys objects at the end of the frame. A failed V7 actor must stop
            // participating before disposing a pause lease can synchronously resume listeners.
            if ((_f10V7InitializationBinding != null || _f10V7InitializationPauseLease != null) &&
                _playerObject != null)
            {
                _playerObject.SetActive(false);
                if (_playerObject.activeSelf || _playerObject.activeInHierarchy)
                    throw new InvalidOperationException("The failed V7 actor remained active before releasing the startup pause.");
            }
#endif

            IsReady = false;
            ExpeditionSessionReadinessRegistration readiness = _readinessRegistration;
            _readinessRegistration = null;
            readiness?.Dispose();

            if (_playerMotor != null)
            {
                _playerMotor.FireballBurnApplied -= OnPlayerFireballBurnApplied;
                _playerMotor.TorchActivated -= OnPlayerTorchActivated;
            }
            if (_roomTransitions != null)
            {
                _roomTransitions.RoomActivated -= OnRoomActivated;
                _roomTransitions.Dispose();
            }
            if (_inputSource != null)
            {
                if (_pauseController != null) _inputSource.PausePressed -= _pauseController.TogglePause;
                _inputSource.Dispose();
                _inputSource = null;
            }
            if (_pauseController != null)
            {
                _pauseController.PauseChanged -= OnPauseChanged;
                _pauseController.SimulationPauseChanged -= OnSimulationPauseChanged;
                _pauseController.SetPaused(false);
            }
#if UNITY_EDITOR
            _f10V7InitializationPauseLease?.Dispose();
            _f10V7InitializationPauseLease = null;
#endif
            _sessionMutationPublicationBinding?.Dispose();
            _sessionMutationPublicationBinding = null;
            _simulationPauseLeaseProviderBinding?.Dispose();
            _simulationPauseLeaseProviderBinding = null;
            if (_returnToTavernButton != null) _returnToTavernButton.onClick.RemoveListener(ReturnToTavern);
            if (_application != null) _application.UnbindExpeditionRoomAmbience();
            if (_playerObject != null)
            {
                GameObject playerToDestroy = _playerObject;
                playerToDestroy.SetActive(false);
                if (playerToDestroy.activeSelf || playerToDestroy.activeInHierarchy)
                    throw new InvalidOperationException("The failed V7 actor remained active during rollback cleanup.");
                Destroy(playerToDestroy);
            }
            _playerObject = null;
            _playerMotor = null;
            _audioRouter = null;
            _itemCollector = null;
            _itemCatalog = null;
            _weaponCatalog = null;
            _armorCatalog = null;
            _activeRoomPrefabs = null;
            if (_gameplayHudRoot != null) _gameplayHudRoot.SetActive(false);
            IsReady = false;
#if UNITY_EDITOR
            _f10V7CleanupPlayerInactiveVerified = true;
#endif
        }

        private void OnDestroy()
        {
            CleanupRuntime();
        }
    }
}
