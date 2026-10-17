namespace Luckbound.Gameplay.Inventory
{
    public enum PlayerConsumableUseStatus
    {
        Idle = 0,
        Preparing = 1,
        Consumed = 2,
        Cancelled = 3,
        RejectedAtFullResource = 4,
        RejectedWithoutQuickConsumable = 5,
        RejectedUnsupportedSubsystem = 6,
        RejectedInventoryChanged = 7,
        RejectedWhileStunned = 8,
        Pending = 9,
        RejectedDurableMutation = 10
    }

    /// <summary>Deterministic result of one player quick-consumable input.</summary>
    public readonly struct PlayerConsumableUseOutcome
    {
        public PlayerConsumableUseOutcome(
            PlayerConsumableUseStatus status,
            string itemId = null,
            int healthRestored = 0,
            int energyRestored = 0)
        {
            Status = status;
            ItemId = itemId ?? string.Empty;
            HealthRestored = healthRestored;
            EnergyRestored = energyRestored;
        }

        public PlayerConsumableUseStatus Status { get; }

        public string ItemId { get; }

        public int HealthRestored { get; }

        public int EnergyRestored { get; }

        public bool WasConsumed => Status == PlayerConsumableUseStatus.Consumed;
    }
}
