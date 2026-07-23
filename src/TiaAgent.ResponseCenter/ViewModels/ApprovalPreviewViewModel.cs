using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TiaAgent.ResponseCenter.ViewModels;

/// <summary>
/// Placeholder ViewModel for future approval workflow.
/// When the runtime returns a WaitingForApproval status, this view model
/// will be populated with structured change details.
/// </summary>
public sealed class ApprovalPreviewViewModel : INotifyPropertyChanged
{
    private string _changeSummary = "";
    private string _affectedObjects = "";
    private string _diffPreview = "";
    private string _risks = "";
    private string _validationResults = "";
    private string _compileImpact = "";
    private bool _isExpanded;

    /// <summary>High-level description of the proposed changes.</summary>
    public string ChangeSummary
    {
        get => _changeSummary;
        set => SetProperty(ref _changeSummary, value);
    }

    /// <summary>List of TIA objects that will be affected.</summary>
    public string AffectedObjects
    {
        get => _affectedObjects;
        set => SetProperty(ref _affectedObjects, value);
    }

    /// <summary>Structured diff or change preview.</summary>
    public string DiffPreview
    {
        get => _diffPreview;
        set => SetProperty(ref _diffPreview, value);
    }

    /// <summary>Identified risks or warnings.</summary>
    public string Risks
    {
        get => _risks;
        set => SetProperty(ref _risks, value);
    }

    /// <summary>Validation results from pre-checks.</summary>
    public string ValidationResults
    {
        get => _validationResults;
        set => SetProperty(ref _validationResults, value);
    }

    /// <summary>Expected compile impact analysis.</summary>
    public string CompileImpact
    {
        get => _compileImpact;
        set => SetProperty(ref _compileImpact, value);
    }

    /// <summary>Whether the approval details section is expanded.</summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void SetProperty(ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
