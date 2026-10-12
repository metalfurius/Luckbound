using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Inventory
{
    /// <summary>An immutable item instance or consumable pile held by a run.</summary>
    public sealed class InventoryItemStack
    {
        public InventoryItemStack(ItemInstanceId instanceId, string itemId, ItemRarity rarity, int upgradeLevel, int quantity)
        {
            if (!instanceId.IsValid) throw new ArgumentException("Item stacks require a valid item instance ID.", nameof(instanceId));
            if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("Item stacks require a content ID.", nameof(itemId));
            if (!Enum.IsDefined(typeof(ItemRarity), rarity)) throw new ArgumentOutOfRangeException(nameof(rarity));
            if (upgradeLevel < 0 || upgradeLevel > 3) throw new ArgumentOutOfRangeException(nameof(upgradeLevel));
            if (quantity < 1) throw new ArgumentOutOfRangeException(nameof(quantity));

            InstanceId = instanceId;
            ItemId = itemId;
            Rarity = rarity;
            UpgradeLevel = upgradeLevel;
            Quantity = quantity;
        }

        public ItemInstanceId InstanceId { get; }
        public string ItemId { get; }
        public ItemRarity Rarity { get; }
        public int UpgradeLevel { get; }
        public int Quantity { get; }

        internal InventoryItemStack WithQuantity(int quantity)
        {
            return new InventoryItemStack(InstanceId, ItemId, Rarity, UpgradeLevel, quantity);
        }
    }
}
