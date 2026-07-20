#if SIEMENS
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TiaAgent.AddIn.Ui;

/// <summary>
/// ViewModel for the AI Assistant panel. Drives data binding in the WPF UserControl.
/// </summary>
public sealed class AssistantPanelViewModel : INotifyPropertyChanged
{
    private string _title = "AI Assistant";
    private string _content = "";
    private string _status = "Ready";
    private bool _isBusy;

    public string Title
    {
        get => _title;
        set { _title = value; OnPropertyChanged(); }
    }

    public string Content
    {
        get => _content;
        set { _content = value; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Updates the panel with a successful result.
    /// </summary>
    public void ShowResult(string action, string result)
    {
        Title = "AI Assistant - " + action;
        Content = result;
        Status = "Completed";
        IsBusy = false;
    }

    /// <summary>
    /// Updates the panel with an error message.
    /// </summary>
    public void ShowError(string message)
    {
        Title = "AI Assistant - Error";
        Content = message;
        Status = "Error";
        IsBusy = false;
    }

    /// <summary>
    /// Shows a loading state while processing.
    /// </summary>
    public void ShowLoading(string action)
    {
        Title = "AI Assistant - " + action;
        Content = "Processing...";
        Status = "Working...";
        IsBusy = true;
    }

    /// <summary>
    /// Shows a warning message (e.g., no object selected).
    /// </summary>
    public void ShowWarning(string message)
    {
        Title = "AI Assistant";
        Content = message;
        Status = "Warning";
        IsBusy = false;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
#endif
