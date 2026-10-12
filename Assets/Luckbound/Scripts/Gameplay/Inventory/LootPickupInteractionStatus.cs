namespace Luckbound.Gameplay.Inventory
{
    public enum LootPickupInteractionStatus
    {
        None = 0,
        Collected = 1,
        Rejected = 2,
        RejectedFull = 3,
        DropStageFailed = 4,
        NotConfigured = 5,
        AlreadyConsumed = 6
    }
}
