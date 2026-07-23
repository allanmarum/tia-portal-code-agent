#if SIEMENS
using System;
using System.Windows;
using System.Windows.Threading;
using TiaAgent.AddIn.Diagnostics;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Displays results via MessageBox (partial-trust compatible).
/// WPF Window requires SecurityPermission(UnmanagedCode) which is unavailable in TIA Portal's sandbox.
///
/// All public methods are safe to call from any thread — they marshal to the UI thread internally.
/// </summary>
public static class AssistantPanelFactory
{
    public static void ShowResult(string action, string result)
    {
        var title = "AI Code Agent - " + action;
        RunOnUiThread(() => MessageBox.Show(result, title, MessageBoxButton.OK, MessageBoxImage.Information));
    }

    public static void ShowError(string message)
    {
        RunOnUiThread(() => MessageBox.Show(message, "AI Code Agent - Error", MessageBoxButton.OK, MessageBoxImage.Error));
    }

    public static void ShowWarning(string message)
    {
        RunOnUiThread(() => MessageBox.Show(message, "AI Code Agent - Warning", MessageBoxButton.OK, MessageBoxImage.Warning));
    }

    public static void ShowLoading(string action)
    {
        // No-op: MessageBox is synchronous, can't show loading state
    }

    /// <summary>
    /// Executes an action on the TIA Portal UI thread.
    /// If already on the UI thread, executes inline.
    /// This is critical because TIA Portal's Openness API (COM-based) requires STA thread affinity.
    /// </summary>
    private static void RunOnUiThread(Action action)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(action);
            }
            else
            {
                action();
            }
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to marshal UI call to dispatcher: {ex.Message}");
            // Fallback: try to execute directly if dispatcher is unavailable
            try { action(); } catch { /* swallow — UI thread unavailable */ }
        }
    }
}
#endif
