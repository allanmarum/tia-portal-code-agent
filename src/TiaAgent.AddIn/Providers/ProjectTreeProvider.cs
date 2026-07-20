#if SIEMENS
using System;
using System.IO;
using System.Windows;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;

namespace TiaAgent.AddIn.Providers;

public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    protected override System.Collections.Generic.IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        yield return new TiaAgentContextMenu();
    }
}

public sealed class TiaAgentContextMenu : ContextMenuAddIn
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TiaAgent");

    public TiaAgentContextMenu() : base("AI Assistant") { }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        var aiSubmenu = addInRoot.Items.AddSubmenu("AI Assistant");

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Explain selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("explain", "Explain this PLC block in detail", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Review selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("review", "Review this PLC code for defects and improvements", selection));

        aiSubmenu.Items.AddActionItem<IEngineeringObject>(
            "Propose change",
            (MenuSelectionProvider<IEngineeringObject> selection) =>
                HandleAction("propose", "Propose a change to this PLC block", selection));
    }

    private void HandleAction(string action, string taskDescription, MenuSelectionProvider<IEngineeringObject> selection)
    {
        try
        {
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

            // Log the request for external processing
            EnsureLogDir();
            var requestFile = Path.Combine(LogDir, "pending_request.json");
            var request = "{\"action\":\"" + action + "\",\"object\":\"" + objName + "\",\"type\":\"" + objType + "\"}";
            File.WriteAllText(requestFile, request);

            // Show info to user
            var msg = "TIA Agent - " + action.ToUpper() + "\n\n"
                    + "Object: " + objName + "\n"
                    + "Type: " + objType + "\n\n"
                    + "To get AI analysis, run in PowerShell:\n"
                    + "  .\\scripts\\run-mcp.ps1\n\n"
                    + "The result will appear in:\n"
                    + "  " + Path.Combine(LogDir, "last_result.txt");

            MessageBox.Show(msg, "TIA Agent - " + action, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Error: " + ex.Message, "TIA Agent", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EnsureLogDir()
    {
        try { if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir); } catch { }
    }
}
#endif
