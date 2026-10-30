using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Expeditions
{
    /// <summary>Resolves one route node against an exact manifest and explicit pact context.</summary>
    public static class EncounterPlanResolverV1
    {
        public const string SupportedResolverId = "encounter-resolver.v1";
        public const int SupportedResolverRevision = 1;

        private const string SupportedIdentityCodec = "encounter-spawn.v1";
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly HashSet<string> CanonicalPactIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "pact.avarice",
            "pact.fervor",
            "pact.haste",
            "pact.clairvoyance",
            "pact.ember",
            "pact.silence",
            "pact.gravity",
            "pact.bond"
        };

        public static bool TryResolve(
            EncounterCatalogManifest manifest,
            ExpeditionRunConfiguration run,
            string nodeId,
            IEnumerable<string> pactIds,
            out ResolvedEncounterPlanV1 plan,
            out EncounterPlanResolutionErrorV1 error)
        {
            plan = null;
            if (manifest == null)
                return Fail(EncounterPlanResolutionErrorCodeV1.NullManifest, "manifest",
                    "An encounter catalog manifest is required.", out plan, out error);
            if (run == null)
                return Fail(EncounterPlanResolutionErrorCodeV1.NullRun, "run",
                    "An expedition run configuration is required.", out plan, out error);
            if (pactIds == null)
                return Fail(EncounterPlanResolutionErrorCodeV1.NullPactIds, "pactIds",
                    "Pact context must be supplied explicitly.", out plan, out error);
            if (!string.Equals(manifest.IdentityCodec, SupportedIdentityCodec, StringComparison.Ordinal))
                return Fail(EncounterPlanResolutionErrorCodeV1.UnsupportedIdentityCodec, "manifest.identityCodec",
                    "The manifest identity codec is not supported by this resolver.", out plan, out error);
            if (!string.Equals(manifest.ResolverId, SupportedResolverId, StringComparison.Ordinal)
                || manifest.ResolverRevision != SupportedResolverRevision)
                return Fail(EncounterPlanResolutionErrorCodeV1.UnsupportedResolver, "manifest.resolverId",
                    "Only encounter-resolver.v1 revision 1 is implemented.", out plan, out error);
            if (string.IsNullOrEmpty(nodeId))
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidNodeId, "nodeId",
                    "A route node ID is required.", out plan, out error);

            ExpeditionRoutePlan route = run.Route;
            int generationVersion = route.GenerationVersion;
            if (!manifest.SupportedRouteGenerationVersions.Contains(generationVersion))
                return Fail(EncounterPlanResolutionErrorCodeV1.UnsupportedRouteGeneration,
                    "run.route.generationVersion",
                    "The manifest does not support this route generation version.", out plan, out error);

            ExpeditionRouteNode node = null;
            for (int index = 0; index < route.Nodes.Count; index++)
            {
                if (!string.Equals(route.Nodes[index].NodeId, nodeId, StringComparison.Ordinal)) continue;
                node = route.Nodes[index];
                break;
            }
            if (node == null)
                return Fail(EncounterPlanResolutionErrorCodeV1.RouteNodeNotFound, "nodeId",
                    "The requested node is not part of the run's route.", out plan, out error);

            if (!TryCopyCanonicalPactContext(pactIds, out string[] canonicalPactIds,
                out EncounterPlanResolutionErrorV1 contextError))
            {
                plan = null;
                error = contextError;
                return false;
            }
            var activePactIds = new HashSet<string>(canonicalPactIds, StringComparer.Ordinal);
            if (activePactIds.Contains("pact.bond"))
                return Fail(EncounterPlanResolutionErrorCodeV1.MissingResolutionAuthority, "pactIds",
                    "Vínculo is present, but no authoritative calendar or provenance producer exists.",
                    out plan, out error);

            EncounterCatalogBinding binding = null;
            int bindingCount = 0;
            for (int index = 0; index < manifest.Bindings.Count; index++)
            {
                EncounterCatalogBinding candidate = manifest.Bindings[index];
                if (candidate.GenerationVersion != generationVersion
                    || candidate.NodeKind != node.Kind
                    || !string.Equals(candidate.TemplateId, node.TemplateId, StringComparison.Ordinal)) continue;
                binding = candidate;
                bindingCount++;
            }
            if (bindingCount == 0)
                return Fail(EncounterPlanResolutionErrorCodeV1.MissingBinding, "manifest.bindings",
                    "The route node has no exact generation/template/kind binding.", out plan, out error);
            if (bindingCount > 1)
                return Fail(EncounterPlanResolutionErrorCodeV1.AmbiguousBinding, "manifest.bindings",
                    "The route node has multiple exact generation/template/kind bindings.", out plan, out error);

            string catalogDigest;
            string routeDigest;
            try
            {
                catalogDigest = EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest);
                routeDigest = ComputeRouteDigest(route.CanonicalForm);
            }
            catch (ArgumentException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "run.route",
                    "The route or catalog contains invalid canonical text.", out plan, out error);
            }
            catch (OverflowException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "run.route",
                    "The route or catalog exceeds canonical framing limits.", out plan, out error);
            }

            RoomInstanceId roomInstanceId;
            try
            {
                roomInstanceId = ExpeditionRoomIdentity.ForNode(run.RunId, node.NodeId);
            }
            catch (ArgumentException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "run.route.nodeId",
                    "A valid route node identity is required to derive the room instance ID.", out plan, out error);
            }

            if (!Enum.IsDefined(typeof(EncounterCatalogBindingMode), binding.Mode))
                return Fail(EncounterPlanResolutionErrorCodeV1.UnsupportedBindingMode, "manifest.bindings.mode",
                    "The binding mode is not supported.", out plan, out error);

            if (binding.Mode == EncounterCatalogBindingMode.None)
            {
                plan = new ResolvedEncounterPlanV1(
                    EncounterCatalogBindingMode.None,
                    catalogDigest,
                    null,
                    routeDigest,
                    run.RunId,
                    run.Seed,
                    generationVersion,
                    node.NodeId,
                    node.TemplateId,
                    node.Kind,
                    roomInstanceId,
                    null,
                    0,
                    null,
                    canonicalPactIds,
                    Array.Empty<EncounterCatalogDescriptor>(),
                    Array.Empty<EncounterSpawnIdentityV1>());
                error = default(EncounterPlanResolutionErrorV1);
                return true;
            }

            EncounterCatalogDefinition definition = null;
            int definitionCount = 0;
            for (int index = 0; index < manifest.Definitions.Count; index++)
            {
                EncounterCatalogDefinition candidate = manifest.Definitions[index];
                if (candidate.DefinitionRevision != binding.DefinitionRevision
                    || !string.Equals(candidate.EncounterId, binding.EncounterId, StringComparison.Ordinal)) continue;
                definition = candidate;
                definitionCount++;
            }
            if (definitionCount == 0)
                return Fail(EncounterPlanResolutionErrorCodeV1.MissingDefinition, "manifest.definitions",
                    "The exact encounter definition referenced by the binding is missing.", out plan, out error);
            if (definitionCount > 1)
                return Fail(EncounterPlanResolutionErrorCodeV1.AmbiguousDefinition, "manifest.definitions",
                    "The exact encounter definition is present more than once.", out plan, out error);

            var applicableVariants = new List<EncounterCatalogVariant>();
            for (int variantIndex = 0; variantIndex < definition.Variants.Count; variantIndex++)
            {
                EncounterCatalogVariant variant = definition.Variants[variantIndex];
                if (variant.ScheduledVinculoOrdinal != 0) continue;
                if (!MatchesPactContext(variant, activePactIds)) continue;
                applicableVariants.Add(variant);
            }
            if (applicableVariants.Count == 0)
                return Fail(EncounterPlanResolutionErrorCodeV1.NoApplicableVariant, "manifest.definitions.variants",
                    "No variant applies to the explicit pact context and no-Vínculo calendar.", out plan, out error);
            if (applicableVariants.Count > 1)
                return Fail(EncounterPlanResolutionErrorCodeV1.AmbiguousVariant, "manifest.definitions.variants",
                    "More than one variant applies to the explicit pact context and no-Vínculo calendar.",
                    out plan, out error);

            EncounterCatalogVariant selectedVariant = applicableVariants[0];
            for (int waveIndex = 0; waveIndex < selectedVariant.Waves.Count; waveIndex++)
            {
                EncounterCatalogWave wave = selectedVariant.Waves[waveIndex];
                for (int spawnIndex = 0; spawnIndex < wave.Spawns.Count; spawnIndex++)
                {
                    if (wave.Spawns[spawnIndex].VinculoSequenceOrdinal != 0)
                        return Fail(EncounterPlanResolutionErrorCodeV1.UnsupportedVariant,
                            "manifest.definitions.variants.waves.spawns.vinculoSequenceOrdinal",
                            "A no-Vínculo variant cannot contain a scheduled Vínculo spawn.", out plan, out error);
                }
            }

            if (!TryCollectUsedDescriptors(manifest, selectedVariant,
                out EncounterCatalogDescriptor[] usedDescriptors,
                out EncounterPlanResolutionErrorV1 descriptorError))
            {
                plan = null;
                error = descriptorError;
                return false;
            }

            string definitionDigest;
            try
            {
                definitionDigest = EncounterCatalogCanonicalV1.ComputeDefinitionSha256(definition);
            }
            catch (ArgumentException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "manifest.definitions",
                    "The encounter definition contains invalid canonical text.", out plan, out error);
            }
            catch (OverflowException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "manifest.definitions",
                    "The encounter definition exceeds canonical framing limits.", out plan, out error);
            }

            var spawnIdentities = new List<EncounterSpawnIdentityV1>();
            try
            {
                for (int waveIndex = 0; waveIndex < selectedVariant.Waves.Count; waveIndex++)
                {
                    EncounterCatalogWave wave = selectedVariant.Waves[waveIndex];
                    foreach (EncounterCatalogSpawn spawn in wave.Spawns.OrderBy(item => item.SpawnId, StringComparer.Ordinal))
                    {
                        spawnIdentities.Add(EncounterSpawnIdentityV1.Create(
                            run.RunId,
                            checked((uint)generationVersion),
                            node.NodeId,
                            roomInstanceId,
                            binding.EncounterId,
                            spawn.SpawnId));
                    }
                }
            }
            catch (ArgumentException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "manifest.definitions.variants.waves.spawns",
                    "A spawn tuple could not be represented by encounter-spawn.v1.", out plan, out error);
            }
            catch (OverflowException)
            {
                return Fail(EncounterPlanResolutionErrorCodeV1.InvalidPlanInput, "run.route.generationVersion",
                    "Route generation cannot be represented by the spawn identity codec.", out plan, out error);
            }

            plan = new ResolvedEncounterPlanV1(
                EncounterCatalogBindingMode.Encounter,
                catalogDigest,
                definitionDigest,
                routeDigest,
                run.RunId,
                run.Seed,
                generationVersion,
                node.NodeId,
                node.TemplateId,
                node.Kind,
                roomInstanceId,
                binding.EncounterId,
                binding.DefinitionRevision,
                selectedVariant,
                canonicalPactIds,
                usedDescriptors,
                spawnIdentities);
            error = default(EncounterPlanResolutionErrorV1);
            return true;
        }

        private static bool TryCopyCanonicalPactContext(
            IEnumerable<string> pactIds,
            out string[] canonicalPactIds,
            out EncounterPlanResolutionErrorV1 error)
        {
            var copied = new List<string>();
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (string pactId in pactIds)
            {
                if (pactId == null || !CanonicalPactIds.Contains(pactId))
                {
                    canonicalPactIds = null;
                    error = new EncounterPlanResolutionErrorV1(
                        EncounterPlanResolutionErrorCodeV1.InvalidResolutionContext,
                        "pactIds",
                        "Pact context contains a null or non-canonical pact ID.");
                    return false;
                }
                if (!unique.Add(pactId))
                {
                    canonicalPactIds = null;
                    error = new EncounterPlanResolutionErrorV1(
                        EncounterPlanResolutionErrorCodeV1.InvalidResolutionContext,
                        "pactIds",
                        "Pact context IDs must be unique using ordinal comparison.");
                    return false;
                }
                copied.Add(pactId);
                if (copied.Count > 2)
                {
                    canonicalPactIds = null;
                    error = new EncounterPlanResolutionErrorV1(
                        EncounterPlanResolutionErrorCodeV1.InvalidResolutionContext,
                        "pactIds",
                        "An expedition can resolve at most two pact IDs.");
                    return false;
                }
            }

            copied.Sort(StringComparer.Ordinal);
            canonicalPactIds = copied.ToArray();
            error = default(EncounterPlanResolutionErrorV1);
            return true;
        }

        private static bool MatchesPactContext(EncounterCatalogVariant variant, HashSet<string> activePactIds)
        {
            for (int index = 0; index < variant.RequiredPactIds.Count; index++)
            {
                if (!activePactIds.Contains(variant.RequiredPactIds[index])) return false;
            }
            for (int index = 0; index < variant.ForbiddenPactIds.Count; index++)
            {
                if (activePactIds.Contains(variant.ForbiddenPactIds[index])) return false;
            }
            return true;
        }

        private static bool TryCollectUsedDescriptors(
            EncounterCatalogManifest manifest,
            EncounterCatalogVariant variant,
            out EncounterCatalogDescriptor[] descriptors,
            out EncounterPlanResolutionErrorV1 error)
        {
            var references = new List<EncounterCatalogDescriptorRef> { variant.RoomDescriptorRef };
            for (int waveIndex = 0; waveIndex < variant.Waves.Count; waveIndex++)
            {
                EncounterCatalogWave wave = variant.Waves[waveIndex];
                for (int spawnIndex = 0; spawnIndex < wave.Spawns.Count; spawnIndex++)
                    references.Add(wave.Spawns[spawnIndex].EnemyDescriptorRef);
            }
            if (variant.Cache != null) references.Add(variant.Cache.CacheDescriptorRef);

            var uniqueReferences = new List<EncounterCatalogDescriptorRef>();
            for (int index = 0; index < references.Count; index++)
            {
                EncounterCatalogDescriptorRef current = references[index];
                bool seen = uniqueReferences.Any(item => item.Revision == current.Revision
                    && string.Equals(item.DescriptorId, current.DescriptorId, StringComparison.Ordinal));
                if (!seen) uniqueReferences.Add(current);
            }

            uniqueReferences.Sort((left, right) =>
            {
                int idOrder = string.Compare(left.DescriptorId, right.DescriptorId, StringComparison.Ordinal);
                return idOrder != 0 ? idOrder : left.Revision.CompareTo(right.Revision);
            });

            var resolved = new List<EncounterCatalogDescriptor>();
            for (int referenceIndex = 0; referenceIndex < uniqueReferences.Count; referenceIndex++)
            {
                EncounterCatalogDescriptorRef reference = uniqueReferences[referenceIndex];
                EncounterCatalogDescriptor descriptor = null;
                int matches = 0;
                for (int descriptorIndex = 0; descriptorIndex < manifest.DescriptorRefs.Count; descriptorIndex++)
                {
                    EncounterCatalogDescriptor candidate = manifest.DescriptorRefs[descriptorIndex];
                    if (candidate.Revision != reference.Revision
                        || !string.Equals(candidate.DescriptorId, reference.DescriptorId, StringComparison.Ordinal)) continue;
                    descriptor = candidate;
                    matches++;
                }
                if (matches == 0)
                {
                    descriptors = null;
                    error = new EncounterPlanResolutionErrorV1(
                        EncounterPlanResolutionErrorCodeV1.MissingDescriptor,
                        "manifest.descriptorRefs",
                        "The selected variant references a missing descriptor.");
                    return false;
                }
                if (matches > 1)
                {
                    descriptors = null;
                    error = new EncounterPlanResolutionErrorV1(
                        EncounterPlanResolutionErrorCodeV1.InvalidPlanInput,
                        "manifest.descriptorRefs",
                        "The selected variant references a descriptor ID/revision pair more than once.");
                    return false;
                }
                resolved.Add(descriptor);
            }

            descriptors = resolved.ToArray();
            error = default(EncounterPlanResolutionErrorV1);
            return true;
        }

        private static string ComputeRouteDigest(string canonicalForm)
        {
            byte[] bytes = StrictUtf8.GetBytes(canonicalForm);
            byte[] digest;
            using (SHA256 sha256 = SHA256.Create()) digest = sha256.ComputeHash(bytes);
            return ToLowerHex(digest);
        }

        private static string ToLowerHex(byte[] bytes)
        {
            const string digits = "0123456789abcdef";
            var characters = new char[bytes.Length * 2];
            for (int index = 0; index < bytes.Length; index++)
            {
                characters[index * 2] = digits[bytes[index] >> 4];
                characters[index * 2 + 1] = digits[bytes[index] & 0x0f];
            }
            return new string(characters);
        }

        private static bool Fail(
            EncounterPlanResolutionErrorCodeV1 code,
            string path,
            string message,
            out ResolvedEncounterPlanV1 plan,
            out EncounterPlanResolutionErrorV1 error)
        {
            plan = null;
            error = new EncounterPlanResolutionErrorV1(code, path, message);
            return false;
        }
    }
}
