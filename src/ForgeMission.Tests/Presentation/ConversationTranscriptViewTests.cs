using Bunit;
using ForgeMission.ClientRuntime.Presentation;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.Tests.Presentation;

public sealed class ConversationTranscriptViewTests : BunitContext
{
    [Fact]
    public void Render_ApprovedEntry_ShowsApprovedText()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Approval, ConversationParticipant.Approver, ApprovalOutcome: ConversationApprovalOutcome.Approved),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Approved", component.Markup);
    }

    [Fact]
    public void Render_RevisionRequestedEntry_ShowsFeedback()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Approval, ConversationParticipant.Approver,
                ApprovalOutcome: ConversationApprovalOutcome.RevisionRequested, Feedback: "needs tests"),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Revision requested", component.Markup);
        Assert.Contains("needs tests", component.Markup);
    }

    [Fact]
    public void Render_NotApprovedEntry_ShowsNotApprovedText()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.NotApproved, ConversationParticipant.Forge, Feedback: "needs tests"),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Contains("Not approved", component.Markup);
    }

    [Fact]
    public void Render_RehydratedCompletedToolResult_ShowsExactlyOneToolRow()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Read", ToolCompleted: true, ToolIsError: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-tool-row"));
        Assert.Contains("completed", component.Markup);
        // Completed work is history, not activity (43.18).
        Assert.Empty(component.FindAll(".convo-activity"));
    }

    // --- 43.18 task 3: durable entries use the shared renderer ---------------------------------

    [Fact]
    public void Render_TypingEntry_UsesTheSharedThinkingActivity()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Typing, ConversationParticipant.Implementer, Attempt: 1),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-activity-thinking"));
        Assert.Contains("Implementer is thinking…", component.Find(".convo-activity-text").TextContent);
        Assert.Empty(component.FindAll(".convo-typing"));
    }

    [Fact]
    public void Render_UnfinishedToolCall_UsesTheSharedWorkingActivityWithTheRunningLabel()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Read", ToolCompleted: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-activity-working"));
        Assert.Contains("Implementer Read running…", component.Find(".convo-activity-text").TextContent);
        Assert.Empty(component.FindAll(".convo-tool-row"));
    }

    [Fact]
    public void Render_UnfinishedThenCompletedToolCalls_KeepsCompletedHistoryAndOneActivity()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Read", ToolCompleted: true),
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Edit", ToolCompleted: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Single(component.FindAll(".convo-tool-row"));
        Assert.Contains("Read completed", component.Find(".convo-tool-row").TextContent);
        Assert.Single(component.FindAll(".convo-activity-working"));
        Assert.Contains("Edit running…", component.Find(".convo-activity-text").TextContent);
    }

    [Fact]
    public void Render_DurableEntries_NeverClaimStreaming()
    {
        var entries = new List<ConversationEntry>
        {
            new(ConversationEntryKind.Typing, ConversationParticipant.Proposer, Attempt: 1),
            new(ConversationEntryKind.ToolCall, ConversationParticipant.Implementer,
                ToolRequestId: Guid.NewGuid(), ToolName: "Bash", ToolCompleted: false),
        };

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, entries));

        Assert.Empty(component.FindAll(".convo-activity-streaming"));
    }

    [Fact]
    public void Render_EntriesFromATwiceAppliedDuplicateEvent_NeverProducesADuplicateBubble()
    {
        var transcript = new ConversationTranscript();
        var evt = new ConversationEvent(Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), 1,
            ConversationEventKind.UserMessage, ConversationParticipant.User, null, "Build the thing.",
            null, null, null, null, null, null, DateTimeOffset.UtcNow);
        transcript.Apply(evt);
        transcript.Apply(evt); // duplicate delivery (live/replay overlap)

        var component = Render<ConversationTranscriptView>(parameters => parameters.Add(p => p.Entries, transcript.Entries));

        Assert.Single(component.FindAll(".convo-user-bubble"));
    }
}
