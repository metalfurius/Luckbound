using System;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;

namespace Luckbound.Core.Combat
{
    /// <summary>Combines Magus command priority, selected skills, and free basic attacks per actor.</summary>
    public sealed class MagusCombatState
    {
        private readonly ActorId _actorId;
        private readonly MagusSkillLoadout _loadout;
        private readonly SkillUseState _skillUseState;
        private readonly MagusBasicAttackState _basicAttackState;
        private long _lastTick = -1;
        private bool _interruptionPending;

        public MagusCombatState(ActorId actorId, MagusSkillLoadout loadout, SkillUseState skillUseState)
        {
            if (!actorId.IsValid) throw new ArgumentException("Magus combat state requires a valid actor ID.", nameof(actorId));
            _actorId = actorId;
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _skillUseState = skillUseState ?? throw new ArgumentNullException(nameof(skillUseState));
            _basicAttackState = new MagusBasicAttackState();
        }

        public SkillUseState SkillUses => _skillUseState;

        public void Interrupt()
        {
            _basicAttackState.Interrupt();
            _interruptionPending = true;
        }

        public MagusCombatResult Tick(PlayerCommand command, bool isAlive, bool interrupted = false)
        {
            if (command.ActorId != _actorId) throw new ArgumentException("Player command belongs to a different actor.", nameof(command));
            if (command.Tick < 0) throw new ArgumentOutOfRangeException(nameof(command), "Simulation ticks must be zero or greater.");
            if (command.Tick <= _lastTick) throw new InvalidOperationException("Magus combat state must be evaluated once per increasing simulation tick.");
            _lastTick = command.Tick;

            bool actionInterrupted = interrupted || _interruptionPending;
            _interruptionPending = false;

            bool dodgePressed = command.WasPressed(PlayerAction.Dodge);
            bool skill1Pressed = command.WasPressed(PlayerAction.Skill1);
            bool skill2Pressed = command.WasPressed(PlayerAction.Skill2);
            bool anySkillPressed = skill1Pressed || skill2Pressed;
            bool ambiguousSkillInput = skill1Pressed && skill2Pressed;
            SkillDefinition requestedSkill = ambiguousSkillInput
                ? null
                : skill1Pressed ? _loadout.Skill1 : skill2Pressed ? _loadout.Skill2 : null;

            bool blocked = !isAlive || actionInterrupted || dodgePressed;
            bool preparing = _skillUseState.IsPreparing;
            bool differentSkillPressed = preparing && requestedSkill != null &&
                !string.Equals(requestedSkill.Id, _skillUseState.PreparingSkillId, StringComparison.Ordinal);
            bool attackCancelsPreparation = preparing && command.WasPressed(PlayerAction.Attack) && !anySkillPressed;
            bool cancelPreparation = blocked || differentSkillPressed || attackCancelsPreparation;
            bool requestIsValid = !blocked && !ambiguousSkillInput;

            SkillUseOutcome skillOutcome = _skillUseState.Update(
                command.Tick,
                requestedSkill,
                actionValid: requestIsValid,
                interrupted: cancelPreparation);

            MagusSkillActivation activation = skillOutcome.Status == SkillUseStatus.Activated
                ? MagusSkillActivation.Create(_loadout.Contains(skillOutcome.SkillId)
                    ? skillOutcome.SkillId == _loadout.Skill1.Id ? _loadout.Skill1 : _loadout.Skill2
                    : throw new InvalidOperationException("Activated skill is outside the selected Magus loadout."))
                : null;

            bool skillActionOwnsTick = anySkillPressed || _skillUseState.IsPreparing || activation != null;
            bool basicAttackAllowed = !blocked && !skillActionOwnsTick && !differentSkillPressed;
            bool basicAttackInterrupted = blocked || anySkillPressed || _skillUseState.IsPreparing || activation != null;
            MagusBasicAttackSnapshot basicAttack = _basicAttackState.Tick(
                command.Tick,
                attackPressed: command.WasPressed(PlayerAction.Attack),
                actionAllowed: basicAttackAllowed,
                interrupted: basicAttackInterrupted);

            return new MagusCombatResult(skillOutcome, activation, basicAttack);
        }
    }
}
