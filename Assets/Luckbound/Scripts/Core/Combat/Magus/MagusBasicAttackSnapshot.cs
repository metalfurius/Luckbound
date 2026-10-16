namespace Luckbound.Core.Combat
{
    /// <summary>Immutable result of evaluating one Magus basic-attack command.</summary>
    public readonly struct MagusBasicAttackSnapshot
    {
        internal MagusBasicAttackSnapshot(
            bool wasStartedThisTick,
            bool isAttacking,
            decimal baseDamage,
            string projectileElement,
            ProjectileDefinition projectile,
            decimal movementSpeedMultiplier)
        {
            WasStartedThisTick = wasStartedThisTick;
            IsAttacking = isAttacking;
            BaseDamage = baseDamage;
            ProjectileElement = projectileElement ?? string.Empty;
            Projectile = projectile;
            MovementSpeedMultiplier = movementSpeedMultiplier;
        }

        public bool WasStartedThisTick { get; }

        public bool IsAttacking { get; }

        /// <summary>Damage to use only when WasStartedThisTick is true.</summary>
        public decimal BaseDamage { get; }

        /// <summary>Element of the basic projectile to spawn when WasStartedThisTick is true.</summary>
        public string ProjectileElement { get; }

        public ProjectileDefinition Projectile { get; }

        public decimal MovementSpeedMultiplier { get; }
    }
}
