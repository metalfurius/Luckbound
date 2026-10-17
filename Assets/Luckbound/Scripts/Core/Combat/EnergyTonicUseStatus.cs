namespace Luckbound.Core.Combat
{
    public enum EnergyTonicUseStatus
    {
        Idle = 0,
        Preparing = 1,
        Energized = 2,
        Cancelled = 3,
        RejectedAtFullEnergy = 4,
        RejectedWithoutCharges = 5
    }
}
