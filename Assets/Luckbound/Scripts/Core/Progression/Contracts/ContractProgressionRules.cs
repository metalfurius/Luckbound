using System;
using System.Linq;

namespace Luckbound.Core.Progression.Contracts
{
    public enum ContractAcceptStatus
    {
        Accepted = 1,
        AlreadyAccepted = 2,
        RejectedUnknownContract = 3,
        RejectedActiveLimit = 4
    }

    public enum ContractProgressStatus
    {
        Progressed = 1,
        NoProgressChange = 2
    }

    public enum ContractClaimStatus
    {
        Claimed = 1,
        AlreadyClaimed = 2,
        RejectedUnknownContract = 3,
        RejectedNotAccepted = 4,
        RejectedIncomplete = 5,
        RejectedRenownOverflow = 6
    }

    public sealed class ContractAcceptOutcome
    {
        internal ContractAcceptOutcome(ContractAcceptStatus status, ContractProgressionSnapshot snapshot)
        {
            Status = status;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public ContractAcceptStatus Status { get; }
        public ContractProgressionSnapshot Snapshot { get; }
        public bool Succeeded => Status == ContractAcceptStatus.Accepted || Status == ContractAcceptStatus.AlreadyAccepted;
        public bool ChangesProfile => Status == ContractAcceptStatus.Accepted;
    }

    public sealed class ContractProgressOutcome
    {
        internal ContractProgressOutcome(ContractProgressStatus status, ContractProgressionSnapshot snapshot)
        {
            Status = status;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public ContractProgressStatus Status { get; }
        public ContractProgressionSnapshot Snapshot { get; }
        public bool ChangesProfile => Status == ContractProgressStatus.Progressed;
    }

    public sealed class ContractClaimOutcome
    {
        internal ContractClaimOutcome(ContractClaimStatus status, ContractProgressionSnapshot snapshot, ContractReward reward)
        {
            Status = status;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Reward = reward;
        }

        public ContractClaimStatus Status { get; }
        public ContractProgressionSnapshot Snapshot { get; }
        public ContractReward Reward { get; }
        public bool Succeeded => Status == ContractClaimStatus.Claimed || Status == ContractClaimStatus.AlreadyClaimed;
        public bool ChangesProfile => Status == ContractClaimStatus.Claimed;
    }

    /// <summary>Pure transitions for E10 profile contracts; callers persist returned snapshots atomically.</summary>
    public static class ContractProgressionRules
    {
        public const int MaximumActiveContracts = 3;

        public static ContractAcceptOutcome Accept(ContractProgressionSnapshot current, string contractId)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!ContractCatalog.TryGet(contractId, out _))
                return AcceptResult(ContractAcceptStatus.RejectedUnknownContract, current);
            if (current.IsAccepted(contractId))
                return AcceptResult(ContractAcceptStatus.AlreadyAccepted, current);
            if (current.ActiveContractIds.Count >= MaximumActiveContracts)
                return AcceptResult(ContractAcceptStatus.RejectedActiveLimit, current);

            return AcceptResult(ContractAcceptStatus.Accepted, current.WithAcceptedContract(contractId));
        }

        /// <summary>
        /// Records one validated profile event, whether or not its contract is active yet. This keeps
        /// lifetime profile counters current so an accepted contract can recognize earlier milestones.
        /// </summary>
        public static ContractProgressOutcome RecordProgress(
            ContractProgressionSnapshot current, ContractProgressEvent progressEvent)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!Enum.IsDefined(typeof(ContractProgressEvent), progressEvent))
                throw new ArgumentOutOfRangeException(nameof(progressEvent));

            ContractGoalType goalType = GetGoalType(progressEvent);
            ContractProgressionSnapshot next = current;
            foreach (ContractDefinition definition in ContractCatalog.ImplementedContracts
                .Where(candidate => candidate.GoalType == goalType))
            {
                int progress = next.GetProgress(definition.Id);
                if (progress >= definition.RequiredProgress) continue;
                next = next.WithProgress(definition.Id, progress + 1);
            }

            return next == current
                ? ProgressResult(ContractProgressStatus.NoProgressChange, current)
                : ProgressResult(ContractProgressStatus.Progressed, next);
        }

        public static ContractClaimOutcome Claim(ContractProgressionSnapshot current, string contractId)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            if (!ContractCatalog.TryGet(contractId, out ContractDefinition definition))
                return ClaimResult(ContractClaimStatus.RejectedUnknownContract, current);
            if (current.IsClaimed(contractId))
                return ClaimResult(ContractClaimStatus.AlreadyClaimed, current);
            if (!current.IsAccepted(contractId))
                return ClaimResult(ContractClaimStatus.RejectedNotAccepted, current);
            if (current.GetProgress(contractId) < definition.RequiredProgress)
                return ClaimResult(ContractClaimStatus.RejectedIncomplete, current);
            if ((long)current.AvailableRenown + definition.Reward.Renown > int.MaxValue)
                return ClaimResult(ContractClaimStatus.RejectedRenownOverflow, current);

            ContractProgressionSnapshot next = current.WithClaimedContract(contractId, definition.Reward);
            return ClaimResult(ContractClaimStatus.Claimed, next, definition.Reward);
        }

        private static ContractGoalType GetGoalType(ContractProgressEvent progressEvent)
        {
            switch (progressEvent)
            {
                case ContractProgressEvent.CombatRoomCompleted: return ContractGoalType.CombatRoomsCompleted;
                case ContractProgressEvent.CustodianDefeated: return ContractGoalType.CustodiansDefeated;
                case ContractProgressEvent.BoneguardDefeated: return ContractGoalType.BoneguardsDefeated;
                case ContractProgressEvent.ItemCrafted:
                case ContractProgressEvent.ItemUpgraded: return ContractGoalType.ItemsCraftedOrUpgraded;
                default: throw new ArgumentOutOfRangeException(nameof(progressEvent));
            }
        }

        private static ContractAcceptOutcome AcceptResult(ContractAcceptStatus status, ContractProgressionSnapshot snapshot)
        {
            return new ContractAcceptOutcome(status, snapshot);
        }

        private static ContractProgressOutcome ProgressResult(ContractProgressStatus status, ContractProgressionSnapshot snapshot)
        {
            return new ContractProgressOutcome(status, snapshot);
        }

        private static ContractClaimOutcome ClaimResult(ContractClaimStatus status,
            ContractProgressionSnapshot snapshot, ContractReward reward = null)
        {
            return new ContractClaimOutcome(status, snapshot, reward);
        }
    }
}
