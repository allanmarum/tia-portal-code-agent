using System.Globalization;
using FluentAssertions;
using TiaAgent.ResponseCenter.Models;
using TiaAgent.ResponseCenter.ViewModels;
using Xunit;

namespace TiaAgent.ResponseCenter.Tests;

public class AgentResponseViewModelTests
{
    private static AgentResponseContext CreateContext()
    {
        return new AgentResponseContext
        {
            TaskId = "task123",
            BridgeUrl = "http://localhost:9999",
            Action = "explain",
            ObjectName = "FB_Conveyor",
            ObjectType = "Function Block",
            PlcName = "PLC_1",
            ProjectName = "MyProject",
            CorrelationId = "tia-abc123"
        };
    }

    [Fact]
    public void ViewModel_InitializesWithContext()
    {
        var ctx = CreateContext();
        ctx.ActionDisplay.Should().Be("Explain selected object");
        ctx.ObjectName.Should().Be("FB_Conveyor");
        ctx.ObjectType.Should().Be("Function Block");
        ctx.PlcName.Should().Be("PLC_1");
        ctx.ProjectName.Should().Be("MyProject");
        ctx.CorrelationId.Should().Be("tia-abc123");
    }

    [Fact]
    public void ErrorDetailsViewModel_InitializesWithDefaults()
    {
        var vm = new ErrorDetailsViewModel();
        vm.UserMessage.Should().Be("");
        vm.TechnicalDetails.Should().Be("");
        vm.CorrelationId.Should().Be("");
        vm.Retryable.Should().BeFalse();
        vm.ShowTechnicalDetails.Should().BeFalse();
    }

    [Fact]
    public void ErrorDetailsViewModel_PropertyChanged_Fires()
    {
        var vm = new ErrorDetailsViewModel();
        var changed = new System.Collections.Generic.List<string>();
        vm.PropertyChanged += (s, e) => changed.Add(e.PropertyName!);

        vm.UserMessage = "Test message";
        changed.Should().Contain("UserMessage");

        vm.TechnicalDetails = "Details";
        changed.Should().Contain("TechnicalDetails");

        vm.Retryable = true;
        changed.Should().Contain("Retryable");
    }

    [Fact]
    public void ErrorDetailsViewModel_PropertyChanged_DoesNotFireForSameValue()
    {
        var vm = new ErrorDetailsViewModel();
        vm.UserMessage = "Test";
        var count = 0;
        vm.PropertyChanged += (s, e) => count++;

        vm.UserMessage = "Test"; // Same value
        count.Should().Be(0);
    }

    [Fact]
    public void ApprovalPreviewViewModel_InitializesWithDefaults()
    {
        var vm = new ApprovalPreviewViewModel();
        vm.ChangeSummary.Should().Be("");
        vm.AffectedObjects.Should().Be("");
        vm.DiffPreview.Should().Be("");
        vm.Risks.Should().Be("");
        vm.ValidationResults.Should().Be("");
        vm.CompileImpact.Should().Be("");
        vm.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void StateToStatusConverter_MapsAllStates()
    {
        var converter = new TiaAgent.ResponseCenter.Converters.StateToStatusConverter();
        var culture = CultureInfo.InvariantCulture;

        converter.Convert(AgentTaskState.Created, typeof(string), null!, culture).Should().Be(Strings.StatusCreated);
        converter.Convert(AgentTaskState.Submitting, typeof(string), null!, culture).Should().Be(Strings.StatusSubmitting);
        converter.Convert(AgentTaskState.Queued, typeof(string), null!, culture).Should().Be(Strings.StatusQueued);
        converter.Convert(AgentTaskState.Running, typeof(string), null!, culture).Should().Be(Strings.StatusRunning);
        converter.Convert(AgentTaskState.WaitingForApproval, typeof(string), null!, culture).Should().Be(Strings.StatusWaitingForApproval);
        converter.Convert(AgentTaskState.Completed, typeof(string), null!, culture).Should().Be(Strings.StatusCompleted);
        converter.Convert(AgentTaskState.Failed, typeof(string), null!, culture).Should().Be(Strings.StatusFailed);
        converter.Convert(AgentTaskState.Cancelled, typeof(string), null!, culture).Should().Be(Strings.StatusCancelled);
        converter.Convert(AgentTaskState.Disconnected, typeof(string), null!, culture).Should().Be(Strings.StatusDisconnected);
    }

    [Fact]
    public void StateToColorConverter_MapsAllStates()
    {
        var converter = new TiaAgent.ResponseCenter.Converters.StateToColorConverter();
        var culture = CultureInfo.InvariantCulture;

        foreach (AgentTaskState state in System.Enum.GetValues(typeof(AgentTaskState)))
        {
            var result = converter.Convert(state, typeof(object), null!, culture);
            result.Should().NotBeNull();
        }
    }

    [Fact]
    public void BoolToVisibilityConverter_MapsCorrectly()
    {
        var converter = new TiaAgent.ResponseCenter.Converters.BoolToVisibilityConverter();
        var culture = CultureInfo.InvariantCulture;

        converter.Convert(true, typeof(object), null!, culture).Should().Be(System.Windows.Visibility.Visible);
        converter.Convert(false, typeof(object), null!, culture).Should().Be(System.Windows.Visibility.Collapsed);
    }

    [Fact]
    public void StringToVisibilityConverter_MapsCorrectly()
    {
        var converter = new TiaAgent.ResponseCenter.Converters.StringToVisibilityConverter();
        var culture = CultureInfo.InvariantCulture;

        converter.Convert("hello", typeof(object), null!, culture).Should().Be(System.Windows.Visibility.Visible);
        converter.Convert("", typeof(object), null!, culture).Should().Be(System.Windows.Visibility.Collapsed);
        converter.Convert(null!, typeof(object), null!, culture).Should().Be(System.Windows.Visibility.Collapsed);
    }
}
