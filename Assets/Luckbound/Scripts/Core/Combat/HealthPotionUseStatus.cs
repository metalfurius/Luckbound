namespace Luckbound.Core.Combat
{
    public enum HealthPotionUseStatus
    {
        Idle = 0,
        Preparing = 1,
        Healed = 2,
        Cancelled = 3,
        RejectedAtFullHealth = 4,
        RejectedWithoutCharges = 5
    }
}
