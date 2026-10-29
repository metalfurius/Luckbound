using System.Collections.Generic;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class I06LootPickupCueTests
    {
        [Test]
        public void SuccessfulItemAndMaterialPickupsSignalOnceForTheirCollector()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                ItemCatalog catalog = LoadCatalog();
                var inventory = new ExpeditionInventory(catalog, "class.bellator");
                ItemCollector2D collector = CreateCollector("I06LootSuccessCollector", inventory, catalog, createdObjects,
                    PlayableRaceRules.DvergarRaceId);
                var signals = new List<ItemCollector2D>();
                collector.LootPickupSucceeded += _ => throw new System.InvalidOperationException("Simulated audio listener failure.");
                collector.LootPickupSucceeded += signals.Add;

                InventoryItemStack item = CreateStack("weapon.bellator.rusted_sword");
                LootPickup2D itemPickup = CreateItemPickup("I06LootItem", item, catalog, createdObjects);
                Assert.That(collector.TryCollect(itemPickup), Is.True);
                Assert.That(itemPickup.IsConsumed, Is.True);
                Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(item.InstanceId));
                Assert.That(signals.Count, Is.EqualTo(1));
                Assert.That(signals[0], Is.SameAs(collector));

                Assert.That(collector.TryCollect(itemPickup), Is.False);
                Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.AlreadyConsumed));
                Assert.That(signals.Count, Is.EqualTo(1), "A consumed pickup must remain silent.");

                WorldMaterialPickup2D materialPickup = CreateMaterialPickup("I06LootDvergarMaterial", MaterialKind.Ore,
                    5, catalog, createdObjects);
                Assert.That(collector.TryCollectWorldMaterial(materialPickup), Is.True);
                Assert.That(materialPickup.IsConsumed, Is.True);
                Assert.That(inventory.GetMaterialCount(MaterialKind.Ore), Is.EqualTo(6));
                Assert.That(inventory.GetWorldMaterialRemainder(MaterialKind.Ore), Is.EqualTo(0));
                Assert.That(signals.Count, Is.EqualTo(2), "One accepted physical pickup produces one signal, including its Dvergar bonus.");
                Assert.That(signals[1], Is.SameAs(collector));

                Assert.That(collector.TryCollectWorldMaterial(materialPickup), Is.False);
                Assert.That(signals.Count, Is.EqualTo(2), "The bonus amount and consumed replay must not produce extra signals.");

                ExpeditionInventory restoredInventory = ExpeditionInventory.Restore(catalog, inventory.CreateSnapshot());
                collector.PublishCommittedInventory(inventory);
                collector.PublishCommittedInventory(restoredInventory);
                Assert.That(signals.Count, Is.EqualTo(2), "Publishing or restoring inventory state must not emit a pickup signal.");
            }
            finally
            {
                DestroyAll(createdObjects);
            }
        }

        [Test]
        public void UnconfiguredExhaustedAndUnstageablePickupsRemainSilent()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                ItemCatalog catalog = LoadCatalog();
                ItemCollector2D collector = CreateCollector("I06LootRejectedCollector", null, null, createdObjects);
                int signalCount = 0;
                collector.LootPickupSucceeded += _ => signalCount++;

                LootPickup2D itemPickup = CreateItemPickup("I06LootRejectedItem",
                    CreateStack("relic.marked_coin"), catalog, createdObjects);
                WorldMaterialPickup2D materialPickup = CreateMaterialPickup("I06LootRejectedMaterial", MaterialKind.Ore,
                    1, catalog, createdObjects);
                Assert.That(collector.TryCollect(itemPickup), Is.False);
                Assert.That(collector.TryCollectWorldMaterial(materialPickup), Is.False);
                Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.NotConfigured));
                Assert.That(signalCount, Is.Zero);

                var inventory = new ExpeditionInventory(catalog, "class.bellator");
                for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++)
                {
                    InventoryOperationOutcome fill = inventory.TryCollectItem(
                        CreateStack("armor.magus.traveler_robe"), TransactionId.New());
                    Assert.That(fill.Succeeded, Is.True);
                }
                Assert.That(inventory.TryAddMaterial(MaterialKind.Ore, 99, TransactionId.New()).Succeeded, Is.True);
                collector.Configure(inventory, catalog);

                Assert.That(collector.TryCollect(itemPickup), Is.False);
                Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.RejectedFull));
                Assert.That(itemPickup.IsConsumed, Is.False);
                Assert.That(collector.TryCollectWorldMaterial(materialPickup), Is.False);
                Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.RejectedFull));
                Assert.That(materialPickup.IsConsumed, Is.False);
                Assert.That(signalCount, Is.Zero, "Full backpack and material capacity failures must be silent.");

                Assert.That(collector.TryReplaceBackpackSlot(0, itemPickup), Is.False);
                Assert.That(collector.LastInteractionStatus, Is.EqualTo(LootPickupInteractionStatus.DropStageFailed));
                Assert.That(itemPickup.IsConsumed, Is.False);
                Assert.That(signalCount, Is.Zero, "A replacement that cannot stage its displaced drop must be silent.");
            }
            finally
            {
                DestroyAll(createdObjects);
            }
        }

        [Test]
        public void SuccessfulReplacementSignalsOnlyAfterTheDisplacedDropIsCommitted()
        {
            var createdObjects = new List<GameObject>();
            try
            {
                ItemCatalog catalog = LoadCatalog();
                var inventory = new ExpeditionInventory(catalog, "class.bellator");
                GameObject player = new GameObject("I06LootReplacementCollector");
                createdObjects.Add(player);
                LootPickupDropStager2D stager = player.AddComponent<LootPickupDropStager2D>();
                ItemCollector2D collector = player.AddComponent<ItemCollector2D>();
                collector.Configure(inventory, catalog, stager);

                InventoryItemStack displaced = CreateStack("weapon.bellator.rusted_sword");
                Assert.That(inventory.TryCollectItem(displaced, TransactionId.New()).Succeeded, Is.True);

                GameObject worldRootObject = new GameObject("I06LootWorldDropRoot");
                createdObjects.Add(worldRootObject);
                LootPickup2D dropPrefab = CreateItemPickup("I06LootDropPrefab", CreateStack("weapon.bellator.rusted_sword"),
                    catalog, createdObjects);
                stager.Configure(dropPrefab, worldRootObject.transform);

                InventoryItemStack incoming = CreateStack("relic.marked_coin");
                LootPickup2D incomingPickup = CreateItemPickup("I06LootIncomingReplacement", incoming, catalog, createdObjects);
                int signalCount = 0;
                bool signalHadCommittedDrop = false;
                ItemCollector2D signaledCollector = null;
                collector.LootPickupSucceeded += sender =>
                {
                    signalCount++;
                    signaledCollector = sender;
                    LootPickup2D[] worldDrops = worldRootObject.GetComponentsInChildren<LootPickup2D>(true);
                    foreach (LootPickup2D worldDrop in worldDrops)
                    {
                        if (worldDrop != null && worldDrop.Item != null &&
                            worldDrop.Item.InstanceId == displaced.InstanceId && worldDrop.gameObject.activeInHierarchy)
                            signalHadCommittedDrop = true;
                    }
                };

                Assert.That(collector.TryReplaceBackpackSlot(0, incomingPickup), Is.True);
                LootPickup2D stagedWorldDrop = worldRootObject.GetComponentInChildren<LootPickup2D>(true);
                Assert.That(collector.LastOperationOutcome.Succeeded, Is.True);
                Assert.That(inventory.GetBackpackSlot(0).InstanceId, Is.EqualTo(incoming.InstanceId));
                Assert.That(incomingPickup.IsConsumed, Is.True);
                Assert.That(stagedWorldDrop, Is.Not.Null);
                Assert.That(stagedWorldDrop.Item.InstanceId, Is.EqualTo(displaced.InstanceId));
                Assert.That(stagedWorldDrop.gameObject.activeInHierarchy, Is.True);
                Assert.That(signalCount, Is.EqualTo(1));
                Assert.That(signaledCollector, Is.SameAs(collector));
                Assert.That(signalHadCommittedDrop, Is.True, "The observer runs only after the staged drop becomes active in the world.");
            }
            finally
            {
                DestroyAll(createdObjects);
            }
        }

        private static ItemCatalog LoadCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(source, Is.Not.Null, "Items.json must be imported from Resources.");
            return ItemCatalogJsonLoader.Load(source.text);
        }

        private static ItemCollector2D CreateCollector(string objectName, ExpeditionInventory inventory, ItemCatalog catalog,
            List<GameObject> createdObjects, string raceId = PlayableRaceRules.HumanRaceId)
        {
            var player = new GameObject(objectName);
            createdObjects.Add(player);
            ItemCollector2D collector = player.AddComponent<ItemCollector2D>();
            if (catalog != null) collector.Configure(inventory, catalog, raceId: raceId);
            return collector;
        }

        private static InventoryItemStack CreateStack(string itemId)
        {
            return new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, 1);
        }

        private static LootPickup2D CreateItemPickup(string objectName, InventoryItemStack item, ItemCatalog catalog,
            List<GameObject> createdObjects)
        {
            var pickupObject = new GameObject(objectName);
            createdObjects.Add(pickupObject);
            pickupObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            pickupObject.AddComponent<CircleCollider2D>();
            LootPickup2D pickup = pickupObject.AddComponent<LootPickup2D>();
            pickup.Initialize(item, catalog);
            return pickup;
        }

        private static WorldMaterialPickup2D CreateMaterialPickup(string objectName, MaterialKind materialKind, int amount,
            ItemCatalog catalog, List<GameObject> createdObjects)
        {
            var pickupObject = new GameObject(objectName);
            createdObjects.Add(pickupObject);
            pickupObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            pickupObject.AddComponent<CircleCollider2D>();
            WorldMaterialPickup2D pickup = pickupObject.AddComponent<WorldMaterialPickup2D>();
            pickup.Initialize(materialKind, amount, catalog);
            return pickup;
        }

        private static void DestroyAll(List<GameObject> createdObjects)
        {
            foreach (GameObject createdObject in createdObjects)
            {
                if (createdObject != null) Object.DestroyImmediate(createdObject);
            }
        }
    }
}
