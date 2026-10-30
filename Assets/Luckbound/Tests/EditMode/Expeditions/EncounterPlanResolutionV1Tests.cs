using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Expeditions
{
    public sealed class EncounterPlanResolutionV1Tests
    {
        private const int GenerationVersion = ExpeditionSampleRouteDefinition.RequiredGenerationVersion;
        private const string CombatNodeId = "sample.patio.combat";
        private const string CombatTemplateId = "room.patio.c1";
        private const string EncounterId = "encounter.patio.c1";
        private const string ZeroSha256 = "0000000000000000000000000000000000000000000000000000000000000000";
        private const string OneSha256 = "1111111111111111111111111111111111111111111111111111111111111111";

        [Test]
        public void ResolvesExactEncounterBindingAndCanonicalizesSpawnOrderWithoutChangingWaveOrder()
        {
            EncounterCatalogVariant selected = Variant("variant.fervor",
                new[] { "pact.fervor" }, Array.Empty<string>(),
                new[]
                {
                    Wave("wave.second", Spawn("zeta"), Spawn("alpha")),
                    Wave("wave.first", Spawn("omega"))
                });
            EncounterCatalogVariant excluded = Variant("variant.no-fervor",
                Array.Empty<string>(), new[] { "pact.fervor" }, new[] { Wave("wave.excluded", Spawn("excluded")) });
            EncounterCatalogManifest manifest = EncounterManifest(selected, excluded);
            ExpeditionRunConfiguration run = CreateRun();

            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, CombatNodeId,
                new[] { "pact.fervor" }, out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);

            Assert.That(plan.Mode, Is.EqualTo(EncounterCatalogBindingMode.Encounter));
            Assert.That(plan.EncounterId, Is.EqualTo(EncounterId));
            Assert.That(plan.DefinitionRevision, Is.EqualTo(1));
            Assert.That(plan.VariantId, Is.EqualTo("variant.fervor"));
            Assert.That(plan.RunId, Is.EqualTo(run.RunId));
            Assert.That(plan.Seed, Is.EqualTo(run.Seed));
            Assert.That(plan.GenerationVersion, Is.EqualTo(run.Route.GenerationVersion));
            Assert.That(plan.NodeId, Is.EqualTo(CombatNodeId));
            Assert.That(plan.TemplateId, Is.EqualTo(CombatTemplateId));
            Assert.That(plan.RoomInstanceId, Is.EqualTo(ExpeditionRoomIdentity.ForNode(run.RunId, CombatNodeId)));
            Assert.That(plan.RouteDigest, Is.EqualTo(ComputeRouteDigestForTest(run.Route.CanonicalForm)));
            Assert.That(plan.CatalogDigest, Is.EqualTo(EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest)));
            Assert.That(plan.DefinitionDigest,
                Is.EqualTo(EncounterCatalogCanonicalV1.ComputeDefinitionSha256(manifest.Definitions.Single())));
            Assert.That(plan.ContextPactIds, Is.EqualTo(new[] { "pact.fervor" }));

            Assert.That(plan.SelectedVariant, Is.Not.SameAs(selected));
            Assert.That(plan.SelectedVariant.Waves.Select(wave => wave.WaveId),
                Is.EqualTo(new[] { "wave.second", "wave.first" }));
            Assert.That(plan.SelectedVariant.Waves[0].Spawns.Select(spawn => spawn.SpawnId),
                Is.EqualTo(new[] { "alpha", "zeta" }));

            EncounterSpawnIdentityV1 expectedIdentity = EncounterSpawnIdentityV1.Create(
                run.RunId, (uint)GenerationVersion, CombatNodeId, plan.RoomInstanceId, EncounterId, "alpha");
            EncounterSpawnIdentityV1 resolvedIdentity = plan.GetSpawnIdentity("alpha");
            Assert.That(resolvedIdentity.Key, Is.EqualTo(expectedIdentity.Key));
            Assert.That(resolvedIdentity.Key, Does.StartWith("encounter-spawn.v1:"));
            Assert.That(plan.GetSpawnIdentity("zeta").LocalSpawnId, Is.EqualTo("zeta"));
            Assert.That(plan.SpawnIdentities.Count, Is.EqualTo(3));
        }

        [Test]
        public void NoneBindingIsExplicitAndHasNoEncounterPayloadOrSpawnIdentities()
        {
            EncounterCatalogManifest manifest = NoneManifest();
            ExpeditionRunConfiguration run = CreateRun();

            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, CombatNodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);

            Assert.That(plan.Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(plan.DefinitionDigest, Is.Null);
            Assert.That(plan.EncounterId, Is.Null);
            Assert.That(plan.DefinitionRevision, Is.Zero);
            Assert.That(plan.VariantId, Is.Null);
            Assert.That(plan.SelectedVariant, Is.Null);
            Assert.That(plan.UsedDescriptors, Is.Empty);
            Assert.That(plan.SpawnIdentities, Is.Empty);
            Assert.Throws<KeyNotFoundException>(() => plan.GetSpawnIdentity("anything"));

            byte[] encoded = ResolvedEncounterPlanCanonicalV1.Encode(plan);
            Assert.That(StartsWith(encoded, System.Text.Encoding.ASCII.GetBytes("Luckbound.NoEncounterBinding.v1\0")), Is.True);
            Assert.That(ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan), Does.Match("^[0-9a-f]{64}$"));
            encoded[0] ^= 0xff;
            Assert.That(ResolvedEncounterPlanCanonicalV1.Encode(plan)[0],
                Is.EqualTo(System.Text.Encoding.ASCII.GetBytes("Luckbound.NoEncounterBinding.v1\0")[0]));
        }

        [Test]
        public void ResolutionContextIsCopiedSortedOrdinallyAndIndependentOfCulture()
        {
            EncounterCatalogVariant variant = Variant("variant.any", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave", Spawn("a")) });
            EncounterCatalogManifest manifest = EncounterManifest(variant);
            ExpeditionRunConfiguration run = CreateRun();
            var input = new List<string> { "pact.fervor", "pact.avarice" };

            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, CombatNodeId, input,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);
            input.Clear();
            Assert.That(plan.ContextPactIds, Is.EqualTo(new[] { "pact.avarice", "pact.fervor" }));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)plan.ContextPactIds)[0] = "pact.silence");

            CultureInfo originalCulture = CultureInfo.CurrentCulture;
            try
            {
                byte[] beforeCulture = ResolvedEncounterPlanCanonicalV1.Encode(plan);
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                Assert.That(ResolvedEncounterPlanCanonicalV1.Encode(plan), Is.EqualTo(beforeCulture));
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void RejectsNullUnknownDuplicateAndOverLimitPactContexts()
        {
            EncounterCatalogManifest manifest = NoneManifest();
            ExpeditionRunConfiguration run = CreateRun();

            AssertResolutionFailure(manifest, run, null, EncounterPlanResolutionErrorCodeV1.NullPactIds);
            AssertResolutionFailure(manifest, run, new[] { "pact.unknown" },
                EncounterPlanResolutionErrorCodeV1.InvalidResolutionContext);
            AssertResolutionFailure(manifest, run, new[] { "pact.fervor", "pact.fervor" },
                EncounterPlanResolutionErrorCodeV1.InvalidResolutionContext);
            AssertResolutionFailure(manifest, run,
                new[] { "pact.avarice", "pact.fervor", "pact.silence" },
                EncounterPlanResolutionErrorCodeV1.InvalidResolutionContext);
        }

        [Test]
        public void VínculoRequiresMissingResolutionAuthorityInsteadOfInferringAnOrdinal()
        {
            EncounterCatalogManifest manifest = NoneManifest();
            ExpeditionRunConfiguration run = CreateRun();

            AssertResolutionFailure(manifest, run, new[] { "pact.bond" },
                EncounterPlanResolutionErrorCodeV1.MissingResolutionAuthority);
        }

        [Test]
        public void RejectsUnsupportedResolverIdAndRevision()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest unknownId = Manifest(
                new[] { NoneBinding() }, Array.Empty<EncounterCatalogDefinition>(),
                resolverId: "encounter-resolver.unknown");
            EncounterCatalogManifest unknownRevision = Manifest(
                new[] { NoneBinding() }, Array.Empty<EncounterCatalogDefinition>(), resolverRevision: 2);

            AssertResolutionFailure(unknownId, run, Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.UnsupportedResolver);
            AssertResolutionFailure(unknownRevision, run, Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.UnsupportedResolver);
        }

        [Test]
        public void MissingBindingFailsAndFactoryRejectsAmbiguousBindingKeys()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest missing = Manifest(Array.Empty<EncounterCatalogBinding>(),
                Array.Empty<EncounterCatalogDefinition>());
            AssertResolutionFailure(missing, run, Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.MissingBinding);

            EncounterCatalogBinding binding = NoneBinding();
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog-duplicate-binding", 1,
                "encounter-spawn.v1", EncounterPlanResolverV1.SupportedResolverId,
                EncounterPlanResolverV1.SupportedResolverRevision, new[] { GenerationVersion },
                Array.Empty<EncounterCatalogDescriptor>(), new[] { binding, binding },
                Array.Empty<EncounterCatalogDefinition>(), out _, out EncounterCatalogError error), Is.False);
            Assert.That(error.Code, Is.EqualTo(EncounterCatalogErrorCode.DuplicateIdentifier));
        }

        [Test]
        public void ZeroOrMultipleApplicableVariantsFailClosed()
        {
            EncounterCatalogVariant unavailable = Variant("variant.requires-silence",
                new[] { "pact.silence" }, Array.Empty<string>(), new[] { Wave("wave", Spawn("a")) });
            EncounterCatalogManifest noMatch = EncounterManifest(unavailable);
            AssertResolutionFailure(noMatch, CreateRun(), Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.NoApplicableVariant);

            EncounterCatalogVariant first = Variant("variant.first", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave.first", Spawn("a")) });
            EncounterCatalogVariant second = Variant("variant.second", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave.second", Spawn("b")) });
            EncounterCatalogManifest ambiguous = EncounterManifest(first, second);
            AssertResolutionFailure(ambiguous, CreateRun(), Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.AmbiguousVariant);
        }

        [Test]
        public void NoVinculoCalendarRequiresZeroScheduleAndZeroSpawnOrdinals()
        {
            EncounterCatalogVariant scheduled = Variant("variant.scheduled", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave", Spawn("a")) }, scheduledOrdinal: 1);
            AssertResolutionFailure(EncounterManifest(scheduled), CreateRun(), Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.NoApplicableVariant);

            EncounterCatalogVariant spawnScheduled = Variant("variant.spawn-scheduled", Array.Empty<string>(),
                Array.Empty<string>(), new[]
                {
                    Wave("wave", Spawn("a", ExpeditionEncounterRewardEnemyKind.Elite, vinculoOrdinal: 1))
                });
            AssertResolutionFailure(EncounterManifest(spawnScheduled), CreateRun(), Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.UnsupportedVariant);
        }

        [Test]
        public void RouteGenerationAndNodeMustBeExactAndSupported()
        {
            ExpeditionRunConfiguration run = CreateRun();
            EncounterCatalogManifest wrongGeneration = Manifest(Array.Empty<EncounterCatalogBinding>(),
                Array.Empty<EncounterCatalogDefinition>(), supportedGenerations: new[] { 1 });
            AssertResolutionFailure(wrongGeneration, run, Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.UnsupportedRouteGeneration);
            AssertResolutionFailure(NoneManifest(), run, Array.Empty<string>(),
                EncounterPlanResolutionErrorCodeV1.RouteNodeNotFound, nodeId: "sample.node.not-in-route");
        }

        [Test]
        public void ChangingPlanSemanticsChangesPlanDigestButKeepsPrimarySpawnKey()
        {
            EncounterCatalogVariant firstVariant = Variant("variant", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave", Spawn("spawn-a", enemyId: "enemy.slime")) });
            EncounterCatalogVariant changedVariant = Variant("variant", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave", Spawn("spawn-a", enemyId: "enemy.guardian")) });
            ExpeditionRunConfiguration run = CreateRun();

            Assert.That(EncounterPlanResolverV1.TryResolve(EncounterManifest(firstVariant), run, CombatNodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 first, out EncounterPlanResolutionErrorV1 firstError),
                Is.True, firstError.Message);
            Assert.That(EncounterPlanResolverV1.TryResolve(EncounterManifest(changedVariant), run, CombatNodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 changed, out EncounterPlanResolutionErrorV1 changedError),
                Is.True, changedError.Message);

            Assert.That(ResolvedEncounterPlanCanonicalV1.ComputeSha256(first),
                Is.Not.EqualTo(ResolvedEncounterPlanCanonicalV1.ComputeSha256(changed)));
            Assert.That(first.GetSpawnIdentity("spawn-a").Key,
                Is.EqualTo(changed.GetSpawnIdentity("spawn-a").Key));
        }

        [Test]
        public void PlanCopiesSelectedVariantCollectionsAndDescriptorClosure()
        {
            EncounterCatalogVariant sourceVariant = Variant("variant", Array.Empty<string>(),
                Array.Empty<string>(), new[] { Wave("wave", Spawn("zeta"), Spawn("alpha")) });
            EncounterCatalogManifest manifest = EncounterManifest(sourceVariant);
            var pactContext = new List<string> { "pact.avarice" };

            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, CreateRun(), CombatNodeId, pactContext,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);
            pactContext[0] = "pact.silence";

            Assert.That(plan.ContextPactIds, Is.EqualTo(new[] { "pact.avarice" }));
            Assert.That(plan.SelectedVariant, Is.Not.SameAs(sourceVariant));
            Assert.That(plan.SelectedVariant.Waves[0].Spawns.Select(spawn => spawn.SpawnId),
                Is.EqualTo(new[] { "alpha", "zeta" }));
            Assert.That(plan.UsedDescriptors.Select(descriptor => descriptor.DescriptorId),
                Is.EqualTo(new[] { "descriptor.enemy", "descriptor.room" }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<EncounterCatalogDescriptor>)plan.UsedDescriptors)[0] = null);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<EncounterCatalogSpawn>)plan.SelectedVariant.Waves[0].Spawns)[0] = null);
        }

        [Test]
        public void IndependentNoneCanonicalByteAndSha256Vector()
        {
            ExpeditionRunConfiguration run = CreateRun(0x10203040u);
            EncounterCatalogManifest manifest = NoneManifest(catalogId: "catalog-none");
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, CombatNodeId,
                Array.Empty<string>(), out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);

            const string expectedBytesHex =
                "4c75636b626f756e642e4e6f456e636f756e74657242696e64696e672e7631000000004064643431"
                + "64363532376436353837616435393736643633346231336166616439386436323465653864383433"
                + "61303639643934656166663635666432326131310000004037363863636639386537376536323137"
                + "63343338643764376134353661623331626435323836363133613762646439363138386531643738"
                + "61363336636261310000002030313233343536373839616263646566303132333435363738396162"
                + "63646566000000083130323033303430000000020000001373616d706c652e706174696f2e636f6d"
                + "6261740000000d726f6f6d2e706174696f2e63310000002038633031306532643663393764343739"
                + "39613733363861356564646435376166000000046e6f6e650000000000";
            const string expectedSha256 = "2af2991af4b3d9c3e96e46a7674e8e2e9a5f6e753e2325d0bd25564e29bb81f3";
            Assert.That(ToHex(ResolvedEncounterPlanCanonicalV1.Encode(plan)), Is.EqualTo(expectedBytesHex));
            Assert.That(ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan), Is.EqualTo(expectedSha256));
        }

        [Test]
        public void IndependentEncounterCanonicalByteAndSha256VectorIncludesUtf8AndDescriptorClosure()
        {
            ExpeditionRunConfiguration run = CreateRun(0x10203040u);
            EncounterCatalogManifest manifest = EncounterVectorManifest();
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, CombatNodeId,
                new[] { "pact.fervor" }, out ResolvedEncounterPlanV1 plan,
                out EncounterPlanResolutionErrorV1 error), Is.True, error.Message);

            const string expectedBytesHex =
                "4c75636b626f756e642e5265736f6c766564456e636f756e746572506c616e2e7631000000004064"
                + "32656365366137346365626465663162613439353734393936666265336134356136376466396636"
                + "38633861623463343636373363303735306166323966300000004037663230303437656533653266"
                + "32316162613763623437656266633062653938303164353835323363306261626537376232623261"
                + "62386361303032663935650000004037363863636639386537376536323137633433386437643761"
                + "34353661623331626435323836363133613762646439363138386531643738613633366362613100"
                + "00002030313233343536373839616263646566303132333435363738396162636465660000000831"
                + "30323033303430000000020000001373616d706c652e706174696f2e636f6d6261740000000d726f"
                + "6f6d2e706174696f2e63310000002038633031306532643663393764343739396137333638613565"
                + "646464353761660000000c656e636f756e7465722dc3a9000000030000000a76617269616e742dc3a9"
                + "000000010000000b706163742e666572766f72000000000a76617269616e742dc3a9000000010000"
                + "000b706163742e666572766f72000000000000000000000009646573632d726f6f6d000000010000"
                + "000100000007776176652dc3a90000000200000005616c70686100000008656e656d792dc3a90000"
                + "000a646573632d656e656d790000000100000009616e63686f722dc3a9000000066e6f726d616c00"
                + "000000000000047a65746100000008656e656d792dc3a90000000a646573632d656e656d79000000"
                + "0100000009616e63686f722dc3a9000000066e6f726d616c0000000000000000020000000a646573"
                + "632d656e656d790000000100000005656e656d790000004031313131313131313131313131313131"
                + "31313131313131313131313131313131313131313131313131313131313131313131313131313131"
                + "313131313131313100000009646573632d726f6f6d0000000100000004726f6f6d00000040303030"
                + "30303030303030303030303030303030303030303030303030303030303030303030303030303030"
                + "303030303030303030303030303030303030303030";
            const string expectedSha256 = "6e5c8f94838702a4bc898005d16457555b539f5865674af81866263f61a0e70b";
            Assert.That(ToHex(ResolvedEncounterPlanCanonicalV1.Encode(plan)), Is.EqualTo(expectedBytesHex));
            Assert.That(ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan), Is.EqualTo(expectedSha256));
        }

        private static void AssertResolutionFailure(
            EncounterCatalogManifest manifest,
            ExpeditionRunConfiguration run,
            IEnumerable<string> pactIds,
            EncounterPlanResolutionErrorCodeV1 expectedCode,
            string nodeId = CombatNodeId)
        {
            Assert.That(EncounterPlanResolverV1.TryResolve(manifest, run, nodeId, pactIds,
                out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 error), Is.False);
            Assert.That(plan, Is.Null);
            Assert.That(error.Code, Is.EqualTo(expectedCode));
            Assert.That(error.Message, Is.Not.Empty);
        }

        private static EncounterCatalogManifest NoneManifest(string catalogId = "catalog-none")
        {
            return Manifest(new[] { NoneBinding() }, Array.Empty<EncounterCatalogDefinition>(), catalogId: catalogId);
        }

        private static EncounterCatalogManifest EncounterVectorManifest()
        {
            EncounterCatalogDescriptor room = Descriptor("desc-room", EncounterCatalogDescriptorKind.Room, ZeroSha256);
            EncounterCatalogDescriptor enemy = Descriptor("desc-enemy", EncounterCatalogDescriptorKind.Enemy, OneSha256);
            EncounterCatalogSpawn zeta;
            EncounterCatalogSpawn alpha;
            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("zeta", "enemy-é", DescriptorRef("desc-enemy", 1),
                "anchor-é", ExpeditionEncounterRewardEnemyKind.Normal, 0,
                out zeta, out EncounterCatalogError zetaError), Is.True, zetaError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("alpha", "enemy-é", DescriptorRef("desc-enemy", 1),
                "anchor-é", ExpeditionEncounterRewardEnemyKind.Normal, 0,
                out alpha, out EncounterCatalogError alphaError), Is.True, alphaError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateWave("wave-é", new[] { zeta, alpha },
                out EncounterCatalogWave wave, out EncounterCatalogError waveError), Is.True, waveError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant("variant-é", new[] { "pact.fervor" },
                Array.Empty<string>(), 0, DescriptorRef("desc-room", 1), new[] { wave }, null,
                out EncounterCatalogVariant variant, out EncounterCatalogError variantError), Is.True, variantError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition("encounter-é", 3, new[] { variant },
                out EncounterCatalogDefinition definition, out EncounterCatalogError definitionError), Is.True, definitionError.Message);
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(GenerationVersion, CombatTemplateId,
                ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.Encounter,
                "encounter-é", 3, out EncounterCatalogBinding binding, out EncounterCatalogError bindingError),
                Is.True, bindingError.Message);
            return Manifest(new[] { binding }, new[] { definition }, new[] { enemy, room },
                catalogId: "catalog-enc", catalogRevision: 4);
        }

        private static EncounterCatalogManifest EncounterManifest(params EncounterCatalogVariant[] variants)
        {
            EncounterCatalogDescriptor room = Descriptor("descriptor.room", EncounterCatalogDescriptorKind.Room, ZeroSha256);
            EncounterCatalogDescriptor enemy = Descriptor("descriptor.enemy", EncounterCatalogDescriptorKind.Enemy, OneSha256);
            EncounterCatalogDefinition definition = Definition(EncounterId, variants);
            EncounterCatalogBinding binding = Binding(EncounterCatalogBindingMode.Encounter, EncounterId, 1);
            return Manifest(new[] { binding }, new[] { definition },
                descriptors: new[] { room, enemy });
        }

        private static EncounterCatalogManifest Manifest(
            IEnumerable<EncounterCatalogBinding> bindings,
            IEnumerable<EncounterCatalogDefinition> definitions,
            IEnumerable<EncounterCatalogDescriptor> descriptors = null,
            string catalogId = "catalog-fixture",
            int catalogRevision = 1,
            string resolverId = null,
            int resolverRevision = EncounterPlanResolverV1.SupportedResolverRevision,
            IEnumerable<int> supportedGenerations = null)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, catalogId, catalogRevision,
                "encounter-spawn.v1",
                resolverId ?? EncounterPlanResolverV1.SupportedResolverId,
                resolverRevision,
                supportedGenerations ?? new[] { GenerationVersion },
                descriptors ?? Array.Empty<EncounterCatalogDescriptor>(),
                bindings,
                definitions,
                out EncounterCatalogManifest manifest,
                out EncounterCatalogError error), Is.True, error.Message);
            return manifest;
        }

        private static EncounterCatalogBinding NoneBinding()
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(GenerationVersion, CombatTemplateId,
                ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.None,
                null, 0, out EncounterCatalogBinding binding, out EncounterCatalogError error), Is.True, error.Message);
            return binding;
        }

        private static EncounterCatalogBinding Binding(
            EncounterCatalogBindingMode mode,
            string encounterId,
            int definitionRevision)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(GenerationVersion, CombatTemplateId,
                ExpeditionRouteNodeKind.Combat, mode, encounterId, definitionRevision,
                out EncounterCatalogBinding binding, out EncounterCatalogError error), Is.True, error.Message);
            return binding;
        }

        private static EncounterCatalogDefinition Definition(string encounterId, params EncounterCatalogVariant[] variants)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition(encounterId, 1, variants,
                out EncounterCatalogDefinition definition, out EncounterCatalogError error), Is.True, error.Message);
            return definition;
        }

        private static EncounterCatalogVariant Variant(
            string variantId,
            IEnumerable<string> requiredPacts,
            IEnumerable<string> forbiddenPacts,
            IEnumerable<EncounterCatalogWave> waves,
            int scheduledOrdinal = 0,
            EncounterCatalogCache cache = null)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant(variantId, requiredPacts,
                forbiddenPacts, scheduledOrdinal, DescriptorRef("descriptor.room", 1),
                waves, cache, out EncounterCatalogVariant variant, out EncounterCatalogError error), Is.True, error.Message);
            return variant;
        }

        private static EncounterCatalogWave Wave(string waveId, params EncounterCatalogSpawn[] spawns)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateWave(waveId, spawns,
                out EncounterCatalogWave wave, out EncounterCatalogError error), Is.True, error.Message);
            return wave;
        }

        private static EncounterCatalogSpawn Spawn(
            string spawnId,
            ExpeditionEncounterRewardEnemyKind rewardKind = ExpeditionEncounterRewardEnemyKind.Normal,
            int vinculoOrdinal = 0,
            string enemyId = "enemy.slime")
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn(spawnId, enemyId,
                DescriptorRef("descriptor.enemy", 1), "anchor.default", rewardKind, vinculoOrdinal,
                out EncounterCatalogSpawn spawn, out EncounterCatalogError error), Is.True, error.Message);
            return spawn;
        }

        private static EncounterCatalogDescriptor Descriptor(string id, EncounterCatalogDescriptorKind kind, string digest)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor(id, 1, kind, digest,
                out EncounterCatalogDescriptor descriptor, out EncounterCatalogError error), Is.True, error.Message);
            return descriptor;
        }

        private static EncounterCatalogDescriptorRef DescriptorRef(string id, int revision)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef(id, revision,
                out EncounterCatalogDescriptorRef descriptorRef, out EncounterCatalogError error), Is.True, error.Message);
            return descriptorRef;
        }

        private static ExpeditionRunConfiguration CreateRun(uint seed = 0x31415926u)
        {
            ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(seed, CreateSampleRouteDefinition());
            var materials = Enum.GetValues(typeof(MaterialKind)).Cast<MaterialKind>()
                .Select((kind, index) => new ItemDefinition(
                    "material.fixture_" + index.ToString(CultureInfo.InvariantCulture),
                    "item.fixture.name", "item.fixture.description", ItemKind.Material, 99,
                    materialKind: kind))
                .ToArray();
            var inventory = new ExpeditionInventory(new ItemCatalog(materials), "class.bellator");
            var runId = new RunId(Guid.ParseExact("0123456789abcdef0123456789abcdef", "N"));
            return new ExpeditionRunConfiguration(runId, "class.bellator", ExpeditionRunConfiguration.HumanRaceId,
                new[] { "skill.fixture.one", "skill.fixture.two" }, seed, route, inventory);
        }

        private static ExpeditionSampleRouteDefinition CreateSampleRouteDefinition()
        {
            const string tutorial = "sample.tutorial";
            const string combat = "sample.patio.combat";
            const string exploration = "sample.patio.exploration";
            const string guardian = "sample.patio.guardian";
            const string extraction = "sample.safe_extraction";

            ExpeditionSampleRouteNodeDefinition[] nodes =
            {
                Node(tutorial, "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial, 0),
                Node(combat, CombatTemplateId, ExpeditionRouteNodeKind.Combat, 1),
                Node(exploration, "room.patio.e1", ExpeditionRouteNodeKind.Exploration, 2),
                Node(guardian, "room.patio.guardian", ExpeditionRouteNodeKind.Guardian, 3),
                Node(extraction, "room.sample.extraction", ExpeditionRouteNodeKind.SafeExtraction, 4)
            };
            string[] mandatory = { tutorial, combat, exploration, guardian, extraction };
            var connections = new ExpeditionSampleRouteConnectionDefinition[mandatory.Length - 1];
            for (int index = 0; index < connections.Length; index++)
            {
                connections[index] = new ExpeditionSampleRouteConnectionDefinition(
                    mandatory[index], ExpeditionSampleRouteDefinition.ExitPortId,
                    mandatory[index + 1], ExpeditionSampleRouteDefinition.EntryPortId, false);
            }

            return new ExpeditionSampleRouteDefinition(
                ExpeditionSampleRouteDefinition.RequiredSchemaVersion,
                GenerationVersion,
                nodes,
                connections,
                mandatory);
        }

        private static ExpeditionSampleRouteNodeDefinition Node(
            string nodeId,
            string templateId,
            ExpeditionRouteNodeKind kind,
            int pathIndex)
        {
            return new ExpeditionSampleRouteNodeDefinition(nodeId, "patio", templateId, kind,
                true, pathIndex, ExpeditionSampleRouteDefinition.EntryPortId,
                ExpeditionSampleRouteDefinition.ExitPortId);
        }

        private static string ComputeRouteDigestForTest(string canonicalForm)
        {
            byte[] bytes = new System.Text.UTF8Encoding(false, true).GetBytes(canonicalForm);
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                return ToHex(digest).ToLowerInvariant();
            }
        }

        private static bool StartsWith(byte[] value, byte[] prefix)
        {
            if (value == null || prefix == null || value.Length < prefix.Length) return false;
            for (int index = 0; index < prefix.Length; index++)
                if (value[index] != prefix[index]) return false;
            return true;
        }

        private static string ToHex(byte[] bytes)
        {
            var characters = new char[bytes.Length * 2];
            const string digits = "0123456789abcdef";
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = digits[bytes[index] >> 4];
                characters[index * 2 + 1] = digits[bytes[index] & 0x0f];
            }
            return new string(characters);
        }
    }
}
