using TiaAgent.Mcp.Tools;
using TiaAgent.Application.Hashing;
using TiaAgent.Application.Common;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Requests;
using TiaAgent.Simulator;
using Xunit;

namespace TiaAgent.Mcp.Tests;

public class TiaContextToolsTests
{
    private readonly TiaContextTools _tools;

    public TiaContextToolsTests()
    {
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock());
        _tools = new TiaContextTools(service);
    }

    [Fact]
    public async Task GetCurrentContext_ReturnsValidContext()
    {
        var context = await _tools.GetCurrentContext(CancellationToken.None);

        Assert.Equal("DemoConveyorLine", context.ProjectName);
        Assert.Equal("V21", context.TiaVersion);
    }

    [Fact]
    public async Task GetCurrentSelection_ReturnsSnapshot()
    {
        // First create a selection
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock());
        service.CreateSelectionToken("sel-mcp-001", "block-fb-conveyor", "FB_Conveyor", "FunctionBlock", "PLC_1/Program blocks/FB_Conveyor");
        var tools = new TiaContextTools(service);

        var snapshot = await tools.GetCurrentSelection("sel-mcp-001", CancellationToken.None);

        Assert.Equal("sel-mcp-001", snapshot.SelectionToken);
        Assert.Single(snapshot.Objects);
    }
}

public class TiaReadToolsTests
{
    private readonly TiaReadTools _tools;

    public TiaReadToolsTests()
    {
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock());
        _tools = new TiaReadTools(service);
    }

    [Fact]
    public async Task ListBlocks_ReturnsAllBlocks()
    {
        var result = await _tools.ListBlocks(new ListBlocksRequest(), CancellationToken.None);

        Assert.Equal(6, result.Items.Count);
    }

    [Fact]
    public async Task ReadBlock_ReturnsBlockWithSource()
    {
        var block = await _tools.ReadBlock(
            new ReadBlockRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);

        Assert.Equal("FB_Conveyor", block.Name);
        Assert.NotNull(block.SourceCode);
    }

    [Fact]
    public async Task GetBlockInterface_ReturnsInterface()
    {
        var iface = await _tools.GetBlockInterface(
            new GetBlockInterfaceRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);

        Assert.Equal(3, iface.InputParams.Count);
        Assert.Equal(3, iface.OutputParams.Count);
    }
}

public class TiaReferenceToolsTests
{
    private readonly TiaReferenceTools _tools;

    public TiaReferenceToolsTests()
    {
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock());
        _tools = new TiaReferenceTools(service);
    }

    [Fact]
    public async Task GetCallHierarchy_ReturnsTree()
    {
        var hierarchy = await _tools.GetCallHierarchy(
            new GetCallHierarchyRequest { ObjectId = "block-ob-main", MaxDepth = 3 },
            CancellationToken.None);

        Assert.Equal("OB_Main", hierarchy.RootName);
        Assert.Single(hierarchy.Nodes);
    }

    [Fact]
    public async Task FindReferences_ReturnsResults()
    {
        var result = await _tools.FindReferences(
            new FindReferencesRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);

        Assert.NotEmpty(result.References);
    }
}

public class TiaChangeToolsTests
{
    private readonly TiaChangeTools _tools;

    public TiaChangeToolsTests()
    {
        var service = new SimulatorTiaProjectService(new ContentHashService(), new SystemClock());
        _tools = new TiaChangeTools(service);
    }

    [Fact]
    public async Task PreviewBlockChange_ReturnsPreview()
    {
        var preview = await _tools.PreviewBlockChange(
            new PreviewBlockChangeRequest
            {
                ObjectId = "block-fb-conveyor",
                ProposedSource = "FUNCTION_BLOCK FB_Conveyor END_FUNCTION_BLOCK"
            },
            CancellationToken.None);

        Assert.NotNull(preview.ChangeSetId);
        Assert.NotNull(preview.Diff);
    }
}
