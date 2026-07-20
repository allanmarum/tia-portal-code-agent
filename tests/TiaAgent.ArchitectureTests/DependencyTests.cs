using System.Reflection;
using FluentAssertions;
using Xunit;

namespace TiaAgent.ArchitectureTests;

/// <summary>
/// Architecture tests enforcing dependency boundaries.
/// </summary>
public class DependencyTests
{
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
            "TiaAgent.Contracts should target netstandard2.0 for cross-framework compatibility");
    }
}
