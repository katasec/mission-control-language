using ForgeMission.ClientRuntime.Services;
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
        await using var dockerMissionRuntime = await StartDockerMissionRuntimeAsync(builder);
        var missionRuntimeBaseUrl = dockerMissionRuntime?.BaseUrl
            ?? builder.Configuration["MissionRuntime:BaseUrl"]
            ?? "http://127.0.0.1:8080/";
        builder.Services.AddScoped(_ => new WorkspaceState(initialWorkspaceRoot));
        builder.Services.AddSingleton<ClientRuntimeEventHub>();
        builder.Services.AddSingleton<ClientRuntimeSessionStore>();
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
            return url is null ? Results.StatusCode(StatusCodes.Status503ServiceUnavailable) : Results.Ok(new { url });
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

    private static async Task<DockerMissionRuntime?> StartDockerMissionRuntimeAsync(WebApplicationBuilder builder)
    {
        var mode = builder.Configuration["MissionRuntime:Mode"] ?? "docker";
        if (!mode.Equals("docker", StringComparison.OrdinalIgnoreCase))
            return null;

        return await DockerMissionRuntime.StartAsync(builder.Configuration);
    }
}
