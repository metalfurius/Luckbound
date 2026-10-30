using System;
using System.Collections.Generic;
using Luckbound.Content;
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
    public enum CombatArenaResult
    {
        Ongoing = 0,
        Victory = 1,
        Defeat = 2
    }

    /// <summary>Composes the player, two-wave C08 encounter, camera and expedition HUD.</summary>
    public sealed class CombatArenaComposition : MonoBehaviour
    {
#if UNITY_EDITOR
        internal readonly struct AudioTraceSnapshot
        {
            internal AudioTraceSnapshot(string phase, long tick, bool grounded, bool baseline,
                bool reportedGrounded, int jumps, int reportedJumps, bool landingCall, string details)
            {
                Phase = phase; Tick = tick; Grounded = grounded; Baseline = baseline;
                ReportedGrounded = reportedGrounded; Jumps = jumps; ReportedJumps = reportedJumps;
                LandingCall = landingCall; Details = details;
            }
            internal string Phase { get; }
            internal long Tick { get; }
            internal bool Grounded { get; }
            internal bool Baseline { get; }
            internal bool ReportedGrounded { get; }
            internal int Jumps { get; }
            internal int ReportedJumps { get; }
            internal bool LandingCall { get; }
            internal string Details { get; }
        }
        internal static Action<AudioTraceSnapshot> EditorAudioTraceForTests { get; set; }

        private void EmitAudioTrace(string phase, bool baselineBefore, bool groundedBefore,
            int reportedJumpsBefore, bool landingCall = false, bool? landingResult = null,
            int pendingBeforeLanding = -1)
        {
            Action<AudioTraceSnapshot> observer = EditorAudioTraceForTests;
            if (observer == null || _playerMotor == null) return;
            bool routerPresent = _audioRouter != null;
            string details = "instance=" + GetEntityId() + "; frame=" + Time.frameCount +
                "; fixedTime=" + Time.fixedTime.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                "; tick=" + _playerMotor.SimulationTick + "; jumps=" + _playerMotor.SuccessfulJumpCount +
                "; reportedJumpsBefore=" + reportedJumpsBefore + "; reportedJumpsAfter=" + _reportedSuccessfulJumps +
                "; grounded=" + _playerMotor.IsGrounded + "; baselineBefore=" + baselineBefore +
                "; baselineAfter=" + _hasGroundedAudioBaseline + "; reportedGroundBefore=" + groundedBefore +
                "; reportedGroundAfter=" + _reportedPlayerGrounded + "; landingCall=" + landingCall +
                "; landingResult=" + (landingResult.HasValue ? landingResult.Value.ToString() : "NotCalledOrRouterMissing") +
                "; routerPresent=" + routerPresent + "; routerActive=" + (routerPresent && _audioRouter.isActiveAndEnabled) +
                "; routerId=" + (routerPresent ? _audioRouter.GetEntityId().ToString() : "Missing") +
                "; gesture=" + (routerPresent && _audioRouter.UserGestureObserved) +
                "; pendingBeforeLanding=" + pendingBeforeLanding +
                "; pendingNow=" + (routerPresent ? _audioRouter.PendingEffectRequestCount : -1);
            observer(new AudioTraceSnapshot(phase, _playerMotor.SimulationTick, _playerMotor.IsGrounded,
                _hasGroundedAudioBaseline, _reportedPlayerGrounded, _playerMotor.SuccessfulJumpCount,
                _reportedSuccessfulJumps, landingCall, details));
        }
#endif

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
        private readonly HashSet<ActorId> _reportedEnemyDeaths = new HashSet<ActorId>();
        private readonly Dictionary<ActorId, int> _reportedEnemyDamageImpacts = new Dictionary<ActorId, int>();
        private CombatEncounterData _encounter;
        private GameObject _playerObject;
        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private ApplicationAudioRouter _audioRouter;
        private PlayerHudPresenter _hudPresenter;
        private ExpeditionPauseController _pauseController;
        private int _waveIndex;
        private int _reportedMeleeAttackActivations;
        private int _reportedMeleeImpacts;
        private int _reportedSuccessfulJumps;
        private int _reportedDodgeActivations;
        private int _reportedSuccessfulPlayerHits;
        private int _reportedHealthPotionUses;
        private bool _hasGroundedAudioBaseline;
        private bool _reportedPlayerGrounded;
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
            _audioRouter = ApplicationBootstrapper.Instance != null
                ? ApplicationBootstrapper.Instance.AudioRouter
                : null;

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
            // These counters are cumulative for the motor lifetime. Treat anything present
            // immediately after initialization as a baseline, not as a new gameplay event.
            _reportedMeleeAttackActivations = _playerMotor.MeleeAttackActivationCount;
            _reportedMeleeImpacts = _playerMotor.MeleeImpactsQueuedCount;
            _reportedSuccessfulJumps = _playerMotor.SuccessfulJumpCount;
            _reportedDodgeActivations = _playerMotor.DodgeActivationCount;
            _reportedSuccessfulPlayerHits = CountSuccessfulPlayerHits();
            _reportedHealthPotionUses = _playerMotor.SuccessfulHealthPotionUseCount;
#if UNITY_EDITOR
            EmitAudioTrace("initialized", _hasGroundedAudioBaseline, _reportedPlayerGrounded, _reportedSuccessfulJumps);
#endif
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
            UpdateCombatAudioEvents();
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
            _reportedEnemyDamageImpacts.Clear();
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
                    SetEnemyDamageAudioBaseline(slime.ActorId, CountEnemyImpacts(slime.MeleeImpactCount, slime.ProjectileImpactCount));
                }
                else
                {
                    Huesoguardia2D huesoguardia = enemyObject.GetComponent<Huesoguardia2D>();
                    if (huesoguardia == null) throw new InvalidOperationException("Huesoguardia prototype prefab is missing Huesoguardia2D.");
                    huesoguardia.Initialize(enemyId, _playerMotor, 1 << groundLayer);
                    huesoguardia.Defeated += OnHuesoguardiaDefeated;
                    _subscribedHuesoguardias.Add(huesoguardia);
                    _huesoguardias.Add(huesoguardia);
                    SetEnemyDamageAudioBaseline(huesoguardia.ActorId,
                        CountEnemyImpacts(huesoguardia.MeleeImpactCount, huesoguardia.ProjectileImpactCount));
                }
            }

            ResetSuccessfulPlayerHitAudioBaselineForWave();
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
            RefreshEncounterText();
        }

        private void OnHuesoguardiaDefeated(ActorId actorId)
        {
            if (!actorId.IsValid || !_reportedEnemyDeaths.Add(actorId)) return;
            _audioRouter?.PlayEffect(ApplicationAudioRouter.EnemyDeathEffectId);
        }

        private void UpdateCombatAudioEvents()
        {
            if (_playerMotor == null) return;

#if UNITY_EDITOR
            bool traceBaselineBefore = _hasGroundedAudioBaseline;
            bool traceGroundedBefore = _reportedPlayerGrounded;
            int traceReportedJumpsBefore = _reportedSuccessfulJumps;
            bool traceLandingCall = false;
            bool? traceLandingResult = null;
            int tracePendingBeforeLanding = -1;
            EmitAudioTrace("audio-before", traceBaselineBefore, traceGroundedBefore, traceReportedJumpsBefore);
#endif
            int newSuccessfulJumps = ConsumeCounterDelta(
                ref _reportedSuccessfulJumps,
                _playerMotor.SuccessfulJumpCount);
            for (int index = 0; index < newSuccessfulJumps; index++)
            {
                _audioRouter?.PlayEffect(ApplicationAudioRouter.JumpEffectId);
            }

            int newDodgeActivations = ConsumeCounterDelta(
                ref _reportedDodgeActivations,
                _playerMotor.DodgeActivationCount);
            for (int index = 0; index < newDodgeActivations; index++)
            {
                _audioRouter?.PlayEffect(ApplicationAudioRouter.DodgeEffectId);
            }

            int newAttackActivations = ConsumeCounterDelta(
                ref _reportedMeleeAttackActivations,
                _playerMotor.MeleeAttackActivationCount);
            for (int index = 0; index < newAttackActivations; index++)
            {
                _audioRouter?.PlayEffect(ApplicationAudioRouter.SwordSwingEffectId);
            }

            int newMeleeImpacts = ConsumeCounterDelta(
                ref _reportedMeleeImpacts,
                _playerMotor.MeleeImpactsQueuedCount);
            for (int index = 0; index < newMeleeImpacts; index++)
            {
                _audioRouter?.PlayEffect(ApplicationAudioRouter.SwordImpactEffectId);
            }

            int newPlayerHits = ConsumeCounterDelta(
                ref _reportedSuccessfulPlayerHits,
                CountSuccessfulPlayerHits());
            for (int index = 0; index < newPlayerHits; index++)
            {
                _audioRouter?.PlayEffect(ApplicationAudioRouter.PlayerDamageEffectId);
            }

            int newHealthPotionUses = ConsumeCounterDelta(
                ref _reportedHealthPotionUses,
                _playerMotor.SuccessfulHealthPotionUseCount);
            for (int index = 0; index < newHealthPotionUses; index++)
            {
                _audioRouter?.PlayEffect(ApplicationAudioRouter.PotionEffectId);
            }

            bool playerIsGrounded = _playerMotor.IsGrounded;
            if (!_hasGroundedAudioBaseline)
            {
                if (playerIsGrounded)
                {
                    _reportedPlayerGrounded = true;
                    _hasGroundedAudioBaseline = true;
                }
            }
            else
            {
                if (!_reportedPlayerGrounded && playerIsGrounded)
                {
#if UNITY_EDITOR
                    traceLandingCall = true;
                    tracePendingBeforeLanding = _audioRouter != null ? _audioRouter.PendingEffectRequestCount : -1;
                    traceLandingResult = _audioRouter?.PlayEffect(ApplicationAudioRouter.LandingEffectId);
#else
                    _audioRouter?.PlayEffect(ApplicationAudioRouter.LandingEffectId);
#endif
                }
                _reportedPlayerGrounded = playerIsGrounded;
            }

#if UNITY_EDITOR
            EmitAudioTrace("ground-decision-after", traceBaselineBefore, traceGroundedBefore, traceReportedJumpsBefore,
                traceLandingCall, traceLandingResult, tracePendingBeforeLanding);
#endif
            foreach (Slime2D slime in _slimes)
            {
                if (slime == null) continue;
                UpdateEnemyDamageAudio(slime.ActorId, CountEnemyImpacts(slime.MeleeImpactCount, slime.ProjectileImpactCount));
                if (!slime.IsDead || !slime.ActorId.IsValid ||
                    !_reportedEnemyDeaths.Add(slime.ActorId))
                {
                    continue;
                }

                _audioRouter?.PlayEffect(ApplicationAudioRouter.EnemyDeathEffectId);
            }

            foreach (Huesoguardia2D huesoguardia in _huesoguardias)
            {
                if (huesoguardia == null) continue;
                UpdateEnemyDamageAudio(huesoguardia.ActorId,
                    CountEnemyImpacts(huesoguardia.MeleeImpactCount, huesoguardia.ProjectileImpactCount));
            }
#if UNITY_EDITOR
            EmitAudioTrace("audio-after", traceBaselineBefore, traceGroundedBefore, traceReportedJumpsBefore,
                traceLandingCall, traceLandingResult, tracePendingBeforeLanding);
#endif
        }

        private void SetEnemyDamageAudioBaseline(ActorId actorId, int impactCount)
        {
            if (actorId.IsValid) _reportedEnemyDamageImpacts[actorId] = Math.Max(0, impactCount);
        }

        private void UpdateEnemyDamageAudio(ActorId actorId, int impactCount)
        {
            if (!actorId.IsValid) return;
            if (!_reportedEnemyDamageImpacts.TryGetValue(actorId, out int reportedCount))
            {
                SetEnemyDamageAudioBaseline(actorId, impactCount);
                return;
            }

            int newImpacts = ConsumeCounterDelta(ref reportedCount, impactCount);
            _reportedEnemyDamageImpacts[actorId] = reportedCount;
            for (int index = 0; index < newImpacts; index++)
                _audioRouter?.PlayEffect(ApplicationAudioRouter.EnemyDamageEffectId);
        }

        private static int CountEnemyImpacts(int meleeImpacts, int projectileImpacts)
        {
            return Math.Max(0, meleeImpacts) + Math.Max(0, projectileImpacts);
        }

        private static int ConsumeCounterDelta(ref int reportedCount, int currentCount)
        {
            if (currentCount < 0) return 0;
            if (currentCount < reportedCount)
            {
                // A replacement/reset counter starts a new baseline; its old value must not replay.
                reportedCount = currentCount;
                return 0;
            }

            int newEvents = currentCount - reportedCount;
            reportedCount = currentCount;
            return newEvents;
        }

        private int CountSuccessfulPlayerHits()
        {
            int count = 0;
            foreach (Slime2D slime in _slimes)
                if (slime != null) count += Math.Max(0, slime.SuccessfulPlayerHitCount);
            foreach (Huesoguardia2D huesoguardia in _huesoguardias)
                if (huesoguardia != null) count += Math.Max(0, huesoguardia.SuccessfulPlayerHitCount);
            return count;
        }

        private void ResetSuccessfulPlayerHitAudioBaselineForWave()
        {
            _reportedSuccessfulPlayerHits = CountSuccessfulPlayerHits();
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
