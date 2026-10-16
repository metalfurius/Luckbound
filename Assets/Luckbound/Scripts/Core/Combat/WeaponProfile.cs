using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Combat
{
    /// <summary>Immutable design-time combat values for one equippable weapon item.</summary>
    public sealed class WeaponProfile
    {
        private readonly ReadOnlyCollection<WeaponTrait> _traits;

        public WeaponProfile(
            string itemId,
            string classId,
            int damage,
            float attackIntervalSeconds,
            IEnumerable<WeaponTrait> traits)
        {
            ValidateWeaponId(itemId);
            ValidateClassId(classId);
            if (damage <= 0) throw new ArgumentOutOfRangeException(nameof(damage));
            if (float.IsNaN(attackIntervalSeconds) || float.IsInfinity(attackIntervalSeconds) || attackIntervalSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(attackIntervalSeconds));
            if (traits == null) throw new ArgumentNullException(nameof(traits));

            var copiedTraits = new List<WeaponTrait>();
            var uniqueTraits = new HashSet<WeaponTraitId>();
            foreach (WeaponTrait trait in traits)
            {
                if (trait == null) throw new ArgumentException("Weapon traits cannot contain null entries.", nameof(traits));
                if (!uniqueTraits.Add(trait.Id)) throw new ArgumentException("Weapon traits must be unique by stable ID.", nameof(traits));
                copiedTraits.Add(trait);
            }
            if (copiedTraits.Count == 0) throw new ArgumentException("Every weapon profile must declare its design trait.", nameof(traits));

            ItemId = itemId;
            ClassId = classId;
            Damage = damage;
            AttackIntervalSeconds = attackIntervalSeconds;
            _traits = copiedTraits.AsReadOnly();
        }

        public string ItemId { get; }
        public string ClassId { get; }
        public int Damage { get; }
        public float AttackIntervalSeconds { get; }
        public IReadOnlyList<WeaponTrait> Traits => _traits;

        public bool HasTrait(WeaponTraitId id)
        {
            for (int index = 0; index < _traits.Count; index++)
                if (_traits[index].Id == id) return true;
            return false;
        }

        private static void ValidateWeaponId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("weapon.", StringComparison.Ordinal) || value.Length <= 7)
                throw new ArgumentException("Weapon profiles must use a stable weapon. content ID.", nameof(value));
            ValidateStableIdCharacters(value, nameof(value));
        }

        private static void ValidateClassId(string value)
        {
            if (value != "class.bellator" && value != "class.venator" && value != "class.magus")
                throw new ArgumentException("Weapon profiles must name one of the three playable classes.", nameof(value));
        }

        private static void ValidateStableIdCharacters(string value, string parameterName)
        {
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!(character >= 'a' && character <= 'z') && !(character >= '0' && character <= '9') && character != '.' && character != '_')
                    throw new ArgumentException("Weapon IDs may contain only lowercase letters, digits, dots, and underscores.", parameterName);
            }
        }
    }
}
