using System;

namespace Luckbound.Core.Consumables
{
    [Flags]
    public enum HarmfulStatusKind
    {
        None = 0,
        Burn = 1 << 0,
        Poison = 1 << 1,
        Slow = 1 << 2,
        Root = 1 << 3,
        Freeze = 1 << 4,
        Stun = 1 << 5,
        All = Burn | Poison | Slow | Root | Freeze | Stun
    }

    /// <summary>Antidote effect command for clearing harmful statuses and protecting against them.</summary>
    public readonly struct AntidoteEffectResolution
    {
        internal AntidoteEffectResolution(long activatedAtTick, long protectionEndsAtTickExclusive)
        {
            ActivatedAtTick = activatedAtTick;
            ProtectionEndsAtTickExclusive = protectionEndsAtTickExclusive;
        }

        public long ActivatedAtTick { get; }

        /// <summary>End of the protection interval; protection is active before this tick only.</summary>
        public long ProtectionEndsAtTickExclusive { get; }

        public HarmfulStatusKind ClearedStatuses => HarmfulStatusKind.All;

        public bool ClearsAllHarmfulStatuses => true;

        public HarmfulStatusKind Clear(HarmfulStatusKind activeStatuses)
        {
            ValidateStatuses(activeStatuses);
            return activeStatuses & ~ClearedStatuses;
        }

        public bool ProtectsAt(HarmfulStatusKind status, long tick)
        {
            if (tick < 0) throw new ArgumentOutOfRangeException(nameof(tick));
            ValidateStatuses(status);
            if (status == HarmfulStatusKind.None)
                throw new ArgumentOutOfRangeException(nameof(status));

            return tick >= ActivatedAtTick && tick < ProtectionEndsAtTickExclusive &&
                (ClearedStatuses & status) == status;
        }

        private static void ValidateStatuses(HarmfulStatusKind statuses)
        {
            if ((statuses & ~HarmfulStatusKind.All) != HarmfulStatusKind.None)
                throw new ArgumentOutOfRangeException(nameof(statuses));
        }
    }

    public static class AntidoteEffect
    {
        public const int ProtectionDurationTicks = 600;

        public static AntidoteEffectResolution Apply(long activationTick)
        {
            if (activationTick < 0) throw new ArgumentOutOfRangeException(nameof(activationTick));
            if (activationTick > long.MaxValue - ProtectionDurationTicks)
                throw new ArgumentOutOfRangeException(nameof(activationTick), "Antidote protection exceeds the simulation tick range.");

            return new AntidoteEffectResolution(activationTick, activationTick + ProtectionDurationTicks);
        }
    }
}
