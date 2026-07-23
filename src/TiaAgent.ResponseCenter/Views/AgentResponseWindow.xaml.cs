using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using TiaAgent.ResponseCenter.ViewModels;

namespace TiaAgent.ResponseCenter.Views;

/// <summary>
/// Code-behind for the Agent Response Center window.
/// Minimal — all logic lives in the ViewModel.
/// </summary>
public partial class AgentResponseWindow : Window
{
    private readonly AgentResponseViewModel _viewModel;
    private string? _rawResponse;

    public AgentResponseWindow(AgentResponseViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        // Wire close request
        _viewModel.RequestClose += OnViewModelRequestClose;

        // Wire response rendering
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Ensure clean shutdown
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Sets the raw response text for rendering.
    /// Called by Program.cs after the response is received.
    /// </summary>
    public void SetResponse(string response)
    {
        _rawResponse = response;
        RenderResponse(response);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgentResponseViewModel.ShowResponse) && _viewModel.ShowResponse)
        {
            // Response just became visible — render it
            if (!string.IsNullOrEmpty(_viewModel.ResponseContent))
            {
                RenderResponse(_viewModel.ResponseContent);
            }
        }
    }

    private void RenderResponse(string markdown)
    {
        try
        {
            var flowDoc = MarkdownRenderer.Render(markdown);
            if (flowDoc != null)
            {
                ResponseViewer.Document = flowDoc;
            }
            else
            {
                // Fallback to plain text
                ResponseViewer.Document = MarkdownRenderer.CreatePlainTextFallback(markdown);
            }
        }
        catch
        {
            // Emergency fallback
            ResponseViewer.Document = MarkdownRenderer.CreatePlainTextFallback(markdown);
        }
    }

    private void OnViewModelRequestClose()
    {
        Dispatcher.Invoke(() =>
        {
            _viewModel.RequestClose -= OnViewModelRequestClose;
            Close();
        });
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Stop monitoring if window is closed while task is in progress
        if (_viewModel.IsBusy)
        {
            _viewModel.CancelCommand.Execute(null);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.Dispose();
    }

    private void ToggleTechnicalDetails_Click(object sender, MouseButtonEventArgs e)
    {
        _viewModel.ToggleTechnicalDetailsCommand.Execute(null);
    }
}
