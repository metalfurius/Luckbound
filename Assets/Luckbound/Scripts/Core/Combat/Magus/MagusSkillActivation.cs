using System;

namespace Luckbound.Core.Combat
{
    /// <summary>One authoritative Magus skill effect for the gameplay adapter to execute.</summary>
    public sealed class MagusSkillActivation
    {
        private MagusSkillActivation(
            string skillId,
            SkillEffectKind effectKind,
            MagusFireballActivation fireball,
            MagusFrostNovaActivation frostNova,
            MagusArcaneSealActivation arcaneSeal)
        {
            SkillId = skillId;
            EffectKind = effectKind;
            Fireball = fireball;
            FrostNova = frostNova;
            ArcaneSeal = arcaneSeal;
        }

        public string SkillId { get; }

        public SkillEffectKind EffectKind { get; }

        public MagusFireballActivation Fireball { get; }

        public MagusFrostNovaActivation FrostNova { get; }

        public MagusArcaneSealActivation ArcaneSeal { get; }

        internal static MagusSkillActivation Create(SkillDefinition skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!string.Equals(skill.ClassId, MagusSkillLoadout.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Only a Magus skill can produce a Magus activation.", nameof(skill));
            }

            switch (skill.EffectKind)
            {
                case SkillEffectKind.ExplosiveProjectile:
                    return new MagusSkillActivation(
                        skill.Id,
                        skill.EffectKind,
                        new MagusFireballActivation(
                            new ProjectileDefinition(
                                skill.BaseDamage,
                                skill.ProjectileSpeed,
                                skill.ProjectileLifetimeTicks,
                                skill.MaxTargets),
                            skill.AreaRadius,
                            skill.StatusPower,
                            skill.StatusDurationTicks),
                        default,
                        default);

                case SkillEffectKind.FrostNova:
                    return new MagusSkillActivation(
                        skill.Id,
                        skill.EffectKind,
                        default,
                        new MagusFrostNovaActivation(
                            skill.BaseDamage,
                            skill.AreaRadius,
                            skill.StatusDurationTicks,
                            freezeProtectionTicks: MagusStatusState.FreezeProtectionDurationTicks),
                        default);

                case SkillEffectKind.DamageZone:
                    return new MagusSkillActivation(
                        skill.Id,
                        skill.EffectKind,
                        default,
                        default,
                        new MagusArcaneSealActivation(
                            skill.AreaRadius,
                            skill.EffectDurationTicks,
                            skill.DamagePerSecond,
                            skill.DamagePulseIntervalTicks));

                default:
                    throw new ArgumentException("The Magus skill effect kind is not supported by this module.", nameof(skill));
            }
        }
    }

    /// <summary>Projectile and status values emitted when Fireball begins its effect.</summary>
    public readonly struct MagusFireballActivation
    {
        internal MagusFireballActivation(
            ProjectileDefinition projectile,
            decimal explosionRadius,
            decimal burnDamagePerSecond,
            int burnDurationTicks)
        {
            Projectile = projectile ?? throw new ArgumentNullException(nameof(projectile));
            ExplosionRadius = explosionRadius;
            BurnDamagePerSecond = burnDamagePerSecond;
            BurnDurationTicks = burnDurationTicks;
        }

        public ProjectileDefinition Projectile { get; }

        public decimal ExplosionRadius { get; }

        public decimal BurnDamagePerSecond { get; }

        public int BurnDurationTicks { get; }
    }

    /// <summary>Area impact and freeze values emitted when Frost Nova begins its effect.</summary>
    public readonly struct MagusFrostNovaActivation
    {
        internal MagusFrostNovaActivation(
            decimal baseDamage,
            decimal radius,
            int freezeDurationTicks,
            int freezeProtectionTicks)
        {
            BaseDamage = baseDamage;
            Radius = radius;
            FreezeDurationTicks = freezeDurationTicks;
            FreezeProtectionTicks = freezeProtectionTicks;
        }

        public decimal BaseDamage { get; }

        public decimal Radius { get; }

        public int FreezeDurationTicks { get; }

        public int FreezeProtectionTicks { get; }
    }

    /// <summary>Room-owned damage-zone values emitted when Arcane Seal begins its effect.</summary>
    public readonly struct MagusArcaneSealActivation
    {
        internal MagusArcaneSealActivation(
            decimal radius,
            int durationTicks,
            decimal damagePerSecond,
            int damagePulseIntervalTicks)
        {
            Radius = radius;
            DurationTicks = durationTicks;
            DamagePerSecond = damagePerSecond;
            DamagePulseIntervalTicks = damagePulseIntervalTicks;
        }

        public decimal Radius { get; }

        public int DurationTicks { get; }

        public decimal DamagePerSecond { get; }

        public int DamagePulseIntervalTicks { get; }
    }
}
