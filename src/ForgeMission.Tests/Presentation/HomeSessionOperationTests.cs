using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bunit;
using ForgeMission.ClientRuntime.Presentation.Pages;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 43.17 Task 3 — Home owns one cancellable session/view operation. The fake channel counts
/// concurrent subscriptions, so "exactly one subscriber survives a replacement" is a measured peak
/// rather than an inference from reading the page.
/// </summary>
public sealed class HomeSessionOperationTests : BunitContext
{
    private readonly FakeClientRuntimeChannel channel = new();

    public HomeSessionOperationTests() => Services.AddSingleton<IClientRuntimeChannel>(channel);
    // --- 43.20 task 1: the launcher renders state and invokes contracts, and owns no rule -------

    [Fact]
    public void Boot_IssuesNoRequestAndOpensNoSubscription()
    {
        var page = Render<Home>();

        Assert.Empty(channel.Requests);
        Assert.Equal(0, channel.ActiveSubscriptions);
        Assert.Equal(0, channel.SubscriptionsStarted);
        Assert.Single(page.FindAll(".pl-goal"));
    }

    [Fact]
    public async Task EnteringAGoal_SendsExactlyOneDraftRequest_WithNoOverrides()
    {
        var page = Render<Home>();

        await DraftAsync(page, "Todos API");

        var draft = Assert.Single(channel.Requests.OfType<ProjectDraftRequest>());
        Assert.Equal("Todos API", draft.Goal);
        Assert.Null(draft.TitleOverride);
        Assert.Null(draft.HomeOverride);
        Assert.Empty(channel.Requests.OfType<ProjectCreateRequest>());
    }

    // No Continue control exists: the reference has one primary action, so committing the goal is
    // what asks for a draft. A blank goal commits nothing and enables nothing.
    [Fact]
    public async Task ABlankGoal_SendsNothing_AndLeavesCreateDisabled()
    {
        var page = Render<Home>();

        await CommitGoalAsync(page, "   ");

        Assert.Empty(channel.Requests);
        Assert.True(page.Find(".pl-create").HasAttribute("disabled"));
    }

    // The page shows what the Client Runtime returned and sends back whatever is left in the
    // fields. It never derives a title, slug, or home of its own.
    [Fact]
    public async Task TheDraftsTitleAndHome_RenderAsEditableFields_AndAreSentVerbatimOnCreate()
    {
        var page = Render<Home>();
        await DraftAsync(page, "Todos API");

        Assert.Equal("Todos API", page.Find(".pl-name").GetAttribute("value"));
        Assert.Equal(FakeClientRuntimeChannel.DefaultHome, page.Find(".pl-location").GetAttribute("value"));

        await InputAsync(page, ".pl-name", "Renamed by hand");
        await StartReplacementAsync(page, ".pl-create");

        var create = Assert.Single(channel.Requests.OfType<ProjectCreateRequest>());
        Assert.Equal("Todos API", create.Goal);
        Assert.Equal("Renamed by hand", create.Title);
        Assert.Equal(FakeClientRuntimeChannel.DefaultHome, create.HomePath);
    }

    [Fact]
    public async Task ACreatedProject_RendersItsTitleAndHome_AndStartsExactlyOneSubscription()
    {
        var page = await OpenCreatedProjectAsync();

        // The header band keeps its product identity; the open Project is named on its own line.
        Assert.Equal("AI Workbench", page.Find(".subtitle").TextContent);
        var openProject = page.Find(".workspace-path").TextContent;
        Assert.Contains("Todos API", openProject);
        Assert.Contains(FakeClientRuntimeChannel.DefaultHome, openProject);
        Assert.Empty(page.FindAll(".pl-goal"));
        Assert.Equal(1, channel.SubscriptionsStarted);
        Assert.Equal(1, channel.ActiveSubscriptions);
    }

    [Fact]
    public async Task AGoalRequiredFolder_RendersTheProposal_AndCreatesNothingUntilConfirmed()
    {
        var page = Render<Home>();
        channel.RespondWith(FakeClientRuntimeChannel.GoalRequired("/src/existing", "existing"));

        await ClickAsync(page, ".pl-open-link");
        await InputAsync(page, ".pl-open-path", "/src/existing");
        await ClickAsync(page, ".pl-open-go");

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".pl-notice")));
        Assert.Equal("existing", page.Find(".pl-name").GetAttribute("value"));
        Assert.Equal("/src/existing", page.Find(".pl-location").GetAttribute("value"));
        Assert.Empty(channel.Requests.OfType<ProjectCreateRequest>());
        Assert.Equal(0, channel.SubscriptionsStarted);

        await InputAsync(page, ".pl-goal", "Add pagination");
        await StartReplacementAsync(page, ".pl-create");

        var create = Assert.Single(channel.Requests.OfType<ProjectCreateRequest>());
        Assert.Equal("Add pagination", create.Goal);
        Assert.Equal("/src/existing", create.HomePath);
    }

    [Fact]
    public async Task AFailedProjectOperation_RendersItsMessage_AndLeavesNoSession()
    {
        var page = Render<Home>();
        channel.RespondWith(FakeClientRuntimeChannel.Failed(
            ProjectOperationErrorCode.HomeNotFound, "No directory exists at /nope."));

        await ClickAsync(page, ".pl-open-link");
        await InputAsync(page, ".pl-open-path", "/nope");
        await ClickAsync(page, ".pl-open-go");

        page.WaitForAssertion(() => Assert.Contains("No directory exists at /nope.",
            page.Find(".pl-error").TextContent));
        Assert.Equal(0, channel.SubscriptionsStarted);
        Assert.Single(page.FindAll(".pl-goal"));
    }

    [Fact]
    public async Task AFailedDraft_RendersItsMessage_AndDoesNotOfferCreate()
    {
        var page = Render<Home>();
        channel.FailNextDraft(ProjectOperationErrorCode.InvalidGoal, "A goal is required to name a Project.");

        await CommitGoalAsync(page, "Todos API");

        page.WaitForAssertion(() => Assert.Contains("A goal is required", page.Find(".pl-error").TextContent));
        Assert.Empty(channel.Requests.OfType<ProjectCreateRequest>());
    }

    // --- 43.20 task 2: Mission Control is the sole active conversation --------------------------

    [Fact]
    public async Task OpeningAProject_OpensMissionControl_WithNothingButItsSessionId()
    {
        var page = await OpenCreatedProjectAsync();

        var open = Assert.Single(channel.Requests.OfType<OpenProjectMissionControlRequest>());
        Assert.Equal(channel.CurrentSessionId, open.SessionId);
        // Everything else the conversation needs — the Project home, the manifest ID, the goal —
        // is resolved below Presentation. The contract has no field to carry any of it.
        Assert.Single(typeof(OpenProjectMissionControlRequest).GetProperties());
        _ = page;
    }

    // The one send path. There is no mission to choose, so there is nothing to fall back to.
    [Fact]
    public async Task AComposerTurn_SendsExactlyOneControlTurn_AndNoPromptRequest()
    {
        var page = await OpenCreatedProjectAsync();

        await SendTurnAsync(page, "narrow this to the API only");

        var turn = Assert.Single(channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>());
        Assert.Equal(channel.CurrentSessionId, turn.SessionId);
        Assert.Equal("narrow this to the API only", turn.Text);
        Assert.NotEqual(Guid.Empty, turn.CommandId);
    }

    // Structural, not behavioural: the page cannot reach the Janus prompt path or the mission-switch
    // path because it no longer references either contract. The fake channel below throws on both,
    // so any reintroduction fails here rather than silently becoming a second surface behaviour.
    [Fact]
    public async Task ThePage_NeverSendsAPromptRequestOrASessionSetupRequest()
    {
        var page = await OpenCreatedProjectAsync();
        await SendTurnAsync(page, "refine it");

        Assert.Empty(channel.Requests.OfType<PromptRequest>());
        Assert.Empty(channel.Requests.OfType<SessionSetupRequest>());
        // No picker exists to render in any state.
        Assert.Empty(page.FindAll(".mission-trigger"));
        Assert.Empty(page.FindAll(".mission-menu"));
    }

    [Fact]
    public void WithNoProjectOpen_NoMissionControlRequestCanBeSent()
    {
        var page = Render<Home>();

        Assert.Empty(page.FindAll(".composer-input"));
        Assert.Empty(channel.Requests.OfType<OpenProjectMissionControlRequest>());
        Assert.Empty(channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>());
    }

    [Fact]
    public async Task AFailedMissionControlOpen_RendersItsTypedMessage()
    {
        channel.FailNextMissionControlOpen(
            ProjectOperationErrorCode.ManifestWriteFailed, "Could not update forge.project.json.");

        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Contains(
            "Could not update forge.project.json.", page.Find(".error-banner").TextContent));
    }

    [Fact]
    public async Task AFailedControlTurn_RendersItsTypedMessage()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextControlTurn(
            ProjectOperationErrorCode.MissionControlConflict, "CommandId already used with different content.");

        await SendTurnAsync(page, "refine it");

        page.WaitForAssertion(() => Assert.Contains(
            "CommandId already used with different content.", page.Find(".error-banner").TextContent));
    }

    // The turn renders from the relayed durable stream, not from the submit response — the same
    // rule the Janus path already followed, now the only rule on this surface.
    [Fact]
    public async Task ADurableControlEvent_RendersThroughTheExistingTranscript()
    {
        var page = await OpenCreatedProjectAsync();

        channel.Publish(ControlEvent(1, ConversationEventKind.UserMessage, ConversationParticipant.User, "refine it"));
        channel.Publish(ControlEvent(2, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.MissionControl, "What outcome would count as done?"));

        page.WaitForAssertion(() => Assert.Contains(
            "What outcome would count as done?", page.Find(".convo-participant-text").TextContent));
        Assert.Equal("Mission Control", page.Find(".convo-participant-name").TextContent);
        Assert.Contains("refine it", page.Find(".convo-user-bubble").TextContent);
    }

    // --- 43.17 task 3: one cancellable session/view operation ----------------------------------

    [Fact]
    public async Task OpenedProject_FocusesTheComposer()
    {
        await OpenCreatedProjectAsync();

        Assert.Contains(JSInterop.Invocations,
            invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task UnexpectedStreamFailure_BecomesVisibleRetryableState()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Contains("stream reset", page.Find(".connection-banner").TextContent));
        Assert.Single(page.FindAll(".connection-retry"));
    }

    [Fact]
    public async Task Retry_OpensANewSubscriptionAndShowsThePersistentGapNotice()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));

        var before = channel.SubscriptionsStarted;
        await ClickAsync(page, ".connection-retry");

        page.WaitForAssertion(() => Assert.Equal(before + 1, channel.SubscriptionsStarted));
        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Empty(page.FindAll(".connection-banner"));
        Assert.Contains("Updates that arrived while disconnected are not shown",
            page.Find(".gap-notice").TextContent);

        // Persistence across later events is asserted on its own below.
    }

    // The notice records a permanent gap in a transcript built only from relayed events, so nothing
    // short of a new view retracts it — not later events, and not a further successful retry.
    //
    // Its clearing half is deliberately NOT asserted here: BeginViewAsync still clears it, but with
    // the mission picker removed (43.20 task 2) no action on this surface begins a second view
    // while a Project is open. Switching Projects is Task 3 rail work; asserting it now would mean
    // testing a path a person cannot reach.
    [Fact]
    public async Task GapNotice_PersistsAcrossLaterEventsAndFurtherRetries()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        await ClickAsync(page, ".connection-retry");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".gap-notice")));

        channel.Publish(ControlEvent(1, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.MissionControl, "still here"));
        page.WaitForAssertion(() => Assert.Contains("still here", page.Find(".convo-participant-text").TextContent));

        Assert.Single(page.FindAll(".gap-notice"));
    }

    // Mission Control's transcript is built only from relayed durable events, so a turn submitted
    // while the stream is down would add a second unrecoverable gap.
    [Fact]
    public async Task WhileDisconnected_ControlTurnsAreBlocked()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        Assert.True(page.Find(".composer-input").HasAttribute("disabled"));
        Assert.True(page.Find(".composer-send").HasAttribute("disabled"));
    }

    // Disposal cancels the subscription and any in-flight call. That cancellation is EXPECTED, so
    // it leaves no error banner behind — the silent-cancellation guarantee, asserted where the
    // cancellation actually happens now that no mission switch exists to trigger one.
    [Fact]
    public async Task DisposeAsync_LeavesNoActiveSubscription_AndIsSilent()
    {
        var page = await OpenCreatedProjectAsync();
        Assert.Equal(1, channel.ActiveSubscriptions);

        await page.Instance.DisposeAsync();

        Assert.Equal(0, channel.ActiveSubscriptions);
        Assert.Empty(page.FindAll(".error-banner"));
        Assert.Empty(page.FindAll(".connection-banner"));
    }

    // Boot creates nothing, so every test that needs a workbench opens a Project first — through
    // the same two contract calls a TUI would make.
    private async Task<IRenderedComponent<Home>> OpenCreatedProjectAsync()
    {
        var page = Render<Home>();
        await DraftAsync(page, "Todos API");
        await StartReplacementAsync(page, ".pl-create");
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".pl-goal")));
        return page;
    }

    private async Task DraftAsync(IRenderedComponent<Home> page, string goal)
    {
        await CommitGoalAsync(page, goal);
        page.WaitForAssertion(() => Assert.True(
            page.Find(".pl-name").GetAttribute("value")?.Length > 0 || page.FindAll(".pl-error").Count > 0));
    }

    // Typing then committing, exactly as a person does: the change event is the goal-commit
    // trigger the launcher listens for.
    private static Task CommitGoalAsync(IRenderedComponent<Home> page, string goal) =>
        page.InvokeAsync(() =>
        {
            var field = page.Find(".pl-goal");
            field.Input(new ChangeEventArgs { Value = goal });
            field.Change(new ChangeEventArgs { Value = goal });
        });

    // One durable control fact, exactly as the Client Runtime relays it — no run, no tool.
    private ClientRuntimeEvent ControlEvent(
        long sequence, ConversationEventKind kind, ConversationParticipant participant, string text) =>
        new(ClientRuntimeEventKind.ConversationEvent, channel.CurrentSessionId,
            Conversation: new ConversationEvent(
                Guid.NewGuid(), 1, channel.ControlConversationId, null, sequence, kind, participant,
                null, text, null, null, null, null, null, null, DateTimeOffset.UtcNow));

    // The click itself only starts the replacement; its async handler completes later, so the
    // helper waits for the new subscription (or a surfaced failure) before returning.
    private async Task StartReplacementAsync(IRenderedComponent<Home> page, string selector, string? text = null)
    {
        var before = channel.SubscriptionsStarted;
        await ClickAsync(page, selector, text);
        page.WaitForAssertion(() => Assert.True(
            channel.SubscriptionsStarted > before || page.FindAll(".pl-error").Count > 0));
    }

    private async Task SendTurnAsync(IRenderedComponent<Home> page, string text)
    {
        var before = channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().Count();
        await InputAsync(page, ".composer-input", text);
        await ClickAsync(page, ".composer-send");
        page.WaitForAssertion(() => Assert.True(
            channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().Count() > before));
    }

    // Find and dispatch inside one dispatcher turn: a concurrent stream render between the two
    // would otherwise invalidate the element bunit found.
    private static Task InputAsync(IRenderedComponent<Home> page, string selector, string value) =>
        page.InvokeAsync(() => page.Find(selector).Input(new ChangeEventArgs { Value = value }));

    private static Task ClickAsync(IRenderedComponent<Home> page, string selector, string? text = null) =>
        page.InvokeAsync(() => Match(page, selector, text).Click());

    private static AngleSharp.Dom.IElement Match(IRenderedComponent<Home> page, string selector, string? text) =>
        text is null
            ? page.Find(selector)
            : page.FindAll(selector).First(candidate => candidate.TextContent.Contains(text, StringComparison.Ordinal));

    private sealed class FakeClientRuntimeChannel : IClientRuntimeChannel
    {
        private readonly Lock gate = new();
        private readonly List<Channel<ClientRuntimeEvent>> streams = [];
        private Exception? nextSubscriptionFault;
        private ProjectOperationError? nextOpenError;
        private ProjectOperationError? nextTurnError;
        private long acceptedSequence;
        private ProjectOperationResponse? nextProjectResponse;
        private ProjectOperationError? nextDraftError;
        private int sessionCounter;

        public int ActiveSubscriptions { get; private set; }
        public int PeakSubscriptions { get; private set; }
        public int SubscriptionsStarted { get; private set; }
        public string CurrentSessionId { get; private set; } = "session-0";

        public void FaultNextSubscription(Exception fault) => nextSubscriptionFault = fault;

        public Guid ControlConversationId { get; } = Guid.NewGuid();

        public void FailNextMissionControlOpen(ProjectOperationErrorCode code, string message) =>
            nextOpenError = new ProjectOperationError(code, message);

        public void FailNextControlTurn(ProjectOperationErrorCode code, string message) =>
            nextTurnError = new ProjectOperationError(code, message);

        private OpenProjectMissionControlResponse OpenMissionControl()
        {
            if (nextOpenError is { } error)
            {
                nextOpenError = null;
                return new OpenProjectMissionControlResponse(null, error);
            }

            return new OpenProjectMissionControlResponse(ControlConversationId);
        }

        private SubmitProjectMissionControlTurnResponse SubmitControlTurn()
        {
            if (nextTurnError is { } error)
            {
                nextTurnError = null;
                return new SubmitProjectMissionControlTurnResponse(null, 0, error);
            }

            return new SubmitProjectMissionControlTurnResponse(ControlConversationId, ++acceptedSequence);
        }

        public void Publish(ClientRuntimeEvent message)
        {
            lock (gate)
            {
                foreach (var stream in streams)
                    stream.Writer.TryWrite(message);
            }
        }

        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        {
            Requests.Add(request!);
            object response = request switch
            {
                ProjectDraftRequest draft => DraftResponse(draft),
                ProjectCreateRequest create => nextProjectResponse ?? Created(create.Title ?? "Todos API", create.HomePath ?? DefaultHome),
                ProjectOpenRequest open => nextProjectResponse ?? Opened(open.HomePath),
                OpenProjectMissionControlRequest => OpenMissionControl(),
                SubmitProjectMissionControlTurnRequest => SubmitControlTurn(),
                // PromptRequest and SessionSetupRequest are deliberately ABSENT. Mission Control is
                // the sole active conversation while a Project is open (43.20 task 2), so a page
                // that sent either would fail loudly here rather than quietly acquiring a second
                // surface behaviour. Both contracts live on in Client Runtime and its own tests.
                _ => throw new InvalidOperationException($"Unexpected request: {typeof(TRequest).Name}."),
            };

            nextProjectResponse = null;
            return Task.FromResult((TResponse)response);
        }

        // Everything a Project operation returns is decided here, never by the page: these fakes
        // stand in for the Client Runtime's derivation and manifest rules, which are asserted in
        // ProjectStoreTests and ProjectTransportContractTests instead.
        public const string DefaultHome = "/profile/Forge/Projects/todos-api";

        public List<object> Requests { get; } = [];

        public void RespondWith(ProjectOperationResponse response) => nextProjectResponse = response;

        public void FailNextDraft(ProjectOperationErrorCode code, string message) =>
            nextDraftError = new ProjectOperationError(code, message);

        public ProjectOperationResponse Created(string title, string home) =>
            new(ProjectOperationOutcome.Created,
                new ProjectSession(NextSession(), [], new ProjectSummary(Guid.NewGuid(), title, "Todos API", home)));

        public ProjectOperationResponse Opened(string home) =>
            new(ProjectOperationOutcome.Opened,
                new ProjectSession(NextSession(), [], new ProjectSummary(Guid.NewGuid(), "Todos API", "Todos API", home)));

        public static ProjectOperationResponse GoalRequired(string home, string title) =>
            new(ProjectOperationOutcome.GoalRequired, Proposal: new ProjectHomeProposal(home, title));

        public static ProjectOperationResponse Failed(ProjectOperationErrorCode code, string message) =>
            new(ProjectOperationOutcome.Failed, Error: new ProjectOperationError(code, message));

        private ProjectDraftResponse DraftResponse(ProjectDraftRequest request)
        {
            if (nextDraftError is { } error)
            {
                nextDraftError = null;
                return new ProjectDraftResponse(Draft: null, error);
            }

            return new ProjectDraftResponse(
                new ProjectHomeProposal(request.HomeOverride ?? DefaultHome, request.TitleOverride ?? "Todos API"),
                Error: null);
        }

        public async IAsyncEnumerable<ClientRuntimeEvent> Subscribe([EnumeratorCancellation] CancellationToken ct)
        {
            var stream = Channel.CreateUnbounded<ClientRuntimeEvent>();
            lock (gate)
            {
                streams.Add(stream);
                ActiveSubscriptions++;
                PeakSubscriptions = Math.Max(PeakSubscriptions, ActiveSubscriptions);
                SubscriptionsStarted++;
            }

            try
            {
                if (nextSubscriptionFault is { } fault)
                {
                    nextSubscriptionFault = null;
                    throw fault;
                }

                await foreach (var message in stream.Reader.ReadAllAsync(ct))
                    yield return message;
            }
            finally
            {
                lock (gate)
                {
                    streams.Remove(stream);
                    ActiveSubscriptions--;
                }
            }
        }

        private string NextSession() => CurrentSessionId = $"session-{++sessionCounter}";
    }
}
