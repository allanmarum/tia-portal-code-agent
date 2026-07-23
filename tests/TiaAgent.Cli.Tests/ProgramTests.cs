using FluentAssertions;
using Xunit;

namespace TiaAgent.Cli.Tests;

public class ProgramTests
{
    private static readonly string[] UnknownArgs = ["--unknown-foo"];
    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    [InlineData("version")]
    public void Main_WithVersionOption_ReturnsZero(string option)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = Program.Main(new[] { option });
            exitCode.Should().Be(0);
            writer.ToString().Should().Contain("tia-agent version");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("help")]
    public void Main_WithHelpOption_ReturnsZero(string option)
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = Program.Main(new[] { option });
            exitCode.Should().Be(0);
            writer.ToString().Should().Contain("Usage: tia-agent");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Main_WithNoArguments_ReturnsZeroAndShowsHelp()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = Program.Main(Array.Empty<string>());
            exitCode.Should().Be(0);
            writer.ToString().Should().Contain("Usage: tia-agent");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Main_WithUnknownOption_ReturnsNonZero()
    {
        var originalError = Console.Error;
        using var writer = new StringWriter();
        Console.SetError(writer);

        try
        {
            var exitCode = Program.Main(UnknownArgs);
            exitCode.Should().NotBe(0);
            writer.ToString().Should().Contain("Unknown command or option");
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Fact]
    public void GetProductVersion_ReturnsVersionString()
    {
        var version = Program.GetProductVersion();
        version.Should().NotBeNullOrEmpty();
    }

    private static readonly string[] DoctorArgs = ["doctor"];
    private static readonly string[] ConfigListArgs = ["config", "list"];

    [Fact]
    public void Main_WithDoctorCommand_ReturnsZero()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = Program.Main(DoctorArgs);
            exitCode.Should().Be(0);
            writer.ToString().Should().Contain("Doctor Diagnostics");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Main_WithConfigCommand_ReturnsZero()
    {
        var originalOut = Console.Out;
        using var writer = new StringWriter();
        Console.SetOut(writer);

        try
        {
            var exitCode = Program.Main(ConfigListArgs);
            exitCode.Should().Be(0);
            writer.ToString().Should().Contain("Configuration File:");
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
