#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Explicit Editor-only storage experiment. Never publishes an application run.</summary>
    public sealed partial class SaveRepository
    {
        private static readonly UTF8Encoding V7Utf8 = new UTF8Encoding(false, true);
        private bool _v7ProfileProtocolLatched;
        private SaveV7LoadView _v7StorageView;

        internal async Task<SaveV7LoadView> LoadProfileV7ForPreproductionAsync()
        {
            await _gate.WaitAsync();
            try
            {
                // A pending write belongs to its original protocol until it finishes. Do not
                // latch, read storage, clear a cache, or replace the pending on this path.
                if (_pendingProfile != null) return V7Rejected(SaveV7ErrorCode.WritePending,
                    "A profile write is pending; loading cannot replace it.");
                LatchV7UnderLock();
                return await LoadV7UnderLockAsync();
            }
            finally { _gate.Release(); }
        }

        internal async Task<SaveWriteResult> CommitPinnedV7ForPreproductionAsync(
            PinnedProfileCandidateV7 candidate, ExpeditionRunConfiguration run,
            EncounterCatalogRegistryV1 registry)
        {
            await _gate.WaitAsync();
            try
            {
                if (_pendingProfile != null)
                    return Failed("WritePending: retry the exact pending profile before another commit.");
                LatchV7UnderLock();
                if (_v7StorageView == null) await LoadV7UnderLockAsync();
                if (_v7StorageView.Access != SaveDataAccess.ReadWrite)
                    return Failed(_v7StorageView.Error.Code + ": profile storage is not writable.");
                if (candidate == null) return Failed("InvalidData: a pinned candidate is required.");

                ProfileSaveEnvelopeV7 prior = _v7StorageView.Envelope;
                ProfileSaveEnvelope legacy = _v7StorageView.LegacyDiagnostic;
                int generation = prior != null ? prior.generation : legacy == null ? 0 : legacy.generation;
                long sequence = prior != null ? prior.sequence : legacy == null ? 0 : legacy.sequence;
                ProfileSaveEnvelopeV7 envelope;
                string json;
                try
                {
                    envelope = candidate.CreateEnvelope(NextGeneration(generation), NextSequence(sequence),
                        DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
                    if (!SaveV7CandidateMapper.TryValidateCommit(envelope, run, registry, out SaveV7Error error))
                        return Failed(error.Code + ": " + error.Message);
                    if (!SaveV7CandidateMapper.TryValidateTransition(prior, legacy, envelope, out error))
                        return Failed(error.Code + ": " + error.Message);
                    byte[] bytes = SaveV7JsonCodec.EncodeProfile(envelope);
                    json = V7Utf8.GetString(bytes);
                    // Store the authenticated result retained with the exact encoded bytes.
                    if (!SaveV7JsonCodec.TryDecodeProfile(bytes, out envelope, out error))
                        return Failed(error.Code + ": " + error.Message);
                }
                catch (SaveV7ValidationException exception)
                { return Failed(exception.Error.Code + ": " + exception.Message); }
                catch (Exception exception) when (exception is ArgumentException || exception is FormatException ||
                    exception is OverflowException || exception is InvalidOperationException)
                { return Failed("InvalidData: " + exception.Message); }

                // One profile pending, one document, one backup and the same gate as protocol 6.
                _pendingProfile = new PendingWrite(SaveDocument.Profile, json,
                    _profilePrimaryValid ? _profilePrimaryJson : null, envelope, 7);
                return await CommitPendingUnderLockAsync(_pendingProfile);
            }
            finally { _gate.Release(); }
        }

        private void LatchV7UnderLock()
        {
            if (_v7ProfileProtocolLatched) return;
            if (_pendingProfile != null) throw new InvalidOperationException("A pending profile prevents a protocol switch.");
            _v7ProfileProtocolLatched = true;
            _profile = null;
            _profileAccess = SaveDataAccess.Unavailable;
            _profileBlocked = true;
            _profileLoaded = true;
            _profilePrimaryJson = null;
            _loadedProfileJson = null;
            _profilePrimaryValid = false;
        }

        private static SaveLoadResult<ProfileSaveEnvelope> ProfileProtocolConflictV6()
        {
            return new SaveLoadResult<ProfileSaveEnvelope>(SaveLoadStatus.Incompatible, null,
                "ProtocolConflict: this repository is latched to explicit V7 preproduction.",
                SaveDataAccess.Unavailable);
        }

        private async Task<SaveV7LoadView> LoadV7UnderLockAsync()
        {
            SaveStorageSnapshot storage;
            try { storage = await _store.ReadAsync(SaveDocument.Profile); }
            catch (Exception exception)
            {
                _v7StorageView = V7Rejected(SaveV7ErrorCode.Corrupted, "Profile read failed: " + exception.Message);
                return _v7StorageView;
            }

            SaveV7LoadView primary = DecodeV7Storage(storage.PrimaryJson, SaveLoadStatus.LoadedPrimary);
            // Unsupported future primary always protects itself against an older backup.
            if (IsV7Usable(primary) || primary.Status == SaveLoadStatus.Incompatible)
                return SelectV7UnderLock(primary, IsV7Usable(primary));

            SaveV7LoadView backup = DecodeV7Storage(storage.BackupJson, SaveLoadStatus.RecoveredBackup);
            if (IsV7Usable(backup) || backup.Status == SaveLoadStatus.Incompatible)
                return SelectV7UnderLock(backup, false);
            if (primary.Status == SaveLoadStatus.Missing && backup.Status == SaveLoadStatus.Missing)
                return SelectV7UnderLock(new SaveV7LoadView(SaveLoadStatus.Missing, 0,
                    SaveDataAccess.ReadWrite, null, null, null, default(SaveV7Error)), false);
            return SelectV7UnderLock(new SaveV7LoadView(SaveLoadStatus.Corrupted, 0,
                SaveDataAccess.Unavailable, storage.PrimaryJson ?? storage.BackupJson, null, null,
                new SaveV7Error(SaveV7ErrorCode.Corrupted, "profile", "Both profile copies are invalid; bytes were preserved.")), false);
        }

        private SaveV7LoadView SelectV7UnderLock(SaveV7LoadView view, bool validPrimary)
        {
            _v7StorageView = view;
            _profilePrimaryJson = validPrimary ? view.OriginalJson : null;
            _profilePrimaryValid = validPrimary;
            _loadedProfileJson = view.OriginalJson;
            // Ordinary application access intentionally remains unavailable after every V7 load.
            _profile = null;
            _profileAccess = SaveDataAccess.Unavailable;
            return view;
        }

        private static bool IsV7Usable(SaveV7LoadView view)
        {
            return view.Status == SaveLoadStatus.LoadedPrimary || view.Status == SaveLoadStatus.RecoveredBackup;
        }

        private static SaveV7LoadView DecodeV7Storage(string json, SaveLoadStatus successStatus)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new SaveV7LoadView(SaveLoadStatus.Missing, 0, SaveDataAccess.ReadWrite,
                    json, null, null, default(SaveV7Error));
            V7VersionProbe probe;
            try { probe = JsonUtility.FromJson<V7VersionProbe>(json); }
            catch (Exception) { return V7Rejected(SaveV7ErrorCode.Corrupted, "Invalid profile JSON.", json); }
            if (probe == null) return V7Rejected(SaveV7ErrorCode.Corrupted, "Missing profile header.", json);
            // This unauthenticated probe selects a decoder only. It is never source provenance.
            if (probe.schemaVersion == 1 && probe.contentVersion >= 1 && probe.contentVersion <= 6)
            {
                DecodeStatus status = SaveJsonCodec.Decode(json, out ProfileSaveEnvelope legacy, out int sourceVersion);
                if (status != DecodeStatus.Valid)
                    return V7Rejected(status == DecodeStatus.Incompatible ? SaveV7ErrorCode.Incompatible :
                        SaveV7ErrorCode.Corrupted, "Legacy profile authentication failed.", json);
                bool active = legacy.hasActiveExpedition && legacy.activeExpedition != null;
                return new SaveV7LoadView(successStatus, sourceVersion,
                    active ? SaveDataAccess.ReadOnlyLegacyEncounterPinMissing : SaveDataAccess.ReadWrite,
                    json, null, legacy, active ? new SaveV7Error(SaveV7ErrorCode.LegacyEncounterPinMissing,
                        "activeExpedition.encounterPlanPin", "An active legacy run has no durable encounter pin.") : default(SaveV7Error));
            }
            if (probe.schemaVersion != 1 || probe.contentVersion != 7)
                return V7Rejected(SaveV7ErrorCode.Incompatible, "Unsupported profile version; bytes were preserved.", json);
            try
            {
                if (!SaveV7JsonCodec.TryDecodeProfile(V7Utf8.GetBytes(json), out ProfileSaveEnvelopeV7 envelope,
                    out SaveV7Error error))
                    return new SaveV7LoadView(error.Code == SaveV7ErrorCode.Incompatible ? SaveLoadStatus.Incompatible :
                        SaveLoadStatus.Corrupted, 0, SaveDataAccess.Unavailable, json, null, null, error);
                return new SaveV7LoadView(successStatus, envelope.contentVersion, SaveDataAccess.ReadWrite,
                    json, envelope, null, default(SaveV7Error));
            }
            catch (EncoderFallbackException)
            { return V7Rejected(SaveV7ErrorCode.Corrupted, "Profile is not strict UTF-8 text.", json); }
        }

        private static SaveV7LoadView V7Rejected(SaveV7ErrorCode code, string message, string json = null)
        {
            return new SaveV7LoadView(code == SaveV7ErrorCode.Incompatible ? SaveLoadStatus.Incompatible :
                SaveLoadStatus.Corrupted, 0, SaveDataAccess.Unavailable, json, null, null,
                new SaveV7Error(code, "profile", message));
        }

        private async Task<SaveWriteResult> CommitPendingV7UnderLockAsync(PendingWrite pending)
        {
            if (!_v7ProfileProtocolLatched || pending.Document != SaveDocument.Profile || pending.Protocol != 7 ||
                !ReferenceEquals(pending, _pendingProfile))
                return Failed("ProtocolConflict: the pending does not belong to this V7 profile protocol.");
            try
            {
                await _store.WriteAsync(SaveDocument.Profile, pending.Json, pending.LastKnownGoodPrimaryJson);
                ProfileSaveEnvelopeV7 envelope = (ProfileSaveEnvelopeV7)pending.Envelope;
                _v7StorageView = new SaveV7LoadView(SaveLoadStatus.LoadedPrimary, 7,
                    SaveDataAccess.ReadWrite, pending.Json, envelope, null, default(SaveV7Error));
                _profilePrimaryJson = pending.Json;
                _loadedProfileJson = pending.Json;
                _profilePrimaryValid = true;
                _pendingProfile = null;
                _profile = null;
                _profileAccess = SaveDataAccess.Unavailable;
                return new SaveWriteResult(true, "V7 profile storage synchronized; no application run was published.");
            }
            catch (Exception exception)
            { return Failed("WritePending: the exact V7 profile bytes are retained: " + exception.Message); }
        }

        [Serializable]
        private sealed class V7VersionProbe
        {
            public int schemaVersion;
            public int contentVersion;
        }
    }

    internal sealed class SaveV7LoadView
    {
        private readonly ProfileSaveEnvelopeV7 _envelope;
        private readonly ProfileSaveEnvelope _legacy;

        internal SaveV7LoadView(SaveLoadStatus status, int sourceContentVersion, SaveDataAccess access,
            string originalJson, ProfileSaveEnvelopeV7 envelope, ProfileSaveEnvelope legacyDiagnostic,
            SaveV7Error error)
        {
            Status = status;
            SourceContentVersion = sourceContentVersion;
            Access = access;
            OriginalJson = originalJson;
            _envelope = SaveV7Clone.Clone(envelope);
            _legacy = CloneLegacy(legacyDiagnostic);
            Error = error;
        }

        internal SaveLoadStatus Status { get; }
        internal int SourceContentVersion { get; }
        internal SaveDataAccess Access { get; }
        internal string OriginalJson { get; }
        internal ProfileSaveEnvelopeV7 Envelope => SaveV7Clone.Clone(_envelope);
        internal ProfileSaveEnvelope LegacyDiagnostic => CloneLegacy(_legacy);
        internal SaveV7Error Error { get; }

        private static ProfileSaveEnvelope CloneLegacy(ProfileSaveEnvelope value)
        {
            return value == null ? null : new ProfileSaveEnvelope
            {
                schemaVersion = value.schemaVersion, contentVersion = value.contentVersion,
                generation = value.generation, sequence = value.sequence, savedAtUtc = value.savedAtUtc,
                checksum = value.checksum, profile = value.profile == null ? null : value.profile.Clone(),
                hasActiveExpedition = value.hasActiveExpedition,
                activeExpedition = SaveV7CandidateMapper.CloneLegacyExpedition(value.activeExpedition)
            };
        }
    }
}
#endif
