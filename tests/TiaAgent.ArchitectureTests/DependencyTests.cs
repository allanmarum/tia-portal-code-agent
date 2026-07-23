using System.Reflection;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

public class DependencyTests
{
    private static Assembly LoadAddInAssembly()
    {
        // Load the AddIn assembly by path to avoid cross-framework ProjectReference (NU1702).
        // The AddIn targets net48; this test project targets net8.0.
        var baseDir = AppContext.BaseDirectory;
        var addInPath = Path.Combine(baseDir, "..", "..", "..", "..", "..",
            "src", "TiaAgent.AddIn", "bin", "Release", "net48", "TiaAgent.AddIn.dll");
        if (!File.Exists(addInPath))
        {
            addInPath = Path.Combine(baseDir, "..", "..", "..", "..", "..",
                "src", "TiaAgent.AddIn", "bin", "Debug", "net48", "TiaAgent.AddIn.dll");
        }
        return Assembly.LoadFrom(Path.GetFullPath(addInPath));
    }
    [Fact]
    public void Contracts_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Contracts must not reference Siemens assemblies");
    }

    [Fact]
    public void Application_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Application.Common.GuidIdGenerator).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Application must not reference Siemens assemblies");
    }

    [Fact]
    public void Contracts_ShouldTargetNetStandard()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        targetFramework?.FrameworkName.Should().Contain("NETStandard",
            "TiaAgent.Contracts should target netstandard2.0");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceOpenCode()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "TiaAgent.OpenCode",
            "TiaAgent.AddIn must not reference TiaAgent.OpenCode");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceApplication()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "TiaAgent.Application",
            "TiaAgent.AddIn must not reference TiaAgent.Application");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceBridge()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "TiaAgent.Bridge",
            "TiaAgent.AddIn must not reference TiaAgent.Bridge (must communicate via IAgentBridgeClient HTTP only)");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceMicrosoftExtensionsLogging()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "Microsoft.Extensions.Logging.Abstractions",
            "TiaAgent.AddIn must not reference Microsoft.Extensions.Logging.Abstractions");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceMicrosoftExtensionsDependencyInjection()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "Microsoft.Extensions.DependencyInjection.Abstractions",
            "TiaAgent.AddIn must not reference Microsoft.Extensions.DependencyInjection.Abstractions");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceMicrosoftBclAsyncInterfaces()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "Microsoft.Bcl.AsyncInterfaces",
            "TiaAgent.AddIn must not reference Microsoft.Bcl.AsyncInterfaces");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceSystemTextJson()
    {
        var assembly = LoadAddInAssembly();
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "System.Text.Json",
            "TiaAgent.AddIn must not reference System.Text.Json");
    }

    [Fact]
    public void Contracts_ShouldNotReferenceMicrosoftExtensions()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name!.StartsWith("Microsoft.Extensions."),
            "TiaAgent.Contracts must not reference Microsoft.Extensions.*");
    }

    [Fact]
    public void Contracts_ShouldNotReferenceSystemTextJson()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "System.Text.Json",
            "TiaAgent.Contracts must not reference System.Text.Json");
    }

    [Fact]
    public void Contracts_ShouldNotReferenceMicrosoftBclAsyncInterfaces()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.IClock).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().NotContain(r =>
            r.Name == "Microsoft.Bcl.AsyncInterfaces",
            "TiaAgent.Contracts must not reference Microsoft.Bcl.AsyncInterfaces");
    }

    [Fact]
    public void Bridge_ShouldReferenceContracts()
    {
        var assembly = typeof(TiaAgent.Bridge.Program).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().Contain(r =>
            r.Name == "TiaAgent.Contracts",
            "TiaAgent.Bridge should reference TiaAgent.Contracts");
    }

    [Fact]
    public void Bridge_ShouldTargetNet8()
    {
        var assembly = typeof(TiaAgent.Bridge.Program).Assembly;
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        targetFramework?.FrameworkName.Should().Contain("NET",
            "TiaAgent.Bridge should target .NET 8");
    }

    [Fact]
    public void Cli_ShouldReferenceContracts()
    {
        var assembly = typeof(TiaAgent.Cli.Program).Assembly;
        var references = assembly.GetReferencedAssemblies();
        references.Should().Contain(r =>
            r.Name == "TiaAgent.Contracts",
            "TiaAgent.Cli should reference TiaAgent.Contracts");
    }

    [Fact]
    public void Cli_ShouldTargetNet8()
    {
        var assembly = typeof(TiaAgent.Cli.Program).Assembly;
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();
        targetFramework?.FrameworkName.Should().Contain("NET",
            "TiaAgent.Cli should target .NET 8");
    }
}
