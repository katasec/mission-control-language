using ForgeMission.ClientRuntime.Services;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ConversationResumeStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("forge-resume-store-").FullName;
    private readonly ConversationResumeStore _store;

    public ConversationResumeStoreTests() => _store = new ConversationResumeStore(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task UpsertThenFind_RoundTripsTheRecord()
    {
        var record = new ResumeRecord(Guid.NewGuid(), "/workspace/a", "Janus", ConversationRunStatus.Queued, DateTimeOffset.UtcNow);

        await _store.UpsertAsync(record, CancellationToken.None);
        var found = await _store.FindAsync("/workspace/a", "Janus", CancellationToken.None);

        var single = Assert.Single(found);
        Assert.Equal(record.ConversationId, single.ConversationId);
        Assert.Equal(record.Status, single.Status);
    }

    [Fact]
    public async Task Find_NeverReturnsARecordForADifferentWorkspaceRoot()
    {
        await _store.UpsertAsync(
            new ResumeRecord(Guid.NewGuid(), "/workspace/a", "Janus", ConversationRunStatus.Queued, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var found = await _store.FindAsync("/workspace/b", "Janus", CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public async Task Find_NeverReturnsARecordForADifferentMissionRef()
    {
        await _store.UpsertAsync(
            new ResumeRecord(Guid.NewGuid(), "/workspace/a", "Janus", ConversationRunStatus.Queued, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var found = await _store.FindAsync("/workspace/a", "SomeOtherMission", CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public async Task Upsert_ForATerminalConversation_IsStillReturnedByFind()
    {
        var record = new ResumeRecord(Guid.NewGuid(), "/workspace/a", "Janus", ConversationRunStatus.Completed, DateTimeOffset.UtcNow);

        await _store.UpsertAsync(record, CancellationToken.None);
        var found = await _store.FindAsync("/workspace/a", "Janus", CancellationToken.None);

        Assert.Single(found); // no deletion by terminal status — retained for a Desktop restart
    }

    [Fact]
    public async Task RepeatedUpsert_PreservesTheOriginalCreatedAtUtc()
    {
        var conversationId = Guid.NewGuid();
        var originalCreatedAt = DateTimeOffset.UtcNow.AddDays(-3);
        await _store.UpsertAsync(
            new ResumeRecord(conversationId, "/workspace/a", "Janus", ConversationRunStatus.Queued, originalCreatedAt),
            CancellationToken.None);

        await _store.UpsertAsync(
            new ResumeRecord(conversationId, "/workspace/a", "Janus", ConversationRunStatus.Completed, DateTimeOffset.UtcNow),
            CancellationToken.None);

        var found = await _store.FindAsync("/workspace/a", "Janus", CancellationToken.None);
        var single = Assert.Single(found);
        Assert.Equal(originalCreatedAt, single.CreatedAtUtc);
        Assert.Equal(ConversationRunStatus.Completed, single.Status); // status still updates
    }
}
