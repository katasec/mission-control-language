using Bunit;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Presentation.Components;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 45.3 Task 3A. The two things worth guarding here are what the surface promises and what
/// it cannot offer: rows must not look openable when opening does not exist yet, and nothing in
/// the creation panel may let an operator alter the access a version fixes.
/// </summary>
public sealed class MissionsLandingTests : BunitContext
{
    // --- the rail names the Project, and stays three destinations ------------------------------

    [Fact]
    public void TheRail_NamesTheOpenProject_AndOffersNoWayToChangeIt()
    {
        var rail = Render<WorkbenchRail>(p => p
            .Add(x => x.ProjectTitle, "Northstar")
            .Add(x => x.Selected, ForgeMission.Presentation.Components.WorkbenchView.Missions));

        Assert.Equal("Northstar", rail.Find(".wb-brand-name").TextContent);
        Assert.DoesNotContain("AI Workbench", rail.Markup);
        // The identity block is a label. Switching Project is not a rail affordance.
        Assert.Empty(rail.FindAll(".wb-brand button"));
        Assert.Empty(rail.FindAll(".wb-brand a"));
        // Still exactly the three destinations, Missions current.
        Assert.Equal(["Project Explorer", "Missions", "Settings"],
            rail.FindAll(".wb-rail-label").Select(node => node.TextContent));
        Assert.Equal("Missions", rail.Find("[aria-current=page] .wb-rail-label").TextContent);
    }

    // --- the list is informational ------------------------------------------------------------

    [Fact]
    public void Rows_AreText_WithNothingThatLooksOpenable()
    {
        var view = Render<MissionsLandingView>(p => p.Add(x => x.Conversations, [Row("Janus", 4)]));

        Assert.Equal("Janus v4", view.Find(".ml-row-name").TextContent);
        Assert.Contains("PINNED", view.Markup);
        // No control, no link, and nothing focusable: opening a conversation is a later task, and
        // an affordance for it now would be a promise this surface cannot keep.
        Assert.Empty(view.FindAll(".ml-rows button"));
        Assert.Empty(view.FindAll(".ml-rows a"));
        Assert.Empty(view.FindAll(".ml-rows [tabindex]"));
        Assert.Empty(view.FindAll(".ml-rows input"));
    }

    [Fact]
    public void AConversationWhoseVersionIsGone_SaysSo_AndInventsNoName()
    {
        var view = Render<MissionsLandingView>(p => p.Add(x => x.Conversations,
            [new MissionConversationListItem(Guid.NewGuid(), null, 9, DateTimeOffset.UtcNow)]));

        Assert.Contains("Mission version is no longer available locally", view.Markup);
        Assert.Contains("Version 9", view.Markup);
    }

    [Fact]
    public void EmptyAndUnavailable_AreDifferentAnswers()
    {
        var empty = Render<MissionsLandingView>(p => p.Add(x => x.Conversations, []));
        Assert.Contains("No mission conversations yet. Start one from an approved version.", empty.Markup);

        var failed = Render<MissionsLandingView>(p => p
            .Add(x => x.Conversations, [Row("Janus", 1)])
            .Add(x => x.Error, new ProjectOperationError(ProjectOperationErrorCode.HistoryUnavailable, "unreachable")));

        // An availability failure never shows a list, stale or otherwise, and always offers retry.
        Assert.DoesNotContain("Janus v1", failed.Markup);
        Assert.Contains("Retry", failed.Markup);
        Assert.Empty(failed.FindAll(".ml-rows"));
    }

    // --- the creation panel fixes access, it never offers to change it ------------------------

    [Fact]
    public void ThePanel_HasNoControlThatCouldAlterAccess()
    {
        var panel = Render<NewMissionConversationPanel>(p => p.Add(x => x.Options, [Option("Janus", 4, MissionHandsProfile.ProjectWorkspaceAndTerminal)]));
        panel.Find("input[type=radio]").Change(true);

        Assert.Contains("Project workspace and terminal access", panel.Markup);
        Assert.Contains("This exact profile is fixed for this conversation.", panel.Markup);
        Assert.Empty(panel.FindAll("input[type=checkbox]"));
        Assert.Empty(panel.FindAll("select"));
        Assert.Empty(panel.FindAll("textarea"));
        Assert.Empty(panel.FindAll("input[type=text]"));
        // The fixed-access summary is a statement, not a control.
        Assert.Empty(panel.FindAll(".np-access button"));
        Assert.Empty(panel.FindAll(".np-access input"));
    }

    [Fact]
    public void ThePanel_OffersOnlyWhatApplicationReturned_AsANamedRadiogroup()
    {
        var panel = Render<NewMissionConversationPanel>(p => p.Add(x => x.Options,
            [Option("Janus", 4, MissionHandsProfile.ProjectWorkspace), Option("Naive", 2, MissionHandsProfile.NoHands)]));

        var group = panel.Find("[role=radiogroup]");
        Assert.Equal("Approved versions", group.GetAttribute("aria-label"));
        Assert.Equal(2, panel.FindAll("input[type=radio]").Count);
        Assert.Equal(["Janus v4", "Naive v2"], panel.FindAll(".np-option-name").Select(node => node.TextContent));
        Assert.Contains("Approved · Project workspace access", panel.Markup);
        Assert.Contains("Approved · No local access", panel.Markup);
    }

    [Fact]
    public void Start_AppearsOnlyAfterAChoice_AndCarriesTheVersionItNames()
    {
        ApprovedMissionVersionOption? started = null;
        var option = Option("Janus", 4, MissionHandsProfile.ProjectWorkspace);
        var panel = Render<NewMissionConversationPanel>(p => p
            .Add(x => x.Options, [option])
            .Add(x => x.Start, chosen => started = chosen));

        Assert.Empty(panel.FindAll(".np-start"));

        panel.Find("input[type=radio]").Change(true);
        Assert.Equal("Start on Janus v4", panel.Find(".np-start").TextContent.Trim());

        panel.Find(".np-start").Click();
        Assert.Equal(option.MissionVersionId, started!.MissionVersionId);
    }

    [Fact]
    public void WhileStarting_EverythingThatCouldChangeTheOutcomeIsDisabled()
    {
        var panel = Render<NewMissionConversationPanel>(p => p.Add(x => x.Options, [Option("Janus", 4, MissionHandsProfile.NoHands)]));
        panel.Find("input[type=radio]").Change(true);
        panel.Render(p => p.Add(x => x.Busy, true));

        Assert.Equal("Starting conversation…", panel.Find(".np-start").TextContent.Trim());
        Assert.True(panel.Find(".np-start").HasAttribute("disabled"));
        Assert.True(panel.Find(".np-cancel").HasAttribute("disabled"));
        Assert.True(panel.Find("input[type=radio]").HasAttribute("disabled"));
    }

    [Fact]
    public void Cancel_ClosesWithoutStarting()
    {
        var cancelled = 0;
        var started = 0;
        var panel = Render<NewMissionConversationPanel>(p => p
            .Add(x => x.Options, [Option("Janus", 4, MissionHandsProfile.NoHands)])
            .Add(x => x.Start, _ => started++)
            .Add(x => x.Cancel, () => cancelled++));

        panel.Find("input[type=radio]").Change(true);
        panel.Find(".np-cancel").Click();

        Assert.Equal(1, cancelled);
        Assert.Equal(0, started);
    }

    [Fact]
    public void NoApprovedVersion_SaysSo_AndOffersNoWayToStartAnyway()
    {
        var panel = Render<NewMissionConversationPanel>(p => p.Add(x => x.Options, []));

        Assert.Contains("No approved mission version is available for this Project.", panel.Markup);
        Assert.Empty(panel.FindAll(".np-start"));
        Assert.Empty(panel.FindAll("input[type=radio]"));
    }

    [Fact]
    public void ARefreshedListThatDropsTheChosenVersion_ClearsTheChoice()
    {
        var chosen = Option("Janus", 4, MissionHandsProfile.ProjectWorkspaceAndTerminal);
        var panel = Render<NewMissionConversationPanel>(p => p.Add(x => x.Options, [chosen]));
        panel.Find("input[type=radio]").Change(true);
        Assert.Contains("Start on Janus v4", panel.Markup);

        // The version moved on. Start must not stay armed against a summary that no longer
        // describes what would be pinned.
        panel.Render(p => p.Add(x => x.Options, [Option("Janus", 5, MissionHandsProfile.NoHands)]));

        Assert.Empty(panel.FindAll(".np-start"));
        Assert.DoesNotContain("Project workspace and terminal access", panel.Markup);
    }

    [Fact]
    public void ATypedRefusal_IsShownWithoutClosingThePanel()
    {
        var panel = Render<NewMissionConversationPanel>(p => p
            .Add(x => x.Options, [Option("Janus", 5, MissionHandsProfile.NoHands)])
            .Add(x => x.Error, new ProjectOperationError(ProjectOperationErrorCode.VersionChanged,
                "This mission's approved version changed while you were reading it.")));

        Assert.Contains("approved version changed", panel.Markup);
        Assert.NotEmpty(panel.FindAll("[role=radiogroup]"));
    }

    private static MissionConversationListItem Row(string mission, int version) =>
        new(Guid.NewGuid(), mission, version, DateTimeOffset.UtcNow);

    private static ApprovedMissionVersionOption Option(string mission, int version, MissionHandsProfile profile) =>
        new(Guid.NewGuid(), mission, Guid.NewGuid(), version, "sha256:hash", profile);
}
