namespace Luckbound.Core.Combat
{
    /// <summary>Result of one command tick, including effects that Gameplay must execute.</summary>
    public readonly struct VenatorCombatResult
    {
        internal VenatorCombatResult(
            SkillUseOutcome skillOutcome,
            VenatorSkillActivation skillActivation,
            VenatorBasicAttackSnapshot basicAttack)
        {
            SkillOutcome = skillOutcome;
            SkillActivation = skillActivation;
            BasicAttack = basicAttack;
        }

        public SkillUseOutcome SkillOutcome { get; }

        public VenatorSkillActivation SkillActivation { get; }

        public VenatorBasicAttackSnapshot BasicAttack { get; }
    }
}
