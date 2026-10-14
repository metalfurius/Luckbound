namespace Luckbound.Core.Combat
{
    /// <summary>Result of one Magus command tick, including effects that Gameplay must execute.</summary>
    public readonly struct MagusCombatResult
    {
        internal MagusCombatResult(
            SkillUseOutcome skillOutcome,
            MagusSkillActivation skillActivation,
            MagusBasicAttackSnapshot basicAttack)
        {
            SkillOutcome = skillOutcome;
            SkillActivation = skillActivation;
            BasicAttack = basicAttack;
        }

        public SkillUseOutcome SkillOutcome { get; }

        public MagusSkillActivation SkillActivation { get; }

        public MagusBasicAttackSnapshot BasicAttack { get; }
    }
}
