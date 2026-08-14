using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;
using Orleans;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary><paramref name="WasAddressable"/> false means the message classified as unaddressable
/// poison input — no grain call was made, <paramref name="DiscardCategory"/> names why, and both
/// fact outcomes are null. True means both the Error and the RunStatus:Failed fact were attempted
/// against the grain; their outcomes are reported independently — a rejected terminal fact is never
/// folded into a blanket "applied" (Phase 43.16 Task 8c).</summary>
public sealed record ConversationProgressDeadLetterResult(
    bool WasAddressable,
    ConversationProgressUnaddressableCategory? DiscardCategory,
    ConversationProgressHandlingOutcome? ErrorFactOutcome,
    string? ErrorFactRejectionReason,
    ConversationProgressHandlingOutcome? FailedFactOutcome,
    string? FailedFactRejectionReason);

/// <summary>
/// SDK-independent handler for the <c>conversation-progress</c> queue's dead-letter sub-queue — a
/// dead-lettered message is delivery the main <see cref="ConversationProgressHandler"/> could never
/// settle. If it classifies as addressable (Phase 43.16 Task 8c:
/// <see cref="ConversationProgressMessageClassifier"/>), this turns it into a stable
/// UUID-v5-derived <see cref="ConversationEventKind.Error"/> fact followed by
/// <see cref="ConversationRunStatus.Failed"/>, so the conversation's own log records the failure
/// rather than the run hanging forever. Unaddressable input is discarded with no grain call —
/// structurally identical to the main queue's handling of the same poison shapes, sharing the same
/// classifier.
/// </summary>
public sealed class ConversationProgressDeadLetterHandler(IGrainFactory grainFactory)
{
    public async Task<ConversationProgressDeadLetterResult> HandleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        var classification = ConversationProgressMessageClassifier.Classify(message);
        if (classification is UnaddressableProgress unaddressable)
            return new ConversationProgressDeadLetterResult(false, unaddressable.Category, null, null, null, null);

        var addressable = (AddressableProgress)classification;
        var address = new ConversationAddress(addressable.TenantId, addressable.Progress.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.ToString());

        var errorProgress = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(addressable.Progress.EventId, "progress-error"),
            addressable.Progress.ConversationId, addressable.Progress.RunId, ConversationEventKind.Error, ConversationParticipant.Forge,
            null, null, "Progress delivery exhausted retries and was dead-lettered.", null, null, null, null, null,
            DateTimeOffset.UtcNow);
        var errorAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(errorProgress, ConversationContractsJsonContext.Default.ConversationProgress)));
        var errorOutcome = errorAcceptance.Outcome == ConversationProgressOutcome.Rejected
            ? ConversationProgressHandlingOutcome.Rejected
            : ConversationProgressHandlingOutcome.Applied;

        var failedProgress = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(addressable.Progress.EventId, "progress-failed"),
            addressable.Progress.ConversationId, addressable.Progress.RunId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            null, null, null, null, null, null, null, ConversationRunStatus.Failed, DateTimeOffset.UtcNow);
        var failedAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(failedProgress, ConversationContractsJsonContext.Default.ConversationProgress)));
        var failedOutcome = failedAcceptance.Outcome == ConversationProgressOutcome.Rejected
            ? ConversationProgressHandlingOutcome.Rejected
            : ConversationProgressHandlingOutcome.Applied;

        return new ConversationProgressDeadLetterResult(
            true, null,
            errorOutcome, errorOutcome == ConversationProgressHandlingOutcome.Rejected ? errorAcceptance.RejectionReason : null,
            failedOutcome, failedOutcome == ConversationProgressHandlingOutcome.Rejected ? failedAcceptance.RejectionReason : null);
    }
}
