using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.ClientRuntime;

/// <summary>
/// Phase 43.20 Task 2 — the Client Runtime half of Project Mission Control, exercised against a
/// scripted <see cref="HttpMessageHandler"/> standing in for the Conversation service. No
/// ConversationHost dependency, matching the project-boundary rule that Client Runtime's own test
/// project never references Host.
///
/// The properties under test: a stored manifest ID reopens WITHOUT creating anything, a null one
/// creates exactly once under a deterministic command ID, a failed manifest write is reported
/// truthfully and stays retryable, and a control turn carries nothing but its command ID and text.
/// </summary>
public sealed class ProjectMissionControlSessionTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-mission-control-").FullName;
    private readonly ProjectStore _store;

    public ProjectMissionControlSessionTests() => _store = new ProjectStore(Path.Combine(_profile, "Forge", "Projects"));

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    // ── 1. Create once, then reopen with no create ─────────────────────────────────

    [Fact]
    public async Task ANullManifestId_CreatesExactlyOnce_UnderTheDeterministicCommandId_AndPersistsIt()
    {
        var project = _store.Create("Ship a todos API", null, null);
        var conversationId = Guid.NewGuid();
        var handler = new ScriptedControlHostHandler(conversationId);
        await using var session = NewSession(handler, project.Home);

        var opened = await session.OpenAsync(CancellationToken.None);

        Assert.Equal(conversationId, opened);
        var create = Assert.Single(handler.Posts, p => p.Route == "conversations/project-control");
        Assert.Equal(
            ConversationDeterministicIds.ProjectControlCreate(project.Manifest.ProjectId).ToString(),
            create.Body.GetProperty("commandId").GetString(), ignoreCase: true);
        Assert.Equal(project.Manifest.ProjectId.ToString(), create.Body.GetProperty("projectId").GetString(), ignoreCase: true);
        Assert.Equal("Ship a todos API", create.Body.GetProperty("projectGoal").GetString());

        // Written back only after acceptance, and readable by the next open.
        Assert.Equal(conversationId, _store.Open(project.Home).Project!.Manifest.LegacyProjectControlConversationId);
    }

    [Fact]
    public async Task AStoredManifestId_ReplaysAndTails_WithNoCreateAndNoSubmit()
    {
        var project = _store.Create("Ship a todos API", null, null);
        var conversationId = Guid.NewGuid();
        _store.SetLegacyProjectControlConversationId(project.Home, conversationId);

        var relayed = new List<ClientRuntimeEvent>();
        var handler = new ScriptedControlHostHandler(conversationId, ToSseBody(
            ControlEvent(conversationId, 1, ConversationEventKind.UserMessage, ConversationParticipant.User, "refine"),
            ControlEvent(conversationId, 2, ConversationEventKind.ParticipantMessage,
                ConversationParticipant.MissionControl, "What would done look like?")));
        await using var session = NewSession(handler, project.Home, relayed.Add);

        var opened = await session.OpenAsync(CancellationToken.None);

        Assert.Equal(conversationId, opened);
        Assert.Empty(handler.Posts); // NOTHING was posted — no create, no submit.
        await WaitUntilAsync(() => relayed.Count(e => e.Kind == ClientRuntimeEventKind.ConversationEvent) >= 2);
        Assert.Equal([1, 2], relayed.Where(e => e.Kind == ClientRuntimeEventKind.ConversationEvent)
            .Select(e => e.Conversation!.Sequence));
        Assert.All(relayed.Where(e => e.Conversation is not null), e => Assert.Null(e.Conversation!.RunId));
    }

    // ── 2. The Host-accepted / manifest-write boundary ─────────────────────────────

    [Fact]
    public async Task AnAcceptedCreateWithAFailedManifestWrite_ReportsIt_AndTheRetryReturnsTheSameConversation()
    {
        var project = _store.Create("Ship a todos API", null, null);
        var conversationId = Guid.NewGuid();
        var handler = new ScriptedControlHostHandler(conversationId);

        // Make the manifest replacement fail without touching the manifest itself.
        var temporaryPath = Path.Combine(project.Home, ProjectStore.ManifestFileName + ".tmp");
        Directory.CreateDirectory(temporaryPath);

        await using (var failing = NewSession(handler, project.Home))
        {
            var failure = await Assert.ThrowsAsync<ProjectOperationException>(
                () => failing.OpenAsync(CancellationToken.None));
            Assert.Equal(ProjectOperationErrorCode.ManifestWriteFailed, failure.Code);
        }

        // The durable conversation is valid; only the local record failed. Nothing was recorded,
        // and it was never reported as a successful write.
        Assert.Null(_store.Open(project.Home).Project!.Manifest.LegacyProjectControlConversationId);

        Directory.Delete(temporaryPath);
        await using var retry = NewSession(handler, project.Home);
        var opened = await retry.OpenAsync(CancellationToken.None);

        // The retry re-derives the SAME deterministic command id, so the Host returns the same
        // conversation rather than creating a second one for this Project.
        Assert.Equal(conversationId, opened);
        var commandIds = handler.Posts
            .Where(p => p.Route == "conversations/project-control")
            .Select(p => p.Body.GetProperty("commandId").GetString())
            .Distinct()
            .ToList();
        Assert.Single(commandIds);
        Assert.Equal(conversationId, _store.Open(project.Home).Project!.Manifest.LegacyProjectControlConversationId);
    }

    // ── 3. A control turn carries nothing it should not ────────────────────────────

    [Fact]
    public async Task AControlTurn_PostsOnlyItsConversationCommandIdAndText()
    {
        var project = _store.Create("Ship a todos API", null, null);
        var conversationId = Guid.NewGuid();
        var handler = new ScriptedControlHostHandler(conversationId);
        await using var session = NewSession(handler, project.Home);
        await session.OpenAsync(CancellationToken.None);

        var commandId = Guid.NewGuid();
        var accepted = await session.SubmitAsync(commandId, "narrow the scope", CancellationToken.None);

        Assert.Equal(conversationId, accepted.ConversationId);
        var submit = Assert.Single(handler.Posts, p => p.Route.EndsWith("/control-messages", StringComparison.Ordinal));

        // Asserted at the JSON-PROPERTY level: the wire body has no field able to carry a project
        // goal, capability, local path, tool, mission, or run id — a caller cannot supply one even
        // by hand-crafting the request.
        var properties = submit.Body.EnumerateObject().Select(p => p.Name).Order().ToList();
        Assert.Equal(["commandId", "conversationId", "text"], properties);
        Assert.Equal(commandId.ToString(), submit.Body.GetProperty("commandId").GetString(), ignoreCase: true);
        Assert.Equal("narrow the scope", submit.Body.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SubmittingBeforeOpening_Fails_RatherThanCreatingAConversation()
    {
        var project = _store.Create("Ship a todos API", null, null);
        var handler = new ScriptedControlHostHandler(Guid.NewGuid());
        await using var session = NewSession(handler, project.Home);

        // A dedicated type, so the endpoint can map exactly this to a typed outcome without also
        // catching unrelated InvalidOperationExceptions.
        await Assert.ThrowsAsync<MissionControlNotOpenedException>(
            () => session.SubmitAsync(Guid.NewGuid(), "refine", CancellationToken.None));

        Assert.Empty(handler.Posts);
    }

    // ── 4. Expected Conversation-service rejections map to typed codes ─────────────

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, ProjectOperationErrorCode.MissionControlInvalid)]
    [InlineData(HttpStatusCode.NotFound, ProjectOperationErrorCode.MissionControlNotFound)]
    [InlineData(HttpStatusCode.Conflict, ProjectOperationErrorCode.MissionControlConflict)]
    public void AnExpectedHostRejection_MapsToItsTypedCode(HttpStatusCode status, ProjectOperationErrorCode expected)
        => Assert.Equal(expected, ProjectControlRuntimeSession.ToErrorCode(status));

    // An unexpected status is left to fail the transport rather than being laundered into a domain
    // code the surface would render as an ordinary outcome.
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    public void AnUnexpectedHostStatus_IsNotADomainCode(HttpStatusCode status)
        => Assert.Null(ProjectControlRuntimeSession.ToErrorCode(status));

    [Fact]
    public async Task AConflictFromTheHost_SurfacesWithItsStatus()
    {
        var project = _store.Create("Ship a todos API", null, null);
        var handler = new ScriptedControlHostHandler(Guid.NewGuid()) { CreateStatus = HttpStatusCode.Conflict };
        await using var session = NewSession(handler, project.Home);

        var failure = await Assert.ThrowsAsync<HttpRequestException>(() => session.OpenAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, failure.StatusCode);
        Assert.Null(_store.Open(project.Home).Project!.Manifest.LegacyProjectControlConversationId);
    }

    // ── helpers ────────────────────────────────────────────────────────────────────

    private ProjectControlRuntimeSession NewSession(
        HttpMessageHandler handler, string home, Action<ClientRuntimeEvent>? publish = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://conversation-host.test/") };
        return new ProjectControlRuntimeSession(
            "session-1", home, _store, new ConversationHostClient(http), publish ?? (_ => { }), CancellationToken.None);
    }

    private static ConversationEvent ControlEvent(
        Guid conversationId, long sequence, ConversationEventKind kind, ConversationParticipant participant, string text) =>
        new(Guid.NewGuid(), 1, conversationId, null, sequence, kind, participant,
            null, text, null, null, null, null, null, null, DateTimeOffset.UtcNow);

    private static string ToSseBody(params ConversationEvent[] events) =>
        string.Concat(events.Select(e =>
            $"event: conversation-event\nid: {e.Sequence}\ndata: " +
            JsonSerializer.Serialize(e, ConversationContractsJsonContext.Default.ConversationEvent) + "\n\n"));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(20);
        Assert.True(condition());
    }

    private sealed record RecordedPost(string Route, JsonElement Body);

    /// <summary>Scripted Conversation service: records every POST with its route and parsed body,
    /// and serves one SSE payload before holding the stream open.</summary>
    private sealed class ScriptedControlHostHandler(Guid conversationId, string sse = "") : HttpMessageHandler
    {
        private int _acceptedSequence;

        public List<RecordedPost> Posts { get; } = [];
        public HttpStatusCode CreateStatus { get; init; } = HttpStatusCode.Created;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var route = request.RequestUri!.AbsolutePath.TrimStart('/');

            if (request.Method == HttpMethod.Get)
            {
                // Serve the scripted replay, then hold so the tail's reconnect loop does not spin.
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(sse, Encoding.UTF8, "text/event-stream"),
                };
                return response;
            }

            var json = await request.Content!.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(json);
            Posts.Add(new RecordedPost(route, document.RootElement.Clone()));

            if (route == "conversations/project-control")
            {
                if (CreateStatus != HttpStatusCode.Created)
                    return new HttpResponseMessage(CreateStatus) { Content = new StringContent("rejected") };

                return Json(new CreateProjectControlConversationResponse(conversationId, 0),
                    ConversationContractsJsonContext.Default.CreateProjectControlConversationResponse);
            }

            return Json(new SubmitProjectControlMessageResponse(conversationId, ++_acceptedSequence),
                ConversationContractsJsonContext.Default.SubmitProjectControlMessageResponse);
        }

        private static HttpResponseMessage Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type) =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(value, type), Encoding.UTF8, "application/json"),
            };
    }
}
