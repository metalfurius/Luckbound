using System;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using NUnit.Framework;

namespace Luckbound.Tests.EditMode
{
    public sealed class ExpeditionRoomIdentityTests
    {
        [Test]
        public void SameRunAndNodeRestoreSameValidRoomIdentity()
        {
            RunId run = new RunId(Guid.Parse("a26e508e-aea9-4f54-8c46-979953f03da1"));

            RoomInstanceId first = ExpeditionRoomIdentity.ForNode(run, "rest.01");
            RoomInstanceId restored = ExpeditionRoomIdentity.ForNode(run, "rest.01");

            Assert.That(first.IsValid, Is.True);
            Assert.That(restored, Is.EqualTo(first));
        }

        [Test]
        public void DifferentRunOrRouteNodeUsesDifferentRoomIdentity()
        {
            RunId firstRun = new RunId(Guid.Parse("a26e508e-aea9-4f54-8c46-979953f03da1"));
            RunId secondRun = new RunId(Guid.Parse("a26e508e-aea9-4f54-8c46-979953f03da2"));

            RoomInstanceId first = ExpeditionRoomIdentity.ForNode(firstRun, "rest.01");

            Assert.That(ExpeditionRoomIdentity.ForNode(firstRun, "rest.02"), Is.Not.EqualTo(first));
            Assert.That(ExpeditionRoomIdentity.ForNode(secondRun, "rest.01"), Is.Not.EqualTo(first));
        }

        [Test]
        public void InvalidRunOrNodeCannotCreateRoomIdentity()
        {
            Assert.Throws<ArgumentException>(() => ExpeditionRoomIdentity.ForNode(default, "rest.01"));
            Assert.Throws<ArgumentException>(() => ExpeditionRoomIdentity.ForNode(RunId.New(), " "));
        }
    }
}
