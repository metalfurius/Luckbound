using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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
            IDisposable pauseProviderBinding = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                pauseProviderBinding = BindNoopSimulationPauseLeaseProvider(app);
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                long expectedExpeditionRevision = app.ActiveExpeditionRevision;
                string expectedBaseInventorySnapshotJson = JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot());
                ExpeditionInventory candidateInventory = CreateInventoryCandidate(app.ActiveRun, 7);
                string candidateInventoryJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot());
                ExpeditionRunConfiguration oldRun = app.ActiveRun;
                string oldInventoryJson = JsonUtility.ToJson(oldRun.Inventory.CreateSnapshot());
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                RoomLifetimeScope sourceScope = world.Controller.ActiveScope;
                int writesBefore = store.ProfileWriteSuccesses;
                bool sourceWasAliveDuringWrite = false;
                ExpeditionRunConfiguration runObservedDuringWrite = null;
                store.BeforeSuccessfulProfileWrite = _ =>
                {
                    sourceWasAliveDuringWrite = !sourceScope.IsDisposed;
                    runObservedDuringWrite = app.ActiveRun;
                };

                RoomAdvanceCommitResult result = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, world.Controller.CurrentRoomId,
                    targetNodeId, "room-result-01", "{\"outcome\":\"cleared\",\"rewardIds\":[]}",
                    candidateInventory, expectedBaseInventorySnapshotJson, expectedExpeditionRevision),
                    value => result = value);

                Assert.That(result, Is.Not.Null);
                Assert.That(result.Persisted, Is.True, result.Message);
                Assert.That(result.Pending, Is.False);
                Assert.That(result.Activated, Is.True, result.Message);
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(writesBefore + 1),
                    "Completion, route cursor and checkpoint must use one profile write.");
                Assert.That(sourceWasAliveDuringWrite, Is.True,
                    "The source scope must remain alive until the durable profile write succeeds.");
                Assert.That(runObservedDuringWrite, Is.SameAs(oldRun),
                    "ActiveRun must remain the old run while its profile candidate is being written.");
                Assert.That(app.ActiveRun, Is.Not.SameAs(oldRun));
                Assert.That(app.ActiveRun.RunId, Is.EqualTo(oldRun.RunId), "Advancing rooms preserves RunId.");
                Assert.That(JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(candidateInventoryJson),
                    "The committed run must publish the saved inventory candidate.");
                Assert.That(JsonUtility.ToJson(oldRun.Inventory.CreateSnapshot()), Is.EqualTo(oldInventoryJson),
                    "Publishing a candidate must leave the prior run inventory untouched.");
                Assert.That(sourceScope.IsDisposed, Is.True);
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));

                ProfileSaveEnvelope envelope = JsonUtility.FromJson<ProfileSaveEnvelope>(store.ProfilePrimary);
                Assert.That(envelope, Is.Not.Null);
                Assert.That(envelope.activeExpedition, Is.Not.Null);
                Assert.That(envelope.activeExpedition.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(envelope.activeExpedition.inventorySnapshotJson, Is.EqualTo(candidateInventoryJson));
                Assert.That(envelope.activeExpedition.completedRoomIds, Does.Contain(sourceNodeId));
                ExpeditionRoomCompletionSaveData savedResult = envelope.activeExpedition.completedRoomResults.Single(
                    item => item.roomNodeId == sourceNodeId);
                Assert.That(savedResult.resultId, Is.EqualTo("room-result-01"));
                Assert.That(savedResult.resultJson, Is.EqualTo("{\"outcome\":\"cleared\",\"rewardIds\":[]}"));
                ExpeditionRoomCheckpointSaveData checkpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(
                    envelope.activeExpedition.roomEntryCheckpointJson);
                Assert.That(checkpoint, Is.Not.Null);
                Assert.That(checkpoint.roomNodeId, Is.EqualTo(targetNodeId));
                Assert.That(checkpoint.inventorySnapshotJson, Is.EqualTo(candidateInventoryJson),
                    "The destination entry checkpoint must begin with the committed candidate inventory.");
            }
            finally
            {
                pauseProviderBinding?.Dispose();
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator ActiveRunWriteRetainsPreparedSessionAndPauseLeaseUntilExactRetry()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            IDisposable pauseBinding = null;
            IDisposable publicationBinding = null;
            int leasesAcquired = 0;
            int leasesReleased = 0;
            int captures = 0;
            int preparations = 0;
            int publications = 0;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                object owner = new object();
                pauseBinding = app.BindExpeditionSimulationPauseLeaseProvider(owner, () =>
                {
                    leasesAcquired++;
                    return new CountingSimulationPauseLease(() => leasesReleased++);
                });
                publicationBinding = app.BindExpeditionSessionMutationPublication(owner,
                    (candidateRun, candidateSave) =>
                    {
                        captures++;
                        Assert.That(candidateRun.RunId, Is.EqualTo(app.ActiveRun.RunId));
                        Assert.That(leasesReleased, Is.EqualTo(0), "Capture and profile I/O must remain under the session lease.");
                        candidateSave.currentHealth = 98;
                        candidateSave.currentEnergy = 44;
                        candidateSave.maximumHealth = 100;
                        candidateSave.maximumEnergy = 50;
                    },
                    (candidateRun, candidateSave) =>
                    {
                        preparations++;
                        Assert.That(candidateSave.currentHealth, Is.EqualTo(98));
                        Assert.That(candidateSave.currentEnergy, Is.EqualTo(44));
                        candidateSave.currentHealth = 71;
                        candidateSave.currentEnergy = 37;
                        return () =>
                        {
                            Assert.That(leasesReleased, Is.EqualTo(0), "Publish before releasing the captured session lease.");
                            publications++;
                        };
                    });

                store.FailNextProfileWrite = true;
                SaveWriteResult first = null;
                yield return Complete(app.SaveActiveExpeditionStateAsync(app.ActiveExpedition), value => first = value);
                Assert.That(first.Succeeded, Is.False);
                Assert.That(app.HasPendingExpeditionWrite, Is.True);
                Assert.That(leasesAcquired, Is.EqualTo(1));
                Assert.That(leasesReleased, Is.EqualTo(0));
                Assert.That(preparations, Is.EqualTo(1));
                Assert.That(captures, Is.EqualTo(1));
                Assert.That(publications, Is.EqualTo(0), "A failed durable write must not publish the actor candidate.");

                SaveWriteResult retry = null;
                yield return Complete(app.RetryPendingExpeditionWriteAsync(), value => retry = value);
                Assert.That(retry.Succeeded, Is.True, retry.Message);
                Assert.That(app.HasPendingExpeditionWrite, Is.False);
                Assert.That(leasesAcquired, Is.EqualTo(1), "Retry reuses the captured candidate and lease.");
                Assert.That(leasesReleased, Is.EqualTo(1));
                Assert.That(preparations, Is.EqualTo(1));
                Assert.That(captures, Is.EqualTo(1), "Retry must reuse the captured resource and inventory snapshot.");
                Assert.That(publications, Is.EqualTo(1));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(71));
                Assert.That(app.ActiveExpedition.currentEnergy, Is.EqualTo(37));
                ProfileSaveEnvelope saved = JsonUtility.FromJson<ProfileSaveEnvelope>(store.ProfilePrimary);
                Assert.That(saved.activeExpedition.currentHealth, Is.EqualTo(71));
                Assert.That(saved.activeExpedition.currentEnergy, Is.EqualTo(37));
            }
            finally
            {
                publicationBinding?.Dispose();
                pauseBinding?.Dispose();
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator DurableRoomAdvanceRetriesActorPublicationWithoutRewritingProfile()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            IDisposable pauseBinding = null;
            IDisposable publicationBinding = null;
            int leasesAcquired = 0;
            int leasesReleased = 0;
            int captures = 0;
            int preparations = 0;
            int publications = 0;
            bool failFirstPublication = true;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                object owner = new object();
                pauseBinding = app.BindExpeditionSimulationPauseLeaseProvider(owner, () =>
                {
                    leasesAcquired++;
                    return new CountingSimulationPauseLease(() => leasesReleased++);
                });
                publicationBinding = app.BindExpeditionSessionMutationPublication(owner,
                    (_, candidateSave) =>
                    {
                        captures++;
                        candidateSave.currentHealth = 87;
                        candidateSave.currentEnergy = 42;
                    },
                    (_, candidateSave) =>
                    {
                        preparations++;
                        Assert.That(candidateSave.currentHealth, Is.EqualTo(87));
                        Assert.That(candidateSave.currentEnergy, Is.EqualTo(42));
                        return () =>
                        {
                            publications++;
                            if (failFirstPublication)
                            {
                                failFirstPublication = false;
                                throw new InvalidOperationException("simulated actor publication fault");
                            }
                        };
                    });

                string sourceNodeId = app.ActiveRun.Route.MandatoryNodeIds[0];
                string targetNodeId = app.ActiveRun.Route.MandatoryNodeIds[1];
                world = CreateRoomWorld(app.ActiveRun, new[] { sourceNodeId });
                RoomLifetimeScope sourceScope = world.Controller.ActiveScope;
                ExpeditionInventory candidateInventory = CreateInventoryCandidate(app.ActiveRun, 3);
                string candidateInventoryJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot());
                int writesBefore = store.ProfileWriteSuccesses;

                RoomAdvanceCommitResult first = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, world.Controller.CurrentRoomId,
                    targetNodeId, "room-result-publish-retry", "{\"outcome\":\"cleared\"}",
                    candidateInventory, JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot()),
                    app.ActiveExpeditionRevision), value => first = value);
                Assert.That(first.Persisted, Is.True, first.Message);
                Assert.That(first.Pending, Is.True, first.Message);
                Assert.That(first.Activated, Is.False);
                Assert.That(app.HasPendingRoomAdvance, Is.True);
                Assert.That(sourceScope.IsDisposed, Is.False);
                Assert.That(leasesReleased, Is.EqualTo(0));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(writesBefore + 1));

                RoomAdvanceCommitResult retry = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => retry = value);
                Assert.That(retry.Persisted, Is.True, retry.Message);
                Assert.That(retry.Pending, Is.False, retry.Message);
                Assert.That(retry.Activated, Is.True, retry.Message);
                Assert.That(publications, Is.EqualTo(2));
                Assert.That(preparations, Is.EqualTo(1));
                Assert.That(captures, Is.EqualTo(1));
                Assert.That(leasesAcquired, Is.EqualTo(1));
                Assert.That(leasesReleased, Is.EqualTo(1));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(writesBefore + 1),
                    "The retry must republish the already durable candidate without requesting another store write.");
                Assert.That(JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(candidateInventoryJson));
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(87));
                Assert.That(app.ActiveExpedition.currentEnergy, Is.EqualTo(42));
                Assert.That(sourceScope.IsDisposed, Is.True);
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(targetNodeId));
            }
            finally
            {
                publicationBinding?.Dispose();
                pauseBinding?.Dispose();
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator RoomAdvanceRejectsCandidateCapturedBeforeANewerExpeditionWrite()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            IDisposable pauseProviderBinding = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                pauseProviderBinding = BindNoopSimulationPauseLeaseProvider(app);
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                long candidateRevision = app.ActiveExpeditionRevision;
                string candidateBaseInventorySnapshotJson = JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot());
                ExpeditionInventory staleCandidate = CreateInventoryCandidate(app.ActiveRun, 7);
                ExpeditionSaveData newerState = app.ActiveExpedition;
                newerState.currentHealth--;
                SaveWriteResult newerWrite = null;
                yield return Complete(app.SaveActiveExpeditionStateAsync(newerState), value => newerWrite = value);
                Assert.That(newerWrite.Succeeded, Is.True, newerWrite.Message);
                Assert.That(app.ActiveExpeditionRevision, Is.GreaterThan(candidateRevision));

                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                int successfulWritesBeforeAdvance = store.ProfileWriteSuccesses;
                RoomAdvanceCommitResult result = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, world.Controller.CurrentRoomId,
                    targetNodeId, "room-result-stale-inventory", "{\"outcome\":\"cleared\"}",
                    staleCandidate, candidateBaseInventorySnapshotJson, candidateRevision), value => result = value);

                Assert.That(result.Persisted, Is.False);
                Assert.That(result.Pending, Is.False);
                Assert.That(result.Message, Does.Contain("inventory candidate was captured"));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(successfulWritesBeforeAdvance),
                    "A stale candidate must be rejected before writing over the newer expedition state.");
                Assert.That(app.ActiveExpedition.currentHealth, Is.EqualTo(newerState.currentHealth));
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(sourceNodeId));
            }
            finally
            {
                pauseProviderBinding?.Dispose();
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator RoomAdvanceRejectsCandidateWhenInventoryMutatesWithoutRevisionChange()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            IDisposable pauseProviderBinding = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                pauseProviderBinding = BindNoopSimulationPauseLeaseProvider(app);
                ExpeditionRunConfiguration run = CreateRun();
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                long candidateRevision = app.ActiveExpeditionRevision;
                ExpeditionInventory activeInventory = app.ActiveRun.Inventory;
                string candidateBaseInventorySnapshotJson = JsonUtility.ToJson(activeInventory.CreateSnapshot());
                ExpeditionInventory staleCandidate = CreateInventoryCandidate(app.ActiveRun, 7);
                int goldBeforePickup = activeInventory.Gold;
                Assert.That(activeInventory.TryAddGold(3, TransactionId.New()).Succeeded, Is.True);
                Assert.That(app.ActiveExpeditionRevision, Is.EqualTo(candidateRevision),
                    "This simulates a world pickup mutating the live inventory without a durable app write.");

                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                int successfulWritesBeforeAdvance = store.ProfileWriteSuccesses;
                RoomAdvanceCommitResult result = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, world.Controller.CurrentRoomId,
                    targetNodeId, "room-result-stale-pickup", "{\"outcome\":\"cleared\"}",
                    staleCandidate, candidateBaseInventorySnapshotJson, candidateRevision), value => result = value);

                Assert.That(result.Persisted, Is.False);
                Assert.That(result.Pending, Is.False);
                Assert.That(result.Message, Does.Contain("active inventory changed"));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(successfulWritesBeforeAdvance));
                Assert.That(app.ActiveRun.Inventory.Gold, Is.EqualTo(goldBeforePickup + 3),
                    "A stale room candidate must not replace the live inventory after a world pickup.");
            }
            finally
            {
                pauseProviderBinding?.Dispose();
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator RoomAdvanceRequiresBoundSimulationPauseOwnerBeforeAnyWrite()
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
                int writeAttemptsBeforeAdvance = store.ProfileWriteAttempts;

                RoomAdvanceCommitResult direct = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller,
                    world.Controller.CurrentRoomId, targetNodeId, "room-result-no-pause-owner",
                    "{\"outcome\":\"cleared\"}"), value => direct = value);
                Assert.That(direct.Persisted, Is.False);
                Assert.That(direct.Pending, Is.False);
                Assert.That(direct.Message, Does.Contain("simulation pause owner is not bound"));

                long revision = app.ActiveExpeditionRevision;
                string baseInventory = JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot());
                ExpeditionInventory candidate = CreateInventoryCandidate(app.ActiveRun, 1);
                RoomAdvanceCommitResult captured = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller,
                    world.Controller.CurrentRoomId, targetNodeId, "room-result-no-pause-owner-candidate",
                    "{\"outcome\":\"cleared\"}", candidate, baseInventory, revision), value => captured = value);
                Assert.That(captured.Persisted, Is.False);
                Assert.That(captured.Pending, Is.False);
                Assert.That(captured.Message, Does.Contain("simulation pause owner is not bound"));

                Assert.That(store.ProfileWriteAttempts, Is.EqualTo(writeAttemptsBeforeAdvance));
                Assert.That(app.HasPendingRoomAdvance, Is.False);
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(sourceNodeId));
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(sourceNodeId));
            }
            finally
            {
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        public IEnumerator ApplicationEncounterGatewayRejectsDisposedPhysicalScopeWithSameLogicalRoomId()
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

                world = CreateRoomWorld(app.ActiveRun, new[] { app.ActiveExpedition.currentNodeId });
                var gateway = new ApplicationEncounterAdvanceGateway(app, world.Controller);
                RoomLifetimeScope capturedRoomScope = world.Controller.ActiveScope;
                RoomInstanceId logicalRoomId = capturedRoomScope.RoomInstanceId;
                bool acceptedBeforeDispose = gateway.IsRoomScopeActive(capturedRoomScope, logicalRoomId);

                capturedRoomScope.Dispose();
                bool acceptedAfterDispose = gateway.IsRoomScopeActive(capturedRoomScope, logicalRoomId);

                Assert.That(acceptedBeforeDispose, Is.True);
                Assert.That(acceptedAfterDispose, Is.False,
                    "The production gateway must reject a disposed RoomLifetimeScope even while its logical ID matches App.");
            }
            finally
            {
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator FailedWriteRetainsTheExactCandidateAndPreparedRoomUntilOneRetryCommits()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            GameObject pauseOwner = null;
            IDisposable pauseLeaseProviderBinding = null;
            float previousTimeScale = Time.timeScale;
            SimulationMode2D previousPhysicsMode = Physics2D.simulationMode;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                Time.timeScale = 0.5f;
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                pauseOwner = new GameObject("PendingRoomAdvancePauseOwner");
                GameObject labelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(pauseOwner.transform, false);
                ExpeditionPauseController pauseController = pauseOwner.AddComponent<ExpeditionPauseController>();
                Text pauseLabel = labelObject.GetComponent<Text>();
                pauseController.Initialize(pauseLabel);
                int pauseChangedCount = 0;
                pauseController.PauseChanged += _ => pauseChangedCount++;
                pauseLeaseProviderBinding = app.BindExpeditionSimulationPauseLeaseProvider(
                    pauseController, pauseController.AcquireSimulationPauseLease);

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
                ExpeditionRunConfiguration oldRun = app.ActiveRun;
                ExpeditionInventory oldInventory = oldRun.Inventory;
                long candidateRevision = app.ActiveExpeditionRevision;
                string candidateBaseInventorySnapshotJson = JsonUtility.ToJson(oldInventory.CreateSnapshot());
                string oldInventoryJson = JsonUtility.ToJson(oldInventory.CreateSnapshot());
                ExpeditionInventory candidateInventory = CreateInventoryCandidate(oldRun, 13);
                string candidateInventoryJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot());
                ExpeditionInventorySnapshot retainedCandidateSnapshot = candidateInventory.CreateSnapshot();
                string durableProfileBeforeAdvance = store.ProfilePrimary;
                store.FailNextProfileWrite = true;

                RoomAdvanceCommitResult failed = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, oldRoomId, targetNodeId,
                    "room-result-retry", "{\"outcome\":\"cleared\"}", candidateInventory,
                    candidateBaseInventorySnapshotJson, candidateRevision),
                    value => failed = value);

                Assert.That(failed.Persisted, Is.False);
                Assert.That(failed.Pending, Is.True, failed.Message);
                Assert.That(app.HasPendingRoomAdvance, Is.True);
                Assert.That(app.HasPendingSessionMutation, Is.True);
                Assert.That(pauseController.IsPaused, Is.False,
                    "The durable simulation lease must remain independent of the player's manual pause state.");
                Assert.That(pauseController.IsSimulationPaused, Is.True);
                Assert.That(Time.timeScale, Is.EqualTo(0f));
                Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.Script));
                Assert.That(pauseChangedCount, Is.Zero);
                Assert.That(pauseLabel.text, Is.Empty);
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(oldSave.currentNodeId));
                Assert.That(app.ActiveExpedition.completedRoomResults, Is.Empty);
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(oldSave.inventorySnapshotJson));
                Assert.That(store.ProfilePrimary, Is.EqualTo(durableProfileBeforeAdvance),
                    "A failed write must leave the durable profile untouched.");
                Assert.That(app.ActiveRun, Is.SameAs(oldRun), "A failed write must keep the previous ActiveRun instance.");
                Assert.That(app.ActiveRun.Inventory, Is.SameAs(oldInventory));
                Assert.That(JsonUtility.ToJson(oldInventory.CreateSnapshot()), Is.EqualTo(oldInventoryJson));
                Assert.That(world.Controller.ActiveRoomRoot, Is.SameAs(oldRoot));
                Assert.That(world.Controller.CurrentRoomId, Is.EqualTo(oldRoomId));
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(sourceNodeId));
                Assert.That(oldScope.IsDisposed, Is.False);
                Assert.That(world.Controller.ActiveRoomCount, Is.EqualTo(1));
                string exactFailedPayload = store.FailedProfileJson;
                Assert.That(exactFailedPayload, Is.Not.Null.And.Not.Empty);
                ProfileSaveEnvelope failedEnvelope = JsonUtility.FromJson<ProfileSaveEnvelope>(exactFailedPayload);
                Assert.That(failedEnvelope.activeExpedition.inventorySnapshotJson, Is.EqualTo(candidateInventoryJson));
                Assert.That(candidateInventory.TryAddGold(1, TransactionId.New()).Succeeded, Is.True);
                Assert.That(JsonUtility.ToJson(candidateInventory.CreateSnapshot()), Is.Not.EqualTo(candidateInventoryJson),
                    "Mutating the caller's inventory after failure must not change the retained retry candidate.");
                ExpeditionRunConfiguration runObservedDuringRetryWrite = null;
                store.BeforeSuccessfulProfileWrite = _ => runObservedDuringRetryWrite = app.ActiveRun;

                store.FailNextProfileWrite = true;
                RoomAdvanceCommitResult retryStillPending = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => retryStillPending = value);
                Assert.That(retryStillPending.Pending, Is.True);
                Assert.That(app.HasPendingRoomAdvance, Is.True);
                Assert.That(pauseController.IsSimulationPaused, Is.True,
                    "A failed exact retry must retain the same pause lease.");
                Assert.That(Time.timeScale, Is.EqualTo(0f));

                ExpeditionInventory exactRetryInventory = ExpeditionInventory.Restore(oldInventory.Catalog,
                    retainedCandidateSnapshot);
                RoomAdvanceCommitResult retried = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, oldRoomId, targetNodeId,
                    "room-result-retry", "{\"outcome\":\"cleared\"}", exactRetryInventory,
                    candidateBaseInventorySnapshotJson, candidateRevision), value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.Pending, Is.False);
                Assert.That(retried.Activated, Is.True, retried.Message);
                Assert.That(app.HasPendingRoomAdvance, Is.False);
                Assert.That(app.HasPendingSessionMutation, Is.False);
                Assert.That(pauseController.IsSimulationPaused, Is.False,
                    "The exact retry releases its lease only after route activation completes.");
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));
                Assert.That(Physics2D.simulationMode, Is.EqualTo(SimulationMode2D.FixedUpdate));
                Assert.That(pauseChangedCount, Is.Zero);
                Assert.That(pauseLabel.text, Is.Empty);
                Assert.That(store.LastSuccessfulProfileJson, Is.EqualTo(exactFailedPayload));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(2),
                    "The failed attempt must be followed by one successful retry, with no second advancement write.");
                Assert.That(app.ActiveExpedition.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(app.ActiveExpedition.completedRoomIds, Does.Contain(sourceNodeId));
                Assert.That(runObservedDuringRetryWrite, Is.SameAs(oldRun),
                    "The retry must publish ActiveRun only after its exact profile write commits.");
                Assert.That(app.ActiveRun, Is.Not.SameAs(oldRun));
                Assert.That(app.ActiveRun.RunId, Is.EqualTo(oldRun.RunId), "Retry publication preserves RunId.");
                Assert.That(app.ActiveRun.Inventory, Is.Not.SameAs(candidateInventory));
                Assert.That(JsonUtility.ToJson(app.ActiveRun.Inventory.CreateSnapshot()), Is.EqualTo(candidateInventoryJson));
                Assert.That(JsonUtility.ToJson(oldInventory.CreateSnapshot()), Is.EqualTo(oldInventoryJson),
                    "The failed candidate must not mutate the previous active inventory.");
                Assert.That(app.ActiveExpedition.inventorySnapshotJson, Is.EqualTo(candidateInventoryJson));
                ProfileSaveEnvelope retryEnvelope = JsonUtility.FromJson<ProfileSaveEnvelope>(store.ProfilePrimary);
                ExpeditionRoomCheckpointSaveData retryCheckpoint = JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(
                    retryEnvelope.activeExpedition.roomEntryCheckpointJson);
                Assert.That(retryCheckpoint.inventorySnapshotJson, Is.EqualTo(candidateInventoryJson));
                Assert.That(oldScope.IsDisposed, Is.True);

                RoomAdvanceCommitResult duplicateRetry = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => duplicateRetry = value);
                Assert.That(duplicateRetry.Persisted, Is.False);
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(2));

                ContractProgressionCommitResult nextProfileMutation = null;
                yield return Complete(app.AcceptContractAsync("CT01"), value => nextProfileMutation = value);
                Assert.That(nextProfileMutation, Is.Not.Null);
                Assert.That(nextProfileMutation.Succeeded, Is.True, nextProfileMutation.Message);
                Assert.That(nextProfileMutation.Persisted, Is.True, nextProfileMutation.Message);
                Assert.That(app.HasPendingRoomAdvance, Is.False);
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(3),
                    "A completed retry must release the profile gate for the next durable mutation.");
            }
            finally
            {
                pauseLeaseProviderBinding?.Dispose();
                if (pauseOwner != null) UnityEngine.Object.DestroyImmediate(pauseOwner);
                Time.timeScale = previousTimeScale;
                Physics2D.simulationMode = previousPhysicsMode;
                DestroyRoomWorld(world);
                DestroyApplication();
                ApplicationBootstrapper.EditorSaveStoreFactoryForTests = previousFactory;
            }
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator ExitRoutesStayBlockedFromTheFirstWriteThroughExactRetry()
        {
            var store = new MemorySaveStore();
            Func<ISaveStore> previousFactory = ApplicationBootstrapper.EditorSaveStoreFactoryForTests;
            ApplicationBootstrapper.EditorSaveStoreFactoryForTests = () => store;
            RoomWorld world = null;
            GameObject pauseOwner = null;
            GameObject gameplayHost = null;
            GameObject presenterHost = null;
            IDisposable pauseProviderBinding = null;
            Task<RoomAdvanceCommitResult> advancing = null;
            UnityAction<Scene, LoadSceneMode> sceneLoadedHandler = null;
            bool sceneLoadedSubscribed = false;
            Scene sceneBeforeGuardChecks = default;
            bool unexpectedSceneLoaded = false;
            bool gameplayReturnAvailable = false;
            Exception exitRouteException = null;
            float previousTimeScale = Time.timeScale;
            SimulationMode2D previousPhysicsMode = Physics2D.simulationMode;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                sceneBeforeGuardChecks = SceneManager.GetActiveScene();
                sceneLoadedHandler = (loadedScene, _) =>
                {
                    if (loadedScene.handle != sceneBeforeGuardChecks.handle) unexpectedSceneLoaded = true;
                };
                SceneManager.sceneLoaded += sceneLoadedHandler;
                sceneLoadedSubscribed = true;
                SaveWriteResult begun = null;
                yield return Complete(app.BeginExpeditionAsync(CreateRun()), value => begun = value);
                Assert.That(begun.Succeeded, Is.True, begun.Message);

                Time.timeScale = 0.5f;
                Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
                pauseOwner = new GameObject("InFlightRoomAdvancePauseOwner");
                GameObject labelObject = new GameObject("PauseLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(pauseOwner.transform, false);
                ExpeditionPauseController pauseController = pauseOwner.AddComponent<ExpeditionPauseController>();
                pauseController.Initialize(labelObject.GetComponent<Text>());
                pauseProviderBinding = app.BindExpeditionSimulationPauseLeaseProvider(
                    pauseController, pauseController.AcquireSimulationPauseLease);

                ExpeditionRunConfiguration run = app.ActiveRun;
                string sourceNodeId = run.Route.MandatoryNodeIds[0];
                string targetNodeId = run.Route.MandatoryNodeIds[1];
                world = CreateRoomWorld(run, new[] { sourceNodeId });
                RoomInstanceId sourceRoomId = world.Controller.CurrentRoomId;

                Task blockedWriteStarted = store.HoldNextProfileWrite();
                store.FailNextProfileWrite = true;
                advancing = app.CompleteAndAdvanceRoomAsync(world.Controller, sourceRoomId, targetNodeId,
                    "room-result-in-flight", "{\"outcome\":\"cleared\"}");
                for (int frame = 0; frame < 120 && !blockedWriteStarted.IsCompleted; frame++) yield return null;

                bool firstWriteWasHeld = blockedWriteStarted.IsCompleted;
                bool firstWriteWasTracked = app.IsRoomAdvanceInFlight;
                bool noPendingRecordBeforeFirstWriteSettles = !app.HasPendingRoomAdvance;
                bool sessionMutationWasGuardedDuringFirstWrite = app.HasPendingSessionMutation;
                bool simulationWasPausedDuringFirstWrite = pauseController.IsSimulationPaused;
                bool globalTimeWasPausedDuringFirstWrite = Time.timeScale == 0f;

                gameplayHost = new GameObject("InFlightGameplayExitGuard");
                ExpeditionGameplayComposition composition = gameplayHost.AddComponent<ExpeditionGameplayComposition>();
                typeof(ExpeditionGameplayComposition).GetField("_application", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(composition, app);
                MethodInfo gameplayReturn = typeof(ExpeditionGameplayComposition).GetMethod(
                    "ReturnToTavern", BindingFlags.Instance | BindingFlags.NonPublic);
                gameplayReturnAvailable = gameplayReturn != null;
                try { gameplayReturn?.Invoke(composition, null); }
                catch (Exception exception) { exitRouteException = exception; }

                presenterHost = new GameObject("InFlightSessionPresenterExitGuard");
                ExpeditionRunSessionPresenter presenter = presenterHost.AddComponent<ExpeditionRunSessionPresenter>();
                presenter.enabled = false;
                try { presenter.ReturnToTavern(); }
                catch (Exception exception) { exitRouteException = exitRouteException ?? exception; }
                yield return null;
                yield return null;
                bool sceneStayedLoadedDuringFirstWrite =
                    SceneManager.GetActiveScene().handle == sceneBeforeGuardChecks.handle && !unexpectedSceneLoaded;
                bool timeStayedPausedAfterExitRequests = Time.timeScale == 0f;

                store.ReleaseBlockedProfileWrite();
                RoomAdvanceCommitResult failed = null;
                yield return Complete(advancing, value => failed = value);
                Assert.That(failed.Pending, Is.True, failed.Message);
                Assert.That(app.IsRoomAdvanceInFlight, Is.False);
                Assert.That(app.HasPendingRoomAdvance, Is.True);
                Assert.That(app.HasPendingSessionMutation, Is.True);
                Assert.That(pauseController.IsSimulationPaused, Is.True);

                store.FailNextProfileWrite = true;
                RoomAdvanceCommitResult failedRetry = null;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => failedRetry = value);
                Assert.That(failedRetry.Pending, Is.True, failedRetry.Message);
                Assert.That(app.HasPendingSessionMutation, Is.True,
                    "A failed retry must continue to block both return paths.");
                Assert.That(pauseController.IsSimulationPaused, Is.True);

                try { presenter.ReturnToTavern(); }
                catch (Exception exception) { exitRouteException = exitRouteException ?? exception; }
                yield return null;
                yield return null;
                bool sceneStayedLoadedAfterPresenterExitRequest =
                    SceneManager.GetActiveScene().handle == sceneBeforeGuardChecks.handle && !unexpectedSceneLoaded;
                bool timeStayedPausedAfterPresenterExitRequest = Time.timeScale == 0f;

                FieldInfo gameplayRetryingField = typeof(ExpeditionGameplayComposition).GetField(
                    "_retryingPendingRoomAdvance", BindingFlags.Instance | BindingFlags.NonPublic);
                bool gameplayRetryFlagAvailable = gameplayRetryingField != null;
                int profileWritesBeforeCompositionRetry = store.ProfileWriteAttempts;
                if (gameplayReturn != null && gameplayRetryingField != null)
                {
                    store.FailNextProfileWrite = true;
                    try { gameplayReturn.Invoke(composition, null); }
                    catch (Exception exception) { exitRouteException = exitRouteException ?? exception; }
                }
                for (int frame = 0; frame < 120 && store.ProfileWriteAttempts == profileWritesBeforeCompositionRetry; frame++)
                    yield return null;
                for (int frame = 0; frame < 120 && gameplayRetryingField != null &&
                    (bool)gameplayRetryingField.GetValue(composition); frame++)
                    yield return null;
                yield return null;
                yield return null;
                bool gameplayCompositionStartedRetry = store.ProfileWriteAttempts > profileWritesBeforeCompositionRetry;
                bool gameplayCompositionRetrySettled = gameplayRetryingField != null &&
                    !(bool)gameplayRetryingField.GetValue(composition);
                bool pendingMutationAfterCompositionRetry = app.HasPendingSessionMutation;
                bool sceneStayedLoadedAfterCompositionRetry =
                    SceneManager.GetActiveScene().handle == sceneBeforeGuardChecks.handle && !unexpectedSceneLoaded;
                bool timeStayedPausedAfterCompositionRetry = Time.timeScale == 0f;

                RoomAdvanceCommitResult retried = null;
                store.FailNextProfileWrite = false;
                yield return Complete(app.RetryPendingRoomAdvanceAsync(), value => retried = value);
                Assert.That(retried.Persisted, Is.True, retried.Message);
                Assert.That(retried.Activated, Is.True, retried.Message);
                Assert.That(app.HasPendingSessionMutation, Is.False);
                Assert.That(pauseController.IsSimulationPaused, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(0.5f));

                Assert.That(firstWriteWasHeld, Is.True, "The controlled store must hold the first room write.");
                Assert.That(firstWriteWasTracked, Is.True);
                Assert.That(noPendingRecordBeforeFirstWriteSettles, Is.True,
                    "The first write must be guarded before App can publish a failed-write pending record.");
                Assert.That(sessionMutationWasGuardedDuringFirstWrite, Is.True);
                Assert.That(simulationWasPausedDuringFirstWrite, Is.True);
                Assert.That(globalTimeWasPausedDuringFirstWrite, Is.True);
                Assert.That(sceneStayedLoadedDuringFirstWrite, Is.True,
                    "Neither return path may unload the session while the first room write is in flight.");
                Assert.That(timeStayedPausedAfterExitRequests, Is.True,
                    "A return guard must not undo the simulation lease by resetting global timeScale.");
                Assert.That(sceneStayedLoadedAfterPresenterExitRequest, Is.True,
                    "The fallback presenter must remain blocked after the exact retry has failed.");
                Assert.That(timeStayedPausedAfterPresenterExitRequest, Is.True);
                Assert.That(gameplayRetryFlagAvailable, Is.True);
                Assert.That(gameplayCompositionStartedRetry, Is.True,
                    "The gameplay return button retries the exact pending room write instead of leaving the session.");
                Assert.That(gameplayCompositionRetrySettled, Is.True);
                Assert.That(pendingMutationAfterCompositionRetry, Is.True,
                    "A failed gameplay retry must leave the session mutation blocked.");
                Assert.That(sceneStayedLoadedAfterCompositionRetry, Is.True);
                Assert.That(timeStayedPausedAfterCompositionRetry, Is.True);
                Assert.That(gameplayReturnAvailable, Is.True, "The gameplay exit route must remain testable.");
                Assert.That(exitRouteException, Is.Null, exitRouteException?.ToString());
            }
            finally
            {
                store.ReleaseBlockedProfileWrite();
                if (sceneLoadedSubscribed) SceneManager.sceneLoaded -= sceneLoadedHandler;
                pauseProviderBinding?.Dispose();
                if (pauseOwner != null) UnityEngine.Object.DestroyImmediate(pauseOwner);
                if (gameplayHost != null) UnityEngine.Object.DestroyImmediate(gameplayHost);
                if (presenterHost != null) UnityEngine.Object.DestroyImmediate(presenterHost);
                Time.timeScale = previousTimeScale;
                Physics2D.simulationMode = previousPhysicsMode;
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
            IDisposable pauseProviderBinding = null;
            try
            {
                ApplicationBootstrapper app = null;
                yield return StartApplication(value => app = value, "Menu");
                pauseProviderBinding = BindNoopSimulationPauseLeaseProvider(app);
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

                ExpeditionSaveData committedSnapshot = app.ActiveExpedition;
                string committedSnapshotJson = JsonUtility.ToJson(committedSnapshot);
                SaveStorageSnapshot committedStorage = store.ReadAsync(SaveDocument.Profile).GetAwaiter().GetResult();
                Assert.That(committedStorage.PrimaryJson, Is.EqualTo(store.ProfilePrimary));

                DestroyRoomWorld(world);
                world = null;
                pauseProviderBinding.Dispose();
                pauseProviderBinding = null;
                DestroyApplication();
                yield return null;
                yield return StartApplication(value => app = value, "Menu");
                pauseProviderBinding = BindNoopSimulationPauseLeaseProvider(app);
                ExpeditionSaveData restored = app.ActiveExpedition;
                Assert.That(app.ProfileLoadStatus, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
                Assert.That(app.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
                Assert.That(app.SaveDataReady, Is.True);
                Assert.That(app.CanWriteProfile, Is.False);
                Assert.That(app.CanStartNewExpedition, Is.False);
                Assert.That(app.CanContinueExpedition, Is.False);
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(app.HasActiveExpedition, Is.True);
                Assert.That(restored.currentNodeId, Is.EqualTo(targetNodeId));
                Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(committedSnapshotJson));
                Assert.That(app.ActiveExpeditionLoadError, Is.EqualTo("LegacyEncounterPinMissing"));
                Assert.That(app.ProfileLoadMessage, Is.EqualTo(
                    "La expedición guardada se conserva, pero no puede continuarse porque falta la referencia de su catálogo de encuentros."));

                bool continueRejected = false;
                try { app.ContinueActiveExpedition(); }
                catch (InvalidOperationException) { continueRejected = true; }
                Assert.That(continueRejected, Is.True);
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));

                SaveWriteResult blockedStart = null;
                yield return Complete(app.BeginExpeditionAsync(run), value => blockedStart = value);
                Assert.That(blockedStart.Succeeded, Is.False);

                world = CreateRoomWorld(run, restored.visitedNodeIds);
                RoomInstanceId protectedRoomId = world.Controller.CurrentRoomId;
                string protectedWorldNodeBefore = world.Controller.CurrentNodeId;
                RoomAdvanceCommitResult blockedReplay = null;
                yield return Complete(app.CompleteAndAdvanceRoomAsync(world.Controller, protectedRoomId,
                    targetNodeId, resultId, resultJson), value => blockedReplay = value);
                Assert.That(blockedReplay.Persisted, Is.False);
                Assert.That(blockedReplay.AlreadyApplied, Is.False);
                Assert.That(blockedReplay.Activated, Is.False);
                Assert.That(blockedReplay.Pending, Is.False);
                Assert.That(world.Controller.CurrentNodeId, Is.EqualTo(protectedWorldNodeBefore));
                Assert.That(world.Controller.ActiveRoomCount, Is.EqualTo(1));
                Assert.That(app.ActiveRun, Is.Null);
                Assert.That(JsonUtility.ToJson(app.ActiveExpedition), Is.EqualTo(committedSnapshotJson));
                SaveStorageSnapshot storageAfterBlockedCalls = store.ReadAsync(SaveDocument.Profile).GetAwaiter().GetResult();
                Assert.That(storageAfterBlockedCalls.PrimaryJson, Is.EqualTo(committedStorage.PrimaryJson));
                Assert.That(storageAfterBlockedCalls.BackupJson, Is.EqualTo(committedStorage.BackupJson));
                Assert.That(store.ProfileWriteSuccesses, Is.EqualTo(successfulWrites));
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Menu"));
            }
            finally
            {
                pauseProviderBinding?.Dispose();
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

        private static ExpeditionInventory CreateInventoryCandidate(ExpeditionRunConfiguration run, int goldToAdd)
        {
            ExpeditionInventory candidate = ExpeditionInventory.Restore(
                run.Inventory.Catalog, run.Inventory.CreateSnapshot());
            Assert.That(candidate.TryAddGold(goldToAdd, TransactionId.New()).Succeeded, Is.True);
            return candidate;
        }

        private static IDisposable BindNoopSimulationPauseLeaseProvider(ApplicationBootstrapper application)
        {
            return application.BindExpeditionSimulationPauseLeaseProvider(new object(),
                () => new TestSimulationPauseLease());
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

        private sealed class TestSimulationPauseLease : IDisposable
        {
            public void Dispose() { }
        }

        private sealed class CountingSimulationPauseLease : IDisposable
        {
            private Action _onDispose;

            public CountingSimulationPauseLease(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                Action callback = _onDispose;
                _onDispose = null;
                callback?.Invoke();
            }
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

            public Task HoldNextProfileWrite()
            {
                _blockNextProfileWrite = true;
                _profileWriteStarted = new TaskCompletionSource<bool>();
                _releaseBlockedProfileWrite = new TaskCompletionSource<bool>();
                return _profileWriteStarted.Task;
            }

            public void ReleaseBlockedProfileWrite()
            {
                _releaseBlockedProfileWrite?.TrySetResult(true);
            }

            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileWriteAttempts++;
                    if (_blockNextProfileWrite)
                    {
                        _blockNextProfileWrite = false;
                        _profileWriteStarted?.TrySetResult(true);
                        await _releaseBlockedProfileWrite.Task;
                    }
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
            }

            private bool _blockNextProfileWrite;
            private TaskCompletionSource<bool> _profileWriteStarted;
            private TaskCompletionSource<bool> _releaseBlockedProfileWrite;
        }
    }
}
