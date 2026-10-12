using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Luckbound.Core.Randomness;

namespace Luckbound.Core.Expeditions
{
    public enum ExpeditionRouteNodeKind
    {
        Combat,
        Exploration,
        Treasure,
        Pact,
        Narrative,
        Guardian,
        Rest,
        FinalBoss,
        OptionalCombat
    }

    public sealed class ExpeditionRouteSectorCatalog
    {
        public ExpeditionRouteSectorCatalog(
            string sectorId,
            IEnumerable<string> combatTemplateIds,
            IEnumerable<string> explorationTemplateIds,
            string treasureTemplateId,
            string pactTemplateId,
            string narrativeTemplateId,
            string guardianTemplateId)
        {
            SectorId = sectorId;
            CombatTemplateIds = Copy(combatTemplateIds);
            ExplorationTemplateIds = Copy(explorationTemplateIds);
            TreasureTemplateId = treasureTemplateId;
            PactTemplateId = pactTemplateId;
            NarrativeTemplateId = narrativeTemplateId;
            GuardianTemplateId = guardianTemplateId;
        }

        public string SectorId { get; }
        public IReadOnlyList<string> CombatTemplateIds { get; }
        public IReadOnlyList<string> ExplorationTemplateIds { get; }
        public string TreasureTemplateId { get; }
        public string PactTemplateId { get; }
        public string NarrativeTemplateId { get; }
        public string GuardianTemplateId { get; }

        private static IReadOnlyList<string> Copy(IEnumerable<string> values)
        {
            return Array.AsReadOnly(values == null ? Array.Empty<string>() : values.ToArray());
        }
    }

    public sealed class ExpeditionRouteCatalog
    {
        private static readonly string[] RequiredSectors = { "patio", "archivo", "forjas" };
        private readonly IReadOnlyList<ExpeditionRouteSectorCatalog> _sectors;

        public ExpeditionRouteCatalog(
            IEnumerable<ExpeditionRouteSectorCatalog> sectors,
            string restTemplateId,
            string finalBossTemplateId)
        {
            _sectors = Array.AsReadOnly(sectors == null
                ? Array.Empty<ExpeditionRouteSectorCatalog>()
                : sectors.ToArray());
            RestTemplateId = restTemplateId;
            FinalBossTemplateId = finalBossTemplateId;
        }

        public IReadOnlyList<ExpeditionRouteSectorCatalog> Sectors => _sectors;
        public string RestTemplateId { get; }
        public string FinalBossTemplateId { get; }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (_sectors.Count != RequiredSectors.Length)
            {
                errors.Add("exactly three sectors are required in Patio, Archivo, Forjas order");
            }

            var allTemplateIds = new HashSet<string>(StringComparer.Ordinal);
            int sectorCount = Math.Min(_sectors.Count, RequiredSectors.Length);
            for (int index = 0; index < sectorCount; index++)
            {
                ExpeditionRouteSectorCatalog sector = _sectors[index];
                if (sector == null)
                {
                    errors.Add("sector " + index + " is missing");
                    continue;
                }

                string expectedSector = RequiredSectors[index];
                if (!string.Equals(sector.SectorId, expectedSector, StringComparison.Ordinal))
                {
                    errors.Add("sector " + index + " must be " + expectedSector);
                }

                ValidateSequence(sector.CombatTemplateIds, expectedSector, "c", 5, allTemplateIds, errors);
                ValidateSequence(sector.ExplorationTemplateIds, expectedSector, "e", 2, allTemplateIds, errors);
                ValidateTemplate(sector.TreasureTemplateId, "room." + expectedSector + ".t", allTemplateIds, errors);
                ValidateTemplate(sector.PactTemplateId, "room." + expectedSector + ".p", allTemplateIds, errors);
                ValidateTemplate(sector.NarrativeTemplateId, "room." + expectedSector + ".n", allTemplateIds, errors);
                ValidateTemplate(sector.GuardianTemplateId, "room." + expectedSector + ".guardian", allTemplateIds, errors);
            }

            ValidateTemplate(RestTemplateId, "room.hub.rest", allTemplateIds, errors);
            ValidateTemplate(FinalBossTemplateId, "room.final.arena", allTemplateIds, errors);
            return errors.AsReadOnly();
        }

        private static void ValidateSequence(
            IReadOnlyList<string> actual,
            string sector,
            string code,
            int expectedCount,
            HashSet<string> allIds,
            List<string> errors)
        {
            if (actual == null || actual.Count != expectedCount)
            {
                errors.Add("sector " + sector + " requires " + expectedCount + " " + code + " templates");
                return;
            }

            for (int index = 0; index < expectedCount; index++)
            {
                string expected = "room." + sector + "." + code + (index + 1).ToString(CultureInfo.InvariantCulture);
                ValidateTemplate(actual[index], expected, allIds, errors);
            }
        }

        private static void ValidateTemplate(string actual, string expected, HashSet<string> allIds, List<string> errors)
        {
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                errors.Add("expected stable room template ID " + expected + "; found " + (actual ?? "<missing>"));
                return;
            }

            if (!allIds.Add(actual)) errors.Add("room template ID is duplicated: " + actual);
        }
    }

    public sealed class ExpeditionRouteNode
    {
        internal ExpeditionRouteNode(
            string nodeId,
            string sectorId,
            string templateId,
            ExpeditionRouteNodeKind kind,
            bool isMandatory,
            int mainPathIndex)
        {
            NodeId = nodeId;
            SectorId = sectorId ?? string.Empty;
            TemplateId = templateId;
            Kind = kind;
            IsMandatory = isMandatory;
            MainPathIndex = mainPathIndex;
            EntryPortId = "port.entry.west";
            ExitPortId = "port.exit.east";
        }

        public string NodeId { get; }
        public string SectorId { get; }
        public string TemplateId { get; }
        public ExpeditionRouteNodeKind Kind { get; }
        public bool IsMandatory { get; }
        public int MainPathIndex { get; }
        public string EntryPortId { get; }
        public string ExitPortId { get; }
    }

    public sealed class ExpeditionRouteConnection
    {
        internal ExpeditionRouteConnection(string fromNodeId, string toNodeId, bool isOptionalBranch)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            IsOptionalBranch = isOptionalBranch;
            FromPortId = "port.exit.east";
            ToPortId = "port.entry.west";
        }

        public string FromNodeId { get; }
        public string ToNodeId { get; }
        public string FromPortId { get; }
        public string ToPortId { get; }
        public bool IsOptionalBranch { get; }
    }

    public sealed class ExpeditionRoutePlan
    {
        private readonly IReadOnlyList<ExpeditionRouteNode> _nodes;
        private readonly IReadOnlyList<ExpeditionRouteConnection> _connections;
        private readonly IReadOnlyList<string> _mandatoryNodeIds;

        internal ExpeditionRoutePlan(
            int generationVersion,
            uint seed,
            IEnumerable<ExpeditionRouteNode> nodes,
            IEnumerable<ExpeditionRouteConnection> connections,
            IEnumerable<string> mandatoryNodeIds)
        {
            GenerationVersion = generationVersion;
            Seed = Xorshift32.NormalizeSeed(seed);
            _nodes = Array.AsReadOnly(nodes.ToArray());
            _connections = Array.AsReadOnly(connections.ToArray());
            _mandatoryNodeIds = Array.AsReadOnly(mandatoryNodeIds.ToArray());
            CanonicalForm = BuildCanonicalForm();
            Fingerprint = SeedDeriver.HashUtf8(CanonicalForm).ToString("X8", CultureInfo.InvariantCulture);
        }

        public int GenerationVersion { get; }
        public uint Seed { get; }
        public IReadOnlyList<ExpeditionRouteNode> Nodes => _nodes;
        public IReadOnlyList<ExpeditionRouteConnection> Connections => _connections;
        public IReadOnlyList<string> MandatoryNodeIds => _mandatoryNodeIds;
        public string CanonicalForm { get; }
        public string Fingerprint { get; }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (_nodes.Count != 24) errors.Add("a route must contain 21 mandatory and three optional nodes");
            if (_mandatoryNodeIds.Count != 21) errors.Add("the required main path must contain 21 locations");
            var nodesById = new Dictionary<string, ExpeditionRouteNode>(StringComparer.Ordinal);
            foreach (ExpeditionRouteNode node in _nodes)
            {
                if (node == null)
                {
                    errors.Add("route contains a missing node");
                    continue;
                }

                if (nodesById.ContainsKey(node.NodeId)) errors.Add("route contains duplicate node ID " + node.NodeId);
                else nodesById.Add(node.NodeId, node);
            }

            if (_nodes.Count(node => node != null && node.IsMandatory) != 21) errors.Add("route must contain exactly 21 mandatory nodes");
            if (_nodes.Count(node => node != null && !node.IsMandatory) != 3) errors.Add("route must contain exactly three optional nodes");
            var mandatoryIds = new HashSet<string>(_mandatoryNodeIds, StringComparer.Ordinal);
            if (mandatoryIds.Count != _mandatoryNodeIds.Count ||
                nodesById.Values.Count(node => node.IsMandatory != mandatoryIds.Contains(node.NodeId)) != 0)
            {
                errors.Add("the mandatory path must list every mandatory node exactly once");
            }

            var connectionKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExpeditionRouteConnection connection in _connections)
            {
                if (connection == null || !nodesById.ContainsKey(connection.FromNodeId) || !nodesById.ContainsKey(connection.ToNodeId))
                {
                    errors.Add("route connection refers to a missing node");
                    continue;
                }

                string key = connection.FromNodeId + "|" + connection.ToNodeId;
                if (!connectionKeys.Add(key)) errors.Add("route connection is duplicated: " + key);
            }

            for (int index = 0; index + 1 < _mandatoryNodeIds.Count; index++)
            {
                string from = _mandatoryNodeIds[index];
                string to = _mandatoryNodeIds[index + 1];
                if (!nodesById.TryGetValue(from, out ExpeditionRouteNode fromNode) || !fromNode.IsMandatory ||
                    !nodesById.TryGetValue(to, out ExpeditionRouteNode toNode) || !toNode.IsMandatory)
                {
                    errors.Add("the mandatory path references a missing or optional node at index " + index);
                    continue;
                }

                if (!connectionKeys.Contains(from + "|" + to)) errors.Add("mandatory route is disconnected between " + from + " and " + to);
            }

            foreach (ExpeditionRouteNode optionalNode in _nodes.Where(node => node != null && !node.IsMandatory))
            {
                ExpeditionRouteConnection[] incoming = _connections.Where(connection => connection != null &&
                    connection.ToNodeId == optionalNode.NodeId && connection.IsOptionalBranch).ToArray();
                ExpeditionRouteConnection[] outgoing = _connections.Where(connection => connection != null &&
                    connection.FromNodeId == optionalNode.NodeId && connection.IsOptionalBranch).ToArray();
                if (incoming.Length != 1 || outgoing.Length != 1)
                {
                    errors.Add("optional branch " + optionalNode.NodeId + " must have one entry and one exit");
                    continue;
                }

                bool connectsMandatoryLocations = nodesById.TryGetValue(incoming[0].FromNodeId, out ExpeditionRouteNode branchStart) &&
                    branchStart.IsMandatory && nodesById.TryGetValue(outgoing[0].ToNodeId, out ExpeditionRouteNode branchEnd) &&
                    branchEnd.IsMandatory && connectionKeys.Contains(branchStart.NodeId + "|" + branchEnd.NodeId);
                if (!connectsMandatoryLocations)
                {
                    errors.Add("optional branch " + optionalNode.NodeId + " must bypass one direct mandatory connection");
                }
            }

            if (_connections.Count(connection => connection != null && connection.IsOptionalBranch) != 6)
            {
                errors.Add("three optional branches require exactly six optional connections");
            }

            foreach (string sectorId in new[] { "patio", "archivo", "forjas" })
            {
                ExpeditionRouteNode[] sectorNodes = _nodes.Where(node => node != null && node.SectorId == sectorId).ToArray();
                if (sectorNodes.Count(node => node.Kind == ExpeditionRouteNodeKind.Combat) != 3 ||
                    sectorNodes.Count(node => node.Kind == ExpeditionRouteNodeKind.OptionalCombat) != 1 ||
                    sectorNodes.Count(node => node.Kind == ExpeditionRouteNodeKind.Exploration) != 1 ||
                    sectorNodes.Count(node => node.Kind == ExpeditionRouteNodeKind.Guardian) != 1 ||
                    sectorNodes.Count(node => node.Kind == ExpeditionRouteNodeKind.Treasure || node.Kind == ExpeditionRouteNodeKind.Pact ||
                        node.Kind == ExpeditionRouteNodeKind.Narrative) != 1)
                {
                    errors.Add("sector " + sectorId + " has an invalid required room distribution");
                }

                string[] combatIds = sectorNodes.Where(node => node.Kind == ExpeditionRouteNodeKind.Combat || node.Kind == ExpeditionRouteNodeKind.OptionalCombat)
                    .Select(node => node.TemplateId).ToArray();
                if (combatIds.Distinct(StringComparer.Ordinal).Count() != combatIds.Length)
                {
                    errors.Add("sector " + sectorId + " repeats a combat template");
                }
            }

            if (_nodes.Count(node => node != null && node.Kind == ExpeditionRouteNodeKind.Rest) != 2 ||
                _nodes.Count(node => node != null && node.Kind == ExpeditionRouteNodeKind.FinalBoss) != 1)
            {
                errors.Add("route must contain two rests and one final boss");
            }

            foreach (ExpeditionRouteNodeKind specialKind in new[]
                { ExpeditionRouteNodeKind.Treasure, ExpeditionRouteNodeKind.Pact, ExpeditionRouteNodeKind.Narrative })
            {
                if (_nodes.Count(node => node != null && node.Kind == specialKind) != 1)
                {
                    errors.Add("route must contain exactly one " + specialKind + " room across the three sectors");
                }
            }

            return errors.AsReadOnly();
        }

        private string BuildCanonicalForm()
        {
            var canonical = new StringBuilder();
            canonical.Append("route-v").Append(GenerationVersion.ToString(CultureInfo.InvariantCulture));
            canonical.Append("|seed=").Append(Seed.ToString("X8", CultureInfo.InvariantCulture)).Append('\n');
            foreach (ExpeditionRouteNode node in _nodes)
            {
                canonical.Append("N|").Append(node.NodeId).Append('|').Append(node.SectorId).Append('|')
                    .Append(node.Kind.ToString()).Append('|').Append(node.TemplateId).Append('|')
                    .Append(node.IsMandatory ? '1' : '0').Append('|')
                    .Append(node.MainPathIndex.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }

            foreach (ExpeditionRouteConnection connection in _connections)
            {
                canonical.Append("E|").Append(connection.FromNodeId).Append('|').Append(connection.FromPortId).Append('|')
                    .Append(connection.ToNodeId).Append('|').Append(connection.ToPortId).Append('|')
                    .Append(connection.IsOptionalBranch ? '1' : '0').Append('\n');
            }

            canonical.Append("M|").Append(string.Join(",", _mandatoryNodeIds));
            return canonical.ToString();
        }
    }

    /// <summary>Builds the fixed 21-location expedition and three skippable combat detours.</summary>
    public static class ExpeditionRouteGenerator
    {
        public const int GenerationVersion = 1;
        private static readonly string[] SectorOrder = { "patio", "archivo", "forjas" };

        public static ExpeditionRoutePlan Generate(uint seed, ExpeditionRouteCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            IReadOnlyList<string> catalogErrors = catalog.Validate();
            if (catalogErrors.Count > 0) throw new ArgumentException("Invalid expedition route catalog: " + string.Join("; ", catalogErrors), nameof(catalog));

            uint normalizedSeed = Xorshift32.NormalizeSeed(seed);
            var nodes = new List<ExpeditionRouteNode>(24);
            var connections = new List<ExpeditionRouteConnection>(26);
            var mainPath = new List<string>(21);
            var optionalBranches = new List<Tuple<string, string, string>>();
            string[] specialKinds = { "treasure", "pact", "narrative" };
            Shuffle(specialKinds, new Xorshift32(SeedDeriver.Derive(GenerationVersion, normalizedSeed, "route.special.order")));

            for (int sectorIndex = 0; sectorIndex < SectorOrder.Length; sectorIndex++)
            {
                string sectorId = SectorOrder[sectorIndex];
                ExpeditionRouteSectorCatalog sector = catalog.Sectors[sectorIndex];
                string[] combatTemplates = sector.CombatTemplateIds.ToArray();
                Shuffle(combatTemplates, new Xorshift32(SeedDeriver.Derive(
                    GenerationVersion, normalizedSeed, "route.templates.combat", sectorId)));
                string exploration = sector.ExplorationTemplateIds[
                    new Xorshift32(SeedDeriver.Derive(GenerationVersion, normalizedSeed, "route.templates.exploration", sectorId))
                        .NextInt(0, sector.ExplorationTemplateIds.Count)];

                string combatOne = AddMainNode(nodes, mainPath, sectorId + ".combat.01", sectorId, combatTemplates[0],
                    ExpeditionRouteNodeKind.Combat);
                AddMainNode(nodes, mainPath, sectorId + ".exploration", sectorId, exploration, ExpeditionRouteNodeKind.Exploration);
                string combatTwo = AddMainNode(nodes, mainPath, sectorId + ".combat.02", sectorId, combatTemplates[1],
                    ExpeditionRouteNodeKind.Combat);
                string specialKind = specialKinds[sectorIndex];
                string specialTemplate = GetSpecialTemplate(sector, specialKind);
                ExpeditionRouteNodeKind routeSpecialKind = GetSpecialNodeKind(specialKind);
                string special = AddMainNode(nodes, mainPath, sectorId + ".special", sectorId, specialTemplate, routeSpecialKind);
                AddMainNode(nodes, mainPath, sectorId + ".combat.03", sectorId, combatTemplates[2], ExpeditionRouteNodeKind.Combat);
                AddMainNode(nodes, mainPath, sectorId + ".guardian", sectorId, sector.GuardianTemplateId, ExpeditionRouteNodeKind.Guardian);

                string optional = sectorId + ".optional.combat";
                nodes.Add(new ExpeditionRouteNode(optional, sectorId, combatTemplates[3], ExpeditionRouteNodeKind.OptionalCombat,
                    false, -1));
                optionalBranches.Add(Tuple.Create(combatTwo, optional, special));

                if (sectorIndex < 2)
                {
                    AddMainNode(nodes, mainPath, "hub.rest." + (sectorIndex + 1).ToString("00", CultureInfo.InvariantCulture),
                        string.Empty, catalog.RestTemplateId, ExpeditionRouteNodeKind.Rest);
                }
            }

            AddMainNode(nodes, mainPath, "final.boss", string.Empty, catalog.FinalBossTemplateId, ExpeditionRouteNodeKind.FinalBoss);
            for (int index = 0; index + 1 < mainPath.Count; index++)
            {
                connections.Add(new ExpeditionRouteConnection(mainPath[index], mainPath[index + 1], false));
            }

            foreach (Tuple<string, string, string> branch in optionalBranches)
            {
                connections.Add(new ExpeditionRouteConnection(branch.Item1, branch.Item2, true));
                connections.Add(new ExpeditionRouteConnection(branch.Item2, branch.Item3, true));
            }

            var plan = new ExpeditionRoutePlan(GenerationVersion, normalizedSeed, nodes, connections, mainPath);
            IReadOnlyList<string> errors = plan.Validate();
            if (errors.Count > 0) throw new InvalidOperationException("Generated an invalid expedition route: " + string.Join("; ", errors));
            return plan;
        }

        private static string AddMainNode(
            List<ExpeditionRouteNode> nodes,
            List<string> mainPath,
            string nodeId,
            string sectorId,
            string templateId,
            ExpeditionRouteNodeKind kind)
        {
            nodes.Add(new ExpeditionRouteNode(nodeId, sectorId, templateId, kind, true, mainPath.Count));
            mainPath.Add(nodeId);
            return nodeId;
        }

        private static string GetSpecialTemplate(ExpeditionRouteSectorCatalog sector, string specialKind)
        {
            switch (specialKind)
            {
                case "treasure": return sector.TreasureTemplateId;
                case "pact": return sector.PactTemplateId;
                case "narrative": return sector.NarrativeTemplateId;
                default: throw new ArgumentOutOfRangeException(nameof(specialKind), specialKind, "Unknown special room kind.");
            }
        }

        private static ExpeditionRouteNodeKind GetSpecialNodeKind(string specialKind)
        {
            switch (specialKind)
            {
                case "treasure": return ExpeditionRouteNodeKind.Treasure;
                case "pact": return ExpeditionRouteNodeKind.Pact;
                case "narrative": return ExpeditionRouteNodeKind.Narrative;
                default: throw new ArgumentOutOfRangeException(nameof(specialKind), specialKind, "Unknown special room kind.");
            }
        }

        private static void Shuffle<T>(T[] values, Xorshift32 random)
        {
            for (int index = values.Length - 1; index > 0; index--)
            {
                int other = random.NextInt(0, index + 1);
                T value = values[index];
                values[index] = values[other];
                values[other] = value;
            }
        }
    }
}
