using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Inventory
{
    /// <summary>Immutable lookup for item definitions and the six canonical materials.</summary>
    public sealed class ItemCatalog
    {
        private readonly Dictionary<string, ItemDefinition> _definitions;
        private readonly ReadOnlyCollection<ItemDefinition> _orderedDefinitions;
        private readonly Dictionary<string, StartingLoadout> _startingLoadouts;

        public ItemCatalog(IEnumerable<ItemDefinition> definitions, IEnumerable<StartingLoadout> startingLoadouts = null)
        {
            if (definitions == null) throw new ArgumentNullException(nameof(definitions));

            _definitions = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
            var ordered = new List<ItemDefinition>();
            var materials = new HashSet<MaterialKind>();
            foreach (ItemDefinition definition in definitions)
            {
                if (definition == null) throw new ArgumentException("Item catalog entries cannot be null.", nameof(definitions));
                if (_definitions.ContainsKey(definition.Id)) throw new ArgumentException("Item IDs must be unique: " + definition.Id + ".", nameof(definitions));
                _definitions.Add(definition.Id, definition);
                ordered.Add(definition);
                if (definition.MaterialKind.HasValue && !materials.Add(definition.MaterialKind.Value))
                {
                    throw new ArgumentException("Each material kind must have exactly one definition: " + definition.MaterialKind.Value + ".", nameof(definitions));
                }
            }

            foreach (MaterialKind materialKind in Enum.GetValues(typeof(MaterialKind)))
            {
                if (!materials.Contains(materialKind)) throw new ArgumentException("Item catalog is missing material definition " + materialKind + ".", nameof(definitions));
            }

            _orderedDefinitions = ordered.AsReadOnly();
            _startingLoadouts = new Dictionary<string, StartingLoadout>(StringComparer.Ordinal);
            if (startingLoadouts != null)
            {
                foreach (StartingLoadout loadout in startingLoadouts)
                {
                    if (loadout == null) throw new ArgumentException("Starting loadouts cannot contain null entries.", nameof(startingLoadouts));
                    if (_startingLoadouts.ContainsKey(loadout.ClassId))
                        throw new ArgumentException("Starting loadouts must be unique by class: " + loadout.ClassId + ".", nameof(startingLoadouts));
                    if (!_definitions.TryGetValue(loadout.WeaponItemId, out ItemDefinition weapon) || weapon.Kind != ItemKind.Weapon ||
                        !weapon.IsCompatibleWithClass(loadout.ClassId))
                        throw new ArgumentException("Starting weapon is missing, not a weapon, or incompatible with " + loadout.ClassId + ".", nameof(startingLoadouts));
                    if (!_definitions.TryGetValue(loadout.ArmorItemId, out ItemDefinition armor) || armor.Kind != ItemKind.Armor)
                        throw new ArgumentException("Starting armor is missing or not armor for " + loadout.ClassId + ".", nameof(startingLoadouts));
                    _startingLoadouts.Add(loadout.ClassId, loadout);
                }

                foreach (string classId in new[] { "class.bellator", "class.venator", "class.magus" })
                {
                    if (!_startingLoadouts.ContainsKey(classId))
                        throw new ArgumentException("Starting loadout is missing for " + classId + ".", nameof(startingLoadouts));
                }
                if (_startingLoadouts.Count != 3)
                    throw new ArgumentException("Starting loadouts must contain only the three playable classes.", nameof(startingLoadouts));
            }
        }

        public int Count => _definitions.Count;
        public IReadOnlyList<ItemDefinition> Definitions => _orderedDefinitions;

        public bool TryGet(string itemId, out ItemDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                definition = null;
                return false;
            }
            return _definitions.TryGetValue(itemId, out definition);
        }

        public ItemDefinition Get(string itemId)
        {
            if (TryGet(itemId, out ItemDefinition definition)) return definition;
            throw new KeyNotFoundException("No item definition exists for content ID " + itemId + ".");
        }

        public ItemDefinition GetMaterial(MaterialKind materialKind)
        {
            foreach (ItemDefinition definition in _orderedDefinitions)
            {
                if (definition.MaterialKind == materialKind) return definition;
            }
            throw new KeyNotFoundException("No item definition exists for material " + materialKind + ".");
        }

        public StartingLoadout GetStartingLoadout(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId) || !_startingLoadouts.TryGetValue(classId, out StartingLoadout loadout))
                throw new KeyNotFoundException("No starting loadout exists for class " + (classId ?? "<null>") + ".");
            return loadout;
        }

        public IReadOnlyCollection<StartingLoadout> StartingLoadouts => _startingLoadouts.Values;
    }
}
