using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>
/// Row layout: an "Event" row (<c>0-{sequence:D19}</c>, holding <c>EventJson</c>/<c>RunId</c>/
/// <c>OccurredAtUtc</c>) and an "Idempotency" row (<c>1-{eventId:N}</c>, holding
/// <c>Sequence</c>/<c>EventJson</c>) in the same partition/transaction per append. Does not create
/// the table — 350 IaC owns table existence in production; the Azurite test fixture owns it there.
/// </summary>
public sealed class AzureTableConversationEventStore : IConversationEventStore
{
    private const string EventRowPrefix = "0-";
    private const string IdempotencyRowPrefix = "1-";

    private readonly TableClient _table;

    public AzureTableConversationEventStore(TableServiceClient tableServiceClient, ConversationStorageOptions options)
    {
        _table = tableServiceClient.GetTableClient(options.EventTableName);
    }

    public async Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
    {
        var raw = await FindIdempotencyRowAsync(address, eventId, ct);
        return raw is null ? null : new StoredConversationEvent(DeserializeEvent(raw.Value.EventJson), raw.Value.AcceptedCommandJson);
    }

    public async Task<ConversationEvent> AppendAsync(
        ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct)
    {
        if (@event.ConversationId != address.ConversationId)
            throw new InvalidOperationException(
                $"Event conversation id '{@event.ConversationId}' does not match address '{address.ConversationId}'.");
        if (@event.Version != 1)
            throw new InvalidOperationException($"Unsupported ConversationEvent.Version {@event.Version}; only 1 is supported.");
        if (@event.Sequence <= 0)
            throw new InvalidOperationException($"ConversationEvent.Sequence must be positive; got {@event.Sequence}.");

        var eventJson = JsonSerializer.Serialize(@event, ConversationContractsJsonContext.Default.ConversationEvent);
        var byteCount = Encoding.UTF8.GetByteCount(eventJson);
        if (byteCount > ConversationJsonLimits.MaxInlineEventJsonBytes)
            throw new InvalidOperationException(
                $"ConversationEvent {@event.EventId} serializes to {byteCount} bytes, exceeding the " +
                $"{ConversationJsonLimits.MaxInlineEventJsonBytes}-byte inline limit. Large content must go through " +
                "IConversationArtifactStore before its reference event is appended.");

        var existing = await FindIdempotencyRowAsync(address, @event.EventId, ct);
        if (existing is not null)
            return AcceptExistingOrThrow(existing.Value, @event, eventJson, associatedCommandJson);

        var eventEntity = new TableEntity(address.PartitionKey, EventRowKey(@event.Sequence))
        {
            ["EventJson"] = eventJson,
            ["OccurredAtUtc"] = @event.OccurredAtUtc,
        };
        if (@event.RunId is { } runId)
            eventEntity["RunId"] = runId;

        var idempotencyEntity = new TableEntity(address.PartitionKey, IdempotencyRowKey(@event.EventId))
        {
            ["Sequence"] = @event.Sequence,
            ["EventJson"] = eventJson,
        };
        if (associatedCommandJson is not null)
            idempotencyEntity["AcceptedCommandJson"] = associatedCommandJson;

        try
        {
            await _table.SubmitTransactionAsync(
                [
                    new TableTransactionAction(TableTransactionActionType.Add, eventEntity),
                    new TableTransactionAction(TableTransactionActionType.Add, idempotencyEntity),
                ], ct);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.Conflict)
        {
            // Concurrent duplicate append. Resolve the event-ID row again: only a complete, equal
            // event (and equal associated command, when either side has one) counts as the same
            // duplicate. No matching row means this was a genuine sequence/row-key collision,
            // never classified as a duplicate.
            var raced = await FindIdempotencyRowAsync(address, @event.EventId, ct);
            if (raced is not null)
                return AcceptExistingOrThrow(raced.Value, @event, eventJson, associatedCommandJson);

            throw new InvalidOperationException(
                $"Table conflict appending event {@event.EventId} at sequence {@event.Sequence} with no " +
                "matching event-ID row — a sequence/row-key collision, not a duplicate.", ex);
        }

        return @event;
    }

    public async IAsyncEnumerable<ConversationEvent> ReadAfterAsync(
        ConversationAddress address, long sequence, [EnumeratorCancellation] CancellationToken ct)
    {
        var lowerBound = EventRowKey(sequence + 1);
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {address.PartitionKey} and RowKey ge {lowerBound} and RowKey lt {IdempotencyRowPrefix}");

        await foreach (var entity in _table.QueryAsync<TableEntity>(filter, cancellationToken: ct))
            yield return DeserializeEvent(entity.GetString("EventJson")!);
    }

    public async Task<ConversationEvent[]> ReadRangeAsync(
        ConversationAddress address, long after, long through, int count, CancellationToken ct)
    {
        if (after < 0 || through < after || count <= 0)
            throw new ArgumentOutOfRangeException();

        var lowerBound = EventRowKey(after + 1);
        var upperBound = EventRowKey(through + 1);
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {address.PartitionKey} and RowKey ge {lowerBound} and RowKey lt {upperBound}");
        var events = new List<ConversationEvent>(count);
        await foreach (var entity in _table.QueryAsync<TableEntity>(filter, cancellationToken: ct))
        {
            events.Add(DeserializeEvent(entity.GetString("EventJson")!));
            if (events.Count == count) break;
        }
        return [.. events];
    }

    public async Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
    {
        var filter = TableClient.CreateQueryFilter(
            $"PartitionKey eq {address.PartitionKey} and RowKey ge {EventRowPrefix} and RowKey lt {IdempotencyRowPrefix} and RunId eq {runId}");

        ConversationEvent? latest = null;
        await foreach (var entity in _table.QueryAsync<TableEntity>(filter, cancellationToken: ct))
        {
            var candidate = DeserializeEvent(entity.GetString("EventJson")!);
            if (candidate.Kind == ConversationEventKind.RunStatus)
                latest = candidate;
        }

        return latest;
    }

    private async Task<(long Sequence, string EventJson, string? AcceptedCommandJson)?> FindIdempotencyRowAsync(
        ConversationAddress address, Guid eventId, CancellationToken ct)
    {
        var response = await _table.GetEntityIfExistsAsync<TableEntity>(
            address.PartitionKey, IdempotencyRowKey(eventId), cancellationToken: ct);

        return response.HasValue
            ? (response.Value!.GetInt64("Sequence")!.Value, response.Value!.GetString("EventJson")!,
               response.Value!.GetString("AcceptedCommandJson"))
            : null;
    }

    // Compares serialized EventJson text rather than record equality: ConversationEvent.ToolRequest
    // is a JsonElement whose default equality is not content-based, but the persisted JSON text is
    // the actual authoritative representation and is deterministic for the same object graph. Also
    // compares the associated command JSON when either side has one, so a reused CommandId with
    // changed command content (MissionRef/Goal/Kind/Capabilities/ToolResult) is never silently
    // accepted as the same duplicate.
    private static ConversationEvent AcceptExistingOrThrow(
        (long Sequence, string EventJson, string? AcceptedCommandJson) existing,
        ConversationEvent proposed, string proposedJson, string? proposedCommandJson)
    {
        var eventMatches = string.Equals(existing.EventJson, proposedJson, StringComparison.Ordinal);
        var commandMatches = string.Equals(existing.AcceptedCommandJson, proposedCommandJson, StringComparison.Ordinal);

        if (eventMatches && commandMatches)
            return DeserializeEvent(existing.EventJson);

        throw new InvalidOperationException(
            $"Event {proposed.EventId} was already recorded with different content — refusing to " +
            "silently accept a mismatched retry.");
    }

    private static ConversationEvent DeserializeEvent(string json)
        => JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)
           ?? throw new InvalidOperationException("Stored EventJson deserialized to null.");

    private static string EventRowKey(long sequence) => $"{EventRowPrefix}{sequence:D19}";
    private static string IdempotencyRowKey(Guid eventId) => $"{IdempotencyRowPrefix}{eventId:N}";
}
