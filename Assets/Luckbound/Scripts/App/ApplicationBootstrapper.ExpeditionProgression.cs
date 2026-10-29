using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using UnityEngine;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        private PendingRoomAdvance _pendingRoomAdvance;
        private int _roomAdvanceInFlightCount;

        public bool HasPendingRoomAdvance => _pendingRoomAdvance != null;
        public bool IsRoomAdvanceInFlight => Volatile.Read(ref _roomAdvanceInFlightCount) > 0;
        private bool IsProfileMutationInFlight => _profileMutationGate.CurrentCount == 0;
        public bool HasPendingSessionMutation => IsRoomAdvanceInFlight || IsProfileMutationInFlight || HasPendingExpeditionWrite ||
            HasPendingRoomAdvance || HasPendingSettlement || HasPendingContractProgression ||
            HasPendingCraftingWrite || HasPendingEconomyMutation || HasPendingPermanentUpgradePurchase;

        /// <summary>
        /// Stages the next room, writes the completion result and new route cursor in one profile payload, and only
        /// then publishes the prepared room. A failed profile write retains this exact candidate and room lease.
        /// </summary>
        public Task<RoomAdvanceCommitResult> CompleteAndAdvanceRoomAsync(
            ExpeditionRoomTransitionController transitionController, RoomInstanceId sourceRoomId,
            string targetNodeId, string resultId, string resultJson, CancellationToken cancellationToken = default)
        {
            if (!TryAcquireRoomAdvanceSimulationPauseLease(out IDisposable simulationPauseLease,
                out string pauseError))
                return Task.FromResult(RoomAdvanceCommitResult.Failure(false, pauseError));

            long expectedExpeditionRevision = ActiveExpeditionRevision;
            ExpeditionRunConfiguration activeRun = ActiveRun;
            ExpeditionInventory currentInventory = activeRun == null ? null : activeRun.Inventory;
            try
            {
                string expectedBaseInventorySnapshotJson = currentInventory == null
                    ? null
                    : JsonUtility.ToJson(currentInventory.CreateSnapshot());
                return TrackRoomAdvanceInFlight(() => CompleteAndAdvanceRoomCoreAsync(transitionController,
                    sourceRoomId, targetNodeId, resultId, resultJson, currentInventory,
                    expectedBaseInventorySnapshotJson, expectedExpeditionRevision, cancellationToken,
                    simulationPauseLease));
            }
            catch
            {
                simulationPauseLease?.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Persists the exact candidate inventory and route advance together, then publishes its run configuration.
        /// Capture the base snapshot and revision together before deriving the candidate; a newer durable or
        /// in-memory inventory mutation invalidates it before the profile write.
        /// </summary>
        public Task<RoomAdvanceCommitResult> CompleteAndAdvanceRoomAsync(
            ExpeditionRoomTransitionController transitionController, RoomInstanceId sourceRoomId,
            string targetNodeId, string resultId, string resultJson, ExpeditionInventory candidateInventory,
            string expectedBaseInventorySnapshotJson, long expectedExpeditionRevision,
            CancellationToken cancellationToken = default)
        {
            if (!TryAcquireRoomAdvanceSimulationPauseLease(out IDisposable simulationPauseLease,
                out string pauseError))
                return Task.FromResult(RoomAdvanceCommitResult.Failure(false, pauseError));

            try
            {
                return TrackRoomAdvanceInFlight(() => CompleteAndAdvanceRoomCoreAsync(transitionController,
                    sourceRoomId, targetNodeId, resultId, resultJson, candidateInventory,
                    expectedBaseInventorySnapshotJson, expectedExpeditionRevision, cancellationToken,
                    simulationPauseLease));
            }
            catch
            {
                simulationPauseLease?.Dispose();
                throw;
            }
        }

        private Task<RoomAdvanceCommitResult> TrackRoomAdvanceInFlight(
            Func<Task<RoomAdvanceCommitResult>> startOperation)
        {
            Interlocked.Increment(ref _roomAdvanceInFlightCount);
            Task<RoomAdvanceCommitResult> operation;
            try
            {
                operation = startOperation();
                if (operation == null)
                    throw new InvalidOperationException("The room advance operation did not return a task.");
            }
            catch
            {
                Interlocked.Decrement(ref _roomAdvanceInFlightCount);
                throw;
            }

            return AwaitRoomAdvanceInFlightAsync(operation);
        }

        private async Task<RoomAdvanceCommitResult> AwaitRoomAdvanceInFlightAsync(
            Task<RoomAdvanceCommitResult> operation)
        {
            try { return await operation; }
            finally { Interlocked.Decrement(ref _roomAdvanceInFlightCount); }
        }

        private async Task<RoomAdvanceCommitResult> CompleteAndAdvanceRoomCoreAsync(
            ExpeditionRoomTransitionController transitionController, RoomInstanceId sourceRoomId,
            string targetNodeId, string resultId, string resultJson, ExpeditionInventory candidateInventory,
            string expectedBaseInventorySnapshotJson, long expectedExpeditionRevision,
            CancellationToken cancellationToken, IDisposable simulationPauseLease)
        {
            bool mutationGateAcquired = false;
            bool retainSimulationPauseLease = false;
            ExpeditionRoomTransitionController.PreparedRoomTransition prepared = null;
            PendingRoomAdvance preparedAdvance = null;
            bool retainPrepared = false;
            bool retainMutationGate = false;
            try
            {
                if (_pendingRoomAdvance != null)
                {
                    if (!_pendingRoomAdvance.Matches(transitionController, sourceRoomId, targetNodeId, resultId,
                        resultJson, candidateInventory, expectedBaseInventorySnapshotJson,
                        expectedExpeditionRevision))
                        return RoomAdvanceCommitResult.Failure(true,
                            "A room advance is pending; retry the exact source, destination, result payload and candidate inventory.");
                    return await RetryPendingRoomAdvanceAsync();
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!_profileMutationGate.Wait(0))
                    return RoomAdvanceCommitResult.Failure(false,
                        "Another profile mutation is in progress; retry the room advance after it settles.");
                mutationGateAcquired = true;
                if (_pendingRoomAdvance != null)
                {
                    if (!_pendingRoomAdvance.Matches(transitionController, sourceRoomId, targetNodeId, resultId,
                        resultJson, candidateInventory, expectedBaseInventorySnapshotJson,
                        expectedExpeditionRevision))
                        return RoomAdvanceCommitResult.Failure(true,
                            "A room advance is pending; retry the exact source, destination, result payload and candidate inventory.");
                    // The retry helper owns this already-held gate and either retains or releases it itself.
                    retainMutationGate = true;
                    return await RetryPendingRoomAdvanceUnderHeldGateAsync();
                }
                if (!CanCommitRoomAdvance(out string unavailableReason))
                    return RoomAdvanceCommitResult.Failure(false, unavailableReason);
                RoomAdvanceCommitResult replay = ResolveCommittedRoomReplay(sourceRoomId, targetNodeId,
                    resultId, resultJson);
                if (replay != null) return replay;
                if (_activeExpeditionRevision != expectedExpeditionRevision)
                    return RoomAdvanceCommitResult.Failure(false,
                        "The active expedition changed after this inventory candidate was captured; rebuild it from the current run and retry.");
                if (!HasSameInventorySnapshotJson(ActiveRun == null ? null : ActiveRun.Inventory,
                    expectedBaseInventorySnapshotJson))
                    return RoomAdvanceCommitResult.Failure(false,
                        "The active inventory changed after this candidate was captured; rebuild it from the current run and retry.");

                string sourceNodeId = _activeExpedition.currentNodeId;
                if (transitionController == null || sourceRoomId != ActiveRoomInstanceId ||
                    transitionController.CurrentRoomId != sourceRoomId ||
                    !string.Equals(transitionController.CurrentNodeId, sourceNodeId, StringComparison.Ordinal))
                    return RoomAdvanceCommitResult.Failure(false,
                        "The active room does not match the expedition's current source room.");
                if (!HasRouteEdge(ActiveRun.Route, sourceNodeId, targetNodeId))
                    return RoomAdvanceCommitResult.Failure(false, "The requested destination is not a saved route edge.");
                if (string.IsNullOrWhiteSpace(resultId) || string.IsNullOrWhiteSpace(resultJson))
                    return RoomAdvanceCommitResult.Failure(false, "A stable result ID and serialized room result are required.");
                if (_activeExpedition.completedRoomResults.Any(result => result != null &&
                    !string.Equals(result.roomNodeId, sourceNodeId, StringComparison.Ordinal) &&
                    string.Equals(result.resultId, resultId, StringComparison.Ordinal)))
                    return RoomAdvanceCommitResult.Failure(false,
                        "The stable room result ID is already assigned to another route node.");

                ExpeditionInventory stableCandidateInventory;
                ExpeditionRunConfiguration candidateRun;
                try
                {
                    if (candidateInventory == null)
                        throw new ArgumentNullException(nameof(candidateInventory));
                    if (!ReferenceEquals(candidateInventory.Catalog, ActiveRun.Inventory.Catalog) ||
                        !string.Equals(candidateInventory.ClassId, ActiveRun.ClassId, StringComparison.Ordinal))
                        throw new InvalidOperationException("The candidate inventory must retain the active run's class and catalog.");

                    stableCandidateInventory = ExpeditionInventory.Restore(
                        ActiveRun.Inventory.Catalog, candidateInventory.CreateSnapshot());
                    candidateRun = ActiveRun.WithInventory(stableCandidateInventory);
                }
                catch (Exception exception)
                {
                    return RoomAdvanceCommitResult.Failure(false, exception.Message);
                }

                if (!candidateRun.RunId.Equals(ActiveRun.RunId) ||
                    !string.Equals(candidateRun.RunId.ToString(), _activeExpedition.runId, StringComparison.Ordinal))
                    return RoomAdvanceCommitResult.Failure(false,
                        "The candidate run must retain the active expedition's RunId.");

                cancellationToken.ThrowIfCancellationRequested();
                if (!transitionController.TryPrepareTransition(sourceRoomId, targetNodeId, out prepared))
                    return RoomAdvanceCommitResult.Failure(false, transitionController.LastTransitionError);

                ExpeditionSaveData candidate;
                try
                {
                    ExpeditionSaveData inventoryCandidate = _activeExpedition.Clone();
                    inventoryCandidate.inventorySnapshotJson = JsonUtility.ToJson(stableCandidateInventory.CreateSnapshot());
                    inventoryCandidate.healthPotionCharges =
                        ExpeditionSaveMapper.GetHealthPotionCharges(stableCandidateInventory);
                    candidate = ExpeditionSaveMapper.CompleteAndAdvanceRoom(inventoryCandidate, resultId,
                        resultJson, targetNodeId);
                }
                catch (Exception exception)
                {
                    return RoomAdvanceCommitResult.Failure(false, exception.Message);
                }

                if (!string.Equals(candidate.currentNodeId, targetNodeId, StringComparison.Ordinal) ||
                    !candidate.completedRoomIds.Contains(sourceNodeId) ||
                    !candidate.completedRoomResults.Any(result => result != null &&
                        string.Equals(result.roomNodeId, sourceNodeId, StringComparison.Ordinal) &&
                        string.Equals(result.resultId, resultId, StringComparison.Ordinal) &&
                        string.Equals(result.resultJson, resultJson, StringComparison.Ordinal)))
                    return RoomAdvanceCommitResult.Failure(false,
                        "The room result and route cursor did not produce one valid candidate.");

                Action publishSession = null;
                if (_expeditionSessionMutationPreparer != null)
                {
                    try
                    {
                        _expeditionSessionStateCapture(candidateRun, candidate);
                        publishSession = _expeditionSessionMutationPreparer(candidateRun, candidate);
                        if (publishSession == null)
                            throw new InvalidOperationException("The live session publisher returned no prepared publication.");
                        candidate.roomEntryCheckpointJson = ExpeditionSaveMapper.SerializeCheckpoint(
                            ExpeditionSaveMapper.CreateCheckpoint(candidate));
                    }
                    catch (Exception exception)
                    {
                        return RoomAdvanceCommitResult.Failure(false,
                            "The live expedition state could not be prepared for the new room: " + exception.Message);
                    }
                }

                IReadOnlyList<string> validationErrors = SaveDataValidation.Validate(candidate);
                if (validationErrors.Count != 0)
                    return RoomAdvanceCommitResult.Failure(false, string.Join(" ", validationErrors));

                preparedAdvance = new PendingRoomAdvance(transitionController, prepared, sourceRoomId,
                    targetNodeId, resultId, resultJson, candidate, candidateRun,
                    expectedBaseInventorySnapshotJson, expectedExpeditionRevision, simulationPauseLease,
                    publishSession);

                cancellationToken.ThrowIfCancellationRequested();
                SaveWriteResult save;
                try
                {
                    save = await _saveRepository.SaveProfileAsync(_profileData, candidate);
                }
                catch (Exception exception)
                {
                    save = new SaveWriteResult(false, exception.Message);
                }

                if (!save.Succeeded)
                {
                    if (_saveRepository.ExportPendingJson(SaveDocument.Profile) != null)
                    {
                        _pendingRoomAdvance = preparedAdvance;
                        retainPrepared = true;
                        retainSimulationPauseLease = simulationPauseLease != null;
                        // Keep every other profile mutation behind this exact retry; the repository owns its failed JSON.
                        retainMutationGate = true;
                        return RoomAdvanceCommitResult.Failure(true, save.Message);
                    }
                    return RoomAdvanceCommitResult.Failure(false, save.Message);
                }

                if (!TryPublishRoomAdvanceCandidate(preparedAdvance, out string publicationError))
                {
                    _pendingRoomAdvance = preparedAdvance;
                    retainPrepared = true;
                    retainSimulationPauseLease = simulationPauseLease != null;
                    retainMutationGate = true;
                    return new RoomAdvanceCommitResult(true, true, false, false,
                        "The route is saved; publishing its live actor will be retried. " + publicationError);
                }
                bool activated;
                try
                {
                    activated = transitionController.TryCommitPreparedTransition(prepared);
                }
                catch (Exception exception)
                {
                    activated = IsRoomActivated(transitionController, targetNodeId);
                    if (!activated)
                    {
                        if (prepared.IsPending)
                        {
                            _pendingRoomAdvance = preparedAdvance;
                            retainPrepared = true;
                            retainSimulationPauseLease = simulationPauseLease != null;
                            retainMutationGate = true;
                        }
                        return new RoomAdvanceCommitResult(true, retainPrepared, false, false,
                            (retainPrepared ? "The route was saved; activating its prepared room will be retried. " :
                                "The route was saved, but its room owner was disposed before activation. ") + exception.Message);
                    }
                }

                return new RoomAdvanceCommitResult(true, false, false, activated,
                    activated ? save.Message : "The route was saved, but the room owner was disposed before activation.");
            }
            finally
            {
                try
                {
                    if (!retainPrepared && prepared != null && prepared.IsPending) prepared.Dispose();
                }
                finally
                {
                    if (!retainMutationGate && mutationGateAcquired) _profileMutationGate.Release();
                    if (!retainSimulationPauseLease) simulationPauseLease?.Dispose();
                }
            }
        }

        /// <summary>Retries only the exact pending room candidate and its prepared room transition.</summary>
        public async Task<RoomAdvanceCommitResult> RetryPendingRoomAdvanceAsync()
        {
            PendingRoomAdvance pending = _pendingRoomAdvance;
            if (pending == null)
            {
                await _profileMutationGate.WaitAsync();
                try
                {
                    return RoomAdvanceCommitResult.Failure(false, "There is no pending room advance.");
                }
                finally { _profileMutationGate.Release(); }
            }

            return await RetryPendingRoomAdvanceUnderHeldGateAsync();
        }

        private async Task<RoomAdvanceCommitResult> RetryPendingRoomAdvanceUnderHeldGateAsync()
        {
            PendingRoomAdvance pending = _pendingRoomAdvance;
            if (pending == null)
                return RoomAdvanceCommitResult.Failure(false, "There is no pending room advance.");
            if (pending.IsRetrying)
                return RoomAdvanceCommitResult.Failure(true, "The exact room advance is already being retried.");

            pending.IsRetrying = true;
            bool retainMutationGate = true;
            try
            {
                if (!pending.DurableCommitted)
                {
                    SaveWriteResult save;
                    try { save = await _saveRepository.RetryPendingAsync(SaveDocument.Profile); }
                    catch (Exception exception) { save = new SaveWriteResult(false, exception.Message); }
                    if (!save.Succeeded)
                        return RoomAdvanceCommitResult.Failure(true, save.Message);
                }

                if (!TryPublishRoomAdvanceCandidate(pending, out string publicationError))
                    return RoomAdvanceCommitResult.Failure(true,
                        "The route is saved; publishing its live actor will be retried. " + publicationError);

                bool activated = false;
                if (pending.PreparedTransition.IsPending && pending.TransitionController != null &&
                    !pending.TransitionController.IsDisposed)
                {
                    try { activated = pending.TransitionController.TryCommitPreparedTransition(pending.PreparedTransition); }
                    catch
                    {
                        activated = IsRoomActivated(pending.TransitionController, pending.TargetNodeId);
                    }
                }

                if (!activated && pending.PreparedTransition.IsPending &&
                    pending.TransitionController != null && !pending.TransitionController.IsDisposed)
                {
                    return new RoomAdvanceCommitResult(true, true, false, false,
                        "The route was saved; its prepared room transition is still waiting to activate.");
                }

                if (pending.PreparedTransition.IsPending) pending.PreparedTransition.Dispose();
                _pendingRoomAdvance = null;
                retainMutationGate = false;
                return new RoomAdvanceCommitResult(true, false, false, activated,
                    activated ? "The room result and route cursor were saved and activated." :
                    "The room result and route cursor were saved; the room owner was disposed before activation.");
            }
            finally
            {
                pending.IsRetrying = false;
                if (!retainMutationGate)
                {
                    try { pending.ReleaseSimulationPauseLease(); }
                    finally { _profileMutationGate.Release(); }
                }
            }
        }

        private bool CanCommitRoomAdvance(out string reason)
        {
            if (!SaveDataReady || !CanContinueExpedition || ActiveRun == null || _activeExpedition == null ||
                !string.IsNullOrEmpty(_activeExpeditionLoadError))
            {
                reason = "There is no safely writable active expedition.";
                return false;
            }
            if (HasPendingExpeditionWrite || HasPendingRoomAdvance || HasPendingSettlement ||
                HasPendingContractProgression || HasPendingCraftingWrite || HasPendingEconomyMutation ||
                HasPendingPermanentUpgradePurchase)
            {
                reason = "Resolve the existing profile operation before advancing the expedition route.";
                return false;
            }
            if (!string.Equals(ActiveRun.RunId.ToString(), _activeExpedition.runId, StringComparison.Ordinal))
            {
                reason = "The active run and saved expedition do not have the same RunId.";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private RoomAdvanceCommitResult ResolveCommittedRoomReplay(RoomInstanceId sourceRoomId, string targetNodeId,
            string resultId, string resultJson)
        {
            if (_activeExpedition.completedRoomResults == null) return null;

            ExpeditionRoomCompletionSaveData existing = _activeExpedition.completedRoomResults.FirstOrDefault(result =>
                result != null && ExpeditionRoomIdentity.ForNode(ActiveRun.RunId, result.roomNodeId) == sourceRoomId);
            if (existing == null) return null;
            if (!string.Equals(existing.resultId, resultId, StringComparison.Ordinal) ||
                !string.Equals(existing.resultJson, resultJson, StringComparison.Ordinal))
            {
                return RoomAdvanceCommitResult.Failure(false,
                    "A different result is already committed for the source room.");
            }
            if (!HasRouteEdge(ActiveRun.Route, existing.roomNodeId, targetNodeId))
            {
                return RoomAdvanceCommitResult.Failure(false,
                    "The requested destination conflicts with the saved route edge.");
            }

            string[] visited = _activeExpedition.visitedNodeIds ?? Array.Empty<string>();
            int sourceIndex = Array.IndexOf(visited, existing.roomNodeId);
            bool edgeWasCommitted = sourceIndex >= 0 && sourceIndex + 1 < visited.Length &&
                string.Equals(visited[sourceIndex + 1], targetNodeId, StringComparison.Ordinal);
            if (edgeWasCommitted)
            {
                return new RoomAdvanceCommitResult(true, false, true, false,
                    "This room result and route edge were already committed.");
            }

            if (!string.Equals(_activeExpedition.currentNodeId, existing.roomNodeId, StringComparison.Ordinal) ||
                visited.Length == 0 || !string.Equals(visited[visited.Length - 1], existing.roomNodeId, StringComparison.Ordinal))
            {
                return RoomAdvanceCommitResult.Failure(false,
                    "The saved room result does not match the current route cursor or its committed outgoing edge.");
            }
            return null;
        }

        private static bool HasRouteEdge(ExpeditionRoutePlan route, string sourceNodeId, string targetNodeId)
        {
            return route != null && !string.IsNullOrWhiteSpace(sourceNodeId) &&
                !string.IsNullOrWhiteSpace(targetNodeId) && route.Connections.Any(connection =>
                    string.Equals(connection.FromNodeId, sourceNodeId, StringComparison.Ordinal) &&
                    string.Equals(connection.ToNodeId, targetNodeId, StringComparison.Ordinal));
        }

        private bool TryPublishRoomAdvanceCandidate(PendingRoomAdvance pending, out string error)
        {
            error = string.Empty;
            // Every caller reaches this method only after the exact candidate is durably stored.
            // Keep that fact separate from actor publication so a retry never replays a completed write.
            pending.DurableCommitted = true;
            if (!pending.RunPublished)
            {
                _activeExpedition = pending.Candidate.Clone();
                ActiveRun = pending.CandidateRun;
                _activeExpeditionRevision++;
                pending.RunPublished = true;
            }
            if (!pending.SessionPublished && pending.SessionPublication != null)
            {
                try
                {
                    pending.SessionPublication();
                    pending.SessionPublished = true;
                }
                catch (Exception exception)
                {
                    error = exception.Message;
                    return false;
                }
            }
            return true;
        }

        private static bool HasSameInventorySnapshot(ExpeditionInventory left, ExpeditionInventory right)
        {
            return left != null && right != null && ReferenceEquals(left.Catalog, right.Catalog) &&
                string.Equals(JsonUtility.ToJson(left.CreateSnapshot()), JsonUtility.ToJson(right.CreateSnapshot()),
                    StringComparison.Ordinal);
        }

        private static bool HasSameInventorySnapshotJson(ExpeditionInventory inventory, string expectedSnapshotJson)
        {
            return inventory != null && !string.IsNullOrWhiteSpace(expectedSnapshotJson) &&
                string.Equals(JsonUtility.ToJson(inventory.CreateSnapshot()), expectedSnapshotJson, StringComparison.Ordinal);
        }

        private bool IsRoomActivated(ExpeditionRoomTransitionController transitionController, string targetNodeId)
        {
            return transitionController != null && !transitionController.IsDisposed && ActiveRun != null &&
                transitionController.CurrentRoomId == ExpeditionRoomIdentity.ForNode(ActiveRun.RunId, targetNodeId) &&
                string.Equals(transitionController.CurrentNodeId, targetNodeId, StringComparison.Ordinal);
        }

        private sealed class PendingRoomAdvance
        {
            public PendingRoomAdvance(ExpeditionRoomTransitionController transitionController,
                ExpeditionRoomTransitionController.PreparedRoomTransition preparedTransition,
                RoomInstanceId sourceRoomId, string targetNodeId, string resultId, string resultJson,
                ExpeditionSaveData candidate, ExpeditionRunConfiguration candidateRun,
                string expectedBaseInventorySnapshotJson, long expectedExpeditionRevision,
                IDisposable simulationPauseLease, Action sessionPublication)
            {
                TransitionController = transitionController;
                PreparedTransition = preparedTransition;
                SourceRoomId = sourceRoomId;
                TargetNodeId = targetNodeId;
                ResultId = resultId;
                ResultJson = resultJson;
                Candidate = candidate.Clone();
                CandidateRun = candidateRun;
                ExpectedBaseInventorySnapshotJson = expectedBaseInventorySnapshotJson;
                ExpectedExpeditionRevision = expectedExpeditionRevision;
                SimulationPauseLease = simulationPauseLease;
                SessionPublication = sessionPublication;
            }

            public ExpeditionRoomTransitionController TransitionController { get; }
            public ExpeditionRoomTransitionController.PreparedRoomTransition PreparedTransition { get; }
            public RoomInstanceId SourceRoomId { get; }
            public string TargetNodeId { get; }
            public string ResultId { get; }
            public string ResultJson { get; }
            public ExpeditionSaveData Candidate { get; }
            public ExpeditionRunConfiguration CandidateRun { get; }
            public string ExpectedBaseInventorySnapshotJson { get; }
            public long ExpectedExpeditionRevision { get; }
            public IDisposable SimulationPauseLease { get; private set; }
            public Action SessionPublication { get; }
            public bool DurableCommitted { get; set; }
            public bool RunPublished { get; set; }
            public bool SessionPublished { get; set; }
            public bool IsRetrying { get; set; }

            public void ReleaseSimulationPauseLease()
            {
                IDisposable lease = SimulationPauseLease;
                SimulationPauseLease = null;
                lease?.Dispose();
            }

            public bool Matches(ExpeditionRoomTransitionController transitionController, RoomInstanceId sourceRoomId,
                string targetNodeId, string resultId, string resultJson, ExpeditionInventory candidateInventory,
                string expectedBaseInventorySnapshotJson, long expectedExpeditionRevision)
            {
                return ReferenceEquals(TransitionController, transitionController) && SourceRoomId == sourceRoomId &&
                    string.Equals(TargetNodeId, targetNodeId, StringComparison.Ordinal) &&
                    string.Equals(ResultId, resultId, StringComparison.Ordinal) &&
                    string.Equals(ResultJson, resultJson, StringComparison.Ordinal) &&
                    string.Equals(ExpectedBaseInventorySnapshotJson, expectedBaseInventorySnapshotJson,
                        StringComparison.Ordinal) &&
                    ExpectedExpeditionRevision == expectedExpeditionRevision &&
                    ApplicationBootstrapper.HasSameInventorySnapshot(candidateInventory, CandidateRun.Inventory);
            }
        }
    }

    public sealed class RoomAdvanceCommitResult
    {
        internal RoomAdvanceCommitResult(bool persisted, bool pending, bool alreadyApplied, bool activated, string message)
        {
            Persisted = persisted;
            Pending = pending;
            AlreadyApplied = alreadyApplied;
            Activated = activated;
            Message = message ?? string.Empty;
        }

        public bool Persisted { get; }
        public bool Pending { get; }
        public bool AlreadyApplied { get; }
        public bool Activated { get; }
        public bool Succeeded => Persisted;
        public string Message { get; }

        internal static RoomAdvanceCommitResult Failure(bool pending, string message) =>
            new RoomAdvanceCommitResult(false, pending, false, false, message);
    }
}
