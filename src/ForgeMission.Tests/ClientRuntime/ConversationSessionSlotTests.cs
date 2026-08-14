using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.ClientRuntime.TransportHost;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.Configuration;

namespace ForgeMission.Tests.ClientRuntime;

// Covers the ConversationSessionSlot lifecycle fix: durable prompt admission, lazy
// ConversationRuntimeSession creation, and SendAsync are one operation serialized by the slot's
// own gate, never exposed as separate GetOrCreate/SendAsync steps a caller could interleave with
// a mission-switch replacement's disposal. See ClientRuntimeSessionStore.cs for the full race
// this closes.
public sealed class ConversationSessionSlotTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-conversation-slot-").FullName;
    private readonly ClientRuntimeSessionStore _store = new(new ClientRuntimeEventHub(), new ConfigurationBuilder().Build());

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public async Task PromptThatObtainedAReplacedSession_IsRejected_WithNoHostCallOrCreatedSession()
    {
        var oldSession = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);
        var handler = new RecordingConversationHostHandler(Guid.NewGuid(), Guid.NewGuid());
        var factoryCalled = false;

        // Simulates the exact race: a /transport/prompt request already obtained oldSession via
        // ClientRuntimeSessionStore.TryGet (this same reference) but had not yet called
        // SendPromptAsync when the mission switch below fully replaces (and disposes) it.
        await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation,
            replacesSessionId: oldSession.Id);

        // The paused prompt now "resumes" and finally calls SendPromptAsync on the replaced session.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            oldSession.Conversation.SendPromptAsync(
                () =>
                {
                    factoryCalled = true;
                    return NewSession(handler, oldSession.Id);
                },
                "Build the thing.", CancellationToken.None));

        Assert.False(factoryCalled); // never created/started a durable session
        Assert.Empty(handler.PostBodies); // never reached the Host — no Start request, no tail
    }

    [Fact]
    public async Task ConcurrentFirstPrompts_ProduceExactlyOneStart_AndOneFollowUp_WithOneConversationId()
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var handler = new RecordingConversationHostHandler(conversationId, runId);
        var session = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);
        var factoryCallCount = 0;

        ConversationRuntimeSession Factory()
        {
            Interlocked.Increment(ref factoryCallCount);
            return NewSession(handler, session.Id);
        }

        try
        {
            var first = session.Conversation.SendPromptAsync(Factory, "First prompt.", CancellationToken.None);
            var second = session.Conversation.SendPromptAsync(Factory, "Second prompt.", CancellationToken.None);
            var ids = await Task.WhenAll(first, second);

            Assert.Equal(1, factoryCallCount); // the same underlying session is reused, never a second one
            Assert.Equal(conversationId, ids[0]);
            Assert.Equal(conversationId, ids[1]); // both callers retain the one ConversationId

            Assert.Equal(2, handler.PostBodies.Count);
            Assert.False(handler.PostBodies[0].TryGetProperty("text", out _)); // the Start request
            Assert.True(handler.PostBodies[0].TryGetProperty("missionRef", out _));
            Assert.True(handler.PostBodies[1].TryGetProperty("text", out _)); // the follow-up request
        }
        finally
        {
            await session.Conversation.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposeAsync_CalledTwiceConcurrently_DoesNotThrow_AndDisposesTheUnderlyingSessionOnlyOnce()
    {
        var handler = new RecordingConversationHostHandler(Guid.NewGuid(), Guid.NewGuid());
        var session = await _store.CreateAsync(_workspace, "Janus", SessionRuntimeKind.DurableConversation);
        await session.Conversation.SendPromptAsync(
            () => NewSession(handler, session.Id), "Build the thing.", CancellationToken.None);

        var first = session.Conversation.DisposeAsync().AsTask();
        var second = session.Conversation.DisposeAsync().AsTask();
        // Must not throw — a naive implementation double-disposing the underlying
        // ConversationRuntimeSession would surface as ObjectDisposedException from its already-
        // disposed CancellationTokenSource here.
        await Task.WhenAll(first, second);

        await session.Conversation.DisposeAsync(); // a third, later call is also a safe no-op
    }

    private ConversationRuntimeSession NewSession(HttpMessageHandler handler, string sessionId)
    {
        var workspace = new LocalDiskWorkspace(_workspace);
        var capabilities = new CapabilityRegistry([new WorkspaceFileProvider(workspace), new WorkspaceTerminalProvider(workspace)]);
        var dispatcher = new CapabilityDispatcher(
            capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default), new InMemoryCapabilityAuditLog());
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") };
        return new ConversationRuntimeSession(
            sessionId, "Janus", new ConversationHostClient(http), capabilities, dispatcher, _ => { }, CancellationToken.None);
    }

    private sealed class RecordingConversationHostHandler(Guid conversationId, Guid runId) : HttpMessageHandler
    {
        private readonly object _gate = new();

        public List<JsonElement> PostBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post &&
                (path == "/conversations" || path.EndsWith("/commands", StringComparison.Ordinal)))
            {
                await using var stream = await request.Content!.ReadAsStreamAsync(ct);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                lock (_gate)
                    PostBodies.Add(document.RootElement.Clone());

                var payload = JsonSerializer.Serialize(new { conversationId, runId, acceptedSequence = 2, status = "queued" });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json"),
                };
            }

            if (request.Method == HttpMethod.Get && path.EndsWith("/events", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(string.Empty, Encoding.UTF8, "text/event-stream"),
                };
            }

            throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");
        }
    }
}
