namespace Luckbound.Core.Combat
{
    /// <summary>Fixed shortbow attack data for the Venator's free basic attack.</summary>
    public sealed class VenatorBasicAttackProfile
    {
        public static readonly VenatorBasicAttackProfile Shortbow = new VenatorBasicAttackProfile();

        private VenatorBasicAttackProfile()
        {
            Projectile = new ProjectileDefinition(
                baseDamage: 10m,
                speedUnitsPerSecond: 18m,
                lifetimeTicks: 120,
                maximumTargets: 1);
        }

        public ProjectileDefinition Projectile { get; }

        public int AttackIntervalTicks => 24;

        public decimal MovementSpeedMultiplier => 0.65m;
    }
}
