using System;
using System.IO;
using TiaAgent.Cli.Supervisor;

namespace TiaAgent.Cli.Commands;

public sealed class StopOptions
{
    public string? CustomRoot { get; set; }
    public bool Force { get; set; }
}

public static class StopCommand
{
    public static int Execute(StopOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        return SupervisorEngine.Stop(options, stdout, stderr);
    }
}
