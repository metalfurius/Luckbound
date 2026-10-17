using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Luckbound.Content
{
    /// <summary>Loads the strict version 1 localization JSON format into an immutable catalog.</summary>
    public static class LocalizationJsonLoader
    {
        public static LocalizedTextCatalog Load(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Localization JSON cannot be empty.", nameof(json));

            object rootValue;
            try
            {
                rootValue = StrictJsonParser.Parse(json);
            }
            catch (FormatException exception)
            {
                throw new FormatException("Localization JSON could not be parsed: " + exception.Message, exception);
            }

            Dictionary<string, object> root = RequireObject(rootValue, "root");
            RequireExactFields(root, "root", "schemaVersion", "defaultLanguageId", "entries");

            int schemaVersion = RequireInteger(root["schemaVersion"], "schemaVersion");
            if (schemaVersion != 1)
                throw new FormatException("Localization catalog schemaVersion must be 1; found " + schemaVersion + ".");

            string defaultLanguageId = RequireString(root["defaultLanguageId"], "defaultLanguageId");
            if (defaultLanguageId != LocalizationLanguage.Spanish && defaultLanguageId != LocalizationLanguage.English)
                throw new FormatException("Localization defaultLanguageId must be exactly 'es' or 'en'.");

            List<object> entryValues = RequireArray(root["entries"], "entries");
            var entries = new Dictionary<string, LocalizedTextEntry>(StringComparer.Ordinal);
            for (int index = 0; index < entryValues.Count; index++)
            {
                string path = "entries[" + index + "]";
                Dictionary<string, object> entry = RequireObject(entryValues[index], path);
                RequireExactFields(entry, path, "key", "es", "en");

                string key = RequireString(entry["key"], path + ".key");
                if (string.IsNullOrWhiteSpace(key))
                    throw new FormatException(path + ".key cannot be empty.");

                string spanish = RequireString(entry["es"], path + ".es");
                if (string.IsNullOrWhiteSpace(spanish))
                    throw new FormatException(path + ".es cannot be empty.");

                string english = RequireString(entry["en"], path + ".en");
                if (string.IsNullOrWhiteSpace(english))
                    throw new FormatException(path + ".en cannot be empty.");

                if (entries.ContainsKey(key))
                    throw new FormatException("Localization catalog contains duplicate key '" + key + "'.");

                entries.Add(key, new LocalizedTextEntry(spanish, english));
            }

            return new LocalizedTextCatalog(defaultLanguageId, entries);
        }

        private static Dictionary<string, object> RequireObject(object value, string path)
        {
            Dictionary<string, object> result = value as Dictionary<string, object>;
            if (result == null) throw new FormatException(path + " must be a JSON object.");
            return result;
        }

        private static List<object> RequireArray(object value, string path)
        {
            List<object> result = value as List<object>;
            if (result == null) throw new FormatException(path + " must be a JSON array.");
            return result;
        }

        private static string RequireString(object value, string path)
        {
            string result = value as string;
            if (result == null) throw new FormatException(path + " must be a JSON string.");
            return result;
        }

        private static int RequireInteger(object value, string path)
        {
            JsonNumber number = value as JsonNumber;
            int result;
            if (number == null ||
                !int.TryParse(number.RawValue, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result))
            {
                throw new FormatException(path + " must be an integer JSON number.");
            }

            return result;
        }

        private static void RequireExactFields(
            Dictionary<string, object> value,
            string path,
            params string[] expectedFields)
        {
            var expected = new HashSet<string>(expectedFields, StringComparer.Ordinal);
            foreach (string field in value.Keys)
            {
                if (!expected.Contains(field))
                    throw new FormatException(path + " contains unsupported field '" + field + "'.");
            }

            foreach (string field in expectedFields)
            {
                if (!value.ContainsKey(field))
                    throw new FormatException(path + " is missing required field '" + field + "'.");
            }
        }

        private sealed class JsonNumber
        {
            internal JsonNumber(string rawValue)
            {
                RawValue = rawValue;
            }

            internal string RawValue { get; }
        }

        /// <summary>
        /// Small strict JSON reader so Unity's JsonUtility cannot silently ignore unknown or duplicate fields.
        /// The returned value tree contains only dictionaries, lists, strings, booleans, null, and number tokens.
        /// </summary>
        private sealed class StrictJsonParser
        {
            private readonly string _json;
            private int _position;

            private StrictJsonParser(string json)
            {
                _json = json;
            }

            internal static object Parse(string json)
            {
                var parser = new StrictJsonParser(json);
                object value = parser.ParseValue();
                parser.SkipWhitespace();
                if (!parser.AtEnd)
                    throw parser.Error("Unexpected characters after the root value.");
                return value;
            }

            private bool AtEnd => _position >= _json.Length;

            private object ParseValue()
            {
                SkipWhitespace();
                if (AtEnd) throw Error("Expected a JSON value.");

                char character = _json[_position];
                if (character == '{') return ParseObject();
                if (character == '[') return ParseArray();
                if (character == '"') return ParseString();
                if (character == 't')
                {
                    ReadLiteral("true");
                    return true;
                }
                if (character == 'f')
                {
                    ReadLiteral("false");
                    return false;
                }
                if (character == 'n')
                {
                    ReadLiteral("null");
                    return null;
                }
                if (character == '-' || IsDigit(character)) return ParseNumber();

                throw Error("Unexpected character while reading a JSON value.");
            }

            private Dictionary<string, object> ParseObject()
            {
                Expect('{');
                SkipWhitespace();
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                if (TryConsume('}')) return result;

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd || _json[_position] != '"')
                        throw Error("Expected a quoted object field name.");

                    string fieldName = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    object fieldValue = ParseValue();
                    if (result.ContainsKey(fieldName))
                        throw Error("Duplicate JSON object field '" + fieldName + "'.");
                    result.Add(fieldName, fieldValue);

                    SkipWhitespace();
                    if (TryConsume('}')) return result;
                    Expect(',');
                }
            }

            private List<object> ParseArray()
            {
                Expect('[');
                SkipWhitespace();
                var result = new List<object>();
                if (TryConsume(']')) return result;

                while (true)
                {
                    result.Add(ParseValue());
                    SkipWhitespace();
                    if (TryConsume(']')) return result;
                    Expect(',');
                }
            }

            private string ParseString()
            {
                Expect('"');
                var builder = new StringBuilder();
                while (!AtEnd)
                {
                    char character = _json[_position++];
                    if (character == '"') return builder.ToString();
                    if (character < 0x20) throw Error("A JSON string contains an unescaped control character.");
                    if (character != '\\')
                    {
                        builder.Append(character);
                        continue;
                    }

                    if (AtEnd) throw Error("A JSON string ends after an escape character.");
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
                        case 'u': builder.Append(ReadUnicodeEscape()); break;
                        default: throw Error("A JSON string contains an invalid escape sequence.");
                    }
                }

                throw Error("A JSON string is not terminated.");
            }

            private char ReadUnicodeEscape()
            {
                if (_position + 4 > _json.Length) throw Error("A Unicode escape must contain four hexadecimal digits.");

                int value = 0;
                for (int index = 0; index < 4; index++)
                {
                    char character = _json[_position++];
                    int digit;
                    if (character >= '0' && character <= '9') digit = character - '0';
                    else if (character >= 'a' && character <= 'f') digit = character - 'a' + 10;
                    else if (character >= 'A' && character <= 'F') digit = character - 'A' + 10;
                    else throw Error("A Unicode escape contains a non-hexadecimal digit.");
                    value = value * 16 + digit;
                }

                return (char)value;
            }

            private JsonNumber ParseNumber()
            {
                int start = _position;
                TryConsume('-');

                if (AtEnd) throw Error("A JSON number is incomplete.");
                if (TryConsume('0'))
                {
                    if (!AtEnd && IsDigit(_json[_position]))
                        throw Error("A JSON number cannot have a leading zero.");
                }
                else
                {
                    if (!IsNonZeroDigit(_json[_position])) throw Error("A JSON number has no integer digits.");
                    _position++;
                    while (!AtEnd && IsDigit(_json[_position])) _position++;
                }

                if (TryConsume('.'))
                {
                    if (AtEnd || !IsDigit(_json[_position])) throw Error("A JSON fraction must contain digits.");
                    while (!AtEnd && IsDigit(_json[_position])) _position++;
                }

                if (TryConsume('e') || TryConsume('E'))
                {
                    if (!TryConsume('+')) TryConsume('-');
                    if (AtEnd || !IsDigit(_json[_position])) throw Error("A JSON exponent must contain digits.");
                    while (!AtEnd && IsDigit(_json[_position])) _position++;
                }

                return new JsonNumber(_json.Substring(start, _position - start));
            }

            private void ReadLiteral(string literal)
            {
                for (int index = 0; index < literal.Length; index++)
                {
                    if (AtEnd || _json[_position] != literal[index])
                        throw Error("Invalid JSON literal.");
                    _position++;
                }
            }

            private void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    char character = _json[_position];
                    if (character != ' ' && character != '\t' && character != '\r' && character != '\n') return;
                    _position++;
                }
            }

            private void Expect(char expected)
            {
                if (AtEnd || _json[_position] != expected)
                    throw Error("Expected '" + expected + "'.");
                _position++;
            }

            private bool TryConsume(char expected)
            {
                if (AtEnd || _json[_position] != expected) return false;
                _position++;
                return true;
            }

            private static bool IsDigit(char character)
            {
                return character >= '0' && character <= '9';
            }

            private static bool IsNonZeroDigit(char character)
            {
                return character >= '1' && character <= '9';
            }

            private FormatException Error(string message)
            {
                return new FormatException(message + " (position " + _position + ").");
            }
        }
    }
}