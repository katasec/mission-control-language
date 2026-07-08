using ForgeMission.Runner;
using ForgeMission.Runner.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Source-gen JSON for the transport contract (clean + trim-safe); reflection fallback stays on for
// anything else the host serialises.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, RunContractsContext.Default));

// Load the baked-in missions once at boot (39.1 — no OCI/blob yet). MissionDir points at the
// missions/ copied into the image; the operator's provider keys are read from each mission's
// forge.toml via env(...) at load time (keys live here, not in the orchestrator).
var missionDir = builder.Configuration["MissionDir"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "missions");
missionDir = Path.GetFullPath(missionDir);

var registry = await RunnerRegistry.LoadAsync(
[
    ("ChatGPT",   "Raw LLM — no verification",                     Path.Combine(missionDir, "vanilla",              "mission.mcl")),
    ("Forge",     "LLM + deterministic verifier, retries on fail", Path.Combine(missionDir, "hallucination-guard", "mission.mcl")),
    ("Assistant", "General assistant, answers LLM-verified",       Path.Combine(missionDir, "assistant",            "mission.mcl")),
    ("Claude",    "Raw Claude — no verification",                  Path.Combine(missionDir, "claude",               "mission.mcl")),
    ("Grok",      "Raw Grok (xAI) — no verification",              Path.Combine(missionDir, "grok",                 "mission.mcl")),
]);

Console.Error.WriteLine(registry.All.Count == 0
    ? "Runner: no missions loaded (no provider keys set)."
    : $"Runner: loaded {registry.All.Count} mission(s): {string.Join(", ", registry.All.Select(m => m.Label))}.");

builder.Services.AddSingleton(registry);
builder.Services.AddSingleton<MissionRunHandler>();

var app = builder.Build();

// Liveness/readiness — ACA probes hit this on the warm runner.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// The orchestrator binds only handles whose mission is loadable here (e.g. provider key present).
app.MapGet("/missions", (RunnerRegistry reg) =>
    reg.All.Select(m => new MissionInfo(m.Label, m.Description)).ToList());

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

app.Run();
