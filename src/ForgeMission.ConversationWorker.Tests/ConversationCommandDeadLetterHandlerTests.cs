using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Tests;

/// <summary>Phase 43.16 Task 8c: no prior dedicated test existed for the Worker's command DLQ
/// path — new coverage, not a re-confirmation.</summary>
public class ConversationCommandDeadLetterHandlerTests
{
    private static ServiceBusReceivedMessage RawMessage(string body, string? sessionId = null, string? messageId = null, IDictionary<string, object>? properties = null) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString("N"),
            sessionId: sessionId ?? "kind-verifier-00000000-0000-0000-0000-000000000000",
            properties: properties ?? new Dictionary<string, object> { ["tenant_id"] = "dev" });

    private static ConversationCommand ValidCommand(Guid conversationId, Guid commandId) => new(
        commandId, conversationId, Guid.NewGuid(), ConversationCommandKind.StartMission, "Janus", "goal", [], null);

    [Fact]
    public async Task InvalidJsonBody_DiscardsWithoutPublishing()
    {
        var publisher = new RecordingPublisher();
        var handler = new ConversationCommandDeadLetterHandler(publisher);
        var message = RawMessage("kind-verifier-8b6b3a8e-3f2b-4a9f-8f7b-2f7c1a2b3c4d");

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.False(result.WasAddressable);
        Assert.Equal(ConversationCommandUnaddressableCategory.InvalidJson, result.DiscardCategory);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task MissingTenantId_DiscardsWithoutPublishing()
    {
        var publisher = new RecordingPublisher();
        var handler = new ConversationCommandDeadLetterHandler(publisher);
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = ValidCommand(conversationId, commandId);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: commandId.ToString("N"), properties: new Dictionary<string, object>());

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.False(result.WasAddressable);
        Assert.Equal(ConversationCommandUnaddressableCategory.MissingTenantProperty, result.DiscardCategory);
        Assert.Empty(publisher.Published);
    }

    [Fact]
    public async Task AddressableMessage_PublishesErrorThenFailed_InOrder()
    {
        var publisher = new RecordingPublisher();
        var handler = new ConversationCommandDeadLetterHandler(publisher);
        var conversationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = ValidCommand(conversationId, commandId);
        var body = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: commandId.ToString("N"));

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.True(result.WasAddressable);
        Assert.Equal(2, publisher.Published.Count);
        Assert.Equal(ConversationEventKind.Error, publisher.Published[0].Progress.Kind);
        Assert.Equal(ConversationEventKind.RunStatus, publisher.Published[1].Progress.Kind);
        Assert.Equal(ConversationRunStatus.Failed, publisher.Published[1].Progress.RunStatus);
        Assert.All(publisher.Published, p => Assert.Equal("dev", p.TenantId));
    }

    private sealed class RecordingPublisher : IConversationProgressPublisher
    {
        public List<(ConversationProgress Progress, string TenantId)> Published { get; } = [];

        public Task PublishAsync(ConversationProgress progress, string tenantId, CancellationToken ct)
        {
            Published.Add((progress, tenantId));
            return Task.CompletedTask;
        }
    }
}
