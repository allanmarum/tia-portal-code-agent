using TiaAgent.Application.Selections;
using TiaAgent.Application.Common;
using TiaAgent.Contracts.Abstractions;
using TiaAgent.Contracts.Dtos;
using Xunit;

namespace TiaAgent.Application.Tests;

public class SelectionSnapshotStoreTests
{
    private static SelectionSnapshotStore CreateStore(IClock? clock = null)
    {
        return new SelectionSnapshotStore(clock ?? new SystemClock());
    }

    private static SelectionSnapshotDto CreateSnapshot(string token = "sel-001", string tiaSessionId = "tia-001")
    {
        return new SelectionSnapshotDto
        {
            SelectionToken = token,
            TiaSessionId = tiaSessionId,
            ProjectId = "proj-001",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            Objects = new List<SelectedObjectDto>
            {
                new()
                {
                    ObjectId = "block-001",
                    NameAtCapture = "FB_Conveyor",
                    PathAtCapture = "PLC_1/FB_Conveyor",
                    ObjectType = "FunctionBlock"
                }
            }
        };
    }

    [Fact]
    public async Task SaveAsync_ThenGetAsync_ReturnsSnapshot()
    {
        var store = CreateStore();
        var snapshot = CreateSnapshot();

        await store.SaveAsync(snapshot, CancellationToken.None);
        var result = await store.GetAsync("sel-001", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("sel-001", result!.SelectionToken);
        Assert.Equal("FB_Conveyor", result.Objects[0].NameAtCapture);
    }

    [Fact]
    public async Task GetAsync_WithUnknownToken_ReturnsNull()
    {
        var store = CreateStore();

        var result = await store.GetAsync("nonexistent", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExpireAsync_RemovesSnapshot()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSnapshot(), CancellationToken.None);

        await store.ExpireAsync("sel-001", "test", CancellationToken.None);
        var result = await store.GetAsync("sel-001", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task ExpireAllForSessionAsync_RemovesAllForSession()
    {
        var store = CreateStore();
        await store.SaveAsync(CreateSnapshot("sel-001", "tia-001"), CancellationToken.None);
        await store.SaveAsync(CreateSnapshot("sel-002", "tia-001"), CancellationToken.None);
        await store.SaveAsync(CreateSnapshot("sel-003", "tia-002"), CancellationToken.None);

        await store.ExpireAllForSessionAsync("tia-001", "test", CancellationToken.None);

        Assert.Null(await store.GetAsync("sel-001", CancellationToken.None));
        Assert.Null(await store.GetAsync("sel-002", CancellationToken.None));
        Assert.NotNull(await store.GetAsync("sel-003", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_WithExpiredSnapshot_ReturnsNull()
    {
        var fakeClock = new FakeClock(DateTimeOffset.UtcNow.AddHours(-2));
        var store = CreateStore(fakeClock);
        var snapshot = CreateSnapshot();
        await store.SaveAsync(snapshot, CancellationToken.None);

        // Advance clock past expiration
        fakeClock.UtcNow = DateTimeOffset.UtcNow.AddHours(1);
        var result = await store.GetAsync("sel-001", CancellationToken.None);

        Assert.Null(result);
    }
}

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; }

    public FakeClock(DateTimeOffset initial) => UtcNow = initial;
}
