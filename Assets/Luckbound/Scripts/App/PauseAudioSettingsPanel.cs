using System;
using System.Collections;
using System.Globalization;
using System.Threading.Tasks;
using Luckbound.Core.Saves;
using Luckbound.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Luckbound.App
{
    /// <summary>Builds the in-game pause mixer and persists previewed values through the application root.</summary>
    public sealed class PauseAudioSettingsPanel : MonoBehaviour
    {
        private const float SaveDebounceSeconds = 0.35f;
        private const float PanelWidth = 430f;
        private const float PanelHeight = 230f;

        private ApplicationBootstrapper _application;
        private RuntimeAudioDirector _audioDirector;
        private RuntimeAmbienceDirector _ambienceDirector;
        private GameSettingsSaveData _settingsTemplate;
        private GameSettingsSaveData _pendingSettings;
        private CanvasGroup _canvasGroup;
        private Slider _masterSlider;
        private Slider _musicSlider;
        private Slider _effectsSlider;
        private Slider _ambienceSlider;
        private Button _retryButton;
        private Text _masterValue;
        private Text _musicValue;
        private Text _effectsValue;
        private Text _ambienceValue;
        private Coroutine _saveCoroutine;
        private Sprite _whiteSprite;
        private bool _english;
        private bool _suppressSliderEvents;
        private bool _isSaving;
        private bool _initialized;

        public Text StatusText { get; private set; }

        public bool IsVisible => _canvasGroup != null && _canvasGroup.alpha > 0f;

        public void Initialize(ApplicationBootstrapper application)
        {
            if (_initialized) throw new InvalidOperationException("PauseAudioSettingsPanel can only be initialized once.");
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (!application.SaveDataReady) throw new InvalidOperationException("Audio settings are not ready yet.");

            _application = application;
            _audioDirector = application.GetComponent<RuntimeAudioDirector>();
            _ambienceDirector = application.GetComponent<RuntimeAmbienceDirector>();
            GameSettingsSaveData pendingSettings = application.PendingSettingsForRetry;
            _settingsTemplate = pendingSettings ?? application.Settings ?? new GameSettingsSaveData();
            _english = string.Equals(_settingsTemplate.languageId, "en", StringComparison.OrdinalIgnoreCase);
            _whiteSprite = CreateWhiteSprite();
            BuildPanel();
            SetVolumeValues(_settingsTemplate);
            BindSlider(_masterSlider, RefreshMasterValue);
            BindSlider(_musicSlider, RefreshMusicValue);
            BindSlider(_effectsSlider, RefreshEffectsValue);
            BindSlider(_ambienceSlider, RefreshAmbienceValue);
            _retryButton.onClick.AddListener(RetryPendingSave);
            if (pendingSettings != null)
            {
                _pendingSettings = pendingSettings.Clone();
                SetSlidersInteractable(false);
                _retryButton.gameObject.SetActive(true);
                ApplyLivePreview(_pendingSettings);
                StatusText.text = _english
                    ? "A previous settings save is pending. Retry it before changing values."
                    : "Hay un guardado de ajustes pendiente. Reinténtalo antes de cambiar valores.";
            }
            else
            {
                _retryButton.gameObject.SetActive(false);
                StatusText.text = _english
                    ? "Volume changes preview immediately and save automatically."
                    : "El volumen se previsualiza y se guarda automáticamente.";
            }
            _initialized = true;
        }

        public void SetVisible(bool visible)
        {
            if (_canvasGroup == null) return;
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void BuildPanel()
        {
            RectTransform root = transform as RectTransform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            Image background = gameObject.AddComponent<Image>();
            background.sprite = _whiteSprite;
            background.color = new Color(0.075f, 0.061f, 0.05f, 0.97f);
            background.raycastTarget = true;

            _canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;

            Text title = CreateText("AudioSettingsTitle", transform,
                _english ? "PAUSED · AUDIO SETTINGS" : "PAUSA · AJUSTES DE AUDIO",
                19, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 88f), new Vector2(360f, 26f));

            _masterValue = CreateRow("Master", _english ? "Master" : "Principal", 50f,
                _settingsTemplate.masterVolume, out _masterSlider);
            _musicValue = CreateRow("Music", _english ? "Music" : "Música", 20f,
                _settingsTemplate.musicVolume, out _musicSlider);
            _effectsValue = CreateRow("Effects", _english ? "Effects" : "Efectos", -10f,
                _settingsTemplate.effectsVolume, out _effectsSlider);
            _ambienceValue = CreateRow("Ambience", _english ? "Ambience" : "Ambiente", -40f,
                _settingsTemplate.ambienceVolume, out _ambienceSlider);

            StatusText = CreateText("AudioSettingsStatus", transform, string.Empty, 12, FontStyle.Normal,
                TextAnchor.MiddleCenter);
            SetRect(StatusText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -73f), new Vector2(396f, 30f));
            StatusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            StatusText.verticalOverflow = VerticalWrapMode.Truncate;

            Text resumeHint = CreateText("PauseResumeHint", transform,
                _english ? "ESC or MENU to resume" : "ESC o MENU: continuar", 10, FontStyle.Normal,
                TextAnchor.MiddleLeft);
            SetRect(resumeHint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, -101f), new Vector2(158f, 22f), new Vector2(0f, 0.5f));

            _retryButton = CreateButton("RetrySettingsButton", _english ? "RETRY SAVE" : "REINTENTAR GUARDADO");
            SetRect((RectTransform)_retryButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(105f, -101f), new Vector2(188f, 25f));
        }

        private Text CreateRow(string key, string label, float y, float value, out Slider slider)
        {
            Text labelText = CreateText("Audio" + key + "Label", transform, label, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft);
            SetRect(labelText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(18f, y), new Vector2(100f, 24f), new Vector2(0f, 0.5f));

            Text valueText = CreateText("Audio" + key + "Value", transform, string.Empty, 14, FontStyle.Bold,
                TextAnchor.MiddleRight);
            SetRect(valueText.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-18f, y), new Vector2(50f, 24f), new Vector2(1f, 0.5f));

            slider = CreateSlider("Audio" + key + "Slider", y, value);
            valueText.text = FormatPercent(value);
            return valueText;
        }

        private Slider CreateSlider(string name, float y, float value)
        {
            GameObject sliderObject = CreateUiObject(name, transform);
            RectTransform sliderRect = (RectTransform)sliderObject.transform;
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.offsetMin = new Vector2(118f, y - 8f);
            sliderRect.offsetMax = new Vector2(-72f, y + 8f);

            Image background = sliderObject.AddComponent<Image>();
            background.sprite = _whiteSprite;
            background.color = new Color(0.2f, 0.17f, 0.14f, 1f);

            GameObject fillObject = CreateUiObject("Fill", sliderObject.transform);
            RectTransform fillRect = (RectTransform)fillObject.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2f, 3f);
            fillRect.offsetMax = new Vector2(-2f, -3f);
            Image fillImage = fillObject.AddComponent<Image>();
            fillImage.sprite = _whiteSprite;
            fillImage.color = new Color(0.84f, 0.61f, 0.25f, 1f);

            GameObject handleObject = CreateUiObject("Handle", sliderObject.transform);
            RectTransform handleRect = (RectTransform)handleObject.transform;
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(16f, 22f);
            Image handleImage = handleObject.AddComponent<Image>();
            handleImage.sprite = _whiteSprite;
            handleImage.color = new Color(0.96f, 0.88f, 0.68f, 1f);

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            return slider;
        }

        private Button CreateButton(string name, string label)
        {
            GameObject buttonObject = CreateUiObject(name, transform);
            Image image = buttonObject.AddComponent<Image>();
            image.sprite = _whiteSprite;
            image.color = new Color(0.45f, 0.29f, 0.15f, 1f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.45f, 0.29f, 0.15f, 1f);
            colors.highlightedColor = new Color(0.62f, 0.41f, 0.2f, 1f);
            colors.pressedColor = new Color(0.31f, 0.2f, 0.12f, 1f);
            colors.disabledColor = new Color(0.27f, 0.23f, 0.2f, 0.6f);
            button.colors = colors;

            Text buttonText = CreateText("Label", buttonObject.transform, label, 12, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            SetRect(buttonText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            buttonText.raycastTarget = false;
            return button;
        }

        private Text CreateText(string name, Transform parent, string value, int fontSize, FontStyle style,
            TextAnchor alignment)
        {
            GameObject textObject = CreateUiObject(name, parent);
            Text text = textObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.94f, 0.9f, 0.8f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = value;
            return text;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            result.transform.SetParent(parent, false);
            return result;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 anchoredPosition, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }

        private static Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "PauseAudioSettingsWhitePixel",
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "PauseAudioSettingsWhiteSprite";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private void BindSlider(Slider slider, Action refreshValue)
        {
            slider.onValueChanged.AddListener(_ =>
            {
                refreshValue();
                OnSliderChanged();
            });
        }

        private void SetVolumeValues(GameSettingsSaveData settings)
        {
            _suppressSliderEvents = true;
            _masterSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.masterVolume));
            _musicSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.musicVolume));
            _effectsSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.effectsVolume));
            _ambienceSlider.SetValueWithoutNotify(Mathf.Clamp01(settings.ambienceVolume));
            RefreshMasterValue();
            RefreshMusicValue();
            RefreshEffectsValue();
            RefreshAmbienceValue();
            _suppressSliderEvents = false;
        }

        private void OnSliderChanged()
        {
            if (!_initialized || _suppressSliderEvents) return;
            if (_isSaving || _pendingSettings != null)
            {
                if (_pendingSettings != null) SetVolumeValues(_pendingSettings);
                return;
            }

            GameSettingsSaveData preview = CaptureSettings();
            ApplyLivePreview(preview);
            StatusText.text = _english ? "Preview active · saving…" : "Vista previa activa · guardando…";
            if (_saveCoroutine != null) StopCoroutine(_saveCoroutine);
            _saveCoroutine = StartCoroutine(SaveAfterDebounce());
        }

        private IEnumerator SaveAfterDebounce()
        {
            yield return new WaitForSecondsRealtime(SaveDebounceSeconds);
            _saveCoroutine = null;
            if (_isSaving || _pendingSettings != null) yield break;

            GameSettingsSaveData candidate = CaptureSettings();
            _pendingSettings = candidate.Clone();
            _isSaving = true;
            SetSlidersInteractable(false);
            StatusText.text = _english ? "Saving audio settings…" : "Guardando ajustes de audio…";

            Task<SaveWriteResult> writeTask;
            try
            {
                writeTask = _application.SaveSettingsAsync(candidate);
            }
            catch (Exception exception)
            {
                CompleteWriteFailure(exception.Message);
                yield break;
            }

            while (!writeTask.IsCompleted) yield return null;
            if (writeTask.IsFaulted)
            {
                CompleteWriteFailure(writeTask.Exception == null
                    ? (_english ? "Unknown settings write failure." : "Fallo desconocido al guardar los ajustes.")
                    : writeTask.Exception.GetBaseException().Message);
            }
            else if (writeTask.IsCanceled)
            {
                CompleteWriteFailure(_english ? "The settings write was cancelled." : "Se canceló el guardado de los ajustes.");
            }
            else if (writeTask.Result.Succeeded)
            {
                CompleteWriteSuccess();
            }
            else
            {
                CompleteWriteFailure(writeTask.Result.Message);
            }
        }

        private void RetryPendingSave()
        {
            if (_pendingSettings == null || _isSaving) return;
            if (_saveCoroutine != null)
            {
                StopCoroutine(_saveCoroutine);
                _saveCoroutine = null;
            }
            _saveCoroutine = StartCoroutine(RetryPendingSaveRoutine());
        }

        private IEnumerator RetryPendingSaveRoutine()
        {
            _isSaving = true;
            SetSlidersInteractable(false);
            _retryButton.interactable = false;
            StatusText.text = _english ? "Retrying audio settings…" : "Reintentando guardar los ajustes…";

            Task<SaveWriteResult> writeTask;
            try
            {
                writeTask = _application.RetryPendingSettingsAsync();
            }
            catch (Exception exception)
            {
                ReconcileAfterRetry(false, exception.Message);
                _saveCoroutine = null;
                yield break;
            }

            while (!writeTask.IsCompleted) yield return null;
            if (writeTask.IsFaulted)
            {
                ReconcileAfterRetry(false, writeTask.Exception == null
                    ? (_english ? "Unknown settings retry failure." : "Fallo desconocido al reintentar los ajustes.")
                    : writeTask.Exception.GetBaseException().Message);
            }
            else if (writeTask.IsCanceled)
            {
                ReconcileAfterRetry(false,
                    _english ? "The settings retry was cancelled." : "Se canceló el reintento de ajustes.");
            }
            else
            {
                ReconcileAfterRetry(writeTask.Result.Succeeded, writeTask.Result.Message);
            }

            _saveCoroutine = null;
        }

        private void ReconcileAfterRetry(bool succeeded, string message)
        {
            _isSaving = false;
            GameSettingsSaveData pending = _application.PendingSettingsForRetry;
            if (pending != null)
            {
                _pendingSettings = pending.Clone();
                _settingsTemplate = pending.Clone();
                SetVolumeValues(pending);
                ApplyLivePreview(pending);
                SetSlidersInteractable(false);
                _retryButton.gameObject.SetActive(true);
                _retryButton.interactable = true;
                StatusText.text = succeeded
                    ? (_english
                        ? "A settings save is still pending. Retry it before changing values."
                        : "Aún hay un guardado de ajustes pendiente. Reinténtalo antes de cambiar valores.")
                    : (_english ? "Couldn't save audio settings: " : "No se pudieron guardar los ajustes: ") +
                      (string.IsNullOrWhiteSpace(message)
                          ? (_english ? "unknown error" : "error desconocido")
                          : message);
                return;
            }

            GameSettingsSaveData confirmed = _application.Settings;
            _pendingSettings = null;
            _settingsTemplate = confirmed.Clone();
            SetVolumeValues(confirmed);
            ApplyLivePreview(confirmed);
            SetSlidersInteractable(true);
            _retryButton.interactable = true;
            _retryButton.gameObject.SetActive(false);
            StatusText.text = succeeded
                ? (_english ? "Audio settings saved." : "Ajustes de audio guardados.")
                : (_english
                    ? "No settings save is pending. Latest saved values restored."
                    : "No hay guardado pendiente. Se restauraron los últimos valores guardados.");
        }

        private void CompleteWriteSuccess()
        {
            _isSaving = false;
            _pendingSettings = null;
            _settingsTemplate = _application.Settings;
            SetVolumeValues(_settingsTemplate);
            SetSlidersInteractable(true);
            _retryButton.interactable = true;
            _retryButton.gameObject.SetActive(false);
            StatusText.text = _english ? "Audio settings saved." : "Ajustes de audio guardados.";
        }

        private void CompleteWriteFailure(string message)
        {
            _isSaving = false;
            SetSlidersInteractable(false);
            _retryButton.gameObject.SetActive(true);
            _retryButton.interactable = true;
            StatusText.text = (_english ? "Couldn't save audio settings: " : "No se pudieron guardar los ajustes: ") +
                              (string.IsNullOrWhiteSpace(message)
                                  ? (_english ? "unknown error" : "error desconocido")
                                  : message);
        }

        private void SetSlidersInteractable(bool interactable)
        {
            _masterSlider.interactable = interactable;
            _musicSlider.interactable = interactable;
            _effectsSlider.interactable = interactable;
            _ambienceSlider.interactable = interactable;
        }

        private GameSettingsSaveData CaptureSettings()
        {
            GameSettingsSaveData settings = _settingsTemplate.Clone();
            settings.masterVolume = _masterSlider.value;
            settings.musicVolume = _musicSlider.value;
            settings.effectsVolume = _effectsSlider.value;
            settings.ambienceVolume = _ambienceSlider.value;
            return settings;
        }

        private void ApplyLivePreview(GameSettingsSaveData settings)
        {
            if (_audioDirector != null)
                _audioDirector.SetVolumes(settings.masterVolume, settings.musicVolume,
                    settings.effectsVolume, settings.ambienceVolume);
            if (_ambienceDirector != null)
                _ambienceDirector.SetVolumes(settings.masterVolume, settings.ambienceVolume);
        }

        private void RefreshMasterValue() => _masterValue.text = FormatPercent(_masterSlider.value);
        private void RefreshMusicValue() => _musicValue.text = FormatPercent(_musicSlider.value);
        private void RefreshEffectsValue() => _effectsValue.text = FormatPercent(_effectsSlider.value);
        private void RefreshAmbienceValue() => _ambienceValue.text = FormatPercent(_ambienceSlider.value);

        private static string FormatPercent(float value) =>
            Mathf.RoundToInt(Mathf.Clamp01(value) * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        private void OnDestroy()
        {
            if (_saveCoroutine != null) StopCoroutine(_saveCoroutine);
            if (_whiteSprite != null)
            {
                Texture2D texture = _whiteSprite.texture;
                Destroy(_whiteSprite);
                if (texture != null) Destroy(texture);
                _whiteSprite = null;
            }
        }
    }
}
