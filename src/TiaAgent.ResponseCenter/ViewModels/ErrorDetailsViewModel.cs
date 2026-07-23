using System;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TiaAgent.ResponseCenter.ViewModels;

/// <summary>
/// ViewModel for structured error presentation.
/// Sanitizes sensitive information from error details.
/// </summary>
public sealed class ErrorDetailsViewModel : INotifyPropertyChanged
{
    private string _userMessage = "";
    private string _technicalDetails = "";
    private string _correlationId = "";
    private bool _retryable;
    private bool _showTechnicalDetails;

    /// <summary>User-friendly explanation of the error.</summary>
    public string UserMessage
    {
        get => _userMessage;
        set => SetProperty(ref _userMessage, value);
    }

    /// <summary>Sanitized technical details (exception info, stack traces).</summary>
    public string TechnicalDetails
    {
        get => _technicalDetails;
        set => SetProperty(ref _technicalDetails, value);
    }

    /// <summary>Correlation ID for support tracing.</summary>
    public string CorrelationId
    {
        get => _correlationId;
        set => SetProperty(ref _correlationId, value);
    }

    /// <summary>Whether retry is a reasonable action for this error.</summary>
    public bool Retryable
    {
        get => _retryable;
        set => SetProperty(ref _retryable, value);
    }

    /// <summary>Whether to show the technical details section.</summary>
    public bool ShowTechnicalDetails
    {
        get => _showTechnicalDetails;
        set => SetProperty(ref _showTechnicalDetails, value);
    }

    /// <summary>
    /// Creates an ErrorDetailsViewModel from a Bridge error response, sanitizing sensitive data.
    /// </summary>
    public static ErrorDetailsViewModel FromBridgeError(
        string? userMessage,
        string? errorCode,
        string? technicalMessage,
        bool retryable,
        string? correlationId)
    {
        var sanitized = Sanitize(technicalMessage ?? userMessage ?? Strings.ErrorGeneric);

        return new ErrorDetailsViewModel
        {
            UserMessage = userMessage ?? MapErrorCodeToUserMessage(errorCode),
            TechnicalDetails = sanitized,
            Retryable = retryable,
            CorrelationId = correlationId ?? ""
        };
    }

    /// <summary>
    /// Sanitizes a string by removing sensitive information (tokens, passwords, API keys).
    /// </summary>
    public static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = input;

        // Remove Bearer tokens
        result = Regex.Replace(result, @"Bearer\s+[A-Za-z0-9\-._~+/]+=*", "[REDACTED]", RegexOptions.IgnoreCase);

        // Remove auth tokens in URLs
        result = Regex.Replace(result, @"(token|key|secret|password|auth)=([^&\s;]+)", "$1=[REDACTED]", RegexOptions.IgnoreCase);

        // Remove bridge.token file path contents
        result = Regex.Replace(result, @"bridge\.token[^\r\n]*", "bridge.token [REDACTED]");

        // Remove environment variable values that look like secrets
        result = Regex.Replace(result, @"([A-Z_]*(?:TOKEN|KEY|SECRET|PASSWORD|AUTH)[A-Z_]*)=([^\s;]+)", "$1=[REDACTED]", RegexOptions.IgnoreCase);

        return result;
    }

    private static string MapErrorCodeToUserMessage(string? errorCode)
    {
        return errorCode switch
        {
            "BRIDGE_BUSY" => "The AI agent is currently processing other tasks. Please try again in a moment.",
            "RUNTIME_UNAVAILABLE" => Strings.ErrorBridgeUnavailable,
            "TASK_TIMEOUT" => Strings.ErrorTaskTimeout,
            "OPENCODE_UNAVAILABLE" => "The AI agent runtime is not available. Please check that the service is running.",
            _ => Strings.ErrorGeneric
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetProperty(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
