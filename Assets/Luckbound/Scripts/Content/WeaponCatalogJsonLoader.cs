using System;
using System.Collections.Generic;
using Luckbound.Core.Combat;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads weapon combat profiles and checks their item and class references.</summary>
    public static class WeaponCatalogJsonLoader
    {
        public const int ExpectedWeaponCount = 9;

        public static WeaponCatalog Load(string json, ItemCatalog itemCatalog)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Weapon catalog JSON cannot be empty.", nameof(json));
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));

            WeaponCatalogData data;
            try
            {
                data = JsonUtility.FromJson<WeaponCatalogData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Weapon catalog JSON could not be parsed.", exception);
            }

            if (data == null || data.schemaVersion != 1 || data.weapons == null)
                throw new FormatException("Weapons.json must use schema version 1 and contain a weapons array.");
            if (data.weapons.Length != ExpectedWeaponCount)
                throw new FormatException("Weapons.json must define exactly nine weapon profiles.");

            var profiles = new List<WeaponProfile>(data.weapons.Length);
            for (int index = 0; index < data.weapons.Length; index++)
            {
                WeaponData weapon = data.weapons[index];
                if (weapon == null) throw new FormatException("Weapons.json contains a null weapon at index " + index + ".");
                if (weapon.traits == null || weapon.traits.Length == 0)
                    throw new FormatException("Weapon " + weapon.id + " must declare at least one trait.");

                var traits = new List<WeaponTrait>(weapon.traits.Length);
                for (int traitIndex = 0; traitIndex < weapon.traits.Length; traitIndex++)
                {
                    TraitData traitData = weapon.traits[traitIndex];
                    if (traitData == null || !WeaponTrait.TryParseStableId(traitData.id, out WeaponTraitId traitId))
                        throw new FormatException("Weapon " + weapon.id + " has an unknown trait ID at index " + traitIndex + ".");

                    try
                    {
                        traits.Add(new WeaponTrait(traitId, traitData.percent, traitData.durationSeconds));
                    }
                    catch (ArgumentException exception)
                    {
                        throw new FormatException("Invalid trait " + traitData.id + " on weapon " + weapon.id + ".", exception);
                    }
                }

                try
                {
                    profiles.Add(new WeaponProfile(
                        weapon.id,
                        weapon.classId,
                        weapon.damage,
                        weapon.attackIntervalSeconds,
                        traits));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid weapon profile at index " + index + " (" + weapon.id + ").", exception);
                }
            }

            WeaponCatalog catalog;
            try
            {
                catalog = new WeaponCatalog(profiles);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid weapon catalog: " + exception.Message, exception);
            }

            ValidateItemCatalog(catalog, itemCatalog);
            return catalog;
        }

        private static void ValidateItemCatalog(WeaponCatalog weapons, ItemCatalog items)
        {
            var itemWeapons = new List<ItemDefinition>();
            foreach (ItemDefinition definition in items.Definitions)
                if (definition.Kind == ItemKind.Weapon) itemWeapons.Add(definition);

            if (itemWeapons.Count != ExpectedWeaponCount)
                throw new FormatException("Items.json must define exactly nine weapons for Weapons.json.");
            if (weapons.Count != itemWeapons.Count)
                throw new FormatException("Weapons.json and Items.json must define the same weapon count.");

            foreach (WeaponProfile profile in weapons.Profiles)
            {
                if (!items.TryGet(profile.ItemId, out ItemDefinition definition))
                    throw new FormatException("Weapon profile references missing item ID " + profile.ItemId + ".");
                if (definition.Kind != ItemKind.Weapon)
                    throw new FormatException("Weapon profile item is not a Weapon item: " + profile.ItemId + ".");
                if (definition.MaxStack != 1)
                    throw new FormatException("Weapons must use maxStack 1: " + profile.ItemId + ".");
                if (definition.CompatibleClassIds.Count != 1 || definition.CompatibleClassIds[0] != profile.ClassId)
                    throw new FormatException("Weapon profile class must exactly match its item's single compatible class: " + profile.ItemId + ".");
            }

            foreach (ItemDefinition definition in itemWeapons)
            {
                if (!weapons.TryGet(definition.Id, out _))
                    throw new FormatException("Weapon item has no combat profile: " + definition.Id + ".");
            }
        }

        [Serializable]
        private sealed class WeaponCatalogData
        {
            public int schemaVersion;
            public WeaponData[] weapons;
        }

        [Serializable]
        private sealed class WeaponData
        {
            public string id;
            public string classId;
            public int damage;
            public float attackIntervalSeconds;
            public TraitData[] traits;
        }

        [Serializable]
        private sealed class TraitData
        {
            public string id;
            public int percent;
            public float durationSeconds;
        }
    }
}
