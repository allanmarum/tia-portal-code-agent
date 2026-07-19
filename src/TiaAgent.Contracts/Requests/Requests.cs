namespace TiaAgent.Contracts.Requests;

public class ListBlocksRequest
{
    public int PageSize { get; init; } = 100;
    public string? Cursor { get; init; }
    public string? TypeFilter { get; init; }
    public string? LanguageFilter { get; init; }
    public string? PlcId { get; init; }
    public string? NameFilter { get; init; }
}

public class ReadBlockRequest
{
    public string? ObjectId { get; init; }
    public string? SelectionToken { get; init; }
    public bool IncludeSource { get; init; } = true;
    public bool IncludeInterface { get; init; } = true;
}

public class GetBlockInterfaceRequest
{
    public required string ObjectId { get; init; }
}

public class GetCallHierarchyRequest
{
    public required string ObjectId { get; init; }
    public int MaxDepth { get; init; } = 3;
    public int MaxNodes { get; init; } = 100;
}

public class FindReferencesRequest
{
    public string? ObjectId { get; init; }
    public string? SymbolName { get; init; }
    public int MaxResults { get; init; } = 100;
}

public class CompileRequest
{
    public string Scope { get; init; } = "Software";
    public string? TargetId { get; init; }
}

public class PreviewBlockChangeRequest
{
    public required string ObjectId { get; init; }
    public required string ProposedSource { get; init; }
    public string? ExpectedContentHash { get; init; }
}

public class ApplyApprovedBlockChangeRequest
{
    public required string ChangeSetId { get; init; }
    public required string ApprovalToken { get; init; }
}
