using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.Conversations.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>
/// Processor on the <c>conversation-progress</c> queue's own dead-letter sub-queue. A plain
/// (non-session) processor via <see cref="ServiceBusProcessorOptions.SubQueue"/> — the SDK's
/// session processor type has no <c>SubQueue</c> setting, and the dead-letter sub-queue of a
/// session-enabled entity is not itself session-enabled, so a real session receiver cannot target
/// it at all; <c>MaxConcurrentCalls = 1</c> keeps the one-at-a-time processing the rest of this
/// pipeline relies on. A dead-lettered message is delivery that the main
/// <see cref="ConversationProgressConsumer"/> could never settle: if its body deserializes AND its
/// trusted <c>tenant_id</c>/<c>SessionId</c>/<c>MessageId</c> envelope matches its own
/// ConversationId/EventId (the same check <see cref="ConversationProgressHandler"/> makes), this
/// turns it into a stable UUID-v5-derived <see cref="ConversationEventKind.Error"/> fact followed by
/// <see cref="ConversationRunStatus.Failed"/>, so the conversation's own log records the failure
/// rather than the run hanging forever. A malformed or unaddressable message (deserialize failure,
/// missing tenant, or an envelope that does not match its own body) cannot be turned into any
/// durable fact at all — it is structured-logged and completed without a grain call, so it does not
/// dead-letter loop.
/// </summary>
public sealed class ConversationProgressDeadLetterConsumer(
    ServiceBusClient client,
    ConversationServiceBusOptions options,
    IGrainFactory grainFactory,
    ILogger<ConversationProgressDeadLetterConsumer> logger)
    : BackgroundService
{
    private ServiceBusProcessor? processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        processor = client.CreateProcessor(options.ProgressQueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentCalls = 1,
            SubQueue = SubQueue.DeadLetter,
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

    private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
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

        message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue);
        var validation = ConversationProgressEnvelopeValidator.Validate(
            progress, message.SessionId, message.MessageId, tenantValue as string);

        return validation.IsValid ? (progress, validation.TenantId) : (progress, null);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Progress dead-letter processor error from {ErrorSource}.", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (processor is not null)
            await processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
