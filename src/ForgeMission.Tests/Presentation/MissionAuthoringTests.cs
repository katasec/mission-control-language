using Bunit;
using ForgeMission.Application.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Presentation.Components;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 45.4 minimum. What matters here is that the surface reports Application's verdict rather
/// than forming one, and that none of the deferred conversation work leaks in as an affordance.
/// </summary>
public sealed class MissionAuthoringTests : BunitContext
{
    [Fact]
    public void ADraft_OffersPromotionAndSaysWhyItCannotBeEvaluatedYet()
    {
        var view = Render<MissionAuthoringView>(p => p.Add(x => x.Document,
            Document(MissionEditableKind.Draft, blocked: "Promote this draft to a candidate before evaluating it.")));

        Assert.Contains("DRAFT", view.Markup);
        Assert.Contains("Promote to candidate", view.Markup);
        Assert.Contains("Promote this draft to a candidate before evaluating it.", view.Markup);
        // Nothing to publish and nothing to evaluate yet, so neither is offered.
        Assert.Empty(view.FindAll(".ma-case-list"));
        Assert.DoesNotContain("Publish", view.Markup);
    }

    [Fact]
    public void PublishIsDisabledWithItsReason_NotHidden()
    {
        var view = Render<MissionAuthoringView>(p => p.Add(x => x.Document, Document(
            MissionEditableKind.Candidate, versionNumber: 1, canPublish: false,
            blocked: "1 of 1 cases did not match. Edit the case or the definition, then re-evaluate.",
            cases: [Case(EvaluationResultStateView.Failed, "produced a plan instead of declining")])));

        var publish = view.Find(".ma-action-accent");
        Assert.Equal("Publish v1", publish.TextContent.Trim());
        Assert.True(publish.HasAttribute("disabled"));
        Assert.Contains("did not match", view.Markup);
        // The failure is shown with what actually came back, not just a verdict.
        Assert.Contains("produced a plan instead of declining", view.Markup);
        Assert.Contains("FAIL", view.Markup);
    }

    [Fact]
    public void PublishIsEnabledOnlyWhenApplicationSaysSo()
    {
        var published = 0;
        var view = Render<MissionAuthoringView>(p => p
            .Add(x => x.Document, Document(MissionEditableKind.Candidate, versionNumber: 1, canPublish: true,
                cases: [Case(EvaluationResultStateView.Passed, "matched expected success")]))
            .Add(x => x.PublishVersion, () => published++));

        var publish = view.Find(".ma-action-accent");
        Assert.False(publish.HasAttribute("disabled"));
        publish.Click();
        Assert.Equal(1, published);
    }

    [Fact]
    public void ARunningCaseSaysSo_AndIsNeverShownAsAVerdict()
    {
        var view = Render<MissionAuthoringView>(p => p.Add(x => x.Document, Document(
            MissionEditableKind.Candidate, versionNumber: 1, canPublish: false,
            blocked: "An evaluation is still running.",
            cases: [Case(EvaluationResultStateView.Pending, null)])));

        Assert.Contains("RUNNING", view.Markup);
        Assert.DoesNotContain("PASS", view.Markup);
        Assert.True(view.Find(".ma-action-accent").HasAttribute("disabled"));
    }

    [Fact]
    public void EditingACaseWarnsThatItsPreviousResultIsCleared()
    {
        var view = Render<MissionAuthoringView>(p => p.Add(x => x.Document, Document(
            MissionEditableKind.Candidate, versionNumber: 1,
            cases: [Case(EvaluationResultStateView.Passed, "matched")])));

        view.FindAll(".ma-link").First(node => node.TextContent.Contains("Edit case")).Click();

        Assert.Contains("Saving a case clears its previous result", view.Markup);
        // The editor edits the question, never the answer.
        Assert.Empty(view.FindAll(".ce select[name*=result i]"));
        Assert.DoesNotContain("Mark as passed", view.Markup);
    }

    [Fact]
    public void TheSurfaceCarriesNoneOfTheDeferredConversationWork()
    {
        var view = Render<MissionAuthoringView>(p => p.Add(x => x.Document, Document(
            MissionEditableKind.Candidate, versionNumber: 1,
            cases: [Case(EvaluationResultStateView.Passed, "matched")])));

        foreach (var absent in new[] { "Send", "Retry turn", "Cancel turn", "transcript", "debate", "Proposer said", "View trace" })
            Assert.DoesNotContain(absent, view.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(view.FindAll(".ma-case a"));

        // The trace reference is evidence to read, not a link to somewhere that does not exist.
        Assert.Contains("trace_", view.Markup);
    }

    [Fact]
    public void TheExplorerListsAuthoredMissionsAndOffersOneWayIn()
    {
        Guid? opened = null;
        var view = Render<ProjectExplorerView>(p => p
            .Add(x => x.Projection, new ProjectWorkbenchProjection([], []))
            .Add(x => x.Missions, [new MissionDefinitionSummary(Guid.NewGuid(), "Janus", 1, MissionVersionStateView.Approved, false)])
            .Add(x => x.MissionSelected, id => opened = id));

        Assert.Contains("Janus", view.Markup);
        Assert.Contains("v1 approved", view.Markup);
        Assert.Contains("Author a mission", view.Markup);

        view.FindAll(".ex-open").First(node => node.TextContent.Contains("Janus")).Click();
        Assert.NotNull(opened);
    }

    private static MissionAuthoringDocument Document(
        MissionEditableKind editable, int? versionNumber = null, bool canPublish = false,
        string? blocked = null, EvaluationCaseView[]? cases = null) =>
        new(Guid.NewGuid(), "Janus", editable, MissionHandsProfile.NoHands,
            editable is MissionEditableKind.Draft ? Guid.NewGuid() : null,
            editable is MissionEditableKind.Candidate ? Guid.NewGuid() : null,
            1, versionNumber, "mission Janus(task) = {\n    Proposer\n    -> Reviewer\n}\n",
            cases ?? [], canPublish, blocked);

    private static EvaluationCaseView Case(EvaluationResultStateView state, string? observed) =>
        new(Guid.NewGuid(), "plan the cutover", string.Empty, string.Empty,
            EvaluationOutcomeView.Succeeded, ["approved"], state, observed,
            state is EvaluationResultStateView.None or EvaluationResultStateView.Pending ? null : "11c0e2abcdef");
}
