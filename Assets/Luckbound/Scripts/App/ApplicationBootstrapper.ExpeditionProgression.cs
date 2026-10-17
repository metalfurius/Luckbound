using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;

namespace Luckbound.App
{
    public sealed partial class ApplicationBootstrapper
    {
        private PendingRoomAdvance _pendingRoomAdvance;

        public bool HasPendingRoomAdvance => _pendingRoomAdvance != null;

        /// <summary>
        /// Stages the next room, writes the completion result and new route cursor in one profile payload, and only
        /// then publishes the prepared room. A failed profile write retains this exact candidate and room lease.
        /// </summary>
        public async Task<RoomAdvanceCommitResult> CompleteAndAdvanceRoomAsync(
            ExpeditionRoomTransitionController transitionController, RoomInstanceId sourceRoomId,
            string targetNodeId, string resultId, string resultJson, CancellationToken cancellationToken = default)
        {
            if (_pendingRoomAdvance != null)
            {
                if (!_pendingRoomAdvance.Matches(transitionController, sourceRoomId, targetNodeId, resultId, resultJson))
                    return RoomAdvanceCommitResult.Failure(true,
                        "A room advance is pending; retry the exact source, destination and result payload.");
                return await RetryPendingRoomAdvanceAsync();
            }

            await _profileMutationGate.WaitAsync(cancellationToken);
            ExpeditionRoomTransitionController.PreparedRoomTransition prepared = null;
            bool retainPrepared = false;
            bool retainMutationGate = false;
            try
            {
                if (_pendingRoomAdvance != null)
                {
                    if (!_pendingRoomAdvance.Matches(transitionController, sourceRoomId, targetNodeId, resultId, resultJson))
                        return RoomAdvanceCommitResult.Failure(true,
                            "A room advance is pending; retry the exact source, destination and result payload.");
                    return await RetryPendingRoomAdvanceUnderHeldGateAsync();
                }
                if (!CanCommitRoomAdvance(out string unavailableReason))
                    return RoomAdvanceCommitResult.Failure(false, unavailableReason);
                RoomAdvanceCommitResult replay = ResolveCommittedRoomReplay(sourceRoomId, targetNodeId,
                    resultId, resultJson);
                if (replay != null) return replay;

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

                cancellationToken.ThrowIfCancellationRequested();
                if (!transitionController.TryPrepareTransition(sourceRoomId, targetNodeId, out prepared))
                    return RoomAdvanceCommitResult.Failure(false, transitionController.LastTransitionError);

                ExpeditionSaveData candidate;
                try
                {
                    candidate = ExpeditionSaveMapper.CompleteAndAdvanceRoom(_activeExpedition, resultId,
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

                IReadOnlyList<string> validationErrors = SaveDataValidation.Validate(candidate);
                if (validationErrors.Count != 0)
                    return RoomAdvanceCommitResult.Failure(false, string.Join(" ", validationErrors));

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
                        _pendingRoomAdvance = new PendingRoomAdvance(transitionController, prepared, sourceRoomId,
                            targetNodeId, resultId, resultJson, candidate);
                        retainPrepared = true;
                        // Keep every other profile mutation behind this exact retry; the repository owns its failed JSON.
                        retainMutationGate = true;
                        return RoomAdvanceCommitResult.Failure(true, save.Message);
                    }
                    return RoomAdvanceCommitResult.Failure(false, save.Message);
                }

                PublishRoomAdvanceCandidate(candidate);
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
                            _pendingRoomAdvance = new PendingRoomAdvance(transitionController, prepared, sourceRoomId,
                                targetNodeId, resultId, resultJson, candidate) { DurableCommitted = true };
                            retainPrepared = true;
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
                if (!retainPrepared && prepared != null && prepared.IsPending) prepared.Dispose();
                if (!retainMutationGate) _profileMutationGate.Release();
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

                    PublishRoomAdvanceCandidate(pending.Candidate);
                    pending.DurableCommitted = true;
                }

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

                _pendingRoomAdvance = null;
                if (pending.PreparedTransition.IsPending) pending.PreparedTransition.Dispose();
                retainMutationGate = false;
                return new RoomAdvanceCommitResult(true, false, false, activated,
                    activated ? "The room result and route cursor were saved and activated." :
                    "The room result and route cursor were saved; the room owner was disposed before activation.");
            }
            finally
            {
                pending.IsRetrying = false;
                if (!retainMutationGate) _profileMutationGate.Release();
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

        private void PublishRoomAdvanceCandidate(ExpeditionSaveData candidate)
        {
            _activeExpedition = candidate.Clone();
            _activeExpeditionRevision++;
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
                ExpeditionSaveData candidate)
            {
                TransitionController = transitionController;
                PreparedTransition = preparedTransition;
                SourceRoomId = sourceRoomId;
                TargetNodeId = targetNodeId;
                ResultId = resultId;
                ResultJson = resultJson;
                Candidate = candidate.Clone();
            }

            public ExpeditionRoomTransitionController TransitionController { get; }
            public ExpeditionRoomTransitionController.PreparedRoomTransition PreparedTransition { get; }
            public RoomInstanceId SourceRoomId { get; }
            public string TargetNodeId { get; }
            public string ResultId { get; }
            public string ResultJson { get; }
            public ExpeditionSaveData Candidate { get; }
            public bool DurableCommitted { get; set; }
            public bool IsRetrying { get; set; }

            public bool Matches(ExpeditionRoomTransitionController transitionController, RoomInstanceId sourceRoomId,
                string targetNodeId, string resultId, string resultJson)
            {
                return ReferenceEquals(TransitionController, transitionController) && SourceRoomId == sourceRoomId &&
                    string.Equals(TargetNodeId, targetNodeId, StringComparison.Ordinal) &&
                    string.Equals(ResultId, resultId, StringComparison.Ordinal) &&
                    string.Equals(ResultJson, resultJson, StringComparison.Ordinal);
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
