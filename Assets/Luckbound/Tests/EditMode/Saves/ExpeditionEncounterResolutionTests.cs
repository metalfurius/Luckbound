using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.App
{
    public sealed class ExpeditionEncounterResolutionTests
    {
        [Test]
        public void ActivationCapturesStableSourceDestinationAndResultIdentity()
        {
            RunId runId = RunId.New();
            var firstGateway = new FakeGateway(runId, "room.source");
            var resolver = new ExpeditionEncounterResolver(firstGateway);

            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            Assert.That(scope.SourceNodeId, Is.EqualTo("room.source"));
            Assert.That(scope.SourceRoomId, Is.EqualTo(ExpeditionRoomIdentity.ForNode(runId, "room.source")));
            Assert.That(scope.TargetNodeId, Is.EqualTo("room.next"));
            Assert.That(scope.ResultId, Is.EqualTo("room-result.v1:" + scope.SourceRoomId));

            var restoredGateway = new FakeGateway(runId, "room.source");
            var restoredResolver = new ExpeditionEncounterResolver(restoredGateway);
            ExpeditionEncounterResolutionScope restored = restoredResolver.ActivateCurrentRoom("room.next");
            Assert.That(restored.ResultId, Is.EqualTo(scope.ResultId));
            Assert.That(restored.SourceRoomId, Is.EqualTo(scope.SourceRoomId));
        }

        [Test]
        public void ActivationRejectsDestinationOutsideSavedRoute()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            var resolver = new ExpeditionEncounterResolver(gateway);

            Assert.Throws<InvalidOperationException>(() => resolver.ActivateCurrentRoom("room.not-connected"));
            Assert.That(gateway.Requests, Is.Empty);
        }

        [Test]
        public void ActivationRejectsLiveRoomThatDoesNotMatchSavedRoom()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source")
            {
                SavedRoomId = ExpeditionRoomIdentity.ForNode(runId, "room.other")
            };
            var resolver = new ExpeditionEncounterResolver(gateway);

            Assert.Throws<InvalidOperationException>(() => resolver.ActivateCurrentRoom("room.next"));
            Assert.That(gateway.Requests, Is.Empty);
        }

        [Test]
        public void VictoryRejectsChangedRunAndMalformedRewardIdsWithoutSaving()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            gateway.RunId = RunId.New();
            ExpeditionEncounterAdvanceOutcome changedRun = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.valid" }).GetAwaiter().GetResult();
            Assert.That(changedRun.Persisted, Is.False);
            Assert.That(changedRun.Message, Does.Contain("stale room scope"));

            gateway.RunId = runId;
            ExpeditionEncounterAdvanceOutcome blankReward = resolver.ResolveVictoryAsync(scope,
                new[] { " " }).GetAwaiter().GetResult();
            ExpeditionEncounterAdvanceOutcome duplicateReward = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.same", "reward.same" }).GetAwaiter().GetResult();
            ExpeditionEncounterAdvanceOutcome nullRewards = resolver.ResolveVictoryAsync(scope,
                null).GetAwaiter().GetResult();

            Assert.That(blankReward.Persisted, Is.False);
            Assert.That(duplicateReward.Persisted, Is.False);
            Assert.That(nullRewards.Persisted, Is.False);
            Assert.That(scope.HasCompletion, Is.False);
            Assert.That(gateway.Requests, Is.Empty);
        }

        [Test]
        public void VictoryAcquiresPauseBeforeReadingRevisionInventoryAndBuildingCandidate()
        {
            TextAsset itemsJson = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(itemsJson, Is.Not.Null);
            ItemCatalog catalog = ItemCatalogJsonLoader.Load(itemsJson.text);
            var baseInventory = new ExpeditionInventory(catalog, "class.bellator", initialGold: 9);
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source") { CurrentInventory = baseInventory };
            gateway.Outcomes.Enqueue(new ExpeditionEncounterAdvanceOutcome(true, false, false, true, "saved"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");
            bool candidateBuiltUnderPause = false;

            ExpeditionEncounterAdvanceOutcome outcome = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.gold" }, inventory =>
                {
                    Assert.That(gateway.ActiveSimulationPauseLeaseCount, Is.EqualTo(1));
                    candidateBuiltUnderPause = true;
                    return CreateGoldCandidate(inventory, 3);
                }).GetAwaiter().GetResult();

            Assert.That(outcome.Persisted, Is.True, outcome.Message);
            Assert.That(candidateBuiltUnderPause, Is.True);
            Assert.That(baseInventory.Gold, Is.EqualTo(9),
                "The candidate builder must not mutate App's live inventory while deriving a room result.");
            Assert.That(gateway.InventoryReadWithoutPause, Is.False);
            Assert.That(gateway.RevisionReadWithoutPause, Is.False);
            Assert.That(gateway.PauseLeaseHeldAtSubmission, Is.True);
            Assert.That(gateway.ActiveSimulationPauseLeaseCount, Is.Zero,
                "The short capture lease is released after App's durable-operation call takes ownership.");
            Assert.That(gateway.Requests, Has.Count.EqualTo(1));
        }

        [Test]
        public void VictoryFailsBeforeSnapshotCaptureWhenPauseLeaseIsUnavailable()
        {
            var gateway = new FakeGateway(RunId.New(), "room.source")
            {
                PauseLeaseAvailable = false
            };
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome outcome = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.gold" }).GetAwaiter().GetResult();

            Assert.That(outcome.Persisted, Is.False);
            Assert.That(outcome.Message, Does.Contain("pause owner is not bound"));
            Assert.That(gateway.InventoryReadWithoutPause, Is.False);
            Assert.That(gateway.RevisionReadWithoutPause, Is.False);
            Assert.That(gateway.Requests, Is.Empty);
        }

        [Test]
        public void FailedWriteKeepsOneCanonicalPayloadForExactRetryAndRejectsDuplicateAndLateCallbacks()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            gateway.Outcomes.Enqueue(ExpeditionEncounterAdvanceOutcome.Failure(true, "simulated save rejection"));
            gateway.Outcomes.Enqueue(new ExpeditionEncounterAdvanceOutcome(true, false, false, true, "saved"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome failed = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.z", "reward.a" }).GetAwaiter().GetResult();

            Assert.That(failed.Persisted, Is.False);
            Assert.That(failed.Pending, Is.True);
            Assert.That(scope.HasCompletion, Is.True);
            string capturedPayload = "{\"schema\":1,\"outcome\":\"cleared\",\"roomInstanceId\":\"" +
                scope.SourceRoomId + "\",\"sourceNodeId\":\"room.source\",\"targetNodeId\":\"room.next\"," +
                "\"rewardIds\":[\"reward.a\",\"reward.z\"]}";
            Assert.That(scope.ResultJson, Is.EqualTo(capturedPayload));
            Assert.That(gateway.Requests.Count, Is.EqualTo(1));

            ExpeditionEncounterAdvanceOutcome duplicate = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.changed" }).GetAwaiter().GetResult();
            Assert.That(duplicate.Persisted, Is.False);
            Assert.That(gateway.Requests.Count, Is.EqualTo(1),
                "A second victory callback must not create a new result or another save.");

            ExpeditionEncounterAdvanceOutcome retry = resolver.RetryCapturedCompletionAsync(scope)
                .GetAwaiter().GetResult();
            Assert.That(retry.Persisted, Is.True, retry.Message);
            Assert.That(gateway.Requests.Count, Is.EqualTo(2));
            Assert.That(gateway.Requests[0].SourceRoomId, Is.EqualTo(gateway.Requests[1].SourceRoomId));
            Assert.That(gateway.Requests[0].TargetNodeId, Is.EqualTo(gateway.Requests[1].TargetNodeId));
            Assert.That(gateway.Requests[0].ResultId, Is.EqualTo(gateway.Requests[1].ResultId));
            Assert.That(gateway.Requests[0].ResultJson, Is.EqualTo(gateway.Requests[1].ResultJson));
            Assert.That(gateway.Requests[1].ResultJson, Is.EqualTo(capturedPayload));

            ExpeditionEncounterAdvanceOutcome completedAgain = resolver.RetryCapturedCompletionAsync(scope)
                .GetAwaiter().GetResult();
            Assert.That(completedAgain.Persisted, Is.False);
            Assert.That(gateway.Requests.Count, Is.EqualTo(2),
                "A durable result cannot be submitted again after the successful retry.");

            resolver.ActivateCurrentRoom("room.after");
            ExpeditionEncounterAdvanceOutcome late = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.late" }).GetAwaiter().GetResult();
            Assert.That(late.Persisted, Is.False);
            Assert.That(late.Message, Does.Contain("stale room scope"));
            Assert.That(gateway.Requests.Count, Is.EqualTo(2));
        }

        [Test]
        public void CapturedRetryFromPreviousRoomIsRejectedWithoutSaving()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            gateway.Outcomes.Enqueue(ExpeditionEncounterAdvanceOutcome.Failure(true, "simulated save rejection"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope oldScope = resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome first = resolver.ResolveVictoryAsync(oldScope,
                new[] { "reward.first" }).GetAwaiter().GetResult();
            Assert.That(first.Pending, Is.True);
            Assert.That(gateway.Requests, Has.Count.EqualTo(1));

            gateway.SetCurrentRoom("room.after");
            resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome retry = resolver.RetryCapturedCompletionAsync(oldScope)
                .GetAwaiter().GetResult();

            Assert.That(retry.Persisted, Is.False);
            Assert.That(retry.Pending, Is.False);
            Assert.That(retry.Message, Does.Contain("stale room scope"));
            Assert.That(gateway.Requests, Has.Count.EqualTo(1),
                "A retry from a no-longer-active room must not reach the durable gateway.");
        }

        [Test]
        public void CapturedRetryFromPreviousScopeTokenIsRejectedAfterSameRoomIsRecreated()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            gateway.Outcomes.Enqueue(ExpeditionEncounterAdvanceOutcome.Failure(true, "simulated save rejection"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope captured = resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome failed = resolver.ResolveVictoryAsync(captured,
                new[] { "reward.first" }).GetAwaiter().GetResult();
            Assert.That(failed.Pending, Is.True);
            Assert.That(gateway.Requests, Has.Count.EqualTo(1));

            gateway.SetCurrentRoomScope(new object());
            ExpeditionEncounterResolutionScope replacement = resolver.ActivateCurrentRoom("room.next");

            Assert.That(replacement, Is.Not.SameAs(captured),
                "A recreated room with the same logical ID must receive a fresh resolver scope token.");
            ExpeditionEncounterAdvanceOutcome retry = resolver.RetryCapturedCompletionAsync(captured)
                .GetAwaiter().GetResult();

            Assert.That(retry.Persisted, Is.False);
            Assert.That(retry.Pending, Is.False);
            Assert.That(retry.Message, Does.Contain("stale room scope"));
            Assert.That(gateway.Requests, Has.Count.EqualTo(1),
                "The previous room scope must not submit its captured retry after same-ID recreation.");
        }

        [Test]
        public void DurableCompletionCanRetryItsPendingRoomActivation()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            gateway.Outcomes.Enqueue(new ExpeditionEncounterAdvanceOutcome(true, true, false, false,
                "The route is durable; destination activation is pending."));
            gateway.Outcomes.Enqueue(new ExpeditionEncounterAdvanceOutcome(true, false, false, true, "activated"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");
            RoomInstanceId sourceRoomId = scope.SourceRoomId;
            RoomInstanceId targetRoomId = ExpeditionRoomIdentity.ForNode(runId, "room.next");

            ExpeditionEncounterAdvanceOutcome durable = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.first" }).GetAwaiter().GetResult();

            Assert.That(durable.Persisted, Is.True);
            Assert.That(durable.Pending, Is.True);
            Assert.That(gateway.CurrentRoomId, Is.EqualTo(sourceRoomId),
                "The source physical scope remains active while destination activation is pending.");
            Assert.That(gateway.SavedRoomId, Is.EqualTo(targetRoomId),
                "The durable route cursor already identifies the destination.");

            ExpeditionEncounterAdvanceOutcome retry = resolver.RetryCapturedCompletionAsync(scope)
                .GetAwaiter().GetResult();

            Assert.That(retry.Persisted, Is.True, retry.Message);
            Assert.That(retry.Pending, Is.False);
            Assert.That(retry.Activated, Is.True);
            Assert.That(gateway.Requests, Has.Count.EqualTo(2));
            Assert.That(gateway.Requests[0].ResultId, Is.EqualTo(gateway.Requests[1].ResultId));
            Assert.That(gateway.Requests[0].ResultJson, Is.EqualTo(gateway.Requests[1].ResultJson));
            Assert.That(gateway.CurrentRoomId, Is.EqualTo(targetRoomId));
        }

        [Test]
        public async Task DuplicateCallbackWhileSaveIsHeldReportsPendingAndSubmitsOnlyOnce()
        {
            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source");
            var heldWrite = new TaskCompletionSource<ExpeditionEncounterAdvanceOutcome>();
            gateway.HeldWrite = heldWrite.Task;
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            Task<ExpeditionEncounterAdvanceOutcome> firstTask = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.first" });
            Assert.That(gateway.Requests.Count, Is.EqualTo(1));
            Assert.That(firstTask.IsCompleted, Is.False);

            ExpeditionEncounterAdvanceOutcome duplicate = await resolver.ResolveVictoryAsync(scope,
                new[] { "reward.changed" });
            Assert.That(duplicate.Persisted, Is.False);
            Assert.That(duplicate.Pending, Is.True);
            Assert.That(gateway.Requests.Count, Is.EqualTo(1),
                "A duplicate callback during an in-flight save must not submit a second request.");

            heldWrite.SetResult(new ExpeditionEncounterAdvanceOutcome(true, false, false, true, "saved"));
            ExpeditionEncounterAdvanceOutcome first = await firstTask;
            Assert.That(first.Persisted, Is.True, first.Message);
            Assert.That(first.Pending, Is.False);
            Assert.That(gateway.Requests.Count, Is.EqualTo(1));
        }

        [Test]
        public void DefinitivelyRejectedCandidateCanBeRebasedWithTheSameRoomReceipt()
        {
            TextAsset itemsJson = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(itemsJson, Is.Not.Null, "The authoritative item catalog is required by this candidate snapshot test.");
            ItemCatalog catalog = ItemCatalogJsonLoader.Load(itemsJson.text);
            var baseInventory = new ExpeditionInventory(catalog, "class.bellator", initialGold: 9);

            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source")
            {
                CurrentInventory = baseInventory,
                CurrentExpeditionRevision = 12
            };
            gateway.Outcomes.Enqueue(ExpeditionEncounterAdvanceOutcome.Failure(false, "stale candidate"));
            gateway.Outcomes.Enqueue(new ExpeditionEncounterAdvanceOutcome(true, false, false, true, "saved"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome rejected = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.gold" }, inventory => CreateGoldCandidate(inventory, 1))
                .GetAwaiter().GetResult();

            Assert.That(rejected.Persisted, Is.False);
            Assert.That(rejected.Pending, Is.False);
            Assert.That(scope.HasCompletion, Is.True);
            string capturedResultId = scope.ResultId;
            string capturedResultJson = scope.ResultJson;

            Assert.That(baseInventory.TryAddGold(3, TransactionId.New()).Succeeded, Is.True);
            gateway.CurrentExpeditionRevision++;
            string currentBaseJson = JsonUtility.ToJson(baseInventory.CreateSnapshot());
            ExpeditionInventory rebasedCandidate = null;

            ExpeditionEncounterAdvanceOutcome changedReceipt = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.changed" }, inventory =>
                {
                    rebasedCandidate = CreateGoldCandidate(inventory, 4);
                    return rebasedCandidate;
                })
                .GetAwaiter().GetResult();
            Assert.That(changedReceipt.Persisted, Is.False);
            Assert.That(gateway.Requests, Has.Count.EqualTo(1),
                "Rebasing cannot replace the already captured reward receipt.");

            ExpeditionEncounterAdvanceOutcome saved = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.gold" }, inventory =>
                {
                    rebasedCandidate = CreateGoldCandidate(inventory, 4);
                    return rebasedCandidate;
                })
                .GetAwaiter().GetResult();

            Assert.That(saved.Persisted, Is.True, saved.Message);
            Assert.That(gateway.Requests, Has.Count.EqualTo(2));
            Assert.That(gateway.Requests[1].ResultId, Is.EqualTo(capturedResultId));
            Assert.That(gateway.Requests[1].ResultJson, Is.EqualTo(capturedResultJson));
            Assert.That(gateway.Requests[1].CandidateInventory.Gold, Is.EqualTo(rebasedCandidate.Gold));
            Assert.That(gateway.Requests[1].ExpectedExpeditionRevision,
                Is.EqualTo(gateway.Requests[0].ExpectedExpeditionRevision + 1));
            Assert.That(gateway.Requests[1].ExpectedBaseInventorySnapshotJson, Is.EqualTo(currentBaseJson));
        }

        [Test]
        public void RetryResubmitsCapturedInventorySnapshotAfterCallerMutatesOriginalCandidate()
        {
            TextAsset itemsJson = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(itemsJson, Is.Not.Null, "The authoritative item catalog is required by this candidate snapshot test.");
            ItemCatalog catalog = ItemCatalogJsonLoader.Load(itemsJson.text);
            var baseInventory = new ExpeditionInventory(catalog, "class.bellator", initialGold: 9);
            ExpeditionInventory[] capturedCandidate = { null };
            int capturedGold = baseInventory.Gold + 4;

            RunId runId = RunId.New();
            var gateway = new FakeGateway(runId, "room.source") { CurrentInventory = baseInventory };
            gateway.Outcomes.Enqueue(ExpeditionEncounterAdvanceOutcome.Failure(true, "simulated save rejection"));
            gateway.Outcomes.Enqueue(new ExpeditionEncounterAdvanceOutcome(true, false, false, true, "saved"));
            var resolver = new ExpeditionEncounterResolver(gateway);
            ExpeditionEncounterResolutionScope scope = resolver.ActivateCurrentRoom("room.next");

            ExpeditionEncounterAdvanceOutcome failed = resolver.ResolveVictoryAsync(scope,
                new[] { "reward.gold" }, inventory =>
                {
                    capturedCandidate[0] = CreateGoldCandidate(inventory, 4);
                    return capturedCandidate[0];
                }).GetAwaiter().GetResult();

            Assert.That(failed.Pending, Is.True);
            Assert.That(gateway.Requests, Has.Count.EqualTo(1));
            Assert.That(gateway.Requests[0].CandidateInventory.Gold, Is.EqualTo(capturedGold));

            Assert.That(capturedCandidate[0].TryAddGold(10, TransactionId.New()).Succeeded, Is.True);
            Assert.That(capturedCandidate[0].Gold, Is.Not.EqualTo(capturedGold));

            ExpeditionEncounterAdvanceOutcome retried = resolver.RetryCapturedCompletionAsync(scope)
                .GetAwaiter().GetResult();

            Assert.That(retried.Persisted, Is.True, retried.Message);
            Assert.That(gateway.Requests, Has.Count.EqualTo(2));
            Assert.That(gateway.Requests[1].CandidateInventory, Is.SameAs(gateway.Requests[0].CandidateInventory));
            Assert.That(gateway.Requests[1].CandidateInventory.Gold, Is.EqualTo(capturedGold),
                "Retry must use the immutable candidate captured with the room result, not the caller's mutated inventory.");
            Assert.That(gateway.Requests[1].ExpectedExpeditionRevision,
                Is.EqualTo(gateway.Requests[0].ExpectedExpeditionRevision),
                "Retry must retain the revision that belonged to the captured inventory candidate.");
            Assert.That(gateway.Requests[1].ExpectedBaseInventorySnapshotJson,
                Is.EqualTo(gateway.Requests[0].ExpectedBaseInventorySnapshotJson),
                "Retry must retain the exact base snapshot from which the candidate was derived.");
        }

        private sealed class FakeGateway : IExpeditionEncounterAdvanceGateway
        {
            public FakeGateway(RunId runId, string currentNodeId)
            {
                RunId = runId;
                CurrentNodeId = currentNodeId;
                CurrentRoomId = ExpeditionRoomIdentity.ForNode(runId, currentNodeId);
                SavedRoomId = CurrentRoomId;
                _currentRoomScopeToken = new object();
            }

            private object _currentRoomScopeToken;
            private bool _currentRoomScopeActive = true;
            public RunId RunId { get; set; }
            public RoomInstanceId CurrentRoomId { get; private set; }
            public RoomInstanceId SavedRoomId { get; set; }
            public string CurrentNodeId { get; private set; }
            public object CurrentRoomScopeToken => _currentRoomScopeToken;
            private ExpeditionInventory _currentInventory;
            private long _currentExpeditionRevision;
            public ExpeditionInventory CurrentInventory
            {
                get
                {
                    if (ActiveSimulationPauseLeaseCount == 0) InventoryReadWithoutPause = true;
                    return _currentInventory;
                }
                set => _currentInventory = value;
            }
            public long CurrentExpeditionRevision
            {
                get
                {
                    if (ActiveSimulationPauseLeaseCount == 0) RevisionReadWithoutPause = true;
                    return _currentExpeditionRevision;
                }
                set => _currentExpeditionRevision = value;
            }
            public bool PauseLeaseAvailable { get; set; } = true;
            public int ActiveSimulationPauseLeaseCount { get; private set; }
            public bool InventoryReadWithoutPause { get; private set; }
            public bool RevisionReadWithoutPause { get; private set; }
            public bool PauseLeaseHeldAtSubmission { get; private set; }
            public Queue<ExpeditionEncounterAdvanceOutcome> Outcomes { get; } =
                new Queue<ExpeditionEncounterAdvanceOutcome>();
            public Task<ExpeditionEncounterAdvanceOutcome> HeldWrite { get; set; }
            public List<CommitRequest> Requests { get; } = new List<CommitRequest>();
            public HashSet<string> ValidTargets { get; } = new HashSet<string>(StringComparer.Ordinal)
            {
                "room.next",
                "room.after"
            };

            public bool HasRouteEdge(string sourceNodeId, string targetNodeId)
            {
                return string.Equals(CurrentNodeId, sourceNodeId, StringComparison.Ordinal) &&
                    ValidTargets.Contains(targetNodeId);
            }

            public bool IsRoomScopeActive(object scopeToken, RoomInstanceId roomId)
            {
                if (!ReferenceEquals(_currentRoomScopeToken, scopeToken) || CurrentRoomId != roomId) return false;
                return _currentRoomScopeActive;
            }

            public void SetCurrentRoomScope(object scopeToken)
            {
                _currentRoomScopeToken = scopeToken ?? throw new ArgumentNullException(nameof(scopeToken));
                _currentRoomScopeActive = true;
            }

            public void SetCurrentRoom(string nodeId)
            {
                CurrentNodeId = nodeId;
                CurrentRoomId = ExpeditionRoomIdentity.ForNode(RunId, nodeId);
                SavedRoomId = CurrentRoomId;
                _currentRoomScopeToken = new object();
                _currentRoomScopeActive = true;
            }

            public bool TryAcquireSimulationPauseLease(out IDisposable lease, out string error)
            {
                if (!PauseLeaseAvailable)
                {
                    lease = null;
                    error = "The simulation pause owner is not bound.";
                    return false;
                }

                ActiveSimulationPauseLeaseCount++;
                lease = new FakeSimulationPauseLease(this);
                error = string.Empty;
                return true;
            }

            public Task<ExpeditionEncounterAdvanceOutcome> CompleteAndAdvanceRoomAsync(
                RoomInstanceId sourceRoomId, string targetNodeId, string resultId, string resultJson,
                ExpeditionInventory candidateInventory, string expectedBaseInventorySnapshotJson,
                long expectedExpeditionRevision)
            {
                PauseLeaseHeldAtSubmission = ActiveSimulationPauseLeaseCount > 0;
                Requests.Add(new CommitRequest(sourceRoomId, targetNodeId, resultId, resultJson,
                    candidateInventory, expectedBaseInventorySnapshotJson, expectedExpeditionRevision));
                if (HeldWrite != null)
                {
                    Task<ExpeditionEncounterAdvanceOutcome> held = HeldWrite;
                    HeldWrite = null;
                    return CompleteHeldWriteAsync(held, targetNodeId, candidateInventory);
                }
                if (Outcomes.Count == 0) throw new InvalidOperationException("No fake result was queued.");
                ExpeditionEncounterAdvanceOutcome outcome = Outcomes.Dequeue();
                if (outcome.Persisted && outcome.Activated)
                {
                    SetCurrentRoom(targetNodeId);
                    CurrentInventory = candidateInventory;
                    CurrentExpeditionRevision++;
                }
                else if (outcome.Persisted && outcome.Pending)
                {
                    SavedRoomId = ExpeditionRoomIdentity.ForNode(RunId, targetNodeId);
                    CurrentInventory = candidateInventory;
                    CurrentExpeditionRevision++;
                }
                return Task.FromResult(outcome);
            }
            private async Task<ExpeditionEncounterAdvanceOutcome> CompleteHeldWriteAsync(
                Task<ExpeditionEncounterAdvanceOutcome> heldWrite, string targetNodeId,
                ExpeditionInventory candidateInventory)
            {
                ExpeditionEncounterAdvanceOutcome outcome = await heldWrite;
                if (outcome.Persisted && outcome.Activated)
                {
                    SetCurrentRoom(targetNodeId);
                    CurrentInventory = candidateInventory;
                    CurrentExpeditionRevision++;
                }
                else if (outcome.Persisted && outcome.Pending)
                {
                    SavedRoomId = ExpeditionRoomIdentity.ForNode(RunId, targetNodeId);
                    CurrentInventory = candidateInventory;
                    CurrentExpeditionRevision++;
                }
                return outcome;
            }

            private sealed class FakeSimulationPauseLease : IDisposable
            {
                private FakeGateway _owner;

                public FakeSimulationPauseLease(FakeGateway owner) { _owner = owner; }

                public void Dispose()
                {
                    FakeGateway owner = _owner;
                    _owner = null;
                    if (owner != null) owner.ActiveSimulationPauseLeaseCount--;
                }
            }
        }

        private static ExpeditionInventory CreateGoldCandidate(ExpeditionInventory source, int goldAmount)
        {
            if (source == null) return null;
            ExpeditionInventory candidate = ExpeditionInventory.Restore(source.Catalog, source.CreateSnapshot());
            Assert.That(candidate.TryAddGold(goldAmount, TransactionId.New()).Succeeded, Is.True);
            return candidate;
        }

        private sealed class CommitRequest
        {
            public CommitRequest(RoomInstanceId sourceRoomId, string targetNodeId, string resultId, string resultJson,
                ExpeditionInventory candidateInventory, string expectedBaseInventorySnapshotJson,
                long expectedExpeditionRevision)
            {
                SourceRoomId = sourceRoomId;
                TargetNodeId = targetNodeId;
                ResultId = resultId;
                ResultJson = resultJson;
                CandidateInventory = candidateInventory;
                ExpectedBaseInventorySnapshotJson = expectedBaseInventorySnapshotJson;
                ExpectedExpeditionRevision = expectedExpeditionRevision;
            }

            public RoomInstanceId SourceRoomId { get; }
            public string TargetNodeId { get; }
            public string ResultId { get; }
            public string ResultJson { get; }
            public ExpeditionInventory CandidateInventory { get; }
            public string ExpectedBaseInventorySnapshotJson { get; }
            public long ExpectedExpeditionRevision { get; }
        }
    }
}
