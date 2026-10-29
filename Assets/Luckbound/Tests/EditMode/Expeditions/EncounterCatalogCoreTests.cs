using System;
using System.Collections.Generic;
using Luckbound.Core.Expeditions;
using NUnit.Framework;

namespace Luckbound.Tests.Core.Expeditions
{
    public sealed class EncounterCatalogCoreTests
    {
        private const string ZeroSha256 = "0000000000000000000000000000000000000000000000000000000000000000";
        private const string OneSha256 = "1111111111111111111111111111111111111111111111111111111111111111";

        [Test]
        public void DefaultErrorIsNoneAndTokensAreExactAndOrdinal()
        {
            Assert.That(default(EncounterCatalogError).Code, Is.EqualTo(EncounterCatalogErrorCode.None));
            Assert.That(EncounterCatalogTokensV1.TryParseDescriptorKind("room",
                out EncounterCatalogDescriptorKind descriptorKind), Is.True);
            Assert.That(descriptorKind, Is.EqualTo(EncounterCatalogDescriptorKind.Room));
            Assert.That(EncounterCatalogTokensV1.TryParseBindingMode("encounter",
                out EncounterCatalogBindingMode mode), Is.True);
            Assert.That(mode, Is.EqualTo(EncounterCatalogBindingMode.Encounter));
            Assert.That(EncounterCatalogTokensV1.TryParseNodeKind("final-boss",
                out ExpeditionRouteNodeKind nodeKind), Is.True);
            Assert.That(nodeKind, Is.EqualTo(ExpeditionRouteNodeKind.FinalBoss));
            Assert.That(EncounterCatalogTokensV1.TryParseRewardKind("guardian",
                out ExpeditionEncounterRewardEnemyKind rewardKind), Is.True);
            Assert.That(rewardKind, Is.EqualTo(ExpeditionEncounterRewardEnemyKind.Guardian));
            Assert.That(EncounterCatalogTokensV1.TryParseRewardKind("Guardian", out _), Is.False);
            Assert.That(EncounterCatalogTokensV1.TryParseNodeKind("Combat", out _), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                EncounterCatalogTokensV1.GetToken((EncounterCatalogBindingMode)99));
        }

        [Test]
        public void FactoriesRejectSchemaRevisionsTokensShaTextAndSpawnAlphabetErrors()
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(2, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, Array.Empty<int>(),
                Array.Empty<EncounterCatalogDescriptor>(), Array.Empty<EncounterCatalogBinding>(),
                Array.Empty<EncounterCatalogDefinition>(), out _, out EncounterCatalogError schemaError), Is.False);
            Assert.That(schemaError.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidSchema));

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "unknown-codec", "resolver", 1, Array.Empty<int>(),
                Array.Empty<EncounterCatalogDescriptor>(), Array.Empty<EncounterCatalogBinding>(),
                Array.Empty<EncounterCatalogDefinition>(), out _, out EncounterCatalogError codecError), Is.False);
            Assert.That(codecError.Code, Is.EqualTo(EncounterCatalogErrorCode.UnsupportedIdentityCodec));

            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef("room", 0,
                out _, out EncounterCatalogError revisionError), Is.False);
            Assert.That(revisionError.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));

            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor("room", 1,
                EncounterCatalogDescriptorKind.Room, new string('A', 64),
                out _, out EncounterCatalogError shaError), Is.False);
            Assert.That(shaError.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));

            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("bad/spawn", "enemy",
                Ref("enemy"), "anchor", ExpeditionEncounterRewardEnemyKind.Normal, 0,
                out _, out EncounterCatalogError spawnError), Is.False);
            Assert.That(spawnError.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));

            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn("spawn", "enemy",
                Ref("enemy"), "anchor", ExpeditionEncounterRewardEnemyKind.Normal, 1,
                out _, out EncounterCatalogError eliteError), Is.False);
            Assert.That(eliteError.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));

            string invalidUtf16 = "bad\uD800";
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, invalidUtf16, 1,
                "encounter-spawn.v1", "resolver", 1, Array.Empty<int>(),
                Array.Empty<EncounterCatalogDescriptor>(), Array.Empty<EncounterCatalogBinding>(),
                Array.Empty<EncounterCatalogDefinition>(), out _, out EncounterCatalogError utf8Error), Is.False);
            Assert.That(utf8Error.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));
        }

        [Test]
        public void BindingNoneAndEncounterHaveDistinctRequiredShapes()
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(1, "template",
                ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.None,
                null, 0, out EncounterCatalogBinding none, out EncounterCatalogError noneError), Is.True,
                noneError.Message);
            Assert.That(none.Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(none.EncounterId, Is.Null);
            Assert.That(none.DefinitionRevision, Is.Zero);

            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(1, "template",
                ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.None,
                "encounter", 1, out _, out EncounterCatalogError invalidNone), Is.False);
            Assert.That(invalidNone.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));

            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(1, "template",
                ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.Encounter,
                "encounter", 1, out EncounterCatalogBinding encounter, out EncounterCatalogError encounterError), Is.True,
                encounterError.Message);
            Assert.That(encounter.Mode, Is.EqualTo(EncounterCatalogBindingMode.Encounter));
            Assert.That(encounter.EncounterId, Is.EqualTo("encounter"));
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(1, "template",
                ExpeditionRouteNodeKind.Combat, EncounterCatalogBindingMode.Encounter,
                null, 1, out _, out EncounterCatalogError invalidEncounter), Is.False);
            Assert.That(invalidEncounter.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));
        }

        [Test]
        public void FactoriesRejectLocalAndGlobalDuplicatesMissingReferencesAndWrongKinds()
        {
            EncounterCatalogSpawn duplicateSpawn = Spawn("spawn-a", Ref("enemy"));
            Assert.That(EncounterCatalogFactoryV1.TryCreateWave("wave",
                new[] { duplicateSpawn, duplicateSpawn }, out _, out EncounterCatalogError waveError), Is.False);
            Assert.That(waveError.Code, Is.EqualTo(EncounterCatalogErrorCode.DuplicateIdentifier));

            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant("variant",
                Array.Empty<string>(), Array.Empty<string>(), 0, Ref("room"),
                new[] { Wave("wave-a", Spawn("shared", Ref("enemy"))),
                    Wave("wave-b", Spawn("shared", Ref("enemy"))) }, null,
                out _, out EncounterCatalogError globalSpawnError), Is.False);
            Assert.That(globalSpawnError.Code, Is.EqualTo(EncounterCatalogErrorCode.DuplicateIdentifier));

            EncounterCatalogVariant variant = Variant("variant", Ref("room"),
                new[] { Wave("wave", Spawn("spawn", Ref("enemy"))) }, null);
            EncounterCatalogDescriptor room = Descriptor("room", EncounterCatalogDescriptorKind.Room);
            EncounterCatalogDescriptor enemy = Descriptor("enemy", EncounterCatalogDescriptorKind.Enemy);
            EncounterCatalogDefinition definition = Definition("encounter", variant);
            EncounterCatalogBinding binding = Binding("encounter", EncounterCatalogBindingMode.Encounter,
                "encounter", 1);

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, new[] { 1 },
                new[] { room, room, enemy }, new[] { binding }, new[] { definition },
                out _, out EncounterCatalogError descriptorDuplicate), Is.False);
            Assert.That(descriptorDuplicate.Code, Is.EqualTo(EncounterCatalogErrorCode.DuplicateIdentifier));

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, new[] { 1 },
                new[] { room, enemy }, new[] { binding, binding }, new[] { definition },
                out _, out EncounterCatalogError bindingDuplicate), Is.False);
            Assert.That(bindingDuplicate.Code, Is.EqualTo(EncounterCatalogErrorCode.DuplicateIdentifier));

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, new[] { 1 },
                new[] { room, enemy }, new[] { binding }, Array.Empty<EncounterCatalogDefinition>(),
                out _, out EncounterCatalogError missingDefinition), Is.False);
            Assert.That(missingDefinition.Code, Is.EqualTo(EncounterCatalogErrorCode.MissingReference));

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, new[] { 1 },
                new[] { room }, new[] { binding }, new[] { definition },
                out _, out EncounterCatalogError missingDescriptor), Is.False);
            Assert.That(missingDescriptor.Code, Is.EqualTo(EncounterCatalogErrorCode.MissingReference));

            EncounterCatalogDescriptor wrongRoomKind = Descriptor("room", EncounterCatalogDescriptorKind.Enemy);
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, new[] { 1 },
                new[] { wrongRoomKind, enemy }, new[] { binding }, new[] { definition },
                out _, out EncounterCatalogError kindMismatch), Is.False);
            Assert.That(kindMismatch.Code, Is.EqualTo(EncounterCatalogErrorCode.ReferenceKindMismatch));

            EncounterCatalogBinding missingGeneration = Binding("encounter", EncounterCatalogBindingMode.None,
                null, 0, generation: 2);
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, new[] { 1 },
                new[] { room, enemy }, new[] { missingGeneration }, new[] { definition },
                out _, out EncounterCatalogError generationError), Is.False);
            Assert.That(generationError.Code, Is.EqualTo(EncounterCatalogErrorCode.MissingReference));
        }

        [Test]
        public void ModelsDefensivelyCopyInputCollectionsAndExposeReadOnlyViews()
        {
            var required = new List<string> { "pact-b", "pact-a" };
            var waves = new List<EncounterCatalogWave> { Wave("wave-a") };
            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant("variant", required,
                Array.Empty<string>(), 0, Ref("room"), waves, null,
                out EncounterCatalogVariant variant, out EncounterCatalogError variantError), Is.True,
                variantError.Message);
            required[0] = "changed";
            waves.Clear();
            Assert.That(variant.RequiredPactIds, Is.EqualTo(new[] { "pact-b", "pact-a" }));
            Assert.That(variant.Waves.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)variant.RequiredPactIds)[0] = "mutated" );

            var generations = new List<int> { 1 };
            var descriptors = new List<EncounterCatalogDescriptor> { Descriptor("room", EncounterCatalogDescriptorKind.Room) };
            var bindings = new List<EncounterCatalogBinding>();
            var definitions = new List<EncounterCatalogDefinition>();
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog", 1,
                "encounter-spawn.v1", "resolver", 1, generations, descriptors, bindings, definitions,
                out EncounterCatalogManifest manifest, out EncounterCatalogError manifestError), Is.True,
                manifestError.Message);
            generations.Clear();
            descriptors.Clear();
            Assert.That(manifest.SupportedRouteGenerationVersions, Is.EqualTo(new[] { 1 }));
            Assert.That(manifest.DescriptorRefs.Count, Is.EqualTo(1));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<int>)manifest.SupportedRouteGenerationVersions)[0] = 9);
        }

        [Test]
        public void CanonicalEncodingSortsSetsSpawnsAndTablesButPreservesWaveOrder()
        {
            EncounterCatalogManifest first = RichManifest(reverseOuterCollections: false,
                reversePacts: false, reverseSpawns: false, reverseWaves: false);
            EncounterCatalogManifest reordered = RichManifest(reverseOuterCollections: true,
                reversePacts: true, reverseSpawns: true, reverseWaves: false);
            CollectionAssert.AreEqual(EncounterCatalogCanonicalV1.EncodeManifest(first),
                EncounterCatalogCanonicalV1.EncodeManifest(reordered));
            Assert.That(EncounterCatalogCanonicalV1.ComputeManifestSha256(first),
                Is.EqualTo(EncounterCatalogCanonicalV1.ComputeManifestSha256(reordered)));

            EncounterCatalogManifest reorderedWaves = RichManifest(reverseOuterCollections: false,
                reversePacts: false, reverseSpawns: false, reverseWaves: true);
            Assert.That(EncounterCatalogCanonicalV1.ComputeManifestSha256(first),
                Is.Not.EqualTo(EncounterCatalogCanonicalV1.ComputeManifestSha256(reorderedWaves)));

            byte[] exposed = EncounterCatalogCanonicalV1.EncodeManifest(first);
            byte original = exposed[0];
            exposed[0] ^= 0xff;
            Assert.That(EncounterCatalogCanonicalV1.EncodeManifest(first)[0], Is.EqualTo(original));
        }

        [Test]
        public void IndependentCanonicalByteAndDigestVectorsIncludeUtf8MultibyteText()
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "cat-é", 7,
                "encounter-spawn.v1", "resolver-a", 2, Array.Empty<int>(),
                Array.Empty<EncounterCatalogDescriptor>(), Array.Empty<EncounterCatalogBinding>(),
                Array.Empty<EncounterCatalogDefinition>(), out EncounterCatalogManifest manifest,
                out EncounterCatalogError manifestError), Is.True, manifestError.Message);
            const string expectedManifestBytes =
                "4c75636b626f756e642e456e636f756e746572436174616c6f672e76310000000001000000066361742dc3a9"
                + "0000000700000012656e636f756e7465722d737061776e2e76310000000a7265736f6c7665722d61"
                + "000000020000000000000000000000000000000000";
            Assert.That(ToHex(EncounterCatalogCanonicalV1.EncodeManifest(manifest)),
                Is.EqualTo(expectedManifestBytes));
            Assert.That(EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest),
                Is.EqualTo("e1fd590d411d733d42f352baad7779360bfa093ec18758c798763d7785331c39"));

            Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition("e-1", 1,
                Array.Empty<EncounterCatalogVariant>(), out EncounterCatalogDefinition definition,
                out EncounterCatalogError definitionError), Is.True, definitionError.Message);
            const string expectedDefinitionBytes =
                "4c75636b626f756e642e456e636f756e746572446566696e6974696f6e2e76310000000003652d31"
                + "0000000100000000";
            Assert.That(ToHex(EncounterCatalogCanonicalV1.EncodeDefinition(definition)),
                Is.EqualTo(expectedDefinitionBytes));
            Assert.That(EncounterCatalogCanonicalV1.ComputeDefinitionSha256(definition),
                Is.EqualTo("675ffc8007e9e44e902397d4075a2bed93fefd2b4870e4f67d7732cb54e55ac2"));
        }

        [Test]
        public void IndependentFullRecordVectorsCoverBindingVariantSpawnAndCacheFraming()
        {
            EncounterCatalogDescriptor room = Descriptor("room", EncounterCatalogDescriptorKind.Room);
            EncounterCatalogDescriptor enemy = Descriptor("enemy", EncounterCatalogDescriptorKind.Enemy, OneSha256);
            EncounterCatalogDescriptor cacheDescriptor = Descriptor("cache", EncounterCatalogDescriptorKind.Cache);
            EncounterCatalogVariant variant = Variant("v", Ref("room"),
                new[] { Wave("w", Spawn("s", Ref("enemy"))) }, Cache("cache-one", Ref("cache"), anchor: "chest"),
                new[] { "a" }, Array.Empty<string>(), 0);
            EncounterCatalogDefinition definition = Definition("enc", variant);
            EncounterCatalogBinding binding = Binding("t", EncounterCatalogBindingMode.Encounter,
                "enc", 1);
            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "c", 1,
                "encounter-spawn.v1", "r", 1, new[] { 1 },
                new[] { cacheDescriptor, enemy, room }, new[] { binding }, new[] { definition },
                out EncounterCatalogManifest manifest, out EncounterCatalogError error), Is.True, error.Message);

            const string expectedManifestBytes =
                "4c75636b626f756e642e456e636f756e746572436174616c6f672e76310000000001000000016300000001"
                + "00000012656e636f756e7465722d737061776e2e7631000000017200000001000000010000000100000003"
                + "0000000563616368650000000100000005636163686500000040303030303030303030303030303030303030"
                + "30303030303030303030303030303030303030303030303030303030303030303030303030303030303030"
                + "303000000005656e656d790000000100000005656e656d790000004031313131313131313131313131313131"
                + "31313131313131313131313131313131313131313131313131313131313131313131313131313131313131"
                + "3131313100000004726f6f6d0000000100000004726f6f6d0000004030303030303030303030303030303030"
                + "30303030303030303030303030303030303030303030303030303030303030303030303030303030303030"
                + "30300000000100000001000000017400000006636f6d62617400000009656e636f756e746572010000000365"
                + "6e63000000010000000100000003656e630000000100000001000000017600000001000000016100000000"
                + "00000000000004726f6f6d0000000100000001000000017700000001000000017300000005656e656d7900"
                + "000005656e656d790000000100000006616e63686f72000000066e6f726d616c0000000001000000096361"
                + "6368652d6f6e6500000005636163686500000001000000056368657374";
            Assert.That(ToHex(EncounterCatalogCanonicalV1.EncodeManifest(manifest)),
                Is.EqualTo(expectedManifestBytes));
            Assert.That(EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest),
                Is.EqualTo("d81be0c0173b35259d041fd5392d3414be819f1a7644cfa2b19c8668861b3e3b"));

            const string expectedDefinitionBytes =
                "4c75636b626f756e642e456e636f756e746572446566696e6974696f6e2e76310000000003656e63000000"
                + "01000000010000000176000000010000000161000000000000000000000004726f6f6d0000000100000001"
                + "000000017700000001000000017300000005656e656d7900000005656e656d790000000100000006616e63"
                + "686f72000000066e6f726d616c00000000010000000963616368652d6f6e65000000056361636865000000"
                + "01000000056368657374";
            Assert.That(ToHex(EncounterCatalogCanonicalV1.EncodeDefinition(definition)),
                Is.EqualTo(expectedDefinitionBytes));
            Assert.That(EncounterCatalogCanonicalV1.ComputeDefinitionSha256(definition),
                Is.EqualTo("5de9e8fac2fb4db58dd697a7e117eb17c3ccd3cfeaf914cba81e0696dd18ffb6"));
        }

        [Test]
        public void CanonicalMethodsRejectNullModels()
        {
            Assert.Throws<ArgumentNullException>(() => EncounterCatalogCanonicalV1.EncodeManifest(null));
            Assert.Throws<ArgumentNullException>(() => EncounterCatalogCanonicalV1.ComputeManifestSha256(null));
            Assert.Throws<ArgumentNullException>(() => EncounterCatalogCanonicalV1.EncodeDefinition(null));
            Assert.Throws<ArgumentNullException>(() => EncounterCatalogCanonicalV1.ComputeDefinitionSha256(null));
        }

        private static EncounterCatalogManifest RichManifest(bool reverseOuterCollections,
            bool reversePacts, bool reverseSpawns, bool reverseWaves)
        {
            EncounterCatalogDescriptor room = Descriptor("room", EncounterCatalogDescriptorKind.Room);
            EncounterCatalogDescriptor enemy = Descriptor("enemy", EncounterCatalogDescriptorKind.Enemy);
            EncounterCatalogDescriptor cacheDescriptor = Descriptor("cache", EncounterCatalogDescriptorKind.Cache);
            EncounterCatalogDescriptorRef roomRef = Ref("room");
            EncounterCatalogDescriptorRef enemyRef = Ref("enemy");
            EncounterCatalogDescriptorRef cacheRef = Ref("cache");

            EncounterCatalogSpawn spawnA = Spawn("spawn-a", enemyRef);
            EncounterCatalogSpawn spawnB = Spawn("spawn-b", enemyRef,
                ExpeditionEncounterRewardEnemyKind.Elite, 1);
            var firstWaveSpawns = reverseSpawns
                ? new[] { spawnB, spawnA }
                : new[] { spawnA, spawnB };
            EncounterCatalogWave waveA = Wave("wave-a", firstWaveSpawns);
            EncounterCatalogWave waveB = Wave("wave-b", Spawn("spawn-c", enemyRef));
            EncounterCatalogWave[] waves = reverseWaves
                ? new[] { waveB, waveA }
                : new[] { waveA, waveB };
            string[] requiredPacts = reversePacts
                ? new[] { "pact-z", "pact-a" }
                : new[] { "pact-a", "pact-z" };

            EncounterCatalogCache cache = Cache("cache-instance", cacheRef);
            EncounterCatalogVariant mainVariant = Variant("variant-main", roomRef, waves, cache,
                requiredPacts, new[] { "pact-forbidden" }, scheduledOrdinal: 1);
            EncounterCatalogVariant alternateVariant = Variant("variant-alt", roomRef,
                new[] { Wave("wave-empty") }, null, Array.Empty<string>(), Array.Empty<string>(), 0);
            EncounterCatalogDefinition mainDefinition = Definition("encounter-main",
                reverseOuterCollections
                    ? new[] { alternateVariant, mainVariant }
                    : new[] { mainVariant, alternateVariant });
            EncounterCatalogDefinition otherDefinition = Definition("encounter-alpha",
                Variant("variant-alpha", roomRef, new[] { Wave("wave-alpha") }, null,
                    Array.Empty<string>(), Array.Empty<string>(), 0));

            EncounterCatalogBinding encounterBinding = Binding("arena", EncounterCatalogBindingMode.Encounter,
                "encounter-main", 1, generation: 3);
            EncounterCatalogBinding noneBinding = Binding("exit", EncounterCatalogBindingMode.None,
                null, 0, nodeKind: ExpeditionRouteNodeKind.SafeExtraction, generation: 1);
            int[] generations = reverseOuterCollections ? new[] { 3, 1 } : new[] { 1, 3 };
            EncounterCatalogDescriptor[] descriptors = reverseOuterCollections
                ? new[] { cacheDescriptor, enemy, room }
                : new[] { room, enemy, cacheDescriptor };
            EncounterCatalogBinding[] bindings = reverseOuterCollections
                ? new[] { noneBinding, encounterBinding }
                : new[] { encounterBinding, noneBinding };
            EncounterCatalogDefinition[] definitions = reverseOuterCollections
                ? new[] { mainDefinition, otherDefinition }
                : new[] { otherDefinition, mainDefinition };

            Assert.That(EncounterCatalogFactoryV1.TryCreateManifest(1, "catalog-fixture", 1,
                "encounter-spawn.v1", "resolver-fixture", 1, generations,
                descriptors, bindings, definitions, out EncounterCatalogManifest manifest,
                out EncounterCatalogError error), Is.True, error.Message);
            return manifest;
        }

        private static EncounterCatalogDescriptor Descriptor(string id, EncounterCatalogDescriptorKind kind,
            string sha256 = ZeroSha256)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor(id, 1, kind, sha256,
                out EncounterCatalogDescriptor value, out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogDescriptorRef Ref(string id, int revision = 1)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptorRef(id, revision,
                out EncounterCatalogDescriptorRef value, out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogSpawn Spawn(string id, EncounterCatalogDescriptorRef enemyRef,
            ExpeditionEncounterRewardEnemyKind kind = ExpeditionEncounterRewardEnemyKind.Normal,
            int vinculoOrdinal = 0)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateSpawn(id, "enemy", enemyRef,
                "anchor", kind, vinculoOrdinal, out EncounterCatalogSpawn value,
                out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogWave Wave(string id, params EncounterCatalogSpawn[] spawns)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateWave(id, spawns,
                out EncounterCatalogWave value, out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogCache Cache(string id, EncounterCatalogDescriptorRef cacheRef,
            string anchor = "cache-anchor")
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateCache(id, cacheRef, anchor,
                out EncounterCatalogCache value, out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogVariant Variant(string id, EncounterCatalogDescriptorRef roomRef,
            IEnumerable<EncounterCatalogWave> waves, EncounterCatalogCache cache,
            IEnumerable<string> required = null, IEnumerable<string> forbidden = null,
            int scheduledOrdinal = 0)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateVariant(id,
                required ?? Array.Empty<string>(), forbidden ?? Array.Empty<string>(),
                scheduledOrdinal, roomRef, waves, cache, out EncounterCatalogVariant value,
                out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogDefinition Definition(string id,
            params EncounterCatalogVariant[] variants)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateDefinition(id, 1, variants,
                out EncounterCatalogDefinition value, out EncounterCatalogError error), Is.True, error.Message);
            return value;
        }

        private static EncounterCatalogBinding Binding(string templateId,
            EncounterCatalogBindingMode mode, string encounterId, int definitionRevision,
            int generation = 1, ExpeditionRouteNodeKind nodeKind = ExpeditionRouteNodeKind.Combat)
        {
            Assert.That(EncounterCatalogFactoryV1.TryCreateBinding(generation, templateId, nodeKind,
                mode, encounterId, definitionRevision, out EncounterCatalogBinding value,
                out EncounterCatalogError error), Is.True, error.Message);
            return value;
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
