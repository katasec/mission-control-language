using System.Net.Http.Json;
using System.Text.Json;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// Phase 48. Host owns three new durable facts for a Mission Conversation: its display title, the
/// generic expert label it relays unchanged, and one finite bounded page of its own ordered events.
/// These tests hold each to its rule — the title is written once and never rewritten, the label is
/// stored exactly as the Worker sent it, and a page never widens, skips, or misreports its range.
/// </summary>
[Collection("Azurite")]
public class MissionChatProjectionTests(AzuriteFixture fixture)
{
    [Fact]
    public async Task TheTitle_IsNewChatBeforeTheFirstTurn_ThenTheFirstMessageNormalizedOnce()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateAsync(grain, address);

        Assert.Equal("New chat", (await SnapshotAsync(grain)).Title);

        await TurnAsync(grain, address, "  Draft   the launch\nplan for Atlas  ");
        Assert.Equal("Draft the launch plan for Atlas", (await SnapshotAsync(grain)).Title);
    }

    [Fact]
    public async Task TheTitle_IsBoundedAndCutOnAWordBoundary()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateAsync(grain, address);

        await TurnAsync(grain, address, string.Join(' ', Enumerable.Repeat("plan", 40)));

        var title = (await SnapshotAsync(grain)).Title!;
        Assert.True(title.Length <= 60, title);
        Assert.EndsWith("plan", title);
        Assert.DoesNotContain("  ", title);
    }

    [Fact]
    public async Task TheTitle_SurvivesReactivation_AndIsNeverRewrittenByALaterTurnOrARetry()
    {
        var address = NewAddress();
        Guid turnId;
        await using (var first = await fixture.StartHostAsync())
        {
            var grain = first.GetConversationGrain(address);
            await CreateAsync(grain, address);
            var accepted = await TurnAsync(grain, address, "First message");
            turnId = accepted.TurnId!.Value;
            await CompleteRunAsync(grain, address, accepted);
            Assert.Equal("First message", (await SnapshotAsync(grain)).Title);
        }

        await using var recovered = await fixture.StartHostAsync();
        var reactivated = recovered.GetConversationGrain(address);
        Assert.Equal("First message", (await SnapshotAsync(reactivated)).Title);

        var second = await TurnAsync(reactivated, address, "Second message");
        await CompleteRunAsync(reactivated, address, second);
        Assert.Equal("First message", (await SnapshotAsync(reactivated)).Title);

        var retry = await reactivated.AcceptMissionConversationTurnAsync(new MissionConversationTurnInput(Guid.NewGuid(), turnId, true, null));
        Assert.Equal(ConversationCommandOutcome.Accepted, retry.Outcome);
        Assert.Equal("First message", (await SnapshotAsync(reactivated)).Title);
    }

    [Fact]
    public async Task TheExpertLabel_ReachesTheCanonicalEventUnchanged_AndIsAbsentWhenTheWorkerSentNone()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateAsync(grain, address);
        var accepted = await TurnAsync(grain, address, "Draft it");

        await RecordAsync(grain, Progress(address, accepted, ConversationEventKind.ParticipantStarted, actorName: "Proposer"));
        await RecordAsync(grain, Progress(address, accepted, ConversationEventKind.ParticipantMessage, text: "A plan", actorName: "Approver"));
        await RecordAsync(grain, Progress(address, accepted, ConversationEventKind.ParticipantMessage, text: "No label"));

        var events = await EventsAsync(grain);
        Assert.Equal("Proposer", events.Single(evt => evt.Kind == ConversationEventKind.ParticipantStarted).ActorName);
        Assert.Equal("Approver", events.Single(evt => evt.Text == "A plan").ActorName);
        Assert.Null(events.Single(evt => evt.Text == "No label").ActorName);
        // Host stored a label; it changed no participant and derived nothing from it.
        Assert.All(events.Where(evt => evt.ActorName is not null),
            evt => Assert.Equal(ConversationParticipant.Forge, evt.Participant));
    }

    [Fact]
    public async Task TheBoundedRead_ReturnsExactlyItsRange_AndReportsWhatItActuallyReturned()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateAsync(grain, address);
        var accepted = await TurnAsync(grain, address, "Draft it");
        await RecordAsync(grain, Progress(address, accepted, ConversationEventKind.ParticipantMessage, text: "one", actorName: "Proposer"));
        var last = (await SnapshotAsync(grain)).LastSequence;

        var whole = await PageAsync(grain, 0, last);
        Assert.Equal(Enumerable.Range(1, (int)last).Select(value => (long)value), whole.Events.Select(evt => evt.Sequence));
        Assert.Equal(0, whole.AfterSequence);
        Assert.Equal(last, whole.RequestedThroughSequence);
        Assert.Equal(last, whole.ReturnedThroughSequence);
        Assert.False(whole.HasMore);

        // A narrower bound is the caller's, and Host never widens it — even though newer events exist.
        var narrowed = await PageAsync(grain, 1, 2);
        Assert.Equal([2], narrowed.Events.Select(evt => evt.Sequence));
        Assert.Equal(2, narrowed.RequestedThroughSequence);
        Assert.Equal(2, narrowed.ReturnedThroughSequence);
        Assert.False(narrowed.HasMore);

        // An empty range reports the caller's own lower bound back and claims nothing more.
        var empty = await PageAsync(grain, last, last);
        Assert.Empty(empty.Events);
        Assert.Equal(last, empty.ReturnedThroughSequence);
        Assert.False(empty.HasMore);
    }

    [Fact]
    public async Task TheBoundedRead_RefusesAnInvalidRange_AndAConversationOfAnotherPurpose()
    {
        await using var host = await fixture.StartHostAsync();
        var address = NewAddress();
        var grain = host.GetConversationGrain(address);
        await CreateAsync(grain, address);
        var last = (await SnapshotAsync(grain)).LastSequence;

        Assert.Equal("invalidRequest", (await grain.ReadMissionConversationEventsAsync(0, last + 1)).ErrorCode);
        Assert.Equal("invalidRequest", (await grain.ReadMissionConversationEventsAsync(2, 1)).ErrorCode);
        Assert.Equal("invalidRequest", (await grain.ReadMissionConversationEventsAsync(-1, 0)).ErrorCode);

        var other = host.GetConversationGrain(NewAddress());
        Assert.Equal("wrongPurpose", (await other.ReadMissionConversationEventsAsync(0, 0)).ErrorCode);
    }

    // --- the route the page is read through -----------------------------------------------------

    [Fact]
    public async Task TheEventsRoute_AnswersThePage_AndRefusesEveryRangeItCannotServe()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var project = Guid.NewGuid();
        var launch = Launch();

        var created = await client.PostAsJsonAsync("/mission-conversations",
            new CreateMissionConversationRequest(project, Guid.NewGuid(), launch),
            ConversationContractsJsonContext.Default.CreateMissionConversationRequest);
        Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);
        var conversation = (await created.Content.ReadFromJsonAsync(
            ConversationContractsJsonContext.Default.CreateMissionConversationResponse))!.ConversationId;

        var submitted = await client.PostAsJsonAsync($"/mission-conversations/{conversation}/turns",
            new SubmitMissionTurnRequest(conversation, Guid.NewGuid(), "Draft it"),
            ConversationContractsJsonContext.Default.SubmitMissionTurnRequest);
        Assert.Equal(System.Net.HttpStatusCode.Accepted, submitted.StatusCode);
        var last = (await client.GetFromJsonAsync($"/conversations/{conversation}",
            ConversationContractsJsonContext.Default.GetConversationResponse))!.Snapshot.LastSequence;

        var page = await client.GetFromJsonAsync($"/mission-conversations/{conversation}/events?after=0&through={last}",
            ConversationContractsJsonContext.Default.MissionConversationEventPage);
        Assert.Equal(conversation, page!.ConversationId);
        Assert.Equal(last, page.ReturnedThroughSequence);
        Assert.False(page.HasMore);
        Assert.Contains(page.Events, evt => evt.Kind == ConversationEventKind.UserMessage && evt.Text == "Draft it");

        // The bound is required, the range must be servable, and an unknown conversation is not a page.
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,
            (await client.GetAsync($"/mission-conversations/{conversation}/events?after=0")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,
            (await client.GetAsync($"/mission-conversations/{conversation}/events?after=0&through={last + 5}")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound,
            (await client.GetAsync($"/mission-conversations/{Guid.NewGuid()}/events?after=0&through=0")).StatusCode);
    }

    [Fact]
    public async Task TheDirectory_CarriesTheDurableTitle_OnCreateAndOnEveryListedRow()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };
        var project = Guid.NewGuid();

        var created = await client.PostAsJsonAsync("/mission-conversations",
            new CreateMissionConversationRequest(project, Guid.NewGuid(), Launch()),
            ConversationContractsJsonContext.Default.CreateMissionConversationRequest);
        var conversation = (await created.Content.ReadFromJsonAsync(
            ConversationContractsJsonContext.Default.CreateMissionConversationResponse))!.ConversationId;

        var before = await client.GetFromJsonAsync($"/mission-conversations/{project}",
            ConversationContractsJsonContext.Default.ListMissionConversationsResponse);
        Assert.Equal("New chat", Assert.Single(before!.Conversations).Title);

        await client.PostAsJsonAsync($"/mission-conversations/{conversation}/turns",
            new SubmitMissionTurnRequest(conversation, Guid.NewGuid(), "Draft the rollout"),
            ConversationContractsJsonContext.Default.SubmitMissionTurnRequest);

        var after = await client.GetFromJsonAsync($"/mission-conversations/{project}",
            ConversationContractsJsonContext.Default.ListMissionConversationsResponse);
        Assert.Equal("Draft the rollout", Assert.Single(after!.Conversations).Title);
    }

    [Fact]
    public async Task AnAcceptedTurn_AnswersWithTheTitleItJustAcquired()
    {
        await using var host = await fixture.StartHostAsync();
        using var client = new HttpClient { BaseAddress = host.BaseAddress };

        var created = await client.PostAsJsonAsync("/mission-conversations",
            new CreateMissionConversationRequest(Guid.NewGuid(), Guid.NewGuid(), Launch()),
            ConversationContractsJsonContext.Default.CreateMissionConversationRequest);
        var conversation = (await created.Content.ReadFromJsonAsync(
            ConversationContractsJsonContext.Default.CreateMissionConversationResponse))!.ConversationId;

        var submitted = await client.PostAsJsonAsync($"/mission-conversations/{conversation}/turns",
            new SubmitMissionTurnRequest(conversation, Guid.NewGuid(), "  Draft the   rollout "),
            ConversationContractsJsonContext.Default.SubmitMissionTurnRequest);

        var accepted = (await submitted.Content.ReadFromJsonAsync(
            ConversationContractsJsonContext.Default.SubmitMissionTurnResponse))!;
        // The caller learns the title from the same answer that accepted the turn, already normalized
        // by the one owner of that rule.
        Assert.Equal("Draft the rollout", accepted.Title);
    }

    // --- helpers ---------------------------------------------------------------------------------

    private static ConversationAddress NewAddress() => new("dev", Guid.NewGuid());

    private static async Task CreateAsync(IConversationGrain grain, ConversationAddress address)
    {
        var launch = Launch();
        var result = await grain.AcceptMissionConversationCreateAsync(new MissionConversationCreateInput(
            Guid.NewGuid(), Guid.NewGuid(), JsonSerializer.Serialize(launch, ConversationContractsJsonContext.Default.DurableMissionLaunch)));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
    }

    private static async Task<ConversationCommandAcceptance> TurnAsync(IConversationGrain grain, ConversationAddress address, string text)
    {
        var result = await grain.AcceptMissionConversationTurnAsync(new MissionConversationTurnInput(Guid.NewGuid(), null, false, text));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
        return result.Acceptance!;
    }

    /// <summary>A turn holds the conversation's one active run, so a later turn needs this one to have
    /// reached a terminal status first.</summary>
    private static Task CompleteRunAsync(IConversationGrain grain, ConversationAddress address, ConversationCommandAcceptance accepted) =>
        RecordAsync(grain, Progress(address, accepted, ConversationEventKind.RunStatus, runStatus: ConversationRunStatus.Completed));

    private static async Task RecordAsync(IConversationGrain grain, ConversationProgress progress)
    {
        var accepted = await grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress)));
        Assert.Equal(ConversationProgressOutcome.Appended, accepted.Outcome);
    }

    private static ConversationProgress Progress(ConversationAddress address, ConversationCommandAcceptance accepted,
        ConversationEventKind kind, string? text = null, string? actorName = null, ConversationRunStatus? runStatus = null) =>
        new(Guid.NewGuid(), address.ConversationId, accepted.RunId, kind, ConversationParticipant.Forge, 1, text, null, null, null, null,
            null, runStatus, DateTimeOffset.UtcNow, null, actorName);

    private static async Task<ConversationSnapshot> SnapshotAsync(IConversationGrain grain) =>
        JsonSerializer.Deserialize((await grain.GetSnapshotAsync()).SnapshotJson, ConversationContractsJsonContext.Default.ConversationSnapshot)!;

    private static async Task<List<ConversationEvent>> EventsAsync(IConversationGrain grain) =>
        [.. (await grain.ReadAfterAsync(0)).EventJson.Select(json =>
            JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!)];

    private static async Task<MissionConversationEventPage> PageAsync(IConversationGrain grain, long after, long through)
    {
        var result = await grain.ReadMissionConversationEventsAsync(after, through);
        Assert.Null(result.ErrorCode);
        return JsonSerializer.Deserialize(result.PayloadJson!, ConversationContractsJsonContext.Default.MissionConversationEventPage)!;
    }

    private static DurableMissionLaunch Launch()
    {
        const string definition = "mission Janus(task) = {\n    Proposer\n    -> Approver\n}\n";
        const string proposer = "---\nname: Proposer\nkind: llm\ninput: task\noutput: proposal\n---\n{{task}}";
        const string approver = "---\nname: Approver\nkind: llm\ninput: proposal\noutput: answer\n---\n{{proposal}}";
        var experts = new[]
        {
            new DurableResolvedExpert("Proposer", "experts", "experts/Proposer/expert.md", Hash(proposer), proposer),
            new DurableResolvedExpert("Approver", "experts", "experts/Approver/expert.md", Hash(approver), approver),
        };
        var input = new ForgeMission.Core.Runtime.DurableMissionPackageInput(
            ForgeMission.Core.Runtime.DurableMissionPackageValidator.CurrentFormatVersion, "", definition, "Janus", "task",
            [.. experts.Select(expert => new ForgeMission.Core.Runtime.DurableResolvedExpertInput(expert.Name, expert.LockSource,
                expert.LockPath, expert.LockHash, expert.ExpertMarkdown))]);
        input = input with { PackageHash = ForgeMission.Core.Runtime.DurableMissionPackageValidator.ComputeHash(input) };
        var package = new DurableMissionPackage(input.FormatVersion, input.PackageHash, definition, "Janus", "task", experts);
        return new DurableMissionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.ProjectWorkspace, package);
    }

    private static string Hash(string value) =>
        "sha256:" + Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
}
