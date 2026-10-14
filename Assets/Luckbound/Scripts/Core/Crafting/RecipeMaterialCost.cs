using System;
using Luckbound.Core.Inventory;

namespace Luckbound.Core.Crafting
{
    /// <summary>An immutable quantity of one canonical material required by a recipe.</summary>
    public sealed class RecipeMaterialCost
    {
        public RecipeMaterialCost(MaterialKind materialKind, int quantity)
        {
            if (!Enum.IsDefined(typeof(MaterialKind), materialKind))
                throw new ArgumentOutOfRangeException(nameof(materialKind));
            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Material costs must be positive.");

            MaterialKind = materialKind;
            Quantity = quantity;
        }

        public MaterialKind MaterialKind { get; }
        public int Quantity { get; }
    }
}
