using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>Why a <see cref="ConversationProgress"/> envelope failed validation — a fixed category,
/// never an interpolated identifier. Callers must log only this enum's name plus the broker
/// MessageId, never a raw SessionId/tenant/body value (Phase 43.16 Task 8c).</summary>
public enum ConversationProgressEnvelopeFailure
{
    MissingTenantProperty,
    SessionIdMismatch,
    MessageIdMismatch,
}

/// <summary>The trusted <c>tenant_id</c> on success, or the fixed category validation failed.</summary>
public readonly record struct EnvelopeValidationResult(bool IsValid, string? TenantId, ConversationProgressEnvelopeFailure? Failure)
{
    public static EnvelopeValidationResult Valid(string tenantId) => new(true, tenantId, null);
    public static EnvelopeValidationResult Invalid(ConversationProgressEnvelopeFailure failure) => new(false, null, failure);
}

/// <summary>
/// The one place a <see cref="ConversationProgress"/> message's routing envelope (trusted
/// <c>tenant_id</c> application property, <c>SessionId</c>/<c>MessageId</c>) is checked against its
/// body — shared by <see cref="ConversationProgressMessageClassifier"/>, in turn shared by
/// <see cref="ConversationProgressHandler"/> (the main queue) and
/// <see cref="ConversationProgressDeadLetterHandler"/> (the dead-letter sub-queue). A mismatch here
/// is unaddressable poison input for both — never a guess at a grain call (Phase 43.16 Task 8c).
/// </summary>
public static class ConversationProgressEnvelopeValidator
{
    public static EnvelopeValidationResult Validate(ConversationProgress progress, string? sessionId, string? messageId, string? tenantIdProperty)
    {
        if (string.IsNullOrEmpty(tenantIdProperty))
            return EnvelopeValidationResult.Invalid(ConversationProgressEnvelopeFailure.MissingTenantProperty);

        if (sessionId != progress.ConversationId.ToString("N"))
            return EnvelopeValidationResult.Invalid(ConversationProgressEnvelopeFailure.SessionIdMismatch);

        if (messageId != progress.EventId.ToString("N"))
            return EnvelopeValidationResult.Invalid(ConversationProgressEnvelopeFailure.MessageIdMismatch);

        return EnvelopeValidationResult.Valid(tenantIdProperty);
    }
}
