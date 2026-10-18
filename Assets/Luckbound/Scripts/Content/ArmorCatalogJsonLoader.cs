using System;
using System.Collections.Generic;
using System.Globalization;
using Luckbound.Core.Armor;
using Luckbound.Core.Inventory;
using UnityEngine;

namespace Luckbound.Content
{
    /// <summary>Loads the authoritative armor profiles and validates their item references.</summary>
    public static class ArmorCatalogJsonLoader
    {
        private const int CurrentSchemaVersion = 1;

        private static readonly string[] KnownArmorItemIds =
        {
            ArmorCatalog.TravelerRobeItemId,
            ArmorCatalog.LeatherVestItemId,
            ArmorCatalog.GuildCuirassItemId,
            ArmorCatalog.RunicMantleItemId,
            ArmorCatalog.IronCuirassItemId,
            ArmorCatalog.BarkArmorItemId
        };

        public static ArmorCatalog Load(string json, ItemCatalog itemCatalog)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Armor catalog JSON cannot be empty.", nameof(json));
            if (itemCatalog == null) throw new ArgumentNullException(nameof(itemCatalog));

            new ArmorJsonShapeReader(json).Validate();

            ArmorCatalogData data;
            try
            {
                data = JsonUtility.FromJson<ArmorCatalogData>(json);
            }
            catch (Exception exception)
            {
                throw new FormatException("Armors.json could not be parsed.", exception);
            }

            if (data == null || data.schemaVersion != CurrentSchemaVersion || data.armors == null)
            {
                throw new FormatException(
                    "Armors.json must use schema version " + CurrentSchemaVersion + " and contain an armors array.");
            }

            var knownIds = new HashSet<string>(KnownArmorItemIds, StringComparer.Ordinal);
            ValidateItemCatalog(itemCatalog, knownIds);

            var profiles = new List<ArmorProfile>(data.armors.Length);
            var loadedIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < data.armors.Length; index++)
            {
                ArmorData armor = data.armors[index];
                if (armor == null)
                    throw new FormatException("Armors.json contains a null profile at index " + index + ".");
                if (string.IsNullOrWhiteSpace(armor.id) || !knownIds.Contains(armor.id))
                    throw new FormatException("Armor profile at index " + index + " uses an unknown armor ID: " + armor.id + ".");
                if (!loadedIds.Add(armor.id))
                    throw new FormatException("Armors.json contains duplicate armor ID " + armor.id + ".");

                if (!itemCatalog.TryGet(armor.id, out ItemDefinition definition) || definition.Kind != ItemKind.Armor)
                    throw new FormatException("Armor profile references a missing or non-armor item definition: " + armor.id + ".");

                try
                {
                    profiles.Add(new ArmorProfile(
                        armor.id,
                        armor.baseDefense,
                        armor.maximumEnergyBonus,
                        armor.movementSpeedPercentModifier,
                        armor.energyRegenerationPerSecondBonus,
                        armor.harmfulStatusDurationReductionPercent));
                }
                catch (ArgumentException exception)
                {
                    throw new FormatException("Invalid armor profile at index " + index + " (" + armor.id + "): " + exception.Message, exception);
                }
            }

            if (loadedIds.Count != KnownArmorItemIds.Length)
                throw new FormatException("Armors.json must define exactly the six known armor IDs.");

            foreach (string itemId in KnownArmorItemIds)
            {
                if (!loadedIds.Contains(itemId))
                    throw new FormatException("Armors.json is missing armor profile " + itemId + ".");
            }

            try
            {
                return new ArmorCatalog(profiles);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException("Invalid armor catalog: " + exception.Message, exception);
            }
        }

        private static void ValidateItemCatalog(ItemCatalog itemCatalog, ISet<string> knownIds)
        {
            int armorCount = 0;
            foreach (ItemDefinition definition in itemCatalog.Definitions)
            {
                if (definition.Kind != ItemKind.Armor) continue;
                armorCount++;
                if (!knownIds.Contains(definition.Id))
                    throw new FormatException("ItemCatalog contains an unknown armor definition: " + definition.Id + ".");
            }

            if (armorCount != KnownArmorItemIds.Length)
                throw new FormatException("ItemCatalog must contain exactly the six known armor definitions.");

            foreach (string itemId in KnownArmorItemIds)
            {
                if (!itemCatalog.TryGet(itemId, out ItemDefinition definition) || definition.Kind != ItemKind.Armor)
                    throw new FormatException("ItemCatalog is missing armor definition " + itemId + ".");
            }
        }

        [Serializable]
        private sealed class ArmorCatalogData
        {
            public int schemaVersion;
            public ArmorData[] armors;
        }

        [Serializable]
        private sealed class ArmorData
        {
            public string id;
            public int baseDefense;
            public int maximumEnergyBonus;
            public int movementSpeedPercentModifier;
            public int energyRegenerationPerSecondBonus;
            public int harmfulStatusDurationReductionPercent;
        }

        /// <summary>Checks exact JSON shape before JsonUtility can silently default missing numeric fields.</summary>
        private sealed class ArmorJsonShapeReader
        {
            private static readonly HashSet<string> ArmorFields = new HashSet<string>(StringComparer.Ordinal)
            {
                "id",
                "baseDefense",
                "maximumEnergyBonus",
                "movementSpeedPercentModifier",
                "energyRegenerationPerSecondBonus",
                "harmfulStatusDurationReductionPercent"
            };

            private readonly string _json;
            private int _position;

            public ArmorJsonShapeReader(string json)
            {
                _json = json;
            }

            public void Validate()
            {
                Expect('{');
                var rootFields = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    string property = ReadPropertyName();
                    if (!rootFields.Add(property)) throw Error("Armors.json contains duplicate root field " + property + ".");
                    Expect(':');

                    if (property == "schemaVersion")
                    {
                        ReadInteger();
                    }
                    else if (property == "armors")
                    {
                        ReadArmorArray();
                    }
                    else
                    {
                        throw Error("Armors.json contains unknown root field " + property + ".");
                    }

                    SkipWhitespace();
                    if (Consume('}')) break;
                    Expect(',');
                }

                if (rootFields.Count != 2 || !rootFields.Contains("schemaVersion") || !rootFields.Contains("armors"))
                    throw Error("Armors.json must contain schemaVersion and armors fields.");

                SkipWhitespace();
                if (_position != _json.Length) throw Error("Armors.json contains trailing data.");
            }

            private void ReadArmorArray()
            {
                Expect('[');
                SkipWhitespace();
                if (Consume(']')) return;

                while (true)
                {
                    ReadArmorObject();
                    SkipWhitespace();
                    if (Consume(']')) return;
                    Expect(',');
                }
            }

            private void ReadArmorObject()
            {
                Expect('{');
                var fields = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    string property = ReadPropertyName();
                    if (!ArmorFields.Contains(property)) throw Error("Armor profile contains unknown field " + property + ".");
                    if (!fields.Add(property)) throw Error("Armor profile contains duplicate field " + property + ".");
                    Expect(':');

                    if (property == "id") ReadStringValue();
                    else ReadInteger();

                    SkipWhitespace();
                    if (Consume('}')) break;
                    Expect(',');
                }

                if (fields.Count != ArmorFields.Count)
                    throw Error("Every armor profile must contain id and all five numeric fields.");
            }

            private string ReadPropertyName()
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != '"')
                    throw Error("Expected a JSON property name.");

                _position++;
                int start = _position;
                while (_position < _json.Length && _json[_position] != '"')
                {
                    char character = _json[_position];
                    if (character == '\\' || character < ' ')
                        throw Error("Armor JSON property names must use plain schema keys.");
                    _position++;
                }

                if (_position >= _json.Length) throw Error("Unterminated JSON property name.");
                string property = _json.Substring(start, _position - start);
                _position++;
                return property;
            }

            private void ReadStringValue()
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != '"') throw Error("Expected a JSON string value.");
                _position++;

                while (_position < _json.Length)
                {
                    char character = _json[_position++];
                    if (character == '"') return;
                    if (character < ' ') throw Error("JSON strings cannot contain unescaped control characters.");
                    if (character != '\\') continue;

                    if (_position >= _json.Length) throw Error("Unterminated JSON escape sequence.");
                    char escape = _json[_position++];
                    if (escape == 'u')
                    {
                        for (int digit = 0; digit < 4; digit++)
                        {
                            if (_position >= _json.Length || !IsHexDigit(_json[_position]))
                                throw Error("Invalid Unicode escape in JSON string.");
                            _position++;
                        }
                    }
                    else if (escape != '"' && escape != '\\' && escape != '/' && escape != 'b' &&
                        escape != 'f' && escape != 'n' && escape != 'r' && escape != 't')
                    {
                        throw Error("Invalid JSON escape sequence.");
                    }
                }

                throw Error("Unterminated JSON string value.");
            }

            private void ReadInteger()
            {
                SkipWhitespace();
                int start = _position;
                if (_position < _json.Length && _json[_position] == '-') _position++;

                if (_position >= _json.Length) throw Error("Expected a JSON integer.");
                if (_json[_position] == '0')
                {
                    _position++;
                }
                else if (_json[_position] >= '1' && _json[_position] <= '9')
                {
                    while (_position < _json.Length && IsDigit(_json[_position])) _position++;
                }
                else
                {
                    throw Error("Expected a JSON integer.");
                }

                string value = _json.Substring(start, _position - start);
                if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
                    throw Error("JSON integer is outside the supported 32-bit range.");
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != expected)
                    throw Error("Expected '" + expected + "'.");
                _position++;
            }

            private bool Consume(char value)
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != value) return false;
                _position++;
                return true;
            }

            private void SkipWhitespace()
            {
                while (_position < _json.Length &&
                    (_json[_position] == ' ' || _json[_position] == '\t' ||
                        _json[_position] == '\r' || _json[_position] == '\n'))
                    _position++;
            }

            private FormatException Error(string message)
            {
                return new FormatException(message + " (character " + _position + ").");
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private static bool IsHexDigit(char value)
            {
                return IsDigit(value) || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');
            }
        }
    }
}
