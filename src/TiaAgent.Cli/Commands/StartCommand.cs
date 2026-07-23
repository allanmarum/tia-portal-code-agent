using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Cli.Supervisor;

namespace TiaAgent.Cli.Commands;

public sealed class StartOptions
{
    public string? CustomRoot { get; set; }
    public string? RepoRoot { get; set; }
    public string? Config { get; set; }
    public bool NoMonitor { get; set; }
    public bool Verbose { get; set; }
}

public static class StartCommand
{
    public static int Execute(StartOptions options, TextWriter? stdout = null, TextWriter? stderr = null, CancellationToken cancellationToken = default)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        return SupervisorEngine.StartAsync(options, stdout, stderr, cancellationToken).GetAwaiter().GetResult();
    }

    public static async Task<int> ExecuteAsync(StartOptions options, TextWriter? stdout = null, TextWriter? stderr = null, CancellationToken cancellationToken = default)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        return await SupervisorEngine.StartAsync(options, stdout, stderr, cancellationToken).ConfigureAwait(false);
    }
}
