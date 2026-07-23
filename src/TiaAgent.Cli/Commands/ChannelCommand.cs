using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TiaAgent.Cli.Layout;
using TiaAgent.Cli.Release;
using TiaAgent.Contracts.Runtime;

namespace TiaAgent.Cli.Commands;

public sealed class ChannelOptions
{
    public string? Subcommand { get; set; }
    public string? Channel { get; set; }
    public string? CustomRoot { get; set; }
    public bool Force { get; set; }
    public bool Json { get; set; }
}

public sealed class ChannelShowReport
{
    public string CurrentChannel { get; set; } = string.Empty;
    public string? ActiveVersion { get; set; }
    public string? ActiveVersionChannel { get; set; }
    public List<string> AvailableChannels { get; set; } = new();
}

public sealed class ChannelSetReport
{
    public bool Success { get; set; }
    public string PreviousChannel { get; set; } = string.Empty;
    public string NewChannel { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public static class ChannelCommand
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static int Execute(ChannelOptions options, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        var sub = (options.Subcommand ?? "show").ToLowerInvariant();
        return sub switch
        {
            "show" or "current" => HandleShow(options, stdout),
            "set" or "switch" => HandleSet(options, stdout, stderr),
            _ => HandleUnknownSubcommand(sub, stderr)
        };
    }

    private static int HandleShow(ChannelOptions options, TextWriter stdout)
    {
        var layout = new TiaAgentLayout(options.CustomRoot);
        var config = ConfigCommand.LoadConfig(layout.ConfigPath);
        var currentChannel = ChannelUtils.NormalizeChannel(config.UpdateChannel) ?? "stable";

        CurrentManifest current;
        try
        {
            current = ManifestStore.Read<CurrentManifest>(layout.CurrentManifestPath);
        }
        catch
        {
            current = new CurrentManifest();
        }

        var activeVersion = current.ActiveVersion;
        var activeVersionChannel = !string.IsNullOrWhiteSpace(activeVersion)
            ? ReleaseStore.ResolveChannel(activeVersion)
            : null;

        var report = new ChannelShowReport
        {
            CurrentChannel = currentChannel,
            ActiveVersion = string.IsNullOrWhiteSpace(activeVersion) ? null : activeVersion,
            ActiveVersionChannel = activeVersionChannel,
            AvailableChannels = ChannelUtils.ValidChannels.ToList()
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
            return 0;
        }

        stdout.WriteLine($"Update Channel: {currentChannel}");
        if (!string.IsNullOrWhiteSpace(activeVersion))
        {
            stdout.WriteLine($"Active Version: {activeVersion} (channel: {activeVersionChannel})");
        }
        else
        {
            stdout.WriteLine("Active Version: (none)");
        }
        stdout.WriteLine();
        stdout.WriteLine("Available channels: " + string.Join(", ", ChannelUtils.ValidChannels));
        stdout.WriteLine();
        stdout.WriteLine("Channel precedence (most to least stable): stable > rc > beta > alpha");
        stdout.WriteLine("  stable  — only stable releases");
        stdout.WriteLine("  rc      — release candidates and stable releases");
        stdout.WriteLine("  beta    — beta, release candidates, and stable releases");
        stdout.WriteLine("  alpha   — all prerelease and stable releases");

        return 0;
    }

    private static int HandleSet(ChannelOptions options, TextWriter stdout, TextWriter stderr)
    {
        var targetChannel = ChannelUtils.NormalizeChannel(options.Channel);
        if (targetChannel == null)
        {
            var err = $"'{options.Channel}' is not a valid channel. Valid channels: {string.Join(", ", ChannelUtils.ValidChannels)}";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new ChannelSetReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
            }
            return 1;
        }

        var layout = new TiaAgentLayout(options.CustomRoot);
        layout.EnsureDirectoriesExist();
        var config = ConfigCommand.LoadConfig(layout.ConfigPath);
        var previousChannel = ChannelUtils.NormalizeChannel(config.UpdateChannel) ?? "stable";

        if (string.Equals(previousChannel, targetChannel, StringComparison.OrdinalIgnoreCase))
        {
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new ChannelSetReport
                {
                    Success = true,
                    PreviousChannel = previousChannel,
                    NewChannel = targetChannel
                }, s_jsonOptions));
            }
            else
            {
                stdout.WriteLine($"Update channel is already set to '{targetChannel}'.");
            }
            return 0;
        }

        // Reject channel downgrades unless --force is used
        if (ChannelUtils.IsChannelDowngrade(previousChannel, targetChannel) && !options.Force)
        {
            var err = $"Changing from '{previousChannel}' to '{targetChannel}' is a channel downgrade (moving to a less stable channel). Use --force to confirm.";
            if (options.Json)
            {
                stdout.WriteLine(JsonSerializer.Serialize(new ChannelSetReport { Success = false, Error = err }, s_jsonOptions));
            }
            else
            {
                stderr.WriteLine(err);
            }
            return 1;
        }

        config.UpdateChannel = targetChannel;
        ManifestStore.WriteAtomic(layout.ConfigPath, config);

        var report = new ChannelSetReport
        {
            Success = true,
            PreviousChannel = previousChannel,
            NewChannel = targetChannel
        };

        if (options.Json)
        {
            stdout.WriteLine(JsonSerializer.Serialize(report, s_jsonOptions));
        }
        else
        {
            if (ChannelUtils.IsChannelDowngrade(previousChannel, targetChannel))
            {
                stdout.WriteLine($"WARNING: Channel changed from '{previousChannel}' to '{targetChannel}' (downgrade).");
            }
            else
            {
                stdout.WriteLine($"Update channel changed from '{previousChannel}' to '{targetChannel}'.");
            }
        }

        return 0;
    }

    private static int HandleUnknownSubcommand(string subcommand, TextWriter stderr)
    {
        stderr.WriteLine($"Unknown channel subcommand '{subcommand}'.");
        stderr.WriteLine("Valid subcommands: show, set");
        return 1;
    }
}
