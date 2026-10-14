namespace Luckbound.Core.Progression
{
    public enum PermanentUpgradeKind
    {
        MaximumHealth = 0,
        MaximumEnergy = 1,
        AdditionalHealthPotion = 2
    }

    public enum PermanentUpgradePurchaseStatus
    {
        Purchased = 0,
        AlreadyProcessed = 1,
        RejectedNotInTavern = 2,
        RejectedInsufficientRenown = 3,
        RejectedMaximumLevel = 4,
        RejectedInvalidTransaction = 5,
        RejectedTransactionConflict = 6
    }
}
