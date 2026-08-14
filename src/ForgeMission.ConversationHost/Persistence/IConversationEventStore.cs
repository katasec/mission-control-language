using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Persistence;

/// <summary>
/// The only application-level reader/writer of <c>forgeconversationevents</c>. Does not generate
/// IDs, sequences, timestamps, or Table keys — those are grain-owned; this store only persists and
/// replays what the grain already decided. A plain Host-internal service: every method here takes
/// and returns real Contracts types directly, because a call to this interface never crosses an
/// Orleans grain boundary.
/// </summary>
public interface IConversationEventStore
{
    /// <summary>Resolves one event by its stable ID together with whatever
    /// <see cref="ConversationCommand"/> JSON was recorded alongside it (non-null only for the
    /// deterministic <c>UserMessage</c> a start/follow-up command produced) — so a caller can
    /// compare full command content itself rather than relying on <see cref="AppendAsync"/>'s own
    /// equality-throw for duplicate-command classification.</summary>
    Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct);

    /// <summary>
    /// Rejects a wrong conversation ID, <c>Version != 1</c>, a non-positive sequence, or an
    /// oversized inline payload. Resolves <paramref name="event"/>'s <c>EventId</c> first: if an
    /// idempotency row already exists for it, returns the stored event only when it is the
    /// complete, equal event <em>and</em> <paramref name="associatedCommandJson"/> equals whatever
    /// was recorded alongside it — otherwise throws a clear invariant violation, so a reused
    /// CommandId with changed command content never silently returns the prior acceptance. A Table
    /// conflict with no matching event-ID row is a sequence/row-key collision, never a duplicate,
    /// and also fails loudly.
    /// </summary>
    /// <param name="associatedCommandJson">The full accepted <see cref="ConversationCommand"/>,
    /// source-generated to JSON, when this event is the deterministic <c>UserMessage</c> a command
    /// produced — persisted on the idempotency row (never transcript data) so a later duplicate
    /// CommandId can be validated against it. Null for every other event.</param>
    Task<ConversationEvent> AppendAsync(
        ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct);

    /// <summary>Ascending order, only <c>Sequence &gt; after</c>. Idempotency rows are never transcript data.</summary>
    IAsyncEnumerable<ConversationEvent> ReadAfterAsync(ConversationAddress address, long sequence, CancellationToken ct);

    /// <summary>Enumerates the ordered conversation range and retains the last event for
    /// <paramref name="runId"/> whose <c>Kind</c> is <see cref="ConversationEventKind.RunStatus"/>.</summary>
    Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct);
}

/// <summary>Host-local pairing of one durable event with its idempotency row's associated command
/// JSON, when it has one. Never crosses an Orleans grain-interface boundary — this is a plain
/// Host-internal service return type, not a wire contract.</summary>
public sealed record StoredConversationEvent(ConversationEvent Event, string? AcceptedCommandJson);
