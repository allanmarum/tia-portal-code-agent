#if SIEMENS
using System.Collections.Generic;
using Siemens.Engineering;
using Siemens.Engineering.AddIn;
using Siemens.Engineering.AddIn.Menu;

namespace TiaAgent.AddIn.Providers;

/// <summary>
/// TIA Portal V21 Project Tree Add-In provider.
/// TIA discovers this via assembly scanning for ProjectTreeAddInProvider subclasses.
/// </summary>
public sealed class ProjectTreeProvider : ProjectTreeAddInProvider
{
    protected override IEnumerable<ContextMenuAddIn> GetContextMenuAddIns()
    {
        yield return new TiaAgentContextMenu();
    }
}

public sealed class TiaAgentContextMenu : ContextMenuAddIn
{
    public TiaAgentContextMenu() : base("AI Assistant") { }

    protected override void BuildContextMenuItems(ContextMenuAddInRoot addInRoot)
    {
        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Explain selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) => { });

        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Review selected object",
            (MenuSelectionProvider<IEngineeringObject> selection) => { });

        addInRoot.Items.AddActionItem<IEngineeringObject>(
            "Propose change",
            (MenuSelectionProvider<IEngineeringObject> selection) => { });
    }
}
#endif
