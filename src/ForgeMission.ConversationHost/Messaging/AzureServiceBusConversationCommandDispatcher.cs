using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;

namespace ForgeMission.ConversationHost.Messaging;

public sealed class AzureServiceBusConversationCommandDispatcher(ServiceBusSender sender) : IConversationCommandDispatcher
{
    public async Task SendAsync(ConversationAddress address, ConversationCommand command, CancellationToken ct)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(command, ConversationContractsJsonContext.Default.ConversationCommand);
        var message = new ServiceBusMessage(body)
        {
            MessageId = command.CommandId.ToString("N"),
            SessionId = address.ConversationId.ToString("N"),
            ContentType = "application/json",
        };
        message.ApplicationProperties["tenant_id"] = address.TenantId;

        await sender.SendMessageAsync(message, ct);
    }
}
