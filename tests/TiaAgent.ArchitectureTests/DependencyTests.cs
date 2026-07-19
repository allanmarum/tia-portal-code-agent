using System.Reflection;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

/// <summary>
/// Architecture tests enforcing dependency boundaries.
/// Only TiaAgent.Openness may reference Siemens assemblies.
/// </summary>
public class DependencyTests
{
    [Fact]
    public void Contracts_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.ITiaProjectService).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Contracts must not reference Siemens assemblies");
    }

    [Fact]
    public void Application_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Application.Hashing.ContentHashService).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Application must not reference Siemens assemblies");
    }

    [Fact]
    public void Simulator_ShouldNotReferenceSiemens()
    {
        var assembly = typeof(TiaAgent.Simulator.SimulatorTiaProjectService).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name!.StartsWith("Siemens."),
            "TiaAgent.Simulator must not reference Siemens assemblies");
    }

    [Fact]
    public void Mcp_ShouldNotReferenceOpenness()
    {
        var assembly = typeof(TiaAgent.Mcp.Tools.TiaContextTools).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "TiaAgent.Openness",
            "TiaAgent.Mcp must not reference TiaAgent.Openness directly");
    }

    [Fact]
    public void Contracts_ShouldTargetNetStandard()
    {
        var assembly = typeof(TiaAgent.Contracts.Abstractions.ITiaProjectService).Assembly;
        var targetFramework = assembly.GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>();

        targetFramework?.FrameworkName.Should().Contain("NETStandard",
            "TiaAgent.Contracts should target netstandard2.0 for cross-framework compatibility");
    }

    [Fact]
    public void AddIn_ShouldNotReferenceSimulator()
    {
        var assembly = typeof(TiaAgent.AddIn.Placeholder).Assembly;
        var references = assembly.GetReferencedAssemblies();

        references.Should().NotContain(r =>
            r.Name == "TiaAgent.Simulator",
            "TiaAgent.AddIn must not reference TiaAgent.Simulator — use ITiaProjectService abstraction");
    }
}
