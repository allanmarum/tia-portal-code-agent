#if SIEMENS
using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using TiaAgent.AddIn.Diagnostics;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// Displays results to the user. Attempts WPF Window first; falls back to MessageBox.
///
/// Rendering strategy:
/// 1. SimpleMarkdownFlowDocumentRenderer (dependency-free WPF) — rich formatting
/// 2. PlainTextFlowDocumentHelper (pure WPF) — monospace plain text fallback
/// 3. MessageBox (Win32) — last resort, always works
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

    // Matches "[Runtime: claude]\n\n" prefix added by ProjectTreeProvider
    private static readonly Regex s_runtimePrefixRegex = new(
        @"^\[Runtime:\s*(.+?)\]\s*\n\s*\n",
        RegexOptions.Compiled);

    public static void ShowResult(string action, string result, string? correlationId = null, string? runtimeId = null, string? targetObject = null)
    {
        ShowUi(result, "AI Code Agent - " + action, isWarning: false, correlationId: correlationId, runtimeId: runtimeId, targetObject: targetObject);
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

    private static void ShowUi(string content, string title, bool isWarning, string? correlationId = null, string? runtimeId = null, string? targetObject = null)
    {
        AddInLogger.Info($"ShowUi called: title='{title}', contentLength={content.Length}, " +
                         $"thread={Environment.CurrentManagedThreadId}, " +
                         $"apartment={Thread.CurrentThread.GetApartmentState()}, " +
                         $"wpfAvailable={_wpfAvailable}");

        // Attempt 1: Use the WPF dispatcher if available (TIA Portal's UI thread).
        AddInLogger.Debug("Attempting dispatcher path...");
        if (TryRunOnDispatcher(() => ShowWpfOrFallback(content, title, isWarning, correlationId, runtimeId, targetObject)))
        {
            AddInLogger.Info("Response displayed via dispatcher path.");
            return;
        }
        AddInLogger.Debug("Dispatcher path unavailable or failed.");

        // Attempt 2: Current thread is STA — try WPF directly.
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            AddInLogger.Debug("Current thread is STA — trying WPF directly.");
            ShowWpfOrFallback(content, title, isWarning, correlationId, runtimeId, targetObject);
            AddInLogger.Info("Response displayed via direct STA path.");
            return;
        }

        // Attempt 3: MTA thread with no dispatcher — create a dedicated STA thread.
        AddInLogger.Debug("Attempting dedicated STA thread path...");
        if (TryRunOnStaThread(() => ShowWpfOrFallback(content, title, isWarning, correlationId, runtimeId, targetObject)))
        {
            AddInLogger.Info("Response displayed via dedicated STA thread path.");
            return;
        }
        AddInLogger.Debug("STA thread path failed.");

        // Last resort: MessageBox (always works, no WPF required).
        AddInLogger.Warn("All WPF paths failed — falling back to MessageBox.");
        ShowMessageBoxFallback(content, title, isWarning);
    }

    private static void ShowWpfOrFallback(string content, string title, bool isWarning, string? correlationId = null, string? runtimeId = null, string? targetObject = null)
    {
        if (TryShowWpfWindow(content, title, isWarning, correlationId, runtimeId, targetObject))
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
            var app = Application.Current;
            if (app == null)
            {
                AddInLogger.Debug("TryRunOnDispatcher: Application.Current is null.");
                return false;
            }

            var dispatcher = app.Dispatcher;
            if (dispatcher == null)
            {
                AddInLogger.Debug("TryRunOnDispatcher: Dispatcher is null.");
                return false;
            }

            AddInLogger.Debug($"TryRunOnDispatcher: Dispatcher available, CheckAccess={dispatcher.CheckAccess()}");

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
            AddInLogger.Warn($"Dispatcher invoke failed: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                AddInLogger.Warn($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
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
            AddInLogger.Debug($"Creating dedicated STA thread (current thread: {Environment.CurrentManagedThreadId})");

            var thread = new Thread(() =>
            {
                try
                {
                    AddInLogger.Debug($"STA thread started (thread {Environment.CurrentManagedThreadId})");
                    action();
                }
                catch (Exception ex)
                {
                    threadException = ex;
                    AddInLogger.Warn($"STA thread action threw: {ex.GetType().Name}: {ex.Message}");
                    if (ex.InnerException != null)
                        AddInLogger.Warn($"  Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Name = "TiaAgent-WpfHost";
            thread.Start();
            thread.Join();

            if (threadException != null)
            {
                AddInLogger.Warn($"STA thread action failed: {threadException.GetType().Name}: {threadException.Message}");
                if (threadException.InnerException != null)
                    AddInLogger.Warn($"  Inner: {threadException.InnerException.GetType().Name}: {threadException.InnerException.Message}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            AddInLogger.Warn($"Failed to create STA thread: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Attempts to create and show a programmatic WPF Window with
    /// a FlowDocumentScrollViewer for Markdown rendering.
    /// Returns true if the window was shown and closed successfully.
    /// </summary>
    private static bool TryShowWpfWindow(string content, string title, bool isWarning,
        string? correlationId = null, string? runtimeIdParam = null, string? targetObject = null)
    {
        try
        {
            if (_wpfAvailable == false)
            {
                AddInLogger.Debug("TryShowWpfWindow: _wpfAvailable is false, skipping.");
                return false;
            }

            // Log build identifier and environment
            var assembly = typeof(AssistantPanelFactory).Assembly;
            var buildId = assembly.GetName().Version?.ToString() ?? "unknown";
            AddInLogger.Info($"=== WPF Response Window ===");
            AddInLogger.Info($"Add-In build: {buildId}");
            AddInLogger.Info($"Assembly: {assembly.FullName}");
            AddInLogger.Info($"Assembly location: {assembly.Location}");
            AddInLogger.Info($"Window title: {title}");
            AddInLogger.Info($"Content length: {content.Length} chars");
            // Log first 200 chars to diagnose encoding and content shape
            var preview = content.Length > 200 ? content.Substring(0, 200) : content;
            AddInLogger.Info($"Content preview: [{preview}]");
            AddInLogger.Info($"Current thread: {Environment.CurrentManagedThreadId}, " +
                             $"apartment: {Thread.CurrentThread.GetApartmentState()}");
            AddInLogger.Info($".NET Runtime: {System.Runtime.InteropServices.RuntimeEnvironment.GetSystemVersion()}");

            // Extract runtime info from content prefix (e.g. "[Runtime: claude]\n\n...")
            string? runtimeId = runtimeIdParam;
            var markdownContent = content;
            var runtimeMatch = s_runtimePrefixRegex.Match(content);
            if (runtimeMatch.Success)
            {
                runtimeId = runtimeId ?? runtimeMatch.Groups[1].Value;
                markdownContent = content.Substring(runtimeMatch.Length);
            }

            // ── Header ──
            var headerGrid = new System.Windows.Controls.Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerStack = new StackPanel();
            var headerTitle = new TextBlock
            {
                Text = "AI Code Agent",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold
            };
            headerStack.Children.Add(headerTitle);

            var headerSubtitle = new TextBlock
            {
                Text = title.Replace("AI Code Agent - ", "").Replace("AI Code Agent — ", ""),
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0)
            };
            headerStack.Children.Add(headerSubtitle);
            System.Windows.Controls.Grid.SetColumn(headerStack, 0);

            if (!string.IsNullOrEmpty(runtimeId))
            {
                var runtimeLabel = new TextBlock
                {
                    Text = $"Runtime: {runtimeId}",
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF)),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };
                System.Windows.Controls.Grid.SetColumn(runtimeLabel, 1);
                headerGrid.Children.Add(runtimeLabel);
            }

            headerGrid.Children.Add(headerStack);

            var headerBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0078D4")),
                Padding = new Thickness(12, 10, 12, 10),
                Child = headerGrid
            };

            // ── Metadata bar: action, target, correlation ID ──
            var metadataPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                Margin = new Thickness(12, 6, 12, 6)
            };

            var actionText = title.Replace("AI Code Agent - ", "").Replace("AI Code Agent — ", "");
            if (!string.IsNullOrEmpty(actionText))
            {
                metadataPanel.Children.Add(CreateMetadataChip($"Action: {actionText}"));
            }
            if (!string.IsNullOrEmpty(targetObject))
            {
                metadataPanel.Children.Add(CreateMetadataChip($"Target: {targetObject}"));
            }
            if (!string.IsNullOrEmpty(correlationId))
            {
                metadataPanel.Children.Add(CreateMetadataChip($"ID: {correlationId}"));
            }

            var metadataBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xF8, 0xF8, 0xF8)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0)),
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding = new Thickness(4, 4, 4, 4),
                Child = metadataPanel
            };

            // ── Content: FlowDocumentScrollViewer ──
            var viewer = new FlowDocumentScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.White,
                MinHeight = 200
            };

            // Render content into FlowDocument.
            // Strategy: SimpleMarkdownFlowDocumentRenderer → PlainText fallback.
            FlowDocument? document = null;
            string renderPath = "none";
            string rendererClass = "none";

            AddInLogger.Info($"Content empty: {string.IsNullOrWhiteSpace(markdownContent)}");

            if (!string.IsNullOrWhiteSpace(markdownContent))
            {
                try
                {
                    var renderer = new SimpleMarkdownFlowDocumentRenderer();
                    rendererClass = nameof(SimpleMarkdownFlowDocumentRenderer);
                    AddInLogger.Info($"Renderer class: {rendererClass}");

                    document = renderer.Render(markdownContent);
                    if (document != null)
                    {
                        renderPath = "markdown";
                        AddInLogger.Info($"Markdown rendered successfully. Blocks: {document.Blocks.Count}");
                    }
                    else
                    {
                        AddInLogger.Warn("Markdown renderer returned null — falling back to plain text.");
                    }
                }
                catch (Exception ex)
                {
                    AddInLogger.Warn($"Markdown rendering failed, falling back to plain text: {ex.GetType().Name}: {ex.Message}");
                    AddInLogger.Warn($"Full exception: {ex}");
                }
            }
            else
            {
                AddInLogger.Info("Markdown content is empty or whitespace — using empty placeholder.");
            }

            // Fallback: plain text WPF
            if (document == null)
            {
                document = string.IsNullOrWhiteSpace(markdownContent)
                    ? PlainTextFlowDocumentHelper.CreateEmpty()
                    : PlainTextFlowDocumentHelper.Create(markdownContent);
                renderPath = string.IsNullOrWhiteSpace(markdownContent) ? "empty" : "plain-text";
                rendererClass = document == null ? "none" : nameof(PlainTextFlowDocumentHelper);
                AddInLogger.Info($"Using {renderPath} rendering. Renderer class: {rendererClass}");
            }

            AddInLogger.Info($"Render path: {renderPath}");
            AddInLogger.Info($"Viewer control type: {viewer.GetType().FullName}");
            AddInLogger.Info($"FlowDocument blocks: {document?.Blocks.Count ?? 0}");

            viewer.Document = document;
            AddInLogger.Info($"Document assigned to {viewer.GetType().Name}.Document successfully.");

            // ── Button bar: Copy + Close ──
            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 12, 12)
            };

            var copyButton = new Button
            {
                Content = "Copy response",
                Width = 120,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0)
            };
            copyButton.Click += (_, __) =>
            {
                try
                {
                    // Get plain text from FlowDocument for clipboard
                    var textRange = new System.Windows.Documents.TextRange(
                        document!.ContentStart, document.ContentEnd);
                    System.Windows.Clipboard.SetText(textRange.Text);
                    copyButton.Content = "Copied!";
                    // Reset after 2 seconds
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (_, __2) =>
                    {
                        copyButton.Content = "Copy response";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    AddInLogger.Warn($"Copy to clipboard failed: {ex.Message}");
                    copyButton.Content = "Copy failed";
                }
            };

            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Height = 32,
                IsCancel = true
            };
            closeButton.Click += (_, __) =>
            {
                // Find and close the parent Window
                var parent = Window.GetWindow(closeButton);
                parent?.Close();
            };

            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(closeButton);

            // ── Main layout: Grid with star-sized content row ──
            var mainGrid = new System.Windows.Controls.Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // metadata
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });  // buttons

            System.Windows.Controls.Grid.SetRow(headerBorder, 0);
            System.Windows.Controls.Grid.SetRow(metadataBorder, 1);
            System.Windows.Controls.Grid.SetRow(viewer, 2);
            System.Windows.Controls.Grid.SetRow(buttonPanel, 3);

            mainGrid.Children.Add(headerBorder);
            mainGrid.Children.Add(metadataBorder);
            mainGrid.Children.Add(viewer);
            mainGrid.Children.Add(buttonPanel);

            var window = new Window
            {
                Title = title,
                Width = 700,
                Height = 600,
                MinWidth = 500,
                MinHeight = 400,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = true,
                Topmost = true,
                Content = mainGrid
            };

            AddInLogger.Info("WPF response window created. Calling ShowDialog().");

            window.ShowDialog();

            AddInLogger.Info("WPF response window closed.");

            _wpfAvailable = true;
            return true;
        }
        catch (TypeInitializationException ex)
        {
            // TypeInitializationException from a static field/constructor in this class
            // or a class it references. Log details and disable WPF — this is a permanent
            // failure for the current AppDomain (CLR caches static constructor failures).
            AddInLogger.Error($"WPF type initialization failed (permanent for this session): {ex.GetType().FullName}: {ex.Message}", ex);
            if (ex.InnerException != null)
                AddInLogger.Warn($"  Inner: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message}");

            AddInLogger.Warn("Non-recoverable type initialization error — disabling WPF for this session.");
            _wpfAvailable = false;
            return false;
        }
        catch (Exception ex)
        {
            AddInLogger.Error($"WPF window creation failed: {ex.GetType().FullName}: {ex.Message}", ex);

            // Log inner exception chain
            var inner = ex.InnerException;
            var depth = 0;
            while (inner != null && depth < 5)
            {
                AddInLogger.Warn($"  Inner exception [{depth}]: {inner.GetType().FullName}: {inner.Message}");
                inner = inner.InnerException;
                depth++;
            }

            // Only cache failure for WPF/permission errors.
            // Threading errors (InvalidOperationException) should retry — the next
            // call may be on a different thread with STA state.
            if (ex is System.InvalidOperationException)
            {
                AddInLogger.Info("Threading error — WPF will be retried on next call.");
                return false;
            }

            // Renderer-specific or unknown errors — do NOT disable WPF.
            // The WPF window itself may still work; only the renderer failed.
            // The next call will use plain text fallback instead.
            AddInLogger.Warn($"Non-threading error ({ex.GetType().Name}) — WPF will be retried with plain text fallback.");
            return false;
        }
    }

    /// <summary>
    /// Creates a metadata chip (small labeled text block) for the header bar.
    /// </summary>
    private static Border CreateMetadataChip(string text)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x60, 0x60, 0x60)),
            VerticalAlignment = VerticalAlignment.Center
        };

        return new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xE8)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 6, 0),
            Child = textBlock
        };
    }
}
#endif
