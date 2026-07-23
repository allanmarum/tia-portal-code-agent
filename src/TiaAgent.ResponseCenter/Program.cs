using System;
using System.Threading;
using System.Windows;
using TiaAgent.ResponseCenter.Models;
using TiaAgent.ResponseCenter.Services;
using TiaAgent.ResponseCenter.ViewModels;
using TiaAgent.ResponseCenter.Views;

namespace TiaAgent.ResponseCenter;

/// <summary>
/// Entry point for the Agent Response Center.
/// Launched by the TIA Portal Add-In with task context as CLI arguments.
/// </summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var context = ParseArguments(args);
            if (context == null)
            {
                ShowUsage();
                return 1;
            }

            // Ensure single instance per task
            var mutexName = $"TiaAgent_ResponseCenter_{context.TaskId}";
            using var mutex = new Mutex(true, mutexName, out bool createdNew);

            if (!createdNew)
            {
                // Another instance is already showing this task
                // TODO: Could implement named pipe to signal existing instance to focus
                return 0;
            }

            var app = new Application();

            var monitor = new BridgeTaskMonitor(context);
            var viewModel = new AgentResponseViewModel(context, monitor);

            var window = new AgentResponseWindow(viewModel)
            {
                Title = $"AI Code Agent — {context.ActionDisplay}"
            };

            // Start monitoring and show window
            viewModel.StartMonitoring();
            window.Show();

            app.Run(window);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to start Agent Response Center:\n\n{ex.Message}",
                "AI Code Agent — Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return 1;
        }
    }

    private static AgentResponseContext? ParseArguments(string[] args)
    {
        string? taskId = null;
        string? bridgeUrl = null;
        string? token = null;
        string? action = null;
        string? objectName = null;
        string? objectType = null;
        string? plcName = null;
        string? projectName = null;
        string? correlationId = null;
        string? initialStatus = null;
        string? initialStage = null;

        for (int i = 0; i < args.Length - 1; i++)
        {
            var key = args[i].ToLowerInvariant();
            var value = args[i + 1];
            i++;

            switch (key)
            {
                case "--task-id":
                    taskId = value;
                    break;
                case "--bridge-url":
                    bridgeUrl = value;
                    break;
                case "--token":
                    token = value;
                    break;
                case "--action":
                    action = value;
                    break;
                case "--object-name":
                    objectName = value;
                    break;
                case "--object-type":
                    objectType = value;
                    break;
                case "--plc-name":
                    plcName = value;
                    break;
                case "--project-name":
                    projectName = value;
                    break;
                case "--correlation-id":
                    correlationId = value;
                    break;
                case "--initial-status":
                    initialStatus = value;
                    break;
                case "--initial-stage":
                    initialStage = value;
                    break;
            }
        }

        if (string.IsNullOrEmpty(taskId) || string.IsNullOrEmpty(bridgeUrl) || string.IsNullOrEmpty(action))
            return null;

        return new AgentResponseContext
        {
            TaskId = taskId,
            BridgeUrl = bridgeUrl,
            AuthToken = token,
            Action = action,
            ObjectName = objectName ?? "",
            ObjectType = objectType ?? "",
            PlcName = plcName,
            ProjectName = projectName,
            CorrelationId = correlationId,
            InitialStatus = initialStatus,
            InitialStage = initialStage
        };
    }

    private static void ShowUsage()
    {
        MessageBox.Show(
            "Usage: TiaAgent.ResponseCenter.exe\n" +
            "  --task-id <id>\n" +
            "  --bridge-url <url>\n" +
            "  --action <explain|review|propose>\n" +
            "  [--token <bearer-token>]\n" +
            "  [--object-name <name>]\n" +
            "  [--object-type <type>]\n" +
            "  [--plc-name <name>]\n" +
            "  [--project-name <name>]\n" +
            "  [--correlation-id <id>]\n" +
            "  [--initial-status <status>]\n" +
            "  [--initial-stage <stage>]",
            "AI Code Agent — Usage",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
