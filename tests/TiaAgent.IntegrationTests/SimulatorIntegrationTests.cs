using TiaAgent.Application.Hashing;
using TiaAgent.Application.Common;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Requests;
using TiaAgent.Simulator;
using Xunit;

namespace TiaAgent.IntegrationTests;

public class SimulatorIntegrationTests
{
    private readonly SimulatorTiaProjectService _service;
    private readonly SimulatorScenarioConfig _config;

    public SimulatorIntegrationTests()
    {
        _config = new SimulatorScenarioConfig();
        _service = new SimulatorTiaProjectService(
            new ContentHashService(),
            new SystemClock(),
            _config);
    }

    [Fact]
    public async Task GetCurrentContext_ReturnsDemoProject()
    {
        var context = await _service.GetCurrentContextAsync(CancellationToken.None);

        Assert.Equal("DemoConveyorLine", context.ProjectName);
        Assert.Equal("V21", context.TiaVersion);
        Assert.Equal("sim-project-001", context.ProjectId);
        Assert.Equal(6, context.BlockCount);
        Assert.True(context.Capabilities.ReadBlockSource);
    }

    [Fact]
    public async Task ListBlocks_ReturnsAllBlocks()
    {
        var result = await _service.ListBlocksAsync(new ListBlocksRequest(), CancellationToken.None);

        Assert.Equal(6, result.Items.Count);
        Assert.Null(result.NextCursor);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public async Task ListBlocks_FiltersByType()
    {
        var result = await _service.ListBlocksAsync(
            new ListBlocksRequest { TypeFilter = "FunctionBlock" },
            CancellationToken.None);

        Assert.All(result.Items, b => Assert.Equal("FunctionBlock", b.BlockType));
    }

    [Fact]
    public async Task ListBlocks_FiltersByName()
    {
        var result = await _service.ListBlocksAsync(
            new ListBlocksRequest { NameFilter = "Conveyor" },
            CancellationToken.None);

        Assert.Contains(result.Items, b => b.Name.Contains("Conveyor"));
    }

    [Fact]
    public async Task ListBlocks_Paginates()
    {
        var result = await _service.ListBlocksAsync(
            new ListBlocksRequest { PageSize = 3 },
            CancellationToken.None);

        Assert.Equal(3, result.Items.Count);
        Assert.NotNull(result.NextCursor);
        Assert.True(result.IsPartial);
    }

    [Fact]
    public async Task ReadBlock_ReturnsFBConveyor()
    {
        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);

        Assert.Equal("FB_Conveyor", block.Name);
        Assert.Equal("FunctionBlock", block.BlockType);
        Assert.Equal("SCL", block.Language);
        Assert.NotNull(block.SourceCode);
        Assert.Contains("FUNCTION_BLOCK", block.SourceCode!);
        Assert.NotNull(block.Interface);
        Assert.Equal(3, block.Interface!.InputParams.Count);
        Assert.Equal(3, block.Interface.OutputParams.Count);
        Assert.NotNull(block.ContentHash);
    }

    [Fact]
    public async Task ReadBlock_ReturnsOBMain()
    {
        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { ObjectId = "block-ob-main" },
            CancellationToken.None);

        Assert.Equal("OB_Main", block.Name);
        Assert.Equal("OrganizationBlock", block.BlockType);
        Assert.Contains("NETWORK 1", block.SourceCode!);
    }

    [Fact]
    public async Task GetBlockInterface_ReturnsInterface()
    {
        var iface = await _service.GetBlockInterfaceAsync(
            new GetBlockInterfaceRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);

        Assert.Equal("FB_Conveyor", iface.Name);
        Assert.Equal(3, iface.InputParams.Count);
        Assert.Equal(3, iface.OutputParams.Count);
        Assert.Contains(iface.InputParams, p => p.Name == "Enable");
        Assert.Contains(iface.OutputParams, p => p.Name == "Running");
    }

    [Fact]
    public async Task GetCallHierarchy_ReturnsOBMainHierarchy()
    {
        var hierarchy = await _service.GetCallHierarchyAsync(
            new GetCallHierarchyRequest { ObjectId = "block-ob-main", MaxDepth = 3, MaxNodes = 100 },
            CancellationToken.None);

        Assert.Equal("block-ob-main", hierarchy.RootObjectId);
        Assert.Equal("OB_Main", hierarchy.RootName);
        Assert.Single(hierarchy.Nodes);
        Assert.Equal("FB_Conveyor", hierarchy.Nodes[0].Name);
        Assert.Equal(3, hierarchy.Nodes[0].Children.Count);
    }

    [Fact]
    public async Task FindReferences_FindsConveyorReferences()
    {
        var result = await _service.FindReferencesAsync(
            new FindReferencesRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);

        Assert.NotEmpty(result.References);
        Assert.Contains(result.References, r => r.SourceName == "OB_Main");
    }

    [Fact]
    public async Task CompileSoftware_ReturnsSuccess()
    {
        var result = await _service.CompileSoftwareAsync(
            new CompileRequest { Scope = "Software" },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotEmpty(result.Messages);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    [Fact]
    public async Task PreviewBlockChange_ReturnsChangeSet()
    {
        var preview = await _service.PreviewBlockChangeAsync(
            new PreviewBlockChangeRequest
            {
                ObjectId = "block-fb-conveyor",
                ProposedSource = "FUNCTION_BLOCK FB_Conveyor ... END_FUNCTION_BLOCK"
            },
            CancellationToken.None);

        Assert.NotNull(preview.ChangeSetId);
        Assert.NotNull(preview.Diff);
        Assert.NotNull(preview.DiffHash);
        Assert.NotEmpty(preview.Risks);
    }

    [Fact]
    public async Task SelectionToken_Works()
    {
        var token = "sel-test-001";
        _service.CreateSelectionToken(token, "block-fb-conveyor", "FB_Conveyor", "FunctionBlock", "PLC_1/Program blocks/FB_Conveyor");

        var snapshot = await _service.GetSelectionAsync(token, CancellationToken.None);

        Assert.Equal(token, snapshot.SelectionToken);
        Assert.Single(snapshot.Objects);
        Assert.Equal("FB_Conveyor", snapshot.Objects[0].NameAtCapture);
    }

    [Fact]
    public async Task ReadBlock_WithSelectionToken()
    {
        var token = "sel-test-002";
        _service.CreateSelectionToken(token, "block-fb-conveyor", "FB_Conveyor", "FunctionBlock", "PLC_1/Program blocks/FB_Conveyor");

        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { SelectionToken = token },
            CancellationToken.None);

        Assert.Equal("FB_Conveyor", block.Name);
    }

    [Fact]
    public async Task ExpiredSelection_ThrowsError()
    {
        _config.SimulateExpiredSelection = true;

        await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.GetSelectionAsync("sel-expired", CancellationToken.None));
    }

    [Fact]
    public async Task ObjectChanged_ThrowsError()
    {
        _config.SimulateObjectChanged = true;

        await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.ReadBlockAsync(
                new ReadBlockRequest { ObjectId = "block-conveyor" },
                CancellationToken.None));
    }

    [Fact]
    public async Task Disconnected_ThrowsError()
    {
        _config.SimulateDisconnected = true;

        await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.GetCurrentContextAsync(CancellationToken.None));
    }

    [Fact]
    public async Task CompileFailure_ReturnsErrors()
    {
        _config.SimulateCompileFailure = true;

        var result = await _service.CompileSoftwareAsync(
            new CompileRequest { Scope = "Software" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Messages, m => m.Severity == "Error");
    }

    [Fact]
    public async Task ApprovalExpired_ThrowsError()
    {
        _config.SimulateApprovalExpired = true;

        await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.ApplyApprovedBlockChangeAsync(
                new ApplyApprovedBlockChangeRequest
                {
                    ChangeSetId = "change-001",
                    ApprovalToken = "approval-001"
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task ApprovalAlreadyUsed_ThrowsError()
    {
        _config.SimulateApprovalAlreadyUsed = true;

        await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.ApplyApprovedBlockChangeAsync(
                new ApplyApprovedBlockChangeRequest
                {
                    ChangeSetId = "change-001",
                    ApprovalToken = "approval-001"
                },
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyApprovedChange_ReturnsSuccess()
    {
        var result = await _service.ApplyApprovedBlockChangeAsync(
            new ApplyApprovedBlockChangeRequest
            {
                ChangeSetId = "change-001",
                ApprovalToken = "approval-001"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.CompileResult);
        Assert.True(result.CompileResult!.Success);
    }

    [Fact]
    public async Task ListBlocks_PageSizeClampedToOneMinimum()
    {
        var result = await _service.ListBlocksAsync(
            new ListBlocksRequest { PageSize = 0 },
            CancellationToken.None);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task ListBlocks_PageSizeClampedToMax()
    {
        var result = await _service.ListBlocksAsync(
            new ListBlocksRequest { PageSize = 5000 },
            CancellationToken.None);

        Assert.Equal(6, result.Items.Count);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ReadBlock_NonexistentObjectId_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.ReadBlockAsync(
                new ReadBlockRequest { ObjectId = "nonexistent" },
                CancellationToken.None));

        Assert.Equal(TiaErrorCode.TIA_OBJECT_NOT_FOUND, ex.Error.Code);
    }

    [Fact]
    public async Task GetBlockInterface_NonexistentBlock_ThrowsNotFound()
    {
        var ex = await Assert.ThrowsAsync<TiaErrorException>(() =>
            _service.GetBlockInterfaceAsync(
                new GetBlockInterfaceRequest { ObjectId = "nonexistent" },
                CancellationToken.None));

        Assert.Equal(TiaErrorCode.TIA_OBJECT_NOT_FOUND, ex.Error.Code);
    }
}
