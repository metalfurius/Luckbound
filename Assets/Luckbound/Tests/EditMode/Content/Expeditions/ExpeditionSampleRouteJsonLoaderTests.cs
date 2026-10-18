using System;
using System.IO;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using NUnit.Framework;

namespace Luckbound.Tests.Content
{
    public sealed class ExpeditionSampleRouteJsonLoaderTests
    {
        private const string SampleRoutePath = "Assets/Resources/Luckbound/Data/Rooms/F10SampleRoute.json";
        private string _json;

        [SetUp]
        public void SetUp()
        {
            _json = File.ReadAllText(SampleRoutePath).Replace("\r\n", "\n");
        }

        [Test]
        public void AuthoritativeSampleRouteJsonLoadsAndProducesTheFiveNodePlan()
        {
            ExpeditionSampleRouteDefinition definition = ExpeditionSampleRouteJsonLoader.Load(_json);
            ExpeditionRoutePlan route = ExpeditionSampleRouteGenerator.Generate(17320508u, definition);

            Assert.That(definition.SchemaVersion, Is.EqualTo(1));
            Assert.That(definition.GenerationVersion, Is.EqualTo(2));
            Assert.That(definition.Validate(), Is.Empty);
            Assert.That(route.Nodes, Has.Count.EqualTo(5));
            Assert.That(route.Nodes[3].TemplateId, Is.EqualTo("room.patio.guardian"));
            Assert.That(route.Nodes[4].Kind, Is.EqualTo(ExpeditionRouteNodeKind.SafeExtraction));
        }

        [Test]
        public void LoaderRejectsInvalidTypesOrderSectorPathIdsPortsAndEdges()
        {
            AssertRejected(_json.Replace("\"kind\": \"Tutorial\"", "\"kind\": \"Combat\""));
            AssertRejected(_json.Replace("\"nodeId\": \"sample.tutorial\"", "\"nodeId\": \"sample.patio.combat\""));
            AssertRejected(ReplaceFirst(_json, "\"sectorId\": \"patio\"", "\"sectorId\": \"forjas\""));
            AssertRejected(_json.Replace("\"mainPathIndex\": 2", "\"mainPathIndex\": 3"));
            AssertRejected(_json.Replace("\"templateId\": \"room.sample.tutorial\"", "\"templateId\": \"room.patio.prototype\""));
            AssertRejected(ReplaceFirst(_json, "\"entryPortId\": \"port.entry.west\"", "\"entryPortId\": \"port.entry.east\""));
            AssertRejected(_json.Replace("\"toNodeId\": \"sample.patio.exploration\"", "\"toNodeId\": \"sample.safe_extraction\""));
            AssertRejected(ReplaceFirst(_json, "\"fromPortId\": \"port.exit.east\"", "\"fromPortId\": \"port.entry.west\""));
            AssertRejected(ReplaceFirst(_json, "\"isOptionalBranch\": false", "\"isOptionalBranch\": true"));
            AssertRejected(_json.Replace("\"mandatoryNodeIds\": [\n    \"sample.tutorial\"", "\"mandatoryNodeIds\": [\n    \"sample.patio.combat\""));
            AssertRejected(_json.Replace("\"generationVersion\": 2", "\"generationVersion\": 3"));
            AssertRejected(_json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"));
        }

        [Test]
        public void LoaderRejectsEmptyAndMissingDocuments()
        {
            Assert.Throws<ArgumentException>(() => ExpeditionSampleRouteJsonLoader.Load(" "));
            Assert.That(ExpeditionSampleRouteJsonLoader.Validate(null), Is.Not.Empty);
        }

        private void AssertRejected(string json)
        {
            Assert.That(json, Is.Not.EqualTo(_json), "The negative fixture must actually change the authoritative data.");
            Assert.Throws<FormatException>(() => ExpeditionSampleRouteJsonLoader.Load(json));
        }

        private static string ReplaceFirst(string source, string oldValue, string newValue)
        {
            int index = source.IndexOf(oldValue, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            return source.Substring(0, index) + newValue + source.Substring(index + oldValue.Length);
        }
    }
}
