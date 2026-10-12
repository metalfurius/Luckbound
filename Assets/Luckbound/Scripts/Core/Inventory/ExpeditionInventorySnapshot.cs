using System;

namespace Luckbound.Core.Inventory
{
    /// <summary>Serializable, identity-preserving snapshot of every inventory slot and accepted transaction.</summary>
    [Serializable]
    public sealed class ExpeditionInventorySnapshot
    {
        public string classId;
        public int gold;
        public InventoryStackSnapshot weapon;
        public InventoryStackSnapshot armor;
        public InventoryStackSnapshot relicOne;
        public InventoryStackSnapshot relicTwo;
        public InventoryStackSnapshot[] backpackSlots = Array.Empty<InventoryStackSnapshot>();
        public string quickConsumableInstanceId;
        public MaterialCountSnapshot[] materials = Array.Empty<MaterialCountSnapshot>();
        public InventoryTransactionSnapshot[] transactions = Array.Empty<InventoryTransactionSnapshot>();
    }

    [Serializable]
    public sealed class InventoryStackSnapshot
    {
        public string instanceId;
        public string itemId;
        public int rarity;
        public int upgradeLevel;
        public int quantity;
    }

    [Serializable]
    public sealed class MaterialCountSnapshot
    {
        public int kind;
        public int count;
    }

    [Serializable]
    public sealed class InventoryTransactionSnapshot
    {
        public string transactionId;
        public string signature;
        public int status;
        public string itemId;
        public InventoryStackSnapshot displacedItem;
    }
}
