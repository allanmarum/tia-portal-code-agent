#if SIEMENS
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using TiaAgent.AddIn.Diagnostics;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Displays results to the user. Attempts WPF Window first; falls back to MessageBox.
///
/// WPF requires:
/// 1. UIPermission (declared in Config.xml) — for Window creation in the sandbox.
/// 2. STA thread — WPF controls must be created on an STA thread.
///
/// Threading strategy:
/// - If Application.Current.Dispatcher is available, marshal to it (TIA Portal's UI thread).
/// - Otherwise, create a dedicated STA thread for the WPF window.
/// - If both fail, fall back to MessageBox (native Win32, works on any thread).
/// </summary>
public static class AssistantPanelFactory
{
    private static bool? _wpfAvailable;

    public static void ShowResult(string action, string result)
    {
        ShowUi(result, "AI Code Agent - " + action, isWarning: false);
    }

    public static void ShowError(string message)
    {
        ShowUi(message, "AI Code Agent - Error", isWarning: false);
    }

    public static void ShowWarning(string message)
    {
        ShowUi(message, "AI Code Agent - Warning", isWarning: true);
    }

    public static void ShowLoading(string action)
    {
        // Placeholder for future async UI
    }

    private static void ShowUi(string content, string title, bool isWarning)
    {
        // Attempt 1: Use the WPF dispatcher if available (TIA Portal's UI thread).
        if (TryRunOnDispatcher(() => ShowWpfOrFallback(content, title, isWarning)))
            return;

        // Attempt 2: Current thread is STA — try WPF directly.
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            ShowWpfOrFallback(content, title, isWarning);
            return;
        }

        // Attempt 3: MTA thread with no dispatcher — create a dedicated STA thread.
        if (TryRunOnStaThread(() => ShowWpfOrFallback(content, title, isWarning)))
            return;

        // Last resort: MessageBox (always works, no WPF required).
        ShowMessageBoxFallback(content, title, isWarning);
    }

    private static void ShowWpfOrFallback(string content, string title, bool isWarning)
    {
        if (TryShowWpfWindow(content, title, isWarning))
            return;

        ShowMessageBoxFallback(content, title, isWarning);
    }

    private static void ShowMessageBoxFallback(string content, string title, bool isWarning)
    {
        AddInLogger.Info("Using MessageBox fallback.");
        var icon = isWarning ? MessageBoxImage.Warning : MessageBoxImage.Information;
        MessageBox.Show(content, title, MessageBoxButton.OK, icon);
    }

    /// <summary>
    /// Tries to run an action via Application.Current.Dispatcher.
    /// Returns true if the dispatcher was available and the action was invoked.
    /// </summary>
    private static bool TryRunOnDispatcher(Action action)
    {
        try
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                return false;

            if (dispatcher.CheckAccess())
            {
                // Already on the dispatcher thread — execute directly.
                action();
                return true;
            }

            AddInLogger.Debug($"Marshalling to dispatcher (thread {Environment.CurrentManagedThreadId})");
            dispatcher.Invoke(action);
            return true;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Dispatcher invoke failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Runs an action on a new dedicated STA thread.
    /// The thread blocks until the action completes (ShowDialog is modal).
    /// Returns true if the thread was created and the action ran.
    /// </summary>
    private static bool TryRunOnStaThread(Action action)
    {
        Exception? threadException = null;

        try
        {
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    threadException = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "TiaAgent-WpfHost";
            thread.Start();
            thread.Join();

            if (threadException != null)
            {
                AddInLogger.Warn($"STA thread action failed: {threadException.Message}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to create STA thread: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to create and show a programmatic WPF Window.
    /// Returns true if the window was shown and closed successfully.
    /// </summary>
    private static bool TryShowWpfWindow(string content, string title, bool isWarning)
    {
        try
        {
            if (_wpfAvailable == false)
                return false;

            AddInLogger.Info("Creating diagnostic WPF window.");
            AddInLogger.Info($"Window title: {title}");
            AddInLogger.Info($"Content length: {content.Length} chars");
            AddInLogger.Info($"Current thread: {Environment.CurrentManagedThreadId}, " +
                             $"apartment: {Thread.CurrentThread.GetApartmentState()}");

            // Create controls programmatically — no XAML dependency
            var headerBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0078D4")),
                Padding = new Thickness(12, 10, 12, 10)
            };

            var headerText = new TextBlock
            {
                Text = title,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold
            };
            headerBorder.Child = headerText;

            var contentBox = new TextBox
            {
                Text = content,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Margin = new Thickness(12)
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 32,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 12, 12),
                IsCancel = true
            };

            var panel = new StackPanel();
            panel.Children.Add(headerBorder);
            panel.Children.Add(contentBox);
            panel.Children.Add(closeButton);

            var window = new Window
            {
                Title = title,
                Width = 600,
                Height = 400,
                MinWidth = 400,
                MinHeight = 300,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true,
                Topmost = true,
                Content = panel
            };

            closeButton.Click += (_, __) => window.Close();

            AddInLogger.Info("WPF window created. Calling ShowDialog().");

            window.ShowDialog();

            AddInLogger.Info("WPF window closed.");

            _wpfAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            AddInLogger.Error("WPF window creation failed.", ex);

            // Only cache failure for permission errors.
            // Threading errors (InvalidOperationException) should retry — the next
            // call may be on a different thread with STA state.
            if (ex is System.InvalidOperationException)
            {
                AddInLogger.Info("Threading error — WPF will be retried on next call.");
                return false;
            }

            // Permission or assembly errors — don't retry
            _wpfAvailable = false;
            return false;
        }
    }
}
#endif
