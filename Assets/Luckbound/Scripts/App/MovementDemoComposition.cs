using System;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Input;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Composes the movement-demo player, input, HUD and room camera for the Expedition scene.</summary>
    public sealed class MovementDemoComposition : MonoBehaviour
    {
        [SerializeField] private GameObject _playerPrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Collider2D _roomBounds;
        [SerializeField] private RoomCameraRig2D _cameraRig;
        [SerializeField] private Text _telemetryText;
        [SerializeField] private Text _courseText;
        [SerializeField] private Button _returnToMenuButton;
        [SerializeField] private GameObject _demoCourseRoot;
        [SerializeField] private GameObject _movementDemoHudRoot;

        private GameObject _playerObject;
        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private PlayerHudPresenter _hudPresenter;
        private ExpeditionPauseController _pauseController;
        private bool _insideGoal;
        private bool _completed;

        public PlayerMotor2D PlayerMotor => _playerMotor;

        public GameObject PlayerObject => _playerObject;

        public bool IsInsideGoal => _insideGoal;

        public bool IsCourseComplete => _completed;

        public bool IsPaused => _pauseController != null && _pauseController.IsPaused;

        private void Start()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer &&
                Uri.TryCreate(Application.absoluteURL, UriKind.Absolute, out Uri probeUrl) &&
                Array.IndexOf(probeUrl.Query.TrimStart('?').Split('&'), "luckboundRenderProbe=1") >= 0)
                StartCoroutine(CaptureRenderProbe());

            if (ApplicationBootstrapper.Instance != null && ApplicationBootstrapper.Instance.ActiveRun != null)
            {
                if (_demoCourseRoot == null || _movementDemoHudRoot == null)
                    throw new InvalidOperationException("A launched expedition must disable its serialized movement-demo course and HUD.");
                _demoCourseRoot.SetActive(false);
                _movementDemoHudRoot.SetActive(false);
                enabled = false;
                return;
            }

            if (_playerPrefab == null || _spawnPoint == null || _roomBounds == null || _cameraRig == null ||
                _telemetryText == null || _courseText == null || _returnToMenuButton == null)
            {
                throw new InvalidOperationException("MovementDemoComposition is missing a required scene reference.");
            }

            ActorId actorId = ActorId.New();
            _inputSource = PlayerInputSource.CreateDefault(actorId);
            _playerObject = Instantiate(_playerPrefab, _spawnPoint.position, _spawnPoint.rotation);
            _playerObject.name = "Player";

            _playerMotor = _playerObject.GetComponent<PlayerMotor2D>();
            PlayerVisualStatePresenter visualPresenter = _playerObject.GetComponent<PlayerVisualStatePresenter>();
            if (_playerMotor == null || visualPresenter == null)
            {
                throw new InvalidOperationException("The movement-demo player prefab must include its motor and visual presenter.");
            }

            _playerMotor.Initialize(actorId, _inputSource, LayerMask.GetMask("Ground"));
            visualPresenter.Initialize(_playerMotor);

            _hudPresenter = gameObject.AddComponent<PlayerHudPresenter>();
            _hudPresenter.Initialize(_playerMotor, _telemetryText);
            _pauseController = gameObject.AddComponent<ExpeditionPauseController>();
            _pauseController.Initialize(_courseText);
            _inputSource.PausePressed += _pauseController.TogglePause;

            Transform cameraTarget = _playerObject.transform.Find("CameraTarget");
            _cameraRig.Bind(cameraTarget != null ? cameraTarget : _playerObject.transform, _roomBounds);
            _returnToMenuButton.onClick.AddListener(ReturnToMenu);
            RefreshHud();
        }

        private void Update()
        {
            if (_playerMotor != null && !IsPaused)
            {
                RefreshHud();
            }
        }

        // Read-only diagnostic, enabled solely by an explicit WebGL capture query.
        private System.Collections.IEnumerator CaptureRenderProbe()
        {
            WriteRenderProbe("start");
            yield return null;
            yield return null;
            WriteRenderProbe("after-two-frames");
        }

        private void WriteRenderProbe(string stage)
        {
            Camera camera = _cameraRig != null ? _cameraRig.GetComponent<Camera>() : Camera.main;
            var output = new System.Text.StringBuilder("LUCKBOUND_RENDER_PROBE stage=").Append(stage)
                .Append(" activeRun=").Append(ApplicationBootstrapper.Instance != null && ApplicationBootstrapper.Instance.ActiveRun != null)
                .Append(" courseSelf=").Append(_demoCourseRoot != null && _demoCourseRoot.activeSelf)
                .Append(" courseHierarchy=").Append(_demoCourseRoot != null && _demoCourseRoot.activeInHierarchy);
            if (camera != null)
                output.Append(" camera=").Append(camera.transform.position.ToString("F3"))
                    .Append(" ortho=").Append(camera.orthographicSize)
                    .Append(" near=").Append(camera.nearClipPlane).Append(" far=").Append(camera.farClipPlane)
                    .Append(" mask=").Append(camera.cullingMask);
            if (_demoCourseRoot != null)
                foreach (SpriteRenderer renderer in _demoCourseRoot.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    output.Append(" | renderer=").Append(renderer.name)
                        .Append(" active=").Append(renderer.gameObject.activeInHierarchy)
                        .Append(" enabled=").Append(renderer.enabled).Append(" visible=").Append(renderer.isVisible)
                        .Append(" sprite=").Append(renderer.sprite != null ? renderer.sprite.name : "null")
                        .Append(" material=").Append(renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "null")
                        .Append(" layer=").Append(renderer.gameObject.layer)
                        .Append(" sorting=").Append(renderer.sortingLayerID).Append('/').Append(renderer.sortingOrder)
                        .Append(" bounds=").Append(renderer.bounds.ToString());
                    if (camera != null)
                        output.Append(" viewportCenter=").Append(camera.WorldToViewportPoint(renderer.bounds.center).ToString("F3"));
                }
            Debug.Log(output.ToString(), this);
        }

        private void OnDestroy()
        {
            if (_inputSource != null && _pauseController != null)
            {
                _inputSource.PausePressed -= _pauseController.TogglePause;
            }

            if (_returnToMenuButton != null)
            {
                _returnToMenuButton.onClick.RemoveListener(ReturnToMenu);
            }

            if (_inputSource != null)
            {
                _inputSource.Dispose();
                _inputSource = null;
            }

            Time.timeScale = 1f;
        }

        public void EnterGoal(Collider2D other)
        {
            if (_playerMotor != null && other != null && other.GetComponentInParent<PlayerMotor2D>() == _playerMotor)
            {
                _insideGoal = true;
                RefreshHud();
            }
        }

        public void ExitGoal(Collider2D other)
        {
            if (_playerMotor != null && other != null && other.GetComponentInParent<PlayerMotor2D>() == _playerMotor)
            {
                _insideGoal = false;
                RefreshHud();
            }
        }

        public void ReturnToMenu()
        {
            if (_pauseController != null)
            {
                _pauseController.SetPaused(false);
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }

        private void RefreshHud()
        {
            if (_playerMotor == null) return;

            if (_completed)
            {
                _courseText.text = "RECORRIDO COMPLETADO · movimiento, salto y esquiva comprobados";
                return;
            }

            if (_insideGoal)
            {
                bool needsJumps = _playerMotor.SuccessfulJumpCount < 3;
                bool needsDodge = _playerMotor.DodgeActivationCount < 1;
                _courseText.text = needsJumps || needsDodge
                    ? "ANTES DEL PORTAL: " + (needsJumps ? "salta las tres barreras" : "") +
                      (needsJumps && needsDodge ? " y " : "") + (needsDodge ? "prueba la esquiva" : "")
                    : "PORTAL ABIERTO";

                if (!needsJumps && !needsDodge)
                {
                    _completed = true;
                    _courseText.text = "RECORRIDO COMPLETADO · movimiento, salto y esquiva comprobados";
                }
                return;
            }

            _courseText.text = "Cruza las tres barreras, prueba la esquiva y llega al portal.";
        }
    }
}
