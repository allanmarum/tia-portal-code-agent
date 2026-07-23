using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TiaAgent.Cli.Commands;

namespace TiaAgent.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || IsHelpOption(args[0]))
        {
            ShowHelp();
            return 0;
        }

        if (IsVersionOption(args[0]))
        {
            return HandleVersion(args.Skip(1).ToArray());
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        return command switch
        {
            "install" => HandleInstall(commandArgs),
            "uninstall" => HandleUninstall(commandArgs),
            "start" or "run" => HandleStart(commandArgs),
            "stop" => HandleStop(commandArgs),
            "status" => HandleStatus(commandArgs),
            "doctor" => HandleDoctor(commandArgs),
            "config" or "configuration" => HandleConfig(commandArgs),
            "version" => HandleVersion(commandArgs),
            _ => HandleUnknown(args[0])
        };
    }

    private static int HandleInstall(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowInstallHelp();
            return 0;
        }

        var options = new InstallOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.Version = args[++i];
            }
            else if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
            {
                options.Force = true;
            }
            else if (string.Equals(arg, "--payload-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.PayloadDir = args[++i];
            }
            else if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--user-addins-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.UserAddInsDir = args[++i];
            }
            else
            {
                Console.Error.WriteLine($"Unknown option for install: '{arg}'");
                ShowInstallHelp();
                return 1;
            }
        }

        return InstallCommand.Execute(options);
    }

    private static int HandleUninstall(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowUninstallHelp();
            return 0;
        }

        var options = new UninstallOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.Version = args[++i];
            }
            else if (string.Equals(arg, "--all", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-a", StringComparison.OrdinalIgnoreCase))
            {
                options.All = true;
            }
            else if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
            {
                options.Force = true;
            }
            else if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--user-addins-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.UserAddInsDir = args[++i];
            }
            else
            {
                Console.Error.WriteLine($"Unknown option for uninstall: '{arg}'");
                ShowUninstallHelp();
                return 1;
            }
        }

        return UninstallCommand.Execute(options);
    }

    private static int HandleDoctor(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowDoctorHelp();
            return 0;
        }

        var options = new DoctorOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--user-addins-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.UserAddInsDir = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (string.Equals(arg, "--verbose", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown option for doctor: '{arg}'");
                ShowDoctorHelp();
                return 1;
            }
        }

        return DoctorCommand.Execute(options);
    }

    private static int HandleConfig(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowConfigHelp();
            return 0;
        }

        var options = new ConfigOptions();
        var positional = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (arg.StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option for config: '{arg}'");
                ShowConfigHelp();
                return 1;
            }
            else
            {
                positional.Add(arg);
            }
        }

        if (positional.Count > 0)
        {
            options.Subcommand = positional[0];
        }
        if (positional.Count > 1)
        {
            options.Key = positional[1];
        }
        if (positional.Count > 2)
        {
            options.Value = positional[2];
        }

        return ConfigCommand.Execute(options);
    }

    private static int HandleVersion(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowVersionHelp();
            return 0;
        }

        var options = new VersionOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else if (string.Equals(arg, "--verbose", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
            }
            else if (arg.StartsWith('-'))
            {
                Console.Error.WriteLine($"Unknown option for version: '{arg}'");
                ShowVersionHelp();
                return 1;
            }
        }

        return VersionCommand.Execute(options);
    }

    private static int HandleStart(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowStartHelp();
            return 0;
        }

        var options = new StartOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--config", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.Config = args[++i];
            }
            else if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--repo-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.RepoRoot = args[++i];
            }
            else if (string.Equals(arg, "--no-monitor", StringComparison.OrdinalIgnoreCase))
            {
                options.NoMonitor = true;
            }
            else if (string.Equals(arg, "--verbose", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase))
            {
                options.Verbose = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown option for start: '{arg}'");
                ShowStartHelp();
                return 1;
            }
        }

        return StartCommand.Execute(options);
    }

    private static int HandleStop(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowStopHelp();
            return 0;
        }

        var options = new StopOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--force", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
            {
                options.Force = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown option for stop: '{arg}'");
                ShowStopHelp();
                return 1;
            }
        }

        return StopCommand.Execute(options);
    }

    private static int HandleStatus(string[] args)
    {
        if (args.Any(IsHelpOption))
        {
            ShowStatusHelp();
            return 0;
        }

        var options = new StatusOptions();
        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--custom-root", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                options.CustomRoot = args[++i];
            }
            else if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                options.Json = true;
            }
            else
            {
                Console.Error.WriteLine($"Unknown option for status: '{arg}'");
                ShowStatusHelp();
                return 1;
            }
        }

        return StatusCommand.Execute(options);
    }

    private static int HandleUnknown(string arg)
    {
        Console.Error.WriteLine($"Unknown command or option: '{arg}'");
        ShowHelp();
        return 1;
    }

    private static bool IsHelpOption(string arg) =>
        string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "help", StringComparison.OrdinalIgnoreCase);

    private static bool IsVersionOption(string arg) =>
        string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(arg, "version", StringComparison.OrdinalIgnoreCase);

    private static void ShowHelp()
    {
        var version = GetProductVersion();
        Console.WriteLine($"TIA Portal Code Agent CLI (tia-agent) v{version}");
        Console.WriteLine("Usage: tia-agent <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  install        Install or activate TIA Agent version");
        Console.WriteLine("  uninstall      Uninstall TIA Agent version(s)");
        Console.WriteLine("  start          Start and monitor runtime services (alias: run)");
        Console.WriteLine("  stop           Stop runtime services");
        Console.WriteLine("  status         Show runtime status and health information");
        Console.WriteLine("  doctor         Run environment and setup diagnostics");
        Console.WriteLine("  config         View or modify user configuration settings");
        Console.WriteLine("  version        Show detailed version information");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version  Show version information");
        Console.WriteLine("  -h, --help     Show help and usage information");
    }

    private static void ShowStartHelp()
    {
        Console.WriteLine("Usage: tia-agent start [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --config <path>          Path to settings.json file");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  --repo-root <path>       Path to custom repository root directory");
        Console.WriteLine("  --no-monitor             Start services and exit after health check passes");
        Console.WriteLine("  -v, --verbose            Enable verbose output");
        Console.WriteLine("  -h, --help               Show help for start command");
    }

    private static void ShowStopHelp()
    {
        Console.WriteLine("Usage: tia-agent stop [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -f, --force              Force kill processes without waiting for graceful shutdown");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  -h, --help               Show help for stop command");
    }

    private static void ShowStatusHelp()
    {
        Console.WriteLine("Usage: tia-agent status [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --json                   Output runtime status in JSON format");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  -h, --help               Show help for status command");
    }

    private static void ShowInstallHelp()
    {
        Console.WriteLine("Usage: tia-agent install [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --version <ver>          Specify version to install");
        Console.WriteLine("  -f, --force              Force reinstallation if version exists");
        Console.WriteLine("  --payload-dir <dir>      Path to custom payload directory");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  --user-addins-dir <dir>  Path to custom Siemens UserAddIns directory");
        Console.WriteLine("  -h, --help               Show help for install command");
    }

    private static void ShowUninstallHelp()
    {
        Console.WriteLine("Usage: tia-agent uninstall [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --version <ver>          Specify version to uninstall");
        Console.WriteLine("  -a, --all                Uninstall all installed versions");
        Console.WriteLine("  -f, --force              Force removal ignoring minor cleanup errors");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  --user-addins-dir <dir>  Path to custom Siemens UserAddIns directory");
        Console.WriteLine("  -h, --help               Show help for uninstall command");
    }

    private static void ShowDoctorHelp()
    {
        Console.WriteLine("Usage: tia-agent doctor [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  --user-addins-dir <dir>  Path to custom Siemens UserAddIns directory");
        Console.WriteLine("  --json                   Output diagnostic report in JSON format");
        Console.WriteLine("  -v, --verbose            Show detailed diagnostic recommendation information");
        Console.WriteLine("  -h, --help               Show help for doctor command");
    }

    private static void ShowConfigHelp()
    {
        Console.WriteLine("Usage: tia-agent config [subcommand] [options]");
        Console.WriteLine();
        Console.WriteLine("Subcommands:");
        Console.WriteLine("  list                     Display all configuration settings (default)");
        Console.WriteLine("  get [key]                Get configuration value for key");
        Console.WriteLine("  set <key> <value>        Set configuration value for key");
        Console.WriteLine("  path                     Output configuration file path");
        Console.WriteLine("  reset                    Reset configuration to default settings");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  --json                   Output configuration in JSON format");
        Console.WriteLine("  -h, --help               Show help for config command");
    }

    private static void ShowVersionHelp()
    {
        Console.WriteLine("Usage: tia-agent version [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --verbose            Show detailed version and installation diagnostics");
        Console.WriteLine("  --json                   Output version information in JSON format");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  -h, --help               Show help for version command");
    }

    public static string GetProductVersion()
    {
        var assembly = typeof(Program).Assembly;
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVersion))
        {
            return infoVersion;
        }

        var assemblyVersion = assembly.GetName().Version?.ToString();
        return assemblyVersion ?? "0.0.0-dev";
    }
}
