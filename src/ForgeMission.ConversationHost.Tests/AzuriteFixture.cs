using Azure.Data.Tables;
using Azure.Storage.Blobs;
using ForgeMission.ConversationHost.Api;
using ForgeMission.ConversationHost.Grains;
using ForgeMission.ConversationHost.Messaging;
using ForgeMission.ConversationHost.Persistence;
using ForgeMission.Conversations.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orleans;
using Orleans.Configuration;
using Testcontainers.Azurite;

namespace ForgeMission.ConversationHost.Tests;

/// <summary>
/// One throwaway Azurite container (Testcontainers.Azurite), shared across every test in the
/// "Azurite" collection so Docker only starts once. Creates the event table + artifact container
/// once; each test uses unique tenant/conversation/run IDs and builds its own fresh in-process
/// Host/Silo(s) against this same endpoint via <see cref="StartHostAsync"/> to prove
/// reactivation/replay rather than reading memory. A missing Docker daemon fails
/// <see cref="InitializeAsync"/> explicitly — this is a required integration-environment error,
/// never a silent skip.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    // This sandbox's clock is set in 2026, so the installed Azure.Storage.Blobs 12.29.1 SDK sends
    // an API version string (2026-06-06) newer than any real Azurite build can support yet — no
    // image tag fixes that. --skipApiVersionCheck is Azurite's own documented workaround
    // (confirmed live: Azurite's error message names this exact flag). 3.36.0 is a fixed tag —
    // confirmed its digest is identical to what "latest" resolved to when this was tested.
    private readonly AzuriteContainer _container =
        new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.36.0")
            .WithCommand("--skipApiVersionCheck")
            .Build();

    public string ConnectionString { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        await new TableServiceClient(ConnectionString)
            .GetTableClient("forgeconversationevents")
            .CreateIfNotExistsAsync();
        await new BlobServiceClient(ConnectionString)
            .GetBlobContainerClient("forgeconversationartifacts")
            .CreateIfNotExistsAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>
    /// Builds and starts a fresh in-process Host/Silo against this fixture's Azurite endpoint,
    /// wired the same way Program.cs's composition root is. Uses ephemeral (port 0) Silo/Gateway
    /// endpoints — production Program.cs keeps Orleans' standard fixed ports — so more than one
    /// Silo can coexist in this one test process without colliding with each other or with a
    /// parallel test's ports.
    /// </summary>
    /// <param name="decorateEventStore">Optional wrapper around the real event store — used only
    /// to simulate a crash mid-append for the pending-transition repair test. Null in every normal
    /// call.</param>
    /// <param name="decorateGrainFactory">Optional wrapper around Orleans' own grain factory —
    /// used only to simulate a crash at the MissionRunGrain-notification stage (after the event
    /// Table append already succeeded). Null in every normal call; this test-only seam never
    /// touches Program.cs or any production type.</param>
    public async Task<ConversationHostInstance> StartHostAsync(
        Func<IConversationEventStore, IConversationEventStore>? decorateEventStore = null,
        Func<IGrainFactory, IGrainFactory>? decorateGrainFactory = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        // Loopback, OS-assigned port — mirrors the Silo/Gateway "reserve a real free port" approach
        // below (Kestrel, unlike Orleans' EndpointOptions, accepts ":0" directly) so more than one
        // Host can coexist in this one test process without colliding.
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ConversationContractsJsonContext.Default));

        var storageOptions = new ConversationStorageOptions { ConnectionString = ConnectionString };
        storageOptions.Validate();
        builder.Services.AddSingleton(storageOptions);

        var tableServiceClient = new TableServiceClient(ConnectionString);
        var blobServiceClient = new BlobServiceClient(ConnectionString);
        builder.Services.AddSingleton(tableServiceClient);
        builder.Services.AddSingleton(blobServiceClient);
        builder.Services.AddSingleton<IConversationEventStore>(sp =>
        {
            IConversationEventStore real = new AzureTableConversationEventStore(
                sp.GetRequiredService<TableServiceClient>(), sp.GetRequiredService<ConversationStorageOptions>());
            return decorateEventStore is null ? real : decorateEventStore(real);
        });
        builder.Services.AddSingleton<IProjectRunIndexStore, AzureTableProjectRunIndexStore>();
        builder.Services.AddSingleton<IConversationArtifactStore, AzureBlobConversationArtifactStore>();

        // The small in-memory dispatcher seam the Task 5 spoke calls for in place of a real Azure
        // Service Bus emulator — exposed to tests via ConversationHostInstance.Dispatcher.
        builder.Services.AddSingleton<FakeConversationCommandDispatcher>();
        builder.Services.AddSingleton<IConversationCommandDispatcher>(
            sp => sp.GetRequiredService<FakeConversationCommandDispatcher>());

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
            siloBuilder.Configure<EndpointOptions>(options =>
            {
                // Orleans rejects SiloPort/GatewayPort = 0 ("no listening port specified") —
                // unlike a raw TCP socket, it does not treat 0 as "let the OS assign one".
                // Reserve two actual free ports ourselves so more than one Silo can coexist in
                // this one test process without colliding with Orleans' fixed default ports.
                options.SiloPort = GetFreeTcpPort();
                options.GatewayPort = GetFreeTcpPort();
                options.AdvertisedIPAddress = System.Net.IPAddress.Loopback;
            });
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

        if (decorateGrainFactory is not null)
        {
            // Manual DI decoration (no Scrutor dependency): Orleans' own UseOrleans(...) call
            // above already registered the real IGrainFactory singleton. Replace that
            // registration with one that resolves the original factory the exact same way, then
            // wraps it — a standard, test-only technique; nothing here reaches into Orleans
            // internals or changes what production Program.cs registers.
            var original = builder.Services.Single(d => d.ServiceType == typeof(IGrainFactory));
            builder.Services.Remove(original);
            builder.Services.AddSingleton<IGrainFactory>(sp =>
            {
                var real = original.ImplementationFactory is not null
                    ? (IGrainFactory)original.ImplementationFactory(sp)
                    : original.ImplementationInstance is not null
                        ? (IGrainFactory)original.ImplementationInstance
                        : (IGrainFactory)ActivatorUtilities.CreateInstance(sp, original.ImplementationType!);
                return decorateGrainFactory(real);
            });
        }

        var app = builder.Build();
        app.MapConversationApi();
        app.MapGet("/health", () => Results.Ok());

        await app.StartAsync();

        var boundAddress = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();

        return new ConversationHostInstance(app, new Uri(boundAddress));
    }

    private static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

/// <summary>A running in-process Host/Silo built by <see cref="AzuriteFixture.StartHostAsync"/>.
/// Disposing stops the Silo (simulating a Host restart) without touching Azurite itself.</summary>
/// <param name="app">The started application.</param>
/// <param name="baseAddress">The real, loopback, Kestrel-bound URI this instance's HTTP/SSE routes
/// are reachable on — tests use a real <see cref="HttpClient"/> against it rather than invoking
/// endpoint delegates.</param>
public sealed class ConversationHostInstance(WebApplication app, Uri baseAddress) : IAsyncDisposable
{
    public Uri BaseAddress { get; } = baseAddress;

    /// <summary>Exposed so tests can call <c>ConversationApiEndpoints.Handle*Async</c> message
    /// handlers directly, proving they work with no HTTP meaning at all.</summary>
    public IGrainFactory GrainFactory => app.Services.GetRequiredService<IGrainFactory>();

    public IConversationGrain GetConversationGrain(ConversationAddress address)
        => app.Services.GetRequiredService<IGrainFactory>().GetGrain<IConversationGrain>(address.PartitionKey);

    public IMissionRunGrain GetMissionRunGrain(string tenantId, Guid runId)
        => app.Services.GetRequiredService<IGrainFactory>().GetGrain<IMissionRunGrain>($"{tenantId}|{runId:N}");

    public IConversationEventStore EventStore => app.Services.GetRequiredService<IConversationEventStore>();

    public IProjectRunIndexStore ProjectRunIndexStore => app.Services.GetRequiredService<IProjectRunIndexStore>();

    public IConversationArtifactStore ArtifactStore => app.Services.GetRequiredService<IConversationArtifactStore>();

    public FakeConversationCommandDispatcher Dispatcher => app.Services.GetRequiredService<FakeConversationCommandDispatcher>();

    public ValueTask DisposeAsync() => new(app.StopAsync());
}

[CollectionDefinition("Azurite")]
public sealed class AzuriteCollection : ICollectionFixture<AzuriteFixture>;
