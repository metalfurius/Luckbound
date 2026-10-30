using System;
using System.Globalization;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Expeditions
{
    public sealed class EncounterPlanPinV1Tests
    {
        private const string NodeId = "sample.patio.combat";
        private const string CatalogId = "catalog.pin.domain.synthetic";
        private const string EncounterId = "encounter.pin.synthetic";

        [Test]
        public void CaptureEncodeDecodePreservesEncounterContextAndCanonicalBytesWithoutAliasing()
        {
            EncounterCatalogManifest manifest = CreateManifest(1);
            ExpeditionRunConfiguration run = CreateRun();
            ResolvedEncounterPlanV1 resolved = Resolve(manifest, run, NodeId, "pact.fervor");
            EncounterPlanPinV1 captured = EncounterPlanPinV1.Capture(manifest, run,
                new[] { resolved }, NodeId);

            byte[] encoded = EncounterPlanPinCanonicalV1.Encode(captured);
            byte[] expectedBytes = (byte[])encoded.Clone();
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(encoded,
                out EncounterPlanPinV1 decoded, out EncounterPlanPinErrorV1 error), Is.True, error.Message);
            Assert.That(error.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.None));
            Assert.That(decoded.SchemaVersion, Is.EqualTo(1));
            Assert.That(decoded.RunId, Is.EqualTo(run.RunId));
            Assert.That(decoded.CatalogId, Is.EqualTo(CatalogId));
            Assert.That(decoded.CatalogRevision, Is.EqualTo(1));
            Assert.That(decoded.IdentityCodec, Is.EqualTo("encounter-spawn.v1"));
            Assert.That(decoded.Bindings.Count, Is.EqualTo(1));
            Assert.That(decoded.Bindings[0].Mode, Is.EqualTo(EncounterCatalogBindingMode.Encounter));
            Assert.That(decoded.Bindings[0].ContextPactIds, Is.EqualTo(new[] { "pact.fervor" }));
            Assert.That(decoded.Bindings[0].CalendarOrdinal, Is.Zero);
            Assert.That(decoded.Bindings[0].DefinitionSha256, Is.EqualTo(resolved.DefinitionDigest));
            Assert.That(decoded.Bindings[0].ResolvedPlanSha256,
                Is.EqualTo(ResolvedEncounterPlanCanonicalV1.ComputeSha256(resolved)));
            Assert.That(decoded.Entry.NodeId, Is.EqualTo(NodeId));
            Assert.That(decoded.Entry.ResolvedPlanSha256, Is.EqualTo(decoded.Bindings[0].ResolvedPlanSha256));

            encoded[0] ^= 0xff;
            CollectionAssert.AreEqual(expectedBytes, EncounterPlanPinCanonicalV1.Encode(decoded));
            Assert.That(((System.Collections.Generic.IList<EncounterPlanNodeBindingV1>)decoded.Bindings).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.Generic.IList<EncounterPlanNodeBindingV1>)decoded.Bindings).Clear());
        }

        [Test]
        public void NoneBindingRetainsItsCanonicalPlanDigestWithoutEncounterPayload()
        {
            EncounterCatalogManifest manifest = CreateManifest(1, includeExploration: false, none: true);
            ExpeditionRunConfiguration run = CreateRun();
            ResolvedEncounterPlanV1 resolved = Resolve(manifest, run, NodeId);
            EncounterPlanPinV1 captured = EncounterPlanPinV1.Capture(manifest, run,
                new[] { resolved }, NodeId);

            Assert.That(resolved.Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(resolved.SelectedVariant, Is.Null);
            Assert.That(captured.Bindings[0].Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(captured.Bindings[0].EncounterId, Is.Null);
            Assert.That(captured.Bindings[0].DefinitionRevision, Is.Zero);
            Assert.That(captured.Bindings[0].DefinitionSha256, Is.Null);
            Assert.That(captured.Bindings[0].VariantId, Is.Null);
            Assert.That(captured.Bindings[0].ResolvedPlanSha256,
                Is.EqualTo(ResolvedEncounterPlanCanonicalV1.ComputeSha256(resolved)));

            byte[] encoded = EncounterPlanPinCanonicalV1.Encode(captured);
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(encoded,
                out EncounterPlanPinV1 decoded, out EncounterPlanPinErrorV1 error), Is.True, error.Message);
            Assert.That(decoded.Bindings[0].Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(decoded.Bindings[0].ResolvedPlanSha256,
                Is.EqualTo(ResolvedEncounterPlanCanonicalV1.ComputeSha256(resolved)));
        }

        [Test]
        public void AppendPreservesBindingsAcceptsExactReplayAndRejectsReplacementOrForeignEntry()
        {
            EncounterCatalogManifest manifest = CreateManifest(1, includeExploration: true);
            ExpeditionRunConfiguration run = CreateRun();
            ResolvedEncounterPlanV1 combat = Resolve(manifest, run, NodeId, "pact.fervor");
            ResolvedEncounterPlanV1 exploration = Resolve(manifest, run,
                "sample.patio.exploration", "pact.fervor");
            EncounterPlanPinV1 original = EncounterPlanPinV1.Capture(manifest, run,
                new[] { combat }, NodeId);

            EncounterPlanPinV1 appended = EncounterPlanPinV1.Append(original, exploration,
                "sample.patio.exploration");
            EncounterPlanPinV1 replayed = EncounterPlanPinV1.Append(appended, exploration,
                "sample.patio.exploration");
            Assert.That(appended.Bindings.Count, Is.EqualTo(2));
            Assert.That(replayed.Bindings.Count, Is.EqualTo(2));
            Assert.That(replayed.Entry.NodeId, Is.EqualTo("sample.patio.exploration"));
            Assert.That(replayed.Bindings[0].NodeId, Is.EqualTo("sample.patio.combat"));
            Assert.That(replayed.Bindings[1].NodeId, Is.EqualTo("sample.patio.exploration"));

            ResolvedEncounterPlanV1 conflicting = Resolve(manifest, run,
                "sample.patio.exploration", "pact.ember");
            EncounterPlanPinValidationExceptionV1 mismatch = Assert.Throws<EncounterPlanPinValidationExceptionV1>(
                () => EncounterPlanPinV1.Append(appended, conflicting, "sample.patio.exploration"));
            Assert.That(mismatch.Error.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.PlanMismatch));

            EncounterPlanPinValidationExceptionV1 missingEntry = Assert.Throws<EncounterPlanPinValidationExceptionV1>(
                () => EncounterPlanPinV1.Append(appended, exploration, "sample.not.on.route"));
            Assert.That(missingEntry.Error.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.MissingBinding));
        }

        [Test]
        public void CodecRejectsMalformedDomainTruncationTrailingBytesAndUnsupportedSchema()
        {
            EncounterCatalogManifest manifest = CreateManifest(1);
            ExpeditionRunConfiguration run = CreateRun();
            EncounterPlanPinV1 pin = EncounterPlanPinV1.Capture(manifest, run,
                new[] { Resolve(manifest, run, NodeId) }, NodeId);
            byte[] valid = EncounterPlanPinCanonicalV1.Encode(pin);

            AssertDecodeFailure(null, EncounterPlanPinErrorCodeV1.NullInput);
            AssertDecodeFailure(valid.Take(valid.Length - 1).ToArray(), EncounterPlanPinErrorCodeV1.MalformedEncoding);
            AssertDecodeFailure(valid.Concat(new byte[] { 0 }).ToArray(), EncounterPlanPinErrorCodeV1.MalformedEncoding);
            byte[] wrongDomain = (byte[])valid.Clone();
            wrongDomain[0] ^= 1;
            AssertDecodeFailure(wrongDomain, EncounterPlanPinErrorCodeV1.MalformedEncoding);
            byte[] futureSchema = (byte[])valid.Clone();
            int schemaOffset = System.Text.Encoding.ASCII.GetByteCount("Luckbound.EncounterPins.v1") + 1;
            futureSchema[schemaOffset + 3] = 2;
            AssertDecodeFailure(futureSchema, EncounterPlanPinErrorCodeV1.UnsupportedSchema);
        }

        [Test]
        public void CaptureRejectsAPlanFromAnotherRunSeedOrRoute()
        {
            EncounterCatalogManifest manifest = CreateManifest(1);
            EncounterCatalogManifest differentRevision = CreateManifest(2);
            ExpeditionRunConfiguration capturedRun = CreateRun(0x31415926u);
            ExpeditionRunConfiguration otherRun = CreateRun(0x10203040u);
            ResolvedEncounterPlanV1 capturedPlan = Resolve(manifest, capturedRun, NodeId);
            ResolvedEncounterPlanV1 otherPlan = Resolve(manifest, otherRun, NodeId);

            EncounterPlanPinValidationExceptionV1 mismatch = Assert.Throws<EncounterPlanPinValidationExceptionV1>(
                () => EncounterPlanPinV1.Capture(manifest, capturedRun, new[] { otherPlan }, NodeId));
            Assert.That(mismatch.Error.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.PlanMismatch));

            EncounterPlanPinValidationExceptionV1 wrongManifest = Assert.Throws<EncounterPlanPinValidationExceptionV1>(
                () => EncounterPlanPinV1.Capture(differentRevision, capturedRun, new[] { capturedPlan }, NodeId));
            Assert.That(wrongManifest.Error.Code, Is.EqualTo(EncounterPlanPinErrorCodeV1.PlanMismatch));
        }

        [Test]
        public void BondContextRemainsMissingResolutionAuthorityWithoutCalendarData()
        {
            EncounterCatalogManifest manifest = CreateManifest(1);
            ExpeditionRunConfiguration run = CreateRun();
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, NodeId,
                new[] { "pact.bond" }, out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 resolutionError), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(resolutionError.Code,
                Is.EqualTo(EncounterPlanResolutionErrorCodeV1.MissingResolutionAuthority));
        }

        private static void AssertDecodeFailure(byte[] bytes, EncounterPlanPinErrorCodeV1 expectedCode)
        {
            Assert.That(EncounterPlanPinCanonicalV1.TryDecode(bytes,
                out EncounterPlanPinV1 pin, out EncounterPlanPinErrorV1 error), Is.False);
            Assert.That(pin, Is.Null);
            Assert.That(error.Code, Is.EqualTo(expectedCode));
            Assert.That(error.Path, Is.Not.Empty);
        }

        private static ResolvedEncounterPlanV1 Resolve(EncounterCatalogManifest manifest,
            ExpeditionRunConfiguration run, string nodeId, params string[] pactIds)
        {
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, nodeId, pactIds,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 error), Is.True,
                error.Message);
            return plan;
        }

        private static EncounterCatalogManifest CreateManifest(int revision,
            bool includeExploration = false, bool none = false, string catalogId = CatalogId)
        {
            var bindings = new System.Collections.Generic.List<EncounterCatalogBinding>
            {
                CreateBinding(ExpeditionRouteNodeKind.Combat, "room.patio.c1", none)
            };
            var definitions = new System.Collections.Generic.List<EncounterCatalogDefinition>();
            var descriptors = new System.Collections.Generic.List<EncounterCatalogDescriptor>();
            if (!none)
            {
                EncounterCatalogDescriptor room = CreateDescriptor("room.synthetic", EncounterCatalogDescriptorKind.Room, 'a');
                EncounterCatalogDescriptor enemy = CreateDescriptor("enemy.synthetic", EncounterCatalogDescriptorKind.Enemy, 'b');
                descriptors.Add(room);
                descriptors.Add(enemy);
                EncounterCatalogVariant variant = CreateVariant("variant.r" + revision.ToString(CultureInfo.InvariantCulture));
                definitions.Add(CreateDefinition(variant));
                if (includeExploration)
                    bindings.Add(CreateBinding(ExpeditionRouteNodeKind.Exploration, "room.patio.e1", false));
            }

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, catalogId, revision,
                "encounter-spawn.v1", EncounterPlanResolverV1.SupportedResolverId,
                EncounterPlanResolverV1.SupportedResolverRevision,
                new[] { ExpeditionSampleRouteDefinition.RequiredGenerationVersion }, descriptors,
                bindings, definitions, out EncounterCatalogManifest manifest,
                out EncounterCatalogError error), Is.True, error.Message);
            return manifest;
        }

        private static EncounterCatalogBinding CreateBinding(ExpeditionRouteNodeKind nodeKind,
            string templateId, bool none)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(
                ExpeditionSampleRouteDefinition.RequiredGenerationVersion, templateId, nodeKind,
                none ? EncounterCatalogBindingMode.None : EncounterCatalogBindingMode.Encounter,
                none ? null : EncounterId, none ? 0 : 1,
                out EncounterCatalogBinding binding, out EncounterCatalogError error), Is.True, error.Message);
            return binding;
        }

        private static EncounterCatalogDescriptor CreateDescriptor(string id,
            EncounterCatalogDescriptorKind kind, char digestCharacter)
        {
            string digest = new string(digestCharacter, 64);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor(id, 1, kind, digest,
                out EncounterCatalogDescriptor descriptor, out EncounterCatalogError error), Is.True, error.Message);
            return descriptor;
        }

        private static EncounterCatalogVariant CreateVariant(string variantId)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("room.synthetic", 1,
                out EncounterCatalogDescriptorRef roomRef, out EncounterCatalogError roomError), Is.True, roomError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("enemy.synthetic", 1,
                out EncounterCatalogDescriptorRef enemyRef, out EncounterCatalogError enemyError), Is.True, enemyError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("spawn.synthetic", "enemy.synthetic",
                enemyRef, "anchor.spawn", ExpeditionEncounterRewardEnemyKind.Normal, 0,
                out EncounterCatalogSpawn spawn, out EncounterCatalogError spawnError), Is.True, spawnError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateWave("wave.synthetic", new[] { spawn },
                out EncounterCatalogWave wave, out EncounterCatalogError waveError), Is.True, waveError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant(variantId,
                Array.Empty<string>(), Array.Empty<string>(), 0, roomRef, new[] { wave }, null,
                out EncounterCatalogVariant variant, out EncounterCatalogError variantError), Is.True, variantError.Message);
            return variant;
        }

        private static EncounterCatalogDefinition CreateDefinition(EncounterCatalogVariant variant)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition(EncounterId, 1,
                new[] { variant }, out EncounterCatalogDefinition definition,
                out EncounterCatalogError error), Is.True, error.Message);
            return definition;
        }

        private static ExpeditionRunConfiguration CreateRun(uint seed = 0x31415926u)
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

            // Match the six-material catalog required by the current ItemCatalog fixture contract.
            ItemDefinition[] materials = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select((kind, index) => new ItemDefinition(
                    "material.pin_fixture_" + index.ToString(CultureInfo.InvariantCulture),
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
