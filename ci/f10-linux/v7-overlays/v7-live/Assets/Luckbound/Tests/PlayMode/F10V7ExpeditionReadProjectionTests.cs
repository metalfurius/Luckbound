#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class F10V7ExpeditionReadProjectionTests
    {
        [Test]
        public void PreparedE1CandidateProjectsItsAdvancedStateAndNewCheckpointAfterAuthentication()
        {
            Fixture fixture = CreateFixture();
            ExpeditionSaveDataV7 state = SaveV7Clone.Clone(fixture.Envelope.activeExpedition);
            string c1 = state.currentNodeId;
            const string e1 = "sample.patio.exploration";
            state.completedRoomIds = state.completedRoomIds.Concat(new[] { c1 }).ToArray();
            state.completedRoomResults = state.completedRoomResults.Concat(new[]
            {
                new ExpeditionRoomCompletionSaveDataV7
                { roomNodeId = c1, resultId = "fixture.c1.complete", resultJson = "{\"synthetic\":true}" }
            }).ToArray();
            state.currentNodeId = e1;
            state.visitedNodeIds = state.visitedNodeIds.Concat(new[] { e1 }).ToArray();
            state.claimedRewardIds = new[] { "fixture.c1.paid" };
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(state.encounterPlanPin,
                out EncounterPlanPinV1 prior, out SaveV7Error error), Is.True, error.Message);
            Assert.That(fixture.Registry.TryGetExact(prior.CatalogId, prior.CatalogRevision,
                prior.CatalogSha256, out EncounterCatalogManifest manifest,
                out EncounterCatalogError catalogError), Is.True, catalogError.Message);
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, fixture.Run, e1,
                state.pactIds, out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 planError), Is.True, planError.Message);
            EncounterPlanPinV1 pin = EncounterPlanPinV1.Append(prior, plan, e1);
            state.encounterPlanPin = SaveV7CandidateMapper.ToPinDto(pin);
            state.roomEntryCheckpointJson = null;
            ProfileSaveEnvelopeV7 prepared = SaveV7CandidateMapper.Prepare(
                fixture.Envelope.profile, state, pin).CreateEnvelope(3, 3,
                "2026-11-27T00:00:00.0000000Z");
            Assert.That(F10V7ExpeditionReadProjector.TryCreateCurrent(prepared,
                fixture.Run.Inventory.Catalog, fixture.Registry, out _, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.Corrupted));
            Assert.That(SaveV7JsonCodec.TryDecodeProfile(SaveV7JsonCodec.EncodeProfile(prepared),
                out ProfileSaveEnvelopeV7 authenticated, out error), Is.True, error.Message);
            Assert.That(F10V7ExpeditionReadProjector.TryCreateCurrent(authenticated,
                fixture.Run.Inventory.Catalog, fixture.Registry,
                out F10V7ExpeditionReadProjection projection, out error), Is.True, error.Message);
            ExpeditionSaveData view = projection.CompositionReadOnlyView;
            Assert.That(view.currentNodeId, Is.EqualTo(e1));
            CollectionAssert.AreEqual(state.claimedRewardIds, view.claimedRewardIds);
            CollectionAssert.AreEqual(state.completedRoomIds, view.completedRoomIds);
            Assert.That(view.completedRoomResults.Last().resultId, Is.EqualTo("fixture.c1.complete"));
            Assert.That(projection.CheckpointAuthorityV7.roomNodeId, Is.EqualTo(e1));
            var checkpoint = UnityEngine.JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(view.roomEntryCheckpointJson);
            Assert.That(checkpoint.roomNodeId, Is.EqualTo(e1));
            CollectionAssert.AreEqual(state.claimedRewardIds, checkpoint.claimedRewardIds);
        }

        [Test]
        public void CurrentProjectionPreservesAuthenticatedLiveStateWhileResumeRollsBack()
        {
            Fixture fixture = CreateFixture();
            ExpeditionSaveDataV7 source = fixture.Envelope.activeExpedition;
            Assert.That(SaveV7JsonCodec.TryDecodeCheckpoint(Encoding.UTF8.GetBytes(source.roomEntryCheckpointJson),
                out ExpeditionRoomCheckpointSaveDataV7 checkpoint, out SaveV7Error error), Is.True, error.Message);
            int liveHealth = checkpoint.currentHealth > 0 ? checkpoint.currentHealth - 1 : checkpoint.currentHealth + 1;
            source.currentHealth = liveHealth;
            source.claimedRewardIds = new[] { "claim.after.entry" };
            Assert.That(SaveV7JsonCodec.TryDecodeProfile(SaveV7JsonCodec.EncodeProfile(fixture.Envelope),
                out ProfileSaveEnvelopeV7 authenticated, out error), Is.True, error.Message);

            Assert.That(F10V7ExpeditionReadProjector.TryCreateCurrent(authenticated, fixture.Run.Inventory.Catalog,
                fixture.Registry, out F10V7ExpeditionReadProjection current, out error), Is.True, error.Message);
            Assert.That(current.CompositionReadOnlyView.currentHealth, Is.EqualTo(liveHealth));
            CollectionAssert.AreEqual(new[] { "claim.after.entry" }, current.CompositionReadOnlyView.claimedRewardIds);
            Assert.That(current.RestoredStateV7.currentHealth, Is.EqualTo(liveHealth));
            Assert.That(current.CheckpointAuthorityV7.currentHealth, Is.EqualTo(checkpoint.currentHealth));
            Assert.That(current.CheckpointAuthorityV7.encounterEntry, Is.Not.Null);

            Assert.That(F10V7ExpeditionReadProjector.TryCreate(authenticated, fixture.Run.Inventory.Catalog,
                fixture.Registry, out F10V7ExpeditionReadProjection resumed, out error), Is.True, error.Message);
            Assert.That(resumed.CompositionReadOnlyView.currentHealth, Is.EqualTo(checkpoint.currentHealth));
            CollectionAssert.AreEqual(checkpoint.claimedRewardIds, resumed.CompositionReadOnlyView.claimedRewardIds);
        }

        [Test]
        public void ProjectionUsesMapperRollbackAndKeepsV7PinAndCheckpointAuthority()
        {
            Fixture fixture = CreateFixture();
            ExpeditionSaveDataV7 sourceState = fixture.Envelope.activeExpedition;
            Assert.That(SaveV7JsonCodec.TryDecodeCheckpoint(Encoding.UTF8.GetBytes(sourceState.roomEntryCheckpointJson),
                out ExpeditionRoomCheckpointSaveDataV7 checkpoint, out SaveV7Error error), Is.True, error.Message);

            ExpeditionRouteNode currentNode = fixture.Run.Route.Nodes.Single(node =>
                node.NodeId == sourceState.currentNodeId);
            Assert.That(currentNode.Kind, Is.EqualTo(ExpeditionRouteNodeKind.Combat));
            Assert.That(checkpoint.roomNodeId, Is.EqualTo(sourceState.currentNodeId));
            int liveHealth = checkpoint.currentHealth == checkpoint.maximumHealth
                ? checkpoint.currentHealth - 1 : checkpoint.currentHealth + 1;
            sourceState.currentHealth = liveHealth;
            Assert.That(sourceState.currentHealth, Is.Not.EqualTo(checkpoint.currentHealth),
                "The authenticated C1 live state must differ from its entry checkpoint before rollback.");
            sourceState.claimedRewardIds = new[] { "claim.after.entry" };
            Assert.That(SaveV7JsonCodec.TryDecodeProfile(SaveV7JsonCodec.EncodeProfile(fixture.Envelope),
                out ProfileSaveEnvelopeV7 authenticated, out error), Is.True, error.Message);

            Assert.That(SaveV7CandidateMapper.TryRestore(authenticated, fixture.Run.Inventory.Catalog,
                fixture.Registry, out ExpeditionRunConfiguration restoredRun,
                out ExpeditionSaveDataV7 restoredState, out ResolvedEncounterPlanV1 restoredPlan,
                out error), Is.True, error.Message);
            Assert.That(restoredRun, Is.Not.Null);
            Assert.That(restoredPlan, Is.Not.Null);
            Assert.That(restoredState.currentHealth, Is.EqualTo(checkpoint.currentHealth),
                "The mapper's combat-entry rollback is the value the projection must consume.");

            Assert.That(F10V7ExpeditionReadProjector.TryCreate(authenticated, fixture.Run.Inventory.Catalog,
                fixture.Registry, out F10V7ExpeditionReadProjection projection, out error), Is.True, error.Message);

            ExpeditionSaveData view = projection.CompositionReadOnlyView;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.currentHealth, Is.EqualTo(restoredState.currentHealth));
            Assert.That(view.currentNodeId, Is.EqualTo(restoredState.currentNodeId));
            Assert.That(view.inventorySnapshotJson, Is.EqualTo(restoredState.inventorySnapshotJson));
            CollectionAssert.AreEqual(restoredState.visitedNodeIds, view.visitedNodeIds);
            CollectionAssert.AreEqual(restoredState.claimedRewardIds, view.claimedRewardIds);
            CollectionAssert.AreEqual(restoredState.collectedItemIds, view.collectedItemIds);
            Assert.That(view.fortunaCharges, Is.EqualTo(restoredState.fortuneOfferBookSnapshot.charges));

            ExpeditionSaveDataV7 sourceAuthority = projection.SourceStateV7;
            ExpeditionSaveDataV7 restoredAuthority = projection.RestoredStateV7;
            Assert.That(sourceAuthority.currentHealth, Is.EqualTo(liveHealth));
            Assert.That(sourceAuthority.claimedRewardIds, Is.EqualTo(new[] { "claim.after.entry" }));
            Assert.That(sourceAuthority.encounterPlanPin, Is.Not.Null);
            Assert.That(restoredAuthority.currentHealth, Is.EqualTo(checkpoint.currentHealth));
            Assert.That(projection.CheckpointAuthorityV7.encounterEntry, Is.Not.Null,
                "The V6 checkpoint DTO has no slot for this V7 field; the wrapper retains it.");

            ExpeditionRoomCheckpointSaveData checkpointView =
                UnityEngine.JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(view.roomEntryCheckpointJson);
            Assert.That(checkpointView, Is.Not.Null);
            Assert.That(checkpointView.roomNodeId, Is.EqualTo(checkpoint.roomNodeId));
            Assert.That(checkpointView.inventorySnapshotJson, Is.EqualTo(checkpoint.inventorySnapshotJson));
            Assert.That(checkpointView.currentHealth, Is.EqualTo(checkpoint.currentHealth));
            CollectionAssert.AreEqual(checkpoint.claimedRewardIds, checkpointView.claimedRewardIds);

            view.currentHealth = -1;
            Assert.That(projection.CompositionReadOnlyView.currentHealth, Is.EqualTo(restoredState.currentHealth),
                "Composition consumers receive detached copies; V7 authority remains unchanged.");
        }

        private static Fixture CreateFixture()
        {
            ExpeditionRunConfiguration run = InvokeFixture<ExpeditionRunConfiguration>("CreateRun", 0x61f09u);
            EncounterCatalogManifest manifest = InvokeFixture<EncounterCatalogManifest>(
                "CreateManifest", 1, "variant.r1", false, false);
            EncounterCatalogRegistryV1 registry = InvokeFixture<EncounterCatalogRegistryV1>(
                "Registry", (object)new[] { manifest });
            PinnedProfileCandidateV7 initial = InvokeFixture<PinnedProfileCandidateV7>("Initial", run, manifest);
            ProfileSaveEnvelopeV7 initialEnvelope = initial.CreateEnvelope(1, 1,
                "2026-11-20T00:00:00.0000000Z");
            Assert.That(SaveV7JsonCodec.TryDecodeProfile(SaveV7JsonCodec.EncodeProfile(initialEnvelope),
                out initialEnvelope, out SaveV7Error error), Is.True, error.Message);
            PinnedProfileCandidateV7 advanced = InvokeFixture<PinnedProfileCandidateV7>(
                "Advance", initialEnvelope, run, manifest);
            ProfileSaveEnvelopeV7 envelope = advanced.CreateEnvelope(2, 2,
                "2026-11-20T00:00:00.0000000Z");
            Assert.That(SaveV7JsonCodec.TryDecodeProfile(SaveV7JsonCodec.EncodeProfile(envelope),
                out envelope, out error), Is.True, error.Message);
            return new Fixture(run, registry, envelope);
        }

        private static T InvokeFixture<T>(string name, params object[] arguments)
        {
            MethodInfo method = typeof(SaveV7AtomicRepositoryTests).GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Fixture method not found: " + name);
            return (T)method.Invoke(null, arguments);
        }

        private sealed class Fixture
        {
            internal Fixture(ExpeditionRunConfiguration run, EncounterCatalogRegistryV1 registry,
                ProfileSaveEnvelopeV7 envelope)
            { Run = run; Registry = registry; Envelope = envelope; }

            internal ExpeditionRunConfiguration Run { get; }
            internal EncounterCatalogRegistryV1 Registry { get; }
            internal ProfileSaveEnvelopeV7 Envelope { get; }
        }
    }
}
#endif
