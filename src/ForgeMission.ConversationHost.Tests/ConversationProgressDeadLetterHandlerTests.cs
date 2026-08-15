using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>Phase 43.16 Task 8c: no prior dedicated test existed for the DLQ consumer's addressable
/// terminal-failure path — this is new, real coverage, not a re-confirmation.</summary>
[Collection("Azurite")]
public class ConversationProgressDeadLetterHandlerTests(AzuriteFixture fixture)
{
    private static ServiceBusReceivedMessage RawMessage(string body, string? sessionId = null, string? messageId = null, IDictionary<string, object>? properties = null) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: messageId ?? Guid.NewGuid().ToString("N"),
            sessionId: sessionId ?? "kind-verifier-00000000-0000-0000-0000-000000000000",
            properties: properties ?? new Dictionary<string, object> { ["tenant_id"] = "dev" });

    private static ConversationProgress ValidProgress(Guid conversationId, Guid runId, Guid eventId) => new(
        eventId, conversationId, runId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
        null, null, null, null, null, null, null, ConversationRunStatus.Completed, DateTimeOffset.UtcNow);

    [Fact]
    public async Task InvalidJsonBody_DiscardsWithoutInvokingGrain()
    {
        var handler = new ConversationProgressDeadLetterHandler(new ThrowingGrainFactory());
        var message = RawMessage("kind-verifier-8b6b3a8e-3f2b-4a9f-8f7b-2f7c1a2b3c4d");

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.False(result.WasAddressable);
        Assert.Equal(ConversationProgressUnaddressableCategory.InvalidJson, result.DiscardCategory);
        Assert.Null(result.ErrorFactOutcome);
        Assert.Null(result.FailedFactOutcome);
    }

    [Fact]
    public async Task MissingTenantId_DiscardsWithoutInvokingGrain()
    {
        var handler = new ConversationProgressDeadLetterHandler(new ThrowingGrainFactory());
        var conversationId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(conversationId, Guid.NewGuid(), eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: conversationId.ToString("N"), messageId: eventId.ToString("N"), properties: new Dictionary<string, object>());

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.False(result.WasAddressable);
        Assert.Equal(ConversationProgressUnaddressableCategory.MissingTenantProperty, result.DiscardCategory);
    }

    [Fact]
    public async Task AddressableMessage_BothFactsApplied_RecordsErrorThenFailedInOrder()
    {
        await using var host = await fixture.StartHostAsync();
        var handler = new ConversationProgressDeadLetterHandler(host.GrainFactory);

        var address = new ConversationAddress("dev", Guid.NewGuid());
        var runId = Guid.NewGuid();
        await AcceptStartCommandAsync(host, address, runId);

        var eventId = Guid.NewGuid();
        var progress = ValidProgress(address.ConversationId, runId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: address.ConversationId.ToString("N"), messageId: eventId.ToString("N"));

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.True(result.WasAddressable);
        Assert.Equal(ConversationProgressHandlingOutcome.Applied, result.ErrorFactOutcome);
        Assert.Equal(ConversationProgressHandlingOutcome.Applied, result.FailedFactOutcome);

        var grain = host.GetConversationGrain(address);
        var batch = await grain.ReadAfterAsync(0);
        var kinds = batch.EventJson
            .Select(json => System.Text.Json.JsonSerializer.Deserialize(json, ConversationContractsJsonContext.Default.ConversationEvent)!.Kind)
            .ToList();
        Assert.Contains(ConversationEventKind.Error, kinds);
        Assert.Contains(ConversationEventKind.RunStatus, kinds);
        Assert.True(kinds.IndexOf(ConversationEventKind.Error) < kinds.LastIndexOf(ConversationEventKind.RunStatus));
    }

    [Fact]
    public async Task AddressableMessage_WrongActiveRun_BothFactsRejected_NeitherCollapsedIntoApplied()
    {
        await using var host = await fixture.StartHostAsync();
        var handler = new ConversationProgressDeadLetterHandler(host.GrainFactory);

        var address = new ConversationAddress("dev", Guid.NewGuid());
        var actualRunId = Guid.NewGuid();
        await AcceptStartCommandAsync(host, address, actualRunId);

        // A different RunId than the conversation's actual active run — the grain's own
        // "Progress does not match this conversation's active run" rejection, exercised for real.
        var wrongRunId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var progress = ValidProgress(address.ConversationId, wrongRunId, eventId);
        var body = System.Text.Json.JsonSerializer.Serialize(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = RawMessage(body, sessionId: address.ConversationId.ToString("N"), messageId: eventId.ToString("N"));

        var result = await handler.HandleAsync(message, CancellationToken.None);

        Assert.True(result.WasAddressable);
        Assert.Equal(ConversationProgressHandlingOutcome.Rejected, result.ErrorFactOutcome);
        Assert.NotNull(result.ErrorFactRejectionReason);
        Assert.Equal(ConversationProgressHandlingOutcome.Rejected, result.FailedFactOutcome);
        Assert.NotNull(result.FailedFactRejectionReason);
    }

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
