using Luckbound.Core.Progression;

namespace Luckbound.App
{
    public sealed class ProgressionPurchaseCommitResult
    {
        internal ProgressionPurchaseCommitResult(
            PermanentUpgradePurchaseResult purchase,
            bool persisted,
            bool pending,
            string message)
        {
            Purchase = purchase;
            Persisted = persisted;
            Pending = pending;
            Message = message ?? string.Empty;
        }

        public PermanentUpgradePurchaseResult Purchase { get; }
        public bool Persisted { get; }
        public bool Pending { get; }
        public string Message { get; }
    }
}
