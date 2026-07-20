using System;
using System.IO;
using FluentAssertions;
using TiaAgent.Bridge.Security;
using Xunit;

namespace TiaAgent.Bridge.Tests;

public class TokenProviderTests
{
    private readonly string _testDir;
    private readonly string _testTokenFile;

    public TokenProviderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "TiaAgentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
        _testTokenFile = Path.Combine(_testDir, "bridge.token");
    }

    [Fact]
    public void Token_IsNotEmpty()
    {
        var provider = new TokenProvider();
        provider.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Token_IsBase64UrlEncoded()
    {
        var provider = new TokenProvider();
        var token = provider.Token;

        token.Should().MatchRegex("^[A-Za-z0-9\\-_]+$");
    }

    [Fact]
    public void Token_IsConsistentAcrossReads()
    {
        var provider1 = new TokenProvider();
        var provider2 = new TokenProvider();

        provider1.Token.Should().Be(provider2.Token);
    }

    [Fact]
    public void Token_PersistsAcrossInstances()
    {
        var provider1 = new TokenProvider();
        File.Exists(provider1.TokenFilePath).Should().BeTrue();

        var provider2 = new TokenProvider();
        provider2.Token.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Validate_CorrectToken_ReturnsTrue()
    {
        var provider = new TokenProvider();
        provider.Validate(provider.Token).Should().BeTrue();
    }

    [Fact]
    public void Validate_WrongToken_ReturnsFalse()
    {
        var provider = new TokenProvider();
        provider.Validate("wrong-token-value").Should().BeFalse();
    }

    [Fact]
    public void Validate_NullToken_ReturnsFalse()
    {
        var provider = new TokenProvider();
        provider.Validate(null).Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyToken_ReturnsFalse()
    {
        var provider = new TokenProvider();
        provider.Validate("").Should().BeFalse();
    }

    [Fact]
    public void Validate_CaseSensitive()
    {
        var provider = new TokenProvider();
        var token = provider.Token;
        var upperToken = token.ToUpperInvariant();

        if (token != upperToken)
        {
            provider.Validate(upperToken).Should().BeFalse();
        }
    }

    [Fact]
    public void Validate_TrimmedToken_ReturnsFalse()
    {
        var provider = new TokenProvider();
        var token = provider.Token;
        provider.Validate(" " + token).Should().BeFalse();
        provider.Validate(token + " ").Should().BeFalse();
    }

    [Fact]
    public void TokenFilePath_IsInTiaAgentDirectory()
    {
        var provider = new TokenProvider();
        provider.TokenFilePath.Should().Contain("TiaAgent");
        provider.TokenFilePath.Should().EndWith("bridge.token");
    }
}
