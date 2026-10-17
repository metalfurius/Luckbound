using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Luckbound.Core.Input;

namespace Luckbound.Core.Combat
{
    /// <summary>Validates the two Bellator abilities selected for one expedition.</summary>
    public sealed class BellatorSkillLoadout
    {
        public const string ClassId = "class.bellator";
        public const string CircularCutId = "skill.bellator.circular_cut";
        public const string GuardId = "skill.bellator.guard";
        public const string ChargeId = "skill.bellator.charge";

        private static readonly string[] AvailableIds =
        {
            CircularCutId,
            GuardId,
            ChargeId
        };

        private readonly SkillDefinition[] _selectedSkills;
        private readonly ReadOnlyCollection<string> _selectedIds;

        public BellatorSkillLoadout(SkillCatalog catalog, IEnumerable<string> selectedSkillIds)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (selectedSkillIds == null) throw new ArgumentNullException(nameof(selectedSkillIds));

            var ids = new List<string>();
            foreach (string id in selectedSkillIds) ids.Add(id);
            if (ids.Count != 2)
            {
                throw new ArgumentException("A Bellator expedition must select exactly two of its three abilities.", nameof(selectedSkillIds));
            }

            for (int index = 0; index < AvailableIds.Length; index++)
            {
                string availableId = AvailableIds[index];
                if (!catalog.TryGet(availableId, out SkillDefinition availableSkill) ||
                    !string.Equals(availableSkill.ClassId, ClassId, StringComparison.Ordinal) ||
                    availableSkill.EffectKind != ExpectedEffect(availableId))
                {
                    throw new ArgumentException("The skill catalog must contain all three valid Bellator definitions.", nameof(catalog));
                }
            }

            if (string.IsNullOrWhiteSpace(ids[0]) || string.IsNullOrWhiteSpace(ids[1]) ||
                string.Equals(ids[0], ids[1], StringComparison.Ordinal))
            {
                throw new ArgumentException("The two selected Bellator abilities must be distinct valid IDs.", nameof(selectedSkillIds));
            }

            _selectedSkills = new SkillDefinition[2];
            for (int index = 0; index < ids.Count; index++)
            {
                string id = ids[index];
                if (!IsAvailable(id) || !catalog.TryGet(id, out SkillDefinition skill) ||
                    !string.Equals(skill.ClassId, ClassId, StringComparison.Ordinal) ||
                    skill.EffectKind != ExpectedEffect(id))
                {
                    throw new ArgumentException("A selected ability must be one of the three valid Bellator definitions in the skill catalog.", nameof(selectedSkillIds));
                }

                _selectedSkills[index] = skill;
            }

            _selectedIds = Array.AsReadOnly(new[] { _selectedSkills[0].Id, _selectedSkills[1].Id });
        }

        public IReadOnlyList<string> SelectedSkillIds => _selectedIds;

        public SkillDefinition Skill1 => _selectedSkills[0];

        public SkillDefinition Skill2 => _selectedSkills[1];

        public SkillDefinition GetSkill(PlayerAction slot)
        {
            if (slot == PlayerAction.Skill1) return Skill1;
            if (slot == PlayerAction.Skill2) return Skill2;
            throw new ArgumentOutOfRangeException(nameof(slot), "A Bellator skill slot must be Skill1 or Skill2.");
        }

        public bool Contains(string skillId)
        {
            return string.Equals(Skill1.Id, skillId, StringComparison.Ordinal) ||
                string.Equals(Skill2.Id, skillId, StringComparison.Ordinal);
        }

        private static bool IsAvailable(string id)
        {
            for (int index = 0; index < AvailableIds.Length; index++)
            {
                if (string.Equals(AvailableIds[index], id, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static SkillEffectKind ExpectedEffect(string id)
        {
            if (string.Equals(id, CircularCutId, StringComparison.Ordinal)) return SkillEffectKind.AreaStrike;
            if (string.Equals(id, GuardId, StringComparison.Ordinal)) return SkillEffectKind.Guard;
            if (string.Equals(id, ChargeId, StringComparison.Ordinal)) return SkillEffectKind.Charge;
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}
