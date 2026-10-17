using System;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;

namespace Luckbound.Core.Combat
{
    /// <summary>Routes selected Bellator skill inputs through the actor's shared skill-use state.</summary>
    public sealed class BellatorCombatState
    {
        private readonly ActorId _actorId;
        private readonly BellatorSkillLoadout _loadout;
        private readonly SkillUseState _skillUseState;
        private long _lastTick = -1;
        private bool _interruptionPending;

        public BellatorCombatState(ActorId actorId, BellatorSkillLoadout loadout, SkillUseState skillUseState)
        {
            if (!actorId.IsValid) throw new ArgumentException("Bellator combat state requires a valid actor ID.", nameof(actorId));
            _actorId = actorId;
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
            _skillUseState = skillUseState ?? throw new ArgumentNullException(nameof(skillUseState));
        }

        public SkillUseState SkillUses => _skillUseState;

        public bool IsPreparingCharge => _skillUseState.IsPreparing &&
            string.Equals(_skillUseState.PreparingSkillId, BellatorSkillLoadout.ChargeId, StringComparison.Ordinal);

        public bool IsPreparingSkill => _skillUseState.IsPreparing && _loadout.Contains(_skillUseState.PreparingSkillId);

        public void Interrupt()
        {
            _interruptionPending = true;
        }

        public BellatorCombatResult Tick(PlayerCommand command, bool isAlive, bool interrupted = false)
        {
            if (command.ActorId != _actorId) throw new ArgumentException("Player command belongs to a different actor.", nameof(command));
            if (command.Tick < 0) throw new ArgumentOutOfRangeException(nameof(command), "Simulation ticks must be zero or greater.");
            if (command.Tick <= _lastTick) throw new InvalidOperationException("Bellator combat state must be evaluated once per increasing simulation tick.");
            _lastTick = command.Tick;

            bool actionInterrupted = interrupted || _interruptionPending;
            _interruptionPending = false;

            bool skill1Pressed = command.WasPressed(PlayerAction.Skill1);
            bool skill2Pressed = command.WasPressed(PlayerAction.Skill2);
            bool anySkillPressed = skill1Pressed || skill2Pressed;
            bool ambiguousSkillInput = skill1Pressed && skill2Pressed;
            SkillDefinition selectedSkill = ambiguousSkillInput
                ? null
                : skill1Pressed ? _loadout.Skill1 : skill2Pressed ? _loadout.Skill2 : null;

            bool blocked = !isAlive || actionInterrupted || command.WasPressed(PlayerAction.Dodge);
            bool preparing = _skillUseState.IsPreparing;
            bool differentSkillPressed = preparing && selectedSkill != null &&
                !string.Equals(selectedSkill.Id, _skillUseState.PreparingSkillId, StringComparison.Ordinal);
            bool attackCancelsPreparation = preparing && command.WasPressed(PlayerAction.Attack) && !anySkillPressed;
            bool cancelPreparation = blocked || differentSkillPressed || attackCancelsPreparation;

            bool requestIsValid = !blocked && !ambiguousSkillInput &&
                (!anySkillPressed || selectedSkill != null);

            SkillUseOutcome skillOutcome = _skillUseState.Update(
                command.Tick,
                selectedSkill,
                actionValid: requestIsValid,
                interrupted: cancelPreparation);

            SkillDefinition activatedSkill = skillOutcome.Status == SkillUseStatus.Activated
                ? ResolveSelectedSkill(skillOutcome.SkillId)
                : null;
            SkillDefinition activatedCharge = activatedSkill != null && activatedSkill.EffectKind == SkillEffectKind.Charge
                ? activatedSkill
                : null;

            bool skillActionOwnsTick = anySkillPressed || _skillUseState.IsPreparing || activatedSkill != null;
            return new BellatorCombatResult(skillOutcome, activatedSkill, skillActionOwnsTick);
        }

        private SkillDefinition ResolveSelectedSkill(string skillId)
        {
            if (string.Equals(_loadout.Skill1.Id, skillId, StringComparison.Ordinal)) return _loadout.Skill1;
            if (string.Equals(_loadout.Skill2.Id, skillId, StringComparison.Ordinal)) return _loadout.Skill2;
            throw new InvalidOperationException("Activated skill is outside the selected Bellator loadout.");
        }
    }

    /// <summary>One Bellator command result and any activated skill definition.</summary>
    public readonly struct BellatorCombatResult
    {
        internal BellatorCombatResult(SkillUseOutcome skillOutcome, SkillDefinition activatedSkill, bool skillActionOwnsTick)
        {
            SkillOutcome = skillOutcome;
            ActivatedSkill = activatedSkill;
            ActivatedCharge = activatedSkill != null && activatedSkill.EffectKind == SkillEffectKind.Charge
                ? activatedSkill
                : null;
            SkillActionOwnsTick = skillActionOwnsTick;
        }

        public SkillUseOutcome SkillOutcome { get; }

        public SkillDefinition ActivatedSkill { get; }

        public SkillDefinition ActivatedCharge { get; }

        public bool SkillActionOwnsTick { get; }
    }
}
