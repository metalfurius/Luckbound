#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    /// <summary>
    /// Storage-only contract probes. The fixture is an authenticated serialized V7 profile;
    /// these tests do not claim that extraction occurred or construct a positive physical permit.
    /// </summary>
    public sealed class SaveRepositoryV7SettlementHandleTests
    {
        [Test]
        public async Task ForeignHandleIsRejectedBeforeFreshReadOrWrite()
        {
            Fixture fixture = CreateFixture();
            var firstStore = new RecordingStore(fixture.Json);
            var secondStore = new RecordingStore(fixture.Json);
            var firstRepository = new SaveRepository(firstStore);
            var secondRepository = new SaveRepository(secondStore);
            F10V7SafeOpenResult first = await firstRepository.TryOpenActiveV7ForPreproductionAsync(
                fixture.Run.Inventory.Catalog, fixture.Registry);
            F10V7SafeOpenResult second = await secondRepository.TryOpenActiveV7ForPreproductionAsync(
                fixture.Run.Inventory.Catalog, fixture.Registry);
            Assert.That(first.Opened, Is.True, first.Error.Message);
            Assert.That(second.Opened, Is.True, second.Error.Message);

            int reads = firstStore.Reads;
            F10ExtractedSettlementV7WriteResult result =
                await firstRepository.CommitF10ExtractedV7ForPreproductionAsync(
                    second.SessionHandle, null);

            Assert.That(result.Persisted, Is.False);
            Assert.That(result.Message, Does.Contain("SessionHandleMismatch"));
            Assert.That(firstStore.Reads, Is.EqualTo(reads));
            Assert.That(firstStore.Writes, Is.Zero);
            Assert.That(firstRepository.ExportPendingJson(SaveDocument.Profile), Is.Null);
        }

        [Test]
        public async Task CompetingSessionMutationBlocksSettlementWithoutAnotherWrite()
        {
            Fixture fixture = CreateFixture();
            var store = new RecordingStore(fixture.Json) { FailWrites = true };
            var repository = new SaveRepository(store);
            F10V7SafeOpenResult opened = await repository.TryOpenActiveV7ForPreproductionAsync(
                fixture.Run.Inventory.Catalog, fixture.Registry);
            Assert.That(opened.Opened, Is.True, opened.Error.Message);

            ExpeditionSaveDataV7 candidateState = SaveV7Clone.Clone(opened.View.Envelope.activeExpedition);
            candidateState.claimedRewardIds = new[] { "fixture.storage-only.claim" };
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(candidateState.encounterPlanPin,
                out EncounterPlanPinV1 pin, out SaveV7Error pinError), Is.True, pinError.Message);
            PinnedProfileCandidateV7 mutation = SaveV7CandidateMapper.Prepare(
                opened.View.Envelope.profile, candidateState, pin);
            F10V7SessionCommitResult failedMutation =
                await repository.CommitF10V7SessionForPreproductionAsync(opened.SessionHandle, mutation);
            Assert.That(failedMutation.Persisted, Is.False);
            Assert.That(failedMutation.Pending, Is.True, failedMutation.Message);
            string exactPending = repository.ExportPendingJson(SaveDocument.Profile);
            Assert.That(exactPending, Is.Not.Null.And.Not.Empty);
            int writes = store.Writes;
            int reads = store.Reads;

            F10ExtractedSettlementV7WriteResult result =
                await repository.CommitF10ExtractedV7ForPreproductionAsync(opened.SessionHandle, null);

            Assert.That(result.Persisted, Is.False);
            Assert.That(result.Message, Does.Contain("WritePending"));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(store.Reads, Is.EqualTo(reads));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.EqualTo(exactPending));
        }

        [Test]
        public async Task FreshAuthenticatedSourceMismatchIsRejectedWithoutWriting()
        {
            Fixture fixture = CreateFixture();
            var store = new RecordingStore(fixture.Json);
            var repository = new SaveRepository(store);
            F10V7SafeOpenResult opened = await repository.TryOpenActiveV7ForPreproductionAsync(
                fixture.Run.Inventory.Catalog, fixture.Registry);
            Assert.That(opened.Opened, Is.True, opened.Error.Message);

            ProfileSaveEnvelopeV7 different = SaveV7Clone.Clone(opened.View.Envelope);
            different.generation++;
            different.sequence++;
            store.Primary = Encoding.UTF8.GetString(SaveV7JsonCodec.EncodeProfile(different));
            int writes = store.Writes;

            F10ExtractedSettlementV7WriteResult result =
                await repository.CommitF10ExtractedV7ForPreproductionAsync(opened.SessionHandle, null);

            Assert.That(result.Persisted, Is.False);
            Assert.That(result.Message, Does.Contain("SessionSourceMismatch"));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(ReadField(repository, "_v7ExtractedSettlementOwner"), Is.Null);
        }

        [Test]
        public async Task MissingPermitAndPrematureAcknowledgementKeepHandleAndWriteNothing()
        {
            Fixture fixture = CreateFixture();
            var store = new RecordingStore(fixture.Json);
            var repository = new SaveRepository(store);
            F10V7SafeOpenResult opened = await repository.TryOpenActiveV7ForPreproductionAsync(
                fixture.Run.Inventory.Catalog, fixture.Registry);
            Assert.That(opened.Opened, Is.True, opened.Error.Message);

            F10ExtractedSettlementV7WriteResult commit =
                await repository.CommitF10ExtractedV7ForPreproductionAsync(opened.SessionHandle, null);
            Assert.That(commit.Persisted, Is.False);
            Assert.That(commit.Pending, Is.False);
            Assert.That(commit.Owner, Is.Null);
            Assert.That(commit.Message, Does.Contain("PhysicalPermitRequired"));

            int writes = store.Writes;
            F10ExtractedSettlementV7WriteResult ack =
                await repository.AcknowledgeF10ExtractedV7ForPreproductionAsync(
                    opened.SessionHandle, commit.Owner);
            Assert.That(ack.Persisted, Is.False);
            Assert.That(ack.Message, Does.Contain("ProtocolConflict"));
            Assert.That(store.Writes, Is.EqualTo(writes));
            Assert.That(ReadField(repository, "_f10V7SessionHandle"), Is.SameAs(opened.SessionHandle));
        }

        private static Fixture CreateFixture()
        {
            MethodInfo factory = typeof(SaveV7ExtractedSettlementCandidateTests).GetMethod(
                "CreateFixture", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(factory, Is.Not.Null);
            object source = factory.Invoke(null, new object[] { true, false, 300, 31 });
            Type sourceType = source.GetType();
            const BindingFlags fixtureMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var run = (ExpeditionRunConfiguration)sourceType.GetProperty("Run", fixtureMembers).GetValue(source);
            var registry = (EncounterCatalogRegistryV1)sourceType.GetProperty("Registry", fixtureMembers).GetValue(source);
            var envelope = (ProfileSaveEnvelopeV7)sourceType.GetProperty("Envelope", fixtureMembers).GetValue(source);
            string json = Encoding.UTF8.GetString(SaveV7JsonCodec.EncodeProfile(envelope));
            return new Fixture(run, registry, json);
        }

        private static object ReadField(object target, string name) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        private sealed class Fixture
        {
            internal Fixture(ExpeditionRunConfiguration run, EncounterCatalogRegistryV1 registry, string json)
            { Run = run; Registry = registry; Json = json; }
            internal ExpeditionRunConfiguration Run { get; }
            internal EncounterCatalogRegistryV1 Registry { get; }
            internal string Json { get; }
        }

        private sealed class RecordingStore : ISaveStore
        {
            internal RecordingStore(string primary) { Primary = primary; }
            internal string Primary;
            internal string Backup;
            internal int Reads;
            internal int Writes;
            internal bool FailWrites;

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                Reads++;
                return Task.FromResult(new SaveStorageSnapshot(Primary, Backup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                Writes++;
                if (FailWrites) return Task.FromException(new InvalidOperationException("Fixture write failure."));
                if (lastKnownGoodPrimaryJson != null) Backup = lastKnownGoodPrimaryJson;
                Primary = json;
                return Task.CompletedTask;
            }
        }
    }
}
#endif
