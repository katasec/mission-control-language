using AngleSharp.Dom;
using Bunit;
using ForgeMission.Application.Transport;
using ForgeMission.Presentation.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 47.1. The prototype's whole claim is that it is local: every transition below changes
/// what is rendered and nothing else. These tests guard three things the references cannot — that
/// each frame keeps its own content, that the composer's two Enter behaviours stay distinct, and
/// that a destination this phase does not design says so out loud rather than sitting inert.
/// </summary>
public sealed class MissionChatPreviewTests : BunitContext
{
    // One channel for the whole context: bUnit fixes its service provider at the first render, and
    // one of these tests renders the component twice to prove a rebuild starts clean.
    private readonly RecordingChannel channel = new();

    public MissionChatPreviewTests() => Services.AddSingleton<IApplicationChannel>(channel);

    // --- the fresh chat is frame 02 -------------------------------------------------------------

    [Fact]
    public void TheFreshChat_NamesItsParticipants_AndRepeatsTheFixedAccessWithNoControl()
    {
        var preview = RenderPreview();

        Assert.Equal("New chat", preview.Find(".mcp-chat-head h2").TextContent);
        Assert.Equal("APPROVED", preview.Find(".mcp-chip-approved").TextContent);
        Assert.Contains("JANUS V1.4", preview.Find(".mcp-chip-pinned").TextContent);
        Assert.Equal(["You", "Proposer", "Approver"],
            preview.FindAll(".mcp-participant-name").Select(node => node.TextContent));
        Assert.Contains("It cannot be changed here.", preview.Find(".mcp-access").TextContent);
        // The access block states a fact; it offers no way to change it.
        Assert.Empty(preview.FindAll(".mcp-access button"));
        Assert.Empty(preview.FindAll(".mcp-access input"));
        Assert.False(preview.Find(".mcp-composer-field").HasAttribute("disabled"));
    }

    [Fact]
    public void TheFreshChat_ShowsItsOwnRowSet_WithTheNewChatRowCurrent()
    {
        var preview = RenderPreview();

        Assert.Equal(["New chat", "Launch plan", "Migration rehearsal", "Docs sweep"], RowTitles(preview));
        Assert.Equal("New chat", CurrentRowTitle(preview));
        Assert.Equal(["JANUS · V1.4 · APPROVED", "NAIVE · V2.1 · APPROVED"],
            preview.FindAll(".mcp-group-label").Select(node => node.TextContent));
    }

    [Fact]
    public void TheRail_StaysTheThreeShippedDestinations_WithMissionsCurrent()
    {
        var preview = RenderPreview();

        Assert.Equal(["Project Explorer", "Missions", "Settings"],
            preview.FindAll(".wb-rail-label").Select(node => node.TextContent));
        Assert.Equal("Missions", preview.Find("[aria-current=page] .wb-rail-label").TextContent);
        Assert.Equal("Atlas Beta", preview.Find(".wb-brand-name").TextContent);
    }

    // --- selecting Launch plan is frame 03 -----------------------------------------------------

    [Fact]
    public void SelectingLaunchPlan_RendersTheSharedTranscript_AndSwapsToItsOwnRowSet()
    {
        var preview = RenderPreview();

        SelectRow(preview, "Launch plan");

        Assert.Equal("Launch plan", preview.Find(".mcp-chat-head h2").TextContent);
        Assert.Equal(["Proposer", "Approver"],
            preview.FindAll(".mcp-participant-label").Select(node => node.TextContent));
        Assert.Contains("Two weeks, no new infrastructure.", preview.FindAll(".mcp-user-bubble")[0].TextContent);
        Assert.Contains("Approved", preview.Find(".mcp-approval").TextContent);
        Assert.Contains("atlas-beta-plan.md", preview.Find(".mcp-tool-row").TextContent);
        Assert.Contains("Proposer is revising the plan", preview.Find(".mcp-activity").TextContent);
        // The active frame draws a different set: no New chat row, and Risk review appears.
        Assert.Equal(["Launch plan", "Migration rehearsal", "Risk review", "Docs sweep"], RowTitles(preview));
        Assert.Equal("Launch plan", CurrentRowTitle(preview));
    }

    [Fact]
    public void ReselectingTheOpenChat_KeepsWhatWasAddedToIt()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");
        Send(preview, "Keep this message.");

        SelectRow(preview, "Launch plan");

        Assert.Contains("Keep this message.", preview.Markup);
    }

    [Fact]
    public void NewChat_ReturnsToTheFreshState_AndDiscardsTheLocalMessage()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");
        Send(preview, "This should not survive New chat.");

        Header(preview, "New chat").Click();

        Assert.Equal("New chat", preview.Find(".mcp-chat-head h2").TextContent);
        Assert.DoesNotContain("This should not survive New chat.", preview.Markup);
        Assert.Equal(["New chat", "Launch plan", "Migration rehearsal", "Docs sweep"], RowTitles(preview));
        Assert.Contains("Started a new local chat", Announcement(preview));
    }

    // --- the composer --------------------------------------------------------------------------

    [Fact]
    public void AMessage_RemovesTheSeededActivity_ThenAddsYouProposerAndApprover()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");

        Send(preview, "Give the soak another day.");

        // The stale "is revising" cue goes before the new rows land, so the column stays in order.
        Assert.Empty(preview.FindAll(".mcp-activity"));
        Assert.Equal("Give the soak another day.", preview.FindAll(".mcp-user-bubble").Last().TextContent);
        var replies = preview.FindAll(".mcp-participant-bubble").TakeLast(2).ToList();
        Assert.Contains("I have updated the proposed sequencing", replies[0].TextContent);
        Assert.Contains("The revised scenario keeps the rollback boundary", replies[1].TextContent);
        Assert.Equal(string.Empty, preview.Find(".mcp-composer-field").GetAttribute("value"));
        Assert.Contains("Proposer and Approver replied", Announcement(preview));
    }

    [Fact]
    public void ASecondMessage_AddsThreeMoreRows_WithNoActivityLeftToRemove()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");
        Send(preview, "First.");
        var afterFirst = preview.FindAll(".mcp-participant-bubble").Count;

        Send(preview, "Second.");

        Assert.Equal(afterFirst + 2, preview.FindAll(".mcp-participant-bubble").Count);
        Assert.Equal("Second.", preview.FindAll(".mcp-user-bubble").Last().TextContent);
        Assert.Empty(preview.FindAll(".mcp-activity"));
    }

    [Fact]
    public void AnEmptyOrWhitespaceMessage_ChangesNothing()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");
        var before = preview.FindAll(".mcp-transcript > *").Count;

        preview.Find(".mcp-send").Click();
        Send(preview, "   \n  ");

        Assert.Equal(before, preview.FindAll(".mcp-transcript > *").Count);
        Assert.Contains("Proposer is revising the plan", preview.Find(".mcp-activity").TextContent);
        Assert.False(preview.Find(".mcp-composer-field").HasAttribute("disabled"));
    }

    [Fact]
    public void ABareEnter_Sends_AndDropsTheNewlineItJustInserted()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");

        // Key-up runs after the character lands, so the field already holds the newline.
        preview.Find(".mcp-composer-field").Input("Move the invite to Thursday.\n");
        preview.Find(".mcp-composer-field").KeyUp(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal("Move the invite to Thursday.", preview.FindAll(".mcp-user-bubble").Last().TextContent);
    }

    [Fact]
    public void ShiftEnter_KeepsItsNewline_AndSendsNothing()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");
        var before = preview.FindAll(".mcp-user-bubble").Count;

        preview.Find(".mcp-composer-field").Input("First line\n");
        preview.Find(".mcp-composer-field").KeyUp(new KeyboardEventArgs { Key = "Enter", ShiftKey = true });

        Assert.Equal(before, preview.FindAll(".mcp-user-bubble").Count);
        Assert.Equal("First line\n", preview.Find(".mcp-composer-field").GetAttribute("value"));
    }

    // --- destinations this phase does not design ------------------------------------------------

    [Fact]
    public void ADeferredRailDestination_SaysSoPolitely_AndNavigatesNowhere()
    {
        var preview = RenderPreview();

        preview.FindAll(".wb-rail-item")
            .First(item => item.TextContent.Contains("Project Explorer", StringComparison.Ordinal))
            .Click();

        Assert.Equal("Project Explorer is not part of this preview.", Announcement(preview));
        Assert.Equal("polite", preview.Find(".mcp-announce").GetAttribute("aria-live"));
        Assert.Equal("status", preview.Find(".mcp-announce").GetAttribute("role"));
        // Nothing moved: Missions is still the rail's destination and the chat is unchanged.
        Assert.Equal("Missions", preview.Find("[aria-current=page] .wb-rail-label").TextContent);
        Assert.Equal("New chat", preview.Find(".mcp-chat-head h2").TextContent);
    }

    [Fact]
    public void ADeferredChatRow_SaysSoPolitely_AndLeavesTheTranscriptAlone()
    {
        var preview = RenderPreview();
        SelectRow(preview, "Launch plan");

        SelectRow(preview, "Risk review");

        Assert.Equal("Risk review is not part of this preview.", Announcement(preview));
        Assert.Equal("Launch plan", preview.Find(".mcp-chat-head h2").TextContent);
        Assert.Equal("Launch plan", CurrentRowTitle(preview));
        Assert.Contains("Proposer is revising the plan", preview.Find(".mcp-activity").TextContent);
    }

    // --- what the surface may not grow ----------------------------------------------------------

    [Fact]
    public void ThePreview_OffersNoControlBeyondTheOnesTheFramesDraw()
    {
        var preview = RenderPreview();

        // Three rail destinations, two header actions, four chat rows, Send. A retry, cancel,
        // approve, stop, drawer toggle or per-turn steering control would fail here first.
        Assert.Equal(10, preview.FindAll("button").Count);
        Assert.Empty(preview.FindAll("a"));
        Assert.Empty(preview.FindAll("input"));
        Assert.Single(preview.FindAll("textarea"));
    }

    [Fact]
    public void ThePreview_SelectsNoSurfaceThemeOfItsOwn()
    {
        var preview = RenderPreview();

        Assert.DoesNotContain("data-surface-theme", preview.Markup);
        Assert.DoesNotContain("forge-desktop-dark", preview.Markup);
    }

    [Fact]
    public void AFreshComponent_StartsOnTheFreshChat_WithNoEarlierStateAvailable()
    {
        var first = RenderPreview();
        SelectRow(first, "Launch plan");
        Send(first, "Typed into the previous instance.");

        var second = RenderPreview();

        Assert.Equal("New chat", second.Find(".mcp-chat-head h2").TextContent);
        Assert.DoesNotContain("Typed into the previous instance.", second.Markup);
        Assert.Empty(second.Find(".mcp-announce").TextContent);
    }

    [Fact]
    public void AuthorAMission_RaisesItsCallbackAndNothingElse()
    {
        var asked = 0;
        var preview = RenderPreview(p => p.Add(x => x.AuthorMission, () => asked++));

        Header(preview, "Author a mission").Click();

        Assert.Equal(1, asked);
    }

    // --- helpers ---------------------------------------------------------------------------------

    /// <summary>The channel is registered but never resolved: this component injects nothing, so a
    /// later edit that gave it a transport dependency would show up as a recorded request.</summary>
    private IRenderedComponent<MissionChatPreview> RenderPreview(
        Action<ComponentParameterCollectionBuilder<MissionChatPreview>>? parameters = null)
    {
        var preview = parameters is null ? Render<MissionChatPreview>() : Render(parameters);
        Assert.Empty(channel.Requests);
        return preview;
    }

    private static IEnumerable<string> RowTitles(IRenderedComponent<MissionChatPreview> preview) =>
        preview.FindAll(".mcp-row-title").Select(node => node.TextContent);

    private static string CurrentRowTitle(IRenderedComponent<MissionChatPreview> preview) =>
        preview.Find(".mcp-row-current .mcp-row-title").TextContent;

    private static string Announcement(IRenderedComponent<MissionChatPreview> preview) =>
        preview.Find(".mcp-announce").TextContent;

    private static void SelectRow(IRenderedComponent<MissionChatPreview> preview, string title) =>
        preview.FindAll(".mcp-row")
            .First(row => row.QuerySelector(".mcp-row-title")!.TextContent == title)
            .Click();

    private static IElement Header(IRenderedComponent<MissionChatPreview> preview, string label) =>
        preview.FindAll(".mcp-header button")
            .First(button => button.TextContent.Contains(label, StringComparison.Ordinal));

    private static void Send(IRenderedComponent<MissionChatPreview> preview, string message)
    {
        preview.Find(".mcp-composer-field").Input(message);
        preview.Find(".mcp-send").Click();
    }

    private sealed class RecordingChannel : IApplicationChannel
    {
        public List<object> Requests { get; } = [];
        public Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request, CancellationToken ct)
        { Requests.Add(request!); throw new InvalidOperationException("The prototype must reach the Application for nothing."); }
        public async IAsyncEnumerable<ApplicationEvent> Subscribe([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct) { await Task.Delay(Timeout.Infinite, ct); yield break; }
    }
}
