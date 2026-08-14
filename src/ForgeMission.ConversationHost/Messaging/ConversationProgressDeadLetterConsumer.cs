using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>
/// The Service Bus SDK adapter for the <c>conversation-progress</c> queue's dead-letter sub-queue.
/// A plain (non-session) processor via <see cref="ServiceBusProcessorOptions.SubQueue"/> — the
/// SDK's session processor type has no <c>SubQueue</c> setting, and the dead-letter sub-queue of a
/// session-enabled entity is not itself session-enabled, so a real session receiver cannot target
/// it at all; <c>MaxConcurrentCalls = 1</c> keeps the one-at-a-time processing the rest of this
/// pipeline relies on. All classification and grain-call logic lives in the SDK-independent
/// <see cref="ConversationProgressDeadLetterHandler"/> (Phase 43.16 Task 8c); this class only reads
/// the Service Bus message, logs the outcome, and completes it — every result completes, since a
/// dead-lettered message never gets a second retry regardless of outcome.
/// </summary>
public sealed class ConversationProgressDeadLetterConsumer(
    ServiceBusClient client,
    ConversationServiceBusOptions options,
    ConversationProgressDeadLetterHandler handler,
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
        var result = await handler.HandleAsync(args.Message, args.CancellationToken);

        if (!result.WasAddressable)
        {
            logger.LogError(
                "Progress dead-letter message '{MessageId}' discarded as unaddressable: {Category}.",
                args.Message.MessageId, result.DiscardCategory);
        }
        else
        {
            if (result.ErrorFactOutcome == ConversationProgressHandlingOutcome.Rejected)
                logger.LogWarning(
                    "Dead-letter Error fact for progress message '{MessageId}' was rejected: {Reason}",
                    args.Message.MessageId, result.ErrorFactRejectionReason);
            if (result.FailedFactOutcome == ConversationProgressHandlingOutcome.Rejected)
                logger.LogWarning(
                    "Dead-letter Failed fact for progress message '{MessageId}' was rejected: {Reason}",
                    args.Message.MessageId, result.FailedFactRejectionReason);
        }

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
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
