namespace Luckbound.Core.Inventory
{
    public enum InventoryOperationStatus
    {
        Unknown = 0,
        Succeeded = 1,
        RejectedInvalidTransaction = 2,
        RejectedTransactionConflict = 3,
        RejectedUnknownItem = 4,
        RejectedInvalidItem = 5,
        RejectedFull = 6,
        RejectedInvalidSlot = 7,
        RejectedIncompatibleClass = 8,
        RejectedWrongEquipmentSlot = 9,
        RejectedDuplicateInstance = 10,
        RejectedDuplicateRelic = 11,
        RejectedInvalidQuickConsumable = 12,
        RejectedInvalidMaterial = 13,
        RejectedMaterialCapacity = 14,
        RejectedInvalidGold = 15
    }
}
