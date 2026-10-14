namespace Luckbound.Core.Combat
{
    /// <summary>Immutable result of evaluating one Venator basic-attack command.</summary>
    public readonly struct VenatorBasicAttackSnapshot
    {
        internal VenatorBasicAttackSnapshot(
            bool wasStartedThisTick,
            bool isAttacking,
            ProjectileDefinition projectile,
            decimal movementSpeedMultiplier)
        {
            WasStartedThisTick = wasStartedThisTick;
            IsAttacking = isAttacking;
            Projectile = projectile;
            MovementSpeedMultiplier = movementSpeedMultiplier;
        }

        public bool WasStartedThisTick { get; }

        public bool IsAttacking { get; }

        public ProjectileDefinition Projectile { get; }

        public decimal MovementSpeedMultiplier { get; }
    }
}
