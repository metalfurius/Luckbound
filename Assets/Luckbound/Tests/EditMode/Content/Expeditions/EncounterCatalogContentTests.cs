using System;
using System.Collections.Generic;
using System.Text;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using NUnit.Framework;

namespace Luckbound.Tests.Content.Route
{
    public sealed class EncounterCatalogContentTests
    {
        [Test]
        public void StrictLoaderBuildsCoreModelsAndCanonicalizesPropertyOrderWhitespaceAndUtf8()
        {
            string firstJson = ValidJson(resolverId: "resolver.café");
            string reorderedJson = firstJson.Replace(
                "\"schemaVersion\":1,\"catalogId\":\"catalog.synthetic\"",
                "\"catalogId\":\"catalog.synthetic\",\"schemaVersion\":1")
                .Replace(",", ", \n").Replace(":", ":  ");
            byte[] originalBytes = Utf8(firstJson);
            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(originalBytes,
                out EncounterCatalogManifest first, out EncounterCatalogError firstError), Is.True,
                firstError.Message);
            originalBytes[0] = (byte)'[';
            Assert.That(first.CatalogId, Is.EqualTo("catalog.synthetic"));

            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(Utf8(reorderedJson),
                out EncounterCatalogManifest reordered, out EncounterCatalogError reorderedError), Is.True,
                reorderedError.Message);
            string firstDigest = EncounterCatalogCanonicalV1.ComputeManifestSha256(first);
            string reorderedDigest = EncounterCatalogCanonicalV1.ComputeManifestSha256(reordered);
            Assert.That(firstDigest, Is.EqualTo(reorderedDigest));
            CollectionAssert.AreEqual(EncounterCatalogCanonicalV1.EncodeManifest(first),
                EncounterCatalogCanonicalV1.EncodeManifest(reordered));
            string firstDefinitionDigest = EncounterCatalogCanonicalV1.ComputeDefinitionSha256(first.Definitions[0]);
            string reorderedDefinitionDigest = EncounterCatalogCanonicalV1.ComputeDefinitionSha256(reordered.Definitions[0]);
            Assert.That(firstDefinitionDigest, Is.EqualTo(reorderedDefinitionDigest));
            CollectionAssert.AreEqual(EncounterCatalogCanonicalV1.EncodeDefinition(first.Definitions[0]),
                EncounterCatalogCanonicalV1.EncodeDefinition(reordered.Definitions[0]));
            Assert.That(first.DescriptorRefs.Count, Is.EqualTo(3));
            Assert.That(first.Definitions[0].Variants[0].Cache.CacheId, Is.EqualTo("cache.synthetic"));
            Assert.That(first.IdentityCodec, Is.EqualTo("encounter-spawn.v1"));
            Assert.That(firstError.Code, Is.EqualTo(EncounterCatalogErrorCode.None));
        }

        [Test]
        public void StrictReaderRejectsInvalidUtf8MalformedDuplicateUnknownMissingAndTrailingJson()
        {
            AssertRejected(ValidJson().Replace("\"catalogId\":\"catalog.synthetic\"",
                "\"catalogId\":\"catalog.synthetic\",\"catalogId\":\"catalog.duplicate\""),
                EncounterCatalogErrorCode.DuplicateMember);
            AssertRejected(ValidJson().Replace("\"schemaVersion\":1,",
                "\"schemaVersion\":1,\"unexpected\":true,"),
                EncounterCatalogErrorCode.UnknownMember);
            AssertRejected(ValidJson().Replace("\"resolverId\":\"resolver.synthetic\",", ""),
                EncounterCatalogErrorCode.MissingMember);
            AssertRejected(ValidJson().Replace(",\"cache\":{\"cacheId\":\"cache.synthetic\",\"cacheDescriptorRef\":{\"descriptorId\":\"cache.synthetic\",\"revision\":1},\"anchorId\":\"anchor.cache\"}", ""),
                EncounterCatalogErrorCode.MissingMember);
            AssertRejected(ValidJson() + " {}", EncounterCatalogErrorCode.TrailingJson);
            AssertRejected("{]", EncounterCatalogErrorCode.MalformedJson);

            byte[] invalidUtf8 = { (byte)'{', (byte)0xc3, (byte)'}' };
            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(invalidUtf8,
                out EncounterCatalogManifest manifest, out EncounterCatalogError error), Is.False);
            Assert.That(manifest, Is.Null);
            Assert.That(error.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidUtf8));
        }

        [Test]
        public void StrictReaderRejectsEscapedDuplicateNestedMembersAndInvalidStringEscapes()
        {
            string escapedDuplicateCatalogId = ValidJson().Replace(
                "\"catalogId\":\"catalog.synthetic\"",
                "\"catalogId\":\"catalog.synthetic\",\"\\u0063atalogId\":\"catalog.escaped\"");
            AssertRejected(escapedDuplicateCatalogId, EncounterCatalogErrorCode.DuplicateMember,
                "$.catalogId");

            string unknownDescriptorMember = ValidJson().Replace(
                "\"descriptorId\":\"room.synthetic\",\"revision\":1,\"kind\":\"room\"",
                "\"descriptorId\":\"room.synthetic\",\"unexpectedNested\":true,\"revision\":1,\"kind\":\"room\"");
            AssertRejected(unknownDescriptorMember, EncounterCatalogErrorCode.UnknownMember,
                "$.descriptorRefs[0].unexpectedNested");

            string duplicateSpawnMember = ValidJson().Replace(
                "\"spawnId\":\"spawn.a\"",
                "\"spawnId\":\"spawn.a\",\"spawnId\":\"spawn.duplicate\"");
            AssertRejected(duplicateSpawnMember, EncounterCatalogErrorCode.DuplicateMember,
                "$.definitions[0].variants[0].waves[0].spawns[0].spawnId");

            string invalidEscape = ValidJson().Replace(
                "\"resolverId\":\"resolver.synthetic\"",
                "\"resolverId\":\"resolver" + ((char)92) + "q.synthetic\"");
            AssertRejected(invalidEscape, EncounterCatalogErrorCode.MalformedJson,
                "$.resolverId");
        }

        [Test]
        public void StrictLoaderEnforcesExactJsonTypesAndInt32Range()
        {
            AssertRejected(ValidJson().Replace("\"catalogRevision\":1", "\"catalogRevision\":\"1\""),
                EncounterCatalogErrorCode.WrongJsonType);
            AssertRejected(ValidJson().Replace("\"catalogRevision\":1", "\"catalogRevision\":1.0"),
                EncounterCatalogErrorCode.WrongJsonType);
            AssertRejected(ValidJson().Replace("\"catalogRevision\":1", "\"catalogRevision\":2147483648"),
                EncounterCatalogErrorCode.SizeOverflow);
            AssertRejected(ValidJson().Replace("\"supportedRouteGenerationVersions\":[1]",
                "\"supportedRouteGenerationVersions\":null"),
                EncounterCatalogErrorCode.WrongJsonType);
            AssertRejected(ValidJson().Replace("\"cache\":{\"cacheId\":\"cache.synthetic\",\"cacheDescriptorRef\":{\"descriptorId\":\"cache.synthetic\",\"revision\":1},\"anchorId\":\"anchor.cache\"}",
                "\"cache\":false"), EncounterCatalogErrorCode.WrongJsonType);
            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(null,
                out EncounterCatalogManifest manifest, out EncounterCatalogError error), Is.False);
            Assert.That(error.Code, Is.EqualTo(EncounterCatalogErrorCode.NullInput));
            Assert.That(manifest, Is.Null);
        }

        [Test]
        public void LoaderDelegatesSchemaTokensAndStructuralValidationToCoreFactories()
        {
            AssertRejected(ValidJson().Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
                EncounterCatalogErrorCode.InvalidSchema);
            AssertRejected(ValidJson().Replace("encounter-spawn.v1", "encounter-spawn.v2"),
                EncounterCatalogErrorCode.UnsupportedIdentityCodec);
            AssertRejected(ValidJson().Replace("\"nodeKind\":\"combat\"", "\"nodeKind\":\"Combat\""),
                EncounterCatalogErrorCode.InvalidValue);
            AssertRejected(ValidJson().Replace("\"rewardKind\":\"normal\"", "\"rewardKind\":\"Normal\""),
                EncounterCatalogErrorCode.InvalidValue);

            string badDigest = ValidJson(roomHash: new string('A', 64));
            AssertRejected(badDigest, EncounterCatalogErrorCode.InvalidValue,
                "$.descriptorRefs[0].sha256");
            string missingDescriptor = ValidJson().Replace(
                "\"enemyDescriptorRef\":{\"descriptorId\":\"enemy.synthetic\"",
                "\"enemyDescriptorRef\":{\"descriptorId\":\"missing.synthetic\"");
            AssertRejected(missingDescriptor, EncounterCatalogErrorCode.MissingReference);
            string wrongDescriptorKind = ValidJson().Replace(
                "\"enemyDescriptorRef\":{\"descriptorId\":\"enemy.synthetic\"",
                "\"enemyDescriptorRef\":{\"descriptorId\":\"room.synthetic\"");
            AssertRejected(wrongDescriptorKind, EncounterCatalogErrorCode.ReferenceKindMismatch);
            string duplicateDescriptor = "{\"descriptorId\":\"room.synthetic\",\"revision\":1,"
                + "\"kind\":\"room\",\"sha256\":\"" + new string('a', 64) + "\"}";
            AssertRejected(ValidJson().Replace("\"descriptorRefs\":[",
                "\"descriptorRefs\":[" + duplicateDescriptor + ","),
                EncounterCatalogErrorCode.DuplicateIdentifier);
        }

        [Test]
        public void BindingNoneOmitsEncounterFieldsAndCacheMayBeExplicitlyNull()
        {
            Assert.That(EncounterCatalogJsonLoaderV1.TryLoad(Utf8(ValidJson(bindingNone: true,
                cacheNull: true)), out EncounterCatalogManifest manifest,
                out EncounterCatalogError error), Is.True, error.Message);
            Assert.That(manifest.Bindings[0].Mode, Is.EqualTo(EncounterCatalogBindingMode.None));
            Assert.That(manifest.Bindings[0].EncounterId, Is.Null);
            Assert.That(manifest.Bindings[0].DefinitionRevision, Is.Zero);
            Assert.That(manifest.Definitions[0].Variants[0].Cache, Is.Null);

            string presentNoneFields = ValidJson(bindingNone: true).Replace("\"mode\":\"none\"",
                "\"mode\":\"none\",\"encounterId\":null");
            AssertRejected(presentNoneFields, EncounterCatalogErrorCode.InvalidValue,
                "$.bindings[0].encounterId");
            string missingEncounterRevision = ValidJson().Replace(",\"definitionRevision\":1}", "}");
            AssertRejected(missingEncounterRevision, EncounterCatalogErrorCode.MissingMember,
                "$.bindings[0].definitionRevision");
        }

        [Test]
        public void CoreCollectionsRemainReadOnlyAfterLoading()
        {
            EncounterCatalogManifest manifest = Load(ValidJson());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<EncounterCatalogDescriptor>)manifest.DescriptorRefs)[0] = null);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<EncounterCatalogWave>)manifest.Definitions[0].Variants[0].Waves).Clear());
        }

        [Test]
        public void RegistryRetainsExactRevisionsIsIdempotentAndRejectsConflictsAndFallbacks()
        {
            EncounterCatalogManifest revisionOne = Load(ValidJson());
            EncounterCatalogManifest revisionTwo = Load(ValidJson(revision: 2));
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { revisionOne, revisionTwo },
                out EncounterCatalogRegistryV1 registry, out EncounterCatalogError createError), Is.True,
                createError.Message);

            string digestOne = EncounterCatalogCanonicalV1.ComputeManifestSha256(revisionOne);
            string digestTwo = EncounterCatalogCanonicalV1.ComputeManifestSha256(revisionTwo);
            Assert.That(registry.TryGetExact("catalog.synthetic", 1, digestOne,
                out EncounterCatalogManifest foundOne, out EncounterCatalogError getOneError), Is.True,
                getOneError.Message);
            Assert.That(foundOne, Is.SameAs(revisionOne));
            Assert.That(registry.TryGetExact("catalog.synthetic", 2, digestTwo,
                out EncounterCatalogManifest foundTwo, out EncounterCatalogError getTwoError), Is.True,
                getTwoError.Message);
            Assert.That(foundTwo, Is.SameAs(revisionTwo));
            Assert.That(registry.TryGetExact("catalog.synthetic", 3, digestTwo,
                out _, out EncounterCatalogError unavailable), Is.False);
            Assert.That(unavailable.Code, Is.EqualTo(EncounterCatalogErrorCode.CatalogRevisionUnavailable));
            Assert.That(registry.TryGetExact("catalog.synthetic", 1, digestTwo,
                out _, out EncounterCatalogError wrongDigest), Is.False);
            Assert.That(wrongDigest.Code, Is.EqualTo(EncounterCatalogErrorCode.CatalogRevisionUnavailable));

            EncounterCatalogManifest[] originalCollection = { revisionOne };
            Assert.That(EncounterCatalogRegistryV1.TryCreate(originalCollection,
                out EncounterCatalogRegistryV1 copiedRegistry, out _), Is.True);
            originalCollection[0] = null;
            Assert.That(copiedRegistry.TryGetExact("catalog.synthetic", 1, digestOne,
                out EncounterCatalogManifest copied, out _), Is.True);
            Assert.That(copied, Is.SameAs(revisionOne));

            string equivalentJson = ValidJson().Replace(
                "\"schemaVersion\":1,\"catalogId\":\"catalog.synthetic\"",
                "\"catalogId\":\"catalog.synthetic\",\"schemaVersion\":1")
                .Replace(",", ", ").Replace(":", ": ");
            EncounterCatalogManifest equivalent = Load(equivalentJson);
            Assert.That(equivalent, Is.Not.SameAs(revisionOne));
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { revisionOne, equivalent },
                out _, out EncounterCatalogError idempotentError), Is.True, idempotentError.Message);
            EncounterCatalogManifest conflict = Load(ValidJson(resolverId: "resolver.changed"));
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { revisionOne, conflict },
                out EncounterCatalogRegistryV1 conflicted, out EncounterCatalogError conflictError), Is.False);
            Assert.That(conflicted, Is.Null);
            Assert.That(conflictError.Code, Is.EqualTo(EncounterCatalogErrorCode.CatalogRevisionConflict));
        }

        [Test]
        public void RegistryRejectsMalformedLookupAndNullManifestInputs()
        {
            EncounterCatalogManifest manifest = Load(ValidJson());
            Assert.That(EncounterCatalogRegistryV1.TryCreate(null,
                out EncounterCatalogRegistryV1 nullRegistry, out EncounterCatalogError nullInput), Is.False);
            Assert.That(nullRegistry, Is.Null);
            Assert.That(nullInput.Code, Is.EqualTo(EncounterCatalogErrorCode.NullInput));
            Assert.That(EncounterCatalogRegistryV1.TryCreate(new EncounterCatalogManifest[] { null },
                out _, out EncounterCatalogError nullEntry), Is.False);
            Assert.That(nullEntry.Code, Is.EqualTo(EncounterCatalogErrorCode.NullInput));

            Assert.That(EncounterCatalogRegistryV1.TryCreate(new[] { manifest },
                out EncounterCatalogRegistryV1 registry, out _), Is.True);
            Assert.That(registry.TryGetExact("catalog.synthetic", 0,
                EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest), out _,
                out EncounterCatalogError invalidRevision), Is.False);
            Assert.That(invalidRevision.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));
            Assert.That(registry.TryGetExact("catalog.synthetic", 1, new string('A', 64),
                out _, out EncounterCatalogError invalidDigest), Is.False);
            Assert.That(invalidDigest.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));
            Assert.That(registry.TryGetExact("catalog.synthetic", 1,
                EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest).ToUpperInvariant(),
                out _, out invalidDigest), Is.False);
            Assert.That(invalidDigest.Code, Is.EqualTo(EncounterCatalogErrorCode.InvalidValue));
        }

        private static void AssertRejected(string json, EncounterCatalogErrorCode expectedCode,
            string expectedPath = null)
        {
            bool loaded = EncounterCatalogJsonLoaderV1.TryLoad(Utf8(json),
                out EncounterCatalogManifest manifest, out EncounterCatalogError error);
            Assert.That(loaded, Is.False, json);
            Assert.That(manifest, Is.Null);
            Assert.That(error.Code, Is.EqualTo(expectedCode), json);
            if (expectedPath != null) Assert.That(error.Path, Is.EqualTo(expectedPath), json);
        }

        private static EncounterCatalogManifest Load(string json)
        {
            bool loaded = EncounterCatalogJsonLoaderV1.TryLoad(Utf8(json),
                out EncounterCatalogManifest manifest, out EncounterCatalogError error);
            Assert.That(loaded, Is.True, error.Message);
            return manifest;
        }

        private static byte[] Utf8(string text)
        {
            return new UTF8Encoding(false, true).GetBytes(text);
        }

        private static string ValidJson(int revision = 1, string resolverId = "resolver.synthetic",
            string identityCodec = "encounter-spawn.v1", bool bindingNone = false,
            bool cacheNull = false, string roomHash = null)
        {
            string bindingTail = bindingNone
                ? "\"mode\":\"none\""
                : "\"mode\":\"encounter\",\"encounterId\":\"encounter.synthetic\",\"definitionRevision\":1";
            string cache = cacheNull ? "null"
                : "{\"cacheId\":\"cache.synthetic\",\"cacheDescriptorRef\":{\"descriptorId\":\"cache.synthetic\",\"revision\":1},\"anchorId\":\"anchor.cache\"}";
            string roomSha = roomHash ?? new string('a', 64);
            string enemySha = new string('b', 64);
            string cacheSha = new string('c', 64);

            return "{"
                + "\"schemaVersion\":1,"
                + "\"catalogId\":\"catalog.synthetic\","
                + "\"catalogRevision\":" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + ","
                + "\"identityCodec\":\"" + identityCodec + "\","
                + "\"resolverId\":\"" + resolverId + "\","
                + "\"resolverRevision\":1,"
                + "\"supportedRouteGenerationVersions\":[1],"
                + "\"descriptorRefs\":["
                    + "{\"descriptorId\":\"room.synthetic\",\"revision\":1,\"kind\":\"room\",\"sha256\":\"" + roomSha + "\"},"
                    + "{\"descriptorId\":\"enemy.synthetic\",\"revision\":1,\"kind\":\"enemy\",\"sha256\":\"" + enemySha + "\"},"
                    + "{\"descriptorId\":\"cache.synthetic\",\"revision\":1,\"kind\":\"cache\",\"sha256\":\"" + cacheSha + "\"}],"
                + "\"bindings\":[{\"generationVersion\":1,\"templateId\":\"template.synthetic\",\"nodeKind\":\"combat\","
                    + bindingTail + "}],"
                + "\"definitions\":[{\"encounterId\":\"encounter.synthetic\",\"definitionRevision\":1,\"variants\":[{"
                    + "\"variantId\":\"variant.synthetic\",\"requiredPactIds\":[],\"forbiddenPactIds\":[],"
                    + "\"scheduledVinculoOrdinal\":0,\"roomDescriptorRef\":{\"descriptorId\":\"room.synthetic\",\"revision\":1},"
                    + "\"waves\":[{\"waveId\":\"wave.synthetic\",\"spawns\":[{\"spawnId\":\"spawn.a\","
                        + "\"enemyId\":\"enemy.synthetic\",\"enemyDescriptorRef\":{\"descriptorId\":\"enemy.synthetic\",\"revision\":1},"
                        + "\"spawnAnchorId\":\"anchor.spawn\",\"rewardKind\":\"normal\",\"vinculoSequenceOrdinal\":0}]}],"
                    + "\"cache\":" + cache + "}]}]}";
        }
    }
}
