#if SIEMENS
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
using TiaAgent.AddIn.Bridge;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Providers;

public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    private readonly TiaPortal _tiaPortal;

    public ProjectTreeProvider(TiaPortal tiaPortal)
    {
        _tiaPortal = tiaPortal;
    }

    protected override System.Collections.Generic.IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        yield return new TiaAgentContextMenu(_tiaPortal);
    }
}

public sealed class TiaAgentContextMenu : ContextMenuAddIn
{
    private readonly TiaPortal _tiaPortal;

    public TiaAgentContextMenu(TiaPortal tiaPortal) : base("AI Code Agent")
    {
        _tiaPortal = tiaPortal;
    }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Explain selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("explain", selection));

        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Review selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("review", selection));

        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Propose change",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("propose", selection));
    }

    private void HandleAction(string action, MenuSelectionProvider<IEngineeringObject> selection)
    {
        try
        {
            var objects = selection.GetSelection();
            var enumerator = objects.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                MessageBox.Show("No object selected.", "AI Code Agent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedObj = enumerator.Current as IEngineeringObject;
            if (selectedObj == null)
            {
                MessageBox.Show("Selected object is not a TIA engineering object.", "AI Code Agent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Capture selection using ToString() — no reflection (avoids VerificationException)
            var selectionInfo = selectedObj.ToString() ?? "Unknown";
            var typeName = selectedObj.GetType().Name;
            var correlationId = $"tia-{Guid.NewGuid():N}";

            // Fire-and-forget: Bridge call on background thread, UI on completion
            Task.Run(() => ExecuteViaBridgeAsync(action, selectionInfo, typeName, correlationId));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error: {ex.Message}", "AI Code Agent", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteViaBridgeAsync(string action, string selectionInfo, string typeName, string correlationId)
    {
        try
        {
            var agentId = action switch
            {
                "explain" => "tia-explain",
                "review" => "tia-review",
                "propose" => "tia-change",
                _ => "tia-explain"
            };

            var actionDescription = action switch
            {
                "explain" => "explain this object",
                "review" => "review this object for issues and improvements",
                "propose" => "propose improvements to this object",
                _ => "analyze this object"
            };

            var request = new BridgeTaskRequest
            {
                ContractVersion = "1.0",
                CorrelationId = correlationId,
                Action = action,
                AgentId = agentId,
                TiaInstance = new TiaInstanceSnapshot
                {
                    ProcessId = 0,
                    SessionId = $"addin-{correlationId}",
                    Version = "21.0"
                },
                Project = new ProjectSnapshot
                {
                    Id = "current",
                    Name = "Current Project",
                    Path = ""
                },
                Selection = new SelectionSnapshot
                {
                    Name = selectionInfo,
                    ObjectType = typeName,
                    RuntimeType = "",
                    PlcName = "",
                    TiaPath = selectionInfo,
                    Language = ""
                },
                UserMessage = $"The user selected object \"{selectionInfo}\" of type \"{typeName}\" in TIA Portal. Please {actionDescription}."
            };

            var accepted = await AddInServices.BridgeClient.StartTaskAsync(request, CancellationToken.None).ConfigureAwait(false);

            // Poll for completion
            var config = AddInServices.Config;
            var timeout = TimeSpan.FromSeconds(config.TaskTimeoutSeconds);
            var startTime = DateTime.UtcNow;

            while (true)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    ShowOnUi("Task timed out waiting for response.", "Warning");
                    return;
                }

                await Task.Delay(config.PollingIntervalMilliseconds).ConfigureAwait(false);

                var status = await AddInServices.BridgeClient.GetTaskAsync(accepted.TaskId, CancellationToken.None).ConfigureAwait(false);

                if (status.Status == BridgeTaskStatusValues.Completed)
                {
                    var response = status.Response ?? "No response received.";
                    if (!string.IsNullOrEmpty(status.RuntimeId))
                    {
                        response = $"[Runtime: {status.RuntimeId}]\n\n{response}";
                    }
                    ShowOnUi(response, $"AI Code Agent - {action}");
                    return;
                }

                if (status.Status == BridgeTaskStatusValues.Failed)
                {
                    var errorMsg = status.Error?.Message ?? status.Message ?? "Unknown error";
                    ShowOnUi(errorMsg, "AI Code Agent - Error");
                    return;
                }

                if (status.Status == BridgeTaskStatusValues.Cancelled)
                {
                    ShowOnUi("Task was cancelled.", "AI Code Agent");
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Bridge execution failed for '{action}'", ex);
            ShowOnUi("Failed to communicate with AI assistant: " + ex.Message, "AI Code Agent - Error");
        }
    }

    private static void ShowOnUi(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }
}

#endif
