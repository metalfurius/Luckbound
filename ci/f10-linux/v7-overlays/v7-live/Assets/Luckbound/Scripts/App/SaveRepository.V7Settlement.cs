#if UNITY_EDITOR
using System;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;

namespace Luckbound.App
{
    /// <summary>Explicit Editor-only terminal V7 settlement persistence; never publishes gameplay state.</summary>
    public sealed partial class SaveRepository
    {
        private static readonly object V7ExtractedOwnerFactoryKey = new object();
        private F10ExtractedSettlementV7Owner _v7ExtractedSettlementOwner;

        internal sealed class F10ExtractedSettlementV7Owner
        {
            private bool _writeSucceededAwaitingReadback;
            private bool _readbackConfirmed;

            internal F10ExtractedSettlementV7Owner(object pendingIdentity, string exactSourceProfileJson,
                SaveV7ExtractedSettlementCandidate.Candidate candidate,
                F10ExtractionPermitV7 permit, F10V7SessionHandle sessionHandle,
                object factoryKey)
            {
                if (!ReferenceEquals(factoryKey, V7ExtractedOwnerFactoryKey))
                    throw new InvalidOperationException("Settlement owners must be adopted by SaveRepository.");
                if (pendingIdentity == null || candidate == null || permit == null ||
                    string.IsNullOrEmpty(exactSourceProfileJson))
                    throw new ArgumentException("A V7 settlement owner requires its exact pending, candidate, permit, and source bytes.");
                PendingIdentity = pendingIdentity;
                ExactSourceProfileJson = exactSourceProfileJson;
                Candidate = candidate;
                Permit = permit;
                SessionHandle = sessionHandle;
                EncodedJson = candidate.EncodedJson;
            }

            internal object PendingIdentity { get; }
            internal string ExactSourceProfileJson { get; }
            internal SaveV7ExtractedSettlementCandidate.Candidate Candidate { get; }
            internal F10ExtractionPermitV7 Permit { get; }
            internal F10V7SessionHandle SessionHandle { get; }
            internal string EncodedJson { get; }
            internal bool WriteSucceededAwaitingReadback => _writeSucceededAwaitingReadback;
            internal bool ReadbackConfirmed => _readbackConfirmed;

            internal void MarkWriteSucceeded(object repositoryKey)
            {
                if (!ReferenceEquals(repositoryKey, V7ExtractedOwnerFactoryKey))
                    throw new InvalidOperationException("Only the owning repository may advance settlement I/O state.");
                _writeSucceededAwaitingReadback = true;
            }

            internal void MarkReadbackConfirmed(object repositoryKey)
            {
                if (!ReferenceEquals(repositoryKey, V7ExtractedOwnerFactoryKey))
                    throw new InvalidOperationException("Only the owning repository may confirm settlement readback.");
                _readbackConfirmed = true;
            }
        }

        internal async Task<F10ExtractedSettlementV7WriteResult> CommitF10ExtractedV7ForPreproductionAsync(
            F10ExtractionPermitV7 permit, ItemCatalog itemCatalog, EncounterCatalogRegistryV1 registry)
        {
            F10ExtractedSettlementV7Owner adoptedOwner = null;
            await _gate.WaitAsync();
            try
            {
                if (_f10V7SessionHandle != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: an admitted V7 session owns this profile; unbound settlement is unavailable.");
                // Never adopt or retry an unrelated V6/V7 profile pending on this path.
                if (_pendingProfile != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "WritePending: another profile operation owns the pending slot.");
                if (_v7ExtractedSettlementOwner != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "WritePending: the prior F10 V7 settlement owner must finish or be acknowledged first.");
                if (permit == null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "PhysicalPermitRequired: an App-issued F10 extraction permit is required.");
                if (itemCatalog == null || registry == null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "InvalidData: authoritative item catalog and exact encounter registry are required.");

                LatchV7UnderLock();
                // Always authenticate fresh storage while holding the profile gate; the cached view
                // may predate a separate writer or recovery event.
                await LoadV7UnderLockAsync();
                if (!TryGetAuthenticatedV7Source(out SaveV7LoadView sourceView,
                    out ProfileSaveEnvelopeV7 sourceEnvelope, out SaveV7Error sourceError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        sourceError.Code + ": " + sourceError.Message);

                if (!SaveV7ExtractedSettlementCandidate.TryCreate(sourceEnvelope, itemCatalog,
                    registry, out SaveV7ExtractedSettlementCandidate.Candidate candidate,
                    out SaveV7Error candidateError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        candidateError.Code + ": " + candidateError.Message);

                // This typed capability is the only positive-authority seam. It must bind the exact
                // source bytes/candidate and revalidate the App owner, handoff, readiness and lease.
                if (!permit.TryBindExactProfileAndCandidate(this, sourceView.OriginalJson,
                    sourceEnvelope, candidate, itemCatalog, registry, out string permitError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "PhysicalPermitRejected: " + permitError);

                if (!TryValidateGeneratedCandidate(sourceEnvelope, candidate,
                    out ProfileSaveEnvelopeV7 nextEnvelope, out SaveV7Error generatedError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        generatedError.Code + ": " + generatedError.Message);

                // The candidate's authenticated bytes and object are adopted before storage I/O.
                var pending = new PendingWrite(SaveDocument.Profile, candidate.EncodedJson,
                    _profilePrimaryValid ? _profilePrimaryJson : null, nextEnvelope, 7);
                var owner = new F10ExtractedSettlementV7Owner(pending, sourceView.OriginalJson,
                    candidate, permit, null, V7ExtractedOwnerFactoryKey);
                _v7ExtractedSettlementOwner = owner;
                _pendingProfile = pending;
                adoptedOwner = owner;

                return await WriteExactPendingAndReadbackAsync(owner, pending);
            }
            catch (Exception exception)
            {
                if (adoptedOwner != null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(adoptedOwner,
                        "The adopted F10 V7 settlement remains owned and pending: " + exception.Message);
                return F10ExtractedSettlementV7WriteResult.Failed("InvalidData: " + exception.Message);
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Commits a terminal extraction against the one authenticated V7 session owner. Catalogs
        /// and registry are derived from that handle; callers cannot substitute another revision.
        /// </summary>
        internal async Task<F10ExtractedSettlementV7WriteResult> CommitF10ExtractedV7ForPreproductionAsync(
            F10V7SessionHandle handle, F10ExtractionPermitV7 permit)
        {
            F10ExtractedSettlementV7Owner adoptedOwner = null;
            await _gate.WaitAsync();
            try
            {
                if (handle == null || !IsRetainedF10V7SessionHandle(handle, handle.ExactJson,
                    handle.RunId, handle.ItemCatalog, handle.Registry))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "SessionHandleMismatch: this repository does not retain that authenticated session capability.");
                if (!_v7ProfileProtocolLatched || !_v7V6ProfileWriteAdmissionBarrierArmed ||
                    _v7StorageView == null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: an authenticated, latched V7 session is required.");
                if (_pendingProfile != null || _f10V7PendingSessionMutation != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "WritePending: resolve the exact profile/session mutation before terminal settlement.");
                if (_v7ExtractedSettlementOwner != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "WritePending: another extracted settlement already owns this repository.");
                if (handle.ItemCatalog == null || handle.Registry == null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "CatalogRevisionUnavailable: the authenticated handle has no exact retained catalogs.");

                string expectedSourceJson = _f10V7SessionConfirmedJson ?? handle.ExactJson;
                if (string.IsNullOrWhiteSpace(expectedSourceJson))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "SessionSourceMissing: no exact confirmed session JSON is retained.");

                SessionV7FreshSelection fresh;
                try { fresh = await ReadFreshF10V7SessionSelectionUnderLockAsync(); }
                catch (Exception exception)
                {
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "FreshReadFailed: " + exception.Message);
                }
                if (fresh == null || fresh.View == null ||
                    (fresh.View.Status != SaveLoadStatus.LoadedPrimary &&
                     fresh.View.Status != SaveLoadStatus.RecoveredBackup) ||
                    fresh.View.SourceContentVersion != 7 || fresh.View.Access != SaveDataAccess.ReadWrite ||
                    fresh.View.Envelope == null || !fresh.View.Envelope.hasActiveExpedition ||
                    fresh.View.Envelope.activeExpedition == null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "AuthenticatedActiveV7Required: fresh storage did not select an active writable V7 profile.");
                if (!string.Equals(fresh.View.OriginalJson, expectedSourceJson, StringComparison.Ordinal))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "SessionSourceMismatch: fresh authenticated primary/backup selection differs from the latest exact confirmed JSON.");
                if (!RunId.TryParse(fresh.View.Envelope.activeExpedition.runId, out RunId sourceRunId) ||
                    sourceRunId != handle.RunId)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "RunIdentityMismatch: the fresh V7 profile is not bound to this session handle.");
                if (permit == null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "PhysicalPermitRequired: an App-issued F10 extraction permit is required.");

                if (!SaveV7ExtractedSettlementCandidate.TryCreate(fresh.View.Envelope,
                    handle.ItemCatalog, handle.Registry,
                    out SaveV7ExtractedSettlementCandidate.Candidate candidate,
                    out SaveV7Error candidateError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        candidateError.Code + ": " + candidateError.Message);
                if (!TryValidateGeneratedCandidate(fresh.View.Envelope, candidate,
                    out ProfileSaveEnvelopeV7 nextEnvelope, out SaveV7Error generatedError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        generatedError.Code + ": " + generatedError.Message);

                // This capability is the positive physical-authority seam. It binds the exact
                // selected source/candidate and revalidates the real App owner after the read.
                if (!permit.TryBindExactProfileAndCandidate(this, fresh.View.OriginalJson,
                    fresh.View.Envelope, candidate, handle.ItemCatalog, handle.Registry,
                    out string permitError))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "PhysicalPermitRejected: " + permitError);
                if (candidate.Receipt.RunId != handle.RunId)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "RunIdentityMismatch: the terminal receipt differs from the authenticated session handle.");

                // Retain the handle, exact candidate, physical permit, and App-bound source
                // together before any storage I/O. Every later retry is tied to this owner.
                var pending = new PendingWrite(SaveDocument.Profile, candidate.EncodedJson,
                    fresh.PrimaryValid ? fresh.View.OriginalJson : null, nextEnvelope, 7);
                var owner = new F10ExtractedSettlementV7Owner(pending, fresh.View.OriginalJson,
                    candidate, permit, handle, V7ExtractedOwnerFactoryKey);
                _v7ExtractedSettlementOwner = owner;
                _pendingProfile = pending;
                adoptedOwner = owner;

                return await WriteExactPendingAndReadbackAsync(owner, pending);
            }
            catch (Exception exception)
            {
                if (adoptedOwner != null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(adoptedOwner,
                        "The handle-bound F10 V7 settlement remains owned and pending: " + exception.Message);
                return F10ExtractedSettlementV7WriteResult.Failed("InvalidData: " + exception.Message);
            }
            finally { _gate.Release(); }
        }

        internal async Task<F10ExtractedSettlementV7WriteResult> RetryF10ExtractedV7ForPreproductionAsync(
            F10ExtractedSettlementV7Owner owner)
        {
            await _gate.WaitAsync();
            try
            {
                if (_f10V7SessionHandle != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: an admitted V7 session owns this profile; unbound settlement retry is unavailable.");
                if (owner == null || !ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: the supplied F10 V7 settlement owner is not the retained owner.");

                PendingWrite pending = _pendingProfile;
                if (pending != null && (!ReferenceEquals(pending, owner.PendingIdentity) ||
                    pending.Document != SaveDocument.Profile || pending.Protocol != 7 ||
                    !string.Equals(pending.Json, owner.EncodedJson, StringComparison.Ordinal)))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The retained profile pending no longer matches the owned F10 V7 settlement.");

                // Once WriteAsync returned successfully, retry only the exact primary readback.
                // A failed/syncing pending is retried only with its original object and bytes.
                if (owner.WriteSucceededAwaitingReadback || owner.ReadbackConfirmed)
                    return await ReadbackExactPrimaryUnderLockAsync(owner);
                if (pending == null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The owned settlement has no pending write and no successful sync; it cannot be confirmed.");
                return await WriteExactPendingAndReadbackAsync(owner, pending);
            }
            catch (Exception exception)
            {
                if (owner != null && ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The retained F10 V7 settlement owner remains pending: " + exception.Message);
                return F10ExtractedSettlementV7WriteResult.Failed("ProtocolConflict: " + exception.Message);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Retries only this handle's retained settlement bytes, or reads them back after sync.</summary>
        internal async Task<F10ExtractedSettlementV7WriteResult> RetryF10ExtractedV7ForPreproductionAsync(
            F10V7SessionHandle handle, F10ExtractedSettlementV7Owner owner)
        {
            await _gate.WaitAsync();
            try
            {
                if (!IsRetainedHandleBoundSettlementUnderLock(handle, owner))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: the supplied handle and settlement owner are not retained together.");
                if (_f10V7PendingSessionMutation != null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "A session mutation competes with the retained terminal settlement.");

                PendingWrite pending = _pendingProfile;
                if (pending != null && (!ReferenceEquals(pending, owner.PendingIdentity) ||
                    pending.Document != SaveDocument.Profile || pending.Protocol != 7 ||
                    !string.Equals(pending.Json, owner.EncodedJson, StringComparison.Ordinal)))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "A different profile pending prevents retry; the handle-bound owner remains retained.");

                if (owner.WriteSucceededAwaitingReadback || owner.ReadbackConfirmed)
                    return await ReadbackExactPrimaryUnderLockAsync(owner);
                if (pending == null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The owned settlement has no pending write and no successful sync; it cannot be confirmed.");
                return await WriteExactPendingAndReadbackAsync(owner, pending);
            }
            catch (Exception exception)
            {
                if (owner != null && ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The handle-bound F10 V7 settlement remains pending: " + exception.Message);
                return F10ExtractedSettlementV7WriteResult.Failed("ProtocolConflict: " + exception.Message);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Releases only the completed repository owner after App has observed durable success.</summary>
        internal async Task<F10ExtractedSettlementV7WriteResult> AcknowledgeF10ExtractedV7ForPreproductionAsync(
            F10ExtractedSettlementV7Owner owner)
        {
            await _gate.WaitAsync();
            try
            {
                if (_f10V7SessionHandle != null)
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: an admitted V7 session owns this profile; unbound settlement acknowledgement is unavailable.");
                if (owner == null || !ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: the supplied settlement owner is not retained.");
                if (!owner.WriteSucceededAwaitingReadback || _pendingProfile != null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The owned write has not completed successfully with no pending; acknowledgement is retained.");
                F10ExtractedSettlementV7WriteResult readback = await ReadbackExactPrimaryUnderLockAsync(owner);
                if (!readback.Persisted) return readback;
                F10ExtractedSettlementV7WriteResult acknowledged =
                    F10ExtractedSettlementV7WriteResult.PersistedResult(owner,
                        "The exact durable F10 V7 settlement owner was acknowledged.");
                _v7ExtractedSettlementOwner = null;
                return acknowledged;
            }
            catch (Exception exception)
            {
                if (owner != null && ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The retained F10 V7 settlement owner remains pending acknowledgement: " + exception.Message);
                return F10ExtractedSettlementV7WriteResult.Failed("ProtocolConflict: " + exception.Message);
            }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Closes the admitted session only after its exact terminal primary was synchronized and
        /// authenticated. The V7 latch and V6 admission barrier remain armed.
        /// </summary>
        internal async Task<F10ExtractedSettlementV7WriteResult> AcknowledgeF10ExtractedV7ForPreproductionAsync(
            F10V7SessionHandle handle, F10ExtractedSettlementV7Owner owner)
        {
            await _gate.WaitAsync();
            try
            {
                if (!IsRetainedHandleBoundSettlementUnderLock(handle, owner))
                    return F10ExtractedSettlementV7WriteResult.Failed(
                        "ProtocolConflict: the supplied handle and settlement owner are not retained together.");
                if (_f10V7PendingSessionMutation != null || _pendingProfile != null)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The terminal settlement cannot be acknowledged while any profile/session mutation is pending.");
                if (!owner.WriteSucceededAwaitingReadback)
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The terminal profile write has not synchronized successfully; acknowledgement is blocked.");

                F10ExtractedSettlementV7WriteResult readback =
                    await ReadbackExactPrimaryUnderLockAsync(owner);
                if (!readback.Persisted || !owner.ReadbackConfirmed) return readback;

                F10ExtractedSettlementV7WriteResult acknowledged =
                    F10ExtractedSettlementV7WriteResult.PersistedResult(owner,
                        "The exact terminal V7 primary was confirmed and the admitted session handle was released.");
                _v7ExtractedSettlementOwner = null;
                _f10V7SessionHandle = null;
                return acknowledged;
            }
            catch (Exception exception)
            {
                if (owner != null && ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The handle-bound settlement remains pending acknowledgement: " + exception.Message);
                return F10ExtractedSettlementV7WriteResult.Failed("ProtocolConflict: " + exception.Message);
            }
            finally { _gate.Release(); }
        }

        private bool IsRetainedHandleBoundSettlementUnderLock(F10V7SessionHandle handle,
            F10ExtractedSettlementV7Owner owner)
        {
            return handle != null && owner != null && _v7ProfileProtocolLatched &&
                IsRetainedF10V7SessionHandle(handle, handle.ExactJson, handle.RunId,
                    handle.ItemCatalog, handle.Registry) &&
                ReferenceEquals(_v7ExtractedSettlementOwner, owner) &&
                ReferenceEquals(owner.SessionHandle, handle) &&
                owner.Permit != null && owner.Candidate != null && owner.Candidate.Receipt != null &&
                owner.Candidate.Receipt.RunId == handle.RunId &&
                owner.Permit.IsBoundTo(this, owner.ExactSourceProfileJson, owner.Candidate,
                    handle.ItemCatalog, handle.Registry);
        }

        private bool TryGetAuthenticatedV7Source(out SaveV7LoadView sourceView,
            out ProfileSaveEnvelopeV7 envelope, out SaveV7Error error)
        {
            sourceView = _v7StorageView;
            envelope = null;
            error = default(SaveV7Error);
            if (sourceView == null || sourceView.Access != SaveDataAccess.ReadWrite ||
                (sourceView.Status != SaveLoadStatus.LoadedPrimary &&
                 sourceView.Status != SaveLoadStatus.RecoveredBackup) ||
                sourceView.SourceContentVersion != 7 || string.IsNullOrWhiteSpace(sourceView.OriginalJson))
            {
                error = sourceView != null && sourceView.Error.Code != SaveV7ErrorCode.None
                    ? sourceView.Error
                    : new SaveV7Error(SaveV7ErrorCode.Incompatible, "profile",
                        "An authenticated current V7 source profile is required.");
                return false;
            }

            if (!SaveV7JsonCodec.TryDecodeProfile(V7Utf8.GetBytes(sourceView.OriginalJson),
                out envelope, out error)) return false;
            if (!SaveV7JsonCodec.TryAuthenticateProfile(envelope, out error))
            {
                envelope = null;
                return false;
            }
            return true;
        }

        private static bool TryValidateGeneratedCandidate(ProfileSaveEnvelopeV7 source,
            SaveV7ExtractedSettlementCandidate.Candidate candidate,
            out ProfileSaveEnvelopeV7 next, out SaveV7Error error)
        {
            next = null;
            error = default(SaveV7Error);
            if (source == null || candidate == null || candidate.Receipt == null ||
                string.IsNullOrWhiteSpace(candidate.EncodedJson))
                return FailGenerated(SaveV7ErrorCode.InvalidData, "candidate",
                    "The generated settlement candidate is incomplete.", out error);
            if (!SaveV7JsonCodec.TryDecodeProfile(V7Utf8.GetBytes(candidate.EncodedJson),
                out ProfileSaveEnvelopeV7 decoded, out error)) return false;

            ProfileSaveEnvelopeV7 candidateView = candidate.Envelope;
            ExpeditionSettlementReceipt receipt = candidate.Receipt;
            if (source.activeExpedition == null || !source.hasActiveExpedition ||
                !RunId.TryParse(source.activeExpedition.runId, out RunId sourceRunId) ||
                sourceRunId != receipt.RunId || receipt.Outcome != ExpeditionOutcome.Extracted ||
                receipt.AlreadyProcessed || decoded.hasActiveExpedition || decoded.activeExpedition != null ||
                decoded.profile.availableRenown != receipt.BalanceAfter ||
                !decoded.profile.processedSettlementRunIds.Contains(receipt.RunId.ToString(), StringComparer.Ordinal) ||
                decoded.generation != source.generation + 1 || decoded.sequence != source.sequence + 1 ||
                decoded.generation != candidateView.generation || decoded.sequence != candidateView.sequence ||
                decoded.checksum != candidateView.checksum)
                return FailGenerated(SaveV7ErrorCode.PlanMismatch, "candidate",
                    "Encoded settlement bytes do not represent the generated terminal receipt and envelope.", out error);

            next = decoded;
            error = default(SaveV7Error);
            return true;
        }

        private static bool FailGenerated(SaveV7ErrorCode code, string path, string message,
            out SaveV7Error error)
        {
            error = new SaveV7Error(code, path, message);
            return false;
        }

        private async Task<F10ExtractedSettlementV7WriteResult> WriteExactPendingAndReadbackAsync(
            F10ExtractedSettlementV7Owner owner, PendingWrite pending)
        {
            if (owner == null || pending == null || !ReferenceEquals(owner.PendingIdentity, pending) ||
                !ReferenceEquals(_pendingProfile, pending) || pending.Protocol != 7 ||
                !string.Equals(pending.Json, owner.EncodedJson, StringComparison.Ordinal))
                return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                    "The exact owned V7 profile pending is unavailable for retry.");
            try
            {
                await _store.WriteAsync(SaveDocument.Profile, pending.Json, pending.LastKnownGoodPrimaryJson);
                owner.MarkWriteSucceeded(V7ExtractedOwnerFactoryKey);
            }
            catch (Exception exception)
            {
                return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                    "WritePending: the exact profile bytes remain adopted for retry: " + exception.Message);
            }
            return await ReadbackExactPrimaryUnderLockAsync(owner);
        }

        private async Task<F10ExtractedSettlementV7WriteResult> ReadbackExactPrimaryUnderLockAsync(
            F10ExtractedSettlementV7Owner owner)
        {
            if (owner == null || !ReferenceEquals(owner, _v7ExtractedSettlementOwner))
                return F10ExtractedSettlementV7WriteResult.Failed(
                    "ProtocolConflict: no exact F10 V7 settlement owner is retained.");
            if (!owner.WriteSucceededAwaitingReadback)
                return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                    "The owned profile write has not returned successfully; readback cannot confirm it.");
            try
            {
                SaveStorageSnapshot readback = await _store.ReadAsync(SaveDocument.Profile);
                if (!string.Equals(readback.PrimaryJson, owner.EncodedJson, StringComparison.Ordinal) ||
                    !SaveV7JsonCodec.TryDecodeProfile(V7Utf8.GetBytes(readback.PrimaryJson),
                        out ProfileSaveEnvelopeV7 durable, out SaveV7Error error) ||
                    !IsTerminalF10Envelope(durable, owner))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "The exact terminal V7 bytes are not authenticated as the current primary; owner retained for readback retry.");

                PendingWrite pending = _pendingProfile;
                if (pending != null && (!ReferenceEquals(pending, owner.PendingIdentity) ||
                    !string.Equals(pending.Json, owner.EncodedJson, StringComparison.Ordinal)))
                    return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                        "A different profile pending prevents confirmation; settlement owner remains retained.");

                _v7StorageView = new SaveV7LoadView(SaveLoadStatus.LoadedPrimary, 7,
                    SaveDataAccess.ReadWrite, readback.PrimaryJson, durable, null, default(SaveV7Error));
                _profilePrimaryJson = readback.PrimaryJson;
                _loadedProfileJson = readback.PrimaryJson;
                _profilePrimaryValid = true;
                _pendingProfile = null;
                _profile = null;
                _profileAccess = SaveDataAccess.Unavailable;
                owner.MarkReadbackConfirmed(V7ExtractedOwnerFactoryKey);
                return F10ExtractedSettlementV7WriteResult.PersistedResult(owner,
                    "The exact terminal V7 primary bytes were read back and authenticated.");
            }
            catch (Exception exception)
            {
                return F10ExtractedSettlementV7WriteResult.PendingResult(owner,
                    "Readback is pending; the exact owner and bytes remain retained: " + exception.Message);
            }
        }

        private static bool IsTerminalF10Envelope(ProfileSaveEnvelopeV7 durable,
            F10ExtractedSettlementV7Owner owner)
        {
            if (durable == null || owner == null || durable.hasActiveExpedition ||
                durable.activeExpedition != null || durable.profile == null) return false;
            ExpeditionSettlementReceipt receipt = owner.Candidate.Receipt;
            ProfileSaveEnvelopeV7 candidate = owner.Candidate.Envelope;
            return receipt != null && receipt.Outcome == ExpeditionOutcome.Extracted &&
                !receipt.AlreadyProcessed && durable.contentVersion == 7 &&
                durable.generation == candidate.generation && durable.sequence == candidate.sequence &&
                durable.profile.availableRenown == receipt.BalanceAfter &&
                durable.profile.processedSettlementRunIds != null &&
                durable.profile.processedSettlementRunIds.Contains(receipt.RunId.ToString(), StringComparer.Ordinal);
        }
    }

    internal sealed class F10ExtractedSettlementV7WriteResult
    {
        private F10ExtractedSettlementV7WriteResult(bool persisted, bool pending,
            SaveRepository.F10ExtractedSettlementV7Owner owner, string message)
        {
            Persisted = persisted;
            Pending = pending;
            Owner = owner;
            Candidate = owner == null ? null : owner.Candidate;
            Receipt = Candidate == null ? null : Candidate.Receipt;
            Message = message ?? string.Empty;
        }

        internal bool Persisted { get; }
        internal bool Pending { get; }
        internal SaveRepository.F10ExtractedSettlementV7Owner Owner { get; }
        internal SaveV7ExtractedSettlementCandidate.Candidate Candidate { get; }
        internal ExpeditionSettlementReceipt Receipt { get; }
        internal string Message { get; }

        internal static F10ExtractedSettlementV7WriteResult Failed(string message) =>
            new F10ExtractedSettlementV7WriteResult(false, false, null, message);
        internal static F10ExtractedSettlementV7WriteResult PendingResult(
            SaveRepository.F10ExtractedSettlementV7Owner owner, string message) =>
            new F10ExtractedSettlementV7WriteResult(false, true, owner, message);
        internal static F10ExtractedSettlementV7WriteResult PersistedResult(
            SaveRepository.F10ExtractedSettlementV7Owner owner, string message) =>
            new F10ExtractedSettlementV7WriteResult(true, false, owner, message);
    }
}
#endif
