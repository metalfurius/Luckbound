using System.Collections.Generic;
using Luckbound.App;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.EditMode
{
    public sealed class ExpeditionRoomPrefabRegistryTests
    {
        private GameObject _prototypeRoom;

        [SetUp]
        public void SetUp()
        {
            _prototypeRoom = Resources.Load<GameObject>("Luckbound/Rooms/PrototypeRoom");
            Assert.That(_prototypeRoom, Is.Not.Null, "The existing prototype room fixture is required.");
        }

        [Test]
        public void MissingTemplateReturnsRecoverableErrorWithoutFallback()
        {
            bool resolved = ExpeditionRoomPrefabRegistry.TryResolve("room.sample.tutorial",
                new ExpeditionRoomPrefabEntry[0], out GameObject prefab, out string error);

            Assert.That(resolved, Is.False);
            Assert.That(prefab, Is.Null);
            StringAssert.Contains("No room prefab is registered", error);
        }

        [Test]
        public void PrototypeRoomCannotBeAliasedToASampleTemplate()
        {
            bool resolved = ExpeditionRoomPrefabRegistry.TryResolve("room.sample.tutorial",
                new[] { new ExpeditionRoomPrefabEntry { templateId = "room.sample.tutorial", prefab = _prototypeRoom } },
                out GameObject prefab, out string error);

            Assert.That(resolved, Is.False);
            Assert.That(prefab, Is.Null);
            StringAssert.Contains("room.patio.prototype", error);
        }

        [Test]
        public void DuplicateRegistrationIsRejectedInsteadOfSelectingOneArbitrarily()
        {
            var entries = new List<ExpeditionRoomPrefabEntry>
            {
                new ExpeditionRoomPrefabEntry { templateId = "room.patio.prototype", prefab = _prototypeRoom },
                new ExpeditionRoomPrefabEntry { templateId = "room.patio.prototype", prefab = _prototypeRoom }
            };

            bool resolved = ExpeditionRoomPrefabRegistry.TryResolve("room.patio.prototype", entries,
                out GameObject prefab, out string error);

            Assert.That(resolved, Is.False);
            Assert.That(prefab, Is.Null);
            StringAssert.Contains("More than one room prefab", error);
        }

        [Test]
        public void CorrectlyRegisteredPrototypeResolvesOnlyItsOwnIdentity()
        {
            bool resolved = ExpeditionRoomPrefabRegistry.TryResolve("room.patio.prototype",
                new[] { new ExpeditionRoomPrefabEntry { templateId = "room.patio.prototype", prefab = _prototypeRoom } },
                out GameObject prefab, out string error);

            Assert.That(resolved, Is.True, error);
            Assert.That(prefab, Is.SameAs(_prototypeRoom));
        }
    }
}
