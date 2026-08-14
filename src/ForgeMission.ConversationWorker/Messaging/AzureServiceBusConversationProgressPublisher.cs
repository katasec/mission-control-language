using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationWorker.Messaging;

public sealed class AzureServiceBusConversationProgressPublisher(ServiceBusSender sender) : IConversationProgressPublisher
{
    public async Task PublishAsync(ConversationProgress progress, string tenantId, CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(progress, ConversationContractsJsonContext.Default.ConversationProgress);
        var message = new ServiceBusMessage(body)
        {
            MessageId = progress.EventId.ToString("N"),
            SessionId = progress.ConversationId.ToString("N"),
            ContentType = "application/json",
        };
        message.ApplicationProperties["tenant_id"] = tenantId;

        await sender.SendMessageAsync(message, ct);
    }
}
