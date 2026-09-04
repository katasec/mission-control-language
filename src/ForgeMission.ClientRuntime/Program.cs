using ForgeMission.ClientRuntime.Services;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.ClientRuntime.TransportHost;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ForgeMission.ClientRuntime;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
            WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
        });
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var missionRuntimeBaseUrl = builder.Configuration["MissionRuntime:BaseUrl"]
            ?? throw new InvalidOperationException(
                "MissionRuntime:BaseUrl is required (set via MissionRuntime__BaseUrl).");
        var missionRuntimeCredential = builder.Configuration["MissionRuntime:Credential"]
            ?? throw new InvalidOperationException(
                "MissionRuntime:Credential is required (set via MissionRuntime__Credential).");
        builder.Services.AddSingleton<ClientRuntimeEventHub>();
        // No projects root is configured: it is <user-profile>/Forge/Projects by fixed convention,
        // and nothing here is touched until a surface actually creates or opens a Project.
        builder.Services.AddSingleton(new ProjectStore());
        // Reads the open Project's manifest and lock file for the Explorer (43.20 task 3). It is
        // stateless and holds no cache, so a projection is always of the Project as it is now.
        builder.Services.AddSingleton<ProjectWorkbenchProjector>();
        builder.Services.AddSingleton<ClientRuntimeSessionStore>();
        // Native AOT has no reflection fallback for minimal-API request/response JSON binding —
        // route it through the same source-generated context the transport channel already uses.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ClientRuntimeJsonContext.Default);
            options.SerializerOptions.TypeInfoResolverChain.Insert(1, ReadyResponseJsonContext.Default);
        });
        builder.Services.AddHttpClient("mission-runtime", client =>
        {
            client.BaseAddress = new Uri(missionRuntimeBaseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", missionRuntimeCredential);
        });
        // Optional and unvalidated at startup — unlike MissionRuntime:BaseUrl, a normal Mission
        // session must not require it. A durable Janus session's first prompt fails loudly
        // through the usual HttpRequestException path if this was never configured.
        var conversationRuntimeBaseUrl = builder.Configuration["ConversationRuntime:BaseUrl"];
        builder.Services.AddHttpClient("conversation-host", client =>
        {
            if (!string.IsNullOrWhiteSpace(conversationRuntimeBaseUrl))
                client.BaseAddress = new Uri(conversationRuntimeBaseUrl, UriKind.Absolute);
        });
        var app = builder.Build();
        app.MapStaticAssets();
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.MapClientRuntimeTransport();
        app.MapGet("/ready", (IServer server) =>
        {
            var url = server.Features.Get<IServerAddressesFeature>()?.Addresses.SingleOrDefault();
            return url is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : Results.Ok(new ReadyResponse(url));
        });

        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var url = app.Urls.SingleOrDefault();
            if (url is not null)
                Console.WriteLine($"FORGE_CLIENT_RUNTIME_URL={url}");
        });

        app.MapFallbackToFile("index.html");
        await app.RunAsync();
    }
}
