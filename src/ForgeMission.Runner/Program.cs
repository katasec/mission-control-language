using ForgeMission.Runner;
using ForgeMission.Runner.Contracts;

var builder = WebApplication.CreateBuilder(args);

// Source-gen JSON for the transport contract (clean + trim-safe); reflection fallback stays on for
// anything else the host serialises.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.TypeInfoResolverChain.Insert(0, RunContractsContext.Default));

// Built-in missions are PULLED from the trusted Forge registry by pinned digest (39.4), not loaded
// from the image — the uniform "everything is pulled" path. MissionDir is the baked-in copy, kept
// as a resilience fallback if a pull fails. The operator's provider keys are still read from each
// mission's forge.toml via env(...) at load time (keys live here, not in the orchestrator).
var missionDir = builder.Configuration["MissionDir"]
    ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "missions");
missionDir = Path.GetFullPath(missionDir);

var specs    = await BuiltinMissions.ResolveAsync(missionDir);
var registry = await RunnerRegistry.LoadAsync(specs);

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
