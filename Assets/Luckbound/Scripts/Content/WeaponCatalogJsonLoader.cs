using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

            new WeaponJsonShapeReader(json).Validate();

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

                // JsonUtility materializes omitted nested reference fields as empty objects.
                // The shape reader above validates whether the JSON actually declared `melee`;
                // classId therefore identifies the only profiles whose parsed melee data is real.
                WeaponMeleeParameters meleeParameters = null;
                if (string.Equals(weapon.classId, "class.bellator", StringComparison.Ordinal))
                {
                    if (weapon.melee == null)
                        throw new FormatException("Bellator weapon " + weapon.id + " is missing melee parameters.");
                    try
                    {
                        meleeParameters = new WeaponMeleeParameters(
                            weapon.melee.centerOffsetX, weapon.melee.centerOffsetY,
                            weapon.melee.width, weapon.melee.height,
                            weapon.melee.knockbackHorizontalSpeed, weapon.melee.knockbackVerticalSpeed,
                            weapon.melee.aiControlSuspensionTicks);
                    }
                    catch (ArgumentException exception)
                    {
                        throw new FormatException("Invalid melee parameters on weapon " + weapon.id + ".", exception);
                    }
                }

                try
                {
                    profiles.Add(new WeaponProfile(
                        weapon.id, weapon.classId, weapon.damage, weapon.attackIntervalSeconds,
                        traits, meleeParameters));
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
            public MeleeData melee;
        }

        [Serializable]
        private sealed class MeleeData
        {
            public float centerOffsetX;
            public float centerOffsetY;
            public float width;
            public float height;
            public float knockbackHorizontalSpeed;
            public float knockbackVerticalSpeed;
            public int aiControlSuspensionTicks;
        }

        [Serializable]
        private sealed class TraitData
        {
            public string id;
            public int percent;
            public float durationSeconds;
        }

        /// <summary>Checks required fields and exact shape before JsonUtility can default or ignore them.</summary>
        private sealed class WeaponJsonShapeReader
        {
            private static readonly HashSet<string> RootFields = new HashSet<string>(StringComparer.Ordinal) { "schemaVersion", "weapons" };
            private static readonly HashSet<string> WeaponFields = new HashSet<string>(StringComparer.Ordinal)
                { "id", "classId", "damage", "attackIntervalSeconds", "traits", "melee" };
            private static readonly HashSet<string> RequiredWeaponFields = new HashSet<string>(StringComparer.Ordinal)
                { "id", "classId", "damage", "attackIntervalSeconds", "traits" };
            private static readonly HashSet<string> TraitFields = new HashSet<string>(StringComparer.Ordinal)
                { "id", "percent", "durationSeconds" };
            private static readonly HashSet<string> MeleeFields = new HashSet<string>(StringComparer.Ordinal)
                { "centerOffsetX", "centerOffsetY", "width", "height", "knockbackHorizontalSpeed", "knockbackVerticalSpeed", "aiControlSuspensionTicks" };
            private readonly string _json;
            private int _position;

            public WeaponJsonShapeReader(string json) { _json = json; }

            public void Validate()
            {
                Expect('{');
                var fields = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    string property = ReadPropertyName();
                    if (!RootFields.Contains(property)) throw Error("Weapons.json contains unknown root field " + property + ".");
                    if (!fields.Add(property)) throw Error("Weapons.json contains duplicate root field " + property + ".");
                    Expect(':');
                    if (property == "schemaVersion") ReadInteger(); else ReadWeaponArray();
                    SkipWhitespace();
                    if (Consume('}')) break;
                    Expect(',');
                }
                if (fields.Count != RootFields.Count) throw Error("Weapons.json must contain schemaVersion and weapons fields.");
                SkipWhitespace();
                if (_position != _json.Length) throw Error("Weapons.json contains trailing data.");
            }

            private void ReadWeaponArray()
            {
                Expect('['); SkipWhitespace();
                if (Consume(']')) return;
                while (true)
                {
                    ReadWeaponObject(); SkipWhitespace();
                    if (Consume(']')) return;
                    Expect(',');
                }
            }

            private void ReadWeaponObject()
            {
                Expect('{');
                var fields = new HashSet<string>(StringComparer.Ordinal);
                string classId = null;
                bool hasMelee = false;
                while (true)
                {
                    string property = ReadPropertyName();
                    if (!WeaponFields.Contains(property)) throw Error("Weapon profile contains unknown field " + property + ".");
                    if (!fields.Add(property)) throw Error("Weapon profile contains duplicate field " + property + ".");
                    Expect(':');
                    switch (property)
                    {
                        case "id": ReadStringValue(); break;
                        case "classId": classId = ReadStringValue(); break;
                        case "damage": ReadInteger(); break;
                        case "attackIntervalSeconds": ReadFiniteNumber(); break;
                        case "traits": ReadTraitArray(); break;
                        case "melee": hasMelee = true; ReadMeleeObject(); break;
                    }
                    SkipWhitespace();
                    if (Consume('}')) break;
                    Expect(',');
                }
                foreach (string required in RequiredWeaponFields)
                    if (!fields.Contains(required)) throw Error("Weapon profile is missing required field " + required + ".");
                if (string.Equals(classId, "class.bellator", StringComparison.Ordinal))
                {
                    if (!hasMelee) throw Error("Every Bellator weapon profile must declare melee parameters.");
                }
                else if (hasMelee)
                {
                    throw Error("Only Bellator weapon profiles may declare melee parameters.");
                }
            }

            private void ReadTraitArray()
            {
                Expect('['); SkipWhitespace();
                if (Consume(']')) return;
                while (true)
                {
                    ReadTraitObject(); SkipWhitespace();
                    if (Consume(']')) return;
                    Expect(',');
                }
            }

            private void ReadTraitObject()
            {
                Expect('{');
                var fields = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    string property = ReadPropertyName();
                    if (!TraitFields.Contains(property)) throw Error("Weapon trait contains unknown field " + property + ".");
                    if (!fields.Add(property)) throw Error("Weapon trait contains duplicate field " + property + ".");
                    Expect(':');
                    if (property == "id") ReadStringValue();
                    else if (property == "percent") ReadInteger();
                    else ReadFiniteNumber();
                    SkipWhitespace();
                    if (Consume('}')) break;
                    Expect(',');
                }
                if (!fields.Contains("id")) throw Error("Every weapon trait must declare id.");
            }

            private void ReadMeleeObject()
            {
                Expect('{');
                var fields = new HashSet<string>(StringComparer.Ordinal);
                while (true)
                {
                    string property = ReadPropertyName();
                    if (!MeleeFields.Contains(property)) throw Error("Melee parameters contain unknown field " + property + ".");
                    if (!fields.Add(property)) throw Error("Melee parameters contain duplicate field " + property + ".");
                    Expect(':');
                    if (property == "aiControlSuspensionTicks") ReadInteger(); else ReadFiniteNumber();
                    SkipWhitespace();
                    if (Consume('}')) break;
                    Expect(',');
                }
                if (fields.Count != MeleeFields.Count) throw Error("Melee parameters must declare all seven required numeric fields.");
            }

            private string ReadPropertyName()
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != '"') throw Error("Expected a JSON property name.");
                return ReadStringValue();
            }

            private string ReadStringValue()
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != '"') throw Error("Expected a JSON string value.");
                _position++;
                var builder = new StringBuilder();
                while (_position < _json.Length)
                {
                    char character = _json[_position++];
                    if (character == '"') return builder.ToString();
                    if (character < ' ') throw Error("JSON strings cannot contain unescaped control characters.");
                    if (character != '\\') { builder.Append(character); continue; }
                    if (_position >= _json.Length) throw Error("Unterminated JSON escape sequence.");
                    char escape = _json[_position++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            int code = 0;
                            for (int digit = 0; digit < 4; digit++)
                            {
                                if (_position >= _json.Length || !IsHexDigit(_json[_position])) throw Error("Invalid Unicode escape.");
                                char hex = _json[_position++];
                                code = code * 16 + (hex <= '9' ? hex - '0' : char.ToLowerInvariant(hex) - 'a' + 10);
                            }
                            builder.Append((char)code);
                            break;
                        default: throw Error("Invalid JSON escape sequence.");
                    }
                }
                throw Error("Unterminated JSON string value.");
            }

            private void ReadInteger()
            {
                string value = ReadNumberToken();
                int parsed;
                if (value.IndexOf('.') >= 0 || value.IndexOf('e') >= 0 || value.IndexOf('E') >= 0 ||
                    !int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parsed))
                    throw Error("Expected a 32-bit JSON integer.");
            }

            private void ReadFiniteNumber()
            {
                string value = ReadNumberToken();
                float parsed;
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                    float.IsNaN(parsed) || float.IsInfinity(parsed))
                    throw Error("Expected a finite JSON number representable as a single-precision float.");
            }

            private string ReadNumberToken()
            {
                SkipWhitespace();
                int start = _position;
                if (_position < _json.Length && _json[_position] == '-') _position++;
                if (_position >= _json.Length) throw Error("Expected a JSON number.");
                if (_json[_position] == '0')
                {
                    _position++;
                    if (_position < _json.Length && IsDigit(_json[_position])) throw Error("A JSON number cannot contain a leading zero.");
                }
                else if (_json[_position] >= '1' && _json[_position] <= '9')
                    while (_position < _json.Length && IsDigit(_json[_position])) _position++;
                else throw Error("Expected a JSON number.");
                if (_position < _json.Length && _json[_position] == '.')
                {
                    _position++;
                    int fractionStart = _position;
                    while (_position < _json.Length && IsDigit(_json[_position])) _position++;
                    if (_position == fractionStart) throw Error("A JSON fraction must contain digits.");
                }
                if (_position < _json.Length && (_json[_position] == 'e' || _json[_position] == 'E'))
                {
                    _position++;
                    if (_position < _json.Length && (_json[_position] == '+' || _json[_position] == '-')) _position++;
                    if (_position >= _json.Length || !IsDigit(_json[_position])) throw Error("A JSON exponent must contain digits.");
                    while (_position < _json.Length && IsDigit(_json[_position])) _position++;
                }
                return _json.Substring(start, _position - start);
            }

            private void Expect(char expected)
            {
                SkipWhitespace();
                if (_position >= _json.Length || _json[_position] != expected) throw Error("Expected '" + expected + "'.");
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
                while (_position < _json.Length && (_json[_position] == ' ' || _json[_position] == '\t' ||
                    _json[_position] == '\r' || _json[_position] == '\n')) _position++;
            }

            private FormatException Error(string message) { return new FormatException(message + " (character " + _position + ")."); }
            private static bool IsDigit(char value) { return value >= '0' && value <= '9'; }
            private static bool IsHexDigit(char value) { return IsDigit(value) || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F'); }
        }
    }
}
