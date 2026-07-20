#if SIEMENS
using System.Windows;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Factory for creating and showing the AI Assistant panel.
/// Manages a singleton panel instance that persists across context menu invocations.
/// </summary>
public static class AssistantPanelFactory
{
    private static AssistantPanel? _panel;
    private static Window? _window;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the shared panel instance, creating it if necessary.
    /// </summary>
    public static AssistantPanel GetOrCreatePanel()
    {
        if (_panel == null)
        {
            lock (_lock)
            {
                _panel ??= new AssistantPanel();
            }
        }
        return _panel;
    }

    /// <summary>
    /// Shows the panel in a standalone window. Creates the window if it doesn't exist.
    /// </summary>
    public static void ShowPanel()
    {
        var panel = GetOrCreatePanel();

        if (_window == null)
        {
            lock (_lock)
            {
                if (_window == null)
                {
                    _window = new Window
                    {
                        Title = "TIA Agent - AI Assistant",
                        Content = panel,
                        Width = 600,
                        Height = 500,
                        WindowStartupLocation = WindowStartupLocation.CenterScreen
                    };
                    _window.Closed += (_, _) =>
                    {
                        lock (_lock)
                        {
                            _window = null;
                        }
                    };
                }
            }
        }

        if (!_window.IsVisible)
        {
            _window.Show();
        }
        _window.Activate();
    }

    /// <summary>
    /// Displays a successful result in the panel.
    /// </summary>
    public static void ShowResult(string action, string result)
    {
        var panel = GetOrCreatePanel();
        panel.ViewModel.ShowResult(action, result);
        ShowPanel();
    }

    /// <summary>
    /// Displays an error message in the panel.
    /// </summary>
    public static void ShowError(string message)
    {
        var panel = GetOrCreatePanel();
        panel.ViewModel.ShowError(message);
        ShowPanel();
    }

    /// <summary>
    /// Displays a warning message in the panel.
    /// </summary>
    public static void ShowWarning(string message)
    {
        var panel = GetOrCreatePanel();
        panel.ViewModel.ShowWarning(message);
        ShowPanel();
    }

    /// <summary>
    /// Shows a loading state in the panel.
    /// </summary>
    public static void ShowLoading(string action)
    {
        var panel = GetOrCreatePanel();
        panel.ViewModel.ShowLoading(action);
        ShowPanel();
    }
}
#endif
