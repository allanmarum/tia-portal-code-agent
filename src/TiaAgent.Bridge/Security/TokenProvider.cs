using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace TiaAgent.Bridge.Security;

public sealed class TokenProvider
{
    private readonly string _tokenFilePath;
    private readonly string _token;

    public TokenProvider()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var tiaAgentDir = Path.Combine(localAppData, "TiaAgent");
        Directory.CreateDirectory(tiaAgentDir);
        _tokenFilePath = Path.Combine(tiaAgentDir, "bridge.token");
        _token = LoadOrCreateToken();
    }

    public string Token => _token;

    public bool Validate(string? bearerToken)
    {
        if (string.IsNullOrEmpty(bearerToken))
            return false;

        var tokenBytes = Encoding.UTF8.GetBytes(_token);
        var inputBytes = Encoding.UTF8.GetBytes(bearerToken);

        if (tokenBytes.Length != inputBytes.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(tokenBytes, inputBytes);
    }

    private string LoadOrCreateToken()
    {
        try
        {
            if (File.Exists(_tokenFilePath))
            {
                var existing = File.ReadAllText(_tokenFilePath).Trim();
                if (!string.IsNullOrEmpty(existing))
                    return existing;
            }
        }
        catch { }

        var token = GenerateToken();
        try { File.WriteAllText(_tokenFilePath, token); } catch { }
        return token;
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
