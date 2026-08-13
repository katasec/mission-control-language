using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage.Blobs;
using ForgeMission.ConversationHost.Persistence;
using Orleans.Configuration;

// ConversationHost — the future durable-conversation Silo/API (Phase 43.16). Task 4 is the
// composition root for Table/Blob persistence and Orleans ownership; Task 6 maps HTTP routes. No
// endpoint, queue, or other hosted service belongs here yet.
var builder = WebApplication.CreateSlimBuilder(args);

var storageOptions = builder.Configuration.GetSection("ConversationStorage").Get<ConversationStorageOptions>()
    ?? new ConversationStorageOptions();
// Fail startup if the selected credential path is incomplete — never silently fall back.
storageOptions.Validate();
builder.Services.AddSingleton(storageOptions);

var tableServiceClient = BuildTableServiceClient(storageOptions);
var blobServiceClient = BuildBlobServiceClient(storageOptions);
builder.Services.AddSingleton(tableServiceClient);
builder.Services.AddSingleton(blobServiceClient);
builder.Services.AddSingleton<IConversationEventStore, AzureTableConversationEventStore>();
builder.Services.AddSingleton<IConversationArtifactStore, AzureBlobConversationArtifactStore>();

builder.UseOrleans(siloBuilder =>
{
    siloBuilder.UseAzureStorageClustering(options => options.TableServiceClient = tableServiceClient);
    siloBuilder.Configure<ClusterOptions>(options =>
    {
        options.ClusterId = storageOptions.OrleansClusterId;
        options.ServiceId = storageOptions.OrleansServiceId;
    });
    // Physical Table names are alphanumeric-only (Azure Table rejects the hyphenated provider
    // names below) and distinct from both the event table and each other.
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
});

var app = builder.Build();
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
