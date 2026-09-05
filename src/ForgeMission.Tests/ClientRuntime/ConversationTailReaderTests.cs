using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

public sealed class ConversationTailReaderTests
{
    [Fact]
    public async Task Gap_PublishesAnError_AndReconnectsFromTheUnadvancedCursor()
    {
        var conversationId = Guid.NewGuid();
        var handler = new TailHandler(conversationId, [Sse(Event(conversationId, 2)), Sse(Event(conversationId, 1))]);
        var events = new ConcurrentQueue<ClientRuntimeEvent>();
        await using var tail = NewTail(handler, events.Enqueue);

        tail.Start(conversationId);
        await WaitUntilAsync(() => handler.After.Count >= 2 && events.Any(x => x.Kind == ClientRuntimeEventKind.ConversationEvent));

        Assert.Equal([0L, 0L], handler.After.Take(2));
        Assert.Contains(events, x => x.Kind == ClientRuntimeEventKind.Error);
        Assert.Single(events, x => x.Kind == ClientRuntimeEventKind.ConversationEvent);
    }

    [Fact]
    public async Task FailedHook_DoesNotAdvanceCursor_AndTheReplayedEventIsRetried()
    {
        var conversationId = Guid.NewGuid();
        var evt = Event(conversationId, 1);
        var handler = new TailHandler(conversationId, [Sse(evt), Sse(evt)]);
        var attempts = 0;
        await using var tail = new ConversationTailReader("session", Host(handler), _ => { }, CancellationToken.None,
            (_, _) =>
            {
                if (Interlocked.Increment(ref attempts) == 1) throw new InvalidOperationException("first delivery failed");
                return Task.CompletedTask;
            });

        tail.Start(conversationId);
        await WaitUntilAsync(() => Volatile.Read(ref attempts) >= 2 && handler.After.Count >= 2);

        Assert.Equal([0L, 0L], handler.After.Take(2));
    }

    [Fact]
    public void TailReader_DoesNotRetainAnUnboundedEventIdHistory()
    {
        Assert.DoesNotContain(typeof(ConversationTailReader).GetFields(
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic), field =>
            field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(HashSet<>));
    }

    private static ConversationTailReader NewTail(HttpMessageHandler handler, Action<ClientRuntimeEvent> publish) =>
        new("session", Host(handler), publish, CancellationToken.None);

    private static ConversationHostClient Host(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") });

    private static ConversationEvent Event(Guid conversationId, long sequence) => new(Guid.NewGuid(), 1, conversationId,
        Guid.NewGuid(), sequence, ConversationEventKind.RunStatus, ConversationParticipant.Forge, null, null, null, null,
        null, null, null, ConversationRunStatus.Running, DateTimeOffset.UtcNow);

    private static string Sse(ConversationEvent evt) => $"data: {JsonSerializer.Serialize(evt, ConversationContractsJsonContext.Default.ConversationEvent)}\n\n";

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Tail did not reach expected state.");
            await Task.Delay(10);
        }
    }

    private sealed class TailHandler(Guid conversationId, IEnumerable<string> bodies) : HttpMessageHandler
    {
        private readonly Queue<string> _bodies = new(bodies);
        public List<long> After { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != $"/conversations/{conversationId}/events")
                throw new InvalidOperationException($"Unexpected request {request.Method} {request.RequestUri}");
            var after = long.Parse(request.RequestUri.Query.Split("after=")[1]);
            lock (After) After.Add(after);
            var body = _bodies.Count == 0 ? string.Empty : _bodies.Dequeue();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") });
        }
    }
}
