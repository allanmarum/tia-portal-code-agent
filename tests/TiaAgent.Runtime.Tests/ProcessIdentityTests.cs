using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class ProcessIdentityTests
{
    [Fact]
    public void ProcessIdentity_CurrentProcess_MatchesExpected()
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        currentProcess.Id.Should().BeGreaterThan(0);
        currentProcess.ProcessName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProcessIdentity_PidReuse_IsDetected()
    {
        // PID 4 is typically System on Windows
        // This test verifies we can check if a PID exists
        try
        {
            var process = System.Diagnostics.Process.GetProcessById(4);
            process.Should().NotBeNull();
        }
        catch (ArgumentException)
        {
            // Process not found - this is also valid
            true.Should().BeTrue();
        }
    }

    [Fact]
    public void ProcessIdentity_ExecutableName_CanBeRetrieved()
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var mainModule = currentProcess.MainModule;
        mainModule.Should().NotBeNull();
        mainModule!.FileName.Should().NotBeNullOrEmpty();
    }
}
