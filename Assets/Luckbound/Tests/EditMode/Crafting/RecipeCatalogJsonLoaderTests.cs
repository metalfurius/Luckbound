using System;
using System.Linq;
using Luckbound.Content;
using Luckbound.Core.Crafting;
using Luckbound.Core.Inventory;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.Crafting
{
    public sealed class RecipeCatalogJsonLoaderTests
    {
        private string _recipesJson;
        private ItemCatalog _itemCatalog;

        [SetUp]
        public void SetUp()
        {
            TextAsset recipes = Resources.Load<TextAsset>("Luckbound/Data/Items/Recipes");
            Assert.That(recipes, Is.Not.Null, "Recipes.json must be available as a Resources asset.");
            _recipesJson = recipes.text;
            _itemCatalog = CreateCompleteItemCatalog();
        }

        [Test]
        public void LoadsTwelveUniqueRecipesAndPreservesExistingConsumableIds()
        {
            RecipeCatalog catalog = RecipeCatalogJsonLoader.Load(_recipesJson, _itemCatalog);

            Assert.That(catalog.Count, Is.EqualTo(12));
            Assert.That(catalog.Definitions.Select(recipe => recipe.Id).Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(12));
            foreach (string itemId in new[]
            {
                "consumable.health_potion",
                "consumable.energy_tonic",
                "consumable.antidote",
                "consumable.bomb",
                "consumable.fortune_brew",
                "consumable.torch"
            })
            {
                Assert.That(_itemCatalog.TryGet(itemId, out ItemDefinition item), Is.True, itemId);
                Assert.That(item.Kind, Is.EqualTo(ItemKind.Consumable), itemId);
                Assert.That(item.MaxStack, Is.EqualTo(3), itemId);
            }
        }

        [Test]
        public void RecipesHaveTheSpecifiedMaterialGoldAndResultValues()
        {
            RecipeCatalog catalog = RecipeCatalogJsonLoader.Load(_recipesJson, _itemCatalog);

            AssertConsumable(catalog, "recipe.consumable.health_potion", "consumable.health_potion", 0,
                "UmbraHerb:2", "DarkResin:1");
            AssertConsumable(catalog, "recipe.consumable.energy_tonic", "consumable.energy_tonic", 0,
                "UmbraHerb:2", "ArcaneDust:1");
            AssertConsumable(catalog, "recipe.consumable.antidote", "consumable.antidote", 0,
                "UmbraHerb:1", "ArcaneDust:1");
            AssertConsumable(catalog, "recipe.consumable.bomb", "consumable.bomb", 0,
                "Ore:2", "VitalEmber:1");
            AssertConsumable(catalog, "recipe.consumable.fortune_brew", "consumable.fortune_brew", 0,
                "ArcaneDust:2", "VitalEmber:1");
            AssertConsumable(catalog, "recipe.consumable.torch", "consumable.torch", 0,
                "ShadowWood:1", "DarkResin:1");

            AssertUpgrade(catalog, "recipe.upgrade.weapon.1", ItemKind.Weapon, 1, 10,
                "Ore:2", "ShadowWood:1");
            AssertUpgrade(catalog, "recipe.upgrade.weapon.2", ItemKind.Weapon, 2, 20,
                "Ore:3", "ArcaneDust:1");
            AssertUpgrade(catalog, "recipe.upgrade.weapon.3", ItemKind.Weapon, 3, 40,
                "Ore:3", "VitalEmber:1");
            AssertUpgrade(catalog, "recipe.upgrade.armor.1", ItemKind.Armor, 1, 10,
                "Ore:2", "DarkResin:1");
            AssertUpgrade(catalog, "recipe.upgrade.armor.2", ItemKind.Armor, 2, 20,
                "Ore:3", "ArcaneDust:1");
            AssertUpgrade(catalog, "recipe.upgrade.armor.3", ItemKind.Armor, 3, 40,
                "Ore:3", "VitalEmber:1");
        }

        [Test]
        public void RejectsUnsupportedSchemaAndMissingRecipeArray()
        {
            string wrongVersion = ReplaceOnce(_recipesJson, "\"schemaVersion\": 1", "\"schemaVersion\": 2");
            string missingArray = ReplaceOnce(_recipesJson, "\"recipes\": [", "\"recipes\": null");

            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(wrongVersion, _itemCatalog));
            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(missingArray, _itemCatalog));
            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load("{}", _itemCatalog));
        }

        [Test]
        public void RejectsDuplicateIdsUnknownMaterialsAndBrokenItemReferences()
        {
            string duplicateIds = ReplaceOnce(_recipesJson,
                "\"id\": \"recipe.consumable.energy_tonic\"",
                "\"id\": \"recipe.consumable.health_potion\"");
            duplicateIds = ReplaceOnce(duplicateIds,
                "\"resultItemId\": \"consumable.energy_tonic\"",
                "\"resultItemId\": \"consumable.health_potion\"");
            string unknownMaterial = ReplaceOnce(_recipesJson,
                "\"materialKind\": \"UmbraHerb\"",
                "\"materialKind\": \"NotAMaterial\"");
            string brokenItemReference = ReplaceOnce(_recipesJson,
                "\"id\": \"recipe.consumable.antidote\"",
                "\"id\": \"recipe.consumable.missing\"");
            brokenItemReference = ReplaceOnce(brokenItemReference,
                "\"resultItemId\": \"consumable.antidote\"",
                "\"resultItemId\": \"consumable.missing\"");

            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(duplicateIds, _itemCatalog));
            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(unknownMaterial, _itemCatalog));
            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(brokenItemReference, _itemCatalog));
        }

        [Test]
        public void RejectsNegativeCostsAndUpgradeLevelSkips()
        {
            string negativeMaterialCost = ReplaceOnce(_recipesJson, "\"quantity\": 2", "\"quantity\": -1");
            string negativeGoldCost = ReplaceOnce(_recipesJson, "\"goldCost\": 10", "\"goldCost\": -10");
            string skippedUpgradeLevel = ReplaceOnce(_recipesJson,
                "\"requiredCurrentUpgradeLevel\": 1",
                "\"requiredCurrentUpgradeLevel\": 0");

            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(negativeMaterialCost, _itemCatalog));
            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(negativeGoldCost, _itemCatalog));
            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(skippedUpgradeLevel, _itemCatalog));
        }

        [Test]
        public void RejectsUpgradeTargetsOutsideWeaponsAndArmor()
        {
            string unsupportedTarget = ReplaceOnce(_recipesJson,
                "\"upgradeTargetKind\": \"Weapon\"",
                "\"upgradeTargetKind\": \"Relic\"");

            Assert.Throws<FormatException>(() => RecipeCatalogJsonLoader.Load(unsupportedTarget, _itemCatalog));
        }

        private static void AssertConsumable(
            RecipeCatalog catalog,
            string recipeId,
            string resultItemId,
            int goldCost,
            params string[] expectedCosts)
        {
            RecipeDefinition recipe = catalog.Get(recipeId);
            Assert.That(recipe.ResultKind, Is.EqualTo(CraftingResultKind.Consumable));
            Assert.That(recipe.ResultItemId, Is.EqualTo(resultItemId));
            Assert.That(recipe.ResultQuantity, Is.EqualTo(1));
            Assert.That(recipe.GoldCost, Is.EqualTo(goldCost));
            AssertMaterials(recipe, expectedCosts);
        }

        private static void AssertUpgrade(
            RecipeCatalog catalog,
            string recipeId,
            ItemKind target,
            int resultLevel,
            int goldCost,
            params string[] expectedCosts)
        {
            RecipeDefinition recipe = catalog.Get(recipeId);
            Assert.That(recipe.ResultKind, Is.EqualTo(CraftingResultKind.Upgrade));
            Assert.That(recipe.UpgradeTargetKind, Is.EqualTo(target));
            Assert.That(recipe.RequiredCurrentUpgradeLevel, Is.EqualTo(resultLevel - 1));
            Assert.That(recipe.ResultUpgradeLevel, Is.EqualTo(resultLevel));
            Assert.That(recipe.GoldCost, Is.EqualTo(goldCost));
            AssertMaterials(recipe, expectedCosts);
        }

        private static void AssertMaterials(RecipeDefinition recipe, string[] expectedCosts)
        {
            string[] actualCosts = recipe.MaterialCosts
                .Select(cost => cost.MaterialKind + ":" + cost.Quantity)
                .ToArray();
            CollectionAssert.AreEquivalent(expectedCosts, actualCosts, recipe.Id);
        }

        private static ItemCatalog CreateCompleteItemCatalog()
        {
            TextAsset items = Resources.Load<TextAsset>("Luckbound/Data/Items/Items");
            Assert.That(items, Is.Not.Null, "Items.json must be available as a Resources asset.");
            return ItemCatalogJsonLoader.Load(items.text);
        }

        private static string ReplaceOnce(string input, string search, string replacement)
        {
            int index = input.IndexOf(search, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Test fixture did not contain: " + search);
            return input.Substring(0, index) + replacement + input.Substring(index + search.Length);
        }
    }
}
