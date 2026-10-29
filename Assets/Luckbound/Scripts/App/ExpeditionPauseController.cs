using System;
using System.Collections.Generic;
using System.Threading;
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
        private ApplicationAudioRouter _audioRouter;
        private PauseAudioSettingsPanel _audioSettingsPanel;
        private readonly HashSet<SimulationPauseLease> _simulationPauseLeases = new HashSet<SimulationPauseLease>();
        private bool _hasSimulationStateSnapshot;
        private bool _destroying;
        private int _unityMainThreadId;

        public bool IsPaused { get; private set; }
        public bool IsSimulationPaused => IsPaused || _simulationPauseLeases.Count > 0;

        public event Action<bool> PauseChanged;
        public event Action<bool> SimulationPauseChanged;

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
            _unityMainThreadId = Thread.CurrentThread.ManagedThreadId;
            _initialized = true;

            Canvas canvas = pauseLabel.GetComponentInParent<Canvas>();
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            _audioRouter = application == null ? null : application.AudioRouter;
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
            if (!_initialized) return;

            bool wasPaused = IsPaused;
            SetPaused(!wasPaused);
            if (IsPaused == wasPaused) return;
            _audioRouter?.PlayEffect(IsPaused
                ? ApplicationAudioRouter.UiOpenEffectId
                : ApplicationAudioRouter.UiCloseEffectId);
        }

        public void SetPaused(bool paused)
        {
            if (!_initialized || _destroying || paused == IsPaused)
            {
                return;
            }

            IsPaused = paused;
            RefreshSimulationPauseState();
            if (paused)
            {
                _pauseLabel.text = "PAUSA\nPulsa ESC o MENU para continuar";
                if (_audioSettingsPanel != null) _audioSettingsPanel.SetVisible(true);
            }
            else
            {
                _pauseLabel.text = string.Empty;
                if (_audioSettingsPanel != null) _audioSettingsPanel.SetVisible(false);
            }

            PauseChanged?.Invoke(IsPaused);
        }

        /// <summary>
        /// Stops scaled simulation without changing the player's manual pause state, pause UI, or pause audio.
        /// The returned lease must be disposed after the operation that owns the simulation lock completes.
        /// </summary>
        public IDisposable AcquireSimulationPauseLease()
        {
            if (!_initialized)
                throw new InvalidOperationException("Initialize the pause controller before acquiring a simulation pause lease.");
            if (_destroying)
                throw new ObjectDisposedException(nameof(ExpeditionPauseController));
            EnsureUnityMainThread();

            var lease = new SimulationPauseLease(this);
            _simulationPauseLeases.Add(lease);
            RefreshSimulationPauseState();
            return lease;
        }

        private void ReleaseSimulationPauseLease(SimulationPauseLease lease)
        {
            EnsureUnityMainThread();
            if (_destroying || !_simulationPauseLeases.Remove(lease)) return;
            RefreshSimulationPauseState();
        }

        private void EnsureUnityMainThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != _unityMainThreadId)
                throw new InvalidOperationException("Simulation pause leases must be acquired and disposed on Unity's main thread.");
        }

        private void RefreshSimulationPauseState()
        {
            bool shouldPauseSimulation = IsPaused || _simulationPauseLeases.Count > 0;
            if (shouldPauseSimulation == _hasSimulationStateSnapshot) return;

            if (shouldPauseSimulation)
            {
                _resumeTimeScale = Time.timeScale;
                _resumePhysicsSimulationMode = Physics2D.simulationMode;
                Time.timeScale = 0f;
                Physics2D.simulationMode = SimulationMode2D.Script;
                _hasSimulationStateSnapshot = true;
                SimulationPauseChanged?.Invoke(true);
                return;
            }

            RestoreSimulationState();
            SimulationPauseChanged?.Invoke(false);
        }

        private void RestoreSimulationState()
        {
            if (!_hasSimulationStateSnapshot) return;
            Physics2D.simulationMode = _resumePhysicsSimulationMode;
            Time.timeScale = _resumeTimeScale;
            _hasSimulationStateSnapshot = false;
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
            _destroying = true;
            foreach (SimulationPauseLease lease in _simulationPauseLeases)
                lease.InvalidateFromOwner();
            _simulationPauseLeases.Clear();

            if (_audioSettingsPanel != null)
            {
                Destroy(_audioSettingsPanel.gameObject);
                _audioSettingsPanel = null;
            }

            RestoreSimulationState();
            IsPaused = false;
        }

        private sealed class SimulationPauseLease : IDisposable
        {
            private ExpeditionPauseController _owner;

            public SimulationPauseLease(ExpeditionPauseController owner)
            {
                _owner = owner;
            }

            public void Dispose()
            {
                ExpeditionPauseController owner = Volatile.Read(ref _owner);
                if (owner == null) return;
                owner.ReleaseSimulationPauseLease(this);
                Interlocked.CompareExchange(ref _owner, null, owner);
            }

            public void InvalidateFromOwner()
            {
                Interlocked.Exchange(ref _owner, null);
            }
        }
    }
}
