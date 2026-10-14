namespace Luckbound.Core.Combat
{
    /// <summary>Tick result for Magus fire and freeze status effects.</summary>
    public readonly struct MagusStatusSnapshot
    {
        internal MagusStatusSnapshot(
            bool isBurning,
            decimal burnDamagePerSecond,
            decimal burnDamageDue,
            long burnEndsAtTick,
            bool isFrozen,
            bool isFreezeProtected,
            long freezeEndsAtTick,
            long freezeProtectionEndsAtTick)
        {
            IsBurning = isBurning;
            BurnDamagePerSecond = burnDamagePerSecond;
            BurnDamageDue = burnDamageDue;
            BurnEndsAtTick = burnEndsAtTick;
            IsFrozen = isFrozen;
            IsFreezeProtected = isFreezeProtected;
            FreezeEndsAtTick = freezeEndsAtTick;
            FreezeProtectionEndsAtTick = freezeProtectionEndsAtTick;
        }

        public bool IsBurning { get; }

        public decimal BurnDamagePerSecond { get; }

        /// <summary>Periodic damage due on this tick; resolve separately from direct hits and invulnerability.</summary>
        public decimal BurnDamageDue { get; }

        public long BurnEndsAtTick { get; }

        public bool IsFrozen { get; }

        public bool IsFreezeProtected { get; }

        public long FreezeEndsAtTick { get; }

        public long FreezeProtectionEndsAtTick { get; }
    }
}
