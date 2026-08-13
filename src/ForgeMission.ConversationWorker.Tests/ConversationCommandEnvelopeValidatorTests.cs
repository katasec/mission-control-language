using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>Review round 2, correction 3: the Worker command-DLQ path (and the main command
/// consumer) must validate non-empty tenant_id AND matching body ConversationId/CommandId against
/// incoming SessionId/MessageId before publishing Error/Failed.</summary>
public class ConversationCommandEnvelopeValidatorTests
{
    private static ConversationCommand NewCommand(Guid conversationId, Guid commandId) => new(
        commandId, conversationId, Guid.NewGuid(), ConversationCommandKind.StartMission, "Janus", "goal", [], null);

    [Fact]
    public void MatchingEnvelope_IsValid_AndReturnsTheTenantId()
    {
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = NewCommand(conversationId, commandId);

        var result = ConversationCommandEnvelopeValidator.Validate(
            command, conversationId.ToString("N"), commandId.ToString("N"), "dev");

        Assert.True(result.IsValid);
        Assert.Equal("dev", result.TenantId);
    }

    [Fact]
    public void MissingTenantId_IsInvalid()
    {
        var command = NewCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = ConversationCommandEnvelopeValidator.Validate(
            command, command.ConversationId.ToString("N"), command.CommandId.ToString("N"), tenantIdProperty: null);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MismatchedSessionId_IsInvalid()
    {
        var command = NewCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = ConversationCommandEnvelopeValidator.Validate(
            command, Guid.NewGuid().ToString("N"), command.CommandId.ToString("N"), "dev");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void MismatchedMessageId_IsInvalid()
    {
        var command = NewCommand(Guid.NewGuid(), Guid.NewGuid());
        var result = ConversationCommandEnvelopeValidator.Validate(
            command, command.ConversationId.ToString("N"), Guid.NewGuid().ToString("N"), "dev");

        Assert.False(result.IsValid);
    }
}
