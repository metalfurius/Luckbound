using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Luckbound.Core.Inventory
{
    /// <summary>Immutable design-time item data resolved by a stable content ID.</summary>
    public sealed class ItemDefinition
    {
        private readonly ReadOnlyCollection<string> _compatibleClassIds;

        public ItemDefinition(
            string id,
            string nameKey,
            string descriptionKey,
            ItemKind kind,
            int maxStack,
            IEnumerable<string> compatibleClassIds = null,
            MaterialKind? materialKind = null)
        {
            if (!Enum.IsDefined(typeof(ItemKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            ValidateContentId(id, kind);
            if (string.IsNullOrWhiteSpace(nameKey)) throw new ArgumentException("Item name key cannot be empty.", nameof(nameKey));
            if (string.IsNullOrWhiteSpace(descriptionKey)) throw new ArgumentException("Item description key cannot be empty.", nameof(descriptionKey));
            int expectedMaxStack = kind == ItemKind.Consumable ? 3 : kind == ItemKind.Material ? 99 : 1;
            if (maxStack != expectedMaxStack)
            {
                throw new ArgumentOutOfRangeException(nameof(maxStack), "Consumables use stacks of three, materials use a 99-unit bag limit, and other items use one.");
            }

            var classIds = new List<string>();
            var uniqueClassIds = new HashSet<string>(StringComparer.Ordinal);
            if (compatibleClassIds != null)
            {
                foreach (string classId in compatibleClassIds)
                {
                    ValidateClassId(classId);
                    if (!uniqueClassIds.Add(classId)) throw new ArgumentException("Compatible class IDs must be unique.", nameof(compatibleClassIds));
                    classIds.Add(classId);
                }
            }

            if (kind == ItemKind.Weapon && classIds.Count == 0)
            {
                throw new ArgumentException("Weapons must declare at least one compatible class.", nameof(compatibleClassIds));
            }
            if (kind != ItemKind.Weapon && classIds.Count != 0)
            {
                throw new ArgumentException("Only weapons declare class compatibility.", nameof(compatibleClassIds));
            }
            if ((kind == ItemKind.Material) != materialKind.HasValue)
            {
                throw new ArgumentException("Material definitions must declare exactly one material kind.", nameof(materialKind));
            }
            if (materialKind.HasValue && !Enum.IsDefined(typeof(MaterialKind), materialKind.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(materialKind));
            }

            Id = id;
            NameKey = nameKey;
            DescriptionKey = descriptionKey;
            Kind = kind;
            MaxStack = maxStack;
            MaterialKind = materialKind;
            _compatibleClassIds = classIds.AsReadOnly();
        }

        public string Id { get; }
        public string NameKey { get; }
        public string DescriptionKey { get; }
        public ItemKind Kind { get; }
        public int MaxStack { get; }
        public MaterialKind? MaterialKind { get; }
        public IReadOnlyList<string> CompatibleClassIds => _compatibleClassIds;

        public bool IsCompatibleWithClass(string classId)
        {
            return Kind == ItemKind.Weapon && _compatibleClassIds.Contains(classId, StringComparer.Ordinal);
        }

        private static void ValidateContentId(string value, ItemKind kind)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Item ID cannot be empty.", nameof(value));
            string prefix = kind switch
            {
                ItemKind.Weapon => "weapon.",
                ItemKind.Armor => "armor.",
                ItemKind.Relic => "relic.",
                ItemKind.Consumable => "consumable.",
                ItemKind.Material => "material.",
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            if (!value.StartsWith(prefix, StringComparison.Ordinal) || value.Length <= prefix.Length)
            {
                throw new ArgumentException("Item IDs must use the stable lowercase prefix for their category.", nameof(value));
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '_')
                {
                    throw new ArgumentException("Item IDs may contain only lowercase letters, digits, dots, and underscores.", nameof(value));
                }
            }
        }

        private static void ValidateClassId(string classId)
        {
            if (string.IsNullOrWhiteSpace(classId) || !classId.StartsWith("class.", StringComparison.Ordinal) || classId.Length <= 6)
            {
                throw new ArgumentException("Compatible class IDs must use the stable class. prefix.", nameof(classId));
            }

            for (int index = 0; index < classId.Length; index++)
            {
                char character = classId[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '_')
                {
                    throw new ArgumentException("Class IDs may contain only lowercase letters, digits, dots, and underscores.", nameof(classId));
                }
            }

            if (classId != "class.bellator" && classId != "class.venator" && classId != "class.magus")
            {
                throw new ArgumentException("Compatible class IDs must name one of the three playable classes.", nameof(classId));
            }
        }
    }
}
