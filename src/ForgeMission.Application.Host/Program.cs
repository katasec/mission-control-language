using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Application.Host;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace ForgeMission.Application.Host;

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
        builder.Services.AddSingleton<ApplicationEventHub>();
        // Native AOT has no reflection fallback for minimal-API request/response JSON binding —
        // route it through the same source-generated context the transport channel already uses.
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, ApplicationJsonContext.Default);
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
        var policy = BuildPolicy(builder.Configuration);
        builder.Services.AddSingleton(sp => ApplicationComposition.Create(
            sp.GetRequiredService<IHttpClientFactory>(),
            builder.Configuration["MissionRuntime:Mode"], policy,
            sp.GetRequiredService<ApplicationEventHub>().Publish,
            sp.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping));
        builder.Services.AddSingleton<IProjectService>(sp => sp.GetRequiredService<ApplicationComposition>().Projects);
        builder.Services.AddSingleton<IApplicationSessionService>(sp => sp.GetRequiredService<ApplicationComposition>().Sessions);
        builder.Services.AddSingleton<IMissionSubmissionService>(sp => sp.GetRequiredService<ApplicationComposition>().MissionSubmissions);
        builder.Services.AddSingleton<IRunHistoryService>(sp => sp.GetRequiredService<ApplicationComposition>().RunHistory);
        builder.Services.AddSingleton<IProjectContentService>(sp => sp.GetRequiredService<ApplicationComposition>().ProjectContent);
        builder.Services.AddSingleton<IConversationService>(sp => sp.GetRequiredService<ApplicationComposition>().Conversations);
        builder.Services.AddSingleton<ICapabilityActionService>(sp => sp.GetRequiredService<ApplicationComposition>().Capabilities);
        builder.Services.AddSingleton<IInteractionService>(sp => sp.GetRequiredService<ApplicationComposition>().Interactions);
        builder.Services.AddSingleton<IMissionHandsConversationService>(sp => sp.GetRequiredService<ApplicationComposition>().MissionHands);
        var app = builder.Build();
        app.MapStaticAssets();
        app.UseBlazorFrameworkFiles();
        app.UseStaticFiles();
        app.UseRouting();
        app.MapApplicationTransport();
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

    private static ForgeMission.Core.Tools.CapabilityAuthorizationPolicy BuildPolicy(IConfiguration configuration)
    {
        var terminal = ParseOutcome(configuration["Authorization:TerminalOutcome"]);
        var file = ParseOutcome(configuration["Authorization:FileOutcome"]);
        return new([
            new KeyValuePair<string, ForgeMission.Core.Tools.CapabilityAuthorizationRule>("file", new(file)),
            new KeyValuePair<string, ForgeMission.Core.Tools.CapabilityAuthorizationRule>("terminal", new(terminal)),
        ]);
    }

    private static ForgeMission.Core.Tools.AuthorizationOutcome ParseOutcome(string? value) =>
        Enum.TryParse<ForgeMission.Core.Tools.AuthorizationOutcome>(value, true, out var outcome)
            ? outcome : ForgeMission.Core.Tools.AuthorizationOutcome.AutoApproved;
}
