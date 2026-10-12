using System;
using System.Linq;
using Luckbound.Core.Expeditions;
using NUnit.Framework;

namespace Luckbound.Tests.Core
{
    public sealed class ExpeditionRouteTraversalTests
    {
        [Test]
        public void MandatoryTraversalAcceptsOnlyTheNextConnectedRouteNode()
        {
            ExpeditionRoutePlan plan = ExpeditionRouteGenerator.Generate(31415926u, CreateCatalog());
            var traversal = new ExpeditionRouteTraversal(plan);

            Assert.That(traversal.CurrentNodeId, Is.EqualTo(plan.MandatoryNodeIds[0]));
            Assert.That(traversal.CanTransitionTo(plan.MandatoryNodeIds[1]), Is.True);
            Assert.That(traversal.CanTransitionTo(plan.MandatoryNodeIds[2]), Is.False);
            Assert.That(traversal.TryTransitionTo(plan.MandatoryNodeIds[1]), Is.True);
            Assert.That(traversal.CurrentNodeId, Is.EqualTo(plan.MandatoryNodeIds[1]));
            Assert.That(traversal.TryTransitionTo(plan.MandatoryNodeIds[1]), Is.False);
            Assert.That(traversal.VisitedNodeIds, Has.Count.EqualTo(2));
        }

        [Test]
        public void TraversalCanChooseEitherTheOptionalDetourOrItsDirectBypass()
        {
            ExpeditionRoutePlan plan = ExpeditionRouteGenerator.Generate(27182818u, CreateCatalog());
            var traversal = new ExpeditionRouteTraversal(plan);
            string patioSecondCombat = "patio.combat.02";
            string optionalNode = "patio.optional.combat";
            string patioSpecial = "patio.special";

            foreach (string mandatoryNodeId in plan.MandatoryNodeIds.Skip(1).Take(2))
            {
                Assert.That(traversal.TryTransitionTo(mandatoryNodeId), Is.True);
            }

            Assert.That(traversal.CurrentNodeId, Is.EqualTo(patioSecondCombat));
            Assert.That(traversal.CanTransitionTo(optionalNode), Is.True);
            Assert.That(traversal.CanTransitionTo(patioSpecial), Is.True);
            Assert.That(traversal.TryTransitionTo(optionalNode), Is.True);
            Assert.That(traversal.TryTransitionTo(patioSpecial), Is.True);
            Assert.That(traversal.VisitedNodeIds.TakeLast(2), Is.EqualTo(new[] { optionalNode, patioSpecial }));
        }

        [Test]
        public void TraversalRejectsAnUnknownStartingNode()
        {
            ExpeditionRoutePlan plan = ExpeditionRouteGenerator.Generate(1u, CreateCatalog());

            Assert.Throws<ArgumentException>(() => new ExpeditionRouteTraversal(plan, "room.missing"));
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
