namespace Luckbound.Core.Combat
{
    public enum DirectImpactStatus
    {
        Applied = 0,
        PreventedByInvulnerability = 1,
        DuplicateHit = 2,
        IgnoredBecauseDead = 3
    }

    /// <summary>Result of applying one identified direct hit to an actor's health state.</summary>
    public readonly struct DirectImpactOutcome
    {
        internal DirectImpactOutcome(
            DirectImpactStatus status,
            DamageResolution resolution,
            int currentHealth,
            bool isDead,
            bool deathOccurred,
            long protectedUntilTick)
        {
            Status = status;
            Resolution = resolution;
            CurrentHealth = currentHealth;
            IsDead = isDead;
            DeathOccurred = deathOccurred;
            ProtectedUntilTick = protectedUntilTick;
        }

        public DirectImpactStatus Status { get; }

        public DamageResolution Resolution { get; }

        public int DamageApplied => Resolution.Damage;

        public int CurrentHealth { get; }

        public bool IsDead { get; }

        public bool DeathOccurred { get; }

        /// <summary>The first simulation tick at which a later direct impact may be accepted.</summary>
        public long ProtectedUntilTick { get; }
    }
}
