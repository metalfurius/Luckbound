using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Combat
{
    /// <summary>Immutable lookup for skill definitions resolved by stable content ID.</summary>
    public sealed class SkillCatalog
    {
        private readonly Dictionary<string, SkillDefinition> _definitions;
        private readonly ReadOnlyCollection<SkillDefinition> _orderedDefinitions;

        public SkillCatalog(IEnumerable<SkillDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            _definitions = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
            var ordered = new List<SkillDefinition>();
            foreach (SkillDefinition definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException("Skill catalog entries cannot be null.", nameof(definitions));
                }

                if (_definitions.ContainsKey(definition.Id))
                {
                    throw new ArgumentException("Skill IDs must be unique: " + definition.Id + ".", nameof(definitions));
                }

                _definitions.Add(definition.Id, definition);
                ordered.Add(definition);
            }

            _orderedDefinitions = ordered.AsReadOnly();
        }

        public int Count => _definitions.Count;

        public IReadOnlyList<SkillDefinition> Definitions => _orderedDefinitions;

        public bool TryGet(string skillId, out SkillDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(skillId))
            {
                definition = null;
                return false;
            }

            return _definitions.TryGetValue(skillId, out definition);
        }

        public SkillDefinition Get(string skillId)
        {
            if (TryGet(skillId, out SkillDefinition definition))
            {
                return definition;
            }

            throw new KeyNotFoundException("No skill definition exists for content ID " + skillId + ".");
        }
    }
}
