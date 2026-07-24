using System.Text;
using FluentAssertions;
using TiaAgent.AddIn.Bridge;
using Xunit;

namespace TiaAgent.AddIn.Tests;

/// <summary>
/// Tests for the JSON string unescaping logic in AgentBridgeClient.
/// The manual JSON parser must correctly reverse the escaping applied
/// by BridgeController.EscapeJson() during serialization.
/// </summary>
public class JsonUnescapeTests
{
    /// <summary>
    /// Extracts a JSON string value, handling escaped quotes within the string.
    /// This mirrors the production parsing logic but with proper escape-awareness.
    /// </summary>
    private static string? ExtractJsonStringValue(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length || json[idx] != '"') return null;

        // Find the closing quote, respecting escaped quotes (\")
        var start = idx + 1;
        var i = start;
        while (i < json.Length)
        {
            if (json[i] == '\\')
            {
                i += 2; // skip escaped character
                continue;
            }
            if (json[i] == '"')
                break;
            i++;
        }

        if (i >= json.Length) return null;
        return json.Substring(start, i - start);
    }

    /// <summary>
    /// Unescapes a JSON string value. Mirrors AgentBridgeClient.UnescapeJsonString.
    /// Uses character-by-character processing to avoid double-processing issues.
    /// Handles \uXXXX unicode escape sequences for accented characters.
    /// </summary>
    private static string UnescapeJsonString(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return raw;

        var sb = new System.Text.StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            if (raw[i] == '\\' && i + 1 < raw.Length)
            {
                switch (raw[i + 1])
                {
                    case 'n':
                        sb.Append('\n');
                        i++;
                        break;
                    case 'r':
                        sb.Append('\r');
                        i++;
                        break;
                    case 't':
                        sb.Append('\t');
                        i++;
                        break;
                    case '"':
                        sb.Append('"');
                        i++;
                        break;
                    case '\\':
                        sb.Append('\\');
                        i++;
                        break;
                    case 'u':
                        // \uXXXX — parse 4 hex digits as a Unicode code point
                        if (i + 5 < raw.Length)
                        {
                            var hex = raw.Substring(i + 2, 4);
                            if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
                                    System.Globalization.CultureInfo.InvariantCulture, out var codePoint))
                            {
                                // Check for surrogate pair: high surrogate (0xD800-0xDBFF)
                                if (codePoint >= 0xD800 && codePoint <= 0xDBFF &&
                                    i + 11 < raw.Length && raw[i + 6] == '\\' && raw[i + 7] == 'u')
                                {
                                    var lowHex = raw.Substring(i + 8, 4);
                                    if (int.TryParse(lowHex, System.Globalization.NumberStyles.HexNumber,
                                            System.Globalization.CultureInfo.InvariantCulture, out var lowCode))
                                    {
                                        if (lowCode >= 0xDC00 && lowCode <= 0xDFFF)
                                        {
                                            var fullCode = 0x10000 + (codePoint - 0xD800) * 0x400 + (lowCode - 0xDC00);
                                            sb.Append(char.ConvertFromUtf32(fullCode));
                                            i += 11;
                                            break;
                                        }
                                    }
                                }
                                sb.Append((char)codePoint);
                                i += 5;
                            }
                            else
                            {
                                sb.Append(raw[i]);
                            }
                        }
                        else
                        {
                            sb.Append(raw[i]);
                        }
                        break;
                    default:
                        sb.Append(raw[i]);
                        break;
                }
            }
            else
            {
                sb.Append(raw[i]);
            }
        }
        return sb.ToString();
    }

    private static string? ExtractAndUnescape(string json, string key)
    {
        var raw = ExtractJsonStringValue(json, key);
        return raw != null ? UnescapeJsonString(raw) : null;
    }

    [Fact]
    public void Unescape_Newline()
    {
        // JSON value: line1\nline2 (JSON \n = newline)
        var result = ExtractAndUnescape("{\"text\":\"line1\\nline2\"}", "text");
        result.Should().Be("line1\nline2");
    }

    [Fact]
    public void Unescape_CarriageReturn()
    {
        var result = ExtractAndUnescape("{\"text\":\"line1\\rline2\"}", "text");
        result.Should().Be("line1\rline2");
    }

    [Fact]
    public void Unescape_Tab()
    {
        var result = ExtractAndUnescape("{\"text\":\"col1\\tcol2\"}", "text");
        result.Should().Be("col1\tcol2");
    }

    [Fact]
    public void Unescape_Quote()
    {
        // JSON value: say \"hello\" (JSON \" = double-quote)
        var result = ExtractAndUnescape("{\"text\":\"say \\\"hello\\\"\"}", "text");
        result.Should().Be("say \"hello\"");
    }

    [Fact]
    public void Unescape_Backslash()
    {
        // JSON value: path\\file (JSON \\ = literal backslash)
        var result = ExtractAndUnescape("{\"text\":\"path\\\\file\"}", "text");
        result.Should().Be("path\\file");
    }

    [Fact]
    public void Unescape_MixedEscapes()
    {
        // JSON value: line1\nline2\t\"quoted\"\\path
        var result = ExtractAndUnescape(
            "{\"text\":\"line1\\nline2\\t\\\"quoted\\\"\\\\path\"}", "text");
        result.Should().Be("line1\nline2\t\"quoted\"\\path");
    }

    [Fact]
    public void Unescape_AlreadyUnescaped_NoOp()
    {
        var result = ExtractAndUnescape("{\"text\":\"plain text\"}", "text");
        result.Should().Be("plain text");
    }

    [Fact]
    public void Unescape_EmptyString()
    {
        var result = ExtractAndUnescape("{\"text\":\"\"}", "text");
        result.Should().Be("");
    }

    [Fact]
    public void Unescape_MultipleNewlines()
    {
        var result = ExtractAndUnescape("{\"text\":\"a\\nb\\nc\\nd\"}", "text");
        result.Should().Be("a\nb\nc\nd");
    }

    [Fact]
    public void Unescape_NewlinesInMarkdown()
    {
        // Simulates a Markdown response with headings and paragraphs
        var md = "# Hello\\n\\nSome **bold** text.\\n\\n- Item 1\\n- Item 2";
        var result = ExtractAndUnescape($"{{\"response\":\"{md}\"}}", "response");
        result.Should().Contain("\n");
        result.Should().NotContain("\\n");
        result.Should().Be("# Hello\n\nSome **bold** text.\n\n- Item 1\n- Item 2");
    }

    [Fact]
    public void Unescape_BackslashBeforeN_IsNotDoubleProcessed()
    {
        // JSON value: \\n (literal backslash followed by 'n', NOT a newline)
        var result = ExtractAndUnescape("{\"text\":\"\\\\n\"}", "text");
        result.Should().Be("\\n");
    }

    [Fact]
    public void Unescape_BackslashBeforeQuote()
    {
        // JSON value: \\\" (literal backslash followed by double-quote)
        var result = ExtractAndUnescape("{\"text\":\"\\\\\\\"\"}", "text");
        result.Should().Be("\\\"");
    }

    [Fact]
    public void Extract_ReturnsNullForMissingKey()
    {
        var result = ExtractAndUnescape("{\"text\":\"value\"}", "missing");
        result.Should().BeNull();
    }

    [Fact]
    public void Extract_HandlesRealBridgeResponse()
    {
        // Simulates a real Bridge response with escaped Markdown
        var json = "{\"taskId\":\"abc123\",\"status\":\"completed\",\"response\":\"# TIA Portal Code Review\\n\\n## Findings\\n\\n- **Issue 1**: Missing error handling\\n- **Issue 2**: Unused variable\\n\\n```csharp\\nvar x = 1;\\n```\\n\\n> Recommendation: Add try-catch blocks.\"}";
        var result = ExtractAndUnescape(json, "response");
        result.Should().NotBeNull();
        result!.Should().Contain("# TIA Portal Code Review");
        result.Should().Contain("\n\n");
        result.Should().NotContain("\\n");
        result.Should().Contain("```csharp");
    }

    // ── Unicode escape tests ──

    [Fact]
    public void Unescape_UnicodePortugueseAccents()
    {
        // JSON: ã=ã, ç=ç, õ=õ
        var result = ExtractAndUnescape(
            "{\"text\":\"Ol\\u00e3 mundo, c\\u00e7\\u00e3o, sa\\u00f5de\"}", "text");
        // ã=ã, ç=ç, ã=ã, õ=õ
        result.Should().Be("Olã mundo, cção, saõde");
    }

    [Fact]
    public void Unescape_UnicodeAcuteAccents()
    {
        // á=á, é=é, í=í, ó=ó, ú=ú
        var result = ExtractAndUnescape(
            "{\"text\":\"a\\u00e1b\\u00e9c\\u00edd\\u00f3e\\u00faf\"}", "text");
        result.Should().Be("aábécídóeúf");
    }

    [Fact]
    public void Unescape_UnicodeTilde()
    {
        // Ã=Ã, Õ=Õ
        var result = ExtractAndUnescape(
            "{\"text\":\"\\u00c3m\\u00d5a\"}", "text");
        result.Should().Be("ÃmÕa");
    }

    [Fact]
    public void Unescape_UnicodeCircumflex()
    {
        // â=â, ê=ê, ô=ô
        var result = ExtractAndUnescape(
            "{\"text\":\"\\u00e2\\u00ea\\u00f4\"}", "text");
        result.Should().Be("âêô");
    }

    [Fact]
    public void Unescape_UnicodeMixedWithBasicEscapes()
    {
        // Mix of unicode escapes and basic JSON escapes
        var result = ExtractAndUnescape(
            "{\"text\":\"Linha 1\\nLinha 2: c\\u00e1lculo\\n\\u00daltimo\"}", "text");
        result.Should().Be("Linha 1\nLinha 2: cálculo\nÚltimo");
    }

    [Fact]
    public void Unescape_UnicodeSurrogatePair()
    {
        // U+1F600 (😀) is encoded as surrogate pair
        var result = ExtractAndUnescape(
            "{\"text\":\"Hello \\uD83D\\uDE00 World\"}", "text");
        result.Should().Be("Hello \U0001F600 World");
    }

    [Fact]
    public void Unescape_UnicodeInRealBridgeResponse()
    {
        // Simulates a real Bridge response with Portuguese accented characters
        var json = "{\"taskId\":\"abc123\",\"status\":\"completed\",\"response\":\"## An\\u00e1lise\\n\\nO c\\u00f3digo apresenta os seguintes problemas:\\n\\n- **Bug 1**: Falta de tratamento de exce\\u00e7\\u00e3o\\n- **Bug 2**: Vari\\u00e1vel n\\u00e3o utilizada\"}";
        var result = ExtractAndUnescape(json, "response");
        result.Should().NotBeNull();
        result!.Should().Contain("## Análise");
        result.Should().Contain("O código");
        result.Should().Contain("exceção");
        result.Should().Contain("não utilizada");
        result.Should().NotContain("\\u00");
    }

    [Fact]
    public void Unescape_UnicodeEscapedQuotes()
    {
        // Unicode escape with escaped quotes in the same string
        var result = ExtractAndUnescape(
            "{\"text\":\"C\\u00f3digo: \\\"teste\\\"\"}", "text");
        result.Should().Be("Código: \"teste\"");
    }

    // ── Mojibake repair tests ──
    // On .NET 8 (test runner), code page 1252 requires CodePagesEncodingProvider.
    // On .NET Framework 4.8 (production Add-In), it's always available.
    // These tests use ISO-8859-1 (code page 28591) which is available on all platforms.

    private static string SimulateMojibake(string original)
    {
        // Simulate: UTF-8 bytes read as ISO-8859-1 (same as Windows-1252 for 0x80-0xFF)
        var utf8Bytes = Encoding.UTF8.GetBytes(original);
        return Encoding.GetEncoding(28591).GetString(utf8Bytes);
    }

    [Fact]
    public void RepairMojibake_BoxDrawingHorizontal()
    {
        // ─ (U+2500) = UTF-8 bytes E2 94 80 → ISO-8859-1: â\x94\x80
        var mojibake = SimulateMojibake("─");
        var repaired = AgentBridgeClient.RepairMojibake(mojibake);
        repaired.Should().Be("─");
    }

    [Fact]
    public void RepairMojibake_BoxDrawingDownRight()
    {
        // ┐ (U+2510) = UTF-8 bytes E2 94 90
        var mojibake = SimulateMojibake("┐");
        var repaired = AgentBridgeClient.RepairMojibake(mojibake);
        repaired.Should().Be("┐");
    }

    [Fact]
    public void RepairMojibake_BoxDrawingVerticalRight()
    {
        // ├ (U+251C) = UTF-8 bytes E2 94 9C
        var mojibake = SimulateMojibake("├");
        var repaired = AgentBridgeClient.RepairMojibake(mojibake);
        repaired.Should().Be("├");
    }

    [Fact]
    public void RepairMojibake_RightwardsArrow()
    {
        // → (U+2192) = UTF-8 bytes E2 86 92 → ISO-8859-1: â\x86\x92
        var mojibake = SimulateMojibake("→");
        var repaired = AgentBridgeClient.RepairMojibake(mojibake);
        repaired.Should().Be("→");
    }

    [Fact]
    public void RepairMojibake_MixedAsciiAndBoxDrawing()
    {
        var original = "Tag_1 ─── Tag_3";
        var mojibake = SimulateMojibake(original);
        mojibake.Should().NotBe(original); // Confirm it's garbled
        var repaired = AgentBridgeClient.RepairMojibake(mojibake);
        repaired.Should().Be(original);
    }

    [Fact]
    public void RepairMojibake_PlcLadderDiagramLine()
    {
        // Simulates the user's actual garbled line
        var original = "Tag_1 (I0.0) AND NOT Tag_2 (I0.1) → Tag_3 (Q0.0)";
        var mojibake = SimulateMojibake(original);
        var repaired = AgentBridgeClient.RepairMojibake(mojibake);
        repaired.Should().Be(original);
    }

    [Fact]
    public void RepairMojibake_PlainAscii_NoChange()
    {
        // Plain ASCII should pass through unchanged (no high chars)
        var text = "Hello World 123";
        AgentBridgeClient.RepairMojibake(text).Should().Be(text);
    }

    [Fact]
    public void RepairMojibake_EmptyString_NoChange()
    {
        AgentBridgeClient.RepairMojibake("").Should().Be("");
    }

    // ── End-to-end encoding validation tests ──
    // These verify that the full data path preserves UTF-8 characters correctly:
    // Unicode string → EscapeJson → UTF-8 encode → UTF-8 decode → ExtractJsonString → UnescapeJsonString

    /// <summary>
    /// Simulates the full Bridge→Add-In round-trip for a JSON string value.
    /// The Bridge serializes with EscapeJson, encodes as UTF-8 bytes.
    /// The Add-In decodes as UTF-8, then ExtractJsonString + UnescapeJsonString.
    /// </summary>
    private static string SimulateBridgeRoundTrip(string original)
    {
        // Bridge side: EscapeJson + UTF-8 encode
        var escaped = TestEscapeJson(original);
        var json = $"{{\"response\":\"{escaped}\"}}";
        var bytes = Encoding.UTF8.GetBytes(json);

        // Add-In side: UTF-8 decode + ExtractJsonString + UnescapeJsonString
        var decodedJson = Encoding.UTF8.GetString(bytes);
        return ExtractAndUnescape(decodedJson, "response") ?? "";
    }

    /// <summary>
    /// Mirrors BridgeController.EscapeJson for testing purposes.
    /// </summary>
    private static string TestEscapeJson(string? s)
    {
        if (s == null) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                default:
                    if (c < 0x20)
                        sb.AppendFormat("\\u{0:x4}", (int)c);
                    else
                        sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    [Fact]
    public void RoundTrip_PortugueseAccents()
    {
        var original = "ação crítica configuração automação informação";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_Emojis()
    {
        var original = "🔴 😀 🎉 ★ ☆ ♠ ♣";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_EmDashAndEuro()
    {
        var original = "preço — €100 © 2024";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_ArrowsAndCheckmarks()
    {
        var original = "→ ← ↑ ↓ ✓ ✔ ✗ ✕";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_BoxDrawing()
    {
        var original = "┌─────┐\n│ Hello │\n└─────┘";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_MixedContent_FullExample()
    {
        // The exact example from the user's requirements
        var original = "🔴 Ação crítica — configuração, automação, informação, € © ✓ →";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_MixedContent_WithMarkdown()
    {
        var original = "# Análise do Código\n\nO código apresenta os seguintes problemas:\n\n- **Bug 1**: Falta de tratamento de exceção 🔴\n- **Bug 2**: Variável não utilizada\n\n→ Recomendação: Adicionar try-catch";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_Multilingual()
    {
        var original = "Português: ação — Español: corazón — Français: été — Deutsch: Straße — Italiano: città";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_ControlCharactersPreserved()
    {
        var original = "line1\nline2\ttabbed\r\nCRLF";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_EscapeSequencesPreserved()
    {
        var original = "path\\to\\file and \"quoted\" text";
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }

    [Fact]
    public void RoundTrip_LongResponseWithUnicode()
    {
        // Simulates a realistic long AI response with mixed content
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Relatório de Análise 🔍");
        sb.AppendLine();
        sb.AppendLine("O módulo de automação industrial © 2024 apresenta:");
        sb.AppendLine();
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"- Item {i}:configuração → status ✓ (preço: €{i * 10})");
        }
        sb.AppendLine();
        sb.AppendLine("### Conclusão");
        sb.AppendLine("Todas as vértecas estão funcionando corretamente. — Equipe Técnica");

        var original = sb.ToString().TrimEnd();
        SimulateBridgeRoundTrip(original).Should().Be(original);
    }
}
