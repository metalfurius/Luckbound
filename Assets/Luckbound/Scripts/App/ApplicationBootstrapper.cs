using System;
using System.Linq;
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

        public static ApplicationBootstrapper Instance => _instance;

        public ExpeditionRunConfiguration ActiveRun { get; private set; }
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
        public bool CanStartNewExpedition => SaveDataReady && IsUsableProfileStatus(ProfileLoadStatus) && !HasPendingSettlement;
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
            _saveRepository = new SaveRepository(new FileSaveStore(Application.persistentDataPath, syncer));
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
                SceneManager.LoadScene("Menu", LoadSceneMode.Single);
            }
        }

        public void BeginExpedition(ExpeditionRunConfiguration run)
        {
            if (!CanStartNewExpedition)
            {
                string reason = HasPendingSettlement
                    ? "A settlement is waiting for a successful save. Retry or export it before starting another expedition."
                    : string.IsNullOrWhiteSpace(ProfileLoadMessage)
                        ? "The profile could not be loaded safely; no expedition was started."
                        : ProfileLoadMessage;
                throw new InvalidOperationException(reason);
            }
            ActiveRun = run ?? throw new System.ArgumentNullException(nameof(run));
        }

        public void ClearActiveRun()
        {
            ActiveRun = null;
        }

        public async Task<ExpeditionSettlementCommitResult> SettleActiveRunAsync(ExpeditionOutcome outcome, int accumulatedRenown)
        {
            if (!SaveDataReady) throw new InvalidOperationException("Profile data has not finished loading.");
            if (!IsUsableProfileStatus(ProfileLoadStatus))
                throw new InvalidOperationException("The profile is unavailable; the expedition result was not changed or settled. " + ProfileLoadMessage);
            if (ActiveRun == null) throw new InvalidOperationException("There is no active expedition to settle.");
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
                return new ExpeditionSettlementCommitResult(receipt, persisted: true, pending: false, save.Message);
            }

            if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
                _pendingSettlement = new PendingSettlement(candidateLedger, candidateProfile, receipt);
            return new ExpeditionSettlementCommitResult(receipt, persisted: false,
                pending: _pendingSettlement != null, save.Message);
        }

        public async Task<ExpeditionSettlementCommitResult> RetryPendingSettlementAsync()
        {
            if (_pendingSettlement == null)
                return new ExpeditionSettlementCommitResult(null, persisted: false, pending: false, "There is no pending settlement.");
            SaveWriteResult save = await _saveRepository.RetryPendingAsync(SaveDocument.Profile);
            if (!save.Succeeded)
                return new ExpeditionSettlementCommitResult(_pendingSettlement.Receipt, persisted: false, pending: true, save.Message);
            _settlementLedger = _pendingSettlement.Ledger;
            _profileData = _pendingSettlement.Profile;
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
