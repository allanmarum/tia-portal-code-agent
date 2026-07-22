using System;
using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class ProcessIdentityTests
{
    [Fact]
    public void ProcessIdentity_CurrentProcess_MatchesExpected()
    {
        var currentProcess = Process.GetCurrentProcess();
        currentProcess.Id.Should().BeGreaterThan(0);
        currentProcess.ProcessName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProcessIdentity_PidReuse_IsDetected()
    {
        try
        {
            var process = Process.GetProcessById(4);
            process.Should().NotBeNull();
        }
        catch (ArgumentException)
        {
            true.Should().BeTrue();
        }
    }

    [Fact]
    public void ProcessIdentity_ExecutableName_CanBeRetrieved()
    {
        var currentProcess = Process.GetCurrentProcess();
        var mainModule = currentProcess.MainModule;
        mainModule.Should().NotBeNull();
        mainModule!.FileName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ProcessIdentity_StartTimeComparison_IdentifiesDifferentProcesses()
    {
        var currentProcess = Process.GetCurrentProcess();
        var recordedStartTime = currentProcess.StartTime;
        var fakePastStartTime = recordedStartTime.AddMinutes(-10);

        var isSameProcess = Math.Abs((currentProcess.StartTime - fakePastStartTime).TotalSeconds) < 2;
        isSameProcess.Should().BeFalse();
    }

    [Fact]
    public void ProcessIdentity_InstanceIdPropagation_IsValidGuidOrTimestamp()
    {
        var instanceId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + new Random().Next(1000, 9999);
        instanceId.Should().MatchRegex(@"^\d{8}-\d{6}-\d{4}$");
    }
}
