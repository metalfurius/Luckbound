using Luckbound.Core.Identifiers;

namespace Luckbound.Core.Progression
{
    public sealed class PermanentUpgradePurchaseResult
    {
        internal PermanentUpgradePurchaseResult(
            TransactionId transactionId,
            PermanentUpgradeKind kind,
            PermanentUpgradePurchaseStatus status,
            int cost,
            PermanentUpgradeSnapshot snapshot)
        {
            TransactionId = transactionId;
            Kind = kind;
            Status = status;
            Cost = cost;
            Snapshot = snapshot;
        }

        public TransactionId TransactionId { get; }
        public PermanentUpgradeKind Kind { get; }
        public PermanentUpgradePurchaseStatus Status { get; }
        public int Cost { get; }
        public PermanentUpgradeSnapshot Snapshot { get; }
        public bool Succeeded => Status == PermanentUpgradePurchaseStatus.Purchased ||
            Status == PermanentUpgradePurchaseStatus.AlreadyProcessed;
        public bool ChangesProfile => Status == PermanentUpgradePurchaseStatus.Purchased;
    }
}
