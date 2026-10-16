using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Economy
{
    /// <summary>Stable result of a persisted merchant or paid rest-stop transaction.</summary>
    public sealed class EconomyTransactionOutcome
    {
        internal EconomyTransactionOutcome(
            TransactionId transactionId,
            EconomyOperationStatus status,
            string itemId = null,
            ItemInstanceId itemInstanceId = default,
            int quantity = 0,
            int goldDelta = 0,
            int resultingHealth = -1,
            int healthRestored = 0,
            string roomInstanceId = null)
        {
            if (!Enum.IsDefined(typeof(EconomyOperationStatus), status))
                throw new ArgumentOutOfRangeException(nameof(status));
            if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (healthRestored < 0) throw new ArgumentOutOfRangeException(nameof(healthRestored));

            TransactionId = transactionId;
            Status = status;
            ItemId = itemId ?? string.Empty;
            ItemInstanceId = itemInstanceId;
            Quantity = quantity;
            GoldDelta = goldDelta;
            ResultingHealth = resultingHealth;
            HealthRestored = healthRestored;
            RoomInstanceId = roomInstanceId ?? string.Empty;
        }

        public TransactionId TransactionId { get; }
        public EconomyOperationStatus Status { get; }
        public string ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        public bool HasItemInstanceId => ItemInstanceId.IsValid;
        public int Quantity { get; }
        public int GoldDelta { get; }
        public int ResultingHealth { get; }
        public int HealthRestored { get; }
        public string RoomInstanceId { get; }
        public bool Succeeded => Status == EconomyOperationStatus.Succeeded;
    }
}
