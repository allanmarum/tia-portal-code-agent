using System;
using System.IO;

namespace TiaAgent.Cli.Layout;

/// <summary>
/// Defines the installed filesystem layout for TIA Agent.
/// Default root is %LOCALAPPDATA%\TiaAgent (or custom root for testing).
/// </summary>
public sealed class TiaAgentLayout
{
    public string RootPath { get; }

    public TiaAgentLayout(string? customRootPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customRootPath))
        {
            RootPath = customRootPath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            RootPath = Path.Combine(localAppData, "TiaAgent");
        }
    }

    public string VersionsPath => Path.Combine(RootPath, "versions");
    public string ConfigPath => Path.Combine(RootPath, "config.json");
    public string CurrentManifestPath => Path.Combine(RootPath, "current.json");
    public string InstallationsManifestPath => Path.Combine(RootPath, "installations.json");
    public string LogsPath => Path.Combine(RootPath, "logs");
    public string RuntimePath => Path.Combine(RootPath, "runtime");
    public string CachePath => Path.Combine(RootPath, "cache");

    public string GetVersionPath(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version cannot be null or empty.", nameof(version));
        }

        return Path.Combine(VersionsPath, version);
    }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(VersionsPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(RuntimePath);
        Directory.CreateDirectory(CachePath);
    }
}
