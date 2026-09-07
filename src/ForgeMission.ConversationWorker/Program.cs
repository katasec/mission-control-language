using Azure.Identity;
using Azure.Messaging.ServiceBus;
using ForgeMission.ConversationWorker.Messaging;
using ForgeMission.Core.Runtime;
using Microsoft.Extensions.Hosting;

// ConversationWorker runs admitted immutable generic packages through one deployment-owned default
// runner. It never references ConversationHost, Orleans, Azure Storage, Client Runtime,
// Presentation, Rooms, a Desktop workspace, or a capability executor.
var builder = Host.CreateApplicationBuilder(args);

var serviceBusOptions = builder.Configuration.GetSection("ConversationServiceBus").Get<ConversationServiceBusOptions>()
    ?? new ConversationServiceBusOptions();
// Worker constructs only the mission-command Listen and conversation-progress Send directions —
// the opposite two directions are Host's alone.
serviceBusOptions.ValidateDirection(serviceBusOptions.MissionCommandListenConnectionString, "MissionCommandListen");
serviceBusOptions.ValidateDirection(serviceBusOptions.ProgressSendConnectionString, "ProgressSend");
builder.Services.AddSingleton(serviceBusOptions);

var providerOptions = new WorkerProviderOptions
{
    DefaultProvider = builder.Configuration["ConversationWorker:DefaultProvider"],
    DefaultModel = builder.Configuration["ConversationWorker:DefaultModel"],
    DefaultEndpoint = builder.Configuration["ConversationWorker:DefaultEndpoint"],
    DefaultApiKey = builder.Configuration["ConversationWorker:DefaultApiKey"],
};
builder.Services.AddSingleton(providerOptions);
builder.Services.AddSingleton<IExpertRunner>(providerOptions.BuildDefaultRunner());

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
