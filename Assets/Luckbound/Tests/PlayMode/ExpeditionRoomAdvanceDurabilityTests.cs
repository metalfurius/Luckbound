using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionRoomAdvanceDurabilityTests
    {
        private static readonly string[] BellatorSkills =
        {
            "skill.bellator.circular_cut", "skill.bellator.guard"
        };

        [Test]
        public void PrepareRejectsStaleSourceAndMissingPrefabWithoutChangingTheCurrentRoom()
        {
            ExpeditionRunConfiguration run = CreateRun();
            ExpeditionRouteNode sourceNode = run.Route.Nodes.Single(node =>
                node.NodeId == run.Route.MandatoryNodeIds[0]);
            string targetNodeId = run.Route.MandatoryNodeIds[1];
            GameObject sourcePrefab = CreateRoomPrefab(sourceNode.TemplateId);
            var host = new GameObject("RoomAdvancePrepareFailureHost");
            var player = new GameObject("RoomAdvancePrepareFailurePlayer");
            ExpeditionRoomTransitionController controller = host.AddComponent<ExpeditionRoomTransitionController>();
            controller.Initialize(run.RunId, run.Route,
                templateId => templateId == sourceNode.TemplateId ? sourcePrefab : null, player.transform);

            GameObject activeRoot = controller.ActiveRoomRoot;
            RoomLifetimeScope activeScope = controller.ActiveScope;
            RoomInstanceId activeRoomId = controller.CurrentRoomId;
            string currentNodeId = controller.CurrentNodeId;
            Vector3 playerPosition = player.transform.position;
            var staleRoomId = RoomInstanceId.New();

            Assert.That(controller.TryPrepareTransition(staleRoomId, targetNodeId,
                out ExpeditionRoomTransitionController.PreparedRoomTransition stalePrepared), Is.False);
            Assert.That(stalePrepared, Is.Null);
            Assert.That(controller.TryPrepareTransition(activeRoomId, targetNodeId,
                out ExpeditionRoomTransitionController.PreparedRoomTransition missingPrefabPrepared), Is.False);
            Assert.That(missingPrefabPrepared, Is.Null);
            Assert.That(controller.LastTransitionError, Does.Contain("No room prefab"));
            Assert.That(controller.ActiveRoomRoot, Is.SameAs(activeRoot));
            Assert.That(controller.ActiveScope, Is.SameAs(activeScope));
            Assert.That(activeScope.IsDisposed, Is.False);
            Assert.That(controller.CurrentRoomId, Is.EqualTo(activeRoomId));
            Assert.That(controller.CurrentNodeId, Is.EqualTo(currentNodeId));
            Assert.That(controller.ActiveRoomCount, Is.EqualTo(1));
            Assert.That(player.transform.position, Is.EqualTo(playerPosition));

            controller.Dispose();
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(sourcePrefab);
        }

        [Test]
        public void PreparedTransitionLeavesTraversalPlayerAndOldScopeUntouchedUntilCommit()
        {
            ExpeditionRunConfiguration run = CreateRun();
            string sourceNodeId = run.Route.MandatoryNodeIds[0];
            string targetNodeId = run.Route.MandatoryNodeIds[1];
            RoomWorld world = CreateRoomWorld(run, new[] { sourceNodeId });
            GameObject oldRoot = world.Controller.ActiveRoomRoot;
            RoomLifetimeScope oldScope = world.Controller.ActiveScope;
            RoomInstanceId oldRoomId = world.Controller.CurrentRoomId;
            Vector3 playerPosition = world.Player.transform.position;

            Assert.That(world.Controller.TryPrepareTransition(oldRoomId, targetNodeId,
                out ExpeditionRoomTransitionController.PreparedRoomTransition prepared), Is.True,
                world.Controller.LastTransitionError);
            Assert.That(prepared, Is.Not.Null);
            Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(sourceNodeId));
            Assert.That(world.Controller.CurrentRoomId, Is.EqualTo(oldRoomId));
            Assert.That(world.Controller.ActiveRoomRoot, Is.SameAs(oldRoot));
            Assert.That(world.Controller.ActiveScope, Is.SameAs(oldScope));
            Assert.That(oldScope.IsDisposed, Is.False);
            Assert.That(world.Controller.ActiveRoomCount, Is.EqualTo(1));
            Assert.That(prepared.IsStagedActive, Is.False);
            Assert.That(world.Player.transform.position, Is.EqualTo(playerPosition));

            Assert.That(world.Controller.TryCommitPreparedTransition(prepared), Is.True,
                world.Controller.LastTransitionError);
            Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));
            Assert.That(world.Controller.CurrentRoomId, Is.Not.EqualTo(oldRoomId));
            Assert.That(oldScope.IsDisposed, Is.True);
            Assert.That(world.Controller.ActiveRoomCount, Is.EqualTo(1));
            Assert.That(world.Controller.ActiveRoomRoot, Is.Not.SameAs(oldRoot));

            string followingNodeId = run.Route.MandatoryNodeIds[2];
            GameObject committedRoot = world.Controller.ActiveRoomRoot;
            RoomInstanceId committedRoomId = world.Controller.CurrentRoomId;
            Assert.That(world.Controller.TryPrepareTransition(committedRoomId, followingNodeId,
                out ExpeditionRoomTransitionController.PreparedRoomTransition cancelled), Is.True,
                world.Controller.LastTransitionError);
            cancelled.Dispose();
            Assert.That(cancelled.IsPending, Is.False);
            Assert.That(cancelled.IsStagedDisposed, Is.True);
            Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));
            Assert.That(world.Controller.ActiveRoomRoot, Is.SameAs(committedRoot));
            Assert.That(world.Controller.CurrentRoomId, Is.EqualTo(committedRoomId));
            Assert.That(world.Controller.ActiveRoomCount, Is.EqualTo(1));
            DestroyRoomWorld(world);
        }

        [UnityTest]
        public IEnumerator RoomResultAndAdvancedCheckpointAreWrittenTogetherBeforeOldScopeDisposal()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                RoomLifetimeScope sourceScope = world.Controller.ActiveScope;
                int writesBefore = store.ProfileWriteSuccesses;
                bool sourceWasAliveDuringWrite = false;
                store.BeforeSuccessfulProfileWrite = _ => sourceWasAliveDuringWrite = !sourceScope.IsDisposed;

                RoomAdvanceCommitResult result = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, world.Controller.CurrentRoomId,
                    targetNodeId, "room-result-01", "{\"outcome\":\"cleared\",\"rewardIds\":[]}"),
                    value => result = value);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.Persisted, Is.True, result.Message);
                Assert.That(result.Pending, Is.False);
                Assert.That(result.Activated, Is.True, result.Message);
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(writesBefore + 1),
                    "Completion, route cursor and checkpoint must use one profile write.");
                Assert.That(sourceWasAliveDuringWrite, Is.True,
                    "The source scope must remain alive until the durable profile write succeeds.");
                Assert.That(sourceScope.IsDisposed, Is.True);
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));

                ProfileSaveEnvelope envelope = JsonUtility.FromJson<ProfileSaveEnvelope>(store.ProfilePrimary);
                Assert.That(envelope, Is.Not.Null);
                Assert.That(envelope.activeExpedition, Is.Not.Null);
                Assert.That(envelope.activeExpedition.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(envelope.activeExpedition.completedRoomIds, Does.Contain(sourceNodeId));
                ExpeditionRoomCompletionSaveData savedResult = envelope.activeExpedition.completedRoomResults.Single(
                    item => item.roomNodeId == sourceNodeId);
                Assert.That(savedResult.resultId, Is.EqualTo("room-result-01"));
                Assert.That(savedResult.resultJson, Is.EqualTo("{\"outcome\":\"cleared\",\"rewardIds\":[]}"));
                ExpeditionRoomCheckpointSaveData checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(
                    envelope.activeExpedition.roomEntryCheckpointJson);
                Assert.That(checkpoint, Is.Not.Null);
                Assert.That(checkpoint.roomNodeId, Is.EqualTo(targetNodeId));
            }
            finally
            {
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator FailedWriteRetainsTheExactCandidateAndPreparedRoomUntilOneRetryCommits()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                GameObject oldRoot = world.Controller.ActiveRoomRoot;
                RoomLifetimeScope oldScope = world.Controller.ActiveScope;
                RoomInstanceId oldRoomId = world.Controller.CurrentRoomId;
                ExpeditionSaveData oldSave = app.ActiveExpedition;
                store.FailNextProfileWrite = true;

                RoomAdvanceCommitResult failed = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, oldRoomId, targetNodeId,
                    "room-result-retry", "{\"outcome\":\"cleared\"}"), value => failed = value);

                Assert.That(failed.Persisted, Is.False);
                Assert.That(failed.Pending, Is.True, failed.Message);
                Assert.That(app.HasPendingRoomAdvance, Is.True);
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(oldSave.currentNodeId));
                Assert.That(app.ActiveExpedition.completedRoomResults, Is.Empty);
                Assert.That(world.Controller.ActiveRoomRoot, Is.SameAs(oldRoot));
                Assert.That(world.Controller.CurrentRoomId, Is.EqualTo(oldRoomId));
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(sourceNodeId));
                Assert.That(oldScope.IsDisposed, Is.False);
                Assert.That(world.Controller.ActiveRoomCount, Is.EqualTo(1));
                string exactFailedPayload = store.FailedProfileJson;
                Assert.That(exactFailedPayload, Is.Not.Null.And.Not.Empty);

                RoomAdvanceCommitResult retried = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.Pending, Is.False);
                Assert.That(retried.Activated, Is.True, retried.Message);
                Assert.That(app.HasPendingRoomAdvance, Is.False);
                Assert.That(store.LastSuccessfulProfileJson, Is.EqualTo(exactFailedPayload));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(2),
                    "The failed attempt must be followed by one successful retry, with no second advancement write.");
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(app.ActiveExpedition.completedRoomIds, Does.Contain(sourceNodeId));
                Assert.That(oldScope.IsDisposed, Is.True);

                RoomAdvanceCommitResult duplicateRetry = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => duplicateRetry = value);
                Assert.That(duplicateRetry.Persisted, Is.False);
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(2));
            }
            finally
            {
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator StableRoomResultReplayAfterReloadIsIdempotentAndConflictsDoNotChangeRoute()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                const string resultId = "room-result-restart";
                const string resultJson = "{\"outcome\":\"cleared\",\"rewardIds\":[\"reward.first\"]}";
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                RoomInstanceId sourceRoomId = world.Controller.CurrentRoomId;
                RoomAdvanceCommitResult first = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, sourceRoomId,
                    targetNodeId, resultId, resultJson), value => first = value);
                Assert.That(first.Persisted, Is.True, first.Message);
                Assert.That(first.Activated, Is.True);
                int successfulWrites = store.ProfileWriteSuccesses;
                int targetVisitedCount = app.ActiveExpedition.visitedNodeIds.Count(node => node == targetNodeId);

                DestroyRoomWorld(world);
                world = null;
                DestroyApplication();
                yield return null;
                yield return StartApplication(value => app = value, "Expedition");
                ExpeditionSaveData restored = app.ActiveExpedition;
                Assert.That(restored.currentNodeId, Is.EqualTo(targetNodeId));
                world = CreateRoomWorld(app.ActiveRun, restored.visitedNodeIds);

                RoomAdvanceCommitResult replay = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, sourceRoomId,
                    targetNodeId, resultId, resultJson), value => replay = value);
                Assert.That(replay.Persisted, Is.True, replay.Message);
                Assert.That(replay.AlreadyApplied, Is.True);
                Assert.That(replay.Activated, Is.False);
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(app.ActiveExpedition.visitedNodeIds.Count(node => node == targetNodeId),
                    Is.EqualTo(targetVisitedCount));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(successfulWrites));

                string wrongTarget = app.ActiveRun.Route.Nodes.Select(node => node.NodeId)
                    .First(node => !HasRouteEdge(app.ActiveRun.Route, sourceNodeId, node));
                RoomAdvanceCommitResult conflictingEdge = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, sourceRoomId,
                    wrongTarget, resultId, resultJson), value => conflictingEdge = value);
                Assert.That(conflictingEdge.Persisted, Is.False);
                Assert.That(conflictingEdge.Message, Does.Contain("route edge"));

                RoomAdvanceCommitResult conflictingResult = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, sourceRoomId,
                    targetNodeId, resultId, "{\"outcome\":\"different\"}"), value => conflictingResult = value);
                Assert.That(conflictingResult.Persisted, Is.False);
                Assert.That(conflictingResult.Message, Does.Contain("different result"));
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(successfulWrites));
            }
            finally
            {
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        private static ExpeditionRunConfiguration CreateRun()
        {
            return ExpeditionRunConfigurationFactory.Create("class.bellator", ExpeditionRunConfiguration.HumanRaceId,
                BellatorSkills);
        }

        private static RoomWorld CreateRoomWorld(ExpeditionRunConfiguration run, IEnumerable<string> visitedNodeIds = null)
        {
            var prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string templateId in run.Route.Nodes.Select(node => node.TemplateId).Distinct(StringComparer.Ordinal))
            {
                prefabs.Add(templateId, CreateRoomPrefab(templateId));
            }

            var host = new GameObject("RoomAdvanceDurabilityHost");
            var player = new GameObject("RoomAdvanceDurabilityPlayer");
            player.AddComponent<Rigidbody2D>();
            ExpeditionRoomTransitionController controller = host.AddComponent<ExpeditionRoomTransitionController>();
            controller.Initialize(run.RunId, run.Route, templateId =>
                prefabs.TryGetValue(templateId, out GameObject prefab) ? prefab : null,
                player.transform, visitedNodeIds ?? new[] { run.Route.MandatoryNodeIds[0] });
            return new RoomWorld(host, player, controller, prefabs);
        }

        private static GameObject CreateRoomPrefab(string templateId)
        {
            var prefab = new GameObject("RoomTemplate." + templateId.Replace('.', '_'));
            var entry = new GameObject("EntryPort");
            entry.transform.SetParent(prefab.transform, false);
            entry.transform.localPosition = new Vector3(0f, 1f, 0f);
            entry.AddComponent<RoomPortMarker2D>().Configure("port.entry.west", "entry", "west", 0, 1, 3);

            var exit = new GameObject("ExitPort");
            exit.transform.SetParent(prefab.transform, false);
            exit.transform.localPosition = new Vector3(18f, 1f, 0f);
            exit.AddComponent<RoomPortMarker2D>().Configure("port.exit.east", "exit", "east", 17, 1, 3);

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(prefab.transform, false);
            spawn.transform.localPosition = new Vector3(1.5f, 1f, 0f);
            spawn.AddComponent<RoomPositionMarker2D>().Configure("spawn.player", "player_spawn", 1, 1);
            return prefab;
        }

        private static bool HasRouteEdge(ExpeditionRoutePlan route, string sourceNodeId, string targetNodeId)
        {
            return route.Connections.Any(connection => connection.FromNodeId == sourceNodeId &&
                connection.ToNodeId == targetNodeId);
        }

        private static IEnumerator StartApplication(Action<ApplicationBootstrapper> ready, string expectedScene)
        {
            Time.timeScale = 1f;
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
            yield return null;
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            for (int frame = 0; frame < 120 && (ApplicationBootstrapper.Instance == null ||
                !ApplicationBootstrapper.Instance.SaveDataReady || SceneManager.GetActiveScene().name != expectedScene); frame++)
                yield return null;

            Assert.That(ApplicationBootstrapper.Instance, Is.Not.Null);
            Assert.That(ApplicationBootstrapper.Instance.SaveDataReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(expectedScene));
            ready(ApplicationBootstrapper.Instance);
        }

        private static IEnumerator Complete<T>(Task<T> task, Action<T> completed)
        {
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) throw task.Exception.Flatten();
            if (task.IsCanceled) throw new OperationCanceledException();
            completed(task.GetAwaiter().GetResult());
        }

        private static void DestroyRoomWorld(RoomWorld world)
        {
            if (world == null) return;
            if (world.Controller != null) world.Controller.Dispose();
            if (world.Player != null) UnityEngine.Object.Destroy(world.Player);
            if (world.Host != null) UnityEngine.Object.Destroy(world.Host);
            foreach (GameObject prefab in world.Prefabs.Values)
                if (prefab != null) UnityEngine.Object.Destroy(prefab);
        }

        private static void DestroyApplication()
        {
            if (ApplicationBootstrapper.Instance != null)
                UnityEngine.Object.Destroy(ApplicationBootstrapper.Instance.gameObject);
        }

        private sealed class RoomWorld
        {
            public RoomWorld(GameObject host, GameObject player, ExpeditionRoomTransitionController controller,
                Dictionary<string, GameObject> prefabs)
            {
                Host = host;
                Player = player;
                Controller = controller;
                Prefabs = prefabs;
            }

            public GameObject Host { get; }
            public GameObject Player { get; }
            public ExpeditionRoomTransitionController Controller { get; }
            public Dictionary<string, GameObject> Prefabs { get; }
        }

        private sealed class MemorySaveStore : ISaveStore
        {
            private string _profilePrimary;
            private string _profileBackup;
            private string _settingsPrimary;
            private string _settingsBackup;

            public bool FailNextProfileWrite { get; set; }
            public int ProfileWriteAttempts { get; private set; }
            public int ProfileWriteSuccesses { get; private set; }
            public string ProfilePrimary => _profilePrimary;
            public string FailedProfileJson { get; private set; }
            public string LastSuccessfulProfileJson { get; private set; }
            public Action<string> BeforeSuccessfulProfileWrite { get; set; }

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                return Task.FromResult(document == SaveDocument.Profile
                    ? new SaveStorageSnapshot(_profilePrimary, _profileBackup)
                    : new SaveStorageSnapshot(_settingsPrimary, _settingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileWriteAttempts++;
                    if (FailNextProfileWrite)
                    {
                        FailNextProfileWrite = false;
                        FailedProfileJson = json;
                        throw new IOException("simulated profile storage failure");
                    }

                    BeforeSuccessfulProfileWrite?.Invoke(json);
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _profileBackup = lastKnownGoodPrimaryJson;
                    _profilePrimary = json;
                    LastSuccessfulProfileJson = json;
                    ProfileWriteSuccesses++;
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(lastKnownGoodPrimaryJson)) _settingsBackup = lastKnownGoodPrimaryJson;
                    _settingsPrimary = json;
                }
                return Task.CompletedTask;
            }
        }
    }
}
