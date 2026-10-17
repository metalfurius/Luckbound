using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Luckbound.Core.Armor
{
    /// <summary>Immutable catalog of the six armor profiles defined by the product plan.</summary>
    public sealed class ArmorCatalog
    {
        public const string GuildCuirassItemId = "armor.bellator.guild_cuirass";
        public const string LeatherVestItemId = "armor.venator.leather_vest";
        public const string TravelerRobeItemId = "armor.magus.traveler_robe";
        public const string RunicMantleItemId = "armor.runic_mantle";
        public const string IronCuirassItemId = "armor.iron_cuirass";
        public const string BarkArmorItemId = "armor.bark_armor";

        private static readonly ArmorCatalog StandardCatalog = CreateStandardCatalog();

        private readonly Dictionary<string, ArmorProfile> _profiles;
        private readonly ReadOnlyCollection<ArmorProfile> _orderedProfiles;

        private ArmorCatalog(IEnumerable<ArmorProfile> profiles)
        {
            _profiles = new Dictionary<string, ArmorProfile>(StringComparer.Ordinal);
            var ordered = new List<ArmorProfile>();
            foreach (ArmorProfile profile in profiles)
            {
                _profiles.Add(profile.ItemId, profile);
                ordered.Add(profile);
            }

            _orderedProfiles = ordered.AsReadOnly();
        }

        public static ArmorCatalog Standard => StandardCatalog;
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

        private static ArmorCatalog CreateStandardCatalog()
        {
            return new ArmorCatalog(new[]
            {
                new ArmorProfile(TravelerRobeItemId, baseDefense: 1, maximumEnergyBonus: 5),
                new ArmorProfile(LeatherVestItemId, baseDefense: 2, movementSpeedPercentModifier: 3),
                new ArmorProfile(GuildCuirassItemId, baseDefense: 3, movementSpeedPercentModifier: -3),
                new ArmorProfile(RunicMantleItemId, baseDefense: 2, energyRegenerationPerSecondBonus: 1),
                new ArmorProfile(IronCuirassItemId, baseDefense: 5, movementSpeedPercentModifier: -5),
                new ArmorProfile(BarkArmorItemId, baseDefense: 3, harmfulStatusDurationReductionPercent: 15)
            });
        }
    }
}
