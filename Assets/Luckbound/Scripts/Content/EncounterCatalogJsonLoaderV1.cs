using System;
using System.Collections.Generic;
using System.Globalization;
using Luckbound.Core.Expeditions;

namespace Luckbound.Content
{
    public static class EncounterCatalogJsonLoaderV1
    {
        private static readonly string[] ManifestMembers =
        {
            "schemaVersion", "catalogId", "catalogRevision", "identityCodec", "resolverId",
            "resolverRevision", "supportedRouteGenerationVersions", "descriptorRefs", "bindings",
            "definitions"
        };

        public static bool TryLoad(byte[] utf8, out EncounterCatalogManifest manifest,
            out EncounterCatalogError error)
        {
            manifest = null;
            EncounterCatalogJsonValueV1 root;
            if (!EncounterCatalogStrictJsonReaderV1.TryRead(utf8, out root, out error)) return false;

            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(root, "$", out members, out error)) return false;
            if (!CheckMembers(members, "$", ManifestMembers, ManifestMembers, out error)) return false;

            int schemaVersion;
            if (!TryReadInt(members["schemaVersion"], "$.schemaVersion", out schemaVersion, out error)) return false;
            string catalogId;
            if (!TryReadString(members["catalogId"], "$.catalogId", out catalogId, out error)) return false;
            int catalogRevision;
            if (!TryReadInt(members["catalogRevision"], "$.catalogRevision", out catalogRevision, out error)) return false;
            string identityCodec;
            if (!TryReadString(members["identityCodec"], "$.identityCodec", out identityCodec, out error)) return false;
            string resolverId;
            if (!TryReadString(members["resolverId"], "$.resolverId", out resolverId, out error)) return false;
            int resolverRevision;
            if (!TryReadInt(members["resolverRevision"], "$.resolverRevision", out resolverRevision, out error)) return false;

            List<int> supportedGenerations;
            if (!TryReadIntArray(members["supportedRouteGenerationVersions"],
                "$.supportedRouteGenerationVersions", out supportedGenerations, out error)) return false;
            List<EncounterCatalogDescriptor> descriptors;
            if (!TryReadDescriptorArray(members["descriptorRefs"], "$.descriptorRefs",
                out descriptors, out error)) return false;
            List<EncounterCatalogBinding> bindings;
            if (!TryReadBindingArray(members["bindings"], "$.bindings", out bindings, out error)) return false;
            List<EncounterCatalogDefinition> definitions;
            if (!TryReadDefinitionArray(members["definitions"], "$.definitions",
                out definitions, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateManifest(schemaVersion, catalogId,
                catalogRevision, identityCodec, resolverId, resolverRevision,
                supportedGenerations, descriptors, bindings, definitions,
                out manifest, out EncounterCatalogError coreError))
            {
                manifest = null;
                error = FromCoreError("$", coreError);
                return false;
            }

            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadDescriptorArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogDescriptor> descriptors, out EncounterCatalogError error)
        {
            descriptors = null;
            List<EncounterCatalogJsonValueV1> items;
            if (!TryReadArray(value, path, out items, out error)) return false;
            descriptors = new List<EncounterCatalogDescriptor>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                EncounterCatalogDescriptor descriptor;
                if (!TryReadDescriptor(items[index], IndexPath(path, index), out descriptor, out error))
                {
                    descriptors = null;
                    return false;
                }
                descriptors.Add(descriptor);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadDescriptor(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogDescriptor descriptor, out EncounterCatalogError error)
        {
            descriptor = null;
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            string[] names = { "descriptorId", "revision", "kind", "sha256" };
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string descriptorId;
            if (!TryReadString(members["descriptorId"], MemberPath(path, "descriptorId"),
                out descriptorId, out error)) return false;
            int revision;
            if (!TryReadInt(members["revision"], MemberPath(path, "revision"), out revision, out error)) return false;
            string kindToken;
            if (!TryReadString(members["kind"], MemberPath(path, "kind"), out kindToken, out error)) return false;
            if (!EncounterCatalogTokensV1.TryParseDescriptorKind(kindToken,
                out EncounterCatalogDescriptorKind kind))
                return Fail(EncounterCatalogErrorCode.InvalidValue, MemberPath(path, "kind"),
                    "Descriptor kind token is not supported by schema 1.", out error);
            string sha256;
            if (!TryReadString(members["sha256"], MemberPath(path, "sha256"), out sha256, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateDescriptor(descriptorId, revision, kind, sha256,
                out descriptor, out EncounterCatalogError coreError))
            {
                descriptor = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadBindingArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogBinding> bindings, out EncounterCatalogError error)
        {
            bindings = null;
            List<EncounterCatalogJsonValueV1> items;
            if (!TryReadArray(value, path, out items, out error)) return false;
            bindings = new List<EncounterCatalogBinding>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                EncounterCatalogBinding binding;
                if (!TryReadBinding(items[index], IndexPath(path, index), out binding, out error))
                {
                    bindings = null;
                    return false;
                }
                bindings.Add(binding);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadBinding(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogBinding binding, out EncounterCatalogError error)
        {
            binding = null;
            string[] allowed =
            {
                "generationVersion", "templateId", "nodeKind", "mode", "encounterId", "definitionRevision"
            };
            string[] required = { "generationVersion", "templateId", "nodeKind", "mode" };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, allowed, required, out error)) return false;

            int generationVersion;
            if (!TryReadInt(members["generationVersion"], MemberPath(path, "generationVersion"),
                out generationVersion, out error)) return false;
            string templateId;
            if (!TryReadString(members["templateId"], MemberPath(path, "templateId"), out templateId, out error)) return false;
            string nodeKindToken;
            if (!TryReadString(members["nodeKind"], MemberPath(path, "nodeKind"), out nodeKindToken, out error)) return false;
            if (!EncounterCatalogTokensV1.TryParseNodeKind(nodeKindToken,
                out ExpeditionRouteNodeKind nodeKind))
                return Fail(EncounterCatalogErrorCode.InvalidValue, MemberPath(path, "nodeKind"),
                    "Node kind token is not supported by schema 1.", out error);
            string modeToken;
            if (!TryReadString(members["mode"], MemberPath(path, "mode"), out modeToken, out error)) return false;
            if (!EncounterCatalogTokensV1.TryParseBindingMode(modeToken,
                out EncounterCatalogBindingMode mode))
                return Fail(EncounterCatalogErrorCode.InvalidValue, MemberPath(path, "mode"),
                    "Binding mode token is not supported by schema 1.", out error);

            string encounterId = null;
            int definitionRevision = 0;
            bool hasEncounterId = members.ContainsKey("encounterId");
            bool hasDefinitionRevision = members.ContainsKey("definitionRevision");
            if (mode == EncounterCatalogBindingMode.None)
            {
                if (hasEncounterId)
                    return Fail(EncounterCatalogErrorCode.InvalidValue, MemberPath(path, "encounterId"),
                        "A none binding must omit encounterId.", out error);
                if (hasDefinitionRevision)
                    return Fail(EncounterCatalogErrorCode.InvalidValue, MemberPath(path, "definitionRevision"),
                        "A none binding must omit definitionRevision.", out error);
            }
            else
            {
                if (!hasEncounterId)
                    return Fail(EncounterCatalogErrorCode.MissingMember, MemberPath(path, "encounterId"),
                        "An encounter binding requires encounterId.", out error);
                if (!hasDefinitionRevision)
                    return Fail(EncounterCatalogErrorCode.MissingMember, MemberPath(path, "definitionRevision"),
                        "An encounter binding requires definitionRevision.", out error);
                if (!TryReadString(members["encounterId"], MemberPath(path, "encounterId"),
                    out encounterId, out error)) return false;
                if (!TryReadInt(members["definitionRevision"], MemberPath(path, "definitionRevision"),
                    out definitionRevision, out error)) return false;
            }

            if (!EncounterCatalogFactoryV1.TryCreateBinding(generationVersion, templateId,
                nodeKind, mode, encounterId, definitionRevision, out binding,
                out EncounterCatalogError coreError))
            {
                binding = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadDefinitionArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogDefinition> definitions, out EncounterCatalogError error)
        {
            definitions = null;
            List<EncounterCatalogJsonValueV1> items;
            if (!TryReadArray(value, path, out items, out error)) return false;
            definitions = new List<EncounterCatalogDefinition>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                EncounterCatalogDefinition definition;
                if (!TryReadDefinition(items[index], IndexPath(path, index), out definition, out error))
                {
                    definitions = null;
                    return false;
                }
                definitions.Add(definition);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadDefinition(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogDefinition definition, out EncounterCatalogError error)
        {
            definition = null;
            string[] names = { "encounterId", "definitionRevision", "variants" };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string encounterId;
            if (!TryReadString(members["encounterId"], MemberPath(path, "encounterId"), out encounterId, out error)) return false;
            int revision;
            if (!TryReadInt(members["definitionRevision"], MemberPath(path, "definitionRevision"), out revision, out error)) return false;
            List<EncounterCatalogVariant> variants;
            if (!TryReadVariantArray(members["variants"], MemberPath(path, "variants"), out variants, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateDefinition(encounterId, revision, variants,
                out definition, out EncounterCatalogError coreError))
            {
                definition = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadVariantArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogVariant> variants, out EncounterCatalogError error)
        {
            variants = null;
            List<EncounterCatalogJsonValueV1> items;
            if (!TryReadArray(value, path, out items, out error)) return false;
            variants = new List<EncounterCatalogVariant>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                EncounterCatalogVariant variant;
                if (!TryReadVariant(items[index], IndexPath(path, index), out variant, out error))
                {
                    variants = null;
                    return false;
                }
                variants.Add(variant);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadVariant(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogVariant variant, out EncounterCatalogError error)
        {
            variant = null;
            string[] names =
            {
                "variantId", "requiredPactIds", "forbiddenPactIds", "scheduledVinculoOrdinal",
                "roomDescriptorRef", "waves", "cache"
            };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string variantId;
            if (!TryReadString(members["variantId"], MemberPath(path, "variantId"), out variantId, out error)) return false;
            List<string> requiredPacts;
            if (!TryReadStringArray(members["requiredPactIds"], MemberPath(path, "requiredPactIds"),
                out requiredPacts, out error)) return false;
            List<string> forbiddenPacts;
            if (!TryReadStringArray(members["forbiddenPactIds"], MemberPath(path, "forbiddenPactIds"),
                out forbiddenPacts, out error)) return false;
            int vinculoOrdinal;
            if (!TryReadInt(members["scheduledVinculoOrdinal"], MemberPath(path, "scheduledVinculoOrdinal"),
                out vinculoOrdinal, out error)) return false;
            EncounterCatalogDescriptorRef roomDescriptorRef;
            if (!TryReadDescriptorReference(members["roomDescriptorRef"], MemberPath(path, "roomDescriptorRef"),
                out roomDescriptorRef, out error)) return false;
            List<EncounterCatalogWave> waves;
            if (!TryReadWaveArray(members["waves"], MemberPath(path, "waves"), out waves, out error)) return false;

            EncounterCatalogCache cache = null;
            EncounterCatalogJsonValueV1 cacheValue = members["cache"];
            if (cacheValue.Kind != EncounterCatalogJsonValueKindV1.Null
                && !TryReadCache(cacheValue, MemberPath(path, "cache"), out cache, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateVariant(variantId, requiredPacts, forbiddenPacts,
                vinculoOrdinal, roomDescriptorRef, waves, cache, out variant,
                out EncounterCatalogError coreError))
            {
                variant = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadWaveArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogWave> waves, out EncounterCatalogError error)
        {
            waves = null;
            List<EncounterCatalogJsonValueV1> items;
            if (!TryReadArray(value, path, out items, out error)) return false;
            waves = new List<EncounterCatalogWave>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                EncounterCatalogWave wave;
                if (!TryReadWave(items[index], IndexPath(path, index), out wave, out error))
                {
                    waves = null;
                    return false;
                }
                waves.Add(wave);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadWave(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogWave wave, out EncounterCatalogError error)
        {
            wave = null;
            string[] names = { "waveId", "spawns" };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string waveId;
            if (!TryReadString(members["waveId"], MemberPath(path, "waveId"), out waveId, out error)) return false;
            List<EncounterCatalogSpawn> spawns;
            if (!TryReadSpawnArray(members["spawns"], MemberPath(path, "spawns"), out spawns, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateWave(waveId, spawns, out wave,
                out EncounterCatalogError coreError))
            {
                wave = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadSpawnArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogSpawn> spawns, out EncounterCatalogError error)
        {
            spawns = null;
            List<EncounterCatalogJsonValueV1> items;
            if (!TryReadArray(value, path, out items, out error)) return false;
            spawns = new List<EncounterCatalogSpawn>(items.Count);
            for (int index = 0; index < items.Count; index++)
            {
                EncounterCatalogSpawn spawn;
                if (!TryReadSpawn(items[index], IndexPath(path, index), out spawn, out error))
                {
                    spawns = null;
                    return false;
                }
                spawns.Add(spawn);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadSpawn(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogSpawn spawn, out EncounterCatalogError error)
        {
            spawn = null;
            string[] names =
            {
                "spawnId", "enemyId", "enemyDescriptorRef", "spawnAnchorId", "rewardKind",
                "vinculoSequenceOrdinal"
            };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string spawnId;
            if (!TryReadString(members["spawnId"], MemberPath(path, "spawnId"), out spawnId, out error)) return false;
            string enemyId;
            if (!TryReadString(members["enemyId"], MemberPath(path, "enemyId"), out enemyId, out error)) return false;
            EncounterCatalogDescriptorRef enemyDescriptorRef;
            if (!TryReadDescriptorReference(members["enemyDescriptorRef"], MemberPath(path, "enemyDescriptorRef"),
                out enemyDescriptorRef, out error)) return false;
            string spawnAnchorId;
            if (!TryReadString(members["spawnAnchorId"], MemberPath(path, "spawnAnchorId"),
                out spawnAnchorId, out error)) return false;
            string rewardKindToken;
            if (!TryReadString(members["rewardKind"], MemberPath(path, "rewardKind"),
                out rewardKindToken, out error)) return false;
            if (!EncounterCatalogTokensV1.TryParseRewardKind(rewardKindToken,
                out ExpeditionEncounterRewardEnemyKind rewardKind))
                return Fail(EncounterCatalogErrorCode.InvalidValue, MemberPath(path, "rewardKind"),
                    "Reward kind token is not supported by schema 1.", out error);
            int vinculoOrdinal;
            if (!TryReadInt(members["vinculoSequenceOrdinal"], MemberPath(path, "vinculoSequenceOrdinal"),
                out vinculoOrdinal, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateSpawn(spawnId, enemyId, enemyDescriptorRef,
                spawnAnchorId, rewardKind, vinculoOrdinal, out spawn,
                out EncounterCatalogError coreError))
            {
                spawn = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadCache(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogCache cache, out EncounterCatalogError error)
        {
            cache = null;
            string[] names = { "cacheId", "cacheDescriptorRef", "anchorId" };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string cacheId;
            if (!TryReadString(members["cacheId"], MemberPath(path, "cacheId"), out cacheId, out error)) return false;
            EncounterCatalogDescriptorRef descriptorRef;
            if (!TryReadDescriptorReference(members["cacheDescriptorRef"], MemberPath(path, "cacheDescriptorRef"),
                out descriptorRef, out error)) return false;
            string anchorId;
            if (!TryReadString(members["anchorId"], MemberPath(path, "anchorId"), out anchorId, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateCache(cacheId, descriptorRef, anchorId,
                out cache, out EncounterCatalogError coreError))
            {
                cache = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadDescriptorReference(EncounterCatalogJsonValueV1 value, string path,
            out EncounterCatalogDescriptorRef descriptorRef, out EncounterCatalogError error)
        {
            descriptorRef = null;
            string[] names = { "descriptorId", "revision" };
            Dictionary<string, EncounterCatalogJsonValueV1> members;
            if (!TryReadObject(value, path, out members, out error)) return false;
            if (!CheckMembers(members, path, names, names, out error)) return false;

            string descriptorId;
            if (!TryReadString(members["descriptorId"], MemberPath(path, "descriptorId"),
                out descriptorId, out error)) return false;
            int revision;
            if (!TryReadInt(members["revision"], MemberPath(path, "revision"), out revision, out error)) return false;

            if (!EncounterCatalogFactoryV1.TryCreateDescriptorRef(descriptorId, revision,
                out descriptorRef, out EncounterCatalogError coreError))
            {
                descriptorRef = null;
                error = FromCoreError(path, coreError);
                return false;
            }
            return true;
        }

        private static bool TryReadIntArray(EncounterCatalogJsonValueV1 value, string path,
            out List<int> items, out EncounterCatalogError error)
        {
            items = null;
            List<EncounterCatalogJsonValueV1> values;
            if (!TryReadArray(value, path, out values, out error)) return false;
            items = new List<int>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                int item;
                if (!TryReadInt(values[index], IndexPath(path, index), out item, out error))
                {
                    items = null;
                    return false;
                }
                items.Add(item);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadStringArray(EncounterCatalogJsonValueV1 value, string path,
            out List<string> items, out EncounterCatalogError error)
        {
            items = null;
            List<EncounterCatalogJsonValueV1> values;
            if (!TryReadArray(value, path, out values, out error)) return false;
            items = new List<string>(values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                string item;
                if (!TryReadString(values[index], IndexPath(path, index), out item, out error))
                {
                    items = null;
                    return false;
                }
                items.Add(item);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadObject(EncounterCatalogJsonValueV1 value, string path,
            out Dictionary<string, EncounterCatalogJsonValueV1> members, out EncounterCatalogError error)
        {
            members = null;
            if (value == null || value.Kind != EncounterCatalogJsonValueKindV1.Object)
                return Fail(EncounterCatalogErrorCode.WrongJsonType, path,
                    "A JSON object is required.", out error);
            members = value.Members;
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadArray(EncounterCatalogJsonValueV1 value, string path,
            out List<EncounterCatalogJsonValueV1> items, out EncounterCatalogError error)
        {
            items = null;
            if (value == null || value.Kind != EncounterCatalogJsonValueKindV1.Array)
                return Fail(EncounterCatalogErrorCode.WrongJsonType, path,
                    "A JSON array is required.", out error);
            items = value.Items;
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadString(EncounterCatalogJsonValueV1 value, string path,
            out string text, out EncounterCatalogError error)
        {
            text = null;
            if (value == null || value.Kind != EncounterCatalogJsonValueKindV1.String)
                return Fail(EncounterCatalogErrorCode.WrongJsonType, path,
                    "A JSON string is required.", out error);
            text = value.Text;
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool TryReadInt(EncounterCatalogJsonValueV1 value, string path,
            out int number, out EncounterCatalogError error)
        {
            number = 0;
            if (value == null || value.Kind != EncounterCatalogJsonValueKindV1.Number)
                return Fail(EncounterCatalogErrorCode.WrongJsonType, path,
                    "A JSON integer is required.", out error);
            if (value.Text.IndexOf('.') >= 0 || value.Text.IndexOf('e') >= 0 || value.Text.IndexOf('E') >= 0)
                return Fail(EncounterCatalogErrorCode.WrongJsonType, path,
                    "A whole JSON number without a fraction or exponent is required.", out error);
            if (!int.TryParse(value.Text, NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out number))
                return Fail(EncounterCatalogErrorCode.SizeOverflow, path,
                    "JSON integer is outside the supported Int32 range.", out error);
            error = default(EncounterCatalogError);
            return true;
        }

        private static bool CheckMembers(Dictionary<string, EncounterCatalogJsonValueV1> members,
            string path, string[] allowedNames, string[] requiredNames, out EncounterCatalogError error)
        {
            var allowed = new HashSet<string>(allowedNames, StringComparer.Ordinal);
            foreach (KeyValuePair<string, EncounterCatalogJsonValueV1> member in members)
            {
                if (!allowed.Contains(member.Key))
                    return Fail(EncounterCatalogErrorCode.UnknownMember, MemberPath(path, member.Key),
                        "Object member is not part of schema 1.", out error);
            }
            for (int index = 0; index < requiredNames.Length; index++)
            {
                string requiredName = requiredNames[index];
                if (!members.ContainsKey(requiredName))
                    return Fail(EncounterCatalogErrorCode.MissingMember, MemberPath(path, requiredName),
                        "Required object member is missing.", out error);
            }
            error = default(EncounterCatalogError);
            return true;
        }

        private static EncounterCatalogError FromCoreError(string scope, EncounterCatalogError coreError)
        {
            string path = coreError.Path;
            if (string.IsNullOrEmpty(path)) path = scope;
            else if (string.Equals(scope, "$", StringComparison.Ordinal)) path = "$" + "." + path;
            else if (path[0] == '[') path = scope + path;
            else path = scope + "." + path;
            return new EncounterCatalogError(coreError.Code, path, coreError.Message);
        }

        private static bool Fail(EncounterCatalogErrorCode code, string path, string message,
            out EncounterCatalogError error)
        {
            error = new EncounterCatalogError(code, path, message);
            return false;
        }

        private static string MemberPath(string path, string member)
        {
            return path + "." + member;
        }

        private static string IndexPath(string path, int index)
        {
            return path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        }
    }
}
