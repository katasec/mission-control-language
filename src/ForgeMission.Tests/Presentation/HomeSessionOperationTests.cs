using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bunit;
using ForgeMission.ClientRuntime.Presentation.Pages;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// ConversationHost's own internal start request shares these names; the surface must only ever
// reach the transport pair, so the tests name them unambiguously too.
using StartRunRequest = ForgeMission.ClientRuntime.Transport.StartProjectMissionRunRequest;
using StartRunResponse = ForgeMission.ClientRuntime.Transport.StartProjectMissionRunResponse;

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
    public async Task ACreatedProject_RendersItsTitle_AndStartsExactlyOneSubscription()
    {
        var page = await OpenCreatedProjectAsync();

        // The rail now carries the product identity, and the content region names the Project.
        // The Project's absolute home is deliberately absent from every open-Project view
        // (43.20 task 3) — it stays in the contract and is simply not rendered.
        Assert.Equal("AI Workbench", page.Find(".wb-brand-sub").TextContent);
        Assert.Equal("Todos API", page.Find(".wb-subtitle").TextContent);
        Assert.DoesNotContain(FakeClientRuntimeChannel.DefaultHome, page.Markup, StringComparison.Ordinal);
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

    // --- 43.21 task 2: the Missions surface is the only way to invoke a Project -----------------

    [Fact]
    public async Task OpeningAProject_ReadsItsMissions_WithNothingButItsSessionId()
    {
        var page = await OpenCreatedProjectAsync();

        var read = Assert.Single(channel.Requests.OfType<GetProjectMissionsRequest>());
        Assert.Equal(channel.CurrentSessionId, read.SessionId);
        // Everything else — the Project home, the manifest, the catalog — is resolved below
        // Presentation. The contract has no field to carry any of it.
        Assert.Single(typeof(GetProjectMissionsRequest).GetProperties());
        // A read, not an invocation: opening a Project starts nothing.
        Assert.Empty(channel.Requests.OfType<StartRunRequest>());
        _ = page;
    }

    [Fact]
    public async Task FirstOpen_ShowsMissionsWithJanusSelected_AndNoActivityYet()
    {
        var page = await OpenCreatedProjectAsync();

        Assert.Equal("Missions", page.Find(".wb-title").TextContent);
        Assert.Contains("Janus", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
        Assert.Contains("Run a mission", page.Find(".mv-empty").TextContent, StringComparison.Ordinal);
        // Nothing typed, so the action cannot succeed and does not pretend it could.
        Assert.True(page.Find(".composer-run").HasAttribute("disabled"));
        Assert.Equal("Run", page.Find(".composer-run").TextContent.Trim());
    }

    // The catalog is the Runtime's, and it is exactly two names — no Default row, and nothing
    // describing a model, provider, or expert.
    [Fact]
    public async Task ThePicker_ExposesExactlyJanusAndNaive_AndNothingElse()
    {
        var page = await OpenCreatedProjectAsync();

        await ClickAsync(page, ".mp-button");

        var options = page.FindAll(".mp-option").Select(option => option.TextContent.Trim()).ToArray();
        Assert.Equal(2, options.Length);
        Assert.StartsWith("Janus", options[0], StringComparison.Ordinal);
        Assert.Equal("Naive", options[1]);
        Assert.DoesNotContain("Default", page.Find(".mp-list").TextContent, StringComparison.Ordinal);
        Assert.Equal("Mission", page.Find(".mp-button").GetAttribute("aria-label"));
        Assert.Equal("true", page.Find(".mp-button").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task PickingNaive_PersistsIt_AndRendersTheCanonicalValue()
    {
        var page = await OpenCreatedProjectAsync();

        await SelectMissionAsync(page, "Naive");

        var select = Assert.Single(channel.Requests.OfType<SelectProjectMissionRequest>());
        Assert.Equal("Naive", select.Mission);
        Assert.Equal(channel.CurrentSessionId, select.SessionId);
        Assert.Contains("Naive", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".mp-list")); // the popup closed
    }

    // A picker showing a mission the Project did not store would be a lie about what the next run
    // executes, so a failure keeps the previous value on screen rather than the attempted one.
    [Fact]
    public async Task AFailedSelection_KeepsThePreviousSelection_AndShowsTheError()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextSelection(ProjectOperationErrorCode.UnknownMission, "'Naive' is not a mission this Project can run.");

        await SelectMissionAsync(page, "Naive");

        page.WaitForAssertion(() => Assert.Contains(
            "is not a mission this Project can run", page.Find(".composer-error").TextContent, StringComparison.Ordinal));
        Assert.Contains("Janus", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReopeningAProject_RendersThePersistedSelection()
    {
        channel.PersistedMission = "Naive";

        var page = await OpenCreatedProjectAsync();

        Assert.Contains("Naive", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
        // Reopening restores a selection, never a transcript: there is no history surface here.
        Assert.Single(page.FindAll(".mv-empty"));
        Assert.Empty(page.FindAll(".convo-participant-bubble"));
    }

    // A manifest naming something Forge cannot run: the error is shown, nothing is invented, and
    // the picker stays usable so choosing a real mission repairs the Project.
    [Fact]
    public async Task ACorruptSelection_ShowsTheError_InventsNothing_AndIsRepairable()
    {
        channel.CorruptSelection = true;

        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Contains(
            "is not a mission Forge can run", page.Find(".composer-error").TextContent, StringComparison.Ordinal));
        Assert.Contains("none selected", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Janus", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
        // No mission means no run — but the repair must stay reachable.
        Assert.True(page.Find(".composer-run").HasAttribute("disabled"));
        Assert.False(page.Find(".mp-button").HasAttribute("disabled"));

        await SelectMissionAsync(page, "Janus");

        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".composer-error")));
        Assert.Contains("Janus", page.Find(".mp-value").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARun_SendsExactlyOneStartRequest_AndNamesNoMission()
    {
        var page = await OpenCreatedProjectAsync();

        await RunAsync(page, "Draft the first release plan.");

        var run = Assert.Single(channel.Requests.OfType<StartRunRequest>());
        Assert.Equal(channel.CurrentSessionId, run.SessionId);
        Assert.Equal("Draft the first release plan.", run.Input);
        Assert.NotEqual(Guid.Empty, run.CommandId);
        // The mission is read from the Project below Presentation. There is no field here through
        // which this page could choose one, and that is what makes the rule structural.
        Assert.Equal(["SessionId", "CommandId", "Input"],
            typeof(StartRunRequest).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task ABlankInstruction_SendsNothing_AndLeavesTheActionDisabled()
    {
        var page = await OpenCreatedProjectAsync();

        await InputAsync(page, ".composer-input", "   ");
        await ClickAsync(page, ".composer-run");

        Assert.Empty(channel.Requests.OfType<StartRunRequest>());
        Assert.True(page.Find(".composer-run").HasAttribute("disabled"));
    }

    [Fact]
    public async Task ATypedInputFailure_IsRendered_AndCreatesNoActivity()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextRun(ProjectOperationErrorCode.InvalidMissionInput,
            "That instruction is too long (48,102 characters); the limit is 32,000.");

        await RunAsync(page, "…");

        page.WaitForAssertion(() => Assert.Contains(
            "the limit is 32,000", page.Find(".composer-error").TextContent, StringComparison.Ordinal));
        Assert.Single(page.FindAll(".mv-empty"));
    }

    [Fact]
    public async Task WhileARunIsLive_TheComposerAndPickerAreDisabled_AndASecondSubmitSendsNothing()
    {
        var page = await OpenCreatedProjectAsync();
        await RunAsync(page, "Draft the first release plan.");

        page.WaitForAssertion(() => Assert.True(page.Find(".composer-input").HasAttribute("disabled")));
        Assert.True(page.Find(".mp-button").HasAttribute("disabled"));
        Assert.Contains("Starting Janus", page.Find(".composer-run").TextContent, StringComparison.Ordinal);

        await ClickAsync(page, ".composer-run");
        Assert.Single(channel.Requests.OfType<StartRunRequest>());
    }

    [Fact]
    public async Task ARunAlreadyActiveAnswer_RendersItsTypedMessage_AndCreatesNoTranscript()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextRun(ProjectOperationErrorCode.RunAlreadyActive,
            "This Project already has a mission run in progress.");

        await RunAsync(page, "Draft the first release plan.");

        page.WaitForAssertion(() => Assert.Contains(
            "already has a mission run in progress", page.Find(".composer-error").TextContent, StringComparison.Ordinal));
        Assert.Single(page.FindAll(".mv-empty"));
        // The instruction survives the refusal.
        Assert.Equal("Draft the first release plan.", page.Find(".composer-input").GetAttribute("value"));
    }

    // The container orders every run the Project has ever had and the tail replays all of them, so
    // without the run filter a reopened Project would render its whole history as if it were live.
    [Fact]
    public async Task OnlyTheAcceptedRunsEventsRender()
    {
        var page = await OpenCreatedProjectAsync();
        await RunAsync(page, "Draft the first release plan.");

        channel.Publish(RunEvent(Guid.NewGuid(), 1, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, "from an older run"));
        channel.Publish(RunEvent(channel.RunId, 2, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, "from this run"));

        page.WaitForAssertion(() => Assert.Contains(
            "from this run", page.Find(".convo-participant-text").TextContent, StringComparison.Ordinal));
        Assert.DoesNotContain("from an older run", page.Markup, StringComparison.Ordinal);
    }

    // The regression this surface would otherwise ship: the durable tail starts inside the same
    // call that starts the run, so the run's own first events can arrive before its id does.
    [Fact]
    public async Task AnEventArrivingBeforeTheAcceptance_StillRenders()
    {
        var page = await OpenCreatedProjectAsync();
        channel.HoldNextRun();

        await InputAsync(page, ".composer-input", "Draft the first release plan.");
        await ClickAsync(page, ".composer-run");
        page.WaitForAssertion(() => Assert.Contains("Starting", page.Find(".composer-run").TextContent, StringComparison.Ordinal));

        // Published while the start request is still in flight.
        channel.Publish(RunEvent(channel.RunId, 1, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, "arrived early"));
        channel.ReleaseHeldRun();

        page.WaitForAssertion(() => Assert.Contains(
            "arrived early", page.Find(".convo-participant-text").TextContent, StringComparison.Ordinal));
    }

    // Terminal means the composer is usable again — and the answer is still readable. Clearing it
    // here would delete the result at the instant it arrived.
    [Fact]
    public async Task ATerminalRunStatus_ReturnsACleanComposer_AndKeepsTheAnswerOnScreen()
    {
        var page = await OpenCreatedProjectAsync();
        await RunAsync(page, "Summarise the release risks.");

        channel.Publish(RunEvent(channel.RunId, 1, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Naive, "The importer is the release risk."));
        channel.Publish(RunStatusEvent(channel.RunId, 2, ConversationRunStatus.Completed));

        page.WaitForAssertion(() => Assert.False(page.Find(".composer-input").HasAttribute("disabled")));
        Assert.False(page.Find(".mp-button").HasAttribute("disabled"));
        Assert.Equal("Run", page.Find(".composer-run").TextContent.Trim());
        Assert.Equal("", page.Find(".composer-input").GetAttribute("value"));
        Assert.Contains("The importer is the release risk.", page.Find(".convo-participant-text").TextContent, StringComparison.Ordinal);
        Assert.Equal("Naive", page.Find(".convo-participant-name").TextContent);
    }

    [Fact]
    public async Task AFailedRun_KeepsTheTypedText_AndRetryingSendsTheIdenticalRequest()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextRun(ProjectOperationErrorCode.MissionRunConflict, "CommandId already used with different content.");

        await RunAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".composer-error")));

        // The text is still on screen — it was the only copy of it that existed.
        Assert.Equal("narrow the scope", page.Find(".composer-input").GetAttribute("value"));

        await ClickAsync(page, ".composer-run");
        page.WaitForAssertion(() => Assert.Equal(2, channel.Requests.OfType<StartRunRequest>().Count()));

        var runs = channel.Requests.OfType<StartRunRequest>().ToList();
        Assert.Equal(runs[0].CommandId, runs[1].CommandId);
        Assert.Equal(runs[0].Input, runs[1].Input);
    }

    [Fact]
    public async Task EditingAfterAFailure_SendsANewCommandId()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextRun(ProjectOperationErrorCode.InvalidMissionInput, "rejected");

        await RunAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".composer-error")));

        await InputAsync(page, ".composer-input", "narrow the scope, but only the API");
        await ClickAsync(page, ".composer-run");
        page.WaitForAssertion(() => Assert.Equal(2, channel.Requests.OfType<StartRunRequest>().Count()));

        var runs = channel.Requests.OfType<StartRunRequest>().ToList();
        Assert.NotEqual(runs[0].CommandId, runs[1].CommandId);
        Assert.Equal("narrow the scope, but only the API", runs[1].Input);
    }

    [Fact]
    public async Task AnAcceptedRun_ClearsTheComposer_AndTheNextRunUsesADifferentCommandId()
    {
        var page = await OpenCreatedProjectAsync();

        await RunAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Equal("", page.Find(".composer-input").GetAttribute("value")));
        Assert.Empty(page.FindAll(".composer-error"));

        channel.Publish(RunStatusEvent(channel.RunId, 1, ConversationRunStatus.Completed));
        page.WaitForAssertion(() => Assert.False(page.Find(".composer-input").HasAttribute("disabled")));

        await RunAsync(page, "and name the success criteria");

        var runs = channel.Requests.OfType<StartRunRequest>().ToList();
        // Clearing is proved by the NEXT run minting a fresh id, not by inspecting private state.
        Assert.NotEqual(runs[0].CommandId, runs[1].CommandId);
    }

    [Fact]
    public async Task ATransportExceptionOnRun_BehavesLikeATypedFailure_ForRetryPurposes()
    {
        var page = await OpenCreatedProjectAsync();
        channel.ThrowOnNextRun(new IOException("socket reset"));

        await RunAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".composer-error")));
        Assert.Equal("narrow the scope", page.Find(".composer-input").GetAttribute("value"));

        await ClickAsync(page, ".composer-run");
        page.WaitForAssertion(() => Assert.Equal(2, channel.Requests.OfType<StartRunRequest>().Count()));

        var runs = channel.Requests.OfType<StartRunRequest>().ToList();
        Assert.Equal(runs[0].CommandId, runs[1].CommandId);
    }

    // A migrated Project states its retained history once, and offers nothing: no link, no button,
    // nothing to click that would reopen it as a current mission.
    // An unexpected fault says what did not happen, in a sentence. It never shows the exception's
    // own text: in the packaged AOT build a plain HTTP failure renders as
    // "net_http_message_not_success_statuscode_reason, 500, Internal Server Error" — observed in
    // the packaged Desktop, which is exactly where a person meets it.
    [Fact]
    public async Task AnUnexpectedFault_ReadsAsASentence_NeverAsTheFrameworksOwnText()
    {
        var page = await OpenCreatedProjectAsync();
        channel.ThrowOnNextRun(new IOException("net_http_message_not_success_statuscode_reason, 500, Internal Server Error"));

        await RunAsync(page, "Draft the first release plan.");

        page.WaitForAssertion(() => Assert.Equal(
            "Forge could not start this run. The runtime did not answer.",
            page.Find(".composer-error").TextContent.Trim()));
        Assert.DoesNotContain("net_http", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALegacyProject_StatesItsRetainedHistory_AndOffersNoAction()
    {
        channel.HasLegacyHistory = true;

        var page = await OpenCreatedProjectAsync();

        var notice = page.Find(".mv-legacy");
        Assert.Equal(
            "This Project has earlier legacy history. It is retained and is not shown here.",
            notice.TextContent.Trim());
        Assert.Empty(notice.QuerySelectorAll("a, button"));
    }

    [Fact]
    public async Task WithoutLegacyHistory_NoNoticeIsRendered()
    {
        var page = await OpenCreatedProjectAsync();

        Assert.Empty(page.FindAll(".mv-legacy"));
    }

    [Fact]
    public void WithNoProjectOpen_NoMissionRequestCanBeSent()
    {
        var page = Render<Home>();

        Assert.Empty(page.FindAll(".composer-input"));
        Assert.Empty(page.FindAll(".mp-button"));
        Assert.Empty(channel.Requests.OfType<GetProjectMissionsRequest>());
        Assert.Empty(channel.Requests.OfType<StartRunRequest>());
    }

    // Structural, not behavioural: the page cannot reach the Janus prompt path, the mission-switch
    // path, or the legacy control route, because it no longer references any of their contracts.
    // The fake channel below throws on all of them.
    [Fact]
    public async Task ThePage_NeverSendsAPromptSessionSetupOrLegacyControlRequest()
    {
        var page = await OpenCreatedProjectAsync();
        await RunAsync(page, "refine it");

        Assert.Empty(channel.Requests.OfType<PromptRequest>());
        Assert.Empty(channel.Requests.OfType<SessionSetupRequest>());
        Assert.Empty(channel.Requests.OfType<OpenProjectMissionControlRequest>());
        Assert.Empty(channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>());
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

    // The notice records a permanent gap in activity built only from relayed events, so nothing
    // short of a new view retracts it — traffic resuming is not the gap being closed.
    //
    // Its clearing half is deliberately NOT asserted here: BeginViewAsync still clears it, but no
    // action on this surface begins a second view while a Project is open. Switching Projects is
    // rail work; asserting it now would mean testing a path a person cannot reach.
    [Fact]
    public async Task GapNotice_PersistsAcrossLaterEvents()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        await ClickAsync(page, ".connection-retry");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".gap-notice")));

        // Traffic resuming is not the same as the gap being closed, so an arriving event must not
        // retract the notice. It belongs to no live run, so it also renders nothing — which is the
        // filter working, not the stream failing.
        channel.Publish(RunEvent(channel.RunId, 1, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Proposer, "still here"));
        page.WaitForState(() => true);
        Assert.DoesNotContain("still here", page.Markup, StringComparison.Ordinal);
        Assert.Single(page.FindAll(".gap-notice"));
    }

    // A run's activity is built only from relayed durable events, so starting one while the stream
    // is down would add a second unrecoverable gap.
    [Fact]
    public async Task WhileDisconnected_RunsAreBlocked()
    {
        channel.FaultNextSubscription(new IOException("stream reset"));
        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".connection-banner")));
        Assert.True(page.Find(".composer-input").HasAttribute("disabled"));
        Assert.True(page.Find(".composer-run").HasAttribute("disabled"));
        Assert.True(page.Find(".mp-button").HasAttribute("disabled"));
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

    // --- 43.20 task 3: the rail, the Explorer, and project-scoped navigation -------------------

    [Fact]
    public async Task AnOpenedProject_ShowsMissionsAndTheThreeEntryRailInOrder()
    {
        var page = await OpenCreatedProjectAsync();

        Assert.Equal(
            ["Project Explorer", "Missions", "Settings"],
            page.FindAll(".wb-rail-label").Select(item => item.TextContent).ToArray());
        Assert.Equal("Missions", page.Find(".wb-title").TextContent);
        Assert.Single(page.FindAll(".composer-input"));
        // No workbench read happens until the Explorer is actually opened.
        Assert.Empty(channel.Requests.OfType<GetProjectWorkbenchRequest>());
    }

    [Fact]
    public async Task TheSelectedDestination_IsMarkedForAssistiveTechnologyAndVisually()
    {
        var page = await OpenCreatedProjectAsync();

        var current = Assert.Single(page.FindAll(".wb-rail-item[aria-current='page']"));
        Assert.Contains("Missions", current.TextContent, StringComparison.Ordinal);
        Assert.Contains("wb-rail-item-current", current.ClassName!, StringComparison.Ordinal);
    }

    // The core navigation invariant: switching destinations is a view change and nothing else.
    // Measured on the channel rather than inferred from reading the page.
    [Fact]
    public async Task SwitchingDestinations_CreatesNoProjectSessionOrSubscription()
    {
        var page = await OpenCreatedProjectAsync();
        var subscriptions = channel.SubscriptionsStarted;
        var sessionId = channel.CurrentSessionId;

        await SelectAsync(page, "Project Explorer");
        await SelectAsync(page, "Settings");
        await SelectAsync(page, "Missions");

        Assert.Equal(subscriptions, channel.SubscriptionsStarted);
        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Equal(sessionId, channel.CurrentSessionId);
        Assert.Single(channel.Requests.OfType<ProjectCreateRequest>());
        // Navigating back to Missions re-reads nothing: the picker's state outlives every switch.
        Assert.Single(channel.Requests.OfType<GetProjectMissionsRequest>());
    }

    [Fact]
    public async Task AnEmptyExplorer_NamesEachSectionAndItsEmptyState()
    {
        var page = await OpenCreatedProjectAsync();

        await SelectAsync(page, "Explorer");

        Assert.Equal("Project Explorer", page.Find(".ex-title").TextContent);
        Assert.Equal(
            ["Project assets", "Source context", "Runs"],
            page.FindAll(".ex-heading").Select(heading => heading.TextContent).ToArray());
        Assert.Equal(
            ["No local assets yet", "No context attached", "No runs yet"],
            page.FindAll(".ex-empty").Select(empty => empty.TextContent).ToArray());
    }

    // Local assets are editable entries; a resolved OCI dependency is read-only evidence showing
    // its pinned reference and digest, labelled in words rather than by colour alone.
    [Fact]
    public async Task ThePinnedDependency_RendersItsSourceAndAReadOnlyLabel()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbench = FakeClientRuntimeChannel.PopulatedWorkbench;

        await SelectAsync(page, "Explorer");

        Assert.Equal("· OCI dependency · read-only", page.Find(".ex-badge").TextContent);
        Assert.Equal(FakeClientRuntimeChannel.OciDependency.Source, page.Find(".ex-source").TextContent);
        // The editable local asset carries no source line and no read-only label.
        Assert.Single(page.FindAll(".ex-badge"));
        Assert.Single(page.FindAll(".ex-source"));
    }

    // A run is listed as a fact, not offered as an affordance that would fail when clicked.
    [Fact]
    public async Task ARunIsListedWithoutBeingOpenable()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbench = FakeClientRuntimeChannel.PopulatedWorkbench;

        await SelectAsync(page, "Explorer");

        Assert.Contains(page.FindAll(".ex-name, .ex-dependency-name"),
            element => element.TextContent == "Draft the release plan");
        Assert.DoesNotContain(page.FindAll(".ex-open"),
            element => element.TextContent == "Draft the release plan");
    }

    [Fact]
    public async Task AFailedProjection_ShowsTheRuntimesMessageInsteadOfAPartialList()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbenchError = new ProjectOperationError(
            ProjectOperationErrorCode.InvalidDependency, "Expert 'Architect' has changed since it was locked.");

        await SelectAsync(page, "Explorer");

        Assert.Equal("Expert 'Architect' has changed since it was locked.", page.Find(".ex-error").TextContent);
        Assert.Empty(page.FindAll(".ex-heading"));
        Assert.Empty(page.FindAll(".ex-list"));
    }

    // The page hands back exactly the entry ID it was given. It builds none, and it has no path or
    // reference it could send instead.
    [Fact]
    public async Task OpeningAnEntry_SendsBackTheRuntimesOwnEntryId()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbench = FakeClientRuntimeChannel.PopulatedWorkbench;
        await SelectAsync(page, "Explorer");

        await OpenEntryAsync(page, "mission.mcl");

        var request = Assert.Single(channel.Requests.OfType<OpenProjectDocumentRequest>());
        Assert.Equal(FakeClientRuntimeChannel.MissionAsset.EntryId, request.EntryId);
        Assert.Equal("mission.mcl", page.Find(".wb-title").TextContent);
        Assert.Equal("mission Demo", page.Find(".doc-body").TextContent);
        // The composer belongs to Mission Control, so it is absent here rather than disabled.
        Assert.Empty(page.FindAll(".composer-input"));
    }

    // An open document still belongs to the Explorer, so the rail keeps saying where you are.
    [Fact]
    public async Task AnOpenDocument_KeepsTheExplorerMarkedAsTheCurrentDestination()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbench = FakeClientRuntimeChannel.PopulatedWorkbench;
        await SelectAsync(page, "Explorer");
        await OpenEntryAsync(page, "mission.mcl");

        Assert.Contains("Explorer",
            Assert.Single(page.FindAll(".wb-rail-item[aria-current='page']")).TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BackToExplorer_ReturnsToTheListWithoutReopeningTheProject()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbench = FakeClientRuntimeChannel.PopulatedWorkbench;
        await SelectAsync(page, "Explorer");
        await OpenEntryAsync(page, "mission.mcl");

        await page.InvokeAsync(() => page.Find(".doc-back").Click());

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".ex-title")));
        Assert.Single(channel.Requests.OfType<ProjectCreateRequest>());
        Assert.Equal(1, channel.ActiveSubscriptions);
    }

    [Fact]
    public async Task AFailedDocumentOpen_ReplacesTheBodyWithTheRuntimesMessage()
    {
        var page = await OpenCreatedProjectAsync();
        channel.NextWorkbench = FakeClientRuntimeChannel.PopulatedWorkbench;
        await SelectAsync(page, "Explorer");
        channel.NextDocumentError = new ProjectOperationError(
            ProjectOperationErrorCode.InvalidDocument, "mission.mcl is too large to open here.");

        await OpenEntryAsync(page, "mission.mcl");

        Assert.Equal("mission.mcl is too large to open here.", page.Find(".doc-error").TextContent);
        Assert.Empty(page.FindAll(".doc-body"));
    }

    [Fact]
    public async Task Settings_IsALabelledPlaceholderWithNoAction()
    {
        var page = await OpenCreatedProjectAsync();

        await SelectAsync(page, "Settings");

        Assert.Equal("Settings", page.Find(".set-title").TextContent);
        Assert.Equal("Project preferences will appear here in a later task.", page.Find(".set-note").TextContent);
        // Nothing on this view can be actioned: the rail's own three buttons are all that remain.
        Assert.Equal(3, page.FindAll("button").Count);
    }

    // The workbench exposes no local path. Task 1's launcher header is unaffected, and the
    // Project's home is still carried in the contract — it is simply never rendered.
    [Fact]
    public async Task AnOpenProject_NeverRendersItsHomePath()
    {
        var page = await OpenCreatedProjectAsync();

        foreach (var destination in new[] { "Project Explorer", "Settings", "Missions" })
        {
            await SelectAsync(page, destination);
            Assert.DoesNotContain(FakeClientRuntimeChannel.DefaultHome, page.Markup, StringComparison.Ordinal);
        }
    }

    private async Task SelectAsync(IRenderedComponent<Home> page, string label)
    {
        await page.InvokeAsync(() => page.FindAll(".wb-rail-item")
            .First(item => item.TextContent.Contains(label, StringComparison.Ordinal))
            .Click());
        page.WaitForState(() => true);
    }

    private async Task OpenEntryAsync(IRenderedComponent<Home> page, string displayName)
    {
        var before = channel.Requests.OfType<OpenProjectDocumentRequest>().Count();
        await page.InvokeAsync(() => page.FindAll(".ex-open")
            .First(item => item.TextContent.Contains(displayName, StringComparison.Ordinal))
            .Click());
        page.WaitForAssertion(() => Assert.True(
            channel.Requests.OfType<OpenProjectDocumentRequest>().Count() > before));
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

    // One durable fact of one run, exactly as the Client Runtime relays it. The run id is what the
    // surface filters on, so it is always explicit here — including when a test deliberately sends
    // a different one.
    private ClientRuntimeEvent RunEvent(
        Guid runId, long sequence, ConversationEventKind kind, ConversationParticipant participant, string text) =>
        new(ClientRuntimeEventKind.ConversationEvent, channel.CurrentSessionId,
            Conversation: new ConversationEvent(
                Guid.NewGuid(), 1, channel.ContainerId, runId, sequence, kind, participant,
                null, text, null, null, null, null, null, null, DateTimeOffset.UtcNow));

    private ClientRuntimeEvent RunStatusEvent(Guid runId, long sequence, ConversationRunStatus status) =>
        new(ClientRuntimeEventKind.ConversationEvent, channel.CurrentSessionId,
            Conversation: new ConversationEvent(
                Guid.NewGuid(), 1, channel.ContainerId, runId, sequence, ConversationEventKind.RunStatus,
                ConversationParticipant.Forge, null, null, null, null, null, null, null, status,
                DateTimeOffset.UtcNow));

    // The click itself only starts the replacement; its async handler completes later, so the
    // helper waits for the new subscription (or a surfaced failure) before returning.
    private async Task StartReplacementAsync(IRenderedComponent<Home> page, string selector, string? text = null)
    {
        var before = channel.SubscriptionsStarted;
        await ClickAsync(page, selector, text);
        page.WaitForAssertion(() => Assert.True(
            channel.SubscriptionsStarted > before || page.FindAll(".pl-error").Count > 0));
    }

    private async Task RunAsync(IRenderedComponent<Home> page, string instruction)
    {
        var before = channel.Requests.OfType<StartRunRequest>().Count();
        await InputAsync(page, ".composer-input", instruction);
        await ClickAsync(page, ".composer-run");
        page.WaitForAssertion(() => Assert.True(
            channel.Requests.OfType<StartRunRequest>().Count() > before));
    }

    // Opening the popup and choosing, exactly as a person does — never by invoking the callback.
    private async Task SelectMissionAsync(IRenderedComponent<Home> page, string mission)
    {
        await ClickAsync(page, ".mp-button");
        await page.InvokeAsync(() => page.FindAll(".mp-option")
            .First(option => option.TextContent.Contains(mission, StringComparison.Ordinal))
            .Click());
        page.WaitForState(() => true);
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
        private ProjectOperationError? nextMissionsError;
        private ProjectOperationError? nextSelectError;
        private ProjectOperationError? nextRunError;
        private Exception? nextRunFault;
        private TaskCompletionSource<StartRunResponse>? heldRun;
        private long acceptedSequence;
        private ProjectOperationResponse? nextProjectResponse;
        private ProjectOperationError? nextDraftError;
        private int sessionCounter;

        public int ActiveSubscriptions { get; private set; }
        public int PeakSubscriptions { get; private set; }
        public int SubscriptionsStarted { get; private set; }
        public string CurrentSessionId { get; private set; } = "session-0";

        public void FaultNextSubscription(Exception fault) => nextSubscriptionFault = fault;

        // --- 43.21 task 2: the picker, the selection, and one run --------------------------

        /// <summary>The Project's durable Mission container. It pins no mission and executes
        /// nothing; every event a test publishes belongs to a run inside it.</summary>
        public Guid ContainerId { get; } = Guid.NewGuid();

        /// <summary>Minted up front rather than at acceptance, so a test can publish an event for
        /// the run BEFORE the start request returns — the ordering the surface has to survive.</summary>
        public Guid RunId { get; } = Guid.NewGuid();

        /// <summary>What the manifest already holds when the Project opens.</summary>
        public string PersistedMission { get; set; } = "Janus";

        public bool HasLegacyHistory { get; set; }

        /// <summary>The manifest names a mission Forge cannot run. The read then returns the
        /// catalog AND an UnknownMission error, which is the only case that carries both.</summary>
        public bool CorruptSelection { get; set; }

        public void FailNextMissionsRead(ProjectOperationErrorCode code, string message) =>
            nextMissionsError = new ProjectOperationError(code, message);

        public void FailNextSelection(ProjectOperationErrorCode code, string message) =>
            nextSelectError = new ProjectOperationError(code, message);

        public void FailNextRun(ProjectOperationErrorCode code, string message) =>
            nextRunError = new ProjectOperationError(code, message);

        public void ThrowOnNextRun(Exception fault) => nextRunFault = fault;

        // Holds the START response so a test can observe the window between pressing Run and the
        // run's identity existing — the window the pre-acceptance buffer covers.
        public void HoldNextRun() =>
            heldRun = new TaskCompletionSource<StartRunResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseHeldRun()
        {
            var held = heldRun ?? throw new InvalidOperationException("No mission run is being held.");
            heldRun = null;
            held.SetResult(StartRun());
        }

        private GetProjectMissionsResponse ReadMissions()
        {
            if (nextMissionsError is { } error)
            {
                nextMissionsError = null;
                return new GetProjectMissionsResponse(null, error);
            }

            // The catalog and the legacy flag arrive whether or not the selection is readable —
            // that is what keeps the repair reachable.
            var view = new ProjectMissionsView(
                ["Janus", "Naive"], CorruptSelection ? null : PersistedMission, HasLegacyHistory);

            return CorruptSelection
                ? new GetProjectMissionsResponse(view, new ProjectOperationError(
                    ProjectOperationErrorCode.UnknownMission,
                    "This Project selects 'Sonnet', which is not a mission Forge can run."))
                : new GetProjectMissionsResponse(view);
        }

        private SelectProjectMissionResponse SelectMission(string mission)
        {
            if (nextSelectError is { } error)
            {
                nextSelectError = null;
                return new SelectProjectMissionResponse(null, error);
            }

            PersistedMission = mission;
            CorruptSelection = false;
            return new SelectProjectMissionResponse(mission);
        }

        private Task<StartRunResponse> StartRunAsync() =>
            heldRun is { } held ? held.Task : Task.FromResult(StartRun());

        private StartRunResponse StartRun()
        {
            if (nextRunFault is { } fault)
            {
                nextRunFault = null;
                throw fault;
            }

            if (nextRunError is { } error)
            {
                nextRunError = null;
                return new StartRunResponse(null, null, 0, ConversationRunStatus.Failed, error);
            }

            // The mission comes back with the acceptance, as the real contract does: what a person
            // is told started must be what actually started.
            return new StartRunResponse(RunId, PersistedMission, ++acceptedSequence, ConversationRunStatus.Queued);
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
                GetProjectMissionsRequest => ReadMissions(),
                SelectProjectMissionRequest select => SelectMission(select.Mission),
                StartRunRequest => await StartRunAsync().WaitAsync(ct),
                GetProjectWorkbenchRequest => WorkbenchResponse(),
                OpenProjectDocumentRequest open => DocumentResponse(open.EntryId),
                // PromptRequest, SessionSetupRequest and BOTH legacy control contracts are
                // deliberately ABSENT (43.21 task 2). A page that sent any of them fails loudly
                // here rather than quietly acquiring a second way to invoke a Project. All of them
                // live on in Client Runtime and its own tests until task 3 removes the legacy pair.
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

        // --- workbench (43.20 task 3) ----------------------------------------------------------
        // The projection and the document both come from the Client Runtime, so the page can only
        // render what it is given. What it does with an ENTRY ID is the interesting part: it hands
        // back exactly what it received, which is why the fake records the id it was asked for.

        public ProjectWorkbenchProjection? NextWorkbench { get; set; }
        public ProjectOperationError? NextWorkbenchError { get; set; }
        public ProjectOperationError? NextDocumentError { get; set; }
        public string? LastOpenedEntryId { get; private set; }

        public static ProjectExplorerEntry MissionAsset { get; } =
            new("asset:mission.mcl", "mission.mcl", ProjectExplorerEntryKind.Mission, false);

        public static ProjectExplorerEntry OciDependency { get; } =
            new("dep:Architect", "Architect", ProjectExplorerEntryKind.OciDependency, true,
                "oci://ghcr.io/katasec/forge-architect@sha256:" + new string('a', 64));

        public static ProjectExplorerEntry RunEntry { get; } =
            new("run:1", "Draft the release plan", ProjectExplorerEntryKind.Run, true);

        public static ProjectWorkbenchProjection EmptyWorkbench { get; } =
            new(new ProjectSummary(Guid.NewGuid(), "Todos API", "Todos API", DefaultHome), [], [], []);

        public static ProjectWorkbenchProjection PopulatedWorkbench { get; } =
            new(new ProjectSummary(Guid.NewGuid(), "Todos API", "Todos API", DefaultHome),
                [MissionAsset, OciDependency], [], [RunEntry]);

        private GetProjectWorkbenchResponse WorkbenchResponse()
        {
            if (NextWorkbenchError is { } error)
                return new GetProjectWorkbenchResponse(null, error);

            return new GetProjectWorkbenchResponse(NextWorkbench ?? EmptyWorkbench);
        }

        private OpenProjectDocumentResponse DocumentResponse(string entryId)
        {
            LastOpenedEntryId = entryId;
            if (NextDocumentError is { } error)
                return new OpenProjectDocumentResponse(null, error);

            return new OpenProjectDocumentResponse(
                new ProjectDocument(entryId["asset:".Length..], "text/plain", "mission Demo"));
        }

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
