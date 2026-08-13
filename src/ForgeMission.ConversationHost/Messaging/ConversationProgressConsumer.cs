using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeMission.ConversationHost.Messaging;

/// <summary>
/// The Service Bus SDK adapter for the <c>conversation-progress</c> queue: peek-lock,
/// auto-complete false, one concurrent session and one concurrent call per session (Janus v1 has
/// exactly one active run per conversation, so nothing is gained by parallelizing within it).
/// Completes only what <see cref="ConversationProgressHandler"/> reports as safe to complete;
/// anything it throws is logged and left unsettled so the broker redelivers it, eventually
/// dead-lettering exhausted input for <see cref="ConversationProgressDeadLetterConsumer"/>.
/// </summary>
public sealed class ConversationProgressConsumer(
    ServiceBusClient client,
    ConversationServiceBusOptions options,
    ConversationProgressHandler handler,
    ILogger<ConversationProgressConsumer> logger)
    : BackgroundService
{
    private ServiceBusSessionProcessor? processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        processor = client.CreateSessionProcessor(options.ProgressQueueName, new ServiceBusSessionProcessorOptions
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
        ConversationProgressHandlingResult result;
        try
        {
            result = await handler.HandleAsync(args.Message, args.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled failure processing progress message '{MessageId}'; leaving unsettled for broker retry.",
                args.Message.MessageId);
            return;
        }

        if (result.RejectionReason is not null)
            logger.LogWarning("Progress message '{MessageId}' rejected: {Reason}", args.Message.MessageId, result.RejectionReason);

        if (result.ShouldComplete)
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Progress session processor error from {ErrorSource}.", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (processor is not null)
            await processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
