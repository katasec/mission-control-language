using Katasec.AnthropicServer;
using Katasec.OaiServer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ForgeMission.Serve;

// One WebApplication, every wire (Phase 42.4 task 1). Converges the two serving surfaces —
// Katasec.AnthropicServer (/v1/messages) and Katasec.OaiServer (/v1/chat/completions,
// /v1/responses, /v1/models) — onto a single app, so `forge serve`, `forge claude`, and the
// cloud runner image all host the SAME wire mapping over the same mission core. A door is
// enabled by passing its IChatClient; null leaves that wire unmapped.
//
// The routes are disjoint by construction: the Anthropic wire maps "/" (HEAD/GET probe) and
// /v1/messages; the OpenAI wire maps /v1/chat/completions, /v1/responses, and /v1/models.
public static class ForgeServe
{
    // Builds a self-contained slim app for the CLI hosts (`forge serve` / `forge claude`).
    // The runner host maps its own builder and calls MapWires directly.
    public static WebApplication BuildApp(
        string agentId,
        int port,
        IChatClient? anthropicDoor,
        IChatClient? openAiDoor,
        IChatClient? auxClient = null,
        ISessionStore? sessionStore = null)
    {
        if (anthropicDoor is null && openAiDoor is null)
            throw new ArgumentException("At least one wire door (anthropicDoor / openAiDoor) is required.");

        var builder = WebApplication.CreateSlimBuilder();

        // Wire handlers serialize with explicit source-gen contexts; this resolver only covers
        // stray Results-based responses. (OaiJsonContext is internal to its package — its
        // handlers never route through the framework serializer.)
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.TypeInfoResolverChain.Insert(0, AnthropicJsonContext.Default));

        // The caller prints its own startup banner and error messages.
        builder.Logging.AddFilter("Microsoft", LogLevel.None);
        builder.Logging.AddFilter("System", LogLevel.None);

        builder.WebHost.UseSetting("urls", $"http://0.0.0.0:{port}");

        var app = builder.Build();
        MapWires(app, agentId, anthropicDoor, openAiDoor, auxClient, sessionStore);
        MapHealth(app);
        return app;
    }

    // Maps the enabled wire doors onto an existing app (the runner calls this on its own host).
    public static void MapWires(
        WebApplication app,
        string agentId,
        IChatClient? anthropicDoor,
        IChatClient? openAiDoor,
        IChatClient? auxClient = null,
        ISessionStore? sessionStore = null)
    {
        if (anthropicDoor is not null)
            new AnthropicServer(anthropicDoor, agentId, auxClient).Map(app);

        if (openAiDoor is not null)
            new OaiServer(openAiDoor, sessionStore ?? new LocalFileSessionStore(), agentId).Map(app);
    }

    // Same probe shape the cloud runner serves — keeps local Docker ≡ ACA scheduling honest.
    private static void MapHealth(WebApplication app) =>
        app.MapGet("/health", (RequestDelegate)(ctx =>
        {
            ctx.Response.ContentType = "application/json";
            return ctx.Response.WriteAsync("""{"status":"ok"}""", ctx.RequestAborted);
        }));
}
