using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TiaAgent.AddIn.Diagnostics;

/// <summary>
/// File-based logger for the TIA Portal Add-In.
/// Writes to %LOCALAPPDATA%\TiaAgent\logs\addin-YYYYMMDD.log.
///
/// Design constraints:
/// - ALL operations are best-effort: a logging failure must NEVER prevent Add-In loading.
/// - LogDir is resolved lazily to avoid TypeInitializationException from static field
///   initializer calling Environment.GetFolderPath() before EnvironmentPermission is available.
/// - The Log() method silently disables file logging on first failure and falls back to no-op.
/// - Startup() catches all exceptions and never throws.
/// </summary>
public static class AddInLogger
{
    // Lazy initialization: resolved on first Log() call, not at class load time.
    // This prevents TypeInitializationException when EnvironmentPermission is not yet granted.
    private static string? _logDir;
    private static bool _logDirResolved;
    private static bool _fileLoggingDisabled;

    private static readonly object Lock = new();

    /// <summary>
    /// Gets the log directory, resolving it lazily on first access.
    /// Returns null if the path cannot be resolved (permission denied, etc.).
    /// </summary>
    private static string? GetLogDir()
    {
        if (_logDirResolved)
            return _logDir;

        lock (Lock)
        {
            if (_logDirResolved)
                return _logDir;

            _logDirResolved = true;

            try
            {
                var localAppData = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrEmpty(localAppData))
                {
                    _logDir = Path.Combine(localAppData, "TiaAgent", "logs");
                }
            }
            catch
            {
                // EnvironmentPermission not granted — file logging will be disabled
            }

            return _logDir;
        }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warn(string message) => Log("WARN", message);
    public static void Debug(string message) => Log("DEBUG", message);

    public static void Error(string message, Exception? ex = null)
    {
        var fullMessage = ex != null ? $"{message}\n{ex}" : message;
        Log("ERROR", fullMessage);
    }

    /// <summary>
    /// Logs startup diagnostics. Must NEVER throw.
    /// </summary>
    public static void Startup()
    {
        try
        {
            LogStartupDiagnostics();
        }
        catch
        {
            // Startup diagnostics are best-effort.
            // Never prevent Add-In loading because of logging.
        }
    }

    private static void LogStartupDiagnostics()
    {
        var arch = System.IntPtr.Size == 8 ? "x64" : "x86";

        Info("=== TIA Portal Add-In Startup ===");
        Info($"Architecture: {arch}");
        Info($".NET Runtime: {System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()}");
        Info($"CLR Version: {Environment.Version}");
        Info($"OS: {Environment.OSVersion}");
        Info($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        Info($"64-bit process: {Environment.Is64BitProcess}");
        Info($"Thread apartment state: {Thread.CurrentThread.GetApartmentState()}");
        Info($"Thread ID: {Environment.CurrentManagedThreadId}");

        // Log process info (requires EnvironmentPermission in some sandbox configurations)
        try
        {
            var process = Process.GetCurrentProcess();
            Info($"Process: {process.ProcessName} (PID {process.Id})");
        }
        catch (Exception ex)
        {
            Warn($"Could not read process info: {ex.Message}");
        }

        // Log WPF assembly availability
        try
        {
            var wpfAssemblies = new[]
            {
                "PresentationFramework",
                "PresentationCore",
                "WindowsBase",
                "System.Xaml"
            };
            foreach (var name in wpfAssemblies)
            {
                try
                {
                    var asm = System.Reflection.Assembly.Load(name);
                    Info($"WPF assembly loaded: {asm.FullName} @ {asm.Location}");
                }
                catch (Exception loadEx)
                {
                    Warn($"WPF assembly '{name}' not loaded: {loadEx.Message}");
                }
            }
        }
        catch (Exception asmEx)
        {
            Warn($"Failed to enumerate WPF assemblies: {asmEx.Message}");
        }

        // Log critical assembly availability
        LogThirdPartyAssemblyDiagnostics();

        Info("=== Startup diagnostics complete ===");
    }

    /// <summary>
    /// Logs diagnostics for third-party assemblies that may be missing at runtime.
    /// Checks both on-disk presence and in-memory load state.
    /// </summary>
    private static void LogThirdPartyAssemblyDiagnostics()
    {
        try
        {
            Info("--- Third-party assembly diagnostics ---");

            // Check probing base directory
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            Info($"AppDomain.BaseDirectory: {baseDir ?? "(null)"}");

            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
            {
                var dlls = Directory.GetFiles(baseDir, "*.dll");
                Info($"DLLs in base directory: {dlls.Length}");
                foreach (var dll in dlls)
                {
                    var fileName = Path.GetFileName(dll);
                    Info($"  {fileName}");
                }
            }

            // Check critical assemblies: on-disk presence + in-memory load state
            var criticalAssemblies = new[]
            {
                "TiaAgent.AddIn",
                "TiaAgent.Contracts"
            };

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var name in criticalAssemblies)
            {
                // Check if already loaded in the AppDomain
                var loaded = Array.Find(loadedAssemblies, a =>
                    a.GetName().Name == name);

                if (loaded != null)
                {
                    Info($"Assembly '{name}' loaded: {loaded.FullName} @ {loaded.Location}");
                }
                else
                {
                    // Check if the DLL exists on disk in the base directory
                    var dllPath = !string.IsNullOrEmpty(baseDir)
                        ? Path.Combine(baseDir, name + ".dll")
                        : null;

                    if (dllPath != null && File.Exists(dllPath))
                    {
                        Warn($"Assembly '{name}' NOT loaded but DLL exists at {dllPath}");
                    }
                    else
                    {
                        Warn($"Assembly '{name}' NOT loaded and DLL NOT found at {dllPath ?? "(no base dir)"}");
                    }
                }
            }

            Info("--- Third-party assembly diagnostics complete ---");
        }
        catch (Exception ex)
        {
            Warn($"Third-party assembly diagnostics failed (best-effort): {ex.Message}");
        }
    }

    /// <summary>
    /// Writes a log entry. On first file I/O failure, silently disables file logging
    /// for the rest of the session (no recurring failures).
    /// Never throws.
    /// </summary>
    private static void Log(string level, string message)
    {
        if (_fileLoggingDisabled)
            return;

        try
        {
            var dir = GetLogDir();
            if (dir == null)
            {
                _fileLoggingDisabled = true;
                return;
            }

            lock (Lock)
            {
                try
                {
                    if (!Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var logFile = Path.Combine(dir,
                        $"addin-{DateTime.Now:yyyyMMdd}.log");

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var threadId = Environment.CurrentManagedThreadId;
                    var entry = $"[{timestamp}] [{level}] [T{threadId}] {message}";

                    File.AppendAllText(logFile, entry + Environment.NewLine);
                }
                catch
                {
                    // First file I/O failure: disable file logging permanently.
                    // This prevents repeated SecurityException / IOException spam.
                    _fileLoggingDisabled = true;
                }
            }
        }
        catch
        {
            // Catch-all: logging must never crash the Add-In.
        }
    }
}
