#nullable disable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace TiaAgent.OpenCode.Client;

/// <summary>
/// Minimal JSON serializer/deserializer for simple flat DTOs.
/// Avoids external dependencies (System.Text.Json, Newtonsoft.Json) that can't be
/// resolved by TIA Portal's OPC loader from the .addin package.
/// </summary>
internal static class SimpleJson
{
    public static string Serialize(object value)
    {
        if (value == null) return "null";
        var sb = new StringBuilder();
        SerializeValue(value, sb);
        return sb.ToString();
    }

    public static T Deserialize<T>(string json)
    {
        var dict = ParseJsonToDict(json);
        return MapDictToDto<T>(dict);
    }

    private static void SerializeValue(object value, StringBuilder sb)
    {
        if (value is string s)
        {
            sb.Append('"');
            sb.Append(EscapeString(s));
            sb.Append('"');
        }
        else if (value is bool b)
        {
            sb.Append(b ? "true" : "false");
        }
        else if (value is int || value is long || value is short || value is byte
                 || value is uint || value is ulong || value is ushort || value is sbyte)
        {
            sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        else if (value is float f)
        {
            sb.Append(f.ToString("R", CultureInfo.InvariantCulture));
        }
        else if (value is double d)
        {
            sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
        }
        else if (value is decimal dec)
        {
            sb.Append(dec.ToString(CultureInfo.InvariantCulture));
        }
        else if (value is DateTimeOffset dto)
        {
            sb.Append('"');
            sb.Append(dto.ToString("o"));
            sb.Append('"');
        }
        else if (value is DateTime dt)
        {
            sb.Append('"');
            sb.Append(dt.ToString("o"));
            sb.Append('"');
        }
        else if (value is Guid guid)
        {
            sb.Append('"');
            sb.Append(guid.ToString());
            sb.Append('"');
        }
        else if (value is IDictionary dict)
        {
            sb.Append('{');
            bool first = true;
            foreach (DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"');
                sb.Append(EscapeString(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? ""));
                sb.Append("\":");
                if (entry.Value == null) sb.Append("null");
                else SerializeValue(entry.Value, sb);
            }
            sb.Append('}');
        }
        else if (value is IEnumerable enumerable)
        {
            sb.Append('[');
            bool first = true;
            foreach (var item in enumerable)
            {
                if (!first) sb.Append(',');
                first = false;
                if (item == null) sb.Append("null");
                else SerializeValue(item, sb);
            }
            sb.Append(']');
        }
        else
        {
            SerializeObject(value, sb);
        }
    }

    private static void SerializeObject(object obj, StringBuilder sb)
    {
        sb.Append('{');
        bool first = true;
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead) continue;
            if (prop.GetIndexParameters().Length > 0) continue;

            var val = prop.GetValue(obj);
            if (val == null) continue;

            if (!first) sb.Append(',');
            first = false;

            sb.Append('"');
            sb.Append(JsonName(prop.Name));
            sb.Append("\":");
            SerializeValue(val, sb);
        }
        sb.Append('}');
    }

    private static string EscapeString(string s)
    {
        var sb = new StringBuilder(s.Length + 16);
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Convert C# PascalCase property name to camelCase JSON name
    private static string JsonName(string pascalName)
    {
        if (string.IsNullOrEmpty(pascalName)) return pascalName;
        return char.ToLowerInvariant(pascalName[0]) + pascalName.Substring(1);
    }

    // --- Minimal JSON tokenizer / parser ---

    private static Dictionary<string, object> ParseJsonToDict(string json)
    {
        var tokens = Tokenize(json);
        int pos = 0;
        return ParseObject(tokens, ref pos);
    }

    private static Dictionary<string, object> ParseObject(List<Token> tokens, ref int pos)
    {
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        Expect(tokens, ref pos, TokenType.BraceOpen);
        if (PeekType(tokens, pos) == TokenType.BraceClose) { pos++; return dict; }

        while (pos < tokens.Count)
        {
            var key = Expect(tokens, ref pos, TokenType.String).Value;
            Expect(tokens, ref pos, TokenType.Colon);
            var val = ParseValue(tokens, ref pos);
            dict[key] = val;
            if (PeekType(tokens, pos) == TokenType.Comma) { pos++; continue; }
            break;
        }

        Expect(tokens, ref pos, TokenType.BraceClose);
        return dict;
    }

    private static object ParseValue(List<Token> tokens, ref int pos)
    {
        var tokType = PeekType(tokens, pos);
        switch (tokType)
        {
            case TokenType.String: return tokens[pos++].Value;
            case TokenType.Number: return ParseNumber(tokens[pos++].Value);
            case TokenType.Bool: return bool.Parse(tokens[pos++].Value);
            case TokenType.Null: pos++; return null;
            case TokenType.BraceOpen: return ParseObject(tokens, ref pos);
            case TokenType.BracketOpen: return ParseArray(tokens, ref pos);
            default: throw new InvalidOperationException("Unexpected token " + tokType + " at position " + pos);
        }
    }

    private static List<object> ParseArray(List<Token> tokens, ref int pos)
    {
        var list = new List<object>();
        Expect(tokens, ref pos, TokenType.BracketOpen);
        if (PeekType(tokens, pos) == TokenType.BracketClose) { pos++; return list; }

        while (pos < tokens.Count)
        {
            list.Add(ParseValue(tokens, ref pos));
            if (PeekType(tokens, pos) == TokenType.Comma) { pos++; continue; }
            break;
        }

        Expect(tokens, ref pos, TokenType.BracketClose);
        return list;
    }

    private static object ParseNumber(string s)
    {
        if (s.IndexOf('.') >= 0 || s.IndexOf('e') >= 0 || s.IndexOf('E') >= 0)
        {
            double d;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
        }
        long l;
        if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out l))
            return l;
        return s;
    }

    private static TokenType PeekType(List<Token> tokens, int pos)
    {
        return pos < tokens.Count ? tokens[pos].Type : TokenType.EOF;
    }

    private static Token Expect(List<Token> tokens, ref int pos, TokenType expected)
    {
        if (pos >= tokens.Count)
            throw new InvalidOperationException("Expected " + expected + " but reached end of input");
        var tok = tokens[pos];
        if (tok.Type != expected)
            throw new InvalidOperationException("Expected " + expected + " but got " + tok.Type + " ('" + tok.Value + "')");
        pos++;
        return tok;
    }

    private static List<Token> Tokenize(string json)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < json.Length)
        {
            char c = json[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            switch (c)
            {
                case '{': tokens.Add(new Token(TokenType.BraceOpen, "{")); i++; break;
                case '}': tokens.Add(new Token(TokenType.BraceClose, "}")); i++; break;
                case '[': tokens.Add(new Token(TokenType.BracketOpen, "[")); i++; break;
                case ']': tokens.Add(new Token(TokenType.BracketClose, "]")); i++; break;
                case ':': tokens.Add(new Token(TokenType.Colon, ":")); i++; break;
                case ',': tokens.Add(new Token(TokenType.Comma, ",")); i++; break;
                case '"':
                    i++;
                    var sb = new StringBuilder();
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\' && i + 1 < json.Length)
                        {
                            i++;
                            switch (json[i])
                            {
                                case '"': sb.Append('"'); break;
                                case '\\': sb.Append('\\'); break;
                                case '/': sb.Append('/'); break;
                                case 'b': sb.Append('\b'); break;
                                case 'f': sb.Append('\f'); break;
                                case 'n': sb.Append('\n'); break;
                                case 'r': sb.Append('\r'); break;
                                case 't': sb.Append('\t'); break;
                                case 'u':
                                    if (i + 4 < json.Length)
                                    {
                                        int cp;
                                        if (int.TryParse(json.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cp))
                                        {
                                            sb.Append((char)cp);
                                            i += 4;
                                        }
                                    }
                                    break;
                                default: sb.Append(json[i]); break;
                            }
                        }
                        else
                        {
                            sb.Append(json[i]);
                        }
                        i++;
                    }
                    if (i < json.Length) i++; // skip closing quote
                    tokens.Add(new Token(TokenType.String, sb.ToString()));
                    break;
                default:
                    if (c == '-' || char.IsDigit(c))
                    {
                        int start = i;
                        if (c == '-') i++;
                        while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == 'e' || json[i] == 'E' || json[i] == '+' || json[i] == '-'))
                        {
                            if ((json[i] == '+' || json[i] == '-') && json[i - 1] != 'e' && json[i - 1] != 'E') break;
                            i++;
                        }
                        tokens.Add(new Token(TokenType.Number, json.Substring(start, i - start)));
                    }
                    else if (json.Substring(i).StartsWith("true", StringComparison.Ordinal))
                    {
                        tokens.Add(new Token(TokenType.Bool, "true"));
                        i += 4;
                    }
                    else if (json.Substring(i).StartsWith("false", StringComparison.Ordinal))
                    {
                        tokens.Add(new Token(TokenType.Bool, "false"));
                        i += 5;
                    }
                    else if (json.Substring(i).StartsWith("null", StringComparison.Ordinal))
                    {
                        tokens.Add(new Token(TokenType.Null, "null"));
                        i += 4;
                    }
                    else
                    {
                        throw new InvalidOperationException("Unexpected character '" + c + "' at position " + i);
                    }
                    break;
            }
        }
        return tokens;
    }

    private struct Token
    {
        public TokenType Type;
        public string Value;

        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value;
        }
    }

    private enum TokenType
    {
        BraceOpen, BraceClose, BracketOpen, BracketClose,
        Colon, Comma, String, Number, Bool, Null, EOF
    }

    /// <summary>
    /// Maps a parsed JSON dictionary to a DTO, respecting property name case-insensitivity.
    /// </summary>
    private static T MapDictToDto<T>(Dictionary<string, object> dict)
    {
        var obj = (T)Activator.CreateInstance(typeof(T));
        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || prop.GetIndexParameters().Length > 0) continue;

            // Try camelCase name first, then PascalCase
            var camelName = JsonName(prop.Name);
            object val;
            if (!dict.TryGetValue(camelName, out val) && !dict.TryGetValue(prop.Name, out val))
                continue;

            if (val == null)
            {
                if (prop.PropertyType == typeof(string) || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                    prop.SetValue(obj, null);
                continue;
            }

            prop.SetValue(obj, ConvertValue(val, prop.PropertyType));
        }
        return obj;
    }

    private static object ConvertValue(object val, Type targetType)
    {
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlying == typeof(string))
            return Convert.ToString(val, CultureInfo.InvariantCulture) ?? "";

        if (underlying == typeof(bool) && val is bool)
            return val;

        if (underlying == typeof(bool) && val is string)
            return bool.Parse((string)val);

        if (underlying == typeof(Guid) && val is string)
            return Guid.Parse((string)val);

        if (underlying == typeof(DateTimeOffset) && val is string)
            return DateTimeOffset.Parse((string)val, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        if (underlying == typeof(DateTime) && val is string)
            return DateTime.Parse((string)val, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        if (underlying == typeof(int) && val is long)
            return (int)(long)val;

        if (underlying == typeof(long) && val is long)
            return val;

        if (underlying == typeof(double) && val is double)
            return val;

        if (underlying == typeof(float) && val is double)
            return (float)(double)val;

        if (underlying == typeof(decimal) && val is double)
            return (decimal)(double)val;

        return Convert.ChangeType(val, underlying, CultureInfo.InvariantCulture);
    }
}
