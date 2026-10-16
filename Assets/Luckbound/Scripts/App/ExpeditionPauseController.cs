using System;
using UnityEngine;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Pauses scaled simulation while keeping input and the UI responsive.</summary>
    public sealed class ExpeditionPauseController : MonoBehaviour
    {
        private Text _pauseLabel;
        private float _resumeTimeScale = 1f;
        private SimulationMode2D _resumePhysicsSimulationMode = SimulationMode2D.FixedUpdate;
        private bool _initialized;
        private bool _pauseOnFocusLoss;
        private PauseAudioSettingsPanel _audioSettingsPanel;

        public bool IsPaused { get; private set; }

        public event Action<bool> PauseChanged;

        public void Initialize(Text pauseLabel, bool pauseOnFocusLoss = true)
        {
            if (_initialized)
            {
                throw new InvalidOperationException("ExpeditionPauseController can only be initialized once.");
            }

            if (pauseLabel == null)
            {
                throw new ArgumentNullException(nameof(pauseLabel));
            }

            _pauseLabel = pauseLabel;
            _pauseOnFocusLoss = pauseOnFocusLoss;
            _initialized = true;

            Canvas canvas = pauseLabel.GetComponentInParent<Canvas>();
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            if (canvas != null && canvas.isActiveAndEnabled && application != null && application.SaveDataReady)
            {
                GameObject panelObject = new GameObject("PauseAudioSettingsPanel", typeof(RectTransform));
                panelObject.transform.SetParent(canvas.transform, false);
                _audioSettingsPanel = panelObject.AddComponent<PauseAudioSettingsPanel>();
                _audioSettingsPanel.Initialize(application);
                _audioSettingsPanel.SetVisible(false);
            }
        }

        public void TogglePause()
        {
            SetPaused(!IsPaused);
        }

        public void SetPaused(bool paused)
        {
            if (!_initialized || paused == IsPaused)
            {
                return;
            }

            IsPaused = paused;
            if (paused)
            {
                _resumeTimeScale = Time.timeScale;
                _resumePhysicsSimulationMode = Physics2D.simulationMode;
                Time.timeScale = 0f;
                Physics2D.simulationMode = SimulationMode2D.Script;
                _pauseLabel.text = "PAUSA\nPulsa ESC o MENU para continuar";
                if (_audioSettingsPanel != null) _audioSettingsPanel.SetVisible(true);
            }
            else
            {
                Physics2D.simulationMode = _resumePhysicsSimulationMode;
                Time.timeScale = _resumeTimeScale > 0f ? _resumeTimeScale : 1f;
                _pauseLabel.text = string.Empty;
                if (_audioSettingsPanel != null) _audioSettingsPanel.SetVisible(false);
            }

            PauseChanged?.Invoke(IsPaused);
        }

        public void HandleApplicationFocus(bool hasFocus)
        {
            if (_initialized && _pauseOnFocusLoss && !hasFocus)
            {
                SetPaused(true);
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            HandleApplicationFocus(hasFocus);
        }

        private void OnDestroy()
        {
            if (_audioSettingsPanel != null)
            {
                Destroy(_audioSettingsPanel.gameObject);
                _audioSettingsPanel = null;
            }

            if (IsPaused)
            {
                Physics2D.simulationMode = _resumePhysicsSimulationMode;
                Time.timeScale = _resumeTimeScale > 0f ? _resumeTimeScale : 1f;
                IsPaused = false;
            }
        }
    }
}
