using System;

namespace Luckbound.Core.Combat
{
    /// <summary>Data-driven basic melee timing and movement rules, expressed in 60 Hz simulation ticks.</summary>
    public sealed class MeleeAttackProfile
    {
        public static readonly MeleeAttackProfile BellatorWornSword = new MeleeAttackProfile(
            baseDamage: 12m,
            attackIntervalTicks: 27,
            damageWindowStartTick: 6,
            damageWindowDurationTicks: 6,
            movementSpeedMultiplier: 0.65m);

        public MeleeAttackProfile(
            decimal baseDamage,
            int attackIntervalTicks,
            int damageWindowStartTick,
            int damageWindowDurationTicks,
            decimal movementSpeedMultiplier)
        {
            if (baseDamage <= 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDamage), "Base damage must be greater than zero.");
            }

            if (attackIntervalTicks <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(attackIntervalTicks), "Attack interval must contain at least one tick.");
            }

            if (damageWindowStartTick < 0 || damageWindowDurationTicks <= 0 ||
                (long)damageWindowStartTick + damageWindowDurationTicks > attackIntervalTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(damageWindowDurationTicks), "The damage window must fit inside the attack interval.");
            }

            if (movementSpeedMultiplier < 0m || movementSpeedMultiplier > 1m)
            {
                throw new ArgumentOutOfRangeException(nameof(movementSpeedMultiplier), "Movement speed multiplier must be between zero and one.");
            }

            BaseDamage = baseDamage;
            AttackIntervalTicks = attackIntervalTicks;
            DamageWindowStartTick = damageWindowStartTick;
            DamageWindowDurationTicks = damageWindowDurationTicks;
            MovementSpeedMultiplier = movementSpeedMultiplier;
        }

        public decimal BaseDamage { get; }

        public int AttackIntervalTicks { get; }

        public int DamageWindowStartTick { get; }

        public int DamageWindowDurationTicks { get; }

        public decimal MovementSpeedMultiplier { get; }
    }
}
