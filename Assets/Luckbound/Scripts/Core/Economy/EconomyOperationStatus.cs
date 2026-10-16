namespace Luckbound.Core.Economy
{
    /// <summary>Outcome codes for shop and paid rest-stop transactions.</summary>
    public enum EconomyOperationStatus
    {
        Unknown = 0,
        Succeeded = 1,
        RejectedInvalidTransaction = 2,
        RejectedTransactionConflict = 3,
        RejectedStockNotInitialized = 4,
        RejectedWrongRun = 5,
        RejectedUnknownOffer = 6,
        RejectedUnknownItem = 7,
        RejectedInvalidQuantity = 8,
        RejectedInvalidDiscount = 9,
        RejectedOutOfStock = 10,
        RejectedInsufficientGold = 11,
        RejectedBackpackFull = 12,
        RejectedMaterialCapacity = 13,
        RejectedIncompatibleClass = 14,
        RejectedNotOwned = 15,
        RejectedInsufficientQuantity = 16,
        RejectedGoldCapacity = 17,
        RejectedInvalidRoom = 18,
        RejectedInvalidHealth = 19,
        RejectedDead = 20,
        RejectedRestNotEntered = 21,
        RejectedAlreadyUsed = 22,
        RejectedFullHealth = 23
    }
}
