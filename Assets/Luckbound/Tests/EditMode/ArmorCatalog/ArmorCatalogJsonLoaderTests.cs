using System;
using System.Collections.Generic;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.ArmorContent
{
    public sealed class ArmorCatalogJsonLoaderTests
    {
        private static readonly ExpectedArmor[] ExpectedArmors =
        {
            new ExpectedArmor(ArmorCatalog.TravelerRobeItemId, 1, 5, 0, 0, 0),
            new ExpectedArmor(ArmorCatalog.LeatherVestItemId, 2, 0, 3, 0, 0),
            new ExpectedArmor(ArmorCatalog.GuildCuirassItemId, 3, 0, -3, 0, 0),
            new ExpectedArmor(ArmorCatalog.RunicMantleItemId, 2, 0, 0, 1, 0),
            new ExpectedArmor(ArmorCatalog.IronCuirassItemId, 5, 0, -5, 0, 0),
            new ExpectedArmor(ArmorCatalog.BarkArmorItemId, 3, 0, 0, 0, 15)
        };

        [Test]
        public void ResourceLoadsExactlySixKnownArmorProfilesWithThePlanValues()
        {
            ArmorCatalog catalog = LoadCanonicalCatalog();

            Assert.That(catalog.Count, Is.EqualTo(6));
            CollectionAssert.AreEqual(GetExpectedIds(), GetProfileIds(catalog));
            foreach (ExpectedArmor expected in ExpectedArmors)
            {
                ArmorProfile profile = catalog.Get(expected.ItemId);
                Assert.That(profile.BaseDefense, Is.EqualTo(expected.BaseDefense), expected.ItemId);
                Assert.That(profile.MaximumEnergyBonus, Is.EqualTo(expected.MaximumEnergyBonus), expected.ItemId);
                Assert.That(profile.MovementSpeedPercentModifier, Is.EqualTo(expected.MovementSpeedPercentModifier), expected.ItemId);
                Assert.That(profile.EnergyRegenerationPerSecondBonus,
                    Is.EqualTo(expected.EnergyRegenerationPerSecondBonus), expected.ItemId);
                Assert.That(profile.HarmfulStatusDurationReductionPercent,
                    Is.EqualTo(expected.HarmfulStatusDurationReductionPercent), expected.ItemId);
            }
        }

        [Test]
        public void LoaderRejectsEmptyMalformedWrongVersionAndStructurallyIncompleteData()
        {
            ItemCatalog items = LoadCanonicalItemCatalog();
            string json = LoadArmorsJson();

            Assert.Throws<ArgumentException>(() => ArmorCatalogJsonLoader.Load(" ", items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load("{ broken", items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2"), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"maximumEnergyBonus\": 5,", ""), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"baseDefense\": 1,", "\"baseDefense\": 1, \"baseDefense\": 1,"), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"baseDefense\": 1,", "\"baseDefense\": 1.5,"), items));
        }

        [Test]
        public void LoaderRejectsDuplicateUnknownAndMissingArmorReferences()
        {
            string json = LoadArmorsJson();
            ItemCatalog items = LoadCanonicalItemCatalog();

            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace(ArmorCatalog.BarkArmorItemId, ArmorCatalog.RunicMantleItemId), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace(ArmorCatalog.BarkArmorItemId, "armor.unknown"), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json, CreateItemCatalogMissingArmor(ArmorCatalog.BarkArmorItemId)));
        }

        [Test]
        public void LoaderRejectsInvalidArmorValuesIncludingAZeroOrNegativeMovementMultiplier()
        {
            string json = LoadArmorsJson();
            ItemCatalog items = LoadCanonicalItemCatalog();

            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"baseDefense\": 1,", "\"baseDefense\": 0,"), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"baseDefense\": 1,", "\"baseDefense\": 2147483647,"), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"movementSpeedPercentModifier\": -5,", "\"movementSpeedPercentModifier\": -100,"), items));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json.Replace("\"harmfulStatusDurationReductionPercent\": 15", "\"harmfulStatusDurationReductionPercent\": 101"), items));
        }

        [Test]
        public void LoaderRequiresAnItemCatalogAndExactlyTheSixArmorDefinitions()
        {
            string json = LoadArmorsJson();
            Assert.Throws<ArgumentNullException>(() => ArmorCatalogJsonLoader.Load(json, null));
            Assert.Throws<FormatException>(() => ArmorCatalogJsonLoader.Load(
                json, CreateItemCatalogMissingArmor(ArmorCatalog.IronCuirassItemId)));
        }

        private static ArmorCatalog LoadCanonicalCatalog()
        {
            return ArmorCatalogJsonLoader.Load(LoadArmorsJson(), LoadCanonicalItemCatalog());
        }

        private static ItemCatalog LoadCanonicalItemCatalog()
        {
            TextAsset items = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(items, Is.Not.Null, "The authoritative Items.json resource should be available to EditMode tests.");
            return ItemCatalogJsonLoader.Load(items.text);
        }

        private static string LoadArmorsJson()
        {
            TextAsset armors = Resources.Load<TextAsset>("Luckbound/Data/Items/Armors");
            Assert.That(armors, Is.Not.Null, "The authoritative Armors.json resource should be available to EditMode tests.");
            return armors.text;
        }

        private static string[] GetProfileIds(ArmorCatalog catalog)
        {
            var ids = new string[catalog.Profiles.Count];
            for (int index = 0; index < catalog.Profiles.Count; index++) ids[index] = catalog.Profiles[index].ItemId;
            return ids;
        }

        private static string[] GetExpectedIds()
        {
            var ids = new string[ExpectedArmors.Length];
            for (int index = 0; index < ExpectedArmors.Length; index++) ids[index] = ExpectedArmors[index].ItemId;
            return ids;
        }

        private static ItemCatalog CreateItemCatalogMissingArmor(string missingArmorId)
        {
            var definitions = new List<ItemDefinition>();
            foreach (ExpectedArmor expected in ExpectedArmors)
            {
                if (expected.ItemId == missingArmorId) continue;
                definitions.Add(new ItemDefinition(expected.ItemId, "test.armor.name", "test.armor.description", ItemKind.Armor, 1));
            }

            foreach (MaterialKind materialKind in Enum.GetValues(typeof(MaterialKind)))
            {
                string id = "material.test_" + materialKind.ToString().ToLowerInvariant();
                definitions.Add(new ItemDefinition(id, "test.material.name", "test.material.description",
                    ItemKind.Material, 99, materialKind: materialKind));
            }

            return new ItemCatalog(definitions);
        }

        private sealed class ExpectedArmor
        {
            public ExpectedArmor(
                string itemId,
                int baseDefense,
                int maximumEnergyBonus,
                int movementSpeedPercentModifier,
                int energyRegenerationPerSecondBonus,
                int harmfulStatusDurationReductionPercent)
            {
                ItemId = itemId;
                BaseDefense = baseDefense;
                MaximumEnergyBonus = maximumEnergyBonus;
                MovementSpeedPercentModifier = movementSpeedPercentModifier;
                EnergyRegenerationPerSecondBonus = energyRegenerationPerSecondBonus;
                HarmfulStatusDurationReductionPercent = harmfulStatusDurationReductionPercent;
            }

            public string ItemId { get; }
            public int BaseDefense { get; }
            public int MaximumEnergyBonus { get; }
            public int MovementSpeedPercentModifier { get; }
            public int EnergyRegenerationPerSecondBonus { get; }
            public int HarmfulStatusDurationReductionPercent { get; }
        }
    }
}
