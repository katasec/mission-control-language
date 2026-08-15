using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>Why a <see cref="ConversationCommand"/> envelope failed validation — a fixed category,
/// never an interpolated identifier. Callers must log only this enum's name plus the broker
/// MessageId, never a raw SessionId/tenant/body value (Phase 43.16 Task 8c).</summary>
public enum ConversationCommandEnvelopeFailure
{
    MissingTenantProperty,
    SessionIdMismatch,
    MessageIdMismatch,
}

/// <summary>The trusted <c>tenant_id</c> on success, or the fixed category validation failed.</summary>
public readonly record struct EnvelopeValidationResult(bool IsValid, string? TenantId, ConversationCommandEnvelopeFailure? Failure)
{
    public static EnvelopeValidationResult Valid(string tenantId) => new(true, tenantId, null);
    public static EnvelopeValidationResult Invalid(ConversationCommandEnvelopeFailure failure) => new(false, null, failure);
}

/// <summary>
/// The one place a <see cref="ConversationCommand"/> message's routing envelope (trusted
/// <c>tenant_id</c> application property, <c>SessionId</c>/<c>MessageId</c>) is checked against its
/// body — shared by <see cref="ConversationCommandMessageClassifier"/>, in turn shared by
/// <see cref="AzureServiceBusMissionCommandConsumer"/>'s main queue and its command dead-letter
/// path. A mismatch is unaddressable poison input for both. An exact duplicate of Host's own
/// <c>ConversationProgressEnvelopeValidator</c> shape adapted to <see cref="ConversationCommand"/> —
/// Worker cannot reference Host, and this shape is too small to justify a shared project
/// (Phase 43.16 Task 8c).
/// </summary>
public static class ConversationCommandEnvelopeValidator
{
    public static EnvelopeValidationResult Validate(ConversationCommand command, string? sessionId, string? messageId, string? tenantIdProperty)
    {
        if (string.IsNullOrEmpty(tenantIdProperty))
            return EnvelopeValidationResult.Invalid(ConversationCommandEnvelopeFailure.MissingTenantProperty);

        if (sessionId != command.ConversationId.ToString("N"))
            return EnvelopeValidationResult.Invalid(ConversationCommandEnvelopeFailure.SessionIdMismatch);

        if (messageId != command.CommandId.ToString("N"))
            return EnvelopeValidationResult.Invalid(ConversationCommandEnvelopeFailure.MessageIdMismatch);

        return EnvelopeValidationResult.Valid(tenantIdProperty);
    }
}
