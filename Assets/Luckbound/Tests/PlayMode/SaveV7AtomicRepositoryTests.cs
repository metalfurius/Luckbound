#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Fortune;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class SaveV7AtomicRepositoryTests
    {
        private const string CatalogId = "catalog.pin.storage.synthetic";
        private const string NodeId = "sample.patio.combat";
        private const string TutorialId = "sample.tutorial";
        private const string EncounterId = "encounter.pin.storage.synthetic";
        private string _directory;

        [SetUp]
        public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), "LuckboundSaveV7Tests", Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            // Resolve and check the exact test sandbox before a recursive cleanup.
            string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LuckboundSaveV7Tests")) + Path.DirectorySeparatorChar;
            string target = Path.GetFullPath(_directory);
            if (!target.StartsWith(parent, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe test cleanup path.");
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PinAdapterMatchesOriginalCoreBytesAndRejectsLossyInputs(bool none)
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1", none);
            EncounterPlanPinV1 original = Pin(manifest, run, none ? TutorialId : NodeId);
            EncounterPlanPinSaveDataV7 dto = SaveV7CandidateMapper.ToPinDto(original);
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(dto, out EncounterPlanPinV1 roundTrip, out SaveV7Error error), Is.True, error.Message);
            Assert.That(EncounterPlanPinCanonicalV1.Encode(roundTrip), Is.EqualTo(EncounterPlanPinCanonicalV1.Encode(original)));
            dto.bindings[0].contextPactIds = new[] { "pact.haste" };
            Assert.That(original.Bindings[0].ContextPactIds.Count, Is.EqualTo(0));
            dto = SaveV7CandidateMapper.ToPinDto(original);
            dto.bindings[0].calendarOrdinal = 1;
            AssertRejectedPin(dto, SaveV7ErrorCode.MissingResolutionAuthority);
            dto = SaveV7CandidateMapper.ToPinDto(original);
            dto.runId = dto.runId.ToUpperInvariant();
            AssertRejectedPin(dto, SaveV7ErrorCode.InvalidData);
            dto = SaveV7CandidateMapper.ToPinDto(original);
            dto.entry.resolvedPlanSha256 = new string('f', 64);
            AssertRejectedPin(dto, SaveV7ErrorCode.InvalidData);
            dto = SaveV7CandidateMapper.ToPinDto(original);
            dto.bindings[0].contextPactIds = new[] { "pact.haste", "pact.fervor" };
            AssertRejectedPin(dto, SaveV7ErrorCode.InvalidData);
            dto = SaveV7CandidateMapper.ToPinDto(original);
            dto.bindings[0].contextPactIds = new[] { "pact.haste", "pact.haste" };
            AssertRejectedPin(dto, SaveV7ErrorCode.InvalidData);
            if (none)
            {
                dto = SaveV7CandidateMapper.ToPinDto(original);
                dto.bindings[0].encounterId = "forbidden";
                AssertRejectedPin(dto, SaveV7ErrorCode.InvalidData);
            }
            AssertRejectedPin(null, SaveV7ErrorCode.InvalidData);
        }

        [Test]
        public void FileStoreCommitsInitialAndTransitionAtomicallyAndRestoresExactRetainedRevisionAndBackup()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest first = CreateManifest(1, "variant.r1");
            EncounterCatalogManifest latest = CreateManifest(2, "variant.r2");
            EncounterCatalogRegistryV1 registry = Registry(first, latest);
            var store = new FileSaveStore(_directory, null);
            var repository = new SaveRepository(store);
            PinnedProfileCandidateV7 a = Initial(run, first);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(a, run, registry)).Succeeded, Is.True);
            string initialBytes = File.ReadAllText(store.GetPrimaryPath(SaveDocument.Profile));
            ProfileSaveEnvelopeV7 initial = Wait(repository.LoadProfileV7ForPreproductionAsync()).Envelope;
            PinnedProfileCandidateV7 b = Advance(initial, run, first);
            ProfileSaveEnvelopeV7 duringCombat = b.CreateEnvelope(1, 1, "2000-01-01T00:00:00.0000000Z");
            string entryCheckpoint = duringCombat.activeExpedition.roomEntryCheckpointJson;
            duringCombat.activeExpedition.currentHealth = 42;
            duringCombat.activeExpedition.currentEnergy = 12;
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(duringCombat.activeExpedition.encounterPlanPin,
                out EncounterPlanPinV1 combatPin, out SaveV7Error combatError), Is.True, combatError.Message);
            b = SaveV7CandidateMapper.Prepare(duringCombat.profile, duringCombat.activeExpedition, combatPin);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(b, run, registry)).Succeeded, Is.True);
            string transitionBytes = File.ReadAllText(store.GetPrimaryPath(SaveDocument.Profile));
            Assert.That(File.ReadAllText(store.GetBackupPath(SaveDocument.Profile)), Is.EqualTo(initialBytes));
            Assert.That(Directory.GetFiles(_directory).Select(Path.GetFileName).OrderBy(x => x),
                Is.EqualTo(new[] { FileSaveStore.ProfileFileName, FileSaveStore.ProfileFileName + ".bak" }.OrderBy(x => x)));

            var fresh = new SaveRepository(new FileSaveStore(_directory, null));
            SaveV7LoadView loaded = Wait(fresh.LoadProfileV7ForPreproductionAsync());
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.LoadedPrimary));
            Assert.That(loaded.SourceContentVersion, Is.EqualTo(7));
            Assert.That(loaded.OriginalJson, Is.EqualTo(transitionBytes));
            Assert.That(loaded.Envelope.generation, Is.EqualTo(2));
            Assert.That(loaded.Envelope.sequence, Is.EqualTo(2));
            Assert.That(loaded.Envelope.activeExpedition.completedRoomResults[0].resultJson, Is.EqualTo("{\"synthetic\":true}"));
            Assert.That(loaded.Envelope.activeExpedition.encounterPlanPin.bindings.Length, Is.EqualTo(2));
            Assert.That(SaveV7CandidateMapper.TryRestore(loaded.Envelope, run.Inventory.Catalog, registry,
                out ExpeditionRunConfiguration restoredRun, out ExpeditionSaveDataV7 state,
                out ResolvedEncounterPlanV1 plan, out SaveV7Error error), Is.True, error.Message);
            Assert.That(plan.VariantId, Is.EqualTo("variant.r1"));
            Assert.That(state.currentNodeId, Is.EqualTo(NodeId));
            Assert.That(loaded.Envelope.activeExpedition.currentHealth, Is.EqualTo(42));
            Assert.That(state.currentHealth, Is.EqualTo(100));
            Assert.That(state.currentEnergy, Is.EqualTo(50));
            Assert.That(state.roomEntryCheckpointJson, Is.EqualTo(entryCheckpoint));
            Assert.That(restoredRun.RunId, Is.EqualTo(run.RunId));
            Assert.That(ExpeditionEncounterPlanProjection.Create(plan).Roster, Is.Not.Null);
            ProfileSaveEnvelopeV7 copy = loaded.Envelope;
            copy.profile.availableRenown = 999;
            copy.activeExpedition.encounterPlanPin.bindings[0].nodeId = "mutated";
            Assert.That(loaded.Envelope.profile.availableRenown, Is.EqualTo(0));
            Assert.That(loaded.Envelope.activeExpedition.encounterPlanPin.bindings[0].nodeId, Is.Not.EqualTo("mutated"));
            Assert.That(fresh.CurrentProfile, Is.Null);
            Assert.That(fresh.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable));

            File.WriteAllText(store.GetPrimaryPath(SaveDocument.Profile), "{broken");
            SaveV7LoadView backup = Wait(new SaveRepository(new FileSaveStore(_directory, null)).LoadProfileV7ForPreproductionAsync());
            Assert.That(backup.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(backup.OriginalJson, Is.EqualTo(initialBytes));
            Assert.That(SaveV7CandidateMapper.TryRestore(backup.Envelope, run.Inventory.Catalog, registry,
                out restoredRun, out state, out plan, out error), Is.True, error.Message);
            Assert.That(plan.Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(ExpeditionEncounterPlanProjection.Create(plan).Roster, Is.Null);
            Assert.That(state.completedRoomIds, Is.Empty);
            Assert.That(File.ReadAllText(store.GetPrimaryPath(SaveDocument.Profile)), Is.EqualTo("{broken"));
        }

        [Test]
        public void FileStoreFinalizationFailureRetainsExactPendingBytesAndIndependentSettings()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1");
            var syncer = new FailingFlushSyncer();
            var store = new FileSaveStore(_directory, syncer);
            var repository = new SaveRepository(store);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, manifest), run, Registry(manifest))).Succeeded, Is.True);
            string previous = File.ReadAllText(store.GetPrimaryPath(SaveDocument.Profile));
            ProfileSaveEnvelopeV7 prior = Wait(repository.LoadProfileV7ForPreproductionAsync()).Envelope;
            syncer.Fail = true;
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Advance(prior, run, manifest), run, Registry(manifest))).Succeeded, Is.False);
            string pending = repository.ExportPendingJson(SaveDocument.Profile);
            Assert.That(pending, Is.EqualTo(File.ReadAllText(store.GetPrimaryPath(SaveDocument.Profile))));
            Assert.That(File.ReadAllText(store.GetBackupPath(SaveDocument.Profile)), Is.EqualTo(previous));
            Assert.That(Wait(repository.LoadProfileV7ForPreproductionAsync()).Error.Code, Is.EqualTo(SaveV7ErrorCode.WritePending));
            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile())).Succeeded, Is.False);
            syncer.Fail = false;
            Assert.That(Wait(repository.SaveSettingsAsync(new GameSettingsSaveData { languageId = "en" })).Succeeded, Is.True);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.EqualTo(pending));
            Assert.That(Wait(repository.RetryPendingAsync(SaveDocument.Profile)).Succeeded, Is.True);
            Assert.That(File.ReadAllText(store.GetPrimaryPath(SaveDocument.Profile)), Is.EqualTo(pending));
            Assert.That(File.ReadAllText(store.GetBackupPath(SaveDocument.Profile)), Is.EqualTo(previous));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(Wait(repository.LoadProfileV7ForPreproductionAsync()).Envelope.sequence, Is.EqualTo(2));
            Assert.That(Wait(repository.LoadProfileAsync()).Message, Does.Contain("ProtocolConflict"));
            Assert.That(repository.CurrentProfile, Is.Null);
        }

        [Test]
        public void PendingV6PreventsLatchWithoutReadOrCacheChangeAndRetriesOriginalProtocol()
        {
            var store = new RecordingStore { FailWrites = true };
            var repository = new SaveRepository(store);
            Assert.That(Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile())).Succeeded, Is.False);
            ProfileSaveEnvelope before = repository.CurrentProfile;
            string pending = repository.ExportPendingJson(SaveDocument.Profile);
            int reads = store.Reads;
            Assert.That(Wait(repository.LoadProfileV7ForPreproductionAsync()).Error.Code, Is.EqualTo(SaveV7ErrorCode.WritePending));
            Assert.That(store.Reads, Is.EqualTo(reads));
            Assert.That(repository.CurrentProfile, Is.SameAs(before));
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.ReadWrite));
            store.FailWrites = false;
            Assert.That(Wait(repository.RetryPendingAsync(SaveDocument.Profile)).Succeeded, Is.True);
            Assert.That(store.Primary, Is.EqualTo(pending));
            Assert.That(Wait(repository.LoadProfileV7ForPreproductionAsync()).SourceContentVersion, Is.EqualTo(6));
            Assert.That(repository.CurrentProfile, Is.Null);
            Assert.That(Wait(repository.SaveProfileAsync(before.profile)).Message, Does.Contain("ProtocolConflict"));
        }

        [UnityTest]
        public IEnumerator PendingV7NeverReadsOrReencodesAndSerializesConcurrentV6Access()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1");
            var store = new RecordingStore { FailWrites = true };
            var repository = new SaveRepository(store);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, manifest), run, Registry(manifest))).Succeeded, Is.False);
            string pending = repository.ExportPendingJson(SaveDocument.Profile);
            int reads = store.Reads;
            Assert.That(Wait(repository.LoadProfileV7ForPreproductionAsync()).Error.Code, Is.EqualTo(SaveV7ErrorCode.WritePending));
            Assert.That(store.Reads, Is.EqualTo(reads));
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, manifest), run, Registry(manifest))).Succeeded, Is.False);
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.EqualTo(pending));
            store.FailWrites = false;
            store.BlockNextWrite = new TaskCompletionSource<bool>();
            Task<SaveWriteResult> retry = repository.RetryPendingAsync(SaveDocument.Profile);
            Task<SaveLoadResult<ProfileSaveEnvelope>> foreign = repository.LoadProfileAsync();
            Assert.That(retry.IsCompleted, Is.False);
            Assert.That(foreign.IsCompleted, Is.False);
            store.BlockNextWrite.SetResult(true);
            yield return WaitForTask(retry);
            yield return WaitForTask(foreign);
            Assert.That(retry.GetAwaiter().GetResult().Succeeded, Is.True);
            Assert.That(foreign.GetAwaiter().GetResult().Message, Does.Contain("ProtocolConflict"));
            Assert.That(store.WrittenJson.All(x => x == pending), Is.True);
            Assert.That(Wait(repository.RetryPendingAsync(SaveDocument.Profile)).Succeeded, Is.False);
            Assert.That(repository.CurrentProfile, Is.Null);
        }

        [Test]
        public void RejectsMutableEnvelopeTamperingMissingRevisionAndRewrittenEntryCheckpoint()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1");
            var store = new RecordingStore();
            var repository = new SaveRepository(store);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, manifest), run, Registry(manifest))).Succeeded, Is.True);
            ProfileSaveEnvelopeV7 envelope = Wait(repository.LoadProfileV7ForPreproductionAsync()).Envelope;
            envelope.profile.availableRenown++;
            Assert.That(SaveV7CandidateMapper.TryRestore(envelope, run.Inventory.Catalog, Registry(manifest),
                out ExpeditionRunConfiguration restoredRun, out ExpeditionSaveDataV7 state,
                out ResolvedEncounterPlanV1 plan, out SaveV7Error error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.Corrupted));
            Assert.That(restoredRun, Is.Null); Assert.That(state, Is.Null); Assert.That(plan, Is.Null);
            envelope = Wait(repository.LoadProfileV7ForPreproductionAsync()).Envelope;
            Assert.That(SaveV7CandidateMapper.TryRestore(envelope, run.Inventory.Catalog, Registry(CreateManifest(2, "variant.r2")),
                out restoredRun, out state, out plan, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.CatalogRevisionUnavailable));
            Assert.That(restoredRun, Is.Null); Assert.That(state, Is.Null); Assert.That(plan, Is.Null);
            ExpeditionSaveDataV7 rewritten = SaveV7Clone.Clone(envelope.activeExpedition);
            rewritten.currentHealth--;
            rewritten.roomEntryCheckpointJson = null;
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(rewritten.encounterPlanPin, out EncounterPlanPinV1 pin, out error), Is.True);
            int writes = store.Writes;
            SaveWriteResult rejected = Wait(repository.CommitPinnedV7ForPreproductionAsync(
                SaveV7CandidateMapper.Prepare(envelope.profile, rewritten, pin), run, Registry(manifest)));
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Message, Does.Contain("checkpoint"));
            Assert.That(store.Writes, Is.EqualTo(writes));
        }

        [Test]
        public void FuturePrimaryNeverFallsBackAndOperationalV6RejectsARealV7Document()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1");
            var store = new RecordingStore();
            var repository = new SaveRepository(store);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, manifest), run, Registry(manifest))).Succeeded, Is.True);
            string v7 = store.Primary;
            SaveLoadResult<ProfileSaveEnvelope> ordinary = Wait(new SaveRepository(store).LoadProfileAsync());
            Assert.That(ordinary.Status, Is.EqualTo(SaveLoadStatus.Incompatible));
            Assert.That(ordinary.Access, Is.EqualTo(SaveDataAccess.Unavailable));
            store.Backup = v7;
            store.Primary = v7.Replace("\"contentVersion\":7", "\"contentVersion\":8");
            string future = store.Primary;
            SaveV7LoadView loaded = Wait(new SaveRepository(store).LoadProfileV7ForPreproductionAsync());
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Incompatible));
            Assert.That(loaded.SourceContentVersion, Is.EqualTo(0));
            Assert.That(loaded.OriginalJson, Is.EqualTo(future));
            Assert.That(loaded.Envelope, Is.Null); Assert.That(loaded.LegacyDiagnostic, Is.Null);
            Assert.That(store.Primary, Is.EqualTo(future)); Assert.That(store.Backup, Is.EqualTo(v7));
        }

        [Test]
        public void CandidateCapturesInputsAndEnvelopeGettersWithoutAliases()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1");
            ProfileSaveDataV7 profile = SaveV7CandidateMapper.ToV7(ProfileSaveData.CreateNewProfile());
            ExpeditionSaveDataV7 state = StorageState(run);
            PinnedProfileCandidateV7 candidate = SaveV7CandidateMapper.Prepare(profile, state, Pin(manifest, run, TutorialId));
            profile.availableRenown = 999;
            state.visitedNodeIds[0] = "mutated";
            ProfileSaveEnvelopeV7 first = candidate.CreateEnvelope(1, 1, "2000-01-01T00:00:00.0000000Z");
            first.activeExpedition.encounterPlanPin.bindings[0].nodeId = "mutated";
            ProfileSaveEnvelopeV7 second = candidate.CreateEnvelope(1, 1, "2000-01-01T00:00:00.0000000Z");
            Assert.That(second.profile.availableRenown, Is.EqualTo(0));
            Assert.That(second.activeExpedition.visitedNodeIds[0], Is.EqualTo(TutorialId));
            Assert.That(second.activeExpedition.encounterPlanPin.bindings[0].nodeId, Is.EqualTo(TutorialId));
        }

        [Test]
        public void SignedUnknownInventorySchemaAndMissingEnteredBindingFailBeforeStorageOrPublication()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1");
            var store = new RecordingStore();
            var repository = new SaveRepository(store);
            Assert.That(Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, manifest), run, Registry(manifest))).Succeeded, Is.True);
            ProfileSaveEnvelopeV7 initial = Wait(repository.LoadProfileV7ForPreproductionAsync()).Envelope;
            ProfileSaveEnvelopeV7 futureInventory = SaveV7Clone.Clone(initial);
            futureInventory.activeExpedition.inventorySnapshotJson = "{\"snapshotVersion\":99}";
            byte[] encoded = SaveV7JsonCodec.EncodeProfile(futureInventory);
            Assert.That(SaveV7JsonCodec.TryDecodeProfile(encoded, out futureInventory, out SaveV7Error error), Is.True, error.Message);
            Assert.That(SaveV7CandidateMapper.TryRestore(futureInventory, run.Inventory.Catalog, Registry(manifest),
                out ExpeditionRunConfiguration restoredRun, out ExpeditionSaveDataV7 resumed,
                out ResolvedEncounterPlanV1 plan, out error), Is.False);
            Assert.That(error.Code, Is.EqualTo(SaveV7ErrorCode.InvalidData));
            Assert.That(restoredRun, Is.Null); Assert.That(resumed, Is.Null); Assert.That(plan, Is.Null);

            ProfileSaveEnvelopeV7 advanced = Advance(initial, run, manifest).CreateEnvelope(1, 1, "2000-01-01T00:00:00.0000000Z");
            advanced.activeExpedition.encounterPlanPin.bindings = advanced.activeExpedition.encounterPlanPin.bindings.Where(b => b.nodeId == NodeId).ToArray();
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(advanced.activeExpedition.encounterPlanPin,
                out EncounterPlanPinV1 incomplete, out error), Is.True, error.Message);
            Assert.Throws<SaveV7ValidationException>(() => SaveV7CandidateMapper.Prepare(advanced.profile, advanced.activeExpedition, incomplete));
            Assert.That(store.Writes, Is.EqualTo(1));
            Assert.That(repository.CurrentProfile, Is.Null);
        }

        [Test]
        public void InitialCommitRejectsCatalogMissingOnlyAFutureNodeWithoutWritingOrResolvingItsVariant()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest incomplete = CreateManifest(1, "variant.r1", omitFutureBinding: true);
            var store = new RecordingStore();
            var repository = new SaveRepository(store);
            SaveWriteResult result = Wait(repository.CommitPinnedV7ForPreproductionAsync(Initial(run, incomplete), run, Registry(incomplete)));
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("MissingResolutionAuthority"));
            Assert.That(store.Writes, Is.EqualTo(0));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
            Assert.That(repository.CurrentProfile, Is.Null);
        }

        private static void AssertRejectedPin(EncounterPlanPinSaveDataV7 dto, SaveV7ErrorCode expected)
        {
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(dto, out EncounterPlanPinV1 pin, out SaveV7Error error), Is.False);
            Assert.That(pin, Is.Null); Assert.That(error.Code, Is.EqualTo(expected));
        }

        private static PinnedProfileCandidateV7 Initial(ExpeditionRunConfiguration run, EncounterCatalogManifest manifest)
        {
            ExpeditionSaveDataV7 state = StorageState(run);
            return SaveV7CandidateMapper.Prepare(SaveV7CandidateMapper.ToV7(ProfileSaveData.CreateNewProfile()), state,
                Pin(manifest, run, TutorialId));
        }

        private static PinnedProfileCandidateV7 Advance(ProfileSaveEnvelopeV7 prior, ExpeditionRunConfiguration run, EncounterCatalogManifest manifest)
        {
            ExpeditionSaveDataV7 state = SaveV7Clone.Clone(prior.activeExpedition);
            state.completedRoomIds = new[] { TutorialId };
            state.completedRoomResults = new[] { new ExpeditionRoomCompletionSaveDataV7
                { roomNodeId = TutorialId, resultId = "synthetic.result.tutorial", resultJson = "{\"synthetic\":true}" } };
            state.currentNodeId = NodeId; state.visitedNodeIds = new[] { TutorialId, NodeId }; state.roomEntryCheckpointJson = null;
            Assert.That(SaveV7CandidateMapper.TryToDomainPin(state.encounterPlanPin, out EncounterPlanPinV1 previous, out SaveV7Error error), Is.True, error.Message);
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, NodeId, Array.Empty<string>(), out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 resolveError), Is.True, resolveError.Message);
            EncounterPlanPinV1 appended = EncounterPlanPinV1.Append(previous, plan, NodeId);
            state.encounterPlanPin = SaveV7CandidateMapper.ToPinDto(appended);
            return SaveV7CandidateMapper.Prepare(prior.profile, state, appended);
        }

        // Storage-only fixture: real Core route/inventory/Fortune snapshots, with no scene,
        // production loadout binder, Resources lookup, actors, combat, or fabricated victory.
        private static ExpeditionSaveDataV7 StorageState(ExpeditionRunConfiguration run)
        {
            return new ExpeditionSaveDataV7
            {
                runId = run.RunId.ToString(), seed = run.Seed, generationVersion = run.Route.GenerationVersion,
                contentVersion = 7, classId = run.ClassId, raceId = run.RaceId,
                selectedSkillIds = run.SkillIds.ToArray(),
                startingEquipmentInstanceIds = run.StartingEquipmentInstanceIds.Select(x => x.ToString()).ToArray(),
                routeSnapshotJson = run.Route.CanonicalForm, currentNodeId = TutorialId, visitedNodeIds = new[] { TutorialId },
                completedRoomIds = Array.Empty<string>(), completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveDataV7>(),
                inventorySnapshotJson = JsonUtility.ToJson(run.Inventory.CreateSnapshot()),
                maximumHealth = run.StartingHealth, currentHealth = run.StartingHealth,
                maximumEnergy = run.StartingEnergy, currentEnergy = run.StartingEnergy, healthPotionCharges = 0,
                frozenPermanentUpgrades = new ExpeditionPermanentUpgradeSnapshotSaveDataV7
                { healthUpgradeLevel = run.HealthUpgradeLevel, energyUpgradeLevel = run.EnergyUpgradeLevel, additionalPotionUpgradeLevel = run.AdditionalPotionUpgradeLevel },
                hasFrozenPermanentUpgrades = true, accumulatedRenown = 0, pactIds = Array.Empty<string>(),
                fortuneOfferBookSnapshot = SaveV7CandidateMapper.ToV7(new FortuneOfferBook(run.Route.GenerationVersion, run.Seed, run.RaceId).CreateSnapshot()),
                collectedItemIds = Array.Empty<string>(), claimedRewardIds = Array.Empty<string>()
            };
        }

        private static EncounterPlanPinV1 Pin(EncounterCatalogManifest manifest, ExpeditionRunConfiguration run, string node)
        {
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, node, Array.Empty<string>(), out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);
            return EncounterPlanPinV1.Capture(manifest, run, new[] { plan }, node);
        }

        private static EncounterCatalogRegistryV1 Registry(params EncounterCatalogManifest[] manifests)
        {
            Assert.That(EncounterCatalogRegistryV1.TryCreate(manifests, out EncounterCatalogRegistryV1 registry,
                out EncounterCatalogError error), Is.True, error.Message);
            return registry;
        }

        private static T Wait<T>(Task<T> task)
        {
            if (!task.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("The storage operation did not finish.");
            return task.GetAwaiter().GetResult();
        }

        private static IEnumerator WaitForTask(Task task)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (!task.IsCompleted && clock.Elapsed < TimeSpan.FromSeconds(15)) yield return null;
            Assert.That(task.IsCompleted, Is.True, "The concurrent storage operation timed out.");
        }

        private sealed class FailingFlushSyncer : IPersistentFileSystemSyncer
        {
            internal bool Fail;
            public Task PrepareForReadAsync() => Task.CompletedTask;
            public Task FlushAsync() => Fail ? Task.FromException(new IOException("Synthetic finalization failure")) : Task.CompletedTask;
        }

        private sealed class RecordingStore : ISaveStore
        {
            internal string Primary;
            internal string Backup;
            internal bool FailWrites;
            internal int Reads;
            internal int Writes;
            internal TaskCompletionSource<bool> BlockNextWrite;
            internal readonly List<string> WrittenJson = new List<string>();
            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            { Reads++; return Task.FromResult(new SaveStorageSnapshot(Primary, Backup)); }
            public async Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                Writes++; WrittenJson.Add(json);
                if (FailWrites) throw new IOException("Synthetic write failure");
                TaskCompletionSource<bool> blocker = BlockNextWrite;
                if (blocker != null) await blocker.Task;
                if (lastKnownGoodPrimaryJson != null) Backup = lastKnownGoodPrimaryJson;
                Primary = json;
            }
        }

        private static EncounterCatalogManifest CreateManifest(int revision, string variantId,
            bool none = false, bool omitFutureBinding = false)
        {
            var bindings = new System.Collections.Generic.List<EncounterCatalogBinding>();
            var descriptors = new System.Collections.Generic.List<EncounterCatalogDescriptor>();
            var definitions = new System.Collections.Generic.List<EncounterCatalogDefinition>();
            if (none)
            {
                Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(
                    ExpeditionSampleRouteDefinition.RequiredGenerationVersion, "room.patio.c1",
                    ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.None, null, 0,
                    out EncounterCatalogBinding noneBinding, out EncounterCatalogError noneError), Is.True,
                    noneError.Message);
                bindings.Add(noneBinding);
            }
            else
            {
                Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor("room.projection.synthetic", 1,
                    EncounterCatalogDescriptorKind.Room, new string('c', 64),
                    out EncounterCatalogDescriptor room, out EncounterCatalogError roomError), Is.True, roomError.Message);
                Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor("enemy.projection.synthetic", 1,
                    EncounterCatalogDescriptorKind.Enemy, new string('d', 64),
                    out EncounterCatalogDescriptor enemy, out EncounterCatalogError enemyError), Is.True, enemyError.Message);
                descriptors.Add(room);
                descriptors.Add(enemy);
                Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("room.projection.synthetic", 1,
                    out EncounterCatalogDescriptorRef roomRef, out roomError), Is.True, roomError.Message);
                Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("enemy.projection.synthetic", 1,
                    out EncounterCatalogDescriptorRef enemyRef, out enemyError), Is.True, enemyError.Message);
                Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("spawn.projection.synthetic",
                    "enemy.projection.synthetic", enemyRef, "anchor.projection.synthetic",
                    ExpeditionEncounterRewardEnemyKind.Normal, 0,
                    out EncounterCatalogSpawn spawn, out EncounterCatalogError spawnError), Is.True, spawnError.Message);
                Assert.That(EncounterCatalogFactoryV1.TryCreateWave("wave.projection.synthetic", new[] { spawn },
                    out EncounterCatalogWave wave, out EncounterCatalogError waveError), Is.True, waveError.Message);
                Assert.That(EncounterCatalogFactoryV1.TryCreateVariant(variantId,
                    Array.Empty<string>(), Array.Empty<string>(), 0, roomRef, new[] { wave }, null,
                    out EncounterCatalogVariant variant, out EncounterCatalogError variantError), Is.True, variantError.Message);
                Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition(EncounterId, 1,
                    new[] { variant }, out EncounterCatalogDefinition definition,
                    out EncounterCatalogError definitionError), Is.True, definitionError.Message);
                definitions.Add(definition);
                Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(
                    ExpeditionSampleRouteDefinition.RequiredGenerationVersion, "room.patio.c1",
                    ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.Encounter,
                    EncounterId, 1, out EncounterCatalogBinding encounterBinding,
                    out EncounterCatalogError bindingError), Is.True, bindingError.Message);
                bindings.Add(encounterBinding);
            }

            foreach (var row in new[] { new { Template = "room.sample.tutorial", Kind = ExpeditionRouteNodeKind.Tutorial }, new { Template = "room.patio.e1", Kind = ExpeditionRouteNodeKind.Exploration }, new { Template = "room.patio.guardian", Kind = ExpeditionRouteNodeKind.Guardian }, new { Template = "room.sample.extraction", Kind = ExpeditionRouteNodeKind.SafeExtraction } })
            {
                if (omitFutureBinding && row.Template == "room.patio.e1") continue;
                Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(ExpeditionSampleRouteDefinition.RequiredGenerationVersion, row.Template, row.Kind, EncounterCatalogBindingMode.None, null, 0, out EncounterCatalogBinding binding, out EncounterCatalogError error), Is.True, error.Message);
                bindings.Add(binding);
            }

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, CatalogId, revision,
                "encounter-spawn.v1", EncounterPlanResolverV1.SupportedResolverId,
                EncounterPlanResolverV1.SupportedResolverRevision,
                new[] { ExpeditionSampleRouteDefinition.RequiredGenerationVersion },
                descriptors, bindings, definitions, out EncounterCatalogManifest manifest,
                out EncounterCatalogError manifestError), Is.True, manifestError.Message);
            return manifest;
        }

        private static ExpeditionRunConfiguration CreateRun(uint seed = 0x61f09u)
        {
            const string tutorial = "sample.tutorial";
            const string combat = "sample.patio.combat";
            const string exploration = "sample.patio.exploration";
            const string guardian = "sample.patio.guardian";
            const string extraction = "sample.safe_extraction";
            int generation = ExpeditionSampleRouteDefinition.RequiredGenerationVersion;
            ExpeditionSampleRouteNodeDefinition[] nodes =
            {
                Node(tutorial, "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial, 0),
                Node(combat, "room.patio.c1", ExpeditionRouteNodeKind.Combat, 1),
                Node(exploration, "room.patio.e1", ExpeditionRouteNodeKind.Exploration, 2),
                Node(guardian, "room.patio.guardian", ExpeditionRouteNodeKind.Guardian, 3),
                Node(extraction, "room.sample.extraction", ExpeditionRouteNodeKind.SafeExtraction, 4)
            };
            string[] mandatory = { tutorial, combat, exploration, guardian, extraction };
            var connections = new ExpeditionSampleRouteConnectionDefinition[mandatory.Length - 1];
            for (int index = 0; index < connections.Length; index++)
                connections[index] = new ExpeditionSampleRouteConnectionDefinition(mandatory[index],
                    ExpeditionSampleRouteDefinition.ExitPortId, mandatory[index + 1],
                    ExpeditionSampleRouteDefinition.EntryPortId, false);
            var routeDefinition = new ExpeditionSampleRouteDefinition(
                ExpeditionSampleRouteDefinition.RequiredSchemaVersion, generation, nodes, connections, mandatory);
            ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(seed, routeDefinition);

            // Match EncounterPlanResolutionV1Tests.CreateRun: the ItemCatalog has six materials.
            ItemDefinition[] materials = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select((kind, index) => new ItemDefinition(
                    "material.pin_projection_fixture_" + index.ToString(CultureInfo.InvariantCulture),
                    "item.fixture.name", "item.fixture.description", ItemKind.Material, 99,
                    materialKind: kind))
                .ToArray();
            var inventory = new ExpeditionInventory(new ItemCatalog(materials), "class.bellator");
            var runId = new RunId(Guid.ParseExact("0123456789abcdef0123456789abcdef", "N"));
            return new ExpeditionRunConfiguration(runId, "class.bellator",
                ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.fixture.one", "skill.fixture.two" }, seed, route, inventory, new[] { ItemInstanceId.Parse("11111111111111111111111111111111"), ItemInstanceId.Parse("22222222222222222222222222222222") });
        }

        private static ExpeditionSampleRouteNodeDefinition Node(string nodeId, string templateId,
            ExpeditionRouteNodeKind kind, int pathIndex)
        {
            return new ExpeditionSampleRouteNodeDefinition(nodeId, "patio", templateId, kind, true,
                pathIndex, ExpeditionSampleRouteDefinition.EntryPortId,
                ExpeditionSampleRouteDefinition.ExitPortId);
        }
// Integration target: paste this fragment inside the body of
// SaveV7AtomicRepositoryTests. It uses that fixture's Wait<T>() helper.
// No using additions are required; UnityEngine and SHA256 are fully qualified.

        [TestCase(1, false)] [TestCase(1, true)]
        [TestCase(2, false)] [TestCase(2, true)]
        [TestCase(3, false)] [TestCase(3, true)]
        [TestCase(4, false)] [TestCase(4, true)]
        [TestCase(5, false)] [TestCase(5, true)]
        [TestCase(6, false)] [TestCase(6, true)]
        public void V7LegacyActiveMatrixAuthenticatesAndRemainsReadOnly(int version, bool fromBackup)
        {
            string original = BuildLegacyProfile(version, active: true);
            string originalPrimary = fromBackup ? "corrupt-primary-sentinel" : original;
            string originalBackup = fromBackup ? original : "preserved-backup-sentinel";
            var store = new LegacyMatrixStore
            {
                ProfilePrimary = originalPrimary,
                ProfileBackup = originalBackup
            };
            var repository = new SaveRepository(store);

            SaveV7LoadView loaded = Wait(repository.LoadProfileV7ForPreproductionAsync());

            Assert.That(loaded.Status, Is.EqualTo(fromBackup ? SaveLoadStatus.RecoveredBackup : SaveLoadStatus.LoadedPrimary), "V" + version);
            Assert.That(loaded.SourceContentVersion, Is.EqualTo(version), "Only an authenticated header is provenance.");
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            Assert.That(loaded.Error.Code, Is.EqualTo(SaveV7ErrorCode.LegacyEncounterPinMissing));
            Assert.That(loaded.Envelope, Is.Null, "An active legacy document must never become a V7 envelope or be auto-pinned.");
            Assert.That(loaded.LegacyDiagnostic, Is.Not.Null);
            Assert.That(loaded.LegacyDiagnostic.hasActiveExpedition, Is.True);
            Assert.That(loaded.LegacyDiagnostic.activeExpedition, Is.Not.Null);
            Assert.That(loaded.OriginalJson, Is.EqualTo(original));
            Assert.That(repository.CurrentProfile, Is.Null);
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable),
                "The storage diagnostic does not grant ordinary V6 application access.");

            SaveWriteResult overwrite = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile()));
            SaveWriteResult retry = Wait(repository.RetryPendingAsync(SaveDocument.Profile));
            PinnedProfileCandidateV7 inactiveReplacement = SaveV7CandidateMapper.Prepare(
                SaveV7CandidateMapper.ToV7(ProfileSaveData.CreateNewProfile()), null, null);
            SaveWriteResult v7Replacement = Wait(repository.CommitPinnedV7ForPreproductionAsync(
                inactiveReplacement, null, null));
            Assert.That(overwrite.Succeeded, Is.False);
            Assert.That(overwrite.Message, Does.Contain("ProtocolConflict"));
            Assert.That(retry.Succeeded, Is.False);
            Assert.That(v7Replacement.Succeeded, Is.False);
            Assert.That(v7Replacement.Message, Does.Contain("LegacyEncounterPinMissing"),
                "An explicit inactive V7 candidate cannot replace an active legacy run.");
            Assert.That(store.ProfileWrites, Is.Zero);
            Assert.That(store.ProfilePrimary, Is.EqualTo(originalPrimary));
            Assert.That(store.ProfileBackup, Is.EqualTo(originalBackup));
            Assert.That(repository.ExportPendingJson(SaveDocument.Profile), Is.Null);
        }

        [TestCase(1, false)] [TestCase(1, true)]
        [TestCase(2, false)] [TestCase(2, true)]
        [TestCase(3, false)] [TestCase(3, true)]
        [TestCase(4, false)] [TestCase(4, true)]
        [TestCase(5, false)] [TestCase(5, true)]
        [TestCase(6, false)] [TestCase(6, true)]
        public void V7LegacyInactiveMatrixIsDiagnosticOnlyUntilExplicitCommit(int version, bool fromBackup)
        {
            string original = BuildLegacyProfile(version, active: false);
            string originalPrimary = fromBackup ? "corrupt-primary-sentinel" : original;
            string originalBackup = fromBackup ? original : "preserved-backup-sentinel";
            var store = new LegacyMatrixStore { ProfilePrimary = originalPrimary, ProfileBackup = originalBackup };
            var repository = new SaveRepository(store);

            SaveV7LoadView loaded = Wait(repository.LoadProfileV7ForPreproductionAsync());

            Assert.That(loaded.Status, Is.EqualTo(fromBackup ? SaveLoadStatus.RecoveredBackup : SaveLoadStatus.LoadedPrimary), "V" + version);
            Assert.That(loaded.SourceContentVersion, Is.EqualTo(version));
            Assert.That(loaded.Access, Is.EqualTo(SaveDataAccess.ReadWrite), "This is storage capability only.");
            Assert.That(loaded.Error.Code, Is.EqualTo(SaveV7ErrorCode.None));
            Assert.That(loaded.Envelope, Is.Null, "Load must not migrate in place.");
            Assert.That(loaded.LegacyDiagnostic, Is.Not.Null);
            Assert.That(loaded.LegacyDiagnostic.hasActiveExpedition, Is.False);
            Assert.That(loaded.LegacyDiagnostic.activeExpedition, Is.Null);
            Assert.That(loaded.OriginalJson, Is.EqualTo(original));
            Assert.That(repository.CurrentProfile, Is.Null);
            Assert.That(repository.ProfileAccess, Is.EqualTo(SaveDataAccess.Unavailable));

            SaveWriteResult ordinaryV6Write = Wait(repository.SaveProfileAsync(ProfileSaveData.CreateNewProfile()));
            Assert.That(ordinaryV6Write.Succeeded, Is.False, "Only the explicit V7 candidate API may migrate this profile.");
            Assert.That(store.ProfileReads, Is.EqualTo(1));
            Assert.That(store.ProfileWrites, Is.Zero);
            Assert.That(store.ProfilePrimary, Is.EqualTo(originalPrimary));
            Assert.That(store.ProfileBackup, Is.EqualTo(originalBackup));
        }

        [Test]
        public void ExplicitV6InactiveMigrationPreservesProfileArraysReceiptsAndAdvancesDurableIdentity()
        {
            // The receipt-rich profile is copied from the frozen V6 P3 capture. Only the outer
            // marker/payload are changed to a valid inactive V6 wire shape; the V6 checksum below
            // is computed independently over that complete legacy envelope.
            string original = BuildInactiveV6WithFrozenP3Profile();
            var store = new LegacyMatrixStore { ProfilePrimary = original };
            var repository = new SaveRepository(store);
            SaveV7LoadView legacy = Wait(repository.LoadProfileV7ForPreproductionAsync());

            Assert.That(legacy.SourceContentVersion, Is.EqualTo(6));
            Assert.That(legacy.Access, Is.EqualTo(SaveDataAccess.ReadWrite));
            Assert.That(legacy.Envelope, Is.Null);
            Assert.That(legacy.LegacyDiagnostic.profile.processedContractProgressEvents, Has.Length.EqualTo(3));
            Assert.That(legacy.LegacyDiagnostic.profile.progressionPurchaseTransactions, Has.Length.EqualTo(5));
            Assert.That(store.ProfileWrites, Is.Zero, "Legacy loading itself is read-only.");
            Assert.That(store.ProfilePrimary, Is.EqualTo(original));

            ProfileSaveDataV7 preserved = SaveV7CandidateMapper.ToV7(legacy.LegacyDiagnostic.profile);
            PinnedProfileCandidateV7 candidate = SaveV7CandidateMapper.Prepare(preserved, null, null);
            SaveWriteResult migrated = Wait(repository.CommitPinnedV7ForPreproductionAsync(candidate, null, null));

            Assert.That(migrated.Succeeded, Is.True, migrated.Message);
            Assert.That(store.ProfileWrites, Is.EqualTo(1));
            Assert.That(store.ProfileBackup, Is.EqualTo(original));
            SaveV7LoadView current = Wait(repository.LoadProfileV7ForPreproductionAsync());
            Assert.That(current.SourceContentVersion, Is.EqualTo(7));
            Assert.That(current.Envelope, Is.Not.Null);
            Assert.That(current.Envelope.hasActiveExpedition, Is.False);
            Assert.That(current.Envelope.generation, Is.EqualTo(2));
            Assert.That(current.Envelope.sequence, Is.EqualTo(12));
            CollectionAssert.AreEqual(preserved.unlockedRaceIds, current.Envelope.profile.unlockedRaceIds);
            CollectionAssert.AreEqual(preserved.unlockedStartingKitIds, current.Envelope.profile.unlockedStartingKitIds);
            CollectionAssert.AreEqual(preserved.acceptedContractIds, current.Envelope.profile.acceptedContractIds);
            CollectionAssert.AreEqual(preserved.claimedContractIds, current.Envelope.profile.claimedContractIds);
            CollectionAssert.AreEqual(
                preserved.contractCounters.Select(x => x.contractId).ToArray(),
                current.Envelope.profile.contractCounters.Select(x => x.contractId).ToArray());
            CollectionAssert.AreEqual(preserved.processedSettlementRunIds, current.Envelope.profile.processedSettlementRunIds);
            CollectionAssert.AreEqual(
                preserved.processedContractProgressEvents.Select(x => x.eventId).ToArray(),
                current.Envelope.profile.processedContractProgressEvents.Select(x => x.eventId).ToArray());
            CollectionAssert.AreEqual(
                preserved.progressionPurchaseTransactions.Select(x => x.transactionId).ToArray(),
                current.Envelope.profile.progressionPurchaseTransactions.Select(x => x.transactionId).ToArray());
        }

        [Test]
        public void V7ProfileLatchLeavesFrozenV6SettingsOnTheIndependentOperationalPath()
        {
            string originalProfile = BuildLegacyProfile(6, active: true);
            string originalSettings = ReadV6SettingsFixture();
            var store = new LegacyMatrixStore { ProfilePrimary = originalProfile, SettingsPrimary = originalSettings };
            var repository = new SaveRepository(store);

            SaveV7LoadView profile = Wait(repository.LoadProfileV7ForPreproductionAsync());
            Assert.That(profile.Access, Is.EqualTo(SaveDataAccess.ReadOnlyLegacyEncounterPinMissing));
            SaveWriteResult settingsWrite = Wait(repository.SaveSettingsAsync(new GameSettingsSaveData { languageId = "es" }));

            Assert.That(settingsWrite.Succeeded, Is.True, settingsWrite.Message);
            Assert.That(store.ProfileReads, Is.EqualTo(1));
            Assert.That(store.ProfileWrites, Is.Zero);
            Assert.That(store.ProfilePrimary, Is.EqualTo(originalProfile));
            Assert.That(store.SettingsReads, Is.EqualTo(1));
            Assert.That(store.SettingsWrites, Is.EqualTo(1));
            Assert.That(store.SettingsBackup, Is.EqualTo(originalSettings));
            SettingsSaveEnvelope settings = UnityEngine.JsonUtility.FromJson<SettingsSaveEnvelope>(store.SettingsPrimary);
            Assert.That(settings.contentVersion, Is.EqualTo(SaveFormat.ContentVersion));
            Assert.That(settings.sequence, Is.EqualTo(12));
        }

        private static string BuildLegacyProfile(int version, bool active)
        {
            if (version == 5 || version == 6) return ReadFrozenProfileFixture(version, active);
            if (version < 1 || version > 4) throw new ArgumentOutOfRangeException(nameof(version));

            ProfileSaveData profile = ProfileSaveData.CreateNewProfile();
            ExpeditionSaveData expedition = active ? ReadFrozenV5ActiveSource() : null;
            const int generation = 4;
            const long sequence = 17;
            const string savedAt = "2000-01-01T00:00:00.0000000Z";

            if (version == 1)
            {
                var envelope = new LegacyMatrixProfileEnvelopeV1
                {
                    schemaVersion = SaveFormat.SchemaVersion, contentVersion = version,
                    generation = generation, sequence = sequence, savedAtUtc = savedAt,
                    checksum = string.Empty, profile = ToLegacyProfileV1(profile),
                    hasActiveExpedition = active,
                    activeExpedition = active ? ToLegacyExpeditionV2(expedition) : null
                };
                envelope.checksum = LegacyMatrixChecksum(UnityEngine.JsonUtility.ToJson(envelope, false));
                return UnityEngine.JsonUtility.ToJson(envelope, true);
            }
            if (version == 2)
            {
                var envelope = new LegacyMatrixProfileEnvelopeV2
                {
                    schemaVersion = SaveFormat.SchemaVersion, contentVersion = version,
                    generation = generation, sequence = sequence, savedAtUtc = savedAt,
                    checksum = string.Empty, profile = ToLegacyProfileV2(profile),
                    hasActiveExpedition = active,
                    activeExpedition = active ? ToLegacyExpeditionV2(expedition) : null
                };
                envelope.checksum = LegacyMatrixChecksum(UnityEngine.JsonUtility.ToJson(envelope, false));
                return UnityEngine.JsonUtility.ToJson(envelope, true);
            }

            var v4Envelope = new LegacyMatrixProfileEnvelopeV4
            {
                schemaVersion = SaveFormat.SchemaVersion, contentVersion = version,
                generation = generation, sequence = sequence, savedAtUtc = savedAt,
                checksum = string.Empty, profile = ToLegacyProfileV5(profile),
                hasActiveExpedition = active,
                activeExpedition = active ? ToLegacyExpeditionV4(expedition) : null
            };
            v4Envelope.checksum = LegacyMatrixChecksum(UnityEngine.JsonUtility.ToJson(v4Envelope, false));
            return UnityEngine.JsonUtility.ToJson(v4Envelope, true);
        }

        private static LegacyMatrixProfileDataV1 ToLegacyProfileV1(ProfileSaveData source)
        {
            return new LegacyMatrixProfileDataV1
            {
                availableRenown = source.availableRenown, healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel, additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = source.unlockedRaceIds, unlockedStartingKitIds = source.unlockedStartingKitIds,
                acceptedContractIds = source.acceptedContractIds, claimedContractIds = source.claimedContractIds,
                readStoryFragmentIds = source.readStoryFragmentIds, contractCounters = source.contractCounters,
                processedSettlementRunIds = source.processedSettlementRunIds
            };
        }

        private static LegacyMatrixProfileDataV2 ToLegacyProfileV2(ProfileSaveData source)
        {
            return new LegacyMatrixProfileDataV2
            {
                availableRenown = source.availableRenown, healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel, additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = source.unlockedRaceIds, unlockedStartingKitIds = source.unlockedStartingKitIds,
                acceptedContractIds = source.acceptedContractIds, claimedContractIds = source.claimedContractIds,
                readStoryFragmentIds = source.readStoryFragmentIds, contractCounters = source.contractCounters,
                processedSettlementRunIds = source.processedSettlementRunIds,
                progressionPurchaseTransactions = source.progressionPurchaseTransactions
            };
        }

        private static LegacyMatrixProfileDataV5 ToLegacyProfileV5(ProfileSaveData source)
        {
            return new LegacyMatrixProfileDataV5
            {
                availableRenown = source.availableRenown, healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel, additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = source.unlockedRaceIds, unlockedStartingKitIds = source.unlockedStartingKitIds,
                acceptedContractIds = source.acceptedContractIds, claimedContractIds = source.claimedContractIds,
                readStoryFragmentIds = source.readStoryFragmentIds, contractCounters = source.contractCounters,
                processedSettlementRunIds = source.processedSettlementRunIds,
                progressionPurchaseTransactions = source.progressionPurchaseTransactions
            };
        }

        private static ExpeditionSaveData ReadFrozenV5ActiveSource()
        {
            string json = ReadFrozenProfileFixture(5, active: true);
            ProfileSaveEnvelope source = UnityEngine.JsonUtility.FromJson<ProfileSaveEnvelope>(json);
            if (source == null || source.contentVersion != 5 || !source.hasActiveExpedition || source.activeExpedition == null)
                throw new InvalidOperationException("The frozen V5 active source fixture has an unexpected shape.");
            if (SaveJsonCodec.Decode(json, out ProfileSaveEnvelope ignored, out int sourceVersion) != DecodeStatus.Valid || sourceVersion != 5)
                throw new InvalidOperationException("The frozen V5 source fixture failed its independent historical reader.");
            return source.activeExpedition;
        }

        private static LegacyMatrixExpeditionDataV2 ToLegacyExpeditionV2(ExpeditionSaveData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            FortuneOfferBookSnapshot book = source.fortuneOfferBookSnapshot;
            if (book != null && ((book.offers != null && book.offers.Length != 0) ||
                (book.processedGuardianIds != null && book.processedGuardianIds.Length != 0) ||
                (book.rerollReceipts != null && book.rerollReceipts.Length != 0) ||
                (book.fortuneBrewReceipts != null && book.fortuneBrewReceipts.Length != 0)))
                throw new InvalidOperationException("The V2 wire shape cannot represent the frozen V5 offer-book payload.");
            int charges = book == null ? 0 : book.charges;
            return new LegacyMatrixExpeditionDataV2
            {
                runId = source.runId, seed = source.seed, generationVersion = source.generationVersion,
                contentVersion = SaveFormat.ContentVersion, classId = source.classId, raceId = source.raceId,
                selectedSkillIds = source.selectedSkillIds, startingEquipmentInstanceIds = source.startingEquipmentInstanceIds,
                routeSnapshotJson = source.routeSnapshotJson, currentNodeId = source.currentNodeId,
                visitedNodeIds = source.visitedNodeIds, completedRoomIds = source.completedRoomIds,
                completedRoomResults = source.completedRoomResults,
                roomEntryCheckpointJson = ToLegacyCheckpointV2(source.roomEntryCheckpointJson, charges),
                inventorySnapshotJson = source.inventorySnapshotJson, maximumHealth = source.maximumHealth,
                currentHealth = source.currentHealth, maximumEnergy = source.maximumEnergy,
                currentEnergy = source.currentEnergy, healthPotionCharges = source.healthPotionCharges,
                accumulatedRenown = source.accumulatedRenown, pactIds = source.pactIds, fortunaCharges = charges,
                pendingOfferIds = Array.Empty<string>(), offerRerollCounts = Array.Empty<int>(),
                collectedItemIds = source.collectedItemIds, claimedRewardIds = source.claimedRewardIds
            };
        }

        private static string ToLegacyCheckpointV2(string json, int charges)
        {
            if (string.IsNullOrWhiteSpace(json)) return string.Empty;
            ExpeditionRoomCheckpointSaveData source = UnityEngine.JsonUtility.FromJson<ExpeditionRoomCheckpointSaveData>(json);
            if (source == null) throw new InvalidOperationException("The V5 checkpoint could not be read as the current typed checkpoint.");
            return UnityEngine.JsonUtility.ToJson(new LegacyMatrixCheckpointV2
            {
                roomNodeId = source.roomNodeId, inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth, currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy, currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges, accumulatedRenown = source.accumulatedRenown,
                fortunaCharges = charges, pactIds = source.pactIds, pendingOfferIds = Array.Empty<string>(),
                offerRerollCounts = Array.Empty<int>(), collectedItemIds = source.collectedItemIds,
                claimedRewardIds = source.claimedRewardIds
            });
        }

        private static LegacyMatrixExpeditionDataV4 ToLegacyExpeditionV4(ExpeditionSaveData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            FortuneOfferBookSnapshot book = source.fortuneOfferBookSnapshot;
            return new LegacyMatrixExpeditionDataV4
            {
                runId = source.runId, seed = source.seed, generationVersion = source.generationVersion,
                contentVersion = SaveFormat.ContentVersion, classId = source.classId, raceId = source.raceId,
                selectedSkillIds = source.selectedSkillIds, startingEquipmentInstanceIds = source.startingEquipmentInstanceIds,
                routeSnapshotJson = source.routeSnapshotJson, currentNodeId = source.currentNodeId,
                visitedNodeIds = source.visitedNodeIds, completedRoomIds = source.completedRoomIds,
                completedRoomResults = source.completedRoomResults,
                roomEntryCheckpointJson = source.roomEntryCheckpointJson, inventorySnapshotJson = source.inventorySnapshotJson,
                maximumHealth = source.maximumHealth, currentHealth = source.currentHealth,
                maximumEnergy = source.maximumEnergy, currentEnergy = source.currentEnergy,
                healthPotionCharges = source.healthPotionCharges, accumulatedRenown = source.accumulatedRenown,
                pactIds = source.pactIds, fortuneOfferBookSnapshot = book == null ? null : new LegacyMatrixFortuneSnapshotV4
                {
                    snapshotVersion = book.snapshotVersion, generationVersion = book.generationVersion,
                    seed = book.seed, raceId = book.raceId, charges = book.charges, offers = book.offers,
                    processedGuardianIds = book.processedGuardianIds, rerollReceipts = book.rerollReceipts
                },
                collectedItemIds = source.collectedItemIds, claimedRewardIds = source.claimedRewardIds
            };
        }

        private static string BuildInactiveV6WithFrozenP3Profile()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V6",
                "V6-P3-receipts-full.Frozen.envelope.json");
            string captured = File.ReadAllText(path, Encoding.UTF8);
            ProfileSaveEnvelope source = UnityEngine.JsonUtility.FromJson<ProfileSaveEnvelope>(captured);
            if (source == null || source.contentVersion != 6 || source.profile == null ||
                source.profile.processedContractProgressEvents == null || source.profile.processedContractProgressEvents.Length != 3 ||
                source.profile.progressionPurchaseTransactions == null || source.profile.progressionPurchaseTransactions.Length != 5)
                throw new InvalidOperationException("Frozen V6 P3 profile no longer has the receipt-rich shape required by this migration test.");
            var envelope = new LegacyMatrixProfileEnvelopeV6
            {
                schemaVersion = source.schemaVersion, contentVersion = 6, generation = source.generation,
                sequence = source.sequence, savedAtUtc = source.savedAtUtc, checksum = string.Empty,
                profile = ToLegacyProfileV6(source.profile), hasActiveExpedition = false, activeExpedition = null
            };
            envelope.checksum = LegacyMatrixChecksum(UnityEngine.JsonUtility.ToJson(envelope, false));
            return UnityEngine.JsonUtility.ToJson(envelope, true);
        }

        private static LegacyMatrixProfileDataV6 ToLegacyProfileV6(ProfileSaveData source)
        {
            return new LegacyMatrixProfileDataV6
            {
                availableRenown = source.availableRenown, healthUpgradeLevel = source.healthUpgradeLevel,
                energyUpgradeLevel = source.energyUpgradeLevel, additionalPotionUpgradeLevel = source.additionalPotionUpgradeLevel,
                unlockedRaceIds = source.unlockedRaceIds, unlockedStartingKitIds = source.unlockedStartingKitIds,
                acceptedContractIds = source.acceptedContractIds, claimedContractIds = source.claimedContractIds,
                readStoryFragmentIds = source.readStoryFragmentIds,
                processedSettlementRunIds = source.processedSettlementRunIds,
                contractCounters = source.contractCounters == null ? null : source.contractCounters.Select(x => x == null ? null :
                    new LegacyMatrixContractCounterV6 { contractId = x.contractId, progress = x.progress }).ToArray(),
                processedContractProgressEvents = source.processedContractProgressEvents == null ? null :
                    source.processedContractProgressEvents.Select(x => x == null ? null :
                        new LegacyMatrixContractProgressEventV6 { eventId = x.eventId, progressEvent = x.progressEvent }).ToArray(),
                progressionPurchaseTransactions = source.progressionPurchaseTransactions == null ? null :
                    source.progressionPurchaseTransactions.Select(x => x == null ? null :
                        new LegacyMatrixPermanentPurchaseV6 { transactionId = x.transactionId, kind = x.kind, cost = x.cost }).ToArray()
            };
        }

        private static string ReadFrozenProfileFixture(int version, bool active)
        {
            string path;
            if (version == 5)
            {
                path = Path.Combine(UnityEngine.Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V5",
                    "capture", "cultures", "es-ES", active ? "P1-known-nontrivial" : "P0-no-active", "envelope.json");
            }
            else if (version == 6)
            {
                path = Path.Combine(UnityEngine.Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V6",
                    active ? "V6-P1-active-known-nontrivial.Frozen.envelope.json" : "V6-P0-profile-no-active.Frozen.envelope.json");
            }
            else throw new ArgumentOutOfRangeException(nameof(version));

            string json = File.ReadAllText(path, Encoding.UTF8);
            ProfileSaveEnvelope envelope = UnityEngine.JsonUtility.FromJson<ProfileSaveEnvelope>(json);
            if (envelope == null || envelope.schemaVersion != SaveFormat.SchemaVersion || envelope.contentVersion != version ||
                envelope.hasActiveExpedition != active || (active && envelope.activeExpedition == null))
                throw new InvalidOperationException("Frozen V" + version + " fixture has an unexpected profile header or activity marker.");
            return json;
        }

        private static string ReadV6SettingsFixture()
        {
            string path = Path.Combine(UnityEngine.Application.dataPath, "Luckbound", "Tests", "Fixtures", "Saves", "V6",
                "V6-S0-settings.Frozen.envelope.json");
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static string LegacyMatrixChecksum(string compactHistoricalJson)
        {
            using (System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(compactHistoricalJson)))
                    .Replace("-", string.Empty).ToLowerInvariant();
        }

        [Serializable]
        private sealed class LegacyMatrixProfileEnvelopeV1
        {
            public int schemaVersion; public int contentVersion; public int generation; public long sequence;
            public string savedAtUtc; public string checksum; public LegacyMatrixProfileDataV1 profile;
            public bool hasActiveExpedition; public LegacyMatrixExpeditionDataV2 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyMatrixProfileEnvelopeV2
        {
            public int schemaVersion; public int contentVersion; public int generation; public long sequence;
            public string savedAtUtc; public string checksum; public LegacyMatrixProfileDataV2 profile;
            public bool hasActiveExpedition; public LegacyMatrixExpeditionDataV2 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyMatrixProfileEnvelopeV4
        {
            public int schemaVersion; public int contentVersion; public int generation; public long sequence;
            public string savedAtUtc; public string checksum; public LegacyMatrixProfileDataV5 profile;
            public bool hasActiveExpedition; public LegacyMatrixExpeditionDataV4 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyMatrixProfileDataV1
        {
            public int availableRenown; public int healthUpgradeLevel; public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel; public string[] unlockedRaceIds; public string[] unlockedStartingKitIds;
            public string[] acceptedContractIds; public string[] claimedContractIds; public string[] readStoryFragmentIds;
            public ContractCounterSaveData[] contractCounters; public string[] processedSettlementRunIds;
        }

        [Serializable]
        private sealed class LegacyMatrixProfileDataV2
        {
            public int availableRenown; public int healthUpgradeLevel; public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel; public string[] unlockedRaceIds; public string[] unlockedStartingKitIds;
            public string[] acceptedContractIds; public string[] claimedContractIds; public string[] readStoryFragmentIds;
            public ContractCounterSaveData[] contractCounters; public string[] processedSettlementRunIds;
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions;
        }

        [Serializable]
        private sealed class LegacyMatrixProfileDataV5
        {
            public int availableRenown; public int healthUpgradeLevel; public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel; public string[] unlockedRaceIds; public string[] unlockedStartingKitIds;
            public string[] acceptedContractIds; public string[] claimedContractIds; public string[] readStoryFragmentIds;
            public ContractCounterSaveData[] contractCounters; public string[] processedSettlementRunIds;
            public PermanentUpgradePurchaseSaveData[] progressionPurchaseTransactions;
        }

        [Serializable]
        private sealed class LegacyMatrixExpeditionDataV2
        {
            public string runId; public uint seed; public int generationVersion; public int contentVersion;
            public string classId; public string raceId; public string[] selectedSkillIds = Array.Empty<string>();
            public string[] startingEquipmentInstanceIds = Array.Empty<string>(); public string routeSnapshotJson;
            public string currentNodeId; public string[] visitedNodeIds = Array.Empty<string>();
            public string[] completedRoomIds = Array.Empty<string>();
            public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
            public string roomEntryCheckpointJson; public string inventorySnapshotJson; public int maximumHealth = 100;
            public int currentHealth = 100; public int maximumEnergy = 50; public int currentEnergy = 50;
            public int healthPotionCharges = 2; public int accumulatedRenown; public string[] pactIds = Array.Empty<string>();
            public int fortunaCharges; public string[] pendingOfferIds = Array.Empty<string>();
            public int[] offerRerollCounts = Array.Empty<int>(); public string[] collectedItemIds = Array.Empty<string>();
            public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyMatrixExpeditionDataV4
        {
            public string runId; public uint seed; public int generationVersion; public int contentVersion;
            public string classId; public string raceId; public string[] selectedSkillIds = Array.Empty<string>();
            public string[] startingEquipmentInstanceIds = Array.Empty<string>(); public string routeSnapshotJson;
            public string currentNodeId; public string[] visitedNodeIds = Array.Empty<string>();
            public string[] completedRoomIds = Array.Empty<string>();
            public ExpeditionRoomCompletionSaveData[] completedRoomResults = Array.Empty<ExpeditionRoomCompletionSaveData>();
            public string roomEntryCheckpointJson; public string inventorySnapshotJson; public int maximumHealth = 100;
            public int currentHealth = 100; public int maximumEnergy = 50; public int currentEnergy = 50;
            public int healthPotionCharges = 2; public int accumulatedRenown; public string[] pactIds = Array.Empty<string>();
            public LegacyMatrixFortuneSnapshotV4 fortuneOfferBookSnapshot;
            public string[] collectedItemIds = Array.Empty<string>(); public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyMatrixFortuneSnapshotV4
        {
            public int snapshotVersion; public int generationVersion; public uint seed; public string raceId;
            public int charges; public FortuneOfferSnapshot[] offers = Array.Empty<FortuneOfferSnapshot>();
            public string[] processedGuardianIds = Array.Empty<string>();
            public FortuneRerollReceiptSnapshot[] rerollReceipts = Array.Empty<FortuneRerollReceiptSnapshot>();
        }

        [Serializable]
        private sealed class LegacyMatrixCheckpointV2
        {
            public string roomNodeId; public string inventorySnapshotJson; public int maximumHealth;
            public int currentHealth; public int maximumEnergy; public int currentEnergy; public int healthPotionCharges;
            public int accumulatedRenown; public int fortunaCharges; public string[] pactIds = Array.Empty<string>();
            public string[] pendingOfferIds = Array.Empty<string>(); public int[] offerRerollCounts = Array.Empty<int>();
            public string[] collectedItemIds = Array.Empty<string>(); public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyMatrixProfileEnvelopeV6
        {
            public int schemaVersion; public int contentVersion; public int generation; public long sequence;
            public string savedAtUtc; public string checksum; public LegacyMatrixProfileDataV6 profile;
            // Only the independently generated inactive V6 variant uses this null payload.
            public bool hasActiveExpedition; public LegacyMatrixExpeditionDataV6 activeExpedition;
        }

        [Serializable]
        private sealed class LegacyMatrixProfileDataV6
        {
            public int availableRenown; public int healthUpgradeLevel; public int energyUpgradeLevel;
            public int additionalPotionUpgradeLevel; public string[] unlockedRaceIds = Array.Empty<string>();
            public string[] unlockedStartingKitIds = Array.Empty<string>(); public string[] acceptedContractIds = Array.Empty<string>();
            public string[] claimedContractIds = Array.Empty<string>(); public string[] readStoryFragmentIds = Array.Empty<string>();
            public LegacyMatrixContractCounterV6[] contractCounters = Array.Empty<LegacyMatrixContractCounterV6>();
            public string[] processedSettlementRunIds = Array.Empty<string>();
            public LegacyMatrixContractProgressEventV6[] processedContractProgressEvents = Array.Empty<LegacyMatrixContractProgressEventV6>();
            public LegacyMatrixPermanentPurchaseV6[] progressionPurchaseTransactions = Array.Empty<LegacyMatrixPermanentPurchaseV6>();
        }

        [Serializable]
        private sealed class LegacyMatrixContractCounterV6
        {
            public string contractId; public int progress;
        }

        [Serializable]
        private sealed class LegacyMatrixContractProgressEventV6
        {
            public string eventId; public int progressEvent;
        }

        [Serializable]
        private sealed class LegacyMatrixPermanentPurchaseV6
        {
            public string transactionId; public int kind; public int cost;
        }

        [Serializable]
        private sealed class LegacyMatrixExpeditionDataV6
        {
            public string runId; public uint seed; public int generationVersion; public int contentVersion;
            public string classId; public string raceId; public string[] selectedSkillIds = Array.Empty<string>();
            public string[] startingEquipmentInstanceIds = Array.Empty<string>(); public string routeSnapshotJson;
            public string currentNodeId; public string[] visitedNodeIds = Array.Empty<string>();
            public string[] completedRoomIds = Array.Empty<string>();
            public LegacyMatrixRoomCompletionV6[] completedRoomResults = Array.Empty<LegacyMatrixRoomCompletionV6>();
            public string roomEntryCheckpointJson; public string inventorySnapshotJson; public int maximumHealth = 100;
            public int currentHealth = 100; public int maximumEnergy = 50; public int currentEnergy = 50;
            public int healthPotionCharges = 2; public LegacyMatrixFrozenUpgradesV6 frozenPermanentUpgrades;
            public bool hasFrozenPermanentUpgrades; public int accumulatedRenown; public string[] pactIds = Array.Empty<string>();
            public LegacyMatrixFortuneSnapshotV6 fortuneOfferBookSnapshot;
            public string[] collectedItemIds = Array.Empty<string>(); public string[] claimedRewardIds = Array.Empty<string>();
        }

        [Serializable]
        private sealed class LegacyMatrixRoomCompletionV6
        {
            public string roomNodeId; public string resultId; public string resultJson;
        }

        [Serializable]
        private sealed class LegacyMatrixFrozenUpgradesV6
        {
            public int healthUpgradeLevel; public int energyUpgradeLevel; public int additionalPotionUpgradeLevel;
        }

        [Serializable]
        private sealed class LegacyMatrixFortuneSnapshotV6
        {
            public int snapshotVersion; public int generationVersion; public uint seed; public string raceId;
            public int charges; public FortuneOfferSnapshot[] offers = Array.Empty<FortuneOfferSnapshot>();
            public string[] processedGuardianIds = Array.Empty<string>();
            public FortuneRerollReceiptSnapshot[] rerollReceipts = Array.Empty<FortuneRerollReceiptSnapshot>();
            public FortuneBrewReceiptSnapshot[] fortuneBrewReceipts = Array.Empty<FortuneBrewReceiptSnapshot>();
        }

        private sealed class LegacyMatrixStore : ISaveStore
        {
            internal string ProfilePrimary;
            internal string ProfileBackup;
            internal string SettingsPrimary;
            internal string SettingsBackup;
            internal int ProfileReads;
            internal int SettingsReads;
            internal int ProfileWrites;
            internal int SettingsWrites;

            public Task<SaveStorageSnapshot> ReadAsync(SaveDocument document)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileReads++;
                    return Task.FromResult(new SaveStorageSnapshot(ProfilePrimary, ProfileBackup));
                }
                SettingsReads++;
                return Task.FromResult(new SaveStorageSnapshot(SettingsPrimary, SettingsBackup));
            }

            public Task WriteAsync(SaveDocument document, string json, string lastKnownGoodPrimaryJson)
            {
                if (document == SaveDocument.Profile)
                {
                    ProfileWrites++;
                    if (lastKnownGoodPrimaryJson != null) ProfileBackup = lastKnownGoodPrimaryJson;
                    ProfilePrimary = json;
                }
                else
                {
                    SettingsWrites++;
                    if (lastKnownGoodPrimaryJson != null) SettingsBackup = lastKnownGoodPrimaryJson;
                    SettingsPrimary = json;
                }
                return Task.CompletedTask;
            }
        }

    }
}
#endif
