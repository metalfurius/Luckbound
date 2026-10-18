using System;
using System.Linq;
using Luckbound.Core.Expeditions;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ExpeditionSampleRouteTests
    {
        private static readonly string[] ExpectedNodeIds =
        {
            "sample.tutorial",
            "sample.patio.combat",
            "sample.patio.exploration",
            "sample.patio.guardian",
            "sample.safe_extraction"
        };

        [Test]
        public void SampleRouteUsesFixedFiveNodeContractAcrossSeedsAndRoundTripsExactly()
        {
            var fingerprints = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            uint[] seeds = { 0u, 1u, 42u, 17320508u, uint.MaxValue };
            foreach (uint seed in seeds)
            {
                ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(seed, CreateDefinition());
                Assert.That(route.GenerationVersion, Is.EqualTo(2));
                Assert.That(route.Nodes, Has.Count.EqualTo(5));
                Assert.That(route.MandatoryNodeIds, Is.EqualTo(ExpectedNodeIds));
                Assert.That(route.Connections, Has.Count.EqualTo(4));
                Assert.That(route.Validate(), Is.Empty);
                Assert.That(route.Nodes.Select(node => node.NodeId), Is.EqualTo(ExpectedNodeIds));
                Assert.That(route.Nodes.Select(node => node.MainPathIndex), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
                Assert.That(route.Nodes.Select(node => node.Kind), Is.EqualTo(new[]
                {
                    ExpeditionRouteNodeKind.Tutorial,
                    ExpeditionRouteNodeKind.Combat,
                    ExpeditionRouteNodeKind.Exploration,
                    ExpeditionRouteNodeKind.Guardian,
                    ExpeditionRouteNodeKind.SafeExtraction
                }));
                Assert.That(route.Nodes.All(node => node.IsMandatory && node.SectorId == "patio"), Is.True);
                Assert.That(route.Nodes.Select(node => node.TemplateId), Is.EqualTo(new[]
                {
                    "room.sample.tutorial", "room.patio.c1", "room.patio.e1", "room.patio.guardian",
                    "room.sample.extraction"
                }));
                Assert.That(route.Nodes.All(node => node.EntryPortId == "port.entry.west" &&
                    node.ExitPortId == "port.exit.east"), Is.True);

                ExpeditionRoutePlan restored = ExpeditionRoutePlan.ParseCanonical(route.CanonicalForm);
                Assert.That(restored.CanonicalForm, Is.EqualTo(route.CanonicalForm));
                Assert.That(restored.Fingerprint, Is.EqualTo(route.Fingerprint));
                Assert.That(restored.Validate(), Is.Empty);
                fingerprints.Add(route.Fingerprint);
            }

            Assert.That(fingerprints.Count, Is.EqualTo(seeds.Length));
        }

        [Test]
        public void SampleTraversalFollowsTheDirectedChainAndRestoresItsHistory()
        {
            ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(27182818u, CreateDefinition());
            var traversal = new ExpeditionRouteTraversal(route);
            Assert.That(traversal.CurrentNodeId, Is.EqualTo(ExpectedNodeIds[0]));
            Assert.That(traversal.CanTransitionTo(ExpectedNodeIds[2]), Is.False);

            Assert.That(traversal.TryTransitionTo(ExpectedNodeIds[1]), Is.True);
            Assert.That(traversal.TryTransitionTo(ExpectedNodeIds[2]), Is.True);
            string[] history = { ExpectedNodeIds[0], ExpectedNodeIds[1], ExpectedNodeIds[2] };
            var restored = new ExpeditionRouteTraversal(
                ExpeditionRoutePlan.ParseCanonical(route.CanonicalForm), history);
            Assert.That(restored.CurrentNodeId, Is.EqualTo(ExpectedNodeIds[2]));
            Assert.That(restored.VisitedNodeIds, Is.EqualTo(history));
            Assert.That(restored.TryTransitionTo(ExpectedNodeIds[3]), Is.True);
            Assert.That(restored.TryTransitionTo(ExpectedNodeIds[4]), Is.True);
            Assert.That(restored.CanTransitionTo(ExpectedNodeIds[0]), Is.False);
        }

        [Test]
        public void SampleDefinitionRejectsWrongIdsKindsOrderSectorPathPortsAndEdges()
        {
            ExpeditionSampleRouteNodeDefinition[] nodes = CreateNodes();
            nodes[0] = Node("sample.unknown", "patio", "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial, 0);
            AssertInvalid(CreateDefinition(nodes: nodes));

            nodes = CreateNodes();
            nodes[0] = Node("sample.tutorial", "patio", "room.sample.tutorial", ExpeditionRouteNodeKind.Combat, 0);
            AssertInvalid(CreateDefinition(nodes: nodes));

            nodes = CreateNodes();
            nodes[0] = Node("sample.tutorial", "forjas", "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial, 0);
            AssertInvalid(CreateDefinition(nodes: nodes));

            nodes = CreateNodes();
            nodes[2] = Node("sample.patio.exploration", "patio", "room.patio.e1", ExpeditionRouteNodeKind.Exploration, 3);
            AssertInvalid(CreateDefinition(nodes: nodes));

            nodes = CreateNodes();
            ExpeditionSampleRouteNodeDefinition reordered = nodes[0];
            nodes[0] = nodes[1];
            nodes[1] = reordered;
            AssertInvalid(CreateDefinition(nodes: nodes));

            nodes = CreateNodes();
            nodes[0] = new ExpeditionSampleRouteNodeDefinition("sample.tutorial", "patio", "room.sample.tutorial",
                ExpeditionRouteNodeKind.Tutorial, true, 0, "port.entry.east", "port.exit.east");
            AssertInvalid(CreateDefinition(nodes: nodes));

            ExpeditionSampleRouteConnectionDefinition[] connections = CreateConnections();
            connections[1] = new ExpeditionSampleRouteConnectionDefinition(
                ExpectedNodeIds[1], "port.exit.east", ExpectedNodeIds[3], "port.entry.west", false);
            AssertInvalid(CreateDefinition(connections: connections));

            connections = CreateConnections();
            connections[0] = new ExpeditionSampleRouteConnectionDefinition(
                ExpectedNodeIds[0], "port.entry.west", ExpectedNodeIds[1], "port.entry.west", false);
            AssertInvalid(CreateDefinition(connections: connections));

            connections = CreateConnections();
            connections[0] = new ExpeditionSampleRouteConnectionDefinition(
                ExpectedNodeIds[0], "port.exit.east", ExpectedNodeIds[1], "port.entry.west", true);
            AssertInvalid(CreateDefinition(connections: connections));
        }

        [Test]
        public void SampleGeneratorAndCanonicalParserRejectUnsupportedVersions()
        {
            AssertInvalid(CreateDefinition(mandatoryNodeIds: new[]
            {
                ExpectedNodeIds[0], ExpectedNodeIds[2], ExpectedNodeIds[1], ExpectedNodeIds[3], ExpectedNodeIds[4]
            }));
            AssertInvalid(CreateDefinition(generationVersion: 1));
            AssertInvalid(CreateDefinition(generationVersion: 3));
            AssertInvalid(CreateDefinition(schemaVersion: 2));

            ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(123u, CreateDefinition());
            Assert.Throws<FormatException>(() => ExpeditionRoutePlan.ParseCanonical(
                route.CanonicalForm.Replace("route-v2|", "route-v3|")));
        }

        private static void AssertInvalid(ExpeditionSampleRouteDefinition definition)
        {
            Assert.That(definition.Validate(), Is.Not.Empty);
            Assert.Throws<ArgumentException>(() => ExpeditionSampleRouteGenerator.Generate(1u, definition));
        }

        private static ExpeditionSampleRouteDefinition CreateDefinition(int schemaVersion = 1,
            int generationVersion = 2, ExpeditionSampleRouteNodeDefinition[] nodes = null,
            ExpeditionSampleRouteConnectionDefinition[] connections = null, string[] mandatoryNodeIds = null)
        {
            return new ExpeditionSampleRouteDefinition(schemaVersion, generationVersion,
                nodes ?? CreateNodes(), connections ?? CreateConnections(), mandatoryNodeIds ?? ExpectedNodeIds);
        }

        private static ExpeditionSampleRouteNodeDefinition[] CreateNodes()
        {
            return new[]
            {
                Node("sample.tutorial", "patio", "room.sample.tutorial", ExpeditionRouteNodeKind.Tutorial, 0),
                Node("sample.patio.combat", "patio", "room.patio.c1", ExpeditionRouteNodeKind.Combat, 1),
                Node("sample.patio.exploration", "patio", "room.patio.e1", ExpeditionRouteNodeKind.Exploration, 2),
                Node("sample.patio.guardian", "patio", "room.patio.guardian", ExpeditionRouteNodeKind.Guardian, 3),
                Node("sample.safe_extraction", "patio", "room.sample.extraction", ExpeditionRouteNodeKind.SafeExtraction, 4)
            };
        }

        private static ExpeditionSampleRouteNodeDefinition Node(string nodeId, string sectorId, string templateId,
            ExpeditionRouteNodeKind kind, int pathIndex)
        {
            return new ExpeditionSampleRouteNodeDefinition(nodeId, sectorId, templateId, kind, true,
                pathIndex, "port.entry.west", "port.exit.east");
        }

        private static ExpeditionSampleRouteConnectionDefinition[] CreateConnections()
        {
            return Enumerable.Range(0, 4).Select(index => new ExpeditionSampleRouteConnectionDefinition(
                ExpectedNodeIds[index], "port.exit.east", ExpectedNodeIds[index + 1], "port.entry.west", false)).ToArray();
        }
    }
}