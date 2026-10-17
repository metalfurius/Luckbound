namespace Luckbound.Core.Combat
{
    /// <summary>Fixed ember-staff timing and damage for the Magus free basic attack.</summary>
    public sealed class MagusBasicAttackProfile
    {
        public static readonly MagusBasicAttackProfile EmberStaff = new MagusBasicAttackProfile(
            new ProjectileDefinition(
                baseDamage: 12m,
                speedUnitsPerSecond: 10m,
                lifetimeTicks: 180,
                maximumTargets: 1,
                element: "fire"),
            attackIntervalTicks: 30,
            projectileElement: "fire");

        public MagusBasicAttackProfile(ProjectileDefinition projectile, int attackIntervalTicks, string projectileElement)
        {
            Projectile = projectile ?? throw new System.ArgumentNullException(nameof(projectile));
            if (attackIntervalTicks <= 0) throw new System.ArgumentOutOfRangeException(nameof(attackIntervalTicks));
            ProjectileElement = projectileElement ?? throw new System.ArgumentNullException(nameof(projectileElement));
            AttackIntervalTicks = attackIntervalTicks;
        }

        public const string WeaponId = "weapon.magus.ember_staff";

        public ProjectileDefinition Projectile { get; }

        public decimal BaseDamage => Projectile.BaseDamage;

        public int AttackIntervalTicks { get; }

        public decimal MovementSpeedMultiplier => 0.65m;

        /// <summary>Element label is descriptive; status effects come from explicit projectile trait data.</summary>
        public string ProjectileElement { get; }
    }
}
