#if SIEMENS
using System.Windows;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Displays results via MessageBox (partial-trust compatible).
/// WPF Window requires SecurityPermission(UnmanagedCode) which is unavailable in TIA Portal's sandbox.
/// </summary>
public static class AssistantPanelFactory
{
    public static void ShowResult(string action, string result)
    {
        var title = "TIA Agent - " + action;
        MessageBox.Show(result, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public static void ShowError(string message)
    {
        MessageBox.Show(message, "TIA Agent - Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public static void ShowWarning(string message)
    {
        MessageBox.Show(message, "TIA Agent - Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public static void ShowLoading(string action)
    {
        // No-op: MessageBox is synchronous, can't show loading state
    }
}
#endif
