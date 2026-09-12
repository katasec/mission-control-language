using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.ClientRuntime;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;
// Contracts and Transport each name these, one per side of the boundary; the fake Host speaks the
// durable side, so it names those explicitly.
using HostCreateMissionConversationResponse = ForgeMission.Conversations.Contracts.CreateMissionConversationResponse;
using HostListMissionConversationsResponse = ForgeMission.Conversations.Contracts.ListMissionConversationsResponse;

namespace ForgeMission.Tests.Application;

/// <summary>
/// Phase 48. The four product actions, held to what they may and may not do: resolve the one managed
/// Project, create chats on its shipped version, carry only ids and text to Host, and report each
/// failure as itself. It delegates history to the one stream owner and reads no page itself.
/// </summary>
public sealed class MissionChatServiceTests : IDisposable
{
    private readonly string profile = Path.Combine(Path.GetTempPath(), "forge-chat-service", Guid.NewGuid().ToString("N"));
    private readonly string root;
    private readonly FakeHost host = new();
    private readonly ApplicationSessionService sessions;
    private readonly ProjectService projects;
    private readonly IMissionChatService service;

    public MissionChatServiceTests()
    {
        root = Path.Combine(profile, "Forge", "Projects");
        sessions = new ApplicationSessionService(CapabilityAuthorizationPolicy.Default, _ => { }, CancellationToken.None);
        projects = new ProjectService(sessions, root);
        var versions = new MissionVersionService(projects);
        var factory = new SingleClientFactory(host);
        var conversations = new MissionConversationService(versions, factory, sessions);
        var hands = new MissionHandsConversationService(projects, sessions, factory, CapabilityAuthorizationPolicy.Default, CancellationToken.None);
        service = new MissionChatService(new ManagedChatProjectService(projects, versions), projects, versions, conversations,
            hands, sessions, factory, _ => { }, CancellationToken.None);
    }

    [Fact]
    public async Task Start_ProvisionsTheManagedProject_CreatesOneChat_AndAttachesItsFixedProfile()
    {
        var response = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);

        Assert.Null(response.Failure);
        Assert.NotNull(response.Session);
        Assert.Equal(Path.Combine(root, response.Session!.Project.ProjectId.ToString("D")), response.Session.Project.Home);
        Assert.NotNull(response.ConversationId);
        Assert.Equal(new MissionChatPin("Janus", 1, "1.4", MissionHandsProfile.ProjectWorkspace, true), response.Pin);
        Assert.Equal(MissionChatAccessState.Attached, response.Access!.State);
        Assert.Empty(response.Events!);
        Assert.Equal(0, response.ThroughSequence);
        var row = Assert.Single(response.Rows!);
        Assert.Equal(response.ConversationId, row.ConversationId);
        Assert.Equal("New chat", row.Title);
        Assert.False(row.HasMessages);
        // Host received the immutable launch only: no Project path and no capability declaration.
        Assert.DoesNotContain(response.Session.Project.Home, host.Bodies, StringComparer.Ordinal);
        Assert.All(host.Bodies, body => Assert.DoesNotContain("capabilit", body, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ASecondStart_ReusesTheSameManagedProject_AndCreatesOneFurtherChat()
    {
        var first = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);
        var second = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);

        Assert.Equal(first.Session!.Project.ProjectId, second.Session!.Project.ProjectId);
        Assert.Equal(first.Session.Project.Home, second.Session.Project.Home);
        Assert.NotEqual(first.ConversationId, second.ConversationId);
        Assert.Equal(2, second.Rows!.Count);
        Assert.Single(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task NewChat_CreatesOneMoreInTheSameProject_WithoutResolvingItAgain()
    {
        var started = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);

        var created = await service.CreateAsync(new CreateMissionChatRequest(started.Session!.SessionId), CancellationToken.None);

        Assert.Null(created.Failure);
        Assert.NotEqual(started.ConversationId, created.ConversationId);
        Assert.Equal(2, created.Rows!.Count);
        Assert.Equal(started.Pin, created.Pin);
        Assert.Single(Directory.GetDirectories(root));
    }

    [Fact]
    public async Task Submit_CarriesOnlyTheConversationCommandAndText()
    {
        var started = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);
        var commandId = Guid.NewGuid();

        // Deliberately worded without any of the terms the body must not carry, so the assertion below
        // is about the request and not about the message.
        const string message = "Sketch the rollout steps";
        var response = await service.SubmitAsync(new SubmitMissionChatTurnRequest(
            started.Session!.SessionId, started.ConversationId!.Value, commandId, message), CancellationToken.None);

        Assert.Null(response.Failure);
        Assert.NotNull(response.TurnId);
        var submitted = JsonSerializer.Deserialize(host.LastTurnBody!, ConversationContractsJsonContext.Default.SubmitMissionTurnRequest)!;
        Assert.Equal(started.ConversationId, submitted.ConversationId);
        Assert.Equal(commandId, submitted.CommandId);
        Assert.Equal(message, submitted.Text);
        foreach (var forbidden in new[] { "package", "profile", "launch", "definition", "title", started.Session.Project.Home })
            Assert.DoesNotContain(forbidden, host.LastTurnBody!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnEmptyMessage_IsRefusedBeforeAnyHostCall()
    {
        var started = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);
        var before = host.TurnCount;

        var response = await service.SubmitAsync(new SubmitMissionChatTurnRequest(
            started.Session!.SessionId, started.ConversationId!.Value, Guid.NewGuid(), "   "), CancellationToken.None);

        Assert.Equal(MissionChatFailureCode.TurnConflict, response.Failure!.Code);
        Assert.Equal(before, host.TurnCount);
    }

    [Fact]
    public async Task Open_RefusesAConversationThatIsNotInThisProject_AndStillListsTheRealOnes()
    {
        var started = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);

        var response = await service.OpenAsync(new OpenMissionChatRequest(started.Session!.SessionId, Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(MissionChatFailureCode.ChatNotFound, response.Failure!.Code);
        Assert.Single(response.Rows!);
        Assert.Null(response.Events);
    }

    [Fact]
    public async Task Open_ReturnsThePinnedProjectionAndTheCompleteHistory_ForAChatInThisProject()
    {
        var started = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);
        host.AppendTurn(started.ConversationId!.Value, "Draft the launch plan");

        var response = await service.OpenAsync(new OpenMissionChatRequest(
            started.Session!.SessionId, started.ConversationId!.Value), CancellationToken.None);

        Assert.Null(response.Failure);
        Assert.Equal(started.ConversationId, response.ConversationId);
        Assert.Equal(started.Pin, response.Pin);
        Assert.Equal(1, response.ThroughSequence);
        var only = Assert.Single(response.Events!);
        Assert.Equal("Draft the launch plan", only.Text);
    }

    [Fact]
    public async Task WhenTheAttachmentFails_TheChatIsStillRealAndListed_WithItsTypedStatus()
    {
        host.RefuseAttachment = true;

        var response = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);

        Assert.Null(response.Failure);
        Assert.NotNull(response.ConversationId);
        Assert.Equal(MissionChatAccessState.Unavailable, response.Access!.State);
        Assert.Equal("Host refused the attachment.", response.Access.Reason);
        Assert.Single(response.Rows!);
    }

    [Fact]
    public async Task WhenACreateResponseIsLost_TheOutcomeIsUncertain_AndNoSecondChatIsCreated()
    {
        host.LoseCreateResponse = true;

        var response = await service.StartAsync(new StartMissionChatRequest(), CancellationToken.None);

        Assert.Equal(MissionChatFailureCode.CreateUncertain, response.Failure!.Code);
        Assert.Null(response.ConversationId);
        Assert.Equal(1, host.CreateAttempts);
    }

    [Fact]
    public async Task AStaleOrForeignSession_IsNotAProductOutcome()
    {
        var foreign = Guid.NewGuid().ToString("N");

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(new CreateMissionChatRequest(foreign), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.OpenAsync(new OpenMissionChatRequest(foreign, Guid.NewGuid()), CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.SubmitAsync(
            new SubmitMissionChatTurnRequest(foreign, Guid.NewGuid(), Guid.NewGuid(), "hello"), CancellationToken.None));
    }

    // --- a Conversation Host that answers this journey's calls ----------------------------------

    private sealed class FakeHost : HttpMessageHandler
    {
        private readonly Dictionary<Guid, (Guid ProjectId, DurableMissionLaunch Launch, List<ConversationEvent> Events)> conversations = [];

        public List<string> Bodies { get; } = [];
        public string? LastTurnBody { get; private set; }
        public int TurnCount { get; private set; }
        public int CreateAttempts { get; private set; }
        public bool RefuseAttachment { get; set; }
        public bool LoseCreateResponse { get; set; }

        public void AppendTurn(Guid conversationId, string text)
        {
            var state = conversations[conversationId];
            state.Events.Add(new ConversationEvent(Guid.NewGuid(), 1, conversationId, null, state.Events.Count + 1,
                ConversationEventKind.UserMessage, ConversationParticipant.User, null, text, null, null, null, null, null, null,
                DateTimeOffset.UnixEpoch));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Content is not null)
                Bodies.Add(await request.Content.ReadAsStringAsync(ct));

            if (path == "/mission-conversations" && request.Method == HttpMethod.Post)
                return Create(Bodies[^1]);
            if (path.StartsWith("/mission-conversations/", StringComparison.Ordinal) && path.EndsWith("/turns", StringComparison.Ordinal))
                return Turn(path, Bodies[^1]);
            if (path.StartsWith("/mission-conversations/", StringComparison.Ordinal) && path.EndsWith("/events", StringComparison.Ordinal))
                return Page(request);
            if (path.StartsWith("/mission-conversations/", StringComparison.Ordinal))
                return List(path);
            if (path == "/mission-hands/attach")
                return Json(new MissionHandsResult(RefuseAttachment ? MissionHandsStatus.Cancelled : MissionHandsStatus.Attached, 0,
                    RefuseAttachment ? "Host refused the attachment." : null), ConversationContractsJsonContext.Default.MissionHandsResult, HttpStatusCode.OK);
            if (path == "/mission-hands/detach")
                return Json(new MissionHandsResult(MissionHandsStatus.Completed, 0), ConversationContractsJsonContext.Default.MissionHandsResult, HttpStatusCode.OK);
            if (path.EndsWith("/events", StringComparison.Ordinal))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("", Encoding.UTF8, "text/event-stream") };
            return Snapshot(path);
        }

        private HttpResponseMessage Create(string body)
        {
            CreateAttempts++;
            if (LoseCreateResponse)
                throw new HttpRequestException("the response never came back");

            var created = JsonSerializer.Deserialize(body, ConversationContractsJsonContext.Default.CreateMissionConversationRequest)!;
            var id = Guid.NewGuid();
            conversations[id] = (created.ProjectId, created.Launch, []);
            return Json(new HostCreateMissionConversationResponse(id, 0, created.Launch),
                ConversationContractsJsonContext.Default.CreateMissionConversationResponse, HttpStatusCode.Created);
        }

        private HttpResponseMessage Turn(string path, string body)
        {
            TurnCount++;
            LastTurnBody = body;
            var id = Guid.Parse(path.Split('/')[^2]);
            return Json(new SubmitMissionTurnResponse(id, Guid.NewGuid(), Guid.NewGuid(), 1, ConversationRunStatus.Queued),
                ConversationContractsJsonContext.Default.SubmitMissionTurnResponse, HttpStatusCode.Accepted);
        }

        private HttpResponseMessage List(string path)
        {
            var projectId = Guid.Parse(path.Split('/')[^1]);
            var summaries = conversations
                .Where(pair => pair.Value.ProjectId == projectId)
                .Select(pair => new MissionConversationSummary(pair.Key, projectId, pair.Value.Launch, ConversationRunStatus.Queued,
                    pair.Value.Events.Count, DateTimeOffset.UnixEpoch, pair.Value.Events.Count == 0 ? "New chat" : pair.Value.Events[0].Text))
                .ToArray();
            return Json(new HostListMissionConversationsResponse(summaries),
                ConversationContractsJsonContext.Default.ListMissionConversationsResponse, HttpStatusCode.OK);
        }

        private HttpResponseMessage Page(HttpRequestMessage request)
        {
            var id = Guid.Parse(request.RequestUri!.AbsolutePath.Split('/')[^2]);
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            var after = long.Parse(query["after"]!);
            var through = long.Parse(query["through"]!);
            var page = conversations[id].Events.Where(evt => evt.Sequence > after && evt.Sequence <= through).ToArray();
            var returned = page.Length == 0 ? after : page[^1].Sequence;
            return Json(new MissionConversationEventPage(id, page, after, through, returned, false),
                ConversationContractsJsonContext.Default.MissionConversationEventPage, HttpStatusCode.OK);
        }

        private HttpResponseMessage Snapshot(string path)
        {
            var id = Guid.Parse(path.Split('/')[^1]);
            var state = conversations[id];
            var snapshot = new ConversationSnapshot(id, "Durable", null, state.Events.Count, ConversationRunStatus.Queued, null,
                DateTimeOffset.UnixEpoch, ConversationPurpose.MissionConversation, state.ProjectId, state.Launch, null, null, null, null, null,
                state.Events.Count == 0 ? "New chat" : state.Events[0].Text);
            return Json(new GetConversationResponse(snapshot), ConversationContractsJsonContext.Default.GetConversationResponse, HttpStatusCode.OK);
        }

        private static HttpResponseMessage Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, HttpStatusCode status) =>
            new(status) { Content = new StringContent(JsonSerializer.Serialize(value, type), Encoding.UTF8, "application/json") };
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://conversation-host/") };
    }

    public void Dispose()
    {
        sessions.DisposeAsync().AsTask().GetAwaiter().GetResult();
        if (Directory.Exists(profile)) Directory.Delete(profile, recursive: true);
    }
}
