using System.Net;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using ForgeMission.ConversationHost.Api;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Real-Kestrel HTTP/SSE integration tests against the additive Task 6 Conversation API — a real
/// <see cref="HttpClient"/> against <see cref="ConversationHostInstance.BaseAddress"/>, never an
/// endpoint delegate invoked directly.
/// </summary>
[Collection("Azurite")]
public class ConversationApiTests(AzuriteFixture fixture)
{
    private static HttpClient CreateClient(ConversationHostInstance host) => new() { BaseAddress = host.BaseAddress };

    private static string SerializeProgress(ConversationProgress progress)
        => JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);

    // ── 1. Start + deterministic exact retry, snapshot, health ──────────────────────

    [Fact]
    public async Task StartConversation_Returns201AndTwoEvents_ExactRetryReturnsOriginalAcceptance()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = CreateClient(host);

        var commandId = Guid.NewGuid();
        var request = new StartConversationRequest(commandId, "Janus", "goal text", []);

        var response = await client.PostAsJsonAsync("/conversations", request, ConversationContractsJsonContext.Default.StartConversationRequest);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var body = await response.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);
        Assert.NotNull(body);
        Assert.Equal(2, body!.AcceptedSequence);
        Assert.Equal(ConversationRunStatus.Queued, body.Status);
        Assert.Contains($"/conversations/{body.ConversationId}", response.Headers.Location!.ToString());

        // Deterministic, not random: derivable from the CommandId alone.
        Assert.Equal(ConversationDeterministicIds.Conversation(commandId), body.ConversationId);
        Assert.Equal(ConversationDeterministicIds.InitialRun(commandId), body.RunId);

        var snapshotResponse = await client.GetAsync($"/conversations/{body.ConversationId}");
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        var getResponseBody = await snapshotResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.GetConversationResponse);
        Assert.NotNull(getResponseBody);
        Assert.Equal("Janus", getResponseBody!.Snapshot.MissionRef);
        Assert.Equal(2, getResponseBody.Snapshot.LastSequence);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // Exact retry of the same request: same deterministic conversation/run, original
        // acceptance, no additional durable event.
        var retryResponse = await client.PostAsJsonAsync("/conversations", request, ConversationContractsJsonContext.Default.StartConversationRequest);
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        var retryBody = await retryResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);
        Assert.Equal(body.ConversationId, retryBody!.ConversationId);
        Assert.Equal(body.RunId, retryBody.RunId);
        Assert.Equal(body.AcceptedSequence, retryBody.AcceptedSequence);
        Assert.Equal(body.Status, retryBody.Status);

        var grain = host.GetConversationGrain(new ConversationAddress("dev", body.ConversationId));
        Assert.Equal(2, (await grain.ReadAfterAsync(0)).EventJson.Length);
    }

    // ── 2. Follow-up: pinned mission/capabilities, not client-supplied ──────────────

    [Fact]
    public async Task Followup_AcceptsWithoutClientSuppliedMissionOrCapabilities_DispatcherObservesPinnedCapabilities()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = CreateClient(host);

        using var schemaDoc = JsonDocument.Parse("""{"type":"object"}""");
        var capabilities = new[] { new ConversationCapabilityDeclaration("read_file", "Reads a file", schemaDoc.RootElement) };
        var startRequest = new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", capabilities);

        var startResponse = await client.PostAsJsonAsync(
            "/conversations", startRequest, ConversationContractsJsonContext.Default.StartConversationRequest);
        var startBody = await startResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);

        // Terminate the first run — Task 6 adds no completion route, so this goes straight through
        // the grain, exactly as a real Worker-reported RunStatus(Completed) fact would arrive.
        var address = new ConversationAddress("dev", startBody!.ConversationId);
        var grain = host.GetConversationGrain(address);
        var completed = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, startBody.RunId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
        var completeAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(completed)));
        Assert.Equal(ConversationProgressOutcome.Appended, completeAcceptance.Outcome);

        var followupRequest = new SubmitConversationCommandRequest(startBody.ConversationId, Guid.NewGuid(), "follow-up text");
        var followupResponse = await client.PostAsJsonAsync(
            $"/conversations/{startBody.ConversationId}/commands", followupRequest,
            ConversationContractsJsonContext.Default.SubmitConversationCommandRequest);

        Assert.Equal(HttpStatusCode.Accepted, followupResponse.StatusCode);
        Assert.Contains($"/conversations/{startBody.ConversationId}", followupResponse.Headers.Location?.ToString() ?? "");
        Assert.Equal(TimeSpan.FromSeconds(1), followupResponse.Headers.RetryAfter?.Delta);
        var followupBody = await followupResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.SubmitConversationCommandResponse);
        Assert.NotNull(followupBody);
        Assert.NotEqual(startBody.RunId, followupBody!.RunId);
        Assert.Equal(ConversationRunStatus.Queued, followupBody.Status);

        var followupDispatch = Assert.Single(
            host.Dispatcher.Sent, s => s.Command.Kind == ConversationCommandKind.StartMission && s.Command.Goal == "follow-up text");
        Assert.Equal(capabilities.Length, followupDispatch.Command.Capabilities.Length);
        Assert.Equal(capabilities[0].Name, followupDispatch.Command.Capabilities[0].Name);
        Assert.Equal(capabilities[0].Description, followupDispatch.Command.Capabilities[0].Description);
    }

    // ── 3. Tool results: expected-request validation, exact duplicate, mismatch ─────

    [Fact]
    public async Task ToolResult_ValidatesExpectedRequest_ExactDuplicateReturnsOriginal_MismatchIs409()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = CreateClient(host);

        var startRequest = new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []);
        var startResponse = await client.PostAsJsonAsync(
            "/conversations", startRequest, ConversationContractsJsonContext.Default.StartConversationRequest);
        var startBody = await startResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);

        var address = new ConversationAddress("dev", startBody!.ConversationId);
        var grain = host.GetConversationGrain(address);

        var toolRequestId = Guid.NewGuid();
        using var argsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequested = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, startBody.RunId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null,
            new ConversationToolRequest(toolRequestId, "read_file", argsDoc.RootElement), null, null, null, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(toolRequested)));

        var commandId = Guid.NewGuid();
        var toolResultRequest = new SubmitToolResultRequest(startBody.ConversationId, commandId, toolRequestId, "file contents", false);

        var firstResponse = await client.PostAsJsonAsync(
            $"/conversations/{startBody.ConversationId}/tool-results", toolResultRequest,
            ConversationContractsJsonContext.Default.SubmitToolResultRequest);
        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Contains($"/conversations/{startBody.ConversationId}", firstResponse.Headers.Location?.ToString() ?? "");
        Assert.Equal(TimeSpan.FromSeconds(1), firstResponse.Headers.RetryAfter?.Delta);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.SubmitToolResultResponse);
        Assert.NotNull(firstBody);
        Assert.Equal(ConversationRunStatus.WaitingForTool, firstBody!.Status);

        // Exact duplicate retry: original acceptance, no new event/dispatch.
        var duplicateResponse = await client.PostAsJsonAsync(
            $"/conversations/{startBody.ConversationId}/tool-results", toolResultRequest,
            ConversationContractsJsonContext.Default.SubmitToolResultRequest);
        Assert.Equal(HttpStatusCode.Accepted, duplicateResponse.StatusCode);
        var duplicateBody = await duplicateResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.SubmitToolResultResponse);
        Assert.Equal(firstBody.AcceptedSequence, duplicateBody!.AcceptedSequence);

        Assert.Single(host.Dispatcher.Sent, s => s.Command.Kind == ConversationCommandKind.ContinueAfterTool);
        var eventsAfterDuplicate = await grain.ReadAfterAsync(0);
        var deserialized = eventsAfterDuplicate.EventJson
            .Select(json => JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!).ToList();
        Assert.Single(deserialized, e => e.Kind == ConversationEventKind.ToolResult);

        // Same CommandId, different content: 409, no state advance.
        var mismatchRequest = new SubmitToolResultRequest(startBody.ConversationId, commandId, toolRequestId, "different content", false);
        var mismatchResponse = await client.PostAsJsonAsync(
            $"/conversations/{startBody.ConversationId}/tool-results", mismatchRequest,
            ConversationContractsJsonContext.Default.SubmitToolResultRequest);
        Assert.Equal(HttpStatusCode.Conflict, mismatchResponse.StatusCode);

        var eventsAfterMismatch = await grain.ReadAfterAsync(0);
        Assert.Equal(eventsAfterDuplicate.EventJson.Length, eventsAfterMismatch.EventJson.Length);
    }

    // ── 4. Malformed / unknown / active-run inputs ───────────────────────────────────

    [Fact]
    public async Task MalformedUnknownActiveRunInputs_MapToDocumentedStatusCodes()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = CreateClient(host);

        // 400: malformed JSON body.
        var malformedResponse = await client.PostAsync(
            "/conversations", new StringContent("{not valid json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);

        // 400: empty required field.
        var emptyFieldResponse = await client.PostAsJsonAsync(
            "/conversations", new StartConversationRequest(Guid.Empty, "Janus", "goal", []),
            ConversationContractsJsonContext.Default.StartConversationRequest);
        Assert.Equal(HttpStatusCode.BadRequest, emptyFieldResponse.StatusCode);

        // 400: unsupported mission.
        var wrongMissionResponse = await client.PostAsJsonAsync(
            "/conversations", new StartConversationRequest(Guid.NewGuid(), "NotJanus", "goal", []),
            ConversationContractsJsonContext.Default.StartConversationRequest);
        Assert.Equal(HttpStatusCode.BadRequest, wrongMissionResponse.StatusCode);

        // 400: invalid route GUID.
        var badRouteResponse = await client.GetAsync("/conversations/not-a-guid");
        Assert.Equal(HttpStatusCode.BadRequest, badRouteResponse.StatusCode);

        var validStartResponse = await client.PostAsJsonAsync(
            "/conversations", new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []),
            ConversationContractsJsonContext.Default.StartConversationRequest);
        var validStartBody = await validStartResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);

        // 400: negative / non-numeric `after`.
        var negativeAfterResponse = await client.GetAsync($"/conversations/{validStartBody!.ConversationId}/events?after=-1");
        Assert.Equal(HttpStatusCode.BadRequest, negativeAfterResponse.StatusCode);
        var nonNumericAfterResponse = await client.GetAsync($"/conversations/{validStartBody.ConversationId}/events?after=abc");
        Assert.Equal(HttpStatusCode.BadRequest, nonNumericAfterResponse.StatusCode);

        // 404: unknown conversation, on every route that names one.
        var unknownId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/conversations/{unknownId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/conversations/{unknownId}/events")).StatusCode);
        var unknownFollowupResponse = await client.PostAsJsonAsync(
            $"/conversations/{unknownId}/commands", new SubmitConversationCommandRequest(unknownId, Guid.NewGuid(), "text"),
            ConversationContractsJsonContext.Default.SubmitConversationCommandRequest);
        Assert.Equal(HttpStatusCode.NotFound, unknownFollowupResponse.StatusCode);
        var unknownToolResultResponse = await client.PostAsJsonAsync(
            $"/conversations/{unknownId}/tool-results", new SubmitToolResultRequest(unknownId, Guid.NewGuid(), Guid.NewGuid(), "content", false),
            ConversationContractsJsonContext.Default.SubmitToolResultRequest);
        Assert.Equal(HttpStatusCode.NotFound, unknownToolResultResponse.StatusCode);

        // 400: route conversationId and request body ConversationId disagree.
        var mismatchedBodyResponse = await client.PostAsJsonAsync(
            $"/conversations/{validStartBody.ConversationId}/commands",
            new SubmitConversationCommandRequest(Guid.NewGuid(), Guid.NewGuid(), "text"),
            ConversationContractsJsonContext.Default.SubmitConversationCommandRequest);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchedBodyResponse.StatusCode);
        var mismatchedToolResultBodyResponse = await client.PostAsJsonAsync(
            $"/conversations/{validStartBody.ConversationId}/tool-results",
            new SubmitToolResultRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "content", false),
            ConversationContractsJsonContext.Default.SubmitToolResultRequest);
        Assert.Equal(HttpStatusCode.BadRequest, mismatchedToolResultBodyResponse.StatusCode);

        // 409: an active run already exists (follow-up while the first run is still Queued).
        var activeFollowupResponse = await client.PostAsJsonAsync(
            $"/conversations/{validStartBody.ConversationId}/commands",
            new SubmitConversationCommandRequest(validStartBody.ConversationId, Guid.NewGuid(), "text"),
            ConversationContractsJsonContext.Default.SubmitConversationCommandRequest);
        Assert.Equal(HttpStatusCode.Conflict, activeFollowupResponse.StatusCode);

        // 400: over-limit start-command / tool-result payloads (Invalid outcome), no state advance.
        var oversizedGoal = new string('x', 40_000);
        var oversizedStartResponse = await client.PostAsJsonAsync(
            "/conversations", new StartConversationRequest(Guid.NewGuid(), "Janus", oversizedGoal, []),
            ConversationContractsJsonContext.Default.StartConversationRequest);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedStartResponse.StatusCode);

        var oversizedToolRequestId = Guid.NewGuid();
        using var oversizedArgsDoc = JsonDocument.Parse("""{"path":"mission/notes.md"}""");
        var toolRequestedForOversized = new ConversationProgress(
            Guid.NewGuid(), validStartBody.ConversationId, validStartBody.RunId, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, 1, null, null, null,
            new ConversationToolRequest(oversizedToolRequestId, "read_file", oversizedArgsDoc.RootElement), null, null, null,
            DateTimeOffset.UtcNow);
        var validGrain = host.GetConversationGrain(new ConversationAddress("dev", validStartBody.ConversationId));
        await validGrain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(toolRequestedForOversized)));
        var eventsBeforeOversizedToolResult = await validGrain.ReadAfterAsync(0);

        var oversizedContent = new string('y', 60_000);
        var oversizedToolResultResponse = await client.PostAsJsonAsync(
            $"/conversations/{validStartBody.ConversationId}/tool-results",
            new SubmitToolResultRequest(validStartBody.ConversationId, Guid.NewGuid(), oversizedToolRequestId, oversizedContent, false),
            ConversationContractsJsonContext.Default.SubmitToolResultRequest);
        Assert.Equal(HttpStatusCode.BadRequest, oversizedToolResultResponse.StatusCode);

        var eventsAfterOversizedToolResult = await validGrain.ReadAfterAsync(0);
        Assert.Equal(eventsBeforeOversizedToolResult.EventJson.Length, eventsAfterOversizedToolResult.EventJson.Length);
    }

    // ── 4b. Transport-neutral: the same Contracts message works without HTTP meaning ─

    [Fact]
    public async Task ContractsMessages_UsableDirectly_WithoutHttpMeaning()
    {
        await using var host = await fixture.StartHostAsync();
        var grainFactory = host.GrainFactory;

        var startRequest = new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []);
        var startResult = await ConversationApiEndpoints.HandleStartConversationAsync(startRequest, grainFactory);
        Assert.Equal(ConversationCommandOutcome.Accepted, startResult.Outcome);
        var conversationId = startResult.Acceptance!.ConversationId;
        var runId = startResult.Acceptance.RunId;

        // Terminate the run directly, then submit a follow-up purely through the message handler —
        // no HttpContext, no route, no header anywhere in this call.
        var grain = host.GetConversationGrain(new ConversationAddress("dev", conversationId));
        var completed = new ConversationProgress(
            Guid.NewGuid(), conversationId, runId, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null,
            ConversationRunStatus.Completed, DateTimeOffset.UtcNow);
        await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(completed)));

        var followupRequest = new SubmitConversationCommandRequest(conversationId, Guid.NewGuid(), "message-only follow-up");
        var followupResult = await ConversationApiEndpoints.HandleSubmitCommandAsync(followupRequest, grainFactory);
        Assert.Equal(ConversationCommandOutcome.Accepted, followupResult.Outcome);

        // Not-found is expressed the same typed way for a message-level caller as for HTTP.
        var unknownResult = await ConversationApiEndpoints.HandleSubmitCommandAsync(
            new SubmitConversationCommandRequest(Guid.NewGuid(), Guid.NewGuid(), "text"), grainFactory);
        Assert.Equal(ConversationCommandOutcome.NotFound, unknownResult.Outcome);
    }

    [Fact]
    public async Task HandleGetConversationAsync_UsableDirectly_ReturnsRealGetConversationResponse()
    {
        await using var host = await fixture.StartHostAsync();
        var grainFactory = host.GrainFactory;

        var startResult = await ConversationApiEndpoints.HandleStartConversationAsync(
            new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []), grainFactory);
        var conversationId = startResult.Acceptance!.ConversationId;

        var found = await ConversationApiEndpoints.HandleGetConversationAsync(new GetConversationRequest(conversationId), grainFactory);
        Assert.Equal(ConversationQueryOutcome.Found, found.Outcome);
        Assert.NotNull(found.Response);
        Assert.Equal("Janus", found.Response!.Snapshot.MissionRef);
        Assert.Equal(conversationId, found.Response.Snapshot.ConversationId);

        var notFound = await ConversationApiEndpoints.HandleGetConversationAsync(new GetConversationRequest(Guid.NewGuid()), grainFactory);
        Assert.Equal(ConversationQueryOutcome.NotFound, notFound.Outcome);
        Assert.Null(notFound.Response);
        Assert.NotNull(notFound.Reason);

        var invalid = await ConversationApiEndpoints.HandleGetConversationAsync(new GetConversationRequest(Guid.Empty), grainFactory);
        Assert.Equal(ConversationQueryOutcome.Invalid, invalid.Outcome);
        Assert.Null(invalid.Response);
    }

    [Fact]
    public async Task HandleReadConversationEventsAsync_UsableDirectly_YieldsRealOrderedEvents()
    {
        await using var host = await fixture.StartHostAsync();
        var grainFactory = host.GrainFactory;

        var startResult = await ConversationApiEndpoints.HandleStartConversationAsync(
            new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []), grainFactory);
        var conversationId = startResult.Acceptance!.ConversationId;

        var found = await ConversationApiEndpoints.HandleReadConversationEventsAsync(
            new ReadConversationEventsRequest(conversationId, 0), grainFactory);
        Assert.Equal(ConversationQueryOutcome.Found, found.Outcome);
        Assert.NotNull(found.Events);
        Assert.Equal([1L, 2L], found.Events!.Select(e => e.Sequence));
        Assert.Equal(ConversationEventKind.UserMessage, found.Events[0].Kind);
        Assert.Equal(ConversationEventKind.RunStatus, found.Events[1].Kind);

        // The `after` cursor is honored by the real query, not simulated.
        var afterFirst = await ConversationApiEndpoints.HandleReadConversationEventsAsync(
            new ReadConversationEventsRequest(conversationId, 1), grainFactory);
        Assert.Equal(ConversationQueryOutcome.Found, afterFirst.Outcome);
        Assert.Equal([2L], afterFirst.Events!.Select(e => e.Sequence));

        var notFound = await ConversationApiEndpoints.HandleReadConversationEventsAsync(
            new ReadConversationEventsRequest(Guid.NewGuid(), 0), grainFactory);
        Assert.Equal(ConversationQueryOutcome.NotFound, notFound.Outcome);
        Assert.Null(notFound.Events);

        var invalid = await ConversationApiEndpoints.HandleReadConversationEventsAsync(
            new ReadConversationEventsRequest(conversationId, -1), grainFactory);
        Assert.Equal(ConversationQueryOutcome.Invalid, invalid.Outcome);
        Assert.Null(invalid.Events);
    }

    // ── 5. SSE: disconnect/reconnect replay, subscribe/catch-up overlap ─────────────

    [Fact]
    public async Task Sse_DisconnectAndReconnect_ReplaysExactlyTheMissedEvents()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = CreateClient(host);

        var startResponse = await client.PostAsJsonAsync(
            "/conversations", new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []),
            ConversationContractsJsonContext.Default.StartConversationRequest);
        var startBody = await startResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);
        var address = new ConversationAddress("dev", startBody!.ConversationId);

        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, $"/conversations/{startBody.ConversationId}/events?after=0");
        using var firstResponse = await client.SendAsync(firstRequest, HttpCompletionOption.ResponseHeadersRead, cts1.Token);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await using var firstStream = await firstResponse.Content.ReadAsStreamAsync(cts1.Token);
        using var firstReader = new StreamReader(firstStream);
        var initial = await ReadSseEventsAsync(firstReader, expectedCount: 2, cts1.Token);
        Assert.Equal([1L, 2L], initial.Select(e => e.Sequence));

        // Disconnect. A dropped SSE connection is a reconnect condition, never data loss.
        await cts1.CancelAsync();

        // Missed while disconnected.
        var grain = host.GetConversationGrain(address);
        var progress = new ConversationProgress(
            Guid.NewGuid(), address.ConversationId, startBody.RunId, ConversationEventKind.ParticipantStarted,
            ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var progressAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress)));
        Assert.Equal(ConversationProgressOutcome.Appended, progressAcceptance.Outcome);

        // Reconnect from the last rendered sequence.
        using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, $"/conversations/{startBody.ConversationId}/events?after=2");
        using var secondResponse = await client.SendAsync(secondRequest, HttpCompletionOption.ResponseHeadersRead, cts2.Token);
        await using var secondStream = await secondResponse.Content.ReadAsStreamAsync(cts2.Token);
        using var secondReader = new StreamReader(secondStream);

        var missed = await ReadSseEventsAsync(secondReader, expectedCount: 1, cts2.Token);
        var missedEvent = Assert.Single(missed);
        Assert.Equal(3, missedEvent.Sequence);
        Assert.Equal(ConversationEventKind.ParticipantStarted, missedEvent.Kind);
    }

    [Fact]
    public async Task Sse_SubscribeCatchUpOverlap_EventIsNeitherOmittedNorDuplicated()
    {
        ConversationHostInstance? capturedHost = null;
        ConversationAddress? capturedAddress = null;
        var runId = Guid.Empty;

        // Injects a REAL, concurrently-accepted progress fact (durable append + live publish,
        // through the actual grain) exactly during the writer's SECOND catch-up read — deterministic
        // reproduction of "an event landed between subscribe and the second read" without a timing
        // race. Call #1 is the grain's own one-time activation corruption-check; #2 is the writer's
        // first replay; #3 is its second — so injecting on call #3 lands exactly at that boundary.
        await using var host = await fixture.StartHostAsync(store => new InjectOnNthReadEventStore(store, injectOnCallNumber: 3, injectAsync: async () =>
        {
            var grain = capturedHost!.GetConversationGrain(capturedAddress!);
            var progress = new ConversationProgress(
                Guid.NewGuid(), capturedAddress!.ConversationId, runId, ConversationEventKind.ParticipantStarted,
                ConversationParticipant.Proposer, 1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
            await grain.RecordProgressAsync(new ConversationProgressInput(SerializeProgress(progress)));
        }));
        capturedHost = host;

        using var client = CreateClient(host);

        var startResponse = await client.PostAsJsonAsync(
            "/conversations", new StartConversationRequest(Guid.NewGuid(), "Janus", "goal", []),
            ConversationContractsJsonContext.Default.StartConversationRequest);
        var startBody = await startResponse.Content.ReadFromJsonAsync(ConversationContractsJsonContext.Default.StartConversationResponse);
        capturedAddress = new ConversationAddress("dev", startBody!.ConversationId);
        runId = startBody.RunId;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/conversations/{startBody.ConversationId}/events?after=0");
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        var events = await ReadSseEventsAsync(reader, expectedCount: 3, cts.Token);
        Assert.Equal([1L, 2L, 3L], events.Select(e => e.Sequence)); // not omitted
        Assert.Equal(ConversationEventKind.ParticipantStarted, events[2].Kind);

        // Confirm no fourth (duplicate) frame follows within a short window.
        using var shortTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, shortTimeout.Token);
        var extra = await ReadSseEventsAsync(reader, expectedCount: 1, linked.Token);
        Assert.Empty(extra); // not duplicated
    }

    private static async Task<List<ConversationEvent>> ReadSseEventsAsync(StreamReader reader, int expectedCount, CancellationToken ct)
    {
        var events = new List<ConversationEvent>();
        string? dataLine = null;
        try
        {
            while (events.Count < expectedCount)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line is null)
                    break;
                if (line.StartsWith("data: ", StringComparison.Ordinal))
                    dataLine = line["data: ".Length..];
                else if (line.Length == 0 && dataLine is not null)
                {
                    events.Add(JsonSerializer.Deserialize(dataLine, ConversationContractsJsonContext.Default.ConversationEvent)!);
                    dataLine = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // A timeout while confirming "nothing more arrives" is the expected outcome, not a
            // test failure — the caller reads an incomplete/empty result as exactly that signal.
        }
        return events;
    }

    /// <summary>Test-only <see cref="IConversationEventStore"/> decorator: on the
    /// <paramref name="injectOnCallNumber"/>th call to <see cref="ReadAfterAsync"/> across this
    /// store's whole lifetime, awaits <paramref name="injectAsync"/> BEFORE querying the real
    /// store — so whatever it durably appends is guaranteed visible to that same read, exactly as
    /// if another request's grain call had landed at that precise moment.</summary>
    private sealed class InjectOnNthReadEventStore(IConversationEventStore inner, int injectOnCallNumber, Func<Task> injectAsync)
        : IConversationEventStore
    {
        private int _readCallCount;

        public Task<StoredConversationEvent?> FindByEventIdAsync(ConversationAddress address, Guid eventId, CancellationToken ct)
            => inner.FindByEventIdAsync(address, eventId, ct);

        public Task<ConversationEvent> AppendAsync(
            ConversationAddress address, ConversationEvent @event, string? associatedCommandJson, CancellationToken ct)
            => inner.AppendAsync(address, @event, associatedCommandJson, ct);

        public async IAsyncEnumerable<ConversationEvent> ReadAfterAsync(
            ConversationAddress address, long sequence, [EnumeratorCancellation] CancellationToken ct)
        {
            if (Interlocked.Increment(ref _readCallCount) == injectOnCallNumber)
                await injectAsync();

            await foreach (var @event in inner.ReadAfterAsync(address, sequence, ct))
                yield return @event;
        }

        public Task<ConversationEvent?> ReadLatestForRunAsync(ConversationAddress address, Guid runId, CancellationToken ct)
            => inner.ReadLatestForRunAsync(address, runId, ct);
    }
}
