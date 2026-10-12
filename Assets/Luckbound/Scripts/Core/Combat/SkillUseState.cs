using System;
using System.Collections.Generic;

namespace Luckbound.Core.Combat
{
    /// <summary>Per-actor energy, cast preparation, and independent ability cooldowns.</summary>
    public sealed class SkillUseState
    {
        private readonly Dictionary<string, long> _readyAtTicks = new Dictionary<string, long>(StringComparer.Ordinal);
        private long _lastTick = -1;
        private SkillDefinition _preparingSkill;
        private long _preparationStartTick;

        public SkillUseState(int maximumEnergy, int initialEnergy)
        {
            if (maximumEnergy < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEnergy));
            }

            if (initialEnergy < 0 || initialEnergy > maximumEnergy)
            {
                throw new ArgumentOutOfRangeException(nameof(initialEnergy), "Initial energy must be between zero and maximum energy.");
            }

            MaximumEnergy = maximumEnergy;
            CurrentEnergy = initialEnergy;
        }

        public int MaximumEnergy { get; }

        public int CurrentEnergy { get; private set; }

        public bool IsPreparing => _preparingSkill != null;

        public string PreparingSkillId => _preparingSkill == null ? string.Empty : _preparingSkill.Id;

        public void SetCurrentEnergy(int value)
        {
            if (value < 0 || value > MaximumEnergy)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Energy must be between zero and maximum energy.");
            }

            CurrentEnergy = value;
        }

        public SkillUseOutcome Update(long tick, SkillDefinition requestedSkill, bool actionValid, bool interrupted)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Simulation ticks must be zero or greater.");
            }

            if (tick <= _lastTick)
            {
                throw new InvalidOperationException("Skill use state must be updated once per increasing simulation tick.");
            }

            _lastTick = tick;

            if (_preparingSkill != null)
            {
                string preparingId = _preparingSkill.Id;
                if (interrupted)
                {
                    ClearPreparation();
                    return new SkillUseOutcome(SkillUseStatus.Cancelled, preparingId, 0, 0);
                }

                if (tick - _preparationStartTick < _preparingSkill.PreparationTicks)
                {
                    return new SkillUseOutcome(SkillUseStatus.Preparing, preparingId, 0, 0);
                }

                if (CurrentEnergy < _preparingSkill.EnergyCost)
                {
                    ClearPreparation();
                    return new SkillUseOutcome(SkillUseStatus.RejectedInsufficientEnergy, preparingId, 0, 0);
                }

                SkillDefinition activatedSkill = _preparingSkill;
                CurrentEnergy -= activatedSkill.EnergyCost;
                long readyAtTick = tick + activatedSkill.CooldownTicks;
                _readyAtTicks[activatedSkill.Id] = readyAtTick;
                ClearPreparation();
                return new SkillUseOutcome(SkillUseStatus.Activated, activatedSkill.Id, activatedSkill.EnergyCost, readyAtTick);
            }

            if (requestedSkill == null)
            {
                return new SkillUseOutcome(SkillUseStatus.Idle, string.Empty, 0, 0);
            }

            if (!actionValid)
            {
                return new SkillUseOutcome(SkillUseStatus.RejectedInvalid, requestedSkill.Id, 0, 0);
            }

            if (_readyAtTicks.TryGetValue(requestedSkill.Id, out long readyAt) && tick < readyAt)
            {
                return new SkillUseOutcome(SkillUseStatus.RejectedCooldown, requestedSkill.Id, 0, readyAt);
            }

            if (CurrentEnergy < requestedSkill.EnergyCost)
            {
                return new SkillUseOutcome(SkillUseStatus.RejectedInsufficientEnergy, requestedSkill.Id, 0, 0);
            }

            _preparingSkill = requestedSkill;
            _preparationStartTick = tick;
            return new SkillUseOutcome(SkillUseStatus.Preparing, requestedSkill.Id, 0, 0);
        }

        public bool IsReady(string skillId, long tick)
        {
            if (string.IsNullOrWhiteSpace(skillId) || tick < 0)
            {
                return false;
            }

            return !_readyAtTicks.TryGetValue(skillId, out long readyAt) || tick >= readyAt;
        }

        private void ClearPreparation()
        {
            _preparingSkill = null;
            _preparationStartTick = 0;
        }
    }
}
