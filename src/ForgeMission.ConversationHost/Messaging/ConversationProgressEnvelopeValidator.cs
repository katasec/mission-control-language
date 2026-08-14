using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>The trusted <c>tenant_id</c> on success, or the reason validation failed.</summary>
public readonly record struct EnvelopeValidationResult(bool IsValid, string? TenantId, string? FailureReason)
{
    public static EnvelopeValidationResult Valid(string tenantId) => new(true, tenantId, null);
    public static EnvelopeValidationResult Invalid(string reason) => new(false, null, reason);
}

/// <summary>
/// The one place a <see cref="ConversationProgress"/> message's routing envelope (trusted
/// <c>tenant_id</c> application property, <c>SessionId</c>/<c>MessageId</c>) is checked against its
/// body — shared by <see cref="ConversationProgressHandler"/> (the main queue, where a mismatch is
/// an unrecoverable-by-retry condition that throws so the broker's own redelivery-then-dead-letter
/// path takes over) and <see cref="ConversationProgressDeadLetterConsumer"/> (where a mismatch means
/// the DLQ envelope itself cannot be trusted addressable, so it is logged and completed without any
/// durable side effect — never a guess at a grain call).
/// </summary>
public static class ConversationProgressEnvelopeValidator
{
    public static EnvelopeValidationResult Validate(ConversationProgress progress, string? sessionId, string? messageId, string? tenantIdProperty)
    {
        if (string.IsNullOrEmpty(tenantIdProperty))
            return EnvelopeValidationResult.Invalid("Missing a non-empty 'tenant_id' application property.");

        if (sessionId != progress.ConversationId.ToString("N"))
            return EnvelopeValidationResult.Invalid(
                $"SessionId '{sessionId}' does not match body ConversationId '{progress.ConversationId:N}'.");

        if (messageId != progress.EventId.ToString("N"))
            return EnvelopeValidationResult.Invalid(
                $"MessageId '{messageId}' does not match body EventId '{progress.EventId:N}'.");

        return EnvelopeValidationResult.Valid(tenantIdProperty);
    }
}
