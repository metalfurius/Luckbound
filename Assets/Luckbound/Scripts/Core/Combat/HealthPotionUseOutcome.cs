namespace Luckbound.Core.Combat
{
    public readonly struct HealthPotionUseOutcome
    {
        public HealthPotionUseOutcome(HealthPotionUseStatus status, int healthRestored, int remainingCharges)
        {
            Status = status;
            HealthRestored = healthRestored;
            RemainingCharges = remainingCharges;
        }

        public HealthPotionUseStatus Status { get; }

        public int HealthRestored { get; }

        public int RemainingCharges { get; }
    }
}
