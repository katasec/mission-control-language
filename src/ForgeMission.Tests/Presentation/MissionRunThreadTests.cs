using ForgeMission.ClientRuntime.Presentation;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Presentation;

/// <summary>
/// Phase 43.21 Task 2 (corrected) — the projection that keeps the human control thread and the
/// experts' own exchange apart.
///
/// The rule it exists to make structural: Missions shows what a person asked and how the run
/// ended, never a Proposer, Approver, Implementer, Naive, approval or tool turn. That is enforced
/// here by the SHAPE of <see cref="MissionRunEntry"/> — it has nowhere to put an expert's words —
/// rather than by a renderer choosing not to draw them.
/// </summary>
public class MissionRunThreadTests
{
    private static readonly Guid Run = Guid.NewGuid();
    private static readonly Guid Container = Guid.NewGuid();

    [Fact]
    public void AnAcceptedRun_EntersTheThreadAsQueued_WithTheSubmittedInstruction()
    {
        var thread = new MissionRunThread();

        thread.Accept(Run, "Janus", "Draft the first release plan.");

        var entry = Assert.Single(thread.Entries);
        Assert.Equal(Run, entry.RunId);
        Assert.Equal("Janus", entry.Mission);
        Assert.Equal("Draft the first release plan.", entry.Instruction);
        Assert.Equal(ConversationRunStatus.Queued, entry.Status);
        // Nothing to read yet, so nothing is offered to open.
        Assert.False(entry.HasTrace);
    }

    // The whole Janus shape, including a revision round, reduced to durable counts and a status.
    [Fact]
    public void AJanusRun_IsCountedNotRead()
    {
        var thread = Started("Janus");

        foreach (var evt in JanusRun())
            thread.Apply(evt);

        var entry = Assert.Single(thread.Entries);
        Assert.Equal(ConversationRunStatus.Completed, entry.Status);
        Assert.Equal(5, entry.ExpertTurns);
        Assert.Equal(0, entry.ToolCalls);
        Assert.True(entry.HasTrace);
    }

    [Fact]
    public void ANaiveRun_IsCountedTheSameWay()
    {
        var thread = Started("Naive");

        thread.Apply(Status(1, ConversationRunStatus.Queued));
        thread.Apply(Message(2, ConversationParticipant.Naive, "The importer is the release risk."));
        thread.Apply(Status(3, ConversationRunStatus.Completed));

        var entry = Assert.Single(thread.Entries);
        Assert.Equal(1, entry.ExpertTurns);
        Assert.Equal(ConversationRunStatus.Completed, entry.Status);
    }

    // The structural half of the rule: there is no member on a thread entry through which an
    // expert's message could reach the control surface at all.
    [Fact]
    public void AThreadEntry_HasNoFieldForAnExpertsWords()
    {
        Assert.Equal(
            ["RunId", "Mission", "Instruction", "Status", "ExpertTurns", "ToolCalls", "HasTrace"],
            typeof(MissionRunEntry).GetProperties().Select(property => property.Name));
    }

    // The prompt belongs to the control thread. Repeating it in the trace would present the
    // person's own words as though an expert had said them.
    [Fact]
    public void TheTrace_HoldsTheExpertExchange_AndNotThePrompt()
    {
        var thread = Started("Janus");

        thread.Apply(UserMessage(1, "Draft the first release plan."));
        foreach (var evt in JanusRun())
            thread.Apply(evt);

        var trace = thread.Trace(Run);
        Assert.NotNull(trace);
        Assert.DoesNotContain(trace!.Entries, entry => entry.Kind == ConversationEntryKind.UserMessage);
        Assert.Contains(trace.Entries, entry =>
            entry.Kind == ConversationEntryKind.ParticipantMessage && entry.Text == "Ship the importer first.");
    }

    // Exactness is the trace's whole purpose: a message reaches it as the durable event stored it.
    [Fact]
    public void ATraceMessage_IsTheDurableTextUnchanged()
    {
        const string exact = "  Ship the importer first; it unblocks everything else.  ";
        var thread = Started("Janus");

        thread.Apply(Message(1, ConversationParticipant.Proposer, exact));

        var entry = Assert.Single(thread.Trace(Run)!.Entries);
        Assert.Equal(exact, entry.Text);
    }

    // A reopened Project replays every run its container ever had. Those belong to runs this
    // session did not start, so they are dropped rather than rendered as if they were live.
    [Fact]
    public void EventsOfARunThisSessionDidNotStart_AreIgnoredEntirely()
    {
        var thread = Started("Janus");
        var foreign = Guid.NewGuid();

        thread.Apply(Message(1, ConversationParticipant.Proposer, "from an older run") with { RunId = foreign });
        thread.Apply(Status(2, ConversationRunStatus.Completed) with { RunId = foreign });

        var entry = Assert.Single(thread.Entries);
        Assert.Equal(ConversationRunStatus.Queued, entry.Status);
        Assert.Equal(0, entry.ExpertTurns);
        Assert.Null(thread.Trace(foreign));
    }

    [Fact]
    public void TwoRunsInOneSession_KeepSeparateThreadEntriesAndSeparateTraces()
    {
        var second = Guid.NewGuid();
        var thread = Started("Janus");
        thread.Accept(second, "Naive", "Summarise the risks.");

        thread.Apply(Message(1, ConversationParticipant.Proposer, "first run"));
        thread.Apply(Message(2, ConversationParticipant.Naive, "second run") with { RunId = second });

        Assert.Equal(2, thread.Entries.Count);
        Assert.Equal(["Draft the first release plan.", "Summarise the risks."],
            thread.Entries.Select(entry => entry.Instruction));
        Assert.Contains(thread.Trace(Run)!.Entries, entry => entry.Text == "first run");
        Assert.DoesNotContain(thread.Trace(Run)!.Entries, entry => entry.Text == "second run");
        Assert.Contains(thread.Trace(second)!.Entries, entry => entry.Text == "second run");
    }

    [Fact]
    public void AToolCall_IsCounted_SoAZeroCanBeShownHonestly()
    {
        var thread = Started("Janus");

        thread.Apply(new ConversationEvent(
            Guid.NewGuid(), 1, Container, Run, 1, ConversationEventKind.ToolRequested,
            ConversationParticipant.Implementer, null, null, null, null,
            new ConversationToolRequest(Guid.NewGuid(), "terminal",
                System.Text.Json.JsonDocument.Parse("{}").RootElement), null, null, null,
            DateTimeOffset.UtcNow));

        Assert.Equal(1, Assert.Single(thread.Entries).ToolCalls);
    }

    [Theory]
    [InlineData(ConversationRunStatus.Completed, true)]
    [InlineData(ConversationRunStatus.Failed, true)]
    [InlineData(ConversationRunStatus.Rejected, true)]
    [InlineData(ConversationRunStatus.Interrupted, true)]
    [InlineData(ConversationRunStatus.Queued, false)]
    [InlineData(ConversationRunStatus.Running, false)]
    [InlineData(ConversationRunStatus.WaitingForTool, false)]
    public void TerminalIsTheFullSetOfEndings(ConversationRunStatus status, bool terminal) =>
        Assert.Equal(terminal, MissionRunThread.IsTerminal(status));

    private static MissionRunThread Started(string mission)
    {
        var thread = new MissionRunThread();
        thread.Accept(Run, mission, "Draft the first release plan.");
        return thread;
    }

    private static IEnumerable<ConversationEvent> JanusRun() =>
    [
        Status(1, ConversationRunStatus.Queued),
        Message(2, ConversationParticipant.Proposer, "Ship the importer first."),
        Message(3, ConversationParticipant.Approver, "Add a rollback gate."),
        Message(4, ConversationParticipant.Proposer, "Revised with a rollback gate."),
        Message(5, ConversationParticipant.Approver, "Approved."),
        Message(6, ConversationParticipant.Implementer, "Starting."),
        Status(7, ConversationRunStatus.Completed),
    ];

    private static ConversationEvent Message(long sequence, ConversationParticipant participant, string text) =>
        new(Guid.NewGuid(), 1, Container, Run, sequence, ConversationEventKind.ParticipantMessage,
            participant, null, text, null, null, null, null, null, null, DateTimeOffset.UtcNow);

    private static ConversationEvent UserMessage(long sequence, string text) =>
        new(Guid.NewGuid(), 1, Container, Run, sequence, ConversationEventKind.UserMessage,
            ConversationParticipant.User, null, text, null, null, null, null, null, null, DateTimeOffset.UtcNow);

    private static ConversationEvent Status(long sequence, ConversationRunStatus status) =>
        new(Guid.NewGuid(), 1, Container, Run, sequence, ConversationEventKind.RunStatus,
            ConversationParticipant.Forge, null, null, null, null, null, null, null, status,
            DateTimeOffset.UtcNow);
}
