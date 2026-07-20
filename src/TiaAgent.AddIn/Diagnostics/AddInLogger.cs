using System;
using System.IO;

namespace TiaAgent.AddIn.Diagnostics;

/// <summary>
/// Simple file-based diagnostic logger for the TIA Portal Add-In.
/// Writes to %LOCALAPPDATA%\TiaAgent\addin.log.
/// Thread-safe, non-blocking, swallows all exceptions.
/// </summary>
public static class AddInLogger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

    private static readonly string LogFile = Path.Combine(LogDir, "addin.log");
    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var text = ex != null ? $"{message}: {ex.Message}" : message;
        Write("ERROR", text);
    }

    public static void Startup()
    {
        Write("INFO", "=== TIA Portal Code Agent Add-In starting ===");
        Write("INFO", $"Version: {typeof(AddInLogger).Assembly.GetName().Version}");
        Write("INFO", $"PID: {System.Diagnostics.Process.GetCurrentProcess().Id}");
        Write("INFO", $".NET: {Environment.Version}");
        Write("INFO", $"OS: {Environment.OSVersion}");
    }

    private static void Write(string level, string message)
    {
        try
        {
            lock (Lock)
            {
                if (!Directory.Exists(LogDir))
                    Directory.CreateDirectory(LogDir);

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";
                File.AppendAllText(LogFile, line);
            }
        }
        catch
        {
            // Swallow — logging must never crash the Add-In
        }
    }
}
