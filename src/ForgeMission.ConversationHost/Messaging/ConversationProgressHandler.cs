using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;
using Orleans;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>Whether the calling consumer should complete the source Service Bus message.
/// <see cref="RejectionReason"/> is set only when the grain itself rejected the fact — a
/// structurally invalid message (bad body, tenant/session/message-ID mismatch) instead throws, so
/// the consumer leaves it unsettled for the broker's own retry-then-dead-letter path.</summary>
public sealed record ConversationProgressHandlingResult(bool ShouldComplete, string? RejectionReason);

/// <summary>
/// The typed adapter between the Service Bus SDK and <see cref="IConversationGrain.RecordProgressAsync"/>.
/// Validates the message's trusted <c>tenant_id</c> application property and that its
/// <c>SessionId</c>/<c>MessageId</c> match the body's <c>ConversationId</c>/<c>EventId</c> before
/// ever calling a grain — a mismatch is corruption, not a retryable condition, so it throws rather
/// than being folded into <see cref="ConversationProgressOutcome.Rejected"/>.
/// </summary>
public sealed class ConversationProgressHandler(IGrainFactory grainFactory)
{
    public async Task<ConversationProgressHandlingResult> HandleAsync(ServiceBusReceivedMessage message, CancellationToken ct)
    {
        var progress = JsonSerializer.Deserialize(message.Body, ConversationContractsJsonContext.Default.ConversationProgress)
            ?? throw new InvalidOperationException($"Progress message '{message.MessageId}' body deserialized to null.");

        if (!message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue)
            || tenantValue is not string { Length: > 0 } tenantId)
            throw new InvalidOperationException(
                $"Progress message '{message.MessageId}' is missing a non-empty 'tenant_id' application property.");

        if (message.SessionId != progress.ConversationId.ToString("N"))
            throw new InvalidOperationException(
                $"Progress message '{message.MessageId}' SessionId '{message.SessionId}' does not match " +
                $"body ConversationId '{progress.ConversationId:N}'.");

        if (message.MessageId != progress.EventId.ToString("N"))
            throw new InvalidOperationException(
                $"Progress message '{message.MessageId}' does not match body EventId '{progress.EventId:N}'.");

        var address = new ConversationAddress(tenantId, progress.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.ToString());
        var progressJson = JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var acceptance = await grain.RecordProgressAsync(new ConversationProgressInput(progressJson));

        return acceptance.Outcome switch
        {
            ConversationProgressOutcome.Appended or ConversationProgressOutcome.AlreadyRecorded
                => new ConversationProgressHandlingResult(true, null),
            ConversationProgressOutcome.Rejected
                => new ConversationProgressHandlingResult(true, acceptance.RejectionReason),
            _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationProgressOutcome)} '{acceptance.Outcome}'."),
        };
    }
}
