using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Converts the authoritative Skills.json TextAsset into immutable Core definitions.</summary>
    public static class SkillCatalogJsonLoader
    {
        public static SkillCatalog Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Skill catalog JSON cannot be empty.", nameof(json));
            }

            SkillCatalogData data = JsonUtility.FromJson<SkillCatalogData>(json);
            if (data == null || data.schemaVersion != 1 || data.tickRate != 60 || data.skills == null)
            {
                throw new FormatException("Skills.json must use schema version 1, a 60 Hz tick rate, and a skills array.");
            }

            var definitions = new List<SkillDefinition>(data.skills.Length);
            for (int index = 0; index < data.skills.Length; index++)
            {
                SkillData skill = data.skills[index];
                if (skill == null)
                {
                    throw new FormatException("Skills.json contains a null definition at index " + index + ".");
                }

                definitions.Add(new SkillDefinition(
                    id: skill.id,
                    classId: skill.classId,
                    energyCost: skill.energyCost,
                    cooldownTicks: skill.cooldownTicks,
                    preparationTicks: skill.preparationTicks,
                    effectKind: ParseEnum<SkillEffectKind>(skill.effectKind, "effectKind", skill.id),
                    baseDamage: skill.baseDamage,
                    areaRadius: (decimal)skill.areaRadius,
                    effectDurationTicks: skill.effectDurationTicks,
                    maxTargets: skill.maxTargets,
                    projectileCount: skill.projectileCount,
                    angleStepDegrees: skill.angleStepDegrees,
                    projectileSpeed: skill.projectileSpeed,
                    projectileLifetimeTicks: skill.projectileLifetimeTicks,
                    statusKind: ParseEnum<SkillStatusKind>(skill.statusKind, "statusKind", skill.id),
                    statusPower: skill.statusPower,
                    statusDurationTicks: skill.statusDurationTicks,
                    damageReductionPercent: skill.damageReductionPercent / 100m,
                    damagePerSecond: skill.damagePerSecond,
                    damagePulseIntervalTicks: skill.damagePulseIntervalTicks,
                    knockbackTier: ParseEnum<SkillKnockbackTier>(skill.knockbackTier, "knockbackTier", skill.id),
                    chargeSpeed: skill.chargeSpeed,
                    knockbackHorizontalSpeed: skill.knockbackHorizontalSpeed,
                    knockbackVerticalSpeed: skill.knockbackVerticalSpeed,
                    targetAiControlSuspensionTicks: skill.targetAiControlSuspensionTicks));
            }

            return new SkillCatalog(definitions);
        }

        private static TEnum ParseEnum<TEnum>(string value, string fieldName, string skillId)
            where TEnum : struct
        {
            if (Enum.TryParse(value, ignoreCase: false, out TEnum parsed) && Enum.IsDefined(typeof(TEnum), parsed))
            {
                return parsed;
            }

            throw new FormatException("Skill " + skillId + " has an unknown " + fieldName + " value: " + value + ".");
        }

        [Serializable]
        private sealed class SkillCatalogData
        {
            public int schemaVersion;
            public int tickRate;
            public SkillData[] skills;
        }

        [Serializable]
        private sealed class SkillData
        {
            public string id;
            public string classId;
            public int energyCost;
            public int cooldownTicks;
            public int preparationTicks;
            public string effectKind;
            public int baseDamage;
            public float areaRadius;
            public int effectDurationTicks;
            public int maxTargets;
            public int projectileCount;
            public int angleStepDegrees;
            public int projectileSpeed;
            public int projectileLifetimeTicks;
            public string statusKind;
            public int statusPower;
            public int statusDurationTicks;
            public int damageReductionPercent;
            public int damagePerSecond;
            public int damagePulseIntervalTicks;
            public string knockbackTier;
            public int chargeSpeed;
            public int knockbackHorizontalSpeed;
            public int knockbackVerticalSpeed;
            public int targetAiControlSuspensionTicks;
        }
    }
}
