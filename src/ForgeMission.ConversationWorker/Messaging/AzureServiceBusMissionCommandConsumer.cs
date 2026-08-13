using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.Conversations.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ForgeMission.ConversationWorker.Messaging;

/// <summary>
/// The Service Bus SDK adapter for the <c>mission-command</c> queue, and — folded into the same
/// class per the locked design — its own dead-letter sub-queue. Both are session processors:
/// peek-lock, auto-complete false, one concurrent session and one concurrent call per session (a
/// single Janus run per conversation at a time). All recovery/outbox decision logic lives in the
/// SDK-independent <see cref="MissionCommandProcessor"/>; this class only reads/writes the Service
/// Bus session state and completes messages.
/// </summary>
public sealed class AzureServiceBusMissionCommandConsumer(
    ServiceBusClient client,
    ConversationServiceBusOptions options,
    JanusMissionContext mission,
    IConversationProgressPublisher publisher,
    ILogger<AzureServiceBusMissionCommandConsumer> logger)
    : BackgroundService
{
    private readonly MissionCommandProcessor _processor = new(mission);

    private ServiceBusSessionProcessor? _commandProcessor;
    private ServiceBusSessionProcessor? _deadLetterProcessor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var processorOptions = new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentSessions = 1,
            MaxConcurrentCallsPerSession = 1,
        };

        _commandProcessor = client.CreateSessionProcessor(options.MissionCommandQueueName, processorOptions);
        _commandProcessor.ProcessMessageAsync += ProcessCommandAsync;
        _commandProcessor.ProcessErrorAsync += ProcessErrorAsync;

        _deadLetterProcessor = client.CreateSessionProcessor(
            $"{options.MissionCommandQueueName}/$DeadLetterQueue", processorOptions);
        _deadLetterProcessor.ProcessMessageAsync += ProcessDeadLetterAsync;
        _deadLetterProcessor.ProcessErrorAsync += ProcessErrorAsync;

        await _commandProcessor.StartProcessingAsync(stoppingToken);
        await _deadLetterProcessor.StartProcessingAsync(stoppingToken);
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _commandProcessor.StopProcessingAsync(CancellationToken.None);
            await _deadLetterProcessor.StopProcessingAsync(CancellationToken.None);
        }
    }

    private async Task ProcessCommandAsync(ProcessSessionMessageEventArgs args)
    {
        var ct = args.CancellationToken;

        ConversationCommand command;
        string tenantId;
        try
        {
            command = JsonSerializer.Deserialize(args.Message.Body, ConversationContractsJsonContext.Default.ConversationCommand)
                ?? throw new InvalidOperationException("Command body deserialized to null.");

            if (!args.Message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue)
                || tenantValue is not string { Length: > 0 } t)
                throw new InvalidOperationException("Missing a non-empty 'tenant_id' application property.");
            tenantId = t;

            if (args.Message.SessionId != command.ConversationId.ToString("N"))
                throw new InvalidOperationException("SessionId does not match body ConversationId.");
            if (args.Message.MessageId != command.CommandId.ToString("N"))
                throw new InvalidOperationException("MessageId does not match body CommandId.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled failure reading command message '{MessageId}'; leaving unsettled for broker retry.",
                args.Message.MessageId);
            return;
        }

        var session = await LoadSessionStateAsync(args, ct);

        await _processor.ProcessAsync(
            command, tenantId, session,
            saveSessionAsync: (s, saveCt) => SaveSessionStateAsync(args, s, saveCt),
            publishAsync: (progress, tid, publishCt) => publisher.PublishAsync(progress, tid, publishCt),
            ct);

        await args.CompleteMessageAsync(args.Message, ct);
    }

    private async Task ProcessDeadLetterAsync(ProcessSessionMessageEventArgs args)
    {
        ConversationCommand? command;
        string? tenantId;
        try
        {
            command = JsonSerializer.Deserialize(args.Message.Body, ConversationContractsJsonContext.Default.ConversationCommand);
            tenantId = args.Message.ApplicationProperties.TryGetValue("tenant_id", out var tenantValue)
                && tenantValue is string { Length: > 0 } t ? t : null;
        }
        catch (JsonException)
        {
            command = null;
            tenantId = null;
        }

        if (command is null || tenantId is null)
        {
            logger.LogError(
                "Command dead-letter message '{MessageId}' is malformed or unaddressable; completing without action.",
                args.Message.MessageId);
            await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            return;
        }

        var errorFact = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(command.CommandId, "command-error"),
            command.ConversationId, command.RunId, ConversationEventKind.Error, ConversationParticipant.Forge,
            null, null, "Mission command delivery exhausted retries and was dead-lettered.", null, null, null, null, null,
            DateTimeOffset.UtcNow);
        await publisher.PublishAsync(errorFact, tenantId, args.CancellationToken);

        var failedFact = new ConversationProgress(
            ConversationDeterministicIds.DeadLetter(command.CommandId, "command-failed"),
            command.ConversationId, command.RunId, ConversationEventKind.RunStatus, ConversationParticipant.Forge,
            null, null, null, null, null, null, null, ConversationRunStatus.Failed, DateTimeOffset.UtcNow);
        await publisher.PublishAsync(failedFact, tenantId, args.CancellationToken);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Mission-command session processor error from {ErrorSource}.", args.ErrorSource);
        return Task.CompletedTask;
    }

    private static async Task<WorkerSessionState?> LoadSessionStateAsync(ProcessSessionMessageEventArgs args, CancellationToken ct)
    {
        var state = await args.GetSessionStateAsync(ct);
        if (state is null || state.ToMemory().Length == 0)
            return null;
        return JsonSerializer.Deserialize(state.ToMemory().Span, WorkerSessionStateJsonContext.Default.WorkerSessionState);
    }

    private static Task SaveSessionStateAsync(ProcessSessionMessageEventArgs args, WorkerSessionState session, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(session, WorkerSessionStateJsonContext.Default.WorkerSessionState);
        return args.SetSessionStateAsync(new BinaryData(json), ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_commandProcessor is not null)
            await _commandProcessor.DisposeAsync();
        if (_deadLetterProcessor is not null)
            await _deadLetterProcessor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
