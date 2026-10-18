using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Armor
{
    /// <summary>Immutable lookup for validated armor profiles supplied by the content catalog.</summary>
    public sealed class ArmorCatalog
    {
        public const string GuildCuirassItemId = "armor.bellator.guild_cuirass";
        public const string LeatherVestItemId = "armor.venator.leather_vest";
        public const string TravelerRobeItemId = "armor.magus.traveler_robe";
        public const string RunicMantleItemId = "armor.runic_mantle";
        public const string IronCuirassItemId = "armor.iron_cuirass";
        public const string BarkArmorItemId = "armor.bark_armor";

        private readonly Dictionary<string, ArmorProfile> _profiles;
        private readonly ReadOnlyCollection<ArmorProfile> _orderedProfiles;

        public ArmorCatalog(IEnumerable<ArmorProfile> profiles)
        {
            if (profiles == null) throw new ArgumentNullException(nameof(profiles));

            _profiles = new Dictionary<string, ArmorProfile>(StringComparer.Ordinal);
            var ordered = new List<ArmorProfile>();
            foreach (ArmorProfile profile in profiles)
            {
                if (profile == null)
                    throw new ArgumentException("Armor catalog entries cannot be null.", nameof(profiles));
                if (_profiles.ContainsKey(profile.ItemId))
                    throw new ArgumentException("Armor profile IDs must be unique: " + profile.ItemId + ".", nameof(profiles));

                _profiles.Add(profile.ItemId, profile);
                ordered.Add(profile);
            }

            if (ordered.Count == 0)
                throw new ArgumentException("Armor catalogs must contain at least one validated profile.", nameof(profiles));

            _orderedProfiles = ordered.AsReadOnly();
        }

        public int Count => _profiles.Count;
        public IReadOnlyList<ArmorProfile> Profiles => _orderedProfiles;

        public bool TryGet(string itemId, out ArmorProfile profile)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                profile = null;
                return false;
            }
            return _profiles.TryGetValue(itemId, out profile);
        }

        public ArmorProfile Get(string itemId)
        {
            if (TryGet(itemId, out ArmorProfile profile)) return profile;
            throw new KeyNotFoundException("No armor profile exists for item ID " + itemId + ".");
        }
    }
}
