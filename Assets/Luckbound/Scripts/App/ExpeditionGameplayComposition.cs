using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
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
        private ExpeditionRunConfiguration _run;
        private SkillCatalog _skillCatalog;
        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private PlayerHudPresenter _hudPresenter;
        private ExpeditionPauseController _pauseController;
        private GameObject _playerObject;
        private bool _attempted;
        private bool _cameraBound;

        public bool IsReady { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public PlayerMotor2D PlayerMotor => _playerMotor;
        public GameObject PlayerObject => _playerObject;
        public ExpeditionRoomTransitionController RoomTransitions => _roomTransitions;

        public bool TryInitialize(ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint, out string error)
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
                if (_productionPlayerPrefab == null)
                    return Fail("F03 pendiente: falta registrar el prefab de jugador final; el jugador B07 es solo una referencia técnica.", out error);
                if (string.Equals(_productionPlayerPrefab.name, "MovementDemoPlayer", StringComparison.Ordinal))
                    return Fail("El jugador B07 no se puede usar como personaje final de Expedición.", out error);
                PlayerMotor2D prefabMotor = _productionPlayerPrefab.GetComponent<PlayerMotor2D>();
                ItemCollector2D prefabCollector = _productionPlayerPrefab.GetComponent<ItemCollector2D>();
                PlayerVisualStatePresenter prefabVisual = _productionPlayerPrefab.GetComponent<PlayerVisualStatePresenter>();
                if (prefabMotor == null || prefabCollector == null || prefabVisual == null ||
                    _productionPlayerPrefab.GetComponent<Rigidbody2D>() == null ||
                    _productionPlayerPrefab.GetComponent<Collider2D>() == null)
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

                foreach (ExpeditionRouteNode node in run.Route.Nodes)
                {
                    if (!ExpeditionRoomPrefabRegistry.TryResolve(node.TemplateId, _roomPrefabs,
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

                _run = run;
                ActorId actorId = ActorId.New();
                _inputSource = PlayerInputSource.CreateDefault(actorId);
                _playerObject = Instantiate(_productionPlayerPrefab, transform.position, transform.rotation, transform);
                _playerObject.name = "ExpeditionPlayerSession";
                _playerMotor = _playerObject.GetComponent<PlayerMotor2D>();
                ItemCollector2D collector = _playerObject.GetComponent<ItemCollector2D>();
                PlayerVisualStatePresenter visualPresenter = _playerObject.GetComponent<PlayerVisualStatePresenter>();

                if (_roomTransitions == null) _roomTransitions = GetComponent<ExpeditionRoomTransitionController>();
                if (_roomTransitions == null) _roomTransitions = gameObject.AddComponent<ExpeditionRoomTransitionController>();
                _roomTransitions.RoomActivated += OnRoomActivated;
                _roomTransitions.Initialize(run.RunId, run.Route, ResolveRoomPrefab, _playerObject.transform,
                    checkpoint.visitedNodeIds);

                ProjectilePool2D projectilePool = null;
                RoomProjectileScope projectileScope = null;
                if (requiresProjectiles)
                    CreateProjectileScope(_roomTransitions.ActiveScope, out projectilePool, out projectileScope);

                ExpeditionPlayerRunBinder.BindFromCheckpoint(
                    run, checkpoint, actorId, _inputSource, LayerMask.GetMask("Ground"), LayerMask.GetMask("Enemies"),
                    0, _playerMotor, collector, run.Inventory.Catalog, weapons, _skillCatalog,
                    projectilePool, projectileScope, requiresProjectiles ? _roomTransitions.ActiveScope : null,
                    fortuneBrewRuntime: _application as IPlayerFortuneBrewRuntime, armorCatalog: armors);
                visualPresenter.Initialize(_playerMotor);

                _gameplayHudRoot.SetActive(true);
                _hudPresenter = GetComponent<PlayerHudPresenter>();
                if (_hudPresenter == null) _hudPresenter = gameObject.AddComponent<PlayerHudPresenter>();
                _hudPresenter.Initialize(_playerMotor, _playerHudText);
                _pauseController = GetComponent<ExpeditionPauseController>();
                if (_pauseController == null) _pauseController = gameObject.AddComponent<ExpeditionPauseController>();
                _pauseController.Initialize(_pauseLabelText);
                _inputSource.PausePressed += _pauseController.TogglePause;
                _pauseController.PauseChanged += OnPauseChanged;
                _returnToTavernButton.onClick.AddListener(ReturnToTavern);
                _returnToTavernButton.gameObject.SetActive(false);
                IsReady = true;
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

        private GameObject ResolveRoomPrefab(string templateId)
        {
            if (ExpeditionRoomPrefabRegistry.TryResolve(templateId, _roomPrefabs,
                    out GameObject prefab, out string error)) return prefab;
            throw new InvalidOperationException(error);
        }

        private void OnRoomActivated(RoomLifetimeScope scope)
        {
            RoomTemplatePrefab2D room = _roomTransitions.ActiveRoomRoot.GetComponentsInChildren<RoomTemplatePrefab2D>(true)
                .SingleOrDefault();
            if (room == null) throw new InvalidOperationException("The active room has no unique RoomTemplatePrefab2D camera bounds.");
            if (_cameraBound) _cameraRig.RebindRoom(room.CameraBounds);
            else
            {
                Transform cameraTarget = _playerObject.transform.Find("CameraTarget");
                _cameraRig.Bind(cameraTarget != null ? cameraTarget : _playerObject.transform, room.CameraBounds);
                _cameraBound = true;
            }

            _application.BindExpeditionRoomAmbience(_roomTransitions);
            if (_playerMotor != null && _playerMotor.ActorId.IsValid &&
                (_playerMotor.IsVenator || _playerMotor.IsMagus))
                BindRangedClassToRoom(scope);
        }

        private void BindRangedClassToRoom(RoomLifetimeScope roomLifetimeScope)
        {
            CreateProjectileScope(roomLifetimeScope, out ProjectilePool2D pool, out RoomProjectileScope scope);
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
        }

        private void ReturnToTavern()
        {
            if (_pauseController != null) _pauseController.SetPaused(false);
            Time.timeScale = 1f;
            SceneManager.LoadScene("Hub", LoadSceneMode.Single);
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
                _pauseController.SetPaused(false);
            }
            if (_returnToTavernButton != null) _returnToTavernButton.onClick.RemoveListener(ReturnToTavern);
            if (_application != null) _application.UnbindExpeditionRoomAmbience();
            if (_playerObject != null) Destroy(_playerObject);
            _playerObject = null;
            _playerMotor = null;
            if (_gameplayHudRoot != null) _gameplayHudRoot.SetActive(false);
            IsReady = false;
        }

        private void OnDestroy()
        {
            CleanupRuntime();
        }
    }
}
