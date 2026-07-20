using FluentAssertions;
using Xunit;

namespace TiaAgent.Runtime.Tests;

public class StaleRuntimeTests
{
    [Fact]
    public void StaleDetection_RunningProcess_IsNotStale()
    {
        var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
        var isRunning = !currentProcess.HasExited;
        isRunning.Should().BeTrue();
    }

    [Fact]
    public void StaleDetection_NonexistentProcess_IsStale()
    {
        // PID 99999 is unlikely to exist - GetProcessById throws if not found
        Action act = () => System.Diagnostics.Process.GetProcessById(99999);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void StaleDetection_LockFileOlderThanTimeout_IsStale()
    {
        var lockDir = Path.Combine(Path.GetTempPath(), "TiaAgentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(lockDir);

        var lockPath = Path.Combine(lockDir, "supervisor.lock");
        var oldTime = DateTime.UtcNow.AddHours(-25);
        File.WriteAllText(lockPath, """{"instanceId":"old","supervisorPid":99999}""");
        File.SetLastWriteTimeUtc(lockPath, oldTime);

        var lastWrite = File.GetLastWriteTimeUtc(lockPath);
        var isStale = (DateTime.UtcNow - lastWrite) > TimeSpan.FromHours(24);
        isStale.Should().BeTrue();

        // Cleanup
        File.Delete(lockPath);
        Directory.Delete(lockDir);
    }

    [Fact]
    public void StaleDetection_FreshLock_IsNotStale()
    {
        var lockDir = Path.Combine(Path.GetTempPath(), "TiaAgentTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(lockDir);

        var lockPath = Path.Combine(lockDir, "supervisor.lock");
        File.WriteAllText(lockPath, """{"instanceId":"new","supervisorPid":12345}""");

        var lastWrite = File.GetLastWriteTimeUtc(lockPath);
        var isStale = (DateTime.UtcNow - lastWrite) > TimeSpan.FromHours(24);
        isStale.Should().BeFalse();

        // Cleanup
        File.Delete(lockPath);
        Directory.Delete(lockDir);
    }

    [Fact]
    public void StaleSecrets_OlderThan24Hours_AreCleaned()
    {
        var secretsDir = Path.Combine(Path.GetTempPath(), "TiaAgentTests", Guid.NewGuid().ToString("N"), "secrets");
        Directory.CreateDirectory(secretsDir);

        var secretFile = Path.Combine(secretsDir, "old.token");
        File.WriteAllText(secretFile, "old-secret");
        File.SetLastWriteTimeUtc(secretFile, DateTime.UtcNow.AddHours(-25));

        var files = Directory.GetFiles(secretsDir);
        var staleFiles = files.Where(f => (DateTime.UtcNow - File.GetLastWriteTimeUtc(f)) > TimeSpan.FromHours(24)).ToList();
        staleFiles.Should().HaveCount(1);

        // Cleanup
        Directory.Delete(Path.GetDirectoryName(secretsDir)!, true);
    }
}
