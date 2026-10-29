using System;
using System.Collections.Generic;
using System.Text;

namespace Luckbound.Core.Expeditions
{
    public static class EncounterCatalogFactoryV1
    {
        private const int CurrentSchemaVersion = 1;
        private const string SupportedIdentityCodec = "encounter-spawn.v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        public static bool TryCreateDescriptorRef(string descriptorId, int revision,
            out EncounterCatalogDescriptorRef value, out EncounterCatalogError error)
        {
            value = null;
            if (!ValidateStableText(descriptorId, "descriptorId", out error)) return false;
            if (revision <= 0)
                return Fail("revision", EncounterCatalogErrorCode.InvalidValue,
                    "Descriptor revision must be positive.", out value, out error);

            value = new EncounterCatalogDescriptorRef(descriptorId, revision);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateDescriptor(string descriptorId, int revision,
            EncounterCatalogDescriptorKind kind, string sha256,
            out EncounterCatalogDescriptor value, out EncounterCatalogError error)
        {
            value = null;
            if (!ValidateStableText(descriptorId, "descriptorId", out error)) return false;
            if (revision <= 0)
                return Fail("revision", EncounterCatalogErrorCode.InvalidValue,
                    "Descriptor revision must be positive.", out value, out error);
            if (!Enum.IsDefined(typeof(EncounterCatalogDescriptorKind), kind))
                return Fail("kind", EncounterCatalogErrorCode.InvalidValue,
                    "Descriptor kind is not supported by schema 1.", out value, out error);
            if (!IsLowerSha256(sha256))
                return Fail("sha256", EncounterCatalogErrorCode.InvalidValue,
                    "SHA-256 must be 64 lowercase hexadecimal characters.", out value, out error);

            value = new EncounterCatalogDescriptor(descriptorId, revision, kind, sha256);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateSpawn(string spawnId, string enemyId,
            EncounterCatalogDescriptorRef enemyDescriptorRef, string spawnAnchorId,
            ExpeditionEncounterRewardEnemyKind rewardKind, int vinculoSequenceOrdinal,
            out EncounterCatalogSpawn value, out EncounterCatalogError error)
        {
            value = null;
            if (!IsSpawnId(spawnId))
                return Fail("spawnId", EncounterCatalogErrorCode.InvalidValue,
                    "Spawn ID must use ASCII letters, digits, dot, underscore, or hyphen.",
                    out value, out error);
            if (!ValidateStableText(enemyId, "enemyId", out error)) return false;
            if (enemyDescriptorRef == null)
                return Fail("enemyDescriptorRef", EncounterCatalogErrorCode.NullInput,
                    "Enemy descriptor reference is required.", out value, out error);
            if (!ValidateStableText(spawnAnchorId, "spawnAnchorId", out error)) return false;
            if (!Enum.IsDefined(typeof(ExpeditionEncounterRewardEnemyKind), rewardKind))
                return Fail("rewardKind", EncounterCatalogErrorCode.InvalidValue,
                    "Reward kind is not supported.", out value, out error);
            if (vinculoSequenceOrdinal < 0 || vinculoSequenceOrdinal > 3)
                return Fail("vinculoSequenceOrdinal", EncounterCatalogErrorCode.InvalidValue,
                    "Vínculo sequence ordinal must be between zero and three.", out value, out error);
            if (vinculoSequenceOrdinal > 0
                && rewardKind != ExpeditionEncounterRewardEnemyKind.Elite)
                return Fail("vinculoSequenceOrdinal", EncounterCatalogErrorCode.InvalidValue,
                    "Only an elite spawn can carry a Vínculo sequence ordinal.", out value, out error);

            value = new EncounterCatalogSpawn(spawnId, enemyId, enemyDescriptorRef,
                spawnAnchorId, rewardKind, vinculoSequenceOrdinal);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateWave(string waveId, IEnumerable<EncounterCatalogSpawn> spawns,
            out EncounterCatalogWave value, out EncounterCatalogError error)
        {
            value = null;
            if (!ValidateStableText(waveId, "waveId", out error)) return false;
            if (spawns == null)
                return Fail("spawns", EncounterCatalogErrorCode.NullInput,
                    "Spawn collection is required.", out value, out error);

            EncounterCatalogSpawn[] copy = new List<EncounterCatalogSpawn>(spawns).ToArray();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Length; index++)
            {
                if (copy[index] == null)
                    return Fail("spawns[" + index + "]", EncounterCatalogErrorCode.NullInput,
                        "Spawn entries cannot be null.", out value, out error);
                if (!identifiers.Add(copy[index].SpawnId))
                    return Fail("spawns[" + index + "].spawnId", EncounterCatalogErrorCode.DuplicateIdentifier,
                        "Spawn IDs must be unique within a wave.", out value, out error);
            }

            value = new EncounterCatalogWave(waveId, copy);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateCache(string cacheId,
            EncounterCatalogDescriptorRef cacheDescriptorRef, string anchorId,
            out EncounterCatalogCache value, out EncounterCatalogError error)
        {
            value = null;
            if (!ValidateStableText(cacheId, "cacheId", out error)) return false;
            if (cacheDescriptorRef == null)
                return Fail("cacheDescriptorRef", EncounterCatalogErrorCode.NullInput,
                    "Cache descriptor reference is required.", out value, out error);
            if (!ValidateStableText(anchorId, "anchorId", out error)) return false;

            value = new EncounterCatalogCache(cacheId, cacheDescriptorRef, anchorId);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateVariant(string variantId,
            IEnumerable<string> requiredPactIds, IEnumerable<string> forbiddenPactIds,
            int scheduledVinculoOrdinal, EncounterCatalogDescriptorRef roomDescriptorRef,
            IEnumerable<EncounterCatalogWave> waves, EncounterCatalogCache cache,
            out EncounterCatalogVariant value, out EncounterCatalogError error)
        {
            value = null;
            if (!ValidateStableText(variantId, "variantId", out error)) return false;
            string[] required;
            if (!CopyStableStringSet(requiredPactIds, "requiredPactIds", out required, out error)) return false;
            string[] forbidden;
            if (!CopyStableStringSet(forbiddenPactIds, "forbiddenPactIds", out forbidden, out error)) return false;
            var requiredSet = new HashSet<string>(required, StringComparer.Ordinal);
            for (int index = 0; index < forbidden.Length; index++)
            {
                if (requiredSet.Contains(forbidden[index]))
                    return Fail("forbiddenPactIds[" + index + "]", EncounterCatalogErrorCode.InvalidValue,
                        "A pact cannot be both required and forbidden.", out value, out error);
            }
            if (scheduledVinculoOrdinal < 0 || scheduledVinculoOrdinal > 3)
                return Fail("scheduledVinculoOrdinal", EncounterCatalogErrorCode.InvalidValue,
                    "Scheduled Vínculo ordinal must be between zero and three.", out value, out error);
            if (roomDescriptorRef == null)
                return Fail("roomDescriptorRef", EncounterCatalogErrorCode.NullInput,
                    "Room descriptor reference is required.", out value, out error);
            if (waves == null)
                return Fail("waves", EncounterCatalogErrorCode.NullInput,
                    "Wave collection is required.", out value, out error);

            EncounterCatalogWave[] waveCopy = new List<EncounterCatalogWave>(waves).ToArray();
            var waveIds = new HashSet<string>(StringComparer.Ordinal);
            var spawnIds = new HashSet<string>(StringComparer.Ordinal);
            var vinculoOrdinals = new HashSet<int>();
            for (int waveIndex = 0; waveIndex < waveCopy.Length; waveIndex++)
            {
                EncounterCatalogWave wave = waveCopy[waveIndex];
                if (wave == null)
                    return Fail("waves[" + waveIndex + "]", EncounterCatalogErrorCode.NullInput,
                        "Wave entries cannot be null.", out value, out error);
                if (!waveIds.Add(wave.WaveId))
                    return Fail("waves[" + waveIndex + "].waveId", EncounterCatalogErrorCode.DuplicateIdentifier,
                        "Wave IDs must be unique within a variant.", out value, out error);
                for (int spawnIndex = 0; spawnIndex < wave.Spawns.Count; spawnIndex++)
                {
                    EncounterCatalogSpawn spawn = wave.Spawns[spawnIndex];
                    if (spawn == null)
                        return Fail("waves[" + waveIndex + "].spawns[" + spawnIndex + "]",
                            EncounterCatalogErrorCode.NullInput, "Spawn entries cannot be null.", out value, out error);
                    if (!spawnIds.Add(spawn.SpawnId))
                        return Fail("waves[" + waveIndex + "].spawns[" + spawnIndex + "].spawnId",
                            EncounterCatalogErrorCode.DuplicateIdentifier,
                            "Spawn IDs must be unique across all waves in a variant.", out value, out error);
                    if (spawn.VinculoSequenceOrdinal > 0
                        && !vinculoOrdinals.Add(spawn.VinculoSequenceOrdinal))
                        return Fail("waves[" + waveIndex + "].spawns[" + spawnIndex + "].vinculoSequenceOrdinal",
                            EncounterCatalogErrorCode.DuplicateIdentifier,
                            "Positive Vínculo sequence ordinals must be unique within a variant.", out value, out error);
                }
            }

            value = new EncounterCatalogVariant(variantId, required, forbidden,
                scheduledVinculoOrdinal, roomDescriptorRef, waveCopy, cache);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateDefinition(string encounterId, int definitionRevision,
            IEnumerable<EncounterCatalogVariant> variants,
            out EncounterCatalogDefinition value, out EncounterCatalogError error)
        {
            value = null;
            if (!ValidateStableText(encounterId, "encounterId", out error)) return false;
            if (definitionRevision <= 0)
                return Fail("definitionRevision", EncounterCatalogErrorCode.InvalidValue,
                    "Definition revision must be positive.", out value, out error);
            if (variants == null)
                return Fail("variants", EncounterCatalogErrorCode.NullInput,
                    "Variant collection is required.", out value, out error);

            EncounterCatalogVariant[] copy = new List<EncounterCatalogVariant>(variants).ToArray();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Length; index++)
            {
                if (copy[index] == null)
                    return Fail("variants[" + index + "]", EncounterCatalogErrorCode.NullInput,
                        "Variant entries cannot be null.", out value, out error);
                if (!identifiers.Add(copy[index].VariantId))
                    return Fail("variants[" + index + "].variantId", EncounterCatalogErrorCode.DuplicateIdentifier,
                        "Variant IDs must be unique within a definition.", out value, out error);
            }

            value = new EncounterCatalogDefinition(encounterId, definitionRevision, copy);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateBinding(int generationVersion, string templateId,
            ExpeditionRouteNodeKind nodeKind, EncounterCatalogBindingMode mode,
            string encounterId, int definitionRevision,
            out EncounterCatalogBinding value, out EncounterCatalogError error)
        {
            value = null;
            if (generationVersion <= 0)
                return Fail("generationVersion", EncounterCatalogErrorCode.InvalidValue,
                    "Route generation version must be positive.", out value, out error);
            if (!ValidateStableText(templateId, "templateId", out error)) return false;
            if (!Enum.IsDefined(typeof(ExpeditionRouteNodeKind), nodeKind))
                return Fail("nodeKind", EncounterCatalogErrorCode.InvalidValue,
                    "Node kind is not supported.", out value, out error);
            if (!Enum.IsDefined(typeof(EncounterCatalogBindingMode), mode))
                return Fail("mode", EncounterCatalogErrorCode.InvalidValue,
                    "Binding mode is not supported.", out value, out error);
            if (mode == EncounterCatalogBindingMode.None)
            {
                if (encounterId != null || definitionRevision != 0)
                    return Fail("encounterId", EncounterCatalogErrorCode.InvalidValue,
                        "A None binding requires an absent encounter ID and revision zero.", out value, out error);
            }
            else
            {
                if (!ValidateStableText(encounterId, "encounterId", out error)) return false;
                if (definitionRevision <= 0)
                    return Fail("definitionRevision", EncounterCatalogErrorCode.InvalidValue,
                        "Encounter binding definition revision must be positive.", out value, out error);
            }

            value = new EncounterCatalogBinding(generationVersion, templateId, nodeKind,
                mode, encounterId, definitionRevision);
            error = default(EncounterCatalogError);
            return true;
        }

        public static bool TryCreateManifest(int schemaVersion, string catalogId,
            int catalogRevision, string identityCodec, string resolverId, int resolverRevision,
            IEnumerable<int> supportedRouteGenerationVersions,
            IEnumerable<EncounterCatalogDescriptor> descriptorRefs,
            IEnumerable<EncounterCatalogBinding> bindings,
            IEnumerable<EncounterCatalogDefinition> definitions,
            out EncounterCatalogManifest value, out EncounterCatalogError error)
        {
            value = null;
            if (schemaVersion != CurrentSchemaVersion)
                return Fail("schemaVersion", EncounterCatalogErrorCode.InvalidSchema,
                    "Only encounter catalog schema version 1 is supported.", out value, out error);
            if (!ValidateStableText(catalogId, "catalogId", out error)) return false;
            if (catalogRevision <= 0)
                return Fail("catalogRevision", EncounterCatalogErrorCode.InvalidValue,
                    "Catalog revision must be positive.", out value, out error);
            if (!ValidateStableText(identityCodec, "identityCodec", out error)) return false;
            if (!string.Equals(identityCodec, SupportedIdentityCodec, StringComparison.Ordinal))
                return Fail("identityCodec", EncounterCatalogErrorCode.UnsupportedIdentityCodec,
                    "Identity codec is not supported.", out value, out error);
            if (!ValidateStableText(resolverId, "resolverId", out error)) return false;
            if (resolverRevision <= 0)
                return Fail("resolverRevision", EncounterCatalogErrorCode.InvalidValue,
                    "Resolver revision must be positive.", out value, out error);
            if (supportedRouteGenerationVersions == null)
                return Fail("supportedRouteGenerationVersions", EncounterCatalogErrorCode.NullInput,
                    "Supported generation collection is required.", out value, out error);
            if (descriptorRefs == null)
                return Fail("descriptorRefs", EncounterCatalogErrorCode.NullInput,
                    "Descriptor collection is required.", out value, out error);
            if (bindings == null)
                return Fail("bindings", EncounterCatalogErrorCode.NullInput,
                    "Binding collection is required.", out value, out error);
            if (definitions == null)
                return Fail("definitions", EncounterCatalogErrorCode.NullInput,
                    "Definition collection is required.", out value, out error);

            int[] generationCopy = new List<int>(supportedRouteGenerationVersions).ToArray();
            EncounterCatalogDescriptor[] descriptorCopy = new List<EncounterCatalogDescriptor>(descriptorRefs).ToArray();
            EncounterCatalogBinding[] bindingCopy = new List<EncounterCatalogBinding>(bindings).ToArray();
            EncounterCatalogDefinition[] definitionCopy = new List<EncounterCatalogDefinition>(definitions).ToArray();

            var generationSet = new HashSet<int>();
            for (int index = 0; index < generationCopy.Length; index++)
            {
                if (generationCopy[index] <= 0)
                    return Fail("supportedRouteGenerationVersions[" + index + "]",
                        EncounterCatalogErrorCode.InvalidValue, "Supported route generation must be positive.",
                        out value, out error);
                if (!generationSet.Add(generationCopy[index]))
                    return Fail("supportedRouteGenerationVersions[" + index + "]",
                        EncounterCatalogErrorCode.DuplicateIdentifier, "Supported generations must be unique.",
                        out value, out error);
            }

            for (int index = 0; index < descriptorCopy.Length; index++)
            {
                EncounterCatalogDescriptor descriptor = descriptorCopy[index];
                if (descriptor == null)
                    return Fail("descriptorRefs[" + index + "]", EncounterCatalogErrorCode.NullInput,
                        "Descriptor entries cannot be null.", out value, out error);
                for (int previous = 0; previous < index; previous++)
                {
                    if (SameDescriptorRef(descriptor, descriptorCopy[previous]))
                        return Fail("descriptorRefs[" + index + "]", EncounterCatalogErrorCode.DuplicateIdentifier,
                            "Descriptor ID and revision pairs must be unique.", out value, out error);
                }
            }

            for (int index = 0; index < definitionCopy.Length; index++)
            {
                EncounterCatalogDefinition definition = definitionCopy[index];
                if (definition == null)
                    return Fail("definitions[" + index + "]", EncounterCatalogErrorCode.NullInput,
                        "Definition entries cannot be null.", out value, out error);
                for (int previous = 0; previous < index; previous++)
                {
                    if (SameDefinitionRef(definition, definitionCopy[previous]))
                        return Fail("definitions[" + index + "]", EncounterCatalogErrorCode.DuplicateIdentifier,
                            "Encounter ID and definition revision pairs must be unique.", out value, out error);
                }
            }

            for (int index = 0; index < bindingCopy.Length; index++)
            {
                EncounterCatalogBinding binding = bindingCopy[index];
                if (binding == null)
                    return Fail("bindings[" + index + "]", EncounterCatalogErrorCode.NullInput,
                        "Binding entries cannot be null.", out value, out error);
                if (!generationSet.Contains(binding.GenerationVersion))
                    return Fail("bindings[" + index + "].generationVersion",
                        EncounterCatalogErrorCode.MissingReference,
                        "Binding generation is not listed as supported by this manifest.", out value, out error);
                for (int previous = 0; previous < index; previous++)
                {
                    EncounterCatalogBinding other = bindingCopy[previous];
                    if (binding.GenerationVersion == other.GenerationVersion
                        && string.Equals(binding.TemplateId, other.TemplateId, StringComparison.Ordinal)
                        && binding.NodeKind == other.NodeKind)
                        return Fail("bindings[" + index + "]", EncounterCatalogErrorCode.DuplicateIdentifier,
                            "Generation, template, and node kind binding keys must be unique.", out value, out error);
                }
                if (binding.Mode == EncounterCatalogBindingMode.Encounter
                    && !ContainsDefinition(definitionCopy, binding.EncounterId, binding.DefinitionRevision))
                    return Fail("bindings[" + index + "].encounterId",
                        EncounterCatalogErrorCode.MissingReference,
                        "Encounter binding does not reference a manifest definition.", out value, out error);
            }

            for (int definitionIndex = 0; definitionIndex < definitionCopy.Length; definitionIndex++)
            {
                EncounterCatalogDefinition definition = definitionCopy[definitionIndex];
                for (int variantIndex = 0; variantIndex < definition.Variants.Count; variantIndex++)
                {
                    EncounterCatalogVariant variant = definition.Variants[variantIndex];
                    if (!ValidateDescriptorReference(descriptorCopy, variant.RoomDescriptorRef,
                        EncounterCatalogDescriptorKind.Room,
                        "definitions[" + definitionIndex + "].variants[" + variantIndex + "].roomDescriptorRef",
                        out error)) return false;
                    if (variant.Cache != null
                        && !ValidateDescriptorReference(descriptorCopy, variant.Cache.CacheDescriptorRef,
                            EncounterCatalogDescriptorKind.Cache,
                            "definitions[" + definitionIndex + "].variants[" + variantIndex + "].cache.cacheDescriptorRef",
                            out error)) return false;
                    for (int waveIndex = 0; waveIndex < variant.Waves.Count; waveIndex++)
                    {
                        EncounterCatalogWave wave = variant.Waves[waveIndex];
                        for (int spawnIndex = 0; spawnIndex < wave.Spawns.Count; spawnIndex++)
                        {
                            EncounterCatalogSpawn spawn = wave.Spawns[spawnIndex];
                            if (!ValidateDescriptorReference(descriptorCopy, spawn.EnemyDescriptorRef,
                                EncounterCatalogDescriptorKind.Enemy,
                                "definitions[" + definitionIndex + "].variants[" + variantIndex
                                    + "].waves[" + waveIndex + "].spawns[" + spawnIndex + "].enemyDescriptorRef",
                                out error)) return false;
                        }
                    }
                }
            }

            value = new EncounterCatalogManifest(schemaVersion, catalogId, catalogRevision,
                identityCodec, resolverId, resolverRevision, generationCopy,
                descriptorCopy, bindingCopy, definitionCopy);
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool ValidateDescriptorReference(EncounterCatalogDescriptor[] descriptors,
            EncounterCatalogDescriptorRef descriptorRef, EncounterCatalogDescriptorKind expectedKind,
            string path, out EncounterCatalogError error)
        {
            if (descriptorRef == null)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.MissingReference,
                    path, "Descriptor reference is required.");
                return false;
            }
            for (int index = 0; index < descriptors.Length; index++)
            {
                EncounterCatalogDescriptor descriptor = descriptors[index];
                if (descriptor.DescriptorId == descriptorRef.DescriptorId
                    && descriptor.Revision == descriptorRef.Revision)
                {
                    if (descriptor.Kind != expectedKind)
                    {
                        error = new EncounterCatalogError(EncounterCatalogErrorCode.ReferenceKindMismatch,
                            path, "Descriptor reference has a different descriptor kind.");
                        return false;
                    }
                    error = default(EncounterCatalogError);
                    return true;
                }
            }
            error = new EncounterCatalogError(EncounterCatalogErrorCode.MissingReference,
                path, "Descriptor reference is not present in this manifest.");
            return false;
        }

        private static bool ContainsDefinition(EncounterCatalogDefinition[] definitions,
            string encounterId, int revision)
        {
            for (int index = 0; index < definitions.Length; index++)
            {
                if (definitions[index].DefinitionRevision == revision
                    && string.Equals(definitions[index].EncounterId, encounterId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static bool SameDescriptorRef(EncounterCatalogDescriptor left,
            EncounterCatalogDescriptor right)
        {
            return right != null && left.Revision == right.Revision
                && string.Equals(left.DescriptorId, right.DescriptorId, StringComparison.Ordinal);
        }

        private static bool SameDefinitionRef(EncounterCatalogDefinition left,
            EncounterCatalogDefinition right)
        {
            return right != null && left.DefinitionRevision == right.DefinitionRevision
                && string.Equals(left.EncounterId, right.EncounterId, StringComparison.Ordinal);
        }

        private static bool CopyStableStringSet(IEnumerable<string> values, string path,
            out string[] copy, out EncounterCatalogError error)
        {
            copy = null;
            if (values == null)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.NullInput,
                    path, "String collection is required.");
                return false;
            }
            copy = new List<string>(values).ToArray();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < copy.Length; index++)
            {
                if (!ValidateStableText(copy[index], path + "[" + index + "]", out error)) return false;
                if (!identifiers.Add(copy[index]))
                {
                    error = new EncounterCatalogError(EncounterCatalogErrorCode.DuplicateIdentifier,
                        path + "[" + index + "]", "Identifiers in this set must be unique.");
                    return false;
                }
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool ValidateStableText(string value, string path, out EncounterCatalogError error)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidValue,
                    path, "Stable text must be non-empty and contain no whitespace.");
                return false;
            }
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsWhiteSpace(value[index]))
                {
                    error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidValue,
                        path, "Stable text must be non-empty and contain no whitespace.");
                    return false;
                }
            }
            try
            {
                int byteCount = StrictUtf8.GetByteCount(value);
                if ((ulong)byteCount > uint.MaxValue)
                {
                    error = new EncounterCatalogError(EncounterCatalogErrorCode.SizeOverflow,
                        path, "UTF-8 field exceeds the UInt32 framing limit.");
                    return false;
                }
            }
            catch (EncoderFallbackException)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidValue,
                    path, "Text contains an invalid UTF-16 sequence.");
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool IsSpawnId(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool allowed = (character >= 'A' && character <= 'Z')
                    || (character >= 'a' && character <= 'z')
                    || (character >= '0' && character <= '9')
                    || character == '.' || character == '_' || character == '-';
                if (!allowed) return false;
            }
            return true;
        }

        private static bool IsLowerSha256(string value)
        {
            if (value == null || value.Length != 64) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f'))) return false;
            }
            return true;
        }

        private static bool Fail<T>(string path, EncounterCatalogErrorCode code,
            string message, out T value, out EncounterCatalogError error) where T : class
        {
            value = null;
            error = new EncounterCatalogError(code, path, message);
            return false;
        }
    }
}
