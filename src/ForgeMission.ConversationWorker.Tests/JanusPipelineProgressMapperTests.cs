using System.Text.Json;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>Verification item 3: the mapper emits ordered Janus revision/approval facts with no
/// deltas and retains the successful Approver text as the approved plan (the plan-retention half is
/// the caller's job — proved here by asserting the Approval fact carries no separate plan payload,
/// so ParticipantMessage.Text is the only place the plan value could come from).</summary>
public class JanusPipelineProgressMapperTests
{
    private static readonly Dictionary<string, ExpertDefinition> Experts = new(StringComparer.Ordinal)
    {
        ["Proposer"] = new ExpertDefinition("Proposer", "in", "out", "prompt", Role: ""),
        ["Approver"] = new ExpertDefinition("Approver", "in", "out", "prompt", Role: "judge"),
        ["Implementer"] = new ExpertDefinition("Implementer", "in", "out", "prompt", Role: "agent"),
    };

    [Fact]
    public void StepDelta_NeverPersists()
    {
        var delta = new PipelineStepDelta("Negotiate", ["Janus", "Negotiate"], "Proposer", "llm", 1, "some text");
        var facts = JanusPipelineProgressMapper.MapTraceEvent(delta, Experts);
        Assert.Empty(facts);
    }

    [Fact]
    public void StepStarted_BecomesParticipantStarted()
    {
        var started = new PipelineStepStarted("Negotiate", ["Janus", "Negotiate"], "Proposer", "llm", 2);
        var facts = JanusPipelineProgressMapper.MapTraceEvent(started, Experts);

        var fact = Assert.Single(facts);
        Assert.Equal(ConversationEventKind.ParticipantStarted, fact.Kind);
        Assert.Equal(ConversationParticipant.Proposer, fact.Participant);
        Assert.Equal(2, fact.Attempt);
    }

    [Fact]
    public void NonJudgePass_BecomesSingleParticipantMessage()
    {
        var completed = new PipelineStepCompleted(
            "Negotiate", ["Janus", "Negotiate"], "Proposer", "llm", 1, new StepEnvelope("here is my proposal", "pass"));
        var facts = JanusPipelineProgressMapper.MapTraceEvent(completed, Experts);

        var fact = Assert.Single(facts);
        Assert.Equal(ConversationEventKind.ParticipantMessage, fact.Kind);
        Assert.Equal(ConversationParticipant.Proposer, fact.Participant);
        Assert.Equal("here is my proposal", fact.Text);
    }

    [Fact]
    public void NonJudgeFail_BecomesSingleError()
    {
        var completed = new PipelineStepCompleted(
            "Implement", ["Janus", "Implement"], "Implementer", "llm", 1, new StepEnvelope("bad", "fail", "it broke"));
        var facts = JanusPipelineProgressMapper.MapTraceEvent(completed, Experts);

        var fact = Assert.Single(facts);
        Assert.Equal(ConversationEventKind.Error, fact.Kind);
        Assert.Equal("it broke", fact.Reason);
    }

    [Fact]
    public void ApproverPass_BecomesOrderedParticipantMessageThenApproval_TextIsTheApprovedPlan()
    {
        var completed = new PipelineStepCompleted(
            "Negotiate", ["Janus", "Negotiate"], "Approver", "llm", 3, new StepEnvelope("the full approved plan verbatim", "pass"));
        var facts = JanusPipelineProgressMapper.MapTraceEvent(completed, Experts);

        Assert.Equal(2, facts.Count);
        Assert.Equal(ConversationEventKind.ParticipantMessage, facts[0].Kind);
        Assert.Equal("the full approved plan verbatim", facts[0].Text);
        Assert.Equal(ConversationEventKind.Approval, facts[1].Kind);
        Assert.Equal(ConversationApprovalOutcome.Approved, facts[1].Approval!.Outcome);
        Assert.Null(facts[1].Approval!.Feedback);
    }

    [Fact]
    public void ApproverFail_BecomesOrderedParticipantMessageThenRevisionRequested()
    {
        var completed = new PipelineStepCompleted(
            "Negotiate", ["Janus", "Negotiate"], "Approver", "llm", 1,
            new StepEnvelope("what's wrong", "fail", "not concrete enough"));
        var facts = JanusPipelineProgressMapper.MapTraceEvent(completed, Experts);

        Assert.Equal(2, facts.Count);
        Assert.Equal(ConversationEventKind.ParticipantMessage, facts[0].Kind);
        Assert.Equal("what's wrong", facts[0].Text);
        Assert.Equal(ConversationEventKind.Approval, facts[1].Kind);
        Assert.Equal(ConversationApprovalOutcome.RevisionRequested, facts[1].Approval!.Outcome);
        Assert.Equal("not concrete enough", facts[1].Approval!.Feedback);
    }

    [Fact]
    public void SingleToolCall_BecomesToolRequestedWithNameArgumentsAndProviderCallId()
    {
        var call = new PipelineToolCall("provider-call-1", "Read", JsonDocument.Parse("""{"file_path":"a.txt"}""").RootElement);
        var toolRequested = new PipelineToolRequested("Implement", ["Janus", "Implement"], "Implementer", "llm", 1, [call]);

        var facts = JanusPipelineProgressMapper.MapTraceEvent(toolRequested, Experts);

        var fact = Assert.Single(facts);
        Assert.Equal(ConversationEventKind.ToolRequested, fact.Kind);
        Assert.Equal("Read", fact.ToolName);
        Assert.Equal("provider-call-1", fact.ProviderCallId);
        Assert.Equal("a.txt", fact.ToolArguments!.Value.GetProperty("file_path").GetString());
        // The mapper never allocates the deterministic RequestId itself — that requires the
        // caller's current progress ordinal, which only its session state holds.
    }

    [Fact]
    public void MultipleToolCalls_FailsVisibly()
    {
        var arg = JsonDocument.Parse("{}").RootElement;
        var calls = new[] { new PipelineToolCall("a", "Read", arg), new PipelineToolCall("b", "Write", arg) };
        var toolRequested = new PipelineToolRequested("Implement", ["Janus", "Implement"], "Implementer", "llm", 1, calls);

        Assert.Throws<InvalidOperationException>(() => JanusPipelineProgressMapper.MapTraceEvent(toolRequested, Experts));
    }

    [Fact]
    public void UnknownExpert_FailsVisibly()
    {
        var started = new PipelineStepStarted("Negotiate", ["Janus", "Negotiate"], "SomeoneElse", "llm", 1);
        Assert.Throws<InvalidOperationException>(() => JanusPipelineProgressMapper.MapTraceEvent(started, Experts));
    }
}
