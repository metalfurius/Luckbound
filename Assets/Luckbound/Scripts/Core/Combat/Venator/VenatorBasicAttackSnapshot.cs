namespace Luckbound.Core.Combat
{
    /// <summary>Immutable result of evaluating one Venator basic-attack command.</summary>
    public readonly struct VenatorBasicAttackSnapshot
    {
        internal VenatorBasicAttackSnapshot(
            bool wasStartedThisTick,
            bool isAttacking,
            bool isCharging,
            ProjectileDefinition projectile,
            decimal movementSpeedMultiplier)
        {
            WasStartedThisTick = wasStartedThisTick;
            IsAttacking = isAttacking;
            IsCharging = isCharging;
            Projectile = projectile;
            MovementSpeedMultiplier = movementSpeedMultiplier;
        }

        public bool WasStartedThisTick { get; }

        public bool IsAttacking { get; }

        /// <summary>True while a charged weapon is waiting for the attack input to be released.</summary>
        public bool IsCharging { get; }

        public ProjectileDefinition Projectile { get; }

        public decimal MovementSpeedMultiplier { get; }
    }
}
