using System;
using System.Collections.Generic;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Converts the authoritative Items.json TextAsset into immutable Core definitions.</summary>
    public static class ItemCatalogJsonLoader
    {
        public static ItemCatalog Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("Item catalog JSON cannot be empty.", nameof(json));

            ItemCatalogData data;
            try
            {
                data = JsonUtility.FromJson<ItemCatalogData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Item catalog JSON could not be parsed.", exception);
            }

            if (data == null || data.schemaVersion != 1 || data.items == null)
            {
                throw new FormatException("Items.json must use schema version 1 and an items array.");
            }

            var definitions = new List<ItemDefinition>(data.items.Length);
            for (int index = 0; index < data.items.Length; index++)
            {
                ItemData item = data.items[index];
                if (item == null) throw new FormatException("Items.json contains a null item at index " + index + ".");
                if (!TryParseEnum(item.kind, out ItemKind kind))
                {
                    throw new FormatException("Item at index " + index + " has an unknown kind: " + item.kind + ".");
                }

                MaterialKind? materialKind = null;
                if (kind == ItemKind.Material)
                {
                    if (!TryParseEnum(item.materialKind, out MaterialKind parsedMaterial))
                    {
                        throw new FormatException("Material " + item.id + " has an unknown materialKind: " + item.materialKind + ".");
                    }
                    materialKind = parsedMaterial;
                }
                else if (!string.IsNullOrEmpty(item.materialKind))
                {
                    throw new FormatException("Only material definitions may declare materialKind: " + item.id + ".");
                }

                try
                {
                    definitions.Add(new ItemDefinition(
                        item.id,
                        item.nameKey,
                        item.descriptionKey,
                        kind,
                        item.maxStack,
                        item.compatibleClassIds ?? Array.Empty<string>(),
                        materialKind));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid item definition at index " + index + " (" + item.id + "): " + exception.Message, exception);
                }
            }

            try
            {
                return new ItemCatalog(definitions);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid item catalog: " + exception.Message, exception);
            }
        }

        private static bool TryParseEnum<TEnum>(string value, out TEnum parsed)
            where TEnum : struct
        {
            return Enum.TryParse(value, ignoreCase: false, out parsed) && Enum.IsDefined(typeof(TEnum), parsed);
        }

        [Serializable]
        private sealed class ItemCatalogData
        {
            public int schemaVersion;
            public ItemData[] items;
        }

        [Serializable]
        private sealed class ItemData
        {
            public string id;
            public string nameKey;
            public string descriptionKey;
            public string kind;
            public int maxStack;
            public string[] compatibleClassIds;
            public string materialKind;
        }
    }
}
