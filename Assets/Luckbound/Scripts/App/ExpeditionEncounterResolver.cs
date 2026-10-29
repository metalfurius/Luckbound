using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Rooms;

namespace Luckbound.App
{
    /// <summary>Minimal result contract shared by the encounter resolver and its durable App gateway.</summary>
    public sealed class ExpeditionEncounterAdvanceOutcome
    {
        public ExpeditionEncounterAdvanceOutcome(bool persisted, bool pending, bool alreadyApplied, bool activated, string message)
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
        public string Message { get; }

        public static ExpeditionEncounterAdvanceOutcome Failure(bool pending, string message)
        {
            return new ExpeditionEncounterAdvanceOutcome(false, pending, false, false, message);
        }
    }

    /// <summary>
    /// Boundary used by the resolver. Production delegates to ApplicationBootstrapper's durable room API;
    /// tests can verify the callback contract without simulating a win or writing a save.
    /// </summary>
    public interface IExpeditionEncounterAdvanceGateway
    {
        RunId RunId { get; }
        RoomInstanceId CurrentRoomId { get; }
        RoomInstanceId SavedRoomId { get; }
        string CurrentNodeId { get; }
        ExpeditionInventory CurrentInventory { get; }
        long CurrentExpeditionRevision { get; }
        object CurrentRoomScopeToken { get; }

        bool HasRouteEdge(string sourceNodeId, string targetNodeId);
        bool IsRoomScopeActive(object scopeToken, RoomInstanceId roomId);
        bool TryAcquireSimulationPauseLease(out IDisposable lease, out string error);

        Task<ExpeditionEncounterAdvanceOutcome> CompleteAndAdvanceRoomAsync(
            RoomInstanceId sourceRoomId, string targetNodeId, string resultId, string resultJson,
            ExpeditionInventory candidateInventory, string expectedBaseInventorySnapshotJson,
            long expectedExpeditionRevision);
    }

    /// <summary>Production adapter over the existing durable ApplicationBootstrapper API.</summary>
    public sealed class ApplicationEncounterAdvanceGateway : IExpeditionEncounterAdvanceGateway
    {
        private readonly ApplicationBootstrapper _application;
        private readonly ExpeditionRoomTransitionController _transitionController;

        public ApplicationEncounterAdvanceGateway(ApplicationBootstrapper application,
            ExpeditionRoomTransitionController transitionController)
        {
            _application = application ?? throw new ArgumentNullException(nameof(application));
            _transitionController = transitionController ?? throw new ArgumentNullException(nameof(transitionController));
        }

        public RunId RunId => _application.ActiveRun == null ? default : _application.ActiveRun.RunId;
        public RoomInstanceId CurrentRoomId => _transitionController.CurrentRoomId;
        public RoomInstanceId SavedRoomId => _application.ActiveRoomInstanceId;
        public string CurrentNodeId => _transitionController.CurrentNodeId;
        public long CurrentExpeditionRevision => _application.ActiveExpeditionRevision;
        public ExpeditionInventory CurrentInventory => _application.ActiveRun == null
            ? null
            : _application.ActiveRun.Inventory;
        public object CurrentRoomScopeToken => _transitionController.ActiveScope;

        public bool HasRouteEdge(string sourceNodeId, string targetNodeId)
        {
            ExpeditionRunConfiguration run = _application.ActiveRun;
            return run != null && run.Route != null && !string.IsNullOrWhiteSpace(sourceNodeId) &&
                !string.IsNullOrWhiteSpace(targetNodeId) && run.Route.Connections.Any(connection =>
                    string.Equals(connection.FromNodeId, sourceNodeId, StringComparison.Ordinal) &&
                    string.Equals(connection.ToNodeId, targetNodeId, StringComparison.Ordinal));
        }

        public bool IsRoomScopeActive(object scopeToken, RoomInstanceId roomId)
        {
            if (!(scopeToken is RoomLifetimeScope capturedScope)) return false;
            return ReferenceEquals(_transitionController.ActiveScope, capturedScope) &&
                capturedScope.isActiveAndEnabled && !capturedScope.IsDisposed &&
                capturedScope.RoomInstanceId == roomId && _transitionController.CurrentRoomId == roomId;
        }

        public bool TryAcquireSimulationPauseLease(out IDisposable lease, out string error)
        {
            return _application.TryAcquireExpeditionSimulationPauseLease(out lease, out error);
        }

        public async Task<ExpeditionEncounterAdvanceOutcome> CompleteAndAdvanceRoomAsync(
            RoomInstanceId sourceRoomId, string targetNodeId, string resultId, string resultJson,
            ExpeditionInventory candidateInventory, string expectedBaseInventorySnapshotJson,
            long expectedExpeditionRevision)
        {
            RoomAdvanceCommitResult result = await _application.CompleteAndAdvanceRoomAsync(
                _transitionController, sourceRoomId, targetNodeId, resultId, resultJson,
                candidateInventory ?? CurrentInventory, expectedBaseInventorySnapshotJson, expectedExpeditionRevision);
            return new ExpeditionEncounterAdvanceOutcome(result.Persisted, result.Pending,
                result.AlreadyApplied, result.Activated, result.Message);
        }
    }

    /// <summary>
    /// Immutable room activation identity with a canonical completion receipt.
    /// A definitively rejected, non-pending candidate can be rebased while retaining the same receipt.
    /// The result ID is stable across retries and restoration of the same run/node.
    /// </summary>
    public sealed class ExpeditionEncounterResolutionScope
    {
        private bool _hasCompletion;
        private string[] _rewardIds;
        private string _resultJson;
        private ExpeditionInventory _candidateInventory;
        private string _expectedBaseInventorySnapshotJson;
        private long _expectedExpeditionRevision;
        private readonly object _roomScopeToken;
        private bool _attemptInProgress;
        private ExpeditionEncounterAdvanceOutcome _lastAttempt;

        internal ExpeditionEncounterResolutionScope(long activationSequence, RunId runId, string sourceNodeId,
            RoomInstanceId sourceRoomId, string targetNodeId, object roomScopeToken)
        {
            ActivationSequence = activationSequence;
            RunId = runId;
            SourceNodeId = sourceNodeId;
            SourceRoomId = sourceRoomId;
            TargetNodeId = targetNodeId;
            _roomScopeToken = roomScopeToken ?? throw new ArgumentNullException(nameof(roomScopeToken));
            ResultId = "room-result.v1:" + sourceRoomId;
        }

        public long ActivationSequence { get; }
        public RunId RunId { get; }
        public string SourceNodeId { get; }
        public RoomInstanceId SourceRoomId { get; }
        public string TargetNodeId { get; }
        public string ResultId { get; }
        internal object RoomScopeToken => _roomScopeToken;
        public bool HasCompletion => _hasCompletion;
        public string ResultJson => _resultJson;
        public long ExpectedExpeditionRevision => _expectedExpeditionRevision;
        public ExpeditionEncounterAdvanceOutcome LastAttempt => _lastAttempt;
        public string ExpectedBaseInventorySnapshotJson => _expectedBaseInventorySnapshotJson;
        internal ExpeditionInventory CandidateInventory => _candidateInventory;

        internal bool AttemptInProgress
        {
            get => _attemptInProgress;
            set => _attemptInProgress = value;
        }

        internal bool TryCaptureCompletion(IEnumerable<string> rewardIds, ExpeditionInventory candidateInventory,
            string expectedBaseInventorySnapshotJson, long expectedExpeditionRevision)
        {
            if (_hasCompletion || rewardIds == null) return false;

            string[] rewards = NormalizeRewardIds(rewardIds);
            ExpeditionInventory stableCandidateInventory = CloneInventory(candidateInventory);
            _rewardIds = rewards;
            _resultJson = SerializeCompletion(SourceRoomId, SourceNodeId, TargetNodeId, rewards);
            _candidateInventory = stableCandidateInventory;
            _expectedBaseInventorySnapshotJson = expectedBaseInventorySnapshotJson;
            _expectedExpeditionRevision = expectedExpeditionRevision;
            _hasCompletion = true;
            return true;
        }

        internal bool TryRebaseRejectedCompletion(IEnumerable<string> rewardIds,
            ExpeditionInventory candidateInventory, string expectedBaseInventorySnapshotJson,
            long expectedExpeditionRevision)
        {
            if (!_hasCompletion || _attemptInProgress || _lastAttempt == null || _lastAttempt.Pending ||
                _lastAttempt.Persisted || rewardIds == null)
                return false;

            string[] rewards = NormalizeRewardIds(rewardIds);
            if (!_rewardIds.SequenceEqual(rewards, StringComparer.Ordinal)) return false;

            _candidateInventory = CloneInventory(candidateInventory);
            _expectedBaseInventorySnapshotJson = expectedBaseInventorySnapshotJson;
            _expectedExpeditionRevision = expectedExpeditionRevision;
            return true;
        }

        private static string[] NormalizeRewardIds(IEnumerable<string> rewardIds)
        {
            string[] rewards = rewardIds.ToArray();
            if (rewards.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Encounter reward IDs cannot be empty.", nameof(rewardIds));
            if (rewards.Distinct(StringComparer.Ordinal).Count() != rewards.Length)
                throw new ArgumentException("Encounter reward IDs must be unique.", nameof(rewardIds));

            Array.Sort(rewards, StringComparer.Ordinal);
            return rewards;
        }

        private static ExpeditionInventory CloneInventory(ExpeditionInventory inventory)
        {
            return inventory == null
                ? null
                : ExpeditionInventory.Restore(inventory.Catalog, inventory.CreateSnapshot());
        }

        internal void RecordAttempt(ExpeditionEncounterAdvanceOutcome outcome)
        {
            _lastAttempt = outcome;
        }

        private static string SerializeCompletion(RoomInstanceId sourceRoomId, string sourceNodeId,
            string targetNodeId, IReadOnlyList<string> rewards)
        {
            string[] encodedRewards = rewards.Select(QuoteJson).ToArray();
            return "{\"schema\":1,\"outcome\":\"cleared\",\"roomInstanceId\":" +
                QuoteJson(sourceRoomId.ToString()) + ",\"sourceNodeId\":" + QuoteJson(sourceNodeId) +
                ",\"targetNodeId\":" + QuoteJson(targetNodeId) + ",\"rewardIds\":[" +
                string.Join(",", encodedRewards) + "]}";
        }

        private static string QuoteJson(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < 0x20)
                            builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else
                            builder.Append(character);
                        break;
                }
            }
            builder.Append('"');
            return builder.ToString();
        }
    }

    /// <summary>
    /// Binds real victory callbacks to the existing durable App operation. The composition owning room activation
    /// should keep one resolver for the expedition and activate one immutable scope per room.
    /// </summary>
    public sealed class ExpeditionEncounterResolver
    {
        private readonly IExpeditionEncounterAdvanceGateway _gateway;
        private ExpeditionEncounterResolutionScope _activeScope;
        private long _activationSequence;

        public ExpeditionEncounterResolver(ApplicationBootstrapper application,
            ExpeditionRoomTransitionController transitionController)
            : this(new ApplicationEncounterAdvanceGateway(application, transitionController))
        {
        }

        public ExpeditionEncounterResolver(IExpeditionEncounterAdvanceGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public ExpeditionEncounterResolutionScope ActivateCurrentRoom(string targetNodeId)
        {
            RunId runId = _gateway.RunId;
            string sourceNodeId = _gateway.CurrentNodeId;
            RoomInstanceId sourceRoomId = _gateway.CurrentRoomId;
            object roomScopeToken = _gateway.CurrentRoomScopeToken;
            if (!runId.IsValid || string.IsNullOrWhiteSpace(sourceNodeId) || !sourceRoomId.IsValid ||
                sourceRoomId != _gateway.SavedRoomId ||
                sourceRoomId != ExpeditionRoomIdentity.ForNode(runId, sourceNodeId) ||
                !_gateway.IsRoomScopeActive(roomScopeToken, sourceRoomId))
                throw new InvalidOperationException("The live and saved expedition source room scope do not match.");

            if (string.IsNullOrWhiteSpace(targetNodeId) || !_gateway.HasRouteEdge(sourceNodeId, targetNodeId))
                throw new InvalidOperationException("The selected destination is not an outgoing route edge.");

            if (_activeScope != null && _activeScope.SourceRoomId == sourceRoomId)
            {
                bool sameRoomAndDestination =
                    string.Equals(_activeScope.SourceNodeId, sourceNodeId, StringComparison.Ordinal) &&
                    string.Equals(_activeScope.TargetNodeId, targetNodeId, StringComparison.Ordinal);
                if (!sameRoomAndDestination)
                    throw new InvalidOperationException("The active room already captured a different destination.");
                if (ReferenceEquals(_activeScope.RoomScopeToken, roomScopeToken))
                    return _activeScope;
            }

            _activeScope = new ExpeditionEncounterResolutionScope(++_activationSequence, runId,
                sourceNodeId, sourceRoomId, targetNodeId, roomScopeToken);
            return _activeScope;
        }

        public Task<ExpeditionEncounterAdvanceOutcome> ResolveVictoryAsync(
            ExpeditionEncounterResolutionScope scope, IEnumerable<string> rewardIds)
        {
            return ResolveVictoryAsync(scope, rewardIds, inventory => inventory);
        }

        /// <summary>
        /// Pauses the session before reading its revision/inventory and invokes the candidate builder on a private
        /// copy of that inventory. App takes its own durable-operation lease before this capture lease is released.
        /// </summary>
        public async Task<ExpeditionEncounterAdvanceOutcome> ResolveVictoryAsync(
            ExpeditionEncounterResolutionScope scope, IEnumerable<string> rewardIds,
            Func<ExpeditionInventory, ExpeditionInventory> candidateInventoryFactory)
        {
            if (!IsCurrentScope(scope))
                return ExpeditionEncounterAdvanceOutcome.Failure(false, "The encounter callback belongs to a stale room scope.");
            if (scope.HasCompletion && (scope.AttemptInProgress || scope.LastAttempt == null ||
                scope.LastAttempt.Pending || scope.LastAttempt.Persisted))
                return ExpeditionEncounterAdvanceOutcome.Failure(scope.AttemptInProgress ||
                    (scope.LastAttempt != null && scope.LastAttempt.Pending),
                    "This room completion callback was already accepted; retry its captured payload explicitly.");
            if (candidateInventoryFactory == null)
                return ExpeditionEncounterAdvanceOutcome.Failure(false, "An inventory candidate factory is required.");
            bool pauseAcquired = _gateway.TryAcquireSimulationPauseLease(out IDisposable capturePauseLease,
                out string pauseError);
            if (!pauseAcquired || capturePauseLease == null)
            {
                capturePauseLease?.Dispose();
                if (string.IsNullOrWhiteSpace(pauseError))
                    pauseError = "The encounter gateway returned no simulation pause lease.";
                return ExpeditionEncounterAdvanceOutcome.Failure(false, pauseError);
            }

            bool captureLeaseHandedOff = false;
            try
            {
                long expectedExpeditionRevision = _gateway.CurrentExpeditionRevision;
                ExpeditionInventory currentInventory = _gateway.CurrentInventory;
                string expectedBaseInventorySnapshotJson = currentInventory == null
                    ? null
                    : UnityEngine.JsonUtility.ToJson(currentInventory.CreateSnapshot());
                ExpeditionInventory candidateSource = currentInventory == null
                    ? null
                    : ExpeditionInventory.Restore(currentInventory.Catalog, currentInventory.CreateSnapshot());
                ExpeditionInventory currentCandidate = candidateInventoryFactory(candidateSource);
                if (candidateSource != null && currentCandidate == null)
                    return ExpeditionEncounterAdvanceOutcome.Failure(false,
                        "The encounter inventory candidate factory returned no inventory.");

                if (scope.HasCompletion)
                {
                    if (!scope.TryRebaseRejectedCompletion(rewardIds, currentCandidate,
                        expectedBaseInventorySnapshotJson, expectedExpeditionRevision))
                        return ExpeditionEncounterAdvanceOutcome.Failure(false,
                            "A rejected room completion can only be rebased with its original reward IDs.");
                }
                else if (!scope.TryCaptureCompletion(rewardIds, currentCandidate,
                    expectedBaseInventorySnapshotJson, expectedExpeditionRevision))
                {
                    return ExpeditionEncounterAdvanceOutcome.Failure(false, "Encounter reward IDs are required.");
                }

                Task<ExpeditionEncounterAdvanceOutcome> submitTask =
                    SubmitCapturedCompletionAsync(scope, capturePauseLease);
                captureLeaseHandedOff = true;
                return await submitTask;
            }
            catch (ArgumentException exception)
            {
                return ExpeditionEncounterAdvanceOutcome.Failure(false, exception.Message);
            }
            catch (FormatException exception)
            {
                return ExpeditionEncounterAdvanceOutcome.Failure(false, exception.Message);
            }
            finally
            {
                if (!captureLeaseHandedOff) capturePauseLease.Dispose();
            }
        }

        public async Task<ExpeditionEncounterAdvanceOutcome> RetryCapturedCompletionAsync(
            ExpeditionEncounterResolutionScope scope)
        {
            if (scope == null || !IsCurrentScope(scope))
                return ExpeditionEncounterAdvanceOutcome.Failure(false, "The retry belongs to a stale room scope.");
            if (!scope.HasCompletion)
                return ExpeditionEncounterAdvanceOutcome.Failure(false, "There is no captured room completion to retry.");
            if (scope.LastAttempt != null && scope.LastAttempt.Persisted && !scope.LastAttempt.Pending)
                return ExpeditionEncounterAdvanceOutcome.Failure(false, "The captured room completion is already durable.");
            return await SubmitCapturedCompletionAsync(scope);
        }

        private bool IsCurrentScope(ExpeditionEncounterResolutionScope scope)
        {
            if (scope == null || !ReferenceEquals(_activeScope, scope) ||
                !ReferenceEquals(scope.RoomScopeToken, _gateway.CurrentRoomScopeToken) ||
                !_gateway.IsRoomScopeActive(scope.RoomScopeToken, scope.SourceRoomId) ||
                _gateway.RunId != scope.RunId || _gateway.CurrentRoomId != scope.SourceRoomId ||
                !string.Equals(_gateway.CurrentNodeId, scope.SourceNodeId, StringComparison.Ordinal))
                return false;

            if (_gateway.SavedRoomId == scope.SourceRoomId) return true;

            bool exactDurableActivationPending = scope.LastAttempt != null && scope.LastAttempt.Persisted &&
                scope.LastAttempt.Pending && _gateway.SavedRoomId ==
                ExpeditionRoomIdentity.ForNode(scope.RunId, scope.TargetNodeId);
            return exactDurableActivationPending;
        }

        private async Task<ExpeditionEncounterAdvanceOutcome> SubmitCapturedCompletionAsync(
            ExpeditionEncounterResolutionScope scope, IDisposable capturePauseLease = null)
        {
            if (scope.AttemptInProgress)
            {
                capturePauseLease?.Dispose();
                return ExpeditionEncounterAdvanceOutcome.Failure(true, "This exact room completion is already being saved.");
            }

            scope.AttemptInProgress = true;
            Task<ExpeditionEncounterAdvanceOutcome> submitTask;
            try
            {
                submitTask = _gateway.CompleteAndAdvanceRoomAsync(
                    scope.SourceRoomId, scope.TargetNodeId, scope.ResultId, scope.ResultJson,
                    scope.CandidateInventory, scope.ExpectedBaseInventorySnapshotJson,
                    scope.ExpectedExpeditionRevision);
            }
            catch (Exception exception)
            {
                ExpeditionEncounterAdvanceOutcome failure =
                    ExpeditionEncounterAdvanceOutcome.Failure(false, exception.Message);
                scope.RecordAttempt(failure);
                scope.AttemptInProgress = false;
                return failure;
            }
            finally
            {
                // The production gateway synchronously acquires App's longer-lived room-advance lease first.
                capturePauseLease?.Dispose();
            }

            try
            {
                ExpeditionEncounterAdvanceOutcome outcome = await submitTask;
                scope.RecordAttempt(outcome);
                return outcome;
            }
            catch (Exception exception)
            {
                ExpeditionEncounterAdvanceOutcome failure =
                    ExpeditionEncounterAdvanceOutcome.Failure(false, exception.Message);
                scope.RecordAttempt(failure);
                return failure;
            }
            finally
            {
                scope.AttemptInProgress = false;
            }
        }
    }
}
