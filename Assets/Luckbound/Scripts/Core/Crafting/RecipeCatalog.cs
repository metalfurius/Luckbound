using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Crafting
{
    /// <summary>Immutable lookup of the validated recipe definitions.</summary>
    public sealed class RecipeCatalog
    {
        private readonly Dictionary<string, RecipeDefinition> _definitions;
        private readonly ReadOnlyCollection<RecipeDefinition> _orderedDefinitions;

        public RecipeCatalog(IEnumerable<RecipeDefinition> definitions)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _definitions = new Dictionary<string, RecipeDefinition>(StringComparer.Ordinal);
            var ordered = new List<RecipeDefinition>();
            foreach (RecipeDefinition definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Recipe catalog entries cannot be null.", nameof(definitions));
                if (_definitions.ContainsKey(definition.Id))
                    throw new ArgumentException("Recipe IDs must be unique: " + definition.Id + ".", nameof(definitions));

                _definitions.Add(definition.Id, definition);
                ordered.Add(definition);
            }

            _orderedDefinitions = ordered.AsReadOnly();
        }

        public int Count => _definitions.Count;
        public IReadOnlyList<RecipeDefinition> Definitions => _orderedDefinitions;

        public bool TryGet(string recipeId, out RecipeDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(recipeId))
            {
                definition = null;
                return false;
            }
            return _definitions.TryGetValue(recipeId, out definition);
        }

        public RecipeDefinition Get(string recipeId)
        {
            if (TryGet(recipeId, out RecipeDefinition definition)) return definition;
            throw new KeyNotFoundException("No recipe definition exists for content ID " + recipeId + ".");
        }
    }
}
