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

    // --- 43.20 task 2 corrections: readiness gate and idempotent retry ------------------------

    // The session id alone is not readiness: EnterProjectAsync sets it before awaiting the open,
    // so gating on it would leave a window in which a turn could be submitted against a
    // conversation that does not exist yet.
    [Fact]
    public async Task BeforeMissionControlOpens_TheComposerIsDisabled_AndNoTurnCanBeSent()
    {
        channel.HoldNextMissionControlOpen();
        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".composer-input")));
        Assert.True(page.Find(".composer-input").HasAttribute("disabled"));
        Assert.True(page.Find(".composer-send").HasAttribute("disabled"));
        Assert.Contains("Opening Mission Control", page.Find(".composer-input").GetAttribute("placeholder")!);

        await InputAsync(page, ".composer-input", "too early");
        await ClickAsync(page, ".composer-send");
        Assert.Empty(channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>());

        channel.ReleaseHeldMissionControlOpen();

        page.WaitForAssertion(() => Assert.False(page.Find(".composer-input").HasAttribute("disabled")));
    }

    [Fact]
    public async Task AFailedMissionControlOpen_LeavesTheComposerDisabled_WithItsErrorVisible()
    {
        channel.FailNextMissionControlOpen(
            ProjectOperationErrorCode.ManifestWriteFailed, "Could not update forge.project.json.");

        var page = await OpenCreatedProjectAsync();

        page.WaitForAssertion(() => Assert.Contains(
            "Could not update forge.project.json.", page.Find(".error-banner").TextContent));
        // The error is shown AND the input stays blocked — one assertion pair, not two hopes.
        Assert.True(page.Find(".composer-input").HasAttribute("disabled"));
        Assert.True(page.Find(".composer-send").HasAttribute("disabled"));
    }

    [Fact]
    public async Task AFailedTurn_KeepsTheTypedText_AndRetryingSendsTheIdenticalRequest()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextControlTurn(
            ProjectOperationErrorCode.MissionControlConflict, "CommandId already used with different content.");

        await SendTurnAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".error-banner")));

        // The text is still on screen — it was the only copy of it that existed.
        Assert.Equal("narrow the scope", page.Find(".composer-input").GetAttribute("value"));

        await ClickAsync(page, ".composer-send");
        page.WaitForAssertion(() =>
            Assert.Equal(2, channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().Count()));

        var turns = channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().ToList();
        Assert.Equal(turns[0].CommandId, turns[1].CommandId);
        Assert.Equal(turns[0].Text, turns[1].Text);
    }

    [Fact]
    public async Task EditingAfterAFailure_SendsANewCommandId()
    {
        var page = await OpenCreatedProjectAsync();
        channel.FailNextControlTurn(ProjectOperationErrorCode.MissionControlInvalid, "rejected");

        await SendTurnAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".error-banner")));

        await InputAsync(page, ".composer-input", "narrow the scope, but only the API");
        await ClickAsync(page, ".composer-send");
        page.WaitForAssertion(() =>
            Assert.Equal(2, channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().Count()));

        var turns = channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().ToList();
        Assert.NotEqual(turns[0].CommandId, turns[1].CommandId);
        Assert.Equal("narrow the scope, but only the API", turns[1].Text);
    }

    [Fact]
    public async Task AnAcceptedTurn_ClearsTheComposer_AndTheNextTurnUsesADifferentCommandId()
    {
        var page = await OpenCreatedProjectAsync();

        await SendTurnAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Equal("", page.Find(".composer-input").GetAttribute("value")));
        Assert.Empty(page.FindAll(".error-banner"));

        await SendTurnAsync(page, "and name the success criteria");

        var turns = channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().ToList();
        // Clearing is proved by the NEXT send minting a fresh id, not by inspecting private state.
        Assert.NotEqual(turns[0].CommandId, turns[1].CommandId);
    }

    [Fact]
    public async Task ATransportExceptionOnSubmit_BehavesLikeATypedFailure_ForRetryPurposes()
    {
        var page = await OpenCreatedProjectAsync();
        channel.ThrowOnNextControlTurn(new IOException("socket reset"));

        await SendTurnAsync(page, "narrow the scope");
        page.WaitForAssertion(() => Assert.Contains("socket reset", page.Find(".error-banner").TextContent));
        Assert.Equal("narrow the scope", page.Find(".composer-input").GetAttribute("value"));

        await ClickAsync(page, ".composer-send");
        page.WaitForAssertion(() =>
            Assert.Equal(2, channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().Count()));

        var turns = channel.Requests.OfType<SubmitProjectMissionControlTurnRequest>().ToList();
        Assert.Equal(turns[0].CommandId, turns[1].CommandId);
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

    // --- 43.20 task 3: the rail, the Explorer, and project-scoped navigation -------------------

    [Fact]
    public async Task AnOpenedProject_ShowsMissionControlAndTheThreeEntryRailInOrder()
    {
        var page = await OpenCreatedProjectAsync();

        Assert.Equal(
            ["Explorer", "Mission Control", "Settings"],
            page.FindAll(".wb-rail-label").Select(item => item.TextContent).ToArray());
        Assert.Equal("Mission Control", page.Find(".wb-title").TextContent);
        Assert.Single(page.FindAll(".composer-input"));
        // No workbench read happens until the Explorer is actually opened.
        Assert.Empty(channel.Requests.OfType<GetProjectWorkbenchRequest>());
    }

    [Fact]
    public async Task TheSelectedDestination_IsMarkedForAssistiveTechnologyAndVisually()
    {
        var page = await OpenCreatedProjectAsync();

        var current = Assert.Single(page.FindAll(".wb-rail-item[aria-current='page']"));
        Assert.Contains("Mission Control", current.TextContent, StringComparison.Ordinal);
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

        await SelectAsync(page, "Explorer");
        await SelectAsync(page, "Settings");
        await SelectAsync(page, "Mission Control");

        Assert.Equal(subscriptions, channel.SubscriptionsStarted);
        Assert.Equal(1, channel.ActiveSubscriptions);
        Assert.Equal(sessionId, channel.CurrentSessionId);
        Assert.Single(channel.Requests.OfType<ProjectCreateRequest>());
        Assert.Single(channel.Requests.OfType<OpenProjectMissionControlRequest>());
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

        foreach (var destination in new[] { "Explorer", "Settings", "Mission Control" })
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
        private Exception? nextTurnFault;
        private TaskCompletionSource<OpenProjectMissionControlResponse>? heldOpen;
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

        public void ThrowOnNextControlTurn(Exception fault) => nextTurnFault = fault;

        // Holds the OPEN response so a test can observe the window between "a session exists" and
        // "Mission Control is actually open" — the window the readiness gate closes.
        public void HoldNextMissionControlOpen() =>
            heldOpen = new TaskCompletionSource<OpenProjectMissionControlResponse>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        public void ReleaseHeldMissionControlOpen()
        {
            var held = heldOpen ?? throw new InvalidOperationException("No Mission Control open is being held.");
            heldOpen = null;
            held.SetResult(new OpenProjectMissionControlResponse(ControlConversationId));
        }

        private Task<OpenProjectMissionControlResponse> OpenMissionControlAsync()
        {
            if (heldOpen is { } held)
                return held.Task;

            return Task.FromResult(OpenMissionControl());
        }

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
            if (nextTurnFault is { } fault)
            {
                nextTurnFault = null;
                throw fault;
            }

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

        public async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        {
            Requests.Add(request!);
            object response = request switch
            {
                ProjectDraftRequest draft => DraftResponse(draft),
                ProjectCreateRequest create => nextProjectResponse ?? Created(create.Title ?? "Todos API", create.HomePath ?? DefaultHome),
                ProjectOpenRequest open => nextProjectResponse ?? Opened(open.HomePath),
                OpenProjectMissionControlRequest => await OpenMissionControlAsync().WaitAsync(ct),
                SubmitProjectMissionControlTurnRequest => SubmitControlTurn(),
                GetProjectWorkbenchRequest => WorkbenchResponse(),
                OpenProjectDocumentRequest open => DocumentResponse(open.EntryId),
                // PromptRequest and SessionSetupRequest are deliberately ABSENT. Mission Control is
                // the sole active conversation while a Project is open (43.20 task 2), so a page
                // that sent either would fail loudly here rather than quietly acquiring a second
                // surface behaviour. Both contracts live on in Client Runtime and its own tests.
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
