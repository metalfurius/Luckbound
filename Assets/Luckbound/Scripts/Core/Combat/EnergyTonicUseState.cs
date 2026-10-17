using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Run-local energy tonic charges and interruptible preparation measured in simulation ticks.</summary>
    public sealed class EnergyTonicUseState
    {
        public const int DefaultInitialCharges = 0;
        public const int DefaultMaximumCharges = 3;
        public const int PreparationDurationTicks = 21;
        public const int EnergyAmount = 25;

        private long _lastTick = -1;
        private long _preparationStartTick;

        public EnergyTonicUseState(int initialCharges = DefaultInitialCharges, int maximumCharges = DefaultMaximumCharges)
        {
            if (maximumCharges < 0 || maximumCharges > DefaultMaximumCharges)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCharges), "Energy tonic capacity must be between zero and three charges.");
            }

            if (initialCharges < 0 || initialCharges > maximumCharges)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCharges), "Initial charges must be within the configured capacity.");
            }

            MaximumCharges = maximumCharges;
            CurrentCharges = initialCharges;
        }

        public int MaximumCharges { get; }

        public int CurrentCharges { get; private set; }

        public bool IsPreparing { get; private set; }

        public int PreparationTicksRemaining => IsPreparing
            ? (int)Math.Max(0L, PreparationDurationTicks - (_lastTick - _preparationStartTick))
            : 0;

        public EnergyTonicUseOutcome Update(
            long tick,
            bool requested,
            int currentEnergy,
            int maximumEnergy,
            bool interrupted)
        {
            if (tick < 0 || tick <= _lastTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Energy tonic state must update once per increasing non-negative tick.");
            }

            if (maximumEnergy < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumEnergy), "Maximum energy cannot be negative.");
            }

            if (currentEnergy < 0 || currentEnergy > maximumEnergy)
            {
                throw new ArgumentOutOfRangeException(nameof(currentEnergy), "Energy must be within its non-negative maximum.");
            }

            _lastTick = tick;
            if (IsPreparing)
            {
                if (interrupted)
                {
                    ClearPreparation();
                    return CreateOutcome(EnergyTonicUseStatus.Cancelled);
                }

                if (tick - _preparationStartTick < PreparationDurationTicks)
                {
                    return CreateOutcome(EnergyTonicUseStatus.Preparing);
                }

                ClearPreparation();
                if (currentEnergy >= maximumEnergy)
                {
                    return CreateOutcome(EnergyTonicUseStatus.RejectedAtFullEnergy);
                }

                int restored = Math.Min(EnergyAmount, maximumEnergy - currentEnergy);
                CurrentCharges--;
                return new EnergyTonicUseOutcome(EnergyTonicUseStatus.Energized, restored, CurrentCharges);
            }

            if (interrupted)
            {
                return CreateOutcome(EnergyTonicUseStatus.Cancelled);
            }

            if (!requested)
            {
                return CreateOutcome(EnergyTonicUseStatus.Idle);
            }

            if (currentEnergy >= maximumEnergy)
            {
                return CreateOutcome(EnergyTonicUseStatus.RejectedAtFullEnergy);
            }

            if (CurrentCharges <= 0)
            {
                return CreateOutcome(EnergyTonicUseStatus.RejectedWithoutCharges);
            }

            IsPreparing = true;
            _preparationStartTick = tick;
            return CreateOutcome(EnergyTonicUseStatus.Preparing);
        }

        public void AddCharges(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            CurrentCharges = (int)Math.Min(MaximumCharges, (long)CurrentCharges + amount);
        }

        public void CancelPreparation()
        {
            ClearPreparation();
        }

        private void ClearPreparation()
        {
            IsPreparing = false;
            _preparationStartTick = 0;
        }

        private EnergyTonicUseOutcome CreateOutcome(EnergyTonicUseStatus status)
        {
            return new EnergyTonicUseOutcome(status, 0, CurrentCharges);
        }
    }
}
