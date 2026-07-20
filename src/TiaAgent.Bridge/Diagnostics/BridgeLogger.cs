using System;
using System.IO;

namespace TiaAgent.Bridge.Diagnostics;

public sealed class BridgeLogger
{
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public BridgeLogger()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var tiaAgentDir = Path.Combine(localAppData, "TiaAgent");
        Directory.CreateDirectory(tiaAgentDir);
        _logFilePath = Path.Combine(tiaAgentDir, "bridge.log");
    }

    public void Info(string message) => WriteLog("INFO", message);
    public void Warn(string message) => WriteLog("WARN", message);
    public void Error(string message, Exception? ex = null) => WriteLog("ERROR", ex != null ? $"{message}: {ex.Message}" : message);
    public void Debug(string message) => WriteLog("DEBUG", message);
    public void Startup(string message) => WriteLog("STARTUP", message);

    private void WriteLog(string level, string message)
    {
        try
        {
            var logLine = $"[{DateTime.UtcNow:O}] [{level}] {message}{Environment.NewLine}";
            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logLine);
            }
        }
        catch
        {
            // Swallow exceptions — logging must never crash the bridge
        }
    }
}
