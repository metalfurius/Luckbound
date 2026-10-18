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
        OptionalCombat,
        Tutorial,
        SafeExtraction
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

    public sealed class ExpeditionSampleRouteNodeDefinition
    {
        public ExpeditionSampleRouteNodeDefinition(string nodeId, string sectorId, string templateId,
            ExpeditionRouteNodeKind kind, bool isMandatory, int mainPathIndex, string entryPortId, string exitPortId)
        {
            NodeId = nodeId;
            SectorId = sectorId;
            TemplateId = templateId;
            Kind = kind;
            IsMandatory = isMandatory;
            MainPathIndex = mainPathIndex;
            EntryPortId = entryPortId;
            ExitPortId = exitPortId;
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

    public sealed class ExpeditionSampleRouteConnectionDefinition
    {
        public ExpeditionSampleRouteConnectionDefinition(string fromNodeId, string fromPortId, string toNodeId,
            string toPortId, bool isOptionalBranch)
        {
            FromNodeId = fromNodeId;
            FromPortId = fromPortId;
            ToNodeId = toNodeId;
            ToPortId = toPortId;
            IsOptionalBranch = isOptionalBranch;
        }

        public string FromNodeId { get; }
        public string FromPortId { get; }
        public string ToNodeId { get; }
        public string ToPortId { get; }
        public bool IsOptionalBranch { get; }
    }

    /// <summary>Fixed, data-driven definition of the five-location F10 sample route.</summary>
    public sealed class ExpeditionSampleRouteDefinition
    {
        public const int RequiredSchemaVersion = 1;
        public const int RequiredGenerationVersion = 2;
        public const string EntryPortId = "port.entry.west";
        public const string ExitPortId = "port.exit.east";
        private static readonly ExpeditionSampleRouteNodeDefinition[] ContractNodes =
        {
            new ExpeditionSampleRouteNodeDefinition("sample.tutorial", "patio", "room.sample.tutorial",
                ExpeditionRouteNodeKind.Tutorial, true, 0, EntryPortId, ExitPortId),
            new ExpeditionSampleRouteNodeDefinition("sample.patio.combat", "patio", "room.patio.c1",
                ExpeditionRouteNodeKind.Combat, true, 1, EntryPortId, ExitPortId),
            new ExpeditionSampleRouteNodeDefinition("sample.patio.exploration", "patio", "room.patio.e1",
                ExpeditionRouteNodeKind.Exploration, true, 2, EntryPortId, ExitPortId),
            new ExpeditionSampleRouteNodeDefinition("sample.patio.guardian", "patio", "room.patio.guardian",
                ExpeditionRouteNodeKind.Guardian, true, 3, EntryPortId, ExitPortId),
            new ExpeditionSampleRouteNodeDefinition("sample.safe_extraction", "patio", "room.sample.extraction",
                ExpeditionRouteNodeKind.SafeExtraction, true, 4, EntryPortId, ExitPortId)
        };

        private readonly IReadOnlyList<ExpeditionSampleRouteNodeDefinition> _nodes;
        private readonly IReadOnlyList<ExpeditionSampleRouteConnectionDefinition> _connections;
        private readonly IReadOnlyList<string> _mandatoryNodeIds;

        public ExpeditionSampleRouteDefinition(int schemaVersion, int generationVersion,
            IEnumerable<ExpeditionSampleRouteNodeDefinition> nodes,
            IEnumerable<ExpeditionSampleRouteConnectionDefinition> connections,
            IEnumerable<string> mandatoryNodeIds)
        {
            SchemaVersion = schemaVersion;
            GenerationVersion = generationVersion;
            _nodes = Array.AsReadOnly(nodes == null
                ? Array.Empty<ExpeditionSampleRouteNodeDefinition>()
                : nodes.ToArray());
            _connections = Array.AsReadOnly(connections == null
                ? Array.Empty<ExpeditionSampleRouteConnectionDefinition>()
                : connections.ToArray());
            _mandatoryNodeIds = Array.AsReadOnly(mandatoryNodeIds == null
                ? Array.Empty<string>()
                : mandatoryNodeIds.ToArray());
        }

        public int SchemaVersion { get; }
        public int GenerationVersion { get; }
        public IReadOnlyList<ExpeditionSampleRouteNodeDefinition> Nodes => _nodes;
        public IReadOnlyList<ExpeditionSampleRouteConnectionDefinition> Connections => _connections;
        public IReadOnlyList<string> MandatoryNodeIds => _mandatoryNodeIds;
        internal static ExpeditionSampleRouteNodeDefinition RequiredNodeAt(int index) => ContractNodes[index];

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (SchemaVersion != RequiredSchemaVersion) errors.Add("schemaVersion must be 1");
            if (GenerationVersion != RequiredGenerationVersion) errors.Add("generationVersion must be 2");
            if (_nodes.Count != ContractNodes.Length)
                errors.Add("the sample route must contain exactly five locations");
            if (_connections.Count != ContractNodes.Length - 1)
                errors.Add("the sample route must contain exactly four linear connections");
            if (_mandatoryNodeIds.Count != ContractNodes.Length)
                errors.Add("the sample route must list all five mandatory locations");

            int nodeCount = Math.Min(_nodes.Count, ContractNodes.Length);
            for (int index = 0; index < nodeCount; index++)
            {
                ExpeditionSampleRouteNodeDefinition actual = _nodes[index];
                ExpeditionSampleRouteNodeDefinition expected = ContractNodes[index];
                if (actual == null)
                {
                    errors.Add("nodes[" + index.ToString(CultureInfo.InvariantCulture) + "] is missing");
                    continue;
                }

                if (!string.Equals(actual.NodeId, expected.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(actual.SectorId, expected.SectorId, StringComparison.Ordinal) ||
                    !string.Equals(actual.TemplateId, expected.TemplateId, StringComparison.Ordinal) ||
                    actual.Kind != expected.Kind || actual.IsMandatory != expected.IsMandatory ||
                    actual.MainPathIndex != expected.MainPathIndex ||
                    !string.Equals(actual.EntryPortId, expected.EntryPortId, StringComparison.Ordinal) ||
                    !string.Equals(actual.ExitPortId, expected.ExitPortId, StringComparison.Ordinal))
                {
                    errors.Add("nodes[" + index.ToString(CultureInfo.InvariantCulture) +
                        "] does not match the fixed sample route contract");
                }
            }

            int connectionCount = Math.Min(_connections.Count, ContractNodes.Length - 1);
            for (int index = 0; index < connectionCount; index++)
            {
                ExpeditionSampleRouteConnectionDefinition actual = _connections[index];
                ExpeditionSampleRouteNodeDefinition from = ContractNodes[index];
                ExpeditionSampleRouteNodeDefinition to = ContractNodes[index + 1];
                if (actual == null ||
                    !string.Equals(actual.FromNodeId, from.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(actual.FromPortId, ExitPortId, StringComparison.Ordinal) ||
                    !string.Equals(actual.ToNodeId, to.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(actual.ToPortId, EntryPortId, StringComparison.Ordinal) ||
                    actual.IsOptionalBranch)
                {
                    errors.Add("connections[" + index.ToString(CultureInfo.InvariantCulture) +
                        "] must connect consecutive sample locations through the standard west/east ports");
                }
            }

            int mandatoryCount = Math.Min(_mandatoryNodeIds.Count, ContractNodes.Length);
            for (int index = 0; index < mandatoryCount; index++)
            {
                if (!string.Equals(_mandatoryNodeIds[index], ContractNodes[index].NodeId, StringComparison.Ordinal))
                {
                    errors.Add("mandatoryNodeIds must preserve the fixed sample location order");
                    break;
                }
            }

            return errors.AsReadOnly();
        }
    }

    /// <summary>Builds the fixed five-location F10 sample without changing campaign generation.</summary>
    public static class ExpeditionSampleRouteGenerator
    {
        public const int GenerationVersion = ExpeditionSampleRouteDefinition.RequiredGenerationVersion;

        public static ExpeditionRoutePlan Generate(uint seed, ExpeditionSampleRouteDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            IReadOnlyList<string> definitionErrors = definition.Validate();
            if (definitionErrors.Count > 0)
                throw new ArgumentException("Invalid expedition sample route definition: " +
                    string.Join("; ", definitionErrors), nameof(definition));

            var nodes = new List<ExpeditionRouteNode>(definition.Nodes.Count);
            foreach (ExpeditionSampleRouteNodeDefinition node in definition.Nodes)
            {
                nodes.Add(new ExpeditionRouteNode(node.NodeId, node.SectorId, node.TemplateId, node.Kind,
                    node.IsMandatory, node.MainPathIndex));
            }

            var connections = new List<ExpeditionRouteConnection>(definition.Connections.Count);
            foreach (ExpeditionSampleRouteConnectionDefinition connection in definition.Connections)
            {
                connections.Add(new ExpeditionRouteConnection(connection.FromNodeId, connection.FromPortId,
                    connection.ToNodeId, connection.ToPortId, connection.IsOptionalBranch));
            }

            var route = new ExpeditionRoutePlan(GenerationVersion, seed, nodes, connections, definition.MandatoryNodeIds);
            IReadOnlyList<string> routeErrors = route.Validate();
            if (routeErrors.Count > 0)
                throw new InvalidOperationException("Generated an invalid expedition sample route: " +
                    string.Join("; ", routeErrors));
            return route;
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
            : this(fromNodeId, "port.exit.east", toNodeId, "port.entry.west", isOptionalBranch)
        {
        }

        internal ExpeditionRouteConnection(string fromNodeId, string fromPortId, string toNodeId, string toPortId,
            bool isOptionalBranch)
        {
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            IsOptionalBranch = isOptionalBranch;
            FromPortId = fromPortId;
            ToPortId = toPortId;
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

        /// <summary>Restores the exact persisted route without consulting mutable content catalogs.</summary>
        public static ExpeditionRoutePlan ParseCanonical(string canonicalForm)
        {
            if (string.IsNullOrWhiteSpace(canonicalForm)) throw new FormatException("Route snapshot is empty.");
            string[] lines = canonicalForm.Split(new[] { '\n' }, StringSplitOptions.None);
            if (lines.Length < 2) throw new FormatException("Route snapshot header is incomplete.");
            string[] header = lines[0].Split('|');
            if (header.Length != 2 || !header[0].StartsWith("route-v", StringComparison.Ordinal) ||
                !header[1].StartsWith("seed=", StringComparison.Ordinal))
                throw new FormatException("Route snapshot header is invalid.");
            if (!int.TryParse(header[0].Substring("route-v".Length), NumberStyles.None, CultureInfo.InvariantCulture, out int generationVersion) ||
                generationVersion < 1)
                throw new FormatException("Route generation version is invalid.");
            if (!uint.TryParse(header[1].Substring("seed=".Length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint seed) || seed == 0)
                throw new FormatException("Route seed is invalid.");

            var nodes = new List<ExpeditionRouteNode>();
            var connections = new List<ExpeditionRouteConnection>();
            string[] mandatoryNodeIds = null;
            for (int index = 1; index < lines.Length; index++)
            {
                string[] parts = lines[index].Split('|');
                if (parts[0] == "N" && parts.Length == 7)
                {
                    if (!Enum.TryParse(parts[3], ignoreCase: false, out ExpeditionRouteNodeKind kind) ||
                        !Enum.IsDefined(typeof(ExpeditionRouteNodeKind), kind) ||
                        parts[5] != "0" && parts[5] != "1" ||
                        !int.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pathIndex))
                        throw new FormatException("Route node snapshot contains invalid metadata.");
                    nodes.Add(new ExpeditionRouteNode(parts[1], parts[2], parts[4], kind, parts[5] == "1", pathIndex));
                }
                else if (parts[0] == "E" && parts.Length == 6)
                {
                    if (parts[5] != "0" && parts[5] != "1") throw new FormatException("Route edge snapshot contains an invalid branch flag.");
                    connections.Add(new ExpeditionRouteConnection(parts[1], parts[2], parts[3], parts[4], parts[5] == "1"));
                }
                else if (parts[0] == "M" && parts.Length == 2 && mandatoryNodeIds == null)
                {
                    mandatoryNodeIds = parts[1].Split(',');
                }
                else
                {
                    throw new FormatException("Route snapshot contains an unknown or malformed record at line " + index + ".");
                }
            }

            if (mandatoryNodeIds == null) throw new FormatException("Route snapshot is missing its mandatory path.");
            var route = new ExpeditionRoutePlan(generationVersion, seed, nodes, connections, mandatoryNodeIds);
            IReadOnlyList<string> errors = route.Validate();
            if (errors.Count != 0) throw new FormatException("Persisted route violates its invariants: " + string.Join("; ", errors));
            if (!string.Equals(route.CanonicalForm, canonicalForm, StringComparison.Ordinal))
                throw new FormatException("Persisted route is not in canonical form.");
            return route;
        }

        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (GenerationVersion != ExpeditionRouteGenerator.GenerationVersion &&
                GenerationVersion != ExpeditionSampleRouteGenerator.GenerationVersion)
            {
                errors.Add("unsupported route generation version " + GenerationVersion.ToString(CultureInfo.InvariantCulture));
                return errors.AsReadOnly();
            }
            if (GenerationVersion == ExpeditionSampleRouteGenerator.GenerationVersion)
            {
                ValidateSampleRoute(errors);
                return errors.AsReadOnly();
            }
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

        private void ValidateSampleRoute(List<string> errors)
        {
            if (_nodes.Count != 5) errors.Add("a sample route must contain exactly five mandatory nodes");
            if (_mandatoryNodeIds.Count != 5) errors.Add("a sample route main path must contain exactly five locations");
            if (_connections.Count != 4) errors.Add("a sample route must contain exactly four linear connections");

            int nodeCount = Math.Min(_nodes.Count, 5);
            for (int index = 0; index < nodeCount; index++)
            {
                ExpeditionRouteNode actual = _nodes[index];
                ExpeditionSampleRouteNodeDefinition expected = ExpeditionSampleRouteDefinition.RequiredNodeAt(index);
                if (actual == null || !string.Equals(actual.NodeId, expected.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(actual.SectorId, expected.SectorId, StringComparison.Ordinal) ||
                    !string.Equals(actual.TemplateId, expected.TemplateId, StringComparison.Ordinal) ||
                    actual.Kind != expected.Kind || actual.IsMandatory != expected.IsMandatory ||
                    actual.MainPathIndex != expected.MainPathIndex ||
                    !string.Equals(actual.EntryPortId, expected.EntryPortId, StringComparison.Ordinal) ||
                    !string.Equals(actual.ExitPortId, expected.ExitPortId, StringComparison.Ordinal))
                {
                    errors.Add("sample route node at index " + index.ToString(CultureInfo.InvariantCulture) +
                        " does not match the fixed sample route contract");
                }
            }

            int mandatoryCount = Math.Min(_mandatoryNodeIds.Count, 5);
            for (int index = 0; index < mandatoryCount; index++)
            {
                if (!string.Equals(_mandatoryNodeIds[index],
                    ExpeditionSampleRouteDefinition.RequiredNodeAt(index).NodeId, StringComparison.Ordinal))
                {
                    errors.Add("sample route mandatory path must preserve its fixed location order");
                    break;
                }
            }

            int connectionCount = Math.Min(_connections.Count, 4);
            for (int index = 0; index < connectionCount; index++)
            {
                ExpeditionRouteConnection actual = _connections[index];
                ExpeditionSampleRouteNodeDefinition from = ExpeditionSampleRouteDefinition.RequiredNodeAt(index);
                ExpeditionSampleRouteNodeDefinition to = ExpeditionSampleRouteDefinition.RequiredNodeAt(index + 1);
                if (actual == null || !string.Equals(actual.FromNodeId, from.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(actual.FromPortId, ExpeditionSampleRouteDefinition.ExitPortId, StringComparison.Ordinal) ||
                    !string.Equals(actual.ToNodeId, to.NodeId, StringComparison.Ordinal) ||
                    !string.Equals(actual.ToPortId, ExpeditionSampleRouteDefinition.EntryPortId, StringComparison.Ordinal) ||
                    actual.IsOptionalBranch)
                {
                    errors.Add("sample route connection at index " + index.ToString(CultureInfo.InvariantCulture) +
                        " must connect consecutive locations through the standard ports");
                }
            }
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
