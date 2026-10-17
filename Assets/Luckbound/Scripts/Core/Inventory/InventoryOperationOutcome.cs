using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Inventory
{
    /// <summary>Stable result returned by an inventory transaction.</summary>
    public sealed class InventoryOperationOutcome
    {
        public InventoryOperationOutcome(
            TransactionId transactionId,
            InventoryOperationStatus status,
            InventoryItemStack displacedItem = null,
            string itemId = null,
            bool wasReplay = false)
        {
            TransactionId = transactionId;
            Status = status;
            DisplacedItem = displacedItem;
            ItemId = itemId ?? string.Empty;
            WasReplay = wasReplay;
        }

        public TransactionId TransactionId { get; }
        public InventoryOperationStatus Status { get; }
        public InventoryItemStack DisplacedItem { get; }
        public string ItemId { get; }
        public bool Succeeded => Status == InventoryOperationStatus.Succeeded;
        public bool WasReplay { get; }
    }
}
