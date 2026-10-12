using System;

namespace Luckbound.Core.Combat
{
    public enum SkillEffectKind
    {
        AreaStrike = 0,
        Guard = 1,
        Charge = 2,
        PiercingProjectile = 3,
        ProjectileVolley = 4,
        RootTrap = 5,
        ExplosiveProjectile = 6,
        FrostNova = 7,
        DamageZone = 8
    }

    public enum SkillStatusKind
    {
        None = 0,
        Burn = 1,
        Root = 2,
        Freeze = 3
    }

    public enum SkillKnockbackTier
    {
        None = 0,
        Strong = 1
    }

    /// <summary>Immutable ability data loaded from the JSON source catalog.</summary>
    public sealed class SkillDefinition
    {
        public SkillDefinition(
            string id,
            string classId,
            int energyCost,
            int cooldownTicks,
            int preparationTicks,
            SkillEffectKind effectKind,
            decimal baseDamage = 0m,
            decimal areaRadius = 0m,
            int effectDurationTicks = 0,
            int maxTargets = 0,
            int projectileCount = 0,
            decimal angleStepDegrees = 0m,
            decimal projectileSpeed = 0m,
            int projectileLifetimeTicks = 0,
            SkillStatusKind statusKind = SkillStatusKind.None,
            decimal statusPower = 0m,
            int statusDurationTicks = 0,
            decimal damageReductionPercent = 0m,
            decimal damagePerSecond = 0m,
            int damagePulseIntervalTicks = 0,
            SkillKnockbackTier knockbackTier = SkillKnockbackTier.None)
        {
            ValidateContentId(id, "id");
            ValidateContentId(classId, "classId");
            if (energyCost < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(energyCost), "Skill energy cost must be at least one.");
            }

            if (cooldownTicks < 30)
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownTicks), "Skill cooldown cannot be less than 0.5 seconds at 60 Hz.");
            }

            if (preparationTicks < 9)
            {
                throw new ArgumentOutOfRangeException(nameof(preparationTicks), "Skill preparation must remain visible for at least 0.15 seconds at 60 Hz.");
            }

            if (!Enum.IsDefined(typeof(SkillEffectKind), effectKind) ||
                !Enum.IsDefined(typeof(SkillStatusKind), statusKind) ||
                !Enum.IsDefined(typeof(SkillKnockbackTier), knockbackTier))
            {
                throw new ArgumentOutOfRangeException(nameof(effectKind), "Skill effect and status values must be defined.");
            }

            ValidateNonNegative(baseDamage, nameof(baseDamage));
            ValidateNonNegative(areaRadius, nameof(areaRadius));
            ValidateNonNegative(angleStepDegrees, nameof(angleStepDegrees));
            ValidateNonNegative(projectileSpeed, nameof(projectileSpeed));
            ValidateNonNegative(statusPower, nameof(statusPower));
            ValidateNonNegative(damageReductionPercent, nameof(damageReductionPercent));
            ValidateNonNegative(damagePerSecond, nameof(damagePerSecond));
            ValidateNonNegative(effectDurationTicks, nameof(effectDurationTicks));
            ValidateNonNegative(maxTargets, nameof(maxTargets));
            ValidateNonNegative(projectileCount, nameof(projectileCount));
            ValidateNonNegative(projectileLifetimeTicks, nameof(projectileLifetimeTicks));
            ValidateNonNegative(statusDurationTicks, nameof(statusDurationTicks));
            ValidateNonNegative(damagePulseIntervalTicks, nameof(damagePulseIntervalTicks));

            switch (effectKind)
            {
                case SkillEffectKind.AreaStrike:
                    Require(baseDamage > 0m && areaRadius > 0m, effectKind);
                    break;
                case SkillEffectKind.FrostNova:
                    Require(baseDamage > 0m && areaRadius > 0m && statusKind == SkillStatusKind.Freeze && statusDurationTicks > 0, effectKind);
                    break;
                case SkillEffectKind.Guard:
                    Require(damageReductionPercent > 0m && damageReductionPercent <= 1m && effectDurationTicks > 0, effectKind);
                    break;
                case SkillEffectKind.Charge:
                    Require(baseDamage > 0m && effectDurationTicks > 0 && knockbackTier != SkillKnockbackTier.None, effectKind);
                    break;
                case SkillEffectKind.PiercingProjectile:
                    Require(baseDamage > 0m && maxTargets > 0 && projectileSpeed > 0m && projectileLifetimeTicks > 0, effectKind);
                    break;
                case SkillEffectKind.ProjectileVolley:
                    Require(baseDamage > 0m && projectileCount > 1 && angleStepDegrees > 0m && projectileSpeed > 0m && projectileLifetimeTicks > 0, effectKind);
                    break;
                case SkillEffectKind.RootTrap:
                    Require(baseDamage > 0m && effectDurationTicks > 0 && statusKind == SkillStatusKind.Root && statusDurationTicks > 0, effectKind);
                    break;
                case SkillEffectKind.ExplosiveProjectile:
                    Require(baseDamage > 0m && areaRadius > 0m && projectileSpeed > 0m && projectileLifetimeTicks > 0 &&
                        statusKind == SkillStatusKind.Burn && statusPower > 0m && statusDurationTicks > 0, effectKind);
                    break;
                case SkillEffectKind.DamageZone:
                    Require(areaRadius > 0m && effectDurationTicks > 0 && damagePerSecond > 0m && damagePulseIntervalTicks > 0, effectKind);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effectKind));
            }

            Id = id;
            ClassId = classId;
            EnergyCost = energyCost;
            CooldownTicks = cooldownTicks;
            PreparationTicks = preparationTicks;
            EffectKind = effectKind;
            BaseDamage = baseDamage;
            AreaRadius = areaRadius;
            EffectDurationTicks = effectDurationTicks;
            MaxTargets = maxTargets;
            ProjectileCount = projectileCount;
            AngleStepDegrees = angleStepDegrees;
            ProjectileSpeed = projectileSpeed;
            ProjectileLifetimeTicks = projectileLifetimeTicks;
            StatusKind = statusKind;
            StatusPower = statusPower;
            StatusDurationTicks = statusDurationTicks;
            DamageReductionPercent = damageReductionPercent;
            DamagePerSecond = damagePerSecond;
            DamagePulseIntervalTicks = damagePulseIntervalTicks;
            KnockbackTier = knockbackTier;
        }

        public string Id { get; }

        public string ClassId { get; }

        public int EnergyCost { get; }

        public int CooldownTicks { get; }

        public int PreparationTicks { get; }

        public SkillEffectKind EffectKind { get; }

        public decimal BaseDamage { get; }

        public decimal AreaRadius { get; }

        public int EffectDurationTicks { get; }

        public int MaxTargets { get; }

        public int ProjectileCount { get; }

        public decimal AngleStepDegrees { get; }

        public decimal ProjectileSpeed { get; }

        public int ProjectileLifetimeTicks { get; }

        public SkillStatusKind StatusKind { get; }

        public decimal StatusPower { get; }

        public int StatusDurationTicks { get; }

        public decimal DamageReductionPercent { get; }

        public decimal DamagePerSecond { get; }

        public int DamagePulseIntervalTicks { get; }

        public SkillKnockbackTier KnockbackTier { get; }

        private static void ValidateContentId(string value, string parameterName)
        {
            string requiredPrefix = parameterName == "id" ? "skill." : "class.";
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(requiredPrefix, StringComparison.Ordinal) || value.Length <= requiredPrefix.Length)
            {
                throw new ArgumentException("Skill and class IDs must use their stable lowercase content prefixes.", parameterName);
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '_')
                {
                    throw new ArgumentException("Content IDs may contain only lowercase letters, digits, dots, and underscores.", parameterName);
                }
            }
        }

        private static void ValidateNonNegative(decimal value, string parameterName)
        {
            if (value < 0m) throw new ArgumentOutOfRangeException(parameterName, "Skill data values cannot be negative.");
        }

        private static void ValidateNonNegative(int value, string parameterName)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(parameterName, "Skill data values cannot be negative.");
        }

        private static void Require(bool condition, SkillEffectKind effectKind)
        {
            if (!condition)
            {
                throw new ArgumentException("Skill effect data is incomplete for " + effectKind + ".", nameof(effectKind));
            }
        }
    }
}
