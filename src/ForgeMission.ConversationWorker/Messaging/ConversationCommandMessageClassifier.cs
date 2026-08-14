using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>Why a message is unaddressable poison input — a fixed category, never an interpolated
/// identifier (Phase 43.16 Task 8c).</summary>
public enum ConversationCommandUnaddressableCategory
{
    InvalidJson,
    NullBody,
    MissingTenantProperty,
    SessionIdMismatch,
    MessageIdMismatch,
}

/// <summary>Whether a Service Bus message body is a durable-addressable <see cref="ConversationCommand"/>,
/// or unaddressable poison input that can never reach session state, the processor, or the
/// publisher.</summary>
public abstract record ConversationCommandClassification
{
    public static ConversationCommandClassification Addressable(ConversationCommand command, string tenantId) =>
        new AddressableCommand(command, tenantId);

    public static ConversationCommandClassification Unaddressable(ConversationCommandUnaddressableCategory category) =>
        new UnaddressableCommand(category);
}

public sealed record AddressableCommand(ConversationCommand Command, string TenantId) : ConversationCommandClassification;

public sealed record UnaddressableCommand(ConversationCommandUnaddressableCategory Category) : ConversationCommandClassification;

/// <summary>
/// The one shared classification seam for the <c>mission-command</c> queue — deserialize then
/// validate the envelope — used by both <see cref="AzureServiceBusMissionCommandConsumer"/>'s main
/// path and its command dead-letter path (<see cref="ConversationCommandDeadLetterHandler"/>).
/// Worker-local; never references Host's equivalent (Phase 43.16 Task 8c).
/// </summary>
public static class ConversationCommandMessageClassifier
{
    public static ConversationCommandClassification Classify(ServiceBusReceivedMessage message)
    {
        ConversationCommand? command;
        try
        {
            command = JsonSerializer.Deserialize(message.Body, ConversationContractsJsonContext.Default.ConversationCommand);
        }
        catch (JsonException)
        {
            return ConversationCommandClassification.Unaddressable(ConversationCommandUnaddressableCategory.InvalidJson);
        }

        if (command is null)
            return ConversationCommandClassification.Unaddressable(ConversationCommandUnaddressableCategory.NullBody);

        message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue);
        var validation = ConversationCommandEnvelopeValidator.Validate(
            command, message.SessionId, message.MessageId, tenantValue as string);

        if (!validation.IsValid)
        {
            var category = validation.Failure switch
            {
                ConversationCommandEnvelopeFailure.MissingTenantProperty => ConversationCommandUnaddressableCategory.MissingTenantProperty,
                ConversationCommandEnvelopeFailure.SessionIdMismatch => ConversationCommandUnaddressableCategory.SessionIdMismatch,
                ConversationCommandEnvelopeFailure.MessageIdMismatch => ConversationCommandUnaddressableCategory.MessageIdMismatch,
                _ => throw new InvalidOperationException($"Unhandled {nameof(ConversationCommandEnvelopeFailure)} '{validation.Failure}'."),
            };
            return ConversationCommandClassification.Unaddressable(category);
        }

        return ConversationCommandClassification.Addressable(command, validation.TenantId!);
    }
}
