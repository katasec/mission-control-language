using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>
/// Session processor on the <c>conversation-progress</c> queue's own dead-letter sub-queue —
/// session-aware because its parent queue is session-enabled. A dead-lettered message is delivery
/// that the main <see cref="ConversationProgressConsumer"/> could never settle: if it is a valid,
/// addressable body (deserializes, carries a non-empty <c>tenant_id</c>), this turns it into a
/// stable UUID-v5-derived <see cref="ConversationEventKind.Error"/> fact followed by
/// <see cref="ConversationRunStatus.Failed"/>, so the conversation's own log records the failure
/// rather than the run hanging forever. A malformed or unaddressable message cannot be turned into
/// any durable fact at all — it is structured-logged and completed so it does not dead-letter loop.
/// </summary>
public sealed class ConversationProgressDeadLetterConsumer(
    ServiceBusClient client,
    ConversationServiceBusOptions options,
    IGrainFactory grainFactory,
    ILogger<ConversationProgressDeadLetterConsumer> logger)
    : BackgroundService
{
    private ServiceBusSessionProcessor? processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var deadLetterPath = $"{options.ProgressQueueName}/$DeadLetterQueue";
        processor = client.CreateSessionProcessor(deadLetterPath, new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentSessions = 1,
            MaxConcurrentCallsPerSession = 1,
        });

        processor.ProcessMessageAsync += ProcessMessageAsync;
        processor.ProcessErrorAsync += ProcessErrorAsync;

        await processor.StartProcessingAsync(stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await processor.StopProcessingAsync(CancellationToken.None);
        }
    }

    private async Task ProcessMessageAsync(ProcessSessionMessageEventArgs args)
    {
        var (progress, tenantId) = TryReadAddressableProgress(args.Message);
        if (progress is null || tenantId is null)
        {
            logger.LogError(
                "Progress dead-letter message '{MessageId}' is malformed or unaddressable; completing without action.",
                args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        var address = new ConversationAddress(tenantId, progress.ConversationId);
        var grain = grainFactory.GetGrain<IConversationGrain>(address.ToString());

        var errorProgress = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(progress.EventId, "progress-error"),
            progress.ConversationId, progress.RunId, ConversationEventKind.Error, ConversationParticipant.Forge,
            null, null, "Progress delivery exhausted retries and was dead-lettered.", null, null, null, null, null,
            DateTimeOffset.UtcNow);
        var errorAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(errorProgress, ConversationContractsJsonContext.Default.ConversationProgress)));
        if (errorAcceptance.Outcome == ConversationProgressOutcome.Rejected)
            logger.LogWarning(
                "Dead-letter Error fact for progress message '{MessageId}' was rejected: {Reason}",
                args.Message.MessageId, errorAcceptance.RejectionReason);

        var failedProgress = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(progress.EventId, "progress-failed"),
            progress.ConversationId, progress.RunId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            null, null, null, null, null, null, null, ConversationRunStatus.Failed, DateTimeOffset.UtcNow);
        var failedAcceptance = await grain.RecordProgressAsync(new ConversationProgressInput(
            JsonSerializer.Serialize(failedProgress, ConversationContractsJsonContext.Default.ConversationProgress)));
        if (failedAcceptance.Outcome == ConversationProgressOutcome.Rejected)
            logger.LogWarning(
                "Dead-letter Failed fact for progress message '{MessageId}' was rejected: {Reason}",
                args.Message.MessageId, failedAcceptance.RejectionReason);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private static (ConversationProgress? Progress, string? TenantId) TryReadAddressableProgress(ServiceBusReceivedMessage message)
    {
        ConversationProgress? progress;
        try
        {
            progress = JsonSerializer.Deserialize(message.Body, ConversationContractsJsonContext.Default.ConversationProgress);
        }
        catch (JsonException)
        {
            return (null, null);
        }

        if (progress is null)
            return (null, null);

        if (!message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue)
            || tenantValue is not string { Length: > 0 } tenantId)
            return (progress, null);

        return (progress, tenantId);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Progress dead-letter session processor error from {ErrorSource}.", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (processor is not null)
            await processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
