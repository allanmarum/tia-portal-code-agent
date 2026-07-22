using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

public sealed class ProductVersionConsistencyTests
{
    private static readonly Regex ProductVersionLiteral = new(
        @"(?<![A-Za-z0-9])\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+)?(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    [Fact]
    public void DirectoryBuildProps_defines_the_single_development_default()
    {
        var root = FindRepositoryRoot();
        var document = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var versions = document.Descendants("Version").ToArray();

        versions.Should().ContainSingle();
        versions[0].Value.Should().Be("0.0.0-dev");
        versions[0].Attribute("Condition")?.Value.Should().Contain("$(Version)");
        document.Descendants("PackageVersion").Single().Value.Should().Be("$(Version)");
        document.Descendants("ProductVersion").Single().Value.Should().Be("$(Version)");
        document.Descendants("InformationalVersion").Single().Value
            .Should().Contain("$(Version)").And.Contain("$(SourceRevisionId)");
    }

    [Fact]
    public void Project_files_do_not_override_the_product_version()
    {
        var root = FindRepositoryRoot();
        var projectFiles = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var projectFile in projectFiles)
        {
            var document = XDocument.Load(projectFile);
            document.Descendants("Version").Should().BeEmpty($"{projectFile} must inherit Version from Directory.Build.props");
            document.Descendants("PackageVersion").Should().BeEmpty($"{projectFile} must inherit PackageVersion from Directory.Build.props");
            document.Descendants("ProductVersion").Should().BeEmpty($"{projectFile} must inherit ProductVersion from Directory.Build.props");
            document.Descendants("InformationalVersion").Should().BeEmpty($"{projectFile} must inherit InformationalVersion from Directory.Build.props");
        }
    }

    [Fact]
    public void Packaging_uses_the_version_template_and_msbuild_property()
    {
        var root = FindRepositoryRoot();
        var config = File.ReadAllText(Path.Combine(root, "src", "TiaAgent.AddIn", "Config.xml"));
        var buildScript = File.ReadAllText(Path.Combine(root, "build.ps1"));

        config.Should().Contain("<Version>__PRODUCT_VERSION__</Version>");
        ProductVersionLiteral.IsMatch(config).Should().BeFalse();
        buildScript.Should().Contain("/p:Version=$ProductVersion");
        buildScript.Should().Contain(".Replace(\"__PRODUCT_VERSION__\", $ProductVersion)");
        buildScript.Should().Contain("0.0.0-dev");
        buildScript.Should().NotContain("0.1.0");
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
