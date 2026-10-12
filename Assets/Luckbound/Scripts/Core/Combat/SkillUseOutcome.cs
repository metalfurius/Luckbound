namespace Luckbound.Core.Combat
{
    public enum SkillUseStatus
    {
        Idle = 0,
        Preparing = 1,
        Activated = 2,
        RejectedInvalid = 3,
        RejectedCooldown = 4,
        RejectedInsufficientEnergy = 5,
        Cancelled = 6
    }

    /// <summary>Result of one skill input or cast tick.</summary>
    public readonly struct SkillUseOutcome
    {
        internal SkillUseOutcome(SkillUseStatus status, string skillId, int energySpent, long readyAtTick)
        {
            Status = status;
            SkillId = skillId ?? string.Empty;
            EnergySpent = energySpent;
            ReadyAtTick = readyAtTick;
        }

        public SkillUseStatus Status { get; }

        public string SkillId { get; }

        public int EnergySpent { get; }

        public long ReadyAtTick { get; }
    }
}
