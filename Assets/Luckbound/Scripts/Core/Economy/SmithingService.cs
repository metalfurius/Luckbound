using System;
using Luckbound.Core.Crafting;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Economy
{
    /// <summary>Restricts the existing atomic recipe engine to consecutive equipment upgrades.</summary>
    public static class SmithingService
    {
        public static SmithingOutcome TryUpgrade(
            ExpeditionInventory inventory,
            RecipeCatalog recipes,
            string recipeId,
            ItemInstanceId targetInstanceId,
            TransactionId transactionId)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (recipes == null) throw new ArgumentNullException(nameof(recipes));

            return inventory.TryUpgrade(recipes, recipeId, targetInstanceId, transactionId);
        }
    }
}
