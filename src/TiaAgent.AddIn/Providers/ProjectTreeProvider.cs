#if SIEMENS
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
using TiaAgent.AddIn.Diagnostics;
using TiaAgent.AddIn.Ui;
using TiaAgent.Contracts.Abstractions;

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

            var selectedObj = enumerator.Current;
            var objName = selectedObj.ToString() ?? "Unknown";
            var objType = selectedObj.GetType().Name;

            AddInLogger.Info($"Action '{action}' on {objName} ({objType})");

            // Fire-and-forget: run orchestrator on background thread
            // TIA Portal menu callbacks must return quickly
            var correlationId = $"tia-{Guid.NewGuid():N}";
            Task.Run(() => ExecuteViaOrchestratorAsync(action, objName, objType, correlationId));
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Action '{action}' failed", ex);
            MessageBox.Show("Error: " + ex.Message, "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ExecuteViaOrchestratorAsync(string action, string objName, string objType, string correlationId)
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

            var descriptor = new OpenCodeTaskDescriptor
            {
                Action = action,
                CorrelationId = correlationId,
                TiaSessionId = "addin-session",
                ProjectId = "current",
                AgentId = agentId,
                Message = $"The user selected object \"{objName}\" of type \"{objType}\" in TIA Portal. Please {actionDescription}.",
                SelectedObject = new SelectedObjectMetadata
                {
                    Id = objName,
                    Name = objName,
                    ObjectType = objType
                }
            };

            AddInLogger.Info($"Calling orchestrator for '{action}' on {objName} (correlationId={correlationId})");

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = await AddInServices.Orchestrator.ExecuteTaskAsync(descriptor, cts.Token);

            if (result.Success)
            {
                AddInLogger.Info($"Orchestrator completed for '{action}' on {objName} ({result.Duration.TotalSeconds:F1}s, {result.ToolCalls.Count} tool calls)");
                AssistantPanelFactory.ShowResult(action, result.Response ?? "No response received.");
            }
            else
            {
                AddInLogger.Warn($"Orchestrator failed for '{action}' on {objName}: {result.Error}");
                AssistantPanelFactory.ShowError(result.Error ?? "Unknown error occurred.");
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Orchestrator execution failed for '{action}'", ex);
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

                    var msg = "TIA Portal Code Agent - Integration Test\n"
                            + "==========================================\n\n"
                            + "Status:    LOADED AND FUNCTIONAL\n"
                            + "Version:   " + version + "\n"
                            + "Timestamp: " + timestamp + "\n"
                            + "Process:   " + pid + "\n\n"
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
