using System;
using System.Collections.Generic;
using System.Text;
using Luckbound.Core.Expeditions;

namespace Luckbound.Content
{
    internal enum EncounterCatalogJsonValueKindV1
    {
        Object,
        Array,
        String,
        Number,
        True,
        False,
        Null
    }

    internal sealed class EncounterCatalogJsonValueV1
    {
        internal EncounterCatalogJsonValueV1(EncounterCatalogJsonValueKindV1 kind,
            string text = null, Dictionary<string, EncounterCatalogJsonValueV1> members = null,
            List<EncounterCatalogJsonValueV1> items = null)
        {
            Kind = kind;
            Text = text;
            Members = members;
            Items = items;
        }

        internal EncounterCatalogJsonValueKindV1 Kind { get; }
        internal string Text { get; }
        internal Dictionary<string, EncounterCatalogJsonValueV1> Members { get; }
        internal List<EncounterCatalogJsonValueV1> Items { get; }
    }

    internal static class EncounterCatalogStrictJsonReaderV1
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static bool TryRead(byte[] utf8, out EncounterCatalogJsonValueV1 value,
            out EncounterCatalogError error)
        {
            value = null;
            if (utf8 == null)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.NullInput,
                    "$", "JSON input is required.");
                return false;
            }

            byte[] snapshot = (byte[])utf8.Clone();
            string json;
            try
            {
                json = StrictUtf8.GetString(snapshot, 0, snapshot.Length);
            }
            catch (DecoderFallbackException)
            {
                error = new EncounterCatalogError(EncounterCatalogErrorCode.InvalidUtf8,
                    "$", "Input is not valid UTF-8.");
                return false;
            }

            return new Parser(json).TryParse(out value, out error);
        }

        private sealed class Parser
        {
            private const int MaximumDepth = 64;
            private readonly string _json;
            private int _index;

            internal Parser(string json)
            {
                _json = json;
            }

            internal bool TryParse(out EncounterCatalogJsonValueV1 value,
                out EncounterCatalogError error)
            {
                value = null;
                SkipWhitespace();
                if (!TryParseValue("$", 0, out value, out error)) return false;

                SkipWhitespace();
                if (_index != _json.Length)
                {
                    value = null;
                    error = new EncounterCatalogError(EncounterCatalogErrorCode.TrailingJson,
                        "$", "Only one JSON value is permitted.");
                    return false;
                }

                error = default(EncounterCatalogError);
                return true;
            }

            private bool TryParseValue(string path, int depth,
                out EncounterCatalogJsonValueV1 value, out EncounterCatalogError error)
            {
                value = null;
                SkipWhitespace();
                if (_index >= _json.Length)
                    return Fail(path, "A JSON value is required.", out value, out error);
                if (depth > MaximumDepth)
                    return Fail(path, "JSON nesting exceeds the reader limit.", out value, out error);

                char current = _json[_index];
                if (current == '{') return TryParseObject(path, depth, out value, out error);
                if (current == '[') return TryParseArray(path, depth, out value, out error);
                if (current == '"')
                {
                    string text;
                    if (!TryParseString(path, out text, out error)) return false;
                    value = new EncounterCatalogJsonValueV1(
                        EncounterCatalogJsonValueKindV1.String, text);
                    return true;
                }
                if (current == '-' || IsDigit(current))
                {
                    string text;
                    if (!TryParseNumber(path, out text, out error)) return false;
                    value = new EncounterCatalogJsonValueV1(
                        EncounterCatalogJsonValueKindV1.Number, text);
                    return true;
                }
                if (current == 't') return TryParseLiteral("true",
                    EncounterCatalogJsonValueKindV1.True, path, out value, out error);
                if (current == 'f') return TryParseLiteral("false",
                    EncounterCatalogJsonValueKindV1.False, path, out value, out error);
                if (current == 'n') return TryParseLiteral("null",
                    EncounterCatalogJsonValueKindV1.Null, path, out value, out error);

                return Fail(path, "The JSON value is malformed.", out value, out error);
            }

            private bool TryParseObject(string path, int depth,
                out EncounterCatalogJsonValueV1 value, out EncounterCatalogError error)
            {
                value = null;
                _index++;
                SkipWhitespace();
                var members = new Dictionary<string, EncounterCatalogJsonValueV1>(StringComparer.Ordinal);
                if (Consume('}'))
                {
                    value = new EncounterCatalogJsonValueV1(
                        EncounterCatalogJsonValueKindV1.Object, members: members);
                    error = default(EncounterCatalogError);
                    return true;
                }

                while (_index < _json.Length)
                {
                    string name;
                    if (!TryParseString(path, out name, out error)) return false;
                    string memberPath = AppendMemberPath(path, name);
                    if (members.ContainsKey(name))
                    {
                        error = new EncounterCatalogError(EncounterCatalogErrorCode.DuplicateMember,
                            memberPath, "Object member names must be unique.");
                        return false;
                    }

                    SkipWhitespace();
                    if (!Consume(':')) return Fail(memberPath,
                        "A colon is required after an object member name.", out value, out error);
                    EncounterCatalogJsonValueV1 memberValue;
                    if (!TryParseValue(memberPath, depth + 1, out memberValue, out error)) return false;
                    members.Add(name, memberValue);

                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        value = new EncounterCatalogJsonValueV1(
                            EncounterCatalogJsonValueKindV1.Object, members: members);
                        error = default(EncounterCatalogError);
                        return true;
                    }
                    if (!Consume(',')) return Fail(path,
                        "An object member must be followed by a comma or closing brace.", out value, out error);
                    SkipWhitespace();
                }

                return Fail(path, "The object is not closed.", out value, out error);
            }

            private bool TryParseArray(string path, int depth,
                out EncounterCatalogJsonValueV1 value, out EncounterCatalogError error)
            {
                value = null;
                _index++;
                SkipWhitespace();
                var items = new List<EncounterCatalogJsonValueV1>();
                if (Consume(']'))
                {
                    value = new EncounterCatalogJsonValueV1(
                        EncounterCatalogJsonValueKindV1.Array, items: items);
                    error = default(EncounterCatalogError);
                    return true;
                }

                while (_index < _json.Length)
                {
                    EncounterCatalogJsonValueV1 item;
                    if (!TryParseValue(AppendIndexPath(path, items.Count), depth + 1,
                        out item, out error)) return false;
                    items.Add(item);

                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        value = new EncounterCatalogJsonValueV1(
                            EncounterCatalogJsonValueKindV1.Array, items: items);
                        error = default(EncounterCatalogError);
                        return true;
                    }
                    if (!Consume(',')) return Fail(path,
                        "An array item must be followed by a comma or closing bracket.", out value, out error);
                    SkipWhitespace();
                }

                return Fail(path, "The array is not closed.", out value, out error);
            }

            private bool TryParseString(string path, out string value,
                out EncounterCatalogError error)
            {
                value = null;
                if (!Consume('"')) return Fail(path,
                    "An object member name or string value was expected.", out value, out error);

                var builder = new StringBuilder();
                while (_index < _json.Length)
                {
                    char current = _json[_index++];
                    if (current == '"')
                    {
                        value = builder.ToString();
                        error = default(EncounterCatalogError);
                        return true;
                    }
                    if (current < 0x20) return Fail(path,
                        "Unescaped control characters are not valid in JSON strings.", out value, out error);
                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }

                    if (_index >= _json.Length) return Fail(path,
                        "A JSON escape is incomplete.", out value, out error);
                    char escape = _json[_index++];
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
                            int codeUnit;
                            if (!TryReadHexCodeUnit(out codeUnit)) return Fail(path,
                                "A Unicode escape must contain four hexadecimal digits.", out value, out error);
                            builder.Append((char)codeUnit);
                            break;
                        default:
                            return Fail(path, "The JSON escape is not recognized.", out value, out error);
                    }
                }

                return Fail(path, "The JSON string is not closed.", out value, out error);
            }

            private bool TryReadHexCodeUnit(out int value)
            {
                value = 0;
                if (_json.Length - _index < 4) return false;
                for (int offset = 0; offset < 4; offset++)
                {
                    int digit = HexValue(_json[_index++]);
                    if (digit < 0) return false;
                    value = (value << 4) | digit;
                }
                return true;
            }

            private bool TryParseNumber(string path, out string value,
                out EncounterCatalogError error)
            {
                value = null;
                int start = _index;
                if (Consume('-') && _index >= _json.Length)
                    return Fail(path, "The JSON number is incomplete.", out value, out error);

                if (Consume('0'))
                {
                    if (_index < _json.Length && IsDigit(_json[_index]))
                        return Fail(path, "JSON numbers cannot have leading zeroes.", out value, out error);
                }
                else
                {
                    if (_index >= _json.Length || _json[_index] < '1' || _json[_index] > '9')
                        return Fail(path, "The JSON number is malformed.", out value, out error);
                    _index++;
                    while (_index < _json.Length && IsDigit(_json[_index])) _index++;
                }

                if (Consume('.'))
                {
                    if (_index >= _json.Length || !IsDigit(_json[_index]))
                        return Fail(path, "The JSON fraction is malformed.", out value, out error);
                    while (_index < _json.Length && IsDigit(_json[_index])) _index++;
                }

                if (_index < _json.Length && (_json[_index] == 'e' || _json[_index] == 'E'))
                {
                    _index++;
                    if (_index < _json.Length && (_json[_index] == '+' || _json[_index] == '-')) _index++;
                    if (_index >= _json.Length || !IsDigit(_json[_index]))
                        return Fail(path, "The JSON exponent is malformed.", out value, out error);
                    while (_index < _json.Length && IsDigit(_json[_index])) _index++;
                }

                value = _json.Substring(start, _index - start);
                error = default(EncounterCatalogError);
                return true;
            }

            private bool TryParseLiteral(string literal, EncounterCatalogJsonValueKindV1 kind,
                string path, out EncounterCatalogJsonValueV1 value, out EncounterCatalogError error)
            {
                value = null;
                if (_json.Length - _index < literal.Length
                    || string.CompareOrdinal(_json, _index, literal, 0, literal.Length) != 0)
                    return Fail(path, "The JSON literal is malformed.", out value, out error);
                _index += literal.Length;
                value = new EncounterCatalogJsonValueV1(kind);
                error = default(EncounterCatalogError);
                return true;
            }

            private void SkipWhitespace()
            {
                while (_index < _json.Length)
                {
                    char current = _json[_index];
                    if (current != ' ' && current != '\t' && current != '\r' && current != '\n') break;
                    _index++;
                }
            }

            private bool Consume(char expected)
            {
                if (_index >= _json.Length || _json[_index] != expected) return false;
                _index++;
                return true;
            }

            private static bool IsDigit(char value)
            {
                return value >= '0' && value <= '9';
            }

            private static int HexValue(char value)
            {
                if (value >= '0' && value <= '9') return value - '0';
                if (value >= 'a' && value <= 'f') return value - 'a' + 10;
                if (value >= 'A' && value <= 'F') return value - 'A' + 10;
                return -1;
            }

            private static string AppendMemberPath(string path, string member)
            {
                return path + "." + member;
            }

            private static string AppendIndexPath(string path, int index)
            {
                return path + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            }

            private static bool Fail(string path, string message,
                out EncounterCatalogJsonValueV1 value, out EncounterCatalogError error)
            {
                value = null;
                error = new EncounterCatalogError(EncounterCatalogErrorCode.MalformedJson, path, message);
                return false;
            }

            private static bool Fail(string path, string message,
                out string value, out EncounterCatalogError error)
            {
                value = null;
                error = new EncounterCatalogError(EncounterCatalogErrorCode.MalformedJson, path, message);
                return false;
            }
        }
    }
}
