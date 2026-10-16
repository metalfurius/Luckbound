using Luckbound.Core.Crafting;

namespace Luckbound.Core.Economy
{
    /// <summary>Smithing facade result, retaining the detailed crafting validation status.</summary>
    public sealed class SmithingOutcome
    {
        internal SmithingOutcome(CraftingOutcome craftingOutcome, bool rejectedNonUpgradeRecipe = false)
        {
            CraftingOutcome = craftingOutcome ?? throw new System.ArgumentNullException(nameof(craftingOutcome));
            RejectedNonUpgradeRecipe = rejectedNonUpgradeRecipe;
        }

        public CraftingOutcome CraftingOutcome { get; }
        public bool RejectedNonUpgradeRecipe { get; }
        public bool Succeeded => CraftingOutcome != null && CraftingOutcome.Succeeded;
    }
}
