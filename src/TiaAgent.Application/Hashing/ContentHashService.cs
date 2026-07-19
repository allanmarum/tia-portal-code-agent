using System.Security.Cryptography;
using System.Text;
using TiaAgent.Contracts.Abstractions;

namespace TiaAgent.Application.Hashing;

public class ContentHashService : IContentHashService
{
    public string HashPrefix => "sha256";

    public string ComputeHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(bytes);
        var hex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        return $"{HashPrefix}:{hex}";
    }

    public bool ValidateHash(string content, string expectedHash)
    {
        var actualHash = ComputeHash(content);
        return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
