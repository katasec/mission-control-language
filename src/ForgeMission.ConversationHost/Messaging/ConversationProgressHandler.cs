using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;
using Orleans;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>What happened to a progress message: durably applied to its conversation, rejected by
/// the addressable grain (e.g. an unknown/already-completed tool request), or discarded as
/// unaddressable poison input before any grain call was ever made. Never conflated — a discard and
/// a grain-level rejection are logged distinctly (Phase 43.16 Task 8c).</summary>
public enum ConversationProgressHandlingOutcome
{
    Applied,
    Rejected,
    Discarded,
}

/// <summary><paramref name="Reason"/> is the grain's rejection reason when <see cref="Outcome"/> is
/// <see cref="ConversationProgressHandlingOutcome.Rejected"/>, or the fixed
/// <see cref="ConversationProgressUnaddressableCategory"/> name when <see cref="Outcome"/> is
/// <see cref="ConversationProgressHandlingOutcome.Discarded"/>; null when Applied.</summary>
public sealed record ConversationProgressHandlingResult(ConversationProgressHandlingOutcome Outcome, string? Reason);

/// <summary>
/// The typed adapter between the Service Bus SDK and <see cref="IConversationGrain.RecordProgressAsync"/>.
/// Classifies every message first (<see cref="ConversationProgressMessageClassifier"/>): unaddressable
/// poison input (invalid JSON, missing tenant, envelope mismatch) is discarded with no grain call at
/// all — never a throw, never a retry. Only an addressable message reaches the grain, preserving
/// today's Applied/Rejected outcomes unchanged. A genuine failure *after* classification succeeds
/// (e.g. a transient Orleans/Table error) is not caught here — it still propagates to the caller for
/// the broker's own unsettled-retry-then-dead-letter path (Phase 43.16 Task 8c).
/// </summary>
public sealed class ConversationProgressHandler(IGrainFactory grainFactory)
{
    public async Task<ConversationProgressHandlingResult> HandleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        var classification = ConversationProgressMessageClassifier.Classify(message);
        if (classification is UnaddressableProgress unaddressable)
            return new ConversationProgressHandlingResult(ConversationProgressHandlingOutcome.Discarded, unaddressable.Category.ToString());

        var addressable = (AddressableProgress)classification;
        var address = new ConversationAddress(addressable.TenantId, addressable.Progress.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.ToString());
        var progressJson = JsonSerializer.Serialize(addressable.Progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var acceptance = await grain.RecordProgressAsync(new ConversationProgressInput(progressJson));

        return acceptance.Outcome switch
        {
            ConversationProgressOutcome.Appended or ConversationProgressOutcome.AlreadyRecorded
                => new ConversationProgressHandlingResult(ConversationProgressHandlingOutcome.Applied, null),
            ConversationProgressOutcome.Rejected
                => new ConversationProgressHandlingResult(ConversationProgressHandlingOutcome.Rejected, acceptance.RejectionReason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationProgressOutcome)} '{acceptance.Outcome}'."),
        };
    }
}
