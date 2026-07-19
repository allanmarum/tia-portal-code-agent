using TiaAgent.Application.Hashing;
using TiaAgent.Application.Common;
using TiaAgent.Contracts.Abstractions;
using Xunit;

namespace TiaAgent.Application.Tests;

public class ContentHashServiceTests
{
    private readonly ContentHashService _service = new();

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var content = "test content";
        var hash1 = _service.ComputeHash(content);
        var hash2 = _service.ComputeHash(content);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentContentProducesDifferentHash()
    {
        var hash1 = _service.ComputeHash("content A");
        var hash2 = _service.ComputeHash("content B");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ProducesCorrectFormat()
    {
        var hash = _service.ComputeHash("test");

        Assert.StartsWith("sha256:", hash);
        Assert.Equal(71, hash.Length); // "sha256:" (7) + 64 hex chars
    }

    [Fact]
    public void ValidateHash_ReturnsTrueForMatchingContent()
    {
        var content = "FUNCTION_BLOCK FB_Test END_FUNCTION_BLOCK";
        var hash = _service.ComputeHash(content);

        Assert.True(_service.ValidateHash(content, hash));
    }

    [Fact]
    public void ValidateHash_ReturnsFalseForDifferentContent()
    {
        var hash = _service.ComputeHash("original");

        Assert.False(_service.ValidateHash("modified", hash));
    }

    [Fact]
    public void ValidateHash_IsCaseInsensitive()
    {
        var content = "test content";
        var hash = _service.ComputeHash(content);

        Assert.True(_service.ValidateHash(content, hash.ToUpper(System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void ComputeHash_EmptyString_ProducesValidHash()
    {
        var hash = _service.ComputeHash(string.Empty);

        Assert.StartsWith("sha256:", hash);
        Assert.Equal(71, hash.Length);
    }

    [Fact]
    public void ComputeHash_UnicodeContent_ProducesDeterministicHash()
    {
        var content = "FB_Motor \u00dcberhitzungsschutz";
        var hash1 = _service.ComputeHash(content);
        var hash2 = _service.ComputeHash(content);

        Assert.Equal(hash1, hash2);
    }
}

public class GuidIdGeneratorTests
{
    private readonly GuidIdGenerator _generator = new();

    [Fact]
    public void NewId_ReturnsUniqueIds()
    {
        var id1 = _generator.NewId();
        var id2 = _generator.NewId();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void NewSessionId_HasExpectedFormat()
    {
        var sessionId = _generator.NewSessionId();

        Assert.StartsWith("tia-", sessionId);
        Assert.Contains("-", sessionId);
    }

    [Fact]
    public void NewChangeSetId_HasExpectedPrefix()
    {
        var id = _generator.NewChangeSetId();

        Assert.StartsWith("change-", id);
    }

    [Fact]
    public void NewApprovalToken_HasExpectedPrefix()
    {
        var token = _generator.NewApprovalToken();

        Assert.StartsWith("approval-", token);
    }

    [Fact]
    public void NewSelectionToken_HasExpectedPrefix()
    {
        var token = _generator.NewSelectionToken();

        Assert.StartsWith("sel-", token);
    }

    [Fact]
    public void NewToken_HasExpectedPrefix()
    {
        var token = _generator.NewToken();

        Assert.StartsWith("tok-", token);
    }

    [Fact]
    public void NewId_HasCorrectLength()
    {
        var id = _generator.NewId();

        Assert.Equal(32, id.Length); // GUID "N" format is 32 hex chars
    }
}

public class SystemClockTests
{
    [Fact]
    public void UtcNow_ReturnsCurrentTime()
    {
        var clock = new SystemClock();
        var before = DateTimeOffset.UtcNow;
        var now = clock.UtcNow;
        var after = DateTimeOffset.UtcNow;

        Assert.True(now >= before);
        Assert.True(now <= after);
    }
}
