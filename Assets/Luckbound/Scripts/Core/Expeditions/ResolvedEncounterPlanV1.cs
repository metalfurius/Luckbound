using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Expeditions
{
    /// <summary>
    /// An immutable, resolution-bound view of one route node. A None plan has no selected variant or
    /// spawn identities; it does not describe a roster or certify a room as cleared.
    /// </summary>
    public sealed class ResolvedEncounterPlanV1
    {
        private readonly IReadOnlyList<string> _contextPactIds;
        private readonly IReadOnlyList<EncounterCatalogDescriptor> _usedDescriptors;
        private readonly IReadOnlyList<EncounterSpawnIdentityV1> _spawnIdentities;
        private readonly Dictionary<string, EncounterSpawnIdentityV1> _identitiesBySpawnId;

        internal ResolvedEncounterPlanV1(
            EncounterCatalogBindingMode mode,
            string catalogDigest,
            string definitionDigest,
            string routeDigest,
            RunId runId,
            uint seed,
            int generationVersion,
            string nodeId,
            string templateId,
            ExpeditionRouteNodeKind nodeKind,
            RoomInstanceId roomInstanceId,
            string encounterId,
            int definitionRevision,
            EncounterCatalogVariant selectedVariant,
            IEnumerable<string> contextPactIds,
            IEnumerable<EncounterCatalogDescriptor> usedDescriptors,
            IEnumerable<EncounterSpawnIdentityV1> spawnIdentities)
        {
            if (contextPactIds == null) throw new ArgumentNullException(nameof(contextPactIds));
            if (usedDescriptors == null) throw new ArgumentNullException(nameof(usedDescriptors));
            if (spawnIdentities == null) throw new ArgumentNullException(nameof(spawnIdentities));

            Mode = mode;
            CatalogDigest = catalogDigest ?? throw new ArgumentNullException(nameof(catalogDigest));
            DefinitionDigest = definitionDigest;
            RouteDigest = routeDigest ?? throw new ArgumentNullException(nameof(routeDigest));
            RunId = runId;
            Seed = seed;
            GenerationVersion = generationVersion;
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            TemplateId = templateId ?? throw new ArgumentNullException(nameof(templateId));
            NodeKind = nodeKind;
            RoomInstanceId = roomInstanceId;
            EncounterId = encounterId;
            DefinitionRevision = definitionRevision;
            SelectedVariant = selectedVariant == null ? null : CloneVariant(selectedVariant);

            _contextPactIds = Array.AsReadOnly(contextPactIds.ToArray());
            _usedDescriptors = Array.AsReadOnly(usedDescriptors
                .Select(CloneDescriptor)
                .ToArray());

            EncounterSpawnIdentityV1[] identities = spawnIdentities.ToArray();
            if (identities.Any(identity => identity == null))
                throw new ArgumentException("Spawn identities cannot contain null entries.", nameof(spawnIdentities));
            _spawnIdentities = Array.AsReadOnly((EncounterSpawnIdentityV1[])identities.Clone());
            _identitiesBySpawnId = new Dictionary<string, EncounterSpawnIdentityV1>(StringComparer.Ordinal);
            for (int index = 0; index < identities.Length; index++)
            {
                EncounterSpawnIdentityV1 identity = identities[index];
                if (!_identitiesBySpawnId.TryAdd(identity.LocalSpawnId, identity))
                    throw new ArgumentException("Spawn identity IDs must be unique.", nameof(spawnIdentities));
            }

            if (mode == EncounterCatalogBindingMode.None)
            {
                if (definitionDigest != null || encounterId != null || definitionRevision != 0
                    || SelectedVariant != null || identities.Length != 0 || _usedDescriptors.Count != 0)
                    throw new ArgumentException("A None plan cannot contain encounter payload.", nameof(mode));
            }
            else if (mode == EncounterCatalogBindingMode.Encounter)
            {
                if (string.IsNullOrEmpty(definitionDigest) || string.IsNullOrEmpty(encounterId)
                    || definitionRevision <= 0 || SelectedVariant == null)
                    throw new ArgumentException("An Encounter plan requires its selected definition and variant.", nameof(mode));
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public EncounterCatalogBindingMode Mode { get; }
        public string CatalogDigest { get; }
        public string DefinitionDigest { get; }
        public string RouteDigest { get; }
        public RunId RunId { get; }
        public uint Seed { get; }
        public int GenerationVersion { get; }
        public string NodeId { get; }
        public string TemplateId { get; }
        public ExpeditionRouteNodeKind NodeKind { get; }
        public RoomInstanceId RoomInstanceId { get; }
        public string EncounterId { get; }
        public int DefinitionRevision { get; }
        public string VariantId => SelectedVariant == null ? null : SelectedVariant.VariantId;
        public EncounterCatalogVariant SelectedVariant { get; }
        public IReadOnlyList<string> ContextPactIds => _contextPactIds;
        public IReadOnlyList<EncounterCatalogDescriptor> UsedDescriptors => _usedDescriptors;
        public IReadOnlyList<EncounterSpawnIdentityV1> SpawnIdentities => _spawnIdentities;

        /// <summary>Returns the durable identity for a spawn in the selected variant.</summary>
        public EncounterSpawnIdentityV1 GetSpawnIdentity(string spawnId)
        {
            if (spawnId == null) throw new ArgumentNullException(nameof(spawnId));
            if (!_identitiesBySpawnId.TryGetValue(spawnId, out EncounterSpawnIdentityV1 identity))
                throw new KeyNotFoundException("The resolved plan has no spawn with ID " + spawnId + ".");
            return identity;
        }

        private static EncounterCatalogVariant CloneVariant(EncounterCatalogVariant source)
        {
            var waves = new EncounterCatalogWave[source.Waves.Count];
            for (int waveIndex = 0; waveIndex < source.Waves.Count; waveIndex++)
            {
                EncounterCatalogWave sourceWave = source.Waves[waveIndex];
                EncounterCatalogSpawn[] orderedSourceSpawns = sourceWave.Spawns
                    .OrderBy(item => item.SpawnId, StringComparer.Ordinal)
                    .ToArray();
                var spawns = new EncounterCatalogSpawn[orderedSourceSpawns.Length];
                for (int spawnIndex = 0; spawnIndex < orderedSourceSpawns.Length; spawnIndex++)
                {
                    EncounterCatalogSpawn sourceSpawn = orderedSourceSpawns[spawnIndex];
                    spawns[spawnIndex] = new EncounterCatalogSpawn(
                        sourceSpawn.SpawnId,
                        sourceSpawn.EnemyId,
                        CloneDescriptorRef(sourceSpawn.EnemyDescriptorRef),
                        sourceSpawn.SpawnAnchorId,
                        sourceSpawn.RewardKind,
                        sourceSpawn.VinculoSequenceOrdinal);
                }

                waves[waveIndex] = new EncounterCatalogWave(sourceWave.WaveId, spawns);
            }

            EncounterCatalogCache cache = source.Cache == null
                ? null
                : new EncounterCatalogCache(source.Cache.CacheId,
                    CloneDescriptorRef(source.Cache.CacheDescriptorRef), source.Cache.AnchorId);

            return new EncounterCatalogVariant(
                source.VariantId,
                source.RequiredPactIds.ToArray(),
                source.ForbiddenPactIds.ToArray(),
                source.ScheduledVinculoOrdinal,
                CloneDescriptorRef(source.RoomDescriptorRef),
                waves,
                cache);
        }

        private static EncounterCatalogDescriptorRef CloneDescriptorRef(EncounterCatalogDescriptorRef source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new EncounterCatalogDescriptorRef(source.DescriptorId, source.Revision);
        }

        private static EncounterCatalogDescriptor CloneDescriptor(EncounterCatalogDescriptor source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return new EncounterCatalogDescriptor(source.DescriptorId, source.Revision, source.Kind, source.Sha256);
        }
    }
}
