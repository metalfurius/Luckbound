using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;

namespace Luckbound.App
{
    /// <summary>
    /// Owns the application lifetime and routes a fresh launch to the main menu.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ApplicationBootstrapper : MonoBehaviour
    {
        private static ApplicationBootstrapper _instance;
        private ExpeditionSettlementLedger _settlementLedger = new ExpeditionSettlementLedger();
        private SaveRepository _saveRepository;
        private ProfileSaveData _profileData = ProfileSaveData.CreateNewProfile();
        private GameSettingsSaveData _settingsData = new GameSettingsSaveData();
        private PendingSettlement _pendingSettlement;
        private readonly SemaphoreSlim _profileMutationGate = new SemaphoreSlim(1, 1);
        private ExpeditionSaveData _activeExpedition;
        private long _activeExpeditionRevision;
        private ExpeditionSaveData _pendingExpeditionCandidate;
        private ExpeditionRunConfiguration _pendingExpeditionRun;
        private string _activeExpeditionLoadError = string.Empty;

        public static ApplicationBootstrapper Instance => _instance;
#if UNITY_EDITOR
        public static Func<ISaveStore> EditorSaveStoreFactoryForTests { get; set; }
#endif

        public ExpeditionRunConfiguration ActiveRun { get; private set; }
        public ExpeditionSaveData ActiveExpedition => _activeExpedition == null ? null : _activeExpedition.Clone();
        public string ActiveExpeditionLoadError => _activeExpeditionLoadError;
        public ExpeditionSettlementLedger SettlementLedger => _settlementLedger;
        public int AvailableRenown => _settlementLedger.AvailableRenown;
        public ProfileSaveData Profile => _profileData.Clone();
        public GameSettingsSaveData Settings => _settingsData.Clone();
        public bool SaveDataReady { get; private set; }
        public SaveLoadStatus ProfileLoadStatus { get; private set; } = SaveLoadStatus.Missing;
        public string ProfileLoadMessage { get; private set; } = string.Empty;
        public SaveLoadStatus SettingsLoadStatus { get; private set; } = SaveLoadStatus.Missing;
        public string SettingsLoadMessage { get; private set; } = string.Empty;
        public bool HasPendingSettlement => _pendingSettlement != null;
        public bool HasActiveExpedition => _activeExpedition != null;
        public bool HasPendingExpeditionWrite => _pendingExpeditionCandidate != null;
        public bool CanStartNewExpedition => SaveDataReady && IsUsableProfileStatus(ProfileLoadStatus) &&
            !HasPendingSettlement && !HasPendingExpeditionWrite && !HasActiveExpedition;
        public bool CanContinueExpedition => SaveDataReady && IsUsableProfileStatus(ProfileLoadStatus) &&
            !HasPendingSettlement && !HasPendingExpeditionWrite && HasActiveExpedition && string.IsNullOrEmpty(_activeExpeditionLoadError);
        public string PendingProfileExport => _saveRepository == null ? null : _saveRepository.ExportPendingJson(SaveDocument.Profile);

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            WebGlFileSystemSyncer syncer = GetComponent<WebGlFileSystemSyncer>();
            if (syncer == null) syncer = gameObject.AddComponent<WebGlFileSystemSyncer>();
            ISaveStore store = null;
#if UNITY_EDITOR
            if (EditorSaveStoreFactoryForTests != null) store = EditorSaveStoreFactoryForTests();
#endif
            _saveRepository = new SaveRepository(store ?? new FileSaveStore(Application.persistentDataPath, syncer));
        }

        private async void Start()
        {
            if (SceneManager.GetActiveScene().name == "Bootstrap")
            {
                try
                {
                    await LoadSaveDataAsync();
                }
                catch (Exception exception)
                {
                    ProfileLoadStatus = SaveLoadStatus.Corrupted;
                    SettingsLoadStatus = SaveLoadStatus.Corrupted;
                    ProfileLoadMessage = "Save initialization failed; existing files were preserved. " + exception.Message;
                    SettingsLoadMessage = ProfileLoadMessage;
                    Debug.LogError(ProfileLoadMessage);
                }
                finally
                {
                    SaveDataReady = true;
                }
                SceneManager.LoadScene(ActiveRun == null ? "Menu" : "Expedition", LoadSceneMode.Single);
            }
        }

        public async Task<SaveWriteResult> BeginExpeditionAsync(ExpeditionRunConfiguration run)
        {
            if (!CanStartNewExpedition)
            {
                string reason = HasPendingSettlement || HasPendingExpeditionWrite
                    ? "A profile write is waiting for a successful save. Retry or export it before starting another expedition."
                    : HasActiveExpedition
                        ? "An expedition is already saved. Continue or settle it before starting another."
                    : string.IsNullOrWhiteSpace(ProfileLoadMessage)
                        ? "The profile could not be loaded safely; no expedition was started."
                        : ProfileLoadMessage;
                throw new InvalidOperationException(reason);
            }
            if (run == null) throw new ArgumentNullException(nameof(run));
            ExpeditionSaveData candidate = ExpeditionSaveMapper.Create(run);
            await _profileMutationGate.WaitAsync();
            try
            {
                if (!CanStartNewExpedition) throw new InvalidOperationException("The profile changed before the expedition could be saved.");
                SaveWriteResult result = await _saveRepository.SaveProfileAsync(_profileData, candidate);
                if (result.Succeeded)
                {
                    _activeExpedition = candidate.Clone();
                    _activeExpeditionRevision++;
                    ActiveRun = run;
                }
                else if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
                {
                    _pendingExpeditionCandidate = candidate.Clone();
                    _pendingExpeditionRun = run;
                }
                return result;
            }
            finally { _profileMutationGate.Release(); }
        }

        public void ContinueActiveExpedition()
        {
            if (!CanContinueExpedition) throw new InvalidOperationException("There is no safely resumable expedition.");
            if (ActiveRun == null)
            {
                ExpeditionRunConfiguration restored = ExpeditionSaveMapper.Restore(_activeExpedition, out ExpeditionSaveData resumedState);
                _activeExpedition = resumedState;
                ActiveRun = restored;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene("Expedition", LoadSceneMode.Single);
        }

        public async Task<SaveWriteResult> RetryPendingExpeditionWriteAsync()
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingExpeditionCandidate == null)
                    return new SaveWriteResult(false, "There is no pending expedition write.");
                SaveWriteResult result = await _saveRepository.RetryPendingAsync(SaveDocument.Profile);
                if (result.Succeeded)
                {
                    _activeExpedition = _pendingExpeditionCandidate.Clone();
                    _activeExpeditionRevision++;
                    if (_pendingExpeditionRun != null) ActiveRun = _pendingExpeditionRun;
                    _pendingExpeditionCandidate = null;
                    _pendingExpeditionRun = null;
                }
                return result;
            }
            finally { _profileMutationGate.Release(); }
        }

        /// <summary>Commits a complete in-room snapshot while preserving the immutable room-entry rollback point.</summary>
        public Task<SaveWriteResult> SaveActiveExpeditionStateAsync(ExpeditionSaveData currentState)
        {
            if (_activeExpedition == null) throw new InvalidOperationException("There is no active expedition to update.");
            if (currentState == null) throw new ArgumentNullException(nameof(currentState));
            long expectedRevision = _activeExpeditionRevision;
            if (!SameExpeditionIdentity(_activeExpedition, currentState) ||
                !string.Equals(_activeExpedition.currentNodeId, currentState.currentNodeId, StringComparison.Ordinal) ||
                !string.Equals(_activeExpedition.roomEntryCheckpointJson, currentState.roomEntryCheckpointJson, StringComparison.Ordinal) ||
                !_activeExpedition.visitedNodeIds.SequenceEqual(currentState.visitedNodeIds) ||
                !_activeExpedition.completedRoomIds.SequenceEqual(currentState.completedRoomIds) ||
                !RoomResultsEqual(_activeExpedition.completedRoomResults, currentState.completedRoomResults))
                throw new InvalidOperationException("An in-room update cannot change route identity, completion history, or its entry checkpoint.");
            return SaveExpeditionCandidateAsync(currentState.Clone(), expectedRevision);
        }

        public async Task<SaveWriteResult> CompleteCurrentRoomAsync(string resultId, string resultJson)
        {
            if (_activeExpedition == null) throw new InvalidOperationException("There is no active expedition to complete a room for.");
            if (string.IsNullOrWhiteSpace(resultId) || string.IsNullOrWhiteSpace(resultJson))
                throw new ArgumentException("A stable result ID and serialized room result are required.");
            long expectedRevision = _activeExpeditionRevision;
            ExpeditionRoomCompletionSaveData existing = _activeExpedition.completedRoomResults
                .FirstOrDefault(result => string.Equals(result.roomNodeId, _activeExpedition.currentNodeId, StringComparison.Ordinal));
            if (existing != null)
                return string.Equals(existing.resultId, resultId, StringComparison.Ordinal) &&
                    string.Equals(existing.resultJson, resultJson, StringComparison.Ordinal)
                    ? new SaveWriteResult(true, "This room result was already committed.")
                    : new SaveWriteResult(false, "A different result is already committed for this room.");

            ExpeditionSaveData candidate = ExpeditionSaveMapper.CompleteRoom(_activeExpedition, resultId, resultJson);
            return await SaveExpeditionCandidateAsync(candidate, expectedRevision);
        }

        public async Task<SaveWriteResult> AdvanceToRoomAsync(string nextNodeId)
        {
            if (_activeExpedition == null) throw new InvalidOperationException("There is no active expedition to advance.");
            long expectedRevision = _activeExpeditionRevision;
            ExpeditionSaveData candidate;
            try { candidate = ExpeditionSaveMapper.AdvanceToRoom(_activeExpedition, nextNodeId); }
            catch (InvalidOperationException exception) { return new SaveWriteResult(false, exception.Message); }
            return await SaveExpeditionCandidateAsync(candidate, expectedRevision);
        }

        private async Task<SaveWriteResult> SaveExpeditionCandidateAsync(ExpeditionSaveData candidate, long expectedRevision)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_activeExpedition == null || !SameExpeditionIdentity(_activeExpedition, candidate))
                    return new SaveWriteResult(false, "The active expedition changed before this update could be saved.");
                if (_activeExpeditionRevision != expectedRevision)
                    return ExpeditionStatesEqual(_activeExpedition, candidate)
                        ? new SaveWriteResult(true, "This expedition snapshot was already committed.")
                        : new SaveWriteResult(false, "A newer expedition snapshot was committed before this update could be saved.");
                if (HasPendingSettlement)
                    return new SaveWriteResult(false, "A settlement is pending; expedition progress cannot be overwritten.");
                if (HasPendingExpeditionWrite)
                    return new SaveWriteResult(false, "An expedition write is already pending; retry that exact payload before making another change.");
                if (SaveDataValidation.Validate(candidate).Count != 0)
                    return new SaveWriteResult(false, string.Join(" ", SaveDataValidation.Validate(candidate)));
                SaveWriteResult result = await _saveRepository.SaveProfileAsync(_profileData, candidate);
                if (result.Succeeded)
                {
                    _activeExpedition = candidate.Clone();
                    _activeExpeditionRevision++;
                }
                else if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
                {
                    _pendingExpeditionCandidate = candidate.Clone();
                    _pendingExpeditionRun = ActiveRun;
                }
                return result;
            }
            finally { _profileMutationGate.Release(); }
        }

        private static bool SameExpeditionIdentity(ExpeditionSaveData left, ExpeditionSaveData right)
        {
            return left != null && right != null && string.Equals(left.runId, right.runId, StringComparison.Ordinal) &&
                left.seed == right.seed && left.generationVersion == right.generationVersion &&
                left.contentVersion == right.contentVersion && string.Equals(left.classId, right.classId, StringComparison.Ordinal) &&
                string.Equals(left.raceId, right.raceId, StringComparison.Ordinal) &&
                left.selectedSkillIds.SequenceEqual(right.selectedSkillIds) &&
                left.startingEquipmentInstanceIds.SequenceEqual(right.startingEquipmentInstanceIds) &&
                string.Equals(left.routeSnapshotJson, right.routeSnapshotJson, StringComparison.Ordinal);
        }

        private static bool ExpeditionStatesEqual(ExpeditionSaveData left, ExpeditionSaveData right)
        {
            return left != null && right != null &&
                string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
        }

        private static bool RoomResultsEqual(ExpeditionRoomCompletionSaveData[] left, ExpeditionRoomCompletionSaveData[] right)
        {
            if (left == null || right == null || left.Length != right.Length) return false;
            for (int index = 0; index < left.Length; index++)
            {
                if (left[index] == null || right[index] == null ||
                    !string.Equals(left[index].roomNodeId, right[index].roomNodeId, StringComparison.Ordinal) ||
                    !string.Equals(left[index].resultId, right[index].resultId, StringComparison.Ordinal) ||
                    !string.Equals(left[index].resultJson, right[index].resultJson, StringComparison.Ordinal)) return false;
            }
            return true;
        }

        public void ClearActiveRun()
        {
            ActiveRun = null;
        }

        public async Task<ExpeditionSettlementCommitResult> SettleActiveRunAsync(ExpeditionOutcome outcome, int accumulatedRenown)
        {
            await _profileMutationGate.WaitAsync();
            try { return await SettleActiveRunUnderGateAsync(outcome, accumulatedRenown); }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ExpeditionSettlementCommitResult> SettleActiveRunUnderGateAsync(ExpeditionOutcome outcome, int accumulatedRenown)
        {
            if (!SaveDataReady) throw new InvalidOperationException("Profile data has not finished loading.");
            if (!IsUsableProfileStatus(ProfileLoadStatus))
                throw new InvalidOperationException("The profile is unavailable; the expedition result was not changed or settled. " + ProfileLoadMessage);
            if (ActiveRun == null) throw new InvalidOperationException("There is no active expedition to settle.");
            if (HasPendingExpeditionWrite)
                throw new InvalidOperationException("The active room has a pending save. Retry or export it before settling the expedition.");
            if (_pendingSettlement != null)
            {
                bool sameRun = _pendingSettlement.Receipt.RunId == ActiveRun.RunId;
                return new ExpeditionSettlementCommitResult(sameRun ? _pendingSettlement.Receipt : null,
                    persisted: false, pending: true,
                    sameRun ? "The result is awaiting a persistence retry." : "Resolve the pending result before settling another run.");
            }

            var candidateLedger = new ExpeditionSettlementLedger(
                _settlementLedger.AvailableRenown, _settlementLedger.ProcessedRunIds);
            ExpeditionSettlementReceipt receipt = candidateLedger.Settle(ActiveRun.RunId, outcome,
                accumulatedRenown, ActiveRun.Inventory, ActiveRun.StartingEquipmentInstanceIds);
            if (receipt.AlreadyProcessed)
                return new ExpeditionSettlementCommitResult(receipt, persisted: true, pending: false,
                    "This RunId was already present in the loaded profile.");

            ProfileSaveData candidateProfile = _profileData.Clone();
            candidateProfile.availableRenown = candidateLedger.AvailableRenown;
            candidateProfile.processedSettlementRunIds = candidateLedger.ProcessedRunIds
                .Select(runId => runId.ToString()).ToArray();
            SaveWriteResult save = await _saveRepository.SaveProfileAsync(candidateProfile);
            if (save.Succeeded)
            {
                _settlementLedger = candidateLedger;
                _profileData = candidateProfile;
                _activeExpedition = null;
                _activeExpeditionRevision++;
                return new ExpeditionSettlementCommitResult(receipt, persisted: true, pending: false, save.Message);
            }

            if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
                _pendingSettlement = new PendingSettlement(candidateLedger, candidateProfile, receipt);
            return new ExpeditionSettlementCommitResult(receipt, persisted: false,
                pending: _pendingSettlement != null, save.Message);
        }

        public async Task<ExpeditionSettlementCommitResult> RetryPendingSettlementAsync()
        {
            await _profileMutationGate.WaitAsync();
            try { return await RetryPendingSettlementUnderGateAsync(); }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ExpeditionSettlementCommitResult> RetryPendingSettlementUnderGateAsync()
        {
            if (_pendingSettlement == null)
                return new ExpeditionSettlementCommitResult(null, persisted: false, pending: false, "There is no pending settlement.");
            SaveWriteResult save = await _saveRepository.RetryPendingAsync(SaveDocument.Profile);
            if (!save.Succeeded)
                return new ExpeditionSettlementCommitResult(_pendingSettlement.Receipt, persisted: false, pending: true, save.Message);
            _settlementLedger = _pendingSettlement.Ledger;
            _profileData = _pendingSettlement.Profile;
            _activeExpedition = null;
            _activeExpeditionRevision++;
            ExpeditionSettlementReceipt receipt = _pendingSettlement.Receipt;
            _pendingSettlement = null;
            return new ExpeditionSettlementCommitResult(receipt, persisted: true, pending: false, save.Message);
        }

        public async Task<SaveWriteResult> SaveSettingsAsync(GameSettingsSaveData settings)
        {
            if (!SaveDataReady) throw new InvalidOperationException("Settings have not finished loading.");
            SaveWriteResult result = await _saveRepository.SaveSettingsAsync(settings);
            if (result.Succeeded) _settingsData = settings.Clone();
            return result;
        }

        public Task<SaveWriteResult> RetryPendingSettingsAsync()
        {
            return _saveRepository.RetryPendingAsync(SaveDocument.Settings);
        }

        private async Task LoadSaveDataAsync()
        {
            try
            {
                SaveLoadResult<ProfileSaveEnvelope> profileResult = await _saveRepository.LoadProfileAsync();
                ProfileLoadStatus = profileResult.Status;
                ProfileLoadMessage = profileResult.Message;
                if (profileResult.HasUsableData)
                {
                    _profileData = profileResult.Data.profile.Clone();
                    _settlementLedger = new ExpeditionSettlementLedger(_profileData.availableRenown,
                        _profileData.processedSettlementRunIds.Select(RunId.Parse));
                    if (profileResult.Data.hasActiveExpedition && profileResult.Data.activeExpedition != null)
                    {
                        try
                        {
                            ActiveRun = ExpeditionSaveMapper.Restore(profileResult.Data.activeExpedition, out _activeExpedition);
                        }
                        catch (Exception exception)
                        {
                            _activeExpedition = profileResult.Data.activeExpedition.Clone();
                            _activeExpeditionLoadError = exception.Message;
                            ProfileLoadMessage = "The saved expedition was preserved but could not be resumed: " + exception.Message;
                            Debug.LogError(ProfileLoadMessage);
                        }
                    }
                }
                else if (profileResult.Status == SaveLoadStatus.Corrupted || profileResult.Status == SaveLoadStatus.Incompatible)
                {
                    Debug.LogError(ProfileLoadMessage);
                }
                if (profileResult.Status == SaveLoadStatus.RecoveredBackup) Debug.LogWarning(ProfileLoadMessage);
            }
            catch (Exception exception)
            {
                ProfileLoadStatus = SaveLoadStatus.Corrupted;
                ProfileLoadMessage = "Profile storage could not be read; existing files were preserved. " + exception.Message;
                Debug.LogError(ProfileLoadMessage);
            }

            try
            {
                SaveLoadResult<SettingsSaveEnvelope> settingsResult = await _saveRepository.LoadSettingsAsync();
                SettingsLoadStatus = settingsResult.Status;
                SettingsLoadMessage = settingsResult.Message;
                if (settingsResult.HasUsableData) _settingsData = settingsResult.Data.settings.Clone();
                if (settingsResult.Status == SaveLoadStatus.Corrupted || settingsResult.Status == SaveLoadStatus.RecoveredBackup)
                    Debug.LogWarning(SettingsLoadMessage);
                else if (settingsResult.Status == SaveLoadStatus.Incompatible) Debug.LogError(SettingsLoadMessage);
            }
            catch (Exception exception)
            {
                SettingsLoadStatus = SaveLoadStatus.Corrupted;
                SettingsLoadMessage = "Settings storage could not be read; existing files were preserved. " + exception.Message;
                Debug.LogWarning(SettingsLoadMessage);
            }
        }

        private static bool IsUsableProfileStatus(SaveLoadStatus status)
        {
            return status == SaveLoadStatus.Missing || status == SaveLoadStatus.LoadedPrimary ||
                status == SaveLoadStatus.RecoveredBackup;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private sealed class PendingSettlement
        {
            public PendingSettlement(ExpeditionSettlementLedger ledger, ProfileSaveData profile, ExpeditionSettlementReceipt receipt)
            {
                Ledger = ledger;
                Profile = profile;
                Receipt = receipt;
            }
            public ExpeditionSettlementLedger Ledger { get; }
            public ProfileSaveData Profile { get; }
            public ExpeditionSettlementReceipt Receipt { get; }
        }
    }
}
