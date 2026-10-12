using System;
using System.Linq;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Randomness;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ExpeditionRouteGeneratorTests
    {
        [Test]
        public void Xorshift32UsesTheSpecifiedVersionOneStepAndNormalizesZero()
        {
            var random = new Xorshift32(1u);
            Assert.That(random.NextUInt(), Is.EqualTo(270369u));
            Assert.That(new Xorshift32(0u).State, Is.EqualTo(0x6D2B79F5u));
        }

        [Test]
        public void SeedDerivationSeparatesDomainsAndIdentifiersAndUsesInvariantSeedEncoding()
        {
            uint route = SeedDeriver.Derive(1, 17320508u, "route.templates.combat", "patio");
            uint exploration = SeedDeriver.Derive(1, 17320508u, "route.templates.exploration", "patio");
            uint otherSector = SeedDeriver.Derive(1, 17320508u, "route.templates.combat", "archivo");

            Assert.That(exploration, Is.Not.EqualTo(route));
            Assert.That(otherSector, Is.Not.EqualTo(route));
            Assert.That(SeedDeriver.Derive(1, 0u, "test"), Is.EqualTo(
                SeedDeriver.Derive(1, Xorshift32.ZeroSeedSubstitute, "test")));
        }

        [Test]
        public void NextIntHonorsExclusiveBoundsAndRejectsInvalidRanges()
        {
            var random = new Xorshift32(0x13579BDFu);
            for (int index = 0; index < 1000; index++)
            {
                int value = random.NextInt(-3, 4);
                Assert.That(value, Is.InRange(-3, 3));
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(4, 4));
        }

        [Test]
        public void SameSeedProducesTheSameCanonicalRouteAndFingerprint()
        {
            const uint seed = 17320508u;
            ExpeditionRoutePlan first = ExpeditionRouteGenerator.Generate(seed, CreateCatalog());
            ExpeditionRoutePlan second = ExpeditionRouteGenerator.Generate(seed, CreateCatalog());

            Assert.That(second.CanonicalForm, Is.EqualTo(first.CanonicalForm));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(first.Nodes, Has.Count.EqualTo(24));
            Assert.That(first.MandatoryNodeIds, Has.Count.EqualTo(21));
            Assert.That(first.Connections, Has.Count.EqualTo(26));
        }

        [Test]
        public void OneHundredSeedsProduceConnectedRoutesWithoutRepeatingRoomTemplatesWithinASector()
        {
            var fingerprints = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (uint seed = 1; seed <= 100; seed++)
            {
                ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(seed, CreateCatalog());
                Assert.That(route.Validate(), Is.Empty, "seed=" + seed);
                Assert.That(route.MandatoryNodeIds, Has.Count.EqualTo(21), "seed=" + seed);
                Assert.That(route.Nodes.Count(node => !node.IsMandatory), Is.EqualTo(3), "seed=" + seed);
                foreach (string sectorId in new[] { "patio", "archivo", "forjas" })
                {
                    string[] templates = route.Nodes.Where(node => node.SectorId == sectorId).Select(node => node.TemplateId).ToArray();
                    Assert.That(templates.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(templates.Length),
                        "sector=" + sectorId + " seed=" + seed);
                }

                fingerprints.Add(route.Fingerprint);
            }

            Assert.That(fingerprints.Count, Is.GreaterThan(1), "Different seeds should alter the route distribution.");
        }

        [Test]
        public void CatalogRejectsAnUnstableOrDuplicatedTemplatePool()
        {
            ExpeditionRouteCatalog valid = CreateCatalog();
            var invalidSector = new ExpeditionRouteSectorCatalog(
                "patio",
                new[] { "room.patio.c1", "room.patio.c1", "room.patio.c3", "room.patio.c4", "room.patio.c5" },
                new[] { "room.patio.e1", "room.patio.e2" },
                "room.patio.t", "room.patio.p", "room.patio.n", "room.patio.guardian");
            var invalid = new ExpeditionRouteCatalog(
                new[] { invalidSector, valid.Sectors[1], valid.Sectors[2] }, valid.RestTemplateId, valid.FinalBossTemplateId);

            Assert.That(invalid.Validate(), Is.Not.Empty);
            Assert.Throws<ArgumentException>(() => ExpeditionRouteGenerator.Generate(1u, invalid));
        }

        private static ExpeditionRouteCatalog CreateCatalog()
        {
            return new ExpeditionRouteCatalog(new[]
            {
                CreateSector("patio"),
                CreateSector("archivo"),
                CreateSector("forjas")
            }, "room.hub.rest", "room.final.arena");
        }

        private static ExpeditionRouteSectorCatalog CreateSector(string sectorId)
        {
            return new ExpeditionRouteSectorCatalog(
                sectorId,
                Enumerable.Range(1, 5).Select(index => "room." + sectorId + ".c" + index),
                Enumerable.Range(1, 2).Select(index => "room." + sectorId + ".e" + index),
                "room." + sectorId + ".t",
                "room." + sectorId + ".p",
                "room." + sectorId + ".n",
                "room." + sectorId + ".guardian");
        }
    }
}
