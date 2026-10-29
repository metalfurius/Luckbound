#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Core.Saves;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Luckbound.Tests.PlayMode
{
    public sealed class PauseAudioSettingsTests
    {
        private FailFirstSettingsStore _store;
        private GameObject _canvasObject;
        private GameObject _pauseObject;
        private float _previousTimeScale;
        private SimulationMode2D _previousPhysicsMode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousTimeScale = Time.timeScale;
            _previousPhysicsMode = Physics2D.simulationMode;
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;

            _store = new FailFirstSettingsStore();
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => _store;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 &&
                 (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady ||
                  SceneManager.GetActiveScene().name == "Bootstrap"); frame++)
                yield return null;

            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"),
                "The test fixture must wait until the Bootstrap handoff has completed.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = null;
            if (_canvasObject != null) UnityEngine.Object.Destroy(_canvasObject);
            if (_pauseObject != null) UnityEngine.Object.Destroy(_pauseObject);
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;
            Time.timeScale = _previousTimeScale;
            Physics2D.simulationMode = _previousPhysicsMode;
        }

        [UnityTest]
        public IEnumerator PauseMixerPreviewsDefaultsAndRetriesThePendingDurableWrite()
        {
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            GameSettingsSaveData defaults = application.Settings;
            Assert.That(defaults.masterVolume, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(defaults.musicVolume, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(defaults.effectsVolume, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(defaults.ambienceVolume, Is.EqualTo(0.45f).Within(0.0001f));

            GameSettingsSaveData persisted = defaults.Clone();
            persisted.masterVolume = 0.72f;
            persisted.musicVolume = 0.34f;
            persisted.effectsVolume = 0.56f;
            persisted.ambienceVolume = 0.23f;
            Task<SaveWriteResult> seedWrite = application.SaveSettingsAsync(persisted);
            while (!seedWrite.IsCompleted) yield return null;
            Assert.That(seedWrite.Result.Succeeded, Is.True);
            UnityEngine.Object.Destroy(application.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 &&
                 (ApplicationBootstrapper.Instance == null || !ApplicationBootstrapper.Instance.SaveDataReady ||
                  SceneManager.GetActiveScene().name == "Bootstrap"); frame++)
                yield return null;
            application = ApplicationBootstrapper.Instance;
            Assert.That(application, Is.Not.Null);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"),
                "The panel canvas must be created after the Bootstrap scene has handed off to Menu.");
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.34f).Within(0.0001f),
                "The application should restore the persisted settings before opening pause.");
            _store.FailNextSettingsWrite = true;

            _canvasObject = new GameObject("PauseAudioSettingsTestCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject labelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(canvas.transform, false);
            Text pauseLabel = labelObject.GetComponent<Text>();
            pauseLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _pauseObject = new GameObject("PauseController");
            ExpeditionPauseController pause = _pauseObject.AddComponent<ExpeditionPauseController>();
            pause.Initialize(pauseLabel);
            pause.SetPaused(true);

            PauseAudioSettingsPanel panel = canvas.GetComponentInChildren<PauseAudioSettingsPanel>(true);
            Assert.That(panel, Is.Not.Null, "A canvas-backed pause flow should build the mixer panel.");
            Assert.That(panel.IsVisible, Is.True);
            Assert.That(panel.transform.Find("AudioMasterSlider").GetComponent<Slider>().value, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(panel.transform.Find("AudioMusicSlider").GetComponent<Slider>().value, Is.EqualTo(0.34f).Within(0.0001f));
            Assert.That(panel.transform.Find("AudioEffectsSlider").GetComponent<Slider>().value, Is.EqualTo(0.56f).Within(0.0001f));
            Assert.That(panel.transform.Find("AudioAmbienceSlider").GetComponent<Slider>().value, Is.EqualTo(0.23f).Within(0.0001f));
            Assert.That(application.GetComponent<RuntimeAudioDirector>().MusicVolume, Is.EqualTo(0.34f).Within(0.0001f));

            Slider master = panel.transform.Find("AudioMasterSlider").GetComponent<Slider>();
            Slider music = panel.transform.Find("AudioMusicSlider").GetComponent<Slider>();
            Slider effects = panel.transform.Find("AudioEffectsSlider").GetComponent<Slider>();
            Slider ambience = panel.transform.Find("AudioAmbienceSlider").GetComponent<Slider>();
            master.value = 0.9f;
            music.value = 0.4f;
            effects.value = 0.6f;
            ambience.value = 0.3f;
            RuntimeAudioDirector audio = application.GetComponent<RuntimeAudioDirector>();
            RuntimeAmbienceDirector ambienceAudio = application.GetComponent<RuntimeAmbienceDirector>();
            Assert.That(audio.MasterVolume, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(audio.EffectsVolume, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(ambienceAudio.MasterVolume, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(ambienceAudio.AmbienceVolume, Is.EqualTo(0.3f).Within(0.0001f),
                "The audio directors should preview all four slider values immediately.");

            yield return new WaitForSecondsRealtime(0.55f);
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.34f).Within(0.0001f),
                "A failed durable write must not publish the new settings as saved.");
            Assert.That(application.Settings.masterVolume, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(application.Settings.effectsVolume, Is.EqualTo(0.56f).Within(0.0001f));
            Assert.That(application.Settings.ambienceVolume, Is.EqualTo(0.23f).Within(0.0001f));
            Text status = panel.transform.Find("AudioSettingsStatus").GetComponent<Text>();
            StringAssert.Contains("simulated audio settings write failure", status.text.ToLowerInvariant());

            Button retry = panel.transform.Find("RetrySettingsButton").GetComponent<Button>();
            Assert.That(retry.gameObject.activeSelf, Is.True, "A failed write should expose an in-game retry action.");
            retry.onClick.Invoke();
            for (int frame = 0; frame < 60 && retry.gameObject.activeSelf; frame++) yield return null;

            Assert.That(retry.gameObject.activeSelf, Is.False);
            Assert.That(application.Settings.masterVolume, Is.EqualTo(0.9f).Within(0.0001f));
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(application.Settings.effectsVolume, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(application.Settings.ambienceVolume, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(audio.MusicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(pause.IsPaused, Is.True, "Retrying settings must leave the run paused.");
            Assert.That(_store.SettingsWriteAttempts, Is.EqualTo(3), "Retry must reuse the pending settings write.");

            pause.TogglePause();
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(panel.IsVisible, Is.False, "Resuming should hide the pause mixer.");
            Assert.That(Time.timeScale, Is.EqualTo(_previousTimeScale).Within(0.0001f));
            pause.HandleApplicationFocus(hasFocus: false);
            Assert.That(pause.IsPaused, Is.True, "Focus loss should keep the existing pause behavior.");
            Assert.That(panel.IsVisible, Is.True, "Focus loss should present the same pause mixer again.");
        }

        [UnityTest]
        public IEnumerator PendingSettingsSurvivePausePanelDestructionAndAllowANewEdit()
        {
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            _store.BlockNextSettingsWrite();

            _canvasObject = new GameObject("PauseAudioSettingsLifecycleCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject labelObject = new GameObject("PauseLifecycleLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(canvas.transform, false);
            Text pauseLabel = labelObject.GetComponent<Text>();
            pauseLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _pauseObject = new GameObject("PauseControllerBeforeRetry");
            ExpeditionPauseController pause = _pauseObject.AddComponent<ExpeditionPauseController>();
            pause.Initialize(pauseLabel);
            pause.SetPaused(true);

            PauseAudioSettingsPanel panel = canvas.GetComponentInChildren<PauseAudioSettingsPanel>(true);
            Assert.That(panel, Is.Not.Null);
            Slider master = panel.transform.Find("AudioMasterSlider").GetComponent<Slider>();
            Slider music = panel.transform.Find("AudioMusicSlider").GetComponent<Slider>();
            Slider effects = panel.transform.Find("AudioEffectsSlider").GetComponent<Slider>();
            Slider ambience = panel.transform.Find("AudioAmbienceSlider").GetComponent<Slider>();
            master.value = 0.72f;
            music.value = 0.4f;
            effects.value = 0.6f;
            ambience.value = 0.3f;

            for (int frame = 0; frame < 120 && !_store.BlockedSettingsWriteStarted.IsCompleted; frame++)
                yield return null;
            Assert.That(_store.BlockedSettingsWriteStarted.IsCompleted, Is.True,
                "The debounced settings write should reach the blocked store.");

            GameSettingsSaveData confirmed = application.Settings;
            GameSettingsSaveData pending = application.PendingSettingsForRetry;
            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.masterVolume, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(pending.musicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(confirmed.masterVolume, Is.EqualTo(1f).Within(0.0001f),
                "The in-flight payload must not be published as saved.");

            UnityEngine.Object.Destroy(_pauseObject);
            _pauseObject = null;
            yield return null;
            Assert.That(panel == null, Is.True, "The pause owner destroys its panel when the owner is destroyed.");

            _store.FailBlockedSettingsWrite();
            yield return null;
            yield return null;

            _pauseObject = new GameObject("PauseControllerAfterRetry");
            ExpeditionPauseController reopenedPause = _pauseObject.AddComponent<ExpeditionPauseController>();
            reopenedPause.Initialize(pauseLabel);
            reopenedPause.SetPaused(true);
            panel = canvas.GetComponentInChildren<PauseAudioSettingsPanel>(true);
            Assert.That(panel, Is.Not.Null);
            master = panel.transform.Find("AudioMasterSlider").GetComponent<Slider>();
            music = panel.transform.Find("AudioMusicSlider").GetComponent<Slider>();
            effects = panel.transform.Find("AudioEffectsSlider").GetComponent<Slider>();
            ambience = panel.transform.Find("AudioAmbienceSlider").GetComponent<Slider>();
            Assert.That(master.value, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(music.value, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(effects.value, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(ambience.value, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(master.interactable, Is.False, "Pending values stay locked until their exact payload is retried.");
            Text reopenedStatus = panel.transform.Find("AudioSettingsStatus").GetComponent<Text>();
            string pendingMessage = reopenedStatus.text.ToLowerInvariant();
            Assert.That(pendingMessage.Contains("pendiente") || pendingMessage.Contains("pending"), Is.True,
                "The recreated panel must describe the payload as pending, not already saved.");

            Button retry = panel.transform.Find("RetrySettingsButton").GetComponent<Button>();
            Assert.That(retry.gameObject.activeSelf, Is.True);
            retry.onClick.Invoke();
            for (int frame = 0; frame < 60 && retry.gameObject.activeSelf; frame++) yield return null;

            Assert.That(retry.gameObject.activeSelf, Is.False);
            Assert.That(master.interactable, Is.True);
            Assert.That(application.Settings.masterVolume, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(master.value, Is.EqualTo(0.72f).Within(0.0001f),
                "The UI must display the payload that the retry actually saved.");
            string savedMessage = reopenedStatus.text.ToLowerInvariant();
            Assert.That(savedMessage.Contains("guardados") || savedMessage.Contains("saved"), Is.True,
                "Only a successful write may change the status to saved.");
            Assert.That(_store.AttemptedSettingsJson.Count, Is.EqualTo(2));
            Assert.That(_store.AttemptedSettingsJson[1], Is.EqualTo(_store.AttemptedSettingsJson[0]),
                "Explicit retry must replay the exact JSON captured by the failed write.");

            master.value = 0.82f;
            yield return new WaitForSecondsRealtime(0.55f);

            Assert.That(_store.AttemptedSettingsJson.Count, Is.EqualTo(3),
                "The retry and the later edit should each produce one write; panel recreation must not duplicate either.");
            Assert.That(_store.AttemptedSettingsJson[2], Is.Not.EqualTo(_store.AttemptedSettingsJson[1]));
            Assert.That(application.Settings.masterVolume, Is.EqualTo(0.82f).Within(0.0001f));
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(application.Settings.effectsVolume, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(application.Settings.ambienceVolume, Is.EqualTo(0.3f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator SuccessfulWriteWhileReopenedPanelWaitsForRetryRestoresConfirmedUi()
        {
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            _store.BlockNextSettingsWrite();

            _canvasObject = new GameObject("PauseAudioSettingsRaceCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject labelObject = new GameObject("PauseRaceLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(canvas.transform, false);
            Text pauseLabel = labelObject.GetComponent<Text>();
            pauseLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _pauseObject = new GameObject("PauseControllerBeforeSuccessfulWrite");
            ExpeditionPauseController pause = _pauseObject.AddComponent<ExpeditionPauseController>();
            pause.Initialize(pauseLabel);
            pause.SetPaused(true);

            PauseAudioSettingsPanel panel = canvas.GetComponentInChildren<PauseAudioSettingsPanel>(true);
            Slider master = panel.transform.Find("AudioMasterSlider").GetComponent<Slider>();
            Slider music = panel.transform.Find("AudioMusicSlider").GetComponent<Slider>();
            Slider effects = panel.transform.Find("AudioEffectsSlider").GetComponent<Slider>();
            Slider ambience = panel.transform.Find("AudioAmbienceSlider").GetComponent<Slider>();
            master.value = 0.72f;
            music.value = 0.4f;
            effects.value = 0.6f;
            ambience.value = 0.3f;

            for (int frame = 0; frame < 120 && !_store.BlockedSettingsWriteStarted.IsCompleted; frame++)
                yield return null;
            Assert.That(_store.BlockedSettingsWriteStarted.IsCompleted, Is.True);

            UnityEngine.Object.Destroy(_pauseObject);
            _pauseObject = null;
            yield return null;

            _pauseObject = new GameObject("PauseControllerWhileWritePending");
            ExpeditionPauseController reopenedPause = _pauseObject.AddComponent<ExpeditionPauseController>();
            reopenedPause.Initialize(pauseLabel);
            reopenedPause.SetPaused(true);
            panel = canvas.GetComponentInChildren<PauseAudioSettingsPanel>(true);
            Assert.That(panel, Is.Not.Null);
            master = panel.transform.Find("AudioMasterSlider").GetComponent<Slider>();
            Assert.That(master.value, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(master.interactable, Is.False);
            Button retry = panel.transform.Find("RetrySettingsButton").GetComponent<Button>();
            Assert.That(retry.gameObject.activeSelf, Is.True);
            retry.onClick.Invoke();
            yield return null;
            Assert.That(retry.gameObject.activeSelf, Is.True,
                "Retry should wait for the original write's repository gate.");
            Assert.That(retry.interactable, Is.False,
                "The retry action must be in progress while the repository gate is held.");
            Text retryStatus = panel.transform.Find("AudioSettingsStatus").GetComponent<Text>();
            string retryMessage = retryStatus.text.ToLowerInvariant();
            Assert.That(retryMessage.Contains("retrying") || retryMessage.Contains("reintentando"), Is.True,
                "The panel should report that the retry is waiting before the original write is released.");

            _store.CompleteBlockedSettingsWriteSuccessfully();
            for (int frame = 0; frame < 120 && retry.gameObject.activeSelf; frame++) yield return null;

            Assert.That(application.PendingSettingsForRetry, Is.Null);
            Assert.That(application.Settings.masterVolume, Is.EqualTo(0.72f).Within(0.0001f));
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(application.Settings.effectsVolume, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(application.Settings.ambienceVolume, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(retry.gameObject.activeSelf, Is.False,
                "A no-pending retry result must clear the stale retry UI after the original write succeeded.");
            Assert.That(master.interactable, Is.True);
            Assert.That(master.value, Is.EqualTo(0.72f).Within(0.0001f));
            Text status = panel.transform.Find("AudioSettingsStatus").GetComponent<Text>();
            string message = status.text.ToLowerInvariant();
            Assert.That(message.Contains("guardados") || message.Contains("saved"), Is.True);
            Assert.That(_store.AttemptedSettingsJson.Count, Is.EqualTo(1),
                "The stale retry must not start a second store write.");
        }

        [UnityTest]
        public IEnumerator PendingSettingsFromApplicationCallerCanBeRetriedFromPausePanel()
        {
            ApplicationBootstrapper application = ApplicationBootstrapper.Instance;
            GameSettingsSaveData requested = application.Settings;
            requested.masterVolume = 0.61f;
            requested.musicVolume = 0.22f;
            requested.effectsVolume = 0.43f;
            requested.ambienceVolume = 0.37f;
            _store.FailNextSettingsWrite = true;

            Task<SaveWriteResult> failedWrite = application.SaveSettingsAsync(requested);
            while (!failedWrite.IsCompleted) yield return null;
            Assert.That(failedWrite.Result.Succeeded, Is.False);
            Assert.That(application.PendingSettingsForRetry, Is.Not.Null);
            Assert.That(application.PendingSettingsForRetry.masterVolume, Is.EqualTo(0.61f).Within(0.0001f));
            Assert.That(_store.AttemptedSettingsJson.Count, Is.EqualTo(1));

            _canvasObject = new GameObject("PauseAudioSettingsCallerCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GameObject labelObject = new GameObject("PauseCallerLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            labelObject.transform.SetParent(canvas.transform, false);
            Text pauseLabel = labelObject.GetComponent<Text>();
            pauseLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _pauseObject = new GameObject("PauseControllerForApplicationPendingWrite");
            ExpeditionPauseController pause = _pauseObject.AddComponent<ExpeditionPauseController>();
            pause.Initialize(pauseLabel);
            pause.SetPaused(true);
            PauseAudioSettingsPanel panel = canvas.GetComponentInChildren<PauseAudioSettingsPanel>(true);
            Slider master = panel.transform.Find("AudioMasterSlider").GetComponent<Slider>();
            Button retry = panel.transform.Find("RetrySettingsButton").GetComponent<Button>();
            Assert.That(master.value, Is.EqualTo(0.61f).Within(0.0001f));
            Assert.That(master.interactable, Is.False);
            Assert.That(retry.gameObject.activeSelf, Is.True);

            retry.onClick.Invoke();
            for (int frame = 0; frame < 60 && retry.gameObject.activeSelf; frame++) yield return null;

            Assert.That(application.PendingSettingsForRetry, Is.Null);
            Assert.That(application.Settings.masterVolume, Is.EqualTo(0.61f).Within(0.0001f));
            Assert.That(application.Settings.musicVolume, Is.EqualTo(0.22f).Within(0.0001f));
            Assert.That(application.Settings.effectsVolume, Is.EqualTo(0.43f).Within(0.0001f));
            Assert.That(application.Settings.ambienceVolume, Is.EqualTo(0.37f).Within(0.0001f));
            Assert.That(retry.gameObject.activeSelf, Is.False);
            Assert.That(master.interactable, Is.True);
            Assert.That(master.value, Is.EqualTo(0.61f).Within(0.0001f));
            Assert.That(_store.AttemptedSettingsJson.Count, Is.EqualTo(2));
            Assert.That(_store.AttemptedSettingsJson[1], Is.EqualTo(_store.AttemptedSettingsJson[0]),
                "A pending write created through the shared application API must be replayed byte-for-byte.");
        }
        private sealed class FailFirstSettingsStore : ISaveStore
        {
            private readonly Dictionary<SaveDocument, string> _documents = new Dictionary<SaveDocument, string>();
            private readonly List<string> _attemptedSettingsJson = new List<string>();
            private TaskCompletionSource<bool> _blockedSettingsWrite;
            private TaskCompletionSource<bool> _blockedSettingsWriteStarted;
            private string _blockedSettingsJson;
            public bool FailNextSettingsWrite { get; set; }

            public int SettingsWriteAttempts { get; private set; }
            public IReadOnlyList<string> AttemptedSettingsJson => _attemptedSettingsJson;
            public Task BlockedSettingsWriteStarted => _blockedSettingsWriteStarted == null
                ? Task.CompletedTask
                : _blockedSettingsWriteStarted.Task;

            public void BlockNextSettingsWrite()
            {
                _blockedSettingsWrite = new TaskCompletionSource<bool>();
                _blockedSettingsWriteStarted = new TaskCompletionSource<bool>();
            }

            public void FailBlockedSettingsWrite()
            {
                if (_blockedSettingsWrite == null)
                    throw new InvalidOperationException("There is no blocked settings write to fail.");
                TaskCompletionSource<bool> blocked = _blockedSettingsWrite;
                _blockedSettingsWrite = null;
                _blockedSettingsJson = null;
                blocked.SetException(new IOException("simulated blocked audio settings write failure"));
            }

            public void CompleteBlockedSettingsWriteSuccessfully()
            {
                if (_blockedSettingsWrite == null)
                    throw new InvalidOperationException("There is no blocked settings write to complete.");
                TaskCompletionSource<bool> blocked = _blockedSettingsWrite;
                _blockedSettingsWrite = null;
                _documents[SaveDocument.Settings] = _blockedSettingsJson;
                _blockedSettingsJson = null;
                blocked.SetResult(true);
            }

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                _documents.TryGetValue(document, out string primary);
                return Task.FromResult(new SaveStorageSnapshot(primary, null));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Settings)
                {
                    SettingsWriteAttempts++;
                    _attemptedSettingsJson.Add(json);
                    if (_blockedSettingsWrite != null)
                    {
                        _blockedSettingsJson = json;
                        _blockedSettingsWriteStarted.TrySetResult(true);
                        return _blockedSettingsWrite.Task;
                    }
                    if (FailNextSettingsWrite)
                    {
                        FailNextSettingsWrite = false;
                        return Task.FromException(new IOException("simulated audio settings write failure"));
                    }
                }

                _documents[document] = json;
                return Task.CompletedTask;
            }
        }
    }
}
#endif
