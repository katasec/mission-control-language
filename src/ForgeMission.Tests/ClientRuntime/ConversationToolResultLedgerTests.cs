using ForgeMission.ClientRuntime.Services;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ConversationToolResultLedgerTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("forge-tool-ledger-").FullName;
    private readonly ConversationToolResultLedger _ledger;

    public ConversationToolResultLedgerTests() => _ledger = new ConversationToolResultLedger(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task UnseenRequestId_HasNoLedgerEntry()
    {
        var entry = await _ledger.TryGetAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task MarkStarted_ThenTryGet_ReportsStartedWithNoResult()
    {
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        await _ledger.MarkStartedAsync(conversationId, requestId, CancellationToken.None);
        var entry = await _ledger.TryGetAsync(conversationId, requestId, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(ToolResultLedgerState.Started, entry.State);
        Assert.Null(entry.ResultContent);
    }

    [Fact]
    public async Task MarkExecuted_ThenTryGet_ReportsExecutedWithTheHeldResult()
    {
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await _ledger.MarkStartedAsync(conversationId, requestId, CancellationToken.None);

        await _ledger.MarkExecutedAsync(conversationId, requestId, new ToolExecutionResult("file contents here", false), CancellationToken.None);
        var entry = await _ledger.TryGetAsync(conversationId, requestId, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(ToolResultLedgerState.Executed, entry.State);
        Assert.Equal("file contents here", entry.ResultContent);
        Assert.False(entry.ResultIsError);
    }

    // Phase 43.16 Task 8d correction: raw tool output can be sensitive (file/command content) and
    // must not linger once the Host has durably confirmed it.
    [Fact]
    public async Task MarkAcknowledged_ClearsThePersistedResultContent_ButKeepsTheEntryQueryableAsAcknowledged()
    {
        var conversationId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        await _ledger.MarkStartedAsync(conversationId, requestId, CancellationToken.None);
        await _ledger.MarkExecutedAsync(conversationId, requestId, new ToolExecutionResult("the secret word is PLATYPUS", false), CancellationToken.None);

        await _ledger.MarkAcknowledgedAsync(conversationId, requestId, CancellationToken.None);
        var entry = await _ledger.TryGetAsync(conversationId, requestId, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(ToolResultLedgerState.Acknowledged, entry.State);
        Assert.Null(entry.ResultContent);
        Assert.Null(entry.ResultIsError);
    }

    [Fact]
    public async Task MarkAcknowledged_ForAnUnknownRequestId_IsANoOp()
    {
        var conversationId = Guid.NewGuid();

        await _ledger.MarkAcknowledgedAsync(conversationId, Guid.NewGuid(), CancellationToken.None);
        var entry = await _ledger.TryGetAsync(conversationId, Guid.NewGuid(), CancellationToken.None);

        Assert.Null(entry);
    }

    [Fact]
    public async Task EntriesForDifferentConversations_NeverCollide()
    {
        var requestId = Guid.NewGuid();
        var conversationA = Guid.NewGuid();
        var conversationB = Guid.NewGuid();
        await _ledger.MarkStartedAsync(conversationA, requestId, CancellationToken.None);

        var entryB = await _ledger.TryGetAsync(conversationB, requestId, CancellationToken.None);

        Assert.Null(entryB);
    }
}
