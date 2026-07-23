using System;
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
            ShowVersion();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var commandArgs = args.Skip(1).ToArray();

        return command switch
        {
            "install" => HandleInstall(commandArgs),
            "uninstall" => HandleUninstall(commandArgs),
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
            if ((string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
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
            if ((string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-v", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
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

    private static void ShowVersion()
    {
        var version = GetProductVersion();
        Console.WriteLine($"tia-agent version {version}");
    }

    private static void ShowHelp()
    {
        var version = GetProductVersion();
        Console.WriteLine($"TIA Portal Code Agent CLI (tia-agent) v{version}");
        Console.WriteLine("Usage: tia-agent <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  install      Install or activate TIA Agent version");
        Console.WriteLine("  uninstall    Uninstall TIA Agent version(s)");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version    Show version information");
        Console.WriteLine("  -h, --help       Show help and usage information");
    }

    private static void ShowInstallHelp()
    {
        Console.WriteLine("Usage: tia-agent install [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version <ver>      Specify version to install");
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
        Console.WriteLine("  -v, --version <ver>      Specify version to uninstall");
        Console.WriteLine("  -a, --all                Uninstall all installed versions");
        Console.WriteLine("  -f, --force              Force removal ignoring minor cleanup errors");
        Console.WriteLine("  --custom-root <root>     Path to custom installation root directory");
        Console.WriteLine("  --user-addins-dir <dir>  Path to custom Siemens UserAddIns directory");
        Console.WriteLine("  -h, --help               Show help for uninstall command");
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
