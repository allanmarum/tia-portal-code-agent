using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Bridge;

/// <summary>
/// HTTP client for communicating with the TIA Portal Code Agent Bridge.
/// Uses System.Net.Http.HttpClient (available in net48).
/// Manual JSON serialization — no System.Text.Json dependency.
/// </summary>
public sealed class AgentBridgeClient : IAgentBridgeClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AddInConfig _config;
    private readonly bool _ownsHttpClient;

    public AgentBridgeClient(AddInConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.BridgeBaseUrl),
            Timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds)
        };
        ConfigureAuthentication(_httpClient, config);
        _ownsHttpClient = true;
    }

    public AgentBridgeClient(HttpClient httpClient, AddInConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        ConfigureAuthentication(_httpClient, config);
        _ownsHttpClient = false;
    }

    /// <summary>
    /// Reads HTTP response content as a string using explicit UTF-8 encoding.
    /// This prevents encoding corruption when the server response lacks a
    /// charset in the Content-Type header (which causes HttpClient to fall
    /// back to Latin-1, producing garbled characters like rÃ©sultat).
    /// </summary>
    private static async Task<string> ReadResponseUtf8Async(HttpResponseMessage response)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

        // Check if server declared a charset; if so, use it.
        var charset = response.Content.Headers.ContentType?.CharSet;
        if (!string.IsNullOrEmpty(charset))
        {
            try
            {
                // Normalize charset name (e.g. "utf-8" → "utf-8")
                var encoding = Encoding.GetEncoding(charset);
                var result = encoding.GetString(bytes);
                AddInLogger.Debug($"ReadResponseUtf8Async: charset='{charset}', {bytes.Length} bytes → {result.Length} chars");
                return result;
            }
            catch
            {
                // Unknown charset — fall through to UTF-8
            }
        }

        // Default: UTF-8 (the universal encoding for JSON APIs)
        var utf8Result = Encoding.UTF8.GetString(bytes);
        AddInLogger.Debug($"ReadResponseUtf8Async: charset=null (defaulting to UTF-8), {bytes.Length} bytes → {utf8Result.Length} chars");
        return utf8Result;
    }

    private void ConfigureAuthentication(HttpClient client, AddInConfig config)
    {
        if (!string.IsNullOrEmpty(config.AuthToken))
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AuthToken);
            AddInLogger.Info($"Bridge auth configured: Bearer token loaded ({TokenFingerprint(config.AuthToken!)})");
        }
        else
        {
            AddInLogger.Warn("Bridge auth token not found — requests to Bridge will be rejected");
        }
    }

    private static string TokenFingerprint(string token)
    {
        if (string.IsNullOrEmpty(token)) return "<empty>";
        if (token.Length > 8)
            return string.Format("{0}...{1} ({2} chars)", token.Substring(0, 4), token.Substring(token.Length - 4), token.Length);
        return string.Format("{0}... ({1} chars)", token.Substring(0, 2), token.Length);
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _httpClient.Dispose();
    }

    public async Task<BridgeHealthResponse> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync("/health", cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await ReadResponseUtf8Async(response).ConfigureAwait(false);
        return ParseHealthResponse(json);
    }

    public async Task<BridgeTaskAccepted> StartTaskAsync(BridgeTaskRequest request, CancellationToken cancellationToken)
    {
        var json = BuildTaskRequestJson(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("/v1/tasks", content, cancellationToken).ConfigureAwait(false);
        var responseJson = await ReadResponseUtf8Async(response).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new BridgeTaskException($"Bridge returned {(int)response.StatusCode}: {responseJson}");

        return ParseTaskAccepted(responseJson);
    }

    public async Task<BridgeTaskStatus> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"/v1/tasks/{taskId}", cancellationToken).ConfigureAwait(false);
        var json = await ReadResponseUtf8Async(response).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new BridgeTaskException($"Bridge returned {(int)response.StatusCode}: {json}");

        return ParseTaskStatus(json);
    }

    public async Task CancelTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync($"/v1/tasks/{taskId}/cancel", null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    #region JSON Serialization (Manual)

    private string BuildTaskRequestJson(BridgeTaskRequest request)
    {
        var sb = new StringBuilder();
        sb.Append('{');

        sb.AppendFormat("\"contractVersion\":\"{0}\"", EscapeJson(request.ContractVersion));
        sb.AppendFormat(",\"correlationId\":\"{0}\"", EscapeJson(request.CorrelationId));
        sb.AppendFormat(",\"action\":\"{0}\"", EscapeJson(request.Action));
        sb.AppendFormat(",\"agentId\":\"{0}\"", EscapeJson(request.AgentId));
        sb.AppendFormat(",\"userMessage\":\"{0}\"", EscapeJson(request.UserMessage));

        if (request.TiaInstance != null)
        {
            sb.Append(",\"tiaInstance\":{");
            sb.AppendFormat("\"processId\":{0}", request.TiaInstance.ProcessId);
            sb.AppendFormat(",\"sessionId\":\"{0}\"", EscapeJson(request.TiaInstance.SessionId));
            sb.AppendFormat(",\"version\":\"{0}\"", EscapeJson(request.TiaInstance.Version));
            sb.Append('}');
        }

        if (request.Project != null)
        {
            sb.Append(",\"project\":{");
            sb.AppendFormat("\"id\":\"{0}\"", EscapeJson(request.Project.Id));
            sb.AppendFormat(",\"name\":\"{0}\"", EscapeJson(request.Project.Name));
            sb.AppendFormat(",\"path\":\"{0}\"", EscapeJson(request.Project.Path));
            sb.Append('}');
        }

        if (request.Selection != null)
        {
            sb.Append(",\"selection\":{");
            sb.AppendFormat("\"name\":\"{0}\"", EscapeJson(request.Selection.Name));
            sb.AppendFormat(",\"objectType\":\"{0}\"", EscapeJson(request.Selection.ObjectType));
            sb.AppendFormat(",\"runtimeType\":\"{0}\"", EscapeJson(request.Selection.RuntimeType));
            sb.AppendFormat(",\"plcName\":\"{0}\"", EscapeJson(request.Selection.PlcName));
            sb.AppendFormat(",\"tiaPath\":\"{0}\"", EscapeJson(request.Selection.TiaPath));
            sb.AppendFormat(",\"language\":\"{0}\"", EscapeJson(request.Selection.Language));
            sb.Append('}');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static string EscapeJson(string? value)
    {
        if (value == null) return "";
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string? ExtractJsonString(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length) return null;

        if (json[idx] == '"')
        {
            var start = idx + 1;
            // Find the closing quote, respecting escaped quotes (\")
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
            var raw = json.Substring(start, i - start);
            return UnescapeJsonString(raw);
        }

        return null;
    }

    /// <summary>
    /// Unescapes a JSON string value. Reverses the escaping applied by
    /// BridgeController.EscapeJson() during serialization.
    /// Uses character-by-character processing to avoid double-processing
    /// issues with sequential Replace calls (e.g. "\\n" → wrong result).
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
                        // \uXXXX — parse 4 hex digits as a Unicode code point.
                        // Handles supplementary plane (surrogate pairs) for full Unicode support.
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
                                            // Valid surrogate pair — combine into a single character
                                            var fullCode = 0x10000 + (codePoint - 0xD800) * 0x400 + (lowCode - 0xDC00);
                                            sb.Append(char.ConvertFromUtf32(fullCode));
                                            i += 11; // skip \uXXXX\uXXXX
                                            break;
                                        }
                                    }
                                }
                                // Basic BMP character
                                sb.Append((char)codePoint);
                                i += 5; // skip \uXXXX
                            }
                            else
                            {
                                // Invalid hex — keep literal
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

    /// <summary>
    /// Detects and repairs UTF-8 mojibake: text where UTF-8 bytes were incorrectly
    /// decoded as Windows-1252 (Latin-1), producing garbled characters like ΓöÇ instead of ─.
    ///
    /// Pattern: UTF-8 encodes U+2500..U+257F (box-drawing) as 3 bytes (E2 94/95 80..BF).
    /// When those bytes are read as Windows-1252, they become: â (0xE2) + two chars in
    /// 0x80..0xBF range. This method detects that pattern and re-encodes correctly.
    ///
    /// Used as defense-in-depth on the Add-In side. The primary fix is in ProcessRunner
    /// (StandardOutputEncoding = UTF8), but this catches residual corruption from
    /// any other encoding mis-handling in the pipeline.
    /// </summary>
    internal static string RepairMojibake(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 3)
            return text;

        // Quick check: if no chars in the 0xC0-0xFF range, no mojibake is possible.
        // UTF-8 3-byte sequences always contain bytes in 0x80-0xBF (continuation bytes),
        // and the lead byte is always ≥ 0xC0. When read as Windows-1252, the lead byte
        // maps to a char ≥ 0xC0, so this check catches all 3-byte mojibake.
        bool hasHighChars = false;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] >= 0xC0)
            {
                hasHighChars = true;
                break;
            }
        }
        if (!hasHighChars)
            return text;

        // Try to detect and repair: encode as ISO-8859-1 bytes, then decode as UTF-8.
        // If the result contains valid Unicode (no U+FFFD replacement chars) and differs
        // from the original, it was mojibake.
        // ISO-8859-1 (code page 28591) is a direct byte→char mapping for 0x00-0xFF,
        // identical to Windows-1252 for this range, and available on all .NET platforms.
        try
        {
            var latin1 = Encoding.GetEncoding(28591);

            var bytes = latin1.GetBytes(text);
            var repaired = Encoding.UTF8.GetString(bytes);

            // Validate: the repaired text should not contain replacement chars (U+FFFD)
            // and should differ from the original (meaning mojibake was detected and fixed)
            if (repaired != text && !repaired.Contains('�'))
            {
                AddInLogger.Info($"RepairMojibake: repaired mojibake ({text.Length}→{repaired.Length} chars). " +
                                 $"Preview: [{(repaired.Length > 100 ? repaired.Substring(0, 100) : repaired)}]");
                return repaired;
            }
            else if (repaired.Contains('�'))
            {
                AddInLogger.Debug($"RepairMojibake: detected high chars but UTF-8 decode produced replacement chars — not mojibake.");
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"RepairMojibake: failed: {ex.GetType().Name}: {ex.Message}");
        }

        return text;
    }

    private static int ExtractJsonInt(string json, string key, int defaultValue = 0)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return defaultValue;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return defaultValue;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length) return defaultValue;

        var start = idx;
        while (idx < json.Length && char.IsDigit(json[idx])) idx++;

        if (idx > start && int.TryParse(json.Substring(start, idx - start), out var value))
            return value;

        return defaultValue;
    }

    private static bool ExtractJsonBool(string json, string key, bool defaultValue = false)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return defaultValue;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return defaultValue;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx + 4 <= json.Length && json.Substring(idx, 4) == "true")
            return true;
        if (idx + 5 <= json.Length && json.Substring(idx, 5) == "false")
            return false;

        return defaultValue;
    }

    private BridgeHealthResponse ParseHealthResponse(string json)
    {
        return new BridgeHealthResponse
        {
            Status = ExtractJsonString(json, "status") ?? "unknown",
            BridgeVersion = ExtractJsonString(json, "bridgeVersion") ?? "unknown",
            McpConfigured = ExtractJsonBool(json, "mcpConfigured"),
            // New runtime fields (backward compatible)
            RuntimeId = ExtractJsonString(json, "runtimeId"),
            RuntimeDisplayName = ExtractJsonString(json, "runtimeDisplayName"),
            RuntimeAvailable = ExtractJsonBool(json, "runtimeAvailable"),
            RuntimeVersion = ExtractJsonString(json, "runtimeVersion"),
            // Legacy fields (map to runtime fields for backward compat)
            OpenCodeAvailable = ExtractJsonBool(json, "openCodeAvailable") || ExtractJsonBool(json, "runtimeAvailable"),
            OpenCodeVersion = ExtractJsonString(json, "openCodeVersion") ?? ExtractJsonString(json, "runtimeVersion") ?? ""
        };
    }

    private BridgeTaskAccepted ParseTaskAccepted(string json)
    {
        return new BridgeTaskAccepted
        {
            TaskId = ExtractJsonString(json, "taskId") ?? "",
            Status = ExtractJsonString(json, "status") ?? "pending",
            CorrelationId = ExtractJsonString(json, "correlationId") ?? ""
        };
    }

    private BridgeTaskStatus ParseTaskStatus(string json)
    {
        var errorJson = ExtractJsonObject(json, "error");
        BridgeError? error = null;
        if (errorJson != null)
        {
            error = new BridgeError
            {
                Code = ExtractJsonString(errorJson, "code") ?? "",
                Message = ExtractJsonString(errorJson, "message") ?? "",
                Retryable = ExtractJsonBool(errorJson, "retryable")
            };
        }

        return new BridgeTaskStatus
        {
            TaskId = ExtractJsonString(json, "taskId") ?? "",
            Status = ExtractJsonString(json, "status") ?? "",
            Stage = ExtractJsonString(json, "stage") ?? "",
            Message = ExtractJsonString(json, "message") ?? "",
            RuntimeId = ExtractJsonString(json, "runtimeId"),
            RuntimeVersion = ExtractJsonString(json, "runtimeVersion"),
            Response = RepairMojibake(ExtractJsonString(json, "response") ?? ""),
            Error = error
        };
    }

    private static string? ExtractJsonObject(string json, string key)
    {
        var search = "\"" + key + "\"";
        var idx = json.IndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        idx = json.IndexOf(':', idx + search.Length);
        if (idx < 0) return null;

        idx++;
        while (idx < json.Length && json[idx] == ' ') idx++;

        if (idx >= json.Length || json[idx] != '{') return null;

        var depth = 0;
        var start = idx;
        for (var i = idx; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return json.Substring(start, i - start + 1);
            }
        }

        return null;
    }

    #endregion
}

/// <summary>
/// Exception thrown when a Bridge task operation fails.
/// </summary>
public sealed class BridgeTaskException : Exception
{
    public BridgeTaskException(string message) : base(message) { }
    public BridgeTaskException(string message, Exception inner) : base(message, inner) { }
}
