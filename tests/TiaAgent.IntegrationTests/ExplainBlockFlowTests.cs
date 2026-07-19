using TiaAgent.Application.Hashing;
using TiaAgent.Application.Common;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Requests;
using TiaAgent.Simulator;
using Xunit;

namespace TiaAgent.IntegrationTests;

/// <summary>
/// End-to-end test demonstrating the full explain-block vertical slice
/// without requiring a real TIA Portal installation.
/// </summary>
public class ExplainBlockFlowTests
{
    private readonly SimulatorTiaProjectService _service;

    public ExplainBlockFlowTests()
    {
        _service = new SimulatorTiaProjectService(
            new ContentHashService(),
            new SystemClock());
    }

    [Fact]
    public async Task FullExplainBlockFlow_SimulatesAgentWorkflow()
    {
        // Step 1: Get the TIA context (simulated project)
        var context = await _service.GetCurrentContextAsync(CancellationToken.None);
        Assert.Equal("DemoConveyorLine", context.ProjectName);
        Assert.Equal("V21", context.TiaVersion);
        Assert.True(context.Capabilities.ReadBlockSource);

        // Step 2: Capture selection for FB_Conveyor
        var selectionToken = "sel-explain-001";
        _service.CreateSelectionToken(
            selectionToken,
            "block-fb-conveyor",
            "FB_Conveyor",
            "FunctionBlock",
            "PLC_1/Program blocks/FB_Conveyor");

        // Step 3: Get the selection snapshot
        var selection = await _service.GetSelectionAsync(selectionToken, CancellationToken.None);
        Assert.Equal(selectionToken, selection.SelectionToken);
        Assert.Single(selection.Objects);
        Assert.Equal("FB_Conveyor", selection.Objects[0].NameAtCapture);

        // Step 4: Read the block (agent would do this via MCP)
        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { SelectionToken = selectionToken },
            CancellationToken.None);
        Assert.Equal("FB_Conveyor", block.Name);
        Assert.NotNull(block.SourceCode);
        Assert.Contains("FUNCTION_BLOCK", block.SourceCode!);
        Assert.NotNull(block.Interface);

        // Step 5: Read the interface (agent would do this via MCP)
        var iface = await _service.GetBlockInterfaceAsync(
            new GetBlockInterfaceRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);
        Assert.Equal(3, iface.InputParams.Count);
        Assert.Equal(3, iface.OutputParams.Count);

        // Step 6: Get call hierarchy (agent would do this via MCP)
        var hierarchy = await _service.GetCallHierarchyAsync(
            new GetCallHierarchyRequest { ObjectId = "block-ob-main", MaxDepth = 3 },
            CancellationToken.None);
        Assert.Equal("OB_Main", hierarchy.RootName);
        Assert.Single(hierarchy.Nodes);
        Assert.Equal(3, hierarchy.Nodes[0].Children.Count);

        // Step 7: Find references (agent would do this via MCP)
        var refs = await _service.FindReferencesAsync(
            new FindReferencesRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);
        Assert.NotEmpty(refs.References);

        // All steps complete - this is the data the agent would use to generate an explanation
        Assert.NotNull(context);
        Assert.NotNull(selection);
        Assert.NotNull(block);
        Assert.NotNull(iface);
        Assert.NotNull(hierarchy);
        Assert.NotEmpty(refs.References);
    }

    [Fact]
    public async Task FullChangeFlow_SimulatesApprovalWorkflow()
    {
        // Step 1: Read the current block
        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);
        Assert.NotNull(block.ContentHash);

        // Step 2: Preview a change
        var proposedSource = block.SourceCode + "\n// Added by agent";
        var preview = await _service.PreviewBlockChangeAsync(
            new PreviewBlockChangeRequest
            {
                ObjectId = "block-fb-conveyor",
                ProposedSource = proposedSource,
                ExpectedContentHash = block.ContentHash
            },
            CancellationToken.None);
        Assert.NotNull(preview.ChangeSetId);
        Assert.NotNull(preview.Diff);
        Assert.NotEmpty(preview.Risks);

        // Step 3: Apply with approval (simulated)
        var result = await _service.ApplyApprovedBlockChangeAsync(
            new ApplyApprovedBlockChangeRequest
            {
                ChangeSetId = preview.ChangeSetId,
                ApprovalToken = "approval-simulated"
            },
            CancellationToken.None);
        Assert.True(result.Success);
        Assert.NotNull(result.CompileResult);
        Assert.True(result.CompileResult!.Success);
    }

    [Fact]
    public async Task ErrorScenarios_AllReturnStructuredErrors()
    {
        // Expired selection
        var config = new SimulatorScenarioConfig { SimulateExpiredSelection = true };
        var service = new SimulatorTiaProjectService(
            new ContentHashService(),
            new SystemClock(),
            config);

        var ex1 = await Assert.ThrowsAsync<TiaErrorException>(() =>
            service.GetSelectionAsync("sel-expired", CancellationToken.None));
        Assert.Equal(Contracts.Errors.TiaErrorCode.TIA_SELECTION_EXPIRED, ex1.Error.Code);

        // Object changed
        config.SimulateObjectChanged = true;
        config.SimulateExpiredSelection = false;
        var ex2 = await Assert.ThrowsAsync<TiaErrorException>(() =>
            service.ReadBlockAsync(
                new ReadBlockRequest { ObjectId = "block-conveyor" },
                CancellationToken.None));
        Assert.Equal(Contracts.Errors.TiaErrorCode.TIA_OBJECT_CHANGED, ex2.Error.Code);

        // Disconnected
        config.SimulateDisconnected = true;
        config.SimulateObjectChanged = false;
        var ex3 = await Assert.ThrowsAsync<TiaErrorException>(() =>
            service.GetCurrentContextAsync(CancellationToken.None));
        Assert.Equal(Contracts.Errors.TiaErrorCode.TIA_NOT_CONNECTED, ex3.Error.Code);

        // Compile failure
        config.SimulateDisconnected = false;
        config.SimulateCompileFailure = true;
        var compileResult = await service.CompileSoftwareAsync(
            new CompileRequest { Scope = "Software" },
            CancellationToken.None);
        Assert.False(compileResult.Success);
        Assert.Contains(compileResult.Messages, m => m.Severity == "Error");
    }
}
