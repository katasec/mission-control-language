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
        var initialWorkspaceRoot = builder.Configuration["Workspace:InitialRoot"];
        var missionRuntimeBaseUrl = builder.Configuration["MissionRuntime:BaseUrl"]
            ?? "http://127.0.0.1:8080/";
        builder.Services.AddScoped(_ => new WorkspaceState(initialWorkspaceRoot));
        builder.Services.AddSingleton<ClientRuntimeEventHub>();
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
        });
        builder.Services.AddHttpClient<MissionRuntimeSession>(client =>
        {
            client.BaseAddress = new Uri(missionRuntimeBaseUrl, UriKind.Absolute);
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
