using ForgeMission.Application;
using ForgeMission.Application.Transport;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ForgeMission.Application.Host;

public sealed record LocalApplicationHostOptions(string MissionUrl, string MissionMode, string Credential, string ConversationUrl, string ContentRoot);

// Reusable loopback composition. The executable Program remains a thin out-of-process entry point;
// a native root may instead own this instance and its deterministic shutdown.
public sealed class LocalApplicationHost : IAsyncDisposable
{
    private readonly WebApplication _app;
    private LocalApplicationHost(WebApplication app, string url) => (_app, Url) = (app, url);
    public string Url { get; }

    public static async Task<LocalApplicationHost> StartAsync(LocalApplicationHostOptions options, CancellationToken ct)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = options.ContentRoot, WebRootPath = Path.Combine(options.ContentRoot, "wwwroot") });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> {
            ["MissionRuntime:BaseUrl"] = options.MissionUrl, ["MissionRuntime:Mode"] = options.MissionMode,
            ["MissionRuntime:Credential"] = options.Credential, ["ConversationRuntime:BaseUrl"] = options.ConversationUrl });
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton<ApplicationEventHub>();
        builder.Services.ConfigureHttpJsonOptions(o => { o.SerializerOptions.TypeInfoResolverChain.Insert(0, ApplicationJsonContext.Default); o.SerializerOptions.TypeInfoResolverChain.Insert(1, ReadyResponseJsonContext.Default); });
        builder.Services.AddHttpClient("mission-runtime", c => { c.BaseAddress = new Uri(options.MissionUrl); c.DefaultRequestHeaders.Authorization = new("Bearer", options.Credential); });
        builder.Services.AddHttpClient("conversation-host", c => c.BaseAddress = new Uri(options.ConversationUrl));
        builder.Services.AddSingleton(sp => ApplicationComposition.Create(sp.GetRequiredService<IHttpClientFactory>(), options.MissionMode, new([], null), sp.GetRequiredService<ApplicationEventHub>().Publish, sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping));
        builder.Services.AddSingleton<IProjectService>(sp => sp.GetRequiredService<ApplicationComposition>().Projects);
        builder.Services.AddSingleton<IApplicationSessionService>(sp => sp.GetRequiredService<ApplicationComposition>().Sessions);
        builder.Services.AddSingleton<IMissionSubmissionService>(sp => sp.GetRequiredService<ApplicationComposition>().MissionSubmissions);
        builder.Services.AddSingleton<IRunHistoryService>(sp => sp.GetRequiredService<ApplicationComposition>().RunHistory);
        builder.Services.AddSingleton<IProjectContentService>(sp => sp.GetRequiredService<ApplicationComposition>().ProjectContent);
        builder.Services.AddSingleton<IConversationService>(sp => sp.GetRequiredService<ApplicationComposition>().Conversations);
        builder.Services.AddSingleton<ICapabilityActionService>(sp => sp.GetRequiredService<ApplicationComposition>().Capabilities);
        builder.Services.AddSingleton<IInteractionService>(sp => sp.GetRequiredService<ApplicationComposition>().Interactions);
        builder.Services.AddSingleton<IMissionHandsConversationService>(sp => sp.GetRequiredService<ApplicationComposition>().MissionHands);
        builder.Services.AddSingleton<IMissionConversationService>(sp => sp.GetRequiredService<ApplicationComposition>().MissionConversations);
        builder.Services.AddSingleton<IMissionAuthoringService>(sp => sp.GetRequiredService<ApplicationComposition>().MissionAuthoring);
        var app = builder.Build(); app.MapStaticAssets(); app.UseBlazorFrameworkFiles(); app.UseStaticFiles(); app.UseRouting(); app.MapApplicationTransport();
        app.MapGet("/ready", (IServer s) => s.Features.Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault() is { } url ? Results.Ok(new ReadyResponse(url)) : Results.StatusCode(503)); app.MapFallbackToFile("index.html");
        await app.StartAsync(ct); return new(app, app.Urls.Single());
    }
    public ValueTask DisposeAsync() => new(_app.DisposeAsync().AsTask());
}
