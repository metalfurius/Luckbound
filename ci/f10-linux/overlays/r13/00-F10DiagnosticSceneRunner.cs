#if UNITY_EDITOR
using System;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Luckbound.App
{
    internal enum F10DiagnosticSessionMode
    {
        BeginNew = 0,
        ResumeExisting = 1
    }

    /// <summary>Starts the isolated technical F10 scene in Play Mode. It is never included in a player build.</summary>
    public sealed class F10DiagnosticSceneRunner : MonoBehaviour
    {
        [SerializeField] private F10DiagnosticSessionMode _sessionMode = F10DiagnosticSessionMode.BeginNew;
        [SerializeField] private GameObject _technicalPlayerPrefab;
        [SerializeField] private ExpeditionRoomPrefabEntry[] _technicalRooms;
        [SerializeField] private GameObject _technicalGoldCachePrefab;
        private ApplicationBootstrapper _ownedApplication;
        private GameObject _ownedCamera;
        private GameObject _ownedHud;
        private GameObject _ownedGameplay;
        private F10TutorialAdvanceCoordinator _tutorialAdvance;
        private F10TechnicalEncounterPlanProvider _encounterProvider;
        private F10C1RuntimeCoordinator _c1Encounter;
        private F10ExplorationAdvanceCoordinator _explorationAdvance;
        private F10GuardianRuntimeCoordinator _guardianEncounter;
        private bool _c1AttachAttempted;
        private string _c1AttachError = string.Empty;
        private bool _c1RoomAdvanceAttempted;
        private bool _c1RoomAdvanceInFlight;
        private bool _c1RoomAdvancePending;
        private bool _c1RoomAdvanceFreshRetryAvailable;
        private bool _c1RoomAdvanceCompleted;
        private string _c1RoomAdvanceMessage = string.Empty;
        private bool _explorationAttachAttempted;
        private string _explorationAttachError = string.Empty;
        private bool _explorationRetryInFlight;
        private bool _explorationAdvanceCompleted;
        private bool _guardianAttachAttempted;
        private string _guardianAttachError = string.Empty;
        private bool _guardianRoomAdvanceAttempted;
        private bool _guardianRoomAdvanceInFlight;
        private bool _guardianRoomAdvancePending;
        private bool _guardianRoomAdvanceFreshRetryAvailable;
        private bool _guardianRoomAdvanceCompleted;
        private string _guardianRoomAdvanceMessage = string.Empty;
        private F10ExtractionSettlementCoordinator _extractionSettlement;
        private bool _extractionAttachAttempted;
        private bool _extractionCoordinatorAttached;
        private string _extractionAttachError = string.Empty;
        private string _extractionRunId = string.Empty;
        private bool _extractionRetryInFlight;
        private Text _tutorialStatus;
        private Button _tutorialRetry;
        private static F10DiagnosticSessionMode? _nextSessionModeForEditor;

        private const string TutorialNodeId = "sample.tutorial";
        private const string ExplorationNodeId = "sample.patio.exploration";
        private const string ExtractionNodeId = "sample.safe_extraction";

        public string LastError { get; private set; } = string.Empty;
        public ExpeditionGameplayComposition Composition { get; private set; }
        public GameObject GoldCachePrefab => _technicalGoldCachePrefab;

        /// <summary>Sets a one-shot explicit process mode before an Editor test loads this scene.</summary>
        internal static void SetSessionModeForNextStart(F10DiagnosticSessionMode mode)
        {
            if (mode != F10DiagnosticSessionMode.BeginNew && mode != F10DiagnosticSessionMode.ResumeExisting)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (ApplicationBootstrapper.Instance != null || HasLoadedDiagnosticRunner())
                throw new InvalidOperationException("A diagnostic bootstrapper or runner is already active; its session mode cannot be changed.");
            if (_nextSessionModeForEditor.HasValue)
                throw new InvalidOperationException("A one-shot diagnostic session mode is already selected and cannot be replaced.");
            _nextSessionModeForEditor = mode;
        }

        private static bool HasLoadedDiagnosticRunner()
        {
            return Resources.FindObjectsOfTypeAll<F10DiagnosticSceneRunner>().Any(runner =>
                runner != null && runner.gameObject.scene.IsValid() && runner.gameObject.scene.isLoaded);
        }

        /// <summary>Called only by the diagnostic scene generator before entering Play Mode.</summary>
        public void ConfigureForEditor(GameObject player, ExpeditionRoomPrefabEntry[] rooms, GameObject goldCache)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Diagnostic assets must be configured before Play Mode.");
            ValidateAssets(player, rooms, goldCache);
            _technicalPlayerPrefab = player;
            _technicalRooms = rooms.Select(entry => new ExpeditionRoomPrefabEntry
                { templateId = entry.templateId, prefab = entry.prefab }).ToArray();
            _technicalGoldCachePrefab = goldCache;
        }

        private async void Start()
        {
            ApplicationBootstrapper application = null;
            try
            {
                ValidateAssets(_technicalPlayerPrefab, _technicalRooms, _technicalGoldCachePrefab);
                const string expectedScenePath = "Assets/Luckbound/Editor/Diagnostic/F10Diagnostic.unity";
                UnityEngine.SceneManagement.Scene ownerScene = gameObject.scene;
                if (!ownerScene.IsValid() || !ownerScene.isLoaded ||
                    !string.Equals(ownerScene.path, expectedScenePath, StringComparison.Ordinal) ||
                    !string.Equals(ownerScene.name, "F10Diagnostic", StringComparison.Ordinal))
                    throw new InvalidOperationException("F10 runner requires its loaded canonical owner scene 'F10Diagnostic' at '" +
                        expectedScenePath + "'; actual name='" + ownerScene.name + "', path='" + ownerScene.path +
                        "', handle=" + ownerScene.handle + ".");
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene() != ownerScene)
                    UnityEngine.SceneManagement.SceneManager.SetActiveScene(ownerScene);
                UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (activeScene != ownerScene)
                    throw new InvalidOperationException("F10 runner could not activate owner scene '" + expectedScenePath +
                        "', handle=" + ownerScene.handle + "; actual active name='" + activeScene.name + "', path='" +
                        activeScene.path + "', handle=" + activeScene.handle + ".");
                F10DiagnosticSessionMode sessionMode = ConsumeSessionMode();
                application = sessionMode == F10DiagnosticSessionMode.ResumeExisting
                    ? await ApplicationBootstrapper.ResumeF10DiagnosticSessionAsync()
                    : await ApplicationBootstrapper.BeginF10DiagnosticSessionAsync();
                if (this == null || !isActiveAndEnabled)
                {
                    CleanupApplicationIfOwned(application);
                    return;
                }
                _ownedApplication = application;

                GameObject cameraObject = new GameObject("F10 Technical Camera");
                _ownedCamera = cameraObject;
                cameraObject.SetActive(false);
                cameraObject.transform.SetParent(transform, false);
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);
                RoomCameraRig2D cameraRig = cameraObject.AddComponent<RoomCameraRig2D>();
                AudioListener listener = cameraObject.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
                cameraObject.SetActive(true);

                GameObject hud = new GameObject("F10 Technical HUD", typeof(RectTransform),
                    typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                _ownedHud = hud;
                hud.transform.SetParent(transform, false);
                hud.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                if (EventSystem.current == null)
                {
                    var eventObject = new GameObject("F10 Technical Event System",
                        typeof(EventSystem), typeof(InputSystemUIInputModule));
                    eventObject.transform.SetParent(hud.transform, false);
                }
                else if (EventSystem.current.GetComponent<InputSystemUIInputModule>() == null)
                    throw new InvalidOperationException("The existing event system does not support the Input System UI module.");
                Text playerHud = CreateLabel(hud.transform, "Player HUD", new Vector2(12f, -12f));
                Text pauseLabel = CreateLabel(hud.transform, "Pause", new Vector2(12f, -42f));
                _tutorialStatus = CreateLabel(hud.transform, "Tutorial status", new Vector2(12f, -72f));
                var buttonObject = new GameObject("Return", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(hud.transform, false);
                Button returnButton = buttonObject.GetComponent<Button>();
                Text buttonText = CreateLabel(buttonObject.transform, "Label", Vector2.zero);
                buttonText.text = "Volver";
                var retryObject = new GameObject("Retry tutorial save", typeof(RectTransform), typeof(Image), typeof(Button));
                retryObject.transform.SetParent(hud.transform, false);
                RectTransform retryRect = retryObject.GetComponent<RectTransform>();
                retryRect.anchorMin = retryRect.anchorMax = new Vector2(0f, 1f);
                retryRect.pivot = new Vector2(0f, 1f);
                retryRect.anchoredPosition = new Vector2(12f, -110f);
                retryRect.sizeDelta = new Vector2(220f, 36f);
                _tutorialRetry = retryObject.GetComponent<Button>();
                _tutorialRetry.onClick.AddListener(RetryTutorial);
                Text retryLabel = CreateLabel(retryObject.transform, "Label", Vector2.zero);
                retryLabel.text = "Reintentar";
                retryLabel.color = Color.black;
                retryObject.SetActive(false);

                var gameplayObject = new GameObject("F10 Technical Expedition");
                _ownedGameplay = gameplayObject;
                gameplayObject.transform.SetParent(transform, false);
                Composition = gameplayObject.AddComponent<ExpeditionGameplayComposition>();
                Composition.ConfigureF10TechnicalPresentation(cameraRig, hud, playerHud, pauseLabel, returnButton);
                if (!Composition.TryInitializeF10Technical(application.ActiveRun, application.ActiveExpedition,
                        _technicalPlayerPrefab, _technicalRooms, out string error))
                    throw new InvalidOperationException(error);
                _encounterProvider = FreezeEncounterProvider();
                AttachCoordinatorForDurableCursor(application, Composition);
            }
            catch (Exception exception)
            {
                if (this != null)
                {
                    LastError = exception.Message;
                    CleanupOwnedObjects();
                    Debug.LogException(exception, this);
                }
                CleanupApplicationIfOwned(application);
            }
        }

        private void OnDestroy()
        {
            CleanupOwnedObjects();
            CleanupApplicationIfOwned(_ownedApplication);
            _ownedApplication = null;
        }

        private void Update()
        {
            if (_tutorialStatus == null || _tutorialRetry == null) return;
            if (TryShowDurableExtractionResult()) return;
            if (Composition != null && Composition.RoomTransitions != null &&
                Composition.RoomTransitions.CurrentNodeId == F10TechnicalEncounterPlanProvider.C1NodeId)
            {
                UpdateC1Encounter();
                return;
            }
            if (Composition != null && Composition.RoomTransitions != null &&
                string.Equals(Composition.RoomTransitions.CurrentNodeId, ExplorationNodeId, StringComparison.Ordinal))
            {
                UpdateE1Exploration();
                return;
            }
            if (IsCurrentNode(F10TechnicalEncounterPlanProvider.GuardianNodeId))
            {
                UpdateGuardianEncounter();
                return;
            }
            if (IsCurrentNode(ExtractionNodeId))
            {
                UpdateExtractionSettlement();
                return;
            }
            if (!IsCurrentNode(TutorialNodeId) || _tutorialAdvance == null) return;

            if (_guardianRoomAdvanceCompleted)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Custodio derrotado y guardado · extracción pendiente";
                return;
            }
            if (_explorationAdvanceCompleted)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Salida de E1 guardada";
                return;
            }
            if (_c1RoomAdvanceCompleted)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Combate de C1 guardado · E1 activo";
                return;
            }
            F10TutorialAdvanceState state = _tutorialAdvance.State;
            bool retryable = state == F10TutorialAdvanceState.PendingRetry ||
                state == F10TutorialAdvanceState.Failed || state == F10TutorialAdvanceState.SetupFailed;
            _tutorialRetry.gameObject.SetActive(retryable);
            _tutorialStatus.text = retryable
                ? (state == F10TutorialAdvanceState.PendingRetry ? "GUARDADO PENDIENTE · " : "NO SE PUDO AVANZAR · ") + _tutorialAdvance.Error
                : state == F10TutorialAdvanceState.Resolving
                    ? "Guardando sala…"
                : state == F10TutorialAdvanceState.Completed
                    ? "Tutorial guardado · entrada al combate"
                    : "Muévete, salta y pulsa E en la interacción";
        }

        private async void RetryTutorial()
        {
            if (IsCurrentNode(F10TechnicalEncounterPlanProvider.C1NodeId))
            {
                if (_c1RoomAdvanceInFlight) return;
                if (_c1Encounter == null && !string.IsNullOrEmpty(_c1AttachError))
                {
                    _c1AttachAttempted = false;
                    _c1AttachError = string.Empty;
                    return;
                }
                if (_c1RoomAdvancePending)
                {
                    await RetryPendingC1RoomAdvanceAsync();
                    return;
                }
                if (_c1Encounter != null && _c1RoomAdvanceFreshRetryAvailable &&
                    CanStartC1RoomAdvanceAttempt(_c1Encounter.HasPhysicalClearObservation,
                        _c1Encounter.RuntimeStatus, _c1RoomAdvanceAttempted,
                        _c1RoomAdvanceInFlight, _c1RoomAdvanceFreshRetryAvailable))
                    await StartC1RoomAdvanceAsync();
                return;
            }
            if (IsCurrentNode(ExplorationNodeId))
            {
                if (_explorationRetryInFlight) return;
                if (_explorationAdvance == null && !string.IsNullOrEmpty(_explorationAttachError))
                {
                    _explorationAttachAttempted = false;
                    _explorationAttachError = string.Empty;
                    return;
                }
                if (_explorationAdvance == null) return;
                _explorationRetryInFlight = true;
                try { await _explorationAdvance.RetryAsync(); }
                catch (Exception exception)
                {
                    if (this != null) _explorationAttachError = exception.Message;
                }
                finally
                {
                    if (this != null) _explorationRetryInFlight = false;
                }
                return;
            }
            if (IsCurrentNode(F10TechnicalEncounterPlanProvider.GuardianNodeId))
            {
                if (_guardianRoomAdvanceInFlight) return;
                if (_guardianEncounter == null && !string.IsNullOrEmpty(_guardianAttachError))
                {
                    _guardianAttachAttempted = false;
                    _guardianAttachError = string.Empty;
                    return;
                }
                if (_guardianRoomAdvancePending)
                {
                    await RetryPendingGuardianRoomAdvanceAsync();
                    return;
                }
                if (_guardianEncounter != null && _guardianRoomAdvanceFreshRetryAvailable &&
                    CanStartGuardianRoomAdvanceAttempt(_guardianEncounter.HasPhysicalClearObservation,
                        _guardianEncounter.RuntimeStatus, _guardianRoomAdvanceAttempted,
                        _guardianRoomAdvanceInFlight, _guardianRoomAdvanceFreshRetryAvailable))
                    await StartGuardianRoomAdvanceAsync();
                return;
            }
            if (IsCurrentNode(ExtractionNodeId))
            {
                if (_extractionRetryInFlight) return;
                if (_extractionSettlement == null && !string.IsNullOrEmpty(_extractionAttachError))
                {
                    _extractionAttachAttempted = false;
                    _extractionAttachError = string.Empty;
                    return;
                }
                if (_extractionSettlement == null) return;
                F10ExtractionSettlementState state = _extractionSettlement.State;
                if (state != F10ExtractionSettlementState.SetupFailed &&
                    state != F10ExtractionSettlementState.PendingRetry &&
                    state != F10ExtractionSettlementState.Failed) return;

                _extractionRetryInFlight = true;
                try { await _extractionSettlement.RetryAsync(); }
                catch (Exception exception)
                {
                    if (this != null) _extractionAttachError = exception.Message;
                }
                finally
                {
                    if (this != null) _extractionRetryInFlight = false;
                }
                return;
            }
            if (_tutorialAdvance == null) return;
            try { await _tutorialAdvance.RetryAsync(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void UpdateC1Encounter()
        {
            if (_c1Encounter == null && !_c1AttachAttempted && _ownedApplication != null &&
                !_ownedApplication.HasPendingSessionMutation && !_ownedApplication.IsRoomAdvanceInFlight)
            {
                _c1AttachAttempted = true;
                try { _c1Encounter = F10C1RuntimeCoordinator.Attach(_ownedApplication, Composition, _encounterProvider); }
                catch (Exception exception) { _c1AttachError = exception.Message; }
            }

            _tutorialRetry.gameObject.SetActive(_c1Encounter == null && !string.IsNullOrEmpty(_c1AttachError));
            if (!string.IsNullOrEmpty(_c1AttachError))
                _tutorialStatus.text = "NO SE PUDO INICIAR EL COMBATE · " + _c1AttachError;
            else if (_c1Encounter == null)
                _tutorialStatus.text = "Preparando combate…";
            else if (_c1Encounter.IsSetupFailed || _c1Encounter.RuntimeStatus == RoomEncounterRuntimeStatus.Faulted)
                _tutorialStatus.text = "NO SE PUDO INICIAR EL COMBATE · " + _c1Encounter.StatusMessage;
            else if (_c1Encounter.HasPhysicalClearObservation &&
                _c1Encounter.RuntimeStatus == RoomEncounterRuntimeStatus.Cleared)
            {
                if (!_c1RoomAdvanceAttempted && !_c1RoomAdvanceInFlight &&
                    CanStartC1RoomAdvanceAttempt(true, _c1Encounter.RuntimeStatus,
                        _c1RoomAdvanceAttempted, _c1RoomAdvanceInFlight, false))
                    _ = StartC1RoomAdvanceAsync();
                UpdateC1RoomAdvanceStatus();
            }
            else if (_c1Encounter.HasPhysicalClearObservation)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Despeje físico observado · esperando cierre del runtime";
            }
            else if (_c1Encounter.RuntimeStatus == RoomEncounterRuntimeStatus.Armed)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Derrota a los enemigos · oleada " + _c1Encounter.CurrentWaveNumber;
            }
            else
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Preparando combate…";
            }
        }

        private async System.Threading.Tasks.Task StartC1RoomAdvanceAsync()
        {
            if (_c1Encounter == null || _ownedApplication == null || Composition == null ||
                !CanStartC1RoomAdvanceAttempt(_c1Encounter.HasPhysicalClearObservation,
                    _c1Encounter.RuntimeStatus, _c1RoomAdvanceAttempted,
                    _c1RoomAdvanceInFlight, _c1RoomAdvanceFreshRetryAvailable))
                return;

            ApplicationBootstrapper application = _ownedApplication;
            RoomEncounterRuntime runtime = _c1Encounter.Runtime;
            RoomEncounterClearObservation clearObservation = _c1Encounter.PhysicalClearObservation;
            if (runtime == null || clearObservation == null || runtime.Status != RoomEncounterRuntimeStatus.Cleared)
            {
                _c1RoomAdvanceFreshRetryAvailable = false;
                _c1RoomAdvanceMessage = "El runtime ya no conserva el despeje físico exacto de C1.";
                UpdateC1RoomAdvanceStatus();
                return;
            }

            _c1RoomAdvanceAttempted = true;
            _c1RoomAdvanceInFlight = true;
            _c1RoomAdvancePending = false;
            _c1RoomAdvanceFreshRetryAvailable = false;
            _c1RoomAdvanceMessage = "Guardando recompensas y avance C1 → E1…";
            UpdateC1RoomAdvanceStatus();
            try
            {
                RoomAdvanceCommitResult result = await application.CompleteAndAdvanceF10CombatAsync(
                    Composition, runtime, clearObservation);
                if (this == null) return;
                ApplyC1RoomAdvanceResult(result, allowFreshRetry: true);
            }
            catch (Exception exception)
            {
                if (this == null) return;
                _c1RoomAdvancePending = application.HasPendingRoomAdvance;
                _c1RoomAdvanceFreshRetryAvailable = false;
                _c1RoomAdvanceMessage = _c1RoomAdvancePending
                    ? "El avance preparado sigue pendiente · " + exception.Message
                    : "No se pudo confirmar el resultado del avance · " + exception.Message;
            }
            finally
            {
                if (this != null)
                {
                    _c1RoomAdvanceInFlight = false;
                    UpdateC1RoomAdvanceStatus();
                }
            }
        }

        private async System.Threading.Tasks.Task RetryPendingC1RoomAdvanceAsync()
        {
            if (_c1RoomAdvanceInFlight || !_c1RoomAdvancePending || _ownedApplication == null) return;
            ApplicationBootstrapper application = _ownedApplication;
            _c1RoomAdvanceInFlight = true;
            _c1RoomAdvanceMessage = "Reintentando el avance C1 → E1 ya preparado…";
            UpdateC1RoomAdvanceStatus();
            try
            {
                RoomAdvanceCommitResult result = await application.RetryPendingRoomAdvanceAsync();
                if (this == null) return;
                ApplyC1RoomAdvanceResult(result, allowFreshRetry: false);
            }
            catch (Exception exception)
            {
                if (this == null) return;
                _c1RoomAdvancePending = application.HasPendingRoomAdvance;
                _c1RoomAdvanceFreshRetryAvailable = false;
                _c1RoomAdvanceMessage = "El reintento del avance preparado falló · " + exception.Message;
            }
            finally
            {
                if (this != null)
                {
                    _c1RoomAdvanceInFlight = false;
                    UpdateC1RoomAdvanceStatus();
                }
            }
        }

        private void ApplyC1RoomAdvanceResult(RoomAdvanceCommitResult result, bool allowFreshRetry)
        {
            _c1RoomAdvancePending = IsC1PendingAdvanceResult(result);
            _c1RoomAdvanceCompleted = result != null && result.Succeeded && !result.Pending && result.Activated;
            _c1RoomAdvanceFreshRetryAvailable = allowFreshRetry && result != null &&
                !result.Succeeded && !result.Pending;

            if (result == null)
                _c1RoomAdvanceMessage = "El avance C1 → E1 no devolvió resultado.";
            else if (result.Pending)
                _c1RoomAdvanceMessage = "Avance C1 → E1 pendiente · " + result.Message;
            else if (result.Succeeded && result.Activated)
                _c1RoomAdvanceMessage = "Recompensa de C1 guardada · E1 activo";
            else if (result.Succeeded)
                _c1RoomAdvanceMessage = "Ruta C1 → E1 guardada, pero E1 no quedó activo · " + result.Message;
            else
                _c1RoomAdvanceMessage = "No se pudo avanzar de C1 a E1 · " + result.Message;
        }

        private void UpdateC1RoomAdvanceStatus()
        {
            if (_tutorialStatus == null || _tutorialRetry == null) return;
            _tutorialRetry.gameObject.SetActive(!_c1RoomAdvanceInFlight &&
                (_c1RoomAdvancePending || _c1RoomAdvanceFreshRetryAvailable));
            _tutorialStatus.text = _c1RoomAdvanceMessage;
        }

        internal static bool CanStartC1RoomAdvanceAttempt(bool hasPhysicalClearObservation,
            RoomEncounterRuntimeStatus runtimeStatus, bool attempted, bool inFlight,
            bool freshRetryAvailable)
        {
            return hasPhysicalClearObservation && runtimeStatus == RoomEncounterRuntimeStatus.Cleared &&
                !inFlight && (!attempted || freshRetryAvailable);
        }

        internal static bool IsC1PendingAdvanceResult(RoomAdvanceCommitResult result) =>
            result != null && result.Pending;

        private void UpdateGuardianEncounter()
        {
            if (_guardianEncounter == null && !_guardianAttachAttempted && _ownedApplication != null &&
                Composition != null && Composition.isActiveAndEnabled && Composition.IsReady &&
                !_ownedApplication.HasPendingSessionMutation && !_ownedApplication.IsRoomAdvanceInFlight &&
                !_ownedApplication.HasPendingRoomAdvance)
            {
                _guardianAttachAttempted = true;
                try
                {
                    _guardianEncounter = F10GuardianRuntimeCoordinator.Attach(
                        _ownedApplication, Composition, _encounterProvider);
                }
                catch (Exception exception)
                {
                    _guardianAttachError = exception.Message;
                }
            }

            _tutorialRetry.gameObject.SetActive(_guardianEncounter == null &&
                !string.IsNullOrEmpty(_guardianAttachError));
            if (!string.IsNullOrEmpty(_guardianAttachError))
            {
                _tutorialStatus.text = GuardianRouteStatus("NO SE PUDO PREPARAR EL GUARDIÁN · " + _guardianAttachError);
                return;
            }
            if (_guardianEncounter == null)
            {
                _tutorialStatus.text = GuardianRouteStatus("Esperando preparación y readiness del Guardián…");
                return;
            }
            if (_guardianEncounter.IsSetupFailed || _guardianEncounter.RuntimeStatus == RoomEncounterRuntimeStatus.Faulted ||
                _guardianEncounter.RuntimeStatus == RoomEncounterRuntimeStatus.SessionEnded ||
                _guardianEncounter.RuntimeStatus == RoomEncounterRuntimeStatus.Disposed)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = GuardianRouteStatus("NO SE PUDO INICIAR EL GUARDIÁN · " + _guardianEncounter.StatusMessage);
                return;
            }
            if (_guardianEncounter.HasPhysicalClearObservation &&
                _guardianEncounter.RuntimeStatus == RoomEncounterRuntimeStatus.Cleared)
            {
                if (!_guardianRoomAdvanceAttempted && !_guardianRoomAdvanceInFlight &&
                    CanStartGuardianRoomAdvanceAttempt(true, _guardianEncounter.RuntimeStatus,
                        _guardianRoomAdvanceAttempted, _guardianRoomAdvanceInFlight, false))
                    _ = StartGuardianRoomAdvanceAsync();
                UpdateGuardianRoomAdvanceStatus();
                return;
            }
            _tutorialRetry.gameObject.SetActive(false);
            if (_guardianEncounter.HasPhysicalClearObservation)
                _tutorialStatus.text = GuardianRouteStatus("Despeje físico observado · esperando cierre del runtime del Guardián");
            else if (_guardianEncounter.RuntimeStatus == RoomEncounterRuntimeStatus.Armed)
                _tutorialStatus.text = GuardianRouteStatus(_guardianEncounter.StatusMessage);
            else
                _tutorialStatus.text = GuardianRouteStatus(_guardianEncounter.StatusMessage);
        }

        private async System.Threading.Tasks.Task StartGuardianRoomAdvanceAsync()
        {
            if (_guardianEncounter == null || _ownedApplication == null || Composition == null ||
                !CanStartGuardianRoomAdvanceAttempt(_guardianEncounter.HasPhysicalClearObservation,
                    _guardianEncounter.RuntimeStatus, _guardianRoomAdvanceAttempted,
                    _guardianRoomAdvanceInFlight, _guardianRoomAdvanceFreshRetryAvailable))
                return;

            ApplicationBootstrapper application = _ownedApplication;
            RoomEncounterRuntime runtime = _guardianEncounter.Runtime;
            RoomEncounterClearObservation clearObservation = _guardianEncounter.PhysicalClearObservation;
            if (runtime == null || clearObservation == null || runtime.Status != RoomEncounterRuntimeStatus.Cleared)
            {
                _guardianRoomAdvanceFreshRetryAvailable = false;
                _guardianRoomAdvanceMessage = "El runtime ya no conserva el despeje físico exacto del Custodio.";
                UpdateGuardianRoomAdvanceStatus();
                return;
            }

            _guardianRoomAdvanceAttempted = true;
            _guardianRoomAdvanceInFlight = true;
            _guardianRoomAdvancePending = false;
            _guardianRoomAdvanceFreshRetryAvailable = false;
            _guardianRoomAdvanceMessage = "Guardando la recompensa y avance del Guardián…";
            UpdateGuardianRoomAdvanceStatus();
            try
            {
                RoomAdvanceCommitResult result = await application.CompleteAndAdvanceF10GuardianAsync(
                    Composition, runtime, clearObservation);
                if (this == null) return;
                ApplyGuardianRoomAdvanceResult(result, allowFreshRetry: true);
            }
            catch (Exception exception)
            {
                if (this == null) return;
                _guardianRoomAdvancePending = application.HasPendingRoomAdvance;
                _guardianRoomAdvanceFreshRetryAvailable = false;
                _guardianRoomAdvanceMessage = _guardianRoomAdvancePending
                    ? "El avance preparado del Guardián sigue pendiente · " + exception.Message
                    : "No se pudo confirmar el resultado del Guardián · " + exception.Message;
            }
            finally
            {
                if (this != null)
                {
                    _guardianRoomAdvanceInFlight = false;
                    UpdateGuardianRoomAdvanceStatus();
                }
            }
        }

        private async System.Threading.Tasks.Task RetryPendingGuardianRoomAdvanceAsync()
        {
            if (_guardianRoomAdvanceInFlight || !_guardianRoomAdvancePending || _ownedApplication == null) return;
            ApplicationBootstrapper application = _ownedApplication;
            _guardianRoomAdvanceInFlight = true;
            _guardianRoomAdvanceMessage = "Reintentando el avance del Guardián ya preparado…";
            UpdateGuardianRoomAdvanceStatus();
            try
            {
                RoomAdvanceCommitResult result = await application.RetryPendingRoomAdvanceAsync();
                if (this == null) return;
                ApplyGuardianRoomAdvanceResult(result, allowFreshRetry: false);
            }
            catch (Exception exception)
            {
                if (this == null) return;
                _guardianRoomAdvancePending = application.HasPendingRoomAdvance;
                _guardianRoomAdvanceFreshRetryAvailable = false;
                _guardianRoomAdvanceMessage = "El reintento del avance preparado del Guardián falló · " + exception.Message;
            }
            finally
            {
                if (this != null)
                {
                    _guardianRoomAdvanceInFlight = false;
                    UpdateGuardianRoomAdvanceStatus();
                }
            }
        }

        private void ApplyGuardianRoomAdvanceResult(RoomAdvanceCommitResult result, bool allowFreshRetry)
        {
            _guardianRoomAdvancePending = result != null && result.Pending;
            _guardianRoomAdvanceCompleted = result != null && result.Succeeded &&
                !result.Pending && result.Activated;
            _guardianRoomAdvanceFreshRetryAvailable = allowFreshRetry && result != null &&
                !result.Succeeded && !result.Pending;

            if (result == null)
                _guardianRoomAdvanceMessage = "El avance del Guardián no devolvió resultado.";
            else if (result.Pending)
                _guardianRoomAdvanceMessage = "Avance del Guardián pendiente · " + result.Message;
            else if (result.Succeeded && result.Activated)
                _guardianRoomAdvanceMessage = "Custodio derrotado y guardado · extracción pendiente";
            else if (result.Succeeded)
                _guardianRoomAdvanceMessage = "Resultado del Guardián guardado, pero la salida no quedó activa · " + result.Message;
            else
                _guardianRoomAdvanceMessage = "No se pudo avanzar desde el Guardián · " + result.Message;
        }

        private void UpdateGuardianRoomAdvanceStatus()
        {
            if (_tutorialStatus == null || _tutorialRetry == null) return;
            _tutorialRetry.gameObject.SetActive(!_guardianRoomAdvanceInFlight &&
                (_guardianRoomAdvancePending || _guardianRoomAdvanceFreshRetryAvailable));
            _tutorialStatus.text = GuardianRouteStatus(_guardianRoomAdvanceMessage);
        }

        private string GuardianRouteStatus(string status)
        {
            ApplicationBootstrapper application = _ownedApplication;
            ExpeditionSaveData saved = application == null ? null : application.ActiveExpedition;
            bool e1Committed = application != null &&
                application.OwnsF10DiagnosticRun(application.ActiveRun) && saved != null &&
                string.Equals(saved.currentNodeId, F10TechnicalEncounterPlanProvider.GuardianNodeId, StringComparison.Ordinal) &&
                saved.completedRoomIds != null && saved.completedRoomIds.Contains(ExplorationNodeId, StringComparer.Ordinal);
            return e1Committed ? "Salida de E1 guardada · " + status : status;
        }

        private bool HasSavedGuardianResultAtExtractionCursor()
        {
            ApplicationBootstrapper application = _ownedApplication;
            ExpeditionRunConfiguration run = application == null ? null : application.ActiveRun;
            ExpeditionSaveData saved = application == null ? null : application.ActiveExpedition;
            ExpeditionRoomTransitionController transitions = Composition == null ? null : Composition.RoomTransitions;
            if (application == null || run == null || saved == null || transitions == null ||
                !application.OwnsF10DiagnosticRun(run) || application.HasPendingSessionMutation ||
                application.IsRoomAdvanceInFlight || application.HasPendingRoomAdvance ||
                !string.Equals(saved.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(saved.currentNodeId, ExtractionNodeId, StringComparison.Ordinal) ||
                !string.Equals(transitions.CurrentNodeId, ExtractionNodeId, StringComparison.Ordinal) ||
                transitions.CurrentRoomId != application.ActiveRoomInstanceId ||
                saved.completedRoomIds == null ||
                !saved.completedRoomIds.Contains(F10TechnicalEncounterPlanProvider.GuardianNodeId, StringComparer.Ordinal) ||
                saved.visitedNodeIds == null || saved.visitedNodeIds.Length < 2 ||
                !string.Equals(saved.visitedNodeIds[saved.visitedNodeIds.Length - 2],
                    F10TechnicalEncounterPlanProvider.GuardianNodeId, StringComparison.Ordinal) ||
                !string.Equals(saved.visitedNodeIds[saved.visitedNodeIds.Length - 1], ExtractionNodeId, StringComparison.Ordinal) ||
                saved.completedRoomResults == null)
                return false;

            string roomId = ExpeditionRoomIdentity.ForNode(run.RunId,
                F10TechnicalEncounterPlanProvider.GuardianNodeId).ToString();
            string expectedResultId = "room-result.v1:" + roomId;
            string expectedResultJson = "{\"schema\":1,\"outcome\":\"cleared\",\"roomInstanceId\":\"" + roomId +
                "\",\"sourceNodeId\":\"" + F10TechnicalEncounterPlanProvider.GuardianNodeId +
                "\",\"targetNodeId\":\"" + ExtractionNodeId + "\",\"technicalPayment\":\"f10-room.v1\"}";
            return saved.completedRoomResults.Any(result => result != null &&
                string.Equals(result.roomNodeId, F10TechnicalEncounterPlanProvider.GuardianNodeId, StringComparison.Ordinal) &&
                string.Equals(result.resultId, expectedResultId, StringComparison.Ordinal) &&
                string.Equals(result.resultJson, expectedResultJson, StringComparison.Ordinal));
        }

        private void UpdateExtractionSettlement()
        {
            if (_ownedApplication == null || Composition == null)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Esperando el propietario guardado de extracción…";
                return;
            }

            if (_extractionSettlement == null && !_extractionAttachAttempted)
            {
                if (!HasSavedGuardianResultAtExtractionCursor())
                {
                    _tutorialRetry.gameObject.SetActive(false);
                    _tutorialStatus.text = "Esperando resultado durable del Guardián y perfil libre…";
                    return;
                }

                _extractionAttachAttempted = true;
                _extractionRunId = _ownedApplication.ActiveRun == null
                    ? string.Empty
                    : _ownedApplication.ActiveRun.RunId.ToString();
                try
                {
                    _extractionSettlement = F10ExtractionSettlementCoordinator.Attach(
                        _ownedApplication, Composition);
                    if (_extractionSettlement == null)
                        throw new InvalidOperationException("The F10 extraction coordinator did not attach.");
                    _extractionCoordinatorAttached = true;
                    _extractionAttachError = string.Empty;
                }
                catch (Exception exception)
                {
                    _extractionAttachError = exception.Message;
                }
            }

            if (_extractionSettlement == null)
            {
                bool retryableAttach = !string.IsNullOrEmpty(_extractionAttachError);
                _tutorialRetry.gameObject.SetActive(retryableAttach);
                _tutorialStatus.text = retryableAttach
                    ? "NO SE PUDO PREPARAR LA EXTRACCIÓN · " + _extractionAttachError
                    : "Esperando resultado durable del Guardián…";
                return;
            }

            F10ExtractionSettlementState state = _extractionSettlement.State;
            bool retryable = state == F10ExtractionSettlementState.SetupFailed ||
                state == F10ExtractionSettlementState.PendingRetry ||
                state == F10ExtractionSettlementState.Failed;
            _tutorialRetry.gameObject.SetActive(retryable && !_extractionRetryInFlight);
            switch (state)
            {
                case F10ExtractionSettlementState.SetupFailed:
                    _tutorialStatus.text = "NO SE PUDO PREPARAR LA EXTRACCIÓN · " + _extractionSettlement.Error;
                    break;
                case F10ExtractionSettlementState.WaitingForExtraction:
                    _tutorialStatus.text = "Usa la interacción real en la salida segura";
                    break;
                case F10ExtractionSettlementState.Resolving:
                    _tutorialRetry.gameObject.SetActive(false);
                    _tutorialStatus.text = "Confirmando extracción y guardado…";
                    break;
                case F10ExtractionSettlementState.PendingRetry:
                    _tutorialStatus.text = "Extracción pendiente · " + _extractionSettlement.Error;
                    break;
                case F10ExtractionSettlementState.Failed:
                    _tutorialStatus.text = "No se pudo completar la extracción · " + _extractionSettlement.Error;
                    break;
                case F10ExtractionSettlementState.Completed:
                    _tutorialRetry.gameObject.SetActive(false);
                    if (!TryShowDurableExtractionResult())
                        _tutorialStatus.text = "Extracción confirmada; verificando el perfil durable…";
                    break;
                default:
                    _tutorialRetry.gameObject.SetActive(false);
                    _tutorialStatus.text = "Preparando la salida segura…";
                    break;
            }
        }

        private bool TryShowDurableExtractionResult()
        {
            ApplicationBootstrapper application = _ownedApplication;
            if (!_extractionCoordinatorAttached || string.IsNullOrWhiteSpace(_extractionRunId) ||
                application == null || application.ActiveRun != null || application.HasActiveExpedition ||
                application.HasPendingSessionMutation || application.HasPendingSettlement ||
                application.SettlementLedger == null)
                return false;

            ProfileSaveData profile = application.Profile;
            if (profile == null || profile.processedSettlementRunIds == null ||
                !profile.processedSettlementRunIds.Contains(_extractionRunId, StringComparer.Ordinal) ||
                !application.SettlementLedger.ProcessedRunIds.Any(runId =>
                    string.Equals(runId.ToString(), _extractionRunId, StringComparison.Ordinal)))
                return false;

            _tutorialRetry.gameObject.SetActive(false);
            _tutorialStatus.text = "Extracción confirmada · Renombre disponible: " + profile.availableRenown;
            return true;
        }

        internal static bool CanStartGuardianRoomAdvanceAttempt(bool hasPhysicalClearObservation,
            RoomEncounterRuntimeStatus runtimeStatus, bool attempted, bool inFlight,
            bool freshRetryAvailable)
        {
            return hasPhysicalClearObservation && runtimeStatus == RoomEncounterRuntimeStatus.Cleared &&
                !inFlight && (!attempted || freshRetryAvailable);
        }

        private void UpdateE1Exploration()
        {
            if (_c1RoomAdvanceInFlight)
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Esperando la publicación guardada de E1…";
                return;
            }

            if (_explorationAdvance == null && (_ownedApplication == null || Composition == null))
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Esperando el propietario guardado de E1…";
                return;
            }

            if (_explorationAdvance == null && (_ownedApplication.HasPendingSessionMutation ||
                _ownedApplication.IsRoomAdvanceInFlight || _ownedApplication.HasPendingRoomAdvance))
            {
                _tutorialRetry.gameObject.SetActive(false);
                _tutorialStatus.text = "Esperando la publicación guardada de E1…";
                return;
            }

            if (_explorationAdvance == null && !_explorationAttachAttempted)
            {
                _explorationAttachAttempted = true;
                try
                {
                    _explorationAdvance = F10ExplorationAdvanceCoordinator.Attach(
                        _ownedApplication, Composition, _technicalGoldCachePrefab);
                }
                catch (Exception exception)
                {
                    _explorationAttachError = exception.Message;
                }
            }

            if (_explorationAdvance == null)
            {
                _tutorialRetry.gameObject.SetActive(!string.IsNullOrEmpty(_explorationAttachError));
                _tutorialStatus.text = !string.IsNullOrEmpty(_explorationAttachError)
                    ? "NO SE PUDO PREPARAR E1 · " + _explorationAttachError
                    : "Preparando exploración E1…";
                return;
            }

            F10ExplorationAdvanceState state = _explorationAdvance.State;
            F10ExplorationGoldCacheBinding2D cache = _explorationAdvance.CacheBinding;
            bool cacheRetry = cache != null && cache.CanRetry;
            bool retryable = cacheRetry || state == F10ExplorationAdvanceState.SetupFailed ||
                state == F10ExplorationAdvanceState.WaitingForSessionMutation ||
                state == F10ExplorationAdvanceState.PendingRetry || state == F10ExplorationAdvanceState.Failed ||
                state == F10ExplorationAdvanceState.WaitingForCacheSettlement;
            _tutorialRetry.gameObject.SetActive(retryable && !_explorationRetryInFlight);

            if (cache != null && cache.HasRetainedContact)
            {
                _tutorialStatus.text = cache.IsRequestInFlight
                    ? "Guardando el caché físico de E1…"
                    : cacheRetry
                        ? "Caché físico pendiente · " + cache.LastError
                        : "Caché físico retenido · " + cache.LastError;
                return;
            }

            switch (state)
            {
                case F10ExplorationAdvanceState.SetupFailed:
                    _tutorialStatus.text = "NO SE PUDO PREPARAR E1 · " + _explorationAdvance.Error;
                    break;
                case F10ExplorationAdvanceState.WaitingForExplorationExit:
                    _tutorialStatus.text = "Explora E1 y alcanza su salida por el recorrido";
                    break;
                case F10ExplorationAdvanceState.WaitingForCacheSettlement:
                    _tutorialStatus.text = "Esperando que termine la operación del caché · " + _explorationAdvance.Error;
                    break;
                case F10ExplorationAdvanceState.WaitingForSessionMutation:
                    _tutorialStatus.text = "Esperando una mutación guardada antes del avance E1 · " + _explorationAdvance.Error;
                    break;
                case F10ExplorationAdvanceState.Resolving:
                    _tutorialRetry.gameObject.SetActive(false);
                    _tutorialStatus.text = "Guardando la salida observada de E1…";
                    break;
                case F10ExplorationAdvanceState.PendingRetry:
                    _tutorialStatus.text = "Avance E1 pendiente · " + _explorationAdvance.Error;
                    break;
                case F10ExplorationAdvanceState.Completed:
                    _explorationAdvanceCompleted = true;
                    _tutorialRetry.gameObject.SetActive(false);
                    _tutorialStatus.text = "Salida de E1 guardada · " + _explorationAdvance.TargetNodeId;
                    break;
                case F10ExplorationAdvanceState.Failed:
                    _tutorialStatus.text = "No se pudo completar E1 · " + _explorationAdvance.Error;
                    break;
                default:
                    _tutorialRetry.gameObject.SetActive(false);
                    _tutorialStatus.text = "No se pudo completar E1 · " + _explorationAdvance.Error;
                    break;
            }
        }

        private bool IsCurrentNode(string nodeId) => Composition != null && Composition.RoomTransitions != null &&
            string.Equals(Composition.RoomTransitions.CurrentNodeId, nodeId, StringComparison.Ordinal);

        private void AttachCoordinatorForDurableCursor(ApplicationBootstrapper application,
            ExpeditionGameplayComposition composition)
        {
            string currentNodeId = composition?.RoomTransitions?.CurrentNodeId;
            if (string.Equals(currentNodeId, TutorialNodeId, StringComparison.Ordinal))
            {
                _tutorialAdvance = F10TutorialAdvanceCoordinator.Attach(application, composition);
                return;
            }
            if (string.Equals(currentNodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                string.Equals(currentNodeId, ExplorationNodeId, StringComparison.Ordinal) ||
                string.Equals(currentNodeId, F10TechnicalEncounterPlanProvider.GuardianNodeId, StringComparison.Ordinal) ||
                string.Equals(currentNodeId, ExtractionNodeId, StringComparison.Ordinal))
                return; // Update attaches the coordinator that owns this durable node.
            throw new InvalidOperationException("The durable route cursor is outside the fixed technical F10 path: " + currentNodeId);
        }

        private F10DiagnosticSessionMode ConsumeSessionMode()
        {
            if (_nextSessionModeForEditor.HasValue)
            {
                _sessionMode = _nextSessionModeForEditor.Value;
                _nextSessionModeForEditor = null;
            }
            return _sessionMode;
        }

        private static F10TechnicalEncounterPlanProvider FreezeEncounterProvider()
        {
            GameObject slime = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab");
            GameObject boneguard = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab");
            GameObject custodian = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab");
            GameObject wave = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab");
            TextAsset c08 = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/C08Arena.json");
            TextAsset boss = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json");
            if (!F10TechnicalEncounterPlanProvider.TryFreeze(slime, boneguard, custodian, wave, c08, boss,
                    out F10TechnicalEncounterPlanProvider provider, out string error))
                throw new InvalidOperationException("No se pudo preparar el catálogo técnico de combate: " + error);
            return provider;
        }

        private void CleanupOwnedObjects()
        {
            if (_ownedGameplay != null) Destroy(_ownedGameplay);
            if (_ownedHud != null) Destroy(_ownedHud);
            if (_ownedCamera != null) Destroy(_ownedCamera);
            _ownedGameplay = null;
            _ownedHud = null;
            _ownedCamera = null;
            _tutorialAdvance = null;
            _encounterProvider = null;
            _c1Encounter = null;
            _explorationAdvance = null;
            _guardianEncounter = null;
            _tutorialStatus = null;
            _tutorialRetry = null;
            Composition = null;
        }

        private static void CleanupApplicationIfOwned(ApplicationBootstrapper application)
        {
            if (application == null || !ReferenceEquals(ApplicationBootstrapper.Instance, application) ||
                application.HasPendingSessionMutation || application.F10DiagnosticWitness == null ||
                !ReferenceEquals(application.F10DiagnosticWitness.Owner, application)) return;
            Destroy(application.gameObject);
        }

        private static void ValidateAssets(GameObject player, ExpeditionRoomPrefabEntry[] rooms, GameObject goldCache)
        {
            if (player == null || !string.Equals(player.name, "F10TechnicalDiagnosticPlayer", StringComparison.Ordinal) ||
                goldCache == null || rooms == null || rooms.Length != 5 ||
                rooms.Any(entry => entry == null || string.IsNullOrWhiteSpace(entry.templateId) || entry.prefab == null) ||
                rooms.Select(entry => entry.templateId).Distinct(StringComparer.Ordinal).Count() != 5)
                throw new InvalidOperationException("The F10 diagnostic scene requires its technical player, cache and five distinct rooms.");
        }

        private static Text CreateLabel(Transform parent, string name, Vector2 anchoredPosition)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(600f, 30f);
            Text label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.color = Color.white;
            return label;
        }
    }
}
#endif
