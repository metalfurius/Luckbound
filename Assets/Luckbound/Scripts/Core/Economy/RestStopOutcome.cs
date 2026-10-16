using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Economy
{
    /// <summary>Result of entering a rest stop and claiming its one-time recovery.</summary>
    public sealed class RestStopOutcome
    {
        internal RestStopOutcome(
            RoomInstanceId roomInstanceId,
            EconomyOperationStatus status,
            int healthAfter,
            int healthRestored)
        {
            RoomInstanceId = roomInstanceId;
            Status = status;
            HealthAfter = healthAfter;
            HealthRestored = healthRestored;
        }

        public RoomInstanceId RoomInstanceId { get; }
        public EconomyOperationStatus Status { get; }
        public int HealthAfter { get; }
        public int HealthRestored { get; }
        public bool Succeeded => Status == EconomyOperationStatus.Succeeded;
    }
}
