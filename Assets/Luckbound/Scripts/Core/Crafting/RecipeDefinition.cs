using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Crafting
{
    /// <summary>An immutable crafting recipe. Content references are resolved by the Content loader.</summary>
    public sealed class RecipeDefinition
    {
        private readonly ReadOnlyCollection<RecipeMaterialCost> _materialCosts;

        public RecipeDefinition(
            string id,
            IEnumerable<RecipeMaterialCost> materialCosts,
            int goldCost,
            CraftingResultKind resultKind,
            string resultItemId = null,
            int resultQuantity = 0,
            ItemKind? upgradeTargetKind = null,
            int requiredCurrentUpgradeLevel = 0,
            int resultUpgradeLevel = 0)
        {
            ValidateId(id);
            if (materialCosts == null) throw new ArgumentNullException(nameof(materialCosts));
            if (goldCost < 0) throw new ArgumentOutOfRangeException(nameof(goldCost));
            if (!Enum.IsDefined(typeof(CraftingResultKind), resultKind))
                throw new ArgumentOutOfRangeException(nameof(resultKind));

            var costs = new List<RecipeMaterialCost>();
            var materialKinds = new HashSet<MaterialKind>();
            foreach (RecipeMaterialCost cost in materialCosts)
            {
                if (cost == null) throw new ArgumentException("Material costs cannot contain null entries.", nameof(materialCosts));
                if (!materialKinds.Add(cost.MaterialKind))
                    throw new ArgumentException("A recipe can list each material only once.", nameof(materialCosts));
                costs.Add(cost);
            }
            if (costs.Count == 0) throw new ArgumentException("A recipe must require at least one material.", nameof(materialCosts));

            if (resultKind == CraftingResultKind.Consumable)
            {
                if (string.IsNullOrWhiteSpace(resultItemId))
                    throw new ArgumentException("Consumable recipes require a result item ID.", nameof(resultItemId));
                if (resultQuantity <= 0)
                    throw new ArgumentOutOfRangeException(nameof(resultQuantity), "Consumable results must have a positive quantity.");
                if (upgradeTargetKind.HasValue || requiredCurrentUpgradeLevel != 0 || resultUpgradeLevel != 0)
                    throw new ArgumentException("Consumable recipes cannot declare an upgrade target or level.");
            }
            else
            {
                if (!string.IsNullOrEmpty(resultItemId) || resultQuantity != 0)
                    throw new ArgumentException("Upgrade recipes cannot declare a consumable result.");
                if (upgradeTargetKind != ItemKind.Weapon && upgradeTargetKind != ItemKind.Armor)
                    throw new ArgumentException("Upgrade recipes must target weapons or armor.", nameof(upgradeTargetKind));
                if (resultUpgradeLevel < 1 || resultUpgradeLevel > 3)
                    throw new ArgumentOutOfRangeException(nameof(resultUpgradeLevel), "Equipment upgrades must finish at level +1, +2, or +3.");
                if (requiredCurrentUpgradeLevel != resultUpgradeLevel - 1)
                    throw new ArgumentException("Equipment upgrades must require the immediately preceding level.", nameof(requiredCurrentUpgradeLevel));
            }

            Id = id;
            _materialCosts = costs.AsReadOnly();
            GoldCost = goldCost;
            ResultKind = resultKind;
            ResultItemId = resultItemId;
            ResultQuantity = resultQuantity;
            UpgradeTargetKind = upgradeTargetKind;
            RequiredCurrentUpgradeLevel = requiredCurrentUpgradeLevel;
            ResultUpgradeLevel = resultUpgradeLevel;
        }

        public string Id { get; }
        public IReadOnlyList<RecipeMaterialCost> MaterialCosts => _materialCosts;
        public int GoldCost { get; }
        public CraftingResultKind ResultKind { get; }
        public string ResultItemId { get; }
        public int ResultQuantity { get; }
        public ItemKind? UpgradeTargetKind { get; }
        public int RequiredCurrentUpgradeLevel { get; }
        public int ResultUpgradeLevel { get; }

        private static void ValidateId(string id)
        {
            const string prefix = "recipe.";
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith(prefix, StringComparison.Ordinal) || id.Length <= prefix.Length)
                throw new ArgumentException("Recipe IDs must use the stable recipe. prefix.", nameof(id));

            for (int index = 0; index < id.Length; index++)
            {
                char character = id[index];
                if (!(character >= 'a' && character <= 'z') &&
                    !(character >= '0' && character <= '9') &&
                    character != '.' && character != '_')
                    throw new ArgumentException("Recipe IDs may contain only lowercase letters, digits, dots, and underscores.", nameof(id));
            }
        }
    }
}
