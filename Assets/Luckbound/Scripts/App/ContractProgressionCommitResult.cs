using Luckbound.Core.Progression.Contracts;

namespace Luckbound.App
{
    public enum ContractProgressionCommitOperation
    {
        None = 0,
        Accept = 1,
        RecordProgress = 2,
        Claim = 3
    }

    /// <summary>Reports a contract transition and whether its profile payload was durably committed.</summary>
    public sealed class ContractProgressionCommitResult
    {
        internal ContractProgressionCommitResult(
            ContractProgressionCommitOperation operation,
            ContractProgressionSnapshot snapshot,
            ContractAcceptStatus? acceptStatus,
            ContractProgressStatus? progressStatus,
            ContractClaimStatus? claimStatus,
            ContractReward reward,
            bool persisted,
            bool pending,
            string message)
        {
            Operation = operation;
            Snapshot = snapshot;
            AcceptStatus = acceptStatus;
            ProgressStatus = progressStatus;
            ClaimStatus = claimStatus;
            Reward = reward;
            Persisted = persisted;
            Pending = pending;
            Message = message ?? string.Empty;
        }

        public ContractProgressionCommitOperation Operation { get; }
        /// <summary>The latest confirmed profile projection; an uncommitted candidate is never exposed here.</summary>
        public ContractProgressionSnapshot Snapshot { get; }
        public ContractAcceptStatus? AcceptStatus { get; }
        public ContractProgressStatus? ProgressStatus { get; }
        public ContractClaimStatus? ClaimStatus { get; }
        /// <summary>Non-null only after the claim has been persisted.</summary>
        public ContractReward Reward { get; }
        public bool Persisted { get; }
        public bool Pending { get; }
        public bool Succeeded => Persisted;
        public string Message { get; }
    }
}
