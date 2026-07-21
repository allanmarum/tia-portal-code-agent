using System;
using System.IO;
using System.Text.Json;
using FluentAssertions;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Bridge.Tests;

public class RuntimeConfigTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BridgeLogger _logger = new();
    private static readonly JsonSerializerOptions s_jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public RuntimeConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"TiaAgentTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TiaAgentConfig_DefaultValues_AreCorrect()
    {
        var config = new TiaAgentConfig();

        config.DefaultRuntime.Should().Be("opencode");
        config.Runtimes.Should().BeEmpty();
    }

    [Fact]
    public void RuntimeEntryConfig_DefaultValues_AreCorrect()
    {
        var entry = new RuntimeEntryConfig();

        entry.Enabled.Should().BeTrue();
        entry.Executable.Should().BeNull();
        entry.Mode.Should().BeNull();
        entry.ServerUrl.Should().BeNull();
        entry.Environment.Should().BeEmpty();
    }

    [Fact]
    public void TiaAgentConfig_CanDeserialize_Json()
    {
        var json = @"{
            ""defaultRuntime"": ""claude"",
            ""runtimes"": {
                ""mimo"": {
                    ""enabled"": true,
                    ""executable"": ""mimo""
                },
                ""opencode"": {
                    ""enabled"": true,
                    ""mode"": ""server"",
                    ""serverUrl"": ""http://127.0.0.1:43120""
                },
                ""claude"": {
                    ""enabled"": true,
                    ""executable"": ""claude""
                }
            }
        }";

        var config = JsonSerializer.Deserialize<TiaAgentConfig>(json, s_jsonOptions);

        config.Should().NotBeNull();
        config!.DefaultRuntime.Should().Be("claude");
        config.Runtimes.Should().HaveCount(3);
        config.Runtimes["mimo"].Enabled.Should().BeTrue();
        config.Runtimes["mimo"].Executable.Should().Be("mimo");
        config.Runtimes["opencode"].Mode.Should().Be("server");
        config.Runtimes["opencode"].ServerUrl.Should().Be("http://127.0.0.1:43120");
        config.Runtimes["claude"].Executable.Should().Be("claude");
    }

    [Fact]
    public void TiaAgentConfig_CanSerialize_AndDeserialize()
    {
        var original = new TiaAgentConfig
        {
            DefaultRuntime = "mimo",
            Runtimes =
            {
                ["mimo"] = new RuntimeEntryConfig { Enabled = true, Executable = "mimo" },
                ["claude"] = new RuntimeEntryConfig { Enabled = false, Mode = "cli" }
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TiaAgentConfig>(json);

        deserialized.Should().NotBeNull();
        deserialized!.DefaultRuntime.Should().Be("mimo");
        deserialized.Runtimes.Should().HaveCount(2);
        deserialized.Runtimes["mimo"].Enabled.Should().BeTrue();
        deserialized.Runtimes["claude"].Enabled.Should().BeFalse();
    }

    [Fact]
    public void RuntimeConfigLoader_GetConfigPath_ReturnsValidPath()
    {
        var path = RuntimeConfigLoader.GetConfigPath();

        path.Should().Contain("TiaAgent");
        path.Should().EndWith("config.json");
    }
}
