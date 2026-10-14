namespace Luckbound.Core.Combat
{
    /// <summary>Fixed ember-staff timing and damage for the Magus free basic attack.</summary>
    public sealed class MagusBasicAttackProfile
    {
        public static readonly MagusBasicAttackProfile EmberStaff = new MagusBasicAttackProfile();

        private MagusBasicAttackProfile()
        {
        }

        public const string WeaponId = "weapon.magus.ember_staff";

        public decimal BaseDamage => 12m;

        public int AttackIntervalTicks => 30;

        public decimal MovementSpeedMultiplier => 0.65m;

        /// <summary>The weapon fires a fire projectile; travel parameters belong to Gameplay weapon data.</summary>
        public string ProjectileElement => "fire";
    }
}
