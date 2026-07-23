using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

public sealed class PayloadBundlingTests
{
    [Fact]
    public void CliCsproj_IncludesPayloadFilesForPacking()
    {
        var root = FindRepositoryRoot();
        var csprojPath = Path.Combine(root, "src", "TiaAgent.Cli", "TiaAgent.Cli.csproj");
        var csprojContent = File.ReadAllText(csprojPath);

        csprojContent.Should().Contain("tools/net8.0/any/payload/");
        csprojContent.Should().Contain("payload\\**\\*");
        csprojContent.Should().Contain("Pack=\"true\"");
    }

    [Fact]
    public void BuildScript_ContainsPayloadStagingAndVerificationLogic()
    {
        var root = FindRepositoryRoot();
        var buildScriptPath = Path.Combine(root, "build.ps1");
        var buildScriptContent = File.ReadAllText(buildScriptPath);

        buildScriptContent.Should().Contain("payload-manifest.json");
        buildScriptContent.Should().Contain("Bridge");
        buildScriptContent.Should().Contain("AddIn");
        buildScriptContent.Should().Contain("config");
        buildScriptContent.Should().Contain("notices");
        buildScriptContent.Should().Contain("Siemens.*.dll");
        buildScriptContent.Should().Contain("ZipFile");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
