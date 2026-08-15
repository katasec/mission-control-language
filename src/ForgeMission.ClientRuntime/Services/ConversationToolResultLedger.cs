using System.Text.Json;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.Services;

internal enum ToolResultLedgerState
{
    Started,
    Executed,
    Acknowledged,
}

// ResultContent/ResultIsError hold the actual local tool output — potentially sensitive (file
// contents, command output) — and are populated ONLY while State is Executed and not yet
// Acknowledged: the exact window a resend might be needed after a lost Host acknowledgement.
// MarkAcknowledgedAsync strips them immediately, retaining only enough to know this RequestId is
// fully settled (Phase 43.16 Task 8d).
internal sealed record ToolResultLedgerEntry(
    Guid RequestId,
    ToolResultLedgerState State,
    string? ResultContent,
    bool? ResultIsError,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? ExecutedAtUtc,
    DateTimeOffset? AcknowledgedAtUtc);

internal sealed record ToolResultLedgerFile(List<ToolResultLedgerEntry> Entries);

// One durable, append/overwrite-in-place record per (ConversationId, RequestId), stored under the
// same user-profile application-data location as ConversationResumeStore. Read-modify-write of the
// whole per-conversation file, gated by a process-local semaphore — conversations see at most a
// handful of tool calls, so this stays simple rather than needing per-entry file locking.
internal sealed class ConversationToolResultLedger
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ConversationToolResultLedger(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "forge-client-runtime", "conversations");
    }

    public Task MarkStartedAsync(Guid conversationId, Guid requestId, CancellationToken ct) =>
        MutateAsync(conversationId, entries =>
        {
            entries.RemoveAll(e => e.RequestId == requestId);
            entries.Add(new ToolResultLedgerEntry(
                requestId, ToolResultLedgerState.Started, null, null, DateTimeOffset.UtcNow, null, null));
        }, ct);

    public Task MarkExecutedAsync(Guid conversationId, Guid requestId, ToolExecutionResult result, CancellationToken ct) =>
        MutateAsync(conversationId, entries =>
        {
            var existing = entries.FirstOrDefault(e => e.RequestId == requestId);
            entries.RemoveAll(e => e.RequestId == requestId);
            entries.Add(new ToolResultLedgerEntry(
                requestId, ToolResultLedgerState.Executed, result.Content, result.IsError,
                existing?.StartedAtUtc ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null));
        }, ct);

    // Strips the raw result content on settlement — no lingering potentially-sensitive payload
    // once the Host has durably confirmed it.
    public Task MarkAcknowledgedAsync(Guid conversationId, Guid requestId, CancellationToken ct) =>
        MutateAsync(conversationId, entries =>
        {
            var existing = entries.FirstOrDefault(e => e.RequestId == requestId);
            if (existing is null)
                return;

            entries.RemoveAll(e => e.RequestId == requestId);
            entries.Add(existing with
            {
                State = ToolResultLedgerState.Acknowledged,
                ResultContent = null,
                ResultIsError = null,
                AcknowledgedAtUtc = DateTimeOffset.UtcNow,
            });
        }, ct);

    public async Task<ToolResultLedgerEntry?> TryGetAsync(Guid conversationId, Guid requestId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var entries = await ReadAsync(conversationId, ct);
            return entries.FirstOrDefault(e => e.RequestId == requestId);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task MutateAsync(Guid conversationId, Action<List<ToolResultLedgerEntry>> mutate, CancellationToken ct)
    {
        Directory.CreateDirectory(_directory);
        await _gate.WaitAsync(ct);
        try
        {
            var entries = await ReadAsync(conversationId, ct);
            var mutable = entries.ToList();
            mutate(mutable);
            await using var stream = File.Create(PathFor(conversationId));
            await JsonSerializer.SerializeAsync(
                stream, new ToolResultLedgerFile(mutable), ConversationRecoveryJsonContext.Default.ToolResultLedgerFile, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // Gate-protected by both callers above — never called without _gate already held.
    private async Task<IReadOnlyList<ToolResultLedgerEntry>> ReadAsync(Guid conversationId, CancellationToken ct)
    {
        var path = PathFor(conversationId);
        if (!File.Exists(path))
            return [];

        await using var stream = File.OpenRead(path);
        var file = await JsonSerializer.DeserializeAsync(stream, ConversationRecoveryJsonContext.Default.ToolResultLedgerFile, ct);
        return file?.Entries ?? [];
    }

    private string PathFor(Guid conversationId) => Path.Combine(_directory, $"{conversationId:N}.ledger.json");
}
