namespace Luckbound.Core.Combat
{
    /// <summary>Fixed ember-staff timing and damage for the Magus free basic attack.</summary>
    public sealed class MagusBasicAttackProfile
    {
        public static readonly MagusBasicAttackProfile EmberStaff = new MagusBasicAttackProfile();

        private MagusBasicAttackProfile()
        {
            Projectile = new ProjectileDefinition(
                baseDamage: 12m,
                speedUnitsPerSecond: 10m,
                lifetimeTicks: 180,
                maximumTargets: 1);
        }

        public const string WeaponId = "weapon.magus.ember_staff";

        public ProjectileDefinition Projectile { get; }

        public decimal BaseDamage => Projectile.BaseDamage;

        public int AttackIntervalTicks => 30;

        public decimal MovementSpeedMultiplier => 0.65m;

        /// <summary>The weapon fires a single-target fire projectile using the fixed weapon profile.</summary>
        public string ProjectileElement => "fire";
    }
}
