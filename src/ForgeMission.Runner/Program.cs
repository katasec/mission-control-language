using System.Text.Json;
using ForgeMission.Cli;
using ForgeMission.Runner;
using ForgeMission.Runner.Contracts;
using ForgeMission.Serve;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Source-gen JSON for the transport contract (clean + trim-safe); reflection fallback stays on for
// anything else the host serialises.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, RunContractsContext.Default));

// OpenTelemetry tracing. Captures, per run: the mission-level span (mission ref, provider, model),
// the gen_ai.* span from the instrumented IChatClient (model + token usage), and the OUTBOUND HTTP
// span whose `server.address` is the actual provider endpoint (api.openai.com vs api.x.ai) — the
// ground truth for diagnosing a mis-routed @-agent. Credential safety: HTTP instrumentation does NOT
// record request headers (so no Authorization / x-api-key), and the chat-client instrumentation
// leaves EnableSensitiveData off (see MissionRunHandler), so prompts/answers/keys never enter a span.
// Console exporter is always on for local visibility; the OTLP exporter self-configures from
// OTEL_EXPORTER_OTLP_ENDPOINT when set (prod), and is a no-op otherwise.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("forge-runner"))
    .WithTracing(t =>
    {
        t.AddSource(RunnerTelemetry.SourceName)   // mission spans + gen_ai.* (same sourceName)
         .AddAspNetCoreInstrumentation()          // inbound /run
         .AddHttpClientInstrumentation()          // outbound provider call → server.address
         .AddConsoleExporter();
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            t.AddOtlpExporter();
    });

// MissionRef is the normal component boundary: the runner owns OCI pull/cache/load and the
// client supplies only a pinned reference. MissionFile remains for the legacy CLI container path.
var specs = await RunnerMissionSource.ResolveAsync(builder.Configuration);

var registry = await RunnerRegistry.LoadAsync(specs);

Console.Error.WriteLine(registry.All.Count == 0
    ? "Runner: no missions loaded (no provider keys set)."
    : $"Runner: loaded {registry.All.Count} mission(s): {string.Join(", ", registry.All.Select(m => m.Label))}.");

builder.Services.AddSingleton(registry);
builder.Services.AddSingleton<MissionRunHandler>();
builder.Services.AddSingleton<IRunnerArtifactStore, RunnerArtifactStore>();

var app = builder.Build();

// Liveness/readiness — ACA probes hit this on the warm runner.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// The /v1 wire doors (42.4 task 2): the SAME wire mapping `forge serve` hosts locally, over the
// same mission registry the /run contract executes — this image IS the one artifact. The wire's
// model field routes to a mission by label; a single-mission registry answers regardless (the
// `forge claude --container` case). Unmetered until 42.6 wraps the hosting layer.
ForgeServe.MapWires(app, "forge-runner",
    anthropicDoor: new MissionDoorClient(registry, fullConversation: true),
    openAiDoor:    new MissionDoorClient(registry, fullConversation: false));

// The orchestrator binds only handles whose mission is loadable here (e.g. provider key present).
app.MapGet("/missions", (RunnerRegistry reg) =>
    reg.All.Select(m => new MissionInfo(m.Label, m.Description, m.ArtifactCapabilities)).ToList());

// Internal raw-byte artifact scratch. ForgeAPI copies public uploads here immediately before a run,
// and copies produced outputs back afterward. Bytes stay off the JSON run contract.
app.MapPost("/artifacts/upload", async (HttpContext ctx, IRunnerArtifactStore artifacts) =>
{
    try
    {
        var artifact = await artifacts.SaveAsync(
            new RunArtifactWriteRequest(
                Name: Header(ctx, "X-Forge-Artifact-Name"),
                ContentType: ctx.Request.ContentType ?? "application/octet-stream",
                Sha256: Header(ctx, "X-Forge-Artifact-Sha256"),
                Role: Header(ctx, "X-Forge-Artifact-Role"),
                DeclaredSize: ctx.Request.ContentLength),
            ctx.Request.Body,
            ctx.RequestAborted);
        return Results.Json(artifact, RunContractsContext.Default.RunArtifact);
    }
    catch (ArtifactTooLargeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/artifacts/{id}", async (string id, HttpContext ctx, IRunnerArtifactStore artifacts) =>
{
    await using var read = await artifacts.OpenAsync(id, ctx.RequestAborted);
    if (read is null) return Results.NotFound();

    ctx.Response.ContentType = read.Artifact.ContentType;
    ctx.Response.ContentLength = read.Artifact.Size;
    ctx.Response.Headers["X-Forge-Artifact-Name"] = read.Artifact.Name;
    ctx.Response.Headers["X-Forge-Artifact-Sha256"] = read.Artifact.Sha256;
    await read.Content.CopyToAsync(ctx.Response.Body, ctx.RequestAborted);
    return Results.Empty;
});

app.MapDelete("/artifacts/{id}", async (string id, HttpContext ctx, IRunnerArtifactStore artifacts) =>
{
    await artifacts.DeleteAsync(id, ctx.RequestAborted);
    return Results.NoContent();
});

// The one hot path: run a mission, return result + trace + cost signals.
app.MapPost("/run", async (RunRequest request, MissionRunHandler handler, CancellationToken ct) =>
{
    try
    {
        var response = await handler.RunAsync(request, ct);
        return response is null
            ? Results.NotFound(new { error = $"Unknown mission '{request.MissionRef}'." })
            : Results.Ok(response);
    }
    catch (ArgumentException ex) // e.g. an unknown run policy — a malformed caller request
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

// The interactive hot path (41.7): stream progress → result as NDJSON so the room shows live
// progress and continuous bytes defeat idle timeouts. Each line is one RunStreamEvent, flushed
// immediately. HTTP 200 is committed up front; a run-level failure arrives as a terminal `error`
// event rather than a status code (the stream has already begun).
app.MapPost("/run/stream", async (RunRequest request, MissionRunHandler handler, HttpContext ctx) =>
{
    ctx.Response.ContentType = "application/x-ndjson";
    var newline = "\n"u8.ToArray();

    await foreach (var evt in handler.RunStreamAsync(request, ctx.RequestAborted))
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(evt, RunContractsContext.Default.RunStreamEvent);
        await ctx.Response.Body.WriteAsync(json, ctx.RequestAborted);
        await ctx.Response.Body.WriteAsync(newline, ctx.RequestAborted);
        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);   // push each event as it happens
    }
});

app.Run();

static string Header(HttpContext ctx, string name) =>
    ctx.Request.Headers.TryGetValue(name, out var value) ? value.ToString() : "";
