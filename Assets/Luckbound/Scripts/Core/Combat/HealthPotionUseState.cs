using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Run-local health potion charges and interruptible preparation measured in simulation ticks.</summary>
    public sealed class HealthPotionUseState
    {
        public const int DefaultInitialCharges = 2;
        public const int DefaultMaximumCharges = 3;
        public const int PreparationDurationTicks = 21;
        public const int HealingAmount = 25;

        private long _lastTick = -1;
        private long _preparationStartTick;

        public HealthPotionUseState(int initialCharges = DefaultInitialCharges, int maximumCharges = DefaultMaximumCharges)
        {
            if (maximumCharges < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumCharges));
            }

            if (initialCharges < 0 || initialCharges > maximumCharges)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCharges));
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

        public HealthPotionUseOutcome Update(
            long tick,
            bool requested,
            int currentHealth,
            int maximumHealth,
            bool interrupted)
        {
            if (tick < 0 || tick <= _lastTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), "Potion state must update once per increasing non-negative tick.");
            }

            if (maximumHealth <= 0 || currentHealth < 0 || currentHealth > maximumHealth)
            {
                throw new ArgumentOutOfRangeException(nameof(currentHealth), "Health must be within its positive maximum.");
            }

            _lastTick = tick;
            if (IsPreparing)
            {
                if (interrupted)
                {
                    ClearPreparation();
                    return CreateOutcome(HealthPotionUseStatus.Cancelled);
                }

                if (tick - _preparationStartTick < PreparationDurationTicks)
                {
                    return CreateOutcome(HealthPotionUseStatus.Preparing);
                }

                ClearPreparation();
                if (currentHealth >= maximumHealth)
                {
                    return CreateOutcome(HealthPotionUseStatus.RejectedAtFullHealth);
                }

                int restored = Math.Min(HealingAmount, maximumHealth - currentHealth);
                CurrentCharges--;
                return new HealthPotionUseOutcome(HealthPotionUseStatus.Healed, restored, CurrentCharges);
            }

            if (interrupted)
            {
                return CreateOutcome(HealthPotionUseStatus.Cancelled);
            }

            if (!requested)
            {
                return CreateOutcome(HealthPotionUseStatus.Idle);
            }

            if (CurrentCharges <= 0)
            {
                return CreateOutcome(HealthPotionUseStatus.RejectedWithoutCharges);
            }

            if (currentHealth >= maximumHealth)
            {
                return CreateOutcome(HealthPotionUseStatus.RejectedAtFullHealth);
            }

            IsPreparing = true;
            _preparationStartTick = tick;
            return CreateOutcome(HealthPotionUseStatus.Preparing);
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

        private HealthPotionUseOutcome CreateOutcome(HealthPotionUseStatus status)
        {
            return new HealthPotionUseOutcome(status, 0, CurrentCharges);
        }
    }
}
