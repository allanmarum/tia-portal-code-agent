using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Requests;

namespace TiaAgent.AddIn;

/// <summary>
/// Console-based development host that exercises the same workflow as the real TIA Add-In.
/// Enables testing without a TIA Portal installation.
/// </summary>
public class DevelopmentHost
{
    private readonly ITiaProjectService _service;
    private readonly ISelectionTokenFactory? _selectionFactory;

    public DevelopmentHost(ITiaProjectService service, ISelectionTokenFactory? selectionFactory = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _selectionFactory = selectionFactory;
    }

    public async Task RunAsync()
    {
        Console.WriteLine("=== TIA Portal Code Agent - Development Host ===");
        Console.WriteLine("Simulator mode: DemoConveyorLine project");
        Console.WriteLine();

        while (true)
        {
            Console.WriteLine("Available commands:");
            Console.WriteLine("  1. Get context");
            Console.WriteLine("  2. List blocks");
            Console.WriteLine("  3. Read block");
            Console.WriteLine("  4. Get block interface");
            Console.WriteLine("  5. Get call hierarchy");
            Console.WriteLine("  6. Find references");
            Console.WriteLine("  7. Explain selected block (full flow)");
            Console.WriteLine("  8. Preview change");
            Console.WriteLine("  9. Compile software");
            Console.WriteLine("  0. Exit");
            Console.WriteLine();

            Console.Write("Select command: ");
            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1": await GetContextAsync(); break;
                    case "2": await ListBlocksAsync(); break;
                    case "3": await ReadBlockAsync(); break;
                    case "4": await GetBlockInterfaceAsync(); break;
                    case "5": await GetCallHierarchyAsync(); break;
                    case "6": await FindReferencesAsync(); break;
                    case "7": await ExplainBlockFlowAsync(); break;
                    case "8": await PreviewChangeAsync(); break;
                    case "9": await CompileAsync(); break;
                    case "0": return;
                    default: Console.WriteLine("Invalid choice."); break;
                }
            }
            catch (TiaErrorException ex)
            {
                Console.WriteLine($"\n[ERROR] {ex.Error.Code}: {ex.Error.Message}");
                if (ex.Error.Details != null)
                {
                    foreach (var detail in ex.Error.Details)
                        Console.WriteLine($"  {detail.Key}: {detail.Value}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[UNEXPECTED ERROR] {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private async Task GetContextAsync()
    {
        var context = await _service.GetCurrentContextAsync(CancellationToken.None);
        Console.WriteLine($"\nTIA Version:     {context.TiaVersion}");
        Console.WriteLine($"Project:         {context.ProjectName}");
        Console.WriteLine($"Project ID:      {context.ProjectId}");
        Console.WriteLine($"PLCs:            {context.PlcCount}");
        Console.WriteLine($"Blocks:          {context.BlockCount}");
        Console.WriteLine($"Session:         {context.TiaSessionId}");
        Console.WriteLine($"Capabilities:");
        Console.WriteLine($"  Read blocks:   {context.Capabilities.ReadBlockSource}");
        Console.WriteLine($"  References:    {context.Capabilities.FindReferences}");
        Console.WriteLine($"  Compile:       {context.Capabilities.CompileSoftware}");
        Console.WriteLine($"  Write:         {context.Capabilities.HardwareWrites}");
    }

    private async Task ListBlocksAsync()
    {
        var result = await _service.ListBlocksAsync(new ListBlocksRequest(), CancellationToken.None);
        Console.WriteLine($"\nBlocks ({result.Items.Count} total):");
        foreach (var block in result.Items)
        {
            Console.WriteLine($"  [{block.BlockType}] {block.Name} ({block.Language}) - {block.ObjectId}");
        }
    }

    private async Task ReadBlockAsync()
    {
        Console.Write("Enter block ID (e.g., block-fb-conveyor): ");
        var blockId = Console.ReadLine();

        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { ObjectId = blockId },
            CancellationToken.None);

        Console.WriteLine($"\nBlock: {block.Name} ({block.BlockType})");
        Console.WriteLine($"Language: {block.Language}");
        Console.WriteLine($"Path: {block.Path}");
        Console.WriteLine($"Content Hash: {block.ContentHash}");
        Console.WriteLine($"\nSource Code:");
        Console.WriteLine(block.SourceCode ?? "(no source)");
    }

    private async Task GetBlockInterfaceAsync()
    {
        Console.Write("Enter block ID: ");
        var blockId = Console.ReadLine();

        var iface = await _service.GetBlockInterfaceAsync(
            new GetBlockInterfaceRequest { ObjectId = blockId! },
            CancellationToken.None);

        Console.WriteLine($"\nInterface for {iface.Name}:");
        Console.WriteLine("  Input Parameters:");
        foreach (var p in iface.InputParams)
            Console.WriteLine($"    {p.Name} : {p.DataType} - {p.Comment}");
        Console.WriteLine("  Output Parameters:");
        foreach (var p in iface.OutputParams)
            Console.WriteLine($"    {p.Name} : {p.DataType} - {p.Comment}");
        Console.WriteLine("  Static Variables:");
        foreach (var p in iface.StaticVars)
            Console.WriteLine($"    {p.Name} : {p.DataType}");
    }

    private async Task GetCallHierarchyAsync()
    {
        Console.Write("Enter root block ID (e.g., block-ob-main): ");
        var blockId = Console.ReadLine();

        var hierarchy = await _service.GetCallHierarchyAsync(
            new GetCallHierarchyRequest { ObjectId = blockId!, MaxDepth = 3 },
            CancellationToken.None);

        Console.WriteLine($"\nCall Hierarchy for {hierarchy.RootName}:");
        PrintHierarchy(hierarchy.Nodes, "  ");
    }

    private static void PrintHierarchy(IReadOnlyList<CallHierarchyNodeDto> nodes, string indent)
    {
        foreach (var node in nodes)
        {
            Console.WriteLine($"{indent}├── {node.Name} ({node.BlockType})");
            if (node.Children.Count > 0)
                PrintHierarchy(node.Children, indent + "│   ");
        }
    }

    private async Task FindReferencesAsync()
    {
        Console.Write("Enter block ID to find references for: ");
        var blockId = Console.ReadLine();

        var result = await _service.FindReferencesAsync(
            new FindReferencesRequest { ObjectId = blockId },
            CancellationToken.None);

        Console.WriteLine($"\nReferences ({result.References.Count} found):");
        foreach (var r in result.References)
        {
            Console.WriteLine($"  {r.SourceName} → {r.TargetName} ({r.ReferenceType}) at {r.Location}");
        }
    }

    private async Task ExplainBlockFlowAsync()
    {
        Console.WriteLine("\n=== Full Explain Block Flow ===\n");

        // Step 1: Get context
        Console.Write("[1/6] Getting TIA context...");
        var context = await _service.GetCurrentContextAsync(CancellationToken.None);
        Console.WriteLine($" OK ({context.ProjectName})");

        // Step 2: Select block
        Console.Write("[2/6] Selecting FB_Conveyor...");
        var token = "sel-dev-001";
        if (_selectionFactory == null)
        {
            Console.WriteLine(" SKIPPED (no ISelectionTokenFactory)");
            return;
        }
        _selectionFactory.CreateSelectionToken(token, "block-fb-conveyor", "FB_Conveyor", "FunctionBlock", "PLC_1/Program blocks/FB_Conveyor");
        Console.WriteLine($" OK (token: {token})");

        // Step 3: Get selection
        Console.Write("[3/6] Getting selection snapshot...");
        var selection = await _service.GetSelectionAsync(token, CancellationToken.None);
        Console.WriteLine($" OK ({selection.Objects[0].NameAtCapture})");

        // Step 4: Read block
        Console.Write("[4/6] Reading block source...");
        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { SelectionToken = token },
            CancellationToken.None);
        Console.WriteLine($" OK ({block.SourceCode?.Length ?? 0} chars)");

        // Step 5: Get interface
        Console.Write("[5/6] Getting block interface...");
        var iface = await _service.GetBlockInterfaceAsync(
            new GetBlockInterfaceRequest { ObjectId = "block-fb-conveyor" },
            CancellationToken.None);
        Console.WriteLine($" OK ({iface.InputParams.Count} inputs, {iface.OutputParams.Count} outputs)");

        // Step 6: Get hierarchy
        Console.Write("[6/6] Getting call hierarchy...");
        var hierarchy = await _service.GetCallHierarchyAsync(
            new GetCallHierarchyRequest { ObjectId = "block-ob-main", MaxDepth = 2 },
            CancellationToken.None);
        Console.WriteLine($" OK ({hierarchy.Nodes.Count} top-level calls)");

        // Summary
        Console.WriteLine("\n--- Agent Would Now Generate Explanation ---");
        Console.WriteLine($"Block: {block.Name} ({block.BlockType}, {block.Language})");
        Console.WriteLine($"Inputs: {string.Join(", ", iface.InputParams.Select(p => $"{p.Name}:{p.DataType}"))}");
        Console.WriteLine($"Outputs: {string.Join(", ", iface.OutputParams.Select(p => $"{p.Name}:{p.DataType}"))}");
        Console.WriteLine($"Called by: OB_Main");
        Console.WriteLine($"Calls: FB_Motor, FB_SafetyInterlock, FC_AlarmHandler");
        Console.WriteLine("Flow: Enable -> Ramp up -> Run motor -> Monitor safety -> Handle alarms");
    }

    private async Task PreviewChangeAsync()
    {
        Console.Write("Enter block ID to preview change: ");
        var blockId = Console.ReadLine();

        var block = await _service.ReadBlockAsync(
            new ReadBlockRequest { ObjectId = blockId },
            CancellationToken.None);

        var proposedSource = block.SourceCode + "\n// Modification by agent";

        Console.WriteLine("\nPreviewing change...");
        var preview = await _service.PreviewBlockChangeAsync(
            new PreviewBlockChangeRequest
            {
                ObjectId = blockId!,
                ProposedSource = proposedSource,
                ExpectedContentHash = block.ContentHash
            },
            CancellationToken.None);

        Console.WriteLine($"Change Set ID: {preview.ChangeSetId}");
        Console.WriteLine($"Diff Hash: {preview.DiffHash}");
        Console.WriteLine($"Risks: {string.Join(", ", preview.Risks)}");
        Console.WriteLine($"\nDiff:\n{preview.Diff}");
    }

    private async Task CompileAsync()
    {
        Console.WriteLine("\nCompiling software...");
        var result = await _service.CompileSoftwareAsync(
            new CompileRequest { Scope = "Software" },
            CancellationToken.None);

        Console.WriteLine($"Success: {result.Success}");
        Console.WriteLine($"Duration: {result.Duration.TotalSeconds:F1}s");
        Console.WriteLine("Messages:");
        foreach (var msg in result.Messages)
        {
            Console.WriteLine($"  [{msg.Severity}] {msg.Message}");
        }
    }
}
