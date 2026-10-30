using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.App.Compatibility;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
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

    public enum SaveDataAccess
    {
        Unavailable = 0,
        ReadWrite = 1,
        ReadOnlyLegacyEncounterPinMissing = 2
    }

    public sealed class SaveLoadResult<T>
    {
        internal SaveLoadResult(SaveLoadStatus status, T data, string message, SaveDataAccess access)
        {
            Status = status;
            Data = data;
            Message = message ?? string.Empty;
            Access = access;
        }

        public SaveLoadStatus Status { get; }
        public T Data { get; }
        public string Message { get; }
        public SaveDataAccess Access { get; }
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
        private string _loadedProfileJson;
        private string _settingsPrimaryJson;
        private bool _profilePrimaryValid;
        private bool _settingsPrimaryValid;
        private bool _profileLoaded;
        private bool _settingsLoaded;
        private bool _profileBlocked;
        private bool _settingsBlocked;
        private SaveDataAccess _profileAccess = SaveDataAccess.Unavailable;
        private PendingWrite _pendingProfile;
        private PendingWrite _pendingSettings;

        public SaveRepository(ISaveStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public ProfileSaveEnvelope CurrentProfile => _profile;
        public SettingsSaveEnvelope CurrentSettings => _settings;
        public SaveDataAccess ProfileAccess => _profileAccess;
        public string ExportLoadedProfileJson() => _loadedProfileJson;
        /// <summary>Returns a copy of the exact settings payload retained for retry, if any.</summary>
        public GameSettingsSaveData PendingSettingsForRetry => _pendingSettings == null
            ? null
            : ((SettingsSaveEnvelope)_pendingSettings.Envelope).settings.Clone();

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
                if (_profileAccess != SaveDataAccess.ReadWrite)
                    return Failed("Profile writes are blocked because the loaded profile is read-only or unavailable.");
                if (_profileBlocked) return Failed("Profile writes are blocked because the saved profile is incompatible or unrecoverable.");
                if (_pendingProfile != null) return Failed("A profile write is pending. Retry it before starting another save.");
                ProfileSaveData copy = profile.Clone();
                copy.NormalizeForSave();
                string[] errors = SaveDataValidation.Validate(copy).ToArray();
                if (errors.Length != 0) return Failed(string.Join(" ", errors));
                if (activeExpedition != null)
                {
                    string[] expeditionErrors = SaveDataValidation.Validate(activeExpedition).ToArray();
                    if (expeditionErrors.Length != 0)
                        return Failed(string.Join(" ", expeditionErrors));
                    try { ExpeditionSaveMapper.ValidateRoomEntryCheckpoint(activeExpedition); }
                    catch (Exception exception) { return Failed("Active expedition is invalid: " + exception.Message); }
                }

                ProfileSaveEnvelope envelope = NewProfileEnvelope(copy, activeExpedition == null ? null : activeExpedition.Clone(),
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
                if (document == SaveDocument.Profile && _profileAccess != SaveDataAccess.ReadWrite)
                    return Failed("Profile writes are blocked because the loaded profile is read-only or unavailable.");
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
            if (_pendingProfile != null)
                throw new InvalidOperationException("A profile cannot be reloaded while a profile write is pending.");

            SaveStorageSnapshot storage = await _store.ReadAsync(SaveDocument.Profile);
            DecodeStatus primaryStatus = SaveJsonCodec.Decode(storage.PrimaryJson, out ProfileSaveEnvelope primary,
                out int primarySourceContentVersion);
            if (primaryStatus == DecodeStatus.Valid)
            {
                _profile = primary;
                _profilePrimaryJson = storage.PrimaryJson;
                _loadedProfileJson = storage.PrimaryJson;
                _profilePrimaryValid = true;
                _profileBlocked = false;
                _profileAccess = ResolveProfileAccess(primary, primarySourceContentVersion);
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.LoadedPrimary, primary,
                    "Profile loaded.", _profileAccess);
            }
            if (primaryStatus == DecodeStatus.Incompatible)
            {
                _profile = null;
                _profilePrimaryJson = null;
                _loadedProfileJson = null;
                _profilePrimaryValid = false;
                _profileBlocked = true;
                _profileAccess = SaveDataAccess.Unavailable;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The profile was created by a newer or unsupported save format; it was preserved.",
                    SaveDataAccess.Unavailable);
            }

            DecodeStatus backupStatus = SaveJsonCodec.Decode(storage.BackupJson, out ProfileSaveEnvelope backup,
                out int backupSourceContentVersion);
            if (backupStatus == DecodeStatus.Valid)
            {
                _profile = backup;
                _profilePrimaryJson = null;
                _loadedProfileJson = storage.BackupJson;
                _profilePrimaryValid = false;
                _profileBlocked = false;
                _profileAccess = ResolveProfileAccess(backup, backupSourceContentVersion);
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.RecoveredBackup, backup,
                    "The primary profile was unusable; the valid backup was recovered.", _profileAccess);
            }
            if (backupStatus == DecodeStatus.Incompatible)
            {
                _profile = null;
                _profilePrimaryJson = null;
                _loadedProfileJson = null;
                _profilePrimaryValid = false;
                _profileBlocked = true;
                _profileAccess = SaveDataAccess.Unavailable;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The profile backup uses a newer or unsupported save format; both files were preserved.",
                    SaveDataAccess.Unavailable);
            }
            if (primaryStatus == DecodeStatus.Missing && backupStatus == DecodeStatus.Missing)
            {
                _profile = NewProfileEnvelope(ProfileSaveData.CreateNewProfile(), null, 0, 0);
                _profilePrimaryJson = null;
                _loadedProfileJson = null;
                _profilePrimaryValid = false;
                _profileBlocked = false;
                _profileAccess = SaveDataAccess.ReadWrite;
                _profileLoaded = true;
                return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Missing, _profile,
                    "No profile exists; new profile defaults are ready.", SaveDataAccess.ReadWrite);
            }

            _profile = null;
            _profilePrimaryJson = null;
            _loadedProfileJson = null;
            _profilePrimaryValid = false;
            _profileBlocked = true;
            _profileAccess = SaveDataAccess.Unavailable;
            _profileLoaded = true;
            return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Corrupted, null,
                "The profile and backup are invalid; they were preserved and profile writes are blocked.",
                SaveDataAccess.Unavailable);
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
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.LoadedPrimary, primary,
                    "Settings loaded.", SaveDataAccess.ReadWrite);
            }
            if (primaryStatus == DecodeStatus.Incompatible)
            {
                _settingsBlocked = true;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The settings use a newer or unsupported save format; they were preserved.",
                    SaveDataAccess.Unavailable);
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
                    "The primary settings were unusable; the valid backup was recovered.", SaveDataAccess.ReadWrite);
            }
            if (backupStatus == DecodeStatus.Incompatible)
            {
                _settingsBlocked = true;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                    "The settings backup uses a newer or unsupported save format; both files were preserved.",
                    SaveDataAccess.Unavailable);
            }
            if (primaryStatus == DecodeStatus.Missing && backupStatus == DecodeStatus.Missing)
            {
                _settings = NewSettingsEnvelope(new GameSettingsSaveData(), 0, 0);
                _settingsPrimaryJson = null;
                _settingsPrimaryValid = false;
                _settingsBlocked = false;
                _settingsLoaded = true;
                return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Missing, _settings,
                    "No settings exist; defaults are ready.", SaveDataAccess.ReadWrite);
            }

            _settings = NewSettingsEnvelope(new GameSettingsSaveData(), 0, 0);
            _settingsPrimaryJson = null;
            _settingsPrimaryValid = false;
            _settingsBlocked = false;
            _settingsLoaded = true;
            return new SaveLoadResult<SettingsSaveEnvelope>(SaveLoadStatus.Corrupted, _settings,
                "Settings and backup were invalid; defaults were restored and both files were preserved.",
                SaveDataAccess.ReadWrite);
        }

        private async Task<SaveWriteResult> CommitPendingUnderLockAsync(PendingWrite pending)
        {
            if (pending.Document == SaveDocument.Profile && _profileAccess != SaveDataAccess.ReadWrite)
                return Failed("Profile writes are blocked because the loaded profile is read-only or unavailable.");

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

        private static SaveDataAccess ResolveProfileAccess(ProfileSaveEnvelope source, int sourceContentVersion)
        {
            return sourceContentVersion == 6 && source != null &&
                source.hasActiveExpedition && source.activeExpedition != null
                ? SaveDataAccess.ReadOnlyLegacyEncounterPinMissing
                : SaveDataAccess.ReadWrite;
        }

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
            return Decode(json, out envelope, out _);
        }

        internal static DecodeStatus Decode(string json, out ProfileSaveEnvelope envelope, out int sourceContentVersion)
        {
            envelope = null;
            sourceContentVersion = 0;
            if (string.IsNullOrWhiteSpace(json)) return DecodeStatus.Missing;
            SaveVersionHeader header;
            try { header = JsonUtility.FromJson<SaveVersionHeader>(json); }
            catch { return DecodeStatus.Corrupted; }
            if (header == null) return DecodeStatus.Corrupted;
            DecodeStatus versionStatus = CheckVersion(header.schemaVersion, header.contentVersion);
            if (versionStatus != DecodeStatus.Valid) return versionStatus;
            try
            {
                if (header.contentVersion == 1)
                {
                    LegacyProfileSaveEnvelopeV1 legacy = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV1>(json);
                    if (legacy == null || !NormalizeLegacyProfileEnvelope(legacy) ||
                        !string.Equals(legacy.checksum, ComputeLegacyProfileChecksum(legacy), StringComparison.Ordinal))
                        return DecodeStatus.Corrupted;
                    envelope = MigrateLegacyProfile(legacy.schemaVersion, legacy.generation, legacy.sequence,
                        legacy.savedAtUtc, legacy.profile, legacy.hasActiveExpedition, legacy.activeExpedition,
                        Array.Empty<PermanentUpgradePurchaseSaveData>());
                }
                else if (header.contentVersion == 2)
                {
                    LegacyProfileSaveEnvelopeV2 legacy = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV2>(json);
                    if (legacy == null || !NormalizeLegacyProfileEnvelope(legacy) ||
                        !string.Equals(legacy.checksum, ComputeLegacyProfileChecksum(legacy), StringComparison.Ordinal))
                        return DecodeStatus.Corrupted;
                    envelope = MigrateLegacyProfile(legacy.schemaVersion, legacy.generation, legacy.sequence,
                        legacy.savedAtUtc, ToLegacyProfileV1(legacy.profile), legacy.hasActiveExpedition,
                        legacy.activeExpedition, legacy.profile == null ? null : legacy.profile.progressionPurchaseTransactions);
                }
                else if (header.contentVersion == 5)
                {
                    Luckbound.App.Compatibility.LegacyProfileSaveEnvelopeV5 verified;
                    DecodeStatus status = SaveV5HistoricalReader.ReadProfile(json, out verified);
                    if (status != DecodeStatus.Valid) return status;
                    envelope = HistoricalSaveMapper.ToLive(verified);
                    envelope.checksum = ComputeChecksum(envelope);
                }
                else if (header.contentVersion < 5)
                {
                    LegacyProfileSaveEnvelopeV4 legacy = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV4>(json);
                    bool validLegacyChecksum = legacy != null && NormalizeLegacyProfileEnvelope(legacy) &&
                        string.Equals(legacy.checksum, ComputeLegacyProfileChecksum(legacy), StringComparison.Ordinal);
                    if (validLegacyChecksum)
                    {
                        envelope = MigrateLegacyCurrentProfile(legacy);
                    }
                    else if (header.contentVersion == 4)
                    {
                        // Fortune Brew added a nested receipt after ContentVersion 4 had already shipped.
                        // Accept both historical shapes while checking each checksum against its exact DTO.
                        LegacyProfileSaveEnvelopeV4Brew legacyBrew = JsonUtility.FromJson<LegacyProfileSaveEnvelopeV4Brew>(json);
                        if (legacyBrew == null || !NormalizeLegacyProfileEnvelope(legacyBrew) ||
                            !string.Equals(legacyBrew.checksum, ComputeLegacyProfileChecksum(legacyBrew), StringComparison.Ordinal))
                            return DecodeStatus.Corrupted;
                        envelope = MigrateLegacyCurrentProfile(legacyBrew);
                    }
                    else
                    {
                        return DecodeStatus.Corrupted;
                    }
                }
                else
                {
                    LegacyProfileSaveEnvelopeV6 verified;
                    DecodeStatus status = SaveV6HistoricalReader.ReadProfile(json, out verified);
                    if (status != DecodeStatus.Valid) return status;
                    envelope = HistoricalSaveMapper.ToLive(verified);
                }
            }
            catch
            {
                return DecodeStatus.Corrupted;
            }
            // V5/V6 have already passed complete versioned validation before mapping.
            if (header.contentVersion < 5 && !ValidateProfileEnvelope(envelope)) return DecodeStatus.Corrupted;
            sourceContentVersion = header.contentVersion;
            return DecodeStatus.Valid;
        }

        public static DecodeStatus Decode(string json, out SettingsSaveEnvelope envelope)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(json)) return DecodeStatus.Missing;
            SaveVersionHeader header;
            try { header = JsonUtility.FromJson<SaveVersionHeader>(json); }
            catch { return DecodeStatus.Corrupted; }
            if (header == null) return DecodeStatus.Corrupted;
            DecodeStatus versionStatus = CheckVersion(header.schemaVersion, header.contentVersion);
            if (versionStatus != DecodeStatus.Valid) return versionStatus;

            if (header.contentVersion <= 3)
            {
                LegacySettingsSaveEnvelope legacy;
                try { legacy = JsonUtility.FromJson<LegacySettingsSaveEnvelope>(json); }
                catch { return DecodeStatus.Corrupted; }
                if (legacy == null || legacy.settings == null ||
                    !string.Equals(legacy.checksum, ComputeLegacySettingsChecksum(legacy), StringComparison.Ordinal))
                    return DecodeStatus.Corrupted;

                envelope = new SettingsSaveEnvelope
                {
                    schemaVersion = legacy.schemaVersion,
                    contentVersion = SaveFormat.ContentVersion,
                    generation = legacy.generation,
                    sequence = legacy.sequence,
                    savedAtUtc = legacy.savedAtUtc,
                    settings = new GameSettingsSaveData
                    {
                        languageId = legacy.settings.languageId,
                        masterVolume = legacy.settings.masterVolume,
                        musicVolume = legacy.settings.musicVolume,
                        effectsVolume = legacy.settings.effectsVolume,
                        ambienceVolume = 0.45f
                    }
                };
                envelope.checksum = ComputeChecksum(envelope);
            }
            else if (header.contentVersion == 4)
            {
                LegacySettingsSaveEnvelopeV4 legacy;
                try { legacy = JsonUtility.FromJson<LegacySettingsSaveEnvelopeV4>(json); }
                catch { return DecodeStatus.Corrupted; }
                if (legacy == null || legacy.settings == null ||
                    !string.Equals(legacy.checksum, ComputeLegacySettingsChecksum(legacy), StringComparison.Ordinal))
                    return DecodeStatus.Corrupted;

                envelope = new SettingsSaveEnvelope
                {
                    schemaVersion = legacy.schemaVersion,
                    contentVersion = SaveFormat.ContentVersion,
                    generation = legacy.generation,
                    sequence = legacy.sequence,
                    savedAtUtc = legacy.savedAtUtc,
                    settings = new GameSettingsSaveData
                    {
                        languageId = legacy.settings.languageId,
                        masterVolume = legacy.settings.masterVolume,
                        musicVolume = legacy.settings.musicVolume,
                        effectsVolume = legacy.settings.effectsVolume,
                        ambienceVolume = legacy.settings.ambienceVolume
                    }
                };
                envelope.checksum = ComputeChecksum(envelope);
            }
            else if (header.contentVersion == 5)
            {
                LegacySettingsSaveEnvelopeV5 verified;
                DecodeStatus status = SaveV5HistoricalReader.ReadSettings(json, out verified);
                if (status != DecodeStatus.Valid) return status;
                envelope = HistoricalSaveMapper.ToLive(verified);
                envelope.checksum = ComputeChecksum(envelope);
                return DecodeStatus.Valid;
            }
            else
            {
                LegacySettingsSaveEnvelopeV6 verified;
                DecodeStatus status = SaveV6HistoricalReader.ReadSettings(json, out verified);
                if (status != DecodeStatus.Valid) return status;
                envelope = HistoricalSaveMapper.ToLive(verified);
                return DecodeStatus.Valid;
            }

            return SaveDataValidation.Validate(envelope).Count == 0 ? DecodeStatus.Valid : DecodeStatus.Corrupted;
        }

        private static DecodeStatus CheckVersion(int schema, int content)
        {
            if (schema > SaveFormat.SchemaVersion || content > SaveFormat.ContentVersion) return DecodeStatus.Incompatible;
            if (schema != SaveFormat.SchemaVersion || content < 1) return DecodeStatus.Incompatible;
            return DecodeStatus.Valid;
        }

        private static bool NormalizeLegacyProfileEnvelope(LegacyProfileSaveEnvelopeV1 envelope)
        {
            if (envelope.hasActiveExpedition) return envelope.activeExpedition != null;
            if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
            envelope.activeExpedition = null;
            return true;
        }

        private static bool NormalizeLegacyProfileEnvelope(LegacyProfileSaveEnvelopeV2 envelope)
        {
            if (envelope.hasActiveExpedition) return envelope.activeExpedition != null;
            if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
            envelope.activeExpedition = null;
            return true;
        }

        private static bool NormalizeLegacyProfileEnvelope(LegacyProfileSaveEnvelopeV4 envelope)
        {
            if (envelope.hasActiveExpedition) return envelope.activeExpedition != null;
            if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
            envelope.activeExpedition = null;
            return true;
        }

        private static bool NormalizeLegacyProfileEnvelope(LegacyProfileSaveEnvelopeV4Brew envelope)
        {
            if (envelope.hasActiveExpedition) return envelope.activeExpedition != null;
            if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
            envelope.activeExpedition = null;
            return true;
        }

        private static bool NormalizeLegacyProfileEnvelope(LegacyProfileSaveEnvelopeV5 envelope)
        {
            if (envelope.hasActiveExpedition) return envelope.activeExpedition != null;
            if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
            envelope.activeExpedition = null;
            return true;
        }

        private static bool HasActiveExpeditionPayload(string runId, uint seed, int generationVersion,
            int contentVersion, string classId, string raceId, string routeSnapshotJson,
            string currentNodeId, string roomEntryCheckpointJson, string inventorySnapshotJson)
        {
            return !string.IsNullOrWhiteSpace(runId) || seed != 0 || generationVersion != 0 || contentVersion != 0 ||
                !string.IsNullOrWhiteSpace(classId) || !string.IsNullOrWhiteSpace(raceId) ||
                !string.IsNullOrWhiteSpace(routeSnapshotJson) || !string.IsNullOrWhiteSpace(currentNodeId) ||
                !string.IsNullOrWhiteSpace(roomEntryCheckpointJson) || !string.IsNullOrWhiteSpace(inventorySnapshotJson);
        }

        private static bool HasActiveExpeditionPayload(LegacyExpeditionSaveDataV2 expedition)
        {
            if (expedition == null) return false;
            return HasActiveExpeditionPayload(expedition.runId, expedition.seed, expedition.generationVersion,
                    expedition.contentVersion, expedition.classId, expedition.raceId, expedition.routeSnapshotJson,
                    expedition.currentNodeId, expedition.roomEntryCheckpointJson, expedition.inventorySnapshotJson) ||
                HasEntries(expedition.selectedSkillIds) || HasEntries(expedition.startingEquipmentInstanceIds) ||
                HasEntries(expedition.visitedNodeIds) || HasEntries(expedition.completedRoomIds) ||
                HasEntries(expedition.completedRoomResults) || HasEntries(expedition.pactIds) ||
                HasEntries(expedition.pendingOfferIds) || HasEntries(expedition.offerRerollCounts) ||
                HasEntries(expedition.collectedItemIds) || HasEntries(expedition.claimedRewardIds);
        }

        private static bool HasActiveExpeditionPayload(LegacyExpeditionSaveDataV4 expedition)
        {
            if (expedition == null) return false;
            return HasActiveExpeditionPayload(expedition.runId, expedition.seed, expedition.generationVersion,
                    expedition.contentVersion, expedition.classId, expedition.raceId, expedition.routeSnapshotJson,
                    expedition.currentNodeId, expedition.roomEntryCheckpointJson, expedition.inventorySnapshotJson) ||
                HasEntries(expedition.selectedSkillIds) || HasEntries(expedition.startingEquipmentInstanceIds) ||
                HasEntries(expedition.visitedNodeIds) || HasEntries(expedition.completedRoomIds) ||
                HasEntries(expedition.completedRoomResults) || HasEntries(expedition.pactIds) ||
                HasEntries(expedition.collectedItemIds) || HasEntries(expedition.claimedRewardIds) ||
                HasActiveFortuneSnapshot(expedition.fortuneOfferBookSnapshot);
        }

        private static bool HasActiveExpeditionPayload(LegacyExpeditionSaveDataV4Brew expedition)
        {
            if (expedition == null) return false;
            return HasActiveExpeditionPayload(expedition.runId, expedition.seed, expedition.generationVersion,
                    expedition.contentVersion, expedition.classId, expedition.raceId, expedition.routeSnapshotJson,
                    expedition.currentNodeId, expedition.roomEntryCheckpointJson, expedition.inventorySnapshotJson) ||
                HasEntries(expedition.selectedSkillIds) || HasEntries(expedition.startingEquipmentInstanceIds) ||
                HasEntries(expedition.visitedNodeIds) || HasEntries(expedition.completedRoomIds) ||
                HasEntries(expedition.completedRoomResults) || HasEntries(expedition.pactIds) ||
                HasEntries(expedition.collectedItemIds) || HasEntries(expedition.claimedRewardIds) ||
                HasActiveFortuneSnapshot(expedition.fortuneOfferBookSnapshot);
        }

        private static bool HasActiveFortuneSnapshot(LegacyFortuneOfferBookSnapshotV4 snapshot)
        {
            return snapshot != null && (snapshot.snapshotVersion != 0 || snapshot.generationVersion != 0 ||
                snapshot.seed != 0 || !string.IsNullOrWhiteSpace(snapshot.raceId) || snapshot.charges != 0 ||
                HasEntries(snapshot.offers) || HasEntries(snapshot.processedGuardianIds) || HasEntries(snapshot.rerollReceipts));
        }

        private static bool HasActiveFortuneSnapshot(LegacyFortuneOfferBookSnapshotV4Brew snapshot)
        {
            return snapshot != null && (snapshot.snapshotVersion != 0 || snapshot.generationVersion != 0 ||
                snapshot.seed != 0 || !string.IsNullOrWhiteSpace(snapshot.raceId) || snapshot.charges != 0 ||
                HasEntries(snapshot.offers) || HasEntries(snapshot.processedGuardianIds) || HasEntries(snapshot.rerollReceipts) ||
                HasEntries(snapshot.fortuneBrewReceipts));
        }

        private static bool HasEntries(Array values) => values != null && values.Length != 0;

        private static bool HasActiveExpeditionPayload(ExpeditionSaveData expedition)
        {
            if (expedition == null) return false;
            bool hasFortune = expedition.fortuneOfferBookSnapshot != null &&
                (expedition.fortuneOfferBookSnapshot.snapshotVersion != 0 ||
                 expedition.fortuneOfferBookSnapshot.generationVersion != 0 || expedition.fortuneOfferBookSnapshot.seed != 0 ||
                 !string.IsNullOrWhiteSpace(expedition.fortuneOfferBookSnapshot.raceId) ||
                 expedition.fortuneOfferBookSnapshot.charges != 0 || HasEntries(expedition.fortuneOfferBookSnapshot.offers) ||
                 HasEntries(expedition.fortuneOfferBookSnapshot.processedGuardianIds) ||
                 HasEntries(expedition.fortuneOfferBookSnapshot.rerollReceipts) ||
                 HasEntries(expedition.fortuneOfferBookSnapshot.fortuneBrewReceipts));
            return HasActiveExpeditionPayload(expedition.runId, expedition.seed, expedition.generationVersion,
                    expedition.contentVersion, expedition.classId, expedition.raceId, expedition.routeSnapshotJson,
                    expedition.currentNodeId, expedition.roomEntryCheckpointJson, expedition.inventorySnapshotJson) ||
                HasEntries(expedition.selectedSkillIds) || HasEntries(expedition.startingEquipmentInstanceIds) ||
                HasEntries(expedition.visitedNodeIds) || HasEntries(expedition.completedRoomIds) ||
                HasEntries(expedition.completedRoomResults) || HasEntries(expedition.pactIds) ||
                HasEntries(expedition.collectedItemIds) || HasEntries(expedition.claimedRewardIds) ||
                expedition.hasFrozenPermanentUpgrades || HasFrozenUpgradeValues(expedition.frozenPermanentUpgrades) || hasFortune;
        }

        private static bool HasFrozenUpgradeValues(ExpeditionPermanentUpgradeSnapshotSaveData snapshot) =>
            snapshot != null && (snapshot.healthUpgradeLevel != 0 || snapshot.energyUpgradeLevel != 0 ||
                snapshot.additionalPotionUpgradeLevel != 0);

        private static bool NormalizeProfileEnvelope(ProfileSaveEnvelope envelope)
        {
            if (envelope.hasActiveExpedition)
            {
                if (envelope.activeExpedition == null) return false;
            }
            else
            {
                if (HasActiveExpeditionPayload(envelope.activeExpedition)) return false;
                envelope.activeExpedition = null;
            }

            if (envelope.activeExpedition != null && !envelope.activeExpedition.hasFrozenPermanentUpgrades)
            {
                if (HasFrozenUpgradeValues(envelope.activeExpedition.frozenPermanentUpgrades)) return false;
                envelope.activeExpedition.frozenPermanentUpgrades = null;
            }
            return true;
        }

        private static bool ValidateProfileEnvelope(ProfileSaveEnvelope envelope)
        {
            if (envelope == null || !NormalizeProfileEnvelope(envelope)) return false;
            if (SaveDataValidation.Validate(envelope).Count != 0) return false;
            if (envelope.activeExpedition == null) return true;
            try
            {
                ExpeditionSaveMapper.ValidateRoomEntryCheckpoint(envelope.activeExpedition);
                return true;
            }
            catch { return false; }
        }

        private static ProfileSaveEnvelope MigrateLegacyProfile(int schemaVersion, int generation, long sequence,
            string savedAtUtc, LegacyProfileSaveDataV1 profile, bool hasActiveExpedition,
            LegacyExpeditionSaveDataV2 activeExpedition,
            PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions)
        {
            var migrated = new ProfileSaveEnvelope
            {
                schemaVersion = schemaVersion,
                contentVersion = SaveFormat.ContentVersion,
                generation = generation,
                sequence = sequence,
                savedAtUtc = savedAtUtc,
                checksum = string.Empty,
                profile = profile == null ? null : new ProfileSaveData
                {
                    availableRenown = profile.availableRenown,
                    healthUpgradeLevel = profile.healthUpgradeLevel,
                    energyUpgradeLevel = profile.energyUpgradeLevel,
                    additionalPotionUpgradeLevel = profile.additionalPotionUpgradeLevel,
                    unlockedRaceIds = Copy(profile.unlockedRaceIds),
                    unlockedStartingKitIds = Copy(profile.unlockedStartingKitIds),
                    acceptedContractIds = Copy(profile.acceptedContractIds),
                    claimedContractIds = Copy(profile.claimedContractIds),
                    readStoryFragmentIds = Copy(profile.readStoryFragmentIds),
                    contractCounters = profile.contractCounters,
                    processedSettlementRunIds = Copy(profile.processedSettlementRunIds),
                    progressionPurchaseTransactions = progressionPurchaseTransactions ?? Array.Empty<PermanentUpgradePurchaseSaveData>()
                },
                hasActiveExpedition = hasActiveExpedition,
                activeExpedition = hasActiveExpedition
                    ? MigrateLegacyExpedition(activeExpedition)
                    : null
            };
            migrated.checksum = ComputeChecksum(migrated);
            return migrated;
        }

        private static ProfileSaveEnvelope MigrateLegacyCurrentProfile(LegacyProfileSaveEnvelopeV4 legacy)
        {
            var migrated = new ProfileSaveEnvelope
            {
                schemaVersion = legacy.schemaVersion,
                contentVersion = SaveFormat.ContentVersion,
                generation = legacy.generation,
                sequence = legacy.sequence,
                savedAtUtc = legacy.savedAtUtc,
                checksum = string.Empty,
                profile = MigrateLegacyProfileDataV5(legacy.profile),
                hasActiveExpedition = legacy.hasActiveExpedition,
                activeExpedition = legacy.activeExpedition == null
                    ? null
                    : MigrateLegacyCurrentExpedition(legacy.activeExpedition)
            };
            migrated.checksum = ComputeChecksum(migrated);
            return migrated;
        }

        private static ProfileSaveEnvelope MigrateLegacyCurrentProfile(LegacyProfileSaveEnvelopeV4Brew legacy)
        {
            var migrated = new ProfileSaveEnvelope
            {
                schemaVersion = legacy.schemaVersion,
                contentVersion = SaveFormat.ContentVersion,
                generation = legacy.generation,
                sequence = legacy.sequence,
                savedAtUtc = legacy.savedAtUtc,
                checksum = string.Empty,
                profile = MigrateLegacyProfileDataV5(legacy.profile),
                hasActiveExpedition = legacy.hasActiveExpedition,
                activeExpedition = legacy.activeExpedition == null
                    ? null
                    : MigrateLegacyCurrentExpedition(legacy.activeExpedition)
            };
            migrated.checksum = ComputeChecksum(migrated);
            return migrated;
        }

        private static ProfileSaveEnvelope MigrateLegacyCurrentProfile(LegacyProfileSaveEnvelopeV5 legacy)
        {
            var migrated = new ProfileSaveEnvelope
            {
                schemaVersion = legacy.schemaVersion,
                contentVersion = SaveFormat.ContentVersion,
                generation = legacy.generation,
                sequence = legacy.sequence,
                savedAtUtc = legacy.savedAtUtc,
                checksum = string.Empty,
                profile = MigrateLegacyProfileDataV5(legacy.profile),
                hasActiveExpedition = legacy.hasActiveExpedition,
                activeExpedition = legacy.activeExpedition == null ? null : legacy.activeExpedition.Clone()
            };
            migrated.checksum = ComputeChecksum(migrated);
            return migrated;
        }

        private static ProfileSaveData MigrateLegacyProfileDataV5(LegacyProfileSaveDataV5 source)
        {
            if (source == null) return null;
            return new ProfileSaveData
            {
                availableRenown = source.availableRenown,
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = Copy(source.unlockedRaceIds),
                unlockedStartingKitIds = Copy(source.unlockedStartingKitIds),
                acceptedContractIds = Copy(source.acceptedContractIds),
                claimedContractIds = Copy(source.claimedContractIds),
                readStoryFragmentIds = Copy(source.readStoryFragmentIds),
                contractCounters = source.contractCounters == null ? null : source.contractCounters
                    .Select(counter => counter == null ? null : counter.Clone()).ToArray(),
                processedSettlementRunIds = Copy(source.processedSettlementRunIds),
                processedContractProgressEvents = Array.Empty<ContractProgressEventReceiptSaveData>(),
                progressionPurchaseTransactions = source.progressionPurchaseTransactions == null ? null : source.progressionPurchaseTransactions
                    .Select(transaction => transaction == null ? null : transaction.Clone()).ToArray()
            };
        }

        private static ExpeditionSaveData MigrateLegacyCurrentExpedition(LegacyExpeditionSaveDataV4 legacy)
        {
            return new ExpeditionSaveData
            {
                runId = legacy.runId,
                seed = legacy.seed,
                generationVersion = legacy.generationVersion,
                contentVersion = legacy.contentVersion,
                classId = legacy.classId,
                raceId = legacy.raceId,
                selectedSkillIds = Copy(legacy.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(legacy.startingEquipmentInstanceIds),
                routeSnapshotJson = legacy.routeSnapshotJson,
                currentNodeId = legacy.currentNodeId,
                visitedNodeIds = Copy(legacy.visitedNodeIds),
                completedRoomIds = Copy(legacy.completedRoomIds),
                completedRoomResults = legacy.completedRoomResults == null ? null : legacy.completedRoomResults
                    .Select(result => result == null ? null : result.Clone()).ToArray(),
                roomEntryCheckpointJson = legacy.roomEntryCheckpointJson,
                inventorySnapshotJson = legacy.inventorySnapshotJson,
                maximumHealth = legacy.maximumHealth,
                currentHealth = legacy.currentHealth,
                maximumEnergy = legacy.maximumEnergy,
                currentEnergy = legacy.currentEnergy,
                healthPotionCharges = legacy.healthPotionCharges,
                frozenPermanentUpgrades = null,
                accumulatedRenown = legacy.accumulatedRenown,
                pactIds = Copy(legacy.pactIds),
                fortuneOfferBookSnapshot = MigrateLegacyFortuneBook(legacy.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(legacy.collectedItemIds),
                claimedRewardIds = Copy(legacy.claimedRewardIds)
            };
        }

        private static ExpeditionSaveData MigrateLegacyCurrentExpedition(LegacyExpeditionSaveDataV4Brew legacy)
        {
            return new ExpeditionSaveData
            {
                runId = legacy.runId,
                seed = legacy.seed,
                generationVersion = legacy.generationVersion,
                contentVersion = legacy.contentVersion,
                classId = legacy.classId,
                raceId = legacy.raceId,
                selectedSkillIds = Copy(legacy.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(legacy.startingEquipmentInstanceIds),
                routeSnapshotJson = legacy.routeSnapshotJson,
                currentNodeId = legacy.currentNodeId,
                visitedNodeIds = Copy(legacy.visitedNodeIds),
                completedRoomIds = Copy(legacy.completedRoomIds),
                completedRoomResults = legacy.completedRoomResults == null ? null : legacy.completedRoomResults
                    .Select(result => result == null ? null : result.Clone()).ToArray(),
                roomEntryCheckpointJson = legacy.roomEntryCheckpointJson,
                inventorySnapshotJson = legacy.inventorySnapshotJson,
                maximumHealth = legacy.maximumHealth,
                currentHealth = legacy.currentHealth,
                maximumEnergy = legacy.maximumEnergy,
                currentEnergy = legacy.currentEnergy,
                healthPotionCharges = legacy.healthPotionCharges,
                frozenPermanentUpgrades = null,
                accumulatedRenown = legacy.accumulatedRenown,
                pactIds = Copy(legacy.pactIds),
                fortuneOfferBookSnapshot = MigrateLegacyFortuneBook(legacy.fortuneOfferBookSnapshot),
                collectedItemIds = Copy(legacy.collectedItemIds),
                claimedRewardIds = Copy(legacy.claimedRewardIds)
            };
        }

        private static FortuneOfferBookSnapshot MigrateLegacyFortuneBook(LegacyFortuneOfferBookSnapshotV4 legacy)
        {
            if (legacy == null) return null;
            return new FortuneOfferBookSnapshot
            {
                snapshotVersion = legacy.snapshotVersion,
                generationVersion = legacy.generationVersion,
                seed = legacy.seed,
                raceId = legacy.raceId,
                charges = legacy.charges,
                offers = legacy.offers,
                processedGuardianIds = Copy(legacy.processedGuardianIds),
                rerollReceipts = legacy.rerollReceipts,
                fortuneBrewReceipts = Array.Empty<FortuneBrewReceiptSnapshot>()
            };
        }

        private static FortuneOfferBookSnapshot MigrateLegacyFortuneBook(LegacyFortuneOfferBookSnapshotV4Brew legacy)
        {
            if (legacy == null) return null;
            return new FortuneOfferBookSnapshot
            {
                snapshotVersion = legacy.snapshotVersion,
                generationVersion = legacy.generationVersion,
                seed = legacy.seed,
                raceId = legacy.raceId,
                charges = legacy.charges,
                offers = legacy.offers,
                processedGuardianIds = Copy(legacy.processedGuardianIds),
                rerollReceipts = legacy.rerollReceipts,
                fortuneBrewReceipts = legacy.fortuneBrewReceipts
            };
        }

        private static LegacyProfileSaveDataV1 ToLegacyProfileV1(LegacyProfileSaveDataV2 source)
        {
            if (source == null) return null;
            return new LegacyProfileSaveDataV1
            {
                availableRenown = source.availableRenown,
                healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel,
                additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = source.unlockedRaceIds,
                unlockedStartingKitIds = source.unlockedStartingKitIds,
                acceptedContractIds = source.acceptedContractIds,
                claimedContractIds = source.claimedContractIds,
                readStoryFragmentIds = source.readStoryFragmentIds,
                contractCounters = source.contractCounters,
                processedSettlementRunIds = source.processedSettlementRunIds
            };
        }

        private static ExpeditionSaveData MigrateLegacyExpedition(LegacyExpeditionSaveDataV2 legacy)
        {
            if (legacy == null) throw new FormatException("Legacy active expedition is missing.");
            var migrated = new ExpeditionSaveData
            {
                runId = legacy.runId,
                seed = legacy.seed,
                generationVersion = legacy.generationVersion,
                contentVersion = SaveFormat.ContentVersion,
                classId = legacy.classId,
                raceId = legacy.raceId,
                selectedSkillIds = Copy(legacy.selectedSkillIds),
                startingEquipmentInstanceIds = Copy(legacy.startingEquipmentInstanceIds),
                routeSnapshotJson = legacy.routeSnapshotJson,
                currentNodeId = legacy.currentNodeId,
                visitedNodeIds = Copy(legacy.visitedNodeIds),
                completedRoomIds = Copy(legacy.completedRoomIds),
                completedRoomResults = legacy.completedRoomResults,
                roomEntryCheckpointJson = string.Empty,
                inventorySnapshotJson = legacy.inventorySnapshotJson,
                maximumHealth = legacy.maximumHealth,
                currentHealth = legacy.currentHealth,
                maximumEnergy = legacy.maximumEnergy,
                currentEnergy = legacy.currentEnergy,
                healthPotionCharges = legacy.healthPotionCharges,
                accumulatedRenown = legacy.accumulatedRenown,
                pactIds = Copy(legacy.pactIds),
                fortuneOfferBookSnapshot = MigrateLegacyFortuneBook(legacy.generationVersion, legacy.seed,
                    legacy.raceId, legacy.fortunaCharges, legacy.pendingOfferIds, legacy.offerRerollCounts),
                collectedItemIds = Copy(legacy.collectedItemIds),
                claimedRewardIds = Copy(legacy.claimedRewardIds)
            };
            migrated.roomEntryCheckpointJson = MigrateLegacyCheckpoint(legacy.roomEntryCheckpointJson, migrated);
            return migrated;
        }

        private static string MigrateLegacyCheckpoint(string checkpointJson, ExpeditionSaveData expedition)
        {
            if (string.IsNullOrWhiteSpace(checkpointJson)) return string.Empty;
            LegacyExpeditionRoomCheckpointSaveDataV2 legacy;
            try { legacy = JsonUtility.FromJson<LegacyExpeditionRoomCheckpointSaveDataV2>(checkpointJson); }
            catch (Exception exception) { throw new FormatException("Legacy room-entry checkpoint is invalid.", exception); }
            if (legacy == null) throw new FormatException("Legacy room-entry checkpoint is missing.");
            var migrated = new ExpeditionRoomCheckpointSaveData
            {
                roomNodeId = legacy.roomNodeId,
                inventorySnapshotJson = legacy.inventorySnapshotJson,
                maximumHealth = legacy.maximumHealth,
                currentHealth = legacy.currentHealth,
                maximumEnergy = legacy.maximumEnergy,
                currentEnergy = legacy.currentEnergy,
                healthPotionCharges = legacy.healthPotionCharges,
                accumulatedRenown = legacy.accumulatedRenown,
                fortuneOfferBookSnapshot = MigrateLegacyFortuneBook(expedition.generationVersion, expedition.seed,
                    expedition.raceId, legacy.fortunaCharges, legacy.pendingOfferIds, legacy.offerRerollCounts),
                pactIds = Copy(legacy.pactIds),
                collectedItemIds = Copy(legacy.collectedItemIds),
                claimedRewardIds = Copy(legacy.claimedRewardIds)
            };
            return JsonUtility.ToJson(migrated);
        }

        private static FortuneOfferBookSnapshot MigrateLegacyFortuneBook(int generationVersion, uint seed, string raceId,
            int charges, string[] pendingOfferIds, int[] offerRerollCounts)
        {
            // The old summary arrays do not contain choices, contexts, option sets, or history. Do not invent rewards.
            if ((pendingOfferIds != null && pendingOfferIds.Length != 0) ||
                (offerRerollCounts != null && offerRerollCounts.Length != 0))
                throw new FormatException("Legacy Fortuna offer summaries are incomplete and cannot be migrated safely.");
            var snapshot = new FortuneOfferBookSnapshot
            {
                snapshotVersion = FortuneOfferBook.SnapshotVersion,
                generationVersion = generationVersion,
                seed = seed,
                raceId = raceId,
                charges = charges,
                offers = Array.Empty<FortuneOfferSnapshot>(),
                processedGuardianIds = Array.Empty<string>()
            };
            FortuneOfferBook.Restore(snapshot);
            return snapshot;
        }

        private static string ComputeLegacyProfileChecksum(LegacyProfileSaveEnvelopeV1 source)
        {
            source.checksum = string.Empty;
            return Hash(JsonUtility.ToJson(source, false));
        }

        private static string ComputeLegacyProfileChecksum(LegacyProfileSaveEnvelopeV2 source)
        {
            source.checksum = string.Empty;
            return Hash(JsonUtility.ToJson(source, false));
        }

        private static string ComputeLegacyProfileChecksum(LegacyProfileSaveEnvelopeV4 source)
        {
            source.checksum = string.Empty;
            return Hash(JsonUtility.ToJson(source, false));
        }

        private static string ComputeLegacyProfileChecksum(LegacyProfileSaveEnvelopeV4Brew source)
        {
            source.checksum = string.Empty;
            return Hash(JsonUtility.ToJson(source, false));
        }

        private static string ComputeLegacyProfileChecksum(LegacyProfileSaveEnvelopeV5 source)
        {
            source.checksum = string.Empty;
            return Hash(JsonUtility.ToJson(source, false));
        }

        private static string ComputeChecksum(ProfileSaveEnvelope source)
        {
            ExpeditionSaveData canonicalExpedition = source.hasActiveExpedition && source.activeExpedition != null
                ? source.activeExpedition.Clone()
                : null;
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
                activeExpedition = source.hasActiveExpedition ? canonicalExpedition : null
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

        private static string ComputeLegacySettingsChecksum(LegacySettingsSaveEnvelope source)
        {
            var canonical = new LegacySettingsSaveEnvelope
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

        private static string ComputeLegacySettingsChecksum(LegacySettingsSaveEnvelopeV4 source)
        {
            source.checksum = string.Empty;
            return Hash(JsonUtility.ToJson(source, false));
        }

        private static string[] Copy(string[] values) => values == null ? null : (string[])values.Clone();

        [Serializable]
        private sealed class SaveVersionHeader { public int schemaVersion; public int contentVersion; }

        [Serializable]
        private sealed class LegacySettingsSaveEnvelope
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyGameSettingsSaveData settings;
        }

        [Serializable]
        private sealed class LegacySettingsSaveEnvelopeV4
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyGameSettingsSaveDataV4 settings;
        }

        [Serializable]
        private sealed class LegacyGameSettingsSaveData
        {
            public string languageId;
            public float masterVolume;
            public float musicVolume;
            public float effectsVolume;
        }

        [Serializable]
        private sealed class LegacyGameSettingsSaveDataV4
        {
            public string languageId;
            public float masterVolume;
            public float musicVolume;
            public float effectsVolume;
            public float ambienceVolume;
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
            public LegacyExpeditionSaveDataV2 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV2
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileSaveDataV2 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveDataV2 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV4
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            // Content v4 predates durable contract-event receipts. Keep this exact DTO shape so
            // checksum verification never serializes the newly added v6 field.
            public LegacyProfileSaveDataV5 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveDataV4 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV4Brew
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            // The Brew variation changes the active-expedition DTO only, not the profile shape.
            public LegacyProfileSaveDataV5 profile;
            public bool hasActiveExpedition;
            public LegacyExpeditionSaveDataV4Brew activeExpedition;
        }

        [Serializable]
        private sealed class LegacyProfileSaveEnvelopeV5
        {
            public int schemaVersion;
            public int contentVersion;
            public int generation;
            public long sequence;
            public string savedAtUtc;
            public string checksum;
            public LegacyProfileSaveDataV5 profile;
            public bool hasActiveExpedition;
            public ExpeditionSaveData activeExpedition;
        }

        [Serializable]
        private class LegacyProfileSaveDataV1
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

        [Serializable]
        private sealed class LegacyProfileSaveDataV2
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
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions;
        }

        [Serializable]
        private sealed class LegacyProfileSaveDataV5
        {
            public int availableRenown;
            public int healthUpgradeLevel;
            public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel;
            public string[] unlockedRaceIds = Array.Empty<string>();
            public string[] unlockedStartingKitIds = Array.Empty<string>();
            public string[] acceptedContractIds = Array.Empty<string>();
            public string[] claimedContractIds = Array.Empty<string>();
            public string[] readStoryFragmentIds = Array.Empty<string>();
            public ContractCounterSaveData[] contractCounters = Array.Empty<ContractCounterSaveData>();
            public string[] processedSettlementRunIds = Array.Empty<string>();
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions = Array.Empty<PermanentUpgradePurchaseSaveData>();
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveDataV2
        {
            public string runId;
            public uint seed;
            public int generationVersion;
            public int contentVersion;
            public string classId;
            public string raceId;
            public string[] selectedSkillIds = Array.Empty<string>();
            public string[] startingEquipmentInstanceIds = Array.Empty<string>();
            public string routeSnapshotJson;
            public string currentNodeId;
            public string[] visitedNodeIds = Array.Empty<string>();
            public string[] completedRoomIds = Array.Empty<string>();
            public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
            public string roomEntryCheckpointJson;
            public string inventorySnapshotJson;
            public int maximumHealth = 100;
            public int currentHealth = 100;
            public int maximumEnergy = 50;
            public int currentEnergy = 50;
            public int healthPotionCharges = 2;
            public int accumulatedRenown;
            public string[] pactIds = Array.Empty<string>();
            public int fortunaCharges;
            public string[] pendingOfferIds = Array.Empty<string>();
            public int[] offerRerollCounts = Array.Empty<int>();
            public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveDataV4
        {
            public string runId;
            public uint seed;
            public int generationVersion;
            public int contentVersion;
            public string classId;
            public string raceId;
            public string[] selectedSkillIds = Array.Empty<string>();
            public string[] startingEquipmentInstanceIds = Array.Empty<string>();
            public string routeSnapshotJson;
            public string currentNodeId;
            public string[] visitedNodeIds = Array.Empty<string>();
            public string[] completedRoomIds = Array.Empty<string>();
            public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
            public string roomEntryCheckpointJson;
            public string inventorySnapshotJson;
            public int maximumHealth = 100;
            public int currentHealth = 100;
            public int maximumEnergy = 50;
            public int currentEnergy = 50;
            public int healthPotionCharges = 2;
            public int accumulatedRenown;
            public string[] pactIds = Array.Empty<string>();
            public LegacyFortuneOfferBookSnapshotV4 fortuneOfferBookSnapshot;
            public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyExpeditionSaveDataV4Brew
        {
            public string runId;
            public uint seed;
            public int generationVersion;
            public int contentVersion;
            public string classId;
            public string raceId;
            public string[] selectedSkillIds = Array.Empty<string>();
            public string[] startingEquipmentInstanceIds = Array.Empty<string>();
            public string routeSnapshotJson;
            public string currentNodeId;
            public string[] visitedNodeIds = Array.Empty<string>();
            public string[] completedRoomIds = Array.Empty<string>();
            public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
            public string roomEntryCheckpointJson;
            public string inventorySnapshotJson;
            public int maximumHealth = 100;
            public int currentHealth = 100;
            public int maximumEnergy = 50;
            public int currentEnergy = 50;
            public int healthPotionCharges = 2;
            public int accumulatedRenown;
            public string[] pactIds = Array.Empty<string>();
            public LegacyFortuneOfferBookSnapshotV4Brew fortuneOfferBookSnapshot;
            public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyFortuneOfferBookSnapshotV4
        {
            public int snapshotVersion;
            public int generationVersion;
            public uint seed;
            public string raceId;
            public int charges;
            public FortuneOfferSnapshot[] offers = Array.Empty<FortuneOfferSnapshot>();
            public string[] processedGuardianIds = Array.Empty<string>();
            public FortuneRerollReceiptSnapshot[] rerollReceipts = Array.Empty<FortuneRerollReceiptSnapshot>();
        }

        [Serializable]
        private sealed class LegacyFortuneOfferBookSnapshotV4Brew
        {
            public int snapshotVersion;
            public int generationVersion;
            public uint seed;
            public string raceId;
            public int charges;
            public FortuneOfferSnapshot[] offers = Array.Empty<FortuneOfferSnapshot>();
            public string[] processedGuardianIds = Array.Empty<string>();
            public FortuneRerollReceiptSnapshot[] rerollReceipts = Array.Empty<FortuneRerollReceiptSnapshot>();
            public FortuneBrewReceiptSnapshot[] fortuneBrewReceipts = Array.Empty<FortuneBrewReceiptSnapshot>();
        }

        [Serializable]
        private sealed class LegacyExpeditionRoomCheckpointSaveDataV2
        {
            public string roomNodeId;
            public string inventorySnapshotJson;
            public int maximumHealth;
            public int currentHealth;
            public int maximumEnergy;
            public int currentEnergy;
            public int healthPotionCharges;
            public int accumulatedRenown;
            public int fortunaCharges;
            public string[] pactIds = Array.Empty<string>();
            public string[] pendingOfferIds = Array.Empty<string>();
            public int[] offerRerollCounts = Array.Empty<int>();
            public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
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
