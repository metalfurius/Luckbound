using System;
using System.Collections.Generic;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Pacts;

namespace Luckbound.Core.Combat
{
    /// <summary>Per-actor energy, cast preparation, and independent ability cooldowns.</summary>
    public sealed class SkillUseState
    {
        private readonly Dictionary<string, long> _readyAtTicks = new Dictionary<string, long>(StringComparer.Ordinal);
        private long _lastTick = -1;
        private SkillDefinition _preparingSkill;
        private long _preparationStartTick;
        private readonly PactModifierTotals _pactModifiers;
        private readonly string _raceId;

        public SkillUseState(int maximumEnergy, int initialEnergy)
            : this(maximumEnergy, initialEnergy, default(PactModifierTotals), PlayableRaceRules.HumanRaceId)
        {
        }

        public SkillUseState(int maximumEnergy, int initialEnergy, PactModifierTotals pactModifiers)
            : this(maximumEnergy, initialEnergy, pactModifiers, PlayableRaceRules.HumanRaceId)
        {
        }

        public SkillUseState(
            int maximumEnergy,
            int initialEnergy,
            PactModifierTotals pactModifiers,
            string raceId = PlayableRaceRules.HumanRaceId)
        {
            if (maximumEnergy < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEnergy));
            }

            if (initialEnergy < 0 || initialEnergy > maximumEnergy)
            {
                throw new ArgumentOutOfRangeException(nameof(initialEnergy), "Initial energy must be between zero and maximum energy.");
            }

            if (raceId == null)
            {
                throw new ArgumentNullException(nameof(raceId));
            }

            if (!PlayableRaceRules.IsSupportedRace(raceId))
            {
                throw new ArgumentException("Unknown playable race ID.", nameof(raceId));
            }

            MaximumEnergy = maximumEnergy;
            CurrentEnergy = initialEnergy;
            _pactModifiers = pactModifiers;
            _raceId = raceId;
        }

        public int MaximumEnergy { get; private set; }

        public string RaceId => _raceId;

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

        /// <summary>Publishes a committed energy maximum without resetting cooldowns or cast preparation.</summary>
        public void ReplaceEnergyLimits(int maximumEnergy, int currentEnergy)
        {
            if (maximumEnergy < 0) throw new ArgumentOutOfRangeException(nameof(maximumEnergy));
            if (currentEnergy < 0 || currentEnergy > MaximumEnergy)
                throw new ArgumentOutOfRangeException(nameof(currentEnergy));

            MaximumEnergy = maximumEnergy;
            CurrentEnergy = Math.Min(currentEnergy, maximumEnergy);
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

                int preparedSkillCost = GetSkillCost(_preparingSkill);
                if (CurrentEnergy < preparedSkillCost)
                {
                    ClearPreparation();
                    return new SkillUseOutcome(SkillUseStatus.RejectedInsufficientEnergy, preparingId, 0, 0);
                }

                SkillDefinition activatedSkill = _preparingSkill;
                CurrentEnergy -= preparedSkillCost;
                long readyAtTick = tick + activatedSkill.CooldownTicks;
                _readyAtTicks[activatedSkill.Id] = readyAtTick;
                ClearPreparation();
                return new SkillUseOutcome(SkillUseStatus.Activated, activatedSkill.Id, preparedSkillCost, readyAtTick);
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

            if (CurrentEnergy < GetSkillCost(requestedSkill))
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

        private int GetSkillCost(SkillDefinition skill)
        {
            int pactAdjustedCost = _pactModifiers.ApplySkillCost(skill.EnergyCost);
            return PlayableRaceRules.ApplySkillCost(_raceId, pactAdjustedCost);
        }
    }
}
