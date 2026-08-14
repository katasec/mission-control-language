using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Phase 43.16 Task 8c: unaddressable poison input must never invoke a grain call, and
/// addressable-message behavior (Applied/Rejected) must be provably unchanged.</summary>
[Collection("Azurite")]
public class ConversationProgressHandlerTests(AzuriteFixture fixture)
{
    private static ServiceBusReceivedMessage RawMessage(string body, string? sessionId = null, string? messageId = null, IDictionary<string, object>? properties = null) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString("N"),
            sessionId: sessionId ?? "kind-verifier-00000000-0000-0000-0000-000000000000",
            properties: properties ?? new Dictionary<string, object> { ["tenant_id"] = "dev" });

    // ── Poison input: never reaches IGrainFactory ──────────────────────────────

    [Fact]
    public async Task InvalidJsonBody_DiscardsWithoutInvokingGrain()
    {
        var handler = new ConversationProgressHandler(new ThrowingGrainFactory());
        var message = RawMessage("kind-verifier-8b6b3a8e-3f2b-4a9f-8f7b-2f7c1a2b3c4d");

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(ConversationProgressHandlingOutcome.Discarded, result.Outcome);
        Assert.Equal(nameof(ConversationProgressUnaddressableCategory.InvalidJson), result.Reason);
    }

    [Fact]
    public async Task MissingTenantId_DiscardsWithoutInvokingGrain()
    {
        var handler = new ConversationProgressHandler(new ThrowingGrainFactory());
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: eventId.ToString("N"), properties: new Dictionary<string, object>());

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(ConversationProgressHandlingOutcome.Discarded, result.Outcome);
        Assert.Equal(nameof(ConversationProgressUnaddressableCategory.MissingTenantProperty), result.Reason);
    }

    [Fact]
    public async Task SessionIdMismatch_DiscardsWithoutInvokingGrain()
    {
        var handler = new ConversationProgressHandler(new ThrowingGrainFactory());
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: Guid.NewGuid().ToString("N"), messageId: eventId.ToString("N"));

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(ConversationProgressHandlingOutcome.Discarded, result.Outcome);
        Assert.Equal(nameof(ConversationProgressUnaddressableCategory.SessionIdMismatch), result.Reason);
    }

    // ── Addressable input: real grain, unchanged behavior ──────────────────────

    [Fact]
    public async Task AddressableMessage_StillCallsGrain_AppliedOutcomeUnchanged()
    {
        await using var host = await fixture.StartHostAsync();
        var handler = new ConversationProgressHandler(host.GrainFactory);

        var address = new ConversationAddress("dev", Guid.NewGuid());
        var runId = Guid.NewGuid();
        await AcceptStartCommandAsync(host, address, runId);

        var eventId = Guid.NewGuid();
        var progress = new ConversationProgress(
            eventId, address.ConversationId, runId, ConversationEventKind.ParticipantStarted, ConversationParticipant.Proposer,
            1, null, null, null, null, null, null, null, DateTimeOffset.UtcNow);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: address.ConversationId.ToString("N"), messageId: eventId.ToString("N"));

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.Equal(ConversationProgressHandlingOutcome.Applied, result.Outcome);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task TransientFailureAfterClassification_StillPropagates_ForBrokerRetry()
    {
        var handler = new ConversationProgressHandler(new ThrowingGrainFactory());

        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        // A VALID envelope reaches the (throwing) grain factory — proves a genuine
        // post-classification failure is not swallowed, unlike the poison-discard path above.
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: eventId.ToString("N"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(message, CancellationToken.None));
    }

    private static ConversationProgress ValidProgress(Guid conversationId, Guid eventId) => new(
        eventId, conversationId, Guid.NewGuid(), ConversationEventKind.RunStatus, ConversationParticipant.Forge,
        null, null, null, null, null, null, null, ConversationRunStatus.Completed, DateTimeOffset.UtcNow);

    private static async Task AcceptStartCommandAsync(ConversationHostInstance host, ConversationAddress address, Guid runId)
    {
        var grain = host.GetConversationGrain(address);
        var command = new ConversationCommand(
            Guid.NewGuid(), address.ConversationId, runId, ConversationCommandKind.StartMission, "Janus", "goal", [], null);
        var json = System.Text.Json.JsonSerializer.Serialize(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var result = await grain.AcceptCommandAsync(new ConversationCommandInput(json));
        Assert.Equal(ConversationCommandOutcome.Accepted, result.Outcome);
    }
}
