namespace Luckbound.Core.Crafting
{
    /// <summary>Outcome codes for a single atomic crafting operation.</summary>
    public enum CraftingOperationStatus
    {
        Unknown = 0,
        Succeeded = 1,
        RejectedInvalidTransaction = 2,
        RejectedTransactionConflict = 3,
        RejectedUnknownRecipe = 4,
        RejectedInvalidRecipe = 5,
        RejectedInsufficientMaterials = 6,
        RejectedInsufficientGold = 7,
        RejectedBackpackFull = 8,
        RejectedMissingTarget = 9,
        RejectedUnexpectedTarget = 10,
        RejectedWrongTargetKind = 11,
        RejectedIncompatibleClass = 12,
        RejectedInvalidUpgradeLevel = 13,
        RejectedUpgradeLimit = 14
    }
}
