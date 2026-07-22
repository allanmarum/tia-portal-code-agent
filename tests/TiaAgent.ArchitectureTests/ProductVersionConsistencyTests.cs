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
        document.Descendants("InformationalVersion").Single().Value.Should().Contain("$(Version)");
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
        var targets = File.ReadAllText(Path.Combine(root, "src", "TiaAgent.AddIn", "PackageAddIn.targets"));

        config.Should().Contain("<Version>__PRODUCT_VERSION__</Version>");
        ProductVersionLiteral.IsMatch(config).Should().BeFalse();

        // build.ps1 passes version to MSBuild via -p:Version argument.
        buildScript.Should().Contain("-p:Version=$ProductVersion");
        buildScript.Should().Contain("0.0.0-dev");
        buildScript.Should().NotContain("0.1.0");

        // Siemens Publisher requires numeric-only versions; the MSBuild target
        // strips the prerelease suffix before substituting the Config.xml template.
        targets.Should().Contain("PublisherVersion");
        targets.Should().Contain("Replace('__PRODUCT_VERSION__', '$(PublisherVersion)')");
    }

    [Fact]
    public void PackAddIn_never_copies_to_AppData()
    {
        var root = FindRepositoryRoot();
        var targets = File.ReadAllText(Path.Combine(root, "src", "TiaAgent.AddIn", "PackageAddIn.targets"));

        // Extract the PackAddIn target section (from "<Target Name="PackAddIn">" to "</Target>").
        var packStart = targets.IndexOf("<Target Name=\"PackAddIn\">", StringComparison.Ordinal);
        var packEnd = targets.IndexOf("</Target>", packStart, StringComparison.Ordinal);
        var packTarget = targets.Substring(packStart, packEnd - packStart);

        // PackAddIn must not reference the deploy directory (%APPDATA%).
        packTarget.Should().NotContain("AddInDeployDir",
            "PackAddIn must never copy files to %APPDATA%; only InstallAddIn may deploy");
        packTarget.Should().NotContain("APPDATA",
            "PackAddIn must never reference %APPDATA%");
    }

    [Fact]
    public void PackAddIn_uses_atomic_temp_file_pattern()
    {
        var root = FindRepositoryRoot();
        var targets = File.ReadAllText(Path.Combine(root, "src", "TiaAgent.AddIn", "PackageAddIn.targets"));

        // Extract the PackAddIn target section to verify atomicity is scoped correctly.
        var packStart = targets.IndexOf("<Target Name=\"PackAddIn\">", StringComparison.Ordinal);
        var packEnd = targets.IndexOf("</Target>", packStart, StringComparison.Ordinal);
        var packTarget = targets.Substring(packStart, packEnd - packStart);

        // The final artifact must only appear after verification.
        // This requires a temp file that is renamed after verification passes.
        packTarget.Should().Contain("AddInTempPackagePath",
            "PackAddIn must write to a temp file before the final path");

        // The temp file extension (.addin.tmp) must be defined in properties
        // so it differs from the final artifact name.
        targets.Should().Contain(".addin.tmp",
            "Temp file must use .addin.tmp extension to distinguish from final artifact");

        packTarget.Should().Contain("Move-Item",
            "Atomic finalize must use Move-Item to rename temp to final path");
        packTarget.Should().Contain("AddInPackagePath",
            "Atomic finalize must target the final artifact path");
    }

    [Fact]
    public void Config_xml_still_uses_version_template()
    {
        var root = FindRepositoryRoot();
        var config = File.ReadAllText(Path.Combine(root, "src", "TiaAgent.AddIn", "Config.xml"));

        // Config.xml must use the placeholder, never a hardcoded version.
        config.Should().Contain("__PRODUCT_VERSION__");
        ProductVersionLiteral.IsMatch(config).Should().BeFalse(
            "Config.xml must not contain a hardcoded version number");
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
