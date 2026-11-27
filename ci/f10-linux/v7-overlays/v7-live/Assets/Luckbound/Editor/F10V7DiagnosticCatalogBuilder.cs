#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves.V7;
using UnityEditor;
using UnityEngine;

namespace Luckbound.Editor
{
    /// <summary>
    /// Builds an in-memory diagnostic catalog from the exact F10 sources and a verified physical
    /// bundle. It never writes or registers product content and it never changes registryEligible.
    /// </summary>
    internal static class F10V7DiagnosticCatalogBuilder
    {
        internal const string BundleRootRelativePath = F10V7VerifiedPhysicalInputs.BundleRootRelativePath;
        internal const string CatalogId = "catalog.f10-diagnostic";
        internal const int CatalogRevision = 1;
        internal const string C1EncounterId = "encounter.c08_arena";
        internal const string E1EncounterId = "encounter.f10.e1-cache";
        internal const string GuardianEncounterId = "boss.custodian_of_the_oaks";

        private const string RoutePath = "Assets/Resources/Luckbound/Data/Rooms/F10SampleRoute.json";
        private const string C08Path = "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json";
        private const string CustodianPath = "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json";
        private const string DiagnosticRoomRoot = "Assets/Luckbound/Editor/Diagnostic/F10";
        private const string PlayerPrefabPath = "Assets/Luckbound/Editor/Diagnostic/F10TechnicalDiagnosticPlayer.prefab";
        private const string CachePrefabPath = "Assets/Luckbound/Editor/Diagnostic/F10GoldCache/F10ExplorationGoldCacheDiagnostic.prefab";
        private const string SlimePrefabPath = "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab";
        private const string HuesoguardiaPrefabPath = "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab";
        private const string CustodianPrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab";
        private const string CustodianWavePrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab";

        private static readonly RoomSource[] Rooms =
        {
            new RoomSource("sample.tutorial", "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial,
                "F10TutorialRoom.json", "F10TutorialDiagnostic.prefab", false),
            new RoomSource("sample.patio.combat", "room.patio.c1", ExpeditionRouteNodeKind.Combat,
                "F10PatioCombatC1Room.json", "F10PatioCombatC1Diagnostic.prefab", false),
            new RoomSource("sample.patio.exploration", "room.patio.e1", ExpeditionRouteNodeKind.Exploration,
                "F10PatioExplorationE1Room.json", "F10PatioExplorationE1Diagnostic.prefab", false),
            new RoomSource("sample.patio.guardian", "room.patio.guardian", ExpeditionRouteNodeKind.Guardian,
                "F10PatioGuardianRoom.json", "F10PatioGuardianDiagnostic.prefab", false),
            new RoomSource("sample.safe_extraction", "room.sample.extraction", ExpeditionRouteNodeKind.SafeExtraction,
                "F10SafeExtractionRoom.json", "F10SafeExtractionDiagnostic.prefab", true)
        };

        internal static F10V7DiagnosticPhysicalInputs LoadAndVerifyPhysicalInputs(string projectRoot,
            string revisionId, string expectedBundleManifestSha256)
        {
            F10V7VerifiedPhysicalInputs verified = F10V7VerifiedPhysicalInputs.VerifyAndLoad(
                projectRoot, revisionId, expectedBundleManifestSha256);
            return FromVerifiedInputs(verified);
        }

        internal static F10V7DiagnosticPhysicalInputs FromVerifiedInputs(F10V7VerifiedPhysicalInputs verified)
        {
            if (verified == null) throw new ArgumentNullException(nameof(verified));
            F10TechnicalPhysicalDescriptorExporter.BundleManifestData manifest =
                JsonUtility.FromJson<F10TechnicalPhysicalDescriptorExporter.BundleManifestData>(verified.BundleManifestJson);
            F10PhysicalDescriptor descriptor = JsonUtility.FromJson<F10PhysicalDescriptor>(verified.PhysicalDescriptorJson);
            if (manifest == null || descriptor == null)
                throw new InvalidDataException("The verified physical-input compatibility DTO could not be reconstructed.");

            F10V7DiagnosticAssetReference Convert(F10V7VerifiedPhysicalInputs.AssetReference row) =>
                new F10V7DiagnosticAssetReference(row.AssetPath, row.Guid, row.AssetSha256, row.MetaSha256, row.Asset);
            F10V7DiagnosticAssetReference[] assets = verified.PersistentAssets.Select(Convert).ToArray();
            F10V7DiagnosticAssetReference[] sourceAssets = verified.SourceAssets.Select(Convert).ToArray();
            F10V7DiagnosticAssetReference[] prefabAssets = verified.PrefabAssets.Select(Convert).ToArray();
            return new F10V7DiagnosticPhysicalInputs(verified.ProjectRoot, verified.BundleDirectory,
                verified.RevisionId, verified.BundleManifestSha256, manifest, descriptor, assets,
                sourceAssets, prefabAssets, verified.MissingPhysicalEvidenceIds.ToArray(),
                verified.PhysicalDescriptorInstallable, verified.BundleInstallable,
                verified.ExporterSourceSha256, verified.ExporterMetaSha256, verified);
        }

        internal static F10V7DiagnosticCatalog Build(F10V7DiagnosticPhysicalInputs inputs,
            ExpeditionRunConfiguration run)
        {
            if (inputs == null) throw new ArgumentNullException(nameof(inputs));
            if (run == null) throw new ArgumentNullException(nameof(run));
            inputs = RequireApprovedCompositeDescriptorFingerprintContract(inputs.VerifiedInputs);
            if (!string.Equals(run.ClassId, "class.bellator", StringComparison.Ordinal) ||
                !string.Equals(run.RaceId, ExpeditionRunConfiguration.HumanRaceId, StringComparison.Ordinal) ||
                run.Route == null || run.Route.GenerationVersion != ExpeditionSampleRouteGenerator.GenerationVersion)
                throw new InvalidDataException("The V7 diagnostic pin requires the real Bellator/Human F10 sample route.");

            string routeJson = ReadSourceText(inputs, RoutePath);
            ExpeditionSampleRouteDefinition routeDefinition = ExpeditionSampleRouteJsonLoader.Load(routeJson);
            ExpeditionRoutePlan sourceRoute = ExpeditionSampleRouteGenerator.Generate(run.Seed, routeDefinition);
            if (!string.Equals(sourceRoute.CanonicalForm, run.Route.CanonicalForm, StringComparison.Ordinal))
                throw new InvalidDataException("CreateSample's route differs from the exact F10SampleRoute source bytes.");
            ValidateRouteContract(run.Route);

            var roomData = new Dictionary<string, RoomTemplateData>(StringComparer.Ordinal);
            foreach (RoomSource source in Rooms)
            {
                string jsonPath = RoomJsonPath(source);
                RoomTemplateData room = RoomTemplateJsonLoader.Load(ReadSourceText(inputs, jsonPath));
                if (!string.Equals(room.templateId, source.TemplateId, StringComparison.Ordinal))
                    throw new InvalidDataException("Room JSON template does not match its route mapping: " + jsonPath);
                roomData.Add(source.TemplateId, room);
            }

            CombatEncounterData c08 = CombatEncounterJsonLoader.Load(ReadSourceText(inputs, C08Path));
            CustodianBossDefinition custodian = CustodianBossJsonLoader.Load(ReadSourceText(inputs, CustodianPath));
            Dictionary<string, string> c1AnchorBySpawn = BuildC1SpawnAnchorMapping(roomData["room.patio.c1"], c08);
            ValidatePhysicalAnchors(roomData, c08, custodian, c1AnchorBySpawn);

            List<EncounterCatalogDescriptor> descriptors = BuildDescriptors(inputs);
            List<EncounterCatalogDefinition> definitions = BuildDefinitions(inputs, roomData, c08, custodian, c1AnchorBySpawn);
            List<EncounterCatalogBinding> bindings = BuildBindings(run.Route);
            if (!EncounterCatalogFactoryV1.TryCreateManifest(1, CatalogId, CatalogRevision,
                    EncounterPlanPinV1.SupportedIdentityCodec, EncounterPlanResolverV1.SupportedResolverId,
                    EncounterPlanResolverV1.SupportedResolverRevision,
                    new[] { run.Route.GenerationVersion }, descriptors, bindings, definitions,
                    out EncounterCatalogManifest manifest, out EncounterCatalogError manifestError))
                throw CatalogFailure("manifest", manifestError);

            if (!EncounterCatalogRegistryV1.TryCreate(new[] { manifest }, out EncounterCatalogRegistryV1 registry,
                    out EncounterCatalogError registryError))
                throw CatalogFailure("registry", registryError);

            var resolvedPlans = new List<ResolvedEncounterPlanV1>(run.Route.Nodes.Count);
            foreach (ExpeditionRouteNode node in run.Route.Nodes)
            {
                if (!EncounterPlanResolverV1.TryResolve(manifest, run, node.NodeId, Array.Empty<string>(),
                        out ResolvedEncounterPlanV1 plan, out EncounterPlanResolutionErrorV1 resolutionError))
                    throw new InvalidDataException("Route node " + node.NodeId + " did not resolve: " +
                        resolutionError.Code + " at " + resolutionError.Path + ": " + resolutionError.Message);
                resolvedPlans.Add(plan);
            }
            EncounterPlanPinV1 pin;
            try { pin = EncounterPlanPinV1.Capture(manifest, run, resolvedPlans, run.Route.MandatoryNodeIds[0]); }
            catch (EncounterPlanPinValidationExceptionV1 exception)
            { throw new InvalidDataException("The exact diagnostic route could not be pinned: " + exception.Message, exception); }

            return new F10V7DiagnosticCatalog(manifest, registry, pin, resolvedPlans,
                EncounterCatalogCanonicalV1.ComputeManifestSha256(manifest), inputs);
        }

        /// <summary>
        /// Revalidates the exact opaque receipt before reconstructing every compatibility field.
        /// Raw data supplied with the original wrapper never authorizes catalog construction.
        /// </summary>
        internal static F10V7DiagnosticPhysicalInputs RequireApprovedCompositeDescriptorFingerprintContract(
            F10V7VerifiedPhysicalInputs retained)
        {
            if (!ApplicationBootstrapper.TryRevalidateExactF10V7PhysicalInputs(retained,
                    out F10V7VerifiedPhysicalInputs fresh, out string error))
                throw new InvalidDataException("The exact verified physical inputs are required: " + error);
            return FromVerifiedInputs(fresh);
        }

        private static List<EncounterCatalogDescriptor> BuildDescriptors(F10V7DiagnosticPhysicalInputs inputs)
        {
            F10V7VerifiedPhysicalInputs verified = inputs?.VerifiedInputs ??
                throw new InvalidDataException("Canonical verified inputs are required for diagnostic descriptors.");
            var descriptors = new List<EncounterCatalogDescriptor>(F10PhysicalDescriptorTableV1.DescriptorCount);
            foreach (F10PhysicalDescriptorV1 row in F10PhysicalDescriptorTableV1.Descriptors)
            {
                if (!F10PhysicalDescriptorTableV1.TryComputeFingerprint(row.DescriptorId, 1,
                        verified.PhysicalDescriptorPayloadSha256, verified.BundleManifestSha256,
                        out F10PhysicalDescriptorFingerprintResultV1 fingerprint))
                    throw new InvalidDataException("The fixed physical descriptor has no exact composite fingerprint: " + row.DescriptorId);
                EncounterCatalogDescriptorKind kind;
                switch (row.Kind)
                {
                    case "room": kind = EncounterCatalogDescriptorKind.Room; break;
                    case "enemy": kind = EncounterCatalogDescriptorKind.Enemy; break;
                    case "cache": kind = EncounterCatalogDescriptorKind.Cache; break;
                    default: throw new InvalidDataException("Unsupported fixed physical descriptor kind: " + row.Kind);
                }
                if (!EncounterCatalogFactoryV1.TryCreateDescriptor(row.DescriptorId, 1, kind,
                        fingerprint.Fingerprint, out EncounterCatalogDescriptor descriptor,
                        out EncounterCatalogError error))
                    throw CatalogFailure("Physical descriptor " + row.DescriptorId, error);
                descriptors.Add(descriptor);
            }
            return descriptors;
        }

        private static List<EncounterCatalogDefinition> BuildDefinitions(F10V7DiagnosticPhysicalInputs inputs,
            Dictionary<string, RoomTemplateData> rooms, CombatEncounterData c08, CustodianBossDefinition custodian,
            IReadOnlyDictionary<string, string> c1AnchorBySpawn)
        {
            var definitions = new List<EncounterCatalogDefinition>();
            EncounterCatalogDescriptorRef c1Room = DescriptorRef("room.patio.c1");
            var c1Waves = new List<EncounterCatalogWave>(c08.waves.Length);
            foreach (CombatEncounterWaveData sourceWave in c08.waves)
            {
                var spawns = new List<EncounterCatalogSpawn>(sourceWave.spawns.Length);
                foreach (CombatEnemySpawnData sourceSpawn in sourceWave.spawns)
                {
                    string descriptorId = sourceSpawn.enemyId;
                    spawns.Add(CreateSpawn(sourceSpawn.spawnId, sourceSpawn.enemyId,
                        DescriptorRef(descriptorId), c1AnchorBySpawn[sourceSpawn.spawnId], ExpeditionEncounterRewardEnemyKind.Normal));
                }
                c1Waves.Add(CreateWave(sourceWave.id, spawns));
            }
            definitions.Add(CreateDefinition(C1EncounterId,
                CreateVariant("variant.f10.c1", c1Room, c1Waves, null)));

            EncounterCatalogCache cache = CreateCache("cache.exploration", DescriptorRef("cache.exploration"), "cache.exploration");
            definitions.Add(CreateDefinition(E1EncounterId,
                CreateVariant("variant.f10.e1", DescriptorRef("room.patio.e1"), Array.Empty<EncounterCatalogWave>(), cache)));

            EncounterCatalogSpawn bossSpawn = CreateSpawn("boss.custodian", custodian.BossId,
                DescriptorRef(GuardianEncounterId), "boss.custodian", ExpeditionEncounterRewardEnemyKind.Guardian);
            EncounterCatalogWave guardianWave = CreateWave("wave.custodian", new[] { bossSpawn });
            definitions.Add(CreateDefinition(GuardianEncounterId,
                CreateVariant("variant.f10.guardian", DescriptorRef("room.patio.guardian"),
                    new[] { guardianWave }, null)));
            return definitions;

            EncounterCatalogDescriptorRef DescriptorRef(string descriptorId)
            {
                if (!EncounterCatalogFactoryV1.TryCreateDescriptorRef(descriptorId, 1,
                        out EncounterCatalogDescriptorRef value, out EncounterCatalogError error))
                    throw CatalogFailure(descriptorId, error);
                return value;
            }

            EncounterCatalogSpawn CreateSpawn(string spawnId, string enemyId, EncounterCatalogDescriptorRef enemy,
                string anchor, ExpeditionEncounterRewardEnemyKind kind)
            {
                if (!EncounterCatalogFactoryV1.TryCreateSpawn(spawnId, enemyId, enemy, anchor, kind, 0,
                        out EncounterCatalogSpawn value, out EncounterCatalogError error))
                    throw CatalogFailure(spawnId, error);
                return value;
            }

            EncounterCatalogWave CreateWave(string waveId, IEnumerable<EncounterCatalogSpawn> spawns)
            {
                if (!EncounterCatalogFactoryV1.TryCreateWave(waveId, spawns,
                        out EncounterCatalogWave value, out EncounterCatalogError error))
                    throw CatalogFailure(waveId, error);
                return value;
            }

            EncounterCatalogCache CreateCache(string cacheId, EncounterCatalogDescriptorRef cacheDescriptor, string anchor)
            {
                if (!EncounterCatalogFactoryV1.TryCreateCache(cacheId, cacheDescriptor, anchor,
                        out EncounterCatalogCache value, out EncounterCatalogError error))
                    throw CatalogFailure(cacheId, error);
                return value;
            }

            EncounterCatalogVariant CreateVariant(string variantId, EncounterCatalogDescriptorRef room,
                IEnumerable<EncounterCatalogWave> waves, EncounterCatalogCache cacheValue)
            {
                if (!EncounterCatalogFactoryV1.TryCreateVariant(variantId, Array.Empty<string>(), Array.Empty<string>(),
                        0, room, waves, cacheValue, out EncounterCatalogVariant value, out EncounterCatalogError error))
                    throw CatalogFailure(variantId, error);
                return value;
            }

            EncounterCatalogDefinition CreateDefinition(string id, EncounterCatalogVariant variant)
            {
                if (!EncounterCatalogFactoryV1.TryCreateDefinition(id, 1, new[] { variant },
                        out EncounterCatalogDefinition value, out EncounterCatalogError error))
                    throw CatalogFailure(id, error);
                return value;
            }
        }

        private static List<EncounterCatalogBinding> BuildBindings(ExpeditionRoutePlan route)
        {
            var bindings = new List<EncounterCatalogBinding>(route.Nodes.Count);
            foreach (ExpeditionRouteNode node in route.Nodes)
            {
                EncounterCatalogBindingMode mode;
                string encounterId = null;
                int definitionRevision = 0;
                switch (node.Kind)
                {
                    case ExpeditionRouteNodeKind.Tutorial:
                    case ExpeditionRouteNodeKind.SafeExtraction:
                        mode = EncounterCatalogBindingMode.None;
                        break;
                    case ExpeditionRouteNodeKind.Combat:
                        mode = EncounterCatalogBindingMode.Encounter;
                        encounterId = C1EncounterId;
                        definitionRevision = 1;
                        break;
                    case ExpeditionRouteNodeKind.Exploration:
                        mode = EncounterCatalogBindingMode.Encounter;
                        encounterId = E1EncounterId;
                        definitionRevision = 1;
                        break;
                    case ExpeditionRouteNodeKind.Guardian:
                        mode = EncounterCatalogBindingMode.Encounter;
                        encounterId = GuardianEncounterId;
                        definitionRevision = 1;
                        break;
                    default:
                        throw new InvalidDataException("The F10 diagnostic route contains an unsupported node kind: " + node.Kind);
                }
                if (!EncounterCatalogFactoryV1.TryCreateBinding(route.GenerationVersion, node.TemplateId,
                        node.Kind, mode, encounterId, definitionRevision,
                        out EncounterCatalogBinding binding, out EncounterCatalogError error))
                    throw CatalogFailure(node.NodeId, error);
                bindings.Add(binding);
            }
            return bindings;
        }

        private static void ValidateRouteContract(ExpeditionRoutePlan route)
        {
            if (route.GenerationVersion != ExpeditionSampleRouteGenerator.GenerationVersion ||
                route.Nodes.Count != Rooms.Length || route.MandatoryNodeIds.Count != Rooms.Length)
                throw new InvalidDataException("The F10 sample route must contain exactly the five canonical mandatory nodes.");
            for (int index = 0; index < Rooms.Length; index++)
            {
                RoomSource expected = Rooms[index];
                ExpeditionRouteNode node = route.Nodes.SingleOrDefault(value => value.NodeId == expected.NodeId);
                if (node == null || node.Kind != expected.NodeKind || node.TemplateId != expected.TemplateId ||
                    !node.IsMandatory || route.MandatoryNodeIds[index] != expected.NodeId)
                    throw new InvalidDataException("The F10 sample route node/order changed at " + expected.NodeId + ".");
            }
        }

        private static void ValidatePhysicalAnchors(Dictionary<string, RoomTemplateData> rooms,
            CombatEncounterData c08, CustodianBossDefinition custodian,
            IReadOnlyDictionary<string, string> c1AnchorBySpawn)
        {
            RoomTemplateData tutorial = rooms["room.sample.tutorial"];
            RoomTemplateData c1 = rooms["room.patio.c1"];
            RoomTemplateData e1 = rooms["room.patio.e1"];
            RoomTemplateData guardian = rooms["room.patio.guardian"];
            RoomTemplateData extraction = rooms["room.sample.extraction"];
            RequireMarker(tutorial, "tutorial.interaction", "encounter_anchor");
            RequireMarker(extraction, "extraction.interact", "extraction");
            if (Markers(tutorial, "enemy_spawn", "boss_spawn").Any() || Markers(extraction, "enemy_spawn", "boss_spawn").Any())
                throw new InvalidDataException("Tutorial and extraction may use None only while their JSONs contain no combat or boss spawns.");

            string[] c08Spawns = c08.waves.SelectMany(wave => wave.spawns).Select(spawn => spawn.spawnId)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (c08.waves.Length != 2 || c08Spawns.Length != 4 || c1AnchorBySpawn.Count != 4 ||
                !c08Spawns.SequenceEqual(c1AnchorBySpawn.Keys.OrderBy(id => id, StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidDataException("C1 must map C08Arena's exact two waves/four spawn IDs to its two JSON anchors.");

            if (Markers(e1, "enemy_spawn", "boss_spawn").Any())
                throw new InvalidDataException("E1's diagnostic encounter must retain its zero-wave cache-only plan.");
            RequireMarker(e1, "cache.exploration", "treasure");

            if (!string.Equals(custodian.BossId, GuardianEncounterId, StringComparison.Ordinal))
                throw new InvalidDataException("Guardian's encounter identity differs from CustodianBoss.json.");
            string[] guardianBosses = Markers(guardian, "boss_spawn").Select(marker => marker.id).ToArray();
            if (guardianBosses.Length != 1 || guardianBosses[0] != "boss.custodian" ||
                Markers(guardian, "enemy_spawn").Any())
                throw new InvalidDataException("Guardian must map one Custodian boss to the exact boss.custodian room marker.");
        }

        internal static Dictionary<string, string> BuildC1SpawnAnchorMapping(RoomTemplateData c1,
            CombatEncounterData c08)
        {
            if (c1 == null) throw new ArgumentNullException(nameof(c1));
            if (c08 == null) throw new ArgumentNullException(nameof(c08));

            var expected = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.Ordinal)
            {
                { "spawn.slime.01", new KeyValuePair<string, string>("enemy.slime", "spawn.slime.01") },
                { "spawn.huesoguardia.01", new KeyValuePair<string, string>("enemy.huesoguardia", "spawn.slime.01") },
                { "spawn.slime.02", new KeyValuePair<string, string>("enemy.slime", "spawn.slime.02") },
                { "spawn.huesoguardia.02", new KeyValuePair<string, string>("enemy.huesoguardia", "spawn.slime.02") }
            };
            string[] roomAnchors = Markers(c1, "enemy_spawn").Select(marker => marker.id)
                .OrderBy(id => id, StringComparer.Ordinal).ToArray();
            if (!roomAnchors.SequenceEqual(new[] { "spawn.slime.01", "spawn.slime.02" }, StringComparer.Ordinal))
                throw new InvalidDataException("C1 must expose exactly the two measured slime anchors used by both waves.");
            if (c08.waves == null || c08.waves.Length != 2 || c08.waves.Any(wave => wave == null || wave.spawns == null))
                throw new InvalidDataException("C08Arena must contain exactly two non-null waves and spawn lists.");

            var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (CombatEncounterWaveData wave in c08.waves)
            {
                foreach (CombatEnemySpawnData spawn in wave.spawns)
                {
                    if (spawn == null || !expected.TryGetValue(spawn.spawnId, out KeyValuePair<string, string> binding) ||
                        !string.Equals(spawn.enemyId, binding.Key, StringComparison.Ordinal) ||
                        !mapping.TryAdd(spawn.spawnId, binding.Value))
                        throw new InvalidDataException("C08Arena contains an unexpected, duplicated, or incorrectly typed C1 spawn.");
                }
            }
            if (mapping.Count != expected.Count || expected.Keys.Any(id => !mapping.ContainsKey(id)))
                throw new InvalidDataException("C08Arena must map its four exact spawn IDs onto the two measured C1 anchors.");
            return mapping;
        }

        private static IEnumerable<RoomPositionMarkerData> Markers(RoomTemplateData room, params string[] kinds)
        {
            var accepted = new HashSet<string>(kinds, StringComparer.Ordinal);
            return (room.markers ?? Array.Empty<RoomPositionMarkerData>()).Where(marker => marker != null && accepted.Contains(marker.kind));
        }

        private static void RequireMarker(RoomTemplateData room, string id, string kind)
        {
            RoomPositionMarkerData[] found = (room.markers ?? Array.Empty<RoomPositionMarkerData>())
                .Where(marker => marker != null && marker.id == id && marker.kind == kind).ToArray();
            if (found.Length != 1) throw new InvalidDataException("Required JSON marker is absent or duplicated: " + room.templateId + "/" + id);
        }

        private static string RoomJsonPath(RoomSource room) =>
            "Assets/Resources/Luckbound/Data/Rooms/" + room.JsonFileName;

        private static string ReadSourceText(F10V7DiagnosticPhysicalInputs inputs, string path)
        {
            F10V7DiagnosticAssetReference reference = inputs.RequireAsset(path);
            TextAsset text = reference.Asset as TextAsset;
            if (text == null) throw new FileNotFoundException("A required JSON source is not imported as TextAsset: " + path);
            return text.text;
        }

        private static InvalidDataException CatalogFailure(string scope, EncounterCatalogError error) =>
            new InvalidDataException(scope + " failed catalog validation: " + error.Code + " at " + error.Path + ": " + error.Message);


        private sealed class RoomSource
        {
            internal RoomSource(string nodeId, string templateId, ExpeditionRouteNodeKind nodeKind,
                string jsonFileName, string prefabFileName, bool isExtraction)
            { NodeId = nodeId; TemplateId = templateId; NodeKind = nodeKind; JsonFileName = jsonFileName; PrefabFileName = prefabFileName; IsExtraction = isExtraction; }
            internal string NodeId { get; }
            internal string TemplateId { get; }
            internal ExpeditionRouteNodeKind NodeKind { get; }
            internal string JsonFileName { get; }
            internal string PrefabFileName { get; }
            internal bool IsExtraction { get; }
        }

        [Serializable] private sealed class F10PhysicalDescriptor
        {
            public string schema;
            public string revisionId;
            public bool installable;
            public string unityDependencyHashUse;
            public F10PhysicalRoomRow[] rooms;
            public F10PhysicalPrefabRow[] physicalPrefabs;
            public F10PhysicalMissingDependencyRow[] missingPhysicalDependencies;
            public F10PhysicalRootRow[] assetRoots;
            public F10PhysicalDependencyRow[] dependencies;
        }
        [Serializable] private sealed class F10PhysicalRoomRow
        {
            public string prefabPath;
            public bool validated;
            public F10PhysicalRoomDefinitionRow definition;
        }
        [Serializable] private sealed class F10PhysicalRoomDefinitionRow
        {
            public string templateId;
        }
        [Serializable] private sealed class F10PhysicalPrefabRow
        {
            public string assetPath;
            public string role;
            public bool hasExpectedAdapterComponent;
            public bool exclusiveAdapterIdentity;
            public bool custodianWavePhysicalAdapter;
            public bool physicalPlayerComponentsMeasured;
            public bool physicalCachePrefabMeasured;
            public bool projectilePhysicalComponentsMeasured;
        }
        [Serializable] private sealed class F10PhysicalMissingDependencyRow
        {
            public string dependencyId;
            public bool installable;
            public bool observed;
            public string reason;
        }
        [Serializable] private sealed class F10PhysicalRootRow
        {
            public string assetPath;
            public string guid;
            public string rootPath;
        }
        [Serializable] private sealed class F10PhysicalDependencyRow
        {
            public string assetPath;
            public string guid;
        }
    }

    internal sealed class F10V7DiagnosticPhysicalInputs
    {
        private readonly Dictionary<string, F10V7DiagnosticAssetReference> _assets;
        internal F10V7DiagnosticPhysicalInputs(string projectRoot, string bundleDirectory, string revisionId,
            string bundleManifestSha256, F10TechnicalPhysicalDescriptorExporter.BundleManifestData bundle,
            object descriptor, F10V7DiagnosticAssetReference[] assets,
            F10V7DiagnosticAssetReference[] sourceAssets, F10V7DiagnosticAssetReference[] prefabAssets,
            string[] missingPhysicalEvidenceIds, bool physicalDescriptorInstallable,
            bool bundleInstallable, string exporterSourceSha256, string exporterMetaSha256,
            F10V7VerifiedPhysicalInputs verifiedInputs)
        {
            ProjectRoot = projectRoot; BundleDirectory = bundleDirectory; RevisionId = revisionId;
            BundleManifestSha256 = bundleManifestSha256; Bundle = bundle; Descriptor = descriptor;
            Assets = Array.AsReadOnly((F10V7DiagnosticAssetReference[])assets.Clone());
            SourceAssets = Array.AsReadOnly((F10V7DiagnosticAssetReference[])sourceAssets.Clone());
            PrefabAssets = Array.AsReadOnly((F10V7DiagnosticAssetReference[])prefabAssets.Clone());
            MissingPhysicalEvidenceIds = Array.AsReadOnly((string[])missingPhysicalEvidenceIds.Clone());
            PhysicalDescriptorInstallable = physicalDescriptorInstallable;
            BundleInstallable = bundleInstallable;
            ExporterSourceSha256 = exporterSourceSha256;
            ExporterMetaSha256 = exporterMetaSha256;
            VerifiedInputs = verifiedInputs ?? throw new ArgumentNullException(nameof(verifiedInputs));
            _assets = Assets.ToDictionary(row => row.AssetPath, StringComparer.Ordinal);
        }
        internal string ProjectRoot { get; }
        internal string BundleDirectory { get; }
        internal string RevisionId { get; }
        internal string BundleManifestSha256 { get; }
        internal F10TechnicalPhysicalDescriptorExporter.BundleManifestData Bundle { get; }
        internal object Descriptor { get; }
        internal IReadOnlyList<F10V7DiagnosticAssetReference> Assets { get; }
        internal IReadOnlyList<F10V7DiagnosticAssetReference> SourceAssets { get; }
        internal IReadOnlyList<F10V7DiagnosticAssetReference> PrefabAssets { get; }
        internal IReadOnlyList<string> MissingPhysicalEvidenceIds { get; }
        internal bool PhysicalDescriptorInstallable { get; }
        internal bool BundleInstallable { get; }
        internal string ExporterSourceSha256 { get; }
        internal string ExporterMetaSha256 { get; }
        internal F10V7VerifiedPhysicalInputs VerifiedInputs { get; }
        internal F10V7DiagnosticAssetReference RequireAsset(string path) => _assets.TryGetValue(path, out var row)
            ? row : throw new FileNotFoundException("No exact physical/source asset was retained for " + path);
    }

    internal sealed class F10V7DiagnosticAssetReference
    {
        internal F10V7DiagnosticAssetReference(string assetPath, string guid, string assetSha256,
            string metaSha256, UnityEngine.Object asset)
        { AssetPath = assetPath; Guid = guid; AssetSha256 = assetSha256; MetaSha256 = metaSha256; Asset = asset; }
        internal string AssetPath { get; }
        internal string Guid { get; }
        internal string AssetSha256 { get; }
        internal string MetaSha256 { get; }
        internal UnityEngine.Object Asset { get; }
    }

    internal sealed class F10V7DiagnosticCatalog
    {
        internal F10V7DiagnosticCatalog(EncounterCatalogManifest manifest, EncounterCatalogRegistryV1 registry,
            EncounterPlanPinV1 pin, IReadOnlyList<ResolvedEncounterPlanV1> resolvedPlans,
            string manifestSha256, F10V7DiagnosticPhysicalInputs physicalInputs)
        {
            Manifest = manifest; Registry = registry; Pin = pin;
            ResolvedPlans = Array.AsReadOnly(resolvedPlans.ToArray());
            ManifestSha256 = manifestSha256; PhysicalInputs = physicalInputs;
        }
        internal EncounterCatalogManifest Manifest { get; }
        internal EncounterCatalogRegistryV1 Registry { get; }
        internal EncounterPlanPinV1 Pin { get; }
        internal IReadOnlyList<ResolvedEncounterPlanV1> ResolvedPlans { get; }
        internal string ManifestSha256 { get; }
        internal F10V7DiagnosticPhysicalInputs PhysicalInputs { get; }
    }

    internal static class F10V7DiagnosticOpeningPathPolicy
    {
        private const string StoresRelativePath = "Logs/F10V7DiagnosticOpening-20261120/stores";
        private static readonly System.Text.RegularExpressions.Regex RevisionPattern =
            new System.Text.RegularExpressions.Regex("^[0-9]{8}T[0-9]{13}Z-[0-9a-f]{32}$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        internal static string ResolveBundleDirectory(string projectRoot, string revisionId)
        {
            return F10V7VerifiedPhysicalInputs.ResolveBundleDirectory(projectRoot, revisionId);
        }

        internal static string CreateUniqueStoreDirectory(string projectRoot, string persistentDataPath)
        {
            string root = Path.GetFullPath(projectRoot);
            string storesRoot = Path.GetFullPath(Path.Combine(root, StoresRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            EnsureContainedAndDisjoint(storesRoot, root, persistentDataPath);
            if (File.Exists(storesRoot)) throw new IOException("The technical store root is occupied by a file; it was preserved.");
            if (!Directory.Exists(storesRoot)) Directory.CreateDirectory(storesRoot);
            EnsureNoReparsePoints(storesRoot, root);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                string candidate = Path.GetFullPath(Path.Combine(storesRoot, Guid.NewGuid().ToString("N")));
                EnsureContainedAndDisjoint(candidate, storesRoot, persistentDataPath);
                if (Directory.Exists(candidate) || File.Exists(candidate)) continue;
                Directory.CreateDirectory(candidate);
                EnsureNoReparsePoints(candidate, root);
                if (Directory.GetFileSystemEntries(candidate).Length != 0)
                    throw new IOException("The unique technical store directory was not empty; existing content was preserved.");
                return candidate;
            }
            throw new IOException("A unique isolated technical store could not be reserved.");
        }

        internal static void EnsureContainedAndDisjoint(string candidate, string containmentRoot, string persistentDataPath)
        {
            string normalized = Path.GetFullPath(candidate);
            string root = Path.GetFullPath(containmentRoot);
            string persistent = Path.GetFullPath(persistentDataPath ?? string.Empty);
            if (!IsEqualOrWithin(normalized, root) || PathsOverlap(normalized, persistent))
                throw new InvalidDataException("The diagnostic path is outside its disposable-copy root or overlaps persistentDataPath.");
        }

        internal static bool IsPathWithinStore(string candidateFile, string storeDirectory)
        {
            if (string.IsNullOrWhiteSpace(candidateFile) || string.IsNullOrWhiteSpace(storeDirectory)) return false;
            string candidate = Path.GetFullPath(candidateFile);
            string store = Path.GetFullPath(storeDirectory);
            string prefix = store.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        internal static void EnsureRequiredRoots(IEnumerable<string> roots, IEnumerable<string> required)
        {
            var present = new HashSet<string>(roots ?? Array.Empty<string>(), StringComparer.Ordinal);
            foreach (string path in required ?? Array.Empty<string>())
                if (!present.Contains(path)) throw new FileNotFoundException("A required physical asset root is absent: " + path);
        }

        private static void EnsureNoReparsePoints(string target, string stopAt)
        {
            string root = Path.GetFullPath(stopAt);
            string current = Path.GetFullPath(target);
            while (IsEqualOrWithin(current, root))
            {
                if (File.Exists(current) || Directory.Exists(current))
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Reparse points are forbidden in the isolated diagnostic path: " + current);
                if (PathsEqual(current, root)) return;
                current = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(current)) break;
            }
            throw new InvalidDataException("The diagnostic path is not contained in its verified disposable project.");
        }

        private static bool IsEqualOrWithin(string candidate, string root)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar);
            return PathsEqual(normalizedCandidate, Path.GetFullPath(root)) || normalizedCandidate.StartsWith(normalizedRoot,
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsOverlap(string left, string right) =>
            IsEqualOrWithin(left, right) || IsEqualOrWithin(right, left);

        private static bool PathsEqual(string left, string right) =>
            string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
#endif
