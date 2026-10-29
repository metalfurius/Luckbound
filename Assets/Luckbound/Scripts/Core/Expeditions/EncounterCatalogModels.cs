using System;
using System.Collections.Generic;

namespace Luckbound.Core.Expeditions
{
    public enum EncounterCatalogDescriptorKind
    {
        Room = 1,
        Enemy = 2,
        Cache = 3
    }

    public enum EncounterCatalogBindingMode
    {
        None = 1,
        Encounter = 2
    }

    public sealed class EncounterCatalogManifest
    {
        internal EncounterCatalogManifest(int schemaVersion, string catalogId, int catalogRevision,
            string identityCodec, string resolverId, int resolverRevision,
            IEnumerable<int> supportedRouteGenerationVersions,
            IEnumerable<EncounterCatalogDescriptor> descriptorRefs,
            IEnumerable<EncounterCatalogBinding> bindings,
            IEnumerable<EncounterCatalogDefinition> definitions)
        {
            SchemaVersion = schemaVersion;
            CatalogId = catalogId;
            CatalogRevision = catalogRevision;
            IdentityCodec = identityCodec;
            ResolverId = resolverId;
            ResolverRevision = resolverRevision;
            SupportedRouteGenerationVersions = Copy(supportedRouteGenerationVersions);
            DescriptorRefs = Copy(descriptorRefs);
            Bindings = Copy(bindings);
            Definitions = Copy(definitions);
        }

        public int SchemaVersion { get; }
        public string CatalogId { get; }
        public int CatalogRevision { get; }
        public string IdentityCodec { get; }
        public string ResolverId { get; }
        public int ResolverRevision { get; }
        public IReadOnlyList<int> SupportedRouteGenerationVersions { get; }
        public IReadOnlyList<EncounterCatalogDescriptor> DescriptorRefs { get; }
        public IReadOnlyList<EncounterCatalogBinding> Bindings { get; }
        public IReadOnlyList<EncounterCatalogDefinition> Definitions { get; }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = new List<T>(values).ToArray();
            return Array.AsReadOnly(copy);
        }
    }

    public sealed class EncounterCatalogDescriptor
    {
        internal EncounterCatalogDescriptor(string descriptorId, int revision,
            EncounterCatalogDescriptorKind kind, string sha256)
        {
            DescriptorId = descriptorId;
            Revision = revision;
            Kind = kind;
            Sha256 = sha256;
        }

        public string DescriptorId { get; }
        public int Revision { get; }
        public EncounterCatalogDescriptorKind Kind { get; }
        public string Sha256 { get; }
    }

    public sealed class EncounterCatalogDescriptorRef
    {
        internal EncounterCatalogDescriptorRef(string descriptorId, int revision)
        {
            DescriptorId = descriptorId;
            Revision = revision;
        }

        public string DescriptorId { get; }
        public int Revision { get; }
    }

    public sealed class EncounterCatalogBinding
    {
        internal EncounterCatalogBinding(int generationVersion, string templateId,
            ExpeditionRouteNodeKind nodeKind, EncounterCatalogBindingMode mode,
            string encounterId, int definitionRevision)
        {
            GenerationVersion = generationVersion;
            TemplateId = templateId;
            NodeKind = nodeKind;
            Mode = mode;
            EncounterId = encounterId;
            DefinitionRevision = definitionRevision;
        }

        public int GenerationVersion { get; }
        public string TemplateId { get; }
        public ExpeditionRouteNodeKind NodeKind { get; }
        public EncounterCatalogBindingMode Mode { get; }
        public string EncounterId { get; }
        public int DefinitionRevision { get; }
    }

    public sealed class EncounterCatalogDefinition
    {
        internal EncounterCatalogDefinition(string encounterId, int definitionRevision,
            IEnumerable<EncounterCatalogVariant> variants)
        {
            EncounterId = encounterId;
            DefinitionRevision = definitionRevision;
            Variants = Copy(variants);
        }

        public string EncounterId { get; }
        public int DefinitionRevision { get; }
        public IReadOnlyList<EncounterCatalogVariant> Variants { get; }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return Array.AsReadOnly(new List<T>(values).ToArray());
        }
    }

    public sealed class EncounterCatalogVariant
    {
        internal EncounterCatalogVariant(string variantId,
            IEnumerable<string> requiredPactIds, IEnumerable<string> forbiddenPactIds,
            int scheduledVinculoOrdinal, EncounterCatalogDescriptorRef roomDescriptorRef,
            IEnumerable<EncounterCatalogWave> waves, EncounterCatalogCache cache)
        {
            VariantId = variantId;
            RequiredPactIds = Copy(requiredPactIds);
            ForbiddenPactIds = Copy(forbiddenPactIds);
            ScheduledVinculoOrdinal = scheduledVinculoOrdinal;
            RoomDescriptorRef = roomDescriptorRef;
            Waves = Copy(waves);
            Cache = cache;
        }

        public string VariantId { get; }
        public IReadOnlyList<string> RequiredPactIds { get; }
        public IReadOnlyList<string> ForbiddenPactIds { get; }
        public int ScheduledVinculoOrdinal { get; }
        public EncounterCatalogDescriptorRef RoomDescriptorRef { get; }
        public IReadOnlyList<EncounterCatalogWave> Waves { get; }
        public EncounterCatalogCache Cache { get; }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            return Array.AsReadOnly(new List<T>(values).ToArray());
        }
    }

    public sealed class EncounterCatalogWave
    {
        internal EncounterCatalogWave(string waveId, IEnumerable<EncounterCatalogSpawn> spawns)
        {
            WaveId = waveId;
            if (spawns == null) throw new ArgumentNullException(nameof(spawns));
            Spawns = Array.AsReadOnly(new List<EncounterCatalogSpawn>(spawns).ToArray());
        }

        public string WaveId { get; }
        public IReadOnlyList<EncounterCatalogSpawn> Spawns { get; }
    }

    public sealed class EncounterCatalogSpawn
    {
        internal EncounterCatalogSpawn(string spawnId, string enemyId,
            EncounterCatalogDescriptorRef enemyDescriptorRef, string spawnAnchorId,
            ExpeditionEncounterRewardEnemyKind rewardKind, int vinculoSequenceOrdinal)
        {
            SpawnId = spawnId;
            EnemyId = enemyId;
            EnemyDescriptorRef = enemyDescriptorRef;
            SpawnAnchorId = spawnAnchorId;
            RewardKind = rewardKind;
            VinculoSequenceOrdinal = vinculoSequenceOrdinal;
        }

        public string SpawnId { get; }
        public string EnemyId { get; }
        public EncounterCatalogDescriptorRef EnemyDescriptorRef { get; }
        public string SpawnAnchorId { get; }
        public ExpeditionEncounterRewardEnemyKind RewardKind { get; }
        public int VinculoSequenceOrdinal { get; }
    }

    public sealed class EncounterCatalogCache
    {
        internal EncounterCatalogCache(string cacheId, EncounterCatalogDescriptorRef cacheDescriptorRef,
            string anchorId)
        {
            CacheId = cacheId;
            CacheDescriptorRef = cacheDescriptorRef;
            AnchorId = anchorId;
        }

        public string CacheId { get; }
        public EncounterCatalogDescriptorRef CacheDescriptorRef { get; }
        public string AnchorId { get; }
    }
}
