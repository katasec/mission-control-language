using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>The trusted <c>tenant_id</c> on success, or the reason validation failed.</summary>
public readonly record struct EnvelopeValidationResult(bool IsValid, string? TenantId, string? FailureReason)
{
    public static EnvelopeValidationResult Valid(string tenantId) => new(true, tenantId, null);
    public static EnvelopeValidationResult Invalid(string reason) => new(false, null, reason);
}

/// <summary>
/// The one place a <see cref="ConversationCommand"/> message's routing envelope (trusted
/// <c>tenant_id</c> application property, <c>SessionId</c>/<c>MessageId</c>) is checked against its
/// body — shared by <see cref="AzureServiceBusMissionCommandConsumer"/>'s main queue (where a
/// mismatch is unrecoverable-by-retry and throws so the broker's own redelivery-then-dead-letter
/// path takes over) and its command dead-letter path (where a mismatch means the DLQ envelope
/// cannot be trusted addressable, so it is logged and completed without any durable side effect).
/// An exact duplicate of Host's own <c>ConversationProgressEnvelopeValidator</c> shape adapted to
/// <see cref="ConversationCommand"/> — Worker cannot reference Host, and this shape is too small to
/// justify a shared project.
/// </summary>
public static class ConversationCommandEnvelopeValidator
{
    public static EnvelopeValidationResult Validate(ConversationCommand command, string? sessionId, string? messageId, string? tenantIdProperty)
    {
        if (string.IsNullOrEmpty(tenantIdProperty))
            return EnvelopeValidationResult.Invalid("Missing a non-empty 'tenant_id' application property.");

        if (sessionId != command.ConversationId.ToString("N"))
            return EnvelopeValidationResult.Invalid(
                $"SessionId '{sessionId}' does not match body ConversationId '{command.ConversationId:N}'.");

        if (messageId != command.CommandId.ToString("N"))
            return EnvelopeValidationResult.Invalid(
                $"MessageId '{messageId}' does not match body CommandId '{command.CommandId:N}'.");

        return EnvelopeValidationResult.Valid(tenantIdProperty);
    }
}
