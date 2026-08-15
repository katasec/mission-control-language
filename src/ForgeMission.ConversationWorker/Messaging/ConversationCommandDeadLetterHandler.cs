using Azure.Messaging.ServiceBus;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary><paramref name="WasAddressable"/> false means the message classified as unaddressable
/// poison input — no publish call was made. True means both the Error and the RunStatus:Failed
/// fact were published via <see cref="IConversationProgressPublisher"/> (Phase 43.16 Task 8c).
/// </summary>
public sealed record ConversationCommandDeadLetterResult(
    bool WasAddressable,
    ConversationCommandUnaddressableCategory? DiscardCategory);

/// <summary>
/// SDK-independent handler for the <c>mission-command</c> queue's dead-letter sub-queue. A
/// dead-lettered command is delivery the main consumer could never settle. If it classifies as
/// addressable, this publishes a stable UUID-v5-derived <see cref="ConversationEventKind.Error"/>
/// fact followed by <see cref="ConversationRunStatus.Failed"/> — the Worker has no grain/Orleans
/// access, so both facts go through the same <see cref="IConversationProgressPublisher"/> the main
/// path already uses; the Host's own consumer durably records them. Unaddressable input publishes
/// nothing (Phase 43.16 Task 8c).
/// </summary>
public sealed class ConversationCommandDeadLetterHandler(IConversationProgressPublisher publisher)
{
    public async Task<ConversationCommandDeadLetterResult> HandleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        var classification = ConversationCommandMessageClassifier.Classify(message);
        if (classification is UnaddressableCommand unaddressable)
            return new ConversationCommandDeadLetterResult(false, unaddressable.Category);

        var addressable = (AddressableCommand)classification;

        var errorFact = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(addressable.Command.CommandId, "command-error"),
            addressable.Command.ConversationId, addressable.Command.RunId, ConversationEventKind.Error, ConversationParticipant.Forge,
            null, null, "Mission command delivery exhausted retries and was dead-lettered.", null, null, null, null, null,
            DateTimeOffset.UtcNow);
        await publisher.PublishAsync(errorFact, addressable.TenantId, ct);

        var failedFact = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(addressable.Command.CommandId, "command-failed"),
            addressable.Command.ConversationId, addressable.Command.RunId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            null, null, null, null, null, null, null, ConversationRunStatus.Failed, DateTimeOffset.UtcNow);
        await publisher.PublishAsync(failedFact, addressable.TenantId, ct);

        return new ConversationCommandDeadLetterResult(true, null);
    }
}
