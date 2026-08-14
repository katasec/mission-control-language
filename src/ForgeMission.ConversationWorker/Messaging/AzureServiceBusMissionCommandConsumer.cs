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
/// single Janus run per conversation at a time). Every message is classified first
/// (<see cref="ConversationCommandMessageClassifier"/>, Phase 43.16 Task 8c): unaddressable poison
/// input is completed with no session-state load, no <see cref="MissionCommandProcessor"/> call,
/// and no publish — proven by <see cref="ProcessCommandCoreAsync"/> taking session load/save as
/// injected delegates rather than touching the SDK session directly, so a test can prove they are
/// never invoked for poison input without needing a real <c>ProcessSessionMessageEventArgs</c>. All
/// other recovery/outbox decision logic lives in the SDK-independent
/// <see cref="MissionCommandProcessor"/>; this class only reads/writes the Service Bus session
/// state and completes messages.
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
    private readonly ConversationCommandDeadLetterHandler _deadLetterHandler = new(publisher);

    private ServiceBusSessionProcessor? _commandProcessor;
    private ServiceBusProcessor? _deadLetterProcessor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _commandProcessor = client.CreateSessionProcessor(options.MissionCommandQueueName, new ServiceBusSessionProcessorOptions
        {
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentSessions = 1,
            MaxConcurrentCallsPerSession = 1,
        });
        _commandProcessor.ProcessMessageAsync += ProcessCommandAsync;
        _commandProcessor.ProcessErrorAsync += ProcessErrorAsync;

        // A plain (non-session) processor: the SDK's session processor options have no SubQueue
        // setting, and the dead-letter sub-queue of a session-enabled entity is not itself
        // session-enabled — a session receiver cannot target it at all. MaxConcurrentCalls = 1
        // keeps the one-at-a-time processing the rest of this pipeline relies on.
        _deadLetterProcessor = client.CreateProcessor(options.MissionCommandQueueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            MaxConcurrentCalls = 1,
            SubQueue = SubQueue.DeadLetter,
        });
        _deadLetterProcessor.ProcessMessageAsync += ProcessDeadLetterAsync;
        _deadLetterProcessor.ProcessErrorAsync += ProcessErrorAsync;

        await _commandProcessor.StartProcessingAsync(stoppingToken);
        await _deadLetterProcessor.StartProcessingAsync(stoppingToken);
        logger.LogInformation(
            "Mission-command session processor and dead-letter processor started for queue '{QueueName}'.",
            options.MissionCommandQueueName);
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
        await ProcessCommandCoreAsync(
            args.Message,
            loadSessionAsync: loadCt => LoadSessionStateAsync(args, loadCt),
            saveSessionAsync: (s, saveCt) => SaveSessionStateAsync(args, s, saveCt),
            ct);
        await args.CompleteMessageAsync(args.Message, ct);
    }

    /// <summary>SDK-independent core: classify first, and only an addressable command ever touches
    /// session state or the processor. Internal + <c>InternalsVisibleTo</c> (Phase 43.16 Task 8c) so
    /// a test can pass a <paramref name="loadSessionAsync"/> that throws if ever invoked, proving
    /// structurally that unaddressable input short-circuits before any session-state touch — without
    /// needing an uninstantiable real <c>ProcessSessionMessageEventArgs</c>.</summary>
    internal async Task ProcessCommandCoreAsync(
        ServiceBusReceivedMessage message,
        Func<CancellationToken, Task<WorkerSessionState?>> loadSessionAsync,
        Func<WorkerSessionState, CancellationToken, Task> saveSessionAsync,
        CancellationToken ct)
    {
        var classification = ConversationCommandMessageClassifier.Classify(message);
        if (classification is UnaddressableCommand unaddressable)
        {
            logger.LogError(
                "Command message '{MessageId}' is unaddressable ({Category}); completing without action.",
                message.MessageId, unaddressable.Category);
            return;
        }

        var addressable = (AddressableCommand)classification;
        var session = await loadSessionAsync(ct);

        await _processor.ProcessAsync(
            addressable.Command, addressable.TenantId, session,
            saveSessionAsync: saveSessionAsync,
            publishAsync: (progress, tid, publishCt) => publisher.PublishAsync(progress, tid, publishCt),
            ct);
    }

    private async Task ProcessDeadLetterAsync(ProcessMessageEventArgs args)
    {
        var result = await _deadLetterHandler.HandleAsync(args.Message, args.CancellationToken);
        if (!result.WasAddressable)
            logger.LogError(
                "Command dead-letter message '{MessageId}' discarded as unaddressable: {Category}.",
                args.Message.MessageId, result.DiscardCategory);

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
