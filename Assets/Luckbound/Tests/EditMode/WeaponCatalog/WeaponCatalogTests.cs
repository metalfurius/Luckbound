using System;
using System.Collections.Generic;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.Weapons
{
    public sealed class WeaponCatalogTests
    {
        private static readonly ExpectedWeapon[] ExpectedWeapons =
        {
            new ExpectedWeapon("weapon.bellator.rusted_sword", "class.bellator", 12, 0.45f,
                "weapon.trait.balanced"),
            new ExpectedWeapon("weapon.bellator.trail_saber", "class.bellator", 9, 0.33f,
                "weapon.trait.rapid_cadence"),
            new ExpectedWeapon("weapon.bellator.umbra_greatsword", "class.bellator", 18, 0.70f,
                "weapon.trait.greater_range", "weapon.trait.greater_knockback"),
            new ExpectedWeapon("weapon.venator.short_bow", "class.venator", 10, 0.40f,
                "weapon.trait.rapid_cadence"),
            new ExpectedWeapon("weapon.venator.long_bow", "class.venator", 14, 0.65f,
                "weapon.trait.optional_charge_damage_bonus"),
            new ExpectedWeapon("weapon.venator.ruins_crossbow", "class.venator", 16, 0.75f,
                "weapon.trait.pierce_one_target"),
            new ExpectedWeapon("weapon.magus.ember_staff", "class.magus", 12, 0.50f,
                "weapon.trait.fire_projectile"),
            new ExpectedWeapon("weapon.magus.frost_staff", "class.magus", 10, 0.45f,
                "weapon.trait.slow_on_hit"),
            new ExpectedWeapon("weapon.magus.shadow_staff", "class.magus", 14, 0.60f,
                "weapon.trait.ignore_one_defense")
        };

        private TextAsset _itemsAsset;
        private TextAsset _weaponsAsset;
        private ItemCatalog _items;
        private WeaponCatalog _weapons;

        [SetUp]
        public void SetUp()
        {
            _itemsAsset = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            _weaponsAsset = Resources.Load<TextAsset>("Luckbound/Data/Weapons");
            Assert.That(_itemsAsset, Is.Not.Null, "Items.json must be imported from Resources.");
            Assert.That(_weaponsAsset, Is.Not.Null, "Weapons.json must be imported from Resources.");
            _items = ItemCatalogJsonLoader.Load(_itemsAsset.text);
            _weapons = WeaponCatalogJsonLoader.Load(_weaponsAsset.text, _items);
        }

        [Test]
        public void ContentHasExactNineProfilesAndMatchingWeaponItems()
        {
            Assert.That(_weapons.Count, Is.EqualTo(9));
            Assert.That(_weapons.Count, Is.EqualTo(WeaponCatalogJsonLoader.ExpectedWeaponCount));
            Assert.That(_items.Definitions.Count(definition => definition.Kind == ItemKind.Weapon), Is.EqualTo(9));
            CollectionAssert.AreEquivalent(ExpectedWeapons.Select(expected => expected.ItemId).ToArray(),
                _weapons.Profiles.Select(profile => profile.ItemId).ToArray());

            foreach (ExpectedWeapon expected in ExpectedWeapons)
            {
                WeaponProfile profile = _weapons.Get(expected.ItemId);
                ItemDefinition item = _items.Get(expected.ItemId);
                Assert.That(profile.ClassId, Is.EqualTo(expected.ClassId), expected.ItemId);
                Assert.That(profile.Damage, Is.EqualTo(expected.Damage), expected.ItemId);
                Assert.That(profile.AttackIntervalSeconds, Is.EqualTo(expected.AttackIntervalSeconds).Within(0.00001f), expected.ItemId);
                CollectionAssert.AreEqual(expected.TraitIds, profile.Traits.Select(trait => trait.StableId).ToArray(), expected.ItemId);
                Assert.That(item.Kind, Is.EqualTo(ItemKind.Weapon), expected.ItemId);
                Assert.That(item.MaxStack, Is.EqualTo(1), expected.ItemId);
                Assert.That(item.CompatibleClassIds.Count, Is.EqualTo(1), expected.ItemId);
                Assert.That(item.CompatibleClassIds[0], Is.EqualTo(expected.ClassId), expected.ItemId);
            }
        }

        [Test]
        public void TraitEffectsUseExactValuesAndQualitativeTraitsHaveNoInventedTuning()
        {
            WeaponTrait charge = _weapons.Get("weapon.venator.long_bow").Traits.Single();
            Assert.That(charge.Id, Is.EqualTo(WeaponTraitId.OptionalChargeDamageBonus));
            Assert.That(charge.Percent, Is.EqualTo(30));

            WeaponTrait piercing = _weapons.Get("weapon.venator.ruins_crossbow").Traits.Single();
            Assert.That(piercing.Id, Is.EqualTo(WeaponTraitId.PierceOneTarget));
            Assert.That(piercing.PiercedTargetCount, Is.EqualTo(1));

            WeaponTrait slow = _weapons.Get("weapon.magus.frost_staff").Traits.Single();
            Assert.That(slow.Id, Is.EqualTo(WeaponTraitId.SlowOnHit));
            Assert.That(slow.Percent, Is.EqualTo(20));
            Assert.That(slow.DurationSeconds, Is.EqualTo(0.75f).Within(0.00001f));

            WeaponTrait defense = _weapons.Get("weapon.magus.shadow_staff").Traits.Single();
            Assert.That(defense.Id, Is.EqualTo(WeaponTraitId.IgnoreOneDefense));
            Assert.That(defense.DefensePointsIgnored, Is.EqualTo(1));

            WeaponProfile greatsword = _weapons.Get("weapon.bellator.umbra_greatsword");
            Assert.That(greatsword.HasTrait(WeaponTraitId.GreaterRange), Is.True);
            Assert.That(greatsword.HasTrait(WeaponTraitId.GreaterKnockback), Is.True);
            Assert.That(greatsword.Traits.All(trait => trait.Percent == 0 && trait.DurationSeconds == 0f), Is.True);
        }

        [Test]
        public void ExistingStartingWeaponIdsRemainUnchanged()
        {
            Assert.That(_items.GetStartingLoadout("class.bellator").WeaponItemId, Is.EqualTo("weapon.bellator.rusted_sword"));
            Assert.That(_items.GetStartingLoadout("class.venator").WeaponItemId, Is.EqualTo("weapon.venator.short_bow"));
            Assert.That(_items.GetStartingLoadout("class.magus").WeaponItemId, Is.EqualTo("weapon.magus.ember_staff"));
        }

        [Test]
        public void EveryWeaponCanBeCollectedAndEquippedByItsClassAndRejectedByOtherClasses()
        {
            string[] classIds = { "class.bellator", "class.venator", "class.magus" };
            foreach (ExpectedWeapon expected in ExpectedWeapons)
            {
                var compatibleInventory = new ExpeditionInventory(_items, expected.ClassId);
                Assert.That(CollectAndEquip(compatibleInventory, expected.ItemId).Succeeded, Is.True, expected.ItemId);
                Assert.That(compatibleInventory.Weapon.ItemId, Is.EqualTo(expected.ItemId), expected.ItemId);
                Assert.That(compatibleInventory.Backpack[0], Is.Null, expected.ItemId);

                foreach (string classId in classIds.Where(classId => classId != expected.ClassId))
                {
                    var incompatibleInventory = new ExpeditionInventory(_items, classId);
                    InventoryOperationOutcome rejected = CollectAndEquip(incompatibleInventory, expected.ItemId);
                    Assert.That(rejected.Status, Is.EqualTo(InventoryOperationStatus.RejectedIncompatibleClass),
                        expected.ItemId + " rejected for " + classId);
                    Assert.That(incompatibleInventory.Weapon, Is.Null, expected.ItemId + " stays unequipped for " + classId);
                    Assert.That(incompatibleInventory.Backpack[0].ItemId, Is.EqualTo(expected.ItemId),
                        expected.ItemId + " remains available as loot after rejection for " + classId);
                }
            }
        }

        [Test]
        public void ProfileAndCatalogCollectionsAreImmutable()
        {
            Assert.Throws<NotSupportedException>(() => ((IList<WeaponProfile>)_weapons.Profiles).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<WeaponTrait>)_weapons.Get("weapon.bellator.umbra_greatsword").Traits).Clear());
        }

        [Test]
        public void LoaderRejectsMalformedAndDuplicateProfileIds()
        {
            string malformedId = _weaponsAsset.text.Replace(
                "weapon.bellator.trail_saber", "weapon.Bellator.trail_saber");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(malformedId, _items));

            string duplicateId = _weaponsAsset.text.Replace(
                "weapon.bellator.trail_saber", "weapon.bellator.rusted_sword");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(duplicateId, _items));

            string duplicateItemId = _itemsAsset.text.Replace(
                "\"id\": \"weapon.venator.short_bow\"", "\"id\": \"weapon.bellator.rusted_sword\"");
            Assert.Throws<FormatException>(() => ItemCatalogJsonLoader.Load(duplicateItemId));
        }

        [Test]
        public void LoaderRejectsProfilesThatDisagreeWithItemDefinitions()
        {
            string wrongClass = _weaponsAsset.text.Replace(
                "\"classId\": \"class.bellator\"", "\"classId\": \"class.venator\"");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(wrongClass, _items));

            string missingItem = _weaponsAsset.text.Replace(
                "weapon.bellator.trail_saber", "weapon.bellator.missing_sword");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(missingItem, _items));
        }

        [Test]
        public void LoaderRejectsMalformedJsonAndWrongProfileCountOrReferences()
        {
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load("{ malformed", _items));
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load("{\"schemaVersion\":1,\"weapons\":[]}", _items));
            string missingProfile = _weaponsAsset.text.Replace(
                "weapon.bellator.trail_saber", "weapon.bellator.unknown_sword");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(missingProfile, _items));
        }

        [Test]
        public void LoaderRejectsInvalidCombatValuesAndTraitPayloads()
        {
            string zeroDamage = _weaponsAsset.text.Replace("\"damage\": 12", "\"damage\": 0");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(zeroDamage, _items));

            string zeroInterval = _weaponsAsset.text.Replace(
                "\"attackIntervalSeconds\": 0.45", "\"attackIntervalSeconds\": 0");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(zeroInterval, _items));

            string unknownTrait = _weaponsAsset.text.Replace(
                "weapon.trait.balanced", "weapon.trait.unknown");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(unknownTrait, _items));

            string invalidCharge = _weaponsAsset.text.Replace(
                "\"percent\": 30", "\"percent\": -1");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(invalidCharge, _items));

            string duplicateTrait = _weaponsAsset.text.Replace(
                "weapon.trait.greater_knockback", "weapon.trait.greater_range");
            Assert.Throws<FormatException>(() => WeaponCatalogJsonLoader.Load(duplicateTrait, _items));
        }

        private static InventoryOperationOutcome CollectAndEquip(ExpeditionInventory inventory, string itemId)
        {
            var item = new InventoryItemStack(ItemInstanceId.New(), itemId, ItemRarity.Common, 0, 1);
            InventoryOperationOutcome collected = inventory.TryCollectItem(item, TransactionId.New());
            Assert.That(collected.Succeeded, Is.True, itemId + " should be collectable as loot.");
            return inventory.EquipBackpackItem(0, EquipmentSlot.Weapon, TransactionId.New());
        }

        private sealed class ExpectedWeapon
        {
            public ExpectedWeapon(string itemId, string classId, int damage, float interval, params string[] traitIds)
            {
                ItemId = itemId;
                ClassId = classId;
                Damage = damage;
                AttackIntervalSeconds = interval;
                TraitIds = traitIds;
            }

            public string ItemId { get; }
            public string ClassId { get; }
            public int Damage { get; }
            public float AttackIntervalSeconds { get; }
            public string[] TraitIds { get; }
        }
    }
}
