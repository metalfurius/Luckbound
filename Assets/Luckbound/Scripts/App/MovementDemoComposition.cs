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

        private GameObject _playerObject;
        private PlayerInputSource _inputSource;
        private PlayerMotor2D _playerMotor;
        private bool _insideGoal;
        private bool _completed;

        public PlayerMotor2D PlayerMotor => _playerMotor;

        public GameObject PlayerObject => _playerObject;

        public bool IsInsideGoal => _insideGoal;

        public bool IsCourseComplete => _completed;

        private void Start()
        {
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

            Transform cameraTarget = _playerObject.transform.Find("CameraTarget");
            _cameraRig.Bind(cameraTarget != null ? cameraTarget : _playerObject.transform, _roomBounds);
            _returnToMenuButton.onClick.AddListener(ReturnToMenu);
            RefreshHud();
        }

        private void Update()
        {
            if (_playerMotor != null)
            {
                RefreshHud();
            }
        }

        private void OnDestroy()
        {
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
            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu", LoadSceneMode.Single);
        }

        private void RefreshHud()
        {
            if (_playerMotor == null) return;

            _telemetryText.text =
                "A/D o ←/→  MOVER     ESPACIO  SALTAR     MAYÚS  ESQUIVAR\n" +
                "Mando: stick izquierdo · A · RB\n" +
                "VELOCIDAD " + Mathf.Abs(_playerMotor.Velocity.x).ToString("0.0") +
                " u/s     SALTOS " + _playerMotor.SuccessfulJumpCount + "/3     ESQUIVAS " + _playerMotor.DodgeActivationCount + "/1";

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
