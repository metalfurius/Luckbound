using Luckbound.Content;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Gameplay.Inventory
{
    /// <summary>Adapts pickup interactions into idempotent expedition-inventory commands.</summary>
    public sealed class ItemCollector2D : MonoBehaviour
    {
        [SerializeField] private LootPickupDropStager2D _dropStager;

        public ExpeditionInventory Inventory { get; private set; }
        public ItemCatalog Catalog { get; private set; }
        public LootPickupInteractionStatus LastInteractionStatus { get; private set; }
        public InventoryOperationOutcome LastOperationOutcome { get; private set; }
        public bool GrantsDvergarBonus { get; private set; }

        public void Configure(ExpeditionInventory inventory, ItemCatalog catalog, LootPickupDropStager2D dropStager = null,
            bool grantsDvergarBonus = false)
        {
            Inventory = inventory;
            Catalog = catalog;
            _dropStager = dropStager;
            GrantsDvergarBonus = grantsDvergarBonus;
            LastInteractionStatus = LootPickupInteractionStatus.None;
            LastOperationOutcome = null;
        }

        public bool TryCollectWorldMaterial(WorldMaterialPickup2D pickup)
        {
            if (pickup == null || pickup.IsConsumed)
            {
                LastInteractionStatus = LootPickupInteractionStatus.AlreadyConsumed;
                return false;
            }
            if (Inventory == null || Catalog == null)
            {
                LastInteractionStatus = LootPickupInteractionStatus.NotConfigured;
                return false;
            }

            InventoryOperationOutcome outcome = Inventory.TryCollectWorldMaterial(
                pickup.MaterialKind, pickup.Amount, GrantsDvergarBonus, TransactionId.New());
            LastOperationOutcome = outcome;
            if (!outcome.Succeeded)
            {
                LastInteractionStatus = outcome.Status == InventoryOperationStatus.RejectedMaterialCapacity
                    ? LootPickupInteractionStatus.RejectedFull
                    : LootPickupInteractionStatus.Rejected;
                return false;
            }

            pickup.MarkCollected();
            LastInteractionStatus = LootPickupInteractionStatus.Collected;
            return true;
        }

        public bool TryCollect(LootPickup2D pickup)
        {
            if (pickup == null || pickup.IsConsumed)
            {
                LastInteractionStatus = LootPickupInteractionStatus.AlreadyConsumed;
                return false;
            }
            if (Inventory == null || Catalog == null)
            {
                LastInteractionStatus = LootPickupInteractionStatus.NotConfigured;
                return false;
            }

            InventoryOperationOutcome outcome = Inventory.TryCollectItem(pickup.Item, TransactionId.New());
            LastOperationOutcome = outcome;
            if (!outcome.Succeeded)
            {
                LastInteractionStatus = outcome.Status == InventoryOperationStatus.RejectedFull
                    ? LootPickupInteractionStatus.RejectedFull
                    : LootPickupInteractionStatus.Rejected;
                return false;
            }

            pickup.MarkCollected();
            LastInteractionStatus = LootPickupInteractionStatus.Collected;
            return true;
        }

        public bool TryReplaceBackpackSlot(int index, LootPickup2D incomingPickup)
        {
            if (incomingPickup == null || incomingPickup.IsConsumed)
            {
                LastInteractionStatus = LootPickupInteractionStatus.AlreadyConsumed;
                return false;
            }
            if (Inventory == null || Catalog == null)
            {
                LastInteractionStatus = LootPickupInteractionStatus.NotConfigured;
                return false;
            }

            InventoryItemStack displacedItem;
            try
            {
                displacedItem = Inventory.GetBackpackSlot(index);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                LastOperationOutcome = Inventory.TryReplaceBackpackSlot(index, incomingPickup.Item, TransactionId.New());
                LastInteractionStatus = LootPickupInteractionStatus.Rejected;
                return false;
            }

            IStagedLootDrop stagedDrop = null;
            if (displacedItem != null)
            {
                if (_dropStager == null || !_dropStager.TryStageDrop(displacedItem, Catalog, incomingPickup.transform.position, this, out stagedDrop))
                {
                    LastInteractionStatus = LootPickupInteractionStatus.DropStageFailed;
                    LastOperationOutcome = null;
                    return false;
                }
            }

            using (stagedDrop)
            {
                InventoryOperationOutcome outcome = Inventory.TryReplaceBackpackSlot(index, incomingPickup.Item, TransactionId.New());
                LastOperationOutcome = outcome;
                if (!outcome.Succeeded)
                {
                    LastInteractionStatus = LootPickupInteractionStatus.Rejected;
                    return false;
                }

                incomingPickup.MarkCollected();
                stagedDrop?.Commit();
                LastInteractionStatus = LootPickupInteractionStatus.Collected;
                return true;
            }
        }
    }
}
