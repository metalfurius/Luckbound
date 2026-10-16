using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Luckbound.Content;
using Luckbound.Core.Crafting;
using Luckbound.Core.Economy;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Progression;
using Luckbound.Core.Saves;
using Luckbound.Presentation.Audio;

namespace Luckbound.App
{
    /// <summary>
    /// Owns the application lifetime and routes a fresh launch to the main menu.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed partial class ApplicationBootstrapper : MonoBehaviour
    {
        private static ApplicationBootstrapper _instance;
        private ExpeditionSettlementLedger _settlementLedger = new ExpeditionSettlementLedger();
        private SaveRepository _saveRepository;
        private ProfileSaveData _profileData = ProfileSaveData.CreateNewProfile();
        private GameSettingsSaveData _settingsData = new GameSettingsSaveData();
        private PendingSettlement _pendingSettlement;
        private PendingPermanentUpgradePurchase _pendingPermanentUpgradePurchase;
        private PendingCraft _pendingCraft;
        private PendingEconomyMutation _pendingEconomyMutation;
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
        public bool HasPendingPermanentUpgradePurchase => _pendingPermanentUpgradePurchase != null;
        public bool HasPendingCraftingWrite => _pendingCraft != null;
        public bool HasPendingEconomyMutation => _pendingEconomyMutation != null;
        public string PendingEconomyOperationKey => _pendingEconomyMutation == null
            ? string.Empty
            : _pendingEconomyMutation.OperationKey;
        public PermanentUpgradeKind? PendingPermanentUpgradeKind => _pendingPermanentUpgradePurchase == null
            ? (PermanentUpgradeKind?)null
            : _pendingPermanentUpgradePurchase.Purchase.Kind;
        public bool HasActiveExpedition => _activeExpedition != null;
        public RoomInstanceId ActiveRoomInstanceId => ActiveRun == null || _activeExpedition == null
            ? default
            : ExpeditionRoomIdentity.ForNode(ActiveRun.RunId, _activeExpedition.currentNodeId);
        public bool HasPendingExpeditionWrite => _pendingExpeditionCandidate != null;
        public bool CanStartNewExpedition => SaveDataReady && IsUsableProfileStatus(ProfileLoadStatus) &&
            !HasPendingSettlement && !HasPendingPermanentUpgradePurchase && !HasPendingExpeditionWrite && !HasActiveExpedition;
        public bool CanContinueExpedition => SaveDataReady && IsUsableProfileStatus(ProfileLoadStatus) &&
            !HasPendingSettlement && !HasPendingExpeditionWrite && HasActiveExpedition && string.IsNullOrEmpty(_activeExpeditionLoadError);
        public string PendingProfileExport => _saveRepository == null ? null : _saveRepository.ExportPendingJson(SaveDocument.Profile);

        public async Task<ProgressionPurchaseCommitResult> PurchasePermanentUpgradeAsync(
            PermanentUpgradeKind kind, TransactionId transactionId)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingPermanentUpgradePurchase != null)
                {
                    if (_pendingPermanentUpgradePurchase.Purchase.TransactionId != transactionId ||
                        _pendingPermanentUpgradePurchase.Purchase.Kind != kind)
                    {
                        return new ProgressionPurchaseCommitResult(_pendingPermanentUpgradePurchase.Purchase,
                            persisted: false, pending: true,
                            "A different permanent-upgrade purchase is waiting for its exact save retry.");
                    }
                    return await RetryPendingPermanentUpgradeUnderGateAsync();
                }

                PermanentUpgradeSnapshot current = CreatePermanentUpgradeSnapshot(_profileData);
                bool inTavern = SceneManager.GetActiveScene().name == "Hub";
                if (!inTavern)
                {
                    PermanentUpgradePurchaseResult unavailable = PermanentUpgradeRules.PreparePurchase(
                        current, kind, transactionId, isInTavern: false);
                    return new ProgressionPurchaseCommitResult(unavailable, persisted: false, pending: false,
                        "Permanent upgrades can only be bought in the tavern.");
                }
                if (!SaveDataReady || !IsUsableProfileStatus(ProfileLoadStatus) || !CanStartNewExpedition)
                {
                    string reason = !SaveDataReady
                        ? "Profile data has not finished loading."
                        : !IsUsableProfileStatus(ProfileLoadStatus)
                            ? "The profile could not be loaded safely; its existing data is preserved."
                            : HasActiveExpedition
                                ? "Permanent upgrades cannot be bought while an expedition is active."
                                : "Resolve the pending profile write before buying another permanent upgrade.";
                    return new ProgressionPurchaseCommitResult(null, persisted: false, pending: false, reason);
                }

                PermanentUpgradePurchaseResult purchase = PermanentUpgradeRules.PreparePurchase(
                    current, kind, transactionId, inTavern);
                if (!purchase.ChangesProfile)
                {
                    bool repeated = purchase.Status == PermanentUpgradePurchaseStatus.AlreadyProcessed;
                    return new ProgressionPurchaseCommitResult(purchase, persisted: repeated, pending: false,
                        repeated ? "This transaction was already committed." : purchase.Status.ToString());
                }

                ProfileSaveData candidate = _profileData.Clone();
                ApplyPermanentUpgradeSnapshot(candidate, purchase.Snapshot);
                SaveWriteResult save = await _saveRepository.SaveProfileAsync(candidate);
                if (save.Succeeded)
                {
                    CommitPermanentUpgradeProfile(candidate);
                    return new ProgressionPurchaseCommitResult(purchase, persisted: true, pending: false, save.Message);
                }

                if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
                    _pendingPermanentUpgradePurchase = new PendingPermanentUpgradePurchase(candidate, purchase);
                return new ProgressionPurchaseCommitResult(purchase, persisted: false,
                    pending: _pendingPermanentUpgradePurchase != null, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        public async Task<ProgressionPurchaseCommitResult> RetryPendingPermanentUpgradePurchaseAsync()
        {
            await _profileMutationGate.WaitAsync();
            try { return await RetryPendingPermanentUpgradeUnderGateAsync(); }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ProgressionPurchaseCommitResult> RetryPendingPermanentUpgradeUnderGateAsync()
        {
            if (_pendingPermanentUpgradePurchase == null)
                return new ProgressionPurchaseCommitResult(null, persisted: false, pending: false,
                    "There is no pending permanent-upgrade purchase.");

            SaveWriteResult save = await _saveRepository.RetryPendingAsync(SaveDocument.Profile);
            if (!save.Succeeded)
                return new ProgressionPurchaseCommitResult(_pendingPermanentUpgradePurchase.Purchase,
                    persisted: false, pending: true, save.Message);

            PendingPermanentUpgradePurchase pending = _pendingPermanentUpgradePurchase;
            CommitPermanentUpgradeProfile(pending.Profile);
            _pendingPermanentUpgradePurchase = null;
            return new ProgressionPurchaseCommitResult(pending.Purchase, persisted: true, pending: false, save.Message);
        }

        private void CommitPermanentUpgradeProfile(ProfileSaveData profile)
        {
            _profileData = profile.Clone();
            _settlementLedger = new ExpeditionSettlementLedger(_profileData.availableRenown,
                _profileData.processedSettlementRunIds.Select(RunId.Parse));
        }

        private static PermanentUpgradeSnapshot CreatePermanentUpgradeSnapshot(ProfileSaveData profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            PermanentUpgradePurchaseSaveData[] savedTransactions = profile.progressionPurchaseTransactions ??
                Array.Empty<PermanentUpgradePurchaseSaveData>();
            PermanentUpgradePurchaseRecord[] records = savedTransactions.Select(transaction =>
            {
                if (transaction == null || !TransactionId.TryParse(transaction.transactionId, out TransactionId id) ||
                    !Enum.IsDefined(typeof(PermanentUpgradeKind), transaction.kind))
                    throw new FormatException("The saved permanent-upgrade transaction ledger is invalid.");
                return new PermanentUpgradePurchaseRecord(id, (PermanentUpgradeKind)transaction.kind, transaction.cost);
            }).ToArray();
            return new PermanentUpgradeSnapshot(profile.availableRenown, profile.healthUpgradeLevel,
                profile.energyUpgradeLevel, profile.additionalPotionUpgradeLevel, records);
        }

        private static void ApplyPermanentUpgradeSnapshot(ProfileSaveData profile, PermanentUpgradeSnapshot snapshot)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            profile.availableRenown = snapshot.AvailableRenown;
            profile.healthUpgradeLevel = snapshot.HealthUpgradeLevel;
            profile.energyUpgradeLevel = snapshot.EnergyUpgradeLevel;
            profile.additionalPotionUpgradeLevel = snapshot.AdditionalPotionUpgradeLevel;
            profile.progressionPurchaseTransactions = snapshot.PurchaseRecords.Select(record => new PermanentUpgradePurchaseSaveData
            {
                transactionId = record.TransactionId.ToString(),
                kind = (int)record.Kind,
                cost = record.Cost
            }).ToArray();
        }

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
                EnsureRuntimeAudio();
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

        private void EnsureRuntimeAudio()
        {
            if (GetComponent<RuntimeAudioDirector>() == null)
                gameObject.AddComponent<RuntimeAudioDirector>();
            if (GetComponent<RuntimeAmbienceDirector>() == null)
                gameObject.AddComponent<RuntimeAmbienceDirector>();
            if (GetComponent<ExpeditionRoomAmbienceRouter>() == null)
                gameObject.AddComponent<ExpeditionRoomAmbienceRouter>();
            if (GetComponent<ApplicationAudioRouter>() == null)
                gameObject.AddComponent<ApplicationAudioRouter>();
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
                if (_pendingEconomyMutation != null)
                    return new SaveWriteResult(false,
                        "Retry the exact pending merchant or rest-stop operation through its original action.");
                return await RetryPendingExpeditionWriteUnderGateAsync();
            }
            finally { _profileMutationGate.Release(); }
        }

        public async Task<CraftingCommitResult> CraftActiveExpeditionAsync(
            string recipeId, TransactionId transactionId, ItemInstanceId upgradeTargetInstanceId = default)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingCraft != null)
                {
                    if (_pendingCraft.TransactionId != transactionId ||
                        !string.Equals(_pendingCraft.RecipeId, recipeId, StringComparison.Ordinal) ||
                        _pendingCraft.UpgradeTargetInstanceId != upgradeTargetInstanceId)
                    {
                        return new CraftingCommitResult(_pendingCraft.Outcome, persisted: false, pending: true,
                            "A different crafting operation is waiting for its exact save retry.");
                    }
                    return await RetryPendingCraftingUnderGateAsync();
                }

                if (_pendingExpeditionCandidate != null)
                    return new CraftingCommitResult(null, persisted: false, pending: true,
                        "Retry the pending expedition write before starting another crafting operation.");
                if (!CanContinueExpedition || ActiveRun == null || _activeExpedition == null)
                    return new CraftingCommitResult(null, persisted: false, pending: false,
                        "There is no safely resumable active expedition to craft in.");

                RecipeCatalog recipeCatalog = RecipeCatalogJsonLoader.Load(
                    LoadRequired("Luckbound/Data/Items/Recipes").text, ActiveRun.Inventory.Catalog);
                ExpeditionInventory candidateInventory = ExpeditionInventory.Restore(
                    ActiveRun.Inventory.Catalog, ActiveRun.Inventory.CreateSnapshot());
                CraftingOutcome outcome = candidateInventory.TryCraft(
                    recipeCatalog, recipeId, transactionId, upgradeTargetInstanceId);
                ExpeditionRunConfiguration candidateRun = ActiveRun.WithInventory(candidateInventory);
                ExpeditionSaveData candidateSave = _activeExpedition.Clone();
                candidateSave.inventorySnapshotJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot());
                InventoryItemStack quickConsumable = candidateInventory.QuickConsumable;
                candidateSave.healthPotionCharges = quickConsumable == null ? 0 : quickConsumable.Quantity;

                SaveWriteResult save = await SaveExpeditionCandidateUnderGateAsync(
                    candidateSave, _activeExpeditionRevision, candidateRun);
                if (save.Succeeded)
                    return new CraftingCommitResult(outcome, persisted: true, pending: false, save.Message);

                if (HasPendingExpeditionWrite)
                {
                    _pendingCraft = new PendingCraft(recipeId, transactionId, upgradeTargetInstanceId, outcome);
                    return new CraftingCommitResult(outcome, persisted: false, pending: true, save.Message);
                }
                return new CraftingCommitResult(outcome, persisted: false, pending: false, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        public async Task<CraftingCommitResult> RetryPendingCraftingAsync()
        {
            await _profileMutationGate.WaitAsync();
            try { return await RetryPendingCraftingUnderGateAsync(); }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<CraftingCommitResult> RetryPendingCraftingUnderGateAsync()
        {
            if (_pendingCraft == null)
                return new CraftingCommitResult(null, persisted: false, pending: false,
                    "There is no pending crafting operation to retry.");

            PendingCraft pending = _pendingCraft;
            SaveWriteResult save = await RetryPendingExpeditionWriteUnderGateAsync();
            return new CraftingCommitResult(pending.Outcome, persisted: save.Succeeded,
                pending: !save.Succeeded && HasPendingExpeditionWrite, save.Message);
        }

        public MerchantStockSnapshot GetActiveMerchantStock()
        {
            // A failed write blocks new mutations, but the last committed expedition remains
            // the authoritative read model while the exact pending operation awaits retry.
            if (ActiveRun == null || _activeExpedition == null || !string.IsNullOrEmpty(_activeExpeditionLoadError))
                return null;
            return ActiveRun.Inventory.GetMerchantStock(ActiveRun.RunId, ActiveRoomInstanceId);
        }

        /// <summary>Persists fixed stock and the one-time entry recovery as one room-entry mutation.</summary>
        public Task<ExpeditionMutationCommitResult<RestStopEntryCommitData>> EnterRestStopAsync(RoomInstanceId roomInstanceId)
        {
            string operationKey = "rest.enter|" + roomInstanceId;
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: true,
                (run, inventory, candidateSave) =>
                {
                    MerchantStockSnapshot stock = MerchantService.EnsureStock(inventory, run.RunId, roomInstanceId);
                    RestStopOutcome rest = RestStopService.Enter(inventory, roomInstanceId,
                        candidateSave.currentHealth, candidateSave.maximumHealth);
                    candidateSave.currentHealth = rest.HealthAfter;
                    return new RestStopEntryCommitData(rest, stock);
                });
        }

        public Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> PurchaseFromMerchantAsync(
            RoomInstanceId roomInstanceId, string itemId, int quantity, int discountPercent, TransactionId transactionId)
        {
            string operationKey = string.Join("|", "merchant.buy", roomInstanceId, transactionId,
                itemId ?? string.Empty, quantity.ToString(CultureInfo.InvariantCulture),
                discountPercent.ToString(CultureInfo.InvariantCulture));
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: true,
                (run, inventory, _) => MerchantService.TryPurchase(inventory, run.RunId, roomInstanceId,
                    itemId, quantity, discountPercent, transactionId));
        }

        public Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> SellItemToMerchantAsync(
            RoomInstanceId roomInstanceId, ItemInstanceId itemInstanceId, int quantity, TransactionId transactionId)
        {
            string operationKey = string.Join("|", "merchant.sell-item", roomInstanceId, transactionId,
                itemInstanceId, quantity.ToString(CultureInfo.InvariantCulture));
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: true,
                (run, inventory, _) => MerchantService.TrySellItem(inventory, run.RunId, roomInstanceId,
                    itemInstanceId, quantity, transactionId));
        }

        public Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> SellMaterialToMerchantAsync(
            RoomInstanceId roomInstanceId, MaterialKind materialKind, int quantity, TransactionId transactionId)
        {
            string operationKey = string.Join("|", "merchant.sell-material", roomInstanceId, transactionId,
                materialKind, quantity.ToString(CultureInfo.InvariantCulture));
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: true,
                (run, inventory, _) => MerchantService.TrySellMaterial(inventory, run.RunId, roomInstanceId,
                    materialKind, quantity, transactionId));
        }

        public Task<ExpeditionMutationCommitResult<SmithingOutcome>> UpgradeEquipmentAsync(
            RoomInstanceId roomInstanceId, string recipeId, ItemInstanceId targetInstanceId, TransactionId transactionId)
        {
            string operationKey = string.Join("|", "smith.upgrade", roomInstanceId, transactionId,
                recipeId ?? string.Empty, targetInstanceId);
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: true,
                (run, inventory, _) => SmithingService.TryUpgrade(inventory,
                    LoadRecipeCatalog(run), recipeId, targetInstanceId, transactionId));
        }

        public Task<ExpeditionMutationCommitResult<EconomyTransactionOutcome>> UseTaliaAsync(
            RoomInstanceId roomInstanceId, TransactionId transactionId)
        {
            string operationKey = string.Join("|", "rest.talia", roomInstanceId, transactionId);
            return ExecuteEconomyMutationAsync(operationKey, roomInstanceId, requiresRestStop: true,
                (run, inventory, candidateSave) =>
                {
                    EconomyTransactionOutcome outcome = RestStopService.TryUseTalia(inventory, roomInstanceId,
                        candidateSave.currentHealth, candidateSave.maximumHealth, transactionId);
                    if (outcome.ResultingHealth >= 0) candidateSave.currentHealth = outcome.ResultingHealth;
                    return outcome;
                });
        }

        /// <summary>Retries only the exact pending economy operation and returns its original result.</summary>
        public async Task<ExpeditionMutationCommitResult<T>> RetryPendingEconomyMutationAsync<T>(string operationKey)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingEconomyMutation == null)
                    return new ExpeditionMutationCommitResult<T>(default, false, false,
                        "There is no pending economy mutation.");
                if (!string.Equals(_pendingEconomyMutation.OperationKey, operationKey, StringComparison.Ordinal))
                    return new ExpeditionMutationCommitResult<T>(default, false, true,
                        "A different economy operation is waiting for its exact save retry.");
                if (!(_pendingEconomyMutation.Outcome is T outcome))
                    return new ExpeditionMutationCommitResult<T>(default, false, true,
                        "The pending economy operation has a different result type.");

                if (_pendingExpeditionCandidate == null)
                {
                    _pendingEconomyMutation = null;
                    return new ExpeditionMutationCommitResult<T>(outcome, true, false,
                        "The economy operation was already durably committed.");
                }

                SaveWriteResult save = await RetryPendingExpeditionWriteUnderGateAsync();
                if (!save.Succeeded)
                    return new ExpeditionMutationCommitResult<T>(outcome, false, HasPendingExpeditionWrite, save.Message);

                _pendingEconomyMutation = null;
                return new ExpeditionMutationCommitResult<T>(outcome, true, false, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ExpeditionMutationCommitResult<T>> ExecuteEconomyMutationAsync<T>(
            string operationKey,
            RoomInstanceId roomInstanceId,
            bool requiresRestStop,
            Func<ExpeditionRunConfiguration, ExpeditionInventory, ExpeditionSaveData, T> mutation)
        {
            await _profileMutationGate.WaitAsync();
            try
            {
                if (_pendingEconomyMutation != null)
                {
                    if (!string.Equals(_pendingEconomyMutation.OperationKey, operationKey, StringComparison.Ordinal))
                        return new ExpeditionMutationCommitResult<T>(default, false, true,
                            "A different economy operation is waiting for its exact save retry.");
                    if (!(_pendingEconomyMutation.Outcome is T pendingOutcome))
                        return new ExpeditionMutationCommitResult<T>(default, false, true,
                            "The pending economy operation has a different result type.");
                    return await RetryPendingEconomyMutationUnderGateAsync(pendingOutcome);
                }
                if (HasPendingExpeditionWrite)
                    return new ExpeditionMutationCommitResult<T>(default, false, true,
                        "Retry the pending expedition write before starting an economy operation.");
                if (!CanContinueExpedition || ActiveRun == null || _activeExpedition == null)
                    return new ExpeditionMutationCommitResult<T>(default, false, false,
                        "There is no safely resumable active expedition for this operation.");
                if (!roomInstanceId.IsValid || roomInstanceId != ActiveRoomInstanceId)
                    return new ExpeditionMutationCommitResult<T>(default, false, false,
                        "The operation does not belong to the current room instance.");
                if (requiresRestStop && !IsCurrentRoomRestStop())
                    return new ExpeditionMutationCommitResult<T>(default, false, false,
                        "Merchant and rest-stop services are only available in a rest room.");

                ExpeditionInventory candidateInventory = ExpeditionInventory.Restore(
                    ActiveRun.Inventory.Catalog, ActiveRun.Inventory.CreateSnapshot());
                ExpeditionSaveData candidateSave = _activeExpedition.Clone();
                T outcome = mutation(ActiveRun, candidateInventory, candidateSave);
                candidateSave.inventorySnapshotJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot());
                InventoryItemStack quickConsumable = candidateInventory.QuickConsumable;
                candidateSave.healthPotionCharges = quickConsumable == null ? 0 : quickConsumable.Quantity;
                ExpeditionRunConfiguration candidateRun = ActiveRun.WithInventory(candidateInventory);

                if (string.Equals(candidateSave.inventorySnapshotJson, _activeExpedition.inventorySnapshotJson, StringComparison.Ordinal) &&
                    candidateSave.currentHealth == _activeExpedition.currentHealth)
                    return new ExpeditionMutationCommitResult<T>(outcome, true, false,
                        "The operation was already represented by the saved expedition state.");

                SaveWriteResult save = await SaveExpeditionCandidateUnderGateAsync(
                    candidateSave, _activeExpeditionRevision, candidateRun);
                if (save.Succeeded)
                    return new ExpeditionMutationCommitResult<T>(outcome, true, false, save.Message);
                if (HasPendingExpeditionWrite)
                {
                    _pendingEconomyMutation = new PendingEconomyMutation(operationKey, outcome);
                    return new ExpeditionMutationCommitResult<T>(outcome, false, true, save.Message);
                }
                return new ExpeditionMutationCommitResult<T>(outcome, false, false, save.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<ExpeditionMutationCommitResult<T>> RetryPendingEconomyMutationUnderGateAsync<T>(T outcome)
        {
            if (_pendingExpeditionCandidate == null)
            {
                _pendingEconomyMutation = null;
                return new ExpeditionMutationCommitResult<T>(outcome, true, false,
                    "The economy operation was already durably committed.");
            }
            SaveWriteResult save = await RetryPendingExpeditionWriteUnderGateAsync();
            if (save.Succeeded) _pendingEconomyMutation = null;
            return new ExpeditionMutationCommitResult<T>(outcome, save.Succeeded,
                !save.Succeeded && HasPendingExpeditionWrite, save.Message);
        }

        private bool IsCurrentRoomRestStop()
        {
            ExpeditionRouteNode node = ActiveRun.Route.Nodes.SingleOrDefault(candidate =>
                string.Equals(candidate.NodeId, _activeExpedition.currentNodeId, StringComparison.Ordinal));
            return node != null && node.Kind == ExpeditionRouteNodeKind.Rest;
        }

        private static RecipeCatalog LoadRecipeCatalog(ExpeditionRunConfiguration run)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            return RecipeCatalogJsonLoader.Load(LoadRequired("Luckbound/Data/Items/Recipes").text, run.Inventory.Catalog);
        }

        private async Task<SaveWriteResult> RetryPendingExpeditionWriteUnderGateAsync()
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
                _pendingCraft = null;
            }
            return result;
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
            try { return await SaveExpeditionCandidateUnderGateAsync(candidate, expectedRevision, null); }
            finally { _profileMutationGate.Release(); }
        }

        private async Task<SaveWriteResult> SaveExpeditionCandidateUnderGateAsync(
            ExpeditionSaveData candidate, long expectedRevision, ExpeditionRunConfiguration runToCommit)
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
            IReadOnlyList<string> errors = SaveDataValidation.Validate(candidate);
            if (errors.Count != 0) return new SaveWriteResult(false, string.Join(" ", errors));

            SaveWriteResult result = await _saveRepository.SaveProfileAsync(_profileData, candidate);
            if (result.Succeeded)
            {
                _activeExpedition = candidate.Clone();
                _activeExpeditionRevision++;
                if (runToCommit != null) ActiveRun = runToCommit;
            }
            else if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
            {
                _pendingExpeditionCandidate = candidate.Clone();
                _pendingExpeditionRun = runToCommit ?? ActiveRun;
            }
            return result;
        }

        private static TextAsset LoadRequired(string resourcePath)
        {
            TextAsset asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
                throw new InvalidOperationException("Required application data is missing: Resources/" + resourcePath + ".");
            return asset;
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
            if (result.Succeeded)
            {
                _settingsData = settings.Clone();
                ApplyRuntimeAudioSettings();
            }
            return result;
        }

        public async Task<SaveWriteResult> RetryPendingSettingsAsync()
        {
            SaveWriteResult result = await _saveRepository.RetryPendingAsync(SaveDocument.Settings);
            if (result.Succeeded && _saveRepository.CurrentSettings?.settings != null)
            {
                _settingsData = _saveRepository.CurrentSettings.settings.Clone();
                ApplyRuntimeAudioSettings();
            }
            return result;
        }

        private void ApplyRuntimeAudioSettings()
        {
            RuntimeAudioDirector audioDirector = GetComponent<RuntimeAudioDirector>();
            if (audioDirector != null)
                audioDirector.SetVolumes(_settingsData.masterVolume, _settingsData.musicVolume,
                    _settingsData.effectsVolume, _settingsData.ambienceVolume);

            RuntimeAmbienceDirector ambienceDirector = GetComponent<RuntimeAmbienceDirector>();
            if (ambienceDirector != null)
                ambienceDirector.SetVolumes(_settingsData.masterVolume, _settingsData.ambienceVolume);
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

            ApplyRuntimeAudioSettings();
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

        private sealed class PendingPermanentUpgradePurchase
        {
            public PendingPermanentUpgradePurchase(ProfileSaveData profile, PermanentUpgradePurchaseResult purchase)
            {
                Profile = profile.Clone();
                Purchase = purchase;
            }

            public ProfileSaveData Profile { get; }
            public PermanentUpgradePurchaseResult Purchase { get; }
        }

        private sealed class PendingCraft
        {
            public PendingCraft(string recipeId, TransactionId transactionId,
                ItemInstanceId upgradeTargetInstanceId, CraftingOutcome outcome)
            {
                RecipeId = recipeId;
                TransactionId = transactionId;
                UpgradeTargetInstanceId = upgradeTargetInstanceId;
                Outcome = outcome;
            }

            public string RecipeId { get; }
            public TransactionId TransactionId { get; }
            public ItemInstanceId UpgradeTargetInstanceId { get; }
            public CraftingOutcome Outcome { get; }
        }

        private sealed class PendingEconomyMutation
        {
            public PendingEconomyMutation(string operationKey, object outcome)
            {
                OperationKey = operationKey ?? throw new ArgumentNullException(nameof(operationKey));
                Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            }

            public string OperationKey { get; }
            public object Outcome { get; }
        }
    }
}
