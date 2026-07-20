using System;
using System.Security.Cryptography;

namespace TiaAgent.Mcp.Auth;

/// <summary>
/// Generates and manages an ephemeral bearer token for MCP server authentication.
/// A new token is generated on each server instance lifetime.
/// </summary>
public sealed class SessionTokenProvider
{
    private readonly string _token;

    public SessionTokenProvider()
    {
        _token = GenerateSecureToken();
    }

    /// <summary>
    /// Returns the current ephemeral session token.
    /// </summary>
    public string GetToken() => _token;

    /// <summary>
    /// Validates whether the supplied token matches the current session token.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool ValidateToken(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(_token),
            System.Text.Encoding.UTF8.GetBytes(token));
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        return Convert.ToBase64String(bytes);
    }
}
