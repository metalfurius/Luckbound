using System;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Input;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    public enum CustodianArenaResult
    {
        Ongoing = 0,
        Victory = 1,
        Defeat = 2
    }

    /// <summary>Runs the D06 Custodian encounter with the shared player input, HUD, pause and camera systems.</summary>
    public sealed class CustodianArenaComposition : MonoBehaviour
    {
        private const string BossResourcePath = "Luckbound/Data/Encounters/CustodianBoss";

        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private GameObject _custodianPrefab;
        [SerializeField] private GameObject _wavePrefab;
        [SerializeField] private Transform _playerSpawn;
        [SerializeField] private Transform _waveRoot;
        [SerializeField] private Collider2D _roomBounds;
        [SerializeField] private RoomCameraRig2D _cameraRig;
        [SerializeField] private Text _telemetryText;
        [SerializeField] private Text _bossStatusText;
        [SerializeField] private Text _pauseText;
        [SerializeField] private Button _returnToMenuButton;

        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private CustodianBoss2D _boss;
        private PlayerHudPresenter _hudPresenter;
        private ExpeditionPauseController _pauseController;
        private bool _resultSet;

        public PlayerMotor2D PlayerMotor => _playerMotor;
        public CustodianBoss2D Boss => _boss;
        public CustodianArenaResult Result { get; private set; }

        private void Start()
        {
            if (_playerPrefab == null || _custodianPrefab == null || _wavePrefab == null || _playerSpawn == null ||
                _waveRoot == null || _roomBounds == null || _cameraRig == null || _telemetryText == null ||
                _bossStatusText == null || _pauseText == null || _returnToMenuButton == null)
                throw new InvalidOperationException("CustodianArenaComposition is missing a required scene reference.");

            TextAsset bossJson = Resources.Load<TextAsset>(BossResourcePath);
            if (bossJson == null) throw new InvalidOperationException("Missing boss data at Resources/" + BossResourcePath + ".json.");
            CustodianBossDefinition definition = CustodianBossJsonLoader.Load(bossJson.text);

            int enemiesLayer = LayerMask.NameToLayer("Enemies");
            if (enemiesLayer < 0) throw new InvalidOperationException("The Custodian arena requires the Enemies layer.");

            ActorId playerId = ActorId.New();
            _inputSource = PlayerInputSource.CreateDefault(playerId);
            GameObject playerObject = Instantiate(_playerPrefab, _playerSpawn.position, _playerSpawn.rotation);
            playerObject.name = "Player";
            _playerMotor = playerObject.GetComponent<PlayerMotor2D>();
            PlayerVisualStatePresenter visualPresenter = playerObject.GetComponent<PlayerVisualStatePresenter>();
            if (_playerMotor == null || visualPresenter == null)
                throw new InvalidOperationException("The arena player prefab must include its motor and visual presenter.");
            _playerMotor.Initialize(playerId, _inputSource, LayerMask.GetMask("Ground"), enemyLayers: 1 << enemiesLayer);
            visualPresenter.Initialize(_playerMotor);

            GameObject bossObject = Instantiate(_custodianPrefab, _playerSpawn.position + new Vector3(10f, 0.9f, 0f), Quaternion.identity);
            bossObject.name = "CustodianOfTheOaks";
            bossObject.layer = enemiesLayer;
            _boss = bossObject.GetComponent<CustodianBoss2D>();
            if (_boss == null) throw new InvalidOperationException("Custodian prototype prefab is missing CustodianBoss2D.");
            _boss.Initialize(ActorId.New(), _playerMotor, definition, _wavePrefab, _waveRoot);

            _hudPresenter = gameObject.AddComponent<PlayerHudPresenter>();
            _hudPresenter.Initialize(_playerMotor, _telemetryText);
            _pauseController = gameObject.AddComponent<ExpeditionPauseController>();
            _pauseController.Initialize(_pauseText);
            _inputSource.PausePressed += _pauseController.TogglePause;
            _cameraRig.Bind(playerObject.transform.Find("CameraTarget") ?? playerObject.transform, _roomBounds);
            _returnToMenuButton.onClick.AddListener(ReturnToMenu);
            RefreshBossStatus();
        }

        private void Update()
        {
            if (_playerMotor == null || _boss == null || _pauseController != null && _pauseController.IsPaused || _resultSet) return;
            if (_playerMotor.IsDead)
            {
                SetResult(CustodianArenaResult.Defeat);
                return;
            }
            if (_boss.IsDead)
            {
                SetResult(CustodianArenaResult.Victory);
                return;
            }
            RefreshBossStatus();
        }

        private void OnDestroy()
        {
            if (_inputSource != null && _pauseController != null) _inputSource.PausePressed -= _pauseController.TogglePause;
            if (_returnToMenuButton != null) _returnToMenuButton.onClick.RemoveListener(ReturnToMenu);
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

        private void SetResult(CustodianArenaResult result)
        {
            Result = result;
            _resultSet = true;
            if (result == CustodianArenaResult.Victory)
                _bossStatusText.text = "EL CUSTODIO CAE · COMBATE COMPLETADO";
            else
                _bossStatusText.text = "DERROTA · EL PERSONAJE HA CAÍDO";
        }

        private void RefreshBossStatus()
        {
            if (_bossStatusText == null || _boss == null) return;
            string phase = _boss.Phase == CustodianPhase.PhaseTwo ? "II" : "I";
            string state;
            switch (_boss.State)
            {
                case CustodianBehaviorState.Telegraphing:
                    state = "ALERTA " + PatternLabel(_boss.CurrentPattern) + " · " +
                        (_boss.TelegraphTicksRemaining / 60f).ToString("F2") + " s";
                    break;
                case CustodianBehaviorState.Executing:
                    state = "ATAQUE " + PatternLabel(_boss.CurrentPattern);
                    break;
                case CustodianBehaviorState.Recovery:
                    state = "RECUPERACIÓN · " + _boss.RecoveryTicksRemaining + " TICKS";
                    break;
                case CustodianBehaviorState.PhaseTransition:
                    state = "TRANSICIÓN · " + (_boss.PhaseTransitionTicksRemaining / 60f).ToString("F2") + " s";
                    phase = "CAMBIO";
                    break;
                default:
                    state = "OBSERVA EL TERRENO";
                    break;
            }
            _bossStatusText.text = "CUSTODIO DE LAS ENCINAS · " + _boss.CurrentHealth + "/" + _boss.MaximumHealth +
                " · FASE " + phase + "\n" + state;
        }

        private static string PatternLabel(CustodianAttackPattern pattern)
        {
            switch (pattern)
            {
                case CustodianAttackPattern.Cut: return "CORTE";
                case CustodianAttackPattern.Wave: return "ONDA";
                case CustodianAttackPattern.Charge: return "CARGA";
                default: return pattern.ToString().ToUpperInvariant();
            }
        }
    }
}
