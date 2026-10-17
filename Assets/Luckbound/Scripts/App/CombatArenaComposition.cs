using System;
using System.Collections.Generic;
using Luckbound.Content;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Progression.Contracts;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Input;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    public enum CombatArenaResult
    {
        Ongoing = 0,
        Victory = 1,
        Defeat = 2
    }

    /// <summary>Composes the player, two-wave C08 encounter, camera and expedition HUD.</summary>
    public sealed class CombatArenaComposition : MonoBehaviour
    {
        private const string EncounterResourcePath = "Luckbound/Data/Encounters/C08Arena";
        private const float ArenaFloorTop = -3.5f;

        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _slimePrefab;
        [SerializeField] private GameObject _huesoguardiaPrefab;
        [SerializeField] private Transform _playerSpawn;
        [SerializeField] private Collider2D _roomBounds;
        [SerializeField] private RoomCameraRig2D _cameraRig;
        [SerializeField] private Text _telemetryText;
        [SerializeField] private Text _encounterText;
        [SerializeField] private Text _pauseText;
        [SerializeField] private Button _returnToMenuButton;

        private readonly List<Slime2D> _slimes = new List<Slime2D>();
        private readonly List<Huesoguardia2D> _huesoguardias = new List<Huesoguardia2D>();
        private readonly List<Huesoguardia2D> _subscribedHuesoguardias = new List<Huesoguardia2D>();
        private readonly HashSet<ActorId> _reportedHuesoguardiaDefeats = new HashSet<ActorId>();
        private CombatEncounterData _encounter;
        private GameObject _playerObject;
        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private PlayerHudPresenter _hudPresenter;
        private ExpeditionPauseController _pauseController;
        private int _waveIndex;
        private bool _resultSet;

        public PlayerMotor2D PlayerMotor => _playerMotor;
        public int CurrentWave => _waveIndex + 1;
        public int WaveCount => _encounter != null ? _encounter.waves.Length : 0;
        public CombatArenaResult Result { get; private set; }
        public IReadOnlyList<Slime2D> Slimes => _slimes;
        public IReadOnlyList<Huesoguardia2D> Huesoguardias => _huesoguardias;
        public int LivingEnemyCount => CountLivingEnemies();

        private void Start()
        {
            if (_playerPrefab == null || _slimePrefab == null || _huesoguardiaPrefab == null || _playerSpawn == null ||
                _roomBounds == null || _cameraRig == null || _telemetryText == null || _encounterText == null ||
                _pauseText == null || _returnToMenuButton == null)
            {
                throw new InvalidOperationException("CombatArenaComposition is missing a required scene reference.");
            }

            TextAsset encounterJson = Resources.Load<TextAsset>(EncounterResourcePath);
            if (encounterJson == null) throw new InvalidOperationException("Missing encounter data at Resources/" + EncounterResourcePath + ".json.");
            _encounter = CombatEncounterJsonLoader.Load(encounterJson.text);

            int groundLayer = LayerMask.NameToLayer("Ground");
            int enemiesLayer = LayerMask.NameToLayer("Enemies");
            if (groundLayer < 0 || enemiesLayer < 0) throw new InvalidOperationException("The arena requires Ground and Enemies layers.");

            ActorId playerId = ActorId.New();
            _inputSource = PlayerInputSource.CreateDefault(playerId);
            _playerObject = Instantiate(_playerPrefab, _playerSpawn.position, _playerSpawn.rotation);
            _playerObject.name = "Player";
            _playerMotor = _playerObject.GetComponent<PlayerMotor2D>();
            PlayerVisualStatePresenter visualPresenter = _playerObject.GetComponent<PlayerVisualStatePresenter>();
            if (_playerMotor == null || visualPresenter == null)
            {
                throw new InvalidOperationException("The arena player prefab must include its motor and visual presenter.");
            }

            _playerMotor.Initialize(playerId, _inputSource, 1 << groundLayer, enemyLayers: 1 << enemiesLayer);
            visualPresenter.Initialize(_playerMotor);
            _hudPresenter = gameObject.AddComponent<PlayerHudPresenter>();
            _hudPresenter.Initialize(_playerMotor, _telemetryText);
            _pauseController = gameObject.AddComponent<ExpeditionPauseController>();
            _pauseController.Initialize(_pauseText);
            _inputSource.PausePressed += _pauseController.TogglePause;
            _cameraRig.Bind(_playerObject.transform.Find("CameraTarget") ?? _playerObject.transform, _roomBounds);
            _returnToMenuButton.onClick.AddListener(ReturnToMenu);

            _waveIndex = 0;
            SpawnWave(_encounter.waves[_waveIndex], groundLayer);
            RefreshEncounterText();
        }

        private void Update()
        {
            if (_playerMotor == null || _pauseController != null && _pauseController.IsPaused || _resultSet) return;

            if (_playerMotor.IsDead)
            {
                SetResult(CombatArenaResult.Defeat);
                return;
            }

            if (CountLivingEnemies() > 0) return;
            if (_waveIndex + 1 < _encounter.waves.Length)
            {
                _waveIndex++;
                SpawnWave(_encounter.waves[_waveIndex], LayerMask.NameToLayer("Ground"));
                RefreshEncounterText();
                return;
            }

            SetResult(CombatArenaResult.Victory);
        }

        private void OnDestroy()
        {
            if (_inputSource != null && _pauseController != null) _inputSource.PausePressed -= _pauseController.TogglePause;
            if (_returnToMenuButton != null) _returnToMenuButton.onClick.RemoveListener(ReturnToMenu);
            foreach (Huesoguardia2D enemy in _subscribedHuesoguardias)
                if (enemy != null) enemy.Defeated -= OnHuesoguardiaDefeated;
            _subscribedHuesoguardias.Clear();
            if (_inputSource != null)
            {
                _inputSource.Dispose();
                _inputSource = null;
            }

            Time.timeScale = 1f;
        }

        public void ReturnToMenu()
        {
            if (_pauseController != null) _pauseController.SetPaused(false);
            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }

        private void SpawnWave(CombatEncounterWaveData wave, int groundLayer)
        {
            _slimes.Clear();
            _huesoguardias.Clear();
            foreach (CombatEnemySpawnData spawn in wave.spawns)
            {
                bool isSlime = string.Equals(spawn.enemyId, "enemy.slime", StringComparison.Ordinal);
                GameObject enemyObject = Instantiate(
                    isSlime ? _slimePrefab : _huesoguardiaPrefab,
                    new Vector3(spawn.x, spawn.y + (isSlime ? 0.36f : 0.6f), 0f),
                    Quaternion.identity);
                enemyObject.layer = LayerMask.NameToLayer("Enemies");
                ActorId enemyId = ActorId.New();
                if (isSlime)
                {
                    Slime2D slime = enemyObject.GetComponent<Slime2D>();
                    if (slime == null) throw new InvalidOperationException("Slime prototype prefab is missing Slime2D.");
                    slime.Initialize(enemyId, _playerMotor, 1 << groundLayer);
                    _slimes.Add(slime);
                }
                else
                {
                    Huesoguardia2D huesoguardia = enemyObject.GetComponent<Huesoguardia2D>();
                    if (huesoguardia == null) throw new InvalidOperationException("Huesoguardia prototype prefab is missing Huesoguardia2D.");
                    huesoguardia.Initialize(enemyId, _playerMotor, 1 << groundLayer);
                    huesoguardia.Defeated += OnHuesoguardiaDefeated;
                    _subscribedHuesoguardias.Add(huesoguardia);
                    _huesoguardias.Add(huesoguardia);
                }
            }
        }

        private int CountLivingEnemies()
        {
            int count = 0;
            foreach (Slime2D slime in _slimes) if (slime != null && !slime.IsDead) count++;
            foreach (Huesoguardia2D huesoguardia in _huesoguardias) if (huesoguardia != null && !huesoguardia.IsDead) count++;
            return count;
        }

        private void SetResult(CombatArenaResult result)
        {
            if (_resultSet) return;
            Result = result;
            _resultSet = true;
            if (result == CombatArenaResult.Victory)
                ApplicationBootstrapper.Instance?.RecordContractProgressEventFromGameplay(
                    ContractProgressEvent.CombatRoomCompleted);
            RefreshEncounterText();
        }

        private void OnHuesoguardiaDefeated(ActorId actorId)
        {
            if (!actorId.IsValid || !_reportedHuesoguardiaDefeats.Add(actorId)) return;
            ApplicationBootstrapper.Instance?.RecordContractProgressEventFromGameplay(
                ContractProgressEvent.BoneguardDefeated);
        }

        private void RefreshEncounterText()
        {
            if (_encounterText == null) return;
            if (Result == CombatArenaResult.Victory)
            {
                _encounterText.text = "ARENA SUPERADA · DOS OLEADAS VENCIDAS";
                return;
            }

            if (Result == CombatArenaResult.Defeat)
            {
                _encounterText.text = "DERROTA · EL PERSONAJE HA CAÍDO";
                return;
            }

            _encounterText.text = "OLEADA " + CurrentWave + "/" + WaveCount + " · ENEMIGOS " + CountLivingEnemies() +
                "     J · ATACAR     SHIFT · ESQUIVAR     F · POCIÓN";
        }
    }
}
