using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using TiaAgent.Cli.Layout;

namespace TiaAgent.Cli.Supervisor;

public sealed class SupervisorLock : IDisposable
{
    private const string MutexName = @"Local\TiaAgent.Supervisor";
    private Mutex? _mutex;
    private bool _hasMutex;

    public string InstanceId { get; }
    public string LockFilePath { get; }

    private SupervisorLock(Mutex mutex, bool hasMutex, string instanceId, string lockFilePath)
    {
        _mutex = mutex;
        _hasMutex = hasMutex;
        InstanceId = instanceId;
        LockFilePath = lockFilePath;
    }

    public static SupervisorLock Acquire(TiaAgentLayout layout)
    {
        layout.EnsureDirectoriesExist();
        var lockFilePath = Path.Combine(layout.RuntimePath, "supervisor.lock");

        Mutex? mutex = null;
        bool createdNew = false;

        try
        {
            mutex = new Mutex(false, MutexName, out createdNew);
        }
        catch
        {
            // On platform where named mutex fails, fallback to file-locking check
        }

        if (mutex != null && !createdNew)
        {
            CheckAndCleanStaleLock(lockFilePath, ref mutex, ref createdNew);
        }
        else if (mutex == null)
        {
            CheckAndCleanStaleLock(lockFilePath, ref mutex, ref createdNew);
        }

        var instanceId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Random.Shared.Next(1000, 10000);
        var currentPid = Environment.ProcessId;

        var lockData = new SupervisorLockData
        {
            InstanceId = instanceId,
            SupervisorPid = currentPid,
            StartedAt = DateTime.UtcNow.ToString("o")
        };

        ManifestStore.WriteAtomic(lockFilePath, lockData);

        return new SupervisorLock(mutex!, createdNew, instanceId, lockFilePath);
    }

    private static void CheckAndCleanStaleLock(string lockFilePath, ref Mutex? mutex, ref bool createdNew)
    {
        if (File.Exists(lockFilePath))
        {
            try
            {
                var lockData = ManifestStore.Read<SupervisorLockData>(lockFilePath);
                if (lockData != null && lockData.SupervisorPid > 0)
                {
                    try
                    {
                        var existingProc = Process.GetProcessById(lockData.SupervisorPid);
                        if (!existingProc.HasExited)
                        {
                            var procName = existingProc.ProcessName.ToLowerInvariant();
                            if (procName.Contains("tiaagent") || procName.Contains("tia-agent") || procName.Contains("powershell") || procName.Contains("pwsh") || procName.Contains("dotnet"))
                            {
                                throw new InvalidOperationException($"Another TIA Agent supervisor is already running (PID: {lockData.SupervisorPid})");
                            }
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Process is dead — stale lock
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                // Unparseable or stale lock file
            }

            // Stale lock — remove file
            try { File.Delete(lockFilePath); } catch { }

            if (mutex != null && !createdNew)
            {
                try
                {
                    mutex.Dispose();
                    mutex = new Mutex(false, MutexName, out createdNew);
                }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        if (File.Exists(LockFilePath))
        {
            try { File.Delete(LockFilePath); } catch { }
        }

        if (_mutex != null)
        {
            if (_hasMutex)
            {
                try { _mutex.ReleaseMutex(); } catch { }
            }
            try { _mutex.Dispose(); } catch { }
            _mutex = null;
        }
    }
}
