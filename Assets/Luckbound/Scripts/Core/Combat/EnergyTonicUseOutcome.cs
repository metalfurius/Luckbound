namespace Luckbound.Core.Combat
{
    public readonly struct EnergyTonicUseOutcome
    {
        public EnergyTonicUseOutcome(EnergyTonicUseStatus status, int energyRestored, int remainingCharges)
        {
            Status = status;
            EnergyRestored = energyRestored;
            RemainingCharges = remainingCharges;
        }

        public EnergyTonicUseStatus Status { get; }

        public int EnergyRestored { get; }

        public int RemainingCharges { get; }
    }
}
