using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.ClientRuntime;

// Exercises ConversationRuntimeSession/ConversationHostClient against a scripted HttpMessageHandler
// standing in for the Task 6 HTTP/SSE contract — no ConversationHost/Azurite dependency, matching
// the project-boundary rule that Client Runtime's own test project never references Host. A real
// LocalDiskWorkspace/CapabilityDispatcher/ToolExecutorRegistry proves the local tool hand-off,
// mirroring MissionRuntimeSessionTests' own real-workspace pattern.
public sealed class ConversationRuntimeSessionTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("forge-conversation-session-").FullName;

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    [Fact]
    public async Task SendAsync_FirstPrompt_StartsConversation_WithCapabilityDeclarations_AndRetainsId()
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var handler = new ScriptedConversationHostHandler(conversationId, runId, new Queue<string>());
        var (capabilities, dispatcher) = BuildWorkspace();
        var published = new List<ClientRuntimeEvent>();

        await using var session = NewSession(handler, capabilities, dispatcher, published.Add);

        var returnedId = await session.SendAsync("Build the thing.", CancellationToken.None);

        Assert.Equal(conversationId, returnedId);
        var startBody = handler.PostBodies[0];
        Assert.Equal("Janus", startBody.GetProperty("missionRef").GetString());
        Assert.Equal("Build the thing.", startBody.GetProperty("goal").GetString());
        var capabilityNames = startBody.GetProperty("capabilities").EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()).ToList();
        Assert.Contains("Read", capabilityNames);
        Assert.Contains("Bash", capabilityNames);

        var followUpId = await session.SendAsync("A follow-up.", CancellationToken.None);

        Assert.Equal(conversationId, followUpId);
        Assert.Equal(2, handler.PostBodies.Count);
        var followUpBody = handler.PostBodies[1];
        Assert.Equal("A follow-up.", followUpBody.GetProperty("text").GetString());
        Assert.Equal(conversationId.ToString(), followUpBody.GetProperty("conversationId").GetString(),
            ignoreCase: true);
    }

    [Fact]
    public async Task Tail_ParsesARealShapedMultiFrameSseStream_AndRelaysEachEvent()
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var userEvent = NewEvent(conversationId, runId, 1, ConversationEventKind.UserMessage,
            ConversationParticipant.User, text: "Build the thing.");
        var startedEvent = NewEvent(conversationId, runId, 2, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, attempt: 1);
        var messageEvent = NewEvent(conversationId, runId, 3, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, attempt: 1, text: "Here is a plan.");
        var sse = ToSseBody(userEvent, startedEvent, messageEvent);
        var handler = new ScriptedConversationHostHandler(conversationId, runId, new Queue<string>([sse]));
        var (capabilities, dispatcher) = BuildWorkspace();
        var published = new List<ClientRuntimeEvent>();

        await using var session = NewSession(handler, capabilities, dispatcher, published.Add);
        await session.SendAsync("Build the thing.", CancellationToken.None);

        await WaitUntilAsync(() => published.Count(p => p.Kind == ClientRuntimeEventKind.ConversationEvent) >= 3);

        var relayed = published.Where(p => p.Kind == ClientRuntimeEventKind.ConversationEvent)
            .Select(p => p.Conversation!.EventId).ToList();
        Assert.Equal([userEvent.EventId, startedEvent.EventId, messageEvent.EventId], relayed);
    }

    [Fact]
    public async Task Tail_Reconnects_FromLastDeliveredSequence_AndIgnoresARedeliveredDuplicateEvent()
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var firstEvent = NewEvent(conversationId, runId, 1, ConversationEventKind.UserMessage,
            ConversationParticipant.User, text: "Build the thing.");
        var secondEvent = NewEvent(conversationId, runId, 2, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, runStatus: ConversationRunStatus.Queued);
        // First connection: delivers event 1, then the connection ends (dropped/completed).
        // Reconnect: the Host redelivers event 1 (a harmless overlap duplicate) followed by the
        // genuinely new event 2.
        var firstConnectionBody = ToSseBody(firstEvent);
        var secondConnectionBody = ToSseBody(firstEvent, secondEvent);
        var handler = new ScriptedConversationHostHandler(
            conversationId, runId, new Queue<string>([firstConnectionBody, secondConnectionBody]));
        var (capabilities, dispatcher) = BuildWorkspace();
        var published = new List<ClientRuntimeEvent>();

        await using var session = NewSession(handler, capabilities, dispatcher, published.Add);
        await session.SendAsync("Build the thing.", CancellationToken.None);

        await WaitUntilAsync(() => handler.SseAfterValues.Count >= 2);
        await WaitUntilAsync(() => published.Count(p => p.Kind == ClientRuntimeEventKind.ConversationEvent) >= 2);

        Assert.Equal(0, handler.SseAfterValues[0]);
        Assert.Equal(1, handler.SseAfterValues[1]); // reconnected using the last delivered sequence

        var relayedEventIds = published.Where(p => p.Kind == ClientRuntimeEventKind.ConversationEvent)
            .Select(p => p.Conversation!.EventId).ToList();
        Assert.Equal([firstEvent.EventId, secondEvent.EventId], relayedEventIds); // no duplicate
    }

    [Fact]
    public async Task ExpectedToolRequest_ReachesTheDispatcher_AndPostsTheStableClientToolResultCommandId()
    {
        var notesPath = Path.Combine(_workspace, "notes.txt");
        await File.WriteAllTextAsync(notesPath, "the secret word is PLATYPUS");
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var arguments = JsonDocument.Parse($$"""{"file_path":"{{JsonEncode(notesPath)}}"}""").RootElement.Clone();
        var toolRequested = NewEvent(conversationId, runId, 1, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer,
            toolRequest: new ConversationToolRequest(requestId, "Read", arguments));
        var handler = new ScriptedConversationHostHandler(
            conversationId, runId, new Queue<string>([ToSseBody(toolRequested)]));
        var (capabilities, dispatcher, executionCount) = BuildCountingWorkspace();

        await using var session = NewSession(handler, capabilities, dispatcher, _ => { });
        await session.SendAsync("Build the thing.", CancellationToken.None);

        await WaitUntilAsync(() => handler.PostBodies.Count(IsToolResultBody) >= 1);

        var toolResultBody = handler.PostBodies.First(IsToolResultBody);
        Assert.Equal(requestId.ToString(), toolResultBody.GetProperty("toolRequestId").GetString(), ignoreCase: true);
        Assert.Equal(
            ConversationDeterministicIds.ClientToolResult(requestId).ToString(),
            toolResultBody.GetProperty("commandId").GetString(),
            ignoreCase: true);
        Assert.False(toolResultBody.GetProperty("isError").GetBoolean());
        Assert.Contains("PLATYPUS", toolResultBody.GetProperty("content").GetString());
        Assert.Equal(1, executionCount.Value);
    }

    [Fact]
    public async Task UnsupportedToolRequest_ReturnsAnErrorResult_WithoutExecuting()
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var arguments = JsonDocument.Parse("{}").RootElement.Clone();
        var toolRequested = NewEvent(conversationId, runId, 1, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer,
            toolRequest: new ConversationToolRequest(requestId, "WebFetch", arguments));
        var handler = new ScriptedConversationHostHandler(
            conversationId, runId, new Queue<string>([ToSseBody(toolRequested)]));
        var (capabilities, dispatcher, executionCount) = BuildCountingWorkspace();

        await using var session = NewSession(handler, capabilities, dispatcher, _ => { });
        await session.SendAsync("Build the thing.", CancellationToken.None);

        await WaitUntilAsync(() => handler.PostBodies.Count(IsToolResultBody) >= 1);

        var toolResultBody = handler.PostBodies.First(IsToolResultBody);
        Assert.True(toolResultBody.GetProperty("isError").GetBoolean());
        Assert.Contains("Unsupported or invalid tool request", toolResultBody.GetProperty("content").GetString());
        Assert.Equal(0, executionCount.Value);
    }

    [Fact]
    public async Task DisposeAsync_CancelsAnInFlightTail_AndNoFurtherToolExecutesAfterward()
    {
        var conversationId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        var arguments = JsonDocument.Parse("{}").RootElement.Clone();
        var toolRequested = NewEvent(conversationId, runId, 1, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer,
            toolRequest: new ConversationToolRequest(requestId, "Bash", arguments));
        var handler = new ScriptedConversationHostHandler(
            conversationId, runId, new Queue<string>([ToSseBody(toolRequested)]));
        var (capabilities, _) = BuildWorkspace();
        // Blocks inside DispatchAsync (a well-defined, cancellation-respecting await) rather than
        // inside HTTP content serialization — the tail is genuinely still executing this call,
        // not merely still connected, when the test disposes the session.
        var gate = new SemaphoreSlim(0, 1);
        var dispatcher = new BlockingDispatcher(gate);

        var session = NewSession(handler, capabilities, dispatcher, _ => { });
        await session.SendAsync("Build the thing.", CancellationToken.None);
        await WaitUntilAsync(() => dispatcher.CallCount >= 1);

        await session.DisposeAsync();
        gate.Release();
        await Task.Delay(200); // give a wrongly-still-running tail a chance to (incorrectly) act

        Assert.Equal(1, dispatcher.CallCount); // never retried/re-executed after disposal
        Assert.DoesNotContain(handler.PostBodies, IsToolResultBody); // the blocked dispatch never completed
    }

    private static bool IsToolResultBody(JsonElement body) => body.TryGetProperty("toolRequestId", out _);

    private ConversationRuntimeSession NewSession(
        HttpMessageHandler handler, CapabilityRegistry capabilities, ICapabilityDispatcher dispatcher,
        Action<ClientRuntimeEvent> publish)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") };
        return new ConversationRuntimeSession(
            "session-1", "Janus", new ConversationHostClient(http), capabilities, dispatcher, publish, CancellationToken.None);
    }

    private (CapabilityRegistry Capabilities, ICapabilityDispatcher Dispatcher) BuildWorkspace()
    {
        var workspace = new LocalDiskWorkspace(_workspace);
        var capabilities = new CapabilityRegistry([new WorkspaceFileProvider(workspace), new WorkspaceTerminalProvider(workspace)]);
        var dispatcher = new CapabilityDispatcher(
            capabilities, new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default), new InMemoryCapabilityAuditLog());
        return (capabilities, dispatcher);
    }

    private (CapabilityRegistry Capabilities, ICapabilityDispatcher Dispatcher, StrongBox<int> ExecutionCount) BuildCountingWorkspace()
    {
        var (capabilities, dispatcher) = BuildWorkspace();
        var count = new StrongBox<int>(0);
        return (capabilities, new CountingDispatcher(dispatcher, count), count);
    }

    private static ConversationEvent NewEvent(
        Guid conversationId, Guid runId, long sequence, ConversationEventKind kind, ConversationParticipant participant,
        int? attempt = null, string? text = null, ConversationRunStatus? runStatus = null,
        ConversationToolRequest? toolRequest = null) =>
        new(Guid.NewGuid(), 1, conversationId, runId, sequence, kind, participant, attempt, text,
            Reason: null, Approval: null, ToolRequest: toolRequest, ToolResult: null, Artifact: null,
            RunStatus: runStatus, OccurredAtUtc: DateTimeOffset.UtcNow);

    private static string ToSseBody(params ConversationEvent[] events)
    {
        var builder = new StringBuilder();
        foreach (var evt in events)
        {
            var json = JsonSerializer.Serialize(evt, ConversationContractsJsonContext.Default.ConversationEvent);
            builder.Append("event: conversation-event\n");
            builder.Append($"id: {evt.Sequence}\n");
            builder.Append($"data: {json}\n\n");
        }

        return builder.ToString();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Condition was not met in time.");
            await Task.Delay(10);
        }
    }

    private static string JsonEncode(string value) => value.Replace("\\", "\\\\");

    private sealed class StrongBox<T>(T value)
    {
        public T Value { get; set; } = value;
    }

    private sealed class CountingDispatcher(ICapabilityDispatcher inner, StrongBox<int> count) : ICapabilityDispatcher
    {
        public Task<ToolExecutionResult> DispatchAsync(string capabilityName, object request, CancellationToken ct)
        {
            count.Value++;
            return inner.DispatchAsync(capabilityName, request, ct);
        }
    }

    private sealed class BlockingDispatcher(SemaphoreSlim gate) : ICapabilityDispatcher
    {
        public int CallCount { get; private set; }

        public async Task<ToolExecutionResult> DispatchAsync(string capabilityName, object request, CancellationToken ct)
        {
            CallCount++;
            await gate.WaitAsync(ct);
            return new ToolExecutionResult("done");
        }
    }

    private sealed class ScriptedConversationHostHandler(Guid conversationId, Guid runId, Queue<string> sseBodies)
        : HttpMessageHandler
    {
        public List<JsonElement> PostBodies { get; } = [];
        public List<long> SseAfterValues { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;

            if (request.Method == HttpMethod.Post && path == "/conversations")
                return await AcceptAsync(request, ct);
            if (request.Method == HttpMethod.Post && path == $"/conversations/{conversationId}/commands")
                return await AcceptAsync(request, ct);
            if (request.Method == HttpMethod.Post && path == $"/conversations/{conversationId}/tool-results")
                return await AcceptAsync(request, ct, waitingForTool: true);
            if (request.Method == HttpMethod.Get && path == $"/conversations/{conversationId}/events")
                return ServeEvents(request);

            throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");
        }

        private async Task<HttpResponseMessage> AcceptAsync(HttpRequestMessage request, CancellationToken ct, bool waitingForTool = false)
        {
            PostBodies.Add(await ReadJsonAsync(request, ct));
            var payload = JsonSerializer.Serialize(new
            {
                conversationId,
                runId,
                acceptedSequence = 2,
                status = waitingForTool ? "waitingForTool" : "queued",
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json"),
            };
        }

        private HttpResponseMessage ServeEvents(HttpRequestMessage request)
        {
            var query = request.RequestUri!.Query;
            var after = query.Contains("after=", StringComparison.Ordinal)
                ? long.Parse(query.Split("after=")[1].Split('&')[0])
                : 0;
            SseAfterValues.Add(after);

            var body = sseBodies.Count > 0 ? sseBodies.Dequeue() : string.Empty;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/event-stream"),
            };
        }

        private static async Task<JsonElement> ReadJsonAsync(HttpRequestMessage request, CancellationToken ct)
        {
            await using var stream = await request.Content!.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return document.RootElement.Clone();
        }
    }

}
