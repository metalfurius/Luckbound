using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using Luckbound.Core.Progression;
using UnityEngine;

namespace Luckbound.App
{
    public enum SaveLoadStatus
    {
        Missing = 0,
        LoadedPrimary = 1,
        RecoveredBackup = 2,
        Corrupted = 3,
        Incompatible = 4
    }

    public sealed class SaveLoadResult<T>
    {
        internal SaveLoadResult(SaveLoadStatus status, T data, string message)
        {
            Status = status;
            Data = data;
            Message = message ?? string.Empty;
        }

        public SaveLoadStatus Status { get; }
        public T Data { get; }
        public string Message { get; }
        public bool HasUsableData => Data != null && Status != SaveLoadStatus.Incompatible;
    }

    public sealed class SaveWriteResult
    {
        internal SaveWriteResult(bool succeeded, string message)
        {
            Succeeded = succeeded;
            Message = message ?? string.Empty;
        }

        public bool Succeeded { get; }
        public string Message { get; }
    }

    public sealed class ExpeditionSettlementCommitResult
    {
        internal ExpeditionSettlementCommitResult(ExpeditionSettlementReceipt receipt, bool persisted, bool pending, string message)
        {
            Receipt = receipt;
            Persisted = persisted;
            Pending = pending;
            Message = message ?? string.Empty;
        }

        public ExpeditionSettlementReceipt Receipt { get; }
        public bool Persisted { get; }
        public bool Pending { get; }
        public string Message { get; }
    }

    /// <summary>Checksummed JSON envelopes, primary/backup recovery, sequences and retryable writes.</summary>
    public sealed class SaveRepository
    {
        private readonly ISaveStore _store;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private ProfileSaveEnvelope _profile;
        private SettingsSaveEnvelope _settings;
        private string _profilePrimaryJson;
        private string _settingsPrimaryJson;
        private bool _profilePrimaryValid;
        private bool _settingsPrimaryValid;
        private bool _profileLoaded;
        private bool _settingsLoaded;
        private bool _profileBlocked;
        private bool _settingsBlocked;
        private PendingWrite _pendingProfile;
        private PendingWrite _pendingSettings;

        public SaveRepository(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ProfileSaveEnvelope CurrentProfile => _profile;
        public SettingsSaveEnvelope CurrentSettings => _settings;

        public async Task<SaveLoadResult<ProfileSaveEnvelope>> LoadProfileAsync()
        {
            await _gate.WaitAsync();
            try { return await LoadProfileUnderLockAsync(); }
            finally { _gate.Release(); }
        }

        public async Task<SaveLoadResult<SettingsSaveEnvelope>> LoadSettingsAsync()
        {
            await _gate.WaitAsync();
            try { return await LoadSettingsUnderLockAsync(); }
            finally { _gate.Release(); }
        }

        public async Task<SaveWriteResult> SaveProfileAsync(ProfileSaveData profile, ExpeditionSaveData activeExpedition = null)
        {
            if (profile == null) return Failed("Profile data is missing.");
            if (!_profileLoaded) await LoadProfileAsync();
            await _gate.WaitAsync();
            try
            {
                if (_profileBlocked) return Failed("Profile writes are blocked because the saved profile is incompatible or unrecoverable.");
                if (_pendingProfile != null) return Failed("A profile write is pending. Retry it before starting another save.");
                ProfileSaveData copy = profile.Clone();
                copy.NormalizeForSave();
                string[] errors = SaveDataValidation.Validate(copy).ToArray();
                if (errors.Length != 0) return Failed(string.Join(" ", errors));

                ProfileSaveEnvelope envelope = NewProfileEnvelope(copy, activeExpedition,
                    NextGeneration(_profile == null ? 0 : _profile.generation),
                    NextSequence(_profile == null ? 0 : _profile.sequence));
                string json = SaveJsonCodec.Encode(envelope);
                _pendingProfile = new PendingWrite(SaveDocument.Profile, json, _profilePrimaryValid ? _profilePrimaryJson : null, envelope);
                return await CommitPendingUnderLockAsync(_pendingProfile);
            }
            finally { _gate.Release(); }
        }

        public async Task<SaveWriteResult> SaveSettingsAsync(GameSettingsSaveData settings)
        {
            if (settings == null) return Failed("Settings data is missing.");
            if (!_settingsLoaded) await LoadSettingsAsync();
            await _gate.WaitAsync();
            try
            {
                if (_settingsBlocked) return Failed("Settings writes are blocked because the saved version is incompatible.");
                if (_pendingSettings != null) return Failed("A settings write is pending. Retry it before starting another save.");
                GameSettingsSaveData copy = settings.Clone();
                string[] errors = SaveDataValidation.Validate(copy).ToArray();
                if (errors.Length != 0) return Failed(string.Join(" ", errors));

                SettingsSaveEnvelope envelope = NewSettingsEnvelope(copy,
                    NextGeneration(_settings == null ? 0 : _settings.generation),
                    NextSequence(_settings == null ? 0 : _settings.sequence));
                string json = SaveJsonCodec.Encode(envelope);
                _pendingSettings = new PendingWrite(SaveDocument.Settings, json, _settingsPrimaryValid ? _settingsPrimaryJson : null, envelope);
                return await CommitPendingUnderLockAsync(_pendingSettings);
            }
            finally { _gate.Release(); }
        }

        public async Task<SaveWriteResult> RetryPendingAsync(SaveDocument document)
        {
            await _gate.WaitAsync();
            try
            {
                PendingWrite pending = document == SaveDocument.Profile ? _pendingProfile : _pendingSettings;
                if (pending == null) return Failed("There is no pending write for " + document + ".");
                return await CommitPendingUnderLockAsync(pending);
            }
            finally { _gate.Release(); }
        }

        public string ExportPendingJson(SaveDocument document)
        {
            PendingWrite pending = document == SaveDocument.Profile ? _pendingProfile : _pendingSettings;
            return pending == null ? null : pending.Json;
        }

        private async Task<SaveLoadResult<ProfileSaveEnvelope>> LoadProfileUnderLockAsync()
        {
            SaveStorageSnapshot storage = await _store.ReadAsync(SaveDocument.Profile);
            DecodeStatus primaryStatus = SaveJsonCodec.Decode(storage.PrimaryJson, out ProfileSaveEnvelope primary);
            if (primaryStatus == DecodeStatus.Valid)
            {
                _profile = primary;
                _profilePrimaryJson = storage.PrimaryJson;
                _profilePrimaryValid = true;
                _profileBlocked = false;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.LoadedPrimary, primary, "Profile loaded.");
            }
            if (primaryStatus == DecodeStatus.Incompatible)
            {
                _profileBlocked = true;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The profile was created by a newer or unsupported save format; it was preserved.");
            }

            DecodeStatus backupStatus = SaveJsonCodec.Decode(storage.BackupJson, out ProfileSaveEnvelope backup);
            if (backupStatus == DecodeStatus.Valid)
            {
                _profile = backup;
                _profilePrimaryJson = null;
                _profilePrimaryValid = false;
                _profileBlocked = false;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.RecoveredBackup, backup,
                    "The primary profile was unusable; the valid backup was recovered.");
            }
            if (backupStatus == DecodeStatus.Incompatible)
            {
                _profileBlocked = true;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The profile backup uses a newer or unsupported save format; both files were preserved.");
            }
            if (primaryStatus == DecodeStatus.Missing && backupStatus == DecodeStatus.Missing)
            {
                _profile = NewProfileEnvelope(ProfileSaveData.CreateNewProfile(), null, 0, 0);
                _profilePrimaryJson = null;
                _profilePrimaryValid = false;
                _profileBlocked = false;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Missing, _profile, "No profile exists; new profile defaults are ready.");
            }

            _profile = null;
            _profilePrimaryJson = null;
            _profilePrimaryValid = false;
            _profileBlocked = true;
            _profileLoaded = true;
            return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Corrupted, null,
                "The profile and backup are invalid; they were preserved and profile writes are blocked.");
        }

        private async Task<SaveLoadResult<SettingsSaveEnvelope>> LoadSettingsUnderLockAsync()
        {
            SaveStorageSnapshot storage = await _store.ReadAsync(SaveDocument.Settings);
            DecodeStatus primaryStatus = SaveJsonCodec.Decode(storage.PrimaryJson, out SettingsSaveEnvelope primary);
            if (primaryStatus == DecodeStatus.Valid)
            {
                _settings = primary;
                _settingsPrimaryJson = storage.PrimaryJson;
                _settingsPrimaryValid = true;
                _settingsBlocked = false;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.LoadedPrimary, primary, "Settings loaded.");
            }
            if (primaryStatus == DecodeStatus.Incompatible)
            {
                _settingsBlocked = true;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The settings use a newer or unsupported save format; they were preserved.");
            }

            DecodeStatus backupStatus = SaveJsonCodec.Decode(storage.BackupJson, out SettingsSaveEnvelope backup);
            if (backupStatus == DecodeStatus.Valid)
            {
                _settings = backup;
                _settingsPrimaryJson = null;
                _settingsPrimaryValid = false;
                _settingsBlocked = false;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.RecoveredBackup, backup,
                    "The primary settings were unusable; the valid backup was recovered.");
            }
            if (backupStatus == DecodeStatus.Incompatible)
            {
                _settingsBlocked = true;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The settings backup uses a newer or unsupported save format; both files were preserved.");
            }
            if (primaryStatus == DecodeStatus.Missing && backupStatus == DecodeStatus.Missing)
            {
                _settings = NewSettingsEnvelope(new GameSettingsSaveData(), 0, 0);
                _settingsPrimaryJson = null;
                _settingsPrimaryValid = false;
                _settingsBlocked = false;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Missing, _settings, "No settings exist; defaults are ready.");
            }

            _settings = NewSettingsEnvelope(new GameSettingsSaveData(), 0, 0);
            _settingsPrimaryJson = null;
            _settingsPrimaryValid = false;
            _settingsBlocked = false;
            _settingsLoaded = true;
            return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Corrupted, _settings,
                "Settings and backup were invalid; defaults were restored and both files were preserved.");
        }

        private async Task<SaveWriteResult> CommitPendingUnderLockAsync(PendingWrite pending)
        {
            try
            {
                await _store.WriteAsync(pending.Document, pending.Json, pending.LastKnownGoodPrimaryJson);
                if (pending.Document == SaveDocument.Profile)
                {
                    _profile = (ProfileSaveEnvelope)pending.Envelope;
                    _profilePrimaryJson = pending.Json;
                    _profilePrimaryValid = true;
                    _profileBlocked = false;
                    _profileLoaded = true;
                    _pendingProfile = null;
                }
                else
                {
                    _settings = (SettingsSaveEnvelope)pending.Envelope;
                    _settingsPrimaryJson = pending.Json;
                    _settingsPrimaryValid = true;
                    _settingsBlocked = false;
                    _settingsLoaded = true;
                    _pendingSettings = null;
                }
                return new SaveWriteResult(true, "The " + pending.Document + " document was written and synchronized.");
            }
            catch (Exception exception)
            {
                return Failed("The " + pending.Document + " write is pending: " + exception.Message);
            }
        }

        private static ProfileSaveEnvelope NewProfileEnvelope(ProfileSaveData profile, ExpeditionSaveData expedition,
            int generation, long sequence)
        {
            return new ProfileSaveEnvelope
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = SaveFormat.ContentVersion,
                generation = generation,
                sequence = sequence,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                profile = profile,
                hasActiveExpedition = expedition != null,
                activeExpedition = expedition
            };
        }

        private static SettingsSaveEnvelope NewSettingsEnvelope(GameSettingsSaveData settings, int generation, long sequence)
        {
            return new SettingsSaveEnvelope
            {
                schemaVersion = SaveFormat.SchemaVersion,
                contentVersion = SaveFormat.ContentVersion,
                generation = generation,
                sequence = sequence,
                savedAtUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                checksum = string.Empty,
                settings = settings
            };
        }

        private static int NextGeneration(int current) => checked(current + 1);
        private static long NextSequence(long current) => checked(current + 1);
        private static SaveWriteResult Failed(string message) => new SaveWriteResult(false, message);

        private sealed class PendingWrite
        {
            public PendingWrite(SaveDocument document, string json, string lastKnownGoodPrimaryJson, object envelope)
            {
                Document = document;
                Json = json;
                LastKnownGoodPrimaryJson = lastKnownGoodPrimaryJson;
                Envelope = envelope;
            }
            public SaveDocument Document { get; }
            public string Json { get; }
            public string LastKnownGoodPrimaryJson { get; }
            public object Envelope { get; }
        }
    }

    internal enum DecodeStatus { Missing, Valid, Corrupted, Incompatible }

    internal static class SaveJsonCodec
    {
        public static string Encode(ProfileSaveEnvelope envelope)
        {
            envelope.checksum = ComputeChecksum(envelope);
            return JsonUtility.ToJson(envelope, true);
        }

        public static string Encode(SettingsSaveEnvelope envelope)
        {
            envelope.checksum = ComputeChecksum(envelope);
            return JsonUtility.ToJson(envelope, true);
        }

        public static DecodeStatus Decode(string json, out ProfileSaveEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(json)) return DecodeStatus.Missing;
            try { envelope = JsonUtility.FromJson<ProfileSaveEnvelope>(json); }
            catch { return DecodeStatus.Corrupted; }
            if (envelope == null) return DecodeStatus.Corrupted;
            if (!envelope.hasActiveExpedition) envelope.activeExpedition = null;
            DecodeStatus versionStatus = CheckVersion(envelope.schemaVersion, envelope.contentVersion);
            if (versionStatus != DecodeStatus.Valid) return versionStatus;
            if (envelope.contentVersion == 1)
            {
                if (!string.Equals(envelope.checksum, ComputeLegacyProfileChecksum(envelope), StringComparison.Ordinal))
                    return DecodeStatus.Corrupted;
                if (envelope.profile != null && envelope.profile.progressionPurchaseTransactions == null)
                    envelope.profile.progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>();
                envelope.contentVersion = SaveFormat.ContentVersion;
                envelope.checksum = ComputeChecksum(envelope);
            }
            else if (!string.Equals(envelope.checksum, ComputeChecksum(envelope), StringComparison.Ordinal))
            {
                return DecodeStatus.Corrupted;
            }
            return SaveDataValidation.Validate(envelope).Count == 0 ? DecodeStatus.Valid : DecodeStatus.Corrupted;
        }

        public static DecodeStatus Decode(string json, out SettingsSaveEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(json)) return DecodeStatus.Missing;
            try { envelope = JsonUtility.FromJson<SettingsSaveEnvelope>(json); }
            catch { return DecodeStatus.Corrupted; }
            if (envelope == null) return DecodeStatus.Corrupted;
            DecodeStatus versionStatus = CheckVersion(envelope.schemaVersion, envelope.contentVersion);
            if (versionStatus != DecodeStatus.Valid) return versionStatus;
            if (!string.Equals(envelope.checksum, ComputeChecksum(envelope), StringComparison.Ordinal)) return DecodeStatus.Corrupted;
            if (envelope.contentVersion == 1)
            {
                envelope.contentVersion = SaveFormat.ContentVersion;
                envelope.checksum = ComputeChecksum(envelope);
            }
            return SaveDataValidation.Validate(envelope).Count == 0 ? DecodeStatus.Valid : DecodeStatus.Corrupted;
        }

        private static DecodeStatus CheckVersion(int schema, int content)
        {
            if (schema > SaveFormat.SchemaVersion || content > SaveFormat.ContentVersion) return DecodeStatus.Incompatible;
            if (schema != SaveFormat.SchemaVersion || content < 1) return DecodeStatus.Incompatible;
            return DecodeStatus.Valid;
        }

        private static string ComputeLegacyProfileChecksum(ProfileSaveEnvelope source)
        {
            var legacy = new LegacyProfileSaveEnvelopeV1
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                profile = source.profile == null ? null : new LegacyProfileSaveDataV1
                {
                    availableRenown = source.profile.availableRenown,
                    healthUpgradeLevel = source.profile.healthUpgradeLevel,
                    energyUpgradeLevel = source.profile.energyUpgradeLevel,
                    additionalPotionUpgradeLevel = source.profile.additionalPotionUpgradeLevel,
                    unlockedRaceIds = source.profile.unlockedRaceIds,
                    unlockedStartingKitIds = source.profile.unlockedStartingKitIds,
                    acceptedContractIds = source.profile.acceptedContractIds,
                    claimedContractIds = source.profile.claimedContractIds,
                    readStoryFragmentIds = source.profile.readStoryFragmentIds,
                    contractCounters = source.profile.contractCounters,
                    processedSettlementRunIds = source.profile.processedSettlementRunIds
                },
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = source.activeExpedition
            };
            return Hash(JsonUtility.ToJson(legacy, false));
        }

        private static string ComputeChecksum(ProfileSaveEnvelope source)
        {
            var canonical = new ProfileSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                profile = source.profile,
                hasActiveExpedition = source.hasActiveExpedition,
                activeExpedition = source.activeExpedition
            };
            return Hash(JsonUtility.ToJson(canonical, false));
        }

        private static string ComputeChecksum(SettingsSaveEnvelope source)
        {
            var canonical = new SettingsSaveEnvelope
            {
                schemaVersion = source.schemaVersion,
                contentVersion = source.contentVersion,
                generation = source.generation,
                sequence = source.sequence,
                savedAtUtc = source.savedAtUtc,
                checksum = string.Empty,
                settings = source.settings
            };
            return Hash(JsonUtility.ToJson(canonical, false));
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV1
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileSaveDataV1 profile;
            public bool hasActiveExpedition;
            public ExpeditionSaveData activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveDataV1
        {
            public int availableRenown;
            public int healthUpgradeLevel;
            public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel;
            public string[] unlockedRaceIds;
            public string[] unlockedStartingKitIds;
            public string[] acceptedContractIds;
            public string[] claimedContractIds;
            public string[] readStoryFragmentIds;
            public ContractCounterSaveData[] contractCounters;
            public string[] processedSettlementRunIds;
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
