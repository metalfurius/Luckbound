using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Combat
{
    /// <summary>One authoritative Venator skill effect for the gameplay adapter to execute.</summary>
    public sealed class VenatorSkillActivation
    {
        private readonly ReadOnlyCollection<VenatorProjectileShot> _projectiles;

        private VenatorSkillActivation(
            string skillId,
            SkillEffectKind effectKind,
            IEnumerable<VenatorProjectileShot> projectiles,
            VenatorRootTrapActivation rootTrap)
        {
            SkillId = skillId;
            EffectKind = effectKind;
            _projectiles = Array.AsReadOnly(projectiles == null
                ? Array.Empty<VenatorProjectileShot>()
                : new List<VenatorProjectileShot>(projectiles).ToArray());
            RootTrap = rootTrap;
        }

        public string SkillId { get; }

        public SkillEffectKind EffectKind { get; }

        public IReadOnlyList<VenatorProjectileShot> Projectiles => _projectiles;

        public VenatorRootTrapActivation RootTrap { get; }

        internal static VenatorSkillActivation Create(SkillDefinition skill)
        {
            if (skill == null) throw new ArgumentNullException(nameof(skill));
            if (!string.Equals(skill.ClassId, VenatorSkillLoadout.ClassId, StringComparison.Ordinal))
            {
                throw new ArgumentException("Only a Venator skill can produce a Venator activation.", nameof(skill));
            }

            switch (skill.EffectKind)
            {
                case SkillEffectKind.PiercingProjectile:
                    return new VenatorSkillActivation(
                        skill.Id,
                        skill.EffectKind,
                        new[] { CreateProjectileShot(skill, 0m) },
                        default);

                case SkillEffectKind.ProjectileVolley:
                    var shots = new List<VenatorProjectileShot>(skill.ProjectileCount);
                    decimal center = (skill.ProjectileCount - 1) / 2m;
                    for (int index = 0; index < skill.ProjectileCount; index++)
                    {
                        decimal angle = (index - center) * skill.AngleStepDegrees;
                        shots.Add(CreateProjectileShot(skill, angle));
                    }

                    return new VenatorSkillActivation(skill.Id, skill.EffectKind, shots, default);

                case SkillEffectKind.RootTrap:
                    return new VenatorSkillActivation(
                        skill.Id,
                        skill.EffectKind,
                        null,
                        new VenatorRootTrapActivation(skill.BaseDamage, skill.EffectDurationTicks, skill.StatusDurationTicks));

                default:
                    throw new ArgumentException("The Venator skill effect kind is not supported by this module.", nameof(skill));
            }
        }

        private static VenatorProjectileShot CreateProjectileShot(SkillDefinition skill, decimal angleOffsetDegrees)
        {
            var definition = new ProjectileDefinition(
                skill.BaseDamage,
                skill.ProjectileSpeed,
                skill.ProjectileLifetimeTicks,
                skill.MaxTargets);
            return new VenatorProjectileShot(definition, angleOffsetDegrees);
        }
    }

    public readonly struct VenatorProjectileShot
    {
        internal VenatorProjectileShot(ProjectileDefinition definition, decimal angleOffsetDegrees)
        {
            Definition = definition;
            AngleOffsetDegrees = angleOffsetDegrees;
        }

        public ProjectileDefinition Definition { get; }

        public decimal AngleOffsetDegrees { get; }
    }

    /// <summary>Root-trap values emitted at activation; placement and contact belong to Gameplay.</summary>
    public readonly struct VenatorRootTrapActivation
    {
        internal VenatorRootTrapActivation(decimal baseDamage, int lifetimeTicks, int rootDurationTicks)
        {
            BaseDamage = baseDamage;
            LifetimeTicks = lifetimeTicks;
            RootDurationTicks = rootDurationTicks;
        }

        public decimal BaseDamage { get; }

        public int LifetimeTicks { get; }

        public int RootDurationTicks { get; }
    }
}
