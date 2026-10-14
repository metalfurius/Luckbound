using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Crafting
{
    /// <summary>Immutable result of crafting, including every stack changed or created by the operation.</summary>
    public sealed class CraftingOutcome
    {
        private readonly ReadOnlyCollection<InventoryItemStack> _resultStacks;

        internal CraftingOutcome(
            TransactionId transactionId,
            string recipeId,
            CraftingOperationStatus status,
            IEnumerable<InventoryItemStack> resultStacks = null)
        {
            if (!Enum.IsDefined(typeof(CraftingOperationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));

            var stacks = new List<InventoryItemStack>();
            if (resultStacks != null)
            {
                foreach (InventoryItemStack stack in resultStacks)
                {
                    if (stack == null) throw new ArgumentException("Crafting results cannot contain null stacks.", nameof(resultStacks));
                    stacks.Add(stack);
                }
            }

            TransactionId = transactionId;
            RecipeId = recipeId ?? string.Empty;
            Status = status;
            _resultStacks = stacks.AsReadOnly();
        }

        public TransactionId TransactionId { get; }
        public string RecipeId { get; }
        public CraftingOperationStatus Status { get; }
        public IReadOnlyList<InventoryItemStack> ResultStacks => _resultStacks;
        public InventoryItemStack ResultStack => _resultStacks.Count == 1 ? _resultStacks[0] : null;
        public bool Succeeded => Status == CraftingOperationStatus.Succeeded;
    }
}
