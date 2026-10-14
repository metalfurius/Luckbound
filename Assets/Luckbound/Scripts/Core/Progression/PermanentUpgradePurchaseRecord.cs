using System;
using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Progression
{
    /// <summary>Durable receipt identity for a successfully committed profile upgrade purchase.</summary>
    public sealed class PermanentUpgradePurchaseRecord
    {
        public PermanentUpgradePurchaseRecord(TransactionId transactionId, PermanentUpgradeKind kind, int cost)
        {
            if (!transactionId.IsValid) throw new ArgumentException("A progression receipt requires a valid transaction ID.", nameof(transactionId));
            if (!Enum.IsDefined(typeof(PermanentUpgradeKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost));
            TransactionId = transactionId;
            Kind = kind;
            Cost = cost;
        }

        public TransactionId TransactionId { get; }
        public PermanentUpgradeKind Kind { get; }
        public int Cost { get; }
    }
}
