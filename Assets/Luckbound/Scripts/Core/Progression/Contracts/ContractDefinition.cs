using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Luckbound.Core.Progression.Contracts
{
    public enum ContractGoalType
    {
        CombatRoomsCompleted = 1,
        CustodiansDefeated = 2,
        BoneguardsDefeated = 3,
        ItemsCraftedOrUpgraded = 4,
        StoryFragmentsFound = 5,
        ExpeditionsExtractedAfterGuardian = 6,
        MagistersDefeated = 7,
        SkillHitsApplied = 8,
        PactsAcceptedBetweenExpeditions = 9,
        WeaponsUpgradedToPlusThree = 10,
        EndingsCompleted = 11
    }

    public enum ContractProgressEvent
    {
        CombatRoomCompleted = 1,
        CustodianDefeated = 2,
        BoneguardDefeated = 3,
        ItemCrafted = 4,
        ItemUpgraded = 5,
        StoryFragmentFound = 6,
        ExpeditionExtractedAfterGuardian = 7,
        MagisterDefeated = 8,
        SkillHitApplied = 9,
        PactAcceptedBetweenExpeditions = 10,
        WeaponUpgradedToPlusThree = 11,
        EndingCompleted = 12
    }

    public enum ContractUnlockKind
    {
        StartingKit = 1,
        PlayableRace = 2,
        GuildRecognition = 3,
        GuildDecoration = 4
    }

    /// <summary>A stable, typed unlock granted by a claimed contract.</summary>
    public sealed class ContractRewardUnlock
    {
        public ContractRewardUnlock(ContractUnlockKind kind, string unlockId)
        {
            if (!Enum.IsDefined(typeof(ContractUnlockKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (string.IsNullOrWhiteSpace(unlockId)) throw new ArgumentException("A contract unlock requires a stable ID.", nameof(unlockId));

            Kind = kind;
            UnlockId = unlockId;
        }

        public ContractUnlockKind Kind { get; }
        public string UnlockId { get; }
    }

    /// <summary>The immutable reward payload shown and applied when one contract is claimed.</summary>
    public sealed class ContractReward
    {
        private readonly ReadOnlyCollection<ContractRewardUnlock> _unlocks;

        public ContractReward(int renown, IEnumerable<ContractRewardUnlock> unlocks)
        {
            if (renown < 0) throw new ArgumentOutOfRangeException(nameof(renown));
            if (unlocks == null) throw new ArgumentNullException(nameof(unlocks));

            ContractRewardUnlock[] values = unlocks.ToArray();
            if (values.Any(unlock => unlock == null))
                throw new ArgumentException("Contract rewards cannot contain null unlocks.", nameof(unlocks));
            if (values.Select(unlock => unlock.UnlockId).Distinct(StringComparer.Ordinal).Count() != values.Length)
                throw new ArgumentException("Contract reward unlock IDs must be unique.", nameof(unlocks));

            Renown = renown;
            _unlocks = Array.AsReadOnly(values);
        }

        public int Renown { get; }
        public IReadOnlyList<ContractRewardUnlock> Unlocks => _unlocks;
    }

    /// <summary>Stable objective and localization references for a profile contract.</summary>
    public sealed class ContractDefinition
    {
        public ContractDefinition(string id, ContractGoalType goalType, int requiredProgress,
            string titleLocalizationKey, string objectiveLocalizationKey, ContractReward reward)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A contract requires a stable ID.", nameof(id));
            if (!Enum.IsDefined(typeof(ContractGoalType), goalType)) throw new ArgumentOutOfRangeException(nameof(goalType));
            if (requiredProgress <= 0) throw new ArgumentOutOfRangeException(nameof(requiredProgress));
            if (string.IsNullOrWhiteSpace(titleLocalizationKey))
                throw new ArgumentException("A contract requires a title localization key.", nameof(titleLocalizationKey));
            if (string.IsNullOrWhiteSpace(objectiveLocalizationKey))
                throw new ArgumentException("A contract requires an objective localization key.", nameof(objectiveLocalizationKey));
            if (reward == null) throw new ArgumentNullException(nameof(reward));

            Id = id;
            GoalType = goalType;
            RequiredProgress = requiredProgress;
            TitleLocalizationKey = titleLocalizationKey;
            ObjectiveLocalizationKey = objectiveLocalizationKey;
            Reward = reward;
        }

        public string Id { get; }
        public ContractGoalType GoalType { get; }
        public int RequiredProgress { get; }
        public string TitleLocalizationKey { get; }
        public string ObjectiveLocalizationKey { get; }
        public ContractReward Reward { get; }
    }
}
