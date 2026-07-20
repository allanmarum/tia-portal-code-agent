#if SIEMENS
using System;
using System.IO;
using System.Windows;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;
using TiaAgent.AddIn.Diagnostics;

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
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

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

            EnsureLogDir();
            var requestFile = Path.Combine(LogDir, "pending_request.json");
            var request = "{\"action\":\"" + action + "\",\"object\":\"" + objName + "\",\"type\":\"" + objType + "\"}";
            File.WriteAllText(requestFile, request);

            var msg = "TIA Agent - " + action.ToUpper() + "\n\n"
                    + "Object: " + objName + "\n"
                    + "Type: " + objType + "\n\n"
                    + "To get AI analysis, run in PowerShell:\n"
                    + "  .\\scripts\\run-mcp.ps1\n\n"
                    + "The result will appear in:\n"
                    + "  " + Path.Combine(LogDir, "last_result.txt");

            MessageBox.Show(msg, "TIA Agent - " + action, MessageBoxButton.OK, MessageBoxImage.Information);
            AddInLogger.Info($"Action '{action}' completed for {objName}");
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"Action '{action}' failed", ex);
            MessageBox.Show("Error: " + ex.Message, "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EnsureLogDir()
    {
        try { if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir); } catch { }
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
