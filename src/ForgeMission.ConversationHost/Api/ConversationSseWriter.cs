using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Microsoft.AspNetCore.Http;

namespace ForgeMission.ConversationHost.Api;

/// <summary>
/// Owns SSE response framing and the replay/live handoff for
/// <c>GET /conversations/{conversationId}/events</c> (Task 6). Table remains the recovery source:
/// a Host restart, notifier loss, a full client channel, and an ordinary network disconnect are all
/// reconnect conditions, never data loss.
///
/// Fixed ordering, carrying one local <c>cursor</c> starting at the caller's requested
/// <c>after</c>: (1) read and emit durable Table events with <c>Sequence &gt; cursor</c>, advancing
/// <c>cursor</c> after each; (2) subscribe to the address's hub BEFORE the final catch-up; (3) read
/// and emit durable events after the new cursor again, skipping anything already emitted; (4) drain
/// the subscription, emitting only events past <c>cursor</c>. The first replay supplies history;
/// subscribing before the second read closes the append race (an event appended between step 1 and
/// the subscribe would otherwise never publish to a not-yet-existing subscription); the second
/// durable read closes the subscribe race (an event appended between subscribe and the second read
/// would otherwise arrive only via the live channel, which this second read already covers); the
/// cursor removes the harmless duplicate an append landing in both sources could otherwise produce.
/// </summary>
public sealed class ConversationSseWriter(IConversationEventStore eventStore, IConversationEventNotifier notifier)
{
    public async Task WriteAsync(HttpResponse response, ConversationAddress address, long after, CancellationToken ct)
    {
        response.Headers.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        // Flush headers immediately, independent of how long the first body write takes — a client
        // using HttpCompletionOption.ResponseHeadersRead must not block on the first durable/live
        // event when there happens to be none yet.
        await response.StartAsync(ct);

        var cursor = await ReplayDurableEventsAsync(response, address, after, ct);

        await using var subscription = notifier.Subscribe(address);

        cursor = await ReplayDurableEventsAsync(response, address, cursor, ct);

        await foreach (var @event in subscription.Reader.ReadAllAsync(ct))
        {
            if (@event.Sequence <= cursor)
                continue; // Already emitted by one of the two durable reads above — harmless overlap.

            await WriteEventAsync(response, @event, ct);
            cursor = @event.Sequence;
        }
    }

    /// <summary>Trusts <see cref="IConversationEventStore.ReadAfterAsync"/>'s own postcondition
    /// (strictly <c>Sequence &gt; after</c>, ascending) rather than re-checking it here.</summary>
    private async Task<long> ReplayDurableEventsAsync(HttpResponse response, ConversationAddress address, long after, CancellationToken ct)
    {
        var cursor = after;
        await foreach (var @event in eventStore.ReadAfterAsync(address, after, ct))
        {
            await WriteEventAsync(response, @event, ct);
            cursor = @event.Sequence;
        }
        return cursor;
    }

    private static async Task WriteEventAsync(HttpResponse response, ConversationEvent @event, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(@event, ConversationContractsJsonContext.Default.ConversationEvent);
        await response.WriteAsync($"event: conversation-event\nid: {@event.Sequence}\ndata: {json}\n\n", ct);
        await response.Body.FlushAsync(ct);
    }
}
