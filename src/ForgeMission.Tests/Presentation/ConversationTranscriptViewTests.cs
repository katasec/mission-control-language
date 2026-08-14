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
