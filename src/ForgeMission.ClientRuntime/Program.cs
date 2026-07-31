using ForgeMission.ClientRuntime.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.FileProviders;

namespace ForgeMission.ClientRuntime;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.UseStaticWebAssets();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        var initialWorkspaceRoot = builder.Configuration["Workspace:InitialRoot"];
        await using var dockerMissionRuntime = await StartDockerMissionRuntimeAsync(builder);
        var missionRuntimeBaseUrl = dockerMissionRuntime?.BaseUrl
            ?? builder.Configuration["MissionRuntime:BaseUrl"]
            ?? "http://127.0.0.1:8080/";
        builder.Services.AddScoped(_ => new WorkspaceState(initialWorkspaceRoot));
        builder.Services.AddHttpClient<MissionRuntimeSession>(client =>
        {
            client.BaseAddress = new Uri(missionRuntimeBaseUrl, UriKind.Absolute);
        });

        var app = builder.Build();
        var forgeUiWebRoot = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "ForgeUI", "wwwroot"));
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(forgeUiWebRoot),
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
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
