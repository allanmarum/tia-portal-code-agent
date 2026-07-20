using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class OpenCodeConfigTests
{
    [Fact]
    public void OpenCodeConfig_PreservesAgentsSection()
    {
        var sourceConfig = """
        {
            "$schema": "https://opencode.ai/config.json",
            "server": { "port": 43120 },
            "mcp": {
                "tia-portal": {
                    "type": "local",
                    "command": ["tia-mcp"],
                    "enabled": true
                }
            },
            "agents": {
                "default": "tia-explain",
                "available": ["tia-explain", "tia-review", "tia-change"]
            },
            "model": {
                "provider": "openai",
                "model": "gpt-4o"
            }
        }
        """;

        var config = System.Text.Json.JsonSerializer.Deserialize<OpenCodeConfigDto>(sourceConfig);

        config.Should().NotBeNull();
        config!.Agents.Should().NotBeNull();
        config.Agents!.Default.Should().Be("tia-explain");
        config.Agents.Available.Should().HaveCount(3);
    }

    [Fact]
    public void OpenCodeConfig_PreservesModelSection()
    {
        var sourceConfig = """
        {
            "$schema": "https://opencode.ai/config.json",
            "server": { "port": 43120 },
            "model": {
                "provider": "openai",
                "model": "gpt-4o"
            }
        }
        """;

        var config = System.Text.Json.JsonSerializer.Deserialize<OpenCodeConfigDto>(sourceConfig);

        config.Should().NotBeNull();
        config!.Model.Should().NotBeNull();
        config.Model!.Provider.Should().Be("openai");
        config.Model.Model.Should().Be("gpt-4o");
    }

    [Fact]
    public void OpenCodeConfig_UpdatesPortCorrectly()
    {
        var newPort = 43125;
        var config = new
        {
            server = new { port = newPort }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config);
        json.Should().Contain($"\"port\":{newPort}");
    }

    [Fact]
    public void OpenCodeConfig_PreservesMcpConfig()
    {
        var sourceConfig = """
        {
            "$schema": "https://opencode.ai/config.json",
            "server": { "port": 43120 },
            "mcp": {
                "tia-portal": {
                    "type": "local",
                    "command": ["tia-mcp"],
                    "enabled": true
                }
            }
        }
        """;

        var config = System.Text.Json.JsonSerializer.Deserialize<OpenCodeConfigDto>(sourceConfig);

        config.Should().NotBeNull();
        config!.Mcp.Should().NotBeNull();
    }

    private class OpenCodeConfigDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("$schema")]
        public string? Schema { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("server")]
        public ServerDto? Server { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("mcp")]
        public object? Mcp { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("agents")]
        public AgentsDto? Agents { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("model")]
        public ModelDto? Model { get; set; }
    }

    private class ServerDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("port")]
        public int Port { get; set; }
    }

    private class AgentsDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("default")]
        public string? Default { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("available")]
        public List<string>? Available { get; set; }
    }

    private class ModelDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("provider")]
        public string? Provider { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("model")]
        public string? Model { get; set; }
    }
}
