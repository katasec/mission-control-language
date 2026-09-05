using Bunit;
using ForgeMission.ClientRuntime.Presentation;
using ForgeMission.ClientRuntime.Presentation.Components;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Presentation;

public sealed class WorkbenchPresentationTests : BunitContext
{
    [Fact]
    public void MissionsView_OnlyRendersSummaryFacts_NotExpertText()
    {
        var page = new ProjectRunPage(Guid.NewGuid(), 9, 9, false,
            [new ProjectRunSummary(Guid.NewGuid(), Guid.NewGuid(), "Janus", "Build an API", 1, 9,
                ConversationRunStatus.Completed, 3, 0, DateTimeOffset.UtcNow)], null);

        var view = Render<MissionsView>(p => p.Add(x => x.Page, page));

        Assert.Contains("Build an API", view.Markup);
        Assert.Contains("3 expert turns · 0 tool calls", view.Markup);
        Assert.DoesNotContain("Proposer answer", view.Markup);
    }

    [Fact]
    public void Trace_OmitsTheOriginalUserPrompt_AndRendersExpertEvidence()
    {
        var runId = Guid.NewGuid();
        var detail = new ProjectRunDetail(new ProjectRunSummary(runId, Guid.NewGuid(), "Naive", "Question", 4, 7,
            ConversationRunStatus.Completed, 1, 0, DateTimeOffset.UtcNow), "secret original prompt", 7, 7);
        var events = new ProjectRunEventPage(Guid.NewGuid(), runId, 7, 7,
            [Event(4, ConversationEventKind.UserMessage, "secret original prompt"), Event(5, ConversationEventKind.ParticipantMessage, "Expert evidence")], false);

        var trace = Render<RunTraceView>(p => p.Add(x => x.Run, detail).Add(x => x.Events, events).Add(x => x.AfterSequence, 3).Add(x => x.ThroughSequence, 7));

        Assert.Contains("Expert evidence", trace.Markup);
        Assert.DoesNotContain("secret original prompt", trace.Markup);
        Assert.Contains("Events 4–5", trace.Markup);
    }

    [Fact]
    public void Composer_DisablesRun_WhenNoCanonicalSelectionExists()
    {
        var composer = Render<MissionComposer>(p => p
            .Add(x => x.Missions, new ProjectMissionsView(["Janus", "Naive"], null, false))
            .Add(x => x.HistoryAvailable, true));

        Assert.Contains("Mission: none selected", composer.Markup);
        Assert.True(composer.Find(".mc-run").HasAttribute("disabled"));
        Assert.False(composer.Find(".mc-input").HasAttribute("disabled"));
    }

    private static ConversationEvent Event(long sequence, ConversationEventKind kind, string? text) =>
        new(Guid.NewGuid(), 1, Guid.NewGuid(), null, sequence, kind, ConversationParticipant.Proposer, 1, text, null, null, null, null, null, null, DateTimeOffset.UtcNow);
}



