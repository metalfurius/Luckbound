using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionRouteGenerationTests
    {
        [Test]
        public void D02_SeededMandatoryRouteIsReproducibleAndConnectedAcrossOneHundredSeeds()
        {
            using (var evidence = new ScenarioEvidenceRecorder("D02", "D02_SeededMandatoryRoute", 17320508))
            {
                TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
                bool catalogAvailable = source != null;
                evidence.RecordAssertion("Route template catalog is available as a Unity resource", catalogAvailable,
                    "Resources/Luckbound/Data/Rooms/RouteTemplates.json", source == null ? "missing" : "loaded");
                Assert.That(source, Is.Not.Null);

                ExpeditionRouteCatalog catalog = ExpeditionRouteCatalogJsonLoader.Load(source.text);
                ExpeditionRoutePlan first = ExpeditionRouteGenerator.Generate(17320508u, catalog);
                ExpeditionRoutePlan repeat = ExpeditionRouteGenerator.Generate(17320508u, catalog);
                bool sameSeedEquivalent = first.CanonicalForm == repeat.CanonicalForm && first.Fingerprint == repeat.Fingerprint;
                evidence.RecordAssertion("Same seed and content version reproduce identical route data", sameSeedEquivalent,
                    "same canonical form and fingerprint", first.Fingerprint + " / " + repeat.Fingerprint);

                bool requiredPathConnected = first.Validate().Count == 0 && first.MandatoryNodeIds.Count == 21 &&
                    first.Connections.Count(connection => !connection.IsOptionalBranch) == 20;
                evidence.RecordAssertion("All 21 required locations form a connected path", requiredPathConnected,
                    "21 mandatory nodes connected by 20 required edges", first.MandatoryNodeIds.Count + " mandatory nodes; " +
                    first.Connections.Count(connection => !connection.IsOptionalBranch) + " required edges");

                bool optionalBranchesValid = first.Nodes.Count(node => !node.IsMandatory) == 3 &&
                    first.Connections.Count(connection => connection.IsOptionalBranch) == 6;
                evidence.RecordAssertion("Each sector adds one skippable combat detour", optionalBranchesValid,
                    "3 optional combat nodes and 6 optional connections",
                    first.Nodes.Count(node => !node.IsMandatory) + " optional nodes; " +
                    first.Connections.Count(connection => connection.IsOptionalBranch) + " optional edges");

                int validSeeds = 0;
                var fingerprints = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                for (uint seed = 1; seed <= 100; seed++)
                {
                    ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(seed, catalog);
                    bool valid = route.Validate().Count == 0;
                    foreach (string sectorId in new[] { "patio", "archivo", "forjas" })
                    {
                        string[] templateIds = route.Nodes.Where(node => node.SectorId == sectorId)
                            .Select(node => node.TemplateId).ToArray();
                        valid &= templateIds.Distinct(StringComparer.Ordinal).Count() == templateIds.Length;
                    }

                    if (valid) validSeeds++;
                    fingerprints.Add(route.Fingerprint);
                }

                bool seedSweepPassed = validSeeds == 100 && fingerprints.Count > 1;
                evidence.RecordAssertion("One hundred seeds preserve the route invariants and vary the result", seedSweepPassed,
                    "100 valid routes and more than one fingerprint", validSeeds + " valid; " + fingerprints.Count + " fingerprints");
                evidence.RecordEvent("seed=17320508 fingerprint=" + first.Fingerprint + " nodes=" + first.Nodes.Count +
                    " mandatory=" + first.MandatoryNodeIds.Count + " edges=" + first.Connections.Count);
                evidence.Complete();

                Assert.That(sameSeedEquivalent, Is.True);
                Assert.That(requiredPathConnected, Is.True);
                Assert.That(optionalBranchesValid, Is.True);
                Assert.That(seedSweepPassed, Is.True);
            }
        }

        [Test]
        public void D02RouteCatalogRejectsAChangedGenerationVersionAndDuplicateRoomId()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
            Assert.That(source, Is.Not.Null);
            Assert.Throws<FormatException>(() => ExpeditionRouteCatalogJsonLoader.Load(
                source.text.Replace("\"generationVersion\": 1", "\"generationVersion\": 2")));
            Assert.Throws<FormatException>(() => ExpeditionRouteCatalogJsonLoader.Load(
                source.text.Replace("room.patio.c2", "room.patio.c1")));
        }
    }
}
