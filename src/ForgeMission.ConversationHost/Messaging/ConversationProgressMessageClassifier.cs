using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>Why a message is unaddressable poison input — a fixed category, never an interpolated
/// identifier. Every log line derived from this must use only the category's name plus the broker
/// MessageId, never message body text, SessionId, tenant_id, or any other property value
/// (Phase 43.16 Task 8c).</summary>
public enum ConversationProgressUnaddressableCategory
{
    InvalidJson,
    NullBody,
    MissingTenantProperty,
    SessionIdMismatch,
    MessageIdMismatch,
}

/// <summary>Whether a Service Bus message body is a durable-addressable <see cref="ConversationProgress"/>
/// fact, or unaddressable poison input that can never become a grain call.</summary>
public abstract record ConversationProgressClassification
{
    public static ConversationProgressClassification Addressable(ConversationProgress progress, string tenantId) =>
        new AddressableProgress(progress, tenantId);

    public static ConversationProgressClassification Unaddressable(ConversationProgressUnaddressableCategory category) =>
        new UnaddressableProgress(category);
}

public sealed record AddressableProgress(ConversationProgress Progress, string TenantId) : ConversationProgressClassification;

public sealed record UnaddressableProgress(ConversationProgressUnaddressableCategory Category) : ConversationProgressClassification;

/// <summary>
/// The one shared classification seam for the <c>conversation-progress</c> queue — deserialize
/// then validate the envelope — used by both <see cref="ConversationProgressHandler"/> (main queue)
/// and <see cref="ConversationProgressDeadLetterHandler"/> (dead-letter sub-queue). Never touches
/// <c>message.Body</c> text in any classification result; a JSON failure's category names the
/// failure kind only, never the bytes (Phase 43.16 Task 8c).
/// </summary>
public static class ConversationProgressMessageClassifier
{
    public static ConversationProgressClassification Classify(ServiceBusReceivedMessage message)
    {
        ConversationProgress? progress;
        try
        {
            progress = JsonSerializer.Deserialize(message.Body, ConversationContractsJsonContext.Default.ConversationProgress);
        }
        catch (JsonException)
        {
            return ConversationProgressClassification.Unaddressable(ConversationProgressUnaddressableCategory.InvalidJson);
        }

        if (progress is null)
            return ConversationProgressClassification.Unaddressable(ConversationProgressUnaddressableCategory.NullBody);

        message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue);
        var validation = ConversationProgressEnvelopeValidator.Validate(
            progress, message.SessionId, message.MessageId, tenantValue as string);

        if (!validation.IsValid)
        {
            var category = validation.Failure switch
            {
                ConversationProgressEnvelopeFailure.MissingTenantProperty => ConversationProgressUnaddressableCategory.MissingTenantProperty,
                ConversationProgressEnvelopeFailure.SessionIdMismatch => ConversationProgressUnaddressableCategory.SessionIdMismatch,
                ConversationProgressEnvelopeFailure.MessageIdMismatch => ConversationProgressUnaddressableCategory.MessageIdMismatch,
                _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationProgressEnvelopeFailure)} '{validation.Failure}'."),
            };
            return ConversationProgressClassification.Unaddressable(category);
        }

        return ConversationProgressClassification.Addressable(progress, validation.TenantId!);
    }
}
