using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Fixed shortbow attack data for the Venator's free basic attack.</summary>
    public sealed class VenatorBasicAttackProfile
    {
        public static readonly VenatorBasicAttackProfile Shortbow = new VenatorBasicAttackProfile(
            new ProjectileDefinition(
                baseDamage: 10m,
                speedUnitsPerSecond: 18m,
                lifetimeTicks: 120,
                maximumTargets: 1),
            attackIntervalTicks: 24);

        public VenatorBasicAttackProfile(
            ProjectileDefinition projectile,
            int attackIntervalTicks,
            int chargeDamageBonusPercent = 0)
        {
            if (projectile == null) throw new ArgumentNullException(nameof(projectile));
            if (attackIntervalTicks <= 0) throw new ArgumentOutOfRangeException(nameof(attackIntervalTicks));
            if (chargeDamageBonusPercent < 0) throw new ArgumentOutOfRangeException(nameof(chargeDamageBonusPercent));

            Projectile = projectile;
            AttackIntervalTicks = attackIntervalTicks;
            ChargeDamageBonusPercent = chargeDamageBonusPercent;
        }

        public ProjectileDefinition Projectile { get; }

        public int AttackIntervalTicks { get; }

        public int ChargeDamageBonusPercent { get; }

        public decimal MovementSpeedMultiplier => 0.65m;

        public ProjectileDefinition CreateProjectile(bool charged)
        {
            if (charged && ChargeDamageBonusPercent <= 0)
                throw new InvalidOperationException("This Venator weapon does not declare a charged damage bonus.");
            if (!charged) return Projectile;

            return new ProjectileDefinition(
                Projectile.BaseDamage * (1m + ChargeDamageBonusPercent / 100m),
                Projectile.SpeedUnitsPerSecond,
                Projectile.LifetimeTicks,
                Projectile.MaximumTargets,
                Projectile.DefensePenetration,
                Projectile.SlowPercent,
                Projectile.SlowDurationTicks,
                Projectile.Element);
        }
    }
}
