using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TiaAgent.ResponseCenter.Models;

namespace TiaAgent.ResponseCenter.Converters;

/// <summary>
/// Converts AgentTaskState to a user-friendly status display string.
/// </summary>
[ValueConversion(typeof(AgentTaskState), typeof(string))]
public sealed class StateToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AgentTaskState state ? state switch
        {
            AgentTaskState.Created => Strings.StatusCreated,
            AgentTaskState.Submitting => Strings.StatusSubmitting,
            AgentTaskState.Queued => Strings.StatusQueued,
            AgentTaskState.Running => Strings.StatusRunning,
            AgentTaskState.WaitingForApproval => Strings.StatusWaitingForApproval,
            AgentTaskState.Completed => Strings.StatusCompleted,
            AgentTaskState.Failed => Strings.StatusFailed,
            AgentTaskState.Cancelled => Strings.StatusCancelled,
            AgentTaskState.Disconnected => Strings.StatusDisconnected,
            _ => state.ToString()
        } : value?.ToString() ?? "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts AgentTaskState to a status indicator color.
/// </summary>
[ValueConversion(typeof(AgentTaskState), typeof(Brush))]
public sealed class StateToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush RunningBrush = new(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush CompletedBrush = new(Color.FromRgb(0x10, 0x7C, 0x10));
    private static readonly SolidColorBrush FailedBrush = new(Color.FromRgb(0xD1, 0x34, 0x38));
    private static readonly SolidColorBrush CancelledBrush = new(Color.FromRgb(0x76, 0x76, 0x76));
    private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(0xFF, 0x8C, 0x00));
    private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(0x50, 0x50, 0x50));

    static StateToColorConverter()
    {
        RunningBrush.Freeze();
        CompletedBrush.Freeze();
        FailedBrush.Freeze();
        CancelledBrush.Freeze();
        WarningBrush.Freeze();
        DefaultBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AgentTaskState state ? state switch
        {
            AgentTaskState.Running or AgentTaskState.Queued or AgentTaskState.Submitting => RunningBrush,
            AgentTaskState.Completed => CompletedBrush,
            AgentTaskState.Failed or AgentTaskState.Disconnected => FailedBrush,
            AgentTaskState.Cancelled => CancelledBrush,
            AgentTaskState.WaitingForApproval => WarningBrush,
            _ => DefaultBrush
        } : DefaultBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts AgentTaskState to Visibility for the progress indicator.
/// </summary>
[ValueConversion(typeof(AgentTaskState), typeof(Visibility))]
public sealed class StateToProgressVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AgentTaskState state && (state == AgentTaskState.Running || state == AgentTaskState.Queued || state == AgentTaskState.Submitting)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts bool to Visibility.
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

/// <summary>
/// Converts a string to Visibility (visible when not null or empty).
/// </summary>
[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
