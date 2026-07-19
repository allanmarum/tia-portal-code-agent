using TiaAgent.Contracts.Dtos;
using TiaAgent.Contracts.Errors;
using TiaAgent.Contracts.Common;
using Xunit;

namespace TiaAgent.Contracts.Tests;

public class TiaContextDtoTests
{
    [Fact]
    public void TiaContextDto_IsSerializable()
    {
        var dto = new TiaContextDto
        {
            TiaVersion = "V21",
            OpennessVersion = "V21",
            TiaSessionId = "tia-test-001",
            ProjectId = "proj-001",
            ProjectName = "TestProject",
            PlcCount = 1,
            BlockCount = 10,
            LastModified = DateTimeOffset.UtcNow,
            Capabilities = new CapabilityDto { ListBlocks = true, ReadBlockSource = true }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<TiaContextDto>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("V21", deserialized!.TiaVersion);
        Assert.Equal("TestProject", deserialized.ProjectName);
        Assert.True(deserialized.Capabilities.ListBlocks);
    }
}

public class TiaErrorTests
{
    [Fact]
    public void TiaError_HasRequiredProperties()
    {
        var error = new TiaError
        {
            Code = TiaErrorCode.TIA_NOT_CONNECTED,
            Message = "Not connected",
            Retryable = true,
            CorrelationId = "corr-001"
        };

        Assert.Equal(TiaErrorCode.TIA_NOT_CONNECTED, error.Code);
        Assert.Equal("Not connected", error.Message);
        Assert.True(error.Retryable);
        Assert.Equal("corr-001", error.CorrelationId);
    }

    [Fact]
    public void TiaErrorException_WrapsTiaError()
    {
        var error = new TiaError
        {
            Code = TiaErrorCode.TIA_OBJECT_NOT_FOUND,
            Message = "Block not found"
        };

        var ex = new TiaErrorException(error);

        Assert.Equal(error, ex.Error);
        Assert.Equal("Block not found", ex.Message);
    }

    [Fact]
    public void TiaError_StaticFactoryMethods()
    {
        var notFound = TiaError.NotFound("Block missing", "corr-001");
        Assert.Equal(TiaErrorCode.TIA_OBJECT_NOT_FOUND, notFound.Code);
        Assert.Equal("corr-001", notFound.CorrelationId);

        var expired = TiaError.SessionExpired("corr-002");
        Assert.Equal(TiaErrorCode.TIA_SESSION_EXPIRED, expired.Code);

        var changed = TiaError.ObjectChanged("obj-1", "sha256:old", "sha256:new", "corr-003");
        Assert.Equal(TiaErrorCode.TIA_OBJECT_CHANGED, changed.Code);
        Assert.False(changed.Retryable);
    }
}

public class SelectionSnapshotDtoTests
{
    [Fact]
    public void SelectionSnapshotDto_IsSerializable()
    {
        var dto = new SelectionSnapshotDto
        {
            SelectionToken = "sel-001",
            TiaSessionId = "tia-001",
            ProjectId = "proj-001",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Objects = new List<SelectedObjectDto>
            {
                new()
                {
                    ObjectId = "block-001",
                    NameAtCapture = "FB_Conveyor",
                    PathAtCapture = "PLC_1/Program blocks/FB_Conveyor",
                    ObjectType = "FunctionBlock"
                }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(dto);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SelectionSnapshotDto>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized!.Objects);
        Assert.Equal("FB_Conveyor", deserialized.Objects[0].NameAtCapture);
    }
}

public class BlockSnapshotDtoTests
{
    [Fact]
    public void BlockSnapshotDto_IncludesAllRequiredFields()
    {
        var dto = new BlockSnapshotDto
        {
            ObjectId = "block-001",
            ProjectId = "proj-001",
            Name = "FB_Conveyor",
            Path = "PLC_1/Program blocks/FB_Conveyor",
            BlockType = "FunctionBlock",
            Language = "SCL",
            SourceCode = "FUNCTION_BLOCK FB_Conveyor ...",
            ContentHash = "sha256:abc123",
            CapturedAt = DateTimeOffset.UtcNow,
            Provenance = DataProvenance.Direct
        };

        Assert.Equal("block-001", dto.ObjectId);
        Assert.Equal(DataProvenance.Direct, dto.Provenance);
        Assert.Contains("FUNCTION_BLOCK", dto.SourceCode!);
    }
}

public class PagedResultTests
{
    [Fact]
    public void PagedResult_DefaultValues()
    {
        var result = new PagedResultDto<BlockSummaryDto>
        {
            Items = Array.Empty<BlockSummaryDto>()
        };

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
        Assert.False(result.IsPartial);
    }

    [Fact]
    public void PagedResult_WithPagination()
    {
        var items = Enumerable.Range(0, 10)
            .Select(i => new BlockSummaryDto
            {
                ObjectId = $"block-{i}",
                Name = $"Block_{i}",
                BlockType = "FunctionBlock",
                Path = $"PLC_1/Block_{i}",
                Language = "SCL"
            })
            .ToList();

        var result = new PagedResultDto<BlockSummaryDto>
        {
            Items = items,
            NextCursor = "10",
            IsPartial = true,
            TotalCount = 50
        };

        Assert.Equal(10, result.Items.Count);
        Assert.NotNull(result.NextCursor);
        Assert.True(result.IsPartial);
        Assert.Equal(50, result.TotalCount);
    }
}

public class TiaLimitsTests
{
    [Fact]
    public void Limits_HaveReasonableValues()
    {
        Assert.True(TiaLimits.MaxPageSize > 0);
        Assert.True(TiaLimits.MaxHierarchyDepth > 0);
        Assert.True(TiaLimits.MaxHierarchyNodes > 0);
        Assert.True(TiaLimits.MaxBlockSourceBytes > 0);
        Assert.True(TiaLimits.MaxPageSize <= 1000);
    }
}
