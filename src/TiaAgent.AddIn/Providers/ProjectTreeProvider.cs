#if SIEMENS
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
using TiaAgent.AddIn.Bridge;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.AddIn.Ui;
using TiaAgent.Contracts.Bridge;

namespace TiaAgent.AddIn.Providers;

public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    private readonly TiaPortal _tiaPortal;

    public ProjectTreeProvider(TiaPortal tiaPortal)
    {
        _tiaPortal = tiaPortal;
        AddInLogger.Info("ProjectTreeProvider created");
    }

    protected override System.Collections.Generic.IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        AddInLogger.Info("GetContextMenuAddIns called");
        yield return new TiaAgentContextMenu(_tiaPortal);
        yield return new TiaAgentTestContextMenu(_tiaPortal);
    }
}

public sealed class TiaAgentContextMenu : ContextMenuAddIn
{
    private readonly TiaPortal _tiaPortal;

    public TiaAgentContextMenu(TiaPortal tiaPortal) : base("AI Assistant")
    {
        _tiaPortal = tiaPortal;
    }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        var aiSubmenu = addInRoot.Items.AddSubmenu("AI Assistant");

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Explain selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("explain", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Review selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("review", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Propose change",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("propose", selection));
    }

    private void HandleAction(string action, MenuSelectionProvider<IEngineeringObject> selection)
    {
        try
        {
            AddInLogger.Info($"Action '{action}' triggered");
            var objects = selection.GetSelection();
            var enumerator = objects.GetEnumerator();
            if (!enumerator.MoveNext())
            {
                MessageBox.Show("No object selected.", "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectedObj = enumerator.Current as IEngineeringObject;
            if (selectedObj == null)
            {
                MessageBox.Show("Selected object is not a TIA engineering object.", "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var selectionSnapshot = SelectionSnapshotFactory.Create(selectedObj);

            AddInLogger.Info($"Action '{action}' on {selectionSnapshot.Name} ({selectionSnapshot.ObjectType})");

            // Fire-and-forget: run bridge call on background thread
            // TIA Portal menu callbacks must return quickly
            var correlationId = $"tia-{Guid.NewGuid():N}";
            Task.Run(() => ExecuteViaBridgeAsync(action, selectionSnapshot, correlationId));
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Action '{action}' failed", ex);
            MessageBox.Show("Error: " + ex.Message, "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteViaBridgeAsync(string action, SelectionSnapshot selection, string correlationId)
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

            var pid = Process.GetCurrentProcess().Id;
            var request = new BridgeTaskRequest
            {
                ContractVersion = "1.0",
                CorrelationId = correlationId,
                Action = action,
                AgentId = agentId,
                TiaInstance = new TiaInstanceSnapshot
                {
                    ProcessId = pid,
                    SessionId = $"addin-{pid}",
                    Version = "21.0"
                },
                Project = new ProjectSnapshot
                {
                    Id = "current",
                    Name = "Current Project",
                    Path = ""
                },
                Selection = selection,
                UserMessage = $"The user selected object \"{selection.Name}\" of type \"{selection.ObjectType}\" in TIA Portal. Please {actionDescription}."
            };

            AddInLogger.Info($"Submitting task to Bridge for '{action}' on {selection.Name} (correlationId={correlationId})");

            var accepted = await AddInServices.BridgeClient.StartTaskAsync(request, CancellationToken.None).ConfigureAwait(false);
            AddInLogger.Info($"Task accepted: taskId={accepted.TaskId}, status={accepted.Status}");

            // Poll for completion
            var config = AddInServices.Config;
            var timeout = TimeSpan.FromSeconds(config.TaskTimeoutSeconds);
            var startTime = DateTime.UtcNow;

            while (true)
            {
                if (DateTime.UtcNow - startTime > timeout)
                {
                    AddInLogger.Warn($"Task timed out after {config.TaskTimeoutSeconds}s");
                    AssistantPanelFactory.ShowError("Task timed out waiting for response.");
                    return;
                }

                await Task.Delay(config.PollingIntervalMilliseconds).ConfigureAwait(false);

                var status = await AddInServices.BridgeClient.GetTaskAsync(accepted.TaskId, CancellationToken.None).ConfigureAwait(false);

                if (status.Status == BridgeTaskStatusValues.Completed)
                {
                    AddInLogger.Info($"Task completed for '{action}' on {selection.Name}");
                    AssistantPanelFactory.ShowResult(action, status.Response ?? "No response received.");
                    return;
                }

                if (status.Status == BridgeTaskStatusValues.Failed)
                {
                    var errorMsg = status.Error?.Message ?? status.Message ?? "Unknown error";
                    AddInLogger.Warn($"Task failed for '{action}' on {selection.Name}: {errorMsg}");
                    AssistantPanelFactory.ShowError(errorMsg);
                    return;
                }

                if (status.Status == BridgeTaskStatusValues.Cancelled)
                {
                    AddInLogger.Info($"Task cancelled for '{action}' on {selection.Name}");
                    AssistantPanelFactory.ShowWarning("Task was cancelled.");
                    return;
                }
            }
        }
        catch (BridgeTaskException ex)
        {
            AddInLogger.Error($"Bridge task failed for '{action}'", ex);
            AssistantPanelFactory.ShowError("Failed to communicate with AI assistant: " + ex.Message);
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Bridge execution failed for '{action}'", ex);
            AssistantPanelFactory.ShowError("Failed to communicate with AI assistant: " + ex.Message);
        }
    }
}

public sealed class TiaAgentTestContextMenu : ContextMenuAddIn
{
    private readonly TiaPortal _tiaPortal;

    public TiaAgentTestContextMenu(TiaPortal tiaPortal) : base("TIA Agent Diagnostics")
    {
        _tiaPortal = tiaPortal;
    }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Test Integration",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
            {
                try
                {
                    AddInLogger.Info("Test Integration action triggered");

                    var version = typeof(TiaAgentTestContextMenu).Assembly.GetName().Version?.ToString() ?? "unknown";
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    var pid = System.Diagnostics.Process.GetCurrentProcess().Id;
                    var config = AddInServices.Config;

                    var msg = "TIA Portal Code Agent - Integration Test\n"
                            + "==========================================\n\n"
                            + "Status:    LOADED AND FUNCTIONAL\n"
                            + "Version:   " + version + "\n"
                            + "Timestamp: " + timestamp + "\n"
                            + "Process:   " + pid + "\n\n"
                            + "Bridge URL: " + config.BridgeBaseUrl + "\n"
                            + "Timeout:    " + config.RequestTimeoutSeconds + "s\n\n"
                            + "The Add-In is correctly installed and operational.\n"
                            + "Context menu actions are responding.\n\n"
                            + "MCP Server: Czarnak/tia-portal-mcp (via stdio)\n"
                            + "Log: " + Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "TiaAgent", "addin.log");

                    AddInLogger.Info($"Test Integration passed - v{version} PID={pid}");
                    MessageBox.Show(msg, "TIA Agent - Integration Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AddInLogger.Error("Test Integration failed", ex);
                    MessageBox.Show("Integration test failed: " + ex.Message,
                        "TIA Agent - Integration Test", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
    }
}
#endif
