namespace Luckbound.Core.Combat
{
    /// <summary>Immutable actor-local view of a basic melee attack at one simulation tick.</summary>
    public readonly struct MeleeAttackSnapshot
    {
        internal MeleeAttackSnapshot(
            bool wasStartedThisTick,
            bool isAttacking,
            bool isDamageWindowOpen,
            decimal baseDamage,
            decimal damageMultiplier,
            decimal movementSpeedMultiplier,
            MeleeHitWindow hitWindow)
        {
            WasStartedThisTick = wasStartedThisTick;
            IsAttacking = isAttacking;
            IsDamageWindowOpen = isDamageWindowOpen;
            BaseDamage = baseDamage;
            DamageMultiplier = damageMultiplier;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            HitWindow = hitWindow;
        }

        public bool WasStartedThisTick { get; }

        public bool IsAttacking { get; }

        public bool IsDamageWindowOpen { get; }

        public decimal BaseDamage { get; }

        public decimal DamageMultiplier { get; }

        public decimal MovementSpeedMultiplier { get; }

        public MeleeHitWindow HitWindow { get; }
    }
}
