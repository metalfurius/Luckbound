using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Content.Route
{
    public sealed class EncounterPlanPinRestoreV1Tests
    {
        private const string CatalogId = "catalog.pin.restore.synthetic";
        private const string CombatNodeId = "sample.patio.combat";
        private const string ExplorationNodeId = "sample.patio.exploration";
        private const string EncounterId = "encounter.pin.synthetic";

        [Test]
        public void DecodeAndRestoreUsesExactOldRevisionWhenNewerRevisionIsAlsoInstalled()
        {
            EncounterCatalogManifest revisionOne = CreateManifest(1, "variant.r1");
            EncounterCatalogManifest revisionTwo = CreateManifest(2, "variant.r2");
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { revisionTwo, revisionOne },
                out EncounterCatalogRegistryV1 registry, out EncounterCatalogError createError), Is.True,
                createError.Message);

            ExpeditionRunConfiguration run = CreateRun();
            ResolvedEncounterPlanV1 oldPlan = Resolve(revisionOne, run, CombatNodeId, "pact.fervor");
            ResolvedEncounterPlanV1 newerPlan = Resolve(revisionTwo, run, CombatNodeId, "pact.fervor");
            Assert.That(oldPlan.VariantId, Is.EqualTo("variant.r1"));
            Assert.That(newerPlan.VariantId, Is.EqualTo("variant.r2"));
            Assert.That(oldPlan.CatalogDigest, Is.Not.EqualTo(newerPlan.CatalogDigest));

            EncounterPlanPinV1 captured = EncounterPlanPinV1.Capture(revisionOne, run,
                new[] { oldPlan }, CombatNodeId);
            byte[] wire = EncounterPlanPinCanonicalV1.Encode(captured);
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(wire,
                out EncounterPlanPinV1 decoded, out EncounterPlanPinErrorV1 decodeError), Is.True,
                decodeError.Message);
            Assert.That(EncounterPlanPinRestorerV1.TryRestore(decoded, run, CombatNodeId, registry,
                out ResolvedEncounterPlanV1 restored, out EncounterPlanPinErrorV1 restoreError), Is.True,
                restoreError.Message);

            Assert.That(restored.VariantId, Is.EqualTo("variant.r1"));
            Assert.That(restored.CatalogDigest, Is.EqualTo(oldPlan.CatalogDigest));
            Assert.That(restored.DefinitionDigest, Is.EqualTo(oldPlan.DefinitionDigest));
            Assert.That(ResolvedEncounterPlanCanonicalV1.ComputeSha256(restored),
                Is.EqualTo(captured.Entry.ResolvedPlanSha256));

            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { revisionTwo },
                out EncounterCatalogRegistryV1 withoutOldRevision, out createError), Is.True, createError.Message);
            Assert.That(EncounterPlanPinRestorerV1.TryRestore(decoded, run, CombatNodeId, withoutOldRevision,
                out ResolvedEncounterPlanV1 missingPlan, out EncounterPlanPinErrorV1 missingError), Is.False);
            Assert.That(missingPlan, Is.Null);
            Assert.That(missingError.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.CatalogRevisionUnavailable));
        }

        [Test]
        public void RestoreRejectsForeignEntryChangedContextChangedPlanHashAndChangedRoute()
        {
            EncounterCatalogManifest manifest = CreateManifest(1, "variant.r1", includeExploration: true);
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { manifest },
                out EncounterCatalogRegistryV1 registry, out EncounterCatalogError createError), Is.True,
                createError.Message);
            ExpeditionRunConfiguration run = CreateRun();
            ResolvedEncounterPlanV1 combat = Resolve(manifest, run, CombatNodeId, "pact.haste");
            ResolvedEncounterPlanV1 exploration = Resolve(manifest, run, ExplorationNodeId, "pact.haste");
            EncounterPlanPinV1 pin = EncounterPlanPinV1.Capture(manifest, run,
                new[] { combat, exploration }, CombatNodeId);

            Assert.That(EncounterPlanPinRestorerV1.TryRestore(pin, run, ExplorationNodeId, registry,
                out ResolvedEncounterPlanV1 foreignEntryPlan, out EncounterPlanPinErrorV1 foreignEntryError), Is.False);
            Assert.That(foreignEntryPlan, Is.Null);
            Assert.That(foreignEntryError.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.PlanMismatch));

            byte[] changedContext = EncounterPlanPinCanonicalV1.Encode(pin);
            ReplaceSameLengthUtf8(changedContext, "pact.haste", "pact.ember");
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(changedContext,
                out EncounterPlanPinV1 contextPin, out EncounterPlanPinErrorV1 contextDecodeError), Is.True,
                contextDecodeError.Message);
            AssertRestoreFailure(contextPin, run, CombatNodeId, registry,
                EncounterPlanPinErrorCodeV1.PlanMismatch);

            byte[] changedDigest = EncounterPlanPinCanonicalV1.Encode(pin);
            ReplaceSameLengthUtf8(changedDigest, pin.Entry.ResolvedPlanSha256, new string('0', 64));
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(changedDigest,
                out EncounterPlanPinV1 digestPin, out EncounterPlanPinErrorV1 digestDecodeError), Is.True,
                digestDecodeError.Message);
            AssertRestoreFailure(digestPin, run, CombatNodeId, registry,
                EncounterPlanPinErrorCodeV1.PlanMismatch);

            ExpeditionRunConfiguration changedRoute = CreateRun(0x10203040u);
            AssertRestoreFailure(pin, changedRoute, CombatNodeId, registry,
                EncounterPlanPinErrorCodeV1.PlanMismatch);
        }

        private static void AssertRestoreFailure(EncounterPlanPinV1 pin,
            ExpeditionRunConfiguration run, string nodeId, EncounterCatalogRegistryV1 registry,
            EncounterPlanPinErrorCodeV1 expectedCode)
        {
            Assert.That(EncounterPlanPinRestorerV1.TryRestore(pin, run, nodeId, registry,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanPinErrorV1 error), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(error.Code, Is.EqualTo(expectedCode));
            Assert.That(error.Path, Is.Not.Empty);
        }

        private static void ReplaceSameLengthUtf8(byte[] target, string oldValue, string newValue)
        {
            byte[] oldBytes = new UTF8Encoding(false, true).GetBytes(oldValue);
            byte[] newBytes = new UTF8Encoding(false, true).GetBytes(newValue);
            Assert.That(newBytes.Length, Is.EqualTo(oldBytes.Length));
            int replacements = 0;
            for (int index = 0; index <= target.Length - oldBytes.Length; index++)
            {
                bool match = true;
                for (int offset = 0; offset < oldBytes.Length; offset++)
                {
                    if (target[index + offset] == oldBytes[offset]) continue;
                    match = false;
                    break;
                }
                if (!match) continue;
                Buffer.BlockCopy(newBytes, 0, target, index, newBytes.Length);
                replacements++;
                index += oldBytes.Length - 1;
            }
            Assert.That(replacements, Is.GreaterThan(0));
        }

        private static ResolvedEncounterPlanV1 Resolve(EncounterCatalogManifest manifest,
            ExpeditionRunConfiguration run, string nodeId, params string[] pactIds)
        {
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, nodeId, pactIds,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 error), Is.True,
                error.Message);
            return plan;
        }

        private static EncounterCatalogManifest CreateManifest(int revision, string variantId,
            bool includeExploration = false)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor("room.synthetic", 1,
                EncounterCatalogDescriptorKind.Room, new string('a', 64),
                out EncounterCatalogDescriptor room, out EncounterCatalogError roomError), Is.True, roomError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor("enemy.synthetic", 1,
                EncounterCatalogDescriptorKind.Enemy, new string('b', 64),
                out EncounterCatalogDescriptor enemy, out EncounterCatalogError enemyError), Is.True, enemyError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("room.synthetic", 1,
                out EncounterCatalogDescriptorRef roomRef, out roomError), Is.True, roomError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("enemy.synthetic", 1,
                out EncounterCatalogDescriptorRef enemyRef, out enemyError), Is.True, enemyError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("spawn.synthetic", "enemy.synthetic",
                enemyRef, "anchor.spawn", ExpeditionEncounterRewardEnemyKind.Normal, 0,
                out EncounterCatalogSpawn spawn, out EncounterCatalogError spawnError), Is.True, spawnError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateWave("wave.synthetic", new[] { spawn },
                out EncounterCatalogWave wave, out EncounterCatalogError waveError), Is.True, waveError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant(variantId,
                Array.Empty<string>(), Array.Empty<string>(), 0, roomRef, new[] { wave }, null,
                out EncounterCatalogVariant variant, out EncounterCatalogError variantError), Is.True, variantError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition(EncounterId, 1,
                new[] { variant }, out EncounterCatalogDefinition definition,
                out EncounterCatalogError definitionError), Is.True, definitionError.Message);

            var bindings = new System.Collections.Generic.List<EncounterCatalogBinding>
            {
                CreateBinding(ExpeditionRouteNodeKind.Combat, "room.patio.c1")
            };
            if (includeExploration)
                bindings.Add(CreateBinding(ExpeditionRouteNodeKind.Exploration, "room.patio.e1"));

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, CatalogId, revision,
                "encounter-spawn.v1", EncounterPlanResolverV1.SupportedResolverId,
                EncounterPlanResolverV1.SupportedResolverRevision,
                new[] { ExpeditionSampleRouteDefinition.RequiredGenerationVersion },
                new[] { room, enemy }, bindings, new[] { definition },
                out EncounterCatalogManifest manifest, out EncounterCatalogError manifestError), Is.True,
                manifestError.Message);
            return manifest;
        }

        private static EncounterCatalogBinding CreateBinding(ExpeditionRouteNodeKind nodeKind,
            string templateId)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(
                ExpeditionSampleRouteDefinition.RequiredGenerationVersion, templateId, nodeKind,
                EncounterCatalogBindingMode.Encounter, EncounterId, 1,
                out EncounterCatalogBinding binding, out EncounterCatalogError error), Is.True, error.Message);
            return binding;
        }

        private static ExpeditionRunConfiguration CreateRun(uint seed = 0x51f09u)
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

            // Keep the fixture aligned with EncounterPlanResolutionV1Tests.CreateRun: six materials.
            ItemDefinition[] materials = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select((kind, index) => new ItemDefinition(
                    "material.pin_restore_fixture_" + index.ToString(CultureInfo.InvariantCulture),
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
