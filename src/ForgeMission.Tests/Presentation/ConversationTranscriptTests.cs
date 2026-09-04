using System.Text.Json;
using ForgeMission.ClientRuntime.Presentation;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Presentation;

public sealed class ConversationTranscriptTests
{
    private static readonly Guid ConversationId = Guid.NewGuid();
    private static readonly Guid RunId = Guid.NewGuid();

    [Fact]
    public void Apply_UserMessage_AddsAUserBubble()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.UserMessage, ConversationParticipant.User, text: "Build the thing."));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal(ConversationEntryKind.UserMessage, entry.Kind);
        Assert.Equal("Build the thing.", entry.Text);
    }

    [Fact]
    public void Apply_ParticipantStarted_ThenMessage_ReplacesTheTypingIndicatorInPlace()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.ParticipantStarted, ConversationParticipant.Proposer, attempt: 1));
        Assert.Equal(ConversationEntryKind.Typing, Assert.Single(transcript.Entries).Kind);

        transcript.Apply(NewEvent(2, ConversationEventKind.ParticipantMessage, ConversationParticipant.Proposer, attempt: 1, text: "Here's a plan."));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal(ConversationEntryKind.ParticipantMessage, entry.Kind);
        Assert.Equal("Here's a plan.", entry.Text);
    }

    [Fact]
    public void Apply_ContiguousSameParticipantAttemptMessages_MergeIntoOneBubble()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.ParticipantMessage, ConversationParticipant.Proposer, attempt: 1, text: "Part one. "));
        transcript.Apply(NewEvent(2, ConversationEventKind.ParticipantMessage, ConversationParticipant.Proposer, attempt: 1, text: "Part two."));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal("Part one. Part two.", entry.Text);
    }

    [Fact]
    public void Apply_MessageForADifferentAttempt_DoesNotMergeIntoTheFirstOne()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.ParticipantMessage, ConversationParticipant.Proposer, attempt: 1, text: "Attempt one."));
        transcript.Apply(NewEvent(2, ConversationEventKind.ParticipantMessage, ConversationParticipant.Proposer, attempt: 2, text: "Attempt two."));

        Assert.Equal(2, transcript.Entries.Count);
        Assert.Equal("Attempt one.", transcript.Entries[0].Text);
        Assert.Equal("Attempt two.", transcript.Entries[1].Text);
    }

    [Fact]
    public void Apply_Approval_Approved_AddsAnApprovedEntry()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.Approval, ConversationParticipant.Approver,
            approval: new ConversationApproval(ConversationApprovalOutcome.Approved, Feedback: null)));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal(ConversationEntryKind.Approval, entry.Kind);
        Assert.Equal(ConversationApprovalOutcome.Approved, entry.ApprovalOutcome);
    }

    [Fact]
    public void Apply_RunStatusRejected_MapsToNotApproved()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            runStatus: ConversationRunStatus.Rejected));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal(ConversationEntryKind.NotApproved, entry.Kind);
    }

    [Fact]
    public void Apply_RunStatusFailed_AfterRevisionRequested_MapsToNotApproved_WithThatFeedback()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.Approval, ConversationParticipant.Approver,
            approval: new ConversationApproval(ConversationApprovalOutcome.RevisionRequested, Feedback: "needs tests")));
        transcript.Apply(NewEvent(2, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            runStatus: ConversationRunStatus.Failed));

        var entry = transcript.Entries[^1];
        Assert.Equal(ConversationEntryKind.NotApproved, entry.Kind);
        Assert.Equal("needs tests", entry.Feedback);
    }

    [Fact]
    public void Apply_RunStatusFailed_WithoutARevisionRequested_RemainsItsOwnOperationalState()
    {
        var transcript = new ConversationTranscript();

        transcript.Apply(NewEvent(1, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            runStatus: ConversationRunStatus.Failed));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal(ConversationEntryKind.Status, entry.Kind);
        Assert.Equal(ConversationRunStatus.Failed, entry.RunStatus);
    }

    [Fact]
    public void Apply_ToolRequested_ThenMatchingToolResult_UpdatesTheSameRow()
    {
        var transcript = new ConversationTranscript();
        var requestId = Guid.NewGuid();

        transcript.Apply(NewEvent(1, ConversationEventKind.ToolRequested, ConversationParticipant.Implementer,
            toolRequest: new ConversationToolRequest(requestId, "Read", JsonDocument.Parse("{}").RootElement)));
        transcript.Apply(NewEvent(2, ConversationEventKind.ToolResult, ConversationParticipant.Implementer,
            toolResult: new ConversationToolResult(requestId, "file content", IsError: false)));

        var entry = Assert.Single(transcript.Entries);
        Assert.Equal(ConversationEntryKind.ToolCall, entry.Kind);
        Assert.True(entry.ToolCompleted);
        Assert.False(entry.ToolIsError);
    }

    [Fact]
    public void Apply_SameEventIdTwice_DoesNotProduceADuplicateEntry()
    {
        var transcript = new ConversationTranscript();
        var evt = NewEvent(1, ConversationEventKind.UserMessage, ConversationParticipant.User, text: "Build the thing.");

        transcript.Apply(evt);
        transcript.Apply(evt);

        Assert.Single(transcript.Entries);
    }

    [Fact]
    public void Apply_SameToolRequestedEventIdTwice_DoesNotProduceASecondToolRow()
    {
        var transcript = new ConversationTranscript();
        var requestId = Guid.NewGuid();
        var evt = NewEvent(1, ConversationEventKind.ToolRequested, ConversationParticipant.Implementer,
            toolRequest: new ConversationToolRequest(requestId, "Read", JsonDocument.Parse("{}").RootElement));

        transcript.Apply(evt);
        transcript.Apply(evt);

        Assert.Single(transcript.Entries);
    }

    private static ConversationEvent NewEvent(
        long sequence, ConversationEventKind kind, ConversationParticipant participant,
        int? attempt = null, string? text = null, string? reason = null,
        ConversationApproval? approval = null, ConversationToolRequest? toolRequest = null,
        ConversationToolResult? toolResult = null, ConversationRunStatus? runStatus = null) =>
        new(Guid.NewGuid(), 1, ConversationId, RunId, sequence, kind, participant, attempt, text, reason,
            approval, toolRequest, toolResult, Artifact: null, runStatus, DateTimeOffset.UtcNow);

    // 43.20 task 2: the zero-tool control mission publishes a ParticipantMessage with NO preceding
    // ParticipantStarted, so the transcript must render it as its own bubble rather than needing a
    // typing indicator to attach to.
    [Fact]
    public void ABareMissionControlMessage_BecomesOneParticipantBubble()
    {
        var transcript = new ConversationTranscript();
        var conversationId = Guid.NewGuid();

        transcript.Apply(new ConversationEvent(
            Guid.NewGuid(), 1, conversationId, null, 1, ConversationEventKind.UserMessage,
            ConversationParticipant.User, null, "refine", null, null, null, null, null, null, DateTimeOffset.UtcNow));
        transcript.Apply(new ConversationEvent(
            Guid.NewGuid(), 1, conversationId, null, 2, ConversationEventKind.ParticipantMessage,
            ConversationParticipant.MissionControl, null, "What would done look like?", null,
            null, null, null, null, null, DateTimeOffset.UtcNow));

        Assert.Equal(2, transcript.Entries.Count);
        Assert.Equal(ConversationEntryKind.UserMessage, transcript.Entries[0].Kind);
        Assert.Equal(ConversationEntryKind.ParticipantMessage, transcript.Entries[1].Kind);
        Assert.Equal(ConversationParticipant.MissionControl, transcript.Entries[1].Participant);
        Assert.Equal("What would done look like?", transcript.Entries[1].Text);
    }
}
