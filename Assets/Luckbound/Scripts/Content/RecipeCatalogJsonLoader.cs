using System;
using System.Collections.Generic;
using Luckbound.Core.Crafting;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads Recipes.json and resolves its outputs and materials against the authoritative item catalog.</summary>
    public static class RecipeCatalogJsonLoader
    {
        public const int ExpectedRecipeCount = 12;

        public static RecipeCatalog Load(string json, ItemCatalog itemCatalog)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Recipe catalog JSON cannot be empty.", nameof(json));
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));

            RecipeCatalogData data;
            try
            {
                data = JsonUtility.FromJson<RecipeCatalogData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Recipe catalog JSON could not be parsed.", exception);
            }

            if (data == null || data.schemaVersion != 1 || data.recipes == null ||
                data.recipes.Length != ExpectedRecipeCount)
            {
                throw new FormatException("Recipes.json must use schema version 1 and contain exactly twelve recipes.");
            }

            var definitions = new List<RecipeDefinition>(data.recipes.Length);
            for (int index = 0; index < data.recipes.Length; index++)
            {
                RecipeData recipe = data.recipes[index];
                if (recipe == null)
                    throw new FormatException("Recipes.json contains a null recipe at index " + index + ".");
                if (recipe.materialCosts == null)
                    throw new FormatException("Recipe at index " + index + " must define materialCosts.");

                var costs = new List<RecipeMaterialCost>(recipe.materialCosts.Length);
                for (int costIndex = 0; costIndex < recipe.materialCosts.Length; costIndex++)
                {
                    MaterialCostData cost = recipe.materialCosts[costIndex];
                    if (cost == null)
                        throw new FormatException("Recipe " + recipe.id + " contains a null material cost at index " + costIndex + ".");
                    MaterialKind materialKind = ParseEnum<MaterialKind>(cost.materialKind, "materialKind", recipe.id);
                    try
                    {
                        itemCatalog.GetMaterial(materialKind);
                        costs.Add(new RecipeMaterialCost(materialKind, cost.quantity));
                    }
                    catch (ArgumentException exception)
                    {
                        throw new FormatException("Invalid material cost in recipe " + recipe.id + ": " + exception.Message, exception);
                    }
                    catch (KeyNotFoundException exception)
                    {
                        throw new FormatException("Recipe " + recipe.id + " references an unknown material " + materialKind + ".", exception);
                    }
                }

                CraftingResultKind resultKind = ParseEnum<CraftingResultKind>(recipe.resultKind, "resultKind", recipe.id);
                ItemKind? upgradeTargetKind = null;
                if (resultKind == CraftingResultKind.Upgrade)
                {
                    if (!TryParseEnum(recipe.upgradeTargetKind, out ItemKind parsedTarget) ||
                        (parsedTarget != ItemKind.Weapon && parsedTarget != ItemKind.Armor))
                        throw new FormatException("Upgrade recipe " + recipe.id + " must target Weapon or Armor.");
                    upgradeTargetKind = parsedTarget;
                }
                else if (!string.IsNullOrEmpty(recipe.upgradeTargetKind))
                {
                    throw new FormatException("Consumable recipe " + recipe.id + " cannot declare upgradeTargetKind.");
                }

                try
                {
                    var definition = new RecipeDefinition(
                        recipe.id,
                        costs,
                        recipe.goldCost,
                        resultKind,
                        recipe.resultItemId,
                        recipe.resultQuantity,
                        upgradeTargetKind,
                        recipe.requiredCurrentUpgradeLevel,
                        recipe.resultUpgradeLevel);
                    ValidateStableId(definition);
                    if (resultKind == CraftingResultKind.Consumable)
                    {
                        if (!itemCatalog.TryGet(definition.ResultItemId, out ItemDefinition resultItem) ||
                            resultItem.Kind != ItemKind.Consumable)
                            throw new FormatException("Recipe " + definition.Id + " references a missing or non-consumable result " + definition.ResultItemId + ".");
                        if (definition.ResultQuantity > resultItem.MaxStack)
                            throw new FormatException("Recipe " + definition.Id + " creates more than the result item's stack limit.");
                    }

                    definitions.Add(definition);
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid recipe at index " + index + " (" + recipe.id + "): " + exception.Message, exception);
                }
            }

            try
            {
                return new RecipeCatalog(definitions);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid recipe catalog: " + exception.Message, exception);
            }
        }

        private static void ValidateStableId(RecipeDefinition definition)
        {
            string expectedId;
            if (definition.ResultKind == CraftingResultKind.Consumable)
            {
                expectedId = "recipe." + definition.ResultItemId;
            }
            else
            {
                string target = definition.UpgradeTargetKind.Value == ItemKind.Weapon ? "weapon" : "armor";
                expectedId = "recipe.upgrade." + target + "." + definition.ResultUpgradeLevel;
            }

            if (!string.Equals(definition.Id, expectedId, StringComparison.Ordinal))
                throw new FormatException("Recipe ID " + definition.Id + " must be derived from its result as " + expectedId + ".");
        }

        private static TEnum ParseEnum<TEnum>(string value, string fieldName, string recipeId)
            where TEnum : struct
        {
            if (TryParseEnum(value, out TEnum parsed)) return parsed;
            throw new FormatException("Recipe " + recipeId + " has an unknown " + fieldName + " value: " + value + ".");
        }

        private static bool TryParseEnum<TEnum>(string value, out TEnum parsed)
            where TEnum : struct
        {
            return Enum.TryParse(value, ignoreCase: false, out parsed) && Enum.IsDefined(typeof(TEnum), parsed);
        }

        [Serializable]
        private sealed class RecipeCatalogData
        {
            public int schemaVersion;
            public RecipeData[] recipes;
        }

        [Serializable]
        private sealed class RecipeData
        {
            public string id;
            public MaterialCostData[] materialCosts;
            public int goldCost;
            public string resultKind;
            public string resultItemId;
            public int resultQuantity;
            public string upgradeTargetKind;
            public int requiredCurrentUpgradeLevel;
            public int resultUpgradeLevel;
        }

        [Serializable]
        private sealed class MaterialCostData
        {
            public string materialKind;
            public int quantity;
        }
    }
}
