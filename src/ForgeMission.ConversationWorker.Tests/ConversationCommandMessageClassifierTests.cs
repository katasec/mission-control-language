using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>Pure classification tests — no processor, no session state, no Service Bus connection.
/// Mirrors the Host's own classifier tests for <see cref="ConversationCommand"/> (Phase 43.16
/// Task 8c).</summary>
public class ConversationCommandMessageClassifierTests
{
    private static ConversationCommand ValidCommand(Guid conversationId, Guid commandId) => new(
        commandId, conversationId, Guid.NewGuid(), ConversationCommandKind.StartMission, "Janus", "goal", [], null);

    private static ServiceBusReceivedMessage RawMessage(string body, string? sessionId = null, string? messageId = null, IDictionary<string, object>? properties = null) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString("N"),
            sessionId: sessionId ?? "kind-verifier-00000000-0000-0000-0000-000000000000",
            properties: properties ?? new Dictionary<string, object> { ["tenant_id"] = "dev" });

    [Fact]
    public void InvalidJsonBody_ClassifiesUnaddressable_InvalidJson()
    {
        var message = RawMessage("kind-verifier-8b6b3a8e-3f2b-4a9f-8f7b-2f7c1a2b3c4d");

        var classification = ConversationCommandMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableCommand>(classification);
        Assert.Equal(ConversationCommandUnaddressableCategory.InvalidJson, unaddressable.Category);
    }

    [Fact]
    public void NullJsonBody_ClassifiesUnaddressable_NullBody()
    {
        var message = RawMessage("null");

        var classification = ConversationCommandMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableCommand>(classification);
        Assert.Equal(ConversationCommandUnaddressableCategory.NullBody, unaddressable.Category);
    }

    [Fact]
    public void MissingTenantIdProperty_ClassifiesUnaddressable_MissingTenantProperty()
    {
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = ValidCommand(conversationId, commandId);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: commandId.ToString("N"), properties: new Dictionary<string, object>());

        var classification = ConversationCommandMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableCommand>(classification);
        Assert.Equal(ConversationCommandUnaddressableCategory.MissingTenantProperty, unaddressable.Category);
    }

    [Fact]
    public void SessionIdMismatch_ClassifiesUnaddressable_SessionIdMismatch()
    {
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = ValidCommand(conversationId, commandId);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: Guid.NewGuid().ToString("N"), messageId: commandId.ToString("N"));

        var classification = ConversationCommandMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableCommand>(classification);
        Assert.Equal(ConversationCommandUnaddressableCategory.SessionIdMismatch, unaddressable.Category);
    }

    [Fact]
    public void MessageIdMismatch_ClassifiesUnaddressable_MessageIdMismatch()
    {
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = ValidCommand(conversationId, commandId);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: Guid.NewGuid().ToString("N"));

        var classification = ConversationCommandMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableCommand>(classification);
        Assert.Equal(ConversationCommandUnaddressableCategory.MessageIdMismatch, unaddressable.Category);
    }

    [Fact]
    public void ValidEnvelope_ClassifiesAddressable()
    {
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = ValidCommand(conversationId, commandId);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: commandId.ToString("N"));

        var classification = ConversationCommandMessageClassifier.Classify(message);

        var addressable = Assert.IsType<AddressableCommand>(classification);
        Assert.Equal("dev", addressable.TenantId);
        Assert.Equal(commandId, addressable.Command.CommandId);
    }
}
