using System;

namespace Luckbound.Core.Consumables
{
    /// <summary>Additional-light interval measured in simulation ticks.</summary>
    public readonly struct TorchLightActivation
    {
        internal TorchLightActivation(long startsAtTick, long endsAtTickExclusive)
        {
            StartsAtTick = startsAtTick;
            EndsAtTickExclusive = endsAtTickExclusive;
        }

        public long StartsAtTick { get; }

        public long EndsAtTickExclusive { get; }

        public int DurationTicks => TorchEffect.DurationTicks;

        public bool IsActiveAt(long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            return tick >= StartsAtTick && tick < EndsAtTickExclusive;
        }
    }

    public static class TorchEffect
    {
        public const int DurationTicks = 3600;

        public static TorchLightActivation Activate(long activationTick)
        {
            if (activationTick < 0) throw new ArgumentOutOfRangeException(nameof(activationTick));
            if (activationTick > long.MaxValue - DurationTicks)
                throw new ArgumentOutOfRangeException(nameof(activationTick), "Torch duration exceeds the simulation tick range.");

            return new TorchLightActivation(activationTick, activationTick + DurationTicks);
        }
    }
}
