using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Janus;

/// <summary>A translated progress fact's content, still missing the identity/addressing fields
/// (<c>EventId</c>/<c>ConversationId</c>/<c>RunId</c>/<c>OccurredAtUtc</c>) that only the caller —
/// which owns the command's progress-ordinal allocator — can assign.</summary>
public sealed record MappedProgressFact(
    ConversationEventKind Kind,
    ConversationParticipant Participant,
    int? Attempt,
    string? Text,
    string? Reason,
    ConversationApproval? Approval,
    ConversationToolRequest? ToolRequest);

/// <summary>
/// Fixed translation from a <see cref="PipelineTraceEvent"/> (current Janus mission shape only) to
/// zero, one, or two <see cref="MappedProgressFact"/> values. A step start becomes
/// <see cref="ConversationEventKind.ParticipantStarted"/>. A step completion becomes
/// <see cref="ConversationEventKind.ParticipantMessage"/> for a non-judge pass,
/// <see cref="ConversationEventKind.Error"/> for a non-judge fail, or — for the Approver — BOTH a
/// <c>ParticipantMessage</c> (its own <c>Envelope.Text</c>, which the caller separately retains as
/// the approved plan on a pass) and an <see cref="ConversationEventKind.Approval"/> fact
/// (Approved on pass, RevisionRequested with the fail reason otherwise). A step delta never
/// persists. Unknown experts and zero/multiple tool calls fail visibly (throw) rather than guess.
/// </summary>
public static class JanusPipelineProgressMapper
{
    public static IReadOnlyList<MappedProgressFact> MapTraceEvent(
        PipelineTraceEvent traceEvent,
        IReadOnlyDictionary<string, ExpertDefinition> experts,
        Guid? toolRequestId = null)
    {
        switch (traceEvent)
        {
            case PipelineStepDelta:
                return [];

            case PipelineStepStarted started:
                return [new MappedProgressFact(
                    ConversationEventKind.ParticipantStarted, MapParticipant(started.ExpertName),
                    started.Attempt, null, null, null, null)];

            case PipelineStepCompleted completed:
                return MapStepCompleted(completed, experts);

            case PipelineToolRequested toolRequested:
                return [MapToolRequested(toolRequested, toolRequestId
                    ?? throw new InvalidOperationException(
                        "A PipelineToolRequested trace event requires a pre-assigned deterministic tool RequestId."))];

            default:
                throw new InvalidOperationException($"Unhandled trace event type '{traceEvent.GetType().Name}'.");
        }
    }

    private static IReadOnlyList<MappedProgressFact> MapStepCompleted(
        PipelineStepCompleted completed, IReadOnlyDictionary<string, ExpertDefinition> experts)
    {
        var participant = MapParticipant(completed.ExpertName);
        var expert = experts.TryGetValue(completed.ExpertName, out var e)
            ? e
            : throw new InvalidOperationException($"Unknown Janus expert '{completed.ExpertName}' — no ExpertDefinition.");

        var envelope = completed.Envelope;
        var passed = string.Equals(envelope.Status, "pass", StringComparison.OrdinalIgnoreCase);

        if (expert.IsJudge)
        {
            var message = new MappedProgressFact(
                ConversationEventKind.ParticipantMessage, participant, completed.Attempt, envelope.Text, null, null, null);

            var approval = passed
                ? new MappedProgressFact(
                    ConversationEventKind.Approval, participant, completed.Attempt, null, null,
                    new ConversationApproval(ConversationApprovalOutcome.Approved, null), null)
                : new MappedProgressFact(
                    ConversationEventKind.Approval, participant, completed.Attempt, null, null,
                    new ConversationApproval(ConversationApprovalOutcome.RevisionRequested, envelope.Reason ?? envelope.Text), null);

            return [message, approval];
        }

        if (!passed)
            return [new MappedProgressFact(
                ConversationEventKind.Error, participant, completed.Attempt, null, envelope.Reason ?? envelope.Text, null, null)];

        return [new MappedProgressFact(
            ConversationEventKind.ParticipantMessage, participant, completed.Attempt, envelope.Text, null, null, null)];
    }

    private static MappedProgressFact MapToolRequested(PipelineToolRequested toolRequested, Guid toolRequestId)
    {
        var calls = toolRequested.Calls;
        if (calls.Count != 1)
            throw new InvalidOperationException(
                $"Janus v1 supports exactly one tool call per request; got {calls.Count}.");

        var call = calls[0];
        return new MappedProgressFact(
            ConversationEventKind.ToolRequested, MapParticipant(toolRequested.ExpertName), toolRequested.Attempt,
            null, null, null, new ConversationToolRequest(toolRequestId, call.Name, call.Arguments));
    }

    private static ConversationParticipant MapParticipant(string expertName) => expertName switch
    {
        "Proposer" => ConversationParticipant.Proposer,
        "Approver" => ConversationParticipant.Approver,
        "Implementer" => ConversationParticipant.Implementer,
        _ => throw new InvalidOperationException($"Unknown Janus expert '{expertName}' — no ConversationParticipant mapping."),
    };
}
