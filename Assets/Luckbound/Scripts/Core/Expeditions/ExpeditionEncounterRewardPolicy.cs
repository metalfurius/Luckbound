using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Randomness;

namespace Luckbound.Core.Expeditions
{
    public enum ExpeditionEncounterRewardEnemyKind
    {
        Normal = 0,
        Elite = 1,
        Guardian = 2
    }

    /// <summary>
    /// Stable caller-supplied identity for one physical spawn. A scene instance ID or generated actor ID is not a
    /// valid substitute because this key must survive room teardown, application restart, and retry.
    /// </summary>
    public sealed class ExpeditionEncounterRewardSpawn
    {
        public ExpeditionEncounterRewardSpawn(string spawnKey, ExpeditionEncounterRewardEnemyKind enemyKind,
            int vinculoSequenceOrdinal = 0)
        {
            if (!IsStableText(spawnKey))
                throw new ArgumentException("A reward spawn requires a non-empty stable spawn key without whitespace.", nameof(spawnKey));
            if (!Enum.IsDefined(typeof(ExpeditionEncounterRewardEnemyKind), enemyKind))
                throw new ArgumentOutOfRangeException(nameof(enemyKind));
            if (vinculoSequenceOrdinal < 0 || vinculoSequenceOrdinal > 3)
                throw new ArgumentOutOfRangeException(nameof(vinculoSequenceOrdinal), "Vínculo elite ordinal must be 0 or 1–3.");
            if (vinculoSequenceOrdinal > 0 && enemyKind != ExpeditionEncounterRewardEnemyKind.Elite)
                throw new ArgumentException("Only an elite can carry a Vínculo sequence ordinal.", nameof(vinculoSequenceOrdinal));

            SpawnKey = spawnKey;
            EnemyKind = enemyKind;
            VinculoSequenceOrdinal = vinculoSequenceOrdinal;
        }

        public string SpawnKey { get; }
        public ExpeditionEncounterRewardEnemyKind EnemyKind { get; }
        /// <summary>Zero for an ordinary spawn; 1–3 for one of the three explicitly scheduled Vínculo elites.</summary>
        public int VinculoSequenceOrdinal { get; }

        private static bool IsStableText(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsWhiteSpace);
        }
    }

    /// <summary>
    /// Immutable facts needed to reconstruct the policy output. Pact state and the stable identity of the physical
    /// exploration cache are explicit inputs; E1/E2 requests must declare the cache required by their template.
    /// </summary>
    public sealed class ExpeditionEncounterRewardRequest
    {
        private readonly IReadOnlyList<ExpeditionEncounterRewardSpawn> _spawns;

        public ExpeditionEncounterRewardRequest(RunId runId, uint seed, int generationVersion, string nodeId,
            string templateId, string sectorId, ExpeditionRouteNodeKind nodeKind,
            IEnumerable<ExpeditionEncounterRewardSpawn> spawns, bool avaricePactActive = false,
            bool vinculoPactActive = false, bool hasExplorationCache = false, string explorationCacheKey = null)
        {
            if (!runId.IsValid) throw new ArgumentException("A reward request requires a valid RunId.", nameof(runId));
            if (generationVersion <= 0) throw new ArgumentOutOfRangeException(nameof(generationVersion));
            if (!IsStableText(nodeId)) throw new ArgumentException("A stable route node ID is required.", nameof(nodeId));
            if (!IsStableText(templateId)) throw new ArgumentException("A stable room template ID is required.", nameof(templateId));
            if (sectorId == null || (sectorId.Length > 0 && !IsStableText(sectorId)))
                throw new ArgumentException("Sector ID must be empty or a non-empty stable ID without whitespace.", nameof(sectorId));
            if (hasExplorationCache && !IsStableText(explorationCacheKey))
                throw new ArgumentException("A declared physical exploration cache requires a stable cache key.", nameof(explorationCacheKey));
            if (!hasExplorationCache && explorationCacheKey != null)
                throw new ArgumentException("A cache key cannot be supplied when no physical exploration cache is declared.", nameof(explorationCacheKey));
            if (!Enum.IsDefined(typeof(ExpeditionRouteNodeKind), nodeKind))
                throw new ArgumentOutOfRangeException(nameof(nodeKind));
            if (spawns == null) throw new ArgumentNullException(nameof(spawns));

            ExpeditionEncounterRewardSpawn[] copy = spawns.ToArray();
            if (copy.Any(spawn => spawn == null))
                throw new ArgumentException("Reward spawn entries cannot be null.", nameof(spawns));

            RunId = runId;
            Seed = seed;
            GenerationVersion = generationVersion;
            NodeId = nodeId;
            TemplateId = templateId;
            SectorId = sectorId;
            NodeKind = nodeKind;
            AvaricePactActive = avaricePactActive;
            VinculoPactActive = vinculoPactActive;
            HasExplorationCache = hasExplorationCache;
            ExplorationCacheKey = explorationCacheKey;
            _spawns = Array.AsReadOnly(copy);
        }

        public RunId RunId { get; }
        public uint Seed { get; }
        public int GenerationVersion { get; }
        public string NodeId { get; }
        public string TemplateId { get; }
        public string SectorId { get; }
        public ExpeditionRouteNodeKind NodeKind { get; }
        public bool AvaricePactActive { get; }
        public bool VinculoPactActive { get; }
        public bool HasExplorationCache { get; }
        public string ExplorationCacheKey { get; }
        public IReadOnlyList<ExpeditionEncounterRewardSpawn> Spawns => _spawns;

        private static bool IsStableText(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsWhiteSpace);
        }
    }

    /// <summary>One unit of material loot for a physical world pickup; no inventory or race bonus is applied here.</summary>
    public sealed class ExpeditionMaterialRewardGrant
    {
        internal ExpeditionMaterialRewardGrant(string grantId, MaterialKind material)
        {
            GrantId = grantId;
            Material = material;
            Quantity = 1;
        }

        public string GrantId { get; }
        public MaterialKind Material { get; }
        /// <summary>One physical material unit; separate drops may resolve to the same material kind.</summary>
        public int Quantity { get; }
    }

    /// <summary>Room-clear payout only; enemy drops are represented by separate outcomes.</summary>
    public sealed class ExpeditionRoomCompletionPayout
    {
        internal ExpeditionRoomCompletionPayout(int gold, int renown, string goldGrantId, string renownGrantId)
        {
            Gold = gold;
            Renown = renown;
            GoldGrantId = goldGrantId;
            RenownGrantId = renownGrantId;
        }

        public int Gold { get; }
        public int Renown { get; }
        public string GoldGrantId { get; }
        public string RenownGrantId { get; }
        public bool HasReward => Gold > 0 || Renown > 0;
    }

    /// <summary>Gold attached to one physical exploration-cache pickup, paid only when that pickup is collected.</summary>
    public sealed class ExpeditionExplorationCacheReward
    {
        internal ExpeditionExplorationCacheReward(string cacheKey, int gold, string goldGrantId)
        {
            CacheKey = cacheKey;
            Gold = gold;
            GoldGrantId = goldGrantId;
        }

        public string CacheKey { get; }
        public int Gold { get; }
        public string GoldGrantId { get; }
    }

    public enum ExpeditionRewardOfferKind
    {
        TreasureEquipmentOrRelic = 0,
        Pact = 1
    }

    /// <summary>Stable request to create a content-defined offer; it contains no offer choices or payload.</summary>
    public sealed class ExpeditionRewardOfferRequirement
    {
        internal ExpeditionRewardOfferRequirement(string offerId, ExpeditionRewardOfferKind kind)
        {
            OfferId = offerId;
            Kind = kind;
        }

        public string OfferId { get; }
        public ExpeditionRewardOfferKind Kind { get; }
    }

    /// <summary>Reward result for one stable enemy spawn.</summary>
    public sealed class ExpeditionEnemyRewardOutcome
    {
        private readonly IReadOnlyList<ExpeditionMaterialRewardGrant> _baseMaterialGrants;
        private readonly IReadOnlyList<ExpeditionMaterialRewardGrant> _vinculoMaterialGrants;

        internal ExpeditionEnemyRewardOutcome(string spawnKey, ExpeditionEncounterRewardEnemyKind enemyKind,
            int vinculoSequenceOrdinal, int baseGold, int vinculoBonusGold, int totalGold, string goldGrantId,
            IEnumerable<ExpeditionMaterialRewardGrant> baseMaterialGrants,
            IEnumerable<ExpeditionMaterialRewardGrant> vinculoMaterialGrants, int fortuneCharges,
            string fortuneChargeGrantId, string relicOfferId)
        {
            SpawnKey = spawnKey;
            EnemyKind = enemyKind;
            VinculoSequenceOrdinal = vinculoSequenceOrdinal;
            BaseGold = baseGold;
            VinculoBonusGold = vinculoBonusGold;
            TotalGold = totalGold;
            GoldGrantId = goldGrantId;
            _baseMaterialGrants = Array.AsReadOnly(baseMaterialGrants.ToArray());
            _vinculoMaterialGrants = Array.AsReadOnly(vinculoMaterialGrants.ToArray());
            FortuneCharges = fortuneCharges;
            FortuneChargeGrantId = fortuneChargeGrantId;
            RelicOfferId = relicOfferId;
        }

        public string SpawnKey { get; }
        public ExpeditionEncounterRewardEnemyKind EnemyKind { get; }
        public int VinculoSequenceOrdinal { get; }
        /// <summary>Fixed enemy gold before Avarice and before any Vínculo addition.</summary>
        public int BaseGold { get; }
        /// <summary>Vínculo's separate fixed +15 gold component before Avarice; zero for other enemies.</summary>
        public int VinculoBonusGold { get; }
        /// <summary>The amount to grant after applying Avarice once to this enemy's combined gold source.</summary>
        public int TotalGold { get; }
        public string GoldGrantId { get; }
        public IReadOnlyList<ExpeditionMaterialRewardGrant> BaseMaterialGrants => _baseMaterialGrants;
        public IReadOnlyList<ExpeditionMaterialRewardGrant> VinculoMaterialGrants => _vinculoMaterialGrants;
        public int FortuneCharges { get; }
        public string FortuneChargeGrantId { get; }
        /// <summary>Stable ID for the third Vínculo elite's required relic offer; null for other spawns.</summary>
        public string RelicOfferId { get; }
        public bool RelicOfferRequired => RelicOfferId != null;
    }

    public sealed class ExpeditionEncounterRewardResolution
    {
        private readonly IReadOnlyList<ExpeditionEnemyRewardOutcome> _enemyOutcomes;

        internal ExpeditionEncounterRewardResolution(string resolutionId, ExpeditionRoomCompletionPayout roomPayout,
            ExpeditionExplorationCacheReward explorationCacheReward, ExpeditionRewardOfferRequirement offerRequirement,
            IEnumerable<ExpeditionEnemyRewardOutcome> enemyOutcomes)
        {
            ResolutionId = resolutionId;
            RoomPayout = roomPayout;
            ExplorationCacheReward = explorationCacheReward;
            OfferRequirement = offerRequirement;
            _enemyOutcomes = Array.AsReadOnly(enemyOutcomes.ToArray());
        }

        public string ResolutionId { get; }
        public ExpeditionRoomCompletionPayout RoomPayout { get; }
        public ExpeditionExplorationCacheReward ExplorationCacheReward { get; }
        public ExpeditionRewardOfferRequirement OfferRequirement { get; }
        public IReadOnlyList<ExpeditionEnemyRewardOutcome> EnemyOutcomes => _enemyOutcomes;
    }

    /// <summary>
    /// Pure deterministic reward policy. The versioned SeedDeriver "loot" domain derives an isolated stream for
    /// each material chance and spawn/drop-slot selection, independent of UI, audio, spawn ordering, other enemies,
    /// and future random consumers. Material selections use the sector pool with replacement, so two unit drops
    /// may share a material kind.
    /// Stable grant IDs hash the full normalized run/seed/version/node/template/sector/kind/spawn/grant identity
    /// with SHA-256.
    /// </summary>
    public static class ExpeditionEncounterRewardPolicy
    {
        private const string LootDomain = "loot";
        private const string RewardIdPrefix = "encounter-reward.v1:";
        private const string RewardIdDomain = "luckbound-encounter-reward-id-v1";

        public static ExpeditionEncounterRewardResolution Resolve(ExpeditionEncounterRewardRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            ValidateRouteContext(request);
            ValidateSpawns(request);

            ExpeditionRoomCompletionPayout roomPayout = ResolveRoomPayout(request);
            ExpeditionExplorationCacheReward cacheReward = ResolveExplorationCacheReward(request);
            ExpeditionRewardOfferRequirement offerRequirement = ResolveOfferRequirement(request);
            ExpeditionEnemyRewardOutcome[] enemies = request.Spawns
                .OrderBy(spawn => spawn.SpawnKey, StringComparer.Ordinal)
                .Select(spawn => ResolveEnemy(request, spawn))
                .ToArray();
            string resolutionId = BuildStableId(request, "resolution", request.ExplorationCacheKey ?? string.Empty,
                "room-resolution", 0);
            return new ExpeditionEncounterRewardResolution(resolutionId, roomPayout, cacheReward, offerRequirement, enemies);
        }

        private static void ValidateRouteContext(ExpeditionEncounterRewardRequest request)
        {
            string sector = request.SectorId;
            string template = request.TemplateId;
            switch (request.NodeKind)
            {
                case ExpeditionRouteNodeKind.Combat:
                case ExpeditionRouteNodeKind.OptionalCombat:
                    RequireSector(sector);
                    RequireSectorTemplate(template, sector, 'c', 1, 5);
                    break;
                case ExpeditionRouteNodeKind.Exploration:
                    RequireSector(sector);
                    RequireSectorTemplate(template, sector, 'e', 1, 2);
                    if (!request.HasExplorationCache)
                        throw new InvalidOperationException("Every E1/E2 exploration template contains a physical cache that requires a stable cache key.");
                    break;
                case ExpeditionRouteNodeKind.Treasure:
                    RequireSector(sector);
                    RequireExact(template, "room." + sector + ".t");
                    break;
                case ExpeditionRouteNodeKind.Pact:
                    RequireSector(sector);
                    RequireExact(template, "room." + sector + ".p");
                    break;
                case ExpeditionRouteNodeKind.Narrative:
                    RequireSector(sector);
                    RequireExact(template, "room." + sector + ".n");
                    break;
                case ExpeditionRouteNodeKind.Guardian:
                    RequireSector(sector);
                    RequireExact(template, "room." + sector + ".guardian");
                    break;
                case ExpeditionRouteNodeKind.Tutorial:
                    RequireExact(sector, "patio");
                    RequireExact(template, "room.sample.tutorial");
                    if (request.HasExplorationCache)
                        throw new InvalidOperationException("Tutorial rooms cannot declare an exploration cache.");
                    break;
                case ExpeditionRouteNodeKind.SafeExtraction:
                    RequireExact(sector, "patio");
                    RequireExact(template, "room.sample.extraction");
                    if (request.HasExplorationCache)
                        throw new InvalidOperationException("Extraction rooms cannot declare an exploration cache.");
                    break;
                case ExpeditionRouteNodeKind.Rest:
                    RequireExact(sector, string.Empty);
                    RequireExact(template, "room.hub.rest");
                    if (request.HasExplorationCache)
                        throw new InvalidOperationException("Rest rooms cannot declare an exploration cache.");
                    break;
                case ExpeditionRouteNodeKind.FinalBoss:
                    RequireExact(sector, string.Empty);
                    RequireExact(template, "room.final.arena");
                    if (request.HasExplorationCache)
                        throw new InvalidOperationException("The final boss room cannot declare an exploration cache.");
                    break;
                default:
                    throw new InvalidOperationException("Unsupported route node kind for encounter rewards: " + request.NodeKind + ".");
            }

            if (request.NodeKind != ExpeditionRouteNodeKind.Exploration && request.HasExplorationCache)
                throw new InvalidOperationException("Only an Exploration node can declare an exploration cache.");
        }

        private static void ValidateSpawns(ExpeditionEncounterRewardRequest request)
        {
            string[] keys = request.Spawns.Select(spawn => spawn.SpawnKey).ToArray();
            if (keys.Distinct(StringComparer.Ordinal).Count() != keys.Length)
                throw new InvalidOperationException("Reward spawn keys must be unique within a room.");

            switch (request.NodeKind)
            {
                case ExpeditionRouteNodeKind.Combat:
                case ExpeditionRouteNodeKind.OptionalCombat:
                    if (request.Spawns.Count == 0)
                        throw new InvalidOperationException("A combat completion requires at least one resolved enemy spawn.");

                    int ordinaryEliteCount = 0;
                    int vinculoEliteCount = 0;
                    var vinculoOrdinals = new HashSet<int>();
                    foreach (ExpeditionEncounterRewardSpawn spawn in request.Spawns)
                    {
                        if (spawn.EnemyKind == ExpeditionEncounterRewardEnemyKind.Guardian)
                            throw new InvalidOperationException("Guardian reward entries require a Guardian route node.");
                        if (spawn.VinculoSequenceOrdinal > 0)
                        {
                            if (!request.VinculoPactActive)
                                throw new InvalidOperationException("A Vínculo elite requires the active Vínculo pact in the request.");
                            vinculoEliteCount++;
                            if (!vinculoOrdinals.Add(spawn.VinculoSequenceOrdinal))
                                throw new InvalidOperationException("Vínculo sequence ordinals must be unique within a room.");
                        }
                        else if (spawn.EnemyKind == ExpeditionEncounterRewardEnemyKind.Elite)
                        {
                            ordinaryEliteCount++;
                        }
                    }

                    if (ordinaryEliteCount > 1 || vinculoEliteCount > 1)
                        throw new InvalidOperationException("A room may contain at most one ordinary elite and one scheduled Vínculo elite.");
                    break;
                case ExpeditionRouteNodeKind.Guardian:
                    if (request.Spawns.Count != 1 || request.Spawns[0].EnemyKind != ExpeditionEncounterRewardEnemyKind.Guardian)
                        throw new InvalidOperationException("A Guardian room requires exactly one Guardian reward spawn.");
                    break;
                default:
                    if (request.Spawns.Count != 0)
                        throw new InvalidOperationException("This route node kind has no declared enemy reward schedule.");
                    break;
            }
        }

        private static ExpeditionRoomCompletionPayout ResolveRoomPayout(ExpeditionEncounterRewardRequest request)
        {
            int baseGold = 0;
            int renown = 0;
            switch (request.NodeKind)
            {
                case ExpeditionRouteNodeKind.Combat:
                case ExpeditionRouteNodeKind.OptionalCombat:
                    baseGold = GetCombatRoomGold(request.SectorId);
                    renown = 5;
                    break;
                case ExpeditionRouteNodeKind.Exploration:
                    renown = 3;
                    break;
                case ExpeditionRouteNodeKind.Treasure:
                case ExpeditionRouteNodeKind.Pact:
                case ExpeditionRouteNodeKind.Narrative:
                    renown = 3;
                    break;
                case ExpeditionRouteNodeKind.Guardian:
                    renown = GetGuardianRenown(request.SectorId);
                    break;
                case ExpeditionRouteNodeKind.FinalBoss:
                    renown = 60;
                    break;
            }

            int gold = ApplyAvarice(baseGold, request.AvaricePactActive);
            string goldGrantId = gold > 0
                ? BuildStableId(request, "room", string.Empty, "room.gold", 0)
                : null;
            string renownGrantId = renown > 0
                ? BuildStableId(request, "room", string.Empty, "room.renown", 0)
                : null;
            return new ExpeditionRoomCompletionPayout(gold, renown, goldGrantId, renownGrantId);
        }

        private static ExpeditionExplorationCacheReward ResolveExplorationCacheReward(
            ExpeditionEncounterRewardRequest request)
        {
            if (!request.HasExplorationCache) return null;

            int gold = ApplyAvarice(GetExplorationCacheGold(request.SectorId), request.AvaricePactActive);
            string grantId = BuildStableId(request, "cache", request.ExplorationCacheKey,
                "exploration-cache.gold", 0);
            return new ExpeditionExplorationCacheReward(request.ExplorationCacheKey, gold, grantId);
        }

        private static ExpeditionRewardOfferRequirement ResolveOfferRequirement(
            ExpeditionEncounterRewardRequest request)
        {
            ExpeditionRewardOfferKind kind;
            switch (request.NodeKind)
            {
                case ExpeditionRouteNodeKind.Treasure:
                    kind = ExpeditionRewardOfferKind.TreasureEquipmentOrRelic;
                    break;
                case ExpeditionRouteNodeKind.Pact:
                    kind = ExpeditionRewardOfferKind.Pact;
                    break;
                default:
                    return null;
            }

            string offerId = BuildStableId(request, "room", string.Empty,
                "room.offer." + kind.ToString(), 0);
            return new ExpeditionRewardOfferRequirement(offerId, kind);
        }

        private static ExpeditionEnemyRewardOutcome ResolveEnemy(ExpeditionEncounterRewardRequest request,
            ExpeditionEncounterRewardSpawn spawn)
        {
            int baseGold;
            int fortuneCharges = 0;
            var baseMaterials = new List<ExpeditionMaterialRewardGrant>();
            switch (spawn.EnemyKind)
            {
                case ExpeditionEncounterRewardEnemyKind.Normal:
                    baseGold = GetNormalEnemyGold(request.SectorId);
                    if (CreateRandom(request, LootDomain, spawn, "normal-drop-roll", 0).NextInt(0, 100) < 50)
                    {
                        MaterialKind dropped = SelectMaterial(request, spawn, "normal-drop", 0);
                        baseMaterials.Add(CreateMaterialGrant(request, spawn, "normal-drop", 0, dropped));
                    }
                    break;
                case ExpeditionEncounterRewardEnemyKind.Elite:
                    baseGold = GetNormalEnemyGold(request.SectorId) * 3;
                    for (int slot = 0; slot < 2; slot++)
                    {
                        MaterialKind material = SelectMaterial(request, spawn, "elite-guaranteed", slot);
                        baseMaterials.Add(CreateMaterialGrant(request, spawn, "elite-guaranteed", slot, material));
                    }
                    break;
                case ExpeditionEncounterRewardEnemyKind.Guardian:
                    baseGold = GetGuardianGold(request.SectorId);
                    fortuneCharges = 1;
                    break;
                default:
                    throw new InvalidOperationException("Unsupported enemy reward kind: " + spawn.EnemyKind + ".");
            }

            int vinculoRawGold = spawn.VinculoSequenceOrdinal > 0 ? 15 : 0;
            int adjustedTotalGold = ApplyAvarice(checked(baseGold + vinculoRawGold), request.AvaricePactActive);
            var vinculoMaterials = new List<ExpeditionMaterialRewardGrant>();
            if (spawn.VinculoSequenceOrdinal > 0)
            {
                for (int slot = 0; slot < 2; slot++)
                {
                    MaterialKind material = SelectMaterial(request, spawn, "vinculo-extra", slot);
                    vinculoMaterials.Add(CreateMaterialGrant(request, spawn, "vinculo-extra", slot, material));
                }
            }

            string goldGrantId = adjustedTotalGold > 0
                ? BuildStableId(request, "enemy", spawn.SpawnKey, "enemy.gold", 0)
                : null;
            string fortuneGrantId = fortuneCharges > 0
                ? BuildStableId(request, "enemy", spawn.SpawnKey, "guardian.fortune-charge", 0)
                : null;
            string relicOfferId = spawn.VinculoSequenceOrdinal == 3
                ? BuildStableId(request, "enemy", spawn.SpawnKey, "vinculo.relic-offer", spawn.VinculoSequenceOrdinal)
                : null;

            return new ExpeditionEnemyRewardOutcome(spawn.SpawnKey, spawn.EnemyKind,
                spawn.VinculoSequenceOrdinal, baseGold, vinculoRawGold, adjustedTotalGold, goldGrantId,
                baseMaterials, vinculoMaterials, fortuneCharges, fortuneGrantId, relicOfferId);
        }

        private static ExpeditionMaterialRewardGrant CreateMaterialGrant(ExpeditionEncounterRewardRequest request,
            ExpeditionEncounterRewardSpawn spawn, string source, int slot, MaterialKind material)
        {
            return new ExpeditionMaterialRewardGrant(BuildStableId(request, "enemy", spawn.SpawnKey,
                "enemy.material." + source, slot), material);
        }

        private static MaterialKind SelectMaterial(ExpeditionEncounterRewardRequest request,
            ExpeditionEncounterRewardSpawn spawn, string source, int slot)
        {
            int roll = CreateRandom(request, LootDomain, spawn, source, slot).NextInt(0, 100);
            switch (request.SectorId)
            {
                case "patio":
                    if (roll < 40) return MaterialKind.UmbraHerb;
                    if (roll < 75) return MaterialKind.ShadowWood;
                    return MaterialKind.DarkResin;
                case "archivo":
                    if (roll < 40) return MaterialKind.Ore;
                    if (roll < 80) return MaterialKind.ArcaneDust;
                    return MaterialKind.UmbraHerb;
                case "forjas":
                    if (roll < 40) return MaterialKind.Ore;
                    if (roll < 70) return MaterialKind.VitalEmber;
                    return MaterialKind.DarkResin;
                default:
                    throw new InvalidOperationException("No material distribution is defined for sector " + request.SectorId + ".");
            }
        }

        private static Xorshift32 CreateRandom(ExpeditionEncounterRewardRequest request, string domain,
            ExpeditionEncounterRewardSpawn spawn, string purpose, int slot)
        {
            uint derivedSeed = SeedDeriver.Derive(request.GenerationVersion, request.Seed, domain,
                request.RunId.ToString(), request.NodeId, request.TemplateId, request.SectorId,
                request.NodeKind.ToString(), spawn.SpawnKey, spawn.EnemyKind.ToString(), purpose,
                slot.ToString(CultureInfo.InvariantCulture));
            return new Xorshift32(derivedSeed);
        }

        private static int GetNormalEnemyGold(string sector)
        {
            switch (sector)
            {
                case "patio": return 3;
                case "archivo": return 5;
                case "forjas": return 7;
                default: throw new InvalidOperationException("No enemy gold amount is defined for sector " + sector + ".");
            }
        }

        private static int GetCombatRoomGold(string sector)
        {
            switch (sector)
            {
                case "patio": return 12;
                case "archivo": return 18;
                case "forjas": return 24;
                default: throw new InvalidOperationException("No combat room gold amount is defined for sector " + sector + ".");
            }
        }

        private static int GetExplorationCacheGold(string sector)
        {
            switch (sector)
            {
                case "patio": return 15;
                case "archivo": return 25;
                case "forjas": return 35;
                default: throw new InvalidOperationException("No exploration cache gold amount is defined for sector " + sector + ".");
            }
        }

        private static int GetGuardianGold(string sector)
        {
            switch (sector)
            {
                case "patio": return 40;
                case "archivo": return 70;
                case "forjas": return 100;
                default: throw new InvalidOperationException("No Guardian gold amount is defined for sector " + sector + ".");
            }
        }

        private static int GetGuardianRenown(string sector)
        {
            switch (sector)
            {
                case "patio": return 20;
                case "archivo": return 30;
                case "forjas": return 40;
                default: throw new InvalidOperationException("No Guardian renown amount is defined for sector " + sector + ".");
            }
        }

        private static int ApplyAvarice(int gold, bool active)
        {
            if (!active || gold == 0) return gold;
            return decimal.ToInt32(decimal.Floor(gold * 1.35m));
        }

        private static void RequireSector(string sector)
        {
            if (sector != "patio" && sector != "archivo" && sector != "forjas")
                throw new InvalidOperationException("Reward sector must be patio, archivo, or forjas; found " + (sector ?? "<null>") + ".");
        }

        private static void RequireSectorTemplate(string template, string sector, char roomCode,
            int minimumIndex, int maximumIndex)
        {
            for (int index = minimumIndex; index <= maximumIndex; index++)
            {
                string expected = "room." + sector + "." + roomCode + index.ToString(CultureInfo.InvariantCulture);
                if (string.Equals(template, expected, StringComparison.Ordinal)) return;
            }
            throw new InvalidOperationException("Template " + template + " is inconsistent with " + sector + " " + roomCode + " node kind.");
        }

        private static void RequireExact(string actual, string expected)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidOperationException("Expected stable reward context value " + expected + "; found " + (actual ?? "<null>") + ".");
        }

        private static string BuildStableId(ExpeditionEncounterRewardRequest request, string subjectKind,
            string spawnKey, string grantKind, int ordinal)
        {
            var canonical = new StringBuilder(256);
            canonical.Append(RewardIdDomain).Append('|');
            AppendField(canonical, request.RunId.ToString());
            AppendField(canonical, Xorshift32.NormalizeSeed(request.Seed).ToString("X8", CultureInfo.InvariantCulture));
            AppendField(canonical, request.GenerationVersion.ToString(CultureInfo.InvariantCulture));
            AppendField(canonical, request.NodeId);
            AppendField(canonical, request.TemplateId);
            AppendField(canonical, request.SectorId);
            AppendField(canonical, request.NodeKind.ToString());
            AppendField(canonical, subjectKind);
            AppendField(canonical, spawnKey ?? string.Empty);
            AppendField(canonical, grantKind);
            AppendField(canonical, ordinal.ToString(CultureInfo.InvariantCulture));

            byte[] digest;
            using (SHA256 sha256 = SHA256.Create()) digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            var hex = new StringBuilder(digest.Length * 2);
            for (int index = 0; index < digest.Length; index++)
                hex.Append(digest[index].ToString("x2", CultureInfo.InvariantCulture));
            return RewardIdPrefix + hex;
        }

        private static void AppendField(StringBuilder builder, string value)
        {
            builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(value);
            builder.Append('|');
        }
    }
}
