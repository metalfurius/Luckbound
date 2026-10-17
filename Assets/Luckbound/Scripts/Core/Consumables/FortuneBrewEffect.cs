using System;
using Luckbound.Core.Fortune;

namespace Luckbound.Core.Consumables
{
    public enum FortuneBrewUseStatus
    {
        Granted = 0,
        RejectedAtMaximum = 1
    }

    public readonly struct FortuneBrewUseResult
    {
        internal FortuneBrewUseResult(FortuneBrewUseStatus status, int previousCharges, int currentCharges)
        {
            Status = status;
            PreviousCharges = previousCharges;
            CurrentCharges = currentCharges;
        }

        public FortuneBrewUseStatus Status { get; }

        public int PreviousCharges { get; }

        public int CurrentCharges { get; }

        public bool WasConsumed => Status == FortuneBrewUseStatus.Granted;
    }

    public static class FortuneBrewEffect
    {
        public const int ChargeAmount = 1;
        public const int MaximumCharges = FortuneOfferBook.MaximumCharges;

        public static FortuneBrewUseResult Apply(int currentCharges)
        {
            if (currentCharges < 0 || currentCharges > MaximumCharges)
                throw new ArgumentOutOfRangeException(nameof(currentCharges));

            if (currentCharges == MaximumCharges)
                return new FortuneBrewUseResult(FortuneBrewUseStatus.RejectedAtMaximum, currentCharges, currentCharges);

            return new FortuneBrewUseResult(FortuneBrewUseStatus.Granted, currentCharges, currentCharges + ChargeAmount);
        }
    }
}
