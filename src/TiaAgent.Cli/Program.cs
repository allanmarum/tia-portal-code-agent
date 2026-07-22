using System.Reflection;

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

        Console.Error.WriteLine($"Unknown command or option: '{args[0]}'");
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
        Console.WriteLine("Options:");
        Console.WriteLine("  -v, --version    Show version information");
        Console.WriteLine("  -h, --help       Show help and usage information");
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
