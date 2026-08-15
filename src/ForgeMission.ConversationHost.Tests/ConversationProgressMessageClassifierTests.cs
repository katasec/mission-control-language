using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Pure classification tests — no grain, no Service Bus connection. Synthetic
/// <see cref="ServiceBusReceivedMessage"/> instances via <see cref="ServiceBusModelFactory"/>
/// (Phase 43.16 Task 8c).</summary>
public class ConversationProgressMessageClassifierTests
{
    private static ConversationProgress ValidProgress(Guid conversationId, Guid eventId) => new(
        eventId, conversationId, Guid.NewGuid(), ConversationEventKind.RunStatus, ConversationParticipant.Forge,
        null, null, null, null, null, null, null, ConversationRunStatus.Completed, DateTimeOffset.UtcNow);

    private static ServiceBusReceivedMessage RawMessage(string body, string? sessionId = null, string? messageId = null, IDictionary<string, object>? properties = null) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString("N"),
            sessionId: sessionId ?? "kind-verifier-00000000-0000-0000-0000-000000000000",
            properties: properties ?? new Dictionary<string, object> { ["tenant_id"] = "dev" });

    [Fact]
    public void InvalidJsonBody_ClassifiesUnaddressable_InvalidJson()
    {
        // The exact real-incident shape: a raw, non-JSON kind-verifier-<uuid> probe body.
        var message = RawMessage("kind-verifier-8b6b3a8e-3f2b-4a9f-8f7b-2f7c1a2b3c4d");

        var classification = ConversationProgressMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableProgress>(classification);
        Assert.Equal(ConversationProgressUnaddressableCategory.InvalidJson, unaddressable.Category);
    }

    [Fact]
    public void NullJsonBody_ClassifiesUnaddressable_NullBody()
    {
        var message = RawMessage("null");

        var classification = ConversationProgressMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableProgress>(classification);
        Assert.Equal(ConversationProgressUnaddressableCategory.NullBody, unaddressable.Category);
    }

    [Fact]
    public void MissingTenantIdProperty_ClassifiesUnaddressable_MissingTenantProperty()
    {
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: eventId.ToString("N"), properties: new Dictionary<string, object>());

        var classification = ConversationProgressMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableProgress>(classification);
        Assert.Equal(ConversationProgressUnaddressableCategory.MissingTenantProperty, unaddressable.Category);
    }

    [Fact]
    public void SessionIdMismatch_ClassifiesUnaddressable_SessionIdMismatch()
    {
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: Guid.NewGuid().ToString("N"), messageId: eventId.ToString("N"));

        var classification = ConversationProgressMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableProgress>(classification);
        Assert.Equal(ConversationProgressUnaddressableCategory.SessionIdMismatch, unaddressable.Category);
    }

    [Fact]
    public void MessageIdMismatch_ClassifiesUnaddressable_MessageIdMismatch()
    {
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: Guid.NewGuid().ToString("N"));

        var classification = ConversationProgressMessageClassifier.Classify(message);

        var unaddressable = Assert.IsType<UnaddressableProgress>(classification);
        Assert.Equal(ConversationProgressUnaddressableCategory.MessageIdMismatch, unaddressable.Category);
    }

    [Fact]
    public void ValidEnvelope_ClassifiesAddressable()
    {
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: eventId.ToString("N"));

        var classification = ConversationProgressMessageClassifier.Classify(message);

        var addressable = Assert.IsType<AddressableProgress>(classification);
        Assert.Equal("dev", addressable.TenantId);
        Assert.Equal(eventId, addressable.Progress.EventId);
    }
}
