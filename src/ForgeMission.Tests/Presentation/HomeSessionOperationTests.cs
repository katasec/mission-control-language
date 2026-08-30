using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bunit;
using ForgeMission.ClientRuntime.Presentation.Pages;
using ForgeMission.ClientRuntime.Transport;
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
        await InputAsync(page, ".open-folder-path", "/src/existing");
        await ClickAsync(page, ".open-folder-go");

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
        await InputAsync(page, ".open-folder-path", "/nope");
        await ClickAsync(page, ".open-folder-go");

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

    // Replacement only: the request carries the open Project's own home and the session it
    // replaces. The Client Runtime enforces the same rule (ProjectTransportContractTests).
    [Fact]
    public async Task AMissionSwitch_ReplacesTheSession_WithinTheOpenProjectsHome()
    {
        var page = await OpenCreatedProjectAsync();
        var openedSessionId = channel.CurrentSessionId;

        await SelectMissionAsync(page, "Janus");

        var setup = Assert.Single(channel.Requests.OfType<SessionSetupRequest>());
        Assert.Equal(FakeClientRuntimeChannel.DefaultHome, setup.WorkspaceRoot);
        Assert.Equal(openedSessionId, setup.ReplacesSessionId);
    }

    [Fact]
    public void WithNoProjectOpen_NoSessionSetupRequestCanBeSent()
    {
        var page = Render<Home>();

        Assert.Empty(page.FindAll(".mission-trigger"));
        Assert.Empty(channel.Requests.OfType<SessionSetupRequest>());
    }

    // --- 43.17 task 3: one cancellable session/view operation ----------------------------------

    [Fact]
    public async Task RepeatedMissionReplacement_LeavesExactlyOneSubscription()
    {
        var page = await OpenCreatedProjectAsync();

        await SelectMissionAsync(page, "Janus");
        await SelectMissionAsync(page, "ChatGPT");
        await SelectMissionAsync(page, "Websearch");

        Assert.Equal(4, channel.SubscriptionsStarted);
        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Equal(1, channel.PeakSubscriptions);
    }

    [Fact]
    public async Task OpenedProject_FocusesTheComposer()
    {
        await OpenCreatedProjectAsync();

        Assert.Contains(JSInterop.Invocations,
            invocation => invocation.Identifier == "Blazor._internal.domWrapper.focus");
    }

    [Fact]
    public async Task Replacement_AwaitsTheOldSubscription_SoEachEventIsAppliedOnce()
    {
        var page = await OpenCreatedProjectAsync();
        await SelectMissionAsync(page, "Websearch");

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "build it");
        channel.Publish(Delta("answer"));

        page.WaitForAssertion(() => Assert.Contains("answer", page.Find(".response-card").TextContent));
        // A surviving second subscriber would have applied the same delta twice.
        Assert.Equal("answer", page.Find(".response-card").TextContent);
    }

    [Fact]
    public async Task ReplacementWhileEventsAreRendering_CompletesWithoutDeadlock()
    {
        var page = await OpenCreatedProjectAsync();

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "streaming");
        for (var index = 0; index < 200; index++)
            channel.Publish(Delta("x"));

        // The replacement cancels and awaits a subscription that is mid-render. If awaiting the
        // event loop from a UI handler could deadlock, this never returns.
        await SelectMissionAsync(page, "Websearch");

        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Empty(page.FindAll(".response-card"));
    }

    [Fact]
    public async Task StalePromptResult_AfterReplacement_CannotMutateTheNewSession()
    {
        var page = await OpenCreatedProjectAsync();

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "slow prompt");
        await SelectMissionAsync(page, "Websearch");

        channel.ReleaseHeldPrompt(new PromptResponse("stale answer"));
        await Task.Delay(50);

        Assert.DoesNotContain("stale answer", page.Markup);
        Assert.DoesNotContain("slow prompt", page.Markup);
        Assert.Empty(page.FindAll(".error-banner"));
        Assert.Empty(page.FindAll(".connection-banner"));
        // The discarded operation did not clear the replacement's own sending state.
        Assert.Equal("Send", page.Find(".composer-send").TextContent.Trim());
    }

    [Fact]
    public async Task ExpectedCancellation_IsSilent()
    {
        var page = await OpenCreatedProjectAsync();
        await SelectMissionAsync(page, "Janus");

        Assert.Empty(page.FindAll(".error-banner"));
        Assert.Empty(page.FindAll(".connection-banner"));
        Assert.Empty(page.FindAll(".gap-notice"));
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

        // The notice records a permanent gap, so later successful events do not retract it.
        channel.Publish(Delta("later"));
        await Task.Delay(50);
        Assert.Single(page.FindAll(".gap-notice"));
    }

    [Fact]
    public async Task GapNotice_ClearsOnlyWhenANewViewBegins()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        await ClickAsync(page, ".connection-retry");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".gap-notice")));

        await SelectMissionAsync(page, "Websearch");

        Assert.Empty(page.FindAll(".gap-notice"));
    }

    [Fact]
    public async Task WhileDisconnected_DurableConversationPromptsAreBlocked_AndMissionPromptsAreNot()
    {
        var page = await OpenCreatedProjectAsync();

        channel.FaultNextSubscription(new IOException("stream reset"));
        await SelectMissionAsync(page, "Janus");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        Assert.True(page.Find(".composer-input").HasAttribute("disabled"));

        channel.FaultNextSubscription(new IOException("stream reset"));
        await SelectMissionAsync(page, "ChatGPT");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        Assert.False(page.Find(".composer-input").HasAttribute("disabled"));
    }

    [Fact]
    public async Task DisposeAsync_LeavesNoActiveSubscription()
    {
        var page = await OpenCreatedProjectAsync();
        Assert.Equal(1, channel.ActiveSubscriptions);

        await page.Instance.DisposeAsync();

        Assert.Equal(0, channel.ActiveSubscriptions);
    }

    // --- 43.18 task 3: the ordinary mission turn's shared activity states -----------------------

    [Fact]
    public async Task ActiveTurnWithNoToolAndNoText_ShowsThinkingForTheSelectedMission()
    {
        var page = await OpenCreatedProjectAsync();

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "build it");

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".convo-activity-thinking")));
        Assert.Contains("ChatGPT is thinking…", page.Find(".convo-activity-text").TextContent);
    }

    [Fact]
    public async Task RunningTool_BecomesWorkingWithItsLabel_AndNoRunningToolRow()
    {
        var page = await OpenCreatedProjectAsync();

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "build it");
        channel.Publish(ToolStatus("Read", "running", "src/Program.cs"));

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".convo-activity-working")));
        Assert.Contains("ChatGPT Reading src/Program.cs…", page.Find(".convo-activity-text").TextContent);
        // The shared activity replaces the running row rather than sitting beside it.
        Assert.Empty(page.FindAll(".tool-row"));
    }

    [Fact]
    public async Task RunningToolOutranksStreamingText_WhileBothFactsArePresent()
    {
        var page = await OpenCreatedProjectAsync();

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "build it");
        channel.Publish(ToolStatus("Read", "running", "src/Program.cs"));
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".convo-activity-working")));

        channel.Publish(Delta("partial answer"));

        page.WaitForAssertion(() => Assert.Contains("partial answer", page.Find(".response-card").TextContent));
        Assert.Single(page.FindAll(".convo-activity-working"));
        Assert.Empty(page.FindAll(".convo-activity-streaming"));
    }

    [Fact]
    public async Task TextDeltaAfterTheToolCompletes_BecomesStreaming_AndKeepsTheCompletedRow()
    {
        var page = await OpenCreatedProjectAsync();

        channel.HoldNextPrompt();
        await SendPromptAsync(page, "build it");
        channel.Publish(ToolStatus("Read", "running", "src/Program.cs"));
        channel.Publish(Delta("partial answer"));
        channel.Publish(ToolStatus("Read", "completed", "src/Program.cs"));

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".convo-activity-streaming")));
        Assert.Contains("ChatGPT is responding…", page.Find(".convo-activity-text").TextContent);
        // The finished call stays as transcript history.
        Assert.Contains("Read src/Program.cs", page.Find(".tool-row").TextContent);
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

    private ClientRuntimeEvent Delta(string text) =>
        new(ClientRuntimeEventKind.MissionTextDelta, channel.CurrentSessionId, Text: text);

    private ClientRuntimeEvent ToolStatus(string toolName, string status, string? target = null) =>
        new(ClientRuntimeEventKind.ToolCallStatus, channel.CurrentSessionId,
            ToolName: toolName, ToolStatus: status, ToolTarget: target);

    private async Task SelectMissionAsync(IRenderedComponent<Home> page, string missionName)
    {
        await ClickAsync(page, ".mission-trigger");
        await StartReplacementAsync(page, ".mission-item", missionName);
    }

    // The click itself only starts the replacement; its async handler completes later, so the
    // helper waits for the new subscription (or a surfaced failure) before returning.
    private async Task StartReplacementAsync(IRenderedComponent<Home> page, string selector, string? text = null)
    {
        var before = channel.SubscriptionsStarted;
        await ClickAsync(page, selector, text);
        page.WaitForAssertion(() => Assert.True(
            channel.SubscriptionsStarted > before || page.FindAll(".pl-error").Count > 0));
    }

    private static async Task SendPromptAsync(IRenderedComponent<Home> page, string prompt)
    {
        await InputAsync(page, ".composer-input", prompt);
        await ClickAsync(page, ".composer-send");
        page.WaitForAssertion(() => Assert.Contains("Sending", page.Find(".composer-send").TextContent));
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
        private TaskCompletionSource<PromptResponse>? heldPrompt;
        private Exception? nextSubscriptionFault;
        private ProjectOperationResponse? nextProjectResponse;
        private ProjectOperationError? nextDraftError;
        private int sessionCounter;

        public int ActiveSubscriptions { get; private set; }
        public int PeakSubscriptions { get; private set; }
        public int SubscriptionsStarted { get; private set; }
        public string CurrentSessionId { get; private set; } = "session-0";

        public void FaultNextSubscription(Exception fault) => nextSubscriptionFault = fault;

        public void HoldNextPrompt() =>
            heldPrompt = new TaskCompletionSource<PromptResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseHeldPrompt(PromptResponse response)
        {
            var held = heldPrompt ?? throw new InvalidOperationException("No prompt is being held.");
            heldPrompt = null;
            held.SetResult(response);
        }

        public void Publish(ClientRuntimeEvent message)
        {
            lock (gate)
            {
                foreach (var stream in streams)
                    stream.Writer.TryWrite(message);
            }
        }

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        {
            Requests.Add(request!);
            object response = request switch
            {
                ProjectDraftRequest draft => DraftResponse(draft),
                ProjectCreateRequest create => nextProjectResponse ?? Created(create.Title ?? "Todos API", create.HomePath ?? DefaultHome),
                ProjectOpenRequest open => nextProjectResponse ?? Opened(open.HomePath),
                SessionSetupRequest => new SessionSetupResponse(NextSession(), []),
                PromptRequest => await PromptAsync(ct),
                _ => throw new InvalidOperationException($"Unexpected request: {typeof(TRequest).Name}."),
            };

            nextProjectResponse = null;
            return (TResponse)response;
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

        private async Task<PromptResponse> PromptAsync(CancellationToken ct) =>
            heldPrompt is null ? new PromptResponse(string.Empty) : await heldPrompt.Task.WaitAsync(ct);

        private string NextSession() => CurrentSessionId = $"session-{++sessionCounter}";
    }
}
