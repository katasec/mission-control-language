using ForgeMission.ConversationHost.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Review round 2, correction 3: the Host progress-DLQ path (and the main progress
/// consumer) must validate non-empty tenant_id AND matching body ConversationId/EventId against
/// incoming SessionId/MessageId before any grain call.</summary>
public class ConversationProgressEnvelopeValidatorTests
{
    private static ConversationProgress NewProgress(Guid conversationId, Guid eventId) => new(
        eventId, conversationId, Guid.NewGuid(), ConversationEventKind.RunStatus, ConversationParticipant.Forge,
        null, null, null, null, null, null, null, ConversationRunStatus.Completed, DateTimeOffset.UtcNow);

    [Fact]
    public void MatchingEnvelope_IsValid_AndReturnsTheTenantId()
    {
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = NewProgress(conversationId, eventId);

        var result = ConversationProgressEnvelopeValidator.Validate(
            progress, conversationId.ToString("N"), eventId.ToString("N"), "dev");

        Assert.True(result.IsValid);
        Assert.Equal("dev", result.TenantId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void MissingTenantId_IsInvalid()
    {
        var progress = NewProgress(Guid.NewGuid(), Guid.NewGuid());
        var result = ConversationProgressEnvelopeValidator.Validate(
            progress, progress.ConversationId.ToString("N"), progress.EventId.ToString("N"), tenantIdProperty: null);

        Assert.False(result.IsValid);
        Assert.Null(result.TenantId);
    }

    [Fact]
    public void MismatchedSessionId_IsInvalid()
    {
        var progress = NewProgress(Guid.NewGuid(), Guid.NewGuid());
        var result = ConversationProgressEnvelopeValidator.Validate(
            progress, Guid.NewGuid().ToString("N"), progress.EventId.ToString("N"), "dev");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MismatchedMessageId_IsInvalid()
    {
        var progress = NewProgress(Guid.NewGuid(), Guid.NewGuid());
        var result = ConversationProgressEnvelopeValidator.Validate(
            progress, progress.ConversationId.ToString("N"), Guid.NewGuid().ToString("N"), "dev");

        Assert.False(result.IsValid);
    }
}
