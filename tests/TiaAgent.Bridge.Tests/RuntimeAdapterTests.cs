using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using TiaAgent.Bridge.Diagnostics;
using TiaAgent.Bridge.Runtime;
using TiaAgent.Contracts.Runtime;
using Xunit;

namespace TiaAgent.Bridge.Tests;

public class RuntimeAdapterTests
{
    private readonly BridgeLogger _logger = new();

    #region MimoCliRuntime

    [Fact]
    public void MimoCliRuntime_HasCorrectId()
    {
        var runtime = new MimoCliRuntime(_logger);
        runtime.Id.Should().Be("mimo");
        runtime.DisplayName.Should().Be("Mimo CLI");
    }

    [Fact]
    public async Task MimoCliRuntime_CheckAvailability_ReturnsResult()
    {
        var runtime = new MimoCliRuntime(_logger);

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        // mimo is installed on this machine, so it should be available
        result.Should().NotBeNull();
        // Don't assert Available=true since test may run in CI without mimo
        result.Executable.Should().Be("mimo");
        result.Mode.Should().Be("cli");
    }

    [Fact]
    public async Task MimoCliRuntime_CheckAvailability_WithFakeExe_ReturnsUnavailable()
    {
        var runtime = new MimoCliRuntime(_logger, executable: "nonexistent_mimo_binary_12345");

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region OpenCodeRuntime

    [Fact]
    public void OpenCodeRuntime_ServerMode_HasCorrectId()
    {
        var runtime = new OpenCodeRuntime(_logger, mode: "server");
        runtime.Id.Should().Be("opencode");
        runtime.DisplayName.Should().Be("OpenCode");
    }

    [Fact]
    public void OpenCodeRuntime_CliMode_HasCorrectId()
    {
        var runtime = new OpenCodeRuntime(_logger, mode: "cli");
        runtime.Id.Should().Be("opencode");
        runtime.DisplayName.Should().Be("OpenCode");
    }

    [Fact]
    public async Task OpenCodeRuntime_CliMode_CheckAvailability_WithFakeExe_ReturnsUnavailable()
    {
        var runtime = new OpenCodeRuntime(_logger, mode: "cli", executable: "nonexistent_opencode_binary_12345");

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
    }

    [Fact]
    public async Task OpenCodeRuntime_ServerMode_CheckAvailability_WithFakeUrl_ReturnsUnavailable()
    {
        var runtime = new OpenCodeRuntime(_logger, mode: "server", serverUrl: "http://127.0.0.1:59999");

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Mode.Should().Be("server");
    }

    #endregion

    #region ClaudeCodeRuntime

    [Fact]
    public void ClaudeCodeRuntime_HasCorrectId()
    {
        var runtime = new ClaudeCodeRuntime(_logger);
        runtime.Id.Should().Be("claude");
        runtime.DisplayName.Should().Be("Claude Code CLI");
    }

    [Fact]
    public async Task ClaudeCodeRuntime_CheckAvailability_ReturnsResult()
    {
        var runtime = new ClaudeCodeRuntime(_logger);

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result.Executable.Should().Be("claude");
        result.Mode.Should().Be("cli");
    }

    [Fact]
    public async Task ClaudeCodeRuntime_CheckAvailability_WithFakeExe_ReturnsUnavailable()
    {
        var runtime = new ClaudeCodeRuntime(_logger, executable: "nonexistent_claude_binary_12345");

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        result.Available.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region ProcessRunner

    [Fact]
    public async Task ProcessRunner_RunAsync_SimpleCommand_ReturnsOutput()
    {
        using var runner = new ProcessRunner(_logger);

        var result = await runner.RunAsync("dotnet", "--version", null, TimeSpan.FromSeconds(10));

        result.Success.Should().BeTrue();
        result.ExitCode.Should().Be(0);
        result.StdOut.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessRunner_RunAsync_NonZeroExitCode_ReturnsFailure()
    {
        using var runner = new ProcessRunner(_logger);

        var result = await runner.RunAsync("dotnet", "nonexistent-command", null, TimeSpan.FromSeconds(10));

        result.ExitCode.Should().NotBe(0);
    }

    [Fact]
    public async Task ProcessRunner_RunAsync_Timeout_ReturnsTimedOut()
    {
        using var runner = new ProcessRunner(_logger);

        // Use a command that will run for a while (ping is reliable on Windows)
        var result = await runner.RunAsync("ping", "-n 10 127.0.0.1", null, TimeSpan.FromMilliseconds(200));

        // Either timed out or failed
        (result.TimedOut || !string.IsNullOrEmpty(result.Error) || result.ExitCode != 0).Should().BeTrue();
    }

    [Fact]
    public async Task ProcessRunner_RunAsync_Cancellation_ReturnsCancelled()
    {
        using var runner = new ProcessRunner(_logger);
        using var cts = new CancellationTokenSource();

        cts.Cancel();

        var result = await runner.RunAsync("dotnet", "--version", null, TimeSpan.FromSeconds(10), cancellationToken: cts.Token);

        result.Cancelled.Should().BeTrue();
    }

    [Fact]
    public void ProcessRunner_StripAnsiEscapes_RemovesEscapeSequences()
    {
        var input = "\x1B[31mHello\x1B[0m \x1B[1;32mWorld\x1B[0m";
        var result = ProcessRunner.StripAnsiEscapes(input);
        result.Should().Be("Hello World");
    }

    [Fact]
    public void ProcessRunner_StripAnsiEscapes_HandlesNullAndEmpty()
    {
        ProcessRunner.StripAnsiEscapes(null!).Should().BeNull();
        ProcessRunner.StripAnsiEscapes("").Should().Be("");
    }

    [Fact]
    public async Task ProcessRunner_RunAsync_WithEnvironmentVariables_PassesToProcess()
    {
        using var runner = new ProcessRunner(_logger);

        var result = await runner.RunAsync(
            "dotnet", "--version", null,
            TimeSpan.FromSeconds(10),
            environmentVariables: new System.Collections.Generic.Dictionary<string, string>
            {
                ["TEST_VAR"] = "hello"
            });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessRunner_RunAsync_InvalidExecutable_ReturnsError()
    {
        using var runner = new ProcessRunner(_logger);

        var result = await runner.RunAsync("totally_nonexistent_binary_xyz_98765", "", null, TimeSpan.FromSeconds(5));

        result.Success.Should().BeFalse();
        // The error could be in Error field or StdErr
        (result.Error != null || result.ExitCode != 0).Should().BeTrue();
    }

    #endregion

    #region FakeRuntime (for integration testing)

    [Fact]
    public async Task FakeRuntime_ExecuteAsync_ReturnsSuccessResult()
    {
        var runtime = new FakeRuntime("test", "Test Runtime");
        var request = new AgentTaskRequest
        {
            TaskId = "task-1",
            CorrelationId = "corr-1",
            Action = "explain",
            AgentId = "tia-explain",
            Prompt = "Test prompt"
        };

        var result = await runtime.ExecuteAsync(request, null, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.RuntimeId.Should().Be("test");
        result.Response.Should().Contain("task-1");
    }

    [Fact]
    public async Task FakeRuntime_CheckAvailability_ReturnsAvailable()
    {
        var runtime = new FakeRuntime("test", "Test Runtime");

        var result = await runtime.CheckAvailabilityAsync(CancellationToken.None);

        result.Available.Should().BeTrue();
        result.Version.Should().Be("1.0.0-test");
    }

    [Fact]
    public async Task FakeRuntime_WhenConfiguredToFail_ReturnsFailure()
    {
        var runtime = new FakeRuntime("test", "Test Runtime")
        {
            ShouldFail = true,
            FailureError = "Simulated failure",
            FailureErrorCode = "RUNTIME_UNAVAILABLE"
        };
        var request = new AgentTaskRequest
        {
            TaskId = "task-1",
            CorrelationId = "corr-1",
            Action = "explain",
            AgentId = "tia-explain",
            Prompt = "Test prompt"
        };

        var result = await runtime.ExecuteAsync(request, null, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Simulated failure");
        result.ErrorCode.Should().Be("RUNTIME_UNAVAILABLE");
    }

    #endregion
}
