#if SIEMENS
using System.Windows.Controls;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Code-behind for the AI Assistant panel UserControl.
/// </summary>
public partial class AssistantPanel : UserControl
{
    public AssistantPanel()
    {
        InitializeComponent();
        DataContext = new AssistantPanelViewModel();
    }

    /// <summary>
    /// Gets the ViewModel bound to this panel.
    /// </summary>
    public AssistantPanelViewModel ViewModel => (AssistantPanelViewModel)DataContext;
}
#endif
