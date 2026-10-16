using System;
using Luckbound.Core.Economy;

namespace Luckbound.Core.Inventory
{
    /// <summary>Serializable, identity-preserving snapshot of every inventory slot and accepted transaction.</summary>
    [Serializable]
    public sealed class ExpeditionInventorySnapshot
    {
        public int snapshotVersion;
        public string classId;
        public int gold;
        public InventoryStackSnapshot weapon;
        public InventoryStackSnapshot armor;
        public InventoryStackSnapshot relicOne;
        public InventoryStackSnapshot relicTwo;
        public InventoryStackSnapshot[] backpackSlots = Array.Empty<InventoryStackSnapshot>();
        public string quickConsumableInstanceId;
        public MaterialCountSnapshot[] materials = Array.Empty<MaterialCountSnapshot>();
        public WorldMaterialRemainderSnapshot[] worldMaterialRemainders = Array.Empty<WorldMaterialRemainderSnapshot>();
        public string merchantRunId = string.Empty;
        public MerchantStockSnapshot[] merchantStocks = Array.Empty<MerchantStockSnapshot>();
        public RestStopUseSnapshot[] restStops = Array.Empty<RestStopUseSnapshot>();
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
    public sealed class WorldMaterialRemainderSnapshot
    {
        public int kind;
        public int remainder;
    }

    [Serializable]
    public sealed class InventoryTransactionSnapshot
    {
        public string transactionId;
        public string signature;
        public int status;
        public string itemId;
        public InventoryStackSnapshot displacedItem;
        public int craftingStatus;
        public string craftingRecipeId;
        public InventoryStackSnapshot[] craftingResultStacks = Array.Empty<InventoryStackSnapshot>();
        public bool smithingRejectedNonUpgradeRecipe;
        public int economyStatus;
        public string economyItemInstanceId;
        public int economyQuantity;
        public int economyGoldDelta;
        public int economyResultingHealth;
        public int economyHealthRestored;
        public string economyRoomInstanceId;
    }
}
