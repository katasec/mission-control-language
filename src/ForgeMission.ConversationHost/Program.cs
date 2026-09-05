using Azure.Data.Tables;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using ForgeMission.ConversationHost.Api;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Orleans.Configuration;

// ConversationHost — the durable-conversation Silo/API (Phase 43.16). Task 4 is the composition
// root for Table/Blob persistence and Orleans ownership; Task 5 adds the mission-command outbox
// (Service Bus send + a durable reminder retry driver) and the conversation-progress
// consumer/dead-letter consumer; Task 6 maps the additive Forge-native HTTP/SSE routes.
var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, ConversationContractsJsonContext.Default));

var storageOptions = builder.Configuration.GetSection("ConversationStorage").Get<ConversationStorageOptions>()
    ?? new ConversationStorageOptions();
// Fail startup if the selected credential path is incomplete — never silently fall back.
storageOptions.Validate();
builder.Services.AddSingleton(storageOptions);

var serviceBusOptions = builder.Configuration.GetSection("ConversationServiceBus").Get<ConversationServiceBusOptions>()
    ?? new ConversationServiceBusOptions();
// Host constructs only the mission-command Send and conversation-progress Listen directions —
// the opposite two directions are Worker's alone.
serviceBusOptions.ValidateDirection(serviceBusOptions.MissionCommandSendConnectionString, "MissionCommandSend");
serviceBusOptions.ValidateDirection(serviceBusOptions.ProgressListenConnectionString, "ProgressListen");
builder.Services.AddSingleton(serviceBusOptions);

var tableServiceClient = BuildTableServiceClient(storageOptions);
var blobServiceClient = BuildBlobServiceClient(storageOptions);
builder.Services.AddSingleton(tableServiceClient);
builder.Services.AddSingleton(blobServiceClient);
builder.Services.AddSingleton<IConversationEventStore, AzureTableConversationEventStore>();
builder.Services.AddSingleton<IProjectRunIndexStore, AzureTableProjectRunIndexStore>();
builder.Services.AddSingleton<IConversationArtifactStore, AzureBlobConversationArtifactStore>();

var commandSendClient = BuildServiceBusClient(
    serviceBusOptions.MissionCommandSendConnectionString, serviceBusOptions.FullyQualifiedNamespace);
var commandSender = commandSendClient.CreateSender(serviceBusOptions.MissionCommandQueueName);
builder.Services.AddSingleton(commandSender);
builder.Services.AddSingleton<IConversationCommandDispatcher, AzureServiceBusConversationCommandDispatcher>();

var progressListenClient = BuildServiceBusClient(
    serviceBusOptions.ProgressListenConnectionString, serviceBusOptions.FullyQualifiedNamespace);
builder.Services.AddSingleton(progressListenClient);
builder.Services.AddSingleton<ConversationProgressHandler>();
builder.Services.AddSingleton<ConversationProgressDeadLetterHandler>();
builder.Services.AddHostedService<ConversationProgressConsumer>();
builder.Services.AddHostedService<ConversationProgressDeadLetterConsumer>();

builder.Services.AddSingleton<IConversationEventNotifier, ConversationEventHub>();
builder.Services.AddSingleton<ConversationSseWriter>();

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseAzureStorageClustering(options => options.TableServiceClient = tableServiceClient);
    siloBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = storageOptions.OrleansClusterId;
        options.ServiceId = storageOptions.OrleansServiceId;
    });
    // Physical Table names are alphanumeric-only (Azure Table rejects the hyphenated provider
    // names below) and distinct from both the event table, each other, and the reminder table.
    siloBuilder.AddAzureTableGrainStorage("conversation-checkpoint", options =>
    {
        options.TableServiceClient = tableServiceClient;
        options.TableName = "OrleansConversationCheckpoints";
    });
    siloBuilder.AddAzureTableGrainStorage("mission-run-checkpoint", options =>
    {
        options.TableServiceClient = tableServiceClient;
        options.TableName = "OrleansMissionRunCheckpoints";
    });
    siloBuilder.UseAzureTableReminderService(options =>
    {
        options.TableServiceClient = tableServiceClient;
        options.TableName = "OrleansConversationReminders";
    });
});

var app = builder.Build();

app.MapConversationApi();
app.MapGet("/health", () => Results.Ok());

app.Run();

// Selects exactly one credential path: a non-empty ConnectionString (Kind/Azurite), otherwise the
// endpoint with DefaultAzureCredential (production managed identity). storageOptions.Validate()
// above already guarantees whichever path is selected here is complete.
static TableServiceClient BuildTableServiceClient(ConversationStorageOptions options)
    => !string.IsNullOrWhiteSpace(options.ConnectionString)
        ? new TableServiceClient(options.ConnectionString)
        : new TableServiceClient(new Uri(options.TableEndpoint!), new DefaultAzureCredential());

static BlobServiceClient BuildBlobServiceClient(ConversationStorageOptions options)
    => !string.IsNullOrWhiteSpace(options.ConnectionString)
        ? new BlobServiceClient(options.ConnectionString)
        : new BlobServiceClient(new Uri(options.BlobEndpoint!), new DefaultAzureCredential());

// Selects exactly one credential path per direction, mirroring the Table/Blob clients above:
// a scoped connection string (Kind/Azurite), otherwise FullyQualifiedNamespace with
// DefaultAzureCredential (production managed identity). ValidateDirection above already
// guarantees whichever path is selected here is complete.
static ServiceBusClient BuildServiceBusClient(string? scopedConnectionString, string? fullyQualifiedNamespace)
    => !string.IsNullOrWhiteSpace(scopedConnectionString)
        ? new ServiceBusClient(scopedConnectionString)
        : new ServiceBusClient(fullyQualifiedNamespace!, new DefaultAzureCredential());
