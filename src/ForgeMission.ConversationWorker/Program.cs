using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationWorker.Janus;
using ForgeMission.ConversationWorker.Messaging;
using Microsoft.Extensions.Hosting;

// ConversationWorker — the Janus mission executor half of Phase 43.16 Task 5. Listens the
// mission-command queue, runs the checked-in read-only Janus mission via PipelineRunner, and sends
// conversation-progress facts back to ConversationHost. Never references ConversationHost, Orleans,
// Azure Storage, Client Runtime, Presentation, Rooms, a Desktop workspace, or a capability executor.
var builder = Host.CreateApplicationBuilder(args);

var serviceBusOptions = builder.Configuration.GetSection("ConversationServiceBus").Get<ConversationServiceBusOptions>()
    ?? new ConversationServiceBusOptions();
// Worker constructs only the mission-command Listen and conversation-progress Send directions —
// the opposite two directions are Host's alone.
serviceBusOptions.ValidateDirection(serviceBusOptions.MissionCommandListenConnectionString, "MissionCommandListen");
serviceBusOptions.ValidateDirection(serviceBusOptions.ProgressSendConnectionString, "ProgressSend");
builder.Services.AddSingleton(serviceBusOptions);

// Both packaged missions are loaded once at startup and reached only through the named resolver —
// a Worker executes what is baked into its image, never a directory a command names (43.20 task 2).
var janusDirectory = builder.Configuration["ConversationWorker:JanusMissionDirectory"]
    ?? throw new InvalidOperationException("ConversationWorker:JanusMissionDirectory is required.");
var naiveDirectory = builder.Configuration["ConversationWorker:NaiveMissionDirectory"]
    ?? throw new InvalidOperationException("ConversationWorker:NaiveMissionDirectory is required.");
builder.Services.AddSingleton(new WorkerMissionResolver(
    WorkerMissionLoader.Load(janusDirectory), WorkerMissionLoader.Load(naiveDirectory)));

var commandListenClient = BuildServiceBusClient(
    serviceBusOptions.MissionCommandListenConnectionString, serviceBusOptions.FullyQualifiedNamespace);
builder.Services.AddSingleton(commandListenClient);

var progressSendClient = BuildServiceBusClient(
    serviceBusOptions.ProgressSendConnectionString, serviceBusOptions.FullyQualifiedNamespace);
var progressSender = progressSendClient.CreateSender(serviceBusOptions.ProgressQueueName);
builder.Services.AddSingleton(progressSender);
builder.Services.AddSingleton<IConversationProgressPublisher, AzureServiceBusConversationProgressPublisher>();

builder.Services.AddHostedService<AzureServiceBusMissionCommandConsumer>();

var host = builder.Build();
host.Run();

// Selects exactly one credential path per direction, mirroring ConversationHost's Program.cs: a
// scoped connection string (Kind/Azurite), otherwise FullyQualifiedNamespace with
// DefaultAzureCredential (production managed identity). ValidateDirection above already guarantees
// whichever path is selected here is complete.
static ServiceBusClient BuildServiceBusClient(string? scopedConnectionString, string? fullyQualifiedNamespace)
    => !string.IsNullOrWhiteSpace(scopedConnectionString)
        ? new ServiceBusClient(scopedConnectionString)
        : new ServiceBusClient(fullyQualifiedNamespace!, new DefaultAzureCredential());
