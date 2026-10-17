using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ItemCatalogAndPickupTests
    {
        [Test]
        public void D04ItemCatalogLoadsStarterEquipmentConsumablesRelicsAndSixMaterials()
        {
            ItemCatalog catalog = LoadCatalog();
            Assert.That(catalog.Count, Is.EqualTo(26));
            Assert.That(catalog.Definitions.Count(definition => definition.Kind == ItemKind.Weapon), Is.EqualTo(9));
            Assert.That(catalog.Get("weapon.bellator.rusted_sword").IsCompatibleWithClass("class.bellator"), Is.True);
            Assert.That(catalog.Get("weapon.venator.short_bow").IsCompatibleWithClass("class.bellator"), Is.False);
            Assert.That(catalog.GetStartingLoadout("class.bellator").WeaponItemId, Is.EqualTo("weapon.bellator.rusted_sword"));
            Assert.That(catalog.GetStartingLoadout("class.venator").ArmorItemId, Is.EqualTo("armor.venator.leather_vest"));
            Assert.That(catalog.GetStartingLoadout("class.magus").WeaponItemId, Is.EqualTo("weapon.magus.ember_staff"));
            Assert.That(catalog.Get("consumable.health_potion").MaxStack, Is.EqualTo(3));
            Assert.That(catalog.GetMaterial(MaterialKind.Ore).MaxStack, Is.EqualTo(99));
            foreach (MaterialKind materialKind in System.Enum.GetValues(typeof(MaterialKind)))
            {
                Assert.That(catalog.GetMaterial(materialKind), Is.Not.Null);
            }
        }

        [Test]
        public void D04ItemLoaderRejectsDuplicateIdsInvalidStacksMissingMaterialsAndUnknownClasses()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(source, Is.Not.Null);
            Assert.Throws<System.FormatException>(() => ItemCatalogJsonLoader.Load(
                source.text.Replace("weapon.venator.short_bow", "weapon.bellator.rusted_sword")));
            Assert.Throws<System.FormatException>(() => ItemCatalogJsonLoader.Load(
                source.text.Replace("\"maxStack\": 3", "\"maxStack\": 4")));
            Assert.Throws<System.FormatException>(() => ItemCatalogJsonLoader.Load(
                source.text.Replace("\"materialKind\": \"VitalEmber\"", "\"materialKind\": \"Ore\"")));
            Assert.Throws<System.FormatException>(() => ItemCatalogJsonLoader.Load(
                source.text.Replace("\"maxStack\": 99", "\"maxStack\": 1")));
            Assert.Throws<System.FormatException>(() => ItemCatalogJsonLoader.Load(
                source.text.Replace("class.bellator", "class.unknown")));
            Assert.Throws<System.FormatException>(() => ItemCatalogJsonLoader.Load(
                source.text.Replace("\"schemaVersion\": 2", "\"schemaVersion\": 1")));
        }

        [UnityTest]
        public IEnumerator D04_PhysicalPickupEquipFullBagAndStagedReplacementPreserveEveryItem()
        {
            const int scenarioSeed = 27182818;
            using (var evidence = new ScenarioEvidenceRecorder("D04", "D04_PhysicalLootAndInventorySwap", scenarioSeed))
            {
                var createdObjects = new List<GameObject>();
                try
                {
                    ItemCatalog catalog = LoadCatalog();
                    var inventory = new ExpeditionInventory(catalog, "class.bellator");
                    GameObject player = CreateCollector("D04Player", Vector2.zero, inventory, catalog, createdObjects,
                        out ItemCollector2D collector, out LootPickupDropStager2D stager);
                    InventoryItemStack firstSword = CreateStack("weapon.bellator.rusted_sword");
                    LootPickup2D firstPickup = CreatePickup("D04FirstSword", firstSword, Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    evidence.RecordInput(0, "Physics2DTrigger", "player collider overlaps rusted sword pickup");
                    yield return new WaitForFixedUpdate();

                    bool firstPickupCollected = firstPickup.IsConsumed && inventory.GetBackpackSlot(0)?.InstanceId == firstSword.InstanceId;
                    evidence.RecordAssertion("A real 2D trigger collects a compatible weapon into the backpack",
                        firstPickupCollected, "pickup consumed; slot 0 contains the same instance",
                        "consumed=" + firstPickup.IsConsumed + "; slot0=" + inventory.GetBackpackSlot(0)?.InstanceId);

                    InventoryOperationOutcome firstEquip = inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New());
                    InventoryItemStack secondSword = CreateStack("weapon.bellator.rusted_sword");
                    LootPickup2D secondPickup = CreatePickup("D04SecondSword", secondSword, Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    InventoryOperationOutcome secondEquip = inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New());
                    bool equipmentSwapPassed = firstEquip.Succeeded && secondEquip.Succeeded &&
                        inventory.Weapon.InstanceId == secondSword.InstanceId && inventory.GetBackpackSlot(0).InstanceId == firstSword.InstanceId;
                    evidence.RecordInput(1, "Equip", "equip first sword, collect and equip a second sword");
                    evidence.RecordAssertion("Equipping a replacement returns the previous equipment to the freed index",
                        equipmentSwapPassed, "second sword equipped; first sword returned to slot 0",
                        "weapon=" + inventory.Weapon.InstanceId + "; slot0=" + inventory.GetBackpackSlot(0).InstanceId);

                    for (int index = 1; index < ExpeditionInventory.BackpackCapacity; index++)
                    {
                        InventoryOperationOutcome filled = inventory.TryCollectItem(CreateStack("armor.magus.traveler_robe"), TransactionId.New());
                        if (!filled.Succeeded) break;
                    }
                    bool backpackFull = true;
                    for (int index = 0; index < ExpeditionInventory.BackpackCapacity; index++) backpackFull &= inventory.GetBackpackSlot(index) != null;
                    InventoryItemStack incoming = CreateStack("relic.marked_coin");
                    LootPickup2D blockedPickup = CreatePickup("D04FullBagPickup", incoming, Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    evidence.RecordInput(2, "Physics2DTrigger", "enter pickup radius with all 12 backpack indices occupied");
                    yield return new WaitForFixedUpdate();

                    bool fullPickupPreserved = backpackFull && !blockedPickup.IsConsumed && blockedPickup.gameObject.activeInHierarchy &&
                        collector.LastInteractionStatus == LootPickupInteractionStatus.RejectedFull;
                    evidence.RecordAssertion("A full backpack rejects the physical pickup without consuming it",
                        fullPickupPreserved, "pickup remains active and inventory unchanged",
                        "full=" + backpackFull + "; consumed=" + blockedPickup.IsConsumed + "; interaction=" + collector.LastInteractionStatus);

                    ItemInstanceId slotFiveBefore = inventory.GetBackpackSlot(5).InstanceId;
                    bool invalidSlotRejected = !collector.TryReplaceBackpackSlot(-1, blockedPickup) &&
                        collector.LastOperationOutcome.Status == InventoryOperationStatus.RejectedInvalidSlot &&
                        inventory.GetBackpackSlot(5).InstanceId == slotFiveBefore && !blockedPickup.IsConsumed;
                    evidence.RecordAssertion("An invalid replacement index preserves the incoming and displaced objects",
                        invalidSlotRejected, "rejected invalid index; both instances unchanged",
                        "status=" + collector.LastOperationOutcome.Status + "; slot5=" + inventory.GetBackpackSlot(5).InstanceId);

                    bool unpreparedDropRejected = !collector.TryReplaceBackpackSlot(5, blockedPickup) &&
                        collector.LastInteractionStatus == LootPickupInteractionStatus.DropStageFailed &&
                        inventory.GetBackpackSlot(5).InstanceId == slotFiveBefore && !blockedPickup.IsConsumed;
                    evidence.RecordAssertion("A replacement is rejected if the displaced pickup cannot be staged",
                        unpreparedDropRejected, "drop stage failed; both original instances retained",
                        "interaction=" + collector.LastInteractionStatus + "; slot5=" + inventory.GetBackpackSlot(5).InstanceId);

                    GameObject worldRoot = new GameObject("D04WorldDrops");
                    createdObjects.Add(worldRoot);
                    GameObject dropPrefabObject = new GameObject("D04DropPrefab");
                    dropPrefabObject.transform.position = new Vector3(50f, 50f, 0f);
                    dropPrefabObject.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                    CircleCollider2D prefabCollider = dropPrefabObject.AddComponent<CircleCollider2D>();
                    prefabCollider.radius = 0.25f;
                    LootPickup2D dropPrefab = dropPrefabObject.AddComponent<LootPickup2D>();
                    createdObjects.Add(dropPrefabObject);
                    stager.Configure(dropPrefab, worldRoot.transform);

                    InventoryItemStack displaced = inventory.GetBackpackSlot(5);
                    bool replacementCommitted = collector.TryReplaceBackpackSlot(5, blockedPickup);
                    LootPickup2D stagedWorldDrop = worldRoot.GetComponentInChildren<LootPickup2D>(true);
                    bool replacementAtomic = replacementCommitted && collector.LastOperationOutcome.Succeeded &&
                        collector.LastOperationOutcome.DisplacedItem.InstanceId == displaced.InstanceId &&
                        inventory.GetBackpackSlot(5).InstanceId == incoming.InstanceId && blockedPickup.IsConsumed &&
                        stagedWorldDrop != null && stagedWorldDrop.Item.InstanceId == displaced.InstanceId && stagedWorldDrop.gameObject.activeInHierarchy;
                    evidence.RecordInput(3, "ReplaceBackpackSlot", "stage old instance inactive, replace index 5, activate displaced world pickup");
                    evidence.RecordAssertion("A prepared replacement commits once and returns the displaced instance to the world",
                        replacementAtomic, "incoming in slot 5; outgoing pickup active under world root",
                        "committed=" + replacementCommitted + "; incoming=" + inventory.GetBackpackSlot(5).InstanceId +
                        "; staged=" + (stagedWorldDrop == null ? "missing" : stagedWorldDrop.Item.InstanceId.ToString()));

                    player.transform.position = new Vector2(10f, 0f);
                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    bool ignoredOriginalCollector = stagedWorldDrop != null && !stagedWorldDrop.IsConsumed;

                    var secondInventory = new ExpeditionInventory(catalog, "class.bellator");
                    GameObject secondPlayer = CreateCollector("D04SecondPlayer", Vector2.zero, secondInventory, catalog, createdObjects,
                        out ItemCollector2D secondCollector, out _);
                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    bool displacedPickupReusable = ignoredOriginalCollector && secondCollector.LastInteractionStatus == LootPickupInteractionStatus.Collected &&
                        stagedWorldDrop.IsConsumed && secondInventory.GetBackpackSlot(0)?.InstanceId == displaced.InstanceId;
                    evidence.RecordAssertion("The replacement actor cannot immediately collect its own staged drop, while another actor can",
                        displacedPickupReusable, "old actor ignored until it leaves; second actor collects the displaced item",
                        "oldStillIgnored=" + ignoredOriginalCollector + "; second=" + secondCollector.LastInteractionStatus +
                        "; collectedBySecond=" + (secondInventory.GetBackpackSlot(0)?.InstanceId == displaced.InstanceId));
                    evidence.RecordEvent("seed=" + scenarioSeed + "; finalBackpackCount=" + inventory.Backpack.Count +
                        "; firstEquipped=" + firstSword.InstanceId + "; replacementIncoming=" + incoming.InstanceId +
                        "; worldDrop=" + displaced.InstanceId + "; secondPlayer=" + secondPlayer.name);

                    int exitCode = evidence.Complete();
                    Assert.That(exitCode, Is.Zero, "D04 physical pickup and inventory swap scenario failed. Evidence: " + evidence.ReportPath);
                }
                finally
                {
                    foreach (GameObject createdObject in createdObjects)
                    {
                        if (createdObject != null) Object.Destroy(createdObject);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator E01_PhysicalWorldMaterialPickupTracksDvergarBonusAndRejectsOverflowAtomically()
        {
            const int scenarioSeed = 16180340;
            using (var evidence = new ScenarioEvidenceRecorder("E01", "E01_PhysicalWorldMaterialPickupAndBag", scenarioSeed))
            {
                var createdObjects = new List<GameObject>();
                try
                {
                    ItemCatalog catalog = LoadCatalog();
                    var inventory = new ExpeditionInventory(catalog, "class.bellator");
                    CreateCollector("E01DvergarPlayer", Vector2.zero, inventory, catalog, createdObjects,
                        out ItemCollector2D collector, out _, raceId: PlayableRaceRules.DvergarRaceId);

                    WorldMaterialPickup2D first = CreateWorldMaterialPickup("E01OreA", MaterialKind.Ore, 3,
                        Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    evidence.RecordInput(0, "Physics2DTrigger", "Dvergar enters a world pickup containing 3 ore");
                    yield return new WaitForFixedUpdate();
                    bool firstCollected = first.IsConsumed && inventory.GetMaterialCount(MaterialKind.Ore) == 3 &&
                        inventory.GetWorldMaterialRemainder(MaterialKind.Ore) == 3 && inventory.Backpack[0] == null;
                    evidence.RecordAssertion("A material pickup enters the separate bag and carries Dvergar progress",
                        firstCollected, "ore=3; remainder=3; backpack unchanged",
                        "consumed=" + first.IsConsumed + "; ore=" + inventory.GetMaterialCount(MaterialKind.Ore) +
                        "; remainder=" + inventory.GetWorldMaterialRemainder(MaterialKind.Ore));

                    string snapshotJson = JsonUtility.ToJson(inventory.CreateSnapshot());
                    ExpeditionInventory restored = ExpeditionInventory.Restore(catalog,
                        JsonUtility.FromJson<ExpeditionInventorySnapshot>(snapshotJson));
                    bool partialProgressPersisted = restored.GetMaterialCount(MaterialKind.Ore) == 3 &&
                        restored.GetWorldMaterialRemainder(MaterialKind.Ore) == 3;
                    evidence.RecordAssertion("Unity JSON save and restore retains the partial Dvergar world counter",
                        partialProgressPersisted, "ore=3 and remainder=3 survive snapshot serialization",
                        "ore=" + restored.GetMaterialCount(MaterialKind.Ore) +
                        "; remainder=" + restored.GetWorldMaterialRemainder(MaterialKind.Ore));

                    WorldMaterialPickup2D second = CreateWorldMaterialPickup("E01OreB", MaterialKind.Ore, 2,
                        Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    evidence.RecordInput(1, "Physics2DTrigger", "collect 2 more ore from the world");
                    yield return new WaitForFixedUpdate();
                    bool thresholdAwarded = second.IsConsumed && inventory.GetMaterialCount(MaterialKind.Ore) == 6 &&
                        inventory.GetWorldMaterialRemainder(MaterialKind.Ore) == 0;
                    evidence.RecordAssertion("Every five accepted world units award one additional unit of the same material",
                        thresholdAwarded, "5 ore from world becomes 6; remainder resets to 0",
                        "consumed=" + second.IsConsumed + "; ore=" + inventory.GetMaterialCount(MaterialKind.Ore) +
                        "; remainder=" + inventory.GetWorldMaterialRemainder(MaterialKind.Ore));

                    string bagSummary = ExpeditionRunSessionPresenter.FormatMaterialBagSummary(inventory);
                    bool sixCountersDisplayed = new[] { "MADERA 0", "MINERAL 6", "HIERBA 0", "POLVO 0", "RESINA 0", "ASC. VITAL 0" }
                        .All(bagSummary.Contains);
                    evidence.RecordAssertion("The expedition summary displays all six independent material counters",
                        sixCountersDisplayed, "each material appears as a separate bag counter",
                        bagSummary.Replace("\n", " | "));

                    Assert.That(inventory.TryAddMaterial(MaterialKind.ShadowWood, 95, TransactionId.New()).Succeeded, Is.True);
                    WorldMaterialPickup2D fourWood = CreateWorldMaterialPickup("E01WoodProgress", MaterialKind.ShadowWood, 4,
                        Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    bool normalCapReached = fourWood.IsConsumed && inventory.GetMaterialCount(MaterialKind.ShadowWood) == 99 &&
                        inventory.GetWorldMaterialRemainder(MaterialKind.ShadowWood) == 4;
                    WorldMaterialPickup2D overflowingBonus = CreateWorldMaterialPickup("E01OverflowBonus", MaterialKind.ShadowWood, 1,
                        Vector2.zero, catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    evidence.RecordInput(2, "Physics2DTrigger", "Dvergar bonus would raise a capped material above 99");
                    yield return new WaitForFixedUpdate();
                    bool bonusOverflowPreservedPickup = normalCapReached && !overflowingBonus.IsConsumed &&
                        overflowingBonus.gameObject.activeInHierarchy && inventory.GetMaterialCount(MaterialKind.ShadowWood) == 99 &&
                        inventory.GetWorldMaterialRemainder(MaterialKind.ShadowWood) == 4 &&
                        collector.LastOperationOutcome.Status == InventoryOperationStatus.RejectedMaterialCapacity;
                    evidence.RecordAssertion("A Dvergar bonus overflow rejects the entire pickup without advancing its remainder",
                        bonusOverflowPreservedPickup, "pickup remains in world; count=99; remainder=4",
                        "consumed=" + overflowingBonus.IsConsumed + "; count=" + inventory.GetMaterialCount(MaterialKind.ShadowWood) +
                        "; remainder=" + inventory.GetWorldMaterialRemainder(MaterialKind.ShadowWood) +
                        "; status=" + collector.LastOperationOutcome.Status);

                    var orciniInventory = new ExpeditionInventory(catalog, "class.bellator");
                    CreateCollector("E01OrciniPlayer", new Vector2(10f, 0f), orciniInventory, catalog, createdObjects,
                        out ItemCollector2D orciniCollector, out _, raceId: PlayableRaceRules.OrciniRaceId);
                    WorldMaterialPickup2D orciniPickup = CreateWorldMaterialPickup("E01OrciniOre", MaterialKind.Herb, 5,
                        new Vector2(10f, 0f), catalog, createdObjects);
                    Physics2D.SyncTransforms();
                    evidence.RecordInput(3, "Physics2DTrigger", "Orcini enters a world pickup containing 5 herb");
                    yield return new WaitForFixedUpdate();
                    bool nonDvergarCollectedWithoutBonus = orciniPickup.IsConsumed &&
                        orciniInventory.GetMaterialCount(MaterialKind.Herb) == 5 &&
                        orciniInventory.GetWorldMaterialRemainder(MaterialKind.Herb) == 0 &&
                        orciniCollector.RaceId == PlayableRaceRules.OrciniRaceId && !orciniCollector.GrantsDvergarBonus;
                    evidence.RecordAssertion("A known non-Dvergar race receives no material bonus or Dvergar remainder progress",
                        nonDvergarCollectedWithoutBonus, "Orcini collects 5 herb; count=5; remainder=0",
                        "consumed=" + orciniPickup.IsConsumed + "; herb=" + orciniInventory.GetMaterialCount(MaterialKind.Herb) +
                        "; remainder=" + orciniInventory.GetWorldMaterialRemainder(MaterialKind.Herb) +
                        "; race=" + orciniCollector.RaceId + "; bonus=" + orciniCollector.GrantsDvergarBonus);

                    int exitCode = evidence.Complete();
                    Assert.That(exitCode, Is.Zero, "E01 material pickup scenario failed. Evidence: " + evidence.ReportPath);
                }
                finally
                {
                    foreach (GameObject createdObject in createdObjects)
                    {
                        if (createdObject != null) Object.Destroy(createdObject);
                    }
                }
            }
        }

        [Test]
        public void E09ItemCollectorRejectsUnknownRaceIdsWithoutChangingConfiguration()
        {
            var player = new GameObject("E09InvalidRaceCollector");
            try
            {
                ItemCollector2D collector = player.AddComponent<ItemCollector2D>();
                collector.Configure(null, null);
                Assert.That(collector.RaceId, Is.EqualTo(PlayableRaceRules.HumanRaceId));

                Assert.Throws<System.ArgumentException>(() =>
                    collector.Configure(null, null, null, "race.unknown"));

                Assert.That(collector.RaceId, Is.EqualTo(PlayableRaceRules.HumanRaceId));
                Assert.That(collector.GrantsDvergarBonus, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        private static ItemCatalog LoadCatalog()
        {
            TextAsset source = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(source, Is.Not.Null, "Items.json must be imported from Resources.");
            return ItemCatalogJsonLoader.Load(source.text);
        }

        private static InventoryItemStack CreateStack(string itemId)
        {
            return new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, 1);
        }

        private static GameObject CreateCollector(
            string objectName,
            Vector2 position,
            ExpeditionInventory inventory,
            ItemCatalog catalog,
            List<GameObject> createdObjects,
            out ItemCollector2D collector,
            out LootPickupDropStager2D stager,
            string raceId = PlayableRaceRules.HumanRaceId)
        {
            var player = new GameObject(objectName);
            player.transform.position = position;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            BoxCollider2D collider = player.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            stager = player.AddComponent<LootPickupDropStager2D>();
            stager.Configure(null);
            collector = player.AddComponent<ItemCollector2D>();
            collector.Configure(inventory, catalog, stager, raceId);
            createdObjects.Add(player);
            return player;
        }

        private static LootPickup2D CreatePickup(
            string objectName,
            InventoryItemStack item,
            Vector2 position,
            ItemCatalog catalog,
            List<GameObject> createdObjects)
        {
            var pickupObject = new GameObject(objectName);
            pickupObject.transform.position = position;
            Rigidbody2D body = pickupObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            LootPickup2D pickup = pickupObject.AddComponent<LootPickup2D>();
            pickup.Initialize(item, catalog);
            createdObjects.Add(pickupObject);
            return pickup;
        }

        private static WorldMaterialPickup2D CreateWorldMaterialPickup(
            string objectName,
            MaterialKind materialKind,
            int amount,
            Vector2 position,
            ItemCatalog catalog,
            List<GameObject> createdObjects)
        {
            var pickupObject = new GameObject(objectName);
            pickupObject.transform.position = position;
            Rigidbody2D body = pickupObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            CircleCollider2D collider = pickupObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.25f;
            WorldMaterialPickup2D pickup = pickupObject.AddComponent<WorldMaterialPickup2D>();
            pickup.Initialize(materialKind, amount, catalog);
            createdObjects.Add(pickupObject);
            return pickup;
        }
    }
}
