using AngleSharp.Dom;
using Bunit;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Presentation;
using ForgeMission.Presentation.Components;
using Microsoft.AspNetCore.Components.Web;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 48. The surface's whole claim is that everything it shows came from Application: these
/// tests hold it to that. Nothing it renders may be seeded, and nothing this phase declined to add —
/// a paging control, a partial-history notice, a retry — may appear in any state.
/// </summary>
public sealed class MissionChatViewTests : BunitContext
{
    private static readonly Guid FirstChat = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SecondChat = Guid.Parse("22222222-2222-2222-2222-222222222222");

    // --- the fresh chat is frame 02 -------------------------------------------------------------

    [Fact]
    public void TheFreshChat_StatesItsPinnedVersionAndFixedAccess_WithNoControl()
    {
        var view = RenderView();

        Assert.Equal("New chat", view.Find(".mcp-chat-head h2").TextContent);
        Assert.Equal("APPROVED", view.Find(".mcp-chip-approved").TextContent);
        Assert.Equal("PINNED · JANUS 1.4", view.Find(".mcp-chip-pinned").TextContent);
        Assert.Equal("Project workspace access", view.Find(".mcp-access-profile").TextContent);
        Assert.Contains("It cannot be changed here.", view.Find(".mcp-access").TextContent);
        // The access block states a fact; it offers no way to change it and lists no capability.
        Assert.Empty(view.FindAll(".mcp-access button"));
        Assert.Empty(view.FindAll(".mcp-access input"));
        Assert.Empty(view.FindAll(".mcp-access li"));
    }

    [Fact]
    public void Nothing_IsSeeded_WhenApplicationReturnedNothing()
    {
        var view = Render<MissionChatView>(p => p.Add(x => x.ProjectTitle, "Amber Harbor"));

        Assert.Empty(view.FindAll(".mcp-row"));
        Assert.Empty(view.FindAll(".mcp-group-label"));
        Assert.Empty(view.FindAll(".mcp-participant-bubble"));
        Assert.Empty(view.FindAll(".mcp-chip-pinned"));
        var markup = view.Markup;
        foreach (var fabricated in new[] { "Atlas Beta", "Launch plan", "Migration rehearsal", "Docs sweep", "NAIVE" })
            Assert.DoesNotContain(fabricated, markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_AreGroupedBeneathTheVersionTheyPinned_FromSuppliedDataOnly()
    {
        var view = RenderView();

        Assert.Equal(["New chat", "Draft the launch plan"], RowTitles(view));
        Assert.Equal(["JANUS · 1.4 · APPROVED"], view.FindAll(".mcp-group-label").Select(node => node.TextContent));
        Assert.Equal("New chat", view.Find(".mcp-row-current .mcp-row-title").TextContent);
        Assert.Equal(["No messages yet", "Updated " + Updated.ToLocalTime().ToString("MMM d HH:mm")],
            view.FindAll(".mcp-row-meta").Select(node => node.TextContent));
    }

    [Fact]
    public void TheRail_StaysTheThreeShippedDestinations_WithMissionsCurrent()
    {
        var view = RenderView();

        Assert.Equal(["Project Explorer", "Missions", "Settings"],
            view.FindAll(".wb-rail-label").Select(node => node.TextContent));
        Assert.Equal("Missions", view.Find(".wb-rail-item-current .wb-rail-label").TextContent);
        Assert.Equal("Amber Harbor", view.Find(".wb-brand-name").TextContent);
    }

    // --- the active chat is frame 03 ------------------------------------------------------------

    [Fact]
    public void ASuppliedSeed_RendersInOrder_AndNamesEachExpertFromItsOwnEvent()
    {
        var transcript = new ConversationTranscript();
        foreach (var evt in Seed())
            transcript.Apply(evt);

        var view = RenderView(p => p
            .Add(x => x.Entries, transcript.Entries)
            .Add(x => x.SelectedConversationId, SecondChat));

        Assert.Equal("Draft the launch plan", view.Find(".mcp-chat-head h2").TextContent);
        Assert.Equal("Draft the launch plan for the Atlas beta.", view.Find(".mcp-user-bubble").TextContent);
        Assert.Equal(["Proposer", "Approver"], view.FindAll(".mcp-participant-label").Select(node => node.TextContent));
        Assert.Equal(["Two-week plan.", "The rollback path is reversible."],
            view.FindAll(".mcp-participant-lead").Select(node => node.TextContent));
    }

    [Fact]
    public void ALiveEventForTheOpenChat_AppendsOnce_EvenWhenItAlsoArrivedInTheSeed()
    {
        var transcript = new ConversationTranscript();
        var seed = Seed().ToArray();
        foreach (var evt in seed)
            transcript.Apply(evt);

        // The same event delivered again by the live stream, plus one genuinely new one.
        transcript.Apply(seed[^1]);
        transcript.Apply(Participant(5, "Proposer", "A revision."));

        var view = RenderView(p => p.Add(x => x.Entries, transcript.Entries));

        Assert.Equal(["Proposer", "Approver", "Proposer"], view.FindAll(".mcp-participant-label").Select(node => node.TextContent));
    }

    // --- what this phase did not add ------------------------------------------------------------

    [Fact]
    public void TheSurface_OffersOnlyTheControlsTheFramesDraw()
    {
        var view = RenderView(p => p.Add(x => x.Entries, EntriesFromSeed()));

        Assert.Equal(["Author a mission", "New chat", "Send"],
            view.FindAll("button").Where(node => !node.ClassList.Contains("wb-rail-item") && !node.ClassList.Contains("mcp-row"))
                .Select(node => node.TextContent.Trim()));
        Assert.Empty(view.FindAll("select"));
        Assert.Empty(view.FindAll("input"));
        Assert.Single(view.FindAll("textarea"));
    }

    [Theory]
    [InlineData(MissionChatFailureCode.HistoryUnavailable)]
    [InlineData(MissionChatFailureCode.HistoryProtocol)]
    [InlineData(MissionChatFailureCode.CreateUncertain)]
    public void ATypedFailure_IsStatedAsItself_AndAddsNoControlOrNotice(MissionChatFailureCode code)
    {
        var view = RenderView(p => p.Add(x => x.Failure, new MissionChatFailure(code, "Forge could not read that chat.")));

        Assert.Equal("Forge could not read that chat.", view.Find(".mcp-announce").TextContent);
        // No second place to say it, and nothing to press about it.
        Assert.Single(view.FindAll("[role=status]"));
        foreach (var absent in new[] { "Retry", "Load more", "older history", "Show more" })
            Assert.DoesNotContain(absent, view.Markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnavailableFixedAccess_IsReportedWhereTheProfileIs_WithNoRecoveryControl()
    {
        var view = RenderView(p => p.Add(x => x.Access,
            new MissionChatAccess(MissionChatAccessState.Unavailable, "Forge could not attach local access.")));

        Assert.Contains("Fixed access is not currently available", view.Find(".mcp-access-copy").TextContent);
        Assert.Empty(view.FindAll(".mcp-access button"));
    }

    // --- the actions this surface raises --------------------------------------------------------

    [Fact]
    public void SelectingARow_RaisesItsConversationId_AndReselectingTheOpenChatRaisesNothing()
    {
        var selected = new List<Guid>();
        var view = RenderView(p => p.Add(x => x.SelectChat, id => selected.Add(id)));

        SelectRow(view, "Draft the launch plan");
        SelectRow(view, "New chat");

        Assert.Equal([SecondChat], selected);
    }

    [Fact]
    public void NewChat_RaisesItsAction_AndSendRaisesOnlyWithANonEmptyMessage()
    {
        var created = 0;
        var sent = 0;
        var view = RenderView(p => p
            .Add(x => x.NewChat, () => created++)
            .Add(x => x.Send, () => sent++)
            .Add(x => x.Draft, "   "));

        Header(view, "New chat").Click();
        view.Find(".mcp-send").Click();
        view.Render(p => p.Add(x => x.Draft, "Ship it"));
        view.Find(".mcp-send").Click();

        Assert.Equal(1, created);
        Assert.Equal(1, sent);
    }

    [Fact]
    public void ABareEnter_Sends_AndShiftEnterDoesNot()
    {
        var sent = 0;
        var view = RenderView(p => p.Add(x => x.Send, () => sent++).Add(x => x.Draft, "Ship it\n"));

        view.Find(".mcp-composer-field").KeyUp(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });
        Assert.Equal(0, sent);

        view.Find(".mcp-composer-field").KeyUp(new KeyboardEventArgs { Key = "Enter" });
        Assert.Equal(1, sent);
    }

    [Fact]
    public void ADeferredRailDestination_SaysSoPolitely_AndNavigatesNowhere()
    {
        var view = RenderView();

        view.FindAll(".wb-rail-item").First(item => item.TextContent.Contains("Settings", StringComparison.Ordinal)).Click();

        Assert.Equal("Settings is not available from this chat.", view.Find(".mcp-announce").TextContent);
        Assert.Equal("Missions", view.Find(".wb-rail-item-current .wb-rail-label").TextContent);
    }

    [Fact]
    public void WhileBusy_TheActionsThatWouldStartWorkAreDisabled()
    {
        var view = RenderView(p => p.Add(x => x.Busy, true));

        Assert.True(view.Find(".mcp-send").HasAttribute("disabled"));
        Assert.True(Header(view, "New chat").HasAttribute("disabled"));
        Assert.All(view.FindAll(".mcp-row"), row => Assert.True(row.HasAttribute("disabled")));
    }

    [Fact]
    public void TheSurface_SelectsNoThemeOfItsOwn()
    {
        var view = RenderView();

        Assert.DoesNotContain("data-surface-theme", view.Markup, StringComparison.Ordinal);
    }

    // --- helpers ---------------------------------------------------------------------------------

    private static readonly DateTimeOffset Updated = new(2026, 9, 13, 9, 30, 0, TimeSpan.Zero);

    /// <summary>Renders with the one managed Project's real shape, then applies whatever a test is
    /// actually about. Overrides are a second set of parameters on the same component, so each test
    /// states only its own difference.</summary>
    private IRenderedComponent<MissionChatView> RenderView(
        Action<ComponentParameterCollectionBuilder<MissionChatView>>? parameters = null)
    {
        var view = Render<MissionChatView>(p => p
            .Add(x => x.ProjectTitle, "Amber Harbor")
            .Add(x => x.Rows, Rows)
            .Add(x => x.SelectedConversationId, FirstChat)
            .Add(x => x.Pin, new MissionChatPin("Janus", 1, "1.4", MissionHandsProfile.ProjectWorkspace, true))
            .Add(x => x.Access, new MissionChatAccess(MissionChatAccessState.Attached, null)));
        if (parameters is not null)
            view.Render(parameters);
        return view;
    }

    private static IReadOnlyList<MissionChatRow> Rows =>
    [
        new(FirstChat, "New chat", "Janus", 1, "1.4", Updated, false),
        new(SecondChat, "Draft the launch plan", "Janus", 1, "1.4", Updated, true),
    ];

    private static IReadOnlyList<ConversationEntry> EntriesFromSeed()
    {
        var transcript = new ConversationTranscript();
        foreach (var evt in Seed())
            transcript.Apply(evt);
        return transcript.Entries;
    }

    /// <summary>A durable stream as Host would relay it: one user message, then two experts reporting
    /// inside the same attempt under the same generic participant.</summary>
    private static IEnumerable<ConversationEvent> Seed()
    {
        yield return new ConversationEvent(Guid.NewGuid(), 1, SecondChat, null, 1, ConversationEventKind.UserMessage,
            ConversationParticipant.User, null, "Draft the launch plan for the Atlas beta.", null, null, null, null, null, null, Updated);
        yield return Participant(2, "Proposer", "Two-week plan.");
        yield return Participant(3, "Approver", "The rollback path is reversible.");
    }

    private static ConversationEvent Participant(long sequence, string actor, string text) =>
        new(Guid.NewGuid(), 1, SecondChat, null, sequence, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.Forge, 1, text, null, null, null, null, null, null, Updated, null, null, actor);

    private static IEnumerable<string> RowTitles(IRenderedComponent<MissionChatView> view) =>
        view.FindAll(".mcp-row-title").Select(node => node.TextContent);

    private static void SelectRow(IRenderedComponent<MissionChatView> view, string title) =>
        view.FindAll(".mcp-row").First(row => row.QuerySelector(".mcp-row-title")!.TextContent == title).Click();

    private static IElement Header(IRenderedComponent<MissionChatView> view, string label) =>
        view.FindAll(".mcp-header button").First(button => button.TextContent.Contains(label, StringComparison.Ordinal));
}
