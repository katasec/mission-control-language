using System.Text.Json;
using System.Text.Json.Serialization;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ClientRuntime.Services;

// One file per ConversationId under a user-profile application-data directory — never inside the
// git-tracked workspace. Contains no tool output, only routing/discovery metadata (Phase 43.16
// Task 8d). Records are never deleted by this class: a completed conversation's transcript must
// stay reopenable after any number of Desktop restarts. A history/clear action is a separate,
// not-yet-designed task.
internal sealed record ResumeRecord(
    Guid ConversationId,
    string WorkspaceRoot,
    string MissionRef,
    ConversationRunStatus Status,
    DateTimeOffset CreatedAtUtc);

internal sealed class ConversationResumeStore
{
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ConversationResumeStore(string? directory = null)
    {
        _directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "forge-client-runtime", "conversations");
    }

    // Scoped by WorkspaceRoot AND MissionRef — a session for a different mission never sees this
    // workspace's records, even though both are legitimate discovery inputs (Phase 43.16 Task 8d
    // admission rule: workspace match alone is insufficient).
    public async Task<IReadOnlyList<ResumeRecord>> FindAsync(
        string workspaceRoot, string missionRef, CancellationToken ct)
    {
        if (!Directory.Exists(_directory))
            return [];

        var results = new List<ResumeRecord>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.resume.json"))
        {
            var record = await ReadAsync(path, ct);
            if (record is not null
                && string.Equals(record.WorkspaceRoot, workspaceRoot, StringComparison.Ordinal)
                && string.Equals(record.MissionRef, missionRef, StringComparison.Ordinal))
                results.Add(record);
        }

        return results;
    }

    // Preserves the original CreatedAtUtc across repeated upserts (e.g. every resume) so a
    // conversation's discovery record always reflects when it truly started, not when it was
    // last reattached.
    public async Task UpsertAsync(ResumeRecord record, CancellationToken ct)
    {
        Directory.CreateDirectory(_directory);
        var path = PathFor(record.ConversationId);

        await _gate.WaitAsync(ct);
        try
        {
            var existing = await ReadAsync(path, ct);
            var toWrite = existing is null ? record : record with { CreatedAtUtc = existing.CreatedAtUtc };
            await using var stream = File.Create(path);
            await JsonSerializer.SerializeAsync(stream, toWrite, ConversationRecoveryJsonContext.Default.ResumeRecord, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private string PathFor(Guid conversationId) => Path.Combine(_directory, $"{conversationId:N}.resume.json");

    private static async Task<ResumeRecord?> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, ConversationRecoveryJsonContext.Default.ResumeRecord, ct);
    }
}

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(ResumeRecord))]
[JsonSerializable(typeof(ToolResultLedgerFile))]
internal partial class ConversationRecoveryJsonContext : JsonSerializerContext;
