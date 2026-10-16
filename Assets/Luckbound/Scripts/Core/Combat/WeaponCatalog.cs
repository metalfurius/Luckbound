using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Combat
{
    /// <summary>An immutable lookup for weapon combat profiles.</summary>
    public sealed class WeaponCatalog
    {
        private readonly Dictionary<string, WeaponProfile> _profiles;
        private readonly ReadOnlyCollection<WeaponProfile> _orderedProfiles;

        public WeaponCatalog(IEnumerable<WeaponProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));

            _profiles = new Dictionary<string, WeaponProfile>(StringComparer.Ordinal);
            var ordered = new List<WeaponProfile>();
            foreach (WeaponProfile profile in profiles)
            {
                if (profile == null) throw new ArgumentException("Weapon catalog entries cannot be null.", nameof(profiles));
                if (_profiles.ContainsKey(profile.ItemId))
                    throw new ArgumentException("Weapon profile IDs must be unique: " + profile.ItemId + ".", nameof(profiles));
                _profiles.Add(profile.ItemId, profile);
                ordered.Add(profile);
            }
            if (ordered.Count == 0) throw new ArgumentException("Weapon catalogs cannot be empty.", nameof(profiles));

            _orderedProfiles = ordered.AsReadOnly();
        }

        public int Count => _profiles.Count;
        public IReadOnlyList<WeaponProfile> Profiles => _orderedProfiles;

        public bool TryGet(string itemId, out WeaponProfile profile)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                profile = null;
                return false;
            }
            return _profiles.TryGetValue(itemId, out profile);
        }

        public WeaponProfile Get(string itemId)
        {
            if (TryGet(itemId, out WeaponProfile profile)) return profile;
            throw new KeyNotFoundException("No weapon profile exists for item ID " + itemId + ".");
        }
    }
}
