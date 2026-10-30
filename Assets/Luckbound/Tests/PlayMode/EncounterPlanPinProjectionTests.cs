using System;
using System.Globalization;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.PlayMode
{
    public sealed class EncounterPlanPinProjectionTests
    {
        private const string CatalogId = "catalog.pin.projection.synthetic";
        private const string NodeId = "sample.patio.combat";
        private const string EncounterId = "encounter.pin.projection.synthetic";

        [Test]
        public void CaptureEncodeDecodeExactRestoreFeedsExistingEncounterProjection()
        {
            EncounterCatalogManifest revisionOne = CreateManifest(1, "variant.r1");
            EncounterCatalogManifest revisionTwo = CreateManifest(2, "variant.r2");
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { revisionOne, revisionTwo },
                out EncounterCatalogRegistryV1 registry, out EncounterCatalogError createError), Is.True,
                createError.Message);
            ExpeditionRunConfiguration run = CreateRun();
            Assert.That(EncounterPlanResolverV1.TryResolve(revisionOne, run, NodeId,
                new[] { "pact.fervor" }, out ResolvedEncounterPlanV1 sourcePlan,
                out EncounterPlanResolutionErrorV1 resolveError), Is.True, resolveError.Message);

            EncounterPlanPinV1 captured = EncounterPlanPinV1.Capture(revisionOne, run,
                new[] { sourcePlan }, NodeId);
            byte[] bytes = EncounterPlanPinCanonicalV1.Encode(captured);
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(bytes,
                out EncounterPlanPinV1 decoded, out EncounterPlanPinErrorV1 decodeError), Is.True,
                decodeError.Message);
            Assert.That(EncounterPlanPinRestorerV1.TryRestore(decoded, run, NodeId, registry,
                out ResolvedEncounterPlanV1 restored, out EncounterPlanPinErrorV1 restoreError), Is.True,
                restoreError.Message);

            ExpeditionEncounterPlanProjection projection = ExpeditionEncounterPlanProjection.Create(restored);
            Assert.That(restored.VariantId, Is.EqualTo("variant.r1"));
            Assert.That(projection.Mode, Is.EqualTo(EncounterCatalogBindingMode.Encounter));
            Assert.That(projection.PlanSha256, Is.EqualTo(captured.Entry.ResolvedPlanSha256));
            Assert.That(projection.Roster, Is.Not.Null);
            Assert.That(projection.Roster.Waves.Count, Is.EqualTo(1));
            Assert.That(projection.Roster.Waves[0].WaveNumber, Is.EqualTo(1));
            Assert.That(projection.Roster.Waves[0].SpawnKeys.Count, Is.EqualTo(1));
            Assert.That(projection.Roster.Waves[0].SpawnKeys[0], Is.EqualTo(
                restored.GetSpawnIdentity("spawn.projection.synthetic").Key));
        }

        [Test]
        public void NonePinRestoresToNullRosterAndDoesNotCreateClearState()
        {
            EncounterCatalogManifest manifest = CreateManifest(1, null, none: true);
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { manifest },
                out EncounterCatalogRegistryV1 registry, out EncounterCatalogError createError), Is.True,
                createError.Message);
            ExpeditionRunConfiguration run = CreateRun();
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, NodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 sourcePlan,
                out EncounterPlanResolutionErrorV1 resolveError), Is.True, resolveError.Message);

            EncounterPlanPinV1 captured = EncounterPlanPinV1.Capture(manifest, run,
                new[] { sourcePlan }, NodeId);
            byte[] bytes = EncounterPlanPinCanonicalV1.Encode(captured);
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(bytes,
                out EncounterPlanPinV1 decoded, out EncounterPlanPinErrorV1 decodeError), Is.True,
                decodeError.Message);
            Assert.That(EncounterPlanPinRestorerV1.TryRestore(decoded, run, NodeId, registry,
                out ResolvedEncounterPlanV1 restored, out EncounterPlanPinErrorV1 restoreError), Is.True,
                restoreError.Message);

            ExpeditionEncounterPlanProjection projection = ExpeditionEncounterPlanProjection.Create(restored);
            Assert.That(projection.Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(projection.PlanSha256, Is.EqualTo(captured.Entry.ResolvedPlanSha256));
            Assert.That(restored.SelectedVariant, Is.Null);
            Assert.That(projection.Roster, Is.Null);
        }

        private static EncounterCatalogManifest CreateManifest(int revision, string variantId,
            bool none = false)
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
                new[] { "skill.fixture.one", "skill.fixture.two" }, seed, route, inventory);
        }

        private static ExpeditionSampleRouteNodeDefinition Node(string nodeId, string templateId,
            ExpeditionRouteNodeKind kind, int pathIndex)
        {
            return new ExpeditionSampleRouteNodeDefinition(nodeId, "patio", templateId, kind, true,
                pathIndex, ExpeditionSampleRouteDefinition.EntryPortId,
                ExpeditionSampleRouteDefinition.ExitPortId);
        }
    }
}
